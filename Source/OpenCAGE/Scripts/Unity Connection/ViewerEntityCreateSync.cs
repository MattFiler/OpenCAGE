using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using System;

namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Applies ENTITY_CREATE_REQUEST packets sent from the Godot Level Viewer. Creates either a function
    /// entity of the requested type (entity creation mode) or an instance of the requested composite
    /// (a composite dragged out of the browser and dropped on the viewport) at the clicked position.
    /// </summary>
    public static class ViewerEntityCreateSync
    {
        public static bool TryApply(Packet packet)
        {
            if (packet == null || !packet.has_transform)
                return false;
            if (packet.entity_function == 0 && packet.create_composite_instance == 0)
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

            CompositeDisplay display = commands.CompositeDisplay;
            if (display == null || display.IsDisposed || !display.Populated)
                return false;

            if (display.Composite?.shortGUID != new ShortGuid(packet.composite))
                return false;

            cTransform position = new cTransform(packet.position, packet.rotation);

            if (packet.create_composite_instance != 0)
            {
                Composite instanceComposite = commands.Content.Level.Commands.GetComposite(new ShortGuid(packet.create_composite_instance));
                if (instanceComposite == null)
                    return false;

                return Create(display, () => display.CreateCompositeInstanceEntity(instanceComposite, null, position));
            }

            //Only allow types we actually preview in the viewer (matches the Create dropdown)
            FunctionType functionType = (FunctionType)packet.entity_function;
            if (!RenderFilterDefinitions.IsSupported(functionType))
                return false;

            return Create(display, () => display.CreateFunctionEntity(functionType, null, position));
        }

        private static bool Create(CompositeDisplay display, Func<Entity> create)
        {
            Entity newEntity = null;
            ViewerSelectionSync.RunAsViewerOriginated(() =>
            {
                newEntity = create();
                if (newEntity != null)
                    display.LoadEntity(newEntity, true);
            });
            return newEntity != null;
        }
    }
}
