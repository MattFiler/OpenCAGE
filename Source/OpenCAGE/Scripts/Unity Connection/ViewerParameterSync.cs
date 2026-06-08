using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.DockPanels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Applies ENTITY_PARAMETER_MODIFIED packets sent from the Godot Level Viewer
    /// (e.g. gizmo drag). Updates the entity data and refreshes the inspector UI.
    /// </summary>
    public static class ViewerParameterSync
    {
        public static bool TryApply(Packet packet)
        {
            if (packet?.parameters == null || packet.parameters.Count == 0)
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
                    Debug.Log("Websocket", "Failed to queue viewer parameter sync on UI thread: " + ex.Message);
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

            ShortGuid compositeId = new ShortGuid(packet.composite);
            ShortGuid entityId    = new ShortGuid(packet.entity);

            Composite composite = commands.Content.Level.Commands.GetComposite(compositeId);
            if (composite == null)
                return false;

            Entity entity = composite.GetEntityByID(entityId);
            if (entity == null)
                return false;

            LevelContent content = commands.Content;

            ViewerSelectionSync.SuppressSyncBroadcastDepth++;
            try
            {
                foreach (SyncedParameter sync in packet.parameters)
                {
                    if (sync == null)
                        continue;

                    ShortGuid paramName = new ShortGuid(sync.name);
                    bool hadParam = entity.GetParameter(paramName) != null;

                    ParameterSync.ApplyToEntity(entity, sync, content);

                    bool paramAdded = !hadParam && entity.GetParameter(paramName) != null;

                    // Refresh the inspector UI for position / transform changes.
                    DataType dataType = ParameterSync.GetDataType(sync);
                    if (dataType == DataType.TRANSFORM && !sync.removed)
                    {
                        cTransform transform = entity.GetParameter(paramName)?.content as cTransform;
                        if (transform != null)
                        {
                            if (paramAdded)
                            {
                                // Parameter was just added — full entity reload so the inspector shows the new row.
                                commands.CompositeDisplay?.ReloadEntity(entity);
                            }
                            else
                            {
                                // Already existed — just update the existing inspector controls in-place.
                                commands.CompositeDisplay?.EntityDisplay?.ApplyTransformFromExternal(transform);
                            }
                        }
                    }
                }

                Singleton.OnParameterModified?.Invoke();
            }
            finally
            {
                ViewerSelectionSync.SuppressSyncBroadcastDepth--;
            }

            return true;
        }
    }
}
