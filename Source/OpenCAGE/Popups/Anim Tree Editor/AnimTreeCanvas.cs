using CATHODE;
using CATHODE.Animations;
using CATHODE.Scripting;
using CATHODE.Scripting.Refactor;
using CathodeLib;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using static OpenCAGE.AnimTreeLayouts;

namespace OpenCAGE.AnimTrees
{
    /// <summary>
    /// One animation tree drawn on a node canvas: the node for each animation node and any extra copies of it ("ghosts"),
    /// every link the tree holds drawn exactly once, laid out from the tree's saved layout or automatically. The Animation
    /// Tree Editor draws on its own canvas with it; the AI assistant tools draw on an off-screen one, so both size and
    /// place nodes the same way.
    /// </summary>
    /// <remarks>
    /// Ghosts exist on the canvas only - the tree holds each node once. Which copy draws a link is the layout's business:
    /// drawing a link the tree already has onto another copy just moves it there.
    /// </remarks>
    public sealed class AnimTreeCanvas
    {
        public STNodeEditor Editor { get; }
        public AnimTreeDB Database { get; }
        public AnimationTree Tree { get; }
        private readonly AnimationStrings _strings;

        //Every copy of every node on the canvas, by reference, in copy order (copy 0 first)
        private readonly Dictionary<AnimationNode, List<STNode>> _copies = new Dictionary<AnimationNode, List<STNode>>(ByReference.Instance);

        //While the canvas connects or disconnects pins itself: the editor's link events are not the user's then
        private int _building;
        public bool Building => _building != 0;

        /// <summary>Where a tree with no layout is drawn from: its top-left corner.</summary>
        public static readonly Point Origin = new Point(80, 80);

        public AnimTreeCanvas(STNodeEditor editor, AnimTreeDB database, AnimationTree tree, AnimationStrings strings)
        {
            Editor = editor;
            Database = database;
            Tree = tree;
            _strings = strings;
        }

        #region Identity
        /// <summary>The tree set's name as the editor lists it.</summary>
        public static string SetNameOf(AnimTreeDB database) => database?.Entries.FirstOrDefault()?.Set ?? database?.Set ?? "";

        public uint SetHash => AnimTreeLayouts.SetHashOf(Database, _strings);
        public uint TreeHash => AnimTreeLayouts.TreeHashOf(Tree, _strings);

        /// <summary>The copies of a node drawn on the canvas, copy 0 first (empty when it is not drawn).</summary>
        public IReadOnlyList<STNode> CopiesOf(AnimationNode node)
        {
            if (node != null && _copies.TryGetValue(node, out List<STNode> copies))
                return copies;
            return new List<STNode>();
        }

        /// <summary>Which copy of its node this canvas node is (0 = the first), or -1 when it is not this canvas's.</summary>
        public int CopyIndexOf(STNode node)
        {
            if (node?.AnimationNode == null || !_copies.TryGetValue(node.AnimationNode, out List<STNode> copies))
                return -1;
            return copies.IndexOf(node);
        }

        /// <summary>Every node in the tree, the tree itself first, each once.</summary>
        private List<AnimationNode> ModelNodes()
        {
            List<AnimationNode> nodes = new List<AnimationNode>() { Tree };
            HashSet<AnimationNode> seen = new HashSet<AnimationNode>(ByReference.Instance) { Tree };
            foreach (AnimationNode node in Tree.Nodes)
                if (node != null && seen.Add(node))
                    nodes.Add(node);
            return nodes;
        }
        #endregion

        #region Pins
        /// <summary>The pins every node of a type is drawn with: a left trigger for nodes that sit in the flow, right outputs for its children, top pins for what it reads, a bottom value for what reads it.</summary>
        public sealed class NodePins
        {
            public bool Trigger;
            public List<ShortGuid> Outputs = new List<ShortGuid>();
            public List<ShortGuid> Tops = new List<ShortGuid>();
            public bool Value;
        }

        /// <summary>
        /// The node types a tree set file can hold in the flow (as a child of the tree or of another node): anything else
        /// written there makes the whole set unreadable, so only these take a trigger.
        /// </summary>
        public static bool IsFlowType(NodeType type)
        {
            switch (type)
            {
                case NodeType.ANIM_Animation:
                case NodeType.ANIM_Randomised_Animation:
                case NodeType.ANIM_Selector:
                case NodeType.ANIM_Enumerated_Selector:
                case NodeType.ANIM_Ranged_Selector:
                case NodeType.ANIM_Foot_Sync_Selector:
                case NodeType.ANIM_Parametric:
                case NodeType.ANIM_2DParametric:
                case NodeType.ANIM_3DParametric:
                case NodeType.ANIM_4DParametric:
                case NodeType.ANIM_Additive_Blend:
                case NodeType.ANIM_Parametric_Additive_Blend:
                case NodeType.ANIM_Bone_Mask:
                case NodeType.ANIM_IK:
                case NodeType.ANIM_Weighted:
                    return true;
            }
            return false;
        }

        public static NodePins PinsOf(AnimationNode node)
        {
            NodePins pins = new NodePins();
            NodeType type = node.Type;
            pins.Trigger = IsFlowType(type);

            switch (type)
            {
                case NodeType.ANIM_Animation:
                case NodeType.ANIM_2DParametric:
                case NodeType.ANIM_3DParametric:
                case NodeType.ANIM_AutoFloatParameter:
                case NodeType.ANIM_Parameter:
                case NodeType.ANIM_FloatInterpolator:
                case NodeType.ANIM_Callback:
                case NodeType.ANIM_Property:
                case NodeType.ANIM_Event_Callback:
                    pins.Value = true;
                    break;
                case NodeType.ANIM_Selector:
                case NodeType.ANIM_Enumerated_Selector:
                case NodeType.ANIM_Ranged_Selector:
                case NodeType.ANIM_Parametric:
                    pins.Outputs.AddRange(ShortGuids.States.Take(type == NodeType.ANIM_Ranged_Selector ? 8 : 16));
                    break;
                case NodeType.ANIM_Foot_Sync_Selector:
                    pins.Outputs.Add(ShortGuids.LeftStrikeChild);
                    pins.Outputs.Add(ShortGuids.RightStrikeChild);
                    break;
                case NodeType.ANIM_Additive_Blend:
                case NodeType.ANIM_Parametric_Additive_Blend:
                    pins.Outputs.Add(ShortGuids.base_node);
                    pins.Outputs.Add(ShortGuids.additive_node);
                    break;
                case NodeType.ANIM_Tree_Top_Level:
                    pins.Outputs.Add(ShortGuids.NODES);
                    break;
                case NodeType.ANIM_Weighted:
                    pins.Outputs.Add(ShortGuids.child);
                    break;
            }

            switch (type)
            {
                case NodeType.ANIM_Animation:
                    pins.Tops.Add(ShortGuids.Callback);
                    break;
                case NodeType.ANIM_2DParametric:
                case NodeType.ANIM_3DParametric:
                    pins.Tops.Add(ShortGuids.ParameterBindingX);
                    pins.Tops.Add(ShortGuids.ParameterBindingY);
                    if (type == NodeType.ANIM_3DParametric)
                        pins.Tops.Add(ShortGuids.ParameterBindingZ);
                    pins.Tops.Add(ShortGuids.OverflowCallback);
                    break;
                case NodeType.ANIM_Selector:
                case NodeType.ANIM_Enumerated_Selector:
                case NodeType.ANIM_Ranged_Selector:
                case NodeType.ANIM_Parametric:
                    pins.Tops.Add(ShortGuids.ParameterBinding);
                    break;
                case NodeType.ANIM_IK:
                    pins.Tops.Add(ShortGuids.IkEffector);
                    break;
                case NodeType.ANIM_Parametric_Additive_Blend:
                    pins.Tops.Add(ShortGuids.WeightControlParameter);
                    break;
                case NodeType.ANIM_Weighted:
                    pins.Tops.Add(ShortGuids.Parameter);
                    break;
                case NodeType.ANIM_FloatInterpolator:
                    pins.Tops.Add(ShortGuids.SourceParameter);
                    break;
                case NodeType.ANIM_Property_Listener:
                    pins.Tops.Add(ShortGuids.LeafNode);
                    break;
                case NodeType.ANIM_Randomised_Animation:
                    pins.Tops.Add(ShortGuids.Callback);
                    pins.Tops.Add(ShortGuids.RandomCallback); //auto generated: the node name with #@RAND@# at the end
                    break;
            }
            return pins;
        }

