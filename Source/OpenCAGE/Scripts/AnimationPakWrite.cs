using CATHODE;
using CATHODE.Animations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// What everything writing ANIMATION.PAK checks first. OpenCAGE holds the whole PAK in memory and writes it back whole,
    /// so the file must still be the one it loaded: the Mod Manager, a game file check or another tool may have replaced it
    /// since, and writing the copy in memory would put the old one back - its trees, and the tree layouts kept with them.
    /// A write of every tree as it is in memory also needs each name in them in the debug string table, and each tree set
    /// to load again as it would be written - one that doesn't is gone, every tree in it, the next time the PAK loads.
    /// </summary>
    /// <remarks>UI thread, apart from <see cref="ChangedOnDisk"/>, which only looks at the file and can be asked from any.</remarks>
    public static class AnimationPakWrite
    {
        //ANIMATION.PAK as it was on disk when OpenCAGE loaded it, or last wrote it: its size and time, and its contents' hash
        //(worked out in the background, for when the file is written again unchanged - a mod manager rollback, say) - and the
        //animations in memory that are that file (loaded again, any held from before are not)
        private static string _path;
        private static long _length;
        private static DateTime _writtenUtc;
        private static Task<byte[]> _hash;
        private static CathodeLib.Animation _stamped;
        //A write that didn't go through left what it was writing in memory only (see Written)
        private static bool _writeFailed;

        /// <summary>The file on disk is what's in memory now: just loaded, or just written by OpenCAGE (or tried to be).</summary>
        public static void Stamp(CathodeLib.Animation animations)
        {
            _stamped = animations;
            _writeFailed = false;
            string path = animations?.PAK?.Filepath;
            _path = null;
            _hash = null;
            if (string.IsNullOrEmpty(path))
                return;
            try
            {
                FileInfo file = new FileInfo(path);
                _length = file.Exists ? file.Length : -1;
                _writtenUtc = file.Exists ? file.LastWriteTimeUtc : DateTime.MinValue;
                _path = file.FullName;
                string full = file.FullName;
                _hash = file.Exists ? Task.Run(() => Hash(full)) : null;
            }
            catch { }
        }

        /// <summary>Why the PAK in memory mustn't be written over the file: it has changed on disk since it was loaded. Null when it hasn't.</summary>
        public static string ChangedOnDisk(string path)
        {
            if (_path == null || string.IsNullOrEmpty(path))
                return null;
            FileInfo file;
            try
            {
                file = new FileInfo(path);
                if (!string.Equals(file.FullName, _path, StringComparison.OrdinalIgnoreCase))
                    return null;
                if ((file.Exists ? file.Length : -1) == _length && (file.Exists ? file.LastWriteTimeUtc : DateTime.MinValue) == _writtenUtc)
                    return null;
            }
            catch
            {
                return null;
            }
            //Written again, but with the same bytes: still the file in memory (when either hash can't be had, it's taken as changed)
            if (file.Exists && file.Length == _length)
            {
                byte[] was = null, now = null;
                try
                {
                    was = _hash?.Result;
                    now = Hash(file.FullName);
                }
                catch { }
                if (was != null && now != null && was.AsSpan().SequenceEqual(now))
                {
                    _writtenUtc = file.LastWriteTimeUtc;
                    return null;
                }
            }
            return "ANIMATION.PAK has changed on disk since OpenCAGE loaded it (a game file check or another tool changed it), so saving "
                + "now would put the old one back. Restart OpenCAGE to load it as it is now - changes to animations made here and not saved yet can't be kept.";
        }

        /// <summary>
        /// Why these animations mustn't be written over ANIMATION.PAK: they've been loaded again since they were read (the Mod
        /// Manager changed the file), or the file changed on disk since (<see cref="ChangedOnDisk"/>). Null when they can be.
        /// </summary>
        public static string Stale(CathodeLib.Animation animations)
        {
            if (Singleton.CurrentBackupState == Singleton.BackupState.APPLYING_MODS)
                return "Mods are being applied, which can change ANIMATION.PAK. Save once that's done.";
            //Every writer asks this first: the hash of the file as last loaded or written reads it in the background, and a
            //writer can't open the file while it does - so it's let finish (a second or two after a load or a save)
            try { _hash?.Wait(); }
            catch { }
            if (animations != null && _stamped != null && !ReferenceEquals(animations, _stamped))
                return "ANIMATION.PAK was loaded again after these animations were read (the Mod Manager changed it), so they can't be written over it. "
                    + "Open them again to work on them as they are now.";
            return ChangedOnDisk(animations?.PAK?.Filepath);
        }

        private static byte[] Hash(string path)
        {
            using (SHA256 sha = Modding.ModToolkit.NewSha256())
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 20))
                return sha.ComputeHash(stream);
        }

        /// <summary>
        /// Every tree set made ready to be written as it is in memory: each name in it registered in the debug string table,
        /// and each checked to load again (and, before that, refused for a foot sync selector missing a strike, which stops
        /// its whole set loading - said by name). Gives each set's entry in the PAK and its bytes to write there; null with the
        /// reason when any can't be written, and then nothing should be. The debug string table is to be written with them, every
        /// time: names a refused write registered are in it already.
        /// </summary>
        public static List<(AnimTreeDB Database, PAK2.File Entry, byte[] Content)> PrepareTrees(CathodeLib.Animation animations, out string error)
        {
            error = null;
            List<AnimTreeDB> databases = animations.Trees.ToList();

            foreach (AnimTreeDB database in databases)
            {
                foreach (AnimationTree tree in database.Entries)
                {
                    FootSyncSelectorNode footSync = FlowNodes(tree).OfType<FootSyncSelectorNode>().FirstOrDefault(o => o.LeftStrikeChild == null || o.RightStrikeChild == null);
                    if (footSync == null)
                        continue;
                    error = "The foot sync selector '" + footSync.Name + "' in tree '" + tree.Name + "' (set '" + database.Set + "') needs an animation linked to both its LeftStrikeChild and RightStrikeChild pins, so nothing was saved.";
                    return null;
                }
            }

            //Every name in the PAK is stored as a hash: names made or changed here must be in the debug string table, or
            //they read back as numbers (and a tree whose own name is missing stops its whole set loading)
            foreach (AnimTreeDB database in databases)
                foreach (AnimationTree tree in database.Entries)
                    foreach (AnimationNode node in new AnimationNode[] { tree }.Concat(tree.Nodes))
                    {
                        RegisterName(animations, node?.Name);
                        if (node is AnimationTree named)
                            RegisterName(animations, named.Set);
                    }

            List<(AnimTreeDB, PAK2.File, byte[])> sets = new List<(AnimTreeDB, PAK2.File, byte[])>();
            foreach (AnimTreeDB database in databases)
            {
                PAK2.File entry = animations.PAK.Entries.FirstOrDefault(o => string.Equals(o.Filename, database.Filepath, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                    continue;
                byte[] content = null;
                try { content = database.ToBytes(); }
                catch { }
                if (content == null)
                {
                    error = "Animation set '" + database.Set + "' could not be written, so nothing was saved.";
                    return null;
                }
                //A set that would not read back would be gone, every tree in it, the next time the animations load
                AnimTreeDB check = null;
                try { check = new AnimTreeDB(content, animations.StringsDebug, database.Filepath); }
                catch { }
                if (check == null || !check.Loaded || check.Entries.Count != database.Entries.Count)
                {
                    error = "Animation set '" + database.Set + "' would not load again as it is now, so nothing was saved. Put back the last links or nodes you changed in it and try again.";
                    return null;
                }
                sets.Add((database, entry, content));
            }
            return sets;
        }

        /// <summary>
        /// Before ANIMATION.PAK is written whole from memory (CathodeLib's Animation.Save, which writes every tree, unsaved
        /// edits and all): the file still the one loaded, the trees ready (<see cref="PrepareTrees"/>), the original kept
        /// for the Mod Manager, and the tree layouts put in with the trees. Null when it can go ahead, else why not.
        /// </summary>
        public static string BeforeWholeSave(CathodeLib.Animation animations)
        {
            string path = animations?.PAK?.Filepath;
            string error = Stale(animations);
            if (error != null)
                return error;
            PrepareTrees(animations, out error);
            if (error != null)
                return error;
            Modding.ModServices.CaptureBeforeWrite(path);
            AnimTreeLayoutManager.Commit(out _);
            return null;
        }

        /// <summary>
        /// After OpenCAGE wrote ANIMATION.PAK, or tried to (what's on disk is then its doing either way). <paramref name="saved"/>:
        /// whether the write went through. <paramref name="whole"/>: whether it wrote everything in memory (CathodeLib's
        /// Animation.Save), rather than the PAK with some entries put in first - which leaves out what else waits in memory.
        /// </summary>
        public static void Written(CathodeLib.Animation animations, bool saved, bool whole)
        {
            bool failedBefore = _writeFailed;
            Stamp(animations);
            //A write that didn't go through leaves what it was writing in memory only - out of sight of the trees' comparison with
            //the PAK, as it put them in its entries first - until a write of everything puts it all out
            _writeFailed = !saved || (failedBefore && !whole);
            if (saved && whole)
            {
                foreach (EditBlendSets window in Application.OpenForms.OfType<EditBlendSets>().ToList())
                    window.MarkSaved();
                MCP.McpAnimationAssetTools.ForgetUnwrittenChanges();
            }
        }

        /// <summary>
        /// Changes to the animations in memory that aren't in ANIMATION.PAK yet, in words ("tree set HUMANOID", "blend sets"):
        /// what writing the PAK from somewhere else (the Mod Manager) would leave behind. Trees are only looked at once they're
        /// read - nothing can have changed them before.
        /// </summary>
        public static List<string> Unsaved(CathodeLib.Animation animations)
        {
            List<string> unsaved = new List<string>();
            if (animations?.PAK == null)
                return unsaved;
            if (animations.ContentsLoaded)
            {
                foreach (AnimTreeDB database in animations.Trees)
                {
                    PAK2.File entry = animations.PAK.Entries.FirstOrDefault(o => string.Equals(o.Filename, database.Filepath, StringComparison.OrdinalIgnoreCase));
                    byte[] content = null;
                    try { content = database.ToBytes(); }
                    catch { }
                    if (entry?.Content == null || content == null || !entry.Content.AsSpan().SequenceEqual(content))
                        unsaved.Add("the animation trees of " + database.Set);
                }
            }
            if (_writeFailed)
                unsaved.Add("what a save that didn't go through left unsaved");
            if (AnimTreeLayoutManager.HasPending)
                unsaved.Add("animation tree layouts");
            if (Application.OpenForms.OfType<EditBlendSets>().Any(o => !o.IsDisposed && o.HasUnsavedChanges))
                unsaved.Add("blend sets");
            if (MCP.McpAnimationAssetTools.HasUnwrittenChanges)
                unsaved.Add("changes the AI assistant made");
            return unsaved;
        }

        /// <summary>
        /// Write every animation in memory to ANIMATION.PAK as it is (<see cref="BeforeWholeSave"/>, then the PAK): what holds
        /// unsaved changes then holds none. Null when written, else why not.
        /// </summary>
        public static string SaveWhole(CathodeLib.Animation animations)
        {
            string refused = BeforeWholeSave(animations);
            if (refused != null)
                return refused;
            bool saved = false;
            try { saved = animations.Save(); }
            catch (Exception e) { return "ANIMATION.PAK could not be written: " + e.Message; }
            finally { Written(animations, saved, true); }
            return saved ? null : "ANIMATION.PAK could not be written.";
        }

        /* Every node in a tree's flow - what is written, from the tree's children down - each once */
        private static IEnumerable<AnimationNode> FlowNodes(AnimationTree tree)
        {
            HashSet<AnimationNode> seen = new HashSet<AnimationNode>(AnimTrees.AnimTreeCanvas.ByReference.Instance);
            Stack<AnimationNode> todo = new Stack<AnimationNode>(tree.Children);
            while (todo.Count != 0)
            {
                AnimationNode node = todo.Pop();
                if (node == null || !seen.Add(node))
                    continue;
                yield return node;
                foreach (AnimationNode child in node.Children)
                    todo.Push(child);
            }
        }

        /* A name the debug string table does not hold yet goes in (a number standing for a hash the table never knew stays as it is) */
        private static void RegisterName(CathodeLib.Animation animations, string name)
        {
            AnimationStrings strings = animations?.StringsDebug;
            if (strings == null || string.IsNullOrEmpty(name))
                return;
            uint id = strings.GetID(name);
            if (strings.Entries.ContainsKey(id) || id != CathodeLib.Utilities.AnimationHashedString(name))
                return;
            animations.AddName(name, true);
        }
    }
}
