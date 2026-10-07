#if ENABLE_MOD_PACKAGES
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenCAGE.Modding.Merging
{
    /// <summary>
    /// A file seen as a list of records with names - localised strings by key, config lines by setting - merged
    /// three ways against vanilla: each mod's changed, added and removed records are carried into the result, a
    /// record two mods both change goes to the later one, and new records go in after the record they followed.
    /// </summary>
    public class KeyedListMerge
    {
        public class Record
        {
            public string Key;
            public string Text;
        }

        private readonly string _target;
        private readonly Func<string, string> _describeKey;
        private readonly Dictionary<string, string> _claims = new Dictionary<string, string>();
        public List<MergeConflict> Conflicts { get; } = new List<MergeConflict>();

        public KeyedListMerge(string target, Func<string, string> describeKey)
        {
            _target = target;
            _describeKey = describeKey;
        }

        public List<Record> Merge(List<Record> vanilla, IList<(string ModName, List<Record> Records)> versions)
        {
            List<Record> merged = vanilla.Select(o => new Record() { Key = o.Key, Text = o.Text }).ToList();
            Dictionary<string, Record> baseByKey = Index(vanilla);
            List<string> applied = new List<string>();
            foreach ((string modName, List<Record> records) in versions)
            {
                Dictionary<string, Record> modByKey = Index(records);
                Dictionary<string, Record> mergedByKey = Index(merged);

                foreach (Record b in vanilla)
                {
                    if (b.Key == null) continue;
                    modByKey.TryGetValue(b.Key, out Record t);
                    mergedByKey.TryGetValue(b.Key, out Record m);
                    if (t == null)
                    {
                        if (m == null) continue;
                        if (m.Text != b.Text) Conflict(b.Key, ConflictKind.RemovedVsChanged, modName, applied, "'" + modName + "' removes it");
                        merged.Remove(m);
                        _claims[b.Key] = modName;
                    }
                    else if (t.Text != b.Text && (m == null || t.Text != m.Text))
                    {
                        if (m == null)
                        {
                            Conflict(b.Key, ConflictKind.RemovedVsChanged, modName, applied, "an earlier mod removes it, '" + modName + "' changes it");
                            InsertAfterAnchor(merged, new Record() { Key = t.Key, Text = t.Text }, records, t);
                        }
                        else
                        {
                            if (m.Text != b.Text) Conflict(b.Key, ConflictKind.Overridden, modName, applied, null);
                            m.Text = t.Text;
                        }
                        _claims[b.Key] = modName;
                    }
                }

                foreach (Record t in records)
                {
                    if (t.Key == null || baseByKey.ContainsKey(t.Key)) continue;
                    if (mergedByKey.TryGetValue(t.Key, out Record twin))
                    {
                        if (twin.Text != t.Text)
                        {
                            Conflict(t.Key, ConflictKind.Overridden, modName, applied, "both add it, differently");
                            twin.Text = t.Text;
                        }
                    }
                    else
                        InsertAfterAnchor(merged, new Record() { Key = t.Key, Text = t.Text }, records, t);
                    _claims[t.Key] = modName;
                }
                applied.Add(modName);
            }
            return merged;
        }

        private static Dictionary<string, Record> Index(List<Record> records)
        {
            Dictionary<string, Record> index = new Dictionary<string, Record>();
            foreach (Record r in records)
                if (r.Key != null && !index.ContainsKey(r.Key))
                    index[r.Key] = r;
            return index;
        }

        private static void InsertAfterAnchor(List<Record> merged, Record added, List<Record> source, Record sourceRecord)
        {
            int at = source.IndexOf(sourceRecord);
            for (int i = at - 1; i >= 0; i--)
            {
                if (source[i].Key == null) continue;
                int found = merged.FindIndex(o => o.Key == source[i].Key);
                if (found >= 0)
                {
                    merged.Insert(found + 1, added);
                    return;
                }
            }
            if (at == 0) merged.Insert(0, added); else merged.Add(added);
        }

        private void Conflict(string key, ConflictKind kind, string modName, List<string> applied, string detail)
        {
            _claims.TryGetValue(key, out string earlier);
            if (earlier == modName) return;
            Conflicts.Add(new MergeConflict()
            {
                Target = _target,
                Where = _describeKey(key),
                Kind = kind,
                Kept = modName,
                Lost = earlier ?? (applied.Count == 0 ? "an earlier mod" : string.Join(", ", applied)),
                Detail = detail,
            });
        }
    }

    /// <summary>
    /// The game's localised string tables (DATA/TEXT/&lt;language&gt;/*.TXT, and a level's own TEXT folder):
    /// "[KEY]" then "{text}" blocks, UTF-16. Merged string by string.
    /// </summary>
    public static class TextDbMerge
    {
        public static bool LooksLikeTextDb(byte[] bytes)
        {
            string text = Decode(bytes, out _);
            string trimmed = text.TrimStart('﻿', ' ', '\r', '\n', '\t');
            return trimmed.StartsWith("[") && text.Contains("]") && text.Contains("{");
        }

        public static List<KeyedListMerge.Record> Parse(string text)
        {
            //Each record runs from its "[KEY]" line to just before the next one; anything before the first key is a
            //keyless record that rides along unchanged
            List<KeyedListMerge.Record> records = new List<KeyedListMerge.Record>();
            int pos = 0;
            int start = 0;
            string key = null;
            while (pos <= text.Length)
            {
                int lineEnd = text.IndexOf('\n', pos);
                if (lineEnd < 0) lineEnd = text.Length;
                string line = text.Substring(pos, lineEnd - pos).TrimEnd('\r');
                bool isKey = line.Length > 2 && line[0] == '[' && line[line.Length - 1] == ']' && !InsideBraces(text, pos);
                if (isKey)
                {
                    if (pos > start || key != null)
                        records.Add(new KeyedListMerge.Record() { Key = key, Text = text.Substring(start, pos - start) });
                    key = line.Substring(1, line.Length - 2);
                    start = pos;
                }
                if (lineEnd >= text.Length) break;
                pos = lineEnd + 1;
            }
            if (start < text.Length || key != null)
                records.Add(new KeyedListMerge.Record() { Key = key, Text = text.Substring(start) });
            return records;
        }

        /* A "[...]" line can sit inside a multi-line {value}: count the braces before it */
        private static bool InsideBraces(string text, int pos)
        {
            int open = text.LastIndexOf('{', Math.Max(0, pos - 1));
            if (open < 0) return false;
            int close = text.LastIndexOf('}', Math.Max(0, pos - 1));
            return open > close;
        }

        public static string Write(List<KeyedListMerge.Record> records)
        {
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < records.Count; i++)
            {
                string record = records[i].Text;
                //A record that was last in its own file may lack the blank line the next one needs
                if (i < records.Count - 1 && !record.EndsWith("\n"))
                    record += "\n\n";
                text.Append(record);
            }
            return text.ToString();
        }

        public static MergeOutcome Merge(string target, byte[] vanilla, IList<Contribution> versions)
        {
            string baseText = Decode(vanilla, out Encoding encoding);
            KeyedListMerge merge = new KeyedListMerge(target, key => "string " + key);
            List<KeyedListMerge.Record> merged = merge.Merge(Parse(baseText),
                versions.Select(o => (o.ModName, Parse(Decode(o.Bytes, out _)))).ToList());
            return new MergeOutcome() { Bytes = Encode(Write(merged), encoding), Conflicts = merge.Conflicts };
        }

        public static string Decode(byte[] bytes, out Encoding encoding)
        {
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) { encoding = new UnicodeEncoding(false, true); return encoding.GetString(bytes, 2, bytes.Length - 2); }
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) { encoding = new UnicodeEncoding(true, true); return encoding.GetString(bytes, 2, bytes.Length - 2); }
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) { encoding = new UTF8Encoding(true); return encoding.GetString(bytes, 3, bytes.Length - 3); }
            encoding = new UTF8Encoding(false);
            return encoding.GetString(bytes);
        }

        public static byte[] Encode(string text, Encoding encoding)
        {
            byte[] preamble = encoding.GetPreamble();
            byte[] body = encoding.GetBytes(text);
            return preamble.Concat(body).ToArray();
        }
    }

    /// <summary>
    /// Any other text file, line by line: diff3. Changes to different lines all survive; where two mods change
    /// the same lines the later mod's lines are kept.
    /// </summary>
    public static class LineMerge
    {
        public static MergeOutcome Merge(string target, byte[] vanilla, IList<Contribution> versions)
        {
            string baseText = TextDbMerge.Decode(vanilla, out Encoding encoding);
            string newline = baseText.Contains("\r\n") ? "\r\n" : "\n";
            List<string> baseLines = Split(baseText);
            List<string> merged = new List<string>(baseLines);
            MergeOutcome outcome = new MergeOutcome();
            List<string> applied = new List<string>();
            //Which mod last wrote each line of the result, for naming the earlier side of a clash
            List<string> owners = baseLines.Select(o => (string)null).ToList();

            foreach (Contribution version in versions)
            {
                List<string> theirs = Split(TextDbMerge.Decode(version.Bytes, out _));
                Diff3(baseLines, merged, owners, theirs, version.ModName, applied, target, outcome.Conflicts, out merged, out owners);
                applied.Add(version.ModName);
            }
            outcome.Bytes = TextDbMerge.Encode(string.Join(newline, merged), encoding);
            return outcome;
        }

        private static List<string> Split(string text)
        {
            return text.Replace("\r\n", "\n").Split('\n').ToList();
        }

        /* Classic diff3: the stretches of vanilla that both versions keep act as anchors; between anchors, a side
           that changed wins over one that didn't, and two sides that changed differently is a clash */
        private static void Diff3(List<string> o, List<string> a, List<string> aOwners, List<string> b, string modName, List<string> applied,
            string target, List<MergeConflict> conflicts, out List<string> result, out List<string> owners)
        {
            List<(int, int)> oa = Lcs(o, a), ob = Lcs(o, b);
            Dictionary<int, int> oToA = oa.ToDictionary(x => x.Item1, x => x.Item2);
            Dictionary<int, int> oToB = ob.ToDictionary(x => x.Item1, x => x.Item2);
            List<int> anchors = Enumerable.Range(0, o.Count).Where(i => oToA.ContainsKey(i) && oToB.ContainsKey(i)).ToList();

            result = new List<string>();
            owners = new List<string>();
            int pa = 0, pb = 0, po = 0;
            foreach (int anchor in anchors.Concat(new[] { o.Count }))
            {
                int ea = anchor < o.Count ? oToA[anchor] : a.Count;
                int eb = anchor < o.Count ? oToB[anchor] : b.Count;
                List<string> oChunk = o.GetRange(po, anchor - po);
                List<string> aChunk = a.GetRange(pa, ea - pa);
                List<string> bChunk = b.GetRange(pb, eb - pb);
                List<string> aOwn = aOwners.GetRange(pa, ea - pa);

                if (bChunk.SequenceEqual(oChunk) || bChunk.SequenceEqual(aChunk))
                {
                    result.AddRange(aChunk); owners.AddRange(aOwn);
                }
                else
                {
                    if (!aChunk.SequenceEqual(oChunk))
                    {
                        string earlier = aOwn.FirstOrDefault(x => x != null);
                        if (earlier != modName)
                            conflicts.Add(new MergeConflict()
                            {
                                Target = target,
                                Where = "line " + (pa + 1) + (aChunk.Count > 0 ? " ('" + Clip(aChunk[0]) + "')" : ""),
                                Kind = ConflictKind.Overridden,
                                Kept = modName,
                                Lost = earlier ?? (applied.Count == 0 ? "an earlier mod" : string.Join(", ", applied)),
                            });
                    }
                    result.AddRange(bChunk); owners.AddRange(bChunk.Select(x => modName));
                }

                if (anchor < o.Count)
                {
                    result.Add(a[ea]); owners.Add(aOwners[ea]);
                }
                po = anchor + 1; pa = ea + 1; pb = eb + 1;
            }
        }

        private static string Clip(string s) { s = s.Trim(); return s.Length > 40 ? s.Substring(0, 37) + "..." : s; }

        private static List<(int, int)> Lcs(List<string> a, List<string> b)
        {
            //Trim the common head and tail first: config files differ in a few lines, and this keeps the table small
            int head = 0;
            while (head < a.Count && head < b.Count && a[head] == b[head]) head++;
            int tail = 0;
            while (tail < a.Count - head && tail < b.Count - head && a[a.Count - 1 - tail] == b[b.Count - 1 - tail]) tail++;
            List<(int, int)> pairs = new List<(int, int)>();
            for (int i = 0; i < head; i++) pairs.Add((i, i));

            int n = a.Count - head - tail, m = b.Count - head - tail;
            if ((long)n * m <= 40_000_000)
            {
                int[,] table = new int[n + 1, m + 1];
                for (int i = n - 1; i >= 0; i--)
                    for (int j = m - 1; j >= 0; j--)
                        table[i, j] = a[head + i] == b[head + j] ? table[i + 1, j + 1] + 1 : Math.Max(table[i + 1, j], table[i, j + 1]);
                int x = 0, y = 0;
                while (x < n && y < m)
                {
                    if (a[head + x] == b[head + y]) { pairs.Add((head + x, head + y)); x++; y++; }
                    else if (table[x + 1, y] >= table[x, y + 1]) x++;
                    else y++;
                }
            }
            for (int i = 0; i < tail; i++) pairs.Add((a.Count - tail + i, b.Count - tail + i));
            return pairs;
        }
    }
}
#endif
