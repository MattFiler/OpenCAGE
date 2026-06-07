using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using OpenCAGE.Popups.Base;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using static OpenCAGE.NodeUtils;

namespace OpenCAGE.Popups
{
    public partial class ManageEntityPins : BaseWindow
    {
        private STNode _node;
        private Composite _composite;
        private Commands _commands;

        public event Action<STNode> PinsSaved;

        public ManageEntityPins() : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();
        }

        public void PopulateOptions(STNode node, Composite composite, Commands commands)
        {
            _node = node;
            _composite = composite;
            _commands = commands;

            this.Text = "Manage Pins - " + node.Title;

            listView1.BeginUpdate();
            listView1.Groups.Clear();
            listView1.Groups.Add(new ListViewGroup("Location: Left"));
            listView1.Groups.Add(new ListViewGroup("Location: Top (In)"));
            listView1.Groups.Add(new ListViewGroup("Location: Top (Out)"));
            listView1.Groups.Add(new ListViewGroup("Location: Right"));
            listView1.Groups.Add(new ListViewGroup("Location: Bottom"));
            listView1.Items.Clear();

            Dictionary<ShortGuid, STNodeOption> existingPins = new Dictionary<ShortGuid, STNodeOption>();
            foreach (STNodeOption option in node.GetAllOptions())
                existingPins[option.ShortGUID] = option;

            HashSet<ShortGuid> listedPins = new HashSet<ShortGuid>();
            List<PinPositionInfo> allPinPositions = node.GetAllPinPositions(composite, commands);
            foreach (PinPositionInfo pinInfo in allPinPositions)
            {
                listedPins.Add(pinInfo.ParameterGUID);
                AddPinListItem(pinInfo, existingPins.TryGetValue(pinInfo.ParameterGUID, out STNodeOption existing) ? existing : null);
            }

            foreach (STNodeOption existing in node.GetAllOptions())
            {
                if (listedPins.Contains(existing.ShortGUID))
                    continue;

                AddPinListItem(new PinPositionInfo
                {
                    ParameterGUID = existing.ShortGUID,
                    Location = existing.Location,
                    Style = existing.Style,
                }, existing);
            }

            listView1.EndUpdate();
        }

        private void AddPinListItem(PinPositionInfo pinInfo, STNodeOption existingOption)
        {
            ListViewItem item = new ListViewItem(GetPinDisplayName(existingOption, pinInfo.ParameterGUID));
            item.Tag = pinInfo;
            item.Checked = existingOption != null;
            item.SubItems.Add(FormatLinkCount(existingOption?.ConnectionCount ?? 0));
            ApplyPinListItemVisuals(item, pinInfo);
            listView1.Items.Add(item);
        }

        private static string GetPinDisplayName(STNodeOption existingOption, ShortGuid guid)
        {
            if (existingOption != null && !string.IsNullOrEmpty(existingOption.Text))
                return existingOption.Text;

            string named = ShortGuidUtils.FindString(guid);
            return string.IsNullOrEmpty(named) ? guid.ToByteString() : named;
        }

        private static string FormatLinkCount(int connectionCount)
        {
            if (connectionCount <= 0)
                return "";

            return connectionCount == 1 ? "1 link" : connectionCount + " links";
        }

        private void ApplyPinListItemVisuals(ListViewItem item, PinPositionInfo pinInfo)
        {
            switch (pinInfo.Location)
            {
                case PinLocation.Left:
                    item.ImageIndex = 1;
                    item.Group = listView1.Groups[0];
                    break;
                case PinLocation.Top:
                    item.ImageIndex = pinInfo.Style == PinStyle.ArrowUp ? 4 : 3;
                    item.Group = listView1.Groups[pinInfo.Style == PinStyle.ArrowUp ? 2 : 1];
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
        }

        private void selectAllBtn_Click(object sender, EventArgs e)
        {
            SetAllItemsChecked(true);
        }

        private void deselectAllBtn_Click(object sender, EventArgs e)
        {
            SetAllItemsChecked(false);
        }

        private void SetAllItemsChecked(bool isChecked)
        {
            listView1.BeginUpdate();
            foreach (ListViewItem item in listView1.Items)
                item.Checked = isChecked;
            listView1.EndUpdate();
        }

        private void saveBtn_Click(object sender, EventArgs e)
        {
            if (_node == null || _composite == null || _commands == null)
                return;

            HashSet<ShortGuid> selectedPinGuids = new HashSet<ShortGuid>();
            foreach (ListViewItem item in listView1.Items)
            {
                if (!item.Checked || !(item.Tag is PinPositionInfo pinInfo))
                    continue;

                selectedPinGuids.Add(pinInfo.ParameterGUID);
            }

            ApplyManagedPinSelection(_node, _composite, _commands, selectedPinGuids);
            PinsSaved?.Invoke(_node);
            Close();
        }
    }
}
