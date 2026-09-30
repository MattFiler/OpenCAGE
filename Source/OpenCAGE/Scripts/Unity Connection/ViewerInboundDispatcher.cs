using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace OpenCAGE.UnityConnection
{
    public static class ViewerInboundDispatcher
    {
        private static readonly ConcurrentQueue<Packet> _queue = new ConcurrentQueue<Packet>();
        private static int _drainScheduled;
        private static bool _paused;

        public static void Pause()
        {
            _paused = true;
        }

        public static void Resume()
        {
            _paused = false;
            ScheduleDrain();
        }

        /* Save & Build writes the level's script on a worker while the UI thread pumps messages (its DoEvents loop), and
           a drain inside that loop applied viewport edits - a gizmo drag, a paste, a delete, a deep-select alias - to the
           script as it was being written: a torn or failed save. What the viewer sends that changes the level waits, in
           order, until the build is done. (A plain save does not pump, so nothing drains during it anyway.) */
        private static volatile bool _heldForSave;

        public static void HoldForSave()
        {
            _heldForSave = true;
        }

        public static void ReleaseAfterSave()
        {
            _heldForSave = false;
            ScheduleDrain();
        }

        public static void Enqueue(Packet packet)
        {
            if (packet == null)
                return;

            /* The viewport camera, streamed while the game's camera follows it (or sent once to say the viewer can follow the
               game's): it touches no level data, and a pose is only worth anything while it is the latest, so it goes
               straight on from this (the socket's) thread - never queued behind the UI thread, a populate or a Save & Build,
               where it would arrive late and in a heap. */
            if (packet.packet_event == PacketEvent.VIEWER_CAMERA_POSE)
            {
                RuntimeUtilsConnection.LiveLinkCameraSync.OnViewerPose(packet);
                return;
            }

            if (_heldForSave)
            {
                //Another save asked for during a build was always refused, and a menu would open where the
                //right-click was a whole build ago
                if (packet.packet_event == PacketEvent.SAVE_REQUEST || packet.packet_event == PacketEvent.SAVE_AND_BUILD_REQUEST
                    || packet.packet_event == PacketEvent.VIEWPORT_CONTEXT_MENU)
                    return;

                /* What touches no level data goes straight through: populate progress kept in step with a viewer that
                   might go away mid-build (its disconnect is not queued here), and a menu the viewer has closed. */
                if (packet.packet_event == PacketEvent.VIEWER_POPULATE_STARTED || packet.packet_event == PacketEvent.VIEWER_POPULATE_FINISHED
                    || packet.packet_event == PacketEvent.VIEWER_LOG || packet.packet_event == PacketEvent.VIEWPORT_CONTEXT_MENU_DISMISS)
                {
                    CommandsEditor editor = Singleton.Editor;
                    try
                    {
                        editor?.BeginInvoke(new Action(() => Apply(packet)));
                    }
                    catch
                    {
                    }
                    return;
                }
            }

            _queue.Enqueue(packet);
            if (!_paused && !_heldForSave)
                ScheduleDrain();
        }

        private static void ScheduleDrain()
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed || ViewerEmbedCoordinator.IsEmbedding)
                return;

            if (Interlocked.CompareExchange(ref _drainScheduled, 1, 0) != 0)
                return;

            try
            {
                editor.BeginInvoke(new Action(Drain));
            }
            catch
            {
                Interlocked.Exchange(ref _drainScheduled, 0);
            }
        }

        private static void Drain()
        {
            Interlocked.Exchange(ref _drainScheduled, 0);

            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return;

            if (editor.InvokeRequired)
            {
                try
                {
                    editor.BeginInvoke(new Action(Drain));
                }
                catch
                {
                }

                return;
            }

            if (ViewerEmbedCoordinator.IsEmbedding || _paused || _heldForSave)
                return;

            /* A handler that shows a modal box (the viewport Delete's "Are you sure?", a create error) pumps messages,
               and the next queued drain ran underneath it: a second Delete pressed in the viewport opened a second box,
               and answering Yes to both deleted the entity and then threw deleting it again - a crash on a Ship build.
               Packets that arrive meanwhile wait for the box to close; this drain goes on with them afterwards. */
            if (_draining)
                return;

            _draining = true;
            try
            {
                const int budget = 48;
                for (int i = 0; i < budget && _queue.TryDequeue(out Packet packet); i++)
                    Apply(packet);
            }
            finally
            {
                _draining = false;
                //Also after a handler threw: what is still queued must not wait for the viewer's next packet
                if (!_queue.IsEmpty && !_paused && !_heldForSave && !ViewerEmbedCoordinator.IsEmbedding)
                    ScheduleDrain();
            }
        }

        private static bool _draining;

        private static void Apply(Packet packet)
        {
            switch (packet.packet_event)
            {
                case PacketEvent.ENTITY_SELECTED:
                    ViewerSelectionSync.TryApply(packet);
                    break;
                case PacketEvent.ENTITY_ADDED:
                case PacketEvent.ENTITY_DELETED:
                case PacketEvent.ENTITY_ALIAS_RELEASED:
                    ViewerEntitySync.TryApply(packet);
                    break;
                case PacketEvent.ENTITY_DELETE_REQUEST:
                    ViewerEntitySync.TryApplyDeleteRequest(packet);
                    break;
                case PacketEvent.ENTITY_PARAMETER_MODIFIED:
                    ViewerParameterSync.TryApply(packet);
                    break;
                case PacketEvent.VIEWER_LOG:
                    ViewerLogRelay.Write(packet.log_message, packet.log_is_error);
                    break;
                case PacketEvent.VIEWER_POPULATE_STARTED:
                    ViewerPopulateSync.NotifyStarted(packet);
                    break;
                case PacketEvent.VIEWER_POPULATE_FINISHED:
                    ViewerPopulateSync.NotifyFinished(packet);
                    break;
                case PacketEvent.VIEWPORT_MODE_CHANGED:
                    ViewerViewportModeSync.TryApply(packet);
                    break;
                case PacketEvent.ENTITY_CREATE_REQUEST:
                    ViewerEntityCreateSync.TryApply(packet);
                    break;
                case PacketEvent.ENTITY_CLIPBOARD_COPY:
                case PacketEvent.ENTITY_CLIPBOARD_PASTE:
                    ViewerClipboardSync.TryApply(packet);
                    break;
                case PacketEvent.ENTITY_DUPLICATE_REQUEST:
                    ViewerEntityDuplicateSync.TryApply(packet);
                    break;
                case PacketEvent.UNDO_REQUEST:
                case PacketEvent.REDO_REQUEST:
                    ViewerUndoSync.TryApply(packet);
                    break;
                case PacketEvent.VIEWPORT_CONTEXT_MENU:
                case PacketEvent.VIEWPORT_CONTEXT_MENU_DISMISS:
                    ViewerContextMenu.TryApply(packet);
                    break;
                case PacketEvent.COMPOSITE_PREVIEW_CAPTURED:
                    CompositePreviewManager.OnCaptured(packet);
                    break;
                case PacketEvent.SAVE_REQUEST:
                    Singleton.Editor?.BeginInvoke(new Action(() => Singleton.Editor?.SaveLevel(false)));
                    break;
                case PacketEvent.SAVE_AND_BUILD_REQUEST:
                    Singleton.Editor?.BeginInvoke(new Action(() => Singleton.Editor?.SaveLevel(true)));
                    break;
                case PacketEvent.FILES_DROPPED:
                    {
                        //Out of the drain: opening a package shows windows, and a modal loop inside the batch
                        //would let the next packet's drain run re-entrantly underneath it
                        List<string> files = new List<string>(packet.dropped_files ?? new List<string>());
                        Singleton.Editor?.BeginInvoke(new Action(() => PackageFiles.OpenMany(files)));
                    }
                    break;
            }
        }
    }
}
