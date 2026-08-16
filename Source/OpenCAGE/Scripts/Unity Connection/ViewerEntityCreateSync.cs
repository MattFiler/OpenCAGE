using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using System;

namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Applies ENTITY_CREATE_REQUEST packets sent from the Godot Level Viewer (entity creation mode).
    /// Creates a function entity of the requested type at the clicked position.
    /// </summary>
    public static class ViewerEntityCreateSync
    {
        public static bool TryApply(Packet packet)
        {
            if (packet == null || packet.entity_function == 0 || !packet.has_transform)
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
                    Debug.Log("Websocket", "Failed to queue viewer entity create on UI thread: " + ex.Message);
                    return false;
                }
            }

            return ApplyCore(packet);
        }

        private static bool ApplyCore(Packet packet)
        {
            CompositeBrowser commands = Singleton.Editor?.CompositeBrowser;
            if (commands?.Content?.Level == null)
                return false;

            //Only allow types we actually preview in the viewer (matches the Create dropdown)
            FunctionType functionType = (FunctionType)packet.entity_function;
            if (!RenderFilterDefinitions.IsSupported(functionType))
                return false;

            CompositeDisplay display = commands.CompositeDisplay;
            if (display == null || display.IsDisposed || !display.Populated)
                return false;

            if (display.Composite?.shortGUID != new ShortGuid(packet.composite))
                return false;

            cTransform position = new cTransform(packet.position, packet.rotation);
            Entity newEntity = null;
            ViewerSelectionSync.RunAsViewerOriginated(() =>
            {
                newEntity = display.CreateFunctionEntity(functionType, null, position);
                if (newEntity != null)
                    display.LoadEntity(newEntity, true);
            });
            return newEntity != null;
        }
    }
}
