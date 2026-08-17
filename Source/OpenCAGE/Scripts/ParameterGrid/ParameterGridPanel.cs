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

        private readonly Panel _banner;
        private readonly Label _bannerLabel;
        private readonly TabControl _tabs;
        private readonly PropertyGrid _grid;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _removeParam;
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

            _banner = new Panel()
            {
                Dock = DockStyle.Top,
                Height = 22,
                BackColor = Color.FromArgb(255, 249, 196),
                Visible = false
            };
            _bannerLabel = new Label()
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Bold),
                ForeColor = Color.FromArgb(102, 77, 3)
            };
            _banner.Controls.Add(_bannerLabel);

            //Dock order: fill control first so the top-docked controls stack above it
            Controls.Add(_grid);
            Controls.Add(_tabs);
            Controls.Add(_banner);

            HookNumericScrolling();

            _removeParam = new ToolStripMenuItem("Remove Parameter");
            _removeParam.Click += RemoveParam_Click;
            _copyValue = new ToolStripMenuItem("Copy Value");
            _copyValue.Click += CopyValue_Click;
            _pasteValue = new ToolStripMenuItem("Paste Value");
            _pasteValue.Click += PasteValue_Click;
            _menu = new ContextMenuStrip();
            _menu.Items.AddRange(new ToolStripItem[] { _removeParam, new ToolStripSeparator(), _copyValue, _pasteValue });
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

            _banner.Visible = multi;
            if (multi)
                _bannerLabel.Text = "Editing " + entityCount + " entities - changes apply to all entities in the active tab";

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
            _banner.Visible = false;
            _grid.SelectedObjects = new object[0];
        }

        /* Re-read values from the entity data (e.g. after a viewer gizmo move or popup edit) */
        public void RefreshValues()
        {
            _grid.Refresh();
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

        /* Mark a parameter as modified and raise the editor-wide modification events */
        public void NotifyParameterEdited(EntityParameterProxy proxy, Parameter parameter)
        {
            ParameterModificationTracker.SetParameterModified(proxy.Composite.shortGUID, proxy.Entity.shortGUID, parameter.name);
            Singleton.OnEntityParameterModified?.Invoke(proxy.Entity, parameter, false);
            Singleton.OnParameterModified?.Invoke();

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

                    ParameterModificationTracker.SetParameterModified(proxy.Composite.shortGUID, proxy.Entity.shortGUID, descriptor.Parameter.name);
                    Singleton.OnEntityParameterModified?.Invoke(proxy.Entity, descriptor.Parameter, false);
                    if (descriptor.Parameter.content is cTransform movedTransform)
                        Singleton.OnEntityMoved?.Invoke(movedTransform, proxy.Entity);
                    propagatedAnything = true;
                }
            }

            if (propagatedAnything)
            {
                Singleton.OnParameterModified?.Invoke();
                RefreshValues();
            }
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
        }

        private static float GetStepForItem(GridItem item)
        {
            //Rotation axes use the rotation step; everything else uses the position/generic step
            if (item?.Parent?.PropertyDescriptor?.Name == "Rotation")
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
                item = item.Parent;
            ParameterGridDescriptor direct = item?.PropertyDescriptor as ParameterGridDescriptor;
            if (direct != null)
                return direct;

            //Merged multi-selection rows wrap our descriptors - resolve by name through the first proxy
            item = _grid.SelectedGridItem;
            while (item != null && item.GridItemType != GridItemType.Property)
                item = item.Parent;
            while (item?.Parent != null && item.Parent.GridItemType == GridItemType.Property)
                item = item.Parent;
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

            _removeParam.Text = "Remove '" + descriptor.Name + "'";
            bool copyable = descriptor.Parameter.content is cTransform || descriptor.Parameter.content is cVector3;
            _copyValue.Visible = copyable;
            _pasteValue.Visible = copyable;
        }

        private void RemoveParam_Click(object sender, EventArgs e)
        {
            ParameterGridDescriptor descriptor = GetSelectedParameterDescriptor();
            TypeGroup group = ActiveGroup;
            if (descriptor == null || group == null)
                return;

            foreach (EntityParameterProxy proxy in group.Proxies)
            {
                ParameterGridDescriptor target = proxy.GetParameterDescriptor(descriptor.Name);
                if (target == null)
                    continue;

                Parameter param = target.Parameter;
                Singleton.OnEntityParameterModified?.Invoke(proxy.Entity, param, true);
                if (param?.content != null && param.name == ShortGuidUtils.Generate("position") && param.content.dataType == DataType.TRANSFORM)
                    Singleton.OnEntityMoved?.Invoke(null, proxy.Entity);
                proxy.Entity.parameters.Remove(param);
            }
            Singleton.OnParameterModified?.Invoke();

            //Single selection: run the inspector's full reload (links etc). Multi: just rebuild rows.
            if (!IsMultiEditing && Inspector != null)
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
