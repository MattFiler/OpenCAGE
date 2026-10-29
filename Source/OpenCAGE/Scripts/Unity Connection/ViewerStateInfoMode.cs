namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Which state's generated navigation data the Level Viewer is drawing. A level has one set of
    /// these per state (STATE_x/NAV_MESH and STATE_x/COVER), and they're rebuilt on an instanced
    /// save, so this is just an index - the viewer re-reads the files when it repopulates.
    ///
    /// -1 means off. Navmesh and cover are tracked separately so both can be on at once.
    /// </summary>
    public static class ViewerStateInfoMode
    {
        public const int None = -1;

        public static int NavMeshState { get; set; } = None;
        public static int CoverState { get; set; } = None;

        public static bool IsActive => NavMeshState != None || CoverState != None;

        public static void Clear()
        {
            NavMeshState = None;
            CoverState = None;
        }
    }
}
