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
            AddControl(FlowgraphControls, "Move Canvas", "Drag Holding Middle Mouse");
            AddControl(FlowgraphControls, "Move Node", "Drag Holding Left Mouse");
            AddControl(FlowgraphControls, "Create Link", "Drag Left Mouse Between Node Pins");
            AddControl(FlowgraphControls, "Remove Link", "Right Click Link");
            AddControl(FlowgraphControls, "Create Node", "Right Click Canvas");
            AddControl(FlowgraphControls, "Manage Node", "Right Click Node");

            //Model viewer controls
            AddControl(ModelViewerControls, "Zoom In/Out", "Scrollwheel Up/Down");
            AddControl(ModelViewerControls, "Rotate", "Drag Holding Right Mouse");
            AddControl(ModelViewerControls, "Move", "Drag Holding Middle Mouse");
            AddControl(ModelViewerControls, "Recenter", "Home Key");
        }

        private void AddControl(ListView view, string action, string binding)
        {
            ListViewItem item = new ListViewItem(action);
            item.SubItems.Add(binding);
            view.Items.Add(item);
        }
    }
}
