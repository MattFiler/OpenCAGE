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
        static Dictionary<string, bool> _bools = new Dictionary<string, bool>();
        static Dictionary<string, bool[]> _boolArrays = new Dictionary<string, bool[]>();
        static Dictionary<string, string> _strings = new Dictionary<string, string>();
        static Dictionary<string, string[]> _stringArrays = new Dictionary<string, string[]>();
        static Dictionary<string, int> _integers = new Dictionary<string, int>();
        static Dictionary<string, int[]> _integerArrays = new Dictionary<string, int[]>();
        static Dictionary<string, float> _floats = new Dictionary<string, float>();
        static Dictionary<string, float[]> _floatArrays = new Dictionary<string, float[]>();
        static Dictionary<string, Dictionary<uint, bool>> _uintBoolDictionaries = new Dictionary<string, Dictionary<uint, bool>>();

        static string _configPath = "OpenCAGE Settings.json";

        static SettingsManager()
        {
            if (File.Exists("OpenCAGE Settings.json"))
            {
                try
                {
                    JObject jsonConfig = JObject.Parse(File.ReadAllText(_configPath));
                    foreach (var entry in jsonConfig)
                    {
                        switch (entry.Key)
                        {
                            case Settings.UniqueId:
                                SetString(entry.Key, entry.Value.Value<string>());
                                break;
                            case Settings.UseStagingBranch:
                                SetBool(entry.Key, entry.Value.Value<bool>());
                                break;
                            case Settings.SaveCounter:
                            case Settings.EntityCounter:
                                SetInteger(entry.Key, entry.Value.Value<int>());
                                break;
                        }
                    }
                }
                catch { }
                File.Delete("OpenCAGE Settings.json");
            }
        }

        /* Work out if a setting value has been previously set */
        static public bool IsSet(string name)
        {
            if (_bools.ContainsKey(name))
                return true;
            if (_boolArrays.ContainsKey(name))
                return true;
            if (_strings.ContainsKey(name))
                return true;
            if (_stringArrays.ContainsKey(name))
                return true;
            if (_integers.ContainsKey(name))
                return true;
            if (_integerArrays.ContainsKey(name))
                return true;
            if (_floats.ContainsKey(name))
                return true;
            if (_floatArrays.ContainsKey(name))
                return true;
            if (_uintBoolDictionaries.ContainsKey(name))
                return true;
            return false;
        }
        
        /* Completely remove a settings key */
        static public void Unset(string name)
        {
            if (_bools.ContainsKey(name))
                _bools.Remove(name);
            if (_boolArrays.ContainsKey(name))
                _boolArrays.Remove(name);
            if (_strings.ContainsKey(name))
                _strings.Remove(name);
            if (_stringArrays.ContainsKey(name))
                _stringArrays.Remove(name);
            if (_integers.ContainsKey(name))
                _integers.Remove(name);
            if (_integerArrays.ContainsKey(name))
                _integerArrays.Remove(name);
            if (_floats.ContainsKey(name))
                _floats.Remove(name);
            if (_floatArrays.ContainsKey(name))
                _floatArrays.Remove(name);
            if (_uintBoolDictionaries.ContainsKey(name))
                _uintBoolDictionaries.Remove(name);
            Save();
        }

        /* Get a config variable */
        static public bool GetBool(string name, bool defaultVal = false)
        {
            return _bools.ContainsKey(name) ? _bools[name] : defaultVal;
        }
        static public bool[] GetBoolArray(string name)
        {
            return _boolArrays.ContainsKey(name) ? _boolArrays[name] : new bool[0];
        }
        static public string GetString(string name, string defaultVal = "")
        {
            return _strings.ContainsKey(name) ? _strings[name] : defaultVal;
        }
        static public string[] GetStringArray(string name)
        {
            return _stringArrays.ContainsKey(name) ? _stringArrays[name] : new string[0];
        }
        static public int GetInteger(string name, int defaultVal = 0)
        {
            return _integers.ContainsKey(name) ? _integers[name] : defaultVal;
        }
        static public int[] GetIntegerArray(string name)
        {
            return _integerArrays.ContainsKey(name) ? _integerArrays[name] : new int[0];
        }
        static public float GetFloat(string name, float defaultVal = 0.0f)
        {
            return _floats.ContainsKey(name) ? _floats[name] : defaultVal;
        }
        static public float[] GetFloatArray(string name)
        {
            return _floatArrays.ContainsKey(name) ? _floatArrays[name] : new float[0];
        }
        static public Dictionary<uint, bool> GetUIntBoolDictionary(string name)
        {
            return _uintBoolDictionaries.ContainsKey(name) ? _uintBoolDictionaries[name] : new Dictionary<uint, bool>();
        }

        /* Set a config variable */
        static public void SetBool(string name, bool value)
        {
            if (_bools.ContainsKey(name))
                _bools[name] = value;
            else
                _bools.Add(name, value);
            Save();
        }
        static public void SetBoolArray(string name, bool[] value)
        {
            if (_boolArrays.ContainsKey(name))
                _boolArrays[name] = value;
            else
                _boolArrays.Add(name, value);
            Save();
        }
        static public void SetString(string name, string value)
        {
            if (_strings.ContainsKey(name))
                _strings[name] = value;
            else
                _strings.Add(name, value);
            Save();
        }
        static public void SetStringArray(string name, string[] value)
        {
            if (_stringArrays.ContainsKey(name))
                _stringArrays[name] = value;
            else
                _stringArrays.Add(name, value);
            Save();
        }
        static public void SetInteger(string name, int value)
        {
            if (_integers.ContainsKey(name))
                _integers[name] = value;
            else
                _integers.Add(name, value);
            Save();
        }
        static public void SetIntegerArray(string name, int[] value)
        {
            if (_integerArrays.ContainsKey(name))
                _integerArrays[name] = value;
            else
                _integerArrays.Add(name, value);
            Save();
        }
        static public void SetFloat(string name, float value)
        {
            if (_floats.ContainsKey(name))
                _floats[name] = value;
            else
                _floats.Add(name, value);
            Save();
        }
        static public void SetFloatArray(string name, float[] value)
        {
            if (_floatArrays.ContainsKey(name))
                _floatArrays[name] = value;
            else
                _floatArrays.Add(name, value);
            Save();
        }
        static public void SetUIntBoolDictionary(string name, Dictionary<uint, bool> value)
        {
            if (_uintBoolDictionaries.ContainsKey(name))
                _uintBoolDictionaries[name] = value;
            else
                _uintBoolDictionaries.Add(name, value);
            Save();
        }
        static private void Save()
        {
            try
            {
                using (BinaryWriter writer = new BinaryWriter(File.OpenWrite(_configPath)))
                {
                    writer.Write(_bools.Count);
                    foreach (KeyValuePair<string, bool> entries in _bools)
                    {
                        writer.Write(entries.Key);
                        writer.Write(entries.Value);
                    }
                    writer.Write(_boolArrays.Count);
                    foreach (KeyValuePair<string, bool[]> entries in _boolArrays)
                    {
                        writer.Write(entries.Key);
                        writer.Write(entries.Value.Length);
                        foreach (bool value in entries.Value)
                            writer.Write(value);
                    }
                    writer.Write(_strings.Count);
                    foreach (KeyValuePair<string, string> entries in _strings)
                    {
                        writer.Write(entries.Key);
                        writer.Write(entries.Value);
                    }
                    writer.Write(_stringArrays.Count);
                    foreach (KeyValuePair<string, string[]> entries in _stringArrays)
                    {
                        writer.Write(entries.Key);
                        writer.Write(entries.Value.Length);
                        foreach (string value in entries.Value)
                            writer.Write(value);
                    }
                    writer.Write(_integers.Count);
                    foreach (KeyValuePair<string, int> entries in _integers)
                    {
                        writer.Write(entries.Key);
                        writer.Write(entries.Value);
                    }
                    writer.Write(_integerArrays.Count);
                    foreach (KeyValuePair<string, int[]> entries in _integerArrays)
                    {
                        writer.Write(entries.Key);
                        writer.Write(entries.Value.Length);
                        foreach (int value in entries.Value)
                            writer.Write(value);
                    }
                    writer.Write(_floats.Count);
                    foreach (KeyValuePair<string, float> entries in _floats)
                    {
                        writer.Write(entries.Key);
                        writer.Write(entries.Value);
                    }
                    writer.Write(_floatArrays.Count);
                    foreach (KeyValuePair<string, float[]> entries in _floatArrays)
                    {
                        writer.Write(entries.Key);
                        writer.Write(entries.Value.Length);
                        foreach (float value in entries.Value)
                            writer.Write(value);
                    }
                    writer.Write(_uintBoolDictionaries.Count);
                    foreach (KeyValuePair<string, Dictionary<uint, bool>> entries in _uintBoolDictionaries)
                    {
                        writer.Write(entries.Key);
                        writer.Write(entries.Value.Count);
                        foreach (KeyValuePair<uint, bool> value in entries.Value)
                        {
                            writer.Write(value.Key);
                            writer.Write(value.Value);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to save! " + e.Message);
            }
        }
    }
}
