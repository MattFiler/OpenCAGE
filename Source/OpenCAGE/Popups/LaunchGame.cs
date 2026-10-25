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
        bool _revertingKeyPicker;

        //Key names the runtime utils ASI understands (see OpenCAGE.RuntimeUtils/Config.cpp), offered for its hot reload and
        //unlock mouse keys. Not F10: Windows sends it to the game as a system key (the one that opens a window's menu), so a
        //helper on it never reacts. A key saved before that is no longer offered falls back to the feature's default (HelperKey).
        internal static readonly string[] HelperKeys = new string[]
        {
            "INSERT", "DELETE", "HOME", "END", "PAGEUP", "PAGEDOWN",
            "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F11", "F12",
        };
        internal const string DefaultHotReloadKey = "INSERT";
        internal const string DefaultUnlockMouseKey = "F4";
        internal const string DefaultPossessKey = "F6";
        internal const string DefaultDebugMenuKey = "F7";

        /* Keys the Debug Menu's free camera uses: F8 turns it on and off and F9 takes a screenshot (unless CinematicCameraKey and
           CinematicScreenshotKey in the runtime utils config name others), the rest act while it flies. The game leaves any of
           them another helper has to that helper, so the camera loses what that key does. */
        internal static readonly string[] CinematicCameraKeys = { "F8", "F9", "F1", "F2", "F3", "HOME", "DELETE", "END", "PAGEUP", "PAGEDOWN" };

        //Frame rates Cap FPS offers (the runtime utils ASI's CapFps)
        internal static readonly string[] CapFpsRates = new string[] { "30", "60", "90", "120" };
        internal const string DefaultCapFpsRate = "60";

        //Shipped beside OpenCAGE.exe: resolved against its folder (Beside), since file dialogs move the current directory
        internal const string RuntimeUtilsFolder = "runtimeutils";
        private static string Beside(string relative) => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relative);

        /* The injector of Cinematic Tools, the separate camera tool an older OpenCAGE started beside the game (the Debug Menu's
           Cinematic group has its camera now). One an older launch left waiting for its game would put them into the next game
           that starts, so it is closed with the game before every launch. */
        internal static readonly string[] OldInjectorProcesses = new string[] { "CinematicTools", "CinematicToolsInjector" };

        /* This install's game, and any old Cinematic Tools injector, closed (another install's AI.exe holds none of the files a
           launch writes). Returns what was running ("AI.exe (path)", names). */
        internal static List<string> CloseGameAndOldInjector()
        {
            return EditorUtils.CloseAI(new List<string>(OldInjectorProcesses), thisInstallOnly: true);
        }

        /* The runtime utils ASI (the scripting helpers, Possess Characters, the Debug Menu and Live Link) supports the Steam, GOG
           and Epic Games Store builds of the game (see OpenCAGE.RuntimeUtils/GameAddresses.h) */
        internal static bool RuntimeUtilsSupports(PatchManager.Platform platform)
        {
            return platform == PatchManager.Platform.STEAM || platform == PatchManager.Platform.GOG || platform == PatchManager.Platform.EPIC_GAMES_STORE;
        }

        /* The scripting helpers are the runtime utils ASI */
        internal static bool ScriptingHelpersAvailable()
        {
            return RuntimeUtilsSupports(Singleton.Platform) && Directory.Exists(Beside(RuntimeUtilsFolder));
        }

        //Every scripting helper's on/off setting: the runtime utils options beside Possess Characters and the Debug Menu (Debug
        //entities, Hot reload and Live Link under Debug tools; Cap FPS and Unlock mouse under Game options)
        internal static readonly string[] HelperSettings = new string[]
        {
            Settings.ScriptingHelpersHotReload, Settings.ScriptingHelpersUnlockMouse, Settings.ScriptingHelpersCapFps,
            Settings.DebugEntities, Settings.ScriptingHelpersLiveLink,
        };

        /* Don't Write Saves as a launch applies it: a Debug Menu feature (the Debug Menu keeps the game from writing its saves),
           so only with the Debug Menu on */
        internal static bool DontWriteSavesOn()
        {
            return SettingsManager.GetBool(Settings.DebugMenu) && SettingsManager.GetBool(Settings.DontWriteSaves);
        }

        /* The Error Log as a launch applies it: the game relays its script errors over Live Link, so only with Live Link on */
        internal static bool ErrorLogOn()
        {
            return SettingsManager.GetBool(Settings.ScriptingHelpersLiveLink) && SettingsManager.GetBool(Settings.ErrorLog);
        }

        public LaunchGame()
        {
            InitializeComponent();
            OpenCAGE.Popups.Base.BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/launch-game/");
            Theming.ThemeManager.ApplyToForm(this);

            //Close this install's game down before we do anything (another install's AI.exe holds none of these files)
            CloseGameAndOldInjector();

            //Every AI.exe patch as its option says, as launch_game writes them (a verified or replaced AI.exe has lost them; the
            //current-gen patch is on unless switched off), rather than the recommended set, which forced the current-gen one on.
            //The patches of options this window no longer has go off; the title screen skip is written with the level at launch.
            MCP.McpLevelTools.ApplyPatchSettings(Singleton.Platform, null);
            PatchManager.UpdateLevelListInPackages(Singleton.Platform, Singleton.PathToAI);

            /* The boxes as the settings already have them: their handlers don't act on it (they'd write each setting back, and the
               patch boxes and the UI tweaks rewrote AI.exe and parsed and rewrote UI.PAK - over a second each time the window
               opened with the UI tweaks on). Launching writes all of that as the settings say. */
            _applyingExternalSettings = true;
            enableHotReload.Checked = SettingsManager.GetBool(Settings.ScriptingHelpersHotReload);
            enableUnlockMouse.Checked = SettingsManager.GetBool(Settings.ScriptingHelpersUnlockMouse);
            enableCapFps.Checked = SettingsManager.GetBool(Settings.ScriptingHelpersCapFps);
            enableDebugEntities.Checked = SettingsManager.GetBool(Settings.DebugEntities);
            enableLiveLink.Checked = SettingsManager.GetBool(Settings.ScriptingHelpersLiveLink);
            enableErrorLog.Checked = SettingsManager.GetBool(Settings.ErrorLog);
            hotReloadKey.Items.AddRange(HelperKeys);
            hotReloadKey.SelectedIndex = Array.IndexOf(HelperKeys, HotReloadKey());
            unlockMouseKey.Items.AddRange(HelperKeys);
            unlockMouseKey.SelectedIndex = Array.IndexOf(HelperKeys, UnlockMouseKey());
            capFpsRate.Items.AddRange(CapFpsRates);
            capFpsRate.SelectedIndex = Array.IndexOf(CapFpsRates, CapFpsRate());
            enablePossess.Checked = SettingsManager.GetBool(Settings.PossessCharacters);
            possessKey.Items.AddRange(HelperKeys);
            possessKey.SelectedIndex = Array.IndexOf(HelperKeys, PossessKey());
            enableDebugMenu.Checked = SettingsManager.GetBool(Settings.DebugMenu);
            debugMenuKey.Items.AddRange(HelperKeys);
            debugMenuKey.SelectedIndex = Array.IndexOf(HelperKeys, DebugMenuKey());
            dontWriteSaves.Checked = SettingsManager.GetBool(Settings.DontWriteSaves);
            disableUI.Checked = SettingsManager.GetBool(Settings.HudDisabled);
            patchCurrentGen.Checked = SettingsManager.GetBool(Settings.PatchCurrentGen, true);
            renderConstantAmbient.Checked = SettingsManager.GetBool(Settings.RenderConstantAmbient);
            debugCheckpoints.Checked = SettingsManager.GetBool(Settings.UiModPauseMenu);
            levelSelect.Checked = SettingsManager.GetBool(Settings.UiModNewFrontendMenu);
            debugLoadingScreen.Checked = SettingsManager.GetBool(Settings.UiModLoadingScreen);
            quitToMenuOnDeath.Checked = SettingsManager.GetBool(Settings.UiModGameOverMenu);
            _applyingExternalSettings = false;

            _scriptingHelpersAvailable = ScriptingHelpersAvailable();
            foreach (CheckBox helper in new CheckBox[] { enableHotReload, enableUnlockMouse, enableCapFps, enableDebugEntities, enableLiveLink })
                helper.Enabled = _scriptingHelpersAvailable;
            hotReloadKey.Enabled = _scriptingHelpersAvailable && enableHotReload.Checked;
            unlockMouseKey.Enabled = _scriptingHelpersAvailable && enableUnlockMouse.Checked;
            capFpsRate.Enabled = _scriptingHelpersAvailable && enableCapFps.Checked;
            //Possess Characters and the Debug Menu run in the runtime utils ASI too, but are not scripting helpers
            enablePossess.Enabled = _scriptingHelpersAvailable;
            possessKey.Enabled = _scriptingHelpersAvailable && enablePossess.Checked;
            enableDebugMenu.Enabled = _scriptingHelpersAvailable;
            UpdateDebugMenuOptionsEnabled();
            UpdateErrorLogEnabled();

            //The picker never offers FRONTEND: leaving this unchecked is how the game starts at its menu (and ticked, it skips the
            //title screen: the old Skip Frontend option is what this means now). With a level open in the editor it's ticked, with
            //that level picked (PopulateLevelDropdown) - that's what you'd launch into. That tick isn't saved, so with no level open
            //it's as it was last left.
            bool levelOpen = Singleton.Editor?.CompositeBrowser?.Content?.Level != null;
            _applyingExternalSettings = true;
            loadToLevel.Checked = levelOpen || SettingsManager.GetBool(Settings.LaunchToLevel);
            _applyingExternalSettings = false;
            levelList.Enabled = loadToLevel.Checked;
            EditorUtils.PopulateLevelDropdown(levelList);

            SettingsManager.SettingsChanged += OnSettingsChanged;
            FormClosed += LaunchGame_FormClosed;
        }

        private void LaunchGame_FormClosed(object sender, FormClosedEventArgs e)
        {
            SettingsManager.SettingsChanged -= OnSettingsChanged;
        }

        /* The Debug Menu's key, and Don't Write Saves (a Debug Menu feature), only while the Debug Menu is ticked: unticked, they
           are greyed out, their choices kept */
        private void UpdateDebugMenuOptionsEnabled()
        {
            bool debugMenu = _scriptingHelpersAvailable && enableDebugMenu.Checked;
            debugMenuKey.Enabled = debugMenu;
            dontWriteSaves.Enabled = debugMenu;
        }

        /* The Error Log only while Live Link is ticked (the game relays its errors over it): unticked, it is greyed out, its
           choice kept */
        private void UpdateErrorLogEnabled()
        {
            enableErrorLog.Enabled = _scriptingHelpersAvailable && enableLiveLink.Checked;
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
                        case Settings.ScriptingHelpersHotReload:
                            enableHotReload.Checked = SettingsManager.GetBool(Settings.ScriptingHelpersHotReload);
                            break;
                        case Settings.ScriptingHelpersHotReloadKey:
                            hotReloadKey.SelectedIndex = Array.IndexOf(HelperKeys, HotReloadKey());
                            break;
                        case Settings.ScriptingHelpersUnlockMouse:
                            enableUnlockMouse.Checked = SettingsManager.GetBool(Settings.ScriptingHelpersUnlockMouse);
                            break;
                        case Settings.ScriptingHelpersUnlockMouseKey:
                            unlockMouseKey.SelectedIndex = Array.IndexOf(HelperKeys, UnlockMouseKey());
                            break;
                        case Settings.ScriptingHelpersCapFps:
                            enableCapFps.Checked = SettingsManager.GetBool(Settings.ScriptingHelpersCapFps);
                            break;
                        case Settings.ScriptingHelpersCapFpsRate:
                            capFpsRate.SelectedIndex = Array.IndexOf(CapFpsRates, CapFpsRate());
                            break;
                        case Settings.DebugEntities:
                            enableDebugEntities.Checked = SettingsManager.GetBool(Settings.DebugEntities);
                            break;
                        case Settings.ScriptingHelpersLiveLink:
                            enableLiveLink.Checked = SettingsManager.GetBool(Settings.ScriptingHelpersLiveLink);
                            break;
                        case Settings.ErrorLog:
                            enableErrorLog.Checked = SettingsManager.GetBool(Settings.ErrorLog);
                            break;
                        case Settings.PossessCharacters:
                            enablePossess.Checked = SettingsManager.GetBool(Settings.PossessCharacters);
                            break;
                        case Settings.PossessCharactersKey:
                            possessKey.SelectedIndex = Array.IndexOf(HelperKeys, PossessKey());
                            break;
                        case Settings.DebugMenu:
                            enableDebugMenu.Checked = SettingsManager.GetBool(Settings.DebugMenu);
                            break;
                        case Settings.DebugMenuKey:
                            debugMenuKey.SelectedIndex = Array.IndexOf(HelperKeys, DebugMenuKey());
                            break;
                        case Settings.DontWriteSaves:
                            dontWriteSaves.Checked = SettingsManager.GetBool(Settings.DontWriteSaves);
                            break;
                        case Settings.HudDisabled:
                            disableUI.Checked = SettingsManager.GetBool(Settings.HudDisabled);
                            break;
                        case Settings.LaunchToLevel:
                            loadToLevel.Checked = SettingsManager.GetBool(Settings.LaunchToLevel);
                            levelList.Enabled = loadToLevel.Checked;
                            break;
                        case Settings.PatchCurrentGen:
                            patchCurrentGen.Checked = SettingsManager.GetBool(Settings.PatchCurrentGen, true);
                            break;
                        case Settings.RenderConstantAmbient:
                            renderConstantAmbient.Checked = SettingsManager.GetBool(Settings.RenderConstantAmbient);
                            break;
                        case Settings.UiModPauseMenu:
                            debugCheckpoints.Checked = SettingsManager.GetBool(Settings.UiModPauseMenu);
                            break;
                        case Settings.UiModNewFrontendMenu:
                            levelSelect.Checked = SettingsManager.GetBool(Settings.UiModNewFrontendMenu);
                            break;
                        case Settings.UiModLoadingScreen:
                            debugLoadingScreen.Checked = SettingsManager.GetBool(Settings.UiModLoadingScreen);
                            break;
                        case Settings.UiModGameOverMenu:
                            quitToMenuOnDeath.Checked = SettingsManager.GetBool(Settings.UiModGameOverMenu);
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

            /* The integrity check and popup patches every launch needs, the options' patches as set (and those of options this
               window no longer has off), and the title screen skip, which goes with the level: skipped whenever the game boots
               into a level, never at the menu */
            List<string> patchFailures = MCP.McpLevelTools.ApplyPatchSettings(Singleton.Platform, !EditorUtils.IsFrontend(MapName));
            bool patchLaunch = PatchManager.PatchLaunchMode(Singleton.Platform, Singleton.PathToAI, MapName);
            if (!patchLaunch || patchFailures.Contains("file integrity check") || patchFailures.Contains("popup message") || patchFailures.Contains(MCP.McpLevelTools.SkipFrontendArg))
                MessageBox.Show("Failed to set level loading values in AI.exe!\nIs the game already open?", "Failed to patch binary.", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            PatchManager.UpdateLevelListInPackages(Singleton.Platform, Singleton.PathToAI);
            //The UI options as their settings say: checking the game's files puts the binary and UI.PAK back as shipped
            UiMovieOverrides.Sync(Singleton.PathToAI);

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
            //Two features that are on with the same key would both act on it: not launched until one has another
            string keyClash = KeyClash();
            if (keyClash != null)
            {
                MessageBox.Show(keyClash, "Key in use", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            //The game loads levels from disk: edits not saved yet are not in it (and the live link only sends what is edited
            //from when it connects), so offer to save first
            if (DirtyTracker.IsDirty && Singleton.Editor != null)
            {
                string level = Singleton.Editor.CompositeBrowser?.Content?.Level?.Name ?? "The open level";
                DialogResult answer = MessageBox.Show(
                    "\"" + level + "\" has unsaved changes. The game loads what is saved on disk, so it will not have them.\n\n" +
                    "Save the level before launching?\n\n" +
                    "(If you have added models or other content that needs building, cancel and use File > Save and Build Level first.)",
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

            string runtimeUtilsProblem = ApplyRuntimeUtils(out string keptAside);
            if (runtimeUtilsProblem != null)
                MessageBox.Show(runtimeUtilsProblem, "Runtime utils error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //Another mod's d3d11.dll was just moved aside for the loader: said once, as that mod won't run in this game
            else if (keptAside != null)
                MessageBox.Show(keptAside, "Another mod's d3d11.dll", MessageBoxButtons.OK, MessageBoxIcon.Information);

            //Work out what option was selected and launch to it - with no level chosen, the game starts at its menu
            string startingLevel = loadToLevel.Checked && levelList.SelectedIndex >= 0
                ? levelList.Items[levelList.SelectedIndex].ToString()
                : EditorUtils.FrontendLevel;
            if (!LaunchToMap(startingLevel))
                return;
            this.Close();
        }

        /* Copy/delete the runtime utils ASI as the settings ask, for a game about to be launched - it is needed if any
           scripting helper, Possess Characters or the Debug Menu is on. A game that will serve Live Link is connected to by
           itself: the Live Link button is switched on (as the editor is told). Returns what went wrong, or null. */
        internal static string ApplyRuntimeUtils() => ApplyRuntimeUtils(out _);

        /* As above. The ASI loader that runs the ASI goes in the game folder as d3d11.dll: another mod's d3d11.dll there
           (ReShade, Special K, DXVK...: anything but an Ultimate ASI Loader, which ours stands in for) is moved aside to
           d3d11.dll.opencage_backup while the runtime utils are in, and put back when they come out. With one kept aside
           already, neither is overwritten: the runtime utils stay out. keptAside is a message for the user when another mod's
           d3d11.dll was moved aside just now, else null. */
        internal static string ApplyRuntimeUtils(out string keptAside)
        {
            string rtUtilASI = Singleton.PathToAI + "\\OpenCAGE.RuntimeUtils.asi";
            string rtUtilDLL = Singleton.PathToAI + "\\d3d11.dll";
            string keptDLL = rtUtilDLL + KeptAsideSuffix;
            bool liveLink = false;
            keptAside = null;

            if (File.Exists(Singleton.PathToAI + "\\OpenCAGE_Utils.asi"))
            {
                try
                {
                    File.Delete(Singleton.PathToAI + "\\OpenCAGE_Utils.asi");
                }
                catch { }
            }

            if (RuntimeUtilsNeeded())
            {
                string blocked = MakeWayForLoader(rtUtilDLL, keptDLL, out bool movedAside);
                if (blocked != null)
                {
                    //Not installed this time: an ASI an earlier launch left goes too, so nothing runs with an old config
                    try
                    {
                        if (File.Exists(rtUtilASI)) File.Delete(rtUtilASI);
                    }
                    catch { }
                    Singleton.Editor?.OnGameLaunching(false);
                    return blocked;
                }
                if (movedAside)
                    keptAside = "The game folder had another mod's d3d11.dll (ReShade, Special K or the like). OpenCAGE's runtime utils need that name for their loader, so it has been moved to d3d11.dll" + KeptAsideSuffix + " - that mod won't run in the game while the Debug Menu, Live Link or any other option that needs the runtime utils is on.\n\n" +
                        "It is put back the next time the game is started from OpenCAGE with all of those options off.";
                try
                {
                    CopyIfChanged(Beside(RuntimeUtilsFolder + "/OpenCAGE.RuntimeUtils.asi"), rtUtilASI);
                    CopyIfChanged(Beside(RuntimeUtilsFolder + "/winmm.dll"), rtUtilDLL);
                }
                catch
                {
                    //The loader never took its place: the other mod's d3d11.dll goes straight back, and nothing runs the ASI
                    if (movedAside && !File.Exists(rtUtilDLL))
                    {
                        PutBackOtherD3D11(rtUtilDLL, keptDLL);
                        keptAside = null;
                        Singleton.Editor?.OnGameLaunching(false);
                        return "Failed to install the runtime utils.";
                    }
                    if (!File.Exists(rtUtilASI) && !File.Exists(rtUtilDLL))
                    {
                        Singleton.Editor?.OnGameLaunching(false);
                        return "Failed to install the runtime utils.";
                    }
                }
                //Written even when a copy failed and an older ASI stays: the config must be this launch's (an old one could
                //switch Live Link off, or have every helper off as an older OpenCAGE wrote it for Cinematic Tools)
                try
                {
                    WriteScriptingHelpersConfig(Singleton.PathToAI);
                }
                catch { }
                liveLink = SettingsManager.GetBool(Settings.ScriptingHelpersLiveLink);
            }
            else
            {
                try
                {
                    if (File.Exists(rtUtilASI)) File.Delete(rtUtilASI);
                }
                catch { }
                //The other mod's d3d11.dll, kept aside while they were in, goes back in place of the loader
                PutBackOtherD3D11(rtUtilDLL, keptDLL);
            }
            Singleton.Editor?.OnGameLaunching(liveLink);
            return null;
        }

        //Another mod's d3d11.dll, moved aside for the runtime utils' loader, is kept beside it under this name
        internal const string KeptAsideSuffix = ".opencage_backup";

        /* The game folder's d3d11.dll made ready for the runtime utils' loader: another mod's (anything but an ASI loader) is
           moved aside to the kept name, to go back when they come out. Nothing is overwritten: with one kept aside already, or
           when the move fails, it says why (and the runtime utils stay out); otherwise null. movedAside: moved just now. */
        private static string MakeWayForLoader(string dll, string kept, out bool movedAside)
        {
            movedAside = false;
            if (!File.Exists(dll) || IsAsiLoader(dll))
                return null;
            //The same file kept aside already (copied back rather than moved): only the copy goes
            try
            {
                if (File.Exists(kept) && new FileInfo(kept).Length == new FileInfo(dll).Length && FileHash(kept).SequenceEqual(FileHash(dll)))
                {
                    File.Delete(dll);
                    movedAside = true;
                    return null;
                }
            }
            catch { }
            if (File.Exists(kept))
                return "The game folder has another mod's d3d11.dll, and also d3d11.dll" + KeptAsideSuffix + " (another mod's d3d11.dll that OpenCAGE moved aside at an earlier launch). OpenCAGE's runtime utils need d3d11.dll for their loader and won't overwrite either file, so they were not installed for this launch.\n\n" +
                    "To use them, move one of the two out of " + Path.GetDirectoryName(dll) + ".";
            try
            {
                File.Move(dll, kept);
                movedAside = true;
                return null;
            }
            catch (Exception ex)
            {
                return "The game folder has another mod's d3d11.dll, which OpenCAGE's runtime utils need to move aside for their loader, and it could not be moved (" + ex.Message + "). The runtime utils were not installed for this launch.";
            }
        }

        /* Another mod's d3d11.dll, kept aside while the runtime utils were in, put back in place of the loader. A d3d11.dll put
           there since that is not an ASI loader is left as it is, and the kept one with it. */
        private static void PutBackOtherD3D11(string dll, string kept)
        {
            try
            {
                if (!File.Exists(kept) || (File.Exists(dll) && !IsAsiLoader(dll)))
                    return;
                if (File.Exists(dll))
                    File.Delete(dll);
                File.Move(kept, dll);
            }
            catch { } //held open (the game still running, say): put back at the next launch with them all off
        }

        /* Whether a DLL is an Ultimate ASI Loader: the one OpenCAGE ships (any build of it, an older OpenCAGE's too), or one
           another mod brought, which ours stands in for (it loads the same .asi files). Told by its contents, its version
           information or the project its debug information names - never by loading it. Anything else is another mod's. */
        internal static bool IsAsiLoader(string path)
        {
            try
            {
                string shipped = Beside(RuntimeUtilsFolder + "/winmm.dll");
                if (File.Exists(shipped) && new FileInfo(shipped).Length == new FileInfo(path).Length && FileHash(shipped).SequenceEqual(FileHash(path)))
                    return true;
            }
            catch { }
            try
            {
                FileVersionInfo version = FileVersionInfo.GetVersionInfo(path);
                if (new string[] { version.FileDescription, version.ProductName, version.InternalName, version.OriginalFilename }.Any(NamesAsiLoader))
                    return true;
            }
            catch { }
            return NamesAsiLoader(PdbPath(path));
        }

        private static bool NamesAsiLoader(string text)
        {
            return text != null && (text.IndexOf("ultimate-asi-loader", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("ultimate asi loader", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /* The PDB a DLL's debug information names (its CodeView entry), or null: read from the PE headers, not the whole file */
        private static string PdbPath(string path)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    if (stream.Length < 0x40 || reader.ReadUInt16() != 0x5A4D) //MZ
                        return null;
                    stream.Position = 0x3C;
                    long pe = reader.ReadUInt32();
                    if (pe + 24 > stream.Length)
                        return null;
                    stream.Position = pe;
                    if (reader.ReadUInt32() != 0x00004550) //PE\0\0
                        return null;
                    stream.Position = pe + 6;
                    int sections = reader.ReadUInt16();
                    stream.Position = pe + 20;
                    int optionalSize = reader.ReadUInt16();
                    long optional = pe + 24;
                    stream.Position = optional;
                    bool wide = reader.ReadUInt16() == 0x20B; //PE32+
                    stream.Position = optional + (wide ? 108 : 92);
                    if (reader.ReadUInt32() <= 6) //no debug directory
                        return null;
                    stream.Position = optional + (wide ? 112 : 96) + 6 * 8;
                    uint debugRva = reader.ReadUInt32(), debugSize = reader.ReadUInt32();
                    if (debugRva == 0 || debugSize == 0)
                        return null;
                    //The debug directory's place in the file, from the section it is in
                    long debugAt = -1;
                    for (int i = 0; i < sections && debugAt < 0; i++)
                    {
                        stream.Position = optional + optionalSize + i * 40 + 8;
                        uint virtualSize = reader.ReadUInt32(), virtualAddress = reader.ReadUInt32(), rawSize = reader.ReadUInt32(), rawAt = reader.ReadUInt32();
                        if (debugRva >= virtualAddress && debugRva < virtualAddress + Math.Max(virtualSize, rawSize))
                            debugAt = rawAt + (debugRva - virtualAddress);
                    }
                    if (debugAt < 0)
                        return null;
                    for (uint entry = 0; entry + 28 <= debugSize && entry < 28 * 32; entry += 28)
                    {
                        stream.Position = debugAt + entry + 12;
                        uint type = reader.ReadUInt32(), size = reader.ReadUInt32();
                        reader.ReadUInt32();
                        uint dataAt = reader.ReadUInt32();
                        if (type != 2 || size < 24 || dataAt + (long)size > stream.Length) //2: CodeView
                            continue;
                        stream.Position = dataAt;
                        uint signature = reader.ReadUInt32();
                        //RSDS: a GUID and an age before the name; NB10: an offset, a time stamp and an age
                        int skip = signature == 0x53445352 ? 20 : signature == 0x3031424E ? 12 : -1;
                        if (skip < 0)
                            continue;
                        stream.Position = dataAt + 4 + skip;
                        byte[] name = reader.ReadBytes((int)Math.Min(size - 4 - skip, 1024));
                        int end = Array.IndexOf(name, (byte)0);
                        return System.Text.Encoding.UTF8.GetString(name, 0, end < 0 ? name.Length : end);
                    }
                }
            }
            catch { }
            return null;
        }

        /* Whether a game started from this folder now serves the live link: the runtime utils are installed, and their
           config does not switch it off (a missing LiveLink= is on, as the ASI reads it). A game launched from OpenCAGE
           starts with what ApplyRuntimeUtils put there, so this is what the running game does. Any thread. */
        internal static bool LiveLinkInstalled()
        {
            return RuntimeUtilsSwitch("LiveLink", true);
        }

        /* A switch in the runtime utils config beside the game, as it was written for the last launch: false while the runtime
           utils are not installed, missingValue when the config does not name it */
        private static bool RuntimeUtilsSwitch(string key, bool missingValue)
        {
            try
            {
                if (!File.Exists(Singleton.PathToAI + "\\OpenCAGE.RuntimeUtils.asi"))
                    return false;
                string ini = Path.Combine(Singleton.PathToAI, "OpenCAGE.RuntimeUtils.ini");
                if (!File.Exists(ini))
                    return missingValue;
                foreach (string line in File.ReadAllLines(ini))
                {
                    int equals = line.IndexOf('=');
                    if (equals < 0 || !string.Equals(line.Substring(0, equals).Trim(), key, StringComparison.OrdinalIgnoreCase))
                        continue;
                    //Read as GetPrivateProfileInt reads it: the number it starts with, and none is 0
                    string digits = new string(line.Substring(equals + 1).Trim().TakeWhile(char.IsDigit).ToArray());
                    return long.TryParse(digits, out long value) && value != 0;
                }
                return missingValue;
            }
            catch
            {
                return false;
            }
        }

        /* Remember selected level */
        private void levelList_SelectedIndexChanged(object sender, EventArgs e)
        {
            SettingsManager.SetString(Settings.LastSelectedLevel, levelList.Items[levelList.SelectedIndex].ToString());
        }

        /* Boot straight into the chosen level past the title screen, or (unchecked) start the game at its main menu: the title
           screen skip is written with the level when the game is launched (LaunchToMap) */
        private void loadToLevel_CheckedChanged(object sender, EventArgs e)
        {
            levelList.Enabled = loadToLevel.Checked;
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.LaunchToLevel, loadToLevel.Checked);
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
            if (_applyingExternalSettings || _revertingKeyPicker || hotReloadKey.SelectedIndex < 0) return;
            if (!KeyPicked(hotReloadKey, "Hot Reload", HotReloadKey())) return;
            SettingsManager.SetString(Settings.ScriptingHelpersHotReloadKey, HelperKeys[hotReloadKey.SelectedIndex]);
        }

        /* Enable/disable the unlock mouse scripting helper: the game leaves the cursor free until its key is pressed */
        private void enableUnlockMouse_CheckedChanged(object sender, EventArgs e)
        {
            unlockMouseKey.Enabled = _scriptingHelpersAvailable && enableUnlockMouse.Checked;
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.ScriptingHelpersUnlockMouse, enableUnlockMouse.Checked);
        }

        /* Choose the unlock mouse key */
        private void unlockMouseKey_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings || _revertingKeyPicker || unlockMouseKey.SelectedIndex < 0) return;
            if (!KeyPicked(unlockMouseKey, "Unlock Mouse", UnlockMouseKey())) return;
            SettingsManager.SetString(Settings.ScriptingHelpersUnlockMouseKey, HelperKeys[unlockMouseKey.SelectedIndex]);
        }

        /* Enable/disable the cap FPS scripting helper: the game runs at most the chosen frames a second (above 360 it runs
           too fast - its clock never moves on less than 1/360 of a second a frame) */
        private void enableCapFps_CheckedChanged(object sender, EventArgs e)
        {
            capFpsRate.Enabled = _scriptingHelpersAvailable && enableCapFps.Checked;
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.ScriptingHelpersCapFps, enableCapFps.Checked);
        }

        /* Choose the frame rate cap */
        private void capFpsRate_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings || capFpsRate.SelectedIndex < 0) return;
            SettingsManager.SetString(Settings.ScriptingHelpersCapFpsRate, CapFpsRates[capFpsRate.SelectedIndex]);
        }

        /* Enable/disable Debug entities: the debug script entities work in game - DebugText, DebugTextStacking, the two markers,
           DebugGraph, DebugMenuToggle, DebugObjectMarker and DebugCaptureScreenShot (the eight runtime utils switches, together) */
        private void enableDebugEntities_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.DebugEntities, enableDebugEntities.Checked);
        }

        /* Enable/disable the live link the game serves (OpenCAGE connects to it by itself once the game is up) */
        private void enableLiveLink_CheckedChanged(object sender, EventArgs e)
        {
            UpdateErrorLogEnabled();
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.ScriptingHelpersLiveLink, enableLiveLink.Checked);
        }

        /* Enable/disable the Error Log: the game notes its script errors, and the Error Log window opens as it starts */
        private void enableErrorLog_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.ErrorLog, enableErrorLog.Checked);
        }

        /* Enable/disable Possess Characters: its key opens an in-game menu to take control of a character */
        private void enablePossess_CheckedChanged(object sender, EventArgs e)
        {
            possessKey.Enabled = _scriptingHelpersAvailable && enablePossess.Checked;
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.PossessCharacters, enablePossess.Checked);
        }

        /* Choose the key that opens the Possess Characters menu */
        private void possessKey_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings || _revertingKeyPicker || possessKey.SelectedIndex < 0) return;
            if (!KeyPicked(possessKey, "Possess Characters", PossessKey())) return;
            SettingsManager.SetString(Settings.PossessCharactersKey, HelperKeys[possessKey.SelectedIndex]);
        }

        /* Enable/disable the Debug Menu: its key opens an in-game window of debug views (characters, behaviour trees, rendering
           switches, the cinematic camera that replaced Cinematic Tools...) */
        private void enableDebugMenu_CheckedChanged(object sender, EventArgs e)
        {
            UpdateDebugMenuOptionsEnabled();
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.DebugMenu, enableDebugMenu.Checked);
        }

        /* Choose the key that opens the Debug Menu */
        private void debugMenuKey_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings || _revertingKeyPicker || debugMenuKey.SelectedIndex < 0) return;
            if (!KeyPicked(debugMenuKey, "Debug Menu", DebugMenuKey())) return;
            SettingsManager.SetString(Settings.DebugMenuKey, HelperKeys[debugMenuKey.SelectedIndex]);
        }

        /* The four features with a key (Hot Reload, Unlock Mouse, Possess Characters, the Debug Menu): their names, whether each
           is on, and their keys, as the settings say */
        private static (string Name, bool On, string Key)[] KeyedFeatures()
        {
            return new (string Name, bool On, string Key)[]
            {
                ("Hot Reload", SettingsManager.GetBool(Settings.ScriptingHelpersHotReload), HotReloadKey()),
                ("Unlock Mouse", SettingsManager.GetBool(Settings.ScriptingHelpersUnlockMouse), UnlockMouseKey()),
                ("Possess Characters", SettingsManager.GetBool(Settings.PossessCharacters), PossessKey()),
                ("Debug Menu", SettingsManager.GetBool(Settings.DebugMenu), DebugMenuKey()),
            };
        }

        /* Two features that are on with the same key, as a message naming both (null when none clash, or the runtime utils
           are not available): the game would do both when it is pressed */
        internal static string KeyClash()
        {
            if (!ScriptingHelpersAvailable()) return null;
            (string Name, bool On, string Key)[] features = KeyedFeatures();
            for (int i = 0; i < features.Length; i++)
                for (int j = i + 1; j < features.Length; j++)
                    if (features[i].On && features[j].On && features[i].Key == features[j].Key)
                        return features[i].Name + " and " + features[j].Name + " both use " + features[i].Key + ": the game would do both when it is pressed.\n\nPick another key for one of them.";
            return null;
        }

        /* A key just picked for one of the four features with a key: refused when another of them that is on has it already
           (the picker goes back to its last key), and warned about while the Debug Menu is ticked when its free camera uses it
           (the game leaves that key to the helper, so the camera loses what it does). Returns whether the key is kept. */
        private bool KeyPicked(ComboBox picker, string feature, string previousKey)
        {
            string key = HelperKeys[picker.SelectedIndex];
            if (key == previousKey) return true;

            foreach ((string Name, bool On, string Key) other in KeyedFeatures())
            {
                if (other.Name == feature || !other.On || other.Key != key)
                    continue;
                MessageBox.Show(key + " is already the " + other.Name + " key.\n\nPick another key for " + feature + ", or give " + other.Name + " another key first.", "Key in use", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _revertingKeyPicker = true;
                try
                {
                    picker.SelectedIndex = Array.IndexOf(HelperKeys, previousKey);
                }
                finally
                {
                    _revertingKeyPicker = false;
                }
                return false;
            }

            if (enableDebugMenu.Checked && CinematicCameraKeys.Contains(key))
            {
                string use;
                if (key == "F8") use = "F8 turns the Debug Menu's free camera on and off (unless CinematicCameraKey in OpenCAGE.RuntimeUtils.ini names another key)";
                else if (key == "F9") use = "F9 takes the Debug Menu's screenshots (unless CinematicScreenshotKey in OpenCAGE.RuntimeUtils.ini names another key)";
                else use = key + " is one of the keys the Debug Menu's free camera uses while it flies";
                MessageBox.Show(use + ".\n\nWith " + feature + " on " + key + " too, the game leaves the key to " + feature + " and the free camera goes without it.", "Free camera key", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return true;
        }

        /* Enable/disable Don't Write Saves: with the Debug Menu on, the game writes nothing to the player's saved games while it
           runs (the Debug Menu can switch it in game too) */
        private void dontWriteSaves_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.DontWriteSaves, dontWriteSaves.Checked);
        }

        /* Whether the runtime utils ASI is needed at all: for any scripting helper (Live Link among them, which the ASI serves),
           Possess Characters or the Debug Menu */
        internal static bool RuntimeUtilsNeeded()
        {
            return ScriptingHelpersAvailable() && (HelperSettings.Any(o => SettingsManager.GetBool(o)) || SettingsManager.GetBool(Settings.PossessCharacters)
                || SettingsManager.GetBool(Settings.DebugMenu));
        }

        /* Whether a launch runs any scripting helper */
        internal static bool AnyHelperOn()
        {
            return ScriptingHelpersAvailable() && HelperSettings.Any(o => SettingsManager.GetBool(o));
        }

        /* The hot reload, unlock mouse and possess keys as set, or their defaults when the setting names none of the keys offered */
        internal static string HotReloadKey() => HelperKey(Settings.ScriptingHelpersHotReloadKey, DefaultHotReloadKey);
        internal static string UnlockMouseKey() => HelperKey(Settings.ScriptingHelpersUnlockMouseKey, DefaultUnlockMouseKey);
        internal static string PossessKey() => HelperKey(Settings.PossessCharactersKey, DefaultPossessKey);
        internal static string DebugMenuKey() => HelperKey(Settings.DebugMenuKey, DefaultDebugMenuKey);
        private static string HelperKey(string setting, string fallback)
        {
            string key = SettingsManager.GetString(setting, fallback);
            return Array.IndexOf(HelperKeys, key) >= 0 ? key : fallback;
        }

        /* The cap FPS frame rate as set, or the default when the setting names none of the rates offered */
        internal static string CapFpsRate()
        {
            string rate = SettingsManager.GetString(Settings.ScriptingHelpersCapFpsRate, DefaultCapFpsRate);
            return Array.IndexOf(CapFpsRates, rate) >= 0 ? rate : DefaultCapFpsRate;
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

        /* Write the config file the runtime utils ASI reads on startup, next to the game executable. The keys listed here are
           rewritten for this launch, and CinematicTools= (an older OpenCAGE's) and LiveLinkPort= (OpenCAGE only connects on its
           own port) are dropped; any other [RuntimeUtils] key someone set by hand (D3DDebugLayer, ExtraShadowLights,
           CinematicCameraKey, LoadAllZones...) is kept. */
        private static void WriteScriptingHelpersConfig(string pathToAI)
        {
            string debugEntities = SettingsManager.GetBool(Settings.DebugEntities) ? "1" : "0";
            string[] lines = new string[]
            {
                "[RuntimeUtils]",
                "HotReload=" + (SettingsManager.GetBool(Settings.ScriptingHelpersHotReload) ? "1" : "0"),
                "HotReloadKey=" + HotReloadKey(),
                //The game leaves the cursor free until this key is pressed in its window
                "UnlockMouse=" + (SettingsManager.GetBool(Settings.ScriptingHelpersUnlockMouse) ? "1" : "0"),
                "UnlockMouseKey=" + UnlockMouseKey(),
                //The most frames a second the game runs at (0 = no cap)
                "CapFps=" + (SettingsManager.GetBool(Settings.ScriptingHelpersCapFps) ? CapFpsRate() : "0"),
                //Debug entities: every kind of debug script entity works in game, all together (with or without the Debug Menu)
                "DebugText=" + debugEntities,
                "DebugTextStacking=" + debugEntities,
                "DebugEnvironmentMarker=" + debugEntities,
                "DebugPositionMarker=" + debugEntities,
                "DebugGraph=" + debugEntities,
                "DebugMenuToggle=" + debugEntities,
                "DebugObjectMarker=" + debugEntities,
                "DebugCaptureScreenShot=" + debugEntities,
                //The live link server (127.0.0.1 only) runs only when Live Link is ticked
                "LiveLink=" + (SettingsManager.GetBool(Settings.ScriptingHelpersLiveLink) ? "1" : "0"),
                //"OpenCAGE Live Link" in the game's top-right corner while connected (Options > Misc > Hide Live Link Label In-Game)
                "LiveLinkLabel=" + (SettingsManager.GetBool(Settings.HideLiveLinkLabel) ? "0" : "1"),
                //The Error Log: the game notes its script errors for OpenCAGE to read over Live Link (the Debug Menu does anyway)
                "ScriptErrors=" + (ErrorLogOn() ? "1" : "0"),
                //Possess Characters (a launch option, not a scripting helper): its key opens the in-game menu of characters
                "Possess=" + (SettingsManager.GetBool(Settings.PossessCharacters) ? "1" : "0"),
                "PossessKey=" + PossessKey(),
                //The Debug Menu (a launch option too): its key opens the in-game window of debug views
                "DebugMenu=" + (SettingsManager.GetBool(Settings.DebugMenu) ? "1" : "0"),
                "DebugMenuKey=" + DebugMenuKey(),
                //The Debug Menu's Don't Write Saves as it starts (it can be switched in game): the game writes no saves while it is on
                "DontWriteSaves=" + (DontWriteSavesOn() ? "1" : "0"),
            };
            string path = Path.Combine(pathToAI, "OpenCAGE.RuntimeUtils.ini");
            HashSet<string> managed = new HashSet<string>(lines.Skip(1).Select(l => l.Substring(0, l.IndexOf('='))), StringComparer.OrdinalIgnoreCase) { "CinematicTools", "LiveLinkPort" };
            List<string> kept = new List<string>();
            try
            {
                if (File.Exists(path))
                {
                    bool inSection = false;
                    foreach (string raw in File.ReadAllLines(path))
                    {
                        string line = raw.Trim();
                        if (line.StartsWith("[")) { inSection = string.Equals(line.Trim('[', ']').Trim(), "RuntimeUtils", StringComparison.OrdinalIgnoreCase); continue; }
                        int equals = line.IndexOf('=');
                        if (!inSection || equals <= 0 || line.StartsWith(";")) continue;
                        if (managed.Contains(line.Substring(0, equals).Trim())) continue;
                        kept.Add(line);
                    }
                }
            }
            catch { } //an unreadable old file still gets this launch's keys
            File.WriteAllLines(path, lines.Concat(kept));
        }

        /* Enable/disable in-game HUD */
        private void disableUI_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            SettingsManager.SetBool(Settings.HudDisabled, disableUI.Checked);
            if (!PatchManager.PatchNoUIFlag(Singleton.Platform, Singleton.PathToAI, disableUI.Checked))
                MessageBox.Show("Failed to set HUD disabled option.\nIs Alien: Isolation open?", "Couldn't write!", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        /* UI tweaks: OpenCAGE's own pause menu (debug checkpoints), main menu (level selection), loading screen and game-over
           menu beside the game's in UI.PAK (UiMovieOverrides) */
        private void debugCheckpoints_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            UpdateUI(Settings.UiModPauseMenu, debugCheckpoints.Checked);
        }
        private void levelSelect_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            UpdateUI(Settings.UiModNewFrontendMenu, levelSelect.Checked);
        }
        private void debugLoadingScreen_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            UpdateUI(Settings.UiModLoadingScreen, debugLoadingScreen.Checked);
        }
        private void quitToMenuOnDeath_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings) return;
            UpdateUI(Settings.UiModGameOverMenu, quitToMenuOnDeath.Checked);
        }
        private void UpdateUI(string setting, bool modded)
        {
            if (_applyingExternalSettings) return;
            //Menus an older OpenCAGE wrote over the game's put back first (in the file and what the Mod Manager keeps of it), then
            //UI.PAK read as it is now: launching syncs it too, so a copy read earlier could be out of date
            UiMovieOverrides.PutBackOldMenus(Singleton.PathToAI);
            PAK2 uiPAK = new PAK2(Singleton.PathToAI + "/DATA/UI.PAK");
            string problem = ApplyUIMod(uiPAK, setting, modded);
            if (problem != null)
                MessageBox.Show("The option couldn't be " + (modded ? "turned on" : "turned off") + " completely: " + problem + ".\n\nIt will be tried again when the game is next launched from OpenCAGE.",
                    "UI option", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /* Put one UI option's movie in UI.PAK beside the game's own and point the game at it (or take it out and point the game
           back - see UiMovieOverrides), write UI.PAK if that changed it, and remember the choice. Returns what went wrong, or null. */
        internal static string ApplyUIMod(PAK2 uiPAK, string setting, bool modded)
        {
            UiMovieOverrides.Movie movie = UiMovieOverrides.Find(setting);
            string problem = UiMovieOverrides.Apply(uiPAK, Singleton.PathToAI, movie, modded, out bool pakChanged);
            if (pakChanged)
            {
                Modding.ModServices.CaptureBeforeWrite(uiPAK.Filepath, usersEdit: false);
                if (!uiPAK.Save())
                    problem = problem ?? "UI.PAK could not be written (is the game open?)";
            }
            SettingsManager.SetBool(setting, modded);
            return problem;
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
