using CATHODE.Scripting;
using OpenCAGE.Popups.Base;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    /// <summary>
    /// The Render Filters panel's row order: drag the filters into place and add dividers between groups.
    /// </summary>
    public partial class RenderFilterOrderWindow : BaseWindow
    {
        private sealed class Entry
        {
            public string Key;
            public string Label;
            public Image Icon;
            public bool IsDivider => Key == RenderFilters.DividerEntry;
            public override string ToString() => IsDivider ? "Divider" : Label;
        }

        private int _dragIndex = -1;
        private Point _dragStart;
        private int _dropIndex = -1;

        /// <summary>The order as shown, top to bottom.</summary>
        public List<string> Order => orderList.Items.Cast<Entry>().Select(entry => entry.Key).ToList();

        public RenderFilterOrderWindow(List<string> order)
        {
            InitializeComponent();
            StayAboveEditor = true;
            Fill(order);
            UpdateButtons();
        }

        private void Fill(List<string> order)
        {
            DisposeIcons();
            orderList.BeginUpdate();
            orderList.Items.Clear();
            foreach (string key in order)
            {
                Entry entry = MakeEntry(key);
                if (entry != null)
                    orderList.Items.Add(entry);
            }
            orderList.EndUpdate();
        }

        private static Entry MakeEntry(string key)
        {
            if (key == RenderFilters.DividerEntry)
                return new Entry() { Key = key };
            if (RenderFilters.TryParseSceneEntry(key, out SceneFilterKind kind) && RenderFilterDefinitions.TryGetSceneFilter(kind, out SceneFilterDefinition scene))
                return new Entry() { Key = key, Label = scene.Label, Icon = RenderFilters.CreateColorSwatch(RenderFilters.ToMenuColor(scene)) };
            if (RenderFilters.TryParseTypeEntry(key, out uint functionType) && RenderFilterDefinitions.TryGetDefinition((FunctionType)functionType, out RenderFilterDefinitions.Definition definition))
                return new Entry() { Key = key, Label = definition.FunctionType.ToString(), Icon = RenderFilters.CreateFilterListIcon(definition) };
            return null;
        }

        private void DisposeIcons()
        {
            foreach (Entry entry in orderList.Items.Cast<Entry>())
                entry.Icon?.Dispose();
        }

        private Entry SelectedEntry => orderList.SelectedIndex < 0 ? null : (Entry)orderList.Items[orderList.SelectedIndex];

        private void UpdateButtons()
        {
            int at = orderList.SelectedIndex;
            removeDividerButton.Enabled = SelectedEntry?.IsDivider == true;
            moveUpButton.Enabled = at > 0;
            moveDownButton.Enabled = at >= 0 && at < orderList.Items.Count - 1;
        }

        private void MoveSelected(int step)
        {
            int at = orderList.SelectedIndex;
            int to = at + step;
            if (at < 0 || to < 0 || to >= orderList.Items.Count)
                return;
            object entry = orderList.Items[at];
            orderList.Items.RemoveAt(at);
            orderList.Items.Insert(to, entry);
            orderList.SelectedIndex = to;
        }

        #region BUTTONS
        private void addDividerButton_Click(object sender, EventArgs e)
        {
            int at = orderList.SelectedIndex < 0 ? orderList.Items.Count : orderList.SelectedIndex + 1;
            orderList.Items.Insert(at, new Entry() { Key = RenderFilters.DividerEntry });
            orderList.SelectedIndex = at;
            orderList.Focus();
        }

        private void removeDividerButton_Click(object sender, EventArgs e)
        {
            int at = orderList.SelectedIndex;
            if (SelectedEntry?.IsDivider != true)
                return;
            orderList.Items.RemoveAt(at);
            if (orderList.Items.Count > 0)
                orderList.SelectedIndex = Math.Min(at, orderList.Items.Count - 1);
            UpdateButtons();
            orderList.Focus();
        }

        private void moveUpButton_Click(object sender, EventArgs e)
        {
            MoveSelected(-1);
            orderList.Focus();
        }

        private void moveDownButton_Click(object sender, EventArgs e)
        {
            MoveSelected(1);
            orderList.Focus();
        }

        private void resetButton_Click(object sender, EventArgs e)
        {
            Fill(RenderFilters.DefaultOrder());
            UpdateButtons();
        }

        private void orderList_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        private void orderList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && removeDividerButton.Enabled)
                removeDividerButton_Click(sender, e);
            else if (e.Alt && e.KeyCode == Keys.Up)
                MoveSelected(-1);
            else if (e.Alt && e.KeyCode == Keys.Down)
                MoveSelected(1);
            else
                return;
            e.Handled = true;
        }

        private void RenderFilterOrderWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
            DisposeIcons();
        }
        #endregion

        #region DRAG
        private void orderList_MouseDown(object sender, MouseEventArgs e)
        {
            _dragIndex = e.Button == MouseButtons.Left ? orderList.IndexFromPoint(e.Location) : -1;
            _dragStart = e.Location;
        }

        private void orderList_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragIndex < 0 || e.Button != MouseButtons.Left)
                return;
            Size threshold = SystemInformation.DragSize;
            if (Math.Abs(e.X - _dragStart.X) < threshold.Width / 2 && Math.Abs(e.Y - _dragStart.Y) < threshold.Height / 2)
                return;

            int from = _dragIndex;
            _dragIndex = -1;
            if (from >= orderList.Items.Count)
                return;
            orderList.DoDragDrop(orderList.Items[from], DragDropEffects.Move);
            SetDropIndex(-1);
        }

        private void orderList_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(Entry)))
            {
                e.Effect = DragDropEffects.None;
                return;
            }
            e.Effect = DragDropEffects.Move;

            Point point = orderList.PointToClient(new Point(e.X, e.Y));
            //Near the top or bottom edge the list scrolls under the drag
            if (point.Y < orderList.ItemHeight && orderList.TopIndex > 0)
                orderList.TopIndex--;
            else if (point.Y > orderList.ClientSize.Height - orderList.ItemHeight)
                orderList.TopIndex++;
            SetDropIndex(DropIndexAt(point));
        }

        private void orderList_DragDrop(object sender, DragEventArgs e)
        {
            Entry entry = e.Data.GetData(typeof(Entry)) as Entry;
            int to = _dropIndex;
            SetDropIndex(-1);
            int from = entry == null ? -1 : orderList.Items.IndexOf(entry);
            if (from < 0 || to < 0)
                return;
            if (to > from)
                to--;
            if (to == from)
                return;
            orderList.Items.RemoveAt(from);
            orderList.Items.Insert(to, entry);
            orderList.SelectedIndex = to;
        }

        private void orderList_DragLeave(object sender, EventArgs e)
        {
            SetDropIndex(-1);
        }

        /* The gap the dragged row would go into: before the row under the pointer, or after it past its middle */
        private int DropIndexAt(Point point)
        {
            int index = orderList.IndexFromPoint(point);
            if (index < 0)
                return orderList.Items.Count;
            Rectangle bounds = orderList.GetItemRectangle(index);
            return point.Y > bounds.Top + bounds.Height / 2 ? index + 1 : index;
        }

        private void SetDropIndex(int index)
        {
            if (_dropIndex == index)
                return;
            _dropIndex = index;
            orderList.Invalidate();
        }
        #endregion

        private void orderList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= orderList.Items.Count)
                return;
            Entry entry = (Entry)orderList.Items[e.Index];
            bool dark = Theming.ThemeManager.IsDark;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color back = selected ? (dark ? Theming.ThemeColours.Selection : SystemColors.Highlight) : orderList.BackColor;
            Color fore = selected && !dark ? SystemColors.HighlightText : orderList.ForeColor;

            using (SolidBrush brush = new SolidBrush(back))
                e.Graphics.FillRectangle(brush, e.Bounds);

            if (entry.IsDivider)
            {
                Color line = selected ? fore : (dark ? Theming.ThemeColours.BorderStrong : SystemColors.ControlDark);
                int y = e.Bounds.Top + e.Bounds.Height / 2;
                using (Pen pen = new Pen(line))
                    e.Graphics.DrawLine(pen, e.Bounds.Left + 4, y, e.Bounds.Right - 4, y);
            }
            else
            {
                if (entry.Icon != null)
                    e.Graphics.DrawImage(entry.Icon, e.Bounds.Left + 2, e.Bounds.Top + (e.Bounds.Height - 16) / 2, 16, 16);
                Rectangle text = new Rectangle(e.Bounds.Left + 22, e.Bounds.Top, e.Bounds.Width - 22, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, entry.Label, orderList.Font, text, fore,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }

            //Where a dragged row would land: a line on this row's top, or under the last row
            bool markTop = _dropIndex == e.Index;
            bool markBottom = _dropIndex == orderList.Items.Count && e.Index == orderList.Items.Count - 1;
            if (markTop || markBottom)
            {
                int y = markTop ? e.Bounds.Top : e.Bounds.Bottom - 2;
                using (SolidBrush mark = new SolidBrush(dark ? Theming.ThemeColours.Accent : SystemColors.HotTrack))
                    e.Graphics.FillRectangle(mark, e.Bounds.Left, y, e.Bounds.Width, 2);
            }
        }
    }
}