        /// <summary>A canvas node for an animation node, with its pins, added to the canvas.</summary>
        private STNode MakeNode(AnimationNode animNode)
        {
            STNode node = new STNode();
            node.SizeToTitle = true;
            node.AnimationNode = animNode;
            node.SetOpenCAGEColour(FlowgraphLayoutManager.GetColourForAnimEntity(animNode.Type.ToString()));
            node.IconId = (int)EditorIcons.ForAnimNodeType(animNode.Type); //what it shows zoomed out
            Editor.Nodes.Add(node);

            NodePins pins = PinsOf(animNode);
            if (pins.Trigger)
                node.AddInputOption(ShortGuids.trigger);
            foreach (ShortGuid output in pins.Outputs)
                node.AddOutputOption(output);
            if (pins.Value)
                node.AddBottomOption(ShortGuids.value);
            foreach (ShortGuid top in pins.Tops)
                node.AddTopOption(top);

            if (!_copies.TryGetValue(animNode, out List<STNode> copies))
            {
                copies = new List<STNode>();
                _copies[animNode] = copies;
            }
            copies.Add(node);
            return node;
        }
        #endregion

        #region Links
        /// <summary>
        /// A link the tree holds, as the canvas draws it: from an output pin of <see cref="From"/> (a flow child's parent,
        /// or a value's provider) to an input pin of <see cref="To"/> (the child's trigger, or one of the consumer's top pins).
        /// </summary>
        public struct ModelLink
        {
            public AnimationNode From;
            public ShortGuid FromPin;
            public AnimationNode To;
            public ShortGuid ToPin;
            public bool Flow => ToPin == ShortGuids.trigger;
        }

        /// <summary>Every link in the tree the canvas can draw, in one fixed order (the tree first, then its nodes in order, each node's fields in order).</summary>
        public static List<ModelLink> LinksOf(AnimationTree tree)
        {
            List<ModelLink> links = new List<ModelLink>();
            HashSet<AnimationNode> inTree = new HashSet<AnimationNode>(ByReference.Instance) { tree };
            foreach (AnimationNode node in tree.Nodes)
                if (node != null) inTree.Add(node);

            void Flow(AnimationNode parent, ShortGuid pin, AnimationNode child)
            {
                if (child == null || !inTree.Contains(child) || !PinsOf(child).Trigger)
                    return;
                links.Add(new ModelLink() { From = parent, FromPin = pin, To = child, ToPin = ShortGuids.trigger });
            }
            void Value(AnimationNode consumer, ShortGuid pin, AnimationNode provider)
            {
                if (provider == null || !inTree.Contains(provider) || !PinsOf(provider).Value)
                    return;
                links.Add(new ModelLink() { From = provider, FromPin = ShortGuids.value, To = consumer, ToPin = pin });
            }

            HashSet<AnimationNode> done = new HashSet<AnimationNode>(ByReference.Instance);
            foreach (AnimationNode node in new AnimationNode[] { tree }.Concat(tree.Nodes))
            {
                if (node == null || !done.Add(node))
                    continue;
                switch (node.Type)
                {
                    case NodeType.ANIM_Animation:
                        if (node is LeafNode leaf) Value(node, ShortGuids.Callback, leaf.Callback);
                        break;
                    case NodeType.ANIM_Property_Listener:
                        if (node is PropertyListenerNode listener) Value(node, ShortGuids.LeafNode, listener.LeafNode);
                        break;
                    case NodeType.ANIM_2DParametric:
                    case NodeType.ANIM_3DParametric:
                        if (node is Parametric2DNode parametric2D)
                        {
                            Value(node, ShortGuids.ParameterBindingX, parametric2D.ParameterBindingX);
                            Value(node, ShortGuids.ParameterBindingY, parametric2D.ParameterBindingY);
                            if (node.Type == NodeType.ANIM_3DParametric && node is Parametric3DNode parametric3D)
                                Value(node, ShortGuids.ParameterBindingZ, parametric3D.ParameterBindingZ);
                            Value(node, ShortGuids.OverflowCallback, parametric2D.OverflowCallback);
                        }
                        break;
                    case NodeType.ANIM_Selector:
                    case NodeType.ANIM_Enumerated_Selector:
                        if (node is SelectorNode selector)
                        {
                            for (int i = 0; i < selector.States.Length && i < ShortGuids.States.Length; i++)
                                Flow(node, ShortGuids.States[i], selector.States[i]?.Node);
                            Value(node, ShortGuids.ParameterBinding, selector.ParameterBinding);
                        }
                        break;
                    case NodeType.ANIM_Ranged_Selector:
                        if (node is RangedSelectorNode ranged)
                        {
                            for (int i = 0; i < ranged.States.Length && i < ShortGuids.States.Length; i++)
                                Flow(node, ShortGuids.States[i], ranged.States[i]?.Node);
                            Value(node, ShortGuids.ParameterBinding, ranged.ParameterBinding);
                        }
                        break;
                    case NodeType.ANIM_Parametric:
                        if (node is ParametricNode parametric)
                        {
                            for (int i = 0; i < parametric.States.Length && i < ShortGuids.States.Length; i++)
                                Flow(node, ShortGuids.States[i], parametric.States[i]?.Node);
                            Value(node, ShortGuids.ParameterBinding, parametric.ParameterBinding);
                        }
                        break;
                    case NodeType.ANIM_Foot_Sync_Selector:
                        if (node is FootSyncSelectorNode footSync)
                        {
                            Flow(node, ShortGuids.LeftStrikeChild, footSync.LeftStrikeChild);
                            Flow(node, ShortGuids.RightStrikeChild, footSync.RightStrikeChild);
                        }
                        break;
                    case NodeType.ANIM_Additive_Blend:
                    case NodeType.ANIM_Parametric_Additive_Blend:
                        if (node is AdditiveBlendNode additive)
                        {
                            Flow(node, ShortGuids.base_node, additive.BaseNode);
                            Flow(node, ShortGuids.additive_node, additive.AdditiveNode);
                            if (node is ParametricAdditiveBlendNode parametricAdditive)
                                Value(node, ShortGuids.WeightControlParameter, parametricAdditive.WeightControlParameter);
                        }
                        break;
                    case NodeType.ANIM_IK:
                        if (node is IkNode ik) Value(node, ShortGuids.IkEffector, ik.IkEffector);
                        break;
                    case NodeType.ANIM_FloatInterpolator:
                        if (node is FloatInterpolatorNode interpolator) Value(node, ShortGuids.SourceParameter, interpolator.SourceParameter);
                        break;
                    case NodeType.ANIM_Randomised_Animation:
                        if (node is RandomisedLeafNode randomised)
                        {
                            Value(node, ShortGuids.Callback, randomised.Callback);
                            Value(node, ShortGuids.RandomCallback, randomised.RandomCallback);
                        }
                        break;
                    case NodeType.ANIM_Tree_Top_Level:
                        foreach (AnimationNode child in ((AnimationTree)node).Children)
                            Flow(node, ShortGuids.NODES, child);
                        break;
                    case NodeType.ANIM_Weighted:
                        if (node is WeightedNode weighted)
                        {
                            Value(node, ShortGuids.Parameter, weighted.Parameter);
                            Flow(node, ShortGuids.child, weighted.Child);
                        }
                        break;
                }
            }
            return links;
        }

