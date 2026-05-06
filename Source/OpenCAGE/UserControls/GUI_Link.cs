using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CommandsEditor.DockPanels;
using System;
using System.Windows.Forms;

namespace CommandsEditor.UserControls
{
    public partial class GUI_Link : ParameterUserControl
    {
        public Action<Entity> GoToEntity;
        public Action<Entity, Entity> OnLinkEdited;

        private Entity _linkedEntity;

        private EntityConnector _link;
        private bool _isLinkOut;

        private EntityInspector _entityDisplay;

        public GUI_Link(EntityInspector editor) : base()
        {
            _entityDisplay = editor;
            InitializeComponent();
            this.ContextMenuStrip = contextMenuStrip1;
        }

        public void PopulateUI(EntityConnector link, bool isLinkOut, ShortGuid linkInGuid = new ShortGuid()) // linkInGuid only needs to be given if linkInGuid is false
        {
            _link = link;
            _isLinkOut = isLinkOut;

            if (isLinkOut)
            {
                _linkedEntity = _entityDisplay.Composite.GetEntityByID(link.linkedEntityID);
                group.Text = ShortGuidUtils.FindString(link.thisParamID);
                label1.Text = "Connects OUT to \"" + ShortGuidUtils.FindString(link.linkedParamID) + "\" on: ";
                this.deleteToolStripMenuItem.Text = "Delete '" + ShortGuidUtils.FindString(link.thisParamID) + "'";
            }
            else
            {
                _linkedEntity = _entityDisplay.Composite.GetEntityByID(linkInGuid);
                group.Text = ShortGuidUtils.FindString(link.linkedParamID);
                label1.Text = "Connects IN from \"" + ShortGuidUtils.FindString(link.thisParamID) + "\" on: ";
                GoTo.Image = invIconResource.Image;
                this.deleteToolStripMenuItem.Text = "Delete '" + ShortGuidUtils.FindString(link.linkedParamID) + "'";
            }

            textBox1.Text = Content.EditorUtils.GenerateEntityName(_linkedEntity, _entityDisplay.Composite);

            _hasDoneSetup = true;
        }

        private void GoTo_Click(object sender, EventArgs e)
        {
            GoToEntity?.Invoke(_linkedEntity);
        }

        private void EditLink_Click(object sender, EventArgs e)
        {
            AddOrEditLink editor;
            if (_isLinkOut)
                editor = new AddOrEditLink(_entityDisplay, _entityDisplay.Entity, _linkedEntity, ShortGuidUtils.FindString(_link.thisParamID), ShortGuidUtils.FindString(_link.linkedParamID), true, _link.ID);
            else
                editor = new AddOrEditLink(_entityDisplay, _linkedEntity, _entityDisplay.Entity, ShortGuidUtils.FindString(_link.thisParamID), ShortGuidUtils.FindString(_link.linkedParamID), false, _link.ID);

            editor.Show();
            editor.OnSaved += link_editor_OnSaved;
        }
        private void link_editor_OnSaved()
        {
            OnLinkEdited?.Invoke(_entityDisplay.Entity, _linkedEntity);
        }

        private void DeleteLink_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to remove this link?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            if (_isLinkOut)
                _entityDisplay.Entity.childLinks.RemoveAll(o => o.ID == _link.ID);
            else
                _linkedEntity.childLinks.RemoveAll(o => o.ID == _link.ID);

            OnLinkEdited?.Invoke(_entityDisplay.Entity, _linkedEntity);
        }

        public override void HighlightAsModified(bool updateDatabase = true, Control fontToUpdate = null)
        {
            base.HighlightAsModified(updateDatabase, group);
        }
    }
}
