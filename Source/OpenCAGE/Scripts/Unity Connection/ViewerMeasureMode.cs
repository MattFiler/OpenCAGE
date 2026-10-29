namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Transient measuring state (the viewport toolbar's Measure), shared between the toolbar and websocket sync. While
    /// it is on, a click in the viewport puts down a point of the viewer's ruler instead of selecting.
    /// </summary>
    public static class ViewerMeasureMode
    {
        public static bool Active { get; set; } = false;
    }
}
