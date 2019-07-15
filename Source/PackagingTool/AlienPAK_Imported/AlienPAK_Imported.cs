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
    public partial class AlienPAK_Imported : Form
    {
        PAK AlienPAK = new PAK();

        public AlienPAK_Imported(string[] args, AlienPAK_Wrapper.AlienContentType LaunchAs) //LaunchAs added for Alien Mod Tools port
        {
            InitializeComponent();

            //Support "open with" from Windows on PAK files
            if (args.Length > 0 && File.Exists(args[0]))
            {
                OpenFileAndPopulateGUI(args[0]);
            }

            //Link image list to GUI elements for icons
            FileTree.ImageList = imageList1;

            /* ADDITIONS FOR ALIEN ISOLATION MOD TOOLS */
            AlienModToolsAdditions(args, LaunchAs);
        }

        /* Open a PAK and populate the GUI */
        private void OpenFileAndPopulateGUI(string filename)
        {
            //Open PAK
            AlienPAK.Open(filename);

            //Parse the PAK's file list
            List<string> ParsedFiles = new List<string>();
            ParsedFiles = AlienPAK.Parse();
            if (ParsedFiles == null)
            {
                MessageBox.Show("The selected PAK is currently unsupported.", "Unsupported", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            //Populate the GUI with the files found within the archive
            FileTree.Nodes.Clear();
            foreach (string FileName in ParsedFiles)
            {
                string[] FileNameParts = FileName.Split('/');
                if (FileNameParts.Length == 1) { FileNameParts = FileName.Split('\\'); }
                AddFileToTree(FileNameParts, 0, FileTree.Nodes);
            }
            UpdateSelectedFilePreview();

            //Update title
            this.Text = "Alien: Isolation PAK Tool - " + Path.GetFileName(filename);
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
            switch (FileExtension.Substring(1).ToUpper())
            {
                case "DDS":
                    return "DDS (Image)";
                case "TGA":
                    return "TGA (Image)";
                case "GFX":
                    return "GFX (Adobe Flash)";
                case "CS2":
                    return "CS2 (Model)";
                case "BIN":
                    return "BIN (Binary File)";
                case "BML":
                    return "BML (Binary XML)";
            }
            return "";
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

            //Exit early if not a file
            if (FileTree.SelectedNode == null || ((TreeItem)FileTree.SelectedNode.Tag).Item_Type != TreeItemType.EXPORTABLE_FILE)
            {
                return;
            }
            string FileName = ((TreeItem)FileTree.SelectedNode.Tag).String_Value;

            //Populate filename/type info
            fileNameInfo.Text = Path.GetFileName(FileName);
            fileTypeInfo.Text = GetFileTypeDescription(Path.GetExtension(FileName));

            //Populate file size info
            int FileSize = AlienPAK.GetFileSize(FileName);
            if (FileSize == -1) { return; }
            fileSizeInfo.Text = FileSize.ToString() + " bytes";

            //Enable buttons
            exportFile.Enabled = true;
            importFile.Enabled = true;
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
            OpenFileDialog filePicker = new OpenFileDialog();
            filePicker.Filter = "Import File|*" + Path.GetExtension(FileTree.SelectedNode.Text);
            if (filePicker.ShowDialog() == DialogResult.OK)
            {
                Cursor.Current = Cursors.WaitCursor;
                switch (AlienPAK.ImportFile(((TreeItem)FileTree.SelectedNode.Tag).String_Value, filePicker.FileName))
                {
                    case PAK.PAKReturnType.SUCCESS:
                        MessageBox.Show("The selected file was imported successfully.", "Imported file", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                    case PAK.PAKReturnType.FAILED_UNSUPPORTED:
                        MessageBox.Show("An error occurred while importing the selected file.\nThe texture you are trying to replace is currently unsupported.", "An error occurred", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                    case PAK.PAKReturnType.FAILED_UNKNOWN:
                        MessageBox.Show("An unknown error occurred while importing the selected file.\nA further popup will appear with detailed information.\nPlease log this information via the GitHub issue tracker.", "An error occurred", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        MessageBox.Show(AlienPAK.LatestError, "Unknown Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                    case PAK.PAKReturnType.FAILED_LOGIC_ERROR:
                        MessageBox.Show("An error occurred while importing the selected file.\nPlease reload the PAK file.", "An error occurred", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                    case PAK.PAKReturnType.FAILED_FILE_IN_USE:
                        MessageBox.Show("An error occurred while importing the selected file.\nIf Alien: Isolation is open, it must be closed.", "An error occurred", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                }
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
            SaveFileDialog filePicker = new SaveFileDialog();
            filePicker.Filter = "Exported File|*" + Path.GetExtension(FileTree.SelectedNode.Text);
            filePicker.FileName = Path.GetFileName(FileTree.SelectedNode.Text);
            if (filePicker.ShowDialog() == DialogResult.OK)
            {
                Cursor.Current = Cursors.WaitCursor;
                switch (AlienPAK.ExportFile(((TreeItem)FileTree.SelectedNode.Tag).String_Value, filePicker.FileName))
                {
                    case PAK.PAKReturnType.SUCCESS:
                        MessageBox.Show("The selected file was exported successfully.", "Exported file", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        break;
                    case PAK.PAKReturnType.FAILED_UNSUPPORTED:
                        MessageBox.Show("An error occurred while exporting the selected file.\nThis file is currently unsupported.", "An error occurred", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                    case PAK.PAKReturnType.FAILED_UNKNOWN:
                        MessageBox.Show("An unknown error occurred while exporting the selected file.\nA further popup will appear with detailed information.\nPlease log this information via the GitHub issue tracker.", "An error occurred", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        MessageBox.Show(AlienPAK.LatestError, "Unknown Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                    case PAK.PAKReturnType.FAILED_LOGIC_ERROR:
                        MessageBox.Show("An error occurred while exporting the selected file.\nPlease reload the PAK file.", "An error occurred", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                    case PAK.PAKReturnType.FAILED_FILE_IN_USE:
                        MessageBox.Show("An error occurred while exporting the selected file.\nMake sure you have write access to the export folder.", "An error occurred", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                }
                Cursor.Current = Cursors.Default;
            }
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

        /* ADDITIONS FOR ALIEN ISOLATION MOD TOOLS BELOW */
        Directories AlienDirectories = new Directories();
        AlienPAK_Wrapper.AlienContentType LaunchMode;
        string ModeFileName = "";

        //Run on init
        private void AlienModToolsAdditions(string[] args, AlienPAK_Wrapper.AlienContentType LaunchAs)
        {
            LaunchMode = LaunchAs;

            //Populate the form with the UI.PAK if launched as so, and exit early
            if (LaunchAs == AlienPAK_Wrapper.AlienContentType.UI)
            {
                OpenFileAndPopulateGUI(AlienDirectories.GameDirectoryRoot() + "/DATA/UI.PAK");
                groupBox4.Hide();
                return;
            }

            //We're loading into map-specific mode, populate GUI and work out what file name to use
            ModeSpecificFileName();
            PopulateMapDropdown();

            //If launched with args, adjust accordingly - else default to index 0 in dropdown
            if (args.Length == 0 || !File.Exists(args[0]))
            {
                mapToLoadContentFrom.SelectedIndex = 0;
            }
            else
            {
                try
                {
                    //Get map name and select it
                    int CutOutStartLength = (AlienDirectories.GameDirectoryRoot() + "/DATA/ENV/PRODUCTION/").Length;
                    int CutOutEndLength = ("/RENDERABLE/" + ModeFileName).Length;
                    string LoadedMap = args[0].Substring(CutOutStartLength, args[0].Length - CutOutStartLength - CutOutEndLength).ToUpper();
                    mapToLoadContentFrom.SelectedItem = LoadedMap;
                }
                catch
                {
                    MessageBox.Show("Failed to load the requested file.", "An error occured.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    mapToLoadContentFrom.SelectedIndex = 0;
                }
            }
        }

        //Populate dropdown for all available maps
        private void PopulateMapDropdown()
        {
            mapToLoadContentFrom.Items.Add("BSP_LV426_PT01");
            mapToLoadContentFrom.Items.Add("BSP_LV426_PT02");
            mapToLoadContentFrom.Items.Add("BSP_TORRENS");
            AddIfHasDLC("DLC/BSPNOSTROMO_RIPLEY");
            AddIfHasDLC("DLC/BSPNOSTROMO_TWOTEAMS");
            AddIfHasDLC("DLC/CHALLENGEMAP1");
            AddIfHasDLC("DLC/CHALLENGEMAP3");
            AddIfHasDLC("DLC/CHALLENGEMAP4");
            AddIfHasDLC("DLC/CHALLENGEMAP5");
            AddIfHasDLC("DLC/CHALLENGEMAP7");
            AddIfHasDLC("DLC/CHALLENGEMAP9");
            AddIfHasDLC("DLC/CHALLENGEMAP11");
            AddIfHasDLC("DLC/CHALLENGEMAP12");
            AddIfHasDLC("DLC/CHALLENGEMAP14");
            AddIfHasDLC("DLC/CHALLENGEMAP16");
            AddIfHasDLC("DLC/SALVAGEMODE1");
            AddIfHasDLC("DLC/SALVAGEMODE2");
            mapToLoadContentFrom.Items.Add("ENG_ALIEN_NEST");
            mapToLoadContentFrom.Items.Add("ENG_REACTORCORE");
            mapToLoadContentFrom.Items.Add("ENG_TOWPLATFORM");
            mapToLoadContentFrom.Items.Add("FRONTEND");
            mapToLoadContentFrom.Items.Add("HAB_AIRPORT");
            mapToLoadContentFrom.Items.Add("HAB_CORPORATEPENT");
            mapToLoadContentFrom.Items.Add("HAB_SHOPPINGCENTRE");
            mapToLoadContentFrom.Items.Add("SCI_ANDROIDLAB");
            mapToLoadContentFrom.Items.Add("SCI_HOSPITALLOWER");
            mapToLoadContentFrom.Items.Add("SCI_HOSPITALUPPER");
            mapToLoadContentFrom.Items.Add("SCI_HUB");
            mapToLoadContentFrom.Items.Add("SOLACE");
            mapToLoadContentFrom.Items.Add("TECH_COMMS");
            mapToLoadContentFrom.Items.Add("TECH_HUB");
            mapToLoadContentFrom.Items.Add("TECH_MUTHRCORE");
            mapToLoadContentFrom.Items.Add("TECH_RND");
            mapToLoadContentFrom.Items.Add("TECH_RND_HZDLAB");
        }
        private void AddIfHasDLC(string MapName)
        {
            if(File.Exists(AlienDirectories.GameDirectoryRoot() + "/DATA/ENV/PRODUCTION/" + MapName + "/WORLD/COMMANDS.PAK"))
            {
                mapToLoadContentFrom.Items.Add(MapName);
            }
        }

        private void ModeSpecificFileName()
        {
            switch (LaunchMode)
            {
                case AlienPAK_Wrapper.AlienContentType.MODEL:
                    ModeFileName = "LEVEL_MODELS.PAK";
                    break;
                case AlienPAK_Wrapper.AlienContentType.TEXTURE:
                    ModeFileName = "LEVEL_TEXTURES.ALL.PAK";
                    break;
            }
        }

        //Load a PAK dependant on selection
        private void mapToLoadContentFrom_SelectedIndexChanged(object sender, EventArgs e)
        {
            OpenFileAndPopulateGUI(AlienDirectories.GameDirectoryRoot() + "/DATA/ENV/PRODUCTION/" + mapToLoadContentFrom.Text + "/RENDERABLE/" + ModeFileName);
        }
    }
}
