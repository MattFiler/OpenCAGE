using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Applies ENTITY_CLIPBOARD_COPY / ENTITY_CLIPBOARD_PASTE packets sent from the Godot Level
    /// Viewer (Ctrl+C / Ctrl+V in the viewport), backed by the shared EntityClipboard.
    /// </summary>
    public static class ViewerClipboardSync
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
                    Debug.Log("Websocket", "Failed to queue viewer clipboard action on UI thread: " + ex.Message);
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

            switch (packet.packet_event)
            {
                case PacketEvent.ENTITY_CLIPBOARD_COPY:
                {
                    Composite composite = commands.Content.Level.Commands.GetComposite(new ShortGuid(packet.composite));
                    Entity entity = composite?.GetEntityByID(new ShortGuid(packet.entity));
                    if (entity == null)
                        return false;

                    /* A ctrl-click selection of several arrives in selection_entities with the primary
                       first, as it does for a delete. Take every one that still exists here, once each;
                       anything else and the copy is just the primary. */
                    List<EntityClipboard.Entry> entries = new List<EntityClipboard.Entry>();
                    if (packet.selection_entities != null && packet.selection_entities.Count > 1)
                    {
                        foreach (uint entityId in packet.selection_entities)
                        {
                            if (composite.GetEntityByID(new ShortGuid(entityId)) == null || entries.Any(o => o.EntityId == entityId))
                                continue;
                            entries.Add(new EntityClipboard.Entry() { EntityId = entityId, Offset = Point.Empty });
                        }
                    }
                    if (entries.Count == 0)
                        entries.Add(new EntityClipboard.Entry() { EntityId = packet.entity, Offset = Point.Empty });

                    //Capture the display's drill path when it matches so ancestor reference-pastes can alias
                    CompositeDisplay copyDisplay = commands.CompositeDisplay;
                    if (copyDisplay != null && !copyDisplay.IsDisposed && copyDisplay.Populated
                        && copyDisplay.Composite?.shortGUID == composite.shortGUID)
                    {
                        copyDisplay.CopyEntitiesToClipboard(entries);
                    }
                    else
                    {
                        EntityClipboard.Set(packet.composite, entries);
                    }
                    return true;
                }
                case PacketEvent.ENTITY_CLIPBOARD_PASTE:
                {
                    CompositeDisplay display = commands.CompositeDisplay;
                    if (display == null || display.IsDisposed || !display.Populated)
                        return false;

                    if (display.Composite?.shortGUID != new ShortGuid(packet.composite))
                        return false;

                    ViewerSelectionSync.RunAsViewerOriginated(() => display.PasteClipboardFromViewport());
                    return true;
                }
                default:
                    return false;
            }
        }
    }
}
