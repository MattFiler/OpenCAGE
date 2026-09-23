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

        /* The gesture the latest packets came from (Packet.gesture), and what it has moved so far - which
           is all its undo step's name needs. A gesture that began with a step of its own, a shift-clone's
           duplicate, keeps that step's name for the drag that follows. */
        private static uint _gesture = 0;
        private static bool _gestureNamed = false;
        private static readonly HashSet<ShortGuid> _gestureEntities = new HashSet<ShortGuid>();

        /// <summary>
        /// What the undo stack joins a gesture's pieces on (UndoStack.BeginGroup): equal for every packet
        /// of one gesture, null for 0 - no gesture, which is also all a viewer from before them sends.
        /// </summary>
        public static object GestureUndoKey(uint gesture)
        {
            return gesture == 0 ? null : new GestureKey(gesture);
        }

        /// <summary>This gesture's step was named by where it began (the copies of a shift-clone): the moves keep that name.</summary>
        public static void GestureBegan(uint gesture)
        {
            if (gesture == 0)
                return;
            _gesture = gesture;
            _gestureNamed = true;
            _gestureEntities.Clear();
        }

        /* A piece of a gesture that moved this entity. Null leaves the step its name: the edit's own
           ("Move Door_1") while the gesture has moved one entity, or the name it began with. */
        private static string GestureLabel(uint gesture, Entity entity)
        {
            if (gesture != _gesture)
            {
                _gesture = gesture;
                _gestureNamed = false;
                _gestureEntities.Clear();
            }
            _gestureEntities.Add(entity.shortGUID);
            if (_gestureNamed || _gestureEntities.Count < 2)
                return null;
            return "Move " + UndoLabels.Count(_gestureEntities.Count, "entity", "entities");
        }

        private sealed class GestureKey
        {
            private readonly uint _id;
            public GestureKey(uint id) { _id = id; }
            public override bool Equals(object obj) => obj is GestureKey other && other._id == _id;
            public override int GetHashCode() => (int)_id;
        }

        /* One viewer edit, recorded as the inspector records one; ApplyCore groups a gesture's run of them */
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

        /* Animation Mode: a move made in the viewport is a keyframe at the playhead, not a new resting
           place for the entity. The packet carries the full instance path, which is what says WHICH
           placement of the entity was dragged - the animation drives one of them, not all of them.
           Answering true here is what keeps the move out of the level's own data. */
        private static bool TryRecordAsAnimationKeyframe(Packet packet)
        {
            AnimationModeSession session = AnimationModeSession.Current;
            if (session == null || packet.parameters == null || packet.path_entities == null || packet.path_entities.Count == 0)
                return false;

            //A packet carrying anything else is not a move, and taking half of it would lose the rest
            foreach (SyncedParameter sync in packet.parameters)
            {
                if (sync == null || sync.removed || ParameterSync.GetDataType(sync) != DataType.TRANSFORM)
                    return false;
            }

            bool recorded = false;
            foreach (SyncedParameter sync in packet.parameters)
            {
                cTransform transform = ParameterSync.Unpack(sync) as cTransform;
                if (transform == null)
                    continue;

                //Something the animation cannot address is not part of it: that move is an ordinary one
                recorded |= session.TryRecordTransform(packet.path_entities, transform);
            }
            return recorded;
        }

        private static bool ApplyCore(Packet packet)
        {
            CompositeBrowser commands = Singleton.Editor?.CompositeBrowser;
            //Level is there from the moment a load begins; its commands only once it is done
            if (commands?.Content == null || !commands.Content.IsLevelDataLoaded)
                return false;

            if (TryRecordAsAnimationKeyframe(packet))
                return true;

            ShortGuid compositeId = new ShortGuid(packet.composite);
            ShortGuid entityId    = new ShortGuid(packet.entity);

            Composite composite = commands.Content.Level.Commands.GetComposite(compositeId);
            if (composite == null)
                return false;

            Entity entity = composite.GetEntityByID(entityId);
            if (entity == null)
                return false;

            LevelContent content = commands.Content;

            /* One gesture's packets are one undo step: a drag of five entities sends five of these, and
               undoing it has to put all five back, not the last of them */
            IDisposable gestureStep = packet.gesture == 0 ? null
                : UndoStack.Current.BeginGroup(GestureLabel(packet.gesture, entity), GestureUndoKey(packet.gesture));

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
                gestureStep?.Dispose();
            }

            return true;
        }
    }
}
