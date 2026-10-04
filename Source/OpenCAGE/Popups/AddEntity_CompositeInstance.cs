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
        //The search the tree is showing, as matched (null before the first)
        private string _currentSearch = null;

        public AddEntity_CompositeInstance(Composite composite, bool flowgraphMode) : base(WindowClosesOn.NEW_COMPOSITE_SELECTION | WindowClosesOn.COMMANDS_RELOAD)
        {
            InitializeComponent();
            StayAboveEditor = true; //small dialog - keep it above the editor window

            showPreview.Checked = SettingsManager.GetBool(Settings.CompInstShowPreview, true);
            previewSplit.Panel2Collapsed = !showPreview.Checked;

            _treeUtility = new TreeUtility(compositeTree, TreeType.SCRIPTS);
            _composite = composite;

            //The last search comes back applied, and builds the tree (all of it, without one)
            searchText.Text = SettingsManager.GetString(Settings.PreviouslySearchedCompInstType);
            Search();

            string funcToSelect = SettingsManager.GetString(Settings.PreviouslySelectedCompInstType);
            if (funcToSelect != "")
                _treeUtility.SelectNode(funcToSelect);

            addDefaultParams.Checked = SettingsManager.GetBool(Settings.PreviouslySearchedParamPopulationComp, false);

#if AUTO_POPULATE_PARAMS
            addDefaultParams.Checked = true;
            addDefaultParams.Visible = false;
            showPreview.Left = addDefaultParams.Left;
#endif

            SettingsManager.SettingsChanged += OnSettingsChanged;
            FormClosed += (s, e) => SettingsManager.SettingsChanged -= OnSettingsChanged;

            _searchTip = new ToolTip(components);
            CompositeSearchOption.ApplyHint(_searchTip, searchButton);
            CompositeSearchOption.Changed += OnCompositeSearchOptionChanged;
            FormClosed += (s, e) => CompositeSearchOption.Changed -= OnCompositeSearchOptionChanged;
        }

        private ToolTip _searchTip;

        /* Search Only Composite Names was switched: the hover text follows, and a search on show is matched again */
        private void OnCompositeSearchOptionChanged()
        {
            if (IsDisposed)
                return;

            CompositeSearchOption.ApplyHint(_searchTip, searchButton);
            if (!string.IsNullOrEmpty(_currentSearch))
                ShowSearchMatches();
        }

        private void OnSettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            if (!e.ExternalChange || IsDisposed)
                return;

            if (!SettingsChangedEventArgs.ContainsKey(e.ChangedKeys, Settings.PreviouslySearchedParamPopulationComp))
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnSettingsChanged(sender, e)));
                return;
            }

            addDefaultParams.Checked = SettingsManager.GetBool(Settings.PreviouslySearchedParamPopulationComp, false);
        }

        /* Searched from the button, or Enter in the box, not as each key is typed: every search rebuilds the
           tree, which on a big level is thousands of composites (issue 721). Emptying the box shows them all again. */
        private void searchButton_Click(object sender, EventArgs e)
        {
            Search();
        }

        private void searchText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.Handled = true;
            e.SuppressKeyPress = true; //no ding from a single-line box
            Search();
        }

        private void searchText_TextChanged(object sender, EventArgs e)
        {
            if (searchText.Text.Replace(" ", "").Length == 0)
                Search();
        }

        private void Search()
        {
            string search = searchText.Text.Replace('\\', '/').Replace(" ", "").ToUpper();
            if (search == _currentSearch)
                return;
            _currentSearch = search;

            ShowSearchMatches();

            SettingsManager.SetString(Settings.PreviouslySearchedCompInstType, searchText.Text);
        }

        private void ShowSearchMatches()
        {
            string search = _currentSearch;
            bool nameOnly = SettingsManager.GetBool(Settings.CompNameOnlyOpt);
            List<string> filteredCompositeNames = new List<string>();
            List<Composite> filteredComposites = new List<Composite>();
            for (int i = 0; i < Content.Level.Commands.Entries.Count; i++)
            {
                string name = Content.Level.Commands.Entries[i].name.Replace('\\', '/');

                if (nameOnly)
                {
                    string[] nameSplit = name.Split('/');
                    name = nameSplit[nameSplit.Length - 1];
                }

                if (!name.ToUpper().Replace(" ", "").Contains(search))
                    continue;

                filteredCompositeNames.Add(Content.Level.Commands.Entries[i].name.Replace('\\', '/'));
                filteredComposites.Add(Content.Level.Commands.Entries[i]);
            }
            _treeUtility.UpdateFileTree(filteredCompositeNames, expandAll: search != "");
            UpdatePreview();
        }

        private void clearSearchBtn_Click(object sender, EventArgs e)
        {
            searchText.Text = "";
            Search();
        }

        string _prevSelected = "";
        private void compositeTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            UpdatePreview();

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

        private void showPreview_CheckedChanged(object sender, EventArgs e)
        {
            previewSplit.Panel2Collapsed = !showPreview.Checked;
            if (SettingsManager.GetBool(Settings.CompInstShowPreview, true) != showPreview.Checked)
                SettingsManager.SetBool(Settings.CompInstShowPreview, showPreview.Checked);
            UpdatePreview();
        }

        /* The preview pane follows the tree's selection: a composite's stored preview, a folder's name, or nothing */
        private void UpdatePreview()
        {
            if (showPreview.Checked)
                compositePreview.ShowTreeNode(compositeTree.SelectedNode, Content.Level.Commands);
        }

        /* The pane makes the window wider than it used to be: on a screen too small for it, the window is shrunk to
           fit and the list gives up the room (the pane keeps its width, and Show Preview hides it) */
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            Rectangle screen = Screen.FromControl(this).WorkingArea;
            Size fitted = new Size(Math.Min(Width, screen.Width), Math.Min(Height, screen.Height));
            if (fitted == Size)
                return;
            Size = fitted;
            Location = new Point(screen.Left + (screen.Width - Width) / 2, screen.Top + (screen.Height - Height) / 2);
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

            // Check logic errors (we can't have cyclical references, including nested A→B→A)
            if (Content.Level.Commands.Utils.WouldCreateCompositeInstanceCycle(_composite, comp))
            {
                MessageBox.Show(
                    "You cannot instance a composite that already contains this composite (directly or through nested instances) — that would create an infinite loop at runtime.",
                    "Logic error!",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            //One undo step for the entity and the node a flowgraph may add for it on OnEntityAdded
            IDisposable undoGroup = OpenCAGE.Undo.UndoStack.Current.BeginGroup("Add " + entityName.Text);
            try
            {
            Singleton.OnEntityAddPending?.Invoke();

            Entity newEntity = _composite.AddFunction(comp);
            Content.Level.Commands.Utils.SetEntityName(_composite, newEntity, entityName.Text);

            if (addDefaultParams.Checked)
            {
                /* Same route the inspector takes, so a new instance carries exactly the parameters an
                 * existing one shows. Taking CathodeLib's default variants instead would write the
                 * composite's reference-style pins (OBJECT, ZONE_LINK) out as floats. */
                Content.Level.Commands.Utils.AddAllDefaultParameters(newEntity, _composite, true,
                    ParameterVariant.STATE_PARAMETER | ParameterVariant.PARAMETER);
                CompositeInstanceParameters.Ensure(newEntity, Content.Level.Commands);
                newEntity.RemoveParameter("delete_me");
            }

            Content.EditorUtils.GenerateCompositeInstances(Content.Level.Commands);

            SettingsManager.SetString(Settings.PreviouslySelectedCompInstType, item.String_Value);
            SettingsManager.SetBool(Settings.PreviouslySearchedParamPopulationComp, addDefaultParams.Checked);

            OpenCAGE.Undo.UndoStack.Current.Record(new OpenCAGE.Undo.EntityAddEdit(_composite, newEntity, "Add " + entityName.Text));
            Singleton.OnEntityAdded?.Invoke(newEntity);
            }
            finally
            {
                undoGroup.Dispose();
            }
            this.Close();
        }

        private void CreateEntityOnEnterKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                createEntity.PerformClick();
        }
    }
}
