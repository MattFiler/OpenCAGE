using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OpenCAGE.Theming
{
    /// <summary>
    /// ListView theming.
    ///
    /// The rows are coloured per item rather than only through the LVM colour messages, because a
    /// grouped list falls back to the system window colour for items regardless of those - and because
    /// alternating row colours have to come from somewhere. Item colours are re-applied automatically
    /// whenever the list's contents change, so a list populated long after it was themed still gets them.
    ///
    /// Group headers are drawn entirely by the visual style and cannot be recoloured: a subclass counting
    /// reflected notifications confirmed no LVCDI_GROUP custom draw is ever sent. They follow the theme,
    /// which is why AllowDarkModeForWindow matters here - without it the list keeps the light theme's
    /// blue group headers even though hover and selection look correct.
    ///
    /// Deliberately NOT used: ListView.OwnerDraw. It is a control-wide switch that takes row, selection
    /// and hot-track painting away from commctrl. The column header is handled instead by subclassing its
    /// own window - see <see cref="ThemeListViewHeader"/>.
    ///
    /// Two things commctrl paints in COLOR_BTNFACE whatever the theme or the LVM colours say, both measured
    /// at (240,240,240): grid lines, and the whole list while it is disabled (the Backup Manager disables
    /// its list for as long as a backup takes, and it went white). Dark mode takes both over - see the
    /// watcher's grid lines and disabled state below.
    /// </summary>
    internal static class ThemeListView
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr window, int msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, EntryPoint = "SendMessage")]
        private static extern IntPtr SendMessageRect(IntPtr window, int msg, IntPtr wParam, ref RECT lParam);

        [DllImport("user32.dll")]
        private static extern bool RedrawWindow(IntPtr window, IntPtr updateRect, IntPtr updateRegion, uint flags);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr window, string subAppName, string subIdList);

        private const uint RDW_INVALIDATE = 0x0001;
        private const uint RDW_ERASE = 0x0004;
        private const uint RDW_UPDATENOW = 0x0100;
        private const uint RDW_ALLCHILDREN = 0x0080;

        private const int LVM_FIRST = 0x1000;
        private const int LVM_GETHEADER = LVM_FIRST + 31;
        private const int LVM_SETBKCOLOR = LVM_FIRST + 4;
        private const int LVM_SETTEXTBKCOLOR = LVM_FIRST + 38;
        private const int LVM_SETTEXTCOLOR = LVM_FIRST + 36;
        private const int LVM_SETEXTENDEDLISTVIEWSTYLE = LVM_FIRST + 54;
        private const int LVS_EX_GRIDLINES = 0x1;

        /// <summary>Sent with the LVM colour messages to go back to the control's own defaults.</summary>
        private static readonly IntPtr ColourDefault = (IntPtr)(-1);

        private static readonly Dictionary<ListView, ItemColourWatcher> _watchers =
            new Dictionary<ListView, ItemColourWatcher>();

        /// <summary>
        /// Apply (or undo) the dark treatment. Safe to call repeatedly - a ListView that gets a new
        /// handle, or has its items rebuilt, needs this again to keep its colours.
        /// </summary>
        public static void Apply(ListView listView, bool dark)
        {
            if (listView == null || listView.IsDisposed)
                return;

            if (!listView.IsHandleCreated)
            {
                //Not realised yet - come back once it is, or the native messages go nowhere
                listView.HandleCreated += OnHandleCreatedReapply;
                return;
            }

            ApplyInternal(listView, dark);
        }

        private static void OnHandleCreatedReapply(object sender, EventArgs e)
        {
            ListView listView = sender as ListView;
            if (listView == null)
                return;

            listView.HandleCreated -= OnHandleCreatedReapply;
            ApplyInternal(listView, ThemeManager.IsDark);
        }

        private static void ApplyInternal(ListView listView, bool dark)
        {
            if (listView == null || listView.IsDisposed || !listView.IsHandleCreated)
                return;

            //Before SetWindowTheme, and easy to miss: the dark theme classes only take on a window that
            //has been opted in. Without this the list themes far enough for hover and selection to look
            //right while group headers stay on the light theme's blue.
            ThemeNative.AllowDarkModeForWindow(listView.Handle, dark);
            SetWindowTheme(listView.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);

            Color listBackground = dark ? ThemeColours.Input : SystemColors.Window;
            Color listForeground = dark ? ThemeColours.Text : SystemColors.WindowText;

            listView.BackColor = listBackground;
            listView.ForeColor = listForeground;

            SendMessage(listView.Handle, LVM_SETBKCOLOR, IntPtr.Zero, dark ? ToCref(listBackground) : ColourDefault);
            SendMessage(listView.Handle, LVM_SETTEXTBKCOLOR, IntPtr.Zero, dark ? ToCref(listBackground) : ColourDefault);
            SendMessage(listView.Handle, LVM_SETTEXTCOLOR, IntPtr.Zero, dark ? ToCref(listForeground) : ColourDefault);

            ApplyItemColours(listView, dark);

            IntPtr header = SendMessage(listView.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
            if (dark)
            {
                ThemeNative.AllowDarkModeForWindow(header, true);
                ThemeListViewHeader.Attach(listView, header);
                ItemColourWatcher.Attach(listView);
                ItemColourWatcher.MarkApplied(listView);
            }
            else
            {
                ThemeListViewHeader.Detach(listView);
                ItemColourWatcher.Detach(listView);
            }

            //commctrl's own grid lines are COLOR_BTNFACE, bright white on the dark rows, and nothing recolours them:
            //in dark mode they are switched off underneath WinForms (its GridLines stays as it was) and the watcher
            //draws them instead. Back in light mode commctrl draws them again, if the list has them.
            SendMessage(listView.Handle, LVM_SETEXTENDEDLISTVIEWSTYLE, (IntPtr)LVS_EX_GRIDLINES,
                !dark && listView.GridLines ? (IntPtr)LVS_EX_GRIDLINES : IntPtr.Zero);

            listView.Invalidate(true);
            RedrawWindow(listView.Handle, IntPtr.Zero, IntPtr.Zero,
                RDW_INVALIDATE | RDW_ERASE | RDW_UPDATENOW | RDW_ALLCHILDREN);
        }

        /// <summary>
        /// Colour the rows, alternating so a row can be followed across to a distant column. Selection
        /// and hover are still drawn over the top by commctrl, so they stay distinct from both shades.
        /// </summary>
        private static void ApplyItemColours(ListView listView, bool dark)
        {
            if (listView == null || listView.IsDisposed || listView.VirtualMode)
                return;

            int row = 0;
            foreach (ListViewItem item in listView.Items)
            {
                //The collection hands back nulls while the handle is being rebuilt
                if (item == null)
                    continue;

                if (dark)
                {
                    item.BackColor = (row & 1) == 0 ? ThemeColours.Input : ThemeColours.InputAlternate;
                    item.ForeColor = ThemeColours.Text;
                }
                else
                {
                    item.BackColor = SystemColors.Window;
                    item.ForeColor = SystemColors.WindowText;
                }

                row++;
            }
        }

        private static IntPtr ToCref(Color colour)
        {
            return (IntPtr)(uint)ColorTranslator.ToWin32(colour);
        }

        /// <summary>
        /// Re-apply after the caller has rebuilt a list's items, columns or groups. Rarely needed now
        /// that the watcher below notices content changes on its own, but harmless and explicit.
        /// </summary>
        public static void Refresh(ListView listView)
        {
            if (listView == null || listView.IsDisposed || !ThemeManager.IsDark)
                return;

            if (listView.IsHandleCreated)
                ApplyInternal(listView, true);
            else
                listView.HandleCreated += OnHandleCreatedReapply;
        }

        /// <summary>
        /// The caller has just given every row its colours itself (see RowColours), so the reapply the
        /// inserts queued has nothing to do; the list only needs repainting.
        /// </summary>
        public static void RowsColoured(ListView listView)
        {
            if (listView == null || listView.IsDisposed || !ThemeManager.IsDark)
                return;

            ItemColourWatcher.MarkApplied(listView);
            listView.Invalidate();
        }

        /// <summary>
        /// The colours the row at this position gets, for a caller that can set them before the row is
        /// listed: on an unlisted row that is a field write, where the pass over a listed one is a repaint
        /// request per row. False (with the defaults) when the light theme is on and nothing is needed.
        /// </summary>
        public static bool RowColours(int row, out Color back, out Color fore)
        {
            if (!ThemeManager.IsDark)
            {
                back = SystemColors.Window;
                fore = SystemColors.WindowText;
                return false;
            }

            back = (row & 1) == 0 ? ThemeColours.Input : ThemeColours.InputAlternate;
            fore = ThemeColours.Text;
            return true;
        }

        /// <summary>
        /// Re-applies row colours when a list's contents change.
        ///
        /// WinForms exposes no event for "the items changed", and lists all over this app are populated
        /// long after their window was themed - which is why some of them still had white rows while
        /// others looked right. Watching the insert and clear messages catches every one of them without
        /// each call site having to remember to ask.
        ///
        /// Being the dark theme's hook on the list's window, it also paints the parts commctrl only draws
        /// in COLOR_BTNFACE: the grid lines, and the disabled state.
        /// </summary>
        private sealed class ItemColourWatcher : NativeWindow
        {
            //WinForms is a Unicode app, so items arrive through the W variant
            private const int LVM_INSERTITEMW = LVM_FIRST + 77;
            private const int LVM_DELETEITEM = LVM_FIRST + 8;
            private const int LVM_DELETEALLITEMS = LVM_FIRST + 9;
            private const int LVM_GETGROUPRECT = LVM_FIRST + 98;
            private const int LVM_GETITEMCOUNT = LVM_FIRST + 4;
            private const int LVM_GETITEMRECT = LVM_FIRST + 14;
            private const int LVM_GETTOPINDEX = LVM_FIRST + 39;
            private const int LVM_APPROXIMATEVIEWRECT = LVM_FIRST + 64;
            private const int LVM_ISGROUPVIEWENABLED = LVM_FIRST + 175;
            private const int HDM_GETITEMCOUNT = 0x1200;
            private const int HDM_GETITEMRECT = 0x1200 + 7;
            private const int WM_PAINT = 0x000F;
            private const int WM_ERASEBKGND = 0x0014;
            private const int SB_HORZ = 0;

            //Custom draw, as WinForms reflects it back to the list
            private const int WM_REFLECT_NOTIFY = 0x2000 + 0x004E;
            private const int NM_CUSTOMDRAW = -12;
            private const int CDDS_PREPAINT = 0x00000001;
            private const int CDDS_POSTPAINT = 0x00000002;
            private const int CDDS_ITEMPREPAINT = 0x00010001;
            private const int CDDS_SUBITEM = 0x00020000;
            private const int CDRF_NOTIFYPOSTPAINT = 0x00000010;

            [StructLayout(LayoutKind.Sequential)]
            private struct NMHDR
            {
                public IntPtr hwndFrom;
                public IntPtr idFrom;
                public int code;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct NMLVCUSTOMDRAW
            {
                public NMHDR hdr;
                public int dwDrawStage;
                public IntPtr hdc;
                public RECT rc;
                public IntPtr dwItemSpec;
                public int uItemState;
                public IntPtr lItemlParam;
                public int clrText;
                public int clrTextBk;
            }

            private static readonly int CodeOffset = Marshal.OffsetOf(typeof(NMHDR), "code").ToInt32();
            private static readonly int TextOffset = Marshal.OffsetOf(typeof(NMLVCUSTOMDRAW), "clrText").ToInt32();
            private static readonly int TextBackOffset = Marshal.OffsetOf(typeof(NMLVCUSTOMDRAW), "clrTextBk").ToInt32();

            [DllImport("user32.dll")]
            private static extern bool GetClientRect(IntPtr window, out RECT rect);

            [DllImport("user32.dll")]
            private static extern bool GetWindowRect(IntPtr window, out RECT rect);

            [DllImport("user32.dll")]
            private static extern int MapWindowPoints(IntPtr from, IntPtr to, ref RECT points, int count);

            [DllImport("user32.dll")]
            private static extern bool IsWindowVisible(IntPtr window);

            [DllImport("user32.dll")]
            private static extern int GetScrollPos(IntPtr window, int bar);

            /// <summary>Ask LVM_GETGROUPRECT for the header strip rather than the whole group.</summary>
            private const int LVGGR_HEADER = 1;

            /// <summary>ListViewGroup.ID is internal, and it is the only way to address a group natively.</summary>
            private static readonly System.Reflection.PropertyInfo GroupIdProperty =
                typeof(ListViewGroup).GetProperty("ID",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            private readonly ListView _listView;
            private bool _pending;
            private bool _applying;
            private bool _dirty;

            private ItemColourWatcher(ListView listView)
            {
                _listView = listView;
            }

            public static void Attach(ListView listView)
            {
                if (listView == null || !listView.IsHandleCreated)
                    return;

                ItemColourWatcher watcher;
                if (_watchers.TryGetValue(listView, out watcher))
                {
                    if (watcher.Handle == listView.Handle)
                        return;

                    //Handle was recreated under us
                    watcher.ReleaseHandle();
                }
                else
                {
                    watcher = new ItemColourWatcher(listView);
                    _watchers.Add(listView, watcher);
                    listView.Disposed += watcher.OnListDisposed;
                    listView.EnabledChanged += watcher.OnListEnabledChanged;
                }

                watcher.AssignHandle(listView.Handle);
            }

            /// <summary>
            /// An explicit Refresh has just coloured every row, so the reapply queued by the inserts that
            /// preceded it has nothing left to do. Populating a list used to colour it twice over.
            /// </summary>
            public static void MarkApplied(ListView listView)
            {
                ItemColourWatcher watcher;
                if (listView != null && _watchers.TryGetValue(listView, out watcher))
                    watcher._dirty = false;
            }

            public static void Detach(ListView listView)
            {
                if (listView == null)
                    return;

                ItemColourWatcher watcher;
                if (!_watchers.TryGetValue(listView, out watcher))
                    return;

                listView.Disposed -= watcher.OnListDisposed;
                listView.EnabledChanged -= watcher.OnListEnabledChanged;
                watcher.ReleaseHandle();
                _watchers.Remove(listView);
            }

            private void OnListDisposed(object sender, EventArgs e)
            {
                Detach(sender as ListView);
            }

            /// <summary>
            /// The disabled colours follow the list's Enabled, which a disabled parent turns off too. commctrl
            /// repaints when its own window is enabled or disabled, but nothing repaints it for a parent's.
            /// </summary>
            private void OnListEnabledChanged(object sender, EventArgs e)
            {
                if (ThemeManager.IsDark && _listView != null && !_listView.IsDisposed)
                    _listView.Invalidate();
            }

            protected override void WndProc(ref Message m)
            {
                if (ThemeManager.IsDark)
                {
                    //Never let commctrl turn its own grid lines back on: WinForms sends every extended style
                    //again whenever one of them changes (CheckBoxes, FullRowSelect...)
                    if (m.Msg == LVM_SETEXTENDEDLISTVIEWSTYLE)
                    {
                        int mask = (int)m.WParam;
                        if (mask == 0 || (mask & LVS_EX_GRIDLINES) != 0)
                            m.LParam = (IntPtr)((int)m.LParam & ~LVS_EX_GRIDLINES);
                    }
                    else if (m.Msg == WM_ERASEBKGND && !_listView.Enabled)
                    {
                        //A disabled list erases in COLOR_BTNFACE, whatever LVM_SETBKCOLOR said
                        RECT client;
                        if (GetClientRect(Handle, out client))
                            Fill(m.WParam, Rectangle.FromLTRB(client.Left, client.Top, client.Right, client.Bottom), ThemeColours.InputDisabled);
                        m.Result = (IntPtr)1;
                        return;
                    }
                }

                base.WndProc(ref m);

                if (_applying || !ThemeManager.IsDark)
                    return;

                if (m.Msg == WM_PAINT)
                {
                    //After commctrl has drawn, not instead of it - only the group header strips are
                    //repainted, so rows, selection and hot tracking are untouched
                    PaintGroupHeaders();
                    return;
                }

                if (m.Msg == WM_REFLECT_NOTIFY)
                {
                    OnCustomDraw(ref m);
                    return;
                }

                if (m.Msg != LVM_INSERTITEMW && m.Msg != LVM_DELETEITEM && m.Msg != LVM_DELETEALLITEMS)
                    return;

                _dirty = true;

                //Coalesced: populating a list sends one of these per row, and recolouring per row would
                //make loading a large composite crawl
                if (_pending)
                    return;

                _pending = true;
                try
                {
                    _listView.BeginInvoke(new MethodInvoker(Reapply));
                }
                catch
                {
                    //Handle went away between the message and the post
                    _pending = false;
                }
            }

            /// <summary>
            /// After WinForms has handled a reflected custom draw (it always asks commctrl for item notifications).
            ///
            /// Disabled: WinForms gives a disabled list's rows no colours at all, and commctrl then fills them,
            /// and everything behind them, with COLOR_BTNFACE - so the rows get the theme's disabled colours here,
            /// and the background at pre-paint, which is where a double-buffered list paints it (into its buffer,
            /// where WM_ERASEBKGND can't reach).
            ///
            /// Grid lines: drawn at post-paint, into commctrl's own DC, so they land in its back buffer when it has
            /// one and in WM_PRINTCLIENT (DrawToBitmap) as well as on screen.
            /// </summary>
            private void OnCustomDraw(ref Message m)
            {
                if (m.LParam == IntPtr.Zero || Marshal.ReadInt32(m.LParam, CodeOffset) != NM_CUSTOMDRAW)
                    return;

                NMLVCUSTOMDRAW draw = (NMLVCUSTOMDRAW)Marshal.PtrToStructure(m.LParam, typeof(NMLVCUSTOMDRAW));
                if (draw.hdr.hwndFrom != Handle || _listView == null || _listView.IsDisposed)
                    return;

                switch (draw.dwDrawStage)
                {
                    case CDDS_PREPAINT:
                        if (!_listView.Enabled)
                            Fill(draw.hdc, Rectangle.FromLTRB(draw.rc.Left, draw.rc.Top, draw.rc.Right, draw.rc.Bottom), ThemeColours.InputDisabled);
                        if (HasGridLines())
                            m.Result = (IntPtr)((int)m.Result | CDRF_NOTIFYPOSTPAINT);
                        break;

                    case CDDS_POSTPAINT:
                        if (HasGridLines())
                            PaintGridLines(draw.hdc);
                        break;

                    case CDDS_ITEMPREPAINT:
                    case CDDS_ITEMPREPAINT | CDDS_SUBITEM:
                        if (_listView.Enabled)
                            break;
                        Marshal.WriteInt32(m.LParam, TextOffset, ColorTranslator.ToWin32(ThemeColours.TextDisabled));
                        Marshal.WriteInt32(m.LParam, TextBackOffset, ColorTranslator.ToWin32(ThemeColours.InputDisabled));
                        break;
                }
            }

            private static void Fill(IntPtr hdc, Rectangle area, Color colour)
            {
                if (hdc == IntPtr.Zero || area.Width <= 0 || area.Height <= 0)
                    return;

                using (Graphics graphics = Graphics.FromHdc(hdc))
                using (SolidBrush brush = new SolidBrush(colour))
                    graphics.FillRectangle(brush, area);
            }

            /// <summary>
            /// Whether the list wants grid lines where commctrl would draw them: report view only. A list
            /// showing groups gets none - commctrl lays its rows out around the group headers, and no list
            /// here has both.
            /// </summary>
            private bool HasGridLines()
            {
                return _listView.GridLines && _listView.View == View.Details
                    && SendMessage(Handle, LVM_ISGROUPVIEWENABLED, IntPtr.Zero, IntPtr.Zero) == IntPtr.Zero;
            }

            /// <summary>
            /// The grid lines commctrl would draw, in the theme's divider colour, at exactly its positions
            /// (checked pixel for pixel, scrolled both ways): along the bottom pixel row of every row, carried
            /// on at the same pitch down the empty space below the last one, and down the first pixel past the
            /// right-hand edge of every column.
            /// </summary>
            private void PaintGridLines(IntPtr hdc)
            {
                RECT client;
                if (hdc == IntPtr.Zero || !GetClientRect(Handle, out client))
                    return;

                int top = client.Top;
                IntPtr header = SendMessage(Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
                if (header != IntPtr.Zero && IsWindowVisible(header))
                {
                    RECT headerRect;
                    GetWindowRect(header, out headerRect);
                    MapWindowPoints(IntPtr.Zero, Handle, ref headerRect, 2);
                    top = Math.Max(top, headerRect.Bottom);
                }

                //The row pitch from the top row, or with no rows from how much taller one more row makes the list
                int first = top, pitch;
                if ((int)SendMessage(Handle, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero) > 0)
                {
                    RECT row = new RECT(); //Left = LVIR_BOUNDS
                    IntPtr topIndex = SendMessage(Handle, LVM_GETTOPINDEX, IntPtr.Zero, IntPtr.Zero);
                    if (SendMessageRect(Handle, LVM_GETITEMRECT, topIndex, ref row) == IntPtr.Zero)
                        return;
                    first = row.Top;
                    pitch = row.Bottom - row.Top;
                }
                else
                {
                    pitch = ApproximateHeight(2) - ApproximateHeight(1);
                }
                if (pitch <= 0)
                    return;

                try
                {
                    using (Graphics graphics = Graphics.FromHdc(hdc))
                    using (Pen pen = new Pen(ThemeColours.Border))
                    {
                        graphics.IntersectClip(Rectangle.FromLTRB(client.Left, top, client.Right, client.Bottom));

                        for (int y = first + pitch - 1; y < client.Bottom; y += pitch)
                            graphics.DrawLine(pen, client.Left, y, client.Right, y);

                        if (header == IntPtr.Zero)
                            return;

                        //Header item rects are in column display order, and start at 0 however far the list is scrolled
                        int scrolled = GetScrollPos(Handle, SB_HORZ);
                        int columns = (int)SendMessage(header, HDM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
                        for (int i = 0; i < columns; i++)
                        {
                            RECT column = new RECT();
                            if (SendMessageRect(header, HDM_GETITEMRECT, (IntPtr)i, ref column) == IntPtr.Zero)
                                continue;
                            int x = column.Right - scrolled;
                            graphics.DrawLine(pen, x, top, x, client.Bottom);
                        }
                    }
                }
                catch
                {
                    //Cosmetic - a list that won't give up its geometry just goes without
                }
            }

            private int ApproximateHeight(int rows)
            {
                return ((int)SendMessage(Handle, LVM_APPROXIMATEVIEWRECT, (IntPtr)rows, (IntPtr)(-1)) >> 16) & 0xFFFF;
            }

            /// <summary>
            /// Repaint the group header strips.
            ///
            /// commctrl draws group headers from the visual style and sends no custom draw for them (a
            /// subclass counting reflected notifications saw CDDS_ITEMPREPAINT for items and never once
            /// for LVCDI_GROUP), so the accent-blue caption cannot be intercepted - only covered. Only
            /// the header rects are touched, which leaves every other part of the list to commctrl.
            /// </summary>
            private void PaintGroupHeaders()
            {
                if (_listView == null || _listView.IsDisposed || _listView.Groups.Count == 0)
                    return;

                try
                {
                    using (Graphics graphics = Graphics.FromHwnd(Handle))
                    {
                        for (int i = 0; i < _listView.Groups.Count; i++)
                        {
                            ListViewGroup group = _listView.Groups[i];

                            //An empty group draws no header natively, but LVM_GETGROUPRECT still reports
                            //a rect for it - painting that put captions on groups that aren't there
                            if (group == null || group.Items.Count == 0)
                                continue;

                            Rectangle bounds;
                            if (!TryGetHeaderRect(group, out bounds) || bounds.Height <= 0 || bounds.Width <= 0)
                                continue;

                            //On the disabled list's colours while it is disabled (see OnCustomDraw)
                            using (SolidBrush brush = new SolidBrush(_listView.Enabled ? ThemeColours.Input : ThemeColours.InputDisabled))
                                graphics.FillRectangle(brush, bounds);

                            Rectangle text = bounds;
                            text.X += 2;
                            text.Width -= 4;
                            TextRenderer.DrawText(
                                graphics,
                                group.Header,
                                _listView.Font,
                                text,
                                _listView.Enabled ? ThemeColours.Text : ThemeColours.TextDisabled,
                                TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                                    | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

                            //The rule the native header draws under its caption, kept so groups still read as groups
                            using (Pen pen = new Pen(ThemeColours.Border))
                                graphics.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
                        }
                    }
                }
                catch
                {
                    //Cosmetic - a list that won't give up its group rects just keeps the native headers
                }
            }

            private bool TryGetHeaderRect(ListViewGroup group, out Rectangle bounds)
            {
                bounds = Rectangle.Empty;
                if (GroupIdProperty == null || group == null)
                    return false;

                int id;
                try
                {
                    id = (int)GroupIdProperty.GetValue(group, null);
                }
                catch
                {
                    return false;
                }

                //LVM_GETGROUPRECT takes which part it should report back in the rect's top field
                RECT rect = new RECT { Top = LVGGR_HEADER };
                if (SendMessageRect(Handle, LVM_GETGROUPRECT, (IntPtr)id, ref rect) == IntPtr.Zero)
                    return false;

                bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
                return true;
            }

            private void Reapply()
            {
                _pending = false;
                if (!_dirty || _listView == null || _listView.IsDisposed || !ThemeManager.IsDark)
                    return;

                _dirty = false;
                _applying = true;
                try
                {
                    ApplyItemColours(_listView, true);
                }
                finally
                {
                    _applying = false;
                }
            }
        }
    }
}
