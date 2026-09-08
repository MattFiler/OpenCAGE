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
            LevelViewerHighlightMode highlightMode = LevelViewerViewportDefinitions.NormalizeHighlightMode(
                packet.selection_highlight_mode);

            SettingsManager.SetInteger(Settings.LevelViewerDeepSelectMode, (int)deepSelectMode);
            SettingsManager.SetInteger(Settings.LevelViewerGizmoMode, (int)gizmoMode);
            /* Alt+1-4 in the viewport picks a highlight mode, and the setting is the editor's - storing it
               here is what moves the tick in Options > Viewport and keeps it after a restart (issue 673).
               The settings packet this sends back carries the mode the viewer already has. */
            SettingsManager.SetInteger(Settings.LevelViewerHighlightMode, (int)highlightMode);
            editor.RefreshHighlightModeMenu();
            ViewerCreateMode.ActiveFunctionType = packet.create_function_type;

            LevelViewerPanel panel = editor.LevelViewerPanel;
            if (panel != null)
            {
                panel.ApplySelectionMode(deepSelectMode);
                panel.ApplyGizmoMode(gizmoMode);
                panel.ApplyCreateMode(packet.create_function_type);
            }

            return true;
        }
    }
}
