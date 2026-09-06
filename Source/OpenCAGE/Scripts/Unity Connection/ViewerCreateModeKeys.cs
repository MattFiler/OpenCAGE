using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Escape, from whichever window has it: it cancels entity creation mode. Docked panels route
    /// keys up to the main window; floating ones are their own top-level windows, so each panel
    /// asks here from its own ProcessCmdKey (same arrangement as the undo shortcuts).
    /// </summary>
    internal static class ViewerCreateModeKeys
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        public static bool TryHandle(Keys keyData)
        {
            if ((keyData & Keys.KeyCode) != Keys.Escape || (keyData & Keys.Modifiers) != Keys.None)
                return false;

            if (!ViewerCreateMode.IsActive)
                return false;

            //Escape in a text box still means "give up on what I was typing"
            if (TextIsBeingEdited())
                return false;

            return Singleton.Editor?.ExitViewerCreateMode() == true;
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
