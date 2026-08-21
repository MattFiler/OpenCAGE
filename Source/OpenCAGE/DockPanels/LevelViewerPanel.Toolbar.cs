using CathodeLib;
using CATHODE.Scripting;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.DockPanels
{
    public partial class LevelViewerPanel
    {
        private ToolStrip _viewerToolStrip;
        private ToolStripDropDownButton _selectionModeButton;
        private ToolStripDropDownButton _controlModeButton;
        private ToolStripDropDownButton _createModeButton;
        private ToolStripDropDownButton _stateInfoButton;
        private ToolStripMenuItem _stateInfoNoneItem;
        private ToolStripDropDownButton _transformGridSnapButton;
        private ToolStripDropDownButton _rotationSnapButton;
        private ToolStripMenuItem _selectionModeRegularItem;
        private ToolStripMenuItem _selectionModeDeepItem;
        private ToolStripMenuItem _selectionModeAdvancedDeepItem;
        private ToolStripMenuItem _controlModeNoneItem;
        private ToolStripMenuItem _controlModeTranslateLocalItem;
        private ToolStripMenuItem _controlModeTranslateWorldItem;
        private ToolStripMenuItem _controlModeRotateLocalItem;
        private ToolStripMenuItem _controlModeRotateWorldItem;

        public ToolStripDropDownButton PanelTransformGridSnapMenu => _transformGridSnapButton;
        public ToolStripDropDownButton PanelRotationSnapMenu => _rotationSnapButton;

        public event EventHandler<LevelViewerDeepSelectMode> SelectionModeChanged;
        public event EventHandler<LevelViewerGizmoMode> GizmoModeChanged;
        /// <summary>FunctionType (uint) selected for entity creation mode; 0 = mode off.</summary>
        public event EventHandler<uint> CreateModeChanged;
        public event EventHandler StateInfoChanged;

        private void InitializeViewerToolbar()
        {
            _viewerToolStrip = new ToolStrip
            {
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                Name = "viewerToolStrip",
            };

            _selectionModeButton = CreateToolbarDropdown("Selection Mode");
            _selectionModeRegularItem = CreateModeMenuItem(
                LevelViewerViewportDefinitions.FormatSelectionModeLabel(LevelViewerDeepSelectMode.Regular),
                LevelViewerViewportDefinitions.GetSelectionModeShortcut(LevelViewerDeepSelectMode.Regular),
                LevelViewerDeepSelectMode.Regular,
                OnSelectionModeMenuItemClick);
            _selectionModeDeepItem = CreateModeMenuItem(
                LevelViewerViewportDefinitions.FormatSelectionModeLabel(LevelViewerDeepSelectMode.Deep),
                LevelViewerViewportDefinitions.GetSelectionModeShortcut(LevelViewerDeepSelectMode.Deep),
                LevelViewerDeepSelectMode.Deep,
                OnSelectionModeMenuItemClick);
            _selectionModeAdvancedDeepItem = CreateModeMenuItem(
                LevelViewerViewportDefinitions.FormatSelectionModeLabel(LevelViewerDeepSelectMode.AdvancedDeep),
                LevelViewerViewportDefinitions.GetSelectionModeShortcut(LevelViewerDeepSelectMode.AdvancedDeep),
                LevelViewerDeepSelectMode.AdvancedDeep,
                OnSelectionModeMenuItemClick);
            _selectionModeButton.DropDownItems.AddRange(new ToolStripItem[]
            {
                _selectionModeRegularItem,
                _selectionModeDeepItem,
                _selectionModeAdvancedDeepItem,
            });

            _controlModeButton = CreateToolbarDropdown("Control");
            _controlModeNoneItem = CreateModeMenuItem(
                LevelViewerViewportDefinitions.FormatTransformModeLabel(LevelViewerGizmoMode.None),
                LevelViewerViewportDefinitions.GetGizmoModeShortcut(LevelViewerGizmoMode.None),
                LevelViewerGizmoMode.None,
                OnControlModeMenuItemClick);
            _controlModeTranslateLocalItem = CreateModeMenuItem(
                LevelViewerViewportDefinitions.FormatTransformModeLabel(LevelViewerGizmoMode.TranslateLocal),
                LevelViewerViewportDefinitions.GetGizmoModeShortcut(LevelViewerGizmoMode.TranslateLocal),
                LevelViewerGizmoMode.TranslateLocal,
                OnControlModeMenuItemClick);
            _controlModeTranslateWorldItem = CreateModeMenuItem(
                LevelViewerViewportDefinitions.FormatTransformModeLabel(LevelViewerGizmoMode.TranslateWorld),
                LevelViewerViewportDefinitions.GetGizmoModeShortcut(LevelViewerGizmoMode.TranslateWorld),
                LevelViewerGizmoMode.TranslateWorld,
                OnControlModeMenuItemClick);
            _controlModeRotateLocalItem = CreateModeMenuItem(
                LevelViewerViewportDefinitions.FormatTransformModeLabel(LevelViewerGizmoMode.RotateLocal),
                LevelViewerViewportDefinitions.GetGizmoModeShortcut(LevelViewerGizmoMode.RotateLocal),
                LevelViewerGizmoMode.RotateLocal,
                OnControlModeMenuItemClick);
            _controlModeRotateWorldItem = CreateModeMenuItem(
                LevelViewerViewportDefinitions.FormatTransformModeLabel(LevelViewerGizmoMode.RotateWorld),
                LevelViewerViewportDefinitions.GetGizmoModeShortcut(LevelViewerGizmoMode.RotateWorld),
                LevelViewerGizmoMode.RotateWorld,
                OnControlModeMenuItemClick);
            _controlModeButton.DropDownItems.AddRange(new ToolStripItem[]
            {
                _controlModeNoneItem,
                _controlModeTranslateLocalItem,
                _controlModeTranslateWorldItem,
                _controlModeRotateLocalItem,
                _controlModeRotateWorldItem,
            });

            _createModeButton = CreateToolbarDropdown("Create");
            foreach (RenderFilterDefinitions.Definition definition in RenderFilterDefinitions.All
                .OrderBy(definition => definition.FunctionType.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                ToolStripMenuItem item = new ToolStripMenuItem(definition.FunctionType.ToString())
                {
                    CheckOnClick = false,
                    Tag = definition.FunctionTypeUInt,
                    Image = RenderFilters.CreateFilterListIcon(definition),
                };
                item.Click += OnCreateModeMenuItemClick;
                _createModeButton.DropDownItems.Add(item);
            }

            _stateInfoButton = CreateToolbarDropdown("Show State Info");
            _stateInfoNoneItem = new ToolStripMenuItem("None") { CheckOnClick = false };
            _stateInfoNoneItem.Click += OnStateInfoNoneClick;
            _stateInfoButton.DropDownItems.Add(_stateInfoNoneItem);

            _transformGridSnapButton = CreateToolbarDropdown("Transform Snap");
            _transformGridSnapButton.Alignment = ToolStripItemAlignment.Right;
            _rotationSnapButton = CreateToolbarDropdown("Rotation Snap");
            _rotationSnapButton.Alignment = ToolStripItemAlignment.Right;

            ToolStripSeparator rightSeparator = new ToolStripSeparator
            {
                Alignment = ToolStripItemAlignment.Right,
            };

            _viewerToolStrip.Items.AddRange(new ToolStripItem[]
            {
                _selectionModeButton,
                new ToolStripSeparator(),
                _controlModeButton,
                new ToolStripSeparator(),
                _createModeButton,
                new ToolStripSeparator(),
                _stateInfoButton,
                rightSeparator,
                _transformGridSnapButton,
                _rotationSnapButton,
            });

            Controls.Add(_viewerToolStrip);
            Controls.SetChildIndex(_viewerToolStrip, 0);
        }

        private static ToolStripDropDownButton CreateToolbarDropdown(string text)
        {
            return new ToolStripDropDownButton(text)
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                ShowDropDownArrow = true,
            };
        }

        private static ToolStripMenuItem CreateModeMenuItem(
            string text,
            string shortcutDisplay,
            object tag,
            EventHandler onClick)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text)
            {
                CheckOnClick = false,
                Tag = tag,
                ShortcutKeyDisplayString = shortcutDisplay,
            };
            item.Click += onClick;
            return item;
        }

        public void ApplySelectionMode(LevelViewerDeepSelectMode mode)
        {
            mode = LevelViewerViewportDefinitions.NormalizeDeepSelectMode((int)mode);
            _selectionModeRegularItem.Checked = mode == LevelViewerDeepSelectMode.Regular;
            _selectionModeDeepItem.Checked = mode == LevelViewerDeepSelectMode.Deep;
            _selectionModeAdvancedDeepItem.Checked = mode == LevelViewerDeepSelectMode.AdvancedDeep;
            _selectionModeButton.Text = "Selection: "
                + LevelViewerViewportDefinitions.FormatSelectionModeLabel(mode);
        }

        public void ApplyGizmoMode(LevelViewerGizmoMode mode)
        {
            mode = LevelViewerViewportDefinitions.NormalizeGizmoMode((int)mode);
            _controlModeNoneItem.Checked = mode == LevelViewerGizmoMode.None;
            _controlModeTranslateLocalItem.Checked = mode == LevelViewerGizmoMode.TranslateLocal;
            _controlModeTranslateWorldItem.Checked = mode == LevelViewerGizmoMode.TranslateWorld;
            _controlModeRotateLocalItem.Checked = mode == LevelViewerGizmoMode.RotateLocal;
            _controlModeRotateWorldItem.Checked = mode == LevelViewerGizmoMode.RotateWorld;
            _controlModeButton.Text = "Control: "
                + LevelViewerViewportDefinitions.FormatTransformModeLabel(mode);
        }

        private void OnSelectionModeMenuItemClick(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item == null || !(item.Tag is LevelViewerDeepSelectMode))
                return;

            LevelViewerDeepSelectMode mode = (LevelViewerDeepSelectMode)item.Tag;
            ApplySelectionMode(mode);
            SelectionModeChanged?.Invoke(this, mode);
        }

        private void OnControlModeMenuItemClick(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item == null || !(item.Tag is LevelViewerGizmoMode))
                return;

            LevelViewerGizmoMode mode = (LevelViewerGizmoMode)item.Tag;
            ApplyGizmoMode(mode);
            GizmoModeChanged?.Invoke(this, mode);
        }

        public void ApplyCreateMode(uint functionType)
        {
            if (_createModeButton == null)
                return;

            string label = null;
            foreach (ToolStripItem toolStripItem in _createModeButton.DropDownItems)
            {
                ToolStripMenuItem item = toolStripItem as ToolStripMenuItem;
                if (item == null || !(item.Tag is uint))
                    continue;

                bool isActive = (uint)item.Tag == functionType && functionType != 0;
                item.Checked = isActive;
                if (isActive)
                    label = item.Text;
            }

            _createModeButton.Text = label != null ? "Create: " + label : "Create";
        }

        /* Rebuild the state list for the loaded level. A level always has state 0 (the default set),
           plus one per ExclusiveMaster resource; each has its own generated navmesh and cover. */
        public void RefreshStateInfoMenu(LevelContent content)
        {
            if (_stateInfoButton == null)
                return;

            for (int i = _stateInfoButton.DropDownItems.Count - 1; i >= 0; i--)
            {
                if (_stateInfoButton.DropDownItems[i] != _stateInfoNoneItem)
                    _stateInfoButton.DropDownItems.RemoveAt(i);
            }

            List<CathodeLib.Level.State> states = content?.Level?.StateResources;
            if (states == null || states.Count == 0)
            {
                _stateInfoButton.Enabled = false;
                ApplyStateInfo();
                return;
            }

            _stateInfoButton.Enabled = true;
            for (int i = 0; i < states.Count; i++)
            {
                ToolStripMenuItem stateItem = new ToolStripMenuItem(DescribeState(content, states[i], i));

                ToolStripMenuItem navItem = new ToolStripMenuItem("Navmesh")
                {
                    CheckOnClick = false,
                    Tag = new StateInfoTag(i, true),
                };
                navItem.Click += OnStateInfoItemClick;

                ToolStripMenuItem coverItem = new ToolStripMenuItem("Cover")
                {
                    CheckOnClick = false,
                    Tag = new StateInfoTag(i, false),
                };
                coverItem.Click += OnStateInfoItemClick;

                stateItem.DropDownItems.Add(navItem);
                stateItem.DropDownItems.Add(coverItem);
                _stateInfoButton.DropDownItems.Add(stateItem);
            }

            ApplyStateInfo();
        }

        private static string DescribeState(LevelContent content, CathodeLib.Level.State state, int index)
        {
            if (index == 0)
                return "State 0 (Default)";

            //Entity names live on the entity as a parameter now, so no composite lookup is needed
            string name = null;
            if (state?.ExclusiveMaster != null)
            {
                CATHODE.Scripting.Parameter nameParameter = state.ExclusiveMaster.GetParameter(ShortGuids.name);
                if (nameParameter?.content is CATHODE.Scripting.cString nameString)
                    name = nameString.value;
            }
            return string.IsNullOrEmpty(name) ? "State " + index : "State " + index + " (" + name + ")";
        }

        /// <summary>Which state, and whether this entry is the navmesh (otherwise cover).</summary>
        private class StateInfoTag
        {
            public StateInfoTag(int state, bool isNavMesh)
            {
                State = state;
                IsNavMesh = isNavMesh;
            }

            public int State { get; }
            public bool IsNavMesh { get; }
        }

        /* Reflect the current overlay selection back into the menu ticks and the button label */
        public void ApplyStateInfo()
        {
            if (_stateInfoButton == null)
                return;

            List<string> active = new List<string>();
            foreach (ToolStripItem toolStripItem in _stateInfoButton.DropDownItems)
            {
                ToolStripMenuItem stateItem = toolStripItem as ToolStripMenuItem;
                if (stateItem == null)
                    continue;

                foreach (ToolStripItem childItem in stateItem.DropDownItems)
                {
                    ToolStripMenuItem child = childItem as ToolStripMenuItem;
                    if (child == null || !(child.Tag is StateInfoTag tag))
                        continue;

                    bool isActive = tag.IsNavMesh
                        ? ViewerStateInfoMode.NavMeshState == tag.State
                        : ViewerStateInfoMode.CoverState == tag.State;

                    child.Checked = isActive;
                    if (isActive)
                        active.Add(child.Text + " " + tag.State);
                }
            }

            _stateInfoNoneItem.Checked = active.Count == 0;
            _stateInfoButton.Text = active.Count == 0
                ? "Show State Info"
                : "State Info: " + string.Join(", ", active);
        }

        private void OnStateInfoNoneClick(object sender, EventArgs e)
        {
            ViewerStateInfoMode.Clear();
            ApplyStateInfo();
            StateInfoChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnStateInfoItemClick(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item == null || !(item.Tag is StateInfoTag tag))
                return;

            //Clicking the active entry again turns that overlay off
            if (tag.IsNavMesh)
                ViewerStateInfoMode.NavMeshState = item.Checked ? ViewerStateInfoMode.None : tag.State;
            else
                ViewerStateInfoMode.CoverState = item.Checked ? ViewerStateInfoMode.None : tag.State;

            ApplyStateInfo();
            StateInfoChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnCreateModeMenuItemClick(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item == null || !(item.Tag is uint))
                return;

            uint functionType = (uint)item.Tag;

            //Clicking the active type again exits creation mode
            if (item.Checked)
                functionType = 0;

            ApplyCreateMode(functionType);
            CreateModeChanged?.Invoke(this, functionType);
        }
    }
}
