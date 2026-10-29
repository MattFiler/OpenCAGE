using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
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

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            base.OnRenderToolStripBackground(e);

            ToolStripDropDownMenu menu = e.ToolStrip as ToolStripDropDownMenu;
            if (menu != null)
            {
                ThemeScrollButton(menu, _upScrollButton);
                ThemeScrollButton(menu, _downScrollButton);
            }
        }

        /* A drop-down too tall for the screen - the viewport's Create menu, say - grows a scroll button at
         * each end, and neither goes anywhere near the renderer. Each is a label hosted on the menu that fills
         * itself from the menu's BackColor, which stays the system's light grey because the renderer paints
         * the menu's real background, then draws a black arrow on top: (240,240,240) strips across the top
         * and bottom of an otherwise dark menu. They're private to the menu and only made once it needs
         * them, so they're picked up the first time the menu paints with them, and given the menu's own
         * colour and a light copy of the arrow. */
        private static readonly FieldInfo _upScrollButton = typeof(ToolStripDropDownMenu).GetField("upScrollButton", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo _downScrollButton = typeof(ToolStripDropDownMenu).GetField("downScrollButton", BindingFlags.NonPublic | BindingFlags.Instance);

        //Every scroll button recoloured, with the arrow it came with, so light mode can put them back
        private static readonly List<KeyValuePair<WeakReference, Image>> _themedScrollButtons = new List<KeyValuePair<WeakReference, Image>>();
        private static readonly Dictionary<Image, Image> _lightArrows = new Dictionary<Image, Image>();

        private void ThemeScrollButton(ToolStripDropDownMenu menu, FieldInfo field)
        {
            if (field == null)
                return;

            ToolStripControlHost host = field.GetValue(menu) as ToolStripControlHost;
            Label label = host != null ? host.Control as Label : null;
            if (label == null || label.IsDisposed)
                return;

            Color back = ColorTable.ToolStripDropDownBackground;
            if (label.BackColor == back)
                return;

            Image arrow = label.Image;
            if (arrow != null && !_lightArrows.ContainsValue(arrow))
            {
                _themedScrollButtons.RemoveAll(x => !x.Key.IsAlive);
                _themedScrollButtons.Add(new KeyValuePair<WeakReference, Image>(new WeakReference(label), arrow));
                label.Image = LightArrow(arrow);
            }
            label.BackColor = back;
        }

        /// <summary>The framework's arrow, same shape, in the text colour.</summary>
        private static Image LightArrow(Image original)
        {
            Image light;
            if (_lightArrows.TryGetValue(original, out light))
                return light;

            Bitmap source = original as Bitmap;
            if (source == null)
                return original;

            Bitmap copy = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    int alpha = source.GetPixel(x, y).A;
                    copy.SetPixel(x, y, alpha == 0 ? Color.Transparent : Color.FromArgb(alpha, ThemeColours.Text));
                }
            }

            _lightArrows[original] = copy;
            return copy;
        }

        /// <summary>Put every recoloured scroll button back the way the framework made it, for light mode.</summary>
        public static void ReleaseScrollButtons()
        {
            foreach (KeyValuePair<WeakReference, Image> entry in _themedScrollButtons)
            {
                Label label = entry.Key.Target as Label;
                if (label == null || label.IsDisposed)
                    continue;

                label.ResetBackColor();
                label.Image = entry.Value;
            }
            _themedScrollButtons.Clear();
        }
    }
}
