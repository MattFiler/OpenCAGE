using Alien_Isolation_Mod_Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AlienPAK
{
    /*
     * 
     * This is a modified version of AlienPAK, which can be found on its own repo as a standalone tool:
     * https://github.com/MattFiler/AlienPAK
     * 
     * While this form and all subseqent classes are largely unchanged, some modifications have been made.
     * Any modifications are labelled with an OPENCAGE marker.
     * 
    */

    public partial class AlienPAK_Imported : Form
    {
        List<PAK> AlienPAKs = new List<PAK>();
        ErrorMessages AlienErrors = new ErrorMessages();

        public AlienPAK_Imported(string[] args, AlienPAK_Wrapper.AlienContentType LaunchAs)
        {
            InitializeComponent();
            
            //Link image list to GUI elements for icons
            FileTree.ImageList = imageList1;

            /* ADDITIONS FOR OPENCAGE */
            AlienModToolsAdditions(args, LaunchAs);
        }

        /* Open a PAK and populate the GUI */
        private void OpenFileAndPopulateGUI(string filename)
        {
            //Open PAK
            Cursor.Current = Cursors.WaitCursor;
            AlienPAKs.Clear();
            AlienPAKs.Add(new PAK());
            AlienPAKs[0].Open(filename);

            //Parse the PAK's file list
            List<string> ParsedFiles = new List<string>();
            ParsedFiles = AlienPAKs[0].Parse();
            if (ParsedFiles == null)
            {
                Cursor.Current = Cursors.Default;
                MessageBox.Show("The selected PAK is currently unsupported.", "Unsupported", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            //Populate the GUI with the files found within the archive
            UpdateFileTree(ParsedFiles);

            //Update title
            this.Text = "Alien: Isolation PAK Tool - " + Path.GetFileName(filename);
            Cursor.Current = Cursors.Default;

            //Show/hide extended archive support if appropriate
            if (AlienPAKs[0].Format == PAKType.PAK2)
            {
                groupBox3.Show();
                return;
            }
            groupBox3.Hide();
        }

        /* Update the file tree GUI */
        private void UpdateFileTree(List<string> FilesToList)
        {
            FileTree.Nodes.Clear();
            foreach (string FileName in FilesToList)
            {
                string[] FileNameParts = FileName.Split('/');
                if (FileNameParts.Length == 1) { FileNameParts = FileName.Split('\\'); }
                AddFileToTree(FileNameParts, 0, FileTree.Nodes);
            }
            UpdateSelectedFilePreview();
            FileTree.Sort();
        }


        /* Add a file to the GUI tree structure */
        private void AddFileToTree(string[] FileNameParts, int index, TreeNodeCollection LoopedNodeCollection)
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
                    FileNode.ContextMenuStrip = fileContextMenu;
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

        /* Get type description based on extension */
        private string GetFileTypeDescription(string FileExtension)
        {
            if (FileExtension == "")
            {
                /*
                if (AlienPAK.Format == PAKType.PAK_SCRIPTS)
                {
                    return "Cathode Script";
                }
                */
                return "Unknown Type";
            }
            switch (FileExtension.Substring(1).ToUpper())
            {
                case "DDS":
                    return "DDS (Image)";
                case "TGA":
                    return "TGA (Image)";
                case "PNG":
                    return "PNG (Image)";
                case "JPG":
                    return "JPG (Image)";
                case "GFX":
                    return "GFX (Adobe Flash)";
                case "CS2":
                    return "CS2 (Model)";
                case "BIN":
                    return "BIN (Binary File)";
                case "BML":
                    return "BML (Binary XML)";
                case "XML":
                    return "XML (Markup)";
                case "TXT":
                    return "TXT (Text)";
                default:
                    return FileExtension.Substring(1).ToUpper();
            }
        }

        /* Update file preview */
        private void UpdateSelectedFilePreview()
        {
            //First, reset the GUI
            filePreviewImage.Image = null;
            fileNameInfo.Text = "";
            fileSizeInfo.Text = "";
            fileTypeInfo.Text = "";
            exportFile.Enabled = false;
            importFile.Enabled = false;
            removeFile.Enabled = false;
            addFile.Enabled = true; //Eventually move this to only be enabled on directory selection

            //Exit early if nothing selected
            if (FileTree.SelectedNode == null)
            {
                return;
            }

            //Handle file selection
            if (((TreeItem)FileTree.SelectedNode.Tag).Item_Type == TreeItemType.EXPORTABLE_FILE)
            {
                string FileName = ((TreeItem)FileTree.SelectedNode.Tag).String_Value;

                //Populate filename/type info
                fileNameInfo.Text = Path.GetFileName(FileName);
                fileTypeInfo.Text = GetFileTypeDescription(Path.GetExtension(FileName));

                //Populate file size info
                int FileSize = -1;
                foreach (PAK thisPAK in AlienPAKs) if (FileSize == -1) FileSize = thisPAK.GetFileSize(FileName);
                if (FileSize == -1) { return; }
                fileSizeInfo.Text = FileSize.ToString() + " bytes";

                //Enable buttons
                exportFile.Enabled = true;
                importFile.Enabled = true;
                removeFile.Enabled = true;
            }
        }

        /* Import a file to replace the selected PAK entry */
        private void ImportSelectedFile()
        {
            if (FileTree.SelectedNode == null || ((TreeItem)FileTree.SelectedNode.Tag).Item_Type != TreeItemType.EXPORTABLE_FILE)
            {
                MessageBox.Show("Please select a file from the list.", "No file selected.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            //Allow selection of a file (force extension), then drop it in
            OpenFileDialog FilePicker = new OpenFileDialog();
            FilePicker.Filter = "Import File|*" + Path.GetExtension(FileTree.SelectedNode.Text);
            if (FilePicker.ShowDialog() == DialogResult.OK)
            {
                Cursor.Current = Cursors.WaitCursor;
                foreach (PAK thisPAK in AlienPAKs) thisPAK.ImportFile(((TreeItem)FileTree.SelectedNode.Tag).String_Value, FilePicker.FileName);
                MessageBox.Show(AlienErrors.ErrorMessageBody(PAKReturnType.SUCCESS), AlienErrors.ErrorMessageTitle(PAKReturnType.SUCCESS), MessageBoxButtons.OK, MessageBoxIcon.Information); //Forcing success message for OpenCAGE
                Cursor.Current = Cursors.Default;
            }
            UpdateSelectedFilePreview();
        }

        /* Export the selected PAK entry as a standalone file */
        private void ExportSelectedFile()
        {
            if (FileTree.SelectedNode == null || ((TreeItem)FileTree.SelectedNode.Tag).Item_Type != TreeItemType.EXPORTABLE_FILE)
            {
                MessageBox.Show("Please select a file from the list.", "No file selected.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            //Let the user decide where to save, then save
            SaveFileDialog FilePicker = new SaveFileDialog();
            FilePicker.Filter = "Exported File|*" + Path.GetExtension(FileTree.SelectedNode.Text);
            FilePicker.FileName = Path.GetFileName(FileTree.SelectedNode.Text);
            if (FilePicker.ShowDialog() == DialogResult.OK)
            {
                Cursor.Current = Cursors.WaitCursor;
                foreach (PAK thisPAK in AlienPAKs) thisPAK.ExportFile(((TreeItem)FileTree.SelectedNode.Tag).String_Value, FilePicker.FileName);
                MessageBox.Show(AlienErrors.ErrorMessageBody(PAKReturnType.SUCCESS), AlienErrors.ErrorMessageTitle(PAKReturnType.SUCCESS), MessageBoxButtons.OK, MessageBoxIcon.Information); //Forcing success message for OpenCAGE
                Cursor.Current = Cursors.Default;
            }
        }

        /* Add file to the loaded archive */
        private void AddFileToArchive_Click(object sender, EventArgs e)
        {
            /* This can only happen for UI files, so for OpenCAGE I'm forcing AlienPAKs[0] - might need changing for other PAKs that gain support */

            //Let the user decide what file to add, then add it
            OpenFileDialog FilePicker = new OpenFileDialog();
            FilePicker.Filter = "Any File|*.*";
            if (FilePicker.ShowDialog() == DialogResult.OK)
            {
                Cursor.Current = Cursors.WaitCursor;
                PAKReturnType ResponseCode = AlienPAKs[0].AddNewFile(FilePicker.FileName);
                MessageBox.Show(AlienErrors.ErrorMessageBody(ResponseCode), AlienErrors.ErrorMessageTitle(ResponseCode), MessageBoxButtons.OK, MessageBoxIcon.Information);
                Cursor.Current = Cursors.Default;
            }
            //This is an expensive call for any PAK except PAK2, as it uses the new system.
            //We only can call with PAK2 here so it's fine, but worth noting.
            UpdateFileTree(AlienPAKs[0].Parse());
        }

        /* Remove selected file from the archive */
        private void RemoveFileFromArchive_Click(object sender, EventArgs e)
        {
            /* This can only happen for UI files, so for OpenCAGE I'm forcing AlienPAKs[0] - might need changing for other PAKs that gain support */

            if (FileTree.SelectedNode == null || ((TreeItem)FileTree.SelectedNode.Tag).Item_Type != TreeItemType.EXPORTABLE_FILE)
            {
                MessageBox.Show("Please select a file from the list.", "No file selected.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            DialogResult ConfirmRemoval = MessageBox.Show("Are you sure you would like to remove this file?", "About to remove selected file...", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ConfirmRemoval == DialogResult.Yes)
            {
                Cursor.Current = Cursors.WaitCursor;
                PAKReturnType ResponseCode = AlienPAKs[0].RemoveFile(((TreeItem)FileTree.SelectedNode.Tag).String_Value);
                MessageBox.Show(AlienErrors.ErrorMessageBody(ResponseCode), AlienErrors.ErrorMessageTitle(ResponseCode), MessageBoxButtons.OK, MessageBoxIcon.Information);
                Cursor.Current = Cursors.Default;
            }
            //This is an expensive call for any PAK except PAK2, as it uses the new system.
            //We only can call with PAK2 here so it's fine, but worth noting.
            UpdateFileTree(AlienPAKs[0].Parse());
        }

        /* Form loads */
        private void Form1_Load(object sender, EventArgs e)
        {
            //For testing purposes
            //OpenFileAndPopulateGUI(@"E:\Program Files\Steam\steamapps\common\Alien Isolation\DATA\UI.PAK");
        }

        /* User requests to open a PAK */
        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Allow selection of a PAK from filepicker, then open
            OpenFileDialog filePicker = new OpenFileDialog();
            filePicker.Filter = "Alien: Isolation PAK|*.PAK";
            if (filePicker.ShowDialog() == DialogResult.OK)
            {
                OpenFileAndPopulateGUI(filePicker.FileName);
            }
        }

        /* Expand/collapse all nodes in the tree */
        private void expandAllDirectoriesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FileTree.ExpandAll();
        }
        private void shrinkAllDirectoriesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FileTree.CollapseAll();
        }

        /* Import/export selected file (main menu) */
        private void importFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ImportSelectedFile();
        }
        private void exportFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportSelectedFile();
        }

        /* Import/export selected file (context menu) */
        private void importFileContext_Click(object sender, EventArgs e)
        {
            ImportSelectedFile();
        }
        private void exportFileContext_Click(object sender, EventArgs e)
        {
            ExportSelectedFile();
        }

        /* Import/export selected file (gui buttons) */
        private void importFile_Click(object sender, EventArgs e)
        {
            ImportSelectedFile();
        }
        private void exportFile_Click(object sender, EventArgs e)
        {
            ExportSelectedFile();
        }

        /* Item selected (show preview info) */
        private void FileTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            UpdateSelectedFilePreview();
        }

        /* ADDITIONS FOR OPENCAGE BELOW */
        ToolPaths Paths = new ToolPaths();
        AlienPAK_Wrapper.AlienContentType LaunchMode;

        //Run on init
        private void AlienModToolsAdditions(string[] args, AlienPAK_Wrapper.AlienContentType LaunchAs)
        {
            LaunchMode = LaunchAs;
            this.Text = "OpenCAGE Content Editor";

            //Populate the form with the UI.PAK if launched as so, and exit early
            if (LaunchAs == AlienPAK_Wrapper.AlienContentType.UI)
            {
                OpenFileAndPopulateGUI(Paths.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/UI.PAK");
                this.Text = "OpenCAGE Content Editor - UI.PAK";
                return;
            }

            //If launching into an unknown file type, hide appropriate GUI elements
            if (LaunchAs == AlienPAK_Wrapper.AlienContentType.UNKNOWN)
            {
                MessageBox.Show("This PAK file is currently unsupported.", "Unsupported PAK", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
                return; //Just to be sure!
            }

            //Work out what file to use from our launch type
            string levelFileToUse = "";
            string globalFileToUse = "";
            switch (LaunchMode)
            {
                case AlienPAK_Wrapper.AlienContentType.MODEL:
                    levelFileToUse = "LEVEL_MODELS.PAK";
                    globalFileToUse = "GLOBAL_MODELS.PAK";
                    break;
                case AlienPAK_Wrapper.AlienContentType.TEXTURE:
                    levelFileToUse = "LEVEL_TEXTURES.ALL.PAK";
                    globalFileToUse = "GLOBAL_TEXTURES.ALL.PAK";
                    break;
            }

            //Load the files for all levels
            Cursor.Current = Cursors.WaitCursor;
            List<string> levelTexturePAKs = Directory.GetFiles(Paths.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/ENV/PRODUCTION/", levelFileToUse, SearchOption.AllDirectories).ToList<string>();
            levelTexturePAKs.Add(Paths.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/ENV/GLOBAL/WORLD/" + globalFileToUse);
            List<string> parsedFiles = new List<string>();
            foreach (string levelTexturePAK in levelTexturePAKs)
            {
                PAK thisPAK = new PAK();
                thisPAK.Open(levelTexturePAK);
                List<string> theseFiles = thisPAK.Parse();
                foreach (string thisPAKEntry in theseFiles)
                {
                    if (!parsedFiles.Contains(thisPAKEntry)) parsedFiles.Add(thisPAKEntry);
                }
                AlienPAKs.Add(thisPAK);
            }
            UpdateFileTree(parsedFiles);
            Cursor.Current = Cursors.Default;
            groupBox3.Hide();
        }
    }
}
