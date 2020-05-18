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
using System.Xml;

namespace Alien_Isolation_Mod_Tools
{
    public partial class Landing_Main : Form
    {
        ToolSettings Settings = new ToolSettings();
        ToolPaths Paths = new ToolPaths();

        public Landing_Main()
        {
            InitializeComponent();
            this.FormClosing += new FormClosingEventHandler(CloseApplicationFully);

            //In an update the asset folder was added, but this requires the UPDATED updater to run to populate it.
            //Therefore, if we don't have this folder, we need to re-run our own updater.
            if (!Directory.Exists(Paths.GetPath(ToolPaths.Paths.FOLDER_ASSETS)))
            {
                VersionCheck.RunUpdater(false);
            }
        }

        private void CloseApplicationFully(object sender, FormClosingEventArgs e)
        {
            Application.Exit();
        }

        private void Landing_Main_Load(object sender, EventArgs e)
        {
            //Get mod tool version
            VersionText.Text = "Version " + ProductVersion;

            //Load fonts
            PrivateFontCollection ModToolFont = new PrivateFontCollection();
            ModToolFont.AddFontFile(Paths.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES) + "Isolation.ttf");
            ModToolFont.AddFontFile(Paths.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES) + "Jixellation.ttf");
            ModToolFont.AddFontFile(Paths.GetPath(ToolPaths.Paths.FOLDER_TOOL_RESOURCES) + "Nostromo.ttf");

            //Set fonts & parents
            OpenConfigTools.Font = new Font(ModToolFont.Families[0], 40);
            OpenConfigTools.Parent = LandingBackground;
            OpenContentTools.Font = new Font(ModToolFont.Families[0], 40);
            OpenContentTools.Parent = LandingBackground;
            OpenExperimentalTools.Font = new Font(ModToolFont.Families[0], 40);
            OpenExperimentalTools.Parent = LandingBackground;
            LaunchGame.Font = new Font(ModToolFont.Families[0], 40);
            LaunchGame.Parent = LandingBackground;
            VersionText.Font = new Font(ModToolFont.Families[1], 15);
            VersionText.Parent = LandingBackground;

            //Bring up to normal state
            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;
            
            //Try free some resources
            try { GC.Collect(); GC.WaitForPendingFinalizers(); } catch { }
            try { ConfigTools_Editor CreateModScreen = (ConfigTools_Editor)Application.OpenForms["Landing"]; if (CreateModScreen != null) CreateModScreen.Close(); } catch { }
            try { Filemanager_ExportMod ExportModScreen = (Filemanager_ExportMod)Application.OpenForms["Filemanager_ExportMod"]; if (ExportModScreen != null) ExportModScreen.Close(); } catch { }
            try { Filemanager_ImportMod ImportModScreen = (Filemanager_ImportMod)Application.OpenForms["Filemanager_ImportMod"]; if (ImportModScreen != null) ImportModScreen.Close(); } catch { }
            try { Filemanager_ResetMod ResetModScreen = (Filemanager_ResetMod)Application.OpenForms["Filemanager_ResetMod"]; if (ResetModScreen != null) ResetModScreen.Close(); } catch { }
        }

        //OPEN CONFIG TOOLS
        private void MakeMod_Click(object sender, EventArgs e)
        {
            Landing_ConfigTools configTools = new Landing_ConfigTools();
            configTools.Show();
            this.Hide();
        }

        //OPEN CONTENT TOOLS
        private void OpenContentTools_Click(object sender, EventArgs e)
        {
            Landing_ContentTools contentTools = new Landing_ContentTools();
            contentTools.Show();
            this.Hide();
        }

        //OPEN EXPERIMENTAL TOOLS
        private void OpenExperimentalTools_Click(object sender, EventArgs e)
        {
            Landing_ExperimentalTools experimentalTools = new Landing_ExperimentalTools();
            experimentalTools.Show();
            this.Hide();
        }

        //LAUNCH GAME
        private void LaunchGame_Click(object sender, EventArgs e)
        {
            Landing_OpenGame launchGame = new Landing_OpenGame();
            launchGame.Show();
        }
    }
}
