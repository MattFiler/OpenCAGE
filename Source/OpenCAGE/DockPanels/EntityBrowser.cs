using CATHODE;
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

        private FunctionTypeList _functionTypeList;
        private bool _isDragging;

        public EntityBrowser()
        {
            InitializeComponent();

            _functionTypeList = new FunctionTypeList();
            _functionTypeList.Dock = DockStyle.Fill;
            _functionTypeList.FunctionTypes.ItemDrag += FunctionTypes_ItemDrag;
            _functionTypeList.FunctionTypes.MouseDoubleClick += FunctionTypes_MouseDoubleClick;
            Controls.Add(_functionTypeList);

            Singleton.OnLevelLoaded += OnLevelLoaded;
            FormClosed += EntityBrowser_FormClosed;
        }

        protected override string GetPersistString()
        {
            return "EntityBrowser";
        }

        private void EntityBrowser_FormClosed(object sender, FormClosedEventArgs e)
        {
            Singleton.OnLevelLoaded -= OnLevelLoaded;
            if (_functionTypeList?.FunctionTypes != null)
            {
                _functionTypeList.FunctionTypes.ItemDrag -= FunctionTypes_ItemDrag;
                _functionTypeList.FunctionTypes.MouseDoubleClick -= FunctionTypes_MouseDoubleClick;
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
                _functionTypeList.Setup();
        }

        private void FunctionTypes_ItemDrag(object sender, ItemDragEventArgs e)
        {
            if (!(e.Item is ListViewItem item) || string.IsNullOrEmpty(item.Text))
                return;

            DataObject data = new DataObject();
            data.SetData(DataFormats.UnicodeText, item.Text);
            data.SetData(FunctionTypeDragFormat, item.Text);

            _isDragging = true;
            DoDragDrop(data, DragDropEffects.Copy);
            _isDragging = false;
        }

        private void FunctionTypes_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (_isDragging)
                return;

            CreateSelectedFunctionEntity(null);
        }

        public void CreateSelectedFunctionEntity(PointF? flowgraphPosition)
        {
            if (_functionTypeList.SelectedItem == null)
                return;

            CompositeDisplay compositeDisplay = Singleton.Editor?.CompositeDisplay;
            if (compositeDisplay == null || !compositeDisplay.Populated)
            {
                MessageBox.Show("Please load a composite first.", "No composite loaded.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!Enum.TryParse(_functionTypeList.SelectedItem.Text, out FunctionType functionType))
                return;

            compositeDisplay.CreateFunctionEntity(functionType, flowgraphPosition);
        }
    }
}
