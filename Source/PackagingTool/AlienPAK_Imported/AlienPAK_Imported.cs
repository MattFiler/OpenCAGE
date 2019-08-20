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
        ErrorMessages AlienErrors = new ErrorMessages();

        public AlienPAK_Imported(string[] args, AlienPAK_Wrapper.AlienContentType LaunchAs) //LaunchAs added for Alien Mod Tools port
        {
            InitializeComponent();
            
            //Link image list to GUI elements for icons
            FileTree.ImageList = imageList1;

            /* ADDITIONS FOR ALIEN ISOLATION MOD TOOLS */
            AlienModToolsAdditions(args, LaunchAs);
        }

        /* Open a PAK and populate the GUI */
        private void OpenFileAndPopulateGUI(string filename)
        {
            //Open PAK
            Cursor.Current = Cursors.WaitCursor;
            AlienPAK.Open(filename);

            //Parse the PAK's file list
            List<string> ParsedFiles = new List<string>();
            ParsedFiles = AlienPAK.Parse();
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
            if (AlienPAK.Format == PAKType.PAK2)
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
                if (AlienPAK.Format == PAKType.PAK_SCRIPTS)
                {
                    return "Cathode Script";
                }
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
                int FileSize = AlienPAK.GetFileSize(FileName);
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
                PAKReturnType ResponseCode = AlienPAK.ImportFile(((TreeItem)FileTree.SelectedNode.Tag).String_Value, FilePicker.FileName);
                MessageBox.Show(AlienErrors.ErrorMessageBody(ResponseCode), AlienErrors.ErrorMessageTitle(ResponseCode), MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                PAKReturnType ResponseCode = AlienPAK.ExportFile(((TreeItem)FileTree.SelectedNode.Tag).String_Value, FilePicker.FileName);
                MessageBox.Show(AlienErrors.ErrorMessageBody(ResponseCode), AlienErrors.ErrorMessageTitle(ResponseCode), MessageBoxButtons.OK, MessageBoxIcon.Information);
                Cursor.Current = Cursors.Default;
            }
        }

        /* Add file to the loaded archive */
        private void AddFileToArchive_Click(object sender, EventArgs e)
        {
            //Let the user decide what file to add, then add it
            OpenFileDialog FilePicker = new OpenFileDialog();
            FilePicker.Filter = "Any File|*.*";
            if (FilePicker.ShowDialog() == DialogResult.OK)
            {
                Cursor.Current = Cursors.WaitCursor;
                PAKReturnType ResponseCode = AlienPAK.AddNewFile(FilePicker.FileName);
                MessageBox.Show(AlienErrors.ErrorMessageBody(ResponseCode), AlienErrors.ErrorMessageTitle(ResponseCode), MessageBoxButtons.OK, MessageBoxIcon.Information);
                Cursor.Current = Cursors.Default;
            }
            //This is an expensive call for any PAK except PAK2, as it uses the new system.
            //We only can call with PAK2 here so it's fine, but worth noting.
            UpdateFileTree(AlienPAK.Parse());
        }

        /* Remove selected file from the archive */
        private void RemoveFileFromArchive_Click(object sender, EventArgs e)
        {
            if (FileTree.SelectedNode == null || ((TreeItem)FileTree.SelectedNode.Tag).Item_Type != TreeItemType.EXPORTABLE_FILE)
            {
                MessageBox.Show("Please select a file from the list.", "No file selected.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            DialogResult ConfirmRemoval = MessageBox.Show("Are you sure you would like to remove this file?", "About to remove selected file...", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (ConfirmRemoval == DialogResult.Yes)
            {
                Cursor.Current = Cursors.WaitCursor;
                PAKReturnType ResponseCode = AlienPAK.RemoveFile(((TreeItem)FileTree.SelectedNode.Tag).String_Value);
                MessageBox.Show(AlienErrors.ErrorMessageBody(ResponseCode), AlienErrors.ErrorMessageTitle(ResponseCode), MessageBoxButtons.OK, MessageBoxIcon.Information);
                Cursor.Current = Cursors.Default;
            }
            //This is an expensive call for any PAK except PAK2, as it uses the new system.
            //We only can call with PAK2 here so it's fine, but worth noting.
            UpdateFileTree(AlienPAK.Parse());
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
        ToolPaths Paths = new ToolPaths();
        AlienPAK_Wrapper.AlienContentType LaunchMode;
        string ModeFileName = "";
        bool LaunchingWithArgs = false;

        //Run on init
        private void AlienModToolsAdditions(string[] args, AlienPAK_Wrapper.AlienContentType LaunchAs)
        {
            LaunchMode = LaunchAs;
            LaunchingWithArgs = !(args.Length == 0 || !File.Exists(args[0]));

            //Populate the form with the UI.PAK if launched as so, and exit early
            if (LaunchAs == AlienPAK_Wrapper.AlienContentType.UI)
            {
                OpenFileAndPopulateGUI(Paths.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/UI.PAK");
                this.Text = "Alien: Isolation Content Editor - UI.PAK";
                groupBox4.Hide();
                return;
            }

            //If launching into an unknown file type, hide appropriate GUI elements
            if (LaunchAs == AlienPAK_Wrapper.AlienContentType.UNKNOWN)
            {
                this.Text = "Alien: Isolation Content Editor - UNKNOWN PAK FILE";
                groupBox4.Hide();
                MessageBox.Show("This PAK file is currently unsupported.", "Unsupported PAK", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Close();
                return; //Just to be sure!
            }

            //We're loading into map-specific mode, populate GUI and work out what file name to use
            ModeSpecificFileName();
            PopulateMapDropdown();

            //If launched with args, adjust accordingly - else default to index 0 in dropdown
            if (LaunchingWithArgs)
            {
                try
                {
                    //Get map name and select it
                    int CutOutStartLength = (Paths.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/ENV/PRODUCTION/").Length;
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
            else
            {
                mapToLoadContentFrom.SelectedIndex = 0;
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
            if(File.Exists(Paths.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/ENV/PRODUCTION/" + MapName + "/WORLD/COMMANDS.PAK"))
            {
                mapToLoadContentFrom.Items.Add(MapName);
            }
        }

        //Get filename depending on load type
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
            OpenFileAndPopulateGUI(Paths.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/ENV/PRODUCTION/" + mapToLoadContentFrom.Text + "/RENDERABLE/" + ModeFileName);
            this.Text = "Alien: Isolation Content Editor - " + mapToLoadContentFrom.Text + " - " + ModeFileName;
        }

        //Launch game to currently loaded map
        private void LaunchGameToMap_Click(object sender, EventArgs e)
        {
            Landing_OpenGame launchGame = new Landing_OpenGame(mapToLoadContentFrom.Text);
        }

        //Form closes
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (LaunchingWithArgs)
            {
                Application.Exit();
            }
        }
    }
}
