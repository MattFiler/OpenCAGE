using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alien_Isolation_Mod_Tools
{
    public partial class CSE_Alpha_EditPin : Form
    {
        private CathodeNodeLink edit_pin = null;
        private CathodeFlowgraph flowgraph = null;
        public CSE_Alpha_EditPin(CathodeNodeLink pin_link, CathodeFlowgraph loaded_flowgraph)
        {
            edit_pin = pin_link;
            flowgraph = loaded_flowgraph;
            InitializeComponent();

            //Populate node dropdowns
            int selected_n_in = -1;
            int selected_n_out = -1;
            int i = 0;
            foreach (CathodeNodeEntity node in flowgraph.nodes)
            {
                string this_node_string = "[" + BitConverter.ToString(node.nodeID) + "] " + NodeDB.GetFriendlyName(node.nodeID);
                pin_in_node.Items.Add(this_node_string);
                pin_out_node.Items.Add(this_node_string);

                if (selected_n_in == -1 && node.nodeID.SequenceEqual(edit_pin.childID)) selected_n_in = i;
                if (selected_n_out == -1 && node.nodeID.SequenceEqual(edit_pin.parentID)) selected_n_out = i;
                i++;
            }
            pin_in_node.SelectedIndex = selected_n_in;
            pin_out_node.SelectedIndex = selected_n_out;

            //Populate parameter dropdowns
            int selected_p_in = -1;
            i = 0;
            foreach (CathodeParameterReference param_ref in flowgraph.nodes[selected_n_in].nodeParameterReferences)
            {
                pin_in_param.Items.Add("[" + BitConverter.ToString(param_ref.paramID) + "] " + NodeDB.GetParameterName(param_ref.paramID));
                if (selected_p_in == -1 && param_ref.paramID.SequenceEqual(edit_pin.childParamID)) selected_p_in = i;
            }
            pin_in_param.SelectedIndex = selected_p_in;
            int selected_p_out = -1;
            i = 0;
            foreach (CathodeParameterReference param_ref in flowgraph.nodes[selected_n_in].nodeParameterReferences)
            {
                pin_out_param.Items.Add("[" + BitConverter.ToString(param_ref.paramID) + "] " + NodeDB.GetParameterName(param_ref.paramID));
                if (selected_p_out == -1 && param_ref.paramID.SequenceEqual(edit_pin.parentParamID)) selected_p_out = i;
            }
            pin_out_param.SelectedIndex = selected_p_out;

            //TODO: i think param links should be treated as actual parameters for this exact reason... we won't get the links show in the list!

            //Title
            pin_link_id.Text = "Pin Link [" + BitConverter.ToString(edit_pin.connectionID) + "]";
        }

        /* Save */
        private void save_pin_Click(object sender, EventArgs e)
        {

        }
    }
}
