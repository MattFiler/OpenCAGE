using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.Popups.Base;

namespace OpenCAGE
{
    public partial class SelectComposite : BaseWindow
    {
        public Action<Composite> OnCompositeGenerated;

        private TreeUtility _treeHelper;
        private string _currentSearch = "";
        private string _startingComposite;

        public SelectComposite(string starting = null) : base(WindowClosesOn.NEW_COMPOSITE_SELECTION | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.COMMANDS_RELOAD)
        {
            InitializeComponent();
            FileTree.ImageList = EditorIcons.CompositeTree;

            //The same preview as Create Composite Instance Entity's, shown or hidden by the same Show Preview setting
            showPreview.Checked = SettingsManager.GetBool(Settings.CompInstShowPreview, true);
            previewSplit.Panel2Collapsed = !showPreview.Checked;

            _startingComposite = starting == null || starting == "" ? Content.Level.Commands.EntryPoints[0].name : starting;

            _treeHelper = new TreeUtility(FileTree, TreeType.SCRIPTS);
            PopulateTree();

            //Opens ready to type a search into
            ActiveControl = searchBox;

            _searchTip = new ToolTip(components);
            CompositeSearchOption.ApplyHint(_searchTip, searchButton);
            CompositeSearchOption.Changed += OnCompositeSearchOptionChanged;

            this.Disposed += SelectComposite_Disposed;
        }

        private ToolTip _searchTip;

        /* Search Only Composite Names was switched: the hover text follows, and a search on show is matched again */
        private void OnCompositeSearchOptionChanged()
        {
            if (IsDisposed)
                return;

            CompositeSearchOption.ApplyHint(_searchTip, searchButton);
            if (_currentSearch != "")
                PopulateTree();
        }

        /* Rebuild the tree, showing only composites matching the search (all of them when it's empty) */
        private void PopulateTree()
        {
            List<string> names = Content.Level.Commands.GetCompositeNames().ToList();
            if (_currentSearch != "")
            {
                bool nameOnly = SettingsManager.GetBool(Settings.CompNameOnlyOpt);
                names = names.FindAll(o =>
                {
                    string toMatch = o.Replace('\\', '/');
                    if (nameOnly)
                    {
                        string[] split = toMatch.Split('/');
                        toMatch = split[split.Length - 1];
                    }
                    return toMatch.ToUpper().Replace(" ", "").Contains(_currentSearch);
                });
            }

            _treeHelper.UpdateFileTree(names, expandAll: _currentSearch != "");

            if (_currentSearch == "")
                _treeHelper.SelectNode(_startingComposite);
            UpdatePreview();
        }

        /* Searched from the button, or Enter in the box, not as each key is typed: every search rebuilds the
           tree, which on a big level is thousands of composites (issue 721). Emptying the box shows them all again. */
        private void searchButton_Click(object sender, EventArgs e)
        {
            ApplySearch();
        }

        private void searchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
                return;
            e.Handled = true;
            e.SuppressKeyPress = true; //no ding from a single-line box
            ApplySearch();
        }

        private void searchBox_TextChanged(object sender, EventArgs e)
        {
            if (searchBox.Text.Replace(" ", "").Length == 0)
                ApplySearch();
        }

        private void ApplySearch()
        {
            string newSearch = searchBox.Text.Replace('\\', '/').ToUpper().Replace(" ", "");
            if (newSearch == _currentSearch)
                return;

            _currentSearch = newSearch;
            PopulateTree();
        }

        private void clearSearchBtn_Click(object sender, EventArgs e)
        {
            searchBox.Text = "";
        }

        private void FileTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            UpdatePreview();
        }

        private void showPreview_CheckedChanged(object sender, EventArgs e)
        {
            previewSplit.Panel2Collapsed = !showPreview.Checked;
            if (SettingsManager.GetBool(Settings.CompInstShowPreview, true) != showPreview.Checked)
                SettingsManager.SetBool(Settings.CompInstShowPreview, showPreview.Checked);
            UpdatePreview();
        }

        /* The preview pane follows the tree's selection */
        private void UpdatePreview()
        {
            if (showPreview.Checked)
                compositePreview.ShowTreeNode(FileTree.SelectedNode, Content.Level.Commands);
        }

        /* The pane makes the window bigger than it used to be: on a screen too small for it, the window is shrunk to
           fit and the tree gives up the room (the pane keeps its width, and Show Preview hides it) */
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

        private void SelectComposite_Disposed(object sender, EventArgs e)
        {
            CompositeSearchOption.Changed -= OnCompositeSearchOptionChanged;
            _treeHelper?.ForceClearTree();
            _treeHelper = null;
        }

        private void SelectEntity_Click(object sender, EventArgs e)
        {
            if (FileTree.SelectedNode == null) return;
            if (((TreeItem)FileTree.SelectedNode.Tag).Item_Type != TreeItemType.EXPORTABLE_FILE) return;
            OnCompositeGenerated?.Invoke(Content.Level.Commands.GetComposite(((TreeItem)FileTree.SelectedNode.Tag).String_Value));
            this.Close();
        }
    }
}
