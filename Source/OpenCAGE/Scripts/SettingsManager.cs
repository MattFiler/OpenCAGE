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
            if (!File.Exists(_configPath)) _jsonConfig = new JObject { };
            else _jsonConfig = JObject.Parse(File.ReadAllText(_configPath));
        }

        /* Work out if a setting value has been previously set */
        static public bool IsSet(string name)
        {
            return _jsonConfig[name] != null;
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
        static private void Save()
        {
            try
            {
                File.WriteAllText(_configPath, _jsonConfig.ToString(Formatting.Indented));
            }
            catch (Exception e)
            {
                CommandsEditor.Debug.Log("Settings Manager", "Failed to save! " + e.Message);
            }
        }
    }
}
