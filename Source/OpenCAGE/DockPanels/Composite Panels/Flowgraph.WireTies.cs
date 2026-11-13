using OpenCAGE.Undo;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Wire ties (STNodeEditor.WireTies.cs): hold T and drag a line across links to tie them together at one point, so links
    /// running the same way read as one cable. Each change is one undo step; the ties are saved with the page's layout
    /// (WireTieLayouts) and laid out again by Arrange.
    /// </summary>
    public partial class Flowgraph
    {
        private ToolStripSeparator _wireTieSeparator;
        private ToolStripMenuItem _tieLinksItem;
        private ToolStripMenuItem _untieItem;
        private ToolStripMenuItem _untieLinkItem;
        private ToolStripMenuItem _straightenTieItem;
        //The tie the menu was opened on
        private WireTieState _menuTie;
        //Arrange: the ties as it found them (recorded first in its undo step), and where the nodes were
        private WireTieEdit _arrangeTies;
        private Dictionary<STNode, Point> _arrangeTieStart;

        /// <summary>The tie menu items and the undo recording. Called once, from the constructor.</summary>
        partial void SetUpWireTies()
        {
            stNodeEditor1.WireTieChanged += StNodeEditor1_WireTieChanged;

            _wireTieSeparator = new ToolStripSeparator();
            _tieLinksItem = new ToolStripMenuItem("Tie Links", null, (s, e) => stNodeEditor1.ArmTieLine())
            {
                ShortcutKeyDisplayString = "Hold T + drag",
                ToolTipText = "Drag a line across links to tie them together where it crosses them.\nOr hold T and drag at any time. Esc cancels.",
            };
            _untieItem = new ToolStripMenuItem("Untie", null, (s, e) => { if (_menuTie != null) stNodeEditor1.UntieWire(_menuTie.Id); })
            {
                ShortcutKeyDisplayString = "Del",
                ToolTipText = "Take the tie away: its links run straight on.",
            };
            _untieLinkItem = new ToolStripMenuItem("Untie Link") { ToolTipText = "Take one link out of the tie." };
            _straightenTieItem = new ToolStripMenuItem("Straighten", null, (s, e) => { if (_menuTie != null) stNodeEditor1.StraightenWireTie(_menuTie.Id); })
            {
                ToolTipText = "Move the tie back onto its links' straight path, with their lanes in order so they don't cross.",
            };
            EditorIcons.Bind(_tieLinksItem, EditorIcon.AddReroutePoint);
            EditorIcons.Bind(_untieItem, EditorIcon.RemoveAllReroutePoints);
            EditorIcons.Bind(_untieLinkItem, EditorIcon.RemoveReroutePoint);
            EditorIcons.Bind(_straightenTieItem, EditorIcon.ArrangeSelected);
            nodeContextMenu.Items.AddRange(new ToolStripItem[] { _wireTieSeparator, _tieLinksItem, _untieItem, _untieLinkItem, _straightenTieItem });
            //Added after the menu's own handler, so this has the last word on what shows over a tie
            nodeContextMenu.Opening += WireTieMenu_Opening;

            this.FormClosed += (s, e) =>
            {
                stNodeEditor1.WireTieChanged -= StNodeEditor1_WireTieChanged;
                nodeContextMenu.Opening -= WireTieMenu_Opening;
            };
        }

        private bool IsWireTieItem(ToolStripItem item)
        {
            return item == _wireTieSeparator || item == _tieLinksItem || item == _untieItem || item == _untieLinkItem || item == _straightenTieItem;
        }

        /* Over a tie, the menu is the tie's alone; on the canvas or a link it offers Tie Links */
        private void WireTieMenu_Opening(object sender, CancelEventArgs e)
        {
            _menuTie = stNodeEditor1.GetWireTieUnderMouse();
            bool onTie = _menuTie != null;
            if (onTie)
            {
                foreach (ToolStripItem item in nodeContextMenu.Items)
                    if (!IsWireTieItem(item))
                        item.Visible = false;
            }

            bool onCanvasOrLink = !onTie && stNodeEditor1.GetHoveredNode() == null && stNodeEditor1.GetHoveredPin() == null;
            _tieLinksItem.Visible = onCanvasOrLink && stNodeEditor1.AllowWireTies;
            _wireTieSeparator.Visible = _tieLinksItem.Visible;
            _untieItem.Visible = onTie;
            _untieLinkItem.Visible = onTie;
            _straightenTieItem.Visible = onTie;

            _untieLinkItem.DropDownItems.Clear();
            if (!onTie)
                return;
            _untieItem.Text = _menuTie.Members.Count == 1 ? "Untie" : "Untie " + _menuTie.Members.Count + " Links";
            foreach (WireTieMemberState member in _menuTie.Members)
            {
                WireTieMemberState link = member;
                int id = _menuTie.Id;
                _untieLinkItem.DropDownItems.Add(new ToolStripMenuItem(DescribeLink(link.From, link.To), null, (s, a) => stNodeEditor1.UntieWireLink(id, link.From, link.To)));
            }
            Theming.ThemeManager.ApplyCurrent(_untieLinkItem.DropDown);
        }

        private static string DescribeLink(STNodeOption from, STNodeOption to)
        {
            string End(STNodeOption pin) => (pin?.Owner?.Title ?? "?") + " [" + (pin?.Text ?? "?") + "]";
            return End(from) + "  →  " + End(to);
        }

        /* Ties are stored in the page's layout, so a change to them is a change to the level */
        private void StNodeEditor1_WireTieChanged(object sender, STNodeEditorWireTieEventArgs e)
        {
            MarkFlowgraphEdit();
            if (!CanRecord || e == null)
                return;
            string label;
            switch (e.Change)
            {
                case WireTieChange.Tied: label = "Tie " + UndoLabels.Count(e.Links, "link", "links"); break;
                case WireTieChange.Untied: label = "Untie " + UndoLabels.Count(e.Links, "link", "links"); break;
                case WireTieChange.UntiedLink: label = "Untie a link"; break;
                case WireTieChange.Straightened: label = "Straighten wire tie"; break;
                default: label = "Move wire tie"; break;
            }
            UndoStack.Current.Record(new WireTieEdit(_composite, _flowgraphName, WireTieRef.From(e.Before), WireTieRef.From(e.After), label));
        }

        /// <summary>Make the page's ties these, as an undo or redo asks (links no longer on the page are left out).</summary>
        internal void SetWireTieRefs(List<WireTieRef> ties)
        {
            List<WireTieState> states = new List<WireTieState>();
            foreach (WireTieRef tie in ties ?? new List<WireTieRef>())
            {
                WireTieState state = new WireTieState() { Id = tie.Id, Position = tie.Position, Axis = tie.Axis };
                foreach (WireTieRef.Member member in tie.Members)
                {
                    STNode source = ResolveNode(member.Line?.Source);
                    STNode target = ResolveNode(member.Line?.Target);
                    if (source == null || target == null)
                        continue;
                    STNodeOption pinOut = FindOption(source, member.Line.SourcePin, member.Line.SourceSide) ?? source.GetOption(member.Line.SourcePin);
                    STNodeOption pinIn = FindOption(target, member.Line.TargetPin, member.Line.TargetSide) ?? target.GetOption(member.Line.TargetPin);
                    if (pinOut != null && pinIn != null)
                        state.Members.Add(new WireTieMemberState() { From = pinOut, To = pinIn, Slot = member.Slot, Order = member.Order });
                }
                states.Add(state);
            }
            stNodeEditor1.SetWireTies(states);
            stNodeEditor1.Invalidate();
            DirtyTracker.MarkLevelDataModified();
        }

        /* A page built from its saved layout: its ties, after its connections */
        partial void ApplyWireTies(CathodeLib.CompositeFlowgraphTable.FlowgraphMeta flowgraphMeta)
        {
            WireTieLayouts.Apply(stNodeEditor1, flowgraphMeta);
        }

        /* Arrange is about to lay nodes out, inside its undo step: the ties as they are go first in the step, so undoing puts them
           back after everything else (the links Arrange moved onto new nodes are back by then) */
        partial void WireTiesBeforeArrange(List<STNode> nodes)
        {
            _arrangeTies = null;
            WireTieState[] ties = stNodeEditor1.GetWireTies();
            if (ties.Length == 0)
                return;
            _arrangeTies = new WireTieEdit(_composite, _flowgraphName, WireTieRef.From(ties), null, "Re-place wire ties");
            if (CanRecord)
                UndoStack.Current.Record(_arrangeTies);
            _arrangeTieStart = stNodeEditor1.Nodes.ToArray().Where(o => o != null).ToDictionary(o => o, o => o.Location);
        }

        /* The nodes are laid out: each tie on a link whose node moved goes back onto its links' path, ties along the same links in
           order - recorded last in the step, so redoing sets them after everything else */
        partial void WireTiesAfterArrange(List<STNode> nodes)
        {
            WireTieEdit begun = _arrangeTies;
            Dictionary<STNode, Point> start = _arrangeTieStart ?? new Dictionary<STNode, Point>();
            _arrangeTies = null;
            _arrangeTieStart = null;
            if (begun == null)
                return;
            HashSet<STNode> arranged = new HashSet<STNode>(nodes ?? new List<STNode>());
            bool Moved(STNode node) => node != null && (arranged.Contains(node) || !start.TryGetValue(node, out Point was) || was != node.Location);
            List<int> ids = stNodeEditor1.GetWireTies()
                .Where(o => o.Members.Any(m => Moved(m.From?.Owner) || Moved(m.To?.Owner)))
                .Select(o => o.Id).ToList();
            if (ids.Count != 0)
                stNodeEditor1.PlaceWireTies(ids);
            if (CanRecord)
                UndoStack.Current.Record(new WireTieEdit(_composite, _flowgraphName, null, WireTieRef.From(stNodeEditor1.GetWireTies()), "Re-place wire ties"));
            stNodeEditor1.Invalidate();
        }
    }
}
