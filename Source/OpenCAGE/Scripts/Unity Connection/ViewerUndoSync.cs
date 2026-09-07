using OpenCAGE.Undo;
using System;

namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Applies UNDO_REQUEST / REDO_REQUEST from the Level Viewer.
    /// </summary>
    /// <remarks>
    /// The undo stack lives here, but the viewport is a separate process with its own window. While it
    /// has focus, nothing it is sent reaches the editor's WinForms chords - which is why Ctrl+Z in the
    /// viewport used to fly the camera at the selection and Ctrl+Y did nothing at all (issue 667). The
    /// viewer recognises the chords and asks; this is where the asking lands.
    /// </remarks>
    public static class ViewerUndoSync
    {
        public static bool TryApply(Packet packet)
        {
            if (packet == null)
                return false;

            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return false;

            if (editor.InvokeRequired)
            {
                try
                {
                    editor.BeginInvoke(new Action(() => ApplyCore(packet)));
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.Log("Websocket", "Failed to queue viewer undo on UI thread: " + ex.Message);
                    return false;
                }
            }

            return ApplyCore(packet);
        }

        private static bool ApplyCore(Packet packet)
        {
            /* Straight onto the same stack the Edit menu drives, so a step made in the viewport and one
               made in the editor are the same history and undo in the same order. */
            if (packet.packet_event == PacketEvent.UNDO_REQUEST)
                UndoStack.Current.Undo();
            else
                UndoStack.Current.Redo();

            return true;
        }
    }
}
