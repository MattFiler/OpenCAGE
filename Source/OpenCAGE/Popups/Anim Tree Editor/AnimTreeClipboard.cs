using CATHODE.Animations;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;

namespace OpenCAGE.AnimTrees
{
    /// <summary>
    /// The Animation Tree Editor's clipboard: nodes copied off one tree's canvas, pasted as new nodes (copies of them, into any
    /// tree) or as ghosts of the same nodes (into the tree they came from) - as the scripting flowgraph's Paste and Paste
    /// Reference do with entities. It holds the nodes themselves, not copies: a paste takes them as they are by then.
    /// </summary>
    public static class AnimTreeClipboard
    {
        public sealed class Entry
        {
            /// <summary>The copied node (one entry per canvas node copied: a node and its ghost are two).</summary>
            public AnimationNode Node;
            /// <summary>Where it sat, from the top-left of everything copied.</summary>
            public Point Offset;
        }

        private static readonly List<Entry> _entries = new List<Entry>();

        /// <summary>The tree the nodes were copied from.</summary>
        public static AnimationTree SourceTree { get; private set; }

        /// <summary>Is there anything left to paste: a copied node still in the tree it was copied from?</summary>
        public static bool HasContent => Live().Count != 0;

        /// <summary>
        /// Copy these canvas nodes of a tree (the tree's own top-level node is skipped: every tree has one). Nothing changes
        /// when none of them can be copied.
        /// </summary>
        public static void Copy(AnimationTree tree, IEnumerable<STNode> nodes)
        {
            List<STNode> copied = (nodes ?? Enumerable.Empty<STNode>())
                .Where(o => o?.AnimationNode != null && !(o.AnimationNode is AnimationTree) && InTree(tree, o.AnimationNode))
                .ToList();
            if (tree == null || copied.Count == 0)
                return;

            int minX = copied.Min(o => o.Left), minY = copied.Min(o => o.Top);
            _entries.Clear();
            foreach (STNode node in copied)
                _entries.Add(new Entry() { Node = node.AnimationNode, Offset = new Point(node.Left - minX, node.Top - minY) });
            SourceTree = tree;
        }

        /// <summary>
        /// The entries whose node is still in the tree it was copied from (one deleted since is left out), in copy order - none
        /// once that tree is no longer loaded (the animations read again from disk).
        /// </summary>
        public static List<Entry> Live()
        {
            if (SourceTree == null || !Loaded(SourceTree))
                return new List<Entry>();
            return _entries.Where(o => InTree(SourceTree, o.Node)).ToList();
        }

        private static bool Loaded(AnimationTree tree)
        {
            CathodeLib.Animation animations = Singleton.Global.Animations;
            return animations?.Trees != null && animations.Trees.Any(db => db?.Entries != null && db.Entries.Any(o => ReferenceEquals(o, tree)));
        }

        /// <summary>The live entries with each node once (its first entry): what a paste of new nodes makes a copy of.</summary>
        public static List<Entry> LiveNodes()
        {
            HashSet<AnimationNode> seen = new HashSet<AnimationNode>(AnimTreeCanvas.ByReference.Instance);
            return Live().Where(o => seen.Add(o.Node)).ToList();
        }

        /// <summary>Can the clipboard be pasted into this tree as ghosts of the copied nodes - is it the tree they came from?</summary>
        public static bool CanPasteReferencesInto(AnimationTree tree) => tree != null && ReferenceEquals(tree, SourceTree) && Live().Count != 0;

        private static bool InTree(AnimationTree tree, AnimationNode node) => tree != null && node != null && tree.Nodes.Any(o => ReferenceEquals(o, node));
    }

    /// <summary>
    /// Copies of animation nodes, for a paste of new ones: every setting copied (deeply - a selector's states and a random
    /// leaf's pool are the copy's own), and every link to another node kept only where that node is copied too, re-pointed at
    /// its copy (the scripting flowgraph's rule: a paste keeps the links that ran between what was copied). A reference the
    /// canvas draws no link for is a setting rather than a link (see <see cref="IsSetting"/>): kept, by the caller's rule. The
    /// copies keep their originals' names: naming them and adding them to a tree is the caller's.
    /// </summary>
    public static class AnimNodeCopier
    {
        private static readonly MethodInfo _memberwiseClone = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

