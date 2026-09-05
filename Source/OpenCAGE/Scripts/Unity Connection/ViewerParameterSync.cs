using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.DockPanels;
using OpenCAGE.Undo;
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

        /* A gizmo drag arrives as a run of packets; the value edits it records merge into one step */
        private static void RecordViewerEdit(Composite composite, Entity entity, Parameter previous, int previousIndex, ParameterData before, bool wasModified, Parameter current, bool removed)
        {
            string label = UndoLabels.ChangeParameter(composite, entity, current ?? previous);
            if (removed)
            {
                if (previous != null && current == null)
                    UndoStack.Current.Record(new ParameterPresenceEdit(composite, entity, previous, previousIndex, false, wasModified, label));
                return;
            }
            if (current == null)
                return;

            if (previous == null)
                UndoStack.Current.Record(new ParameterPresenceEdit(composite, entity, current, entity.parameters.IndexOf(current), true, true, label));
            else
                UndoStack.Current.Record(new ParameterValueEdit(composite, entity, current.name, before, ParameterValues.Clone(current.content), wasModified, true, label));
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
                    Parameter previous = entity.GetParameter(paramName);
                    bool hadParam = previous != null;
                    ParameterData before = ParameterValues.Clone(previous?.content);
                    bool wasModified = hadParam && ParameterModificationTracker.IsParameterModified(composite.shortGUID, entity.shortGUID, paramName);
                    int previousIndex = hadParam ? entity.parameters.IndexOf(previous) : -1;

                    ParameterSync.ApplyToEntity(entity, sync, content);

                    Parameter current = entity.GetParameter(paramName);
                    bool paramAdded = !hadParam && current != null;

                    //Viewer edits count as modifications too, so the inspector bolds them like local edits
                    if (!sync.removed && current != null)
                        ParameterModificationTracker.SetParameterModified(composite.shortGUID, entity.shortGUID, paramName);

                    RecordViewerEdit(composite, entity, previous, previousIndex, before, wasModified, current, sync.removed);

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
                                commands.CompositeDisplay?.EntityDisplay?.ApplyTransformFromExternal(paramName, transform);
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
