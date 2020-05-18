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
    
        private readonly string pathErrorString = "The path is not of a legal form.";

        public VersionCheck()
        {
            InitializeComponent();

            if (!ToolFileSetup()) { return; }
            DeleteOldEXE();

            try
            {
                //Get current Github version
                string branch_name = "master";
                if (File.Exists("DEBUG_MODE")) branch_name = "staging";
                Stream webStream = webClient.OpenRead("https://raw.githubusercontent.com/MattFiler/OpenCAGE/" + branch_name + "/Source/PackagingTool/Properties/AssemblyInfo.cs?v=" + ProductVersion + "&r = " + random.Next(5000).ToString());
                string[] LatestVersionArray = new StreamReader(webStream).ReadToEnd().Split(new[] { "AssemblyFileVersion(\"" }, StringSplitOptions.None);
                string LatestVersionNumber = LatestVersionArray[1].Substring(0, LatestVersionArray[1].Length - 4);

                //Check to see if update is required
                updateRequired = (ProductVersion != LatestVersionNumber);

                if (!updateRequired)
                {
                    if (File.Exists("OpenCAGE Updater.exe")) File.Delete("OpenCAGE Updater.exe");
                    return;
                }
                try
                {
                    //New update needs to be downloaded
                    RunUpdater();
                }
                catch (Exception errormessage)
                {
                    HandleError(errormessage);
                }
            }
            catch (Exception errormessage)
            {
                HandleError(errormessage);
            }
        }

        /* Run the updater tool and close OpenCAGE */
        public static void RunUpdater(bool showMsg = true)
        {
            if (showMsg) MessageBox.Show("A new version of OpenCAGE is available.", "OpenCAGE Updater", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            if (!File.Exists("OpenCAGE Updater.exe"))
            {
                File.WriteAllBytes("OpenCAGE Updater.exe", Properties.Resources.OpenCAGE_Updater);
            }
            Process.Start("OpenCAGE Updater.exe");
            Application.Exit();
            Environment.Exit(0);
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
                            MessageBox.Show("Please locate your Alien: Isolation executable (AI.exe).", "OpenCAGE Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                    Folders.SetPath(ToolPaths.Paths.FOLDER_TEXCONV, Folders.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/MODTOOLS/TEXCONV/");
                    Folders.SetPath(ToolPaths.Paths.FOLDER_ASSETS, Folders.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/MODTOOLS/REMOTE_ASSETS/"); //THIS NEEDS TO MATCH UPDATER
                    Folders.SetPath(ToolPaths.Paths.FILE_LEGENDPLUGIN_DLL, Folders.GetPath(ToolPaths.Paths.FOLDER_BRAINIAC) + "/plugins/LegendPlugin.dll");
                    Folders.SetPath(ToolPaths.Paths.FILE_BRAINIAC_EXE, Folders.GetPath(ToolPaths.Paths.FOLDER_BRAINIAC) + "/Brainiac Designer.exe");
                    Folders.SetPath(ToolPaths.Paths.FILE_TEXCONV, Folders.GetPath(ToolPaths.Paths.FOLDER_TEXCONV) + "/texconv.exe");

                    //Confirm for future launches that the folders are setup
                    Settings.SetSetting(ToolSettings.Settings.SETTING_INTERNAL_DID_SETUP_FOLDERS, true);
                }

                //Create required folders
                Directory.CreateDirectory(Folders.GetPath(ToolPaths.Paths.FOLDER_BEHAVIOUR_TREES));
                Directory.CreateDirectory(Folders.GetPath(ToolPaths.Paths.FOLDER_WORKING_FILES));
                Directory.CreateDirectory(Folders.GetPath(ToolPaths.Paths.FOLDER_MOD_INSTALL_LOCATION));
                Directory.CreateDirectory(Folders.GetPath(ToolPaths.Paths.FOLDER_BRAINIAC) + "/plugins/");
                Directory.CreateDirectory(Folders.GetPath(ToolPaths.Paths.FOLDER_TEXCONV));

                //First - make sure we have Brainiac: if not, we probably haven't done the new update (18/05/20)
                if (!Directory.Exists(Folders.GetPath(ToolPaths.Paths.FOLDER_ASSETS)) || !File.Exists(Folders.GetPath(ToolPaths.Paths.FOLDER_ASSETS) + "Resources/Brainiac Designer.exe"))
                {
                    RunUpdater(false);
                    return false;
                }

                //Copy Brainiac if out of date or doesn't exist
                if (!File.Exists(Folders.GetPath(ToolPaths.Paths.FILE_BRAINIAC_EXE)) || !File.ReadAllBytes(Folders.GetPath(ToolPaths.Paths.FILE_BRAINIAC_EXE)).SequenceEqual(LocalAsset.GetAsBytes("Resources", "Brainiac Designer.exe")))
                {
                    File.WriteAllBytes(Folders.GetPath(ToolPaths.Paths.FILE_BRAINIAC_EXE), LocalAsset.GetAsBytes("Resources", "Brainiac Designer.exe"));
                    File.WriteAllBytes(Folders.GetPath(ToolPaths.Paths.FOLDER_BRAINIAC) + "/Brainiac.Design.dll", LocalAsset.GetAsBytes("Resources", "Brainiac.Design.dll"));
                    File.WriteAllBytes(Folders.GetPath(ToolPaths.Paths.FOLDER_BRAINIAC) + "/WeifenLuo.WinFormsUI.Docking.dll", LocalAsset.GetAsBytes("Resources", "WeifenLuo.WinFormsUI.Docking.dll"));
                    File.WriteAllText(Folders.GetPath(ToolPaths.Paths.FOLDER_BRAINIAC) + "/debug_workspaces.xml", LocalAsset.GetAsString("Resources", "debug_workspaces.xml"));
                }

                //Copy texconv if out of date or doesn't exist
                if (!File.Exists(Folders.GetPath(ToolPaths.Paths.FILE_TEXCONV)) || !File.ReadAllBytes(Folders.GetPath(ToolPaths.Paths.FILE_TEXCONV)).SequenceEqual(LocalAsset.GetAsBytes("Resources", "texconv.exe")))
                {
                    File.WriteAllBytes(Folders.GetPath(ToolPaths.Paths.FILE_TEXCONV), LocalAsset.GetAsBytes("Resources", "texconv.exe"));
                }

                //Copy LegendPlugin if out of date or doesn't exist
                if (!File.Exists(Folders.GetPath(ToolPaths.Paths.FILE_LEGENDPLUGIN_DLL)) || !File.ReadAllBytes(Folders.GetPath(ToolPaths.Paths.FILE_LEGENDPLUGIN_DLL)).SequenceEqual(LocalAsset.GetAsBytes("Resources", "LegendPlugin.dll")))
                {
                    File.WriteAllBytes(Folders.GetPath(ToolPaths.Paths.FILE_LEGENDPLUGIN_DLL), LocalAsset.GetAsBytes("Resources", "LegendPlugin.dll"));
                }

                //Initialise resources for mod tools
                if (!Directory.Exists(Folders.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES)))
                {
                    Directory.CreateDirectory(Folders.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES));
                    File.WriteAllBytes(Folders.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES) + "Isolation.ttf", LocalAsset.GetAsBytes("Fonts", "Isolation_Isolation.ttf"));
                    File.WriteAllBytes(Folders.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES) + "Jixellation.ttf", LocalAsset.GetAsBytes("Fonts", "JixellationBold_Jixellation.ttf"));
                    File.WriteAllBytes(Folders.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES) + "Nostromo.ttf", LocalAsset.GetAsBytes("Fonts", "NostromoBoldCond_Nostromo Cond.ttf"));
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
            MessageBox.Show("An error occurred while setting up.\nPlease restart OpenCAGE.", "OpenCAGE Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(0);
        }

        //Handle any error from the update process
        private void HandleError(Exception errormessage)
        {
            if (errormessage.Message.ToString() != pathErrorString)
            {
                //Probably no internet connection
                MessageBox.Show("Failed to check for updates." + Environment.NewLine + "Continuing with version " + ProductVersion, "OpenCAGE Updater", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                //File error - means we're loading for the first time ... but have to have a messagebox here else we'll crash
                if (File.Exists("modtools_locales.ayz"))
                {
                    MessageBox.Show("OpenCAGE running version " + ProductVersion, "OpenCAGE Updater", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Welcome to OpenCAGE!", "OpenCAGE Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
