using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OpenCAGE.Theming
{
    /// <summary>
    /// The controls Windows still won't draw dark, even with the process opted in.
    ///
    /// Everything here is attached and detached at runtime rather than being a control subclass, so the
    /// hundreds of designer files across the app don't have to change to pick it up.
    /// </summary>
    internal static class ThemePainters
    {
        private static readonly HashSet<GroupBox> _groupBoxes = new HashSet<GroupBox>();
        private static readonly HashSet<Control> _spinButtons = new HashSet<Control>();
        private static readonly HashSet<ButtonBase> _disabledCheckText = new HashSet<ButtonBase>();
        private static readonly Dictionary<TabControl, TabStripPainter> _tabControls = new Dictionary<TabControl, TabStripPainter>();

        #region GroupBox

        /// <summary>
        /// A GroupBox draws its frame from colours derived off the system, so on a dark background it
        /// comes out as a bright etched line. Repainting it is cheap because GroupBox is a WinForms-drawn
        /// control - the Paint event runs after its own OnPaint, so we simply draw over the top.
        /// </summary>
        public static void AttachGroupBox(GroupBox box)
        {
            if (box == null || _groupBoxes.Contains(box))
                return;

            _groupBoxes.Add(box);
            box.Paint += PaintGroupBox;
            box.Disposed += OnGroupBoxDisposed;
            box.Invalidate();
        }

        public static void DetachGroupBox(GroupBox box)
        {
            if (box == null || !_groupBoxes.Remove(box))
                return;

            box.Paint -= PaintGroupBox;
            box.Disposed -= OnGroupBoxDisposed;
            box.Invalidate();
        }

        private static void OnGroupBoxDisposed(object sender, EventArgs e)
        {
            _groupBoxes.Remove(sender as GroupBox);
        }

        private static void PaintGroupBox(object sender, PaintEventArgs e)
        {
            try
            {
                PaintGroupBoxCore(sender as GroupBox, e);
            }
            catch { }
        }

        private static void PaintGroupBoxCore(GroupBox box, PaintEventArgs e)
        {
            if (box == null)
                return;

            Graphics graphics = e.Graphics;
            Color background = box.BackColor;

            //Child controls are separate windows and are clipped out of this, so a full fill is safe
            using (SolidBrush brush = new SolidBrush(background))
                graphics.FillRectangle(brush, box.ClientRectangle);

            bool hasCaption = !string.IsNullOrEmpty(box.Text);
            Size captionSize = hasCaption
                ? TextRenderer.MeasureText(graphics, box.Text, box.Font)
                : Size.Empty;

            //The frame starts half way down the caption, which is what gives the label its cut-out
            int top = hasCaption ? captionSize.Height / 2 : 0;
            Rectangle frame = new Rectangle(0, top, box.Width - 1, box.Height - top - 1);
            if (frame.Width > 0 && frame.Height > 0)
            {
                using (Pen pen = new Pen(ThemeColours.Border))
                    graphics.DrawRectangle(pen, frame);
            }

            if (!hasCaption)
                return;

            Rectangle caption = new Rectangle(8, 0, captionSize.Width, captionSize.Height);
            using (SolidBrush brush = new SolidBrush(background))
                graphics.FillRectangle(brush, caption);

            TextRenderer.DrawText(
                graphics,
                box.Text,
                box.Font,
                caption,
                box.Enabled ? ThemeColours.Text : ThemeColours.TextDisabled,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine);
        }

        #endregion

        #region Disabled CheckBox/RadioButton labels

        /// <summary>
        /// WinForms draws a DISABLED check or radio label with the system's etched grey, ignoring
        /// ForeColor entirely - which on a dark surface is close to invisible (the texture editor's
        /// read-only flag lists were the report). Same trick as the GroupBox: the Paint event runs
        /// after the control's own drawing, so the label is simply painted again on top in the
        /// theme's disabled colour. Enabled controls honour ForeColor and are left alone.
        /// </summary>
        public static void AttachDisabledCheckText(ButtonBase control)
        {
            if (control == null || _disabledCheckText.Contains(control))
                return;
            if (!(control is CheckBox) && !(control is RadioButton))
                return;

            _disabledCheckText.Add(control);
            control.Paint += PaintDisabledCheckText;
            control.EnabledChanged += InvalidateDisabledCheckText;
            control.Disposed += OnDisabledCheckTextDisposed;
            if (!control.Enabled)
                control.Invalidate();
        }

        public static void DetachDisabledCheckText(ButtonBase control)
        {
            if (control == null || !_disabledCheckText.Remove(control))
                return;

            control.Paint -= PaintDisabledCheckText;
            control.EnabledChanged -= InvalidateDisabledCheckText;
            control.Disposed -= OnDisabledCheckTextDisposed;
            control.Invalidate();
        }

        private static void OnDisabledCheckTextDisposed(object sender, EventArgs e)
        {
            _disabledCheckText.Remove(sender as ButtonBase);
        }

        private static void InvalidateDisabledCheckText(object sender, EventArgs e)
        {
            Control control = sender as Control;
            if (control == null)
                return;

            //The theme picked the label colour from the enabled state at apply time - keep it right
            //as the state flips, or a box themed while disabled stays dim once it's enabled
            control.ForeColor = control.Enabled ? ThemeColours.Text : ThemeColours.TextDisabled;
            control.Invalidate();
        }

        private static void PaintDisabledCheckText(object sender, PaintEventArgs e)
        {
            try
            {
                PaintDisabledCheckTextCore(sender as ButtonBase, e);
            }
            catch { }
        }

        private static void PaintDisabledCheckTextCore(ButtonBase control, PaintEventArgs e)
        {
            if (control == null || control.Enabled || string.IsNullOrEmpty(control.Text))
                return;

            //Only the default layout - glyph on the left, text following - which is every check and
            //radio in the app. Anything exotic keeps the system's rendering rather than a bad guess.
            CheckBox check = control as CheckBox;
            RadioButton radio = control as RadioButton;
            if (check != null && (check.Appearance != Appearance.Normal || check.CheckAlign != ContentAlignment.MiddleLeft))
                return;
            if (radio != null && (radio.Appearance != Appearance.Normal || radio.CheckAlign != ContentAlignment.MiddleLeft))
                return;
            if (control.RightToLeft == RightToLeft.Yes)
                return;

            int glyphWidth = check != null
                ? CheckBoxRenderer.GetGlyphSize(e.Graphics, System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedDisabled).Width
                : RadioButtonRenderer.GetGlyphSize(e.Graphics, System.Windows.Forms.VisualStyles.RadioButtonState.UncheckedDisabled).Width;

            Rectangle textRect = new Rectangle(glyphWidth + 1, 0, control.Width - glyphWidth - 1, control.Height);
            if (textRect.Width <= 0 || textRect.Height <= 0)
                return;

            using (SolidBrush brush = new SolidBrush(control.BackColor))
                e.Graphics.FillRectangle(brush, textRect);

            //Text sits two pixels into the fill, which lands it where the enabled label draws
            textRect.X += 2;
            textRect.Width -= 2;
            TextRenderer.DrawText(
                e.Graphics,
                control.Text,
                control.Font,
                textRect,
                ThemeColours.TextDisabled,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        #endregion

        #region NumericUpDown spin buttons

        /// <summary>
        /// The up/down buttons on a NumericUpDown are a private child control that paints itself with the
        /// visual style's spin arrows - always light. Same trick as the GroupBox: draw over it.
        /// </summary>
        public static void AttachSpinButtons(UpDownBase upDown)
        {
            if (upDown == null)
                return;

            Control buttons = FindSpinButtons(upDown);
            if (buttons == null || _spinButtons.Contains(buttons))
                return;

            _spinButtons.Add(buttons);
            buttons.Paint += PaintSpinButtons;
            buttons.Disposed += OnSpinButtonsDisposed;
            buttons.Invalidate();
        }

        public static void DetachSpinButtons(UpDownBase upDown)
        {
            if (upDown == null)
                return;

            Control buttons = FindSpinButtons(upDown);
            if (buttons == null || !_spinButtons.Remove(buttons))
                return;

            buttons.Paint -= PaintSpinButtons;
            buttons.Disposed -= OnSpinButtonsDisposed;
            buttons.Invalidate();
        }

        private static Control FindSpinButtons(UpDownBase upDown)
        {
            foreach (Control child in upDown.Controls)
            {
                //Internal type, so there's nothing to cast to - the name is the only handle on it
                if (child.GetType().Name == "UpDownButtons")
                    return child;
            }

            return null;
        }

        private static void OnSpinButtonsDisposed(object sender, EventArgs e)
        {
            _spinButtons.Remove(sender as Control);
        }

        private static void PaintSpinButtons(object sender, PaintEventArgs e)
        {
            try
            {
                PaintSpinButtonsCore(sender as Control, e);
            }
            catch { }
        }

        private static void PaintSpinButtonsCore(Control buttons, PaintEventArgs e)
        {
            if (buttons == null || buttons.ClientRectangle.Height < 4)
                return;

            Graphics graphics = e.Graphics;
            Rectangle bounds = buttons.ClientRectangle;

            using (SolidBrush brush = new SolidBrush(ThemeColours.Raised))
                graphics.FillRectangle(brush, bounds);

            using (Pen pen = new Pen(ThemeColours.Border))
            {
                //Separate the buttons from the value, and each other
                graphics.DrawLine(pen, bounds.Left, bounds.Top, bounds.Left, bounds.Bottom);
                graphics.DrawLine(pen, bounds.Left + 1, bounds.Height / 2, bounds.Right, bounds.Height / 2);
            }

            Color arrow = buttons.Enabled ? ThemeColours.Text : ThemeColours.TextDisabled;
            DrawArrow(graphics, new Rectangle(bounds.Left, bounds.Top, bounds.Width, bounds.Height / 2), arrow, true);
            DrawArrow(graphics, new Rectangle(bounds.Left, bounds.Height / 2, bounds.Width, bounds.Height - bounds.Height / 2), arrow, false);
        }

        private static void DrawArrow(Graphics graphics, Rectangle bounds, Color colour, bool up)
        {
            const int halfWidth = 3;
            int centreX = bounds.Left + bounds.Width / 2 + 1;
            int centreY = bounds.Top + bounds.Height / 2;

            Point[] points = up
                ? new[]
                {
                    new Point(centreX - halfWidth, centreY + 1),
                    new Point(centreX + halfWidth, centreY + 1),
                    new Point(centreX, centreY - 2),
                }
                : new[]
                {
                    new Point(centreX - halfWidth, centreY - 1),
                    new Point(centreX + halfWidth, centreY - 1),
                    new Point(centreX, centreY + 2),
                };

            using (SolidBrush brush = new SolidBrush(colour))
            {
                System.Drawing.Drawing2D.SmoothingMode previous = graphics.SmoothingMode;
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.FillPolygon(brush, points);
                graphics.SmoothingMode = previous;
            }
        }

        #endregion

        #region TabControl

        /// <summary>
        /// A TabControl's strip is drawn entirely by the native control, and none of it follows BackColor,
        /// so the only way to get a dark strip is to take the paint message over completely. The pages
        /// are child windows and still paint themselves, so this only has to cover the strip and frame.
        /// </summary>
        public static void AttachTabControl(TabControl tabs)
        {
            if (tabs == null || _tabControls.ContainsKey(tabs))
                return;

            TabStripPainter painter = new TabStripPainter(tabs);
            _tabControls.Add(tabs, painter);
            painter.Attach();
        }

        public static void DetachTabControl(TabControl tabs)
        {
            if (tabs == null)
                return;

            TabStripPainter painter;
            if (!_tabControls.TryGetValue(tabs, out painter))
                return;

            painter.Detach();
            _tabControls.Remove(tabs);
        }

        private sealed class TabStripPainter : NativeWindow
        {
            private const int WM_PAINT = 0x000F;
            private const int WM_ERASEBKGND = 0x0014;

            [StructLayout(LayoutKind.Sequential)]
            private struct RECT
            {
                public int Left, Top, Right, Bottom;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct PAINTSTRUCT
            {
                public IntPtr Hdc;
                public bool Erase;
                public RECT Paint;
                public bool Restore;
                public bool IncUpdate;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
                public byte[] Reserved;
            }

            [DllImport("user32.dll")]
            private static extern IntPtr BeginPaint(IntPtr window, out PAINTSTRUCT paint);

            [DllImport("user32.dll")]
            private static extern bool EndPaint(IntPtr window, ref PAINTSTRUCT paint);

            private readonly TabControl _tabs;
            private int _hoveredTab = -1;

            public TabStripPainter(TabControl tabs)
            {
                _tabs = tabs;
            }

            public void Attach()
            {
                _tabs.HandleCreated += OnHandleCreated;
                _tabs.HandleDestroyed += OnHandleDestroyed;
                _tabs.MouseMove += OnMouseMove;
                _tabs.MouseLeave += OnMouseLeave;
                _tabs.Disposed += OnDisposed;

                if (_tabs.IsHandleCreated)
                    AssignHandle(_tabs.Handle);

                _tabs.Invalidate(true);
            }

            public void Detach()
            {
                _tabs.HandleCreated -= OnHandleCreated;
                _tabs.HandleDestroyed -= OnHandleDestroyed;
                _tabs.MouseMove -= OnMouseMove;
                _tabs.MouseLeave -= OnMouseLeave;
                _tabs.Disposed -= OnDisposed;

                ReleaseHandle();
                _tabs.Invalidate(true);
            }

            private void OnHandleCreated(object sender, EventArgs e)
            {
                AssignHandle(_tabs.Handle);
            }

            private void OnHandleDestroyed(object sender, EventArgs e)
            {
                ReleaseHandle();
            }

            private void OnDisposed(object sender, EventArgs e)
            {
                Detach();
                _tabControls.Remove(_tabs);
            }

            private void OnMouseLeave(object sender, EventArgs e)
            {
                if (_hoveredTab == -1)
                    return;

                _hoveredTab = -1;
                _tabs.Invalidate();
            }

            private void OnMouseMove(object sender, MouseEventArgs e)
            {
                int hovered = -1;
                for (int i = 0; i < _tabs.TabCount; i++)
                {
                    if (!_tabs.GetTabRect(i).Contains(e.Location))
                        continue;

                    hovered = i;
                    break;
                }

                if (hovered == _hoveredTab)
                    return;

                _hoveredTab = hovered;
                _tabs.Invalidate();
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_ERASEBKGND)
                {
                    //Painting covers the whole client, so erasing first would only flicker
                    m.Result = (IntPtr)1;
                    return;
                }

                if (m.Msg == WM_PAINT)
                {
                    PaintTabs();
                    m.Result = IntPtr.Zero;
                    return;
                }

                base.WndProc(ref m);
            }

            private void PaintTabs()
            {
                PAINTSTRUCT paint;
                IntPtr hdc = BeginPaint(Handle, out paint);
                if (hdc == IntPtr.Zero)
                    return;

                try
                {
                    using (Graphics graphics = Graphics.FromHdc(hdc))
                        Draw(graphics);
                }
                catch { }
                finally
                {
                    EndPaint(Handle, ref paint);
                }
            }

            private void Draw(Graphics graphics)
            {
                using (SolidBrush brush = new SolidBrush(ThemeColours.Surface))
                    graphics.FillRectangle(brush, _tabs.ClientRectangle);

                //Frame around the page area, so the content reads as belonging to the selected tab
                Rectangle page = _tabs.DisplayRectangle;
                page.Inflate(2, 2);
                page.Width -= 1;
                page.Height -= 1;
                if (page.Width > 0 && page.Height > 0)
                {
                    using (Pen pen = new Pen(ThemeColours.Border))
                        graphics.DrawRectangle(pen, page);
                }

                for (int i = 0; i < _tabs.TabCount; i++)
                    DrawTab(graphics, i, page);
            }

            private void DrawTab(Graphics graphics, int index, Rectangle page)
            {
                Rectangle bounds = _tabs.GetTabRect(index);
                bool selected = _tabs.SelectedIndex == index;
                bool hovered = _hoveredTab == index;

                Color background = selected
                    ? ThemeColours.Raised
                    : (hovered ? ThemeColours.Hover : ThemeColours.Surface);

                using (SolidBrush brush = new SolidBrush(background))
                    graphics.FillRectangle(brush, bounds);

                using (Pen pen = new Pen(ThemeColours.Border))
                {
                    //Leave the edge shared with the page open on the selected tab so the two join up
                    if (selected)
                    {
                        graphics.DrawLine(pen, bounds.Left, bounds.Top, bounds.Left, bounds.Bottom - 1);
                        graphics.DrawLine(pen, bounds.Left, bounds.Top, bounds.Right - 1, bounds.Top);
                        graphics.DrawLine(pen, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom - 1);
                    }
                    else
                    {
                        graphics.DrawRectangle(pen, bounds.Left, bounds.Top, bounds.Width - 1, bounds.Height - 1);
                    }
                }

                if (selected)
                {
                    //Accent along the top, the way Visual Studio marks the active tab
                    using (SolidBrush brush = new SolidBrush(ThemeColours.Accent))
                        graphics.FillRectangle(brush, bounds.Left + 1, bounds.Top + 1, bounds.Width - 2, 2);
                }

                Color foreground = !_tabs.Enabled
                    ? ThemeColours.TextDisabled
                    : (selected ? ThemeColours.Text : ThemeColours.TextDim);

                Rectangle text = bounds;
                text.Y += selected ? 2 : 0;
                TextRenderer.DrawText(
                    graphics,
                    _tabs.TabPages[index].Text,
                    _tabs.Font,
                    text,
                    foreground,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                        | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            }
        }

        #endregion
    }
}
