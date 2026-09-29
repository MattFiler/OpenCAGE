using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenCAGE
{
    public sealed class SettingsChangedEventArgs : EventArgs
    {
        public SettingsChangedEventArgs(IReadOnlyList<string> changedKeys, bool externalChange)
        {
            ChangedKeys = changedKeys;
            ExternalChange = externalChange;
        }

        public IReadOnlyList<string> ChangedKeys { get; }
        public bool ExternalChange { get; }

        public static bool ContainsKey(IReadOnlyList<string> changedKeys, string key)
        {
            if (changedKeys == null || key == null)
                return false;

            foreach (string changedKey in changedKeys)
            {
                if (changedKey == key)
                    return true;
            }

            return false;
        }
    }

    static class SettingsManager
    {
        static readonly object _lock = new object();
        static JObject _jsonConfig = null;
        //Absolute from the start: file dialogs move the process's current directory, and a path resolved
        //against it later would quietly send every read and write to whatever folder was last browsed
        static string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpenCAGE Settings.json");
        static readonly HashSet<string> _dirtyKeys = new HashSet<string>();
        static readonly HashSet<string> _removedKeys = new HashSet<string>();
        static bool _suppressExternalReload = false;
        static FileSystemWatcher _watcher;
        static Timer _reloadDebounceTimer;
        static Timer _saveRetryTimer;
        static string ConfigFullPath => Path.GetFullPath(_configPath);

        public static event EventHandler<SettingsChangedEventArgs> SettingsChanged;

        static SettingsManager()
        {
            /* A file that exists but will not open (another program holding it - crash #425's cause) is waited for a
               while. Going on without it used to mean a crash; going on with an empty config would be worse - the
               defaults below would be written over everything in it on the first save. So if it still cannot be read,
               the defaults only stand in (not marked for saving) until the file can be, and it is adopted then. */
            JObject loaded = null;
            for (int attempt = 0; attempt < 6 && loaded == null; attempt++)
            {
                loaded = LoadFromDisk();
                if (loaded == null)
                    Thread.Sleep(250);
            }
            lock (_lock)
            {
                _jsonConfig = loaded ?? new JObject();
                _diskUnread = loaded == null;
            }

            //Migration to new settings keys - just keep analytics ID
            if (!_diskUnread && GetInteger(Settings.PrefsVersion) < 3)
            {
                JObject newConfig = new JObject();
                foreach (var entry in _jsonConfig)
                {
                    switch (entry.Key)
                    {
                        case Settings.UniqueId:
                        case Settings.SaveCounter:
                        case Settings.EntityCounter:
                            newConfig.Add(entry.Key, entry.Value);
                            break;
                    }
                }
                lock (_lock)
                {
                    _jsonConfig = newConfig;
                    _dirtyKeys.Clear();
                    _removedKeys.Clear();
                }
                SetInteger(Settings.PrefsVersion, 3);
            }

            SettingsDefaults.EnsureApplied();

            if (_diskUnread)
            {
                lock (_lock)
                {
                    _dirtyKeys.Clear();
                    _removedKeys.Clear();
                }
                ScheduleSaveRetry();
            }

            StartFileWatcher();

            //Saves wait a moment (see Save): what is still waiting goes out with any orderly exit, Environment.Exit included
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Flush();
        }

        //The file existed but could not be read when we started: what is in memory is defaults, not the user's settings
        static bool _diskUnread;
        //A save could not read the file to merge into: SetX leaves the disk to the retry timer until one succeeds
        static bool _saveDeferred;
        static int _saveRetryDelay = 1000;

        static void ScheduleSaveRetry()
        {
            lock (_lock)
            {
                _saveDeferred = true;
                if (_saveRetryTimer == null)
                    _saveRetryTimer = new Timer(_ => { try { Save(fromRetry: true); } catch { } }, null, Timeout.Infinite, Timeout.Infinite);
                _saveRetryTimer.Change(_saveRetryDelay, Timeout.Infinite);
                _saveRetryDelay = Math.Min(_saveRetryDelay * 2, 30000);
            }
        }

        static void StartFileWatcher()
        {
            try
            {
                string directory = Path.GetDirectoryName(ConfigFullPath);
                string fileName = Path.GetFileName(ConfigFullPath);
                if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
                    return;

                Directory.CreateDirectory(directory);

                _reloadDebounceTimer = new Timer(_ => ReloadFromExternalChange(), null, Timeout.Infinite, Timeout.Infinite);
                _watcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
                };
                _watcher.Changed += OnConfigFileChanged;
                _watcher.Created += OnConfigFileChanged;
                _watcher.Renamed += OnConfigFileChanged;
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Settings file watcher failed: " + e.Message);
            }
        }

        static void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            if (_suppressExternalReload)
                return;

            _reloadDebounceTimer?.Change(150, Timeout.Infinite);
        }

        /* Runs on a timer thread, where anything thrown ends the process (crash #425: the file was mid-write by
           another program for longer than LoadFromDisk's retries). Unreadable now means try again shortly. */
        static void ReloadFromExternalChange()
        {
            try
            {
                ReloadFromExternalChangeCore();
            }
            catch (Exception e)
            {
                Console.WriteLine("Settings reload failed: " + e.Message);
            }
        }

        static void ReloadFromExternalChangeCore()
        {
            if (_suppressExternalReload)
                return;

            JObject onDisk = LoadFromDisk();
            if (onDisk == null)
            {
                _reloadDebounceTimer?.Change(1000, Timeout.Infinite);
                return;
            }

            List<string> changedKeys = new List<string>();

            lock (_lock)
            {
                if (_suppressExternalReload)
                    return;

                HashSet<string> diskKeys = new HashSet<string>(onDisk.Properties().Select(p => p.Name));

                foreach (var prop in onDisk.Properties())
                {
                    if (_dirtyKeys.Contains(prop.Name) || _removedKeys.Contains(prop.Name))
                        continue;

                    JToken current = _jsonConfig[prop.Name];
                    if (JToken.DeepEquals(current, prop.Value))
                        continue;

                    _jsonConfig[prop.Name] = prop.Value.DeepClone();
                    changedKeys.Add(prop.Name);
                }

                foreach (string key in _jsonConfig.Properties().Select(p => p.Name).ToList())
                {
                    if (diskKeys.Contains(key) || _dirtyKeys.Contains(key) || _removedKeys.Contains(key))
                        continue;

                    _jsonConfig.Remove(key);
                    changedKeys.Add(key);
                }

                //What is in memory now is the file's (plus this session's own changes), not stand-in defaults
                _diskUnread = false;
            }

            if (changedKeys.Count > 0)
                RaiseSettingsChanged(changedKeys, externalChange: true);
        }

        static void RaiseSettingsChanged(IReadOnlyList<string> changedKeys, bool externalChange)
        {
            SettingsChanged?.Invoke(null, new SettingsChangedEventArgs(changedKeys, externalChange));
        }

        /// <summary>The settings on disk; empty when there is no file (or it is not JSON), null when it exists but could
        /// not be read - another program (or a save of ours) had it open for longer than the retries.</summary>
        static JObject LoadFromDisk()
        {
            if (!File.Exists(_configPath))
                return new JObject();

            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    using (FileStream stream = new FileStream(_configPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader reader = new StreamReader(stream))
                        return JObject.Parse(reader.ReadToEnd());
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    if (attempt < 4)
                        Thread.Sleep(50 * (attempt + 1));
                }
                catch (JsonException)
                {
                    return new JObject();
                }
            }

            return null;
        }

        static void MarkDirty(string name)
        {
            _dirtyKeys.Add(name);
            _removedKeys.Remove(name);
        }

        static void MarkRemoved(string name)
        {
            _removedKeys.Add(name);
            _dirtyKeys.Remove(name);
        }

        /* Work out if a setting value has been previously set */
        static public bool IsSet(string name)
        {
            lock (_lock)
            {
                return _jsonConfig[name] != null;
            }
        }

        /* Completely remove a settings key */
        static public void Unset(string name)
        {
            lock (_lock)
            {
                _jsonConfig.Remove(name);
                MarkRemoved(name);
            }
            Save();
        }

        /* Get a config variable */
        static public bool GetBool(string name, bool defaultVal = false)
        {
            lock (_lock)
            {
                return (_jsonConfig[name] != null) ? _jsonConfig[name].Value<bool>() : defaultVal;
            }
        }
        static public string GetString(string name, string defaultVal = "")
        {
            lock (_lock)
            {
                return (_jsonConfig[name] != null) ? _jsonConfig[name].Value<string>() : defaultVal;
            }
        }
        static public int GetInteger(string name, int defaultVal = 0)
        {
            lock (_lock)
            {
                return (_jsonConfig[name] != null) ? _jsonConfig[name].Value<int>() : defaultVal;
            }
        }
        static public float GetFloat(string name, float defaultVal = 0.0f)
        {
            lock (_lock)
            {
                return (_jsonConfig[name] != null) ? _jsonConfig[name].Value<float>() : defaultVal;
            }
        }
        static public string[] GetStringArray(string name)
        {
            lock (_lock)
            {
                return (_jsonConfig[name] != null) ? _jsonConfig[name].Values<string>().ToArray() : new string[0];
            }
        }
        static public int[] GetIntegerArray(string name)
        {
            lock (_lock)
            {
                return (_jsonConfig[name] != null) ? _jsonConfig[name].Values<int>().ToArray() : new int[0];
            }
        }
        static public float[] GetFloatArray(string name)
        {
            lock (_lock)
            {
                return (_jsonConfig[name] != null) ? _jsonConfig[name].Values<float>().ToArray() : new float[0];
            }
        }
        static public Dictionary<uint, bool> GetUIntBoolDictionary(string name)
        {
            lock (_lock)
            {
                Dictionary<uint, bool> values = new Dictionary<uint, bool>();
                if (_jsonConfig[name] is JObject obj)
                {
                    foreach (KeyValuePair<string, JToken> entry in obj)
                    {
                        if (!uint.TryParse(entry.Key, out uint key))
                            continue;
                        values[key] = entry.Value.Value<bool>();
                    }
                }
                return values;
            }
        }

        /* Set a config variable */
        static public void SetBool(string name, bool value)
        {
            lock (_lock)
            {
                _jsonConfig[name] = value;
                MarkDirty(name);
            }
            Save();
        }
        static public void SetString(string name, string value)
        {
            lock (_lock)
            {
                _jsonConfig[name] = value;
                MarkDirty(name);
            }
            Save();
        }
        static public void SetInteger(string name, int value)
        {
            lock (_lock)
            {
                _jsonConfig[name] = value;
                MarkDirty(name);
            }
            Save();
        }
        static public void SetFloat(string name, float value)
        {
            lock (_lock)
            {
                _jsonConfig[name] = value;
                MarkDirty(name);
            }
            Save();
        }
        static public void SetStringArray(string name, string[] value)
        {
            lock (_lock)
            {
                _jsonConfig[name] = new JArray(value);
                MarkDirty(name);
            }
            Save();
        }
        static public void SetIntegerArray(string name, int[] value)
        {
            lock (_lock)
            {
                _jsonConfig[name] = new JArray(value);
                MarkDirty(name);
            }
            Save();
        }
        static public void SetFloatArray(string name, float[] value)
        {
            lock (_lock)
            {
                _jsonConfig[name] = new JArray(value);
                MarkDirty(name);
            }
            Save();
        }
        static public void SetUIntBoolDictionary(string name, Dictionary<uint, bool> value)
        {
            lock (_lock)
            {
                JObject obj = new JObject();
                foreach (KeyValuePair<uint, bool> entry in value)
                    obj[entry.Key.ToString()] = entry.Value;
                _jsonConfig[name] = obj;
                MarkDirty(name);
            }
            Save();
        }

        /* Every SetX used to save on the spot: a read, a merge and a replace of the file on the calling thread - the UI
           thread, for a search box's every keystroke or a row of filter ticks. Usually milliseconds; in the viewport stress
           run one replace blocked for 20 s and the entity search froze with it. Changes now gather for a moment and are
           saved on a timer thread, and whatever is still pending is written when the process exits. */
        const int SaveDebounceMs = 200;
        static Timer _saveDebounceTimer;

        static private void Save()
        {
            lock (_lock)
            {
                if (_saveDebounceTimer == null)
                    _saveDebounceTimer = new Timer(_ => { try { Save(fromRetry: false); } catch (Exception e) { Console.WriteLine("Settings save failed: " + e.Message); } }, null, Timeout.Infinite, Timeout.Infinite);
                _saveDebounceTimer.Change(SaveDebounceMs, Timeout.Infinite);
            }
        }

        /// <summary>Writes any change still waiting to be saved, now, on this thread.</summary>
        public static void Flush()
        {
            lock (_lock)
                _saveDebounceTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            try
            {
                Save(fromRetry: true);
            }
            catch (Exception e)
            {
                Console.WriteLine("Settings flush failed: " + e.Message);
            }
        }

        /* One save at a time (the debounce timer, the retry timer and Flush can each start one): two that read the file
           together would each write back what they read, the later over the earlier's keys. */
        static readonly object _saveLock = new object();

        static private void Save(bool fromRetry)
        {
            lock (_saveLock)
                SaveOnce(fromRetry);
        }

        static void SaveOnce(bool fromRetry)
        {
            lock (_lock)
            {
                if (_dirtyKeys.Count == 0 && _removedKeys.Count == 0 && !_diskUnread)
                {
                    /* Nothing to write (a reload of the file got here first): whatever was waiting on the retry is done.
                       Left set, every later save stood aside for a retry that was never coming. */
                    _saveDeferred = false;
                    _saveRetryDelay = 1000;
                    return;
                }
                //Once a save has had to wait, the retry timer owns the disk until one gets through: otherwise every
                //change made meanwhile would sit out the read retries
                if (_saveDeferred && !fromRetry)
                    return;
            }

            //Read outside the lock - every GetX/SetX waits on it, and a busy file takes a moment to give up on
            JObject merged = LoadFromDisk();
            if (merged == null)
            {
                //Unreadable just now: writing our keys alone would wipe everything else in the file, so they stay dirty
                Console.WriteLine("Settings file busy - save deferred");
                ScheduleSaveRetry();
                return;
            }

            //What this save writes, taken under the lock; the write itself is not (a replace once blocked for 20 s)
            List<string> adopted = null;
            Dictionary<string, JToken> written = new Dictionary<string, JToken>();
            HashSet<string> removed;
            lock (_lock)
            {
                //The file could not be read when we started: what it holds is the user's, the stand-in defaults are not
                if (_diskUnread)
                    adopted = merged.Properties().Where(p => !_dirtyKeys.Contains(p.Name) && !JToken.DeepEquals(_jsonConfig[p.Name], p.Value)).Select(p => p.Name).ToList();

                removed = new HashSet<string>(_removedKeys);
                foreach (string key in removed)
                    merged.Remove(key);

                foreach (string key in _dirtyKeys)
                {
                    if (_jsonConfig[key] == null)
                        continue;
                    JToken value = _jsonConfig[key].DeepClone();
                    merged[key] = value;
                    written[key] = value.DeepClone();
                }
            }

            if (written.Count != 0 || removed.Count != 0)
            {
                try
                {
                    _suppressExternalReload = true;
                    WriteToDisk(merged);
                }
                catch (Exception e)
                {
                    //The write failed (the file taken between the read and the replace): the keys are still dirty
                    Console.WriteLine("Failed to save! " + e.Message);
                    ScheduleSaveRetry();
                    return;
                }
                finally
                {
                    _suppressExternalReload = false;
                }
            }

            lock (_lock)
            {
                //A key changed again while the file was being written is still to be saved (its SetX armed the next save)
                foreach (KeyValuePair<string, JToken> key in written)
                    if (JToken.DeepEquals(_jsonConfig[key.Key], key.Value))
                        _dirtyKeys.Remove(key.Key);
                foreach (string key in removed)
                    if (_jsonConfig[key] == null)
                        _removedKeys.Remove(key);

                //Memory takes the file as written, less whatever has changed here since
                foreach (string key in _dirtyKeys)
                    if (_jsonConfig[key] != null)
                        merged[key] = _jsonConfig[key].DeepClone();
                foreach (string key in _removedKeys)
                    merged.Remove(key);
                _jsonConfig = merged;

                _diskUnread = false;
                _saveDeferred = false;
                _saveRetryDelay = 1000;
            }

            if (adopted != null && adopted.Count != 0)
                RaiseSettingsChanged(adopted, externalChange: true);
        }

        static void WriteToDisk(JObject config)
        {
            string content = config.ToString(Formatting.Indented);
            string tempPath = _configPath + ".tmp";

            using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new StreamWriter(stream))
            {
                writer.Write(content);
            }

            if (File.Exists(_configPath))
                File.Replace(tempPath, _configPath, null);
            else
                File.Move(tempPath, _configPath);
        }
    }
}
