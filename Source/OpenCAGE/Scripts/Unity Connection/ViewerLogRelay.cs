namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Receives log lines from the embedded Level Viewer process / websocket.
    /// </summary>
    public static class ViewerLogRelay
    {
        public static void Write(string message, bool isError = false)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            string line = isError ? "ERROR: " + message : message;
#if DEBUG
            OpenCAGE.Debug.Log("Level Viewer", line);
#else
            System.Console.WriteLine("[Level Viewer] " + line);
#endif
        }
    }
}
