namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Receives populate lifecycle packets from the embedded level viewer.
    /// </summary>
    public static class ViewerPopulateSync
    {
        /// <summary>The populate the viewer has started and not yet reported finished (its token), or 0. UI thread.</summary>
        public static uint ActivePopulateToken { get; private set; }

        /// <summary>How many populate starts and finishes (skipped ones included) have arrived, so a caller can tell one did. UI thread.</summary>
        public static int PopulateEvents { get; private set; }

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
            if (populateToken != 0)
                ActivePopulateToken = populateToken;
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
            PopulateEvents++;
            editor.EndViewerPopulateProgress(populateToken);

            //The viewer now holds what is on disk: give it whatever has changed here since
            ViewerResourceSync.NotifyViewerPopulated();

            /* A zone table sent while it was still loading is gone - the populate resets the scene and
               drops it with everything else - so it goes again now there is something to colour. */
            ViewerZoneSync.SendNow();
        }
    }
}
