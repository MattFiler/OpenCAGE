using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    public partial class ControlsWindow : Form
    {
        public ControlsWindow()
        {
            InitializeComponent();

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
            AddControl(LevelViewerControls, "Translate", "Num1 (with object selected)");
            AddControl(LevelViewerControls, "Rotate (local)", "Num2 (with object selected)");
            AddControl(LevelViewerControls, "Rotate (world)", "Num3 (with object selected)");
            AddControl(LevelViewerControls, "Stop Translate/Rotate", "Num4");
            AddControl(LevelViewerControls, "Deep/Advanced Select", "Num0");
            AddControl(LevelViewerControls, "Hide", "H");
            AddControl(LevelViewerControls, "Un-hide", "Shift + H");
            AddControl(LevelViewerControls, "De-select", "Escape");
        }

        private void AddControl(ListView view, string action, string binding)
        {
            ListViewItem item = new ListViewItem(action);
            item.SubItems.Add(binding);
            view.Items.Add(item);
        }
    }
}
