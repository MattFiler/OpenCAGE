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
            EnsureBool(Settings.SavePakAndBin, true);
            EnsureBool(Settings.PopulateAllPinsOnCreateNode, true);
            EnsureBool(Settings.OptionToDeleteEntityWithNode, true);
            EnsureBool(Settings.AskBeforeDeletingNode, true);

            EnsureBool(Settings.ShowShortGuids, false);
            EnsureBool(Settings.CompNameOnlyOpt, false);
            EnsureBool(Settings.EnableFileBrowser, false);
            EnsureBool(Settings.KeepUsesWindowOpen, false);
            EnsureBool(Settings.CompileInstances, false);
            EnsureBool(Settings.LaunchGameWhenSaved, false);
            EnsureBool(Settings.ShowGamePlatform, false);
            EnsureBool(Settings.ShowCameraPosition, false);
            EnsureBool(Settings.RenderWireframe, false);
            EnsureBool(Settings.HideNestedScriptEntities, false);
            EnsureBool(Settings.ResetRenderFilters, false);

            EnsureInteger(Settings.LevelViewerDeepSelectMode, 0);
            EnsureInteger(Settings.LevelViewerGizmoMode, 0);

            if (!SettingsManager.IsSet(Settings.NumericStep) || SettingsManager.GetFloat(Settings.NumericStep, -1.0f) == -1.0f)
                SettingsManager.SetFloat(Settings.NumericStep, 0.1f);
            if (!SettingsManager.IsSet(Settings.NumericStepRot) || SettingsManager.GetFloat(Settings.NumericStepRot, -1.0f) == -1.0f)
                SettingsManager.SetFloat(Settings.NumericStepRot, 1.0f);

            if (!SettingsManager.IsSet(Settings.TransformGridSnap))
                SettingsManager.SetFloat(Settings.TransformGridSnap, 0f);
            if (!SettingsManager.IsSet(Settings.RotationSnapDegrees))
                SettingsManager.SetFloat(Settings.RotationSnapDegrees, 0f);

            EnsureNodeColours();
        }

        static void EnsureBool(string key, bool value)
        {
            if (!SettingsManager.IsSet(key))
                SettingsManager.SetBool(key, value);
        }

        static void EnsureInteger(string key, int value)
        {
            if (!SettingsManager.IsSet(key))
                SettingsManager.SetInteger(key, value);
        }

        static void EnsureNodeColour(string key, Color colour)
        {
            if (!SettingsManager.IsSet(key))
                SettingsManager.SetInteger(key, colour.ToArgb());
        }

        static void EnsureNodeColours()
        {
            EnsureNodeColour(Settings.NodeColour_FunctionNode, Color.FromArgb(30, 144, 255));
            EnsureNodeColour(Settings.NodeColour_FunctionNodeBottom, Color.FromArgb(10, 109, 157));
            EnsureNodeColour(Settings.NodeColour_FunctionText, Color.White);

            EnsureNodeColour(Settings.NodeColour_AliasNode, Color.FromArgb(255, 114, 30));
            EnsureNodeColour(Settings.NodeColour_AliasNodeBottom, Color.FromArgb(196, 76, 29));
            EnsureNodeColour(Settings.NodeColour_AliasText, Color.White);

            EnsureNodeColour(Settings.NodeColour_ProxyNode, Color.FromArgb(35, 196, 22));
            EnsureNodeColour(Settings.NodeColour_ProxyNodeBottom, Color.FromArgb(9, 153, 72));
            EnsureNodeColour(Settings.NodeColour_ProxyText, Color.White);

            EnsureNodeColour(Settings.NodeColour_InstanceNode, Color.FromArgb(195, 30, 255));
            EnsureNodeColour(Settings.NodeColour_InstanceNodeBottom, Color.FromArgb(118, 10, 157));
            EnsureNodeColour(Settings.NodeColour_InstanceText, Color.White);

            EnsureNodeColour(Settings.NodeColour_VariableNode, Color.Red);
            EnsureNodeColour(Settings.NodeColour_VariableText, Color.White);
        }
    }
}
