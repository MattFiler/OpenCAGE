#if ENABLE_MOD_PACKAGES
using CATHODE;
using CATHODE.Animations;
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
    /// otherwise the later mod's entry is used. An animation PAK's tree layouts (OpenCAGE's own entry) are worked out last,
    /// from the trees that end up installed (<see cref="AnimTreeLayoutsMerge"/>).
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
                    //The tree layouts go last, once it's known which version of each tree is installed
                    if (AnimTreeLayouts.IsEntry(entry.Filename))
                        continue;
                    baseEntries.TryGetValue(entry.Filename, out PAK2.File original);
                    if (original != null && original.Content.AsSpan().SequenceEqual(entry.Content))
                        continue;
                    if (!perEntry.TryGetValue(entry.Filename, out List<Contribution> list))
                        perEntry[entry.Filename] = list = new List<Contribution>();
                    list.Add(new Contribution() { ModName = mod, Bytes = entry.Content });
                }
                foreach (PAK2.File original in baseArchive.Entries)
                    if (!entries.ContainsKey(original.Filename) && !AnimTreeLayouts.IsEntry(original.Filename))
                        removals.Add((mod, original.Filename));
            }

            foreach (KeyValuePair<string, List<Contribution>> entry in perEntry)
            {
                baseEntries.TryGetValue(entry.Key, out PAK2.File original);
                string entryTarget = target + " › " + entry.Key;
                MergeOutcome entryOutcome;
                if (original == null)
                    entryOutcome = entry.Value.All(o => o.Bytes.AsSpan().SequenceEqual(entry.Value[0].Bytes))
                        ? new MergeOutcome() { Bytes = entry.Value[0].Bytes } //added the same by every one: nothing to choose between
                        : FileMerger.WholeFile(entryTarget, entry.Value, "added by more than one mod");
                else
                    entryOutcome = (AnimationStringsMerge.IsTable(entry.Key) ? AnimationStringsMerge.Merge(entryTarget, original.Content, entry.Value) : null)
                        ?? FileMerger.Merge(entryTarget, original.Content, entry.Value);
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

            AnimTreeLayoutsMerge.Into(target, baseArchive, archives, merged, outcome);

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

    /// <summary>
    /// An animation PAK's string tables (ANIM_STRING_DB.BIN, and ANIM_STRING_DB_DEBUG.BIN that the tree sets name themselves
    /// from): everything in the PAK stores names as their hashes, and a mod adding trees, nodes or clips adds their names.
    /// Every mod's names are kept - a name a mod's file needs that isn't in the table makes it read back as a number, and a
    /// tree set missing one of its trees' names doesn't load at all. A hash two mods name differently goes to the later.
    /// </summary>
    public static class AnimationStringsMerge
    {
        public static bool IsTable(string entryName)
        {
            string name = entryName ?? "";
            return name.EndsWith("ANIM_STRING_DB.BIN", StringComparison.OrdinalIgnoreCase) || name.EndsWith("ANIM_STRING_DB_DEBUG.BIN", StringComparison.OrdinalIgnoreCase);
        }

        /// <returns>The tables combined, or null when one can't be read (the caller then takes one whole).</returns>
        public static MergeOutcome Merge(string target, byte[] original, IList<Contribution> versions)
        {
            if (versions.Count == 1 || versions.All(o => o.Bytes.AsSpan().SequenceEqual(versions[0].Bytes)))
                return new MergeOutcome() { Bytes = versions[0].Bytes };
            try
            {
                //Onto the original as it reads, so its own order is kept and only what the mods add is new
                AnimationStrings merged = new AnimationStrings(original);
                if (!merged.Loaded)
                    return null;
                MergeOutcome outcome = new MergeOutcome();
                Dictionary<uint, string> claims = new Dictionary<uint, string>();
                foreach (Contribution version in versions)
                {
                    AnimationStrings strings = new AnimationStrings(version.Bytes);
                    if (!strings.Loaded)
                        return null;
                    foreach (KeyValuePair<uint, string> entry in strings.Entries)
                    {
                        if (merged.Entries.TryGetValue(entry.Key, out string had) && had == entry.Value)
                            continue;
                        if (had != null && claims.TryGetValue(entry.Key, out string earlier) && earlier != version.ModName)
                            outcome.Conflicts.Add(new MergeConflict() { Target = target, Where = "the name with hash " + entry.Key, Kind = ConflictKind.Overridden, Kept = version.ModName, Lost = earlier, Detail = "'" + entry.Value + "' instead of '" + had + "'" });
                        merged.Entries[entry.Key] = entry.Value;
                        claims[entry.Key] = version.ModName;
                    }
                }
                outcome.Bytes = merged.ToBytes();
                return outcome;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// The Animation Tree Editor's layouts (an animation PAK's <see cref="AnimTreeLayouts.EntryName"/> entry), worked out once
    /// every other entry of the PAK is merged. A layout draws one version of its tree, so each tree's layout comes from the
    /// latest version - the PAK as it was before the mods, then the user's own, then each mod in list order - whose own copy
    /// of that tree is the one installed. A layout of a tree another mod's version replaced (or took away) goes, so it can't
    /// be drawn over a tree it wasn't made for; the user's layouts of trees no mod changes stay while mods are installed.
    /// Two mods laying out the same installed tree differently is reported (the later wins); the user's own give way quietly.
    /// Layouts this build can't read are left out, as it couldn't draw them anyway.
    /// </summary>
    public static class AnimTreeLayoutsMerge
    {
        /// <summary>
        /// Put the merged layouts into <paramref name="merged"/> (the merged PAK's entries, every other one final), or take
        /// them out when no tree has one. <paramref name="versions"/> are the full PAKs in list order.
        /// </summary>
        public static void Into(string target, PAK2 original, IList<(string Mod, PAK2 Archive)> versions, List<PAK2.File> merged, MergeOutcome outcome)
        {
            string entryTarget = target + " › " + AnimTreeLayouts.EntryName;
            List<(string Mod, PAK2 Archive)> sources = new List<(string, PAK2)>() { (null, original) };
            sources.AddRange(versions);

            TreeVersions installed = new TreeVersions(merged);
            AnimTreeLayouts result = AnimTreeLayouts.FromBytes(null);
            //Per tree, the last mod to lay it out and its record as written - the same layout twice isn't a clash
            Dictionary<(uint, uint), (string Mod, byte[] Record)> claims = new Dictionary<(uint, uint), (string, byte[])>();
            foreach ((string mod, PAK2 archive) in sources)
            {
                PAK2.File entry = archive.Entries.FirstOrDefault(o => AnimTreeLayouts.IsEntry(o.Filename));
                if (entry == null || !TryRead(entry.Content, out AnimTreeLayouts layouts))
                    continue;
                TreeVersions drawnOn = new TreeVersions(archive.Entries);
                foreach (AnimTreeLayouts.TreeLayout tree in layouts.Trees)
                {
                    if (tree == null || !installed.Same(drawnOn, tree.SetHash, tree.TreeHash))
                        continue;
                    result.Put(tree);
                    if (mod == null || mod == MergeConflict.OwnChanges)
                        continue;
                    byte[] record = RecordOf(tree);
                    if (claims.TryGetValue((tree.SetHash, tree.TreeHash), out (string Mod, byte[] Record) earlier) && earlier.Mod != mod && !earlier.Record.AsSpan().SequenceEqual(record))
                        outcome.Conflicts.Add(new MergeConflict() { Target = entryTarget, Where = Describe(tree), Kind = ConflictKind.Overridden, Kept = mod, Lost = earlier.Mod });
                    claims[(tree.SetHash, tree.TreeHash)] = (mod, record);
                }
            }

            //One entry, where the original had it (or at the end), and none at all when no tree has a layout
            int at = merged.FindIndex(o => AnimTreeLayouts.IsEntry(o.Filename));
            merged.RemoveAll(o => AnimTreeLayouts.IsEntry(o.Filename));
            if (result.Trees.Count == 0)
                return;
            PAK2.File layoutsEntry = new PAK2.File() { Filename = AnimTreeLayouts.EntryName, Content = result.ToBytes() };
            if (at >= 0) merged.Insert(at, layoutsEntry);
            else merged.Add(layoutsEntry);
        }

        /// <summary>Layouts from a file's bytes: no bytes are no layouts; anything else has to read as a layouts file this build understands.</summary>
        public static bool TryRead(byte[] bytes, out AnimTreeLayouts layouts)
        {
            layouts = AnimTreeLayouts.FromBytes(null);
            if (bytes == null || bytes.Length == 0)
                return true;
            try
            {
                layouts = AnimTreeLayouts.FromBytes(bytes);
                return layouts.Loaded;
            }
            catch
            {
                //A CathodeLib built to fail hard throws on a cut-short file instead of saying it didn't load
                return false;
            }
        }

        /// <summary>
        /// The tree sets in one version of an animation PAK, to tell whether a tree is the same in two versions. The same set
        /// file holds the same trees; otherwise each tree is compared alone, as its set file writes it - a set holds many trees
        /// (HUMANOID most of the game's), and a mod changing one of them leaves the rest as they were.
        /// </summary>
        private class TreeVersions
        {
            private readonly IEnumerable<PAK2.File> _entries;
            private Dictionary<uint, PAK2.File> _sets;
            private AnimationStrings _strings;
            private bool _stringsRead;
            private readonly Dictionary<uint, Dictionary<uint, byte[]>> _records = new Dictionary<uint, Dictionary<uint, byte[]>>();

            public TreeVersions(IEnumerable<PAK2.File> entries) { _entries = entries; }

            /// <summary>Is the tree in <paramref name="other"/> this one? Not when either has no such set or tree, or it can't be read.</summary>
            public bool Same(TreeVersions other, uint setHash, uint treeHash)
            {
                PAK2.File mine = Set(setHash), theirs = other.Set(setHash);
                if (mine?.Content == null || theirs?.Content == null)
                    return false;
                if (ReferenceEquals(mine.Content, theirs.Content) || mine.Content.AsSpan().SequenceEqual(theirs.Content))
                    return true;
                byte[] a = null, b = null;
                return (Records(setHash)?.TryGetValue(treeHash, out a) ?? false) && (other.Records(setHash)?.TryGetValue(treeHash, out b) ?? false) && a.AsSpan().SequenceEqual(b);
            }

            /* The set's file, named by the set's hash as AnimTreeLayouts.SetHashOf reads it: DATA\ANIM_SYS\<hash>_ANIM_TREE_DB.BIN */
            private PAK2.File Set(uint setHash)
            {
                if (_sets == null)
                {
                    _sets = new Dictionary<uint, PAK2.File>();
                    foreach (PAK2.File entry in _entries)
                    {
                        //By hand rather than with Path: an entry's name is whatever its PAK says, path characters or not
                        string name = (entry.Filename ?? "").Replace('/', '\\');
                        if (!name.EndsWith("_ANIM_TREE_DB.BIN", StringComparison.OrdinalIgnoreCase))
                            continue;
                        string file = name.Substring(name.LastIndexOf('\\') + 1);
                        int underscore = file.IndexOf('_');
                        if (underscore > 0 && uint.TryParse(file.Substring(0, underscore), out uint hash) && !_sets.ContainsKey(hash))
                            _sets[hash] = entry;
                    }
                }
                return _sets.TryGetValue(setHash, out PAK2.File set) ? set : null;
            }

            /* Each tree of a set as the set file writes it on its own, by its name's hash as layouts key it (null when the set can't be read) */
            private Dictionary<uint, byte[]> Records(uint setHash)
            {
                if (_records.TryGetValue(setHash, out Dictionary<uint, byte[]> records))
                    return records;
                records = null;
                AnimationStrings strings = Strings();
                PAK2.File set = Set(setHash);
                if (strings != null && set != null)
                {
                    try
                    {
                        AnimTreeDB database = new AnimTreeDB(set.Content, strings, set.Filename);
                        if (database.Loaded)
                        {
                            records = new Dictionary<uint, byte[]>();
                            foreach (AnimationTree tree in database.Entries.ToList())
                            {
                                uint treeHash = AnimTreeLayouts.TreeHashOf(tree, strings);
                                if (records.ContainsKey(treeHash))
                                    continue;
                                try
                                {
                                    database.Entries = new List<AnimationTree>() { tree };
                                    records[treeHash] = database.ToBytes();
                                }
                                catch { } //a tree that can't be written alone matches nothing
                            }
                        }
                    }
                    catch
                    {
                        records = null;
                    }
                }
                _records[setHash] = records;
                return records;
            }

            /* The debug string table the tree sets name themselves from */
            private AnimationStrings Strings()
            {
                if (_stringsRead)
                    return _strings;
                _stringsRead = true;
                PAK2.File entry = _entries.FirstOrDefault(o => (o.Filename ?? "").EndsWith("ANIM_STRING_DB_DEBUG.BIN", StringComparison.OrdinalIgnoreCase));
                try
                {
                    AnimationStrings strings = entry?.Content == null ? null : new AnimationStrings(entry.Content, entry.Filename);
                    _strings = strings != null && strings.Loaded ? strings : null;
                }
                catch
                {
                    _strings = null;
                }
                return _strings;
            }
        }

        /* One tree's layout as the file writes it, every list in the file's fixed order: equal bytes, same layout. Where
           each mod's author left the view (pan and zoom) is not part of the layout, so it is left out. */
        private static byte[] RecordOf(AnimTreeLayouts.TreeLayout tree)
        {
            AnimTreeLayouts.TreeLayout drawn = tree.Clone();
            drawn.CanvasX = 0;
            drawn.CanvasY = 0;
            drawn.CanvasScale = 0;
            AnimTreeLayouts single = AnimTreeLayouts.FromBytes(null);
            single.Trees.Add(drawn);
            return single.ToBytes();
        }

        /* "the layout of LOCOMOTION (ALIEN)", by the names stored with it - its hashes when it has none */
        private static string Describe(AnimTreeLayouts.TreeLayout tree)
        {
            string name = string.IsNullOrEmpty(tree.Tree) ? "tree 0x" + tree.TreeHash.ToString("X8") : tree.Tree;
            string set = string.IsNullOrEmpty(tree.Set) ? "set 0x" + tree.SetHash.ToString("X8") : tree.Set;
            return "the layout of " + name + " (" + set + ")";
        }
    }
}
#endif
