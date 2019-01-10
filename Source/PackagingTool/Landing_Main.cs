using PackagingTool;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alien_Isolation_Mod_Tools
{
    public partial class Landing_Main : Form
    {
        Directories AlienDirectories = new Directories();

        public Landing_Main()
        {
            InitializeComponent();
            this.FormClosing += new FormClosingEventHandler(CloseApplicationFully);
        }

        private void CloseApplicationFully(object sender, FormClosingEventArgs e)
        {
            Application.Exit();
        }

        private void Landing_Main_Load(object sender, EventArgs e)
        {
            //Get mod tool version
            VersionText.Text = "Alien: Isolation Mod Tools" + Environment.NewLine + "Version " + ProductVersion;

            //Directories
            string GameDirectory = "";
            string BrainiacDirectory = "";

            /* CREATE SETTINGS FILE */
            if (!File.Exists(Directory.GetCurrentDirectory() + @"\modtools_settings.ayz"))
            {
                File.WriteAllText(Directory.GetCurrentDirectory() + @"\modtools_settings.ayz", "1\n0\n1"); //Write default settings
            }

            /* SET DIRECTORY LOCATIONS */
            bool hasThrownError = false;
            if (!File.Exists(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz"))
            {
                //Check if user has followed tutorial
                if (File.Exists(Directory.GetCurrentDirectory() + @"\AI.exe"))
                {
                    GameDirectory = Directory.GetCurrentDirectory(); //Game directory is current directory
                }
                else
                {
                    if (File.Exists(@"C:\Program Files\Steam\steamapps\common\Alien Isolation\AI.exe"))
                    {
                        GameDirectory = @"C:\Program Files\Steam\steamapps\common\Alien Isolation"; //Game directory is default steam directory
                    }
                    else
                    {
                        MessageBox.Show("Please locate your Alien: Isolation executable (AI.exe).", "Mod Tools Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        OpenFileDialog selectGameFile = new OpenFileDialog();
                        selectGameFile.Filter = "Applications (*.exe)|AI.exe";
                        if (selectGameFile.ShowDialog() == DialogResult.OK)
                        {
                            GameDirectory = Path.GetDirectoryName(selectGameFile.FileName); //Selected directory is game directory
                        }
                        else
                        {
                            hasThrownError = true;
                        }
                    }
                }
                
                if (!hasThrownError)
                {
                    //New as of 10/01/19 - Brainiac is included!
                    BrainiacDirectory = GameDirectory + @"\DATA\MODTOOLS";
                }

                //Save to file
                string[] ModToolsLocales = { GameDirectory, BrainiacDirectory };
                File.WriteAllLines(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz", ModToolsLocales);
            }

            //Get directory info again
            AlienDirectories = new Directories();

            /* VALIDATE GAME DIRECTORY */
            if (!File.Exists(AlienDirectories.GameDirectoryRoot() + @"\DATA\BINARY_BEHAVIOR\_DIRECTORY_CONTENTS.BML") || hasThrownError)
            {
                MessageBox.Show("Please ensure you have selected the correct game install location.", "Missing files!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                File.Delete(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz");
                Environment.Exit(0);
            }
            
            /* HANDLE ISSUES RELATING TO BRAINIAC TOOL UPDATE */
            if (AlienDirectories.BrainiacDirectoryRoot() != AlienDirectories.GameDirectoryRoot() + @"\DATA\MODTOOLS")
            {
                MessageBox.Show("Please re-launch the mod tools to apply the latest update.", "Update requires tool restart!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                File.Delete(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz");
                Environment.Exit(0);
            }

            /* CREATE REQUIRED FOLDERS */
            Directory.CreateDirectory(AlienDirectories.ToolTreeDirectory());
            Directory.CreateDirectory(AlienDirectories.ToolWorkingDirectory());
            Directory.CreateDirectory(AlienDirectories.ToolModInstallDirectory());
            Directory.CreateDirectory(AlienDirectories.BrainiacDirectoryRoot() + "/plugins/");

            //Copy Brainiac if out of date or doesn't exist
            string expectedBrainiacLocation = AlienDirectories.BrainiacDirectoryRoot() + @"\Brainiac Designer.exe";
            bool shouldExportBrainiac = false;
            if (!File.Exists(expectedBrainiacLocation))
            {
                shouldExportBrainiac = true;
            }
            else
            {
                if (!File.ReadAllBytes(expectedBrainiacLocation).SequenceEqual(Properties.Resources.Brainiac_Designer))
                {
                    shouldExportBrainiac = true;
                }
            }
            if (shouldExportBrainiac)
            {
                File.WriteAllBytes(expectedBrainiacLocation, Properties.Resources.Brainiac_Designer);
                File.WriteAllBytes(expectedBrainiacLocation.Substring(0, expectedBrainiacLocation.Length - 13) + ".Design.dll", Properties.Resources.Brainiac_Design);
                File.WriteAllBytes(expectedBrainiacLocation.Substring(0, expectedBrainiacLocation.Length - 21) + "WeifenLuo.WinFormsUI.Docking.dll", Properties.Resources.WeifenLuo_WinFormsUI_Docking);
                File.WriteAllText(expectedBrainiacLocation.Substring(0, expectedBrainiacLocation.Length - 21) + "debug_workspaces.xml", Properties.Resources.debug_workspaces);
            }

            //Copy LegendPlugin to Brainiac Designer folder if it doesn't exist - if it does, make sure its updated
            if (!File.Exists(AlienDirectories.BrainiacDirectoryRoot() + "/plugins/LegendPlugin.dll"))
            {
                File.WriteAllBytes(AlienDirectories.BrainiacDirectoryRoot() + "/plugins/LegendPlugin.dll", Properties.Resources.LegendPlugin);
            }
            else {
                try
                {
                    if (!File.ReadAllBytes(AlienDirectories.BrainiacDirectoryRoot() + "/plugins/LegendPlugin.dll").SequenceEqual(Properties.Resources.LegendPlugin))
                    {
                        //Legendplugin exists but is out of date - update from resources
                        File.Delete(AlienDirectories.BrainiacDirectoryRoot() + "/plugins/LegendPlugin.dll");
                        File.WriteAllBytes(AlienDirectories.BrainiacDirectoryRoot() + "/plugins/LegendPlugin.dll", Properties.Resources.LegendPlugin);
                        MessageBox.Show("LegendPlugin has been updated to a new version." + Environment.NewLine + "These changes will be seen within Brainiac Designer.", "Alien: Isolation Mod Tools Updater", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch { }
            }

            //Initialise resources for mod tools
            if (!Directory.Exists(AlienDirectories.ToolResourceDirectory()))
            {
                Directory.CreateDirectory(AlienDirectories.ToolResourceDirectory());
                File.WriteAllBytes(AlienDirectories.ToolResourceDirectory() + "Isolation.ttf", Properties.Resources.Isolation_Isolation);
                File.WriteAllBytes(AlienDirectories.ToolResourceDirectory() + "Jixellation.ttf", Properties.Resources.JixellationBold_Jixellation);
                File.WriteAllBytes(AlienDirectories.ToolResourceDirectory() + "Nostromo.ttf", Properties.Resources.NostromoBoldCond_Nostromo_Cond);
            }

            //Load fonts
            PrivateFontCollection ModToolFont = new PrivateFontCollection();
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Isolation.ttf");
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Jixellation.ttf");
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Nostromo.ttf");

            //Set fonts & parents
            MakeMod.Font = new Font(ModToolFont.Families[0], 40);
            MakeMod.Parent = LandingBackground;
            SaveMod.Font = new Font(ModToolFont.Families[0], 40);
            SaveMod.Parent = LandingBackground;
            LoadMod.Font = new Font(ModToolFont.Families[0], 40);
            LoadMod.Parent = LandingBackground;
            DeleteMod.Font = new Font(ModToolFont.Families[0], 40);
            DeleteMod.Parent = LandingBackground;
            LaunchGame.Font = new Font(ModToolFont.Families[0], 40);
            LaunchGame.Parent = LandingBackground;
            VersionText.Font = new Font(ModToolFont.Families[1], 15);
            VersionText.Parent = LandingBackground;
            Title2.Font = new Font(ModToolFont.Families[1], 20);
            Title2.Parent = LandingBackground;
            Title3.Font = new Font(ModToolFont.Families[1], 20);
            Title3.Parent = LandingBackground;

            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;
            
            //Try free some resources
            try { GC.Collect(); GC.WaitForPendingFinalizers(); } catch { }
            try { Landing CreateModScreen = (Landing)Application.OpenForms["Landing"]; CreateModScreen.Close(); } catch { }
            try { Filemanager_ExportMod ExportModScreen = (Filemanager_ExportMod)Application.OpenForms["Filemanager_ExportMod"]; ExportModScreen.Close(); } catch { }
            try { Filemanager_ImportMod ImportModScreen = (Filemanager_ImportMod)Application.OpenForms["Filemanager_ImportMod"]; ImportModScreen.Close(); } catch { }
            try { Filemanager_ResetMod ResetModScreen = (Filemanager_ResetMod)Application.OpenForms["Filemanager_ResetMod"]; ResetModScreen.Close(); } catch { }
        }

        //Make Mod
        private void MakeMod_Click(object sender, EventArgs e)
        {
            Landing loadForm = new Landing();
            loadForm.Show();
            this.Hide();
        }
        
        //Save Mod
        private void LoadMod_Click(object sender, EventArgs e)
        {
            Filemanager_ExportMod loadForm = new Filemanager_ExportMod();
            loadForm.Show();
            this.Hide();
        }

        //Load Mod
        private void DeleteMod_Click(object sender, EventArgs e)
        {
            Filemanager_ImportMod loadForm = new Filemanager_ImportMod();
            loadForm.Show();
            this.Hide();
        }

        //Delete Mod
        private void DeleteMod_Click_1(object sender, EventArgs e)
        {
            Filemanager_ResetMod loadForm = new Filemanager_ResetMod();
            loadForm.Show();
            this.Hide();
        }

        //Launch Game
        private void LaunchGame_Click(object sender, EventArgs e)
        {
            Process.Start("steam://rungameid/214490"); // STEAM ONLY. PC release is only steam anyways unless you're torrenting.
        }

        private void Title2_Click(object sender, EventArgs e)
        {
            //unused
        }

        private void Title3_Click(object sender, EventArgs e)
        {
            //unused
        }
    }
}
