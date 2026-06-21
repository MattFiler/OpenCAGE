using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OpenCAGE
{
    static class SettingsManager
    {
        static JObject _jsonConfig = null;
        static string _configPath = "OpenCAGE Settings.json";

        static SettingsManager()
        {
            if (!File.Exists(_configPath))
            {
                _jsonConfig = new JObject { };
            }
            else
            {
                try
                {
                    _jsonConfig = JObject.Parse(File.ReadAllText(_configPath));
                }
                catch
                {
                    _jsonConfig = new JObject { };
                }
            }

            //Migration to new settings keys - just keep analytics ID
            if (GetInteger(Settings.PrefsVersion) < 2)
            {
                JObject newConfig = new JObject();
                foreach (var entry in _jsonConfig)
                {
                    if (entry.Key == Settings.UniqueId)
                    {
                        newConfig.Add(entry.Key, entry.Value);
                        break;
                    }
                }
                _jsonConfig = newConfig;
                SetInteger(Settings.PrefsVersion, 2);
            }
        }

        /* Work out if a setting value has been previously set */
        static public bool IsSet(string name)
        {
            return _jsonConfig[name] != null;
        }
        
        /* Completely remove a settings key */
        static public void Unset(string name)
        {
            _jsonConfig.Remove(name);
            Save();
        }

        /* Get a config variable */
        static public bool GetBool(string name, bool defaultVal = false)
        {
            return (_jsonConfig[name] != null) ? _jsonConfig[name].Value<bool>() : defaultVal;
        }
        static public string GetString(string name, string defaultVal = "")
        {
            return (_jsonConfig[name] != null) ? _jsonConfig[name].Value<string>() : defaultVal;
        }
        static public int GetInteger(string name, int defaultVal = 0)
        {
            return (_jsonConfig[name] != null) ? _jsonConfig[name].Value<int>() : defaultVal;
        }
        static public float GetFloat(string name, float defaultVal = 0.0f)
        {
            return (_jsonConfig[name] != null) ? _jsonConfig[name].Value<float>() : defaultVal;
        }
        static public int[] GetIntegerArray(string name)
        {
            return (_jsonConfig[name] != null) ? _jsonConfig[name].Values<int>().ToArray() : new int[0];
        }
        static public Dictionary<uint, bool> GetUIntBoolDictionary(string name)
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

        /* Set a config variable */
        static public void SetBool(string name, bool value)
        {
            _jsonConfig[name] = value;
            Save();
        }
        static public void SetString(string name, string value)
        {
            _jsonConfig[name] = value;
            Save();
        }
        static public void SetInteger(string name, int value)
        {
            _jsonConfig[name] = value;
            Save();
        }
        static public void SetFloat(string name, float value)
        {
            _jsonConfig[name] = value;
            Save();
        }
        static public void SetIntegerArray(string name, int[] value)
        {
            _jsonConfig[name] = new JArray(value);
            Save();
        }
        static public void SetUIntBoolDictionary(string name, Dictionary<uint, bool> value)
        {
            JObject obj = new JObject();
            foreach (KeyValuePair<uint, bool> entry in value)
                obj[entry.Key.ToString()] = entry.Value;
            _jsonConfig[name] = obj;
            Save();
        }
        static private void Save()
        {
            try
            {
                File.WriteAllText(_configPath, _jsonConfig.ToString(Formatting.Indented));
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to save! " + e.Message);
            }
        }
    }
}
