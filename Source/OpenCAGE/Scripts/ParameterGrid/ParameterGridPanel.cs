using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json;
using OpenCAGE.DockPanels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>
    /// Compact table-style editor for entity parameters, replacing the old stacked UserControl list.
    /// Supports editing multiple entities at once: entities are grouped by type into tabs, and edits
    /// within a tab apply to every selected entity of that type.
    /// </summary>
    public class ParameterGridPanel : UserControl
    {
        //Modeless popup editors need a way back to the active grid to refresh/rebuild after their callbacks fire
        public static ParameterGridPanel Current;

        private readonly TabControl _tabs;
        private readonly PropertyGrid _grid;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _resetParam;
        private readonly ToolStripMenuItem _showAliases;
        private readonly ToolStripSeparator _valueSeparator;
        private readonly ToolStripMenuItem _copyValue;
        private readonly ToolStripMenuItem _pasteValue;

        private readonly List<TypeGroup> _groups = new List<TypeGroup>();
        private bool _suppressTabChange = false;

        public EntityInspector Inspector { get; private set; }
        public LevelContent Content { get; private set; }
        public Composite Composite { get; private set; }
        public bool FilterPinParameters { get; private set; }

        public bool IsMultiEditing => _groups.Sum(o => o.Proxies.Count) > 1;

        private class TypeGroup
        {
            public string Key;
            public string Label;
            public List<EntityParameterProxy> Proxies = new List<EntityParameterProxy>();
        }

        public ParameterGridPanel()
        {
            _grid = new PropertyGrid()
            {
                Dock = DockStyle.Fill,
                ToolbarVisible = false,
                HelpVisible = false,
                PropertySort = PropertySort.Alphabetical
            };

            _tabs = new TabControl()
            {
                Dock = DockStyle.Top,
                Height = 24,
                Visible = false
            };
            _tabs.SelectedIndexChanged += Tabs_SelectedIndexChanged;

            //Dock order: fill control first so the top-docked tabs stack above it
            Controls.Add(_grid);
            Controls.Add(_tabs);

            HookNumericScrolling();
            HookColourDoubleClick();
            GridTabNavigator.Attach(_grid);
            _grid.PropertyValueChanged += (s, e) => RepairAfterCommit();

            _resetParam = new ToolStripMenuItem("Reset to Default");
            _resetParam.Click += ResetParam_Click;
            _showAliases = new ToolStripMenuItem("Show Aliases");
            _showAliases.Click += ShowAliases_Click;
            _copyValue = new ToolStripMenuItem("Copy Value");
            _copyValue.Click += CopyValue_Click;
            _pasteValue = new ToolStripMenuItem("Paste Value");
            _pasteValue.Click += PasteValue_Click;
            _valueSeparator = new ToolStripSeparator();
            _menu = new ContextMenuStrip();
            _menu.Items.AddRange(new ToolStripItem[] { _resetParam, _showAliases, _valueSeparator, _copyValue, _pasteValue });
            _menu.Opening += Menu_Opening;
            _grid.ContextMenuStrip = _menu;
        }

        /* Show the given entities in the grid, grouped by type into tabs */
        public void ShowEntities(EntityInspector inspector, List<Entity> entities, Composite composite, LevelContent content, bool filterPinParameters)
        {
            Current = this;
            Inspector = inspector;
            Content = content;
            Composite = composite;
            FilterPinParameters = filterPinParameters;

            _groups.Clear();
            if (entities != null)
            {
                foreach (Entity entity in entities)
                {
                    if (entity == null) continue;
                    string key = GetTypeKey(entity);
                    TypeGroup group = _groups.FirstOrDefault(o => o.Key == key);
                    if (group == null)
                    {
                        group = new TypeGroup() { Key = key, Label = GetTypeLabel(entity) };
                        _groups.Add(group);
                    }
                    group.Proxies.Add(new EntityParameterProxy(this, entity, composite, content));
                }
            }

            int entityCount = _groups.Sum(o => o.Proxies.Count);
            bool multi = entityCount > 1;

            _suppressTabChange = true;
            _tabs.TabPages.Clear();
            if (multi)
            {
                foreach (TypeGroup group in _groups)
                {
                    TabPage page = new TabPage(group.Label + " (" + group.Proxies.Count + ")") { Tag = group };
                    _tabs.TabPages.Add(page);
                }
            }
            _tabs.Visible = multi;
            _suppressTabChange = false;

            ApplyGroup(_groups.Count > 0 ? _groups[0] : null);
            ApplyRowHeight();
        }

        public void ClearEntities()
        {
            _groups.Clear();
            _suppressTabChange = true;
            _tabs.TabPages.Clear();
            _suppressTabChange = false;
            _tabs.Visible = false;
            _grid.SelectedObjects = new object[0];
        }

        /* Re-read values from the entity data (e.g. after a viewer gizmo move or popup edit) */
        public void RefreshValues()
        {
            _grid.Refresh();
        }

        /* Recompute the linked-pin highlights (called live as flowgraph connections change) */
        public void RefreshStatuses()
        {
            bool changed = false;
            foreach (TypeGroup group in _groups)
                foreach (EntityParameterProxy proxy in group.Proxies)
                    changed |= proxy.RefreshLinkedPinStatuses();

            //The grid caches whether each row paints a custom value, so a status appearing or
            //disappearing needs the rows rebuilt - a plain refresh isn't enough
            if (changed)
                RebuildProperties();
        }

        /* Rebuild all parameter rows (e.g. after a parameter was added/removed externally) */
        public void RebuildProperties()
        {
            foreach (TypeGroup group in _groups)
                foreach (EntityParameterProxy proxy in group.Proxies)
                    proxy.InvalidateProperties();

            object[] selected = _grid.SelectedObjects;
            _grid.SelectedObjects = new object[0];
            _grid.SelectedObjects = selected;
        }

        /// <summary>
        /// True if any entity sharing this proxy's tab has the parameter modified from its default.
        /// Used for the bold "modified" label in multi-edit: the framework's merged descriptor only bolds
        /// when every descriptor reports modified, so they all answer for the group as a whole.
        /// </summary>
        public bool IsParameterModifiedAcrossGroup(EntityParameterProxy proxy, ShortGuid parameter)
        {
            TypeGroup group = _groups.FirstOrDefault(o => o.Proxies.Contains(proxy));
            if (group == null)
                return false;

            foreach (EntityParameterProxy member in group.Proxies)
            {
                if (member.Entity.variant == EntityVariant.VARIABLE)
                    return true;
                if (ParameterModificationTracker.IsParameterModified(member.Composite.shortGUID, member.Entity.shortGUID, parameter))
                    return true;
            }
            return false;
        }

        /* Mark a parameter as modified and raise the editor-wide modification events */
        public void NotifyParameterEdited(EntityParameterProxy proxy, Parameter parameter)
        {
            AttachAliasOverrideIfVirtual(proxy, parameter);
            ParameterModificationTracker.SetParameterModified(proxy.Composite.shortGUID, proxy.Entity.shortGUID, parameter.name);
            Singleton.OnEntityParameterModified?.Invoke(proxy.Entity, parameter, false);
            Singleton.OnParameterModified?.Invoke();

            //The 'name' parameter IS the entity name, so editing it renames the entity everywhere.
            //Deferred so listeners don't rebuild this grid while it's still committing the edit.
            if (parameter.name == ShortGuids.name)
            {
                Entity renamedEntity = proxy.Entity;
                string newName = (parameter.content as cString)?.value ?? "";
                if (IsHandleCreated)
                    BeginInvoke(new Action(() => Singleton.OnEntityRenamed?.Invoke(renamedEntity, newName)));
                else
                    Singleton.OnEntityRenamed?.Invoke(renamedEntity, newName);
            }

            //In multi-edit mode, make sure the edit actually reached every entity in the active tab.
            //The PropertyGrid's merged descriptors are supposed to fan edits out themselves, but when
            //they don't (or only hit some entities), we finish the job after the commit completes.
            if (IsMultiEditing)
            {
                _pendingMultiEdits.Add((proxy, parameter));
                if (!_multiEditFlushQueued)
                {
                    _multiEditFlushQueued = true;
                    if (IsHandleCreated)
                        BeginInvoke(new Action(FlushPendingMultiEdits));
                    else
                        FlushPendingMultiEdits();
                }
            }
        }

        /* A "virtual" alias row (showing the pointed-to entity's value) becomes a real override on first edit.
           The rows are then rebuilt (deferred, so the in-progress commit isn't disturbed) to pick up the
           orange highlight - the grid caches per-row paint state, so a repaint alone isn't enough. */
        private void AttachAliasOverrideIfVirtual(EntityParameterProxy proxy, Parameter parameter)
        {
            if (proxy.Entity.variant != EntityVariant.ALIAS)
                return;
            if (proxy.Entity.GetParameter(parameter.name) != null)
                return;
            proxy.Entity.parameters.Add(parameter);

            if (IsHandleCreated)
                BeginInvoke(new Action(RebuildProperties));
            else
                RebuildProperties();
        }

        private readonly List<(EntityParameterProxy proxy, Parameter parameter)> _pendingMultiEdits = new List<(EntityParameterProxy, Parameter)>();
        private bool _multiEditFlushQueued = false;

        private void FlushPendingMultiEdits()
        {
            _multiEditFlushQueued = false;
            List<(EntityParameterProxy proxy, Parameter parameter)> edits = _pendingMultiEdits.ToList();
            _pendingMultiEdits.Clear();

            TypeGroup group = ActiveGroup;
            if (group == null || edits.Count == 0)
                return;

            bool propagatedAnything = false;
            foreach (var editsForParam in edits.GroupBy(o => o.parameter.name))
            {
                List<EntityParameterProxy> editedProxies = editsForParam.Select(o => o.proxy).Distinct().ToList();
                if (editedProxies.Count >= group.Proxies.Count)
                    continue; //the grid fanned the edit out to everyone itself

                (EntityParameterProxy sourceProxy, Parameter sourceParam) = editsForParam.First();
                string paramName = sourceParam.name.ToString();
                foreach (EntityParameterProxy proxy in group.Proxies)
                {
                    if (editedProxies.Contains(proxy))
                        continue;

                    ParameterGridDescriptor descriptor = proxy.GetParameterDescriptor(paramName);
                    if (descriptor == null)
                        continue;
                    //Resources/splines are entity-specific and never propagate
                    if (descriptor is ResourceParameterDescriptor || descriptor is SplineParameterDescriptor)
                        continue;
                    if (!CopyParameterValue(sourceParam.content, descriptor.Parameter.content, descriptor is MappingParameterDescriptor))
                        continue;

                    AttachAliasOverrideIfVirtual(proxy, descriptor.Parameter);
                    ParameterModificationTracker.SetParameterModified(proxy.Composite.shortGUID, proxy.Entity.shortGUID, descriptor.Parameter.name);
                    Singleton.OnEntityParameterModified?.Invoke(proxy.Entity, descriptor.Parameter, false);
                    if (descriptor.Parameter.content is cTransform movedTransform)
                        Singleton.OnEntityMoved?.Invoke(movedTransform, proxy.Entity);
                    propagatedAnything = true;
                }
            }

            if (propagatedAnything)
                Singleton.OnParameterModified?.Invoke();

            //Grid entries cache whether they're "modified" (the bold label), so rebuild the rows rather
            //than just repainting - otherwise a freshly edited row stays unbolded until reselection
            RebuildProperties();
        }

        /* Copy a parameter value between entities (returns false if nothing changed or the type can't propagate) */
        private static bool CopyParameterValue(ParameterData from, ParameterData to, bool isMapping)
        {
            if (from == null || to == null || ReferenceEquals(from, to))
                return false;

            if (isMapping && from is cResource fromMapping && to is cResource toMapping)
            {
                if (toMapping.shortGUID == fromMapping.shortGUID) return false;
                toMapping.shortGUID = fromMapping.shortGUID;
                return true;
            }

            switch (from)
            {
                case cEnumString fromEnumString when to is cEnumString toEnumString:
                    if (toEnumString.value == fromEnumString.value && toEnumString.enumID == fromEnumString.enumID) return false;
                    toEnumString.value = fromEnumString.value;
                    toEnumString.enumID = fromEnumString.enumID;
                    return true;
                case cString fromString when to is cString toString:
                    if (toString.value == fromString.value) return false;
                    toString.value = fromString.value;
                    return true;
                case cBool fromBool when to is cBool toBool:
                    if (toBool.value == fromBool.value) return false;
                    toBool.value = fromBool.value;
                    return true;
                case cInteger fromInt when to is cInteger toInt:
                    if (toInt.value == fromInt.value) return false;
                    toInt.value = fromInt.value;
                    return true;
                case cFloat fromFloat when to is cFloat toFloat:
                    if (toFloat.value == fromFloat.value) return false;
                    toFloat.value = fromFloat.value;
                    return true;
                case cVector3 fromVec when to is cVector3 toVec:
                    if (toVec.value == fromVec.value) return false;
                    toVec.value = fromVec.value;
                    return true;
                case cTransform fromTransform when to is cTransform toTransform:
                    if (toTransform.position == fromTransform.position && toTransform.rotation == fromTransform.rotation) return false;
                    toTransform.position = fromTransform.position;
                    toTransform.rotation = fromTransform.rotation;
                    return true;
                case cEnum fromEnum when to is cEnum toEnum:
                    if (toEnum.enumID == fromEnum.enumID && toEnum.enumIndex == fromEnum.enumIndex) return false;
                    toEnum.enumID = fromEnum.enumID;
                    toEnum.enumIndex = fromEnum.enumIndex;
                    return true;
            }
            return false;
        }

        private void Tabs_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressTabChange)
                return;
            ApplyGroup(_tabs.SelectedTab?.Tag as TypeGroup);
        }

        private void ApplyGroup(TypeGroup group)
        {
            if (group == null || group.Proxies.Count == 0)
            {
                _grid.SelectedObjects = new object[0];
                return;
            }

            //Use the categorised view only when grouping data exists for this type
            bool hasGroups = group.Proxies.Any(o => ParameterGroupProvider.HasGroups(o.Entity));
            _grid.PropertySort = hasGroups ? PropertySort.Categorized : PropertySort.Alphabetical;
            _grid.SelectedObjects = group.Proxies.Cast<object>().ToArray();
        }

        private TypeGroup ActiveGroup
        {
            get
            {
                if (_groups.Count == 0)
                    return null;
                if (_tabs.Visible && _tabs.SelectedTab?.Tag is TypeGroup group)
                    return group;
                return _groups[0];
            }
        }

        private static string GetTypeKey(Entity entity)
        {
            if (entity is FunctionEntity function)
                return "F:" + function.function.AsUInt32;
            return entity.variant.ToString();
        }

        private string GetTypeLabel(Entity entity)
        {
            switch (entity.variant)
            {
                case EntityVariant.FUNCTION:
                    FunctionEntity function = (FunctionEntity)entity;
                    if (function.function.IsFunctionType)
                        return function.function.AsFunctionType.ToString();
                    Composite composite = Content?.Level?.Commands?.GetComposite(function.function);
                    if (composite != null)
                    {
                        string name = composite.name.Replace('\\', '/');
                        int idx = name.LastIndexOf('/');
                        return idx >= 0 ? name.Substring(idx + 1) : name;
                    }
                    return "Composite Instance";
                case EntityVariant.VARIABLE:
                    return "Variable";
                case EntityVariant.PROXY:
                    return "Proxy";
                case EntityVariant.ALIAS:
                    return "Alias";
                default:
                    return "Entity";
            }
        }

        #region Numeric scrolling
        private TextBox _gridEditBox;
        private System.Reflection.MethodInfo _gridCommitMethod;
        private Control _gridView;

        /* Restore the old NumericUpDown QoL: scrolling the mouse wheel over a focused numeric input steps the value.
           The wheel only steps when the cursor is actually over the edit box, so scrolling to navigate the grid is safe. */
        private void HookNumericScrolling()
        {
            try
            {
                _gridView = _grid.Controls.Cast<Control>().FirstOrDefault(o => o.GetType().Name == "PropertyGridView");
                if (_gridView == null)
                    return;

                //The edit box is created on demand by this internal property getter
                System.Reflection.PropertyInfo editProperty = _gridView.GetType().GetProperty("Edit",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                _gridEditBox = editProperty?.GetValue(_gridView, null) as TextBox;
                if (_gridEditBox == null)
                    return;

                _gridCommitMethod = _gridView.GetType().GetMethod("Commit",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                _gridEditBox.MouseWheel += GridEditBox_MouseWheel;
                _gridView.MouseDown += GridView_MouseDown;
            }
            catch
            {
                //Reflection into PropertyGrid internals failed - scrolling support is a nicety, carry on without it
                _gridEditBox = null;
            }
        }

        /* A double-click on a colour row opens the full picker. The grid's own answer to a double-click
           on a row with a value list is to step to the next entry - and the named-colour list means it
           never offers the picker's button at all - so the click is taken before the grid sees it, on
           both the row itself (the swatch) and the edit box that sits over the value once it's selected. */
        private void HookColourDoubleClick()
        {
            if (_gridView == null)
                return;
            new ColourRowDoubleClickHook(this, _gridView);
            if (_gridEditBox != null)
                new ColourRowDoubleClickHook(this, _gridEditBox);
        }

        private bool TryOpenColourPickerForSelectedRow()
        {
            string name = _grid.SelectedGridItem?.PropertyDescriptor?.Name;
            if (name == null)
                return false;

            //One descriptor per selected entity: the picked colour goes to all of them, as the editor button would
            List<(EntityParameterProxy Proxy, ColourParameterDescriptor Descriptor)> targets = new List<(EntityParameterProxy, ColourParameterDescriptor)>();
            foreach (object selected in _grid.SelectedObjects)
            {
                if (selected is EntityParameterProxy proxy && proxy.GetParameterDescriptor(name) is ColourParameterDescriptor descriptor)
                    targets.Add((proxy, descriptor));
            }
            if (targets.Count == 0)
                return false;

            Color current = targets[0].Descriptor.GetValue(targets[0].Proxy) is Color colour ? colour : Color.Black;
            if (ColourPickerEditor.TryPick(current, out Color chosen))
            {
                foreach ((EntityParameterProxy proxy, ColourParameterDescriptor descriptor) in targets)
                    descriptor.SetValue(proxy, chosen);
                _grid.Refresh();
            }
            return true;
        }

        private sealed class ColourRowDoubleClickHook : NativeWindow
        {
            private const int WM_LBUTTONDBLCLK = 0x0203;
            private readonly ParameterGridPanel _panel;

            public ColourRowDoubleClickHook(ParameterGridPanel panel, Control control)
            {
                _panel = panel;
                if (control.IsHandleCreated)
                    AssignHandle(control.Handle);
                control.HandleCreated += (sender, e) => AssignHandle(control.Handle);
                control.HandleDestroyed += (sender, e) => ReleaseHandle();
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_LBUTTONDBLCLK && _panel.TryOpenColourPickerForSelectedRow())
                    return;
                base.WndProc(ref m);
            }
        }

        /* Make the grid rows a little taller than the cramped default (font height + 2) */
        private void ApplyRowHeight()
        {
            try
            {
                if (_gridView == null)
                    return;
                System.Reflection.FieldInfo rowHeightField = _gridView.GetType().GetField("cachedRowHeight",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                rowHeightField?.SetValue(_gridView, _gridView.Font.Height + 7);
                _gridView.Invalidate();
            }
            catch { }
        }

        /* Single-click toggling for the bool checkbox glyphs (drawn just right of the label column) */
        private void GridView_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            //Defer so the click has already moved the grid selection to the clicked row
            BeginInvoke(new Action(() =>
            {
                GridItem item = _grid.SelectedGridItem;
                if (item?.PropertyDescriptor == null || item.PropertyDescriptor.PropertyType != typeof(bool))
                    return;

                int labelWidth = GetGridLabelWidth();
                if (labelWidth <= 0 || e.X <= labelWidth || e.X > labelWidth + 27)
                    return;

                bool newValue = !(item.Value is bool current && current);
                TypeGroup group = ActiveGroup;
                if (group == null)
                    return;
                foreach (EntityParameterProxy proxy in group.Proxies)
                {
                    ParameterGridDescriptor descriptor = proxy.GetParameterDescriptor(item.PropertyDescriptor.Name);
                    descriptor?.SetValue(proxy, newValue);
                }
                RefreshValues();
            }));
        }

        private int GetGridLabelWidth()
        {
            try
            {
                System.Reflection.PropertyInfo labelWidthProperty = _gridView.GetType().GetProperty("InternalLabelWidth",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (labelWidthProperty != null)
                    return (int)labelWidthProperty.GetValue(_gridView, null);

                System.Reflection.FieldInfo labelWidthField = _gridView.GetType().GetField("labelWidth",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (labelWidthField != null)
                    return (int)labelWidthField.GetValue(_gridView);
            }
            catch { }
            return -1;
        }

        private void GridEditBox_MouseWheel(object sender, MouseEventArgs e)
        {
            //Only step when the cursor is over the edit box itself - wheeling elsewhere keeps scrolling the grid
            if (_gridEditBox == null || !_gridEditBox.Visible)
                return;
            if (!_gridEditBox.ClientRectangle.Contains(_gridEditBox.PointToClient(Cursor.Position)))
                return;

            GridItem item = _grid.SelectedGridItem;
            Type type = item?.PropertyDescriptor?.PropertyType;
            if (type != typeof(float) && type != typeof(int))
                return;

            //We're handling the wheel - don't let the grid scroll underneath the edit
            if (e is HandledMouseEventArgs handled)
                handled.Handled = true;

            int direction = e.Delta > 0 ? 1 : -1;
            string newText = null;
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
                newText = (floatValue + direction * GetStepForItem(item)).ToString("0.######");
            }

            _gridEditBox.Text = newText;
            _gridEditBox.Modified = true;
            _gridEditBox.SelectAll();

            //Commit immediately so e.g. transform edits sync to the viewer per scroll step
            try { _gridCommitMethod?.Invoke(_gridView, null); } catch { }

            /* Committing an axis rebuilds the value above it - X/Y/Z live inside a Position that the
               grid replaces wholesale - which throws away the row we're sitting on and leaves the grid
               selecting a discarded one. Everything that asks for the selection from then on (the next
               scroll step, the context menu) would fail on it, so rebuild the rows and let the grid put
               the selection back on the live equivalent. */
            if (SelectedRowWasDiscarded())
                RefreshValues();
        }

        /// <summary>
        /// True once the grid has thrown away the row it still reports as selected. A discarded row keeps
        /// answering for its PropertyDescriptor, so asking for its parent is the way to tell.
        /// </summary>
        private bool SelectedRowWasDiscarded()
        {
            return IsDiscarded(_grid.SelectedGridItem);
        }

        private static bool IsDiscarded(GridItem item)
        {
            if (item == null)
                return false;
            try
            {
                GridItem parent = item.Parent;
                return false;
            }
            catch (ObjectDisposedException) { return true; }
        }

        /// <summary>
        /// Whether the grid's own list of rows still holds rows it has thrown away. A commit inside an
        /// expanded value replaces that value and rebuilds the rows beneath it; when the edit is two levels
        /// down (an axis inside Position inside position) the grid splices the new rows in but leaves the
        /// old siblings behind. Those rows are disposed: they paint as black bars, and a click on one
        /// hands the in-place edit box a row that no longer exists (issue 647).
        /// </summary>
        private bool HasDiscardedRows()
        {
            if (_gridView == null)
                return false;
            try
            {
                object rows = _gridView.GetType().GetField("allGridEntries",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(_gridView);
                if (!(rows is System.Collections.IEnumerable entries))
                    return false;
                foreach (object entry in entries)
                    if (entry is GridItem item && IsDiscarded(item))
                        return true;
            }
            catch { }
            return false;
        }

        /* Enter, Tab and the wheel put the rows right on their own paths (GridTabNavigator, above).
           Every other way to commit - clicking another row, focus leaving the grid - is the grid's own
           doing and nothing repairs it, so this runs after any committed value. Deferred, because a
           click commits first and then selects the row it hit, and that row may be one of the dead
           ones: the rebuild has to come after the grid has finished with the click. */
        private void RepairAfterCommit()
        {
            if (!IsHandleCreated)
                return;
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || _grid.IsDisposed)
                    return;
                if (!SelectedRowWasDiscarded() && !HasDiscardedRows())
                    return;

                //A full refresh rebuilds the row list and puts the selection back on the live row in
                //the same place, which is the one the click meant
                bool editHadFocus = _gridEditBox != null && _gridEditBox.Focused;
                _grid.Refresh();
                if (editHadFocus && _gridEditBox.Visible)
                {
                    _gridEditBox.Focus();
                    _gridEditBox.SelectAll();
                }
            }));
        }

        /* Walk one step up the row tree, treating a discarded row as having no parent rather than throwing */
        private static GridItem ParentOf(GridItem item)
        {
            try { return item?.Parent; }
            catch (ObjectDisposedException) { return null; }
        }

        private static float GetStepForItem(GridItem item)
        {
            //Rotation axes use the rotation step; everything else uses the position/generic step
            if (ParentOf(item)?.PropertyDescriptor?.Name == "Rotation")
                return NumericStepSettings.RotationStep;
            return NumericStepSettings.PositionStep;
        }
        #endregion

        #region Context menu
        /* The top-level parameter row for the current grid selection (child rows resolve to their parent parameter) */
        private ParameterGridDescriptor GetSelectedParameterDescriptor()
        {
            GridItem item = _grid.SelectedGridItem;
            while (item != null && !(item.PropertyDescriptor is ParameterGridDescriptor))
                item = ParentOf(item);
            ParameterGridDescriptor direct = item?.PropertyDescriptor as ParameterGridDescriptor;
            if (direct != null)
                return direct;

            //Merged multi-selection rows wrap our descriptors - resolve by name through the first proxy
            item = _grid.SelectedGridItem;
            while (item != null && item.GridItemType != GridItemType.Property)
                item = ParentOf(item);
            while (ParentOf(item) != null && ParentOf(item).GridItemType == GridItemType.Property)
                item = ParentOf(item);
            string name = item?.PropertyDescriptor?.Name;
            if (name == null)
                return null;
            return ActiveGroup?.Proxies.FirstOrDefault()?.GetParameterDescriptor(name);
        }

        private void Menu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ParameterGridDescriptor descriptor = GetSelectedParameterDescriptor();
            if (descriptor == null)
            {
                e.Cancel = true;
                return;
            }

            //On an alias, overrides reset by removal so the base value applies; virtual rows have nothing to reset
            bool isAlias = descriptor.Proxy?.Entity?.variant == EntityVariant.ALIAS;
            _resetParam.Text = isAlias
                ? "Reset '" + descriptor.Name + "' (remove override)"
                : "Reset '" + descriptor.Name + "' to default";
            _resetParam.Enabled = !isAlias || descriptor.Proxy.Entity.GetParameter(descriptor.Parameter.name) != null;

            //Jump to the aliases overriding this parameter
            _showAliases.Visible = !isAlias && descriptor.Status == ParameterStatus.AliasOverride;

            bool copyable = descriptor.Parameter.content is cTransform || descriptor.Parameter.content is cVector3;
            _copyValue.Visible = copyable;
            _pasteValue.Visible = copyable;
            _valueSeparator.Visible = copyable; //don't leave a divider dangling at the end of the menu
        }

        ShowCrossRefs _crossRefsDialog = null;
        private void ShowAliases_Click(object sender, EventArgs e)
        {
            ParameterGridDescriptor descriptor = GetSelectedParameterDescriptor();
            if (descriptor == null || Inspector?.CompositeDisplay == null)
                return;

            if (_crossRefsDialog != null)
                _crossRefsDialog.Close();

            _crossRefsDialog = new ShowCrossRefs(descriptor.Proxy.Entity, openOnAliases: true);
            _crossRefsDialog.Show();
            _crossRefsDialog.OnEntitySelected += Inspector.CompositeDisplay.CompositeBrowser.LoadCompositeAndEntity;
            _crossRefsDialog.OnFlowgraphSelected += Inspector.CompositeDisplay.SelectEntityOnFlowgraph;
        }

        private void ResetParam_Click(object sender, EventArgs e)
        {
            ParameterGridDescriptor descriptor = GetSelectedParameterDescriptor();
            TypeGroup group = ActiveGroup;
            if (descriptor == null || group == null)
                return;

            bool structuralChange = false;
            foreach (EntityParameterProxy proxy in group.Proxies)
            {
                ParameterGridDescriptor target = proxy.GetParameterDescriptor(descriptor.Name);
                if (target == null)
                    continue;

                Parameter param = target.Parameter;
                if (proxy.Entity.variant == EntityVariant.ALIAS)
                {
                    //Alias parameters are overrides: reset = remove so the pointed-to entity's value applies
                    Singleton.OnEntityParameterModified?.Invoke(proxy.Entity, param, true);
                    if (param?.content != null && param.name == ShortGuidUtils.Generate("position") && param.content.dataType == DataType.TRANSFORM)
                        Singleton.OnEntityMoved?.Invoke(null, proxy.Entity);
                    proxy.Entity.parameters.Remove(param);
                    structuralChange = true;
                }
                else
                {
                    ParameterData defaultData = Content?.Level?.Commands?.Utils?.CreateDefaultParameterData(proxy.Entity, proxy.Composite, param.name);
                    if (defaultData == null)
                        continue;

                    param.content = defaultData;
                    ParameterModificationTracker.ClearParameterModified(proxy.Composite.shortGUID, proxy.Entity.shortGUID, param.name);
                    Singleton.OnEntityParameterModified?.Invoke(proxy.Entity, param, false);
                    if (defaultData is cTransform defaultTransform)
                        Singleton.OnEntityMoved?.Invoke(defaultTransform, proxy.Entity);
                    if (defaultData is cResource)
                        Singleton.OnResourceModified?.Invoke();
                }
            }
            Singleton.OnParameterModified?.Invoke();

            //Content instances were replaced (or rows removed), so rebuild - and let the inspector
            //refresh links etc for single selections
            if (!IsMultiEditing && Inspector != null && structuralChange)
                Inspector.Reload();
            else
                RebuildProperties();
        }

        private void CopyValue_Click(object sender, EventArgs e)
        {
            ParameterGridDescriptor descriptor = GetSelectedParameterDescriptor();
            if (descriptor == null)
                return;
            Clipboard.SetText(JsonConvert.SerializeObject(descriptor.Parameter.content));
        }

        private void PasteValue_Click(object sender, EventArgs e)
        {
            ParameterGridDescriptor descriptor = GetSelectedParameterDescriptor();
            TypeGroup group = ActiveGroup;
            if (descriptor == null || group == null)
                return;

            string clipboard = Clipboard.GetText();
            cTransform transform = null;
            cVector3 vector = null;
            try
            {
                if (descriptor.Parameter.content is cTransform)
                    transform = JsonConvert.DeserializeObject<cTransform>(clipboard);
                else if (descriptor.Parameter.content is cVector3)
                    vector = JsonConvert.DeserializeObject<cVector3>(clipboard);
            }
            catch { }
            if (transform == null && vector == null)
            {
                MessageBox.Show("Failed to paste value.", "Invalid clipboard", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            foreach (EntityParameterProxy proxy in group.Proxies)
            {
                ParameterGridDescriptor target = proxy.GetParameterDescriptor(descriptor.Name);
                if (target == null)
                    continue;

                if (transform != null && target.Parameter.content is cTransform targetTransform)
                {
                    targetTransform.position = transform.position;
                    targetTransform.rotation = transform.rotation;
                    target.NotifyEdited();
                    Singleton.OnEntityMoved?.Invoke(targetTransform, proxy.Entity);
                }
                else if (vector != null && target.Parameter.content is cVector3 targetVector)
                {
                    targetVector.value = vector.value;
                    target.NotifyEdited();
                }
            }
            RefreshValues();
        }
        #endregion
    }
}
