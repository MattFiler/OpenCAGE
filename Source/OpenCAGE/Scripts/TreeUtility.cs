using CATHODE;
using System;
using System.Collections.Generic;
using System.Linq;
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

        //Composite ids by the path the tree spells, for the rebuild that shows previews (null when it does not)
        private Dictionary<string, CATHODE.Scripting.ShortGuid> _previewIds;

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

        /* Update the file tree GUI. With expandAll every folder comes in open, as a search's results do. */
        public void UpdateFileTree(List<string> FilesToList, ContextMenuStrip contextMenu = null, List<string> tags = null, List<Models.CS2.Component.LOD> models = null, bool expandAll = false)
        {
            if (_fileTree == null || _fileTree.IsDisposed)
                return;

            //A big level's composites take a moment to go in
            Cursor cursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            _fileTree.SuspendLayout();
            _fileTree.BeginUpdate();

            /* Composite trees swap to a taller preview list while previews are on, and back when they
               are not. The composites are looked up once here rather than by name per node. */
            _previewIds = null;
            if (_treeType == TreeType.SCRIPTS)
            {
                bool previews = CompositePreviewImages.TreesEnabled;
                CompositePreviewImages.ApplyToTree(_fileTree, previews);
                if (previews && Content?.Level?.Commands?.Entries != null)
                {
                    _previewIds = new Dictionary<string, CATHODE.Scripting.ShortGuid>(StringComparer.OrdinalIgnoreCase);
                    foreach (CATHODE.Scripting.Composite composite in Content.Level.Commands.Entries)
                    {
                        if (composite == null || string.IsNullOrEmpty(composite.name))
                            continue;
                        _previewIds[composite.name.Replace('\\', '/')] = composite.shortGUID;
                    }

                    //Every preview the nodes below will ask for, into the list as one batch (see EnsurePreviews)
                    List<CATHODE.Scripting.ShortGuid> listed = new List<CATHODE.Scripting.ShortGuid>(FilesToList.Count);
                    for (int i = 0; i < FilesToList.Count; i++)
                    {
                        string name = tags != null && i < tags.Count && tags[i] != "" ? tags[i] : FilesToList[i].Replace('\\', '/');
                        if (_previewIds.TryGetValue(name, out CATHODE.Scripting.ShortGuid id))
                            listed.Add(id);
                    }
                    CompositePreviewImages.EnsurePreviews(_fileTree.ImageList, listed, CompositePreviewImages.TreeSize);
                }
            }

            _fileTree.Nodes.Clear();

            /* Made off the view, sorted, and handed to it in one go. Built inside it, each node went into the
               view as it was found (its folder found by walking its siblings), Sort then took the whole tree
               out and put it back, and a search's ExpandAll opened every folder on screen one at a time: a
               search of a big level (HAB_AIRPORT, 2,500 composites) held the window for seconds a letter. */
            TreeNode top = new TreeNode();
            Dictionary<TreeNode, Dictionary<string, TreeNode>> named = new Dictionary<TreeNode, Dictionary<string, TreeNode>>();
            for (int i = 0; i < FilesToList.Count; i++)
            {
                string[] FileNameParts = FilesToList[i].Split('/');
                if (FileNameParts.Length == 1) { FileNameParts = FilesToList[i].Split('\\'); }
                AddFileToTree(FileNameParts, top, named, contextMenu, (tags == null) ? "" : tags[i], models == null ? null : models[i]);
            }
            SortNodes(top.Nodes, StringComparer.Create(Application.CurrentCulture, false));

            switch (_treeType)
            {
                case TreeType.MODELS:
                    SetModelNodeIcons(top.Nodes);
                    break;
            }

            //A node not in a view yet opens as the view takes it
            if (expandAll)
                ExpandFolders(top.Nodes);

            TreeNode[] roots = new TreeNode[top.Nodes.Count];
            top.Nodes.CopyTo(roots, 0);
            top.Nodes.Clear();
            _fileTree.Nodes.AddRange(roots);

            _fileTree.EndUpdate();
            _fileTree.ResumeLayout();
            Cursor.Current = cursor;
        }

        /* Each level by name, in the order TreeView.Sort gave them (the culture's comparison) */
        private static void SortNodes(TreeNodeCollection nodes, StringComparer comparer)
        {
            if (nodes.Count > 1)
            {
                TreeNode[] ordered = nodes.Cast<TreeNode>().OrderBy(o => o.Text, comparer).ToArray();
                nodes.Clear();
                nodes.AddRange(ordered);
            }
            foreach (TreeNode node in nodes)
                SortNodes(node.Nodes, comparer);
        }

        private void ExpandFolders(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Nodes.Count == 0)
                    continue;
                node.Expand();
                SetFolderIcon(node, true);
                ExpandFolders(node.Nodes);
            }
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

        /* Add a file to the GUI tree structure. named holds each node's children by text, so finding the
           folder a path goes on into is a lookup rather than a walk of its siblings. */
        private void AddFileToTree(string[] FileNameParts, TreeNode root, Dictionary<TreeNode, Dictionary<string, TreeNode>> named, ContextMenuStrip contextMenu = null, string tag = "", Models.CS2.Component.LOD model = null)
        {
            TreeNode LoopedNode = root;
            for (int index = 0; index < FileNameParts.Length; index++)
            {
                if (!named.TryGetValue(LoopedNode, out Dictionary<string, TreeNode> children))
                {
                    children = new Dictionary<string, TreeNode>(StringComparer.Ordinal);
                    named[LoopedNode] = children;
                }
                if (children.TryGetValue(FileNameParts[index], out TreeNode ThisFileNode))
                {
                    LoopedNode = ThisFileNode;
                    continue;
                }
                if (FileNameParts[index] == "")
                    return;

                TreeNode FileNode = new TreeNode(FileNameParts[index]);
                TreeItem ThisTag = new TreeItem();
                if (FileNameParts.Length - 1 == index)
                {
                    //Node is a file
                    ThisTag.String_Value = tag != "" ? tag : string.Join("/", FileNameParts);
                    ThisTag.Model_Value = model;

                    string previewKey = null;
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
                            //A composite with a preview shows it in place of the icon (the index stands for those without one)
                            if (_previewIds != null && _previewIds.TryGetValue(ThisTag.String_Value, out CATHODE.Scripting.ShortGuid compositeId))
                                previewKey = CompositePreviewImages.EnsurePreview(_fileTree.ImageList, compositeId, CompositePreviewImages.TreeSize);
                            break;
                        case TreeType.MODELS:
                        case TreeType.GENERIC_FOLDER_AND_FILE:
                            FileNode.ImageIndex = (int)TreeItemIcon.FILE;
                            break;
                    }
                    FileNode.SelectedImageIndex = FileNode.ImageIndex;
                    if (previewKey != null)
                    {
                        FileNode.ImageKey = previewKey;
                        FileNode.SelectedImageKey = previewKey;
                    }

                    ThisTag.Item_Type = TreeItemType.EXPORTABLE_FILE;
                    if (contextMenu != null) FileNode.ContextMenuStrip = contextMenu;
                }
                else
                {
                    //Node is a directory
                    ThisTag.String_Value = tag != "" ? tag : string.Join("/", FileNameParts, 0, index + 1);
                    ThisTag.Model_Value = model;

                    ThisTag.Item_Type = TreeItemType.DIRECTORY;
                    FileNode.ImageIndex = (int)TreeItemIcon.FOLDER;
                    FileNode.SelectedImageIndex = (int)TreeItemIcon.FOLDER;
                }

                FileNode.Tag = ThisTag;
                children[FileNameParts[index]] = FileNode;
                LoopedNode.Nodes.Add(FileNode);
                LoopedNode = FileNode;
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
            SetFolderIcon(e.Node, false);
        }
        private void FileTree_AfterExpand(object sender, TreeViewEventArgs e)
        {
            SetFolderIcon(e.Node, true);
        }
        private void SetFolderIcon(TreeNode node, bool open)
        {
            if (_treeType == TreeType.MODELS && node.Nodes.Count > 0 && node.Nodes[0].Nodes.Count == 0) return;
            if (((TreeItem)node.Tag).Item_Type != TreeItemType.DIRECTORY) return;
            node.ImageIndex = (int)(open ? TreeItemIcon.FOLDER_OPEN : TreeItemIcon.FOLDER);
            node.SelectedImageIndex = node.ImageIndex;
        }
    }
}
