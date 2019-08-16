using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alien_Isolation_Mod_Tools
{
    public partial class VersionCheck : Form
    {
        private ToolPaths Folders = new ToolPaths();
        private ToolSettings Settings = new ToolSettings();

        private Random random = new Random();
        private WebClient webClient = new WebClient();

        public bool updateRequired = false;
        public bool updateCheckFailed = false;
    
        private string newToolEXE = "";
        private readonly string pathErrorString = "The path is not of a legal form.";

        public VersionCheck()
        {
            InitializeComponent();

            if (!ToolFileSetup()) { return; }

            try
            {
                //Get current Github version
                Stream webStream = webClient.OpenRead("https://raw.githubusercontent.com/MattFiler/LegendPlugin/master/Source/PackagingTool/Properties/AssemblyInfo.cs?v=" + ProductVersion + "&r = " + random.Next(5000).ToString());
                string[] LatestVersionArray = new StreamReader(webStream).ReadToEnd().Split(new[] { "AssemblyFileVersion(\"" }, StringSplitOptions.None);
                string LatestVersionNumber = LatestVersionArray[1].Substring(0, LatestVersionArray[1].Length - 4);

                //Check against current version
                if (ProductVersion != LatestVersionNumber)
                {
                    newToolEXE = Folders.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/Mod Tools V" + LatestVersionNumber + ".exe";
                    if (File.Exists(newToolEXE))
                    {
                        //Start new exe
                        Process.Start(newToolEXE);

                        //Close app
                        Application.Exit();
                        Environment.Exit(0);
                    }
                    else
                    {
                        updateRequired = true;
                    }
                }
                else
                {
                    //Clean up from previous updates
                    DeleteOldEXE();

                    //Update is not required
                    updateRequired = false;
                }
            }
            catch (Exception errormessage)
            {
                HandleError(errormessage);
            }
        }

        /* Setup Tool Files/Folders */
        private bool ToolFileSetup()
        {
            try
            {
                //Set Paths
                if (!Settings.GetSetting(ToolSettings.Settings.SETTING_INTERNAL_DID_SETUP_FOLDERS))
                {
                    //Set game location
                    if (Folders.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) == "")
                    {
                        if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + @"\AI.exe"))
                        {
                            Folders.SetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION, AppDomain.CurrentDomain.BaseDirectory);
                        }
                        else
                        {
                            MessageBox.Show("Please locate your Alien: Isolation executable (AI.exe).", "Mod Tools Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            OpenFileDialog selectGameFile = new OpenFileDialog();
                            selectGameFile.Filter = "Applications (*.exe)|AI.exe";
                            if (selectGameFile.ShowDialog() == DialogResult.OK)
                            {
                                Folders.SetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION, Path.GetDirectoryName(selectGameFile.FileName));
                            }
                        }
                    }

                    //Validate game location
                    if (!File.Exists(Folders.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + @"\DATA\BINARY_BEHAVIOR\_DIRECTORY_CONTENTS.BML"))
                    {
                        HandleSetupFail();
                        return false;
                    }

                    //Set other locations
                    Folders.SetPath(ToolPaths.Paths.FOLDER_BRAINIAC, Folders.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/MODTOOLS");
                    Folders.SetPath(ToolPaths.Paths.FOLDER_BEHAVIOUR_TREES, Folders.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/MODTOOLS/BEHAVIOUR_TREES/");
                    Folders.SetPath(ToolPaths.Paths.FOLDER_MOD_INSTALL_LOCATION, Folders.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/MODS/");
                    Folders.SetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES, Folders.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/MODTOOLS/RESOURCES/");
                    Folders.SetPath(ToolPaths.Paths.FOLDER_WORKING_FILES, Folders.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/MODTOOLS/WORKING_FILES/");
                    Folders.SetPath(ToolPaths.Paths.FILE_LEGENDPLUGIN_DLL, Folders.GetPath(ToolPaths.Paths.FOLDER_BRAINIAC) + "/plugins/LegendPlugin.dll");
                    Folders.SetPath(ToolPaths.Paths.FILE_BRAINIAC_EXE, Folders.GetPath(ToolPaths.Paths.FOLDER_BRAINIAC) + "/Brainiac Designer.exe");

                    //Confirm for future launches that the folders are setup
                    Settings.SetSetting(ToolSettings.Settings.SETTING_INTERNAL_DID_SETUP_FOLDERS, true);
                }

                //Create required folders
                Directory.CreateDirectory(Folders.GetPath(ToolPaths.Paths.FOLDER_BEHAVIOUR_TREES));
                Directory.CreateDirectory(Folders.GetPath(ToolPaths.Paths.FOLDER_WORKING_FILES));
                Directory.CreateDirectory(Folders.GetPath(ToolPaths.Paths.FOLDER_MOD_INSTALL_LOCATION));
                Directory.CreateDirectory(Folders.GetPath(ToolPaths.Paths.FOLDER_BRAINIAC) + "/plugins/");

                //Copy Brainiac if out of date or doesn't exist
                if (!File.Exists(Folders.GetPath(ToolPaths.Paths.FILE_BRAINIAC_EXE)) || !File.ReadAllBytes(Folders.GetPath(ToolPaths.Paths.FILE_BRAINIAC_EXE)).SequenceEqual(Properties.Resources.Brainiac_Designer))
                {
                    File.WriteAllBytes(Folders.GetPath(ToolPaths.Paths.FILE_BRAINIAC_EXE), Properties.Resources.Brainiac_Designer);
                    File.WriteAllBytes(Folders.GetPath(ToolPaths.Paths.FOLDER_BRAINIAC) + "/Brainiac.Design.dll", Properties.Resources.Brainiac_Design);
                    File.WriteAllBytes(Folders.GetPath(ToolPaths.Paths.FOLDER_BRAINIAC) + "/WeifenLuo.WinFormsUI.Docking.dll", Properties.Resources.WeifenLuo_WinFormsUI_Docking);
                    File.WriteAllText(Folders.GetPath(ToolPaths.Paths.FOLDER_BRAINIAC) + "/debug_workspaces.xml", Properties.Resources.debug_workspaces);
                }

                //Copy LegendPlugin if out of date or doesn't exist
                if (!File.Exists(Folders.GetPath(ToolPaths.Paths.FILE_LEGENDPLUGIN_DLL)) || !File.ReadAllBytes(Folders.GetPath(ToolPaths.Paths.FILE_LEGENDPLUGIN_DLL)).SequenceEqual(Properties.Resources.LegendPlugin))
                {
                    File.WriteAllBytes(Folders.GetPath(ToolPaths.Paths.FILE_LEGENDPLUGIN_DLL), Properties.Resources.LegendPlugin);
                }

                //Initialise resources for mod tools
                if (!Directory.Exists(Folders.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES)))
                {
                    Directory.CreateDirectory(Folders.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES));
                    File.WriteAllBytes(Folders.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES) + "Isolation.ttf", Properties.Resources.Isolation_Isolation);
                    File.WriteAllBytes(Folders.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES) + "Jixellation.ttf", Properties.Resources.JixellationBold_Jixellation);
                    File.WriteAllBytes(Folders.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES) + "Nostromo.ttf", Properties.Resources.NostromoBoldCond_Nostromo_Cond);
                }
                return true;
            }
            catch
            {
                HandleSetupFail();
                return false;
            }
        }
        private void HandleSetupFail()
        {
            Folders.SetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION, "");
            Settings.SetSetting(ToolSettings.Settings.SETTING_INTERNAL_DID_SETUP_FOLDERS, false);
            MessageBox.Show("An error occurred while setting up.\nPlease restart the Mod Tools.", "Setup issue!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(0);
        }

        //Update check came back saying that we need to update: show GUI and update with progress bar
        private void VersionCheck_Load(object sender, EventArgs e)
        {
            if (!updateRequired)
            {
                return;
            }

            try
            {
                //New update needs to be downloaded
                MessageBox.Show("A new version of the Alien: Isolation Mod Tools is available." + Environment.NewLine + "The latest version will be downloaded to your game directory.", "Alien: Isolation Mod Tools Updater", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                this.TopMost = true;
                webClient.DownloadProgressChanged += (s, clientprogress) =>
                {
                    UpdateProgress.Value = clientprogress.ProgressPercentage;
                };
                webClient.DownloadFileCompleted += (s, clientprogress) =>
                {
                    //Finish progress bar
                    UpdateProgress.Value = 100;

                    //Start new exe
                    Process.Start(newToolEXE);

                    //Close app
                    Application.Exit();
                    Environment.Exit(0);
                };
                webClient.DownloadFileAsync(new Uri("https://raw.githubusercontent.com/MattFiler/LegendPlugin/master/Mod%20Tools/Mod%20Tools.exe"), newToolEXE);
            }
            catch (Exception errormessage)
            {
                HandleError(errormessage);
            }
        }

        //Handle any error from the update process
        private void HandleError(Exception errormessage)
        {
            if (errormessage.Message.ToString() != pathErrorString)
            {
                //Probably no internet connection
                MessageBox.Show("Failed to check for updates." + Environment.NewLine + "Continuing with version " + ProductVersion, "Alien: Isolation Mod Tools Updater", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                //File error - means we're loading for the first time ... but have to have a messagebox here else we'll crash
                if (File.Exists("modtools_locales.ayz"))
                {
                    MessageBox.Show("Alien: Isolation Mod Tools running version " + ProductVersion, "Alien: Isolation Mod Tools Updater", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Welcome to the Alien: Isolation Mod Tools!", "Mod Tools Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            updateCheckFailed = true;
        }

        //Remove old mod tool exes from game directory
        private void DeleteOldEXE()
        {
            foreach (string file in Directory.GetFiles(Folders.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION)))
            { 
                if ((Path.GetFileName(file).Count() > 9) && (Path.GetFileName(file).Substring(0, 9) == "Mod Tools"))
                {
                    if (System.AppDomain.CurrentDomain.FriendlyName != Path.GetFileName(file))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
        }
    }
}
