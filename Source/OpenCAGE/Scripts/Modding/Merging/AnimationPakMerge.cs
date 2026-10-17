#if ENABLE_MOD_PACKAGES
using CATHODE;
using CATHODE.Animations;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace OpenCAGE.Modding.Merging
{
    /// <summary>
    /// One version of an animation PAK (DATA/GLOBAL/ANIMATION.PAK), read as far as combining mods and choosing what goes in a
    /// mod need: its string tables, and on demand its tree sets and clip databases. Everything in the PAK names things by the
    /// hashes of their names, and the names live in the string tables - so each version's files are read with its own tables.
    /// </summary>
    public class AnimationPakVersion
    {
        public const string DebugStringsSuffix = "ANIM_STRING_DB_DEBUG.BIN";
        public const string StringsSuffix = "ANIM_STRING_DB.BIN";

        public readonly IList<PAK2.File> Entries;
        private readonly Dictionary<string, PAK2.File> _byName = new Dictionary<string, PAK2.File>(StringComparer.OrdinalIgnoreCase);
        private AnimationStrings _debug, _plain;
        private bool _stringsRead;

        public AnimationPakVersion(IList<PAK2.File> entries)
        {
            Entries = entries;
            foreach (PAK2.File entry in entries)
                if (entry?.Filename != null && !_byName.ContainsKey(Key(entry.Filename)))
                    _byName[Key(entry.Filename)] = entry;
        }

        public static string Key(string entryName) => (entryName ?? "").Replace('/', '\\');

        public PAK2.File Entry(string entryName) => _byName.TryGetValue(Key(entryName), out PAK2.File entry) ? entry : null;

        public byte[] Content(string entryName) => Entry(entryName)?.Content;

        /// <summary>The debug string table, which tree sets and clip databases take their names from (null if unreadable).</summary>
        public AnimationStrings Debug { get { ReadStrings(); return _debug; } }

        /// <summary>The plain string table, falling back to the debug one as the game's own files expect.</summary>
        public AnimationStrings Plain { get { ReadStrings(); return _plain; } }

        private void ReadStrings()
        {
            if (_stringsRead)
                return;
            _stringsRead = true;
            _debug = ReadStringTable(Entries.FirstOrDefault(o => (o?.Filename ?? "").EndsWith(DebugStringsSuffix, StringComparison.OrdinalIgnoreCase)));
            _plain = ReadStringTable(Entries.FirstOrDefault(o => (o?.Filename ?? "").EndsWith(StringsSuffix, StringComparison.OrdinalIgnoreCase)
                                                                && !(o.Filename ?? "").EndsWith(DebugStringsSuffix, StringComparison.OrdinalIgnoreCase)));
            if (_plain != null && _plain.Fallback == null)
                _plain.Fallback = _debug;
        }

        private static AnimationStrings ReadStringTable(PAK2.File entry)
        {
            try
            {
                AnimationStrings strings = entry?.Content == null ? null : new AnimationStrings(entry.Content, entry.Filename);
                return strings != null && strings.Loaded ? strings : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Every name any of these versions knows, under the hash it has there - so names read from one version write back to
        /// the same hashes whichever version's file they end up in. The first version's own table is the starting point.
        /// </summary>
        public static AnimationStrings Union(IEnumerable<AnimationStrings> tables)
        {
            List<AnimationStrings> all = tables.Where(o => o != null).ToList();
            if (all.Count == 0)
                return null;
            AnimationStrings union = new AnimationStrings(new byte[8]); //an empty table: no ids, no strings
            foreach (AnimationStrings table in all)
            {
                foreach (KeyValuePair<uint, string> entry in table.Entries)
                    if (!union.Entries.ContainsKey(entry.Key))
                        union.Entries.Add(entry.Key, entry.Value);
                if (table.Fallback != null)
                    foreach (KeyValuePair<uint, string> entry in table.Fallback.Entries)
                        if (!union.Entries.ContainsKey(entry.Key))
                            union.Entries.Add(entry.Key, entry.Value);
            }
            return union;
        }
    }

    /// <summary>
    /// The entries of an animation PAK that hold many separate things, merged thing by thing instead of the later mod's entry
    /// winning outright:
    ///   - a tree set (<c>&lt;hash&gt;_ANIM_TREE_DB.BIN</c>) tree by tree - two mods changing different trees of HUMANOID both
    ///     keep their trees;
    ///   - a character's clip database (<c>&lt;hash&gt;_ANIM_CLIP_DB.BIN</c>) clip line by clip line, and the global clip index
    ///     (<c>ANIM_SYS\ANIM_CLIP_DB.BIN</c>) entry by entry and blend set by blend set - so two mods importing clips into the
    ///     same character both keep them. A clip's own data is an entry of its own named by its path, which already combines;
    ///     these two lists are what point at it, and merging them is what re-running each mod's import would do to them.
    /// Anything one of these can't read, or a change to parts of a file it doesn't take apart, gives null: the entry is then
    /// merged as a whole, the later mod's winning, as before.
    /// </summary>
    public static class AnimationPakMerge
    {
        public static string FileOf(string entryName)
        {
            string name = AnimationPakVersion.Key(entryName);
            return name.Substring(name.LastIndexOf('\\') + 1);
        }

        public static bool IsTreeSet(string entryName) => FileOf(entryName).EndsWith("_ANIM_TREE_DB.BIN", StringComparison.OrdinalIgnoreCase);

        public static bool IsGlobalClipDb(string entryName) => string.Equals(FileOf(entryName), "ANIM_CLIP_DB.BIN", StringComparison.OrdinalIgnoreCase);

        public static bool IsSetClipDb(string entryName)
        {
            string file = FileOf(entryName);
            return file.EndsWith("_ANIM_CLIP_DB.BIN", StringComparison.OrdinalIgnoreCase) && !IsGlobalClipDb(entryName);
        }

        public static bool IsClipSection(string entryName) => FileOf(entryName).StartsWith("ANIM_CLIP_DB_SEC_", StringComparison.OrdinalIgnoreCase);

        public static bool Handles(string entryName) => IsTreeSet(entryName) || IsSetClipDb(entryName) || IsGlobalClipDb(entryName);

        /// <summary>
        /// Merge one entry of the PAK from the versions that change it (in list order), or null when it can't be merged by its
        /// parts. <paramref name="original"/> is the PAK the versions were made from.
        /// </summary>
        public static MergeOutcome Merge(string entryTarget, string entryName, AnimationPakVersion original, IList<(string Mod, AnimationPakVersion Version)> versions)
        {
            try
            {
                if (IsTreeSet(entryName)) return MergeTrees(entryTarget, entryName, original, versions);
                if (IsSetClipDb(entryName)) return MergeSetClips(entryTarget, entryName, original, versions);
                if (IsGlobalClipDb(entryName)) return MergeGlobalClips(entryTarget, entryName, original, versions);
            }
            catch
            {
                //A file that reads but can't be taken apart this way merges as a whole
            }
            return null;
        }

        #region CLAIMS
        /* Who has each part, in list order: a later version takes a part from an earlier one (reported unless it's the same
           change), a removal loses to a change. Parts are keyed by whatever identifies them in the file. */
        private class Claims<TValue> where TValue : class
        {
            public readonly Dictionary<string, (string Mod, TValue Value, string Signature, string What)> Changed = new Dictionary<string, (string, TValue, string, string)>(StringComparer.Ordinal);
            public readonly Dictionary<string, (string Mod, string What)> Removed = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
            public readonly List<string> Order = new List<string>();

            public void Change(MergeOutcome outcome, string target, string key, string mod, TValue value, string signature, string what)
            {
                if (Changed.TryGetValue(key, out var earlier) && earlier.Mod != mod && earlier.Signature != signature)
                    outcome.Conflicts.Add(new MergeConflict() { Target = target, Where = what, Kind = ConflictKind.Overridden, Kept = mod, Lost = earlier.Mod });
                if (Removed.TryGetValue(key, out var removal))
                {
                    outcome.Conflicts.Add(new MergeConflict() { Target = target, Where = what, Kind = ConflictKind.RemovedVsChanged, Kept = mod, Lost = removal.Mod, Detail = "'" + removal.Mod + "' removes it but another mod changes it, so it's kept" });
                    Removed.Remove(key);
                }
                if (!Changed.ContainsKey(key))
                    Order.Add(key);
                Changed[key] = (mod, value, signature, what);
            }

            public void Remove(MergeOutcome outcome, string target, string key, string mod, string what)
            {
                if (Changed.TryGetValue(key, out var change))
                {
                    outcome.Conflicts.Add(new MergeConflict() { Target = target, Where = what, Kind = ConflictKind.RemovedVsChanged, Kept = change.Mod, Lost = mod, Detail = "'" + mod + "' removes it but another mod changes it, so it's kept" });
                    return;
                }
                Removed[key] = (mod, what);
            }
        }
        #endregion

        #region TREES
        /// <summary>Each tree of a set by the hash of its name, with the tree as the set file writes it alone (null if unreadable).</summary>
        public static Dictionary<uint, (AnimationTree Tree, byte[] Record)> Trees(byte[] content, AnimationStrings strings, string entryName)
        {
            if (content == null || strings == null)
                return null;
            AnimTreeDB database = new AnimTreeDB(content, strings, entryName);
            if (!database.Loaded)
                return null;
            List<AnimationTree> all = database.Entries.ToList();
            Dictionary<uint, (AnimationTree, byte[])> trees = new Dictionary<uint, (AnimationTree, byte[])>();
            try
            {
                foreach (AnimationTree tree in all)
                {
                    uint hash = strings.GetID(tree.Name);
                    if (trees.ContainsKey(hash))
                        continue;
                    database.Entries = new List<AnimationTree>() { tree };
                    trees[hash] = (tree, database.ToBytes());
                }
            }
            finally
            {
                database.Entries = all;
            }
            return trees;
        }

        private static MergeOutcome MergeTrees(string target, string entryName, AnimationPakVersion original, IList<(string Mod, AnimationPakVersion Version)> versions)
        {
            byte[] baseBytes = original.Content(entryName);
            AnimationStrings union = AnimationPakVersion.Union(new[] { original.Debug }.Concat(versions.Select(o => o.Version.Debug)));
            Dictionary<uint, (AnimationTree Tree, byte[] Record)> baseTrees = Trees(baseBytes, original.Debug, entryName);
            if (baseTrees == null || union == null)
                return null;

            MergeOutcome outcome = new MergeOutcome();
            Claims<AnimationTree> claims = new Claims<AnimationTree>();
            foreach ((string mod, AnimationPakVersion version) in versions)
            {
                Dictionary<uint, (AnimationTree Tree, byte[] Record)> trees = Trees(version.Content(entryName), version.Debug, entryName);
                if (trees == null)
                    return null;
                foreach (KeyValuePair<uint, (AnimationTree Tree, byte[] Record)> tree in trees)
                {
                    if (baseTrees.TryGetValue(tree.Key, out var before) && before.Record.AsSpan().SequenceEqual(tree.Value.Record))
                        continue;
                    claims.Change(outcome, target, tree.Key.ToString(CultureInfo.InvariantCulture), mod, tree.Value.Tree, Convert.ToBase64String(tree.Value.Record), "the tree " + tree.Value.Tree.Name);
                }
                foreach (KeyValuePair<uint, (AnimationTree Tree, byte[] Record)> tree in baseTrees.Where(o => !trees.ContainsKey(o.Key)))
                    claims.Remove(outcome, target, tree.Key.ToString(CultureInfo.InvariantCulture), mod, "the tree " + tree.Value.Tree.Name);
            }

            //Onto the original set as it reads with every version's names, so every tree writes back under the hashes it had
            AnimTreeDB merged = new AnimTreeDB(baseBytes, union, entryName);
            if (!merged.Loaded)
                return null;
            List<AnimationTree> result = new List<AnimationTree>();
            HashSet<string> placed = new HashSet<string>();
            foreach (AnimationTree tree in merged.Entries)
            {
                string key = union.GetID(tree.Name).ToString(CultureInfo.InvariantCulture);
                placed.Add(key);
                if (claims.Changed.TryGetValue(key, out var change)) result.Add(change.Value);
                else if (!claims.Removed.ContainsKey(key)) result.Add(tree);
            }
            foreach (string key in claims.Order.Where(o => !placed.Contains(o)))
                result.Add(claims.Changed[key].Value);
            merged.Entries = result;
            outcome.Bytes = merged.ToBytes();
            return outcome;
        }
        #endregion

        #region A CHARACTER'S CLIPS
        /* One line of a character's clip database: the context it's in ("" for the character's own list) and its name */
        internal static string LineKey(string context, string kind, uint nameHash) => kind + "|" + (context ?? "") + "|" + nameHash.ToString(CultureInfo.InvariantCulture);

        internal class ClipLines
        {
            public readonly Dictionary<string, (string Context, AnimClipDB.AnimClip Clip, AnimClipDB.BlendSet Blend, string Signature, string What)> Lines
                = new Dictionary<string, (string, AnimClipDB.AnimClip, AnimClipDB.BlendSet, string, string)>(StringComparer.Ordinal);
            public string Shape;   //what isn't taken apart line by line: the character and its contexts
        }

        internal static ClipLines LinesOf(AnimClipDB database, AnimationStrings strings)
        {
            ClipLines lines = new ClipLines();
            lines.Shape = strings.GetID(database.Character) + "|" + string.Join(",", database.Contexts.Select(o => strings.GetID(o.Name ?? "")));
            void Add(string context, List<AnimClipDB.AnimClip> clips, List<AnimClipDB.BlendSet> blends)
            {
                foreach (AnimClipDB.AnimClip clip in clips)
                {
                    string key = LineKey(context, "clip", strings.GetID(clip.Name));
                    if (!lines.Lines.ContainsKey(key))
                        lines.Lines[key] = (context, clip, null, strings.GetID(clip.Path) + "|" + clip.MetadataInstance, "the animation " + clip.Name + (string.IsNullOrEmpty(context) ? "" : " (" + context + ")"));
                }
                foreach (AnimClipDB.BlendSet blend in blends)
                {
                    string key = LineKey(context, "blend", strings.GetID(blend.Name));
                    if (!lines.Lines.ContainsKey(key))
                        lines.Lines[key] = (context, null, blend, strings.GetID(blend.Filename).ToString(CultureInfo.InvariantCulture), "the blend set " + blend.Name + (string.IsNullOrEmpty(context) ? "" : " (" + context + ")"));
                }
            }
            Add("", database.Animations, database.BlendSets);
            foreach (AnimClipDB.Context context in database.Contexts)
                Add(context.Name ?? "", context.Animations, context.BlendSets);
            return lines;
        }

        private static MergeOutcome MergeSetClips(string target, string entryName, AnimationPakVersion original, IList<(string Mod, AnimationPakVersion Version)> versions)
        {
            byte[] baseBytes = original.Content(entryName);
            AnimationStrings union = AnimationPakVersion.Union(new[] { original.Plain, original.Debug }.Concat(versions.SelectMany(o => new[] { o.Version.Plain, o.Version.Debug })));
            if (baseBytes == null || original.Plain == null || union == null)
                return null;
            AnimClipDB baseDb = new AnimClipDB(baseBytes, original.Plain, entryName, original.Debug);
            if (!baseDb.Loaded)
                return null;
            ClipLines baseLines = LinesOf(baseDb, original.Plain);

            MergeOutcome outcome = new MergeOutcome();
            Claims<object> claims = new Claims<object>();
            foreach ((string mod, AnimationPakVersion version) in versions)
            {
                if (version.Plain == null)
                    return null;
                AnimClipDB db = new AnimClipDB(version.Content(entryName), version.Plain, entryName, version.Debug);
                if (!db.Loaded)
                    return null;
                ClipLines lines = LinesOf(db, version.Plain);
                if (lines.Shape != baseLines.Shape)
                    return null; //a character or its contexts changed: not something lines can say
                foreach (var line in lines.Lines)
                {
                    if (baseLines.Lines.TryGetValue(line.Key, out var before) && before.Signature == line.Value.Signature)
                        continue;
                    claims.Change(outcome, target, line.Key, mod, (object)line.Value.Clip ?? line.Value.Blend, line.Value.Signature, line.Value.What);
                }
                foreach (var line in baseLines.Lines.Where(o => !lines.Lines.ContainsKey(o.Key)))
                    claims.Remove(outcome, target, line.Key, mod, line.Value.What);
            }

            AnimClipDB merged = new AnimClipDB(baseBytes, union, entryName, null);
            if (!merged.Loaded)
                return null;
            void Apply(string context, List<AnimClipDB.AnimClip> clips, List<AnimClipDB.BlendSet> blends)
            {
                HashSet<string> present = new HashSet<string>();
                for (int i = clips.Count - 1; i >= 0; i--)
                {
                    string key = LineKey(context, "clip", union.GetID(clips[i].Name));
                    present.Add(key);
                    if (claims.Changed.TryGetValue(key, out var change) && change.Value is AnimClipDB.AnimClip changed)
                        clips[i] = new AnimClipDB.AnimClip() { Name = changed.Name, Path = changed.Path, MetadataInstance = changed.MetadataInstance };
                    else if (claims.Removed.ContainsKey(key))
                        clips.RemoveAt(i);
                }
                for (int i = blends.Count - 1; i >= 0; i--)
                {
                    string key = LineKey(context, "blend", union.GetID(blends[i].Name));
                    present.Add(key);
                    if (claims.Changed.TryGetValue(key, out var change) && change.Value is AnimClipDB.BlendSet changed)
                        blends[i] = new AnimClipDB.BlendSet() { Name = changed.Name, Filename = changed.Filename };
                    else if (claims.Removed.ContainsKey(key))
                        blends.RemoveAt(i);
                }
                //Lines a version added to this list, in the order they were first added
                foreach (string key in claims.Order.Where(o => !present.Contains(o)))
                {
                    var change = claims.Changed[key];
                    if (!key.StartsWith((change.Value is AnimClipDB.AnimClip ? "clip" : "blend") + "|" + context + "|", StringComparison.Ordinal))
                        continue;
                    if (change.Value is AnimClipDB.AnimClip clip)
                        clips.Add(new AnimClipDB.AnimClip() { Name = clip.Name, Path = clip.Path, MetadataInstance = clip.MetadataInstance });
                    else if (change.Value is AnimClipDB.BlendSet blend)
                        blends.Add(new AnimClipDB.BlendSet() { Name = blend.Name, Filename = blend.Filename });
                }
            }
            Apply("", merged.Animations, merged.BlendSets);
            foreach (AnimClipDB.Context context in merged.Contexts)
                Apply(context.Name ?? "", context.Animations, context.BlendSets);
            outcome.Bytes = merged.ToBytes();
            return outcome;
        }
        #endregion

        #region THE GLOBAL CLIP INDEX
        /* What the index holds that isn't taken apart part by part - the dependency tables nothing in OpenCAGE edits */
        internal static string GlobalShape(GlobalAnimClipDB db, AnimationStrings strings)
        {
            StringBuilder shape = new StringBuilder();
            foreach (GlobalAnimClipDB.DependencyRange range in db.DependencyMap)
                shape.Append(strings.GetID(range.Name)).Append(':').Append(range.FirstEntryIndex).Append(':').Append(range.EntryCount).Append(';');
            shape.Append('|').Append(string.Join(",", db.SectionDependencyList));
            shape.Append('|').Append(string.Join(",", db.ParentDependencies.Select(o => strings.GetID(o.Name))));
            return shape.ToString();
        }

        /* A blend set as its fields say - two that read the same are the same */
        public static string BlendSignature(GlobalAnimClipDB.BlendSet blend, AnimationStrings strings)
        {
            StringBuilder s = new StringBuilder();
            s.Append(blend.Version).Append('|').Append(strings.GetID(blend.Name)).Append('|').Append(strings.GetID(blend.AnimSet)).Append('|').Append(strings.GetID(blend.AnimSetContext)).Append('|').Append(blend.Dimensions);
            foreach (GlobalAnimClipDB.BlendClip clip in blend.Clips)
                s.Append('|').Append(strings.GetID(clip.Name)).Append(':').Append(Convert.ToBase64String(clip.Leading)).Append(':').Append(Convert.ToBase64String(clip.Trailing));
            s.Append('|').Append(strings.GetID(blend.BlendPropertyX)).Append(',').Append(strings.GetID(blend.BlendPropertyY)).Append(',').Append(strings.GetID(blend.BlendPropertyZ));
            s.Append('|').Append(blend.CellOrigin.ToString()).Append(blend.CellUnit.ToString()).Append(blend.CellCount).Append(',').Append(blend.CellStride).Append(',').Append(blend.CellDepth);
            s.Append('|').Append(string.Join(",", blend.InstanceProperties.Select(o => BitConverter.ToString(BitConverter.GetBytes(o)))));
            foreach (GlobalAnimClipDB.BlendVertex vertex in blend.Vertices)
                s.Append('|').Append(Convert.ToBase64String(vertex.Instances)).Append(':').Append(Convert.ToBase64String(vertex.Weights));
            s.Append('|').Append(string.Join(",", blend.PlaySpeeds.Select(o => BitConverter.ToString(BitConverter.GetBytes(o)))));
            s.Append('|').Append(Convert.ToBase64String(blend.InstanceToClip));
            s.Append('|').Append(string.Join(",", blend.Durations.Select(o => BitConverter.ToString(BitConverter.GetBytes(o)))));
            s.Append('|').Append(string.Join(",", blend.Mirrored.Select(o => o ? "1" : "0")));
            return s.ToString();
        }

        public static string BlendKey(GlobalAnimClipDB.BlendSet blend, AnimationStrings strings) =>
            strings.GetID(blend.AnimSet) + "|" + strings.GetID(blend.AnimSetContext) + "|" + strings.GetID(blend.Name);

        private static MergeOutcome MergeGlobalClips(string target, string entryName, AnimationPakVersion original, IList<(string Mod, AnimationPakVersion Version)> versions)
        {
            byte[] baseBytes = original.Content(entryName);
            AnimationStrings union = AnimationPakVersion.Union(new[] { original.Plain, original.Debug }.Concat(versions.SelectMany(o => new[] { o.Version.Plain, o.Version.Debug })));
            if (baseBytes == null || original.Plain == null || union == null)
                return null;
            GlobalAnimClipDB baseDb = new GlobalAnimClipDB(baseBytes, original.Plain, entryName, original.Debug);
            if (!baseDb.Loaded)
                return null;
            string baseShape = GlobalShape(baseDb, original.Plain);
            Dictionary<string, (GlobalAnimClipDB.ClipDbSection Section, string Signature)> baseSections = Sections(baseDb, original.Plain);
            Dictionary<string, (GlobalAnimClipDB.BlendSet Blend, string Signature)> baseBlends = Blends(baseDb, original.Plain);

            MergeOutcome outcome = new MergeOutcome();
            Claims<object> claims = new Claims<object>();
            foreach ((string mod, AnimationPakVersion version) in versions)
            {
                if (version.Plain == null)
                    return null;
                GlobalAnimClipDB db = new GlobalAnimClipDB(version.Content(entryName), version.Plain, entryName, version.Debug);
                if (!db.Loaded || GlobalShape(db, version.Plain) != baseShape)
                    return null;
                foreach (var section in Sections(db, version.Plain))
                {
                    if (baseSections.TryGetValue(section.Key, out var before) && before.Signature == section.Value.Signature)
                        continue;
                    claims.Change(outcome, target, "section|" + section.Key, mod, section.Value.Section, section.Value.Signature, "the index entry for " + section.Value.Section.Name);
                }
                Dictionary<string, (GlobalAnimClipDB.ClipDbSection Section, string Signature)> sections = Sections(db, version.Plain);
                foreach (var section in baseSections.Where(o => !sections.ContainsKey(o.Key)))
                    claims.Remove(outcome, target, "section|" + section.Key, mod, "the index entry for " + section.Value.Section.Name);
                Dictionary<string, (GlobalAnimClipDB.BlendSet Blend, string Signature)> blends = Blends(db, version.Plain);
                foreach (var blend in blends)
                {
                    if (baseBlends.TryGetValue(blend.Key, out var before) && before.Signature == blend.Value.Signature)
                        continue;
                    claims.Change(outcome, target, "blend|" + blend.Key, mod, blend.Value.Blend, blend.Value.Signature, "the blend set " + blend.Value.Blend);
                }
                foreach (var blend in baseBlends.Where(o => !blends.ContainsKey(o.Key)))
                    claims.Remove(outcome, target, "blend|" + blend.Key, mod, "the blend set " + blend.Value.Blend);
            }

            GlobalAnimClipDB merged = new GlobalAnimClipDB(baseBytes, union, entryName, null);
            if (!merged.Loaded)
                return null;
            HashSet<string> present = new HashSet<string>();
            for (int i = merged.ClipDbSections.Count - 1; i >= 0; i--)
            {
                string key = "section|" + union.GetID(merged.ClipDbSections[i].Name);
                present.Add(key);
                if (claims.Changed.TryGetValue(key, out var change) && change.Value is GlobalAnimClipDB.ClipDbSection changed)
                    merged.ClipDbSections[i] = new GlobalAnimClipDB.ClipDbSection() { Name = changed.Name, SectionName = changed.SectionName, SectionIndex = changed.SectionIndex };
                else if (claims.Removed.ContainsKey(key))
                    merged.ClipDbSections.RemoveAt(i);
            }
            for (int i = merged.BlendSets.Count - 1; i >= 0; i--)
            {
                string key = "blend|" + BlendKey(merged.BlendSets[i], union);
                present.Add(key);
                if (claims.Changed.TryGetValue(key, out var change) && change.Value is GlobalAnimClipDB.BlendSet changed)
                    merged.BlendSets[i] = changed;
                else if (claims.Removed.ContainsKey(key))
                    merged.BlendSets.RemoveAt(i);
            }
            foreach (string key in claims.Order.Where(o => !present.Contains(o)))
            {
                object value = claims.Changed[key].Value;
                if (value is GlobalAnimClipDB.ClipDbSection section)
                    merged.ClipDbSections.Add(new GlobalAnimClipDB.ClipDbSection() { Name = section.Name, SectionName = section.SectionName, SectionIndex = section.SectionIndex });
                else if (value is GlobalAnimClipDB.BlendSet blend)
                    merged.BlendSets.Add(blend);
            }
            outcome.Bytes = merged.ToBytes();
            return outcome;
        }

        public static Dictionary<string, (GlobalAnimClipDB.ClipDbSection Section, string Signature)> Sections(GlobalAnimClipDB db, AnimationStrings strings)
        {
            Dictionary<string, (GlobalAnimClipDB.ClipDbSection, string)> sections = new Dictionary<string, (GlobalAnimClipDB.ClipDbSection, string)>(StringComparer.Ordinal);
            foreach (GlobalAnimClipDB.ClipDbSection section in db.ClipDbSections)
            {
                string key = strings.GetID(section.Name).ToString(CultureInfo.InvariantCulture);
                if (!sections.ContainsKey(key))
                    sections[key] = (section, strings.GetID(section.SectionName) + "|" + section.SectionIndex);
            }
            return sections;
        }

        public static Dictionary<string, (GlobalAnimClipDB.BlendSet Blend, string Signature)> Blends(GlobalAnimClipDB db, AnimationStrings strings)
        {
            Dictionary<string, (GlobalAnimClipDB.BlendSet, string)> blends = new Dictionary<string, (GlobalAnimClipDB.BlendSet, string)>(StringComparer.Ordinal);
            foreach (GlobalAnimClipDB.BlendSet blend in db.BlendSets)
            {
                string key = BlendKey(blend, strings);
                if (!blends.ContainsKey(key))
                    blends[key] = (blend, BlendSignature(blend, strings));
            }
            return blends;
        }
        #endregion
    }
}
#endif
