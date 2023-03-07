using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class Landing : Form
    {
        private List<Process> _subprocesses = new List<Process>();
        private Settings _settingsUI;
        private Timer _ghPromptTimer = new Timer();

        public Landing()
        {
            InitializeComponent();
            LocateGameExe();

            //Check for update, and launch updater if one is available
            if (UpdateManager.IsUpdateAvailable(ProductVersion))
            {
                DoUpdate();
                return;
            }
            if (File.Exists("OpenCAGE Updater.exe")) File.Delete("OpenCAGE Updater.exe");

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

            //Show GitHub prompt?
            if (SettingsManager.GetInteger("LOG_UntilGHPrompt") < 3)
            {
                SettingsManager.SetInteger("LOG_UntilGHPrompt", SettingsManager.GetInteger("LOG_UntilGHPrompt") + 1);
            }
            else if (SettingsManager.GetInteger("LOG_UntilGHPrompt") == 3)
            {
                _ghPromptTimer.Interval = 10000;
                _ghPromptTimer.Tick += new EventHandler(ShowGitHubPrompt);
                _ghPromptTimer.Start();
                SettingsManager.SetInteger("LOG_UntilGHPrompt", 4);
            }

            this.BringToFront();
            this.Focus();
        }
        private void LocateGameExe()
        {
            if (SettingsManager.GetString("PATH_GameRoot") == "" && File.Exists(AppDomain.CurrentDomain.BaseDirectory + @"\AI.exe"))
            {
                SettingsManager.SetString("PATH_GameRoot", AppDomain.CurrentDomain.BaseDirectory);
            }
            if (!File.Exists(SettingsManager.GetString("PATH_GameRoot") + @"\AI.exe"))
            {
                SettingsManager.SetString("PATH_GameRoot", "");

                MessageBox.Show("Please copy your OpenCAGE executable into Alien: Isolation's game directory before continuing.", "Incorrect launch location", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                Environment.Exit(0);
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
            githubBtn.Parent = LandingBackground;
            LaunchGame.Font = FontManager.GetFont(0, 40);
            LaunchGame.Parent = LandingBackground;
            VersionText.Font = FontManager.GetFont(1, 15);
            VersionText.Parent = LandingBackground;
            DebugText.Font = FontManager.GetFont(1, 15);
            DebugText.Parent = LandingBackground;

            this.BringToFront();
            this.Focus();
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
            _subprocesses.Add(StartProcess("scripteditor/CommandsEditor.exe"));
        }
        private void OpenBehaviourTreeTools_Click(object sender, EventArgs e)
        {
            _subprocesses.Add(StartProcess("legendplugin/BehaviourTreeTool.exe"));
        }
        private void LaunchGame_Click(object sender, EventArgs e)
        {
            _subprocesses.Add(StartProcess("launchgame/LaunchGame.exe"));
        }

        /* Start a process from the remote directory */
        private Process StartProcess(string path)
        {
            string pathToExe = SettingsManager.GetString("PATH_GameRoot") + "/DATA/MODTOOLS/REMOTE_ASSETS/" + path;
            if (!File.Exists(pathToExe))
            {
                DoUpdate(false);
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

        /* Open GitHub */
        private void githubBtn_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/MattFiler/OpenCAGE");
        }

        /* GitHub prompt */
        private void ShowGitHubPrompt(object sender, EventArgs e)
        {
            _ghPromptTimer.Tick -= new EventHandler(ShowGitHubPrompt);
            _ghPromptTimer.Dispose();
            _ghPromptTimer = null;

            GitHubPrompt prompt = new GitHubPrompt();
            prompt.Show();
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
