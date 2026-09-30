using CATHODE;
using CathodeLib;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

//Imported from LaunchGame tool

namespace OpenCAGE
{
    public partial class LaunchGame : Form
    {
        bool _applyingExternalSettings;
        bool _scriptingHelpersAvailable;

        //Key names the runtime utils ASI understands (see RuntimeUtils/Config.cpp)
        internal static readonly string[] HotReloadKeys = new string[]
        {
            "INSERT", "DELETE", "HOME", "END", "PAGEUP", "PAGEDOWN",
            "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
        };
        internal const string DefaultHotReloadKey = "INSERT";

        //Shipped beside OpenCAGE.exe: resolved against its folder (Beside), since file dialogs move the current directory
        internal const string CinematicToolsDll = "cinematictools/CT_AlienIsolation.dll";
        internal const string CinematicToolsInjector = "cinematictools/CinematicTools.exe";
        internal const string RuntimeUtilsFolder = "runtimeutils";
        private static string Beside(string relative) => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relative);

        /* The scripting helpers are the runtime utils ASI, which only supports the Steam build */
        internal static bool ScriptingHelpersAvailable()
        {
            return Singleton.Platform == PatchManager.Platform.STEAM && Directory.Exists(Beside(RuntimeUtilsFolder));
        }

        internal static bool CinematicToolsAvailable()
        {
            return Singleton.Platform == PatchManager.Platform.STEAM && File.Exists(Beside(CinematicToolsDll)) && File.Exists(Beside(CinematicToolsInjector));
        }

        public LaunchGame()
        {
            InitializeComponent();
            Theming.ThemeManager.ApplyToForm(this);

            //Close the game down before we do anything
            EditorUtils.CloseAI(new List<string>(new string[] { "CinematicTools", "CinematicToolsInjector" }));

            PatchManager.PerformRecommendedPatches(Singleton.Platform, Singleton.PathToAI);

            enableCinematicTools.Checked = SettingsManager.GetBool(Settings.CinematicTools);
            enableHotReload.Checked = SettingsManager.GetBool(Settings.ScriptingHelpersHotReload);
            enableDebugText.Checked = SettingsManager.GetBool(Settings.ScriptingHelpersDebugText);
            enableDebugTextStacking.Checked = SettingsManager.GetBool(Settings.ScriptingHelpersDebugTextStacking);
            enableDebugEnvironmentMarker.Checked = SettingsManager.GetBool(Settings.ScriptingHelpersDebugEnvironmentMarker);
            enableDebugPositionMarker.Checked = SettingsManager.GetBool(Settings.ScriptingHelpersDebugPositionMarker);
            enableLiveLink.Checked = SettingsManager.GetBool(Settings.ScriptingHelpersLiveLink);
            hotReloadKey.Items.AddRange(HotReloadKeys);
            hotReloadKey.SelectedIndex = HotReloadKeyIndex(SettingsManager.GetString(Settings.ScriptingHelpersHotReloadKey, DefaultHotReloadKey));
            disableUI.Checked = SettingsManager.GetBool(Settings.HudDisabled);
            skipFrontend.Checked = SettingsManager.GetBool(Settings.SkipFrontend);
            enableUIPerf.Checked = SettingsManager.GetBool(Settings.UiEnabledUiPerf);
            enableMemReplayLogs.Checked = SettingsManager.GetBool(Settings.MemReplayLogs);
            patchCurrentGen.Checked = SettingsManager.GetBool(Settings.PatchCurrentGen);
            renderConstantAmbient.Checked = SettingsManager.GetBool(Settings.RenderConstantAmbient);
            UIMOD_DebugCheckpoints.Checked = SettingsManager.GetBool(Settings.UiModPauseMenu);
            UIMOD_MapName.Checked = SettingsManager.GetBool(Settings.UiModLoadingScreen);
            UIMOD_MapSelection.Checked = SettingsManager.GetBool(Settings.UiModNewFrontendMenu);
            UIMOD_ReturnFrontend.Checked = SettingsManager.GetBool(Settings.UiModGameOverMenu);

            enableCinematicTools.Enabled = CinematicToolsAvailable();
            _scriptingHelpersAvailable = ScriptingHelpersAvailable();
            enableHotReload.Enabled = _scriptingHelpersAvailable;
            enableDebugText.Enabled = _scriptingHelpersAvailable;
            enableDebugTextStacking.Enabled = _scriptingHelpersAvailable;
            enableDebugEnvironmentMarker.Enabled = _scriptingHelpersAvailable;
            enableDebugPositionMarker.Enabled = _scriptingHelpersAvailable;
            enableLiveLink.Enabled = _scriptingHelpersAvailable;
            hotReloadKey.Enabled = _scriptingHelpersAvailable && enableHotReload.Checked;

            //The picker never offers FRONTEND: leaving this unchecked is how the game starts at its menu
            loadToLevel.Checked = SettingsManager.GetBool(Settings.LaunchToLevel);
            levelList.Enabled = loadToLevel.Checked;
            EditorUtils.PopulateLevelDropdown(levelList);

            SettingsManager.SettingsChanged += OnSettingsChanged;
            FormClosed += LaunchGame_FormClosed;
        }

