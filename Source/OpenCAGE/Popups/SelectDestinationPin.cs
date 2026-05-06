using CATHODE.Scripting;
using CommandsEditor.Popups.Base;
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

namespace CommandsEditor.Popups
{
    public partial class SelectDestinationPin : BaseWindow
    {
        private STNodeOption _comingFrom = null;
        private STNode _goingTo = null;

        public SelectDestinationPin() : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();
        }

        public void PopulateOptions(STNode node, STNodeOption option)
        {
            _goingTo = node;
            _comingFrom = option;

            //Hack: create a new node based off the current node's entity and add all pins to it, so that we can know all options to list
            STNode hackGetAllOptions = new STNode();
            hackGetAllOptions.Entity = node.Entity;
            hackGetAllOptions.AddAllPins(Singleton.Editor.CommandsDisplay.CompositeDisplay.Composite, Content.Level.Commands);

            listView1.BeginUpdate();

            listView1.Groups.Clear();
            listView1.Groups.Add(new ListViewGroup("Location: Left"));
            listView1.Groups.Add(new ListViewGroup("Location: Top (In)"));
            listView1.Groups.Add(new ListViewGroup("Location: Top (Out)"));
            listView1.Groups.Add(new ListViewGroup("Location: Right"));
            listView1.Groups.Add(new ListViewGroup("Location: Bottom"));

            listView1.Items.Clear();
            foreach (STNodeOption opt in hackGetAllOptions.GetAllOptions())
            {
                bool connectable = _comingFrom.CanConnect(opt) == ConnectionStatus.Connected;

                if (!connectable)
                    continue;

                ListViewItem item = new ListViewItem(opt.Text);
                switch (opt.Location)
                {
                    case PinLocation.Left:
                        item.ImageIndex = 1;
                        item.Group = listView1.Groups[0];
                        break;
                    case PinLocation.Top:
                        item.ImageIndex = opt.Style == PinStyle.ArrowUp ? 4 : 3;
                        item.Group = listView1.Groups[opt.Style == PinStyle.ArrowUp ? 2 : 1];
                        break;
                    case PinLocation.Right:
                        item.ImageIndex = 2;
                        item.Group = listView1.Groups[3];
                        break;
                    case PinLocation.Bottom:
                        item.ImageIndex = 0;
                        item.Group = listView1.Groups[4];
                        break;
                }
                //item.ForeColor = connectable ? Color.Green : Color.Red;
                item.Tag = opt.Location;
                listView1.Items.Add(item);
            }

            listView1.EndUpdate();

            //todo: should really validate (e.g. inputs cant connect to inputs)
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count == 0)
                return;

            ListViewItem selected = listView1.SelectedItems[0];
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
            _goingTo.Recompute();
            this.Close();
        }
    }
}
