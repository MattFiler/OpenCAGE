using Alien_Isolation_Mod_Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace AlienPAK
{
    /*
     * 
     * This is a modified version of AlienPAK, which can be found on its own repo as a standalone tool:
     * https://github.com/MattFiler/AlienPAK
     * 
     * While all AlienPAK classes are largely a direct port, some minor tweaks have been made, marked with an OPENCAGE comment.
     * This form is heavily modified from the base AlienPAK implementation.
     * Major changes:
     *  - Cannot load a PAK from the form
     *  - Loading as texture edit mode will load all PAKs
     *  - texconv is used instead of DirectXTexNet
     * 
    */

    public partial class AlienPAK_Imported : Form
    {
        List<PAK> AlienPAKs = new List<PAK>();
        ErrorMessages AlienErrors = new ErrorMessages();
        ToolPaths OPENCAGE_Paths = new ToolPaths();
        TreeUtility treeHelper;

        ContentTools_Loadscreen loadscreen;
        public AlienPAK_Imported(AlienPAK_Wrapper.AlienContentType LaunchAs)
        {
            LaunchMode = LaunchAs;
            InitializeComponent();
            
            //Link image list to GUI elements for icons
            FileTree.ImageList = imageList1;

            loadscreen = new ContentTools_Loadscreen(this);
            loadscreen.Show();

            treeHelper = new TreeUtility(FileTree);
        }

        //Start loading content when loadscreen is visible
        public void StartLoadingContent()
        {
            AlienModToolsAdditions();
            loadscreen.Close();
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
            treeHelper.UpdateFileTree(ParsedFiles);
            UpdateSelectedFilePreview();

            //Update title
            //this.Text = "Alien: Isolation PAK Tool - " + Path.GetFileName(filename);
            Cursor.Current = Cursors.Default;

            //Show/hide extended archive support if appropriate
            if (AlienPAKs[0].Format == PAKType.PAK2)
            {
                groupBox3.Show();
                return;
            }
            groupBox3.Hide();
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
                case "DXBC":
                    return "DXBC (Compiled HLSL)";
                default:
                    return FileExtension.Substring(1).ToUpper();
            }
        }

        /* Temp function to get a file as a byte array */
        private byte[] GetFileAsBytes(string FileName)
        {
            PAKReturnType ResponseCode = PAKReturnType.FAIL_UNKNOWN;
            foreach (PAK thisPAK in AlienPAKs)
            {
                ResponseCode = thisPAK.ExportFile(FileName, "temp"); //Should really be able to pull from PAK as bytes
                if (ResponseCode == PAKReturnType.SUCCESS || ResponseCode == PAKReturnType.SUCCESS_WITH_WARNINGS) break;
            }
            if (!File.Exists("temp")) return new byte[] { };
            byte[] ExportedFile = File.ReadAllBytes("temp");
            File.Delete("temp");
            return ExportedFile;
        }

        /* Update file preview */
        private void UpdateSelectedFilePreview()
        {
            //First, reset the GUI
            groupBox1.Visible = false;
            filePreviewImage.BackgroundImage = null;
            fileNameInfo.Text = "";
            fileSizeInfo.Text = "";
            fileTypeInfo.Text = "";
            exportFile.Enabled = false;
            importFile.Enabled = false;
            removeFile.Enabled = false;
            addFile.Enabled = true; //Eventually move this to only be enabled on directory selection

            //Exit early if nothing selected
            if (FileTree.SelectedNode == null) {
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

                //Show file preview if selected an image
                if (Path.GetExtension(FileName).ToUpper() == ".DDS")
                {
                    foreach (PAK thisPAK in AlienPAKs)
                    {
                        PAKReturnType ResponseCode = thisPAK.ExportFile(FileName, OPENCAGE_Paths.GetPath(ToolPaths.Paths.FOLDER_WORKING_FILES) + "temp.dds");
                        if (ResponseCode == PAKReturnType.SUCCESS || ResponseCode == PAKReturnType.SUCCESS_WITH_WARNINGS) break;
                    }
                    byte[] imageFile = ConvertWithTexconv(OPENCAGE_Paths.GetPath(ToolPaths.Paths.FOLDER_WORKING_FILES) + "temp.dds", "png");
                    File.Delete(OPENCAGE_Paths.GetPath(ToolPaths.Paths.FOLDER_WORKING_FILES) + "temp.dds");
                    if (imageFile.Length != 0)
                    {
                        Bitmap previewImage;
                        using (var imageFileStream = new MemoryStream(imageFile))
                        {
                            previewImage = new Bitmap(imageFileStream);
                        }
                        ResizeImagePreview(previewImage);
                    }
                }
                groupBox1.Visible = (filePreviewImage.BackgroundImage != null);

                //Enable buttons
                exportFile.Enabled = true;
                importFile.Enabled = true;
                removeFile.Enabled = true;
            }
        }

        /* Addition for OpenCAGE: use texconv, not DirectXTexNet */
        private byte[] ConvertWithTexconv(string fileToConvert, string outFormat, TextureFormat ddsFormat = TextureFormat.DXGI_FORMAT_BC7_UNORM, bool reattempt = false)
        {
            //Delete lingering files
            string workingFileInput = OPENCAGE_Paths.GetPath(ToolPaths.Paths.FOLDER_TEXCONV) + "temp" + Path.GetExtension(fileToConvert);
            string workingFileOutput = workingFileInput.Substring(0, workingFileInput.Length - 3) + outFormat;
            if (File.Exists(workingFileInput)) File.Delete(workingFileInput);
            if (File.Exists(workingFileOutput)) File.Delete(workingFileOutput);
            File.Copy(fileToConvert, workingFileInput);

            //Convert DDS to PNG
            string dxgiFormat = ddsFormat.ToString();
            if (dxgiFormat.Length > 12 && dxgiFormat.Substring(0, 12) == "DXGI_FORMAT_") dxgiFormat = dxgiFormat.Substring(12);
            if (reattempt) dxgiFormat += "_SRGB"; 
            ProcessStartInfo processInfo = new ProcessStartInfo(OPENCAGE_Paths.GetPath(ToolPaths.Paths.FILE_TEXCONV), "\"" + Path.GetFileName(workingFileInput) + "\" -ft " + outFormat + " -y -l -f " + dxgiFormat);
            processInfo.WorkingDirectory = OPENCAGE_Paths.GetPath(ToolPaths.Paths.FOLDER_TEXCONV);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            Process process = Process.Start(processInfo);
            process.WaitForExit();
            process.Close();

            //Make sure conversion succeeded
            File.Delete(workingFileInput);
            if (!File.Exists(workingFileOutput))
            {
                if (reattempt) return new byte[] { };
                return ConvertWithTexconv(fileToConvert, outFormat, ddsFormat, true); //We re-run, and try the _SRGB variant
            }

            //Return converted file
            byte[] toReturn = File.ReadAllBytes(workingFileOutput);
            File.Delete(workingFileOutput);
            return toReturn;
        }

        /* Set the image in the preview window and scale appropriately */
        private void ResizeImagePreview(Bitmap image)
        {
            filePreviewImage.BackgroundImage = image;
            if (image.Width >= filePreviewImage.Width || image.Height >= filePreviewImage.Height) filePreviewImage.BackgroundImageLayout = ImageLayout.Zoom;
            else filePreviewImage.BackgroundImageLayout = ImageLayout.None;
        }

        /* Import a file to replace the selected PAK entry */
        private void ImportSelectedFile()
        {
            if (FileTree.SelectedNode == null || ((TreeItem)FileTree.SelectedNode.Tag).Item_Type != TreeItemType.EXPORTABLE_FILE)
            {
                MessageBox.Show("Please select a file from the list.", "No file selected.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string filter = "Import File|*" + Path.GetExtension(FileTree.SelectedNode.Text);

            //Allow additional import options through texconv if DDS
            string FileName = ((TreeItem)FileTree.SelectedNode.Tag).String_Value;
            TextureFormat baseFormat = TextureFormat.DXGI_FORMAT_BC7_UNORM;
            if (Path.GetExtension(FileName).ToUpper() == ".DDS")
            {
                //Export DDS from PAK by filename
                string TempOutDDS = OPENCAGE_Paths.GetPath(ToolPaths.Paths.FOLDER_TEXCONV) + "temp.dds";
                foreach (PAK thisPAK in AlienPAKs)
                {
                    PAKReturnType ResponseCode = thisPAK.ExportFile(FileName, TempOutDDS);
                    if (ResponseCode == PAKReturnType.SUCCESS || ResponseCode == PAKReturnType.SUCCESS_WITH_WARNINGS) break;
                }

                //If exported, pull format type
                if (File.Exists(TempOutDDS))
                {
                    BinaryReader TextureReader = new BinaryReader(File.OpenRead(TempOutDDS));

                    //THIS SHOULD MATCH THE CURRENTLY SUPPORTED FORMATS IN "DDSReader.cs"
                    TextureReader.BaseStream.Position = 128;
                    switch (TextureReader.ReadInt32())
                    {
                        case 83:
                            baseFormat = TextureFormat.DXGI_FORMAT_BC5_UNORM;
                            break;
                        case 71:
                            baseFormat = TextureFormat.DXGI_FORMAT_BC1_UNORM;
                            break;
                        case 77:
                            baseFormat = TextureFormat.DXGI_FORMAT_BC3_UNORM;
                            break;
                        case 87:
                            baseFormat = TextureFormat.DXGI_FORMAT_B8G8R8A8_UNORM;
                            break;
                        case 98:
                            baseFormat = TextureFormat.DXGI_FORMAT_BC7_UNORM;
                            break;
                        default:
                            baseFormat = TextureFormat.DXGI_FORMAT_B8G8R8_UNORM; //Fingers crossed
                            break;
                    }
                    //END MATCH

                    TextureReader.Close();
                    File.Delete(TempOutDDS);

                    filter = "DDS Image|*.dds|PNG Image|*.png";
                }
            }

            //Allow selection of a file (force extension), then drop it in
            OpenFileDialog FilePicker = new OpenFileDialog();
            FilePicker.Filter = filter;
            if (FilePicker.ShowDialog() == DialogResult.OK)
            {
                Cursor.Current = Cursors.WaitCursor;

                //Special import for DDS conversion
                bool ImportOK = true;
                bool ImportingConverted = false;
                if (Path.GetExtension(FileName).ToUpper() == ".DDS" && Path.GetExtension(FilePicker.FileName).ToUpper() == ".PNG")
                {
                    byte[] imageFile = ConvertWithTexconv(FilePicker.FileName, "dds", baseFormat);
                    if (imageFile.Length == 0)
                    {
                        ImportOK = false;
                        MessageBox.Show("Failed to import as PNG!\nPlease try again as DDS.", AlienErrors.ErrorMessageTitle(PAKReturnType.FAIL_UNKNOWN), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        FilePicker.FileName += ".DDS";
                        File.WriteAllBytes(FilePicker.FileName, imageFile);
                        ImportingConverted = true;
                    }
                }

                //Regular import
                if (ImportOK)
                {
                    foreach (PAK thisPAK in AlienPAKs) thisPAK.ImportFile(FileName, FilePicker.FileName);
                    if (ImportingConverted) File.Delete(FilePicker.FileName); //We temp dump out a converted file, which this cleans up
                    MessageBox.Show(AlienErrors.ErrorMessageBody(PAKReturnType.SUCCESS), AlienErrors.ErrorMessageTitle(PAKReturnType.SUCCESS), MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            string FileName = ((TreeItem)FileTree.SelectedNode.Tag).String_Value;

            //If export file is DDS allow PNG conversion attempt
            string filter = "Exported File|*" + Path.GetExtension(FileTree.SelectedNode.Text);
            if (Path.GetExtension(FileTree.SelectedNode.Text).ToUpper() == ".DDS") filter = "DDS Image|*.dds|PNG Image|*.png";

            //Remove extension from output filename
            string filename = Path.GetFileName(FileTree.SelectedNode.Text);
            while (Path.GetExtension(filename).Length != 0) filename = filename.Substring(0, filename.Length - Path.GetExtension(filename).Length);

            //Let the user decide where to save, then save
            SaveFileDialog FilePicker = new SaveFileDialog();
            FilePicker.Filter = filter;
            FilePicker.FileName = filename;
            if (FilePicker.ShowDialog() == DialogResult.OK)
            {
                Cursor.Current = Cursors.WaitCursor;
                //Special export for DDS conversion
                if (FilePicker.FilterIndex == 2) //Currently we only allow a second filter if DDS, so it'll always be this if so
                {
                    foreach (PAK thisPAK in AlienPAKs)
                    {
                        PAKReturnType ResponseCode = thisPAK.ExportFile(FileName, OPENCAGE_Paths.GetPath(ToolPaths.Paths.FOLDER_WORKING_FILES) + "temp.dds");
                        if (ResponseCode == PAKReturnType.SUCCESS || ResponseCode == PAKReturnType.SUCCESS_WITH_WARNINGS) break;
                    }
                    byte[] imageFile = ConvertWithTexconv(OPENCAGE_Paths.GetPath(ToolPaths.Paths.FOLDER_WORKING_FILES) + "temp.dds", "png");
                    File.Delete(OPENCAGE_Paths.GetPath(ToolPaths.Paths.FOLDER_WORKING_FILES) + "temp.dds");
                    if (imageFile.Length == 0)
                    {
                        MessageBox.Show("Failed to export as PNG!\nPlease try again as DDS.", AlienErrors.ErrorMessageTitle(PAKReturnType.FAIL_UNKNOWN), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        File.WriteAllBytes(FilePicker.FileName, imageFile);
                        MessageBox.Show(AlienErrors.ErrorMessageBody(PAKReturnType.SUCCESS), AlienErrors.ErrorMessageTitle(PAKReturnType.SUCCESS), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                //Regular export
                else
                {
                    PAKReturnType ResponseCode = PAKReturnType.FAIL_UNKNOWN;
                    foreach (PAK thisPAK in AlienPAKs)
                    {
                        ResponseCode = thisPAK.ExportFile(FileName, FilePicker.FileName);
                        if (ResponseCode == PAKReturnType.SUCCESS || ResponseCode == PAKReturnType.SUCCESS_WITH_WARNINGS) break;
                    }
                    MessageBox.Show(AlienErrors.ErrorMessageBody(ResponseCode), AlienErrors.ErrorMessageTitle(ResponseCode), MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
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
            treeHelper.UpdateFileTree(AlienPAKs[0].Parse());
            UpdateSelectedFilePreview();
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
            treeHelper.UpdateFileTree(AlienPAKs[0].Parse());
            UpdateSelectedFilePreview();
        }

        /* User requests to open a PAK */
        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Allow selection of a PAK from filepicker, then open
            OpenFileDialog ArchivePicker = new OpenFileDialog();
            ArchivePicker.Filter = "Alien: Isolation PAK|*.PAK";
            if (ArchivePicker.ShowDialog() == DialogResult.OK)
            {
                OpenFileAndPopulateGUI(ArchivePicker.FileName);
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
        private void AlienModToolsAdditions()
        {
            this.Text = "OpenCAGE Content Editor";

            //Populate the form with the UI.PAK if launched as so, and exit early
            if (LaunchMode == AlienPAK_Wrapper.AlienContentType.UI)
            {
                this.Text += " - UI";
                OpenFileAndPopulateGUI(Paths.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/UI.PAK");
                return;
            }

            //Populate the form with the ANIMATION.PAK if launched as so, and exit early
            if (LaunchMode == AlienPAK_Wrapper.AlienContentType.ANIMATION)
            {
                this.Text += " - Animations";
                OpenFileAndPopulateGUI(Paths.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/GLOBAL/ANIMATION.PAK");
                return;
            }

            //If launching into an unknown file type, hide appropriate GUI elements
            if (LaunchMode == AlienPAK_Wrapper.AlienContentType.UNKNOWN)
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
                    this.Text += " - Models";
                    break;
                case AlienPAK_Wrapper.AlienContentType.TEXTURE:
                    levelFileToUse = "LEVEL_TEXTURES.ALL.PAK";
                    globalFileToUse = "GLOBAL_TEXTURES.ALL.PAK";
                    this.Text += " - Textures";
                    break;
                case AlienPAK_Wrapper.AlienContentType.SCRIPT:
                    levelFileToUse = "COMMANDS.PAK";
                    this.Text += " - Scripts";
                    break;
            }

            //Load the files for all levels
            Cursor.Current = Cursors.WaitCursor;
            List<string> allLevelPAKs = Directory.GetFiles(Paths.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/ENV/PRODUCTION/", levelFileToUse, SearchOption.AllDirectories).ToList<string>();
            if (globalFileToUse != "") allLevelPAKs.Add(Paths.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/ENV/GLOBAL/WORLD/" + globalFileToUse);
            List<string> parsedFiles = new List<string>();
            foreach (string levelPAK in allLevelPAKs)
            {
                PAK thisPAK = new PAK();
                thisPAK.Open(levelPAK);
                List<string> theseFiles = thisPAK.Parse();
                foreach (string thisPAKEntry in theseFiles)
                {
                    if (!parsedFiles.Contains(thisPAKEntry)) parsedFiles.Add(thisPAKEntry);
                }
                AlienPAKs.Add(thisPAK);
            }
            treeHelper.UpdateFileTree(parsedFiles);
            UpdateSelectedFilePreview();
            Cursor.Current = Cursors.Default;
            groupBox3.Hide();
        }
    }
}
