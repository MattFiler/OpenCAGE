using System;

namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Whether the viewer's main thread is probably tied up (a load or populate is running, or has just been asked for).
    /// </summary>
    /// <remarks>
    /// The viewer's window is another process's, parented into ours, so moving keyboard focus into or out of it waits
    /// for the viewer's thread to take the focus messages. While it populates that thread does nothing else for seconds
    /// at a time (a minute on the big levels), and a focus change made then froze OpenCAGE for as long: the stress run
    /// caught the inspector doing it on every selection that opened another composite. Focus moves that can wait are
    /// held back while this says so - checking the window answers is not enough on its own, since the populate the
    /// selection just asked for has usually not started yet.
    /// </remarks>
    public static class ViewerBusy
    {
        //How long after asking for a populate the viewer can still be about to start it
        private const double RequestGraceSeconds = 2.0;
        //A populate that never reported finished (the viewer threw after starting it) stops counting after this
        private const double PopulateLimitMinutes = 5.0;

        private static DateTime _lastPopulateRequest = DateTime.MinValue;

        /// <summary>A packet that makes the viewer load or rebuild what it shows was just sent.</summary>
        internal static void NotePopulateRequested(Packet packet)
        {
            bool rebuilds;
            switch (packet.packet_event)
            {
                case PacketEvent.LEVEL_LOADED:
                    //The one a save sends, and the one closing a load the viewer already has, are skipped by the viewer
                    rebuilds = packet.level_reload;
                    break;
                case PacketEvent.COMPOSITE_SELECTED:
                    //A drill (a longer path) moves within what is on screen; only a new root is a populate
                    rebuilds = packet.path_composites == null || packet.path_composites.Count <= 1;
                    break;
                case PacketEvent.COMPOSITE_RELOADED:
                    rebuilds = true;
                    break;
                default:
                    rebuilds = false;
                    break;
            }
            if (rebuilds)
                _lastPopulateRequest = DateTime.UtcNow;
        }

        /// <summary>The viewer reported a populate finished (or skipped): it is not about to start the one asked for.</summary>
        internal static void NotePopulateFinished()
        {
            //Unless the request is only just out - the finish may be for the populate before it
            if ((DateTime.UtcNow - _lastPopulateRequest).TotalMilliseconds > 500)
                _lastPopulateRequest = DateTime.MinValue;
        }

        public static bool Likely =>
            Send.Connected
            && ((ViewerPopulateSync.ActivePopulateToken != 0 && (DateTime.UtcNow - ViewerPopulateSync.ActivePopulateSinceUtc).TotalMinutes < PopulateLimitMinutes)
                || (DateTime.UtcNow - _lastPopulateRequest).TotalSeconds < RequestGraceSeconds);

        /// <summary>
        /// Activating a panel now could wait on the viewer's thread, and that thread is not free: busy, or not answering.
        /// </summary>
        /// <remarks>
        /// Not only when focus is in the viewer: DockPanelSuite gives an activated panel's focus back to the window it last
        /// had focused, and for the composite display that is the viewer's own window. A level load reached that with the
        /// viewer still working through a big undo, and OpenCAGE was frozen for as long as the viewer took.
        /// </remarks>
        public static bool FocusMoveWouldWait =>
            Send.Connected && (Likely || !(Singleton.Editor?.LevelViewerPanel?.IsViewerResponding(50) ?? true));
    }
}
