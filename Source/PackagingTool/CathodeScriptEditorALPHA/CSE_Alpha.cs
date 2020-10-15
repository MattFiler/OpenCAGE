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
    public partial class CSE_Alpha : Form
    {
        private CommandsPAK commandsPAK = null;
        private TreeUtility treeHelper;
        private ToolPaths Folders = new ToolPaths();
        private ContentTools_Loadscreen loadscreen;

        public CSE_Alpha()
        {
            InitializeComponent();
            treeHelper = new TreeUtility(FileTree);

            //Populate available maps
            List<string> all_map_dirs = MapDirectories.GetAvailable();
            env_list.Items.Clear();
            foreach (string map in all_map_dirs) env_list.Items.Add(map);
            env_list.SelectedIndex = 0;
        }

        /* Clear the UI */
        private void ClearUI(bool clear_flowgraph_list, bool clear_node_list, bool clear_parameter_list)
        {
            if (clear_flowgraph_list)
            {
                FileTree.Nodes.Clear();
                first_executed_flowgraph.Text = "Entry point: ";
                flowgraph_count.Text = "Flowgraph count: ";
            }
            if (clear_node_list)
            {
                node_search_box.Text = "";
                groupBox1.Text = "Selected Flowgraph Content";
                flowgraph_content.Items.Clear();
                selected_flowgraph = null;
            }
            if (clear_parameter_list)
            {
                selected_node = null;
                selected_node_id.Text = "";
                selected_node_type.Text = "";
                selected_node_type_description.Text = "";
                selected_node_name.Text = "";
                NodeParams.Controls.Clear();
                node_children.Items.Clear();
                node_parents.Items.Clear();
                node_to_flowgraph_jump.Visible = false;
            }
        }

        /* Load a COMMANDS.PAK into the editor */
        private void load_commands_pak_Click(object sender, EventArgs e)
        {
            //Reset all UI here
            ClearUI(true, true, true);
            commandsPAK = null;

            //Call loadscreen, which then calls StartLoadingContent below when shown
            loadscreen = new ContentTools_Loadscreen(null, null, this);
            loadscreen.Show();
        }
        public void StartLoadingContent()
        {
            //Load COMMANDS.PAK and populate file tree
            commandsPAK = new CommandsPAK(Folders.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/ENV/PRODUCTION/" + env_list.SelectedItem + "/WORLD/COMMANDS.PAK");
            treeHelper.UpdateFileTree(commandsPAK.GetFlowgraphNames());

            //Show info in UI
            first_executed_flowgraph.Text = "Entry point: " + commandsPAK.EntryPoints[0].name;
            flowgraph_count.Text = "Flowgraph count: " + commandsPAK.AllFlowgraphs.Count;

            loadscreen.Close();
        }

        /* Save the current edits */
        private void save_commands_pak_Click(object sender, EventArgs e)
        {
            if (commandsPAK == null) return;
            commandsPAK.Save();
            MessageBox.Show("Saved changes!", "Saved.", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /* Load nodes for selected script */
        private void FileTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (FileTree.SelectedNode == null) return;
            if (((TreeItem)FileTree.SelectedNode.Tag).Item_Type != TreeItemType.EXPORTABLE_FILE) return;
            LoadFlowgraph(((TreeItem)FileTree.SelectedNode.Tag).String_Value);
        }

        /* If selected node is a flowgraph instance, allow jump to it */
        private void node_to_flowgraph_jump_Click(object sender, EventArgs e)
        {
            LoadFlowgraph(selected_node_type_description.Text);
        }

        /* Select node from loaded flowgraph */
        private void flowgraph_content_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (flowgraph_content.SelectedIndex == -1 || selected_flowgraph == null) return;
            CathodeNodeEntity thisNodeInfo = selected_flowgraph.GetNodeByID(StringToByteArray(flowgraph_content.SelectedItem.ToString().Substring(0, 11).Replace("-", "")));
            if (thisNodeInfo != null) LoadNode(thisNodeInfo);
            //if (thisNodeInfo.dataTypeParam != null) MessageBox.Show(BitConverter.ToString(thisNodeInfo.dataTypeParam));
        }

        /* Go to parent link when selected */
        private void node_parents_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (node_parents.SelectedIndex == -1 || selected_flowgraph == null) return;
            CathodeNodeEntity thisNodeInfo = selected_flowgraph.GetNodeByID(StringToByteArray(node_parents.SelectedItem.ToString().Substring(1, 11).Replace("-", "")));
            if (thisNodeInfo != null) LoadNode(thisNodeInfo);
        }

        /* Go to selected pin out on button press */
        private void out_pin_goto_Click(object sender, EventArgs e)
        {
            if (node_children.SelectedIndex == -1 || selected_flowgraph == null) return;
            CathodeNodeEntity thisNodeInfo = selected_flowgraph.GetNodeByID(StringToByteArray(node_children.SelectedItem.ToString().Substring(1, 11).Replace("-", "")));
            if (thisNodeInfo != null) LoadNode(thisNodeInfo);
        }

        /* Edit selected pin out on button press */
        private void out_pin_edit_Click(object sender, EventArgs e)
        {
            if (node_children.SelectedIndex == -1 || selected_flowgraph == null) return;
            CSE_Alpha_EditPin pin_editor = new CSE_Alpha_EditPin(selected_flowgraph.GetChildLinksByID(selected_node.nodeID)[node_children.SelectedIndex], selected_flowgraph);
            pin_editor.Show();
            //todo: on close event, refresh list box
        }

        /* Search node list */
        private void node_search_btn_Click(object sender, EventArgs e)
        {

        }

        /* Load a flowgraph into the UI */
        CathodeFlowgraph selected_flowgraph = null;
        private void LoadFlowgraph(string FileName)
        {
            ClearUI(false, true, true);
            CathodeFlowgraph entry = commandsPAK.AllFlowgraphs[commandsPAK.GetFileIndex(FileName)];
            selected_flowgraph = entry;
            Cursor.Current = Cursors.WaitCursor;

            for (int i = 0; i < entry.nodes.Count; i++)
            {
                string desc = "";
                if (entry.nodes[i].HasNodeType) desc = " (" + NodeDB.GetTypeName(entry.nodes[i].nodeType, commandsPAK) + ")";
                else if (entry.nodes[i].HasDataType) desc = " (DataType " + entry.nodes[i].dataType + ")";
                string thisentrytext = BitConverter.ToString(entry.nodes[i].nodeID) + " " + NodeDB.GetFriendlyName(entry.nodes[i].nodeID) + desc;
                flowgraph_content.Items.Add(thisentrytext);
            }
            groupBox1.Text = "Selected Flowgraph Content - (" + BitConverter.ToString(entry.nodeID) + " - " + entry.name + ")";
            Cursor.Current = Cursors.Default;
        }

        /* Load a node into the UI */
        CathodeNodeEntity selected_node = null;
        private void LoadNode(CathodeNodeEntity edit_node)
        {
            ClearUI(false, false, true);
            selected_node = edit_node;
            Cursor.Current = Cursors.WaitCursor;

            //populate info labels
            selected_node_id.Text = BitConverter.ToString(edit_node.nodeID);
            selected_node_type.Text = (edit_node.HasNodeType) ? BitConverter.ToString(edit_node.nodeType) : (edit_node.HasDataType) ? edit_node.dataType.ToString() : "";
            string nodetypedesc = "";
            if (edit_node.HasNodeType) nodetypedesc = NodeDB.GetTypeName(edit_node.nodeType, commandsPAK);
            else if (edit_node.HasDataType) nodetypedesc = "DataType " + edit_node.dataType;
            selected_node_type_description.Text = nodetypedesc;
            selected_node_name.Text = "Node Name: " + NodeDB.GetFriendlyName(edit_node.nodeID);
            node_to_flowgraph_jump.Visible = (commandsPAK.GetFlowgraph(edit_node.nodeType) != null);

            //TODO: load this in nicer
            //List<string> tempTest = File.ReadAllLines("../../../ENUM LIST").ToList<string>();

            //populate parameter inputs
            int current_ui_offset = 10;
            int additive_ui_offset = 30;
            for (int i = 0; i < edit_node.nodeParameterReferences.Count; i++)
            {
                CathodeParameter this_param = commandsPAK.GetParameter(edit_node.nodeParameterReferences[i].offset);

                Button param_edit_button = new Button();
                ComboBox param_selector = new ComboBox();
                Label param_label = new Label();
                int selected_index = -1;
                int selected_index_counter = 0;

                switch (this_param.dataType)
                {
                    case CathodeDataType.TRANSFORM:
                        for (int x = 0; x < commandsPAK.AllParameters.Count; x++)
                        {
                            if (!(commandsPAK.AllParameters[x] is CathodeTransform)) continue;
                            CathodeTransform cTrans = (CathodeTransform)commandsPAK.AllParameters[x];
                            param_selector.Items.Add("(X: " + cTrans.position.x + ", Y: " + cTrans.position.y + ", Z: " + cTrans.position.z + "), " +
                                                     "(X: " + cTrans.rotation.x + ", Y: " + cTrans.rotation.x + ", Z: " + cTrans.rotation.z + ")");
                            if (cTrans.offset == this_param.offset) selected_index = selected_index_counter;
                            selected_index_counter++;
                        }
                        break;

                    case CathodeDataType.INTEGER:
                        for (int x = 0; x < commandsPAK.AllParameters.Count; x++)
                        {
                            if (!(commandsPAK.AllParameters[x] is CathodeInteger)) continue;
                            CathodeInteger cInt = (CathodeInteger)commandsPAK.AllParameters[x];
                            param_selector.Items.Add(cInt.value);
                            if (cInt.offset == this_param.offset) selected_index = selected_index_counter;
                            selected_index_counter++;
                        }
                        break;

                    case CathodeDataType.STRING:
                        for (int x = 0; x < commandsPAK.AllParameters.Count; x++)
                        {
                            if (!(commandsPAK.AllParameters[x] is CathodeString)) continue;
                            CathodeString cString = (CathodeString)commandsPAK.AllParameters[x];
                            param_selector.Items.Add(cString.value);
                            if (cString.offset == this_param.offset) selected_index = selected_index_counter;
                            selected_index_counter++;
                        }
                        break;

                    case CathodeDataType.BOOL:
                        //TODO: might be nice to use a checkbox here
                        for (int x = 0; x < commandsPAK.AllParameters.Count; x++)
                        {
                            if (!(commandsPAK.AllParameters[x] is CathodeBool)) continue;
                            CathodeBool cBool = (CathodeBool)commandsPAK.AllParameters[x];
                            param_selector.Items.Add(cBool.value);
                            if (cBool.offset == this_param.offset) selected_index = selected_index_counter;
                            selected_index_counter++;
                        }
                        param_edit_button.Visible = false;
                        break;

                    case CathodeDataType.FLOAT:
                        for (int x = 0; x < commandsPAK.AllParameters.Count; x++)
                        {
                            if (!(commandsPAK.AllParameters[x] is CathodeFloat)) continue;
                            CathodeFloat cFloat = (CathodeFloat)commandsPAK.AllParameters[x];
                            param_selector.Items.Add(cFloat.value);
                            if (cFloat.offset == this_param.offset) selected_index = selected_index_counter;
                            selected_index_counter++;
                        }
                        break;

                    case CathodeDataType.RESOURCE_ID:
                        for (int x = 0; x < commandsPAK.AllParameters.Count; x++)
                        {
                            if (!(commandsPAK.AllParameters[x] is CathodeResource)) continue;
                            CathodeResource cResource = (CathodeResource)commandsPAK.AllParameters[x];
                            param_selector.Items.Add(BitConverter.ToString(cResource.resourceID));
                            if (cResource.offset == this_param.offset) selected_index = selected_index_counter;
                            selected_index_counter++;
                        }
                        param_edit_button.Visible = false;
                        break;

                    case CathodeDataType.VECTOR3:
                        for (int x = 0; x < commandsPAK.AllParameters.Count; x++)
                        {
                            if (!(commandsPAK.AllParameters[x] is CathodeVector3)) continue;
                            CathodeVector3 cVec3 = (CathodeVector3)commandsPAK.AllParameters[x];
                            param_selector.Items.Add("(X: " + cVec3.value.x + ", Y: " + cVec3.value.y + ", Z: " + cVec3.value.z + ")");
                            if (cVec3.offset == this_param.offset) selected_index = selected_index_counter;
                            selected_index_counter++;
                        }
                        break;

                    case CathodeDataType.ENUM:
                        for (int x = 0; x < commandsPAK.AllParameters.Count; x++)
                        {
                            if (!(commandsPAK.AllParameters[x] is CathodeEnum)) continue;
                            CathodeEnum cEnum = (CathodeEnum)commandsPAK.AllParameters[x];
                            string enumString = BitConverter.ToString(cEnum.enumID);
                            string enumDesc = /*GetEnumCodename(cEnum.enumID);*/ "";
                            string enumIndexDesc = "";
                            if (enumDesc != BitConverter.ToString(cEnum.enumID))
                            {
                                /*
                                for (int y = 0; y < tempTest.Count; y++)
                                {
                                    if (tempTest[y].Contains(enumString))
                                    {
                                        enumIndexDesc = " " + tempTest[y + cEnum.enumIndex + 1];
                                        break;
                                    }
                                }
                                */
                            }
                            param_selector.Items.Add("Enum ID: " + enumString + " (" + enumDesc + ") - Index: " + cEnum.enumIndex + enumIndexDesc);
                            if (cEnum.offset == this_param.offset) selected_index = selected_index_counter;
                            selected_index_counter++;
                        }
                        param_edit_button.Visible = false;
                        break;

                    default:
                        param_label.Text = BitConverter.ToString(this_param.unknownContent) + " = " + BitConverter.ToString(edit_node.nodeParameterReferences[i].paramID) + " (" + NodeDB.GetParameterName(edit_node.nodeParameterReferences[i].paramID) + ") - " + this_param.dataType.ToString();
                        param_label.Location = new Point(10, current_ui_offset + 4);
                        param_label.Size = new Size(550, param_label.Size.Height);
                        NodeParams.Controls.Add(param_label);
                        Label dummy_label = new Label();
                        dummy_label.Text = "";
                        NodeParams.Controls.Add(dummy_label);
                        current_ui_offset += additive_ui_offset;
                        continue;
                }

                param_edit_button.Name = this_param.offset.ToString();
                param_edit_button.Location = new Point(10, current_ui_offset-1);
                param_edit_button.Size = new Size(20, param_selector.Size.Height+2);
                param_edit_button.Text = "*";
                param_edit_button.Click += new System.EventHandler(this.param_edit_btn_Click);
                NodeParams.Controls.Add(param_edit_button);

                param_selector.Name = i.ToString() + " " + ((int)this_param.dataType).ToString();
                param_selector.SelectedIndex = selected_index;
                param_selector.SelectedIndexChanged += new System.EventHandler(param_selector_SelectedIndexChanged);
                param_selector.Location = new Point((param_edit_button.Visible) ? 40 : 10, current_ui_offset);
                param_selector.Size = new Size((param_edit_button.Visible) ? 410 : 450, param_selector.Size.Height);
                param_selector.DropDownStyle = ComboBoxStyle.DropDownList;
                NodeParams.Controls.Add(param_selector);

                param_label.Text = BitConverter.ToString(edit_node.nodeParameterReferences[i].paramID) + " (" + NodeDB.GetParameterName(edit_node.nodeParameterReferences[i].paramID) + ")";
                param_label.Location = new Point(465, current_ui_offset + 4);
                param_label.Size = new Size(550, param_label.Size.Height);
                NodeParams.Controls.Add(param_label);

                current_ui_offset += additive_ui_offset;
            }

            //Child links (pins out of this node)
            node_children.Items.Clear();
            foreach (CathodeNodeLink id in selected_flowgraph.GetChildLinksByID(edit_node.nodeID))
            {
                CathodeNodeEntity thisNodeInfo = selected_flowgraph.GetNodeByID(id.childID);
                string desc = "";
                if (thisNodeInfo.HasNodeType) desc = " (" + NodeDB.GetTypeName(thisNodeInfo.nodeType, commandsPAK) + ")";
                else if (thisNodeInfo.HasDataType) desc = " (DataType " + thisNodeInfo.dataType + ")";
                node_children.Items.Add("[" + BitConverter.ToString(id.connectionID) + "] Pin out " + BitConverter.ToString(id.parentParamID) + " (" + NodeDB.GetParameterName(id.parentParamID) + "), goes to " + BitConverter.ToString(id.childParamID) + " (" + NodeDB.GetParameterName(id.childParamID) + ") on node " + BitConverter.ToString(id.childID) + " (" + NodeDB.GetFriendlyName(id.childID) + desc + ")");
            }

            //Parent links (pins in to this node)
            node_parents.Items.Clear();
            foreach (CathodeNodeLink id in selected_flowgraph.GetParentLinksByID(edit_node.nodeID))
            {
                CathodeNodeEntity thisNodeInfo = selected_flowgraph.GetNodeByID(id.parentID);
                string desc = "";
                if (thisNodeInfo.HasNodeType) desc = " (" + NodeDB.GetTypeName(thisNodeInfo.nodeType, commandsPAK) + ")";
                else if (thisNodeInfo.HasDataType) desc = " (DataType " + thisNodeInfo.dataType + ")";
                node_parents.Items.Add("[" + BitConverter.ToString(id.connectionID) + "] Pin in " + BitConverter.ToString(id.childParamID) + " (" + NodeDB.GetParameterName(id.childParamID) + "), comes from " + BitConverter.ToString(id.parentParamID) + " (" + NodeDB.GetParameterName(id.parentParamID) + ") on node " + BitConverter.ToString(id.parentID) + " (" + NodeDB.GetFriendlyName(id.parentID) + desc + ")");
            }

            Cursor.Current = Cursors.Default;
        }

        /* User selected a new parameter to use, update it in CommandsPAK */
        private void param_selector_SelectedIndexChanged(object sender, EventArgs e)
        {
            int new_selected_index = ((ComboBox)sender).SelectedIndex;
            string[] content = ((ComboBox)sender).Name.Split(' ');
            int i = 0;
            for (int x = 0; x < commandsPAK.AllParameters.Count; x++)
            {
                switch ((CathodeDataType)Convert.ToInt32(content[1]))
                {
                    case CathodeDataType.TRANSFORM:
                        if (!(commandsPAK.AllParameters[x] is CathodeTransform)) continue;
                        break;

                    case CathodeDataType.INTEGER:
                        if (!(commandsPAK.AllParameters[x] is CathodeInteger)) continue;
                        break;

                    case CathodeDataType.STRING:
                        if (!(commandsPAK.AllParameters[x] is CathodeString)) continue;
                        break;

                    case CathodeDataType.BOOL:
                        if (!(commandsPAK.AllParameters[x] is CathodeBool)) continue;
                        break;

                    case CathodeDataType.FLOAT:
                        if (!(commandsPAK.AllParameters[x] is CathodeFloat)) continue;
                        break;

                    case CathodeDataType.RESOURCE_ID:
                        if (!(commandsPAK.AllParameters[x] is CathodeResource)) continue;
                        break;

                    case CathodeDataType.VECTOR3:
                        if (!(commandsPAK.AllParameters[x] is CathodeVector3)) continue;
                        break;

                    case CathodeDataType.ENUM:
                        if (!(commandsPAK.AllParameters[x] is CathodeEnum)) continue;
                        break;
                }
                if (i == new_selected_index) {
                    selected_node.nodeParameterReferences[Convert.ToInt32(content[0])].offset = commandsPAK.AllParameters[x].offset;
                    return;
                }
                i++;
            }
            throw new Exception("ERROR! Couldn't get new offset.");
        }

        /* User selected parameter to edit, show edit UI & refresh when closed */
        private void param_edit_btn_Click(object sender, EventArgs e)
        {
            CSE_Alpha_EditParam param_editor = new CSE_Alpha_EditParam(commandsPAK.GetParameter(Convert.ToInt32(((Button)sender).Name)));
            param_editor.Show();
            //todo: on close event, refresh list box
        }


        //cheers: https://stackoverflow.com/a/321404/3798962
        private byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length).Where(x => x % 2 == 0).Select(x => Convert.ToByte(hex.Substring(x, 2), 16)).ToArray();
        }
    }
}
