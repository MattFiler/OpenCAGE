namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Receives log lines from the embedded Level Viewer process / websocket.
    /// Must run on the WinForms UI thread — websocket callbacks arrive on a worker thread.
    /// </summary>
    public static class ViewerLogRelay
    {
        public static void Write(string message, bool isError = false)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            CommandsEditor editor = Singleton.Editor;
            if (editor != null && !editor.IsDisposed && editor.InvokeRequired)
            {
                try
                {
                    editor.BeginInvoke(new System.Action(() => WriteCore(message, isError)));
                }
                catch
                {
                    // Editor is closing; drop the line rather than cross the disconnected COM apartment.
                }

                return;
            }

            WriteCore(message, isError);
        }

        private static void WriteCore(string message, bool isError)
        {
            string line = isError ? "ERROR: " + message : message;
            Debug.Log("Viewport", line);
        }
    }
}
