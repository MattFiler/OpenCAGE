using System;
using System.Collections.Concurrent;
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

        public static void Enqueue(Packet packet)
        {
            if (packet == null)
                return;

            _queue.Enqueue(packet);
            if (!_paused)
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

            if (ViewerEmbedCoordinator.IsEmbedding || _paused)
                return;

            const int budget = 48;
            for (int i = 0; i < budget && _queue.TryDequeue(out Packet packet); i++)
                Apply(packet);

            if (!_queue.IsEmpty && !_paused && !ViewerEmbedCoordinator.IsEmbedding)
                ScheduleDrain();
        }

        private static void Apply(Packet packet)
        {
            switch (packet.packet_event)
            {
                case PacketEvent.ENTITY_SELECTED:
                    ViewerSelectionSync.TryApply(packet);
                    break;
                case PacketEvent.ENTITY_ADDED:
                case PacketEvent.ENTITY_DELETED:
                    ViewerEntitySync.TryApply(packet);
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
            }
        }
    }
}
