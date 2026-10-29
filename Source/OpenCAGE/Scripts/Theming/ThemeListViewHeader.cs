using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OpenCAGE.Theming
{
    /// <summary>
    /// Draws a ListView's column header.
    ///
    /// The header is a SysHeader32 window of its own, and in dark mode it half-cooperates: it takes
    /// HDM_SETBKCOLOR but keeps drawing its text and its column separators from the light theme, so the
    /// captions come out black on black. Since it is a separate window, it can be taken over completely
    /// without touching how the list draws its rows - which is the trap the previous attempt fell into
    /// by using ListView.OwnerDraw, a control-wide switch that also disables commctrl's row painting.
    /// </summary>
    internal sealed class ThemeListViewHeader : NativeWindow
    {
        private const int WM_PAINT = 0x000F;
        private const int WM_ERASEBKGND = 0x0014;

        private const int HDM_FIRST = 0x1200;
        private const int HDM_GETITEMRECT = HDM_FIRST + 7;

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

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr window, int msg, IntPtr wParam, ref RECT lParam);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr window, out RECT rect);

        private static readonly Dictionary<ListView, ThemeListViewHeader> _painters =
            new Dictionary<ListView, ThemeListViewHeader>();

        private readonly ListView _listView;

        private ThemeListViewHeader(ListView listView)
        {
            _listView = listView;
        }

        public static void Attach(ListView listView, IntPtr header)
        {
            if (listView == null || header == IntPtr.Zero)
                return;

            ThemeListViewHeader painter;
            if (_painters.TryGetValue(listView, out painter))
            {
                if (painter.Handle == header)
                    return;

                //The list got a new handle, so the header did too
                painter.ReleaseHandle();
            }
            else
            {
                painter = new ThemeListViewHeader(listView);
                _painters.Add(listView, painter);
                listView.Disposed += painter.OnListDisposed;
            }

            painter.AssignHandle(header);
        }

        public static void Detach(ListView listView)
        {
            if (listView == null)
                return;

            ThemeListViewHeader painter;
            if (!_painters.TryGetValue(listView, out painter))
                return;

            listView.Disposed -= painter.OnListDisposed;
            painter.ReleaseHandle();
            _painters.Remove(listView);
        }

        private void OnListDisposed(object sender, EventArgs e)
        {
            Detach(sender as ListView);
        }

        protected override void WndProc(ref Message m)
        {
            if (!ThemeManager.IsDark)
            {
                base.WndProc(ref m);
                return;
            }

            if (m.Msg == WM_ERASEBKGND)
            {
                //Painting covers the whole client, so erasing first would only flicker
                m.Result = (IntPtr)1;
                return;
            }

            if (m.Msg == WM_PAINT)
            {
                Paint();
                m.Result = IntPtr.Zero;
                return;
            }

            base.WndProc(ref m);
        }

        private void Paint()
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
            catch
            {
            }
            finally
            {
                EndPaint(Handle, ref paint);
            }
        }

        private void Draw(Graphics graphics)
        {
            RECT client;
            if (!GetClientRect(Handle, out client))
                return;

            Rectangle bounds = Rectangle.FromLTRB(client.Left, client.Top, client.Right, client.Bottom);
            using (SolidBrush brush = new SolidBrush(ThemeColours.Header))
                graphics.FillRectangle(brush, bounds);

            using (Pen pen = new Pen(ThemeColours.Border))
                graphics.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);

            if (_listView == null || _listView.IsDisposed)
                return;

            Font font = _listView.Font;
            for (int i = 0; i < _listView.Columns.Count; i++)
            {
                RECT itemRect = new RECT();
                if (SendMessage(Handle, HDM_GETITEMRECT, (IntPtr)i, ref itemRect) == IntPtr.Zero)
                    continue;

                Rectangle column = Rectangle.FromLTRB(itemRect.Left, itemRect.Top, itemRect.Right, itemRect.Bottom);
                if (column.Width <= 0)
                    continue;

                Rectangle text = column;
                text.X += 6;
                text.Width -= 12;
                if (text.Width > 0)
                {
                    TextRenderer.DrawText(
                        graphics,
                        _listView.Columns[i].Text,
                        font,
                        text,
                        ThemeColours.Text,
                        AlignmentFor(_listView.Columns[i].TextAlign));
                }

                //Separator on the trailing edge, so columns read as columns
                using (Pen pen = new Pen(ThemeColours.Border))
                    graphics.DrawLine(pen, column.Right - 1, column.Top + 3, column.Right - 1, column.Bottom - 4);
            }
        }

        private static TextFormatFlags AlignmentFor(HorizontalAlignment alignment)
        {
            TextFormatFlags flags = TextFormatFlags.VerticalCenter
                | TextFormatFlags.EndEllipsis
                | TextFormatFlags.SingleLine;

            switch (alignment)
            {
                case HorizontalAlignment.Center:
                    return flags | TextFormatFlags.HorizontalCenter;
                case HorizontalAlignment.Right:
                    return flags | TextFormatFlags.Right;
                default:
                    return flags | TextFormatFlags.Left;
            }
        }
    }
}
