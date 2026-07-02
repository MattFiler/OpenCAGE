namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Receives populate lifecycle packets from the embedded level viewer.
    /// </summary>
    public static class ViewerPopulateSync
    {
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
            editor.EndViewerPopulateProgress(populateToken);
        }
    }
}
