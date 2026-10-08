namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Receives populate lifecycle packets from the embedded level viewer.
    /// </summary>
    public static class ViewerPopulateSync
    {
        /// <summary>The populate the viewer has started and not yet reported finished (its token), or 0. UI thread.</summary>
        public static uint ActivePopulateToken { get; private set; }

        /// <summary>When <see cref="ActivePopulateToken"/> was set (UTC).</summary>
        public static System.DateTime ActivePopulateSinceUtc { get; private set; }

        /// <summary>How many populate starts and finishes (skipped ones included) have arrived, so a caller can tell one did. UI thread.</summary>
        public static int PopulateEvents { get; private set; }

        /* The newest populate reported finished. A save holds the viewer's packets back but lets these two through, so a
           populate's STARTED can be applied after its own FINISHED - and would then stand as a populate still running,
           holding every focus hand-over back until the next one. */
        private static uint _lastFinishedToken;

        /// <summary>The viewer went (died, or was stopped) - a populate it had running will never report finished. UI thread.</summary>
        internal static void Reset()
        {
            ActivePopulateToken = 0;
            _lastFinishedToken = 0; //a new viewer numbers its populates from 1 again
        }

        public static void NotifyStarted(Packet packet)
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return;

            if (editor.InvokeRequired)
            {
                try
                {
                    editor.BeginInvoke(new System.Action(() => NotifyStarted(packet)));
                }
                catch
                {
                }

                return;
            }

            string levelName = packet?.level_name;
            uint populateToken = packet?.populate_token ?? 0;
            if (populateToken != 0 && populateToken > _lastFinishedToken)
            {
                ActivePopulateToken = populateToken;
                ActivePopulateSinceUtc = System.DateTime.UtcNow;
            }
            PopulateEvents++;
            editor.ShowViewerPopulateProgress(levelName, populateToken);
        }

        public static void NotifyFinished(Packet packet)
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return;

            if (editor.InvokeRequired)
            {
                try
                {
                    editor.BeginInvoke(new System.Action(() => NotifyFinished(packet)));
                }
                catch
                {
                }

                return;
            }

            uint populateToken = packet?.populate_token ?? 0;
            //0 is a populate the viewer skipped, which it only reports with none of its own in flight
            if (populateToken == 0 || populateToken >= ActivePopulateToken)
                ActivePopulateToken = 0;
            if (populateToken > _lastFinishedToken)
                _lastFinishedToken = populateToken;
            PopulateEvents++;
            ViewerBusy.NotePopulateFinished();
            editor.EndViewerPopulateProgress(populateToken);

            //The viewer now holds what is on disk: give it whatever has changed here since
            ViewerResourceSync.NotifyViewerPopulated();
            //...and the script edits since the save, when it has just connected (queued behind that resource sync)
            ViewerScriptResync.NotifyViewerPopulated();

            /* A zone table sent while it was still loading is gone - the populate resets the scene and
               drops it with everything else - so it goes again now there is something to colour. */
            ViewerZoneSync.SendNow();
        }
    }
}
