using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

            //Try and find on Steam install path
            if (SettingsManager.IsSteamworks && SettingsManager.GetString("PATH_GameRoot") == "")
            {
                if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "/../Alien Isolation/AI.exe"))
                {
                    SettingsManager.SetString("PATH_GameRoot", Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory + "/../Alien Isolation/"));
                    SettingsManager.SetBool("PATH_IsRemote", true);
                }
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

            //If the user is using offline mode, make sure REMOTE_ASSETS is up to date with our offline Assets folder
            if (SettingsManager.IsOfflineMode)
            {
                string _gameAssetPath = SettingsManager.GetString("PATH_GameRoot") + "\\DATA\\MODTOOLS\\REMOTE_ASSETS\\";
                string _offlineAssetPath = AppDomain.CurrentDomain.BaseDirectory + "\\Assets\\";

                if (!Directory.Exists(_offlineAssetPath) || !File.Exists(_offlineAssetPath + "assets.manifest"))
                {
                    MessageBox.Show("If using offline mode, please supply the Assets folder found on GitHub in the same folder as the OpenCAGE executable.", "Offline mode error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                    return;
                }

                if (!File.Exists(_gameAssetPath + "assets.manifest"))
                {
                    Directory.CreateDirectory(_gameAssetPath);

                    List<string> files = Directory.GetFiles(_offlineAssetPath, "*.archive", SearchOption.AllDirectories).ToList();
                    files.Add(_offlineAssetPath + "assets.manifest");
                    for (int i = 0; i < files.Count; i++)
                        File.Copy(files[i], _gameAssetPath + Path.GetFileName(files[i]));
                }
                else
                {
                    JObject offlineManifest = JObject.Parse(File.ReadAllText(_offlineAssetPath + "assets.manifest"));
                    JObject gameManifest = JObject.Parse(File.ReadAllText(_gameAssetPath + "assets.manifest"));
                    foreach (JObject offlineArchive in offlineManifest["archives"])
                    {
                        bool upToDate = false;
                        foreach (JObject gameArchive in gameManifest["archives"])
                        {
                            if (gameArchive["name"].Value<string>() != offlineArchive["name"].Value<string>()) 
                                continue;

                            if (gameArchive.ContainsKey("hash") && offlineArchive.ContainsKey("hash"))
                                upToDate = (gameArchive["hash"].Value<string>() == offlineArchive["hash"].Value<string>());
                            else
                                upToDate = (gameArchive["size"].Value<int>() == offlineArchive["size"].Value<int>());
                            break;
                        }
                        if (upToDate) 
                            continue;

                        try
                        {
                            if (File.Exists(_gameAssetPath + offlineArchive["name"] + ".archive"))
                                File.Delete(_gameAssetPath + offlineArchive["name"] + ".archive");

                            File.Copy(_offlineAssetPath + offlineArchive["name"] + ".archive", _gameAssetPath + offlineArchive["name"] + ".archive");
                        }
                        catch { }
                    }
                    File.Copy(_offlineAssetPath + "assets.manifest", _gameAssetPath + "assets.manifest", true);
                }

                string[] archives = Directory.GetFiles(_gameAssetPath, "*.archive", SearchOption.TopDirectoryOnly);
                if (archives.Length != 0)
                {
                    List<Process> allProcesses = new List<Process>(Process.GetProcessesByName("Unity"));
                    List<string> processNames = new List<string>(Directory.GetFiles(_gameAssetPath, "*.exe", SearchOption.AllDirectories));
                    for (int i = 0; i < processNames.Count; i++) allProcesses.AddRange(Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processNames[i])));
                    for (int i = 0; i < allProcesses.Count; i++) try { allProcesses[i].Kill(); } catch { }

                    foreach (string archive in archives)
                    {
                        //Try delete the base directory to clear out old assets (if it exists)
                        string directory = _gameAssetPath + "/" + Path.GetFileNameWithoutExtension(archive);
                        try { Directory.Delete(directory, true); } catch { }
                        try { Directory.Delete(directory, true); } catch { }

                        //Extract out the new assets
                        BinaryReader reader = new BinaryReader(File.OpenRead(archive));
                        int file_count = reader.ReadInt32();
                        for (int i = 0; i < file_count; i++)
                        {
                            string fileName = reader.ReadString();
                            int fileLength = reader.ReadInt32();
                            byte[] fileContent = reader.ReadBytes(fileLength);

                            Directory.CreateDirectory((_gameAssetPath + fileName).Substring(0, (_gameAssetPath + fileName).Length - Path.GetFileName(_gameAssetPath + fileName).Length));
                            if (File.Exists(_gameAssetPath + fileName)) File.Delete(_gameAssetPath + fileName);
                            File.WriteAllBytes(_gameAssetPath + fileName, fileContent);
                        }
                        reader.Close();
                        File.Delete(archive);
                    }
                }
            }

            //If we haven't been opened in the AI folder, make sure to update the path for our config
            if (SettingsManager.GetBool("PATH_IsRemote"))
            {
                SettingsManager.FlipToRemotePath();
            }

            //Check for update, and launch updater if one is available
            if (!SettingsManager.IsOfflineMode && UpdateManager.IsUpdateAvailable(ProductVersion))
            {
#if !DEBUG
                MessageBox.Show("A new version of OpenCAGE is available!", "OpenCAGE Updater", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                DoUpdate();
                return;
#endif
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
            if (SettingsManager.IsOfflineMode)
                SettingsManager.SetString("CONFIG_RemoteBranch", "master");

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
            if (SettingsManager.IsOfflineMode)
                return;

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
