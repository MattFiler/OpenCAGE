using OpenCAGE.UnityConnection;
using System.Drawing;
using System.Linq;

namespace OpenCAGE
{
    public static class SettingsDefaults
    {
        public static void EnsureApplied()
        {
            EnsureBool(Settings.RuntimeUtilsOpt, false);
            EnsureLiveLinkCameraMode();
            EnsureBool(Settings.LiveLinkAnimateInGame, true);
            EnsureBool(Settings.LiveLinkShowActivity, false);
            EnsureBool(Settings.HighlightAliases, true);
            EnsureBool(Settings.HighlightProxies, true);
            EnsureBool(Settings.ShowTexOpt, true);
            EnsureBool(Settings.ShowSavedMsgOpt, true);
            EnsureBool(Settings.CompositeImportOpenAfter, false);
            EnsureBool(Settings.PromptSaveOnClose, false);
            EnsureBool(Settings.AllowAiAssistants, true);
            EnsureBool(Settings.PopulateAllPinsOnCreateNode, true);
            EnsureBool(Settings.FocusCanvasOnNewNode, false);
            EnsureBool(Settings.NodeIconsWhenZoomedOut, true);
            EnsureBool(Settings.DarkMode, false);
            EnsureBool(Settings.SoundPreviewAutoPlay, false);
            EnsureBool(Settings.AskBeforeDeletingNode, false);

            EnsureBool(Settings.ShowShortGuids, false);
            EnsureBool(Settings.CompNameOnlyOpt, false);
            EnsureCompositeBrowserMode();
            EnsureBool(Settings.CompositePreviewsInTrees, false);
            EnsureInteger(Settings.CompositePreviewScale, 100);
            EnsureBool(Settings.KeepUsesWindowOpen, false);
            EnsureBool(Settings.LaunchGameWhenSaved, false);
            EnsureBool(Settings.LaunchToLevel, false);
            //Launch Game starts with the Debug Menu, Live Link, Debug entities, the current-gen script fixes, Cap FPS and Unlock
            //mouse on (Load straight into is ticked whenever a level is open - see LaunchGame)
            EnsureBool(Settings.ScriptingHelpersHotReload, false);
            EnsureBool(Settings.ScriptingHelpersUnlockMouse, true);
            EnsureBool(Settings.ScriptingHelpersCapFps, true);
            EnsureBool(Settings.ScriptingHelpersLiveLink, true);
            EnsureBool(Settings.PatchCurrentGen, true);
            EnsureCinematicToolsInDebugMenu();
            EnsureBool(Settings.DebugMenu, true);
            EnsureUiOptionKeys();
            EnsureLaunchGameRedesign();
            EnsureBool(Settings.DebugEntities, true);
            EnsureBool(Settings.ShowGamePlatform, false);
            EnsureBool(Settings.ShowCameraPosition, false);
            EnsureBool(Settings.FocusOnSelected, false);
            EnsureBool(Settings.FixCameraToSelected, false);
            EnsureBool(Settings.RenderWireframe, false);
            EnsureBool(Settings.RenderGalaxy, true);
            EnsureBool(Settings.RenderFog, false);
            EnsureBool(Settings.RenderWater, true);
            EnsureBool(Settings.RenderPlanets, true);
            EnsureBool(Settings.PlanarReflections, true);
            EnsureInteger(Settings.LevelViewerAntialiasMode, (int)LevelViewerViewportDefinitions.DefaultAntialiasMode);
            EnsureInteger(Settings.LevelViewerDrawDistance, (int)LevelViewerViewportDefinitions.DefaultDrawDistance);
            EnsureInteger(Settings.LevelViewerHighlightMode, (int)LevelViewerHighlightMode.Green);
            EnsureBool(Settings.HideNestedScriptEntities, false);
            EnsureBool(Settings.ResetRenderFilters, false);
            EnsureBool(Settings.AlwaysFullyRebuildRadiosity, false);
            EnsureInteger(Settings.CubemapCaptureSize, 0); // the size of the cubemap each capture replaces
            EnsureBool(Settings.ShowZones, false);
            EnsureBool(Settings.TransformVertexSnap, false);

            EnsureInteger(Settings.LevelViewerDeepSelectMode, 0);
            EnsureInteger(Settings.LevelViewerGizmoMode, 1); // TranslateWorld by default

            if (!SettingsManager.IsSet(Settings.NumericStep) || SettingsManager.GetFloat(Settings.NumericStep, -1.0f) == -1.0f)
                SettingsManager.SetFloat(Settings.NumericStep, 0.1f);
            if (!SettingsManager.IsSet(Settings.NumericStepRot) || SettingsManager.GetFloat(Settings.NumericStepRot, -1.0f) == -1.0f)
                SettingsManager.SetFloat(Settings.NumericStepRot, 1.0f);

            if (!SettingsManager.IsSet(Settings.TransformGridSnap))
                SettingsManager.SetFloat(Settings.TransformGridSnap, 0f);
            if (!SettingsManager.IsSet(Settings.RotationSnapDegrees))
                SettingsManager.SetFloat(Settings.RotationSnapDegrees, 0f);


        }

        static void EnsureBool(string key, bool value)
        {
            if (!SettingsManager.IsSet(key))
                SettingsManager.SetBool(key, value);
        }

        //The switch the browser mode replaced: whether the folder browser was on under the tree
        private const string LegacyFileBrowserEnabled = "FileBrowserEnabled";