        private static STNodeOption OutputPin(STNode node, ShortGuid pin) => pin == ShortGuids.value ? node.GetBottomOption(pin) : node.GetOutputOption(pin);
        private static STNodeOption InputPin(STNode node, ShortGuid pin) => pin == ShortGuids.trigger ? node.GetInputOption(pin) : node.GetTopOption(pin);

        /// <summary>The pin on <paramref name="copy"/> matching <paramref name="pin"/> (same name, same side).</summary>
        private static STNodeOption SamePin(STNode copy, STNodeOption pin)
        {
            switch (pin.Location)
            {
                case PinLocation.Left: return copy.GetInputOption(pin.ShortGUID);
                case PinLocation.Right: return copy.GetOutputOption(pin.ShortGUID);
                case PinLocation.Top: return copy.GetTopOption(pin.ShortGUID);
                default: return copy.GetBottomOption(pin.ShortGUID);
            }
        }

        /// <summary>Every link drawn on the canvas: the output pin and the input pin it reaches.</summary>
        public List<(STNodeOption output, STNodeOption input)> DrawnLinks()
        {
            List<(STNodeOption, STNodeOption)> drawn = new List<(STNodeOption, STNodeOption)>();
            foreach (STNode node in Editor.Nodes.ToArray())
            {
                foreach (STNodeOption output in node.GetOutputOptions().Concat(node.GetBottomOptions()))
                {
                    if (output == null || output == STNodeOption.Empty)
                        continue;
                    foreach (STNodeOption input in output.GetConnectedOption() ?? new List<STNodeOption>())
                        if (input?.Owner != null)
                            drawn.Add((output, input));
                }
            }
            return drawn;
        }

        private void Connect(STNodeOption output, STNodeOption input)
        {
            if (output == null || input == null)
                return;
            _building++;
            try { output.ConnectOption(input); }
            finally { _building--; }
        }

        private void Disconnect(STNodeOption output, STNodeOption input)
        {
            if (output == null || input == null)
                return;
            _building++;
            try { output.DisconnectOption(input); }
            finally { _building--; }
        }
        #endregion

        #region Populate
        /// <summary>
        /// Draw the tree afresh: from <paramref name="saved"/> when given (copies, positions, which copy draws which link;
        /// anything it lacks is placed beside what it links to), else laid out automatically. True when it was laid out
        /// automatically.
        /// </summary>
        public bool Populate(TreeLayout saved)
        {
            _building++;
            try
            {
                Editor.SuspendLayout();
                Editor.Nodes.Clear();
                _copies.Clear();

                Dictionary<AnimationNode, NodeKey> keys = AnimTreeLayouts.KeysOf(Tree, _strings);
                List<AnimationNode> nodes = ModelNodes();

                //How many copies each node has in the saved layout, and which saved copy number became which copy here
                Dictionary<NodeKey, Dictionary<int, int>> copyOf = new Dictionary<NodeKey, Dictionary<int, int>>();
                Dictionary<NodeKey, List<NodeLayout>> savedNodes = new Dictionary<NodeKey, List<NodeLayout>>();
                if (saved != null)
                {
                    foreach (NodeLayout entry in saved.Nodes)
                    {
                        if (entry == null) continue;
                        if (!savedNodes.TryGetValue(entry.Key, out List<NodeLayout> list))
                        {
                            list = new List<NodeLayout>();
                            savedNodes[entry.Key] = list;
                        }
                        list.Add(entry);
                    }
                    foreach (KeyValuePair<NodeKey, List<NodeLayout>> entry in savedNodes)
                    {
                        Dictionary<int, int> map = new Dictionary<int, int>();
                        foreach (int ghost in entry.Value.Select(o => o.Ghost).Distinct().OrderBy(o => o))
                            map[ghost] = map.Count;
                        copyOf[entry.Key] = map;
                    }
                }

                foreach (AnimationNode node in nodes)
                {
                    int count = 1;
                    if (!(node is AnimationTree) && copyOf.TryGetValue(keys[node], out Dictionary<int, int> map))
                        count = Math.Max(1, map.Count);
                    for (int i = 0; i < count; i++)
                        MakeNode(node);
                }

                //Each link once, on the copies the layout gave it (copy 0 otherwise)
                List<ModelLink> links = LinksOf(Tree);
                List<LinkLayout> assignments = saved?.Links.Where(o => o != null).ToList() ?? new List<LinkLayout>();
                bool[] used = new bool[assignments.Count];
                List<(ModelLink link, int from, int to)> placed = new List<(ModelLink, int, int)>();
                foreach (ModelLink link in links)
                    placed.Add((link, 0, 0));
                for (int pass = 0; pass < 2 && assignments.Count != 0; pass++)
                {
                    for (int i = 0; i < placed.Count; i++)
                    {
                        ModelLink link = placed[i].link;
                        if (placed[i].from != 0 || placed[i].to != 0) continue;
                        NodeKey from = keys[link.From], to = keys[link.To];
                        for (int a = 0; a < assignments.Count; a++)
                        {
                            if (used[a]) continue;
                            LinkLayout assignment = assignments[a];
                            if (assignment.From != from || assignment.To != to) continue;
                            //Exact pins first; then a flow link whose state slot moved (slots are compacted when the tree is written)
                            bool pinsMatch = assignment.FromPin == link.FromPin.AsUInt32 && assignment.ToPin == link.ToPin.AsUInt32;
                            if (pass == 0 ? !pinsMatch : !(link.Flow && assignment.ToPin == ShortGuids.trigger.AsUInt32)) continue;
                            used[a] = true;
                            placed[i] = (link, CopyFor(copyOf, from, assignment.FromGhost, link.From), CopyFor(copyOf, to, assignment.ToGhost, link.To));
                            break;
                        }
                    }
                }
                foreach ((ModelLink link, int from, int to) in placed)
                    Connect(OutputPin(_copies[link.From][from], link.FromPin), InputPin(_copies[link.To][to], link.ToPin));

                bool auto = saved == null;
                if (auto)
                {
                    ArrangeAll(Origin);
                }
                else
                {
                    List<STNode> unplaced = new List<STNode>();
                    foreach (AnimationNode node in nodes)
                    {
                        List<STNode> copies = _copies[node];
                        Dictionary<int, int> map = copyOf.TryGetValue(keys[node], out Dictionary<int, int> found) ? found : null;
                        bool[] positioned = new bool[copies.Count];
                        if (map != null && savedNodes.TryGetValue(keys[node], out List<NodeLayout> entries))
                        {
                            foreach (NodeLayout entry in entries)
                            {
                                if (!map.TryGetValue(entry.Ghost, out int copy) || copy >= copies.Count || positioned[copy]) continue;
                                copies[copy].SetPosition(new Point(entry.X, entry.Y));
                                positioned[copy] = true;
                            }
                        }
                        for (int i = 0; i < copies.Count; i++)
                            if (!positioned[i]) unplaced.Add(copies[i]);
                    }
                    PlaceBesideLinks(unplaced);
                }

                RefreshMarkers();
                Editor.ResumeLayout();
                Editor.Invalidate();
                return auto;
            }
            finally
            {
                _building--;
            }
        }

