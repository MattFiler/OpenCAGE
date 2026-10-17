using CATHODE.Scripting;
using OpenCAGE.Popups.Base;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    public partial class SelectDestinationPin : BaseWindow
    {
        private STNodeOption _comingFrom = null;
        private STNode _goingTo = null;

        /* Every pin the dragged one can connect to, sorted: kept so the Find box can narrow the list and bring it all back */
        private List<PinOption> _options = new List<PinOption>();

        /* When set, the page makes the pin and the connection itself (as one undo step) instead of this window */
        public Action<STNode, ShortGuid, PinLocation> PinChosen = null;

        private class PinOption
        {
            public string Name;
            public PinLocation Location;
            public int Group; //index into the list's groups
            public int Image;
        }

        public SelectDestinationPin() : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();
            EditorIcons.ShowIn(listView1, EditorIcons.Pins, EditorIcons.Pins);
            StayAboveEditor = true; //picker dialog

            //Up and Down step from one group's pins to the next rather than landing on the header between them
            ListViewGroupNavigation.Attach(listView1);

            //Opens ready to type a search into
            ActiveControl = findBox;
        }

        public void PopulateOptions(STNode node, STNodeOption option)
        {
            _goingTo = node;
            _comingFrom = option;

            //Hack: create a new node based off the current node's entity and add all pins to it, so that we can know all options to list
            STNode hackGetAllOptions = new STNode();
            hackGetAllOptions.Entity = node.Entity;
            hackGetAllOptions.AddAllPins(Singleton.Editor.CompositeDisplay.Composite, Content.Level.Commands);

            listView1.BeginUpdate();
            listView1.Groups.Clear();
            listView1.Groups.Add(new ListViewGroup("Location: Left"));
            listView1.Groups.Add(new ListViewGroup("Location: Top (In)"));
            listView1.Groups.Add(new ListViewGroup("Location: Top (Out)"));
            listView1.Groups.Add(new ListViewGroup("Location: Right"));
            listView1.Groups.Add(new ListViewGroup("Location: Bottom"));
            listView1.EndUpdate();

            _options.Clear();
            foreach (STNodeOption opt in hackGetAllOptions.GetAllOptions())
            {
                bool connectable = _comingFrom.CanConnect(opt) == ConnectionStatus.Connected;

                if (!connectable)
                    continue;

                PinOption pin = new PinOption() { Name = opt.Text, Location = opt.Location };
                switch (opt.Location)
                {
                    case PinLocation.Left:
                        pin.Image = 1;
                        pin.Group = 0;
                        break;
                    case PinLocation.Top:
                        pin.Image = opt.Style == PinStyle.ArrowUp ? 4 : 3;
                        pin.Group = opt.Style == PinStyle.ArrowUp ? 2 : 1;
                        break;
                    case PinLocation.Right:
                        pin.Image = 2;
                        pin.Group = 3;
                        break;
                    case PinLocation.Bottom:
                        pin.Image = 0;
                        pin.Group = 4;
                        break;
                }
                _options.Add(pin);
            }

            /* Alphabetical within each group, ignoring case, rather than the order the entity's definition lists them in -
               so a capitalised name like Light_ON isn't put ahead of every lower case one. The same comparison the
               inspector sorts its rows with, so an underscore comes before letters (charge_noise ahead of charged) in both */
            _options.Sort((a, b) =>
            {
                if (a.Group != b.Group)
                    return a.Group.CompareTo(b.Group);
                int order = string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
                return order != 0 ? order : string.CompareOrdinal(a.Name, b.Name);
            });

            ShowOptions();

            //todo: should really validate (e.g. inputs cant connect to inputs)
        }

        /* List the pins whose names hold what's in the Find box, ignoring case - all of them when it's empty. The list is
           rebuilt in the sorted order each time (a node has tens of pins, not thousands), and the list doesn't draw a group
           that's left with nothing in it. */
        private void ShowOptions()
        {
            string search = findBox.Text.Trim();

            //A pin still listed after the search changes stays selected, so Enter still picks it
            ListViewItem selected = listView1.SelectedItems.Count == 0 ? null : listView1.SelectedItems[0];
            string selectedName = selected?.Text;
            ListViewGroup selectedGroup = selected?.Group;
            ListViewItem reselect = null;

            listView1.BeginUpdate();
            listView1.Items.Clear();
            foreach (PinOption pin in _options)
            {
                if (search.Length != 0 && pin.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                ListViewItem item = new ListViewItem(pin.Name);
                item.ImageIndex = pin.Image;
                item.Group = listView1.Groups[pin.Group];
                item.Tag = pin.Location;
                listView1.Items.Add(item);

                if (pin.Name == selectedName && item.Group == selectedGroup)
                    reselect = item;
            }
            listView1.EndUpdate();

            if (reselect != null)
            {
                reselect.Selected = true;
                reselect.Focused = true;
                reselect.EnsureVisible();
            }
        }

        private void findBox_TextChanged(object sender, EventArgs e)
        {
            ShowOptions();
        }

        private void findBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                //Down carries on into the list, onto the first pin listed if none is selected yet
                case Keys.Down:
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    if (listView1.Items.Count == 0)
                        return;
                    ListViewItem item = listView1.SelectedItems.Count != 0 ? listView1.SelectedItems[0] : listView1.Items[0];
                    item.Selected = true;
                    item.Focused = true;
                    item.EnsureVisible();
                    listView1.Focus();
                    break;
                case Keys.Enter:
                    e.Handled = true;
                    e.SuppressKeyPress = true; //no ding from a single-line box
                    ConnectOnEnter();
                    break;
            }
        }

        private void listView1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.Handled = true;
            e.SuppressKeyPress = true;
            ConnectOnEnter();
        }

        /* Enter connects the selected pin, or the first one listed when none is: after a search that leaves just the one
           pin wanted, typing and Enter is all it takes */
        private void ConnectOnEnter()
        {
            if (listView1.Items.Count == 0)
                return;
            if (listView1.SelectedItems.Count == 0)
                listView1.Items[0].Selected = true; //the items are added in the order they're drawn
            button1.PerformClick();
        }

        private void listView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            //A double click on a pin connects it; one on the empty space below the pins does nothing
            if (e.Button == MouseButtons.Left && listView1.HitTest(e.Location).Item != null)
                button1.PerformClick();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0)
                return;

            ListViewItem selected = listView1.SelectedItems[0];
            if (PinChosen != null)
            {
                PinChosen(_goingTo, ShortGuidUtils.Generate(selected.Text), (PinLocation)selected.Tag);
                this.Close();
                return;
            }
            switch ((PinLocation)selected.Tag)
            {
                case PinLocation.Left:
                    _comingFrom.ConnectOption(_goingTo.AddInputOption(ShortGuidUtils.Generate(selected.Text)));
                    break;
                case PinLocation.Right:
                    _comingFrom.ConnectOption(_goingTo.AddOutputOption(ShortGuidUtils.Generate(selected.Text)));
                    break;
                case PinLocation.Top:
                    _comingFrom.ConnectOption(_goingTo.AddTopOption(ShortGuidUtils.Generate(selected.Text)));
                    break;
                case PinLocation.Bottom:
                    _comingFrom.ConnectOption(_goingTo.AddBottomOption(ShortGuidUtils.Generate(selected.Text)));
                    break;
            }
            _goingTo.AlignRelayRows(Singleton.Editor.CompositeDisplay.Composite, Content.Level.Commands);
            _goingTo.Recompute();
            this.Close();
        }
    }
}
