using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Applies ENTITY_DUPLICATE_REQUEST packets from the Godot Level Viewer (Shift held on a gizmo
    /// handle). Duplicates the selection in place and selects the copies; that selection reaches the
    /// viewer as the ordinary ENTITY_ADDED + ENTITY_SELECTED, which is what its gizmo hands the drag to.
    /// </summary>
    public static class ViewerEntityDuplicateSync
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
                    Debug.Log("Websocket", "Failed to queue viewer duplicate on UI thread: " + ex.Message);
                    return false;
                }
            }

            return ApplyCore(packet);
        }

        private static bool ApplyCore(Packet packet)
        {
            CompositeBrowser commands = Singleton.Editor?.CompositeBrowser;
            //Level is there from the moment a load begins; its commands only once it is done
            if (commands?.Content == null || !commands.Content.IsLevelDataLoaded)
                return false;

            CompositeDisplay display = commands.CompositeDisplay;
            if (display == null || display.IsDisposed || !display.Populated)
                return false;

            Composite composite = display.Composite;
            if (composite == null || composite.shortGUID != new ShortGuid(packet.composite))
                return false;

            /* A ctrl-click selection of several arrives in selection_entities, primary first, the same
               way a copy or delete does. Take each that still exists here, once; otherwise just the primary. */
            List<Entity> entities = new List<Entity>();
            if (packet.selection_entities != null && packet.selection_entities.Count > 1)
            {
                foreach (uint entityId in packet.selection_entities)
                {
                    Entity entity = composite.GetEntityByID(new ShortGuid(entityId));
                    if (entity != null && !entities.Contains(entity))
                        entities.Add(entity);
                }
            }
            if (entities.Count == 0)
            {
                Entity primary = composite.GetEntityByID(new ShortGuid(packet.entity));
                if (primary == null)
                    return false;
                entities.Add(primary);
            }

            ViewerSelectionSync.RunAsViewerOriginated(() => display.DuplicateEntities(entities));
            return true;
        }
    }
}
