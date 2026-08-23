using CATHODE;
using CATHODE.Animations;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.AnimTrees
{
    /// <summary>
    /// Edits the selected animation node's properties, using the same PropertyGrid the scripting entity
    /// inspector uses. The rows themselves are built by <see cref="AnimationNodeProxy"/>.
    /// </summary>
    public partial class AnimationNodeEditor : DockContent
    {
        private AnimationNode _currentNode;
        private AnimationTree _currentTree;
        private AnimationNodeProxy _proxy;

        private EditAnimations _animationPicker;
        private EditBlendSets _blendSetPicker;

        //Which rows were bold when the grid last built them - the grid holds onto that decision, so a row
        //that crosses its default needs the rows built again rather than merely repainted
        private readonly HashSet<string> _boldRows = new HashSet<string>();
        private bool _rebuildQueued = false;

        public event Action<AnimationNode> NodeNameChanged;

        public AnimationNodeEditor()
        {
            InitializeComponent();
            Theming.ThemeManager.ApplyToForm(this);
            CloseButton = false;
            CloseButtonVisible = false;

            HookGrid();
        }

        public bool PopulateData(AnimationNode node, AnimationTree tree = null)
        {
            _currentNode = node;
            if (tree != null)
                _currentTree = tree;
            else if (node is AnimationTree animTree)
                _currentTree = animTree;

            if (node == null)
            {
                Text = "";
                _proxy = null;
                propertyGrid.SelectedObject = null;
                _boldRows.Clear();
                return false;
            }

            Text = node.Name + " [" + node.Type.ToString() + "]";

            _proxy = new AnimationNodeProxy(this, node, _currentTree);
            propertyGrid.SelectedObject = _proxy;
            RecordBoldRows();
            ApplyRowHeight();
            return true;
        }

        public void RefreshCurrentNode()
        {
            PopulateData(_currentNode, _currentTree);
        }

        /// <summary>Build the rows from scratch - the shape of the node has changed, not just a value.</summary>
        public void RebuildAfterEdit()
        {
            if (!IsHandleCreated || _rebuildQueued)
                return;

            //Deferred: this arrives from inside the grid's own commit, which is no time to replace its rows
            _rebuildQueued = true;
            BeginInvoke(new Action(() =>
            {
                _rebuildQueued = false;
                RefreshCurrentNode();
            }));
        }

        /// <summary>A row wrote its value into the node.</summary>
        public void OnNodeEdited(AnimationNodeDescriptor descriptor)
        {
            //Values, and the summaries of the groups above them, are read on paint - this is enough for those
            propertyGrid.Refresh();

            /* Bold is not. The grid works out which rows are off their default as it builds them and
             * keeps the answer, so a row crossing that line - either way - needs the rows built again,
             * holding on to what was expanded and which row was selected, since this lands mid-edit. */
            if (descriptor != null && descriptor.Path != null && descriptor.IsModified() != _boldRows.Contains(descriptor.Path))
                RebuildRows();
        }

        /// <summary>A node was renamed through its row.</summary>
        public void OnNodeRenamed(AnimationNode node)
        {
            if (node == null)
                return;

            Text = node.Name + " [" + node.Type.ToString() + "]";
            NodeNameChanged?.Invoke(node);
        }

        #region Browsers
        /* A name that refers to something the editor can open - an animation, a blend set - is chosen in
         * the browser for it rather than typed, reached through the grid's own edit button, the same way
         * a scripting parameter's resources are. The row is still text underneath, so a name can be typed
         * if that is quicker. */

        public void PickAnimation(AnimationNodeDescriptor descriptor)
        {
            if (descriptor == null)
                return;

            if (_animationPicker != null && !_animationPicker.IsDisposed)
                _animationPicker.Close();

            //Open it on the set this tree belongs to, so the clips it offers are the ones that can play here
            string current = descriptor.GetValue(descriptor.Proxy) as string ?? "";
            _animationPicker = new EditAnimations(EditAnimations.PickMode.Animation, _currentTree?.Set, current);
            _animationPicker.Text = string.IsNullOrEmpty(_currentNode?.Name)
                ? "Choose an animation"
                : "Choose an animation for '" + _currentNode.Name + "'";

            _animationPicker.OnPicked += name => Apply(descriptor, name);
            _animationPicker.FormClosed += (s, args) => _animationPicker = null;
            _animationPicker.Show();
        }

        public void PickBlendSet(AnimationNodeDescriptor descriptor)
        {
            if (descriptor == null)
                return;

            if (_blendSetPicker != null && !_blendSetPicker.IsDisposed)
                _blendSetPicker.Close();

            string current = descriptor.GetValue(descriptor.Proxy) as string ?? "";
            _blendSetPicker = new EditBlendSets(true, current);
            _blendSetPicker.OnPicked += name => Apply(descriptor, name);
            _blendSetPicker.FormClosed += (s, args) => _blendSetPicker = null;
            _blendSetPicker.Show();
        }

        /// <summary>
        /// Write a picked name into the row it was chosen for, if that row is still on screen.
        ///
        /// The browsers are modeless, so the editor carries on underneath them and the node showing when
        /// one comes back may not be the one it was opened for. Every node gets its own proxy, so a row
        /// belonging to a different one is a row that is no longer there to write to.
        /// </summary>
        private void Apply(AnimationNodeDescriptor descriptor, string name)
        {
            if (string.IsNullOrEmpty(name) || _proxy == null || !ReferenceEquals(descriptor.Proxy, _proxy))
                return;

            descriptor.SetValue(_proxy, name);
            BringToFront();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_animationPicker != null && !_animationPicker.IsDisposed)
                _animationPicker.Close();
            if (_blendSetPicker != null && !_blendSetPicker.IsDisposed)
                _blendSetPicker.Close();

            base.OnFormClosed(e);
        }
        #endregion

        #region Grid plumbing

        private TextBox _gridEditBox;
        private MethodInfo _gridCommitMethod;
        private Control _gridView;

        /// <summary>Commit whatever is half-typed in the grid, so it is in the node before a save reads it.</summary>
        public void CommitPendingEdits()
        {
            if (_gridEditBox == null || !_gridEditBox.Visible || !_gridEditBox.Modified)
                return;

            try { _gridCommitMethod?.Invoke(_gridView, null); } catch { }
        }

        /* Build the rows again, holding on to what was expanded and which row was selected. Handing the
         * grid the same object twice is what makes it ask for the rows afresh. */
        private void RebuildRows()
        {
            if (_proxy == null)
                return;

            List<string> expanded = new List<string>();
            string selected = PathOf(propertyGrid.SelectedGridItem);
            CollectExpanded(RootItem(), expanded);

            propertyGrid.SelectedObject = null;
            propertyGrid.SelectedObject = _proxy;

            RestoreExpanded(RootItem(), expanded, selected);
            RecordBoldRows();
        }

        /* Remember which rows the grid drew bold, so we can tell when one changes */
        private void RecordBoldRows()
        {
            _boldRows.Clear();
            if (_proxy != null)
                RecordBoldRows(_proxy.GetProperties());
        }

        private void RecordBoldRows(PropertyDescriptorCollection rows)
        {
            foreach (PropertyDescriptor row in rows)
            {
                AnimationNodeDescriptor descriptor = row as AnimationNodeDescriptor;
                if (descriptor == null)
                    continue;

                if (descriptor.Path != null && descriptor.IsModified())
                    _boldRows.Add(descriptor.Path);

                NodeGroupDescriptor group = descriptor as NodeGroupDescriptor;
                if (group != null)
                    RecordBoldRows(group.Children);
            }
        }

        private GridItem RootItem()
        {
            GridItem item = propertyGrid.SelectedGridItem;
            while (item != null && item.GridItemType != GridItemType.Root)
                item = item.Parent;
            return item;
        }

        private static string PathOf(GridItem item)
        {
            return (item?.PropertyDescriptor as AnimationNodeDescriptor)?.Path;
        }

        private static void CollectExpanded(GridItem item, List<string> expanded)
        {
            if (item == null)
                return;

            foreach (GridItem child in item.GridItems)
            {
                if (child.Expandable && child.Expanded)
                {
                    string path = PathOf(child);
                    if (path != null)
                        expanded.Add(path);
                }
                CollectExpanded(child, expanded);
            }
        }

        /* Top down, so a row is expanded before the rows inside it are looked for */
        private static void RestoreExpanded(GridItem item, List<string> expanded, string selected)
        {
            if (item == null)
                return;

            foreach (GridItem child in item.GridItems)
            {
                string path = PathOf(child);
                if (path != null && child.Expandable && expanded.Contains(path))
                    child.Expanded = true;

                if (path != null && path == selected)
                    child.Select();

                RestoreExpanded(child, expanded, selected);
            }
        }

        private void HookGrid()
        {
            try
            {
                _gridView = propertyGrid.Controls.Cast<Control>().FirstOrDefault(o => o.GetType().Name == "PropertyGridView");
                if (_gridView == null)
                    return;

                //The edit box is created on demand by this internal property getter
                PropertyInfo editProperty = _gridView.GetType().GetProperty("Edit", BindingFlags.Instance | BindingFlags.NonPublic);
                _gridEditBox = editProperty?.GetValue(_gridView, null) as TextBox;
                _gridCommitMethod = _gridView.GetType().GetMethod("Commit", BindingFlags.Instance | BindingFlags.NonPublic);

                _gridView.MouseDown += GridView_MouseDown;
                if (_gridEditBox != null)
                    _gridEditBox.MouseWheel += GridEditBox_MouseWheel;
            }
            catch
            {
                //Reflection into PropertyGrid internals failed - all of this is a nicety, carry on without it
                _gridEditBox = null;
            }
        }

        /* Make the grid rows a little taller than the cramped default (font height + 2) */
        private void ApplyRowHeight()
        {
            try
            {
                if (_gridView == null)
                    return;

                FieldInfo rowHeightField = _gridView.GetType().GetField("cachedRowHeight", BindingFlags.Instance | BindingFlags.NonPublic);
                rowHeightField?.SetValue(_gridView, _gridView.Font.Height + 7);
                _gridView.Invalidate();
            }
            catch { }
        }

        /* Single-click toggling for the bool checkbox glyphs (drawn just right of the label column) */
        private void GridView_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || _proxy == null)
                return;

            //Defer so the click has already moved the grid selection to the clicked row
            BeginInvoke(new Action(() =>
            {
                GridItem item = propertyGrid.SelectedGridItem;
                AnimationNodeDescriptor descriptor = item?.PropertyDescriptor as AnimationNodeDescriptor;
                if (descriptor == null || descriptor.PropertyType != typeof(bool))
                    return;

                int labelWidth = GridLabelWidth();
                if (labelWidth <= 0 || e.X <= labelWidth || e.X > labelWidth + 27)
                    return;

                descriptor.SetValue(_proxy, !(item.Value is bool current && current));
            }));
        }

        /* Scrolling the mouse wheel over a focused numeric input steps the value */
        private void GridEditBox_MouseWheel(object sender, MouseEventArgs e)
        {
            //Only step when the cursor is over the edit box itself - wheeling elsewhere keeps scrolling the grid
            if (_gridEditBox == null || !_gridEditBox.Visible)
                return;
            if (!_gridEditBox.ClientRectangle.Contains(_gridEditBox.PointToClient(Cursor.Position)))
                return;

            GridItem item = propertyGrid.SelectedGridItem;
            Type type = item?.PropertyDescriptor?.PropertyType;
            if (type != typeof(float) && type != typeof(int))
                return;

            //We're handling the wheel - don't let the grid scroll underneath the edit
            if (e is HandledMouseEventArgs handled)
                handled.Handled = true;

            int direction = e.Delta > 0 ? 1 : -1;
            string newText;
            if (type == typeof(int))
            {
                if (!int.TryParse(_gridEditBox.Text, out int intValue))
                    return;
                newText = (intValue + direction).ToString();
            }
            else
            {
                if (!float.TryParse(_gridEditBox.Text, out float floatValue))
                    return;
                newText = (floatValue + direction * 0.1f).ToString("0.######");
            }

            _gridEditBox.Text = newText;
            _gridEditBox.Modified = true;
            _gridEditBox.SelectAll();

            try { _gridCommitMethod?.Invoke(_gridView, null); } catch { }
        }

        private int GridLabelWidth()
        {
            try
            {
                PropertyInfo labelWidthProperty = _gridView.GetType().GetProperty("InternalLabelWidth", BindingFlags.Instance | BindingFlags.NonPublic);
                if (labelWidthProperty != null)
                    return (int)labelWidthProperty.GetValue(_gridView, null);

                FieldInfo labelWidthField = _gridView.GetType().GetField("labelWidth", BindingFlags.Instance | BindingFlags.NonPublic);
                if (labelWidthField != null)
                    return (int)labelWidthField.GetValue(_gridView);
            }
            catch { }

            return -1;
        }

        #endregion
    }
}
