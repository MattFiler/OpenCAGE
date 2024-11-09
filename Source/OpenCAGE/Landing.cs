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
        private LandingWPF _ui;

        public Landing()
        {
            //Make sure the path to AI is correct
            if (!File.Exists(SettingsManager.GetString("PATH_GameRoot") + @"\AI.exe"))
            {
                SettingsManager.SetString("PATH_GameRoot", "");
                SettingsManager.SetBool("PATH_IsRemote", false);
            }

            //If the path to AI hasn't been set, set it
            if (SettingsManager.GetString("PATH_GameRoot") == "")
            {
                if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + @"\AI.exe"))
                {
                    SettingsManager.SetString("PATH_GameRoot", AppDomain.CurrentDomain.BaseDirectory);
                    SettingsManager.SetBool("PATH_IsRemote", false);
                }

                MessageBox.Show("Please locate your Alien: Isolation executable (AI.exe).", "OpenCAGE Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Filter = "Applications (*.exe)|AI.exe";
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        SettingsManager.SetString("PATH_GameRoot", Path.GetDirectoryName(dialog.FileName));
                        SettingsManager.SetBool("PATH_IsRemote", true);
                    }
                    else
                    {
                        Application.Exit();
                        Environment.Exit(0);
                        return;
                    }
                }
            }

            //If we haven't been opened in the AI folder, make sure to update the path for our config
            if (SettingsManager.GetBool("PATH_IsRemote"))
            {
                SettingsManager.FlipToRemotePath();
            }

            //Check for update, and launch updater if one is available
            if (UpdateManager.IsUpdateAvailable(ProductVersion))
            {
                MessageBox.Show("A new version of OpenCAGE is available!", "OpenCAGE Updater", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
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

            //Swap legacy UseStagingBranch over to new RemoteBranch config
            if (SettingsManager.GetString("CONFIG_RemoteBranch") == "")
            {
                if (SettingsManager.GetBool("CONFIG_UseStagingBranch"))
                    SettingsManager.SetString("CONFIG_RemoteBranch", "staging");
                else
                    SettingsManager.SetString("CONFIG_RemoteBranch", "master");
            }

            InitializeComponent();
            this.BringToFront();
            this.Focus();

            _ui = (LandingWPF)elementHost1.Child;
            _ui.SetVersionInfo(ProductVersion);
            _ui.OnSettingsRequest += OpenSettingsUI;
            _ui.OnToolOpened += OnToolOpened;
            _ui.OnToolClosed += OnToolClosed;
            _ui.OnUpdateRequest += DoUpdate;
        }

        private void DoUpdate()
        {
            for (int i = 0; i < _subprocesses.Count; i++)
                if (_subprocesses[i] != null && !_subprocesses[i].HasExited)
                    _subprocesses[i].Kill();

            File.WriteAllBytes("OpenCAGE Updater.exe", Properties.Resources.OpenCAGE_Updater);
            Process.Start("OpenCAGE Updater.exe");

            Application.Exit();
            Environment.Exit(0);
        }

        /* Manage our external processes */
        private void OnToolOpened(Process process)
        {
            _subprocesses.Add(process);
        }
        private void OnToolClosed(Process process)
        {
            _subprocesses.Remove(process);
            this.BringToFront();
            this.Focus();
        }

        /* Open Settings UI */
        private void OpenSettingsUI()
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
                DoUpdate();
                return;
            }
            _settingsUI = null;
            this.BringToFront();
            this.Focus();
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