        /* The copy a stored copy number names now, or 0 when that copy is gone */
        private int CopyFor(Dictionary<NodeKey, Dictionary<int, int>> copyOf, NodeKey key, int ghost, AnimationNode node)
        {
            if (!copyOf.TryGetValue(key, out Dictionary<int, int> map) || !map.TryGetValue(ghost, out int copy))
                return 0;
            return copy < _copies[node].Count ? copy : 0;
        }

        /// <summary>Show the asterisk on every node that is drawn more than once.</summary>
        public void RefreshMarkers()
        {
            foreach (List<STNode> copies in _copies.Values)
                foreach (STNode copy in copies)
                    copy.ShowMultiNodeMarker = copies.Count > 1;
        }
        #endregion

        #region Placement
        private const int PlacementGap = 24;
        private const int ColumnGap = 90;
        private const int RowGap = 54;

        /// <summary>
        /// Put nodes the layout had no place for (new since it was stored) beside what they link to: a child right of its
        /// parent, a value above what reads it, a reader below its value - each in the nearest free spot. Anything linked
        /// to nothing placed goes below the rest.
        /// </summary>
        public void PlaceBesideLinks(List<STNode> unplaced)
        {
            if (unplaced.Count == 0)
                return;
            HashSet<STNode> waiting = new HashSet<STNode>(unplaced);
            List<Rectangle> taken = Editor.Nodes.ToArray().Where(o => !waiting.Contains(o)).Select(o => new Rectangle(o.Location, o.Size)).ToList();

            for (int pass = 0; pass < 3 && waiting.Count != 0; pass++)
            {
                foreach (STNode node in OwnOrder(waiting))
                {
                    Point? preferred = BesideLinks(node, waiting, pass == 2);
                    if (preferred == null)
                        continue;
                    Point at = FreeSpot(preferred.Value, node.Size, taken);
                    node.SetPosition(at);
                    taken.Add(new Rectangle(at, node.Size));
                    waiting.Remove(node);
                }
            }
        }

        /* Where a node would sit beside something already placed that it links to (null: nothing placed yet), or - at
           the last pass - below everything */
        private Point? BesideLinks(STNode node, HashSet<STNode> waiting, bool lastResort)
        {
            foreach (STNodeOption pin in node.GetAllOptions())
            {
                if (pin == null || pin == STNodeOption.Empty) continue;
                foreach (STNodeOption other in pin.GetConnectedOption() ?? new List<STNodeOption>())
                {
                    STNode partner = other?.Owner;
                    if (partner == null || waiting.Contains(partner)) continue;
                    switch (pin.Location)
                    {
                        case PinLocation.Left: return new Point(partner.Right + ColumnGap, partner.Top);
                        case PinLocation.Right: return new Point(partner.Left - ColumnGap - node.Width, partner.Top);
                        case PinLocation.Bottom: return new Point(partner.Left, partner.Top - RowGap - node.Height);
                        default: return new Point(partner.Left, partner.Bottom + RowGap);
                    }
                }
            }
            if (!lastResort)
                return null;
            STNode[] placed = Editor.Nodes.ToArray().Where(o => !waiting.Contains(o)).ToArray();
            if (placed.Length == 0)
                return Origin;
            return new Point(placed.Min(o => o.Left), placed.Max(o => o.Bottom) + 140);
        }

        /* The nearest spot to the one wanted (looking down, then up, then sideways) where the node overlaps nothing */
        private static Point FreeSpot(Point wanted, Size size, List<Rectangle> taken)
        {
            bool Free(Point at)
            {
                Rectangle box = new Rectangle(at.X - PlacementGap / 2, at.Y - PlacementGap / 2, size.Width + PlacementGap, size.Height + PlacementGap);
                return !taken.Any(o => o.IntersectsWith(box));
            }
            if (Free(wanted))
                return wanted;
            for (int step = 1; step < 400; step++)
            {
                int d = step * 20;
                foreach (Point at in new[] { new Point(wanted.X, wanted.Y + d), new Point(wanted.X, wanted.Y - d), new Point(wanted.X + d, wanted.Y), new Point(wanted.X - d, wanted.Y) })
                    if (Free(at))
                        return at;
            }
            return wanted;
        }
        #endregion

        #region Arrange
        /// <summary>
        /// A value link longer than this gets a ghost of its value beside the reader. Shorter than a script page's: a tree's
        /// values are small nodes read by animations stacked in one column, and one value read by several of them would
        /// otherwise be wired up and down through the stack.
        /// </summary>
        public const int LongValueLink = 150;

