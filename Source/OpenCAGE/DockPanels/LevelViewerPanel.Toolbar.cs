using OpenCAGE.UnityConnection;
using System;
using System.Windows.Forms;

namespace OpenCAGE.DockPanels
{
    public partial class LevelViewerPanel
    {
        private ToolStrip _viewerToolStrip;
        private ToolStripDropDownButton _selectionModeButton;
        private ToolStripDropDownButton _controlModeButton;
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

            _transformGridSnapButton = CreateToolbarDropdown("Transform Snap");
            _rotationSnapButton = CreateToolbarDropdown("Rotation Snap");

            _viewerToolStrip.Items.AddRange(new ToolStripItem[]
            {
                _selectionModeButton,
                new ToolStripSeparator(),
                _controlModeButton,
                new ToolStripSeparator(),
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
    }
}
