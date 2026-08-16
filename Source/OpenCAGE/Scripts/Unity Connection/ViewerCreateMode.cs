namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Transient entity-creation-mode state shared between the viewport toolbar and websocket sync.
    /// Non-zero means the viewer places an entity of this FunctionType on click; 0 = mode off.
    /// </summary>
    public static class ViewerCreateMode
    {
        public static uint ActiveFunctionType { get; set; } = 0;

        public static bool IsActive => ActiveFunctionType != 0;
    }
}
