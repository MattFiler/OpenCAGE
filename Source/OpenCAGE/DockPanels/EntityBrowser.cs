using CATHODE.Scripting;
using OpenCAGE.Popups.UserControls;
using OpenCAGE;
using OpenCAGE.UnityConnection;
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
        //A drag that ended over the viewport: where, and whether what was dragged could be dropped there
        private Point? _viewportDropPoint = null;
        private bool _viewportDropAllowed;

        public EntityBrowser()
        {
            InitializeComponent();
            Theming.ThemeManager.ApplyToForm(this);

            _split = new SplitContainer();
            _split.Dock = DockStyle.Fill;
            _split.Orientation = Orientation.Horizontal;
            _split.Panel1MinSize = 100;
            _split.Panel2MinSize = 80;
            Controls.Add(_split);

            _functionTypeList = new FunctionTypeList();
            _functionTypeList.Dock = DockStyle.Fill;
            _functionTypeList.FunctionTree.ItemDrag += Palette_TreeItemDrag;
            _functionTypeList.FunctionTree.NodeMouseDoubleClick += Palette_TreeNodeDoubleClick;
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
            if (_functionTypeList?.FunctionTree != null)
            {
                _functionTypeList.FunctionTree.ItemDrag -= Palette_TreeItemDrag;
                _functionTypeList.FunctionTree.NodeMouseDoubleClick -= Palette_TreeNodeDoubleClick;
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
                    item.ImageIndex = 2;
                    item.Tag = function.Value;
                }
                else
                {
                    item = new ListViewItem(variable.Value.ToUIString());
                    item.ImageIndex = 3;
                    item.Tag = variable.Value;
                }
                _lastUsedList.Items.Add(item);
            }
            _lastUsedList.EndUpdate();
            if (_lastUsedList.Columns.Count > 0)
                _lastUsedList.Columns[0].Width = Math.Max(120, _lastUsedList.ClientSize.Width - 90);
        }

        private void Palette_TreeItemDrag(object sender, ItemDragEventArgs e)
        {
            if (!(e.Item is TreeNode node) || node.Tag == null)
                return;

            DragFromPalette(node.Tag);
        }

        private void Palette_TreeNodeDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (_isDragging)
                return;

            if (e.Node?.Tag != null)
                CreateEntityFromTag(e.Node.Tag, null);
            else
                CreateSelectedFunctionEntity(null);
        }

        private void Palette_ItemDrag(object sender, ItemDragEventArgs e)
        {
            if (!(e.Item is ListViewItem item) || item.Tag == null)
                return;

            DragFromPalette(item.Tag);
        }

        /* A drag out of either list, by its Tag (a FunctionType or a CompositePinType; a category has none and
           never gets here). The flowgraph takes it as a DragDrop. The viewport can't: it's another process's
           window sitting over its host panel, so a drop on it can't be relied on to come back to us as a
           DragDrop event (see CompositeBrowser, whose composite drag has the same problem). Watch the cursor
           for the rest of the drag instead, and take the drop ourselves if it lands there - for a function
           with a position to put the dropped point in. Anything else was meant for the flowgraph, and a drop
           on the viewport does nothing. */
        private void DragFromPalette(object tag)
        {
            DataObject data = new DataObject();
            if (tag is FunctionType function)
            {
                data.SetData(DataFormats.UnicodeText, function.ToString());
                data.SetData(FunctionTypeDragFormat, function.ToString());
            }
            else if (tag is CompositePinType pinType)
            {
                data.SetData(DataFormats.UnicodeText, pinType.ToUIString());
                data.SetData(CompositePinTypeDragFormat, pinType.ToString());
            }
            else
            {
                return;
            }

            _viewportDropPoint = null;
            _viewportDropAllowed = ViewerFunctionDrop.CanDrop(tag);
            QueryContinueDrag += PaletteDrag_QueryContinueDrag;
            GiveFeedback += PaletteDrag_GiveFeedback;
            _isDragging = true;
            try
            {
                DoDragDrop(data, DragDropEffects.Copy);
            }
            finally
            {
                QueryContinueDrag -= PaletteDrag_QueryContinueDrag;
                GiveFeedback -= PaletteDrag_GiveFeedback;
                _isDragging = false;
            }

            if (_viewportDropPoint.HasValue)
            {
                Point droppedAt = _viewportDropPoint.Value;
                _viewportDropPoint = null;
                if (_viewportDropAllowed && tag is FunctionType droppedFunction)
                    ViewerFunctionDrop.TryDrop(droppedFunction, droppedAt);
            }
        }

        /* The drop is ours whether or not the viewport can take what's dragged: one it can't take mustn't go
           on to the viewer's window as an OLE drop either, it just does nothing. */
        private void PaletteDrag_QueryContinueDrag(object sender, QueryContinueDragEventArgs e)
        {
            if (e.EscapePressed || e.Action != DragAction.Drop)
                return;
            if (!ViewerCompositeDrop.IsCursorOverViewport())
                return;

            _viewportDropPoint = Cursor.Position;
            e.Action = DragAction.Cancel;
        }

        /* The viewer's window has no idea what we're dragging, so its feedback is meaningless - say ourselves
           whether the drop is on. */
        private void PaletteDrag_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            if (!ViewerCompositeDrop.IsCursorOverViewport())
                return;

            e.UseDefaultCursors = false;
            Cursor.Current = _viewportDropAllowed ? Cursors.Cross : Cursors.No;
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
            ListViewItem item = _functionTypeList.SelectedItem;
            if (item != null)
                CreateEntityFromTag(item.Tag, flowgraphPosition);
        }

        private void CreateEntityFromTag(object tag, PointF? flowgraphPosition)
        {
            if (tag == null)
                return;

            CompositeDisplay compositeDisplay = Singleton.Editor?.CompositeDisplay;
            if (compositeDisplay == null || !compositeDisplay.Populated)
            {
                MessageBox.Show("Please load a composite first.", "No composite loaded.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (tag is FunctionType functionType)
                compositeDisplay.CreateFunctionEntity(functionType, flowgraphPosition);
            else if (tag is CompositePinType pinType)
                compositeDisplay.CreateVariableEntity(pinType, flowgraphPosition);
        }

        private void CreateEntityFromItem(ListViewItem item, PointF? flowgraphPosition)
        {
            CreateEntityFromTag(item?.Tag, flowgraphPosition);
        }
    }
}
