using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.Popups.UserControls;
using OpenCAGE.Properties;
using OpenCAGE.UserControls;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.DockPanels
{
    public partial class EntityList : DockContent
    {
        public const string EntityDragFormat = "OpenCAGE.EntityListEntity";

        protected LevelContent Content => Singleton.Editor?.CompositeBrowser?.Content;

        public CompositeEntityList List => compositeEntityList1;

        public EntityList()
        {
            InitializeComponent();

            compositeEntityList1.ContextMenuStrip = EntityListContextMenu;

            compositeEntityList1.SelectedEntityChanged += OnEntitySelected;
            this.FormClosed += EntityList_FormClosed;

            this.CloseButtonVisible = false;
            this.AllowEndUserDocking = false;
        }

        public void UpdateTitle()
        {
            Composite composite = Singleton.Editor?.CompositeDisplay?.Composite;
            if (composite == null)
            {
                Text = "Entities";
                return;
            }

            string compositeName = EditorUtils.GetCompositeName(composite);
            if (string.IsNullOrEmpty(compositeName))
                compositeName = "(Root)";

            Text = compositeName + " Entities";
        }

        public void FocusPanel()
        {
            if (IsDisposed || Singleton.Editor?.DockPanel == null)
                return;

            if (Pane != null && DockState != DockState.Hidden && DockState != DockState.Float)
                Activate();
            else
                Show(Singleton.Editor.DockPanel, DockState.DockLeft);

            compositeEntityList1.Focus();
        }

        private void EntityList_FormClosed(object sender, FormClosedEventArgs e)
        {
            this.FormClosed -= EntityList_FormClosed;
            if (_entityRenameDialog != null)
                _entityRenameDialog.FormClosed -= _entityRenameDialog_FormClosed;
        }

        private void OnEntitySelected(Entity entity)
        {
            Singleton.OnEntitySelected?.Invoke(entity);
        }

        //disable entity-related actions on the context menu if no entity is selected
        private void EntityListContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool hasSelectedEntity = compositeEntityList1.SelectedEntity != null;

            deleteToolStripMenuItem.Enabled = hasSelectedEntity;
            renameToolStripMenuItem.Enabled = hasSelectedEntity && compositeEntityList1.SelectedEntity.variant != EntityVariant.ALIAS && compositeEntityList1.SelectedEntity.variant != EntityVariant.VARIABLE;
            duplicateToolStripMenuItem.Enabled = hasSelectedEntity && compositeEntityList1.SelectedEntity.variant != EntityVariant.ALIAS && compositeEntityList1.SelectedEntity.variant != EntityVariant.VARIABLE;
            findReferencesToolStripMenuItem.Enabled = hasSelectedEntity && compositeEntityList1.SelectedEntity.variant != EntityVariant.ALIAS && compositeEntityList1.SelectedEntity.variant != EntityVariant.VARIABLE;
        }

        //Temporarily hijacked these options here: they should be handled in CompositeDisplay really...
        private void createParameterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Singleton.Editor.CompositeDisplay.CreateEntity(EntityVariant.VARIABLE);
        }
        private void createFunctionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Singleton.Editor.CompositeDisplay.CreateEntity(EntityVariant.FUNCTION);
        }
        private void createInstanceOfCompositeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Singleton.Editor.CompositeDisplay.CreateEntity(EntityVariant.FUNCTION, true);
        }
        private void createProxyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Singleton.Editor.CompositeDisplay.CreateEntity(EntityVariant.PROXY);
        }
        private void createAliasToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Singleton.Editor.CompositeDisplay.CreateEntity(EntityVariant.ALIAS);
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Singleton.Editor.CompositeDisplay.DeleteEntity(List.SelectedEntity);
        }
        RenameEntity _entityRenameDialog = null;
        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_entityRenameDialog != null)
                _entityRenameDialog.Close();

            _entityRenameDialog = new RenameEntity(List.SelectedEntity, Singleton.Editor.CompositeDisplay.Composite);
            _entityRenameDialog.Show();
            _entityRenameDialog.FormClosed += _entityRenameDialog_FormClosed;
        }
        private void _entityRenameDialog_FormClosed(object sender, FormClosedEventArgs e)
        {
            _entityRenameDialog = null;
        }
        private void duplicateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Singleton.Editor.CompositeDisplay.DuplicateEntity(List.SelectedEntity);
        }

        private void deleteCheckedEntities_Click(object sender, EventArgs e)
        {
            Singleton.Editor?.CompositeDisplay?.DeleteCheckedEntities();
        }

        ShowCrossRefs _crossRefsDialog = null;
        private void findReferencesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_crossRefsDialog != null)
                _crossRefsDialog.Close();

            _crossRefsDialog = new ShowCrossRefs(List.SelectedEntity);
            _crossRefsDialog.Show();
            _crossRefsDialog.OnEntitySelected += Singleton.Editor.CompositeBrowser.LoadCompositeAndEntity;
            _crossRefsDialog.OnFlowgraphSelected += Singleton.Editor.CompositeDisplay.SelectEntityOnFlowgraph;
        }
    }
}
