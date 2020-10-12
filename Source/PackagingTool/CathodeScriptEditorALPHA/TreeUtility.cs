using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alien_Isolation_Mod_Tools
{
    public enum TreeItemType
    {
        EXPORTABLE_FILE, //An exportable file
        LOADED_STRING, //A loaded string (WIP for COMMANDS.PAK)
        PREVIEW_ONLY_FILE, //A read-only file (export not supported yet)
        DIRECTORY //A parent directory listing
    };
    public enum TreeItemIcon
    {
        FOLDER,
        FILE,
        STRING
    };

    public struct TreeItem
    {
        public string String_Value;
        public TreeItemType Item_Type;
    }

    class TreeUtility
    {
        private TreeView FileTree;
        public TreeUtility(TreeView tree)
        {
            FileTree = tree;
        }

        /* Update the file tree GUI */
        public void UpdateFileTree(List<string> FilesToList, ContextMenuStrip contextMenu = null)
        {
            FileTree.Nodes.Clear();
            foreach (string FileName in FilesToList)
            {
                string[] FileNameParts = FileName.Split('/');
                if (FileNameParts.Length == 1) { FileNameParts = FileName.Split('\\'); }
                AddFileToTree(FileNameParts, 0, FileTree.Nodes, contextMenu);
            }
            FileTree.Sort();
        }

        /* Add a file to the GUI tree structure */
        private void AddFileToTree(string[] FileNameParts, int index, TreeNodeCollection LoopedNodeCollection, ContextMenuStrip contextMenu = null)
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
                    AddFileToTree(FileNameParts, index + 1, ThisFileNode.Nodes);
                    break;
                }
            }
            if (should)
            {
                TreeNode FileNode = new TreeNode(FileNameParts[index]);
                TreeItem ThisTag = new TreeItem();
                if (FileNameParts.Length - 1 == index)
                {
                    //Node is a file
                    for (int i = 0; i < FileNameParts.Length; i++)
                    {
                        ThisTag.String_Value += FileNameParts[i] + "/";
                    }
                    ThisTag.String_Value = ThisTag.String_Value.ToString().Substring(0, ThisTag.String_Value.ToString().Length - 1);

                    ThisTag.Item_Type = TreeItemType.EXPORTABLE_FILE;
                    FileNode.ImageIndex = (int)TreeItemIcon.FILE;
                    FileNode.SelectedImageIndex = (int)TreeItemIcon.FILE;
                    if (contextMenu != null) FileNode.ContextMenuStrip = contextMenu;
                }
                else
                {
                    //Node is a directory
                    ThisTag.Item_Type = TreeItemType.DIRECTORY;
                    FileNode.ImageIndex = (int)TreeItemIcon.FOLDER;
                    FileNode.SelectedImageIndex = (int)TreeItemIcon.FOLDER;
                    AddFileToTree(FileNameParts, index + 1, FileNode.Nodes);
                }

                FileNode.Tag = ThisTag;
                LoopedNodeCollection.Add(FileNode);
            }
        }
    }
}
