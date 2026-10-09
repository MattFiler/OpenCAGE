using CATHODE;
using CATHODE.Animations;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static OpenCAGE.AnimTreeLayouts;

namespace OpenCAGE
{
    /// <summary>
    /// The animation tree layouts people have made by hand (<see cref="AnimTreeLayouts"/>: DATA/GLOBAL/AnimTreeLayouts.dat,
    /// beside ANIMATION.PAK) - the Animation Tree Editor's and the AI assistant tools' shared copy. A tree with none is laid
    /// out automatically. Changes wait in memory until the trees are saved (or an assistant tool writes them at once);
    /// the file is read again whenever something else has changed it on disk, such as the mod manager.
    /// </summary>
    /// <remarks>UI thread only.</remarks>
    public static class AnimTreeLayoutManager
    {
        private const string LogSystem = "Anim Tree Layouts";

        private static AnimTreeLayouts _file;
        private static string _loadedFrom;
        private static long _loadedLength = -1;
        private static DateTime _loadedWrite = DateTime.MinValue;

        //Changes not yet written: a layout to store, or null to drop the tree's layout
        private static readonly Dictionary<(uint, uint), TreeLayout> _pending = new Dictionary<(uint, uint), TreeLayout>();

        /// <summary>A tree's layout changed (set hash, tree hash, and whoever changed it - an open graph reloads unless it was the one).</summary>
        public static event Action<uint, uint, object> Changed;

