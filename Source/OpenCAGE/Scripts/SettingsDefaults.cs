using OpenCAGE.UnityConnection;
using System.Drawing;

namespace OpenCAGE
{
    public static class SettingsDefaults
    {
        public static void EnsureApplied()
        {
            EnsureBool(Settings.RuntimeUtilsOpt, false);
            EnsureBool(Settings.HighlightAliases, true);
            EnsureBool(Settings.HighlightProxies, true);
            EnsureBool(Settings.ShowTexOpt, true);
            EnsureBool(Settings.ShowSavedMsgOpt, true);
            EnsureBool(Settings.CompositeImportOpenAfter, false);
            EnsureBool(Settings.PromptSaveOnClose, false);
            EnsureBool(Settings.PopulateAllPinsOnCreateNode, true);
            EnsureBool(Settings.FocusCanvasOnNewNode, false);
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
            EnsureBool(Settings.ScriptingHelpersHotReload, false);
            EnsureBool(Settings.ScriptingHelpersDebugText, false);
            EnsureBool(Settings.ScriptingHelpersDebugTextStacking, false);
            EnsureBool(Settings.ScriptingHelpersDebugEnvironmentMarker, false);
            EnsureBool(Settings.ScriptingHelpersDebugPositionMarker, false);
            EnsureBool(Settings.ShowGamePlatform, false);
            EnsureBool(Settings.ShowCameraPosition, false);
            EnsureBool(Settings.FocusOnSelected, false);
            EnsureBool(Settings.FixCameraToSelected, false);
            EnsureBool(Settings.RenderWireframe, false);
            EnsureBool(Settings.RenderGalaxy, true);
            EnsureInteger(Settings.LevelViewerHighlightMode, (int)LevelViewerHighlightMode.Green);
            EnsureBool(Settings.HideNestedScriptEntities, false);
            EnsureBool(Settings.ResetRenderFilters, false);
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

        static void EnsureInteger(string key, int value)
        {
            if (!SettingsManager.IsSet(key))
                SettingsManager.SetInteger(key, value);
        }


    }
}