        /// <summary>
        /// Lay out the whole tree from <paramref name="origin"/> (its top-left corner) by the links between the nodes, as the
        /// scripting flowgraph's Arrange does: flow left to right, values above what reads them, and a value read far from
        /// where it sits given a ghost beside each distant reader. Ghosts the old layout had are folded back in first, so the
        /// result depends on the tree alone. Returns how many node copies and link placements differ from before (0: it was
        /// laid out this way already).
        /// </summary>
        public int ArrangeAll(Point origin)
        {
            _building++;
            try
            {
                TreeLayout before = Capture();
                MergeAllCopies();

                List<STNode> placing = OwnOrder(Editor.Nodes.ToArray());
                STNode root = _copies.TryGetValue(Tree, out List<STNode> roots) ? roots.FirstOrDefault() : null;
                int added = 0;
                Point[] positions;
                for (int round = 0; ; round++)
                {
                    List<PageArranger.Connection> connections = Measure(placing, out List<(STNodeOption from, STNodeOption to)> pins);
                    List<PageArranger.Node> sized = placing.Select(o => new PageArranger.Node() { Size = new Size(o.Width, o.Height) }).ToList();
                    positions = PageArranger.Arrange(sized, connections);
                    List<PageArranger.Split> splits = round < PageArranger.SplitRounds ? PageArranger.SplitsFor(sized, connections, positions, LongValueLink) : new List<PageArranger.Split>();
                    splits.RemoveAll(o => placing[o.Node] == root);
                    if (splits.Count == 0)
                        break;

                    foreach (PageArranger.Split split in splits)
                    {
                        STNode copy = SplitNode(placing[split.Node], placing[split.Partner], split.Connections.Select(i => pins[i]).ToList());
                        if (copy == null)
                            continue;
                        copy.SetPosition(new Point(origin.X - 100000 + added * 17, origin.Y - 100000 + added * 13));
                        added++;
                    }
                    placing = OwnOrder(Editor.Nodes.ToArray());
                }

                //Nodes nothing is drawn to (values nothing reads, listeners) go in rows of their own under the tree, rather
                //than packed around it where they read as part of it
                Point[] at = new Point[placing.Count];
                List<int> loose = new List<int>();
                Rectangle tree = Rectangle.Empty;
                for (int i = 0; i < placing.Count; i++)
                {
                    at[i] = new Point(origin.X + positions[i].X, origin.Y + positions[i].Y);
                    bool linked = placing[i] == root || placing[i].GetAllOptions().Any(o => o != null && o != STNodeOption.Empty && (o.GetConnectedOption()?.Count ?? 0) != 0);
                    if (!linked)
                        loose.Add(i);
                    else
                        tree = tree.IsEmpty ? new Rectangle(at[i], placing[i].Size) : Rectangle.Union(tree, new Rectangle(at[i], placing[i].Size));
                }
                if (loose.Count != 0)
                {
                    if (tree.IsEmpty)
                        tree = new Rectangle(origin, Size.Empty);
                    int rowWidth = Math.Max(1200, tree.Width), x = tree.Left, y = tree.Bottom + 140, rowHeight = 0;
                    foreach (int i in loose.OrderBy(o => placing[o].AnimationNode.Type.ToString(), StringComparer.Ordinal).ThenBy(o => placing[o].AnimationNode.Name, StringComparer.Ordinal).ThenBy(o => o))
                    {
                        if (x != tree.Left && x + placing[i].Width > tree.Left + rowWidth)
                        {
                            x = tree.Left;
                            y += rowHeight + RowGap;
                            rowHeight = 0;
                        }
                        at[i] = new Point(x, y);
                        x += placing[i].Width + PlacementGap;
                        rowHeight = Math.Max(rowHeight, placing[i].Height);
                    }
                }

                for (int i = 0; i < placing.Count; i++)
                    placing[i].SetPosition(at[i]);
                RefreshMarkers();
                Editor.Invalidate();
                return Differences(before, Capture());
            }
            finally
            {
                _building--;
            }
        }

        /// <summary>How many node copies (where they are) and link placements (which copies draw them) one layout has that the other does not.</summary>
        public static int Differences(TreeLayout a, TreeLayout b)
        {
            HashSet<string> nodesA = new HashSet<string>(a.Nodes.Select(o => o.Key + "/" + o.Ghost + "@" + o.X + "," + o.Y));
            HashSet<string> nodesB = new HashSet<string>(b.Nodes.Select(o => o.Key + "/" + o.Ghost + "@" + o.X + "," + o.Y));
            HashSet<string> linksA = new HashSet<string>(a.Links.Select(o => o.From + "/" + o.FromGhost + "." + o.FromPin + ">" + o.To + "/" + o.ToGhost + "." + o.ToPin));
            HashSet<string> linksB = new HashSet<string>(b.Links.Select(o => o.From + "/" + o.FromGhost + "." + o.FromPin + ">" + o.To + "/" + o.ToGhost + "." + o.ToPin));
            return nodesA.Count(o => !nodesB.Contains(o)) + nodesB.Count(o => !nodesA.Contains(o)) + linksA.Count(o => !linksB.Contains(o)) + linksB.Count(o => !linksA.Contains(o));
        }

        /// <summary>
        /// The canvas's own order for its nodes - the tree's order of its nodes, then copy order - rather than the canvas's,
        /// which clicking a node changes (it moves to the end, to draw on top). The same tree arranges the same way however
        /// it was clicked.
        /// </summary>
        private List<STNode> OwnOrder(IEnumerable<STNode> nodes)
        {
            Dictionary<AnimationNode, int> order = new Dictionary<AnimationNode, int>(ByReference.Instance);
            foreach (AnimationNode node in ModelNodes())
                order[node] = order.Count;
            return nodes.Where(o => o?.AnimationNode != null)
                .OrderBy(o => order.TryGetValue(o.AnimationNode, out int i) ? i : int.MaxValue)
                .ThenBy(o => CopyIndexOf(o))
                .ToList();
        }

        /* Each connection between the nodes once, with its pins' sides and where they sit on the nodes as drawn */
        private static List<PageArranger.Connection> Measure(List<STNode> placing, out List<(STNodeOption from, STNodeOption to)> pins)
        {
            Dictionary<STNode, int> index = new Dictionary<STNode, int>();
            for (int i = 0; i < placing.Count; i++)
                index[placing[i]] = i;

            List<PageArranger.Connection> connections = new List<PageArranger.Connection>();
            pins = new List<(STNodeOption, STNodeOption)>();
            HashSet<(STNodeOption, STNodeOption)> seen = new HashSet<(STNodeOption, STNodeOption)>();
            for (int i = 0; i < placing.Count; i++)
            {
                foreach (STNodeOption pin in placing[i].GetAllOptions())
                {
                    if (pin == null || pin == STNodeOption.Empty)
                        continue;
                    foreach (STNodeOption other in pin.GetConnectedOption() ?? new List<STNodeOption>())
                    {
                        if (other?.Owner == null || !index.TryGetValue(other.Owner, out int j) || j == i)
                            continue;
                        if (seen.Contains((other, pin)) || !seen.Add((pin, other)))
                            continue;
                        connections.Add(new PageArranger.Connection()
                        {
                            From = i,
                            FromSide = SideOf(pin),
                            FromOffset = OffsetOf(placing[i], pin),
                            To = j,
                            ToSide = SideOf(other),
                            ToOffset = OffsetOf(placing[j], other),
                        });
                        pins.Add((pin, other));
                    }
                }
            }
            return connections;
        }

        private static PinSide SideOf(STNodeOption pin)
        {
            switch (pin.Location)
            {
                case PinLocation.Top: return PinSide.Top;
                case PinLocation.Bottom: return PinSide.Bottom;
                case PinLocation.Left: return PinSide.Left;
                default: return PinSide.Right;
            }
        }

        /* Where the pin's dot sits along its side: the middle of its slot for top and bottom pins (worked out, not read from
           the dot, which rounds toward zero and so moves a pixel left of the origin), the dot itself for side pins */
        private static int OffsetOf(STNode node, STNodeOption pin)
        {
            if (pin.Location == PinLocation.Top || pin.Location == PinLocation.Bottom)
            {
                STNodeOption[] row = pin.Location == PinLocation.Top ? node.GetTopOptions() : node.GetBottomOptions();
                int index = Array.IndexOf(row, pin);
                if (index >= 0)
                    return (int)Math.Round((index + 0.5) * node.Width / row.Length);
                return pin.DotLeft + pin.DotSize / 2 - node.Left;
            }
            return pin.DotTop + pin.DotSize / 2 - node.Top;
        }

