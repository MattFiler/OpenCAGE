using System;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    public partial class ControlsWindow : Form
    {
        public ControlsWindow()
        {
            InitializeComponent();

            remainOnTop.Checked = SettingsManager.GetBool(Settings.ControlsWindowRemainOnTop);
            TopMost = remainOnTop.Checked;
            remainOnTop.CheckedChanged += RemainOnTop_CheckedChanged;

            //Flowgraph controls
            AddControl(FlowgraphControls, "Zoom In/Out", "Scrollwheel Up/Down");
            AddControl(FlowgraphControls, "Move Canvas", "Drag While Holding Middle Mouse");
            AddControl(FlowgraphControls, "Move Node", "Drag While Holding Left Mouse");
            AddControl(FlowgraphControls, "Create Link", "Drag Left Mouse Between Node Pins");
            AddControl(FlowgraphControls, "Remove Link", "Right Click Link");
            AddControl(FlowgraphControls, "Create Node", "Right Click Canvas");
            AddControl(FlowgraphControls, "Manage Node", "Right Click Node");
            AddControl(FlowgraphControls, "Add All Pins", "F4");
            AddControl(FlowgraphControls, "Remove Unused Pins", "F5");
            AddControl(FlowgraphControls, "Manage Pins", "F6");
            AddControl(FlowgraphControls, "Step Inside", "Ctrl + Middle Mouse (on node)");

            //Entity list controls
            AddControl(EntityListControls, "Step Inside", "Ctrl + Middle Mouse (composite instance)");

            //Model viewer controls
            AddControl(ModelViewerControls, "Zoom In/Out", "Scrollwheel Up/Down");
            AddControl(ModelViewerControls, "Rotate", "Drag While Holding Right Mouse");
            AddControl(ModelViewerControls, "Move", "Drag While Holding Middle Mouse");
            AddControl(ModelViewerControls, "Recenter", "Home");

            //Level viewer controls
            AddControl(LevelViewerControls, "Look", "Drag While Holding Right Mouse");
            AddControl(LevelViewerControls, "Move", "W/A/S/D");
            AddControl(LevelViewerControls, "Speed", "Scrollwheel Up/Down");
            AddControl(LevelViewerControls, "Recenter", "Z");
            AddControl(LevelViewerControls, "Select", "Left Click");
            AddControl(LevelViewerControls, "Step Inside", "Ctrl + Middle Mouse");
            AddControl(LevelViewerControls, "Step Back", "Minus");
            AddControl(LevelViewerControls, "Translate (world)", "Num1 (with object selected)");
            AddControl(LevelViewerControls, "Translate (local)", "Num2 (with object selected)");
            AddControl(LevelViewerControls, "Rotate (world)", "Num3 (with object selected)");
            AddControl(LevelViewerControls, "Rotate (local)", "Num4 (with object selected)");
            AddControl(LevelViewerControls, "Stop Translate/Rotate", "Num5");
            AddControl(LevelViewerControls, "Regular Select", "Num0");
            AddControl(LevelViewerControls, "Deep Select", "Num8");
            AddControl(LevelViewerControls, "Advanced Deep Select", "Num9");
            AddControl(LevelViewerControls, "Hide", "H");
            AddControl(LevelViewerControls, "Un-hide", "Shift + H");
            AddControl(LevelViewerControls, "De-select", "Escape");

            //Behaviour tree editor controls (Brainiac Designer)
            AddControl(BehaviourTreeControls, "Zoom In/Out", "Scrollwheel Up/Down");
            AddControl(BehaviourTreeControls, "Pan Graph", "Drag While Holding Left Mouse");
            AddControl(BehaviourTreeControls, "Select Node", "Left Click");
            AddControl(BehaviourTreeControls, "Open Referenced Behavior", "Double Click Node");
            AddControl(BehaviourTreeControls, "Add Node From Palette", "Drag From Node List Onto Graph");
            AddControl(BehaviourTreeControls, "Move Node", "Right Drag Node Onto New Parent");
            AddControl(BehaviourTreeControls, "Duplicate Node", "Ctrl + Left Drag Node Onto New Parent");
            AddControl(BehaviourTreeControls, "Include Children When Moving/Duplicating", "Hold Shift While Dragging");
            AddControl(BehaviourTreeControls, "Copy Node", "Ctrl + C");
            AddControl(BehaviourTreeControls, "Copy Branch", "Ctrl + Shift + C");
            AddControl(BehaviourTreeControls, "Cut Node", "Ctrl + X");
            AddControl(BehaviourTreeControls, "Cut Branch", "Ctrl + Shift + X");
            AddControl(BehaviourTreeControls, "Paste", "Ctrl + V, Then Left Click Target");
            AddControl(BehaviourTreeControls, "Paste Branch", "Ctrl + Shift + V, Then Left Click Target");
            AddControl(BehaviourTreeControls, "Delete Node (Keep Children)", "Delete");
            AddControl(BehaviourTreeControls, "Delete Branch", "Shift + Delete");
            AddControl(BehaviourTreeControls, "Rename Behavior/Folder", "F2 (in file list)");
            AddControl(BehaviourTreeControls, "Delete Behavior/Folder", "Delete (in file list)");

            //CAGEAnimation editor controls
            AddControl(CAGEAnimationControls, "Pan Time + Value", "Drag While Holding Middle Mouse");
            AddControl(CAGEAnimationControls, "Pan Time Only", "Ctrl + Middle Mouse, or Middle Mouse on Timeline");
            AddControl(CAGEAnimationControls, "Zoom Value", "Scrollwheel Over Curve Plot");
            AddControl(CAGEAnimationControls, "Zoom Time", "Scrollwheel Over Timeline / Event Lanes");
            AddControl(CAGEAnimationControls, "Pan Value / Time", "Shift + Scrollwheel (over plot / timeline)");
            AddControl(CAGEAnimationControls, "Reset View", "Z");
            AddControl(CAGEAnimationControls, "Select Keyframe / Event", "Left Click");
            AddControl(CAGEAnimationControls, "Move Keyframe / Event", "Drag While Holding Left Mouse");
            AddControl(CAGEAnimationControls, "Add Keyframe on Curve", "Shift + Left Click (on curve)");
            AddControl(CAGEAnimationControls, "Add Event on Lane", "Shift + Left Click (on event lane)");
            AddControl(CAGEAnimationControls, "Curve / Event Context Menu", "Right Click");
            AddControl(CAGEAnimationControls, "Delete Keyframe", "Right Click Keyframe");
            AddControl(CAGEAnimationControls, "Edit Bezier Tangents", "Drag Handles (with Bezier curves on)");
            AddControl(CAGEAnimationControls, "Set Animation Length", "Drag Green Handle on Timeline");
            AddControl(CAGEAnimationControls, "Resize Event Lanes", "Drag Grip Between Plot and Lanes");
        }

        private void RemainOnTop_CheckedChanged(object sender, EventArgs e)
        {
            TopMost = remainOnTop.Checked;
            SettingsManager.SetBool(Settings.ControlsWindowRemainOnTop, remainOnTop.Checked);
        }

        private void AddControl(ListView view, string action, string binding)
        {
            ListViewItem item = new ListViewItem(action);
            item.SubItems.Add(binding);
            view.Items.Add(item);
        }
    }
}
