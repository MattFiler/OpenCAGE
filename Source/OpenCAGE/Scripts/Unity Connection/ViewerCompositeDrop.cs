using CATHODE.Scripting;
using OpenCAGE.DockPanels;
using System.Drawing;
using System.Windows.Forms;

namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Dropping a composite out of the browser onto the viewport, which instances it where it landed.
    ///
    /// The viewer's window belongs to another process and covers the panel hosting it, so a drop over
    /// the viewport can't be relied on to reach us as a DragDrop event the way the flowgraph's does.
    /// The drag source watches the cursor instead (see CompositeBrowser): when the button comes up over
    /// the viewport we cancel the OLE drop and come here. Placement is the viewer's - only it has the
    /// geometry to raycast - so this just asks, and the answer arrives as ENTITY_CREATE_REQUEST.
    /// </summary>
    public static class ViewerCompositeDrop
    {
        public static bool IsCursorOverViewport()
        {
            return TryGetViewportFraction(Cursor.Position, out _, out _);
        }

        /// <summary>Ask the viewer to place an instance of the composite at the dropped screen point.</summary>
        public static bool TryDrop(Composite composite, Point screenPoint)
        {
            if (composite == null || !Send.Connected)
                return false;

            if (!TryGetViewportFraction(screenPoint, out float x, out float y))
                return false;

            Send.SendCompositeDropPacket(composite, x, y);
            return true;
        }

        private static bool TryGetViewportFraction(Point screenPoint, out float x, out float y)
        {
            x = 0f;
            y = 0f;

            LevelViewerPanel panel = Singleton.Editor?.LevelViewerPanel;
            return panel != null && !panel.IsDisposed && panel.TryGetViewportFraction(screenPoint, out x, out y);
        }
    }
}