        /* A new copy of node's animation node, with the connections between node and partner moved onto it */
        private STNode SplitNode(STNode node, STNode partner, List<(STNodeOption from, STNodeOption to)> pins)
        {
            List<(STNodeOption own, STNodeOption other)> moving = pins.Select(o => o.from.Owner == node ? (o.from, o.to) : (o.to, o.from)).ToList();
            if (moving.Count == 0 || moving.Any(o => o.own.Owner != node || o.other.Owner != partner))
                return null;
            STNode copy = MakeNode(node.AnimationNode);
            foreach ((STNodeOption own, STNodeOption other) in moving)
                Relink(own, other, SamePin(copy, own));
            return copy;
        }

        /* The link between pin and other, drawn from replacement instead (a pin of another copy of pin's node) */
        private void Relink(STNodeOption pin, STNodeOption other, STNodeOption replacement)
        {
            if (replacement == null)
                return;
            bool output = pin.Location == PinLocation.Right || pin.Location == PinLocation.Bottom;
            Disconnect(output ? pin : other, output ? other : pin);
            Connect(output ? replacement : other, output ? other : replacement);
        }

        /// <summary>Fold every node's extra copies back into its first, links and all. Returns how many copies went.</summary>
        private int MergeAllCopies()
        {
            int removed = 0;
            foreach (List<STNode> copies in _copies.Values.ToList())
                while (copies.Count > 1)
                {
                    RemoveCopy(copies[copies.Count - 1]);
                    removed++;
                }
            return removed;
        }
        #endregion

        #region Ghosts
        /// <summary>Another copy of a node, drawing no links yet, at <paramref name="at"/>. The tree root has only one.</summary>
        public STNode AddGhost(AnimationNode node, Point at)
        {
            if (node == null || node is AnimationTree || !_copies.ContainsKey(node))
                return null;
            STNode copy = MakeNode(node);
            copy.SetPosition(at);
            RefreshMarkers();
            return copy;
        }

        /// <summary>
        /// Take a copy off the canvas, its links drawn from another copy of the same node instead (the first one left).
        /// False - and nothing done - when it is the node's only copy: removing that is deleting the node.
        /// </summary>
        public bool RemoveCopy(STNode copy)
        {
            if (copy?.AnimationNode == null || !_copies.TryGetValue(copy.AnimationNode, out List<STNode> copies) || copies.Count < 2 || !copies.Contains(copy))
                return false;
            STNode keeper = copies.First(o => o != copy);
            _building++;
            try
            {
                foreach (STNodeOption pin in copy.GetAllOptions())
                {
                    if (pin == null || pin == STNodeOption.Empty) continue;
                    foreach (STNodeOption other in (pin.GetConnectedOption() ?? new List<STNodeOption>()).ToList())
                        Relink(pin, other, SamePin(keeper, pin));
                }
                copies.Remove(copy);
                Editor.Nodes.Remove(copy);
            }
            finally
            {
                _building--;
            }
            RefreshMarkers();
            return true;
        }

        /// <summary>
        /// Draw the link between <paramref name="pin"/> (on a copy of some node) and <paramref name="other"/> from
        /// <paramref name="target"/> instead, another copy of the same node. False when target is not a copy of that node.
        /// </summary>
        public bool MoveLink(STNodeOption pin, STNodeOption other, STNode target)
        {
            if (pin?.Owner?.AnimationNode == null || target == null || target == pin.Owner || !ReferenceEquals(target.AnimationNode, pin.Owner.AnimationNode))
                return false;
            STNodeOption replacement = SamePin(target, pin);
            if (replacement == null)
                return false;
            Relink(pin, other, replacement);
            return true;
        }

        /// <summary>Remove a node and all its copies from the canvas (the tree is the caller's to change).</summary>
        public void RemoveAllCopies(AnimationNode node)
        {
            if (node == null || !_copies.TryGetValue(node, out List<STNode> copies))
                return;
            _building++;
            try
            {
                foreach (STNode copy in copies.ToList())
                    Editor.Nodes.Remove(copy);
            }
            finally
            {
                _building--;
            }
            _copies.Remove(node);
            RefreshMarkers();
        }

        /// <summary>A node newly in the tree, drawn once at <paramref name="at"/>.</summary>
        public STNode AddNode(AnimationNode node, Point at)
        {
            if (node == null || _copies.ContainsKey(node))
                return null;
            STNode drawn = MakeNode(node);
            drawn.SetPosition(at);
            return drawn;
        }

        /// <summary>
        /// Nodes newly in the tree together (a paste of copies), each drawn once where given, with every link the tree holds
        /// between two of them. Returns their canvas nodes, in the order given.
        /// </summary>
        public List<STNode> AddNodes(IList<(AnimationNode node, Point at)> nodes)
        {
            List<STNode> drawn = new List<STNode>();
            HashSet<AnimationNode> added = new HashSet<AnimationNode>(ByReference.Instance);
            foreach ((AnimationNode node, Point at) in nodes)
            {
                STNode made = AddNode(node, at);
                if (made == null)
                    continue;
                drawn.Add(made);
                added.Add(node);
            }
            foreach (ModelLink link in LinksOf(Tree))
                if (added.Contains(link.From) && added.Contains(link.To))
                    Connect(OutputPin(_copies[link.From][0], link.FromPin), InputPin(_copies[link.To][0], link.ToPin));
            return drawn;
        }

        /// <summary>Every canvas node of this node (and its copies) retitled after a rename.</summary>
        public void Retitle(AnimationNode node)
        {
            foreach (STNode copy in CopiesOf(node))
                copy.SetName(node.Name, node.Type.ToString());
        }
        #endregion

        #region Drawn links
        /// <summary>
        /// Can the user's link between these pins stand? Refused (with why) when the tree could not hold it: the wrong kind
        /// of node for the pin, or a child above its own parent. A link the tree already holds is always fine - drawing it
        /// onto another copy only moves it.
        /// </summary>
        public bool CanDraw(STNodeOption a, STNodeOption b, out string refusal)
        {
            refusal = null;
            if (!Orient(a, b, out STNodeOption output, out STNodeOption input))
                return true; //not a pairing the canvas itself allows: it refuses it
            AnimationNode from = output.Owner?.AnimationNode, to = input.Owner?.AnimationNode;
            if (from == null || to == null || !_copies.ContainsKey(from) || !_copies.ContainsKey(to))
                return true;
            if (HasModelLink(from, output.ShortGUID, to, input.ShortGUID))
                return true;
            if (ReferenceEquals(from, to))
            {
                refusal = "A node can't link to itself.";
                return false;
            }

            if (input.Location == PinLocation.Left)
            {
                if (to is AnimationTree)
                {
                    refusal = "Nothing links to the tree itself.";
                    return false;
                }
                if (output.ShortGUID == ShortGuids.LeftStrikeChild || output.ShortGUID == ShortGuids.RightStrikeChild)
                {
                    if (!(to is LeafNode))
                    {
                        refusal = "A foot sync selector's strike children are animations (" + to.Name + " is " + to.Type + ").";
                        return false;
                    }
                }
                if (IsBelow(from, to))
                {
                    refusal = to.Name + " is above " + (from is AnimationTree ? "the tree's top level" : from.Name) + " already, so this would make a loop.";
                    return false;
                }
                return true;
            }

            ShortGuid top = input.ShortGUID;
            if (top == ShortGuids.Callback || top == ShortGuids.RandomCallback || top == ShortGuids.OverflowCallback)
            {
                if (from.Type != NodeType.ANIM_Callback)
                {
                    refusal = "'" + top + "' takes a callback node (" + from.Name + " is " + from.Type + ").";
                    return false;
                }
                return true;
            }
            if (top == ShortGuids.LeafNode)
            {
                if (!PinsOf(from).Trigger)
                {
                    refusal = "A property listener listens to a node in the flow (" + from.Name + " is " + from.Type + ").";
                    return false;
                }
                return true;
            }
            //Every other top pin reads a parameter
            if (!(from is ParameterNode) || (from.Type != NodeType.ANIM_Parameter && from.Type != NodeType.ANIM_FloatInterpolator))
            {
                refusal = "'" + top + "' takes a parameter (" + from.Name + " is " + from.Type + ").";
                return false;
            }
            return true;
        }