        private void LaunchGame_FormClosed(object sender, FormClosedEventArgs e)
        {
            SettingsManager.SettingsChanged -= OnSettingsChanged;
        }

        private void OnSettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            if (!e.ExternalChange || e.ChangedKeys.Count == 0 || IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ApplyExternalSettings(e.ChangedKeys)));
                return;
            }

            ApplyExternalSettings(e.ChangedKeys);
        }

        private void ApplyExternalSettings(IReadOnlyList<string> changedKeys)
        {
            _applyingExternalSettings = true;
            try
            {
                foreach (string key in changedKeys)
                {
                    switch (key)
                    {
                        case Settings.CinematicTools:
                            enableCinematicTools.Checked = SettingsManager.GetBool(Settings.CinematicTools);
                            break;
                        case Settings.ScriptingHelpersHotReload:
                            enableHotReload.Checked = SettingsManager.GetBool(Settings.ScriptingHelpersHotReload);
                            break;
                        case Settings.ScriptingHelpersHotReloadKey:
                            hotReloadKey.SelectedIndex = HotReloadKeyIndex(SettingsManager.GetString(Settings.ScriptingHelpersHotReloadKey, DefaultHotReloadKey));
                            break;
                        case Settings.ScriptingHelpersDebugText:
                            enableDebugText.Checked = SettingsManager.GetBool(Settings.ScriptingHelpersDebugText);
                            break;
                        case Settings.ScriptingHelpersDebugTextStacking:
                            enableDebugTextStacking.Checked = SettingsManager.GetBool(Settings.ScriptingHelpersDebugTextStacking);
                            break;
                        case Settings.ScriptingHelpersDebugEnvironmentMarker:
                            enableDebugEnvironmentMarker.Checked = SettingsManager.GetBool(Settings.ScriptingHelpersDebugEnvironmentMarker);
                            break;
                        case Settings.ScriptingHelpersDebugPositionMarker:
                            enableDebugPositionMarker.Checked = SettingsManager.GetBool(Settings.ScriptingHelpersDebugPositionMarker);
                            break;
                        case Settings.ScriptingHelpersLiveLink:
                            enableLiveLink.Checked = SettingsManager.GetBool(Settings.ScriptingHelpersLiveLink);
                            break;
                        case Settings.HudDisabled:
                            disableUI.Checked = SettingsManager.GetBool(Settings.HudDisabled);
                            break;
                        case Settings.SkipFrontend:
                            skipFrontend.Checked = SettingsManager.GetBool(Settings.SkipFrontend);
                            break;
                        case Settings.LaunchToLevel:
                            loadToLevel.Checked = SettingsManager.GetBool(Settings.LaunchToLevel);
                            levelList.Enabled = loadToLevel.Checked;
                            break;
                        case Settings.UiEnabledUiPerf:
                            enableUIPerf.Checked = SettingsManager.GetBool(Settings.UiEnabledUiPerf);
                            break;
                        case Settings.MemReplayLogs:
                            enableMemReplayLogs.Checked = SettingsManager.GetBool(Settings.MemReplayLogs);
                            break;
                        case Settings.PatchCurrentGen:
                            patchCurrentGen.Checked = SettingsManager.GetBool(Settings.PatchCurrentGen);
                            break;
                        case Settings.RenderConstantAmbient:
                            renderConstantAmbient.Checked = SettingsManager.GetBool(Settings.RenderConstantAmbient);
                            break;
                        case Settings.UiModPauseMenu:
                            UIMOD_DebugCheckpoints.Checked = SettingsManager.GetBool(Settings.UiModPauseMenu);
                            break;
                        case Settings.UiModLoadingScreen:
                            UIMOD_MapName.Checked = SettingsManager.GetBool(Settings.UiModLoadingScreen);
                            break;
                        case Settings.UiModNewFrontendMenu:
                            UIMOD_MapSelection.Checked = SettingsManager.GetBool(Settings.UiModNewFrontendMenu);
                            break;
                        case Settings.UiModGameOverMenu:
                            UIMOD_ReturnFrontend.Checked = SettingsManager.GetBool(Settings.UiModGameOverMenu);
                            break;
                    }
                }
            }
            finally
            {
                _applyingExternalSettings = false;
            }
        }

        /* Load game with given map name */
        private bool LaunchToMap(string MapName)
        {
            //Longer names overrun the byte run the launch patch writes into and corrupt the string after it
            if (MapName.Length > PatchManager.MaxLaunchMapNameLength)
            {
                MessageBox.Show("The name of the selected level is too long to launch into!\nLevel paths can be at most " + PatchManager.MaxLaunchMapNameLength + " characters (e.g. Production/" + new string('X', PatchManager.MaxLaunchMapNameLength - "Production/".Length) + ").\nPlease rename it.", "Level name too long.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            bool patchLaunch = PatchManager.PatchLaunchMode(Singleton.Platform, Singleton.PathToAI, MapName);
            bool patchIntegrity = PatchManager.PatchFileIntegrityCheck(Singleton.Platform, Singleton.PathToAI);
            bool patchMsg = PatchManager.PatchPopupMessage(Singleton.Platform, Singleton.PathToAI);
            if (!patchLaunch || !patchIntegrity || !patchMsg)
                MessageBox.Show("Failed to set level loading values in AI.exe!\nIs the game already open?", "Failed to patch binary.", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            PatchManager.UpdateLevelListInPackages(Singleton.Platform, Singleton.PathToAI);

            //Start game process 
            if (Singleton.Platform == PatchManager.Platform.STEAM)
            {
                Process.Start("steam://rungameid/214490");
            }
            else
            {
                ProcessStartInfo alienProcess = new ProcessStartInfo();
                alienProcess.WorkingDirectory = Singleton.PathToAI;
                alienProcess.FileName = Singleton.PathToAI + "/AI.exe";
                Process.Start(alienProcess);
            }
            Steam.UnlockAchievement(Steam.Achievements.LAUNCHED_GAME);
            return true;
        }

        /* Load game from GUI map selection */
        private void LaunchGame_Click(object sender, EventArgs e)
        {
            //The game loads levels from disk: edits not saved yet are not in it (and the live link only sends what is edited
            //from when it connects), so offer to save first
            if (DirtyTracker.IsDirty && Singleton.Editor != null)
            {
                string level = Singleton.Editor.CompositeBrowser?.Content?.Level?.Name ?? "The open level";
                DialogResult answer = MessageBox.Show(
                    "\"" + level + "\" has unsaved changes. The game loads what is saved on disk, so it will not have them.\n\n" +
                    "Save the level before launching?\n\n" +
                    "(If you have added models or other content that needs building, cancel and use File > Save & Build Level first.)",
                    "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (answer == DialogResult.Cancel)
                    return;
                if (answer == DialogResult.Yes)
                {
                    Singleton.Editor.SaveLevel(false, successMsg: false, allowLaunchGame: false);
                    //Not saved after all (a backup running, or the save failed - it said why): not launched either
                    if (DirtyTracker.IsDirty)
                        return;
                }
            }

            string runtimeUtilsProblem = ApplyRuntimeUtils();
            if (runtimeUtilsProblem != null)
                MessageBox.Show(runtimeUtilsProblem, "Runtime utils error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            //Work out what option was selected and launch to it - with no level chosen, the game starts at its menu
            string startingLevel = loadToLevel.Checked && levelList.SelectedIndex >= 0
                ? levelList.Items[levelList.SelectedIndex].ToString()
                : EditorUtils.FrontendLevel;
            if (!LaunchToMap(startingLevel))
                return;

            //Enable Cinematic Tools if requested
            if (SettingsManager.GetBool(Settings.CinematicTools))
            {
                string cinematicToolsProblem = StartCinematicTools(out string caption);
                if (cinematicToolsProblem != null)
                    MessageBox.Show(cinematicToolsProblem, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            this.Close();
        }

        /* Copy/delete the runtime utils ASI as the settings ask, for a game about to be launched - it is needed if any
           scripting helper is on, or for Cinematic Tools, which rely on it to stream zones around the free camera. A game
           that will serve the live link is connected to by itself: the Live Link button is switched on (as the editor is told). Returns
           what went wrong, or null. */
        internal static string ApplyRuntimeUtils()
        {
            string rtUtilASI = Singleton.PathToAI + "OpenCAGE_Utils.asi";
            string rtUtilDLL = Singleton.PathToAI + "d3d11.dll";
            bool liveLink = false;
            if (RuntimeUtilsNeeded())
            {
                try
                {
                    CopyIfChanged(Beside(RuntimeUtilsFolder + "/OpenCAGE_Utils.asi"), rtUtilASI);
                    CopyIfChanged(Beside(RuntimeUtilsFolder + "/winmm.dll"), rtUtilDLL);
                    WriteScriptingHelpersConfig(Singleton.PathToAI);
                }
                catch
                {
                    if (!File.Exists(rtUtilASI) && !File.Exists(rtUtilDLL))
                    {
                        Singleton.Editor?.OnGameLaunching(false);
                        return "Failed to install the runtime utils.";
                    }
                }
                liveLink = SettingsManager.GetBool(Settings.ScriptingHelpersLiveLink);
            }
            else
            {
                try
                {
                    if (File.Exists(rtUtilASI)) File.Delete(rtUtilASI);
                }
                catch { }
            }
            Singleton.Editor?.OnGameLaunching(liveLink);
            return null;
        }

        /* Whether a game started from this folder now serves the live link: the runtime utils are installed, and their
           config does not switch it off (a missing LiveLink= is on, as the ASI reads it). A game launched from OpenCAGE
           starts with what ApplyRuntimeUtils put there, so this is what the running game does. Any thread. */
        internal static bool LiveLinkInstalled()
        {
            try
            {
                if (!File.Exists(Singleton.PathToAI + "OpenCAGE_Utils.asi"))
                    return false;
                string ini = Path.Combine(Singleton.PathToAI, "OpenCAGE_Utils.ini");
                if (!File.Exists(ini))
                    return true;
                foreach (string line in File.ReadAllLines(ini))
                {
                    int equals = line.IndexOf('=');
                    if (equals < 0 || !string.Equals(line.Substring(0, equals).Trim(), "LiveLink", StringComparison.OrdinalIgnoreCase))
                        continue;
                    //Read as GetPrivateProfileInt reads it: the number it starts with, and none is 0
                    string digits = new string(line.Substring(equals + 1).Trim().TakeWhile(char.IsDigit).ToArray());
                    return long.TryParse(digits, out long value) && value != 0;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /* Start the Cinematic Tools injector for a game just launched. Returns why it did not start (with a caption for it), or null. */
        internal static string StartCinematicTools(out string caption)
        {
            caption = null;
            if (!File.Exists(Beside(CinematicToolsInjector)))
            {
                Debug.Log("Cinematic Tools", "Executable doesn't exist!");
                caption = "Cinematic Tools missing";
                return "Cinematic Tools injector was not found at:\n" + Path.GetFullPath(Beside(CinematicToolsInjector)) +
                    "\n\nThe game was still launched. Reinstall/update OpenCAGE to restore cinematictools/CinematicTools.exe.";
            }
            try
            {
                Process.Start(new ProcessStartInfo
                    {
                        FileName = Beside(CinematicToolsInjector),
                        WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                        Arguments = "-CinematicToolsDLL=\"" + CinematicToolsDll + "\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                );
            }
            catch (Win32Exception ex)
            {
                Debug.Log("Cinematic Tools", "Failed to start injector: " + ex.Message);
                // Windows Defender / antivirus often flags the injector as a false positive
                string message = ex.Message ?? "";
                if (message.IndexOf("virus", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    message.IndexOf("potentially unwanted", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    caption = "Cinematic Tools blocked";
                    return "Windows blocked Cinematic Tools from launching because antivirus flagged CinematicTools.exe as potentially unwanted software.\n\n" +
                        "The game was still launched. To use Cinematic Tools, allow or exclude cinematictools\\CinematicTools.exe in Windows Security (or your antivirus), then try again.";
                }
                caption = "Cinematic Tools error";
                return "Failed to start Cinematic Tools.\n" + ex.Message;
            }
            return null;
        }

        /* Remember selected level */
        private void levelList_SelectedIndexChanged(object sender, EventArgs e)
        {
            SettingsManager.SetString(Settings.LastSelectedLevel, levelList.Items[levelList.SelectedIndex].ToString());
        }

        /* Boot straight into the chosen level, or (unchecked) start the game at its menu */
        private void loadToLevel_CheckedChanged(object sender, EventArgs e)
        {
            levelList.Enabled = loadToLevel.Checked;
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.LaunchToLevel, loadToLevel.Checked);
        }

        /* Enable/disable the Cinematic Tools */
        private void enableCinematicTools_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.CinematicTools, enableCinematicTools.Checked);
        }

        /* Enable/disable cUI rendering for UI perf stats (Cathode debug render) */ 
        private void enableUIPerf_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.UiEnabledUiPerf, enableUIPerf.Checked);
            if (!PatchManager.PatchUIPerfFlag(Singleton.Platform, Singleton.PathToAI, enableUIPerf.Checked))
                MessageBox.Show("Failed to set cUI UI perf option.\nIs Alien: Isolation open?", "Couldn't write!", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /* Enable/disable Mem_Replay_Logs */
        private void enableMemReplayLogs_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.MemReplayLogs, enableMemReplayLogs.Checked);
            if (!PatchManager.PatchMemReplayLogFlag(Singleton.Platform, Singleton.PathToAI, enableMemReplayLogs.Checked))
                MessageBox.Show("Failed to set memory logging option.\nIs Alien: Isolation open?", "Couldn't write!", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /* Enable/disable the hot reload scripting helper */
        private void enableHotReload_CheckedChanged(object sender, EventArgs e)
        {
            hotReloadKey.Enabled = _scriptingHelpersAvailable && enableHotReload.Checked;
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.ScriptingHelpersHotReload, enableHotReload.Checked);
        }

        /* Choose the hot reload key */
        private void hotReloadKey_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings || hotReloadKey.SelectedIndex < 0) return;
            SettingsManager.SetString(Settings.ScriptingHelpersHotReloadKey, HotReloadKeys[hotReloadKey.SelectedIndex]);
        }

        /* Enable/disable the debug entity scripting helpers */
        private void enableDebugText_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.ScriptingHelpersDebugText, enableDebugText.Checked);
        }
        private void enableDebugTextStacking_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.ScriptingHelpersDebugTextStacking, enableDebugTextStacking.Checked);
        }
        private void enableDebugEnvironmentMarker_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.ScriptingHelpersDebugEnvironmentMarker, enableDebugEnvironmentMarker.Checked);
        }
        private void enableDebugPositionMarker_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.ScriptingHelpersDebugPositionMarker, enableDebugPositionMarker.Checked);
        }

        /* Enable/disable the live link the game serves (OpenCAGE connects to it by itself once the game is up) */
        private void enableLiveLink_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.ScriptingHelpersLiveLink, enableLiveLink.Checked);
        }

        /* Whether the runtime utils ASI is needed at all */
        internal static bool RuntimeUtilsNeeded()
        {
            return ScriptingHelpersAvailable() && (
                SettingsManager.GetBool(Settings.CinematicTools)
                || SettingsManager.GetBool(Settings.ScriptingHelpersLiveLink) //the live link is served by the ASI
                || SettingsManager.GetBool(Settings.ScriptingHelpersHotReload)
                || SettingsManager.GetBool(Settings.ScriptingHelpersDebugText)
                || SettingsManager.GetBool(Settings.ScriptingHelpersDebugTextStacking)
                || SettingsManager.GetBool(Settings.ScriptingHelpersDebugEnvironmentMarker)
                || SettingsManager.GetBool(Settings.ScriptingHelpersDebugPositionMarker));
        }

        /* Index of a key name in the hot reload key list, falling back to the default */
        internal static int HotReloadKeyIndex(string key)
        {
            int index = Array.IndexOf(HotReloadKeys, key);
            return index >= 0 ? index : Array.IndexOf(HotReloadKeys, DefaultHotReloadKey);
        }

        /* Copy a file into the game folder unless an identical copy is already there, so the shipped
           version of the runtime utils always wins without touching a file the game may still hold open */
        private static void CopyIfChanged(string source, string destination)
        {
            if (File.Exists(destination) && FileHash(source).SequenceEqual(FileHash(destination)))
                return;
            File.Copy(source, destination, true);
        }

        private static byte[] FileHash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return sha.ComputeHash(stream);
        }

        /* Write the config file the runtime utils ASI reads on startup, next to the game executable */
        private static void WriteScriptingHelpersConfig(string pathToAI)
        {
            string[] lines = new string[]
            {
                "[RuntimeUtils]",
                "HotReload=" + (SettingsManager.GetBool(Settings.ScriptingHelpersHotReload) ? "1" : "0"),
                "HotReloadKey=" + HotReloadKeys[HotReloadKeyIndex(SettingsManager.GetString(Settings.ScriptingHelpersHotReloadKey, DefaultHotReloadKey))],
                "DebugText=" + (SettingsManager.GetBool(Settings.ScriptingHelpersDebugText) ? "1" : "0"),
                "DebugTextStacking=" + (SettingsManager.GetBool(Settings.ScriptingHelpersDebugTextStacking) ? "1" : "0"),
                "DebugEnvironmentMarker=" + (SettingsManager.GetBool(Settings.ScriptingHelpersDebugEnvironmentMarker) ? "1" : "0"),
                "DebugPositionMarker=" + (SettingsManager.GetBool(Settings.ScriptingHelpersDebugPositionMarker) ? "1" : "0"),
                //The live link server (127.0.0.1 only) runs only when Enable Live Link is ticked
                "LiveLink=" + (SettingsManager.GetBool(Settings.ScriptingHelpersLiveLink) ? "1" : "0"),
            };
            File.WriteAllLines(Path.Combine(pathToAI, "OpenCAGE_Utils.ini"), lines);
        }

        /* Enable/disable in-game HUD */
        private void disableUI_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.HudDisabled, disableUI.Checked);
            if (!PatchManager.PatchNoUIFlag(Singleton.Platform, Singleton.PathToAI, disableUI.Checked))
                MessageBox.Show("Failed to set HUD disabled option.\nIs Alien: Isolation open?", "Couldn't write!", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /* Skip Frontend (WARNING: Causes issues when returning to main menu - duh) */
        private void skipFrontend_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.SkipFrontend, skipFrontend.Checked);
            if (!PatchManager.PatchSkipFrontendFlag(Singleton.Platform, Singleton.PathToAI, skipFrontend.Checked))
                MessageBox.Show("Failed to set skip frontend option.\nIs Alien: Isolation open?", "Couldn't write!", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /* Current-gen script optimiser patch */
        private void patchCurrentGen_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.PatchCurrentGen, patchCurrentGen.Checked);
            if (!PatchManager.DisableCurrentGenOptimisations(Singleton.Platform, Singleton.PathToAI, patchCurrentGen.Checked))
                MessageBox.Show("Failed to set optimisation patch option.\nIs Alien: Isolation open?", "Couldn't write!", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /* Render constant ambient */
        private void renderConstantAmbient_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.RenderConstantAmbient, renderConstantAmbient.Checked);
            if (!PatchManager.PatchRenderConstantAmbientFlag(Singleton.Platform, Singleton.PathToAI, renderConstantAmbient.Checked))
                MessageBox.Show("Failed to set constant ambient patch option.\nIs Alien: Isolation open?", "Couldn't write!", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /* UI Modifications */
        PAK2 uiPAK = null;
        private void UIMOD_DebugCheckpoints_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            UpdateUI("PAUSEMENU", UIMOD_DebugCheckpoints.Checked);
        }
        private void UIMOD_MapName_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            UpdateUI("LOADINGSCREEN", UIMOD_MapName.Checked);
        }
        private void UIMOD_MapSelection_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            UpdateUI("NEWFRONTENDMENU", UIMOD_MapSelection.Checked);
        }
        private void UIMOD_ReturnFrontend_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            UpdateUI("GAMEOVERMENU", UIMOD_ReturnFrontend.Checked);
        }
        private void UpdateUI(string file, bool modded)
        {
            if (_applyingExternalSettings) return;
            if (uiPAK == null)
                uiPAK = new PAK2(Singleton.PathToAI + "/DATA/UI.PAK");
            ApplyUIMod(uiPAK, file, modded);
        }

        /* Swap one UI mod's movie (or the original) into UI.PAK, write it, and remember the choice */
        internal static void ApplyUIMod(PAK2 uiPAK, string file, bool modded)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryReader reader = new BinaryReader(stream))
            {
                GetResourceStream((modded) ? "UI_Mods/" + file + "_MOD.GFX" : "UI_Mods/" + file + ".GFX").CopyTo(stream);
                reader.BaseStream.Position = 0;
                PAK2.File pakFile = uiPAK.Entries.FirstOrDefault(o => o.Filename == "DATA/UI/" + file + ".GFX");
                if (pakFile != null)
                    pakFile.Content = reader.ReadBytes((int)reader.BaseStream.Length);
            }

            Modding.ModServices.CaptureBeforeWrite(uiPAK.Filepath);
            uiPAK.Save();
            SettingsManager.SetBool(Settings.UiMod(file), modded);
        }
        protected static Stream GetResourceStream(string resourcePath)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            List<string> resourceNames = new List<string>(assembly.GetManifestResourceNames());

            resourcePath = resourcePath.Replace(@"/", ".");
            resourcePath = resourceNames.FirstOrDefault(r => r.Contains(resourcePath));

            if (resourcePath == null)
                throw new FileNotFoundException("Resource not found");

            return assembly.GetManifestResourceStream(resourcePath);
        }
    }
}
