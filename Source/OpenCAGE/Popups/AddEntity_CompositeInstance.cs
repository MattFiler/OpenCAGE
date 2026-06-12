using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;
using OpenCAGE;
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

namespace OpenCAGE
{
    public partial class AddEntity_CompositeInstance : BaseWindow
    {
        private TreeUtility _treeUtility;
        private Composite _composite;

        public AddEntity_CompositeInstance(Composite composite, bool flowgraphMode) : base(WindowClosesOn.NEW_COMPOSITE_SELECTION | WindowClosesOn.COMMANDS_RELOAD)
        {
            InitializeComponent();

            _treeUtility = new TreeUtility(compositeTree, TreeType.SCRIPTS);
            _composite = composite;

            _treeUtility.UpdateFileTree(Content.Level.Commands.GetCompositeNames().ToList());

            searchText.Text = SettingsManager.GetString(Settings.PreviouslySearchedCompInstType);
            Search();

            string funcToSelect = SettingsManager.GetString(Settings.PreviouslySelectedCompInstType);
            if (funcToSelect != "")
                _treeUtility.SelectNode(funcToSelect);

            addDefaultParams.Checked = SettingsManager.GetBool(Settings.PreviouslySearchedParamPopulationComp, false);

#if AUTO_POPULATE_PARAMS
            addDefaultParams.Checked = true;
            addDefaultParams.Visible = false;
#endif
        }

        private void searchText_TextChanged(object sender, EventArgs e)
        {
            Search();
        }

        private void Search()
        {
            List<string> filteredCompositeNames = new List<string>();
            List<Composite> filteredComposites = new List<Composite>();
            for (int i = 0; i < Content.Level.Commands.Entries.Count; i++)
            {
                string name = Content.Level.Commands.Entries[i].name.Replace('\\', '/');

                if (SettingsManager.GetBool(Settings.CompNameOnlyOpt) == true)
                {
                    string[] nameSplit = name.Split('/');
                    name = nameSplit[nameSplit.Length - 1];
                }

                if (!name.ToUpper().Replace(" ", "").Contains(searchText.Text.Replace('\\', '/').Replace(" ", "").ToUpper())) 
                    continue;

                filteredCompositeNames.Add(Content.Level.Commands.Entries[i].name.Replace('\\', '/'));
                filteredComposites.Add(Content.Level.Commands.Entries[i]);
            }
            _treeUtility.UpdateFileTree(filteredCompositeNames);

            if (searchText.Text != "")
                compositeTree.ExpandAll();

            SettingsManager.SetString(Settings.PreviouslySearchedCompInstType, searchText.Text);
        }

        private void clearSearchBtn_Click(object sender, EventArgs e)
        {
            searchText.Text = "";
            Search();
        }

        string _prevSelected = "";
        private void compositeTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (compositeTree.SelectedNode == null || compositeTree.SelectedNode.Tag == null)
            {
                compositeNameDisplay.Text = "";
                return;
            }
            compositeNameDisplay.Text = ((TreeItem)compositeTree.SelectedNode.Tag).String_Value;

            if (entityName.Text == "" || _prevSelected == entityName.Text)
            {
                entityName.Text = Path.GetFileName(((TreeItem)compositeTree.SelectedNode.Tag).String_Value);
                _prevSelected = entityName.Text;
            }
        }

        private void createEntity_Click(object sender, EventArgs e)
        {
            if (entityName.Text == "")
            {
                MessageBox.Show("Please enter an entity name!", "No name.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (compositeTree.SelectedNode == null)
            {
                MessageBox.Show("Please select a composite to instance!", "No type.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            TreeItem item = (TreeItem)compositeTree.SelectedNode.Tag;
            Composite comp = Content.Level.Commands.GetComposite(item.String_Value);
            if (item.Item_Type != TreeItemType.EXPORTABLE_FILE || comp == null)
            {
                MessageBox.Show("Failed to lookup composite.", "Invalid composite", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            //Check logic errors (we can't have cyclical references)
            if (comp == _composite /*|| GetChildInstancedComposites(_composite).Contains(_composite)*/)
            {
                MessageBox.Show("You cannot create an entity which instances the composite it is contained with - this will result in an infinite loop at runtime! Please check your logic!.", "Logic error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Singleton.OnEntityAddPending?.Invoke();

            Entity newEntity = _composite.AddFunction(comp);
            Content.Level.Commands.Utils.SetEntityName(_composite, newEntity, entityName.Text);

            if (addDefaultParams.Checked)
            {
                Content.Level.Commands.Utils.AddAllDefaultParameters(newEntity, _composite);
                newEntity.RemoveParameter("delete_me");
            }

            Content.EditorUtils.GenerateCompositeInstances(Content.Level.Commands);

            SettingsManager.SetString(Settings.PreviouslySelectedCompInstType, item.String_Value);
            SettingsManager.SetBool(Settings.PreviouslySearchedParamPopulationComp, addDefaultParams.Checked);

            Singleton.OnEntityAdded?.Invoke(newEntity);
            this.Close();
        }

        private List<Composite> GetChildInstancedComposites(Composite composite)
        {
            List<Composite> instances = new List<Composite>();
            foreach (FunctionEntity ent in composite.functions_dictionary.Values)
            {
                Composite instance = Content.Level.Commands.GetComposite(ent.function);
                if (instance == null) continue;
                instances.Add(instance);
                instances.AddRange(GetChildInstancedComposites(instance));
            }
            return instances;
        }

        private void CreateEntityOnEnterKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                createEntity.PerformClick();
        }
    }
}
