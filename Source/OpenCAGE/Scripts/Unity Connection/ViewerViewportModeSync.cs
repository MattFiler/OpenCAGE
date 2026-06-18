using OpenCAGE.DockPanels;
using OpenCAGE.UnityConnection;
using System;

namespace OpenCAGE
{
    /// <summary>
    /// Applies viewport mode packets sent from the embedded Level Viewer (hotkey changes).
    /// </summary>
    public static class ViewerViewportModeSync
    {
        public static bool TryApply(Packet packet)
        {
            if (packet == null)
                return false;

            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return false;

            if (editor.InvokeRequired)
            {
                try
                {
                    editor.BeginInvoke(new Action(() => ApplyCore(packet)));
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.Log("Websocket", "Failed to queue viewer viewport mode on UI thread: " + ex.Message);
                    return false;
                }
            }

            return ApplyCore(packet);
        }

        private static bool ApplyCore(Packet packet)
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return false;

            LevelViewerDeepSelectMode deepSelectMode = LevelViewerViewportDefinitions.NormalizeDeepSelectMode(
                packet.deep_select_mode);
            LevelViewerGizmoMode gizmoMode = LevelViewerViewportDefinitions.NormalizeGizmoMode(packet.gizmo_mode);

            SettingsManager.SetInteger(Settings.LevelViewerDeepSelectMode, (int)deepSelectMode);
            SettingsManager.SetInteger(Settings.LevelViewerGizmoMode, (int)gizmoMode);

            LevelViewerPanel panel = editor.LevelViewerPanel;
            if (panel != null)
            {
                panel.ApplySelectionMode(deepSelectMode);
                panel.ApplyGizmoMode(gizmoMode);
            }

            return true;
        }
    }
}
