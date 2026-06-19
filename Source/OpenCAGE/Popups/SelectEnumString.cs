using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;
using OpenCAGE.UserControls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace OpenCAGE
{
    public partial class SelectEnumString : BaseWindow
    {
        public Action<string> OnSelected;

        private cEnumString _defaultVal;

        private ListViewColumnSorter _sorter = new ListViewColumnSorter();
        private ListViewItem[] _filteredItems;

        public SelectEnumString(string paramName, cEnumString enumString, bool allowTypeSelect) : base(WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION | WindowClosesOn.COMMANDS_RELOAD)
        {
            InitializeComponent();

            Singleton.OnEnumStringUIShown?.Invoke(this);
            Singleton.OnEnumStringUIShown += OnAnotherEnumStringWindowShown;

            _defaultVal = enumString;
            this.Text = "Select for '" + paramName + "'";

            //Calling these to refresh anything that may have changed during runtime. It's likely it's already loaded.
            //NOTE: These are loaded elsewhere on a thread, we should check to see if that finished first.
            EnumStringListViewItems.PopulateGlobalEntries();
            EnumStringListViewItems.PopulateLevelSpecificEntries();

            if (allowTypeSelect)
            {
                List<string> types = new List<string>();
                foreach (EnumStringType type in Enum.GetValues(typeof(EnumStringType)))
                    types.Add(type.ToString());
                types.Clear();
                enumStringTypeSelect.BeginUpdate();
                enumStringTypeSelect.Items.Add(types);
                enumStringTypeSelect.EndUpdate();
                enumStringTypeSelect.SelectedItem = enumString.enumID.ToString();
            }
            else
            {
                enumStringTypeSelect.Visible = false;
                PopulateItems((EnumStringType)enumString.enumID.AsUInt32);
            }

            Search();
            clearSearchBtn.Visible = false;
            strings.ListViewItemSorter = _sorter;

            this.FormClosing += SelectEnumString_FormClosing;
        }

        private void SelectEnumString_FormClosing(object sender, FormClosingEventArgs e)
        {
            Singleton.OnEnumStringUIShown -= OnAnotherEnumStringWindowShown;
        }

        private void OnAnotherEnumStringWindowShown(SelectEnumString window)
        {
            //Ensure only one of these windows is open at once as we share the listviewitems.
            if (window != this)
            {
                OnSelected = null;
                base.Close();
            }
        }

        private void PopulateItems(EnumStringType type)
        {
            Tuple<ListViewItem[], bool> items = EnumStringListViewItems.GetItems(type);

            bool useDescColumn = items.Item2;
            if (type == EnumStringType.ANIMATION)
            {
                //Try and get the AnimationSet to filter this list by. If it doesn't exist, we'll show all.
                string animSet = "";
                Entity animEntity = Singleton.Editor?.CompositeDisplay?.EntityDisplay?.Entity;
                if (animEntity != null)
                {
                    Parameter animEntityAnimSet = animEntity.GetParameter("AnimationSet");
                    if (animEntityAnimSet?.content != null)
                    {
                        switch (animEntityAnimSet.content.dataType)
                        {
                            case DataType.STRING:
                            case DataType.ENUM_STRING:
                                animSet = ((cString)animEntityAnimSet.content).value;
                                break;
                        }
                    }
                }
                List<ListViewItem> filteredItems = new List<ListViewItem>();
                if (animSet != "")
                {
                    for (int i = 0; i < items.Item1.Length; i++)
                    {
                        if (items.Item1[i].SubItems[1].Text == animSet)
                            filteredItems.Add(items.Item1[i]);
                    }
                    useDescColumn = false;
                }
                else
                {
                    List<string> addedAnims = new List<string>();
                    for (int i = 0; i < items.Item1.Length; i++)
                    {
                        if (addedAnims.Contains(items.Item1[i].Text))
                            continue;
                        filteredItems.Add(items.Item1[i]);
                        addedAnims.Add(items.Item1[i].Text);
                    }
                }
                _filteredItems = filteredItems.ToArray();
            }
            else
            {
                _filteredItems = items.Item1;
            }

            //TODO: if the type changes via the list box, this will error. Not using it currently, but note for if we do.
            if (!useDescColumn)
            {
                strings.Columns.RemoveAt(1);
                strings.Columns[0].Width = 600;
            }

            ShowMetadata.Visible = type == EnumStringType.SOUND_EVENT;
        }

        private void SelectSpecialString_Load(object sender, EventArgs e)
        {
            int selectedIndex = -1;
            for (int i = 0; i < strings.Items.Count; i++)
            {
                strings.Items[i].Selected = false;
                if (strings.Items[i].Text == _defaultVal.value)
                {
                    selectedIndex = i;
                    strings.Items[i].Selected = true;
                }
            }
            strings.Invalidate();
            if (selectedIndex != -1)
                strings.EnsureVisible(selectedIndex);
        }

        private void ColumnClick(object sender, System.Windows.Forms.ColumnClickEventArgs e)
        {
            // Determine if the clicked column is already the column that is being sorted.
            if (e.Column == _sorter.SortColumn)
            {
                // Reverse the current sort direction for this column.
                if (_sorter.Order == SortOrder.Ascending)
                {
                    _sorter.Order = SortOrder.Descending;
                }
                else
                {
                    _sorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                // Set the column number that is to be sorted; default to ascending.
                _sorter.SortColumn = e.Column;
                _sorter.Order = SortOrder.Ascending;
            }

            // Perform the sort with these new sort options.
            this.strings.Sort();
        }

        private void search_box_TextChanged(object sender, EventArgs e)
        {
            Search();
            clearSearchBtn.Visible = search_box.Text != "";
        }

        private void clearSearchBtn_Click(object sender, EventArgs e)
        {
            search_box.Text = "";
            //Search();
        }

        private void Search()
        {
            strings.BeginUpdate();
            strings.SuspendLayout();
            strings.Items.Clear();
            strings.Items.AddRange(_filteredItems.Where(o => o.Text.ToUpper().Replace(" ", "").Contains(search_box.Text.ToUpper().Replace(" ", "")) || o.SubItems[o.SubItems.Count - 1].Text.ToUpper().Replace(" ", "").Contains(search_box.Text.ToUpper().Replace(" ", ""))).ToList().ToArray());
            strings.EndUpdate();
        }

        private void selectBtn_Click(object sender, EventArgs e)
        {
            if (strings.SelectedItems.Count == 0)
                return;

            //TODO: if Animation, maybe we also want to update AnimationSet?

            OnSelected?.Invoke(strings.SelectedItems[0].Text);
            this.Close();
        }

        public class AssetList
        {
            ~AssetList()
            {
                items = null;
            }

            public bool global = false;
            public string level = "";

            public EnumStringType type;

            public ListViewItem[] items = null;
            public bool use_desc_column = false;
        }

        private void ShowMetadata_Click(object sender, EventArgs e)
        {
            if (strings.SelectedItems.Count == 0)
                return;

            string selectedString = strings.SelectedItems[0].Text;

            string msg = "This event is contained within the following soundbanks:\n";
            foreach (SoundEventData.Soundbank entry in Content.Level.SoundEventData.Entries)
            {
                if (entry.events.FirstOrDefault(o => o.name == selectedString) == null)
                    continue;

                string soundbankName = entry.id.ToString();
                for (int i = 0; i < Content.Level.SoundBankData.Entries.Count; i++)
                {
                    if (Utilities.SoundHashedString(Content.Level.SoundBankData.Entries[i].Name) != entry.id)
                        continue;

                    soundbankName = Content.Level.SoundBankData.Entries[i].Name;
                    break;
                }

                msg += " - " + soundbankName + "\n";
            }
            MessageBox.Show(msg);
        }

        private void enumStringTypeSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            PopulateItems((EnumStringType)Enum.Parse(typeof(EnumStringType), enumStringTypeSelect.Text));
        }
    }
}
