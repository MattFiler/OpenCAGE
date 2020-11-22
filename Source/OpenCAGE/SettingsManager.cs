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
        static JObject json_config = null;
        static string path_to_config = "OpenCAGE Settings.json";

        static SettingsManager()
        {
            if (!File.Exists(path_to_config)) json_config = new JObject { };
            else json_config = JObject.Parse(File.ReadAllText(path_to_config));
        }

        /* Get a config variable */
        static public bool GetBool(string name)
        {
            return (json_config[name] != null) ? json_config[name].Value<bool>() : false;
        }
        static public string GetString(string name)
        {
            return (json_config[name] != null) ? json_config[name].Value<string>() : "";
        }
        static public int GetInteger(string name)
        {
            return (json_config[name] != null) ? json_config[name].Value<int>() : 0;
        }
        static public float GetFloat(string name)
        {
            return (json_config[name] != null) ? json_config[name].Value<float>() : 0.0f;
        }

        /* Set a config variable */
        static public void SetBool(string name, bool value)
        {
            json_config[name] = value;
            Save();
        }
        static public void SetString(string name, string value)
        {
            json_config[name] = value;
            Save();
        }
        static public void SetInteger(string name, int value)
        {
            json_config[name] = value;
            Save();
        }
        static public void SetFloat(string name, float value)
        {
            json_config[name] = value;
            Save();
        }
        static private void Save()
        {
            File.WriteAllText(path_to_config, json_config.ToString(Formatting.Indented));
        }
    }
}
