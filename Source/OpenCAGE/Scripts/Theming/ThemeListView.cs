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
            }
            else
            {
                ThemeListViewHeader.Detach(listView);
                ItemColourWatcher.Detach(listView);
            }

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
        /// Re-applies row colours when a list's contents change.
        ///
        /// WinForms exposes no event for "the items changed", and lists all over this app are populated
        /// long after their window was themed - which is why some of them still had white rows while
        /// others looked right. Watching the insert and clear messages catches every one of them without
        /// each call site having to remember to ask.
        /// </summary>
        private sealed class ItemColourWatcher : NativeWindow
        {
            //WinForms is a Unicode app, so items arrive through the W variant
            private const int LVM_INSERTITEMW = LVM_FIRST + 77;
            private const int LVM_DELETEITEM = LVM_FIRST + 8;
            private const int LVM_DELETEALLITEMS = LVM_FIRST + 9;
            private const int LVM_GETGROUPRECT = LVM_FIRST + 98;
            private const int WM_PAINT = 0x000F;

            /// <summary>Ask LVM_GETGROUPRECT for the header strip rather than the whole group.</summary>
            private const int LVGGR_HEADER = 1;

            /// <summary>ListViewGroup.ID is internal, and it is the only way to address a group natively.</summary>
            private static readonly System.Reflection.PropertyInfo GroupIdProperty =
                typeof(ListViewGroup).GetProperty("ID",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            private readonly ListView _listView;
            private bool _pending;
            private bool _applying;

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
                }

                watcher.AssignHandle(listView.Handle);
            }

            public static void Detach(ListView listView)
            {
                if (listView == null)
                    return;

                ItemColourWatcher watcher;
                if (!_watchers.TryGetValue(listView, out watcher))
                    return;

                listView.Disposed -= watcher.OnListDisposed;
                watcher.ReleaseHandle();
                _watchers.Remove(listView);
            }

            private void OnListDisposed(object sender, EventArgs e)
            {
                Detach(sender as ListView);
            }

            protected override void WndProc(ref Message m)
            {
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

                if (m.Msg != LVM_INSERTITEMW && m.Msg != LVM_DELETEITEM && m.Msg != LVM_DELETEALLITEMS)
                    return;

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

                            using (SolidBrush brush = new SolidBrush(ThemeColours.Input))
                                graphics.FillRectangle(brush, bounds);

                            Rectangle text = bounds;
                            text.X += 2;
                            text.Width -= 4;
                            TextRenderer.DrawText(
                                graphics,
                                group.Header,
                                _listView.Font,
                                text,
                                ThemeColours.Text,
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
                if (_listView == null || _listView.IsDisposed || !ThemeManager.IsDark)
                    return;

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
