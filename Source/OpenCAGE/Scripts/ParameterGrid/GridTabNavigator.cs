using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Makes Tab and Enter work inside expanded value groups in a PropertyGrid (vector and
    /// transform rows).
    ///
    /// Committing a child of an expandable value replaces the whole parent value - the converter
    /// builds a fresh snapshot object - so the grid throws away and recreates every row beneath it,
    /// folding the expansion back up. The grid's own key handling then runs against the discarded
    /// rows: the selection lands on a dead entry, rows paint as empty bars, and the reused in-place
    /// edit box ends up writing into whichever live row happens to sit where the stale one was. The
    /// wheel-scroll stepping already repairs this after its own commits (see ParameterGridPanel);
    /// this does the same for the keys - commit, re-find the row by path (re-expanding the folded
    /// ancestors), then Tab steps to the neighbour while Enter stays put.
    ///
    /// The takeover is deliberately narrow: only Tab and Enter pressed while editing a CHILD row of
    /// an expanded value are handled here - everywhere else the grid's stock behaviour stands.
    /// </summary>
    public sealed class GridTabNavigator
    {
        private readonly PropertyGrid _grid;
        private readonly Control _view;
        private readonly TextBoxBase _edit;
        private readonly MethodInfo _commit;

        /* Attach to a grid, or return null when the grid's internals aren't what we expect -
           navigation is a nicety, so failure just means stock behaviour */
        public static GridTabNavigator Attach(PropertyGrid grid)
        {
            try
            {
                Control view = grid.Controls.Cast<Control>().FirstOrDefault(o => o.GetType().Name == "PropertyGridView");
                if (view == null)
                    return null;

                PropertyInfo editProperty =
                    view.GetType().GetProperty("Edit", BindingFlags.Instance | BindingFlags.NonPublic) ??
                    view.GetType().GetProperty("EditTextBox", BindingFlags.Instance | BindingFlags.NonPublic);
                TextBoxBase edit = editProperty?.GetValue(view, null) as TextBoxBase;
                MethodInfo commit = view.GetType().GetMethod("Commit", BindingFlags.Instance | BindingFlags.NonPublic);
                if (edit == null || commit == null)
                    return null;

                return new GridTabNavigator(grid, view, edit, commit);
            }
            catch
            {
                return null;
            }
        }

        private GridTabNavigator(PropertyGrid grid, Control view, TextBoxBase edit, MethodInfo commit)
        {
            _grid = grid;
            _view = view;
            _edit = edit;
            _commit = commit;

            TabFilter.Register(this);
        }

        /// <summary>
        /// The grid's in-place edit control consumes Tab and Enter during command-key preprocessing
        /// - before any KeyDown or dialog-key hook a subscriber could reach - so the only reliable
        /// place to take the keys over is an application message filter, which runs first of all.
        /// The filter only claims a key aimed at a registered grid's edit box while a child of an
        /// expanded value is selected; everything else passes straight through.
        /// </summary>
        private sealed class TabFilter : IMessageFilter
        {
            private const int WM_KEYDOWN = 0x0100;
            private const int VK_TAB = 0x09;
            private const int VK_RETURN = 0x0D;

            private static TabFilter _instance;
            private readonly List<GridTabNavigator> _navigators = new List<GridTabNavigator>();

            public static void Register(GridTabNavigator navigator)
            {
                if (_instance == null)
                {
                    _instance = new TabFilter();
                    Application.AddMessageFilter(_instance);
                }

                //Panels come and go with the editor's layout - drop the dead ones as new ones arrive
                _instance._navigators.RemoveAll(o => o._edit == null || o._edit.IsDisposed);
                _instance._navigators.Add(navigator);
            }

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != WM_KEYDOWN)
                    return false;
                int key = (int)m.WParam;
                if (key != VK_TAB && key != VK_RETURN)
                    return false;
                if ((Control.ModifierKeys & (Keys.Control | Keys.Alt)) != 0)
                    return false;

                foreach (GridTabNavigator navigator in _navigators)
                {
                    TextBoxBase edit = navigator._edit;
                    if (edit == null || edit.IsDisposed || !edit.IsHandleCreated || edit.Handle != m.HWnd)
                        continue;
                    if (!navigator.IsInsideExpandedValue())
                        return false;

                    //Enter commits and stays; Tab commits and steps (Shift+Tab steps back)
                    int direction = key == VK_RETURN ? 0
                        : (Control.ModifierKeys & Keys.Shift) != 0 ? -1 : +1;
                    navigator.Navigate(direction);
                    return true;
                }
                return false;
            }
        }

        /* Is the selected row a child inside an expanded value (rather than a top-level parameter)? */
        private bool IsInsideExpandedValue()
        {
            GridItem item = SafeSelectedItem();
            return item?.GridItemType == GridItemType.Property && item.Parent?.GridItemType == GridItemType.Property;
        }

        /* The selected row, or null once the grid has thrown it away - a discarded row keeps
           answering for its descriptor, so asking for its parent is the way to tell */
        private GridItem SafeSelectedItem()
        {
            try
            {
                GridItem item = _grid.SelectedGridItem;
                GridItem probe = item?.Parent;
                return item;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
        }

        private void Navigate(int direction)
        {
            //Remember where we are by label path - the commit is about to replace every row object
            List<string> path = PathOf(_grid.SelectedGridItem);

            try { _commit.Invoke(_view, null); } catch { }

            /* The commit rebuilt the rows AND folded the expanded value back up, and the grid put its
               selection on whatever survived (usually the collapsed parent). None of that can be
               trusted for stepping - re-find our row by path, re-expanding the ancestors on the way,
               and only then move to the neighbour. */
            GridItem current = FindByPath(path);
            if (current == null)
            {
                _grid.Refresh();
                current = FindByPath(path);
            }
            if (current == null)
                return;

            current.Select();

            //Enter stays on the row it committed - re-selected above, with the rows live again
            if (direction == 0)
            {
                FocusEdit();
                return;
            }

            //Tab steps to the neighbouring editable row in display order
            List<GridItem> visible = new List<GridItem>();
            FlattenVisible(RootOf(current), visible);
            int index = visible.IndexOf(current);
            if (index == -1)
                return;

            for (int i = index + direction; i >= 0 && i < visible.Count; i += direction)
            {
                if (visible[i].GridItemType != GridItemType.Property)
                    continue;

                visible[i].Select();
                FocusEdit();
                return;
            }
        }

        private void FocusEdit()
        {
            if (!_edit.Visible)
                return;
            _edit.Focus();
            _edit.SelectAll();
        }

        private static List<string> PathOf(GridItem item)
        {
            List<string> path = new List<string>();
            for (GridItem walk = item; walk != null; walk = SafeParent(walk))
                path.Insert(0, walk.Label ?? "");
            return path;
        }

        private static GridItem SafeParent(GridItem item)
        {
            try { return item.Parent; }
            catch (ObjectDisposedException) { return null; }
        }

        /* Walk from the root along a label path, expanding as we go - the commit folds the
           hierarchy back up, which is half of what the user sees go wrong */
        private GridItem FindByPath(List<string> path)
        {
            GridItem current = LiveRoot();
            if (current == null || path.Count == 0)
                return null;

            //The stored path starts at the (unlabelled) root
            for (int depth = 1; depth < path.Count; depth++)
            {
                GridItem next = null;
                foreach (GridItem child in current.GridItems)
                {
                    if (child.Label == path[depth])
                    {
                        next = child;
                        break;
                    }
                }
                if (next == null)
                    return null;

                //Everything above the final element has to be open for the row to be reachable
                if (depth < path.Count - 1 && next.Expandable && !next.Expanded)
                {
                    try { next.Expanded = true; }
                    catch { }
                }
                current = next;
            }
            return current;
        }

        /* The root of the live entry tree, or null when even the selection is a discarded row */
        private GridItem LiveRoot()
        {
            try
            {
                GridItem root = RootOf(_grid.SelectedGridItem);
                if (root == null)
                    return null;
                int probe = root.GridItems.Count; //throws if we walked up a discarded tree
                return root;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
        }

        private static GridItem RootOf(GridItem item)
        {
            GridItem walk = item;
            while (walk != null)
            {
                GridItem parent = SafeParent(walk);
                if (parent == null)
                    return walk;
                walk = parent;
            }
            return null;
        }

        private static void FlattenVisible(GridItem item, List<GridItem> into)
        {
            if (item == null)
                return;
            foreach (GridItem child in item.GridItems)
            {
                into.Add(child);
                if (child.Expandable && child.Expanded)
                    FlattenVisible(child, into);
            }
        }
    }
}
