using CathodeLib;
using CATHODE.Scripting;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
//Not the whole namespace: its Send and Packet (the game's connection) would clash with the viewport's
using LiveLinkCameraSync = OpenCAGE.RuntimeUtilsConnection.LiveLinkCameraSync;

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
        private ToolStripButton _showZonesButton;
        private ToolStripButton _measureButton;
        private ToolStripSeparator _liveLinkCameraSeparator;
        private ToolStripDropDownButton _liveLinkCameraButton;
        private ToolStripMenuItem _liveLinkCameraDisabledItem;
        private ToolStripMenuItem _liveLinkCameraViewportToGameItem;
        private ToolStripMenuItem _liveLinkCameraGameToViewportItem;
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
        private ToolStripMenuItem _createModeNoneItem;

        public ToolStripDropDownButton PanelTransformGridSnapMenu => _transformGridSnapButton;
        public ToolStripDropDownButton PanelRotationSnapMenu => _rotationSnapButton;

        public event EventHandler<LevelViewerDeepSelectMode> SelectionModeChanged;
        public event EventHandler<LevelViewerGizmoMode> GizmoModeChanged;
        /// <summary>FunctionType (uint) selected for entity creation mode; 0 = mode off.</summary>
        public event EventHandler<uint> CreateModeChanged;
        public event EventHandler StateInfoChanged;
        /// <summary>Tint the level's geometry by zone. The argument is the new state.</summary>
        public event EventHandler<bool> ShowZonesChanged;
        /// <summary>Measure was switched on or off from the toolbar. The argument is the new state.</summary>
        public event EventHandler<bool> MeasureModeChanged;
        /// <summary>The Live Link Camera menu's choice: the argument is the new mode, a LiveLinkCameraSync.CameraMode as a number.</summary>
        public event EventHandler<int> LiveLinkCameraModeChanged;

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

            //Leaving creation mode is a choice in the same list, kept away from the types it cancels
            _createModeNoneItem = new ToolStripMenuItem("None")
            {
                CheckOnClick = false,
                Tag = (uint)0,
            };
            _createModeNoneItem.Click += OnCreateModeMenuItemClick;
            _createModeButton.DropDownItems.Add(_createModeNoneItem);
            _createModeButton.DropDownItems.Add(new ToolStripSeparator());

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

            //Like creation mode it takes the viewport's clicks, so the two are never on at once (CommandsEditor)
            _measureButton = new ToolStripButton("Measure")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                CheckOnClick = true,
                Checked = ViewerMeasureMode.Active,
                ToolTipText = "Measure distances in the viewport: click a point on the level, then a second, for the distance "
                    + "between them and how much of it is vertical and horizontal. A third click starts a new measurement.\n"
                    + "Hold Shift to measure from the selected entity's origin (the measurement follows the entity if you then "
                    + "move it), or V to snap to the nearest vertex. Clicking an entity's icon measures from its origin.\n"
                    + "Escape, or this button again, stops measuring.",
            };
            _measureButton.CheckedChanged += OnMeasureCheckedChanged;

            _stateInfoButton = CreateToolbarDropdown("Show State Info");
            _stateInfoNoneItem = new ToolStripMenuItem("None") { CheckOnClick = false };
            _stateInfoNoneItem.Click += OnStateInfoNoneClick;
            _stateInfoButton.DropDownItems.Add(_stateInfoNoneItem);

            //A plain on/off, so a button that stays pressed rather than a one-entry menu
            _showZonesButton = new ToolStripButton("Highlight Zones")
            {
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                CheckOnClick = true,
                Checked = SettingsManager.GetBool(Settings.ShowZones),
            };
            _showZonesButton.CheckedChanged += OnShowZonesCheckedChanged;

            //Only there while the live link to the game is connected (SetLiveLinkConnected): without a game it does nothing
            _liveLinkCameraSeparator = new ToolStripSeparator
            {
                Visible = false,
            };
            _liveLinkCameraButton = CreateToolbarDropdown("Live Link Camera");
            _liveLinkCameraButton.Visible = false;
            _liveLinkCameraButton.ToolTipText = "Link this viewport's camera and the running game's camera, over Live Link.";
            _liveLinkCameraDisabledItem = CreateLiveLinkCameraItem("Disabled", LiveLinkCameraSync.CameraMode.Disabled,
                "No Live Link camera control: the game's camera and this viewport's camera each move on their own.");
            _liveLinkCameraViewportToGameItem = CreateLiveLinkCameraItem("Sync viewport camera to game", LiveLinkCameraSync.CameraMode.ViewportToGame,
                "The running game's camera follows this viewport's camera: the game draws the level from where the viewport "
                    + "looks, streaming in the zones it needs.\n"
                    + "It follows while the viewport shows the level's root composite, and the game is running this level.\n"
                    + "Scripts and AI that check what the player's camera sees (camera viewcone triggers, the alien) react to "
                    + "this viewport's camera while it follows.");
            _liveLinkCameraGameToViewportItem = CreateLiveLinkCameraItem("Sync game camera to viewport", LiveLinkCameraSync.CameraMode.GameToViewport,
                "This viewport's camera follows the running game's camera - where it is, where it looks and its field of view - "
                    + "so the viewport shows what the player sees.\n"
                    + "It follows while the viewport shows the level's root composite, and the game is running this level. "
                    + "The viewport's camera cannot be moved by hand meanwhile.");
            _liveLinkCameraButton.DropDownItems.AddRange(new ToolStripItem[]
            {
                _liveLinkCameraDisabledItem,
                _liveLinkCameraViewportToGameItem,
                _liveLinkCameraGameToViewportItem,
            });
            ApplyLiveLinkCameraMode(LiveLinkCameraSync.WantedMode);

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
                _measureButton,
                new ToolStripSeparator(),
                _stateInfoButton,
                new ToolStripSeparator(),
                _showZonesButton,
                _liveLinkCameraSeparator,
                _liveLinkCameraButton,
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

        private ToolStripMenuItem CreateLiveLinkCameraItem(string text, LiveLinkCameraSync.CameraMode mode, string toolTip)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text)
            {
                CheckOnClick = false,
                Tag = mode,
                ToolTipText = toolTip,
            };
            item.Click += OnLiveLinkCameraMenuItemClick;
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

            if (mode == LevelViewerDeepSelectMode.AdvancedDeep)
                _selectionModeButton.ForeColor = Color.Red;
            else if (mode == LevelViewerDeepSelectMode.Deep)
                _selectionModeButton.ForeColor = Color.Orange;
            else
                _selectionModeButton.ForeColor = SystemColors.ControlText;
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

                bool isActive = (uint)item.Tag == functionType;
                item.Checked = isActive;
                if (isActive && functionType != 0)
                    label = item.Text;
            }

            _createModeButton.Text = label != null ? "Create: " + label : "Create";
        }
        private bool _isRootComposite = true;
        public void SetIsRootComposite(bool isRoot)
        {
            _isRootComposite = isRoot;
            if (_stateInfoButton != null)
            {
                _stateInfoButton.Enabled = isRoot && _stateInfoButton.DropDownItems.Count > 1;
                if (!isRoot)
                {
                    ViewerStateInfoMode.Clear();
                    ApplyStateInfo();
                    StateInfoChanged?.Invoke(this, EventArgs.Empty);
                }
            }
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

            _stateInfoButton.Enabled = _isRootComposite;
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

        internal static string DescribeState(LevelContent content, CathodeLib.Level.State state, int index)
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

        /// <summary>Put the button in a given state without raising ShowZonesChanged for it.</summary>
        public void ApplyShowZones(bool enabled)
        {
            if (_showZonesButton == null || _showZonesButton.Checked == enabled)
                return;

            _showZonesButton.CheckedChanged -= OnShowZonesCheckedChanged;
            _showZonesButton.Checked = enabled;
            _showZonesButton.CheckedChanged += OnShowZonesCheckedChanged;
        }

        private void OnShowZonesCheckedChanged(object sender, EventArgs e)
        {
            ShowZonesChanged?.Invoke(this, _showZonesButton.Checked);
        }

        /// <summary>Put Measure in a given state without raising MeasureModeChanged for it.</summary>
        public void ApplyMeasureMode(bool enabled)
        {
            if (_measureButton == null || _measureButton.Checked == enabled)
                return;

            _measureButton.CheckedChanged -= OnMeasureCheckedChanged;
            _measureButton.Checked = enabled;
            _measureButton.CheckedChanged += OnMeasureCheckedChanged;
        }

        private void OnMeasureCheckedChanged(object sender, EventArgs e)
        {
            MeasureModeChanged?.Invoke(this, _measureButton.Checked);
        }

        /// <summary>Tick one Live Link Camera choice (and only that one) without raising LiveLinkCameraModeChanged for it.</summary>
        public void ApplyLiveLinkCameraMode(LiveLinkCameraSync.CameraMode mode)
        {
            if (_liveLinkCameraButton == null)
                return;

            _liveLinkCameraDisabledItem.Checked = mode == LiveLinkCameraSync.CameraMode.Disabled;
            _liveLinkCameraViewportToGameItem.Checked = mode == LiveLinkCameraSync.CameraMode.ViewportToGame;
            _liveLinkCameraGameToViewportItem.Checked = mode == LiveLinkCameraSync.CameraMode.GameToViewport;
        }

        /// <summary>Show the Live Link Camera menu (and its separator) only while Live Link is connected to the game.</summary>
        public void SetLiveLinkConnected(bool connected)
        {
            if (_liveLinkCameraButton == null)
                return;

            _liveLinkCameraSeparator.Visible = connected;
            _liveLinkCameraButton.Visible = connected;
        }

        private void OnLiveLinkCameraMenuItemClick(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item == null || !(item.Tag is LiveLinkCameraSync.CameraMode))
                return;

            LiveLinkCameraSync.CameraMode mode = (LiveLinkCameraSync.CameraMode)item.Tag;
            ApplyLiveLinkCameraMode(mode);
            LiveLinkCameraModeChanged?.Invoke(this, (int)mode);
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
