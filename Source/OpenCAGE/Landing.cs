using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class Landing : Form
    {
        List<Process> _subprocesses = new List<Process>();
        Settings _settingsUI;
        LaunchGame _launchGameUI;

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
            if (SettingsManager.GetString("META_GameVersion") == "" || SettingsManager.GetString("META_GameVersion") == "UNKNOWN")
            {
                if      (File.Exists(SettingsManager.GetString("PATH_GameRoot") + "/STEAM_API.DLL")) SettingsManager.SetString("META_GameVersion", GameBuild.STEAM.ToString());
                else if (File.Exists(SettingsManager.GetString("PATH_GameRoot") + "/EOSSDK-Win32-Shipping.dll")) SettingsManager.SetString("META_GameVersion", GameBuild.EPIC_GAMES_STORE.ToString());
                else if (File.Exists(SettingsManager.GetString("PATH_GameRoot") + "/GALAXY.DLL")) SettingsManager.SetString("META_GameVersion", GameBuild.GOG.ToString());
                else if (File.Exists(SettingsManager.GetString("PATH_GameRoot") + "/MicrosoftGame.config")) SettingsManager.SetString("META_GameVersion", GameBuild.WINDOWS_STORE.ToString());
                
                else SettingsManager.SetString("META_GameVersion", GameBuild.UNKNOWN.ToString()); //a new release 0.o
            }

            //If we haven't already, copy the debug_font into the game's directory
            string debugFontDirectory = SettingsManager.GetString("PATH_GameRoot") + "/DATA/debug_font/";
            if (!Directory.Exists(debugFontDirectory))
            {
                Directory.CreateDirectory(debugFontDirectory);
                File.WriteAllBytes(debugFontDirectory + "mini_font.fnt", Properties.Resources.mini_font);
                File.WriteAllBytes(debugFontDirectory + "mini_font_outlined.fnt", Properties.Resources.mini_font_outlined);
                File.WriteAllBytes(debugFontDirectory + "new_font.fnt", Properties.Resources.new_font);
                File.WriteAllBytes(debugFontDirectory + "tiny_font.fnt", Properties.Resources.tiny_font);
            }

            //Version tracking analytics
            AnalyticsManager.LogAppStartup(ProductVersion);

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
        private void DoUpdate(bool showUpdateMsg = true)
        {
            for (int i = 0; i < _subprocesses.Count; i++)
                if (_subprocesses[i] != null && !_subprocesses[i].HasExited)
                    _subprocesses[i].Kill();

            if (showUpdateMsg) MessageBox.Show("A new version of OpenCAGE is available.", "OpenCAGE Updater", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
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
            DebugText.Text = "";
            if (SettingsManager.GetBool("CONFIG_UseStagingBranch")) DebugText.Text += " [staging]";
            else DebugText.Text +=                                                    "  [master]";
            //if (SettingsManager.GetBool("CONFIG_SkipUpdateCheck")) DebugText.Text += " [no_update]";
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
            settingsBtn.Parent = LandingBackground;
            LaunchGame.Font = FontManager.GetFont(0, 40);
            LaunchGame.Parent = LandingBackground;
            VersionText.Font = FontManager.GetFont(1, 15);
            VersionText.Parent = LandingBackground;
            DebugText.Font = FontManager.GetFont(1, 15);
            DebugText.Parent = LandingBackground;

            //Try patch the game binary to circumvent file hashing (do we really wanna do this on start? might trigger antivirus warnings)
            PatchManager.PatchFileIntegrityCheck();
            PatchManager.UpdateLevelListInPackages();
        }

        /* App launch buttons */
        private void OpenConfigTools_Click(object sender, EventArgs e)
        {
            _subprocesses.Add(StartProcess("configeditor/AlienConfigEditor.exe"));
        }
        private void OpenContentTools_Click(object sender, EventArgs e)
        {
            _subprocesses.Add(StartProcess("asseteditor/AlienPAK.exe"));
        }
        private void OpenScriptingTools_Click(object sender, EventArgs e)
        {
            _subprocesses.Add(StartProcess("scripteditor/CathodeEditorGUI.exe"));
        }
        private void OpenBehaviourTreeTools_Click(object sender, EventArgs e)
        {
            _subprocesses.Add(StartProcess("legendplugin/BehaviourTreeTool.exe"));
        }

        /* Start a process from the remote directory */
        private Process StartProcess(string path)
        {
            string pathToExe = SettingsManager.GetString("PATH_GameRoot") + "/DATA/MODTOOLS/REMOTE_ASSETS/" + path;
            if (!File.Exists(pathToExe))
            {
                DoUpdate();
                return null;
            }
            
            Process process = new Process();
            process.Exited += Process_Exited;
            process.StartInfo.FileName = pathToExe;
            process.StartInfo.Arguments = "-opencage " + SettingsManager.GetString("PATH_GameRoot");
            process.StartInfo.WorkingDirectory = pathToExe.Substring(0, pathToExe.Length - Path.GetFileName(pathToExe).Length);
            process.Start();
            
            return process;
        }
        private void Process_Exited(object sender, EventArgs e)
        {
            this.BringToFront();
            this.Focus();
        }

        /* Open Launch Game UI */
        private void LaunchGame_Click(object sender, EventArgs e)
        {
            if (_launchGameUI != null)
            {
                _launchGameUI.BringToFront();
                _launchGameUI.Focus();
                return;
            }
            
            _launchGameUI = new LaunchGame();
            _launchGameUI.FormClosed += LaunchGame_FormClosed;
            _launchGameUI.Show();
        }
        private void LaunchGame_FormClosed(object sender, FormClosedEventArgs e)
        {
            _launchGameUI = null;
            this.BringToFront();
            this.Focus();
        }

        /* Open Settings UI */
        private void settingsBtn_Click(object sender, EventArgs e)
        {
            if (_settingsUI != null)
            {
                _settingsUI.BringToFront();
                _settingsUI.Focus();
                return;
            }
            
            _settingsUI = new Settings();
            _settingsUI.FormClosed += SettingsMenu_FormClosed;
            _settingsUI.Show();
        }
        private void SettingsMenu_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (_settingsUI.DidActuallyUpdateSettings)
            {
                DoUpdate(false);
                return;
            }
            _settingsUI = null;
            this.BringToFront();
            this.Focus();
        }
    }

    public enum GameBuild
    {
        STEAM,
        EPIC_GAMES_STORE,
        GOG,
        WINDOWS_STORE, //UNSUPPORTED
        UNKNOWN
    }
}
