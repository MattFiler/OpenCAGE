using CATHODE;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace OpenCAGE
{
    public enum TreeItemType
    {
        EXPORTABLE_FILE, 
        DIRECTORY
    };

    public enum TreeItemIcon
    {
        FOLDER,
        FILE,
        FOLDER_OPEN
    };

    public enum TreeType
    {
        MODELS,
        SCRIPTS,
        GENERIC_FOLDER_AND_FILE,
    }

    public struct TreeItem
    {
        public string String_Value;
        public Models.CS2.Component.LOD Model_Value;
        public TreeItemType Item_Type;
    }

    class TreeUtility
    {
        protected LevelContent Content => Singleton.Editor?.CompositeBrowser?.Content;

        private TreeView _fileTree;
        private TreeType _treeType;

        public TreeUtility(TreeView tree, TreeType treeType)
        {
            _fileTree = tree;
            _treeType = treeType;

            _fileTree.AfterExpand += FileTree_AfterExpand;
            _fileTree.AfterCollapse += FileTree_AfterCollapse;
        }

        public void ForceClearTree()
        {
            if (_fileTree != null && !_fileTree.IsDisposed)
                _fileTree.Nodes.Clear();

            _fileTree = null;
        }

        /* Update the file tree GUI */
        public void UpdateFileTree(List<string> FilesToList, ContextMenuStrip contextMenu = null, List<string> tags = null, List<Models.CS2.Component.LOD> models = null)
        {
            if (_fileTree == null || _fileTree.IsDisposed)
                return;

            _fileTree.SuspendLayout();
            _fileTree.BeginUpdate();

            _fileTree.Nodes.Clear();
            for (int i = 0; i < FilesToList.Count; i++)
            {
                string[] FileNameParts = FilesToList[i].Split('/');
                if (FileNameParts.Length == 1) { FileNameParts = FilesToList[i].Split('\\'); }
                AddFileToTree(FileNameParts, 0, _fileTree.Nodes, contextMenu, (tags == null) ? "" : tags[i], models == null ? null : models[i]);
            }
            _fileTree.Sort();

            switch (_treeType)
            {
                case TreeType.MODELS:
                    SetModelNodeIcons(_fileTree.Nodes);
                    break;
            }

            _fileTree.EndUpdate();
            _fileTree.ResumeLayout();
        }
        private void SetModelNodeIcons(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Nodes.Count > 0 && node.Nodes[0].Nodes.Count == 0)
                {
                    node.ImageIndex = (int)TreeItemIcon.FILE;
                    node.SelectedImageIndex = node.ImageIndex;
                }
                SetModelNodeIcons(node.Nodes);
            }
        }

        /* Add a file to the GUI tree structure */
        private void AddFileToTree(string[] FileNameParts, int index, TreeNodeCollection LoopedNodeCollection, ContextMenuStrip contextMenu = null, string tag = "", Models.CS2.Component.LOD model = null)
        {
            if (FileNameParts.Length <= index)
            {
                return;
            }

            bool should = true;
            foreach (TreeNode ThisFileNode in LoopedNodeCollection)
            {
                if (ThisFileNode.Text == FileNameParts[index])
                {
                    should = false;
                    AddFileToTree(FileNameParts, index + 1, ThisFileNode.Nodes, contextMenu, tag, model);
                    break;
                }
            }
            if (should && FileNameParts[index] != "")
            {
                TreeNode FileNode = new TreeNode(FileNameParts[index]);
                TreeItem ThisTag = new TreeItem();
                if (FileNameParts.Length - 1 == index)
                {
                    //Node is a file
                    for (int i = 0; i < FileNameParts.Length; i++) ThisTag.String_Value += FileNameParts[i] + "/";
                    ThisTag.String_Value = tag != "" ? tag : ThisTag.String_Value.ToString().Substring(0, ThisTag.String_Value.ToString().Length - 1);
                    ThisTag.Model_Value = model;

                    switch (_treeType)
                    {
                        case TreeType.SCRIPTS:
                            Content?.EnsureEditorUtils();
                            if (Content?.EditorUtils != null)
                            {
                                EditorUtils.CompositeType type = Content.EditorUtils.GetCompositeType(ThisTag.String_Value);
                                FileNode.ImageIndex = type == EditorUtils.CompositeType.IS_GENERIC_COMPOSITE ? 1 : type == EditorUtils.CompositeType.IS_ROOT ? 3 : type == EditorUtils.CompositeType.IS_DISPLAY_MODEL ? 5 : 4;
                            }
                            else
                            {
                                FileNode.ImageIndex = 1;
                            }
                            break;
                        case TreeType.MODELS:
                        case TreeType.GENERIC_FOLDER_AND_FILE:
                            FileNode.ImageIndex = (int)TreeItemIcon.FILE;
                            break;
                    }
                    FileNode.SelectedImageIndex = FileNode.ImageIndex;

                    ThisTag.Item_Type = TreeItemType.EXPORTABLE_FILE;
                    if (contextMenu != null) FileNode.ContextMenuStrip = contextMenu;
                }
                else
                {
                    //Node is a directory
                    for (int i = 0; i < index + 1; i++) ThisTag.String_Value += FileNameParts[i] + "/";
                    ThisTag.String_Value = tag != "" ? tag : ThisTag.String_Value.ToString().Substring(0, ThisTag.String_Value.ToString().Length - 1);
                    ThisTag.Model_Value = model;

                    ThisTag.Item_Type = TreeItemType.DIRECTORY;
                    FileNode.ImageIndex = (int)TreeItemIcon.FOLDER;
                    FileNode.SelectedImageIndex = (int)TreeItemIcon.FOLDER;
                    AddFileToTree(FileNameParts, index + 1, FileNode.Nodes, contextMenu, tag, model);
                }

                FileNode.Tag = ThisTag;
                LoopedNodeCollection.Add(FileNode);
            }
        }

        /* Select a node in the tree based on the path */
        public void SelectNode(string path, bool expandPath = false)
        {
            string[] FileNameParts = path.Replace('\\', '/').Split('/');

            if (FileNameParts[FileNameParts.Length - 1] == "")
                Array.Resize(ref FileNameParts, FileNameParts.Length - 1);

            _fileTree.SelectedNode = null;

            TreeNode selectedNode = null;
            TreeNodeCollection nodeCollection = _fileTree.Nodes;
            for (int x = 0; x < FileNameParts.Length; x++)
            {
                bool found = false;
                for (int i = 0; i < nodeCollection.Count; i++)
                {
                    if (nodeCollection[i].Text != FileNameParts[x])
                        continue;

                    if (x == FileNameParts.Length - 1)
                        selectedNode = nodeCollection[i];
                    else
                        nodeCollection = nodeCollection[i].Nodes;

                    found = true;
                    break;
                }

                if (!found)
                    return;
            }

            if (selectedNode == null)
                return;

            _fileTree.SelectedNode = selectedNode;
            if (expandPath)
                ExpandToNode(selectedNode);
        }

        private void ExpandToNode(TreeNode node)
        {
            if (node == null)
                return;

            TreeNode parent = node.Parent;
            while (parent != null)
            {
                parent.Expand();
                parent = parent.Parent;
            }

            node.EnsureVisible();
        }
        public void SelectNode(Models.CS2.Component.LOD lod)
        {
            _fileTree.SelectedNode = null;
            if (lod != null)
                SelectNodeInternal(lod, _fileTree.Nodes);
        }
        public void SelectNodeInternal(Models.CS2.Component.LOD lod, TreeNodeCollection nodeCollection)
        {
            for (int i = 0; i < nodeCollection.Count; i++)
            {
                if (nodeCollection[i].Nodes.Count == 0 && ((TreeItem)nodeCollection[i].Tag).Model_Value == lod)
                {
                    _fileTree.SelectedNode = nodeCollection[i];
                    return;
                }
                SelectNodeInternal(lod, nodeCollection[i].Nodes);
            }
        }

        private void FileTree_AfterCollapse(object sender, TreeViewEventArgs e)
        {
            if (_treeType == TreeType.MODELS && e.Node.Nodes.Count > 0 && e.Node.Nodes[0].Nodes.Count == 0) return;
            if (((TreeItem)e.Node.Tag).Item_Type != TreeItemType.DIRECTORY) return;
            e.Node.ImageIndex = (int)TreeItemIcon.FOLDER;
            e.Node.SelectedImageIndex = (int)TreeItemIcon.FOLDER;
        }
        private void FileTree_AfterExpand(object sender, TreeViewEventArgs e)
        {
            if (_treeType == TreeType.MODELS && e.Node.Nodes.Count > 0 && e.Node.Nodes[0].Nodes.Count == 0) return;
            if (((TreeItem)e.Node.Tag).Item_Type != TreeItemType.DIRECTORY) return;
            e.Node.ImageIndex = (int)TreeItemIcon.FOLDER_OPEN;
            e.Node.SelectedImageIndex = (int)TreeItemIcon.FOLDER_OPEN;
        }
    }
}