        /* The browser layout used to be two switches - the folder browser on or off, and previews in it or
           not - and is one mode now. Someone who had turned the folder browser on keeps it; everyone else
           gets the default, every composite captured in one list. The old keys are left in the file. */
        static void EnsureCompositeBrowserMode()
        {
            if (SettingsManager.IsSet(Settings.CompositeBrowserMode))
                return;

            bool hadFolderBrowser = SettingsManager.GetBool(LegacyFileBrowserEnabled, false);
            CompositeBrowserModes.Set(hadFolderBrowser ? CompositeBrowserMode.TreeAndBrowser : CompositeBrowserModes.Default);
        }

        /* Launch Game's Enable Cinematic Tools became the Debug Menu, whose Cinematic group has their camera: whoever had Cinematic
           Tools on gets the Debug Menu on instead, once, and the old keys go (an older OpenCAGE then shows Cinematic Tools off).
           The Debug Menu runs beside the scripting helpers, so the rule that kept Cinematic Tools and them apart goes too. */
        static void EnsureCinematicToolsInDebugMenu()
        {
            if (SettingsManager.IsSet(Settings.LegacyCinematicTools))
            {
                if (SettingsManager.GetBool(Settings.LegacyCinematicTools))
                    SettingsManager.SetBool(Settings.DebugMenu, true);
                SettingsManager.Unset(Settings.LegacyCinematicTools);
            }
            if (SettingsManager.IsSet(Settings.LegacyCinematicToolsOrHelpers))
                SettingsManager.Unset(Settings.LegacyCinematicToolsOrHelpers);
        }

        /* The Launch Game UI options were kept under the movies' own names while ticking one wrote OpenCAGE's movie over the
           game's in UI.PAK. They're the same choice the new way (UiMovieOverrides), so each old value moves onto its new key, once,
           and the old key goes - an older OpenCAGE then shows the option off, which is true of UI.PAK once its menus are put back.
           Putting them back is UiMovieOverrides.PutBackOldMenus: it goes by what's in UI.PAK, not by these, so it also catches a
           UI.PAK an older OpenCAGE wrote with the option since turned off elsewhere. */
        static void EnsureUiOptionKeys()
        {
            (string Legacy, string Key)[] keys =
            {
                (Settings.LegacyUiModPauseMenu, Settings.RemovedUiModPauseMenu),
                (Settings.LegacyUiModLoadingScreen, Settings.UiModLoadingScreen),
                (Settings.LegacyUiModNewFrontendMenu, Settings.RemovedUiModNewFrontendMenu),
                (Settings.LegacyUiModGameOverMenu, Settings.UiModGameOverMenu),
            };
            foreach ((string legacy, string key) in keys)
            {
                if (!SettingsManager.IsSet(legacy))
                    continue;
                if (!SettingsManager.IsSet(key))
                    SettingsManager.SetBool(key, SettingsManager.GetBool(legacy));
                SettingsManager.Unset(legacy);
            }
        }

        /* Launch Game was redesigned (22 Oct 2026). Its four debug entity switches (DebugText, DebugTextStacking,
           DebugEnvironmentMarker, DebugPositionMarker) are one now, Debug entities: on for whoever had any of them on. The
           options it dropped go: Skip Frontend (Load straight into a level skips the title screen, starting at the main menu
           never does), the UI memory overlay (the Debug Menu's Performance > Memory has it), memory logging, and the debug
           checkpoints and level selection menus (the Debug Menu's Game > Tools and Game > Game flow do those). Each old key is
           removed once it is read, so this runs once per key (an older OpenCAGE then shows them off). What the dropped options
           left on in AI.exe and UI.PAK goes back off at the next launch (McpLevelTools.ApplyPatchSettings, UiMovieOverrides). */
        static void EnsureLaunchGameRedesign()
        {
            string[] debugEntities = { Settings.LegacyDebugText, Settings.LegacyDebugTextStacking, Settings.LegacyDebugEnvironmentMarker, Settings.LegacyDebugPositionMarker };
            if (!SettingsManager.IsSet(Settings.DebugEntities) && debugEntities.Any(o => SettingsManager.IsSet(o)))
                SettingsManager.SetBool(Settings.DebugEntities, debugEntities.Any(o => SettingsManager.IsSet(o) && SettingsManager.GetBool(o)));

            string[] removed = debugEntities.Concat(new[]
            {
                Settings.LegacySkipFrontend, Settings.LegacyUiMemoryOverlay, Settings.LegacyMemoryLogging,
                Settings.RemovedUiModPauseMenu, Settings.RemovedUiModNewFrontendMenu,
            }).ToArray();
            foreach (string key in removed)
                if (SettingsManager.IsSet(key))
                    SettingsManager.Unset(key);
        }

        /* The Live Link Camera menu replaced the Sync Game Camera button: on was what is now "Sync viewport camera to game".
           The old key is left in the file. */
        static void EnsureLiveLinkCameraMode()
        {
            if (!SettingsManager.IsSet(Settings.LiveLinkCameraMode))
                SettingsManager.SetInteger(Settings.LiveLinkCameraMode, SettingsManager.GetBool(Settings.LiveLinkSyncCamera) ? 1 : 0);
        }

        static void EnsureInteger(string key, int value)
        {
            if (!SettingsManager.IsSet(key))
                SettingsManager.SetInteger(key, value);
        }


    }
}
