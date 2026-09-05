using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OpenCAGE.Undo
{
    /// <summary>
    /// Ctrl+Z, Ctrl+Y and Ctrl+Shift+Z, from whichever window has them. Docked panels route keys up
    /// to the main window; floating ones are their own top-level windows, so each panel asks here
    /// from its own ProcessCmdKey.
    /// </summary>
    internal static class UndoKeys
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        public static bool TryHandle(Keys keyData)
        {
            if ((keyData & Keys.Control) != Keys.Control || (keyData & Keys.Alt) == Keys.Alt)
                return false;

            Keys code = keyData & Keys.KeyCode;
            bool shift = (keyData & Keys.Shift) == Keys.Shift;
            bool undo = code == Keys.Z && !shift;
            bool redo = code == Keys.Y || (code == Keys.Z && shift);
            if (!undo && !redo)
                return false;

            //A text box being typed into keeps its own undo
            if (TextIsBeingEdited())
                return false;

            if (undo)
                UndoStack.Current.Undo();
            else
                UndoStack.Current.Redo();
            return true;
        }

        private static bool TextIsBeingEdited()
        {
            try
            {
                Control focused = Control.FromChildHandle(GetFocus());
                return focused is TextBoxBase || focused is ComboBox || focused is UpDownBase;
            }
            catch
            {
                return false;
            }
        }
    }
}
