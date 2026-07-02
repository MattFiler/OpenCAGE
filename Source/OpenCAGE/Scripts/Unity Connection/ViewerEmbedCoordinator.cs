namespace OpenCAGE.UnityConnection
{
    public static class ViewerEmbedCoordinator
    {
        public static bool IsEmbedding { get; private set; }

        public static void BeginEmbedding()
        {
            IsEmbedding = true;
            ViewerInboundDispatcher.Pause();
        }

        public static void EndEmbedding()
        {
            IsEmbedding = false;
            ViewerInboundDispatcher.Resume();
        }
    }
}
