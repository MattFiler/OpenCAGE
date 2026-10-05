using CATHODE.Scripting;
using CATHODE.Scripting.Refactor;
using OpenCAGE.Undo;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// Tidying a page: its nodes (or just the selected ones) laid out afresh by <see cref="PageArranger"/> so the
    /// links read left to right, as one undo step. A data link that would still run a long way is given a node of its
    /// own beside the node at its other end, as the shipped pages draw a value used in several places - so the page can
    /// gain nodes, but every link is still drawn exactly once and the script itself is untouched. A page someone else's
    /// tool drew in a tangle can be made readable without changing what it does.
    /// </summary>
    public partial class Flowgraph
    {
        private void arrangePageToolStripMenuItem_Click(object sender, EventArgs e) => ArrangeAndShow(null);
        private void arrangeFGToolStripMenuItem_Click(object sender, EventArgs e) => ArrangeAndShow(null);
        private void arrangeSelectedToolStripMenuItem_Click(object sender, EventArgs e) => ArrangeAndShow(stNodeEditor1.GetSelectedNode());

        private void ArrangeAndShow(STNode[] selection)
        {
            bool wholePage = selection == null || selection.Length < 2;
            List<STNode> nodes = wholePage ? stNodeEditor1.Nodes.ToArray().ToList() : selection.ToList();
            if (ArrangeNodes(nodes, wholePage ? "Arrange page " + _flowgraphName : "Arrange " + UndoLabels.Count(nodes.Count, "node", "nodes"), out List<STNode> arranged) == 0)
                return;
            if (wholePage)
                FitCanvasTo(arranged);
            else
                FocusCanvasOnNodes(arranged.ToArray());
        }

        /// <summary>Lay out every node on the page, as Arrange Page does (without moving the view). Returns how many nodes were moved or added.</summary>
        internal int ArrangeAll(string label) => ArrangeNodes(stNodeEditor1.Nodes.ToArray().ToList(), label, out List<STNode> arranged);

        /// <summary>
        /// Lay out <paramref name="nodes"/> by the links between them, keeping the top-left corner of the lot where it
        /// was, giving any data link between them that would run a long way a node of its own, as one undo step. Returns
        /// how many nodes were moved or added; <paramref name="arranged"/> is the nodes laid out, new ones included.
        /// </summary>
        internal int ArrangeNodes(List<STNode> nodes, string label, out List<STNode> arranged)
        {
            List<STNode> placing = OwnOrder(nodes.Where(o => o != null && o.Owner == stNodeEditor1).Distinct());
            arranged = placing;
            if (placing.Count < 2)
                return 0;

            Point origin = new Point(placing.Min(o => o.Left), placing.Min(o => o.Top));
            Dictionary<STNode, Point> start = placing.ToDictionary(o => o, o => o.Location);
            int added = 0;
            List<NodeMoveEdit.Movement> moves = new List<NodeMoveEdit.Movement>();
            using (UndoStack.Current.BeginGroup(label))
            {
                Point[] positions;
                for (int round = 0; ; round++)
                {
                    List<PageArranger.Connection> connections = Measure(placing, out List<(STNodeOption from, STNodeOption to)> pins);
                    List<PageArranger.Node> sized = placing.Select(o => new PageArranger.Node() { Size = new Size(o.Width, o.Height) }).ToList();
                    positions = PageArranger.Arrange(sized, connections);
                    List<PageArranger.Split> splits = round < PageArranger.SplitRounds ? PageArranger.SplitsFor(sized, connections, positions) : new List<PageArranger.Split>();
                    if (splits.Count == 0)
                        break;

                    foreach (PageArranger.Split split in splits)
                    {
                        //Each new node starts somewhere of its own, so an undo or redo can tell it from the others
                        Point at = new Point(origin.X - 100000 + added * 17, origin.Y - 100000 + added * 13);
                        STNode copy = SplitNode(placing[split.Node], placing[split.Partner], split.Connections.Select(i => pins[i]).ToList(), at);
                        if (copy == null)
                            continue;
                        start[copy] = copy.Location;
                        placing.Add(copy);
                        added++;
                    }
                    placing = OwnOrder(placing);
                }

                for (int i = 0; i < placing.Count; i++)
                {
                    Point to = new Point(origin.X + positions[i].X, origin.Y + positions[i].Y);
                    if (start[placing[i]] == to)
                        continue;
                    placing[i].SetPosition(to);
                    moves.Add(new NodeMoveEdit.Movement() { Node = new NodeRef(placing[i]), From = start[placing[i]], To = to });
                }
                if (moves.Count != 0 && CanRecord)
                    UndoStack.Current.Record(new NodeMoveEdit(_composite, _flowgraphName, moves, label));
            }
            arranged = placing;
            if (moves.Count == 0 && added == 0)
                return 0;

            stNodeEditor1.Invalidate();
            MarkFlowgraphEdit();
            RefreshNodeMarkers();
            return moves.Count + added;
        }

        /// <summary>
        /// In an order of their own rather than the page's, which clicking a node changes (it moves to the end, to draw
        /// on top): by entity, then by what each node connects to, so a page arranges the same way however it was clicked.
        /// </summary>
        private static List<STNode> OwnOrder(IEnumerable<STNode> nodes)
        {
            return nodes.OrderBy(o => o.ShortGUID.AsUInt32).ThenBy(Signature, StringComparer.Ordinal).ThenBy(o => o.Top).ThenBy(o => o.Left).ToList();
        }

        /// <summary>Each connection between the nodes once, with its pins' sides and where they sit on the nodes as drawn - and the pins themselves.</summary>
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
                        continue; //a blank row keeping a relay pair aligned, not a pin
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

        /// <summary>
        /// Give <paramref name="node"/>'s entity another node on the page, and move the connections between it and
        /// <paramref name="partner"/> onto that: each taken off the node, the pins left carrying nothing taken off with
        /// them, then the new node added with those pins and connections - recorded as the editor's own edits, inside
        /// whatever undo group is open. Returns the new node.
        /// </summary>
        private STNode SplitNode(STNode node, STNode partner, List<(STNodeOption from, STNodeOption to)> pins, Point at)
        {
            List<(ShortGuid pin, PinLocation side, PinStyle style, STNodeOption own, STNodeOption other)> moving = pins
                .Select(o => o.from.Owner == node ? (o.from, o.to) : (o.to, o.from))
                .Select(o => (o.Item1.ShortGUID, o.Item1.Location, o.Item1.Style, o.Item1, o.Item2)).ToList();
            if (moving.Count == 0 || moving.Any(o => o.own.Owner != node || o.other.Owner != partner))
                return null;

            PinSet before = SnapshotPins(node);
            foreach ((ShortGuid pin, PinLocation side, PinStyle style, STNodeOption own, STNodeOption other) in moving)
            {
                bool feeds = side == PinLocation.Right || side == PinLocation.Top;
                UndoStack.Current.Apply(new LinkEdit(_composite, _flowgraphName, feeds ? own : other, feeds ? other : own, false));
            }

            //The pins those connections used, if they now carry nothing, go from the node they leave
            PinSet after = new PinSet() { Location = before.Location };
            foreach (PinSnapshot kept in before.Pins)
            {
                STNodeOption option = FindOption(node, kept.Parameter, kept.Location);
                bool emptied = option != null && moving.Any(o => o.own == option) && (option.GetConnectedOption()?.Count ?? 0) == 0;
                if (!emptied)
                    after.Pins.Add(kept);
            }
            if (after.Pins.Count != before.Pins.Count)
                UndoStack.Current.Apply(new PinSetEdit(_composite, _flowgraphName, new NodeRef(node), before, after, "Remove pins from " + UndoLabels.Entity(_composite, node.Entity)));

            NodeSnapshot snapshot = new NodeSnapshot()
            {
                Page = _flowgraphName,
                Self = new NodeRef(node.ShortGUID, at),
                NodeID = stNodeEditor1.Nodes.ToArray().Max(o => o.NodeID) + 1,
                Location = at,
            };
            foreach ((ShortGuid pin, PinLocation side, PinStyle style, STNodeOption own, STNodeOption other) in moving)
            {
                if (!snapshot.Pins.Any(o => o.Parameter == pin && o.Location == side))
                    snapshot.Pins.Add(new PinSnapshot() { Parameter = pin, Location = side, Style = style });
                snapshot.Connections.Add(new ConnectionSnapshot()
                {
                    Peer = new NodeRef(partner),
                    ThisPin = pin,
                    ThisSide = side,
                    PeerPin = other.ShortGUID,
                    PeerSide = other.Location,
                    Outgoing = side == PinLocation.Right || side == PinLocation.Top,
                });
            }
            UndoStack.Current.Apply(new NodePresenceEdit(_composite, _flowgraphName, snapshot, true, "Add node for " + UndoLabels.Entity(_composite, node.Entity)));
            return snapshot.Self.Node;
        }

        /// <summary>A node's connections as text (each pin and what it joins), to tell apart nodes of one entity.</summary>
        private static string Signature(STNode node)
        {
            List<string> parts = new List<string>();
            foreach (STNodeOption pin in node.GetAllOptions())
            {
                if (pin == null || pin == STNodeOption.Empty)
                    continue;
                foreach (STNodeOption other in pin.GetConnectedOption() ?? new List<STNodeOption>())
                    parts.Add(pin.ShortGUID.AsUInt32 + ">" + (other.Owner?.ShortGUID.AsUInt32 ?? 0) + "." + other.ShortGUID.AsUInt32);
            }
            parts.Sort(StringComparer.Ordinal);
            return string.Join(";", parts);
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

        /// <summary>Where the pin's dot sits along its side of the node: down from the top for side pins, across from the left for top and bottom ones.</summary>
        private static int OffsetOf(STNode node, STNodeOption pin)
        {
            if (pin.Location == PinLocation.Top || pin.Location == PinLocation.Bottom)
            {
                //The middle of the pin's slot (top and bottom pins share the node's width equally), worked out rather than
                //read from the dot: that is rounded toward zero, so it moves a pixel when the node is left of the origin -
                //enough to give a page arranged a second time a slightly different layout
                STNodeOption[] row = pin.Location == PinLocation.Top ? node.GetTopOptions() : node.GetBottomOptions();
                int index = Array.IndexOf(row, pin);
                if (index >= 0)
                    return (int)Math.Round((index + 0.5) * node.Width / row.Length);
                return pin.DotLeft + pin.DotSize / 2 - node.Left;
            }
            return pin.DotTop + pin.DotSize / 2 - node.Top;
        }

        /// <summary>
        /// Zoom out (never in past 1:1) until the nodes fit the view, and centre on them. A page too big to fit even
        /// zoomed right out shows its top-left corner instead, where the biggest group of nodes starts.
        /// </summary>
        private void FitCanvasTo(List<STNode> nodes)
        {
            if (nodes.Count == 0)
                return;
            const float minScale = 0.2f, margin = 40f;
            int minX = nodes.Min(o => o.Left), minY = nodes.Min(o => o.Top);
            int maxX = nodes.Max(o => o.Left + o.Width), maxY = nodes.Max(o => o.Top + o.Height);
            float fit = Math.Min((stNodeEditor1.Width - margin * 2) / Math.Max(1, maxX - minX), (stNodeEditor1.Height - margin * 2) / Math.Max(1, maxY - minY));
            float scale = Math.Max(minScale, Math.Min(1f, fit));
            stNodeEditor1.ScaleCanvas(scale, stNodeEditor1.Width / 2f, stNodeEditor1.Height / 2f);
            scale = stNodeEditor1.CanvasScale;
            if (fit >= minScale)
                stNodeEditor1.CenterCanvasOn((minX + maxX) / 2f, (minY + maxY) / 2f, false);
            else
                stNodeEditor1.MoveCanvas(margin - minX * scale, margin - minY * scale, false);
        }
    }
}
