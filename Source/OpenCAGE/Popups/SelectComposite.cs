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
using CommandsEditor.Popups.Base;

namespace CommandsEditor
{
    public partial class SelectComposite : BaseWindow
    {
        public Action<Composite> OnCompositeGenerated;

        private TreeUtility _treeHelper;

        public SelectComposite(string starting = null) : base(WindowClosesOn.NEW_COMPOSITE_SELECTION | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.COMMANDS_RELOAD)
        {
            InitializeComponent();

            _treeHelper = new TreeUtility(FileTree, TreeType.SCRIPTS);
            _treeHelper.UpdateFileTree(Content.Level.Commands.GetCompositeNames().ToList());
            _treeHelper.SelectNode(starting == null || starting == "" ? Content.Level.Commands.EntryPoints[0].name : starting);

            this.Disposed += SelectComposite_Disposed;
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
