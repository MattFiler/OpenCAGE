#if ENABLE_MOD_PACKAGES
using CATHODE;
using CATHODE.Animations;
using CathodeLib;
using OpenCAGE.Modding.Merging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenCAGE.Modding
{
    /// <summary>One thing changed inside an archive game file, offered on its own in Create a Mod.</summary>
    public class PakPart
    {
        public string File;             //the archive, normalised (DATA/UI.PAK)
        public string Group;            //what kind of thing it is, for grouping the rows ("Animation trees"); null for a plain entry
        public string Title;            //what it is, as the row says it
        public string Change;           //"changed", "new" or "removed"
        public string Key;              //what it is inside the file
        public long Size;               //its size where that means something, else 0
        internal Func<Builder, bool> Apply;   //puts it into a build of the original; false when it can't
        internal string ClipPath;       //an animation's path, for the parts that go with it

        public override string ToString() => Title;

        /// <summary>The original archive being built into, with the version the parts come from.</summary>
        internal class Builder
        {
            public List<PAK2.File> Entries;
            public AnimationPakVersion Original, Current;
            public AnimationStrings Names;     //every name either version knows (animation archives)
            public readonly Dictionary<string, object> Parsed = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            public PAK2.File Entry(string name) => Entries.FirstOrDefault(o => string.Equals(AnimationPakVersion.Key(o.Filename), AnimationPakVersion.Key(name), StringComparison.OrdinalIgnoreCase));

            public void Put(string name, byte[] content)
            {
                PAK2.File entry = Entry(name);
                if (content == null) { if (entry != null) Entries.Remove(entry); return; }
                if (entry != null) entry.Content = content;
                else Entries.Add(new PAK2.File() { Filename = name, Content = content });
            }

            public T Get<T>(string name, Func<T> parse) where T : class
            {
                if (!Parsed.TryGetValue(name, out object value))
                    Parsed[name] = value = parse();
                return value as T;
            }
        }
    }

    /// <summary>
    /// What changed inside an archive game file (a PAK2: UI.PAK, ANIMATION.PAK...), thing by thing, and the original with only
    /// some of those things put in - so Create a Mod can offer a choice finer than the whole file, and the mod still ships
    /// a finished file (the original plus the chosen changes) that every installer combines entry by entry.
    ///
    /// A plain archive's parts are its entries. An animation archive's are what an author edits: each animation tree, each
    /// tree's layout, each imported or replaced animation (its data files, its line in its character's clip list and its
    /// entry in the global clip index), each blend set, and anything else that differs entry by entry. Its name tables aren't
    /// parts: they go along whole whenever anything does, as a mod's extra names are harmless.
    /// </summary>
    public static class PakParts
    {
        public static bool IsArchive(byte[] content) => content != null && content.Length >= 4 && content[0] == 'P' && content[1] == 'A' && content[2] == 'K' && content[3] == '2';

        public static bool IsAnimationArchive(string normalisedPath) => ModExportBuilder.IsAnimationPak(normalisedPath);

        /// <summary>
        /// The changes in <paramref name="current"/> against <paramref name="original"/>, part by part, or null when the file
        /// can't be taken apart (it then goes whole). An empty list means nothing worth offering differs.
        /// </summary>
        public static List<PakPart> Find(string normalisedPath, byte[] original, byte[] current)
        {
            if (!IsArchive(original) || !IsArchive(current))
                return null;
            try
            {
                PAK2 a = new PAK2(original), b = new PAK2(current);
                if (!a.Loaded || !b.Loaded)
                    return null;
                return IsAnimationArchive(normalisedPath) ? AnimationParts(normalisedPath, a, b) : EntryParts(normalisedPath, a, b, null);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>The original with only the chosen parts put in, as the archive's bytes; null when that can't be done.</summary>
        public static byte[] Build(string normalisedPath, byte[] original, byte[] current, IEnumerable<PakPart> chosen)
        {
            try
            {
                PAK2 a = new PAK2(original), b = new PAK2(current);
                if (!a.Loaded || !b.Loaded)
                    return null;
                PakPart.Builder builder = new PakPart.Builder()
                {
                    Entries = a.Entries.Select(o => new PAK2.File() { Filename = o.Filename, Content = o.Content }).ToList(),
                    Original = new AnimationPakVersion(a.Entries),
                    Current = new AnimationPakVersion(b.Entries),
                };
                List<PakPart> parts = chosen.ToList();
                if (IsAnimationArchive(normalisedPath) && parts.Count != 0)
                {
                    builder.Names = AnimationPakVersion.Union(new[] { builder.Original.Plain, builder.Original.Debug, builder.Current.Plain, builder.Current.Debug });
                    if (builder.Names == null)
                        return null;
                }
                foreach (PakPart part in parts)
                    if (part.Apply == null || !part.Apply(builder))
                        return null;
                if (IsAnimationArchive(normalisedPath) && parts.Count != 0)
                    FinishAnimationBuild(builder);

                a.Entries.Clear();
                a.Entries.AddRange(builder.Entries);
                string temp = Path.GetTempFileName();
                try
                {
                    return a.Save(temp) ? System.IO.File.ReadAllBytes(temp) : null;
                }
                finally
                {
                    System.IO.File.Delete(temp);
                }
            }
            catch
            {
                return null;
            }
        }

        #region PLAIN ARCHIVES
        private static Dictionary<string, PAK2.File> Index(IEnumerable<PAK2.File> entries)
        {
            Dictionary<string, PAK2.File> index = new Dictionary<string, PAK2.File>(StringComparer.OrdinalIgnoreCase);
            foreach (PAK2.File entry in entries)
                if (entry?.Filename != null && !index.ContainsKey(AnimationPakVersion.Key(entry.Filename)))
                    index[AnimationPakVersion.Key(entry.Filename)] = entry;
            return index;
        }

        /* Every entry that differs, one part each - those <paramref name="skip"/> says aren't left out */
        private static List<PakPart> EntryParts(string file, PAK2 a, PAK2 b, Func<string, bool> skip, string group = null)
        {
            Dictionary<string, PAK2.File> before = Index(a.Entries), after = Index(b.Entries);
            List<PakPart> parts = new List<PakPart>();
            foreach (KeyValuePair<string, PAK2.File> entry in after.OrderBy(o => o.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (skip != null && skip(entry.Key))
                    continue;
                bool existed = before.TryGetValue(entry.Key, out PAK2.File was);
                if (existed && was.Content.AsSpan().SequenceEqual(entry.Value.Content))
                    continue;
                parts.Add(EntryPart(file, group, entry.Value.Filename, existed ? "changed" : "new", entry.Value.Content.LongLength));
            }
            foreach (KeyValuePair<string, PAK2.File> entry in before.Where(o => !after.ContainsKey(o.Key)).OrderBy(o => o.Key, StringComparer.OrdinalIgnoreCase))
                if (skip == null || !skip(entry.Key))
                    parts.Add(EntryPart(file, group, entry.Value.Filename, "removed", 0));
            return parts;
        }

        private static PakPart EntryPart(string file, string group, string entryName, string change, long size)
        {
            return new PakPart()
            {
                File = file,
                Group = group,
                Title = entryName.Replace('\\', '/'),
                Change = change,
                Key = "entry|" + AnimationPakVersion.Key(entryName),
                Size = size,
                Apply = b => { b.Put(entryName, change == "removed" ? null : b.Current.Content(entryName)); return true; },
            };
        }
        #endregion

        #region ANIMATION ARCHIVES
        //What an animation archive's parts are, as their rows are grouped
        public const string TreeGroup = "Animation trees", LayoutGroup = "Animation tree layouts", ClipGroup = "Animations", BlendGroup = "Blend sets", OtherGroup = "Other animation data";

        private static List<PakPart> AnimationParts(string file, PAK2 originalPak, PAK2 currentPak)
        {
            AnimationPakVersion o = new AnimationPakVersion(originalPak.Entries), c = new AnimationPakVersion(currentPak.Entries);
            if (o.Debug == null || c.Debug == null || o.Plain == null || c.Plain == null)
                return null;
            Dictionary<string, PAK2.File> before = Index(originalPak.Entries), after = Index(currentPak.Entries);
            List<string> differing = before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase)
                .Where(n => !before.TryGetValue(n, out PAK2.File x) || !after.TryGetValue(n, out PAK2.File y) || !x.Content.AsSpan().SequenceEqual(y.Content))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            HashSet<string> handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<PakPart> parts = new List<PakPart>();

            //Trees, tree by tree. A file below that can't be taken apart, or whose change isn't in the parts it's taken
            //apart into, is left for the last step: it's offered as the entry it is
            Dictionary<(uint, uint), PakPart> treeParts = new Dictionary<(uint, uint), PakPart>();
            foreach (string name in differing.Where(AnimationPakMerge.IsTreeSet))
            {
                var treesBefore = before.ContainsKey(name) ? AnimationPakMerge.Trees(o.Content(name), o.Debug, name) : new Dictionary<uint, (AnimationTree, byte[])>();
                var treesAfter = after.ContainsKey(name) ? AnimationPakMerge.Trees(c.Content(name), c.Debug, name) : new Dictionary<uint, (AnimationTree, byte[])>();
                if (treesBefore == null || treesAfter == null)
                    continue;
                int found = parts.Count;
                uint setHash = SetHashOf(name);
                foreach (var tree in treesAfter)
                {
                    bool existed = treesBefore.TryGetValue(tree.Key, out var was);
                    if (existed && was.Item2.AsSpan().SequenceEqual(tree.Value.Item2))
                        continue;
                    PakPart part = TreePart(file, name, setHash, tree.Key, tree.Value.Item1, existed ? "changed" : "new");
                    treeParts[(setHash, tree.Key)] = part;
                    parts.Add(part);
                }
                foreach (var tree in treesBefore.Where(t => !treesAfter.ContainsKey(t.Key)))
                {
                    PakPart part = TreePart(file, name, setHash, tree.Key, tree.Value.Item1, "removed");
                    treeParts[(setHash, tree.Key)] = part;
                    parts.Add(part);
                }
                if (parts.Count != found)
                    handled.Add(name);
            }

            //Layouts, tree by tree: with their tree when it changed too, else on their own
            string layoutsName = differing.FirstOrDefault(AnimTreeLayouts.IsEntry);
            AnimTreeLayouts lo = null, lc = null;
            if (layoutsName != null && AnimTreeLayoutsMerge.TryRead(o.Content(layoutsName), out lo) && AnimTreeLayoutsMerge.TryRead(c.Content(layoutsName), out lc))
            {
                foreach ((uint setHash, uint treeHash) in lo.Trees.Concat(lc.Trees).Where(t => t != null).Select(t => (t.SetHash, t.TreeHash)).Distinct())
                {
                    AnimTreeLayouts.TreeLayout was = lo.Find(setHash, treeHash), now = lc.Find(setHash, treeHash);
                    if (LayoutRecord(was).AsSpan().SequenceEqual(LayoutRecord(now)))
                        continue;
                    if (treeParts.TryGetValue((setHash, treeHash), out PakPart tree))
                    {
                        //Its layout goes with it
                        tree.Key += "|layout";
                        tree.Apply += b => PutLayout(b, layoutsName, setHash, treeHash);
                    }
                    else
                        parts.Add(LayoutPart(file, layoutsName, setHash, treeHash, now ?? was, now == null ? "removed" : was == null ? "new" : "changed"));
                }
                //Where each tree's canvas was left isn't worth a choice: with nothing else, nothing's offered
                handled.Add(layoutsName);
            }

            //Animations and blend set users, line by line in each character's clip list
            List<PakPart> clipParts = new List<PakPart>();
            foreach (string name in differing.Where(AnimationPakMerge.IsSetClipDb))
            {
                if (!before.ContainsKey(name) || !after.ContainsKey(name))
                    continue; //a whole character's list come or gone: not something lines can say
                AnimClipDB dbo = new AnimClipDB(o.Content(name), o.Plain, name, o.Debug), dbc = new AnimClipDB(c.Content(name), c.Plain, name, c.Debug);
                if (!dbo.Loaded || !dbc.Loaded)
                    continue;
                AnimationPakMerge.ClipLines linesBefore = AnimationPakMerge.LinesOf(dbo, o.Plain), linesAfter = AnimationPakMerge.LinesOf(dbc, c.Plain);
                if (linesBefore.Shape != linesAfter.Shape)
                    continue;
                int found = parts.Count;
                string character = dbc.Character;
                foreach (var line in linesAfter.Lines)
                {
                    bool existed = linesBefore.Lines.TryGetValue(line.Key, out var was);
                    if (existed && was.Signature == line.Value.Signature)
                        continue;
                    PakPart part = LinePart(file, name, character, line.Key, line.Value.Context, line.Value.Clip, line.Value.Blend, existed ? "changed" : "new");
                    parts.Add(part);
                    if (line.Value.Clip != null) clipParts.Add(part);
                }
                foreach (var line in linesBefore.Lines.Where(l => !linesAfter.Lines.ContainsKey(l.Key)))
                {
                    PakPart part = LinePart(file, name, character, line.Key, line.Value.Context, line.Value.Clip, line.Value.Blend, "removed");
                    parts.Add(part);
                    if (line.Value.Clip != null) clipParts.Add(part);
                }
                if (parts.Count != found)
                    handled.Add(name);
            }

            //The global clip index: an entry goes with the animation whose path it is; blend sets on their own
            string globalName = differing.FirstOrDefault(AnimationPakMerge.IsGlobalClipDb);
            GlobalAnimClipDB go = null, gc = null;
            if (globalName != null && before.ContainsKey(globalName) && after.ContainsKey(globalName))
            {
                go = new GlobalAnimClipDB(o.Content(globalName), o.Plain, globalName, o.Debug);
                gc = new GlobalAnimClipDB(c.Content(globalName), c.Plain, globalName, c.Debug);
            }
            if (go != null && go.Loaded && gc.Loaded && AnimationPakMerge.GlobalShape(go, o.Plain) == AnimationPakMerge.GlobalShape(gc, c.Plain))
            {
                int found = parts.Count + clipParts.Sum(p => p.Apply.GetInvocationList().Length);
                var so = AnimationPakMerge.Sections(go, o.Plain);
                var sc = AnimationPakMerge.Sections(gc, c.Plain);
                foreach (string key in so.Keys.Union(sc.Keys).ToList())
                {
                    bool had = so.TryGetValue(key, out var was), has = sc.TryGetValue(key, out var now);
                    if (had && has && was.Signature == now.Signature)
                        continue;
                    string path = (has ? now.Section : was.Section).Name;
                    PakPart owner = clipParts.FirstOrDefault(p => string.Equals(p.ClipPath, path, StringComparison.OrdinalIgnoreCase));
                    Func<PakPart.Builder, bool> apply = bl => PutSection(bl, globalName, key, has ? now.Section : null);
                    if (owner != null)
                        owner.Apply += apply;
                    else
                        parts.Add(new PakPart() { File = file, Group = OtherGroup, Title = "The clip index's entry for " + path, Change = !had ? "new" : !has ? "removed" : "changed", Key = "section|" + key, Apply = apply });
                }
                var bo = AnimationPakMerge.Blends(go, o.Plain);
                var bc = AnimationPakMerge.Blends(gc, c.Plain);
                foreach (string key in bo.Keys.Union(bc.Keys).ToList())
                {
                    bool had = bo.TryGetValue(key, out var was), has = bc.TryGetValue(key, out var now);
                    if (had && has && was.Signature == now.Signature)
                        continue;
                    GlobalAnimClipDB.BlendSet blend = has ? now.Blend : was.Blend;
                    parts.Add(new PakPart()
                    {
                        File = file, Group = BlendGroup, Title = "Blend set " + blend, Change = !had ? "new" : !has ? "removed" : "changed", Key = "blendset|" + key,
                        Apply = bl => PutBlend(bl, globalName, key, has ? now.Blend : null),
                    });
                }
                if (parts.Count + clipParts.Sum(p => p.Apply.GetInvocationList().Length) != found)
                    handled.Add(globalName);
            }

            //An animation's data files: with the animation whose path names them, or as a replaced animation found by its path
            Dictionary<uint, PakPart> byPathHash = new Dictionary<uint, PakPart>();
            foreach (PakPart part in clipParts)
            {
                uint hash = Utilities.AnimationHashedString(part.ClipPath ?? "");
                if (!byPathHash.ContainsKey(hash)) byPathHash[hash] = part;
            }
            Dictionary<uint, (string Character, AnimClipDB.AnimClip Clip, string Context)> allClips = null;
            foreach (string name in differing.Where(AnimationPakMerge.IsClipSection))
            {
                string file2 = AnimationPakMerge.FileOf(name);
                string digits = file2.Substring("ANIM_CLIP_DB_SEC_".Length).Split('.')[0];
                PakPart owner = null;
                if (uint.TryParse(digits, out uint pathHash) && !byPathHash.TryGetValue(pathHash, out owner))
                {
                    allClips = allClips ?? AllClips(c);
                    if (allClips.TryGetValue(pathHash, out var clip))
                    {
                        owner = new PakPart()
                        {
                            File = file, Group = ClipGroup, Title = clip.Clip.Name + "  (" + clip.Character + (string.IsNullOrEmpty(clip.Context) ? "" : ", " + clip.Context) + ")",
                            Change = "changed", Key = "clipdata|" + pathHash, ClipPath = clip.Clip.Path, Apply = bl => true,
                        };
                        byPathHash[pathHash] = owner;
                        parts.Add(owner);
                    }
                }
                string entryName = after.TryGetValue(name, out PAK2.File e) ? e.Filename : before[name].Filename;
                bool exists = after.ContainsKey(name);
                Func<PakPart.Builder, bool> put = bl => { bl.Put(entryName, exists ? bl.Current.Content(entryName) : null); return true; };
                if (owner != null) owner.Apply += put;
                else parts.Add(new PakPart() { File = file, Group = OtherGroup, Title = entryName.Replace('\\', '/'), Change = !before.ContainsKey(name) ? "new" : exists ? "changed" : "removed", Key = "entry|" + name, Apply = put });
                handled.Add(name);
            }

            //Anything else that differs, entry by entry; the name tables go along with whatever else does
            foreach (string name in differing.Where(n => !handled.Contains(n) && !n.EndsWith(AnimationPakVersion.StringsSuffix, StringComparison.OrdinalIgnoreCase) && !n.EndsWith(AnimationPakVersion.DebugStringsSuffix, StringComparison.OrdinalIgnoreCase)))
            {
                string entryName = after.TryGetValue(name, out PAK2.File e) ? e.Filename : before[name].Filename;
                bool exists = after.ContainsKey(name);
                parts.Add(new PakPart()
                {
                    File = file, Group = OtherGroup, Title = entryName.Replace('\\', '/'), Change = !before.ContainsKey(name) ? "new" : exists ? "changed" : "removed", Key = "entry|" + name,
                    Size = exists ? after[name].Content.LongLength : 0,
                    Apply = bl => { bl.Put(entryName, exists ? bl.Current.Content(entryName) : null); return true; },
                });
            }
            //The parts' closures share this method's captured locals: let go of the archives' entries rather than keep them with the parts
            before = null;
            after = null;
            return parts;
        }

        private static uint SetHashOf(string entryName)
        {
            string file = AnimationPakMerge.FileOf(entryName);
            int underscore = file.IndexOf('_');
            return underscore > 0 && uint.TryParse(file.Substring(0, underscore), out uint hash) ? hash : 0;
        }

        private static PakPart TreePart(string file, string setEntry, uint setHash, uint treeHash, AnimationTree tree, string change)
        {
            return new PakPart()
            {
                File = file, Group = TreeGroup, Title = tree.Name + "  (" + tree.Set + ")", Change = change, Key = "tree|" + setHash + "|" + treeHash,
                Apply = b => PutTree(b, setEntry, setHash, treeHash, change != "removed"),
            };
        }

        private static PakPart LayoutPart(string file, string layoutsEntry, uint setHash, uint treeHash, AnimTreeLayouts.TreeLayout layout, string change)
        {
            string tree = string.IsNullOrEmpty(layout?.Tree) ? "tree 0x" + treeHash.ToString("X8") : layout.Tree;
            string set = string.IsNullOrEmpty(layout?.Set) ? "set 0x" + setHash.ToString("X8") : layout.Set;
            return new PakPart()
            {
                File = file, Group = LayoutGroup, Title = tree + "  (" + set + ")", Change = change, Key = "layout|" + setHash + "|" + treeHash,
                Apply = b => PutLayout(b, layoutsEntry, setHash, treeHash),
            };
        }

        private static PakPart LinePart(string file, string clipDb, string character, string lineKey, string context, AnimClipDB.AnimClip clip, AnimClipDB.BlendSet blend, string change)
        {
            string where = character + (string.IsNullOrEmpty(context) ? "" : ", " + context);
            return new PakPart()
            {
                File = file,
                Group = clip != null ? ClipGroup : BlendGroup,
                Title = clip != null ? clip.Name + "  (" + where + ")" : "Blend set " + blend.Name + " used by " + where,
                Change = change,
                Key = "line|" + AnimationPakVersion.Key(clipDb) + "|" + lineKey,
                ClipPath = clip?.Path,
                Apply = b => PutLine(b, clipDb, lineKey, change != "removed"),
            };
        }

        private static Dictionary<uint, (string Character, AnimClipDB.AnimClip Clip, string Context)> AllClips(AnimationPakVersion version)
        {
            Dictionary<uint, (string, AnimClipDB.AnimClip, string)> clips = new Dictionary<uint, (string, AnimClipDB.AnimClip, string)>();
            foreach (PAK2.File entry in version.Entries.Where(e => AnimationPakMerge.IsSetClipDb(e.Filename)))
            {
                try
                {
                    AnimClipDB db = new AnimClipDB(entry.Content, version.Plain, entry.Filename, version.Debug);
                    if (!db.Loaded) continue;
                    foreach (AnimClipDB.AnimClip clip in db.Animations)
                        clips[Utilities.AnimationHashedString(clip.Path)] = (db.Character, clip, "");
                    foreach (AnimClipDB.Context context in db.Contexts)
                        foreach (AnimClipDB.AnimClip clip in context.Animations)
                            clips[Utilities.AnimationHashedString(clip.Path)] = (db.Character, clip, context.Name);
                }
                catch { }
            }
            return clips;
        }
        #endregion

        #region BUILDING AN ANIMATION ARCHIVE
        private static bool PutTree(PakPart.Builder b, string setEntry, uint setHash, uint treeHash, bool present)
        {
            byte[] originalSet = b.Original.Content(setEntry);
            if (originalSet == null)
            {
                //A set the original doesn't have: only as the current version has it
                b.Put(setEntry, b.Current.Content(setEntry));
                return true;
            }
            AnimTreeDB set = b.Get(setEntry, () => { AnimTreeDB db = new AnimTreeDB(originalSet, b.Names, setEntry); return db.Loaded ? db : null; });
            if (set == null)
                return false;
            if (present)
            {
                var trees = b.Get("trees|" + setEntry, () => AnimationPakMerge.Trees(b.Current.Content(setEntry), b.Current.Debug, setEntry));
                if (trees == null || !trees.TryGetValue(treeHash, out var tree))
                    return false;
                int at = set.Entries.FindIndex(t => b.Names.GetID(t.Name) == treeHash);
                if (at >= 0) set.Entries[at] = tree.Item1;
                else set.Entries.Add(tree.Item1);
            }
            else
                set.Entries.RemoveAll(t => b.Names.GetID(t.Name) == treeHash);
            b.Put(setEntry, set.ToBytes());
            return true;
        }

        private static bool PutLayout(PakPart.Builder b, string layoutsEntry, uint setHash, uint treeHash)
        {
            AnimTreeLayouts layouts = b.Get(layoutsEntry, () => AnimTreeLayoutsMerge.TryRead(b.Original.Content(layoutsEntry), out AnimTreeLayouts l) ? l : null);
            if (layouts == null || !AnimTreeLayoutsMerge.TryRead(b.Current.Content(layoutsEntry), out AnimTreeLayouts current))
                return false;
            AnimTreeLayouts.TreeLayout now = current.Find(setHash, treeHash);
            if (now != null) layouts.Put(now.Clone());
            else layouts.Remove(setHash, treeHash);
            b.Put(layoutsEntry, layouts.Empty ? null : layouts.ToBytes());
            return true;
        }

        private static bool PutLine(PakPart.Builder b, string clipDb, string lineKey, bool present)
        {
            AnimClipDB db = b.Get(clipDb, () => { AnimClipDB d = new AnimClipDB(b.Original.Content(clipDb), b.Names, clipDb, null); return d.Loaded ? d : null; });
            if (db == null)
                return false;
            AnimationPakMerge.ClipLines current = b.Get("lines|" + clipDb, () =>
            {
                AnimClipDB d = new AnimClipDB(b.Current.Content(clipDb), b.Current.Plain, clipDb, b.Current.Debug);
                return d.Loaded ? AnimationPakMerge.LinesOf(d, b.Current.Plain) : null;
            });
            if (current == null)
                return false;
            string[] key = lineKey.Split('|');   //kind|context|hash
            string kind = key[0], context = key[1];
            uint hash = uint.Parse(key[2], CultureInfo.InvariantCulture);
            List<AnimClipDB.AnimClip> clips = context.Length == 0 ? db.Animations : db.Contexts.FirstOrDefault(x => (x.Name ?? "") == context)?.Animations;
            List<AnimClipDB.BlendSet> blends = context.Length == 0 ? db.BlendSets : db.Contexts.FirstOrDefault(x => (x.Name ?? "") == context)?.BlendSets;
            if (clips == null || blends == null)
                return false;
            current.Lines.TryGetValue(lineKey, out var line);
            if (kind == "clip")
            {
                int at = clips.FindIndex(x => b.Names.GetID(x.Name) == hash);
                if (!present) { if (at >= 0) clips.RemoveAt(at); }
                else if (line.Clip == null) return false;
                else
                {
                    AnimClipDB.AnimClip copy = new AnimClipDB.AnimClip() { Name = line.Clip.Name, Path = line.Clip.Path, MetadataInstance = line.Clip.MetadataInstance };
                    if (at >= 0) clips[at] = copy; else clips.Add(copy);
                }
            }
            else
            {
                int at = blends.FindIndex(x => b.Names.GetID(x.Name) == hash);
                if (!present) { if (at >= 0) blends.RemoveAt(at); }
                else if (line.Blend == null) return false;
                else
                {
                    AnimClipDB.BlendSet copy = new AnimClipDB.BlendSet() { Name = line.Blend.Name, Filename = line.Blend.Filename };
                    if (at >= 0) blends[at] = copy; else blends.Add(copy);
                }
            }
            b.Put(clipDb, db.ToBytes());
            return true;
        }

        private static GlobalAnimClipDB GlobalOf(PakPart.Builder b, string globalEntry) =>
            b.Get(globalEntry, () => { GlobalAnimClipDB g = new GlobalAnimClipDB(b.Original.Content(globalEntry), b.Names, globalEntry, null); return g.Loaded ? g : null; });

        private static bool PutSection(PakPart.Builder b, string globalEntry, string key, GlobalAnimClipDB.ClipDbSection section)
        {
            GlobalAnimClipDB global = GlobalOf(b, globalEntry);
            if (global == null)
                return false;
            int at = global.ClipDbSections.FindIndex(x => b.Names.GetID(x.Name).ToString(CultureInfo.InvariantCulture) == key);
            if (section == null) { if (at >= 0) global.ClipDbSections.RemoveAt(at); }
            else
            {
                GlobalAnimClipDB.ClipDbSection copy = new GlobalAnimClipDB.ClipDbSection() { Name = section.Name, SectionName = section.SectionName, SectionIndex = section.SectionIndex };
                if (at >= 0) global.ClipDbSections[at] = copy; else global.ClipDbSections.Add(copy);
            }
            b.Put(globalEntry, global.ToBytes());
            return true;
        }

        private static bool PutBlend(PakPart.Builder b, string globalEntry, string key, GlobalAnimClipDB.BlendSet blend)
        {
            GlobalAnimClipDB global = GlobalOf(b, globalEntry);
            if (global == null)
                return false;
            int at = global.BlendSets.FindIndex(x => AnimationPakMerge.BlendKey(x, b.Names) == key);
            if (blend == null) { if (at >= 0) global.BlendSets.RemoveAt(at); }
            else if (at >= 0) global.BlendSets[at] = blend;
            else global.BlendSets.Add(blend);
            b.Put(globalEntry, global.ToBytes());
            return true;
        }

        /* Whatever went in, the current version's name tables go with it: every name a chosen part uses is in them */
        private static void FinishAnimationBuild(PakPart.Builder b)
        {
            foreach (PAK2.File entry in b.Current.Entries.Where(e => (e.Filename ?? "").EndsWith(AnimationPakVersion.StringsSuffix, StringComparison.OrdinalIgnoreCase)
                                                                    || (e.Filename ?? "").EndsWith(AnimationPakVersion.DebugStringsSuffix, StringComparison.OrdinalIgnoreCase)))
                b.Put(entry.Filename, entry.Content);
        }

        /* A tree's layout as the layouts file writes it alone, without where the canvas was left (empty for none) */
        private static byte[] LayoutRecord(AnimTreeLayouts.TreeLayout layout)
        {
            if (layout == null)
                return new byte[0];
            AnimTreeLayouts.TreeLayout drawn = layout.Clone();
            drawn.CanvasX = 0;
            drawn.CanvasY = 0;
            drawn.CanvasScale = 0;
            AnimTreeLayouts single = AnimTreeLayouts.FromBytes(null);
            single.Trees.Add(drawn);
            return single.ToBytes();
        }
        #endregion
    }
}
#endif