        /// <summary>
        /// The user drew a link (the canvas already shows it): make the tree hold it. A link the tree already holds was only
        /// moved to other copies, so its old drawing goes; a new one replaces whatever the pin linked to before - in the tree,
        /// and on the canvas. Returns what changed in the tree (null: nothing, it was a move), or throws when it can't hold it.
        /// </summary>
        public string ApplyDrawnLink(STNodeOption a, STNodeOption b, out bool treeChanged)
        {
            treeChanged = false;
            if (!Orient(a, b, out STNodeOption output, out STNodeOption input))
                return null;
            AnimationNode from = output.Owner.AnimationNode, to = input.Owner.AnimationNode;

            if (HasModelLink(from, output.ShortGUID, to, input.ShortGUID))
            {
                //Already in the tree, drawn between other copies: now drawn here only
                foreach ((STNodeOption o, STNodeOption i) in DrawnLinks())
                {
                    if (o == output && i == input) continue;
                    if (ReferenceEquals(o.Owner.AnimationNode, from) && o.ShortGUID == output.ShortGUID && ReferenceEquals(i.Owner.AnimationNode, to) && i.ShortGUID == input.ShortGUID)
                        Disconnect(o, i);
                }
                return null;
            }

            if (!CanDraw(output, input, out string refusal))
                throw new InvalidOperationException(refusal);

            string done;
            if (input.Location == PinLocation.Left)
                done = SetFlowChild(from, output.ShortGUID, to);
            else
                done = SetTopPin(to, input.ShortGUID, from);
            treeChanged = true;

            //Whatever that pin linked to before is gone from the tree: so is its drawing (the tree's top level takes many children)
            bool single = input.Location == PinLocation.Top || !(from is AnimationTree);
            if (single)
            {
                foreach ((STNodeOption o, STNodeOption i) in DrawnLinks())
                {
                    if (o == output && i == input) continue;
                    bool samePin = input.Location == PinLocation.Top
                        ? ReferenceEquals(i.Owner.AnimationNode, to) && i.ShortGUID == input.ShortGUID
                        : ReferenceEquals(o.Owner.AnimationNode, from) && o.ShortGUID == output.ShortGUID;
                    if (samePin)
                        Disconnect(o, i);
                }
            }
            return done;
        }

        /* The output (right or bottom) and input (left or top) of a drawn link, whichever way round it was drawn */
        private static bool Orient(STNodeOption a, STNodeOption b, out STNodeOption output, out STNodeOption input)
        {
            output = null;
            input = null;
            if (a == null || b == null)
                return false;
            bool aOut = a.Location == PinLocation.Right || a.Location == PinLocation.Bottom;
            bool bOut = b.Location == PinLocation.Right || b.Location == PinLocation.Bottom;
            if (aOut == bOut)
                return false;
            output = aOut ? a : b;
            input = aOut ? b : a;
            //Only the pairings the canvas itself draws: a flow output into a trigger, a value into a top pin
            return (output.Location == PinLocation.Right && input.Location == PinLocation.Left) || (output.Location == PinLocation.Bottom && input.Location == PinLocation.Top);
        }

        private bool HasModelLink(AnimationNode from, ShortGuid fromPin, AnimationNode to, ShortGuid toPin)
        {
            foreach (ModelLink link in LinksOf(Tree))
                if (ReferenceEquals(link.From, from) && link.FromPin == fromPin && ReferenceEquals(link.To, to) && link.ToPin == toPin)
                    return true;
            return false;
        }

        /* Is node somewhere under ancestor's flow already (so parenting ancestor under node would make a loop)? */
        private static bool IsBelow(AnimationNode node, AnimationNode ancestor)
        {
            HashSet<AnimationNode> seen = new HashSet<AnimationNode>(ByReference.Instance);
            Stack<AnimationNode> todo = new Stack<AnimationNode>();
            todo.Push(ancestor);
            while (todo.Count != 0)
            {
                AnimationNode next = todo.Pop();
                if (next == null || !seen.Add(next)) continue;
                if (ReferenceEquals(next, node)) return true;
                foreach (AnimationNode child in next.Children) todo.Push(child);
            }
            return false;
        }

        private static void AddChild(AnimationNode parent, AnimationNode child)
        {
            if (!parent.Children.Any(o => ReferenceEquals(o, child)))
                parent.Children.Add(child);
        }

        /* A flow child on one of parent's output pins: the field that pin stands for, and the parent's children (which is
           what the tree is written from) */
        private static string SetFlowChild(AnimationNode parent, ShortGuid pin, AnimationNode child)
        {
            if (parent is AnimationTree tree && pin == ShortGuids.NODES)
            {
                AddChild(tree, child);
                return "@tree.Children += " + child.Name;
            }
            int state = Array.IndexOf(ShortGuids.States, pin);
            if (state >= 0)
            {
                switch (parent)
                {
                    case SelectorNode selector when state < selector.States.Length:
                        selector.States[state].Node = child;
                        break;
                    case ParametricNode parametric when state < parametric.States.Length:
                        parametric.States[state].Node = child;
                        break;
                    case RangedSelectorNode ranged when state < ranged.States.Length:
                        ranged.States[state].Node = child;
                        break;
                    default:
                        throw new InvalidOperationException(parent.Name + " has no " + pin + ".");
                }
                AddChild(parent, child);
                return parent.Name + ".States[" + state + "] = " + child.Name;
            }
            if (parent is AdditiveBlendNode additive && (pin == ShortGuids.base_node || pin == ShortGuids.additive_node))
            {
                if (pin == ShortGuids.base_node) additive.BaseNode = child;
                else additive.AdditiveNode = child;
                AddChild(parent, child);
                return parent.Name + "." + (pin == ShortGuids.base_node ? "BaseNode" : "AdditiveNode") + " = " + child.Name;
            }
            if (parent is WeightedNode weighted && pin == ShortGuids.child)
            {
                //A weighted node holds exactly one child; the reader refuses more
                weighted.Child = child;
                parent.Children.Clear();
                parent.Children.Add(child);
                return parent.Name + ".Child = " + child.Name;
            }
            if (parent is FootSyncSelectorNode footSync && child is LeafNode leaf && (pin == ShortGuids.LeftStrikeChild || pin == ShortGuids.RightStrikeChild))
            {
                if (pin == ShortGuids.LeftStrikeChild) footSync.LeftStrikeChild = leaf;
                else footSync.RightStrikeChild = leaf;
                AddChild(parent, child);
                return parent.Name + "." + pin + " = " + child.Name;
            }
            throw new InvalidOperationException(parent.Name + "'s " + pin + " can't take " + child.Name + ".");
        }

