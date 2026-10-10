using CATHODE;
using CATHODE.Animations;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using static OpenCAGE.AnimTreeLayouts;

namespace OpenCAGE
{
    /// <summary>
    /// The animation tree layouts people have made by hand (<see cref="AnimTreeLayouts"/>: an entry of OpenCAGE's own inside
    /// ANIMATION.PAK, so they stay with the trees they draw) - the Animation Tree Editor's and the AI assistant tools' shared
    /// copy. A tree with none is laid out automatically. Changes wait here until ANIMATION.PAK is written: whatever writes it
    /// puts them into the PAK's entry first (<see cref="Commit(Func{uint, uint, bool}, out string)"/>) - all of them when it
    /// writes every tree, only those of the trees it writes otherwise. Read from the PAK in memory; one changed on disk since
    /// (by the Mod Manager, say) is never written over from memory (<see cref="AnimationPakWrite"/>), so the layouts and
    /// trees there stay as they were put together.
    /// </summary>
    /// <remarks>UI thread only.</remarks>
    public static class AnimTreeLayoutManager
    {
        private const string LogSystem = "Anim Tree Layouts";

        //The layouts as the PAK in memory holds them, and which PAK and entry content they were read from
        private static AnimTreeLayouts _file;
        private static PAK2 _loadedPak;
        private static byte[] _loadedContent;

        //Changes not yet in the PAK: a layout to store, or null to drop the tree's layout
        private static readonly Dictionary<(uint, uint), TreeLayout> _pending = new Dictionary<(uint, uint), TreeLayout>();

        /// <summary>A tree's layout changed (set hash, tree hash, and whoever changed it - an open graph reloads unless it was the one).</summary>
        public static event Action<uint, uint, object> Changed;

        /// <summary>The animation PAK the layouts are kept in (null when the animations aren't loaded).</summary>
        public static string PakPath => Singleton.Global?.Animations?.PAK?.Filepath;

        private static PAK2 Pak => Singleton.Global?.Animations?.PAK;
        private static AnimationStrings Strings => Singleton.Global?.Animations?.StringsDebug;

        public static bool HasPending => _pending.Count != 0;

        #region Reading
        /* The layouts as the PAK in memory holds them, read again once that is another PAK (the animations loaded again) or
           its entry was replaced */
        private static AnimTreeLayouts Current
        {
            get
            {
                PAK2 pak = Pak;
                PAK2.File entry = EntryOf(pak);
                if (_file == null || !ReferenceEquals(pak, _loadedPak) || !ReferenceEquals(entry?.Content, _loadedContent))
                {
                    if (!ReferenceEquals(pak, _loadedPak))
                        _pending.Clear();
                    _file = Read(entry) ?? AnimTreeLayouts.FromBytes(null);
                    _loadedPak = pak;
                    _loadedContent = entry?.Content;
                }
                return _file;
            }
        }

        private static PAK2.File EntryOf(PAK2 pak) => pak?.Entries.FirstOrDefault(o => AnimTreeLayouts.IsEntry(o.Filename));

        /* The entry's layouts (none when there is no entry), or null when there is one this build can't read - damaged, or from
           a newer OpenCAGE - which must then be left alone, not written over */
        private static AnimTreeLayouts Read(PAK2.File entry)
        {
            if (entry?.Content == null || entry.Content.Length == 0)
                return AnimTreeLayouts.FromBytes(null);
            try
            {
                AnimTreeLayouts file = AnimTreeLayouts.FromBytes(entry.Content, entry.Filename);
                if (file.Loaded)
                    return file;
                Debug.Log(LogSystem, "ANIMATION.PAK's " + entry.Filename + " is not layouts this version reads; its trees are laid out automatically");
            }
            catch (Exception e)
            {
                Debug.Log(LogSystem, "Could not read ANIMATION.PAK's " + entry.Filename + ": " + e.Message);
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
        /// Put every pending change into ANIMATION.PAK's layouts entry in memory, for a write of the PAK that carries every
        /// tree (the Animation Tree Editor's Save, or anything saving the whole animation set). Call it just before the PAK is
        /// written. True when done (or nothing to do); false with the reason otherwise, the changes kept waiting.
        /// </summary>
        public static bool Commit(out string error) => Commit(null, out error);

        /// <summary>
        /// Put only the pending changes to these trees (set hash, tree hash) into the PAK's layouts entry, leaving the rest
        /// waiting for their trees' own save - a write of some trees (or of a layout alone) must not carry the layouts of
        /// others' unsaved edits, which may name nodes as those edits do. Null puts them all in.
        /// </summary>
        public static bool Commit(Func<uint, uint, bool> only, out string error)
        {
            error = null;
            _ = Current;
            List<KeyValuePair<(uint, uint), TreeLayout>> writing = _pending.Where(o => only == null || only(o.Key.Item1, o.Key.Item2)).ToList();
            if (writing.Count == 0)
                return true;
            PAK2 pak = Pak;
            if (pak == null)
            {
                error = "the animations are not loaded, so there is nowhere to keep the layouts";
                return false;
            }

            PAK2.File entry = EntryOf(pak);
            AnimTreeLayouts file = Read(entry);
            if (file == null)
            {
                error = "ANIMATION.PAK's layouts are not ones this version of OpenCAGE can read (damaged, or from a newer version), so they were not written over";
                return false;
            }
            foreach (KeyValuePair<(uint, uint), TreeLayout> change in writing)
            {
                if (change.Value == null)
                    file.Remove(change.Key.Item1, change.Key.Item2);
                else
                    file.Put(change.Value.Clone());
            }

            if (file.Trees.Count == 0)
            {
                //Every layout gone: no entry at all, as before any was made
                if (entry != null)
                    pak.Entries.Remove(entry);
            }
            else if (entry != null)
                entry.Content = file.ToBytes();
            else
                pak.Entries.Add(new PAK2.File() { Filename = AnimTreeLayouts.EntryName, Content = file.ToBytes() });

            foreach (KeyValuePair<(uint, uint), TreeLayout> change in writing)
                _pending.Remove(change.Key);
            _file = null;
            _ = Current;
            Debug.Log(LogSystem, "ANIMATION.PAK holds " + _file.Trees.Count + " tree layouts now");
            return true;
        }

        #endregion
    }
}
