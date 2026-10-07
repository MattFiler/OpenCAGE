#if ENABLE_MOD_PACKAGES
using CATHODE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace OpenCAGE.Modding.Merging
{
    /// <summary>
    /// Combine several mods' versions of one file (not a level file - levels merge as a whole, see
    /// <see cref="LevelMerge"/>). Picks a merge by what the file is; a file nothing knows how to combine goes to the
    /// last mod in the list, and that's reported.
    /// </summary>
    public static class FileMerger
    {
        public enum Kind { Bml, Xml, TextDb, Text, Pak2, Opaque }

        public static Kind Classify(string target, byte[] sample)
        {
            string upper = target.ToUpperInvariant();
            if (upper.EndsWith(".BML")) return Kind.Bml;
            if (sample != null && sample.Length >= 4 && sample[0] == 'P' && sample[1] == 'A' && sample[2] == 'K' && sample[3] == '2') return Kind.Pak2;
            if (upper.EndsWith(".XML") || upper.EndsWith(".PKG") || upper.EndsWith(".XSD")) return Kind.Xml;
            if (upper.EndsWith(".TXT"))
            {
                if (sample != null && TextDbMerge.LooksLikeTextDb(sample)) return Kind.TextDb;
                return Kind.Text;
            }
            return Kind.Opaque;
        }

        /// <summary>Whether two versions of a file can be combined rather than one replacing the other.</summary>
        public static bool CanMerge(string target, byte[] sample)
        {
            return Classify(target, sample) != Kind.Opaque;
        }

        /// <summary>
        /// The merged file, from the vanilla bytes and each mod's full version in list order (later wins clashes).
        /// Never throws for a merge that fails: it falls back to the last mod's version and says so.
        /// </summary>
        public static MergeOutcome Merge(string target, byte[] vanilla, IList<Contribution> versions)
        {
            if (versions.Count == 0)
                return new MergeOutcome() { Bytes = vanilla };
            if (versions.Count == 1)
                return new MergeOutcome() { Bytes = versions[0].Bytes };

            //A mod whose version is vanilla again contributes nothing (it shipped the file but didn't change it)
            List<Contribution> changed = versions.Where(o => vanilla == null || !o.Bytes.AsSpan().SequenceEqual(vanilla)).ToList();
            if (changed.Count == 0) return new MergeOutcome() { Bytes = vanilla };
            if (changed.Count == 1) return new MergeOutcome() { Bytes = changed[0].Bytes };
            //All the same: nothing to combine
            if (changed.All(o => o.Bytes.AsSpan().SequenceEqual(changed[0].Bytes))) return new MergeOutcome() { Bytes = changed[0].Bytes };

            if (vanilla == null)
                return WholeFile(target, changed, "the file isn't part of the game, so there's no original to merge against");

            try
            {
                switch (Classify(target, vanilla))
                {
                    case Kind.Bml: return MergeBml(target, vanilla, changed);
                    case Kind.Xml: return MergeXmlText(target, vanilla, changed);
                    case Kind.TextDb: return TextDbMerge.Merge(target, vanilla, changed);
                    case Kind.Text: return LineMerge.Merge(target, vanilla, changed);
                    case Kind.Pak2: return Pak2Merge.Merge(target, vanilla, changed);
                    default: return WholeFile(target, changed, null);
                }
            }
            catch (Exception e)
            {
                MergeOutcome outcome = WholeFile(target, changed, "merging failed: " + e.Message);
                outcome.FellBack = true;
                return outcome;
            }
        }

        /* Nothing can combine these: the last mod's file is used, and every earlier mod that changed it is told */
        public static MergeOutcome WholeFile(string target, IList<Contribution> changed, string detail)
        {
            MergeOutcome outcome = new MergeOutcome() { Bytes = changed[changed.Count - 1].Bytes };
            for (int i = 0; i < changed.Count - 1; i++)
                outcome.Conflicts.Add(new MergeConflict()
                {
                    Target = target,
                    Where = "the whole file",
                    Kind = ConflictKind.WholeFile,
                    Kept = changed[changed.Count - 1].ModName,
                    Lost = changed[i].ModName,
                    Detail = detail,
                });
            return outcome;
        }

        #region XML FAMILY
        private static MergeOutcome MergeBml(string target, byte[] vanilla, IList<Contribution> versions)
        {
            XmlDocument baseDoc = ReadBml(vanilla);
            List<(string, XmlDocument)> docs = versions.Select(o => (o.ModName, ReadBml(o.Bytes))).ToList();
            XmlMerge merge = new XmlMerge(target);
            XmlDocument merged = merge.Merge(baseDoc, docs);
            return new MergeOutcome() { Bytes = WriteBml(vanilla, merged), Conflicts = merge.Conflicts };
        }

        public static XmlDocument ReadBml(byte[] bytes)
        {
            BML bml = new BML(bytes);
            if (bml.Content == null)
                throw new Exception("not a readable BML file");
            return bml.Content;
        }

        public static byte[] WriteBml(byte[] template, XmlDocument document)
        {
            BML bml = new BML(template);
            bml.Content = document;
            string temp = Path.GetTempFileName();
            try
            {
                if (!bml.Save(temp))
                    throw new Exception("couldn't write the merged BML");
                return File.ReadAllBytes(temp);
            }
            finally
            {
                File.Delete(temp);
            }
        }

        private static MergeOutcome MergeXmlText(string target, byte[] vanilla, IList<Contribution> versions)
        {
            XmlDocument baseDoc = ReadXml(vanilla, out Encoding encoding, out bool hadDeclaration);
            List<(string, XmlDocument)> docs = versions.Select(o => (o.ModName, ReadXml(o.Bytes, out _, out _))).ToList();
            XmlMerge merge = new XmlMerge(target);
            XmlDocument merged = merge.Merge(baseDoc, docs);
            return new MergeOutcome() { Bytes = WriteXml(merged, encoding, hadDeclaration), Conflicts = merge.Conflicts };
        }

        public static XmlDocument ReadXml(byte[] bytes, out Encoding encoding, out bool hadDeclaration)
        {
            string text = TextDbMerge.Decode(bytes, out encoding);
            XmlDocument document = new XmlDocument() { PreserveWhitespace = true };
            document.LoadXml(text);
            hadDeclaration = document.FirstChild is XmlDeclaration;
            //Written back in the encoding the file says it's in (us-ascii, say), so the declaration doesn't change
            if (document.FirstChild is XmlDeclaration declaration && !string.IsNullOrEmpty(declaration.Encoding) && encoding.GetPreamble().Length == 0)
            {
                try
                {
                    Encoding declared = Encoding.GetEncoding(declaration.Encoding);
                    //GetEncoding's UTF-8 writes a byte order mark the file didn't have
                    if (declared.WebName != "utf-8")
                        encoding = declared;
                }
                catch (ArgumentException) { }
            }
            return document;
        }

        public static byte[] WriteXml(XmlDocument document, Encoding encoding, bool hadDeclaration)
        {
            //The document already carries its own whitespace; write it out exactly as it stands
            using (MemoryStream stream = new MemoryStream())
            {
                XmlWriterSettings settings = new XmlWriterSettings()
                {
                    Encoding = encoding,
                    Indent = false,
                    OmitXmlDeclaration = !hadDeclaration,
                    NewLineHandling = NewLineHandling.None,
                };
                using (XmlWriter writer = XmlWriter.Create(stream, settings))
                    document.Save(writer);
                return stream.ToArray();
            }
        }
        #endregion
    }

    /// <summary>
    /// PAK2 archives (UI.PAK, CHR_INFO.PAK, ANIMATION.PAK...): merged entry by entry. Two mods changing different
    /// entries both survive; two changing the same entry merge that entry by its own kind when it's a config,
    /// otherwise the later mod's entry is used.
    /// </summary>
    public static class Pak2Merge
    {
        public static MergeOutcome Merge(string target, byte[] vanilla, IList<Contribution> versions)
        {
            PAK2 baseArchive = new PAK2(vanilla);
            if (!baseArchive.Loaded) throw new Exception("the original isn't a readable PAK2");
            List<(string Mod, PAK2 Archive)> archives = new List<(string, PAK2)>();
            foreach (Contribution version in versions)
            {
                PAK2 archive = new PAK2(version.Bytes);
                if (!archive.Loaded) throw new Exception("'" + version.ModName + "''s version isn't a readable PAK2");
                archives.Add((version.ModName, archive));
            }

            MergeOutcome outcome = new MergeOutcome();
            Dictionary<string, PAK2.File> baseEntries = Index(baseArchive);
            List<PAK2.File> merged = baseArchive.Entries.Select(o => new PAK2.File() { Filename = o.Filename, Content = o.Content }).ToList();

            //Per entry, which mods have their own version of it
            Dictionary<string, List<Contribution>> perEntry = new Dictionary<string, List<Contribution>>(StringComparer.OrdinalIgnoreCase);
            List<(string Mod, string Name)> removals = new List<(string, string)>();
            foreach ((string mod, PAK2 archive) in archives)
            {
                Dictionary<string, PAK2.File> entries = Index(archive);
                foreach (PAK2.File entry in archive.Entries)
                {
                    baseEntries.TryGetValue(entry.Filename, out PAK2.File original);
                    if (original != null && original.Content.AsSpan().SequenceEqual(entry.Content))
                        continue;
                    if (!perEntry.TryGetValue(entry.Filename, out List<Contribution> list))
                        perEntry[entry.Filename] = list = new List<Contribution>();
                    list.Add(new Contribution() { ModName = mod, Bytes = entry.Content });
                }
                foreach (PAK2.File original in baseArchive.Entries)
                    if (!entries.ContainsKey(original.Filename))
                        removals.Add((mod, original.Filename));
            }

            foreach (KeyValuePair<string, List<Contribution>> entry in perEntry)
            {
                baseEntries.TryGetValue(entry.Key, out PAK2.File original);
                string entryTarget = target + " › " + entry.Key;
                MergeOutcome entryOutcome = original == null
                    ? FileMerger.WholeFile(entryTarget, entry.Value, entry.Value.Count > 1 ? "added by more than one mod" : null)
                    : FileMerger.Merge(entryTarget, original.Content, entry.Value);
                outcome.Conflicts.AddRange(entryOutcome.Conflicts);
                PAK2.File existing = merged.FirstOrDefault(o => string.Equals(o.Filename, entry.Key, StringComparison.OrdinalIgnoreCase));
                if (existing != null) existing.Content = entryOutcome.Bytes;
                else merged.Add(new PAK2.File() { Filename = entry.Key, Content = entryOutcome.Bytes });
            }

            foreach ((string mod, string name) in removals)
            {
                if (perEntry.TryGetValue(name, out List<Contribution> changers))
                {
                    outcome.Conflicts.Add(new MergeConflict()
                    {
                        Target = target,
                        Where = name,
                        Kind = ConflictKind.RemovedVsChanged,
                        Kept = changers[changers.Count - 1].ModName,
                        Lost = mod,
                        Detail = "'" + mod + "' removes it but another mod changes it, so it's kept",
                    });
                    continue;
                }
                merged.RemoveAll(o => string.Equals(o.Filename, name, StringComparison.OrdinalIgnoreCase));
            }

            PAK2 result = new PAK2(vanilla);
            result.Entries.Clear();
            result.Entries.AddRange(merged);
            string temp = Path.GetTempFileName();
            try
            {
                if (!result.Save(temp))
                    throw new Exception("couldn't write the merged PAK2");
                outcome.Bytes = File.ReadAllBytes(temp);
            }
            finally
            {
                File.Delete(temp);
            }
            return outcome;
        }

        private static Dictionary<string, PAK2.File> Index(PAK2 archive)
        {
            Dictionary<string, PAK2.File> index = new Dictionary<string, PAK2.File>(StringComparer.OrdinalIgnoreCase);
            foreach (PAK2.File entry in archive.Entries)
                if (!index.ContainsKey(entry.Filename))
                    index[entry.Filename] = entry;
            return index;
        }
    }
}
#endif
