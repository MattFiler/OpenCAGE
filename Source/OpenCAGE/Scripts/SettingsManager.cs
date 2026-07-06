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
        static string _configPath = "OpenCAGE Settings.json";
        static readonly HashSet<string> _dirtyKeys = new HashSet<string>();
        static readonly HashSet<string> _removedKeys = new HashSet<string>();
        static bool _suppressExternalReload = false;
        static FileSystemWatcher _watcher;
        static Timer _reloadDebounceTimer;
        static string ConfigFullPath => Path.GetFullPath(_configPath);

        public static event EventHandler<SettingsChangedEventArgs> SettingsChanged;

        static SettingsManager()
        {
            lock (_lock)
            {
                _jsonConfig = LoadFromDisk() ?? new JObject();
            }

            //Migration to new settings keys - just keep analytics ID
            if (GetInteger(Settings.PrefsVersion) < 3)
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

            StartFileWatcher();
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

        static void ReloadFromExternalChange()
        {
            if (_suppressExternalReload)
                return;

            JObject onDisk = LoadFromDisk();
            if (onDisk == null)
                return;

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
            }

            if (changedKeys.Count > 0)
                RaiseSettingsChanged(changedKeys, externalChange: true);
        }

        static void RaiseSettingsChanged(IReadOnlyList<string> changedKeys, bool externalChange)
        {
            SettingsChanged?.Invoke(null, new SettingsChangedEventArgs(changedKeys, externalChange));
        }

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
                catch (IOException) when (attempt < 4)
                {
                    Thread.Sleep(50);
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

        static private void Save()
        {
            lock (_lock)
            {
                if (_dirtyKeys.Count == 0 && _removedKeys.Count == 0)
                    return;

                try
                {
                    _suppressExternalReload = true;

                    JObject merged = LoadFromDisk() ?? new JObject();

                    foreach (string key in _removedKeys)
                        merged.Remove(key);

                    foreach (string key in _dirtyKeys)
                    {
                        if (_jsonConfig[key] != null)
                            merged[key] = _jsonConfig[key].DeepClone();
                    }

                    WriteToDisk(merged);
                    _jsonConfig = merged;
                    _dirtyKeys.Clear();
                    _removedKeys.Clear();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to save! " + e.Message);
                }
                finally
                {
                    _suppressExternalReload = false;
                }
            }
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
