using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Keeps keyboard navigation in a grouped ListView on the items.
    ///
    /// With groups shown, commctrl treats every group header as a row of its own: Up from the first item
    /// of a group lands on that group's header, Down from the last item lands on the next group's, and
    /// Page Up/Down land on whichever row sits at the edge of the page. A header that takes focus selects
    /// every item in its group, which on the entity list means thousands of selection notifications and
    /// a multi-entity inspector repopulate for a keypress that only meant "previous row". Nothing makes a
    /// header unfocusable, so the moves that would land on one are made here instead, straight to the
    /// neighbouring item; every other keypress still goes to the native control.
    /// </summary>
    internal static class ListViewGroupNavigation
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr window, int msg, IntPtr wParam, IntPtr lParam);

        private const int LVM_FIRST = 0x1000;
        private const int LVM_GETSELECTIONMARK = LVM_FIRST + 66;
        private const int LVM_SETSELECTIONMARK = LVM_FIRST + 67;
        private const int WM_VSCROLL = 0x0115;
        private const int SB_TOP = 6;

        public static void Attach(ListView listView)
        {
            listView.KeyDown -= OnKeyDown;
            listView.KeyDown += OnKeyDown;
        }

        private static void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Alt)
                return;
            if (e.KeyCode != Keys.Up && e.KeyCode != Keys.Down && e.KeyCode != Keys.PageUp && e.KeyCode != Keys.PageDown)
                return;

            ListView listView = sender as ListView;
            if (listView == null || !listView.IsHandleCreated || listView.View != View.Details)
                return;
            if (!listView.ShowGroups || listView.Groups.Count == 0 || listView.Items.Count == 0)
                return;

            List<ListViewItem> rows = DisplayOrder(listView);
            ListViewItem current = listView.FocusedItem;
            if (current == null && listView.SelectedItems.Count > 0)
                current = listView.SelectedItems[0];
            int position = current == null ? -1 : rows.IndexOf(current);

            ListViewItem target;
            if (position < 0)
            {
                //Nothing to move from: the first row is the first group's header, so start on its first item
                target = rows[0];
            }
            else
            {
                switch (e.KeyCode)
                {
                    case Keys.Up:
                        if (position == 0)
                        {
                            //Only the first group's header is above; stay put
                            Swallow(e);
                            return;
                        }
                        if (rows[position - 1].Group == current.Group)
                            return; //a move inside the group, which commctrl makes itself
                        target = rows[position - 1];
                        break;
                    case Keys.Down:
                        if (position == rows.Count - 1)
                            return; //nothing below; commctrl stays put
                        if (rows[position + 1].Group == current.Group)
                            return;
                        target = rows[position + 1];
                        break;
                    default:
                        //commctrl pages to whichever row sits at the edge of the page, header or not, so
                        //page by items instead
                        int page = Math.Max(1, listView.ClientSize.Height / Math.Max(1, current.Bounds.Height));
                        int paged = e.KeyCode == Keys.PageUp ? position - page : position + page;
                        target = rows[Math.Max(0, Math.Min(rows.Count - 1, paged))];
                        break;
                }
            }

            Swallow(e);
            MoveTo(listView, rows, target, e.Shift, e.Control);
        }

        private static void Swallow(KeyEventArgs e)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        /// <summary>
        /// The rows as drawn: groups in their own order, items within a group in list order. The list's
        /// own order interleaves groups, so it cannot be walked for neighbours. Read from the groups
        /// rather than the list: the list's indexer asks the native control for every item, which is a
        /// window message per row on every keypress, while a group holds a plain array that the list
        /// keeps in step as items come and go.
        /// </summary>
        private static List<ListViewItem> DisplayOrder(ListView listView)
        {
            List<ListViewItem> rows = new List<ListViewItem>(listView.Items.Count);
            for (int g = 0; g < listView.Groups.Count; g++)
            {
                foreach (ListViewItem item in listView.Groups[g].Items)
                    if (item.ListView == listView) //a group can hold items that were never added to the list
                        rows.Add(item);
            }

            //Items without a group draw in a default group after the others
            if (rows.Count != listView.Items.Count)
            {
                for (int i = 0; i < listView.Items.Count; i++)
                {
                    ListViewItem item = listView.Items[i];
                    if (item.Group == null)
                        rows.Add(item);
                }
            }
            return rows;
        }

        /// <summary>
        /// Move the way commctrl would have for the same modifiers: plain moves the selection, Shift
        /// selects the run from the anchor to the new focus and nothing else, Ctrl moves only the focus.
        /// </summary>
        private static void MoveTo(ListView listView, List<ListViewItem> rows, ListViewItem target, bool extend, bool focusOnly)
        {
            if (!listView.MultiSelect)
            {
                extend = false;
                focusOnly = false;
            }

            if (focusOnly)
            {
                target.Focused = true;
            }
            else if (extend)
            {
                int anchorIndex = (int)SendMessage(listView.Handle, LVM_GETSELECTIONMARK, IntPtr.Zero, IntPtr.Zero);
                int to = rows.IndexOf(target);
                int anchor = anchorIndex >= 0 && anchorIndex < listView.Items.Count ? rows.IndexOf(listView.Items[anchorIndex]) : -1;
                if (anchor < 0)
                    anchor = to;
                int first = Math.Min(anchor, to);
                int last = Math.Max(anchor, to);
                for (int i = 0; i < rows.Count; i++)
                {
                    bool inRange = i >= first && i <= last;
                    if (rows[i].Selected != inRange)
                        rows[i].Selected = inRange;
                }
                target.Focused = true;
                //Selecting an item moves the mark onto it; the anchor has to stay where it was for the
                //next Shift+arrow to extend from the same end
                SendMessage(listView.Handle, LVM_SETSELECTIONMARK, IntPtr.Zero, (IntPtr)rows[anchor].Index);
            }
            else
            {
                //Deselect before selecting, as commctrl does, so a multi-select listener never sees both
                ListViewItem[] selected = new ListViewItem[listView.SelectedItems.Count];
                listView.SelectedItems.CopyTo(selected, 0);
                for (int i = 0; i < selected.Length; i++)
                    if (selected[i] != target)
                        selected[i].Selected = false;
                target.Selected = true;
                target.Focused = true;
                SendMessage(listView.Handle, LVM_SETSELECTIONMARK, IntPtr.Zero, (IntPtr)target.Index);
            }

            target.EnsureVisible();
            RevealHeader(listView, rows, target);
        }

        /// <summary>
        /// EnsureVisible scrolls an item to the top edge, which leaves the header above the first item of
        /// a group just out of view; bring it back as commctrl would have when passing through it.
        /// </summary>
        private static void RevealHeader(ListView listView, List<ListViewItem> rows, ListViewItem target)
        {
            int row = rows.IndexOf(target);
            if (row > 0 && rows[row - 1].Group == target.Group)
                return;

            //The header sits between the previous group's last item and this one, so showing that item
            //shows the header. Above the first group's header there is only the top of the list.
            //(Pixel scrolling with LVM_SCROLL is not clamped at the top in grouped report view.)
            if (row > 0)
                rows[row - 1].EnsureVisible();
            else
                SendMessage(listView.Handle, WM_VSCROLL, (IntPtr)SB_TOP, IntPtr.Zero);
        }
    }
}
