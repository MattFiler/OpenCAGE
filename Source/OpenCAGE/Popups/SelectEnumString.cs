using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.Audio;
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

        private string _currentText; //the value to select when the list opens

        private ListViewColumnSorter _sorter = new ListViewColumnSorter();
        private ListViewItem[] _filteredItems;
        private SoundPreviewPanel _soundPreview = null;
        private EnumStringType _currentType;

        //Browsing (the Sound Editor on the View menu) rather than picking a value for a parameter
        private readonly bool _browse;

        public SelectEnumString(string paramName, cEnumString enumString, bool allowTypeSelect) : base(WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION | WindowClosesOn.COMMANDS_RELOAD)
        {
            _browse = false;
            Initialise("Select for '" + paramName + "'", enumString, allowTypeSelect);
        }

        /// <summary>
        /// The window as an editor in its own right: every string of one type to look through, preview
        /// and (for sounds) replace, with nothing to select. Stays open across entity and composite
        /// selections, like the other editors on the View menu, and closes when the level does.
        /// </summary>
        public SelectEnumString(EnumStringType type, string title) : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            _browse = true;
            Initialise(title, new cEnumString(type), false);
        }

        /// <summary>
        /// A picker over names the caller supplies rather than an enum string type's list - e.g. the inventory items an
        /// InventoryItem entity names, read from the Item &amp; Inventory editor's file. Each item is (name, description).
        /// </summary>
        public SelectEnumString(string paramName, string currentValue, List<KeyValuePair<string, string>> items) : base(WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION | WindowClosesOn.COMMANDS_RELOAD)
        {
            _browse = false;
            InitializeComponent();
            StayAboveEditor = true;

            Singleton.OnEnumStringUIShown?.Invoke(this);
            Singleton.OnEnumStringUIShown += OnAnotherEnumStringWindowShown;

            _currentText = currentValue;
            this.Text = "Select for '" + paramName + "'";
            enumStringTypeSelect.Visible = false;
            _filteredItems = items.Select(o => new ListViewItem(new string[] { o.Key, o.Value })).ToArray();

            clearSearchBtn.Visible = false;
            strings.ListViewItemSorter = _sorter;
            this.FormClosing += SelectEnumString_FormClosing;
        }

        private void Initialise(string title, cEnumString enumString, bool allowTypeSelect)
        {
            InitializeComponent();
            StayAboveEditor = !_browse; //a picker for a parameter field - it shouldn't be able to fall behind the editor

            this.HelpButtonClicked += (sender, e) =>
            {
                e.Cancel = true;
                if (_currentType == EnumStringType.SOUND_EVENT)
                {
                    try
                    {
                        Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
                        System.Diagnostics.Process.Start("https://opencage.co.uk/docs/sound-events/");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to open documentation: " + ex.Message);
                    }
                }
            };

            Singleton.OnEnumStringUIShown?.Invoke(this);
            Singleton.OnEnumStringUIShown += OnAnotherEnumStringWindowShown;

            _currentText = enumString?.value;
            this.Text = title;

            if (_browse)
            {
                //Nothing to select
                selectBtn.Visible = false;
            }

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

            //The list is filled in Load, once its window exists. Items added before that are inserted one at a time while
            //the window is being created, which took most of a minute for a big level's sound events.
            clearSearchBtn.Visible = false;
            strings.ListViewItemSorter = _sorter;

            this.FormClosing += SelectEnumString_FormClosing;
        }

        private void SelectEnumString_FormClosing(object sender, FormClosingEventArgs e)
        {
            Singleton.OnEnumStringUIShown -= OnAnotherEnumStringWindowShown;

            //Stop the audio device before the window goes, rather than waiting on a collection
            if (_soundPreview != null)
            {
                strings.SelectedIndexChanged -= SoundSelectionChanged;
                _soundPreview.Dispose();
                _soundPreview = null;
            }
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
            // Materials use EditMaterial via the parameter grid's enum-string editor — not this list UI
            if (type == EnumStringType.MATERIAL)
                return;

            _currentType = type;
            bool isSound = type == EnumStringType.SOUND_EVENT;
            this.HelpButton = isSound;
            if (isSound)
            {
                this.MaximizeBox = false;
                this.MinimizeBox = false;
            }

            Tuple<ListViewItem[], bool> items = EnumStringListViewItems.GetItems(type);
            if (items == null)
                return;

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
            else if (type == EnumStringType.SOUND_EVENT)
            {
                _filteredItems = WithSoundbanks(items.Item1);
                useDescColumn = true;
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

            if (type == EnumStringType.SOUND_EVENT)
                ShowSoundPreview();
        }

        /// <summary>
        /// The sound events with the soundbanks each is in (from the level's sound data) in the second column, in place of
        /// the text it shows for other lists - so which bank to load for an event is there on every row, and searching for a
        /// bank's name finds its events. The few events that have text of their own (a line's subtitle) keep it, in a
        /// third column. New items: the cached ones are shared with every other list of this type.
        /// </summary>
        private ListViewItem[] WithSoundbanks(ListViewItem[] events)
        {
            strings.Columns[1].Text = "Soundbanks";

            bool anyText = events.Any(o => o.SubItems.Count > 1);
            if (anyText)
            {
                strings.Columns[0].Width = 250;
                strings.Columns[1].Width = 290;
                strings.Columns.Add("Text", 190);
            }

            ListViewItem[] withBanks = new ListViewItem[events.Length];
            for (int i = 0; i < events.Length; i++)
            {
                List<string> banks = SoundEventMetadata.BanksFor(events[i].Text);
                ListViewItem item = new ListViewItem(new[] { events[i].Text, string.Join(", ", banks.OrderBy(o => o, StringComparer.OrdinalIgnoreCase)) });
                if (anyText)
                    item.SubItems.Add(events[i].SubItems.Count > 1 ? events[i].SubItems[1].Text : "");
                withBanks[i] = item;
            }
            return withBanks;
        }

        /// <summary>
        /// Give the window a player along its bottom edge, and start indexing the game's soundbanks so
        /// the first sound the user clicks is ready rather than waiting on a cold index.
        /// </summary>
        private void ShowSoundPreview()
        {
            if (_soundPreview != null)
                return;

            _soundPreview = new SoundPreviewPanel();

            //Grow the window by the height of the player, then take that height back off the list, so
            //the buttons below stay where they were and the list keeps the size it was designed at
            int height = SoundPreviewPanel.PreferredHeight + 6;
            this.ClientSize = new Size(ClientSize.Width, ClientSize.Height + height);
            strings.Height -= height;

            _soundPreview.Bounds = new System.Drawing.Rectangle(strings.Left, strings.Bottom + 6, strings.Width, SoundPreviewPanel.PreferredHeight);
            _soundPreview.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.Controls.Add(_soundPreview);

            //The open level's own sound package - what it replaces of the base game's sounds - opposite the other buttons
            Button levelOverride = new Button();
            levelOverride.Text = "Level Sound Override...";
            levelOverride.Bounds = new System.Drawing.Rectangle(strings.Left + 10, selectBtn.Top, selectBtn.Width, selectBtn.Height);
            levelOverride.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            levelOverride.UseVisualStyleBackColor = true;
            levelOverride.Click += (s, e) => ShowLevelSoundOverride();
            new ToolTip().SetToolTip(levelOverride, "The soundbanks and streams this level reads in place of the base game's (WORLD\\" + LevelSoundOverride.FileName + ").");
            this.Controls.Add(levelOverride);

            strings.SelectedIndexChanged += SoundSelectionChanged;
            SoundPreviewLibrary.GetAsync();
        }

        private void ShowLevelSoundOverride()
        {
            string package = LevelSoundOverride.PathForOpenLevel();
            if (package == null)
            {
                MessageBox.Show(this, "Open a level first: the sound override package belongs to a level.", "Level Sound Override", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (LevelSoundOverrideEditor editor = new LevelSoundOverrideEditor(package))
                editor.ShowDialog(this);
        }

        private void SoundSelectionChanged(object sender, EventArgs e)
        {
            if (_soundPreview == null)
                return;

            _soundPreview.SetEvent(strings.SelectedItems.Count == 0 ? null : strings.SelectedItems[0].Text);
        }

        private void SelectSpecialString_Load(object sender, EventArgs e)
        {
            Search();

            int selectedIndex = -1;
            for (int i = 0; i < strings.Items.Count; i++)
            {
                strings.Items[i].Selected = false;
                if (strings.Items[i].Text == _currentText)
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
            if (_filteredItems == null)
                return; //Nothing listed for this type

            //Matched on the value and on every column beside it (a sound event's soundbanks and its text, say)
            string wanted = search_box.Text.ToUpper().Replace(" ", "");
            strings.BeginUpdate();
            strings.SuspendLayout();
            strings.Items.Clear();
            strings.Items.AddRange(_filteredItems.Where(o => o.SubItems.Cast<ListViewItem.ListViewSubItem>().Any(s => s.Text.ToUpper().Replace(" ", "").Contains(wanted))).ToArray());
            strings.EndUpdate();
        }

        private void selectBtn_Click(object sender, EventArgs e)
        {
            if (_browse || strings.SelectedItems.Count == 0)
                return;

            //TODO: if Animation, maybe we also want to update AnimationSet?

            OnSelected?.Invoke(strings.SelectedItems[0].Text);
            this.Close();
        }

        public class AssetList
        {
            public bool global = false;
            public string level = "";

            public EnumStringType type;

            public ListViewItem[] items = null;
            public bool use_desc_column = false;
        }

        private void enumStringTypeSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            PopulateItems((EnumStringType)Enum.Parse(typeof(EnumStringType), enumStringTypeSelect.Text));
        }
    }
}
