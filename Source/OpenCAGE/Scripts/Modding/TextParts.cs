#if ENABLE_MOD_PACKAGES
using OpenCAGE.Modding.Merging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenCAGE.Modding
{
    /// <summary>
    /// The game's text files change by change, for Create a Mod, as <see cref="PakParts"/> does for archives: a string table
    /// (DATA/TEXT/&lt;language&gt;/*.TXT, and the same in a level's own TEXT folder) string by string - each one changed, new or
    /// taken out - and a level's TEXT/TEXT_DB_LIST.TXT (which of its text databases it loads) database by database. A mod then
    /// carries the file as it shipped with only the chosen changes in it: still a whole file in the package, so it installs
    /// exactly as before, and every installer - this one and older - combines it with other mods' versions of the file string
    /// by string (<see cref="TextDbMerge"/>) or line by line (<see cref="LineMerge"/>). Mods made before this carried the whole
    /// file as the author had it, which combines the same way.
    /// </summary>
    public static class TextParts
    {
        public const string StringPrefix = "string|";
        public const string DatabasePrefix = "db|";

        /// <summary>Whether a file is one of the game's text files these take apart (it may still go whole: see <see cref="Find"/>).</summary>
        public static bool Handles(string normalisedPath)
        {
            if (string.IsNullOrEmpty(normalisedPath) || !normalisedPath.EndsWith(".TXT", StringComparison.OrdinalIgnoreCase))
                return false;
            return normalisedPath.StartsWith("DATA/TEXT/", StringComparison.OrdinalIgnoreCase) || normalisedPath.IndexOf("/TEXT/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>A level's list of the text databases it loads, rather than a string table.</summary>
        public static bool IsDatabaseList(string normalisedPath) => normalisedPath != null && normalisedPath.EndsWith("/TEXT/TEXT_DB_LIST.TXT", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The changes in <paramref name="current"/> against <paramref name="original"/>, one part each, or null when the file
        /// can't be taken apart (no original to compare with, or not a file of strings): it then goes whole, as before. An
        /// empty list means no string differs (only how the file is laid out).
        /// </summary>
        public static List<PakPart> Find(string normalisedPath, byte[] original, byte[] current)
        {
            if (original == null || current == null)
                return null;
            try
            {
                return IsDatabaseList(normalisedPath) ? DatabaseParts(normalisedPath, original, current) : StringParts(normalisedPath, original, current);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>The original with only the chosen parts put in, as the file's bytes (its own encoding kept); null when that can't be done.</summary>
        public static byte[] Build(string normalisedPath, byte[] original, byte[] current, IEnumerable<PakPart> chosen)
        {
            if (original == null || current == null)
                return null;
            try
            {
                List<PakPart> parts = chosen.ToList();
                return IsDatabaseList(normalisedPath) ? BuildDatabases(original, current, parts) : BuildStrings(original, current, parts);
            }
            catch
            {
                return null;
            }
        }

        #region STRING TABLES
        /* A string table holds records - "[KEY]" then its "{text}" - though it may also be empty (a database made in OpenCAGE
           starts as just the byte-order mark) */
        private static bool IsStringTable(string text)
        {
            string trimmed = text.Trim('﻿', ' ', '\r', '\n', '\t');
            return trimmed.Length == 0 || (trimmed.StartsWith("[") && text.Contains("]") && text.Contains("{"));
        }

        /* A record's text as the game reads it: what follows its key line, newlines as \n, without the blank lines after it -
           how the file is laid out around it (a missing last newline, CRLF or LF) is no change */
        private static string Value(KeyedListMerge.Record record)
        {
            string text = record.Text.Replace("\r\n", "\n");
            int line = text.IndexOf('\n');
            return (line < 0 ? "" : text.Substring(line + 1)).Trim();
        }

        /* The text as a row shows it: its braces taken off, on one line */
        private static string Shown(string value, int length)
        {
            string text = value;
            if (text.StartsWith("{") && text.EndsWith("}") && text.Length >= 2)
                text = text.Substring(1, text.Length - 2);
            text = text.Replace("\n", " / ").Trim();
            return text.Length <= length ? text : text.Substring(0, length) + "...";
        }

        private static Dictionary<string, KeyedListMerge.Record> Keyed(List<KeyedListMerge.Record> records)
        {
            Dictionary<string, KeyedListMerge.Record> index = new Dictionary<string, KeyedListMerge.Record>();
            foreach (KeyedListMerge.Record record in records)
                if (record.Key != null && !index.ContainsKey(record.Key))
                    index[record.Key] = record;
            return index;
        }

        private static List<PakPart> StringParts(string file, byte[] original, byte[] current)
        {
            string a = TextDbMerge.Decode(original, out _), b = TextDbMerge.Decode(current, out _);
            if (!IsStringTable(a) || !IsStringTable(b))
                return null;
            List<KeyedListMerge.Record> before = TextDbMerge.Parse(a), after = TextDbMerge.Parse(b);
            Dictionary<string, KeyedListMerge.Record> was = Keyed(before), now = Keyed(after);
            List<PakPart> parts = new List<PakPart>();
            foreach (KeyValuePair<string, KeyedListMerge.Record> record in now)
            {
                string value = Value(record.Value);
                if (!was.TryGetValue(record.Key, out KeyedListMerge.Record old))
                    parts.Add(StringPart(file, record.Key, "new", record.Key + ": \"" + Shown(value, 60) + "\"", "New: " + Shown(value, 400)));
                else if (Value(old) != value)
                    parts.Add(StringPart(file, record.Key, "changed", record.Key + ": \"" + Shown(value, 60) + "\"", "Was: " + Shown(Value(old), 400) + "\nNow: " + Shown(value, 400)));
            }
            foreach (KeyValuePair<string, KeyedListMerge.Record> record in was)
                if (!now.ContainsKey(record.Key))
                    parts.Add(StringPart(file, record.Key, "removed", record.Key, "Was: " + Shown(Value(record.Value), 400)));
            return parts;
        }

        private static PakPart StringPart(string file, string key, string change, string title, string tooltip)
        {
            return new PakPart() { File = file, Title = title, Change = change, Key = StringPrefix + key, Tooltip = tooltip };
        }

        private static byte[] BuildStrings(byte[] original, byte[] current, List<PakPart> chosen)
        {
            string a = TextDbMerge.Decode(original, out Encoding encoding), b = TextDbMerge.Decode(current, out _);
            if (!IsStringTable(a) || !IsStringTable(b))
                return null;
            string newline = a.Contains("\r\n") || !a.Contains("\n") ? "\r\n" : "\n";
            List<KeyedListMerge.Record> records = TextDbMerge.Parse(a), theirs = TextDbMerge.Parse(b);
            Dictionary<string, KeyedListMerge.Record> now = Keyed(theirs);
            //A record as it goes in: the user's text, in the original's newlines
            string Text(KeyedListMerge.Record record) => record.Text.Replace("\r\n", "\n").Replace("\n", newline).TrimEnd('\r', '\n', ' ', '\t');

            foreach (PakPart part in chosen)
            {
                if (part.Key == null || !part.Key.StartsWith(StringPrefix))
                    return null;
                string key = part.Key.Substring(StringPrefix.Length);
                int at = records.FindIndex(o => o.Key == key);
                switch (part.Change)
                {
                    case "removed":
                        if (at >= 0) records.RemoveAt(at);
                        break;
                    case "changed":
                        if (at < 0 || !now.TryGetValue(key, out KeyedListMerge.Record changed))
                            return null;
                        //The blank lines after it stay as the original had them
                        string old = records[at].Text;
                        records[at] = new KeyedListMerge.Record() { Key = key, Text = Text(changed) + old.Substring(old.TrimEnd('\r', '\n', ' ', '\t').Length) };
                        break;
                }
            }
            //New strings go in after the string they follow in the user's file (or at the top, after anything before the first
            //string), in the user's order, so one after another follows the one before
            foreach (PakPart part in chosen.Where(o => o.Change == "new").OrderBy(o => theirs.FindIndex(r => StringPrefix + r.Key == o.Key)))
            {
                string key = part.Key.Substring(StringPrefix.Length);
                if (!now.TryGetValue(key, out KeyedListMerge.Record added))
                    return null;
                if (records.Any(o => o.Key == key))
                    continue;
                int position = theirs.IndexOf(added), insertAt = -1;
                for (int i = position - 1; i >= 0 && insertAt < 0; i--)
                {
                    if (theirs[i].Key == null) continue;
                    int found = records.FindIndex(o => o.Key == theirs[i].Key);
                    if (found >= 0) insertAt = found + 1;
                }
                if (insertAt < 0)
                    insertAt = records.Count != 0 && records[0].Key == null ? 1 : 0;
                records.Insert(insertAt, new KeyedListMerge.Record() { Key = key, Text = Text(added) });
            }

            //Each record but the last ends with the blank line the next needs; the last ends as the original's did
            bool endsWithNewline = a.EndsWith("\n");
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < records.Count; i++)
            {
                string record = records[i].Text;
                if (i < records.Count - 1)
                {
                    if (!record.EndsWith("\n"))
                        record = record.TrimEnd('\r', '\n', ' ', '\t') + newline + newline;
                }
                else if (endsWithNewline && !record.EndsWith("\n"))
                    record += newline;
                text.Append(record);
            }
            return TextDbMerge.Encode(text.ToString(), encoding);
        }
        #endregion

        #region A LEVEL'S LIST OF TEXT DATABASES
        private static List<string> Names(string text)
        {
            return text.Replace("\r\n", "\n").Split('\n').Select(o => o.Trim().TrimStart('﻿')).Where(o => o.Length != 0).ToList();
        }

        private static List<PakPart> DatabaseParts(string file, byte[] original, byte[] current)
        {
            List<string> before = Names(TextDbMerge.Decode(original, out _)), after = Names(TextDbMerge.Decode(current, out _));
            HashSet<string> was = new HashSet<string>(before, StringComparer.OrdinalIgnoreCase), now = new HashSet<string>(after, StringComparer.OrdinalIgnoreCase);
            List<PakPart> parts = new List<PakPart>();
            foreach (string name in after.Where(o => !was.Contains(o)).Distinct(StringComparer.OrdinalIgnoreCase))
                parts.Add(new PakPart() { File = file, Title = "Loads the text database " + name, Change = "added", Key = DatabasePrefix + name.ToUpperInvariant(), Tooltip = "The level loads " + name + " from its own TEXT folder." });
            foreach (string name in before.Where(o => !now.Contains(o)).Distinct(StringComparer.OrdinalIgnoreCase))
                parts.Add(new PakPart() { File = file, Title = "No longer loads the text database " + name, Change = "removed", Key = DatabasePrefix + name.ToUpperInvariant(), Tooltip = "The level stops loading " + name + " from its own TEXT folder." });
            return parts;
        }

        private static byte[] BuildDatabases(byte[] original, byte[] current, List<PakPart> chosen)
        {
            string a = TextDbMerge.Decode(original, out Encoding encoding), b = TextDbMerge.Decode(current, out _);
            string newline = a.Contains("\r\n") || !a.Contains("\n") ? "\r\n" : "\n";
            List<string> names = Names(a), theirs = Names(b);
            foreach (PakPart part in chosen)
            {
                if (part.Key == null || !part.Key.StartsWith(DatabasePrefix))
                    return null;
                string name = part.Key.Substring(DatabasePrefix.Length);
                if (part.Change == "removed")
                    names.RemoveAll(o => string.Equals(o, name, StringComparison.OrdinalIgnoreCase));
                else if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
                    names.Add(theirs.FirstOrDefault(o => string.Equals(o, name, StringComparison.OrdinalIgnoreCase)) ?? name);
            }
            string text = string.Join(newline, names) + (a.EndsWith("\n") ? newline : "");
            return TextDbMerge.Encode(text, encoding);
        }
        #endregion
    }
}
#endif
