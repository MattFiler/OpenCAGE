using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    /* Every link in and out of one node on a flowgraph page, grouped by the kind of pin on the node it's on. It stays up to
       date while it's open, however the links change (drawn, deleted, undone). Go To brings the page forward with the node
       at the other end of the chosen link selected, and closes the window. It goes with the page it was opened from, and
       closes if the node leaves it.
       Order is a link's place among the links of the pin it leaves from (on whichever node that is), across every page:
       the order a relay fires them in, and for links setting a value, the last one wins. Move Up / Move Down (Alt+Up /
       Alt+Down) change it; see LinkOrder. A link drawn since the composite was last compiled shows "new" (it goes last). */
    public partial class ShowConnectedLinks : BaseWindow
    {
        private readonly Flowgraph _flowgraph;
        private readonly STNode _node;
        private readonly STNodeEditor _editor;

        /* Every link the node has, sorted: kept so the Find box can narrow the list and bring it all back */
        private List<Link> _links = new List<Link>();

        private class Link
        {
            public STNodeOption Pin;   //the node's own pin
            public STNodeOption Other; //the pin it's linked to, on the node at the other end
            public int Group;          //index into the list's groups
            public int Image;
            public string PinName;
            public string NodeName;
            public string NodeType;
            public string OtherPinName;

            //The link as the composite stores it: from the pin on the node that owns it (a right or top pin) to the other end
            public Entity Owner;
            public EntityConnector Connector;
            public int Occurrence; //the k-th listed copy of the same link
            public int Place;      //0-based among its pin's stored links; -1 when not stored yet
            public int PinCount;   //how many links its pin has stored
        }

        /* The groups, in this order: the kind of pin is the side of the node the flowgraph draws it on (as the pin pickers
           show it), so a link is listed with the pin it's drawn from */
        private static readonly string[] GroupNames = { "Input", "Output", "Target", "Method", "Reference" };

        private bool _refreshQueued = false;
        private bool _hooked = false;

        public ShowConnectedLinks(Flowgraph flowgraph, STNode node) : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            InitializeComponent();
            EditorIcons.ShowIn(linkList, EditorIcons.Pins, EditorIcons.Pins);
            StayAboveEditor = true; //kept in view while links are drawn on the page behind it

            //Up and Down step from one group's links to the next rather than landing on the header between them
            ListViewGroupNavigation.Attach(linkList);

            _flowgraph = flowgraph;
            _node = node;
            _editor = flowgraph.Nodegraph;

            linkList.BeginUpdate();
            linkList.Groups.Clear();
            foreach (string name in GroupNames)
                linkList.Groups.Add(new ListViewGroup(name));
            linkList.EndUpdate();

            _editor.OptionConnected += OnLinkChanged;
            _editor.OptionDisconnected += OnLinkChanged;
            _editor.NodeRemoved += OnNodeRemoved;
            Singleton.OnEntityRenamed += OnEntityRenamed;
            LinkOrder.OrderChanged += OnOrderChanged;
            _hooked = true;

            _tips = new ToolTip();
            _tips.SetToolTip(moveUpButton, "Fire the selected link earlier among the links of the pin it leaves from (Alt+Up)");
            _tips.SetToolTip(moveDownButton, "Fire the selected link later among the links of the pin it leaves from (Alt+Down)");
            Disposed += (o, e) => _tips.Dispose();
            FormClosed += (o, e) => Unhook();
            Disposed += (o, e) => Unhook();

            //Closes with the page (and on a level load), not as the selection moves: it's kept open while editing
            CloseWith(flowgraph);

            Populate();

            //Opens ready to type a search into
            ActiveControl = findBox;
        }

        private void Unhook()
        {
            if (!_hooked)
                return;
            _hooked = false;
            _editor.OptionConnected -= OnLinkChanged;
            _editor.OptionDisconnected -= OnLinkChanged;
            _editor.NodeRemoved -= OnNodeRemoved;
            Singleton.OnEntityRenamed -= OnEntityRenamed;
            LinkOrder.OrderChanged -= OnOrderChanged;
        }

        private ToolTip _tips;

        /* A pin's order changed (here, an undo, MCP): any of the node's links may show another place */
        private void OnOrderChanged(Composite composite, Entity owner)
        {
            QueueRefresh();
        }

        /* A link to or from the node was made or broken. The page raises these for every link on it - hundreds at once when
           it's arranged or pasted into - and for attempts that were turned down, so only the node's own count, and a run of
           them is one rebuild */
        private void OnLinkChanged(object sender, STNodeEditorOptionEventArgs e)
        {
            if (e.Status != ConnectionStatus.Connected && e.Status != ConnectionStatus.Disconnected)
                return;
            if (e.TargetOption?.Owner != _node && e.CurrentOption?.Owner != _node)
                return;
            QueueRefresh();
        }

        /* The node itself (it closes, unless it's back by the time this looks: an undo puts it back) or one at the other end
           of a link (a node is taken off the page without each of its links being reported) */
        private void OnNodeRemoved(object sender, STNodeEditorEventArgs e)
        {
            QueueRefresh();
        }

        private void OnEntityRenamed(Entity entity, string name)
        {
            QueueRefresh();
        }

        private void QueueRefresh()
        {
            if (_refreshQueued || IsDisposed)
                return;
            if (!IsHandleCreated)
            {
                Populate();
                return;
            }
            _refreshQueued = true;
            BeginInvoke(new Action(() =>
            {
                _refreshQueued = false;
                Populate();
            }));
        }

        /* Gather the node's links from its pins as they are on the page now */
        private void Populate()
        {
            if (IsDisposed)
                return;
            if (_node.Owner != _editor)
            {
                Close();
                return;
            }

            Text = "Connected Links: " + _node.Title;

            _links.Clear();
            foreach (STNodeOption pin in _node.GetAllOptions())
            {
                if (pin == null || pin == STNodeOption.Empty)
                    continue; //a blank row holding a relay pair aligned, not a pin

                List<STNodeOption> connected = pin.GetConnectedOption();
                if (connected == null)
                    continue;

                (int group, int image) = KindOf(pin);
                foreach (STNodeOption other in connected)
                {
                    STNode otherNode = other?.Owner;
                    if (otherNode == null || otherNode.Owner != _editor)
                        continue; //on a node that's no longer on the page

                    Link link = new Link()
                    {
                        Pin = pin,
                        Other = other,
                        Group = group,
                        Image = image,
                        PinName = pin.Text,
                        NodeName = otherNode.Title ?? "",
                        NodeType = otherNode.SubTitle ?? "",
                        OtherPinName = other.Text,
                    };
                    SetStoredPlace(link);
                    _links.Add(link);
                }
            }

            /* Alphabetical within each group by the node's pin, then the node and pin at the other end, ignoring case - the
               comparison the pin pickers sort with. Links leaving the same pin of this node are listed in their order. */
            _links.Sort((a, b) =>
            {
                if (a.Group != b.Group)
                    return a.Group.CompareTo(b.Group);
                int order = CompareNames(a.PinName, b.PinName);
                if (order == 0 && a.Owner == _node.Entity && b.Owner == _node.Entity)
                    order = PlaceKey(a).CompareTo(PlaceKey(b));
                if (order == 0)
                    order = CompareNames(a.NodeName, b.NodeName);
                if (order == 0)
                    order = CompareNames(a.OtherPinName, b.OtherPinName);
                if (order == 0)
                    order = PlaceKey(a).CompareTo(PlaceKey(b));
                return order;
            });

            ShowLinks();
        }

        private static int PlaceKey(Link link) => link.Place < 0 ? int.MaxValue : link.Place;

        /* Which end owns the link (the compile stores it on the node whose right or top pin it leaves from), and its place
           among that pin's stored links */
        private void SetStoredPlace(Link link)
        {
            STNode node = link.Pin.Owner;
            bool ownedHere = node.GetOutputOptions().Contains(link.Pin) || node.GetTopOptions().Contains(link.Pin);
            STNodeOption from = ownedHere ? link.Pin : link.Other;
            STNodeOption to = ownedHere ? link.Other : link.Pin;
            link.Owner = from.Owner?.Entity;
            link.Connector = new EntityConnector() { thisParamID = from.ShortGUID, linkedEntityID = to.Owner.ShortGUID, linkedParamID = to.ShortGUID };
            link.Occurrence = _links.Count(o => o.Owner == link.Owner && o.Connector.Equals(link.Connector));
            link.Place = link.Owner == null ? -1 : LinkOrder.PlaceOf(link.Owner, link.Connector, link.Occurrence);
            link.PinCount = link.Owner == null ? 0 : LinkOrder.PinLinks(link.Owner, from.ShortGUID).Count;
        }

        private static string OrderText(Link link)
        {
            if (link.Place < 0)
                return "new";
            return (link.Place + 1) + "/" + link.PinCount;
        }

        private static int CompareNames(string a, string b)
        {
            int order = string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase);
            return order != 0 ? order : string.CompareOrdinal(a, b);
        }

        /* The group and pin icon for a pin, by the side of the node it's on (an output is the top pin pointing up) */
        private static (int group, int image) KindOf(STNodeOption pin)
        {
            switch (pin.Location)
            {
                case PinLocation.Top:
                    return pin.Style == PinStyle.ArrowUp ? (1, 4) : (0, 3);
                case PinLocation.Right:
                    return (2, 2);
                case PinLocation.Left:
                    return (3, 1);
                default:
                    return (4, 0);
            }
        }

        /* List the links with what's in the Find box in their pin, node or linked pin's name (or the node's type), ignoring
           case - all of them when it's empty. Rebuilt in the sorted order each time; the list doesn't draw a group left with
           nothing in it. */
        private void ShowLinks()
        {
            string search = findBox.Text.Trim();

            //A link still listed after a search or an update stays selected, so Enter still goes to it
            Link selected = linkList.SelectedItems.Count == 0 ? null : linkList.SelectedItems[0].Tag as Link;
            ListViewItem reselect = null;

            linkList.BeginUpdate();
            linkList.Items.Clear();
            foreach (Link link in _links)
            {
                if (search.Length != 0 && !Matches(link, search))
                    continue;

                ListViewItem item = new ListViewItem(new string[] { link.PinName, OrderText(link), link.NodeName, link.OtherPinName });
                item.ImageIndex = link.Image;
                item.Group = linkList.Groups[link.Group];
                item.Tag = link;
                item.ToolTipText = link.PinName + " - " + link.NodeName + (link.NodeType != "" ? " (" + link.NodeType + ")" : "") + ": " + link.OtherPinName +
                    (link.Place < 0 ? "\nNew: goes after its pin's other links when the composite is saved" : "\nLink " + (link.Place + 1) + " of " + link.PinCount + " on the pin it leaves from (all pages)");
                linkList.Items.Add(item);

                if (selected != null && link.Pin == selected.Pin && link.Other == selected.Other)
                    reselect = item;
            }
            linkList.EndUpdate();

            if (reselect != null)
            {
                reselect.Selected = true;
                reselect.Focused = true;
                reselect.EnsureVisible();
            }

            if (_links.Count == 0)
                countLabel.Text = "No links in or out of this node.";
            else if (linkList.Items.Count != _links.Count)
                countLabel.Text = linkList.Items.Count + " of " + _links.Count + " links match.";
            else
                countLabel.Text = _links.Count == 1 ? "1 link." : _links.Count + " links.";
            goToButton.Enabled = linkList.Items.Count != 0;
            UpdateMoveButtons();
        }

        private Link SelectedLink => linkList.SelectedItems.Count == 0 ? null : linkList.SelectedItems[0].Tag as Link;

        /* A link can move while its pin has another link to swap with (a new one is stored first, so it counts) */
        private void UpdateMoveButtons()
        {
            Link link = SelectedLink;
            bool movable = link?.Owner != null && (link.Place < 0 ? link.PinCount >= 1 : link.PinCount >= 2);
            moveUpButton.Enabled = movable && link.Place != 0;
            moveDownButton.Enabled = movable && link.Place >= 0 && link.Place < link.PinCount - 1;
        }

        private void linkList_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateMoveButtons();
        }

        private void moveUpButton_Click(object sender, EventArgs e)
        {
            MoveSelected(-1);
        }

        private void moveDownButton_Click(object sender, EventArgs e)
        {
            MoveSelected(1);
        }

        /* Move the selected link among its pin's links. One drawn since the last compile is stored first: the composite's pages
           are compiled (which keeps every stored link's order and puts it last) */
        private void MoveSelected(int delta)
        {
            Link link = SelectedLink;
            if (link?.Owner == null)
                return;

            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            if (display == null || display.IsDisposed || display.Composite == null || !display.Flowgraphs.Contains(_flowgraph))
                return;

            if (link.Place < 0)
            {
                display.SaveAllFlowgraphs();
                link.Place = LinkOrder.PlaceOf(link.Owner, link.Connector, link.Occurrence);
                if (link.Place < 0)
                    return;
            }

            if (LinkOrder.Move(display.Composite, link.Owner, link.Connector, delta, link.Occurrence))
            {
                Populate(); //now, not queued: the same link stays selected for the next press
                ListViewItem again = linkList.Items.Cast<ListViewItem>().FirstOrDefault(o => o.Tag is Link l && l.Pin == link.Pin && l.Other == link.Other);
                if (again != null)
                {
                    again.Selected = true;
                    again.Focused = true;
                    again.EnsureVisible();
                }
            }
        }

        private static bool Matches(Link link, string search)
        {
            return link.PinName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || link.NodeName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || link.NodeType.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || link.OtherPinName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void findBox_TextChanged(object sender, EventArgs e)
        {
            ShowLinks();
        }

        private void findBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                //Down carries on into the list, onto the first link listed if none is selected yet
                case Keys.Down:
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    if (linkList.Items.Count == 0)
                        return;
                    ListViewItem item = linkList.SelectedItems.Count != 0 ? linkList.SelectedItems[0] : linkList.Items[0];
                    item.Selected = true;
                    item.Focused = true;
                    item.EnsureVisible();
                    linkList.Focus();
                    break;
                case Keys.Enter:
                    e.Handled = true;
                    e.SuppressKeyPress = true; //no ding from a single-line box
                    GoToOnEnter();
                    break;
            }
        }

        private void linkList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Alt && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                MoveSelected(e.KeyCode == Keys.Up ? -1 : 1);
                return;
            }
            if (e.KeyCode != Keys.Enter)
                return;
            e.Handled = true;
            e.SuppressKeyPress = true;
            GoToOnEnter();
        }

        /* Enter goes to the selected link, or the first one listed when none is: after a search that leaves just the one
           link wanted, typing and Enter is all it takes */
        private void GoToOnEnter()
        {
            if (linkList.Items.Count == 0)
                return;
            if (linkList.SelectedItems.Count == 0)
                linkList.Items[0].Selected = true; //the items are added in the order they're drawn
            goToButton.PerformClick();
        }

        private void linkList_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            //A double click on a link goes to it; one on the empty space below the links does nothing
            if (e.Button == MouseButtons.Left && linkList.HitTest(e.Location).Item != null)
                goToButton.PerformClick();
        }

        private void goToButton_Click(object sender, EventArgs e)
        {
            if (linkList.SelectedItems.Count == 0)
                return;

            Link link = linkList.SelectedItems[0].Tag as Link;
            STNode target = link?.Other?.Owner;
            if (target == null || target.Owner != _editor)
            {
                Populate(); //it went from the page while this was open
                return;
            }

            _flowgraph.GoToNode(target);
            this.Close();
        }
    }
}
