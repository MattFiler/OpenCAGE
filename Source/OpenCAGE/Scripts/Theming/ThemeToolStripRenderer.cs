using System.Drawing;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.Theming
{
    /// <summary>
    /// The docking library's Visual Studio renderer, with the drop-down highlight corrected.
    ///
    /// Menus and toolbars are painted by DockPanelSuite's renderer so they match the docking chrome
    /// exactly, but its menu highlight has a bug for anything that isn't the top-level menu bar: it
    /// decides "is this a drop-down" by testing whether the item's owner is a MenuStrip, which is only
    /// ever true for the menu bar itself. Every context menu and tool-strip drop-down takes the other
    /// branch and gets filled across e.Item.ContentRectangle - a rectangle that starts *after* the image
    /// margin, so the highlight covers only the text half of the row.
    ///
    /// Subclassed rather than patched in place, so the vendored library stays untouched.
    /// </summary>
    internal sealed class ThemeToolStripRenderer : VisualStudioToolStripRenderer
    {
        public ThemeToolStripRenderer(DockPanelColorPalette palette)
            : base(palette)
        {
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            //The menu bar's own items are drawn correctly by the base renderer
            bool onMenuBar = e.Item.Owner is MenuStrip;
            if (onMenuBar || !e.Item.Enabled || !e.Item.Selected)
            {
                base.OnRenderMenuItemBackground(e);
                return;
            }

            //Full row, image margin included - which is what the highlight is supposed to span
            Rectangle bounds = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            using (SolidBrush brush = new SolidBrush(ColorTable.MenuItemSelected))
                e.Graphics.FillRectangle(brush, bounds);

            using (Pen pen = new Pen(ColorTable.MenuItemBorder))
                e.Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        }
    }
}
