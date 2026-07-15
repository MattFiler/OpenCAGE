using CATHODE.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE
{
    public static class EntityPaletteRecent
    {
        public const int MaxEntries = 10;
        public const string FunctionPrefix = "F:";
        public const string VariablePrefix = "V:";

        public static event Action Changed;

        public static void RecordFunction(FunctionType function)
        {
            Record(FunctionPrefix + function.ToString());
        }

        public static void RecordVariable(CompositePinType pinType)
        {
            Record(VariablePrefix + pinType.ToString());
        }

        public static string[] GetEntries()
        {
            return SettingsManager.GetStringArray(Settings.EntityPaletteLastUsed);
        }

        public static bool TryParse(string entry, out FunctionType? function, out CompositePinType? variable)
        {
            function = null;
            variable = null;
            if (string.IsNullOrEmpty(entry))
                return false;

            if (entry.StartsWith(FunctionPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse(entry.Substring(FunctionPrefix.Length), out FunctionType parsedFunction))
                {
                    function = parsedFunction;
                    return true;
                }
                return false;
            }

            if (entry.StartsWith(VariablePrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse(entry.Substring(VariablePrefix.Length), out CompositePinType parsedVariable))
                {
                    variable = parsedVariable;
                    return true;
                }
            }

            return false;
        }

        private static void Record(string key)
        {
            List<string> entries = GetEntries().ToList();
            entries.RemoveAll(o => string.Equals(o, key, StringComparison.OrdinalIgnoreCase));
            entries.Insert(0, key);
            if (entries.Count > MaxEntries)
                entries = entries.Take(MaxEntries).ToList();

            SettingsManager.SetStringArray(Settings.EntityPaletteLastUsed, entries.ToArray());
            Changed?.Invoke();
        }
    }
}
