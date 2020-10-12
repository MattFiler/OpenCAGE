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
        private CommandsPAK commandsPAK;
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

        /* Load a COMMANDS.PAK into the editor */
        private void load_commands_pak_Click(object sender, EventArgs e)
        {
            //Reset all UI here
            FileTree.Nodes.Clear();

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

        }

        /* Load nodes for selected script */
        int selected_script_id = -1;
        private void FileTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (FileTree.SelectedNode == null) return;
            if (((TreeItem)FileTree.SelectedNode.Tag).Item_Type != TreeItemType.EXPORTABLE_FILE) return;
            string FileName = ((TreeItem)FileTree.SelectedNode.Tag).String_Value;
            selected_script_id = commandsPAK.GetFileIndex(FileName);
            CathodeFlowgraph entry = commandsPAK.AllFlowgraphs[selected_script_id];

            Cursor.Current = Cursors.WaitCursor;
            NodeParams.Controls.Clear();
            flowgraph_content.Items.Clear();
            textBox1.Text = "";
            for (int i = 0; i < entry.nodes.Count; i++)
            {
                string desc = "";
                if (entry.nodes[i].HasNodeType) desc = " (" + NodeDB.GetTypeName(entry.nodes[i].nodeType) + ")";
                else if (entry.nodes[i].HasDataType) desc = " (DataType " + entry.nodes[i].dataType + ")";
                string thisentrytext = BitConverter.ToString(entry.nodes[i].nodeID) + " " + NodeDB.GetFriendlyName(entry.nodes[i].nodeID) + desc;
                flowgraph_content.Items.Add(thisentrytext);
            }
            groupBox1.Text = "Selected Flowgraph Content - (" + BitConverter.ToString(entry.nodeID) + ")";
            Cursor.Current = Cursors.Default;
        }

        /* Select node from loaded flowgraph */
        private void flowgraph_content_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (flowgraph_content.SelectedIndex == -1) return;
            CathodeNodeEntity thisNodeInfo = commandsPAK.AllFlowgraphs[selected_script_id].GetNodeByID(StringToByteArray(flowgraph_content.SelectedItem.ToString().Substring(0, 11).Replace("-", "")));
            if (thisNodeInfo != null) LoadNodeToEdit(thisNodeInfo);
            if (thisNodeInfo.dataTypeParam != null) MessageBox.Show(BitConverter.ToString(thisNodeInfo.dataTypeParam));
        }

        /* Select node parent/child link */
        private void node_parents_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (node_parents.SelectedIndex == -1) return;
            CathodeNodeEntity thisNodeInfo = commandsPAK.AllFlowgraphs[selected_script_id].GetNodeByID(StringToByteArray(node_parents.SelectedItem.ToString().Substring(1, 11).Replace("-", "")));
            if (thisNodeInfo != null) LoadNodeToEdit(thisNodeInfo);
        }
        private void node_children_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (node_children.SelectedIndex == -1) return;
            CathodeNodeEntity thisNodeInfo = commandsPAK.AllFlowgraphs[selected_script_id].GetNodeByID(StringToByteArray(node_children.SelectedItem.ToString().Substring(1, 11).Replace("-", "")));
            if (thisNodeInfo != null) LoadNodeToEdit(thisNodeInfo);
        }

        /* Load metadata and params for selected node in script */
        private void LoadNodeToEdit(CathodeNodeEntity ThisNode)
        {
            Cursor.Current = Cursors.WaitCursor;

            //populate info labels
            label2.Text = BitConverter.ToString(ThisNode.nodeID);
            label3.Text = (ThisNode.HasNodeType) ? BitConverter.ToString(ThisNode.nodeType) : (ThisNode.HasDataType) ? ThisNode.dataType.ToString() : "";
            string nodetypedesc = "";
            if (ThisNode.HasNodeType) nodetypedesc = NodeDB.GetTypeName(ThisNode.nodeType);
            else if (ThisNode.HasDataType) nodetypedesc = "DataType " + ThisNode.dataType;
            label5.Text = nodetypedesc;
            label9.Text = "Node Name: " + NodeDB.GetFriendlyName(ThisNode.nodeID);

            //TODO: load this in nicer
            //List<string> tempTest = File.ReadAllLines("../../../ENUM LIST").ToList<string>();

            //populate parameter inputs
            NodeParams.Controls.Clear();
            int current_ui_offset = 10;
            int additive_ui_offset = 30;
            for (int i = 0; i < ThisNode.nodeParameterReferences.Count; i++)
            {
                ComboBox newComboBox = new ComboBox();
                Label newLabel = new Label();
                int newSelectedIndex = -1;
                int selectedIndexCounter = 0;

                CathodeParameter thisParam = commandsPAK.GetParameter(ThisNode.nodeParameterReferences[i].offset);
                switch (thisParam.dataType)
                {
                    case CathodeDataType.TRANSFORM:
                        for (int x = 0; x < commandsPAK.AllParameters.Count; x++)
                        {
                            if (!(commandsPAK.AllParameters[x] is CathodeTransform)) continue;
                            CathodeTransform cTrans = (CathodeTransform)commandsPAK.AllParameters[x];
                            newComboBox.Items.Add("(X: " + cTrans.position.x + ", Y: " + cTrans.position.y + ", Z: " + cTrans.position.z + "), " +
                                                  "(X: " + cTrans.rotation.x + ", Y: " + cTrans.rotation.x + ", Z: " + cTrans.rotation.z + ")");
                            if (cTrans.offset == thisParam.offset) newSelectedIndex = selectedIndexCounter;
                            selectedIndexCounter++;
                        }
                        break;

                    case CathodeDataType.INTEGER:
                        for (int x = 0; x < commandsPAK.AllParameters.Count; x++)
                        {
                            if (!(commandsPAK.AllParameters[x] is CathodeInteger)) continue;
                            CathodeInteger cInt = (CathodeInteger)commandsPAK.AllParameters[x];
                            newComboBox.Items.Add(cInt.value);
                            if (cInt.offset == thisParam.offset) newSelectedIndex = selectedIndexCounter;
                            selectedIndexCounter++;
                        }
                        break;

                    case CathodeDataType.STRING:
                        for (int x = 0; x < commandsPAK.AllParameters.Count; x++)
                        {
                            if (!(commandsPAK.AllParameters[x] is CathodeString)) continue;
                            CathodeString cString = (CathodeString)commandsPAK.AllParameters[x];
                            newComboBox.Items.Add(cString.value);
                            if (cString.offset == thisParam.offset) newSelectedIndex = selectedIndexCounter;
                            selectedIndexCounter++;
                        }
                        break;

                    case CathodeDataType.BOOL:
                        for (int x = 0; x < commandsPAK.AllParameters.Count; x++)
                        {
                            if (!(commandsPAK.AllParameters[x] is CathodeBool)) continue;
                            CathodeBool cBool = (CathodeBool)commandsPAK.AllParameters[x];
                            newComboBox.Items.Add(cBool.value);
                            if (cBool.offset == thisParam.offset) newSelectedIndex = selectedIndexCounter;
                            selectedIndexCounter++;
                        }
                        break;

                    case CathodeDataType.FLOAT:
                        for (int x = 0; x < commandsPAK.AllParameters.Count; x++)
                        {
                            if (!(commandsPAK.AllParameters[x] is CathodeFloat)) continue;
                            CathodeFloat cFloat = (CathodeFloat)commandsPAK.AllParameters[x];
                            newComboBox.Items.Add(cFloat.value);
                            if (cFloat.offset == thisParam.offset) newSelectedIndex = selectedIndexCounter;
                            selectedIndexCounter++;
                        }
                        break;

                    case CathodeDataType.RESOURCE_ID:
                        for (int x = 0; x < commandsPAK.AllParameters.Count; x++)
                        {
                            if (!(commandsPAK.AllParameters[x] is CathodeResource)) continue;
                            CathodeResource cResource = (CathodeResource)commandsPAK.AllParameters[x];
                            newComboBox.Items.Add(BitConverter.ToString(cResource.resourceID));
                            if (cResource.offset == thisParam.offset) newSelectedIndex = selectedIndexCounter;
                            selectedIndexCounter++;
                        }
                        break;

                    case CathodeDataType.VECTOR3:
                        for (int x = 0; x < commandsPAK.AllParameters.Count; x++)
                        {
                            if (!(commandsPAK.AllParameters[x] is CathodeVector3)) continue;
                            CathodeVector3 cVec3 = (CathodeVector3)commandsPAK.AllParameters[x];
                            newComboBox.Items.Add("(X: " + cVec3.value.x + ", Y: " + cVec3.value.y + ", Z: " + cVec3.value.z + ")");
                            if (cVec3.offset == thisParam.offset) newSelectedIndex = selectedIndexCounter;
                            selectedIndexCounter++;
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
                            newComboBox.Items.Add("Enum ID: " + enumString + " (" + enumDesc + ") - Index: " + cEnum.enumIndex + enumIndexDesc);
                            if (cEnum.offset == thisParam.offset) newSelectedIndex = selectedIndexCounter;
                            selectedIndexCounter++;
                        }
                        break;

                    default:
                        newLabel.Text = BitConverter.ToString(thisParam.unknownContent) + " = " + BitConverter.ToString(ThisNode.nodeParameterReferences[i].paramID) + " (" + NodeDB.GetParameterName(ThisNode.nodeParameterReferences[i].paramID) + ") - " + thisParam.dataType.ToString();
                        newLabel.Location = new Point(10, current_ui_offset + 4);
                        newLabel.Size = new Size(550, newLabel.Size.Height);
                        NodeParams.Controls.Add(newLabel);
                        Label dummyLabel = new Label();
                        dummyLabel.Text = "";
                        NodeParams.Controls.Add(dummyLabel);
                        current_ui_offset += additive_ui_offset;
                        continue;
                }

                newComboBox.SelectedIndex = newSelectedIndex;
                newComboBox.Location = new Point(10, current_ui_offset);
                newComboBox.Size = new Size(450, newComboBox.Size.Height);
                newComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
                NodeParams.Controls.Add(newComboBox);

                newLabel.Text = BitConverter.ToString(ThisNode.nodeParameterReferences[i].paramID) + " (" + NodeDB.GetParameterName(ThisNode.nodeParameterReferences[i].paramID) + ")";
                newLabel.Location = new Point(465, current_ui_offset + 4);
                newLabel.Size = new Size(550, newLabel.Size.Height);
                NodeParams.Controls.Add(newLabel);

                current_ui_offset += additive_ui_offset;
            }

            //Child links (pins out of this node)
            node_children.Items.Clear();
            foreach (CathodeNodeLink id in commandsPAK.AllFlowgraphs[selected_script_id].GetChildLinksByID(ThisNode.nodeID))
            {
                CathodeNodeEntity thisNodeInfo = commandsPAK.AllFlowgraphs[selected_script_id].GetNodeByID(id.childID);
                string desc = "";
                if (thisNodeInfo.HasNodeType) desc = " (" + NodeDB.GetTypeName(thisNodeInfo.nodeType) + ")";
                else if (thisNodeInfo.HasDataType) desc = " (DataType " + thisNodeInfo.dataType + ")";
                node_children.Items.Add("[" + BitConverter.ToString(id.childID) + "] Pin out " + BitConverter.ToString(id.parentParamID) + " (" + NodeDB.GetParameterName(id.parentParamID) + "), goes to " + BitConverter.ToString(id.childParamID) + " (" + NodeDB.GetParameterName(id.childParamID) + ") on node " + BitConverter.ToString(id.childID) + " (" + NodeDB.GetFriendlyName(id.childID) + desc + ")");
            }

            //Parent links (pins in to this node)
            node_parents.Items.Clear();
            foreach (CathodeNodeLink id in commandsPAK.AllFlowgraphs[selected_script_id].GetParentLinksByID(ThisNode.nodeID))
            {
                CathodeNodeEntity thisNodeInfo = commandsPAK.AllFlowgraphs[selected_script_id].GetNodeByID(id.parentID);
                string desc = "";
                if (thisNodeInfo.HasNodeType) desc = " (" + NodeDB.GetTypeName(thisNodeInfo.nodeType) + ")";
                else if (thisNodeInfo.HasDataType) desc = " (DataType " + thisNodeInfo.dataType + ")";
                node_parents.Items.Add("[" + BitConverter.ToString(id.parentID) + "] Pin in " + BitConverter.ToString(id.childParamID) + " (" + NodeDB.GetParameterName(id.childParamID) + "), comes from " + BitConverter.ToString(id.parentParamID) + " (" + NodeDB.GetParameterName(id.parentParamID) + ") on node " + BitConverter.ToString(id.parentID) + " (" + NodeDB.GetFriendlyName(id.parentID) + desc + ")");
            }

            Cursor.Current = Cursors.Default;
        }

        //cheers: https://stackoverflow.com/a/321404/3798962
        public byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length).Where(x => x % 2 == 0).Select(x => Convert.ToByte(hex.Substring(x, 2), 16)).ToArray();
        }
    }
}
