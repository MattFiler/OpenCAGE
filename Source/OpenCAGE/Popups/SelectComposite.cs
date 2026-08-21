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

            _startingComposite = starting == null || starting == "" ? Content.Level.Commands.EntryPoints[0].name : starting;

            _treeHelper = new TreeUtility(FileTree, TreeType.SCRIPTS);
            PopulateTree();

            this.Disposed += SelectComposite_Disposed;
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

            _treeHelper.UpdateFileTree(names);

            if (_currentSearch == "")
                _treeHelper.SelectNode(_startingComposite);
            else
                FileTree.ExpandAll();
        }

        private void searchBox_TextChanged(object sender, EventArgs e)
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

        private void SelectComposite_Disposed(object sender, EventArgs e)
        {
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