        /// <summary>DATA/GLOBAL/AnimTreeLayouts.dat beside the animation PAK in use (null when the animations aren't loaded).</summary>
        public static string FilePath
        {
            get
            {
                string pak = Singleton.Global?.Animations?.PAK?.Filepath;
                if (string.IsNullOrEmpty(pak))
                    return null;
                try { return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(pak)), AnimTreeLayouts.FileName); }
                catch { return null; }
            }
        }

        private static AnimationStrings Strings => Singleton.Global?.Animations?.StringsDebug;

        public static bool HasPending => _pending.Count != 0;

        #region Reading
        /* The file as it is on disk now, read again if it changed since (or the game folder did) */
        private static AnimTreeLayouts Current
        {
            get
            {
                string path = FilePath;
                FileInfo info = path == null ? null : new FileInfo(path);
                long length = info != null && info.Exists ? info.Length : -1;
                DateTime write = info != null && info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue;
                if (_file == null || !string.Equals(path, _loadedFrom, StringComparison.OrdinalIgnoreCase) || length != _loadedLength || write != _loadedWrite)
                {
                    if (!string.Equals(path, _loadedFrom, StringComparison.OrdinalIgnoreCase))
                        _pending.Clear();
                    _file = Read(path) ?? AnimTreeLayouts.FromBytes(null);
                    _loadedFrom = path;
                    _loadedLength = length;
                    _loadedWrite = write;
                }
                return _file;
            }
        }

        /* The file's layouts (none when there is no file), or null when there is one this build can't read - damaged, being
           written, or from a newer OpenCAGE - which must then be left alone, not written over */
        private static AnimTreeLayouts Read(string path)
        {
            if (path == null || !System.IO.File.Exists(path))
                return AnimTreeLayouts.FromBytes(null);
            try
            {
                byte[] bytes = System.IO.File.ReadAllBytes(path);
                if (bytes.Length == 0)
                    return AnimTreeLayouts.FromBytes(null);
                AnimTreeLayouts file = AnimTreeLayouts.FromBytes(bytes, path);
                if (file.Loaded)
                    return file;
                Debug.Log(LogSystem, path + " is not a layouts file this version reads; its trees are laid out automatically");
            }
            catch (Exception e)
            {
                Debug.Log(LogSystem, "Could not read " + path + ": " + e.Message);
            }
            return null;
        }

        /// <summary>The layout stored for a tree (pending changes included), or null when it is laid out automatically. A copy: change it and Put it back.</summary>
        public static TreeLayout Get(AnimTreeDB database, AnimationTree tree)
        {
            if (database == null || tree == null)
                return null;
            return Get(AnimTreeLayouts.SetHashOf(database, Strings), AnimTreeLayouts.TreeHashOf(tree, Strings));
        }

        public static TreeLayout Get(uint setHash, uint treeHash)
        {
            AnimTreeLayouts file = Current;
            if (_pending.TryGetValue((setHash, treeHash), out TreeLayout pending))
                return pending?.Clone();
            return file.Find(setHash, treeHash)?.Clone();
        }

        /// <summary>Whether a tree has a stored layout (else it is laid out automatically).</summary>
        public static bool Has(AnimTreeDB database, AnimationTree tree) => Get(database, tree) != null;
        #endregion

        #region Changing
        /// <summary>Store a tree's layout (written with the next save). <paramref name="source"/> is told apart in <see cref="Changed"/>.</summary>
        public static void Put(TreeLayout layout, object source = null)
        {
            if (layout == null)
                return;
            _ = Current;
            _pending[(layout.SetHash, layout.TreeHash)] = layout.Clone();
            Changed?.Invoke(layout.SetHash, layout.TreeHash, source);
        }

        /// <summary>Drop a tree's layout, so it is laid out automatically again (written with the next save). True if it had one.</summary>
        public static bool Forget(AnimTreeDB database, AnimationTree tree, object source = null)
        {
            if (database == null || tree == null)
                return false;
            uint set = AnimTreeLayouts.SetHashOf(database, Strings), name = AnimTreeLayouts.TreeHashOf(tree, Strings);
            return ForgetKey(set, name, source);
        }

        /// <summary>Drop whatever layout is stored under these hashes (a tree since renamed). True if there was one.</summary>
        public static bool ForgetKey(uint setHash, uint treeHash, object source = null)
        {
            bool had = Get(setHash, treeHash) != null;
            if (!had)
                return false;
            _pending[(setHash, treeHash)] = null;
            Changed?.Invoke(setHash, treeHash, source);
            return true;
        }

        /// <summary>
        /// A node of the tree is about to be renamed or removed, or the tree is about to change in some other way that moves
        /// node keys: its layout (if any) is taken now, by the nodes themselves, so <see cref="Rekey"/> can store it again
        /// against the keys they have afterwards.
        /// </summary>
        public static Dictionary<AnimationNode, List<NodeLayout>> Hold(AnimTreeDB database, AnimationTree tree, out TreeLayout held)
        {
            held = Get(database, tree);
            Dictionary<AnimationNode, List<NodeLayout>> byNode = new Dictionary<AnimationNode, List<NodeLayout>>(OpenCAGE.AnimTrees.AnimTreeCanvas.ByReference.Instance);
            if (held == null)
                return byNode;
            Dictionary<NodeKey, AnimationNode> nodes = AnimTreeLayouts.KeysOf(tree, Strings).ToDictionary(o => o.Value, o => o.Key);
            foreach (NodeLayout entry in held.Nodes)
            {
                if (!nodes.TryGetValue(entry.Key, out AnimationNode node))
                    continue;
                if (!byNode.TryGetValue(node, out List<NodeLayout> list))
                {
                    list = new List<NodeLayout>();
                    byNode[node] = list;
                }
                list.Add(entry);
            }
            return byNode;
        }

        /// <summary>
        /// Store a layout taken with <see cref="Hold"/> again after the tree changed: every node still in the tree keeps its
        /// positions and copies under its key now; nodes gone drop out, with the links they drew. Nothing when it had no layout.
        /// </summary>
        public static void Rekey(AnimTreeDB database, AnimationTree tree, TreeLayout held, Dictionary<AnimationNode, List<NodeLayout>> byNode, object source = null)
        {
            if (held == null || database == null || tree == null)
                return;
            Dictionary<NodeKey, AnimationNode> before = new Dictionary<NodeKey, AnimationNode>();
            foreach (KeyValuePair<AnimationNode, List<NodeLayout>> entry in byNode)
                foreach (NodeLayout node in entry.Value)
                    before[node.Key] = entry.Key;

            Dictionary<AnimationNode, NodeKey> now = AnimTreeLayouts.KeysOf(tree, Strings);
            TreeLayout layout = new TreeLayout()
            {
                SetHash = AnimTreeLayouts.SetHashOf(database, Strings),
                TreeHash = AnimTreeLayouts.TreeHashOf(tree, Strings),
                Set = held.Set,
                Tree = tree.Name,
                CanvasX = held.CanvasX,
                CanvasY = held.CanvasY,
                CanvasScale = held.CanvasScale,
            };
            foreach (KeyValuePair<AnimationNode, List<NodeLayout>> entry in byNode)
            {
                if (!now.TryGetValue(entry.Key, out NodeKey key))
                    continue;
                foreach (NodeLayout node in entry.Value)
                    layout.Nodes.Add(new NodeLayout() { Key = key, Ghost = node.Ghost, X = node.X, Y = node.Y });
            }
            foreach (LinkLayout link in held.Links)
            {
                if (!before.TryGetValue(link.From, out AnimationNode from) || !now.TryGetValue(from, out NodeKey fromKey)) continue;
                if (!before.TryGetValue(link.To, out AnimationNode to) || !now.TryGetValue(to, out NodeKey toKey)) continue;
                layout.Links.Add(new LinkLayout() { From = fromKey, FromGhost = link.FromGhost, FromPin = link.FromPin, To = toKey, ToGhost = link.ToGhost, ToPin = link.ToPin });
            }

            //A renamed tree is stored under its new name: the old entry goes
            if (layout.SetHash != held.SetHash || layout.TreeHash != held.TreeHash)
                _pending[(held.SetHash, held.TreeHash)] = null;
            Put(layout, source);
        }
        #endregion

        #region Writing
        /// <summary>
        /// Write the pending changes to the file - read afresh first, so changes made on disk since stand for every tree
        /// not changed here. True when written (or nothing to write); false with the reason otherwise, pending kept.
        /// </summary>
        public static bool Save(out string error) => Save(null, out error);

        /// <summary>
        /// Write only the pending changes to these trees (set hash, tree hash), leaving the rest waiting for the trees' own
        /// save - an AI assistant's layout change must not write the user's unsaved ones, which may name nodes as the
        /// unsaved trees do. Null writes them all.
        /// </summary>
        public static bool Save(Func<uint, uint, bool> only, out string error)
        {
            error = null;
            List<KeyValuePair<(uint, uint), TreeLayout>> writing = _pending.Where(o => only == null || only(o.Key.Item1, o.Key.Item2)).ToList();
            if (writing.Count == 0)
                return true;
            string path = FilePath;
            if (path == null)
            {
                error = "the animations are not loaded, so there is nowhere to write the layouts";
                return false;
            }

            AnimTreeLayouts file = Read(path);
            if (file == null)
            {
                error = path + " is not a layouts file this version of OpenCAGE can read (damaged, in use, or from a newer version), so it was not written over";
                return false;
            }
            foreach (KeyValuePair<(uint, uint), TreeLayout> change in writing)
            {
                if (change.Value == null)
                    file.Remove(change.Key.Item1, change.Key.Item2);
                else
                    file.Put(change.Value.Clone());
            }

            try
            {
                Modding.ModServices.CaptureBeforeWrite(path);
                if (file.Trees.Count == 0 && System.IO.File.Exists(path))
                {
                    //Every layout gone: no file at all, as before any was made
                    System.IO.File.Delete(path);
                }
                else if (file.Trees.Count != 0)
                {
                    if (!file.Save(path, true))
                    {
                        error = "could not write " + path;
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                error = "could not write " + path + ": " + e.Message;
                return false;
            }

            foreach (KeyValuePair<(uint, uint), TreeLayout> change in writing)
                _pending.Remove(change.Key);
            _file = null;
            _ = Current;
            Debug.Log(LogSystem, "Wrote " + _file.Trees.Count + " tree layouts to " + path);
            return true;
        }

        #endregion
    }
}
