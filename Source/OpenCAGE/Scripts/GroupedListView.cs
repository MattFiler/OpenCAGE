using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DarkModeForms;

namespace OpenCAGE
{
    /// <summary>
    /// Custom ListView that properly handles arrow key navigation between groups
    /// </summary>
    public class GroupedListView : ListView
    {
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // DockContent is a Form: FindForm() would stop there without a DarkModeCS; defer and walk ancestors.
            BeginInvoke(new MethodInvoker(() => DarkModeCS.TryRefreshThemedListView(this)));
        }

        protected override void WndProc(ref Message m)
        {
            if (DarkModeCS.TryReflectListViewGroupCustomDraw(ref m, this))
                return;
            base.WndProc(ref m);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Handle arrow key navigation for grouped ListViews
            if (View == View.Details && Groups.Count > 0)
            {
                if (e.KeyCode == Keys.Down)
                {
                    HandleDownArrow();
                    e.Handled = true;
                    return;
                }
                else if (e.KeyCode == Keys.Up)
                {
                    HandleUpArrow();
                    e.Handled = true;
                    return;
                }
            }

            base.OnKeyDown(e);
        }

        private void HandleDownArrow()
        {
            if (SelectedItems.Count == 0)
            {
                // If nothing is selected, select the first item
                if (Items.Count > 0)
                {
                    Items[0].Selected = true;
                    Items[0].Focused = true;
                }
                return;
            }

            ListViewItem currentItem = SelectedItems[0];
            int currentIndex = currentItem.Index;

            // Find the next item in the list
            int nextIndex = currentIndex + 1;
            if (nextIndex < Items.Count)
            {
                ListViewItem nextItem = Items[nextIndex];
                currentItem.Selected = false;
                nextItem.Selected = true;
                nextItem.Focused = true;
                nextItem.EnsureVisible();
            }
        }

        private void HandleUpArrow()
        {
            if (SelectedItems.Count == 0)
            {
                // If nothing is selected, select the last item
                if (Items.Count > 0)
                {
                    Items[Items.Count - 1].Selected = true;
                    Items[Items.Count - 1].Focused = true;
                }
                return;
            }

            ListViewItem currentItem = SelectedItems[0];
            int currentIndex = currentItem.Index;

            // Find the previous item in the list
            int prevIndex = currentIndex - 1;
            if (prevIndex >= 0)
            {
                ListViewItem prevItem = Items[prevIndex];
                currentItem.Selected = false;
                prevItem.Selected = true;
                prevItem.Focused = true;
                prevItem.EnsureVisible();
            }
        }
    }
}
