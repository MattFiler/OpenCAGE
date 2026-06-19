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

            string levelName = packet?.level_name;
            uint populateToken = packet?.populate_token ?? 0;
            editor.BeginInvoke(new System.Action(() => editor.ShowViewerPopulateProgress(levelName, populateToken)));
        }

        public static void NotifyFinished(Packet packet)
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return;

            uint populateToken = packet?.populate_token ?? 0;
            editor.BeginInvoke(new System.Action(() => editor.EndViewerPopulateProgress(populateToken)));
        }
    }
}