        /* What one of consumer's top pins reads */
        private static string SetTopPin(AnimationNode consumer, ShortGuid pin, AnimationNode provider)
        {
            ParameterNode parameter = provider as ParameterNode;
            string label = consumer.Name + "." + pin + " = " + provider.Name;
            if (pin == ShortGuids.Callback)
            {
                if (consumer is LeafNode leaf) { leaf.Callback = provider; return label; }
                if (consumer is RandomisedLeafNode randomised) { randomised.Callback = provider; return label; }
            }
            else if (pin == ShortGuids.RandomCallback && consumer is RandomisedLeafNode randomised) { randomised.RandomCallback = provider; return label; }
            else if (pin == ShortGuids.OverflowCallback && consumer is Parametric2DNode overflow) { overflow.OverflowCallback = provider; return label; }
            else if (pin == ShortGuids.LeafNode && consumer is PropertyListenerNode listener) { listener.LeafNode = provider; return label; }
            else if (parameter != null)
            {
                if (pin == ShortGuids.ParameterBinding)
                {
                    if (consumer is SelectorNode selector) { selector.ParameterBinding = parameter; return label; }
                    if (consumer is ParametricNode parametric) { parametric.ParameterBinding = parameter; return label; }
                    if (consumer is RangedSelectorNode ranged) { ranged.ParameterBinding = parameter; return label; }
                }
                if (pin == ShortGuids.ParameterBindingX && consumer is Parametric2DNode x) { x.ParameterBindingX = parameter; return label; }
                if (pin == ShortGuids.ParameterBindingY && consumer is Parametric2DNode y) { y.ParameterBindingY = parameter; return label; }
                if (pin == ShortGuids.ParameterBindingZ && consumer is Parametric3DNode z) { z.ParameterBindingZ = parameter; return label; }
                if (pin == ShortGuids.IkEffector && consumer is IkNode ik) { ik.IkEffector = parameter; return label; }
                if (pin == ShortGuids.WeightControlParameter && consumer is ParametricAdditiveBlendNode weight) { weight.WeightControlParameter = parameter; return label; }
                if (pin == ShortGuids.Parameter && consumer is WeightedNode weighted) { weighted.Parameter = parameter; return label; }
                if (pin == ShortGuids.SourceParameter && consumer is FloatInterpolatorNode interpolator && !ReferenceEquals(interpolator, parameter)) { interpolator.SourceParameter = parameter; return label; }
            }
            throw new InvalidOperationException(consumer.Name + "'s " + pin + " can't take " + provider.Name + ".");
        }
        #endregion

        #region Layout
        /// <summary>
        /// The canvas as a layout to store: every copy's position (copies numbered 0 up in order), the links drawn on a copy
        /// other than the first at either end, and the view - unless <paramref name="withView"/> is false (a canvas nobody
        /// looks at: its view means nothing).
        /// </summary>
        public TreeLayout Capture(bool withView = true)
        {
            Dictionary<AnimationNode, NodeKey> keys = AnimTreeLayouts.KeysOf(Tree, _strings);
            TreeLayout layout = new TreeLayout()
            {
                SetHash = SetHash,
                TreeHash = TreeHash,
                Set = SetNameOf(Database),
                Tree = Tree.Name,
            };
            if (withView && Editor.Nodes.Count != 0)
            {
                PointF centre = Editor.CanvasCenter;
                layout.CanvasX = centre.X;
                layout.CanvasY = centre.Y;
                layout.CanvasScale = Editor.CanvasScale;
            }

            foreach (KeyValuePair<AnimationNode, List<STNode>> entry in _copies)
            {
                if (!keys.TryGetValue(entry.Key, out NodeKey key))
                    continue;
                for (int i = 0; i < entry.Value.Count; i++)
                    layout.Nodes.Add(new NodeLayout() { Key = key, Ghost = i, X = entry.Value[i].Left, Y = entry.Value[i].Top });
            }
            foreach ((STNodeOption output, STNodeOption input) in DrawnLinks())
            {
                int fromCopy = CopyIndexOf(output.Owner), toCopy = CopyIndexOf(input.Owner);
                if (fromCopy <= 0 && toCopy <= 0)
                    continue;
                if (!keys.TryGetValue(output.Owner.AnimationNode, out NodeKey from) || !keys.TryGetValue(input.Owner.AnimationNode, out NodeKey to))
                    continue;
                layout.Links.Add(new LinkLayout() { From = from, FromGhost = Math.Max(0, fromCopy), FromPin = output.ShortGUID.AsUInt32, To = to, ToGhost = Math.Max(0, toCopy), ToPin = input.ShortGUID.AsUInt32 });
            }
            return layout;
        }

        /// <summary>Put the view back as a layout stored it (after the canvas has its size: a fresh one starts at 1:1, top left).</summary>
        public void RestoreView(TreeLayout layout)
        {
            if (layout == null || layout.CanvasScale <= 0 || Editor.Nodes.Count == 0)
                return;
            Editor.ScaleCanvas(layout.CanvasScale, 0, 0);
            Editor.CenterCanvasOn(layout.CanvasX, layout.CanvasY, false);
        }

        /// <summary>Zoom out (never in past 1:1) until the tree fits the view, and centre on it; a tree too big even zoomed right out shows its top-left corner.</summary>
        public void FitView()
        {
            STNode[] nodes = Editor.Nodes.ToArray();
            if (nodes.Length == 0 || Editor.Width <= 0 || Editor.Height <= 0)
                return;
            const float minScale = 0.2f, margin = 40f;
            int minX = nodes.Min(o => o.Left), minY = nodes.Min(o => o.Top);
            int maxX = nodes.Max(o => o.Left + o.Width), maxY = nodes.Max(o => o.Top + o.Height);
            float fit = Math.Min((Editor.Width - margin * 2) / Math.Max(1, maxX - minX), (Editor.Height - margin * 2) / Math.Max(1, maxY - minY));
            float scale = Math.Max(minScale, Math.Min(1f, fit));
            Editor.ScaleCanvas(scale, Editor.Width / 2f, Editor.Height / 2f);
            scale = Editor.CanvasScale;
            if (fit >= minScale)
                Editor.CenterCanvasOn((minX + maxX) / 2f, (minY + maxY) / 2f, false);
            else
                Editor.MoveCanvas(margin - minX * scale, margin - minY * scale, false);
        }
        #endregion

        /// <summary>Animation nodes compared by reference: their own Equals compares name and type, which repeat.</summary>
        public sealed class ByReference : IEqualityComparer<AnimationNode>
        {
            public static readonly ByReference Instance = new ByReference();
            public bool Equals(AnimationNode x, AnimationNode y) => ReferenceEquals(x, y);
            public int GetHashCode(AnimationNode obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
