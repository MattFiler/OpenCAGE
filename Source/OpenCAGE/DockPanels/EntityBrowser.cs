using CATHODE.Scripting;
using OpenCAGE.Popups.UserControls;
using OpenCAGE;
using System;
using System.Drawing;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.DockPanels
{
    public partial class EntityBrowser : DockContent
    {
        public const string FunctionTypeDragFormat = "OpenCAGE.FunctionType";
        public const string CompositePinTypeDragFormat = "OpenCAGE.CompositePinType";

        private SplitContainer _split;
        private FunctionTypeList _functionTypeList;
        private Label _lastUsedLabel;
        private ListView _lastUsedList;
        private bool _isDragging;
        private bool _splitterRatioApplied;

        public EntityBrowser()
        {
            InitializeComponent();

            _split = new SplitContainer();
            _split.Dock = DockStyle.Fill;
            _split.Orientation = Orientation.Horizontal;
            _split.Panel1MinSize = 100;
            _split.Panel2MinSize = 80;
            Controls.Add(_split);

            _functionTypeList = new FunctionTypeList();
            _functionTypeList.Dock = DockStyle.Fill;
            _functionTypeList.FunctionTypes.ItemDrag += Palette_ItemDrag;
            _functionTypeList.FunctionTypes.MouseDoubleClick += Palette_MouseDoubleClick;
            _split.Panel1.Controls.Add(_functionTypeList);

            Panel lastUsedPanel = new Panel();
            lastUsedPanel.Dock = DockStyle.Fill;
            _split.Panel2.Controls.Add(lastUsedPanel);

            _lastUsedList = new ListView();
            _lastUsedList.Dock = DockStyle.Fill;
            _lastUsedList.View = View.Details;
            _lastUsedList.FullRowSelect = true;
            _lastUsedList.HideSelection = false;
            _lastUsedList.MultiSelect = false;
            _lastUsedList.HeaderStyle = ColumnHeaderStyle.None;
            _lastUsedList.SmallImageList = _functionTypeList.EntityListIcons;
            _lastUsedList.LargeImageList = _functionTypeList.EntityListIcons;
            _lastUsedList.Columns.Add("Entity", 280);
            _lastUsedList.Columns.Add("Type", 100);
            _lastUsedList.ItemDrag += Palette_ItemDrag;
            _lastUsedList.MouseDoubleClick += Palette_MouseDoubleClick;
            lastUsedPanel.Controls.Add(_lastUsedList);

            _lastUsedLabel = new Label();
            _lastUsedLabel.Text = "Last Used";
            _lastUsedLabel.Dock = DockStyle.Top;
            _lastUsedLabel.Height = 20;
            _lastUsedLabel.TextAlign = ContentAlignment.MiddleLeft;
            _lastUsedLabel.Padding = new Padding(4, 0, 0, 0);
            lastUsedPanel.Controls.Add(_lastUsedLabel);

            Load += EntityBrowser_Load;
            SizeChanged += EntityBrowser_SizeChanged;
            Singleton.OnLevelLoaded += OnLevelLoaded;
            EntityPaletteRecent.Changed += RefreshLastUsedList;
            FormClosed += EntityBrowser_FormClosed;

            RefreshLastUsedList();
        }

        private void EntityBrowser_Load(object sender, EventArgs e)
        {
            ApplySplitterRatio();
        }

        private void EntityBrowser_SizeChanged(object sender, EventArgs e)
        {
            if (!_splitterRatioApplied)
                ApplySplitterRatio();
        }

        private void ApplySplitterRatio()
        {
            if (_split == null || _split.Height <= _split.Panel1MinSize + _split.Panel2MinSize)
                return;

            int desired = (int)(_split.Height * 0.8);
            desired = Math.Max(_split.Panel1MinSize, Math.Min(desired, _split.Height - _split.Panel2MinSize));
            _split.SplitterDistance = desired;
            _splitterRatioApplied = true;
        }

        private void EntityBrowser_FormClosed(object sender, FormClosedEventArgs e)
        {
            Singleton.OnLevelLoaded -= OnLevelLoaded;
            EntityPaletteRecent.Changed -= RefreshLastUsedList;
            if (_functionTypeList?.FunctionTypes != null)
            {
                _functionTypeList.FunctionTypes.ItemDrag -= Palette_ItemDrag;
                _functionTypeList.FunctionTypes.MouseDoubleClick -= Palette_MouseDoubleClick;
            }
            if (_lastUsedList != null)
            {
                _lastUsedList.ItemDrag -= Palette_ItemDrag;
                _lastUsedList.MouseDoubleClick -= Palette_MouseDoubleClick;
            }
        }

        private void OnLevelLoaded(LevelContent content)
        {
            if (IsDisposed)
                return;

            BeginInvoke(new Action(InitializeFromLevel));
        }

        public void InitializeFromLevel()
        {
            if (Singleton.Editor?.CompositeBrowser?.Content != null)
                _functionTypeList.Setup(includeVariables: true);
        }

        private void RefreshLastUsedList()
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(RefreshLastUsedList));
                return;
            }

            _lastUsedList.BeginUpdate();
            _lastUsedList.Items.Clear();
            foreach (string entry in EntityPaletteRecent.GetEntries())
            {
                if (!EntityPaletteRecent.TryParse(entry, out FunctionType? function, out CompositePinType? variable))
                    continue;

                ListViewItem item;
                if (function.HasValue)
                {
                    item = new ListViewItem(function.Value.ToString());
                    item.SubItems.Add("Function");
                    item.ImageIndex = 1;
                    item.Tag = function.Value;
                }
                else
                {
                    item = new ListViewItem(variable.Value.ToUIString());
                    item.SubItems.Add("Variable");
                    item.ImageIndex = EditorUtils.GetImageIndexForCompositePinType(variable.Value);
                    item.Tag = variable.Value;
                }
                _lastUsedList.Items.Add(item);
            }
            _lastUsedList.EndUpdate();
            if (_lastUsedList.Columns.Count > 0)
                _lastUsedList.Columns[0].Width = Math.Max(120, _lastUsedList.ClientSize.Width - 90);
            if (_lastUsedList.Columns.Count > 1)
                _lastUsedList.Columns[1].Width = 80;
        }

        private void Palette_ItemDrag(object sender, ItemDragEventArgs e)
        {
            if (!(e.Item is ListViewItem item) || item.Tag == null)
                return;

            DataObject data = new DataObject();
            if (item.Tag is FunctionType function)
            {
                data.SetData(DataFormats.UnicodeText, function.ToString());
                data.SetData(FunctionTypeDragFormat, function.ToString());
            }
            else if (item.Tag is CompositePinType pinType)
            {
                data.SetData(DataFormats.UnicodeText, pinType.ToUIString());
                data.SetData(CompositePinTypeDragFormat, pinType.ToString());
            }
            else
            {
                return;
            }

            _isDragging = true;
            DoDragDrop(data, DragDropEffects.Copy);
            _isDragging = false;
        }

        private void Palette_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (_isDragging)
                return;

            ListView list = sender as ListView;
            if (list?.SelectedItems.Count > 0)
                CreateEntityFromItem(list.SelectedItems[0], null);
            else
                CreateSelectedFunctionEntity(null);
        }

        public void CreateSelectedFunctionEntity(PointF? flowgraphPosition)
        {
            CreateEntityFromItem(_functionTypeList.SelectedItem, flowgraphPosition);
        }

        private void CreateEntityFromItem(ListViewItem item, PointF? flowgraphPosition)
        {
            if (item?.Tag == null)
                return;

            CompositeDisplay compositeDisplay = Singleton.Editor?.CompositeDisplay;
            if (compositeDisplay == null || !compositeDisplay.Populated)
            {
                MessageBox.Show("Please load a composite first.", "No composite loaded.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (item.Tag is FunctionType functionType)
                compositeDisplay.CreateFunctionEntity(functionType, flowgraphPosition);
            else if (item.Tag is CompositePinType pinType)
                compositeDisplay.CreateVariableEntity(pinType, flowgraphPosition);
        }
    }
}
