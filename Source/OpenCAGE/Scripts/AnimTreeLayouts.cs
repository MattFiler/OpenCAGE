using CATHODE;
using CATHODE.Animations;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenCAGE
{
    /// <summary>
    /// How the Animation Tree Editor draws each tree someone has laid out by hand - where every node sits, the extra copies
    /// ("ghosts") a node is drawn as, and which copy draws which link. OpenCAGE's own entry inside ANIMATION.PAK
    /// (<see cref="EntryName"/>), so the layouts go wherever the trees they draw go: a game file check that puts the trees
    /// back takes their layouts with them, and a mod's animation PAK brings its own. The game never asks for it. A tree
    /// with no entry is laid out automatically.
    /// </summary>
    /// <remarks>
    /// Nothing in a tree set has a stable id, so everything is keyed by hashes that survive a save and reload: a tree by
    /// its set's hash and its own name's, a node by its name's hash, its kind, and which of the tree's same-named nodes of
    /// that kind it is (the game's own trees list some parameters and callbacks more than once).
    /// </remarks>
    public class AnimTreeLayouts : CathodeFile
    {
        /// <summary>
        /// The layouts' entry in the animation PAK: a folder of OpenCAGE's own, which the game's files never sit in and no
        /// reader of the PAK's own files picks up.
        /// </summary>
        public const string EntryName = @"DATA\OPENCAGE\ANIMTREELAYOUTS.DAT";

        /// <summary>Where the layouts were kept before they went into the PAK: a file beside it, read once to bring them in.</summary>
        public const string LegacyFileName = "AnimTreeLayouts.dat";

        /// <summary>Is this the layouts' entry of an animation PAK (named either way round with its slashes)?</summary>
        public static bool IsEntry(string entryName) => string.Equals((entryName ?? "").Replace('/', '\\'), EntryName, StringComparison.OrdinalIgnoreCase);

        public List<TreeLayout> Trees = new List<TreeLayout>();
        public static new Implementation Implementation = Implementation.CREATE | Implementation.LOAD | Implementation.SAVE;

        private const uint Magic = 0x594C5441; //ATLY
        private const int Version = 1;

        public AnimTreeLayouts(string path) : base(path) { }
        public AnimTreeLayouts(byte[] data, string path = "") : base(data, path) { }

        /// <summary>A layouts file from its bytes: empty (and not Loaded) when there are none or they are not one.</summary>
        public static AnimTreeLayouts FromBytes(byte[] data, string path = "")
        {
            if (data == null || data.Length == 0)
                return new AnimTreeLayouts(new byte[0], path);
            return new AnimTreeLayouts(data, path);
        }

        #region FILE_IO
        override protected bool LoadInternal(MemoryStream stream)
        {
            List<TreeLayout> trees = new List<TreeLayout>();
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                if (reader.ReadUInt32() != Magic)
                    return false;
                if (reader.ReadInt32() != Version)
                    return false;
                int treeCount = reader.ReadInt32();
                for (int i = 0; i < treeCount; i++)
                {
                    TreeLayout tree = new TreeLayout()
                    {
                        SetHash = reader.ReadUInt32(),
                        TreeHash = reader.ReadUInt32(),
                        Set = reader.ReadString(),
                        Tree = reader.ReadString(),
                        CanvasX = reader.ReadSingle(),
                        CanvasY = reader.ReadSingle(),
                        CanvasScale = reader.ReadSingle(),
                    };
                    int nodeCount = reader.ReadInt32();
                    for (int x = 0; x < nodeCount; x++)
                    {
                        tree.Nodes.Add(new NodeLayout()
                        {
                            Key = ReadKey(reader),
                            Ghost = reader.ReadInt32(),
                            X = reader.ReadInt32(),
                            Y = reader.ReadInt32(),
                        });
                    }
                    int linkCount = reader.ReadInt32();
                    for (int x = 0; x < linkCount; x++)
                    {
                        tree.Links.Add(new LinkLayout()
                        {
                            From = ReadKey(reader),
                            FromGhost = reader.ReadInt32(),
                            FromPin = reader.ReadUInt32(),
                            To = ReadKey(reader),
                            ToGhost = reader.ReadInt32(),
                            ToPin = reader.ReadUInt32(),
                        });
                    }
                    trees.Add(tree);
                }
            }
            Trees = trees;
            return true;
        }

        override protected bool SaveInternal()
        {
            byte[] content = ToBytes();
            string directory = Path.GetDirectoryName(Path.GetFullPath(_filepath));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            //Written beside and swapped in, so a failed write never leaves half a file
            string temp = _filepath + ".tmp";
            File.WriteAllBytes(temp, content);
            if (File.Exists(_filepath))
                File.Replace(temp, _filepath, null);
            else
                File.Move(temp, _filepath);
            return true;
        }

        /// <summary>The file's bytes, every list in a fixed order so the same layouts always write the same file.</summary>
        public byte[] ToBytes()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(Magic);
                    writer.Write(Version);
                    List<TreeLayout> trees = Trees.Where(o => o != null).OrderBy(o => o.SetHash).ThenBy(o => o.TreeHash).ToList();
                    writer.Write(trees.Count);
                    foreach (TreeLayout tree in trees)
                    {
                        writer.Write(tree.SetHash);
                        writer.Write(tree.TreeHash);
                        writer.Write(tree.Set ?? "");
                        writer.Write(tree.Tree ?? "");
                        writer.Write(tree.CanvasX);
                        writer.Write(tree.CanvasY);
                        writer.Write(tree.CanvasScale);

                        List<NodeLayout> nodes = tree.Nodes.Where(o => o != null).OrderBy(o => o.Key).ThenBy(o => o.Ghost).ToList();
                        writer.Write(nodes.Count);
                        foreach (NodeLayout node in nodes)
                        {
                            WriteKey(writer, node.Key);
                            writer.Write(node.Ghost);
                            writer.Write(node.X);
                            writer.Write(node.Y);
                        }

                        List<LinkLayout> links = tree.Links.Where(o => o != null)
                            .OrderBy(o => o.From).ThenBy(o => o.FromPin).ThenBy(o => o.To).ThenBy(o => o.ToPin).ThenBy(o => o.FromGhost).ThenBy(o => o.ToGhost).ToList();
                        writer.Write(links.Count);
                        foreach (LinkLayout link in links)
                        {
                            WriteKey(writer, link.From);
                            writer.Write(link.FromGhost);
                            writer.Write(link.FromPin);
                            WriteKey(writer, link.To);
                            writer.Write(link.ToGhost);
                            writer.Write(link.ToPin);
                        }
                    }
                }
                return stream.ToArray();
            }
        }

        private static NodeKey ReadKey(BinaryReader reader)
        {
            return new NodeKey(reader.ReadUInt32(), reader.ReadInt32(), reader.ReadInt32());
        }

        private static void WriteKey(BinaryWriter writer, NodeKey key)
        {
            writer.Write(key.NameHash);
            writer.Write(key.Kind);
            writer.Write(key.Occurrence);
        }
        #endregion

        #region ACCESSORS
        /// <summary>The stored layout of one tree, or null when it has none (it is laid out automatically).</summary>
        public TreeLayout Find(uint setHash, uint treeHash)
        {
            return Trees.FirstOrDefault(o => o != null && o.SetHash == setHash && o.TreeHash == treeHash);
        }

        /// <summary>Store a tree's layout, replacing the one it had.</summary>
        public void Put(TreeLayout layout)
        {
            if (layout == null)
                return;
            Trees.RemoveAll(o => o == null || (o.SetHash == layout.SetHash && o.TreeHash == layout.TreeHash));
            Trees.Add(layout);
        }

        /// <summary>Drop a tree's layout, so it is laid out automatically again. True if it had one.</summary>
        public bool Remove(uint setHash, uint treeHash)
        {
            return Trees.RemoveAll(o => o == null || (o.SetHash == setHash && o.TreeHash == treeHash)) != 0;
        }
        #endregion

        #region KEYS
        /// <summary>
        /// A tree set's hash: the number its file in ANIMATION.PAK is named by (<c>DATA\ANIM_SYS\&lt;hash&gt;_ANIM_TREE_DB.BIN</c>),
        /// which is the hash of the set's name.
        /// </summary>
        public static uint SetHashOf(AnimTreeDB database, AnimationStrings strings)
        {
            if (database == null)
                return 0;
            string file = Path.GetFileName(database.Filepath ?? "");
            int underscore = file.IndexOf('_');
            if (underscore > 0 && uint.TryParse(file.Substring(0, underscore), out uint hash))
                return hash;
            return strings != null ? strings.GetID(database.Set ?? "") : Utilities.AnimationHashedString(database.Set ?? "");
        }

        /// <summary>A tree's hash: its name's id in the debug string table, as the tree set file stores it.</summary>
        public static uint TreeHashOf(AnimationTree tree, AnimationStrings strings)
        {
            if (tree == null)
                return 0;
            return NameHash(tree.Name, strings);
        }

        /// <summary>
        /// Every node of the tree (and the tree itself) with the key its layout entries use, by reference. The key holds the
        /// name's id from the debug string table - which a name the table does not know reads back as, decimal text and all.
        /// </summary>
        public static Dictionary<AnimationNode, NodeKey> KeysOf(AnimationTree tree, AnimationStrings strings)
        {
            Dictionary<AnimationNode, NodeKey> keys = new Dictionary<AnimationNode, NodeKey>(ReferenceComparer.Instance);
            if (tree == null)
                return keys;
            keys[tree] = new NodeKey(TreeHashOf(tree, strings), (int)NodeType.ANIM_Tree_Top_Level, 0);

            Dictionary<(uint, int), int> seen = new Dictionary<(uint, int), int>();
            foreach (AnimationNode node in tree.Nodes)
            {
                if (node == null || keys.ContainsKey(node))
                    continue;
                uint name = NameHash(node.Name, strings);
                int kind = KindOf(node.Type);
                seen.TryGetValue((name, kind), out int occurrence);
                seen[(name, kind)] = occurrence + 1;
                keys[node] = new NodeKey(name, kind, occurrence);
            }
            return keys;
        }

        /// <summary>The kind a node's key uses: its type, except that a 3D parametric node is filed as 2D (one without a Z binding is written as 2D and reads back as one).</summary>
        public static int KindOf(NodeType type)
        {
            return (int)(type == NodeType.ANIM_3DParametric ? NodeType.ANIM_2DParametric : type);
        }

        private static uint NameHash(string name, AnimationStrings strings)
        {
            name = name ?? "";
            return strings != null ? strings.GetID(name) : Utilities.AnimationHashedString(name);
        }

        private sealed class ReferenceComparer : IEqualityComparer<AnimationNode>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(AnimationNode x, AnimationNode y) => ReferenceEquals(x, y);
            public int GetHashCode(AnimationNode obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
        #endregion

        #region STRUCTURES
        /// <summary>One tree's layout.</summary>
        public class TreeLayout
        {
            public uint SetHash;
            public uint TreeHash;

            /// <summary>The set's and the tree's names when the layout was stored: for people and messages only - the hashes are the key.</summary>
            public string Set = "";
            public string Tree = "";

            /// <summary>The view: canvas centre and zoom (zoom 0 = none stored).</summary>
            public float CanvasX;
            public float CanvasY;
            public float CanvasScale;

            /// <summary>Every drawn node: one entry per copy.</summary>
            public List<NodeLayout> Nodes = new List<NodeLayout>();

            /// <summary>Only the links drawn on a copy other than the first at either end.</summary>
            public List<LinkLayout> Links = new List<LinkLayout>();

            public TreeLayout Clone()
            {
                return new TreeLayout()
                {
                    SetHash = SetHash,
                    TreeHash = TreeHash,
                    Set = Set,
                    Tree = Tree,
                    CanvasX = CanvasX,
                    CanvasY = CanvasY,
                    CanvasScale = CanvasScale,
                    Nodes = Nodes.Where(o => o != null).Select(o => new NodeLayout() { Key = o.Key, Ghost = o.Ghost, X = o.X, Y = o.Y }).ToList(),
                    Links = Links.Where(o => o != null).Select(o => new LinkLayout() { From = o.From, FromGhost = o.FromGhost, FromPin = o.FromPin, To = o.To, ToGhost = o.ToGhost, ToPin = o.ToPin }).ToList(),
                };
            }
        }

        /// <summary>What a node is, in a form that survives the tree being saved and read again.</summary>
        public struct NodeKey : IEquatable<NodeKey>, IComparable<NodeKey>
        {
            public uint NameHash;
            public int Kind;
            public int Occurrence;

            public NodeKey(uint nameHash, int kind, int occurrence)
            {
                NameHash = nameHash;
                Kind = kind;
                Occurrence = occurrence;
            }

            public bool Equals(NodeKey other) => NameHash == other.NameHash && Kind == other.Kind && Occurrence == other.Occurrence;
            public override bool Equals(object obj) => obj is NodeKey other && Equals(other);
            public override int GetHashCode() => unchecked(((int)NameHash * 397 ^ Kind) * 397 ^ Occurrence);
            public static bool operator ==(NodeKey a, NodeKey b) => a.Equals(b);
            public static bool operator !=(NodeKey a, NodeKey b) => !a.Equals(b);

            public int CompareTo(NodeKey other)
            {
                int c = Kind.CompareTo(other.Kind);
                if (c != 0) return c;
                c = NameHash.CompareTo(other.NameHash);
                if (c != 0) return c;
                return Occurrence.CompareTo(other.Occurrence);
            }

            public override string ToString() => Kind + ":" + NameHash + (Occurrence == 0 ? "" : "#" + Occurrence);
        }

        /// <summary>Where one copy of a node is drawn. Copy 0 is the node's first; ghosts are 1 and up.</summary>
        public class NodeLayout
        {
            public NodeKey Key;
            public int Ghost;
            public int X;
            public int Y;
        }

        /// <summary>
        /// Which copies draw a link. <see cref="From"/> is the node whose output pin draws it (a flow link's parent, a value
        /// link's provider) and <see cref="To"/> the node whose input takes it (the child; the consumer, on one of its top pins).
        /// </summary>
        public class LinkLayout
        {
            public NodeKey From;
            public int FromGhost;
            public uint FromPin;
            public NodeKey To;
            public int ToGhost;
            public uint ToPin;
        }
        #endregion
    }
}