        //The references an animation holds that the canvas has no pin for (its context and converge parameters)
        private static readonly HashSet<string> _settingReferences = new HashSet<string>() { "OptionalContextParam", "OptionalConvergeVector", "OptionalConvergeFloat", "OptionalAnimationContext" };

        /// <summary>
        /// Is this field of the node a reference the canvas draws no link for - one the user can't see or draw again, so a
        /// paste must not drop it: an animation's context and converge parameters, and all a 4D parametric reads (the canvas
        /// gives it no pins).
        /// </summary>
        public static bool IsSetting(AnimationNode owner, FieldInfo field)
        {
            return typeof(AnimationNode).IsAssignableFrom(field.FieldType) && (_settingReferences.Contains(field.Name) || owner.Type == NodeType.ANIM_4DParametric);
        }

        /// <summary>
        /// A copy of each node (by reference), the links between them re-pointed at the copies, and links to nodes not copied
        /// dropped. A setting (<see cref="IsSetting"/>) referring to a node not copied takes what <paramref name="setting"/>
        /// gives for that node instead (dropped too without it, or when it gives null).
        /// </summary>
        public static Dictionary<AnimationNode, AnimationNode> Copy(IEnumerable<AnimationNode> nodes, Func<AnimationNode, AnimationNode> setting = null)
        {
            Dictionary<AnimationNode, AnimationNode> copies = new Dictionary<AnimationNode, AnimationNode>(AnimTreeCanvas.ByReference.Instance);
            foreach (AnimationNode node in nodes)
            {
                if (node == null || node is AnimationTree || copies.ContainsKey(node))
                    continue;
                copies[node] = (AnimationNode)_memberwiseClone.Invoke(node, null);
            }

            Func<AnimationNode, AnimationNode> map = o => o != null && copies.TryGetValue(o, out AnimationNode copy) ? copy : null;
            foreach (KeyValuePair<AnimationNode, AnimationNode> pair in copies)
                CopyFields(pair.Key, pair.Value, map, setting);
            return copies;
        }

        /* Every field of the original into the copy, each value copied deeply (a node: its copy, or nothing - or for a node's
           own settings, what the caller says) */
        private static void CopyFields(object from, object to, Func<AnimationNode, AnimationNode> map, Func<AnimationNode, AnimationNode> setting = null)
        {
            AnimationNode owner = from as AnimationNode;
            for (Type type = from.GetType(); type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    object original = field.GetValue(from);
                    object value;
                    if (owner != null && original is AnimationNode target && IsSetting(owner, field))
                        value = map(target) ?? setting?.Invoke(target);
                    else
                        value = CopyValue(original, map);
                    //A node field takes only a node of its own type (a foot sync selector's strikes are animations)
                    if (value != null && !field.FieldType.IsInstanceOfType(value))
                        value = null;
                    field.SetValue(to, value);
                }
            }
        }

        private static object CopyValue(object value, Func<AnimationNode, AnimationNode> map)
        {
            if (value == null)
                return null;
            if (value is AnimationNode node)
                return map(node);

            Type type = value.GetType();
            if (type.IsValueType || value is string)
                return value;
            if (value is Array array)
            {
                Array copy = (Array)array.Clone();
                for (int i = 0; i < array.Length; i++)
                    copy.SetValue(CopyValue(array.GetValue(i), map), i);
                return copy;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                //A list of nodes (a node's children) keeps only the ones copied
                IList copy = (IList)Activator.CreateInstance(type);
                foreach (object item in (IList)value)
                {
                    object copied = CopyValue(item, map);
                    if (item is AnimationNode && copied == null)
                        continue;
                    copy.Add(copied);
                }
                return copy;
            }

            //Anything else a node holds (a state, a pooled animation, a property's value): a copy of its own
            object clone = _memberwiseClone.Invoke(value, null);
            CopyFields(value, clone, map);
            return clone;
        }
    }
}
