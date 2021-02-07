using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class Landing : Form
    {
        public Landing()
        {
            InitializeComponent();

            //Check for update, and launch updater if one is available
            if (UpdateManager.IsUpdateAvailable(ProductVersion))
            {
                DoUpdate();
                return;
            }
            if (File.Exists("OpenCAGE Updater.exe")) File.Delete("OpenCAGE Updater.exe");

            //We're on the right version - check our path to A:I
            while (SettingsManager.GetString("PATH_GameRoot") == "") LocateGameExe();

            //Clear up from legacy OpenCAGE releases
            if (File.Exists("modtools_locales.ayz")) File.Delete("modtools_locales.ayz");
            if (File.Exists("modtools_settings.ayz")) File.Delete("modtools_settings.ayz");
            if (File.Exists("modtools_uimods.ayz")) File.Delete("modtools_uimods.ayz");

            //Check game version
            if (SettingsManager.GetString("META_GameVersion") == "")
            {
                if (File.Exists(SettingsManager.GetString("PATH_GameRoot") + "/STEAM_API.DLL")) SettingsManager.SetString("META_GameVersion", GameBuild.STEAM.ToString());
                else if (File.Exists(SettingsManager.GetString("PATH_GameRoot") + "/EOSSDK-Win32-Shipping.dll")) SettingsManager.SetString("META_GameVersion", GameBuild.EPIC_GAMES_STORE.ToString());
                else SettingsManager.SetString("META_GameVersion", GameBuild.UNKNOWN.ToString());
            }

            this.BringToFront();
            this.Focus();
        }
        private void LocateGameExe()
        {
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + @"\AI.exe")) SettingsManager.SetString("PATH_GameRoot", AppDomain.CurrentDomain.BaseDirectory);
            else
            {
                MessageBox.Show("Please locate your Alien: Isolation executable (AI.exe).", "OpenCAGE Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                OpenFileDialog selectGameFile = new OpenFileDialog();
                selectGameFile.Filter = "Applications (*.exe)|AI.exe";
                if (selectGameFile.ShowDialog() == DialogResult.OK) SettingsManager.SetString("PATH_GameRoot", Path.GetDirectoryName(selectGameFile.FileName));
            }
        }
        private void DoUpdate()
        {
            MessageBox.Show("A new version of OpenCAGE is available.", "OpenCAGE Updater", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            File.WriteAllBytes("OpenCAGE Updater.exe", Properties.Resources.OpenCAGE_Updater);
            Process.Start("OpenCAGE Updater.exe");
            Application.Exit();
            Environment.Exit(0);
        }

        private void Landing_Main_Load(object sender, EventArgs e)
        {
            //Show mod tool version
            VersionText.Text = "Version " + ProductVersion;

            //Show environment info
            if (SettingsManager.GetBool("CONFIG_UseStagingBranch")) DebugText.Text += "[staging] ";
            if (SettingsManager.GetBool("CONFIG_SkipUpdateCheck")) DebugText.Text += "[no_update] ";
            if (DebugText.Text.Length == 0) DebugText.Hide();

            //Set fonts & parents
            OpenConfigTools.Font = FontManager.GetFont(0, 40);
            OpenConfigTools.Parent = LandingBackground;
            OpenContentTools.Font = FontManager.GetFont(0, 40);
            OpenContentTools.Parent = LandingBackground;
            OpenScriptingTools.Font = FontManager.GetFont(0, 40);
            OpenScriptingTools.Parent = LandingBackground;
            OpenBehaviourTreeTools.Font = FontManager.GetFont(0, 40);
            OpenBehaviourTreeTools.Parent = LandingBackground;
            LaunchGame.Font = FontManager.GetFont(0, 40);
            LaunchGame.Parent = LandingBackground;
            VersionText.Font = FontManager.GetFont(1, 15);
            VersionText.Parent = LandingBackground;
            DebugText.Font = FontManager.GetFont(1, 15);
            DebugText.Parent = LandingBackground;
        }

        /* Allow the user to launch the game */
        private void LaunchGame_Click(object sender, EventArgs e)
        {
            LaunchGame startAI = new LaunchGame();
            startAI.Show();
        }

        /* Open config tool */
        private void OpenConfigTools_Click(object sender, EventArgs e)
        {
            StartProcess("configeditor/AlienConfigEditor.exe");
        }

        /* Open AlienPAK */
        private void OpenContentTools_Click(object sender, EventArgs e)
        {
            StartProcess("asseteditor/AlienPAK.exe");
        }

        /* Open Cathode scripting tool */
        private void OpenScriptingTools_Click(object sender, EventArgs e)
        {
            StartProcess("scripteditor/CathodeEditorGUI.exe");
        }

        /* Open Brainiac wrapper */
        private void OpenBehaviourTreeTools_Click(object sender, EventArgs e)
        {
            StartProcess("legendplugin/BehaviourTreeTool.exe");
        }

        /* Start a process from the remote directory */
        private void StartProcess(string path)
        {
            string pathToExe = SettingsManager.GetString("PATH_GameRoot") + "/DATA/MODTOOLS/REMOTE_ASSETS/" + path;
            if (!File.Exists(pathToExe))
            {
                DoUpdate();
                return;
            }
            Process process = new Process();
            process.Exited += Process_Exited;
            process.StartInfo.FileName = pathToExe;
            process.StartInfo.Arguments = "-opencage " + SettingsManager.GetString("PATH_GameRoot");
            process.StartInfo.WorkingDirectory = pathToExe.Substring(0, pathToExe.Length - Path.GetFileName(pathToExe).Length);
            process.Start();
        }
        private void Process_Exited(object sender, EventArgs e)
        {
            this.BringToFront();
            this.Focus();
        }
    }

    public enum GameBuild
    {
        STEAM,
        EPIC_GAMES_STORE,
        WINDOWS_STORE, //UNSUPPORTED
        UNKNOWN
    }
}
