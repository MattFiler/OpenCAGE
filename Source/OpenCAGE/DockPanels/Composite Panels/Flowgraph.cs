using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.Popups;
using OpenCAGE.Popups.Base;
using OpenCAGE.Popups.UserControls;
using OpenCAGE;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using System.Xml.Linq;
using WeifenLuo.WinFormsUI.Docking;
using static CathodeLib.CompositeFlowgraphTable;
using static CathodeLib.CompositePinInfoTable;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace OpenCAGE
{
    public partial class Flowgraph : DockContent
    {
        private Commands _commands;
        private Composite _composite;
        private int _spawnOffset = 0;
        private bool _subscribedToEntEvents = false;

        public string FlowgraphName => _flowgraphName;
        private string _flowgraphName = "";

        public STNodeEditor Nodegraph => stNodeEditor1; //This should be treated as read only. Only the Flowgraph UI should modify it directly.

        [Obsolete("Designer only", true)]
        public Flowgraph()
        {
            InitializeComponent();
        }

        public Flowgraph(Commands commands)
        {
            _commands = commands;

            InitializeComponent();
            this.VisibleChanged += Flowgraph_VisibleChanged;
            this.FormClosed += Flowgraph_FormClosed;

            stNodeEditor1.LoadAssembly(Application.ExecutablePath);
            stNodeEditor1.AllowSameOwnerConnections = true;
            stNodeEditor1.SelectedChanged += Owner_SelectedChanged;
            stNodeEditor1.PinToNodeConnected += StNodeEditor1_PinToNodeConnected;

            //todo: i feel like these events should come from the compositedisplay?
            Singleton.OnEntityDeleted += OnEntityDeletedGlobally;
            Singleton.OnEntityRenamed += OnEntityRenamedGlobally;
            Singleton.OnNodeStyleChanged += OnNodeStyleChanged;
        }

        private void Flowgraph_VisibleChanged(object sender, EventArgs e)
        {
            //Only add/select entities on the visible page
            if (this.Visible)
            {
                if (_subscribedToEntEvents)
                    return;

                _subscribedToEntEvents = true;
                Singleton.OnEntitySelected += OnEntitySelectedGlobally;
            }
            else
            {
                _subscribedToEntEvents = false;
                Singleton.OnEntitySelected -= OnEntitySelectedGlobally;
            }
            Singleton.OnEntityAdded -= OnEntityAddedViaPopup;
        }

        private void Flowgraph_FormClosed(object sender, FormClosedEventArgs e)
        {
            Debug.Log("Flowgraph", this.Text + " -> CLOSING!");

            this.VisibleChanged -= Flowgraph_VisibleChanged;
            this.FormClosed -= Flowgraph_FormClosed;

            stNodeEditor1.SelectedChanged -= Owner_SelectedChanged;
            Singleton.OnEntitySelected -= OnEntitySelectedGlobally;
            Singleton.OnEntityDeleted -= OnEntityDeletedGlobally;
            Singleton.OnEntityRenamed -= OnEntityRenamedGlobally;
            Singleton.OnEntityAdded -= OnEntityAddedViaPopup;
            Singleton.OnNodeStyleChanged -= OnNodeStyleChanged;

            if (_renameFlowgraphPopup != null)
                _renameFlowgraphPopup.FormClosed -= _renameFlowgraphPopup_FormClosed;

            this.Dispose();
        }

        private void OnEntitySelectedGlobally(Entity entity)
        {
            if (entity == _previouslySelectedEntity)
                return;
            SelectAllNodesForEntity(entity);
        }

        private void OnEntityRenamedGlobally(Entity entity, string newNew)
        {
            foreach (STNode node in stNodeEditor1.Nodes)
            {
                if (node.Entity.shortGUID != entity.shortGUID)
                    continue;
                RegenerateNodeStyle(node);
            }
        }

        private void OnNodeStyleChanged()
        {
            foreach (STNode node in stNodeEditor1.Nodes)
            {
                RegenerateNodeStyle(node);
            }
        }

        private Entity _previouslySelectedEntity = null;
        private bool _selectedNodeChanged = false;
        private void Owner_SelectedChanged(object sender, EventArgs e)
        {
            STNode[] nodes = stNodeEditor1.GetSelectedNode();
            if (nodes.Length != 1) return;

            Entity ent = _composite.GetEntityByID(nodes[0].ShortGUID);
            if (ent == _previouslySelectedEntity) return;
            _previouslySelectedEntity = ent;

            _selectedNodeChanged = true;
            Singleton.Editor.CommandsDisplay?.CompositeDisplay?.LoadEntity(ent, false);
            Singleton.OnEntitySelected?.Invoke(ent); //need to call this again b/c the activation event doesn't fire here
            _selectedNodeChanged = false;
        }

        public void SelectAllNodesForEntity(Entity entity)
        {
            if (_selectedNodeChanged) //TEMPORARY HACK FIX FOR DE-SELECTION RACE CONDITION BUG
                return;

            DeselectAllNodes();

            if (entity == null)
                return;

            STNode[] nodes = stNodeEditor1.Nodes.ToArray();
            foreach (STNode node in nodes)
            {
                if (node.ShortGUID != entity.shortGUID)
                    continue;
                SelectNode(node);
            }
        }

        private void SelectNode(STNode node)
        {
            _previouslySelectedEntity = node.Entity;
            Debug.Log("Flowgraph", "Select node: " + node.Title + " - " + node.Guid);

            stNodeEditor1.AddSelectedNode(node);
            node.SetSelected(true, true);
            stNodeEditor1.SetActiveNode(node);

            stNodeEditor1.CenterCanvasOn(node.Location.X + (node.Width / 2), node.Location.Y + (node.Height / 2), true);
        }

        private void DeselectAllNodes()
        {
            STNode[] nodes = stNodeEditor1.Nodes.ToArray();
            foreach (STNode node in nodes)
            {
                if (!node.IsSelected)
                    continue;
                node.SetSelected(false, true);
            }
            stNodeEditor1.SetActiveNode(null);
            stNodeEditor1.RemoveAllSelectedNodes();
        }

        //if a line is dragged from a pin to a node: allow user to select the pin to connect to
        SelectDestinationPin _destPinSelector = null;
        ManageEntityPins _managePinsDialog = null;
        private void StNodeEditor1_PinToNodeConnected(object sender, STNodeEditorPinToNodeEventArgs e)
        {
            if (_destPinSelector != null)
                _destPinSelector.Close();

            _destPinSelector = new SelectDestinationPin();
            _destPinSelector.Show();
            _destPinSelector.PopulateOptions(e.ToNode, e.FromPin);
        }

        private STNode AddNodeForEntity(Entity entity)
        {
            STNode node = EntityToNode(entity);
            if (SettingsManager.GetBool(Singleton.Settings.PopulateAllPinsOnCreateNode))
                AddAllPins(node);
            return node;
        }

        private void OnEntityDeletedGlobally(Entity entity)
        {
            List<STNode> nodes = new List<STNode>();

            STNode[] allNodes = stNodeEditor1.Nodes.ToArray();
            foreach (STNode node in allNodes)
            {
                if (node.ShortGUID != entity.shortGUID)
                    continue;

                nodes.Add(node);
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                stNodeEditor1.Nodes.Remove(nodes[i]);
            }
        }

        private int CountNodesForEntity(Entity entity)
        {
            int count = 0;
            foreach (STNode node in stNodeEditor1.Nodes)
            {
                if (node.Entity == entity)
                    count++;
            }
            return count;
        }
        private bool HasMultipleNodesForEntity(Entity entity)
        {
            bool foundOnce = false;
            foreach (STNode node in stNodeEditor1.Nodes)
            {
                if (node.Entity == entity)
                {
                    if (foundOnce)
                        return true;
                    foundOnce = true;
                }
            }
            return false;
        }

        //NOTE: This assumes you've already checked with FlowgraphLayoutManager that LinksMatch!
        public void ShowFlowgraph(Composite composite, FlowgraphMeta flowgraphMeta)
        {
#if DEBUG
            Stopwatch timer = Stopwatch.StartNew();
            Debug.Log("Flowgraph", "Loading: " + flowgraphMeta.Name);
#endif

            if (_commands.Utils.PurgeDeadLinks(composite))
                _commands.Utils.PurgedComposites.purged.Add(composite.shortGUID);

            _composite = composite;
            this.Text = flowgraphMeta.Name;
            _flowgraphName = flowgraphMeta.Name;

            stNodeEditor1.SuspendLayout();
            stNodeEditor1.Nodes.Clear();
            _spawnOffset = 0;

            //Populate nodes for entities
            List<Tuple<Entity, FlowgraphMeta.NodeMeta>> entities = new List<Tuple<Entity, FlowgraphMeta.NodeMeta>>();
            for (int i = 0; i < flowgraphMeta.Nodes.Count; i++)
            {
                Entity entity = composite.GetEntityByID(flowgraphMeta.Nodes[i].EntityGUID);
                if (entity == null)
                    continue; //If an entity doesn't exist, this should've already been deemed acceptable by FlowgraphLayoutManager.
                entities.Add(new Tuple<Entity, FlowgraphMeta.NodeMeta>(entity, flowgraphMeta.Nodes[i]));
            }
            STNode[] nodes = new STNode[entities.Count];
            for (int i = 0; i < entities.Count; i++)
            {
                nodes[i] = EntityToNode(entities[i].Item1);
                nodes[i].SetPosition(entities[i].Item2.Position);
                nodes[i].NodeID = entities[i].Item2.NodeID;
            }

            //Add only the pins needed for connections and user-added pins
            for (int i = 0; i < entities.Count; i++)
            {
                nodes[i].AddPinsForConnections(composite, _commands, 
                    entities[i].Item2.ConnectionsOut, 
                    entities[i].Item2.UnlinkedPins);
            }

            //Populate connections
            for (int i = 0; i < entities.Count; i++)
            {
                foreach (FlowgraphMeta.NodeMeta.ConnectionMeta connectionMeta in entities[i].Item2.ConnectionsOut)
                {
                    STNode connectedNode = nodes.FirstOrDefault(o => o.NodeID == connectionMeta.ConnectedNodeID && o.ShortGUID == connectionMeta.ConnectedEntityGUID);

                    EntityConnector connector = nodes[i].Entity.childLinks.FirstOrDefault(o => o.thisParamID == connectionMeta.ParameterGUID && o.linkedParamID == connectionMeta.ConnectedParameterGUID && o.linkedEntityID == connectedNode?.ShortGUID);
                    if (!connector.ID.IsInvalid) //NOTE: This condition should never fail if the layout has been checked by FlowgraphLayoutManager!
                    {
                        //Add pins for both nodes in the connection if they don't exist
                        nodes[i].AddPinsForConnection(connectedNode, connectionMeta.ParameterGUID, connectionMeta.ConnectedParameterGUID, composite, _commands);
                        
                        STNodeOption pinOut = nodes[i].GetOption(connectionMeta.ParameterGUID);
                        STNodeOption pinIn = connectedNode.GetOption(connectionMeta.ConnectedParameterGUID);

                        if (pinIn == null)
                        {
                            Debug.Log("Flowgraph", "WARNING: Adding input option for " + connectedNode.Title + ", as pin was not found...");
                            pinIn = connectedNode.AddInputOption(connectionMeta.ConnectedParameterGUID);
                        }
                        if (pinOut == null)
                        {
                            Debug.Log("Flowgraph", "WARNING: Adding output option for " + nodes[i].Title + ", as pin was not found...");
                            pinOut = nodes[i].AddOutputOption(connectionMeta.ParameterGUID);
                        }

                        ConnectionStatus status = pinOut.ConnectOption(pinIn);
                        if (status != ConnectionStatus.Connected)
                        {
                            //NOTE: We hit this for some in the base game, but it SHOULDN'T be a problem -> links that can't connect won't logically work.
                            Debug.Log("Flowgraph", "WARNING: Could not create the following connection...\n\t" + nodes[i].Title + " [" + pinOut.Text + "] " + pinOut.Location + " -> " + connectedNode.Title + " [" + pinIn.Text + "] " + pinIn.Location);
                        }
                    }
#if DEBUG
                    else
                    {
                        throw new Exception("Invalid flowgraph layout loaded!!");
                    }
#endif
                }
            }

            foreach (STNode node in stNodeEditor1.Nodes)
            {
                UpdatePinDelayTexts(node);
                node.EnsureProperNodeSizing();
            }
            stNodeEditor1.ResumeLayout();
            stNodeEditor1.Invalidate();

            //Correctly respect the scale/position of the saved flowgraph after layout is complete to ensure correct window dimensions
            this.BeginInvoke(new Action(() =>
            {
                stNodeEditor1.ScaleCanvas(flowgraphMeta.CanvasScale, 0, 0);
                stNodeEditor1.CenterCanvasOn(flowgraphMeta.CanvasPosition.X, flowgraphMeta.CanvasPosition.Y, false);
            }));

#if DEBUG
            Debug.Log("Flowgraph", "" + flowgraphMeta.Name + " loaded in " + timer.ElapsedMilliseconds + "ms with " + stNodeEditor1.Nodes.Count + " nodes on graph, of " + flowgraphMeta.Nodes.Count + " in layout (" + (flowgraphMeta.Nodes.Count - stNodeEditor1.Nodes.Count) + " missing)");
#endif
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            stNodeEditor1.LoadAssembly(Application.ExecutablePath);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (this.Visible)
            {
                Keys keyCode = keyData & Keys.KeyCode;

                if (keyCode == Keys.Delete)
                {
                    deleteToolStripMenuItem_Click(null, null);
                    deleteLinkToolStripMenuItem_Click(null, null);
                    return true;
                }
                else if (keyCode == Keys.F1)
                {
                    duplicateToolStripMenuItem_Click(null, null);
                    setDelayToolStripMenuItem_Click(null, null);
                    return true;
                }
                else if (keyCode == Keys.F2)
                {
                    findReferencesToolStripMenuItem_Click(null, null);
                    clearDelayToolStripMenuItem_Click(null, null);
                    return true;
                }
                else if (keyCode == Keys.F3)
                {
                    goToNextNodeInFlowgraphToolStripMenuItem_Click(null, null);
                    return true;
                }
                else if (keyCode == Keys.F4)
                {
                    addAllPinsToolStripMenuItem_Click(null, null);
                    return true;
                }
                else if (keyCode == Keys.F5)
                {
                    removeUnusedPinsToolStripMenuItem_Click(null, null);
                    return true;
                }
                else if (keyCode == Keys.F6)
                {
                    managePinsToolStripMenuItem_Click(null, null);
                    return true;
                }
            }
            
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private STNode EntityToNode(Entity entity)
        {
            if (entity == null)
                return null;

            STNode node = new STNode();
            node.Entity = entity;
            RegenerateNodeStyle(node);
            stNodeEditor1.Nodes.Add(node);
            node.SetPosition(new Point(0, _spawnOffset));
            _spawnOffset += node.Height + 10;

            return node;
        }

        //Regenerate the node's visual for the associated entity (sets name, colour, redraws)
        private void RegenerateNodeStyle(STNode node)
        {
            if (node == null)
                return;

            switch (node.Entity.variant)
            {
                case EntityVariant.PROXY:
                case EntityVariant.ALIAS:
                    (Composite comp, Entity ent) = _commands.Utils.GetResolvedTarget(_commands.Utils.ResolveAliasOrProxy(node.Entity, _composite));
                    node.SetColour(
                        node.Entity.variant == EntityVariant.PROXY ? Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_ProxyNode)) : 
                                                                     Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_AliasNode)), 
                        node.Entity.variant == EntityVariant.PROXY ? Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_ProxyNodeBottom)) : 
                                                                     Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_AliasNodeBottom)),
                        node.Entity.variant == EntityVariant.PROXY ? Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_ProxyText)) : 
                                                                     Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_AliasText))); 
                    switch (ent.variant)
                    {
                        case EntityVariant.FUNCTION:
                            FunctionEntity function = (FunctionEntity)ent;
                            string entName = _commands.Utils.GetEntityName(node.Entity.variant == EntityVariant.PROXY ? _composite : comp, node.Entity.variant == EntityVariant.PROXY ? node.Entity : ent); //proxies have custom names, aliases don't
                            if (function.function.IsFunctionType)
                            {
                                node.SetName(entName, node.Entity.variant + " TO: " + function.function.AsFunctionType.ToString());
                            }
                            else
                                node.SetName(entName, node.Entity.variant + " TO: " + Path.GetFileName(_commands.GetComposite(function.function).name));
                            break;
                        case EntityVariant.VARIABLE:
                            node.SetName(node.Entity.variant + " TO: " + ((VariableEntity)ent).name.ToString());
                            break;
                    }
                    break;
                case EntityVariant.FUNCTION:
                    FunctionEntity funcEnt = (FunctionEntity)node.Entity;
                    if (funcEnt.function.IsFunctionType)
                    {
                        node.SetColour(
                            Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_FunctionNode)), 
                            Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_FunctionNodeBottom)),
                            Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_FunctionText)));
                        node.SetName(_commands.Utils.GetEntityName(_composite, node.Entity), funcEnt.function.AsFunctionType.ToString());
                    }
                    else
                    {
                        node.SetColour(
                            Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_InstanceNode)),
                            Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_InstanceNodeBottom)),
                            Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_InstanceText)));
                        node.SetName(_commands.Utils.GetEntityName(_composite, node.Entity), Path.GetFileName(_commands.GetComposite(funcEnt.function).name));
                    }
                    break;
                case EntityVariant.VARIABLE:
                    VariableEntity varEnt = (VariableEntity)node.Entity;
                    node.SetColour(
                        Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_VariableNode)),
                        Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_VariableNode)),
                        Color.FromArgb(SettingsManager.GetInteger(Singleton.Settings.NodeColour_VariableText)));
                    node.SetName(varEnt.name.ToString());
                    AddAllPins(node);
                    break;
            }
            node.Recompute();
        }

        //Saves the Flowgraph's layout, and compiles the links back to commands
        //NOTE: This assumes that you have already cleared all childLinks in the composite already. That can be done by using CompositeUtils.ClearAllLinks
        public int SaveAndCompile()
        {
            FlowgraphMeta layout = FlowgraphLayoutManager.SaveLayout(stNodeEditor1, _composite, _flowgraphName);
            Debug.Log("Flowgraph", "Stored layout: " + layout.Name);

            //Re-generate connections using the content in the nodegraph
            int count = 0;
            foreach (STNode node in stNodeEditor1.Nodes)
            {
                List<STNodeOption> options = node.GetOutputOptions().ToList();
                options.AddRange(node.GetTopOptions());
                for (int y = 0; y < options.Count; y++)
                {
                    List<STNodeOption> connections = options[y].GetConnectedOption();
                    for (int z = 0; z < connections.Count; z++)
                    {
                        STNode connectedNode = connections[z].Owner;
                        node.Entity.AddParameterLink(options[y].ShortGUID, connectedNode.ShortGUID, connections[z].ShortGUID);
                        count++;
                    }
                }
            }
            Debug.Log("Flowgraph", "Layout " + layout.Name + " generated " + count + " connections");
            return count;
        }

        //disable entity-related actions on the context menu if no entity is selected
        private void ContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            STNode node = stNodeEditor1.GetHoveredNode();
            (STNodeOption linkIn, STNodeOption linkOut) = stNodeEditor1.GetHoveredLink();
            STNodeOption hoveredPin = stNodeEditor1.GetHoveredPin();

            if (hoveredPin?.Location == PinLocation.Top || hoveredPin?.Location == PinLocation.Bottom)
                hoveredPin = null; //Only allow right click on in/out pins

            deleteToolStripMenuItem.Visible = node != null && hoveredPin == null;
            duplicateToolStripMenuItem.Visible = node != null && hoveredPin == null;
            toolStripSeparator1.Visible = node != null && hoveredPin == null;
            addAllPinsToolStripMenuItem.Visible = node != null && hoveredPin == null;
            removeUnusedPinsToolStripMenuItem.Visible = node != null && hoveredPin == null;
            managePinsToolStripMenuItem.Visible = node != null && hoveredPin == null;
            toolStripSeparator4.Visible = node != null && hoveredPin == null;
            deleteEntityToolStripMenuItem.Visible = node != null && hoveredPin == null;
            duplicateEntityToolStripMenuItem.Visible = node != null && hoveredPin == null;
            toolStripSeparator5.Visible = node != null && hoveredPin == null;
            findReferencesToolStripMenuItem.Visible = node != null && hoveredPin == null;
            goToNextNodeInFlowgraphToolStripMenuItem.Visible = node != null && hoveredPin == null;

            if (node != null && hoveredPin == null)
                goToNextNodeInFlowgraphToolStripMenuItem.Enabled = HasMultipleNodesForEntity(node.Entity);

            addNodeToolStripMenuItem.Visible = node == null && linkIn == null && hoveredPin == null;
            createToolStripMenuItem.Visible = node == null && linkIn == null && hoveredPin == null;
            addNodeForSelectedEntityToolStripMenuItem.Visible = node == null && linkIn == null && hoveredPin == null;
            addNodeForSelectedEntityToolStripMenuItem.Enabled = Singleton.Editor?.CommandsDisplay?.CompositeDisplay?.EntityDisplay?.Entity != null;

            deleteLinkToolStripMenuItem.Visible = linkIn != null;

            setDelayToolStripMenuItem.Visible = hoveredPin != null;
            clearDelayToolStripMenuItem.Visible = hoveredPin != null;
            clearDelayToolStripMenuItem.Enabled = hoveredPin != null && (hoveredPin.LeftText != "" || hoveredPin.RightText != "");
        }

        //Add new nodes batch select
        Point _nodeSpawnPosition = new Point();
        private void addNodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SelectHierarchy selectEnt = new SelectHierarchy(_composite, new Popups.UserControls.CompositeEntityList.DisplayOptions()
            {
                DisplayAliases = true,
                DisplayFunctions = true,
                DisplayProxies = true,
                DisplayVariables = true,
                ShowCheckboxes = true,
            }, false);
            selectEnt.OnFinalEntitiesSelected += AddNodeCallbackEntitySelected;
            selectEnt.Show();
            _nodeSpawnPosition = new Point((int)stNodeEditor1.MousePositionInCanvas.X, (int)stNodeEditor1.MousePositionInCanvas.Y);
        }
        private void AddNodeCallbackEntitySelected(List<Entity> ent)
        {
            for (int i = 0; i < ent.Count; i++)
            {
                STNode node = EntityToNode(ent[i]);
                Point offsetSpawnPos = new Point(_nodeSpawnPosition.X + (i * 20), _nodeSpawnPosition.Y + (i * 20));
                node.SetPosition(offsetSpawnPos);
                if (SettingsManager.GetBool(Singleton.Settings.PopulateAllPinsOnCreateNode))
                    AddAllPins(node);
            }
        }

        //add new node for the selected entity, if one's selected
        private void addNodeForSelectedEntityToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Entity selectedEntity = Singleton.Editor?.CommandsDisplay?.CompositeDisplay?.EntityDisplay?.Entity;
            if (selectedEntity == null) return;
            STNode node = AddNodeForEntity(selectedEntity);
            node.SetPosition(new Point((int)stNodeEditor1.MousePositionInCanvas.X, (int)stNodeEditor1.MousePositionInCanvas.Y));
            SelectNode(node);
        }

        //delete the whole entity and associated nodes
        private void deleteEntityToolStripMenuItem_Click(object sender, EventArgs e)
        {
            STNode node = stNodeEditor1.GetHoveredNode();
            if (node == null) return;
            Entity entity = _composite.GetEntityByID(node.ShortGUID);
            if (entity == null) return;
            Singleton.Editor.CommandsDisplay.CompositeDisplay.DeleteEntity(entity);
        }

        //Add/remove batch pins in/out
        private void addAllPinsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            STNode node = stNodeEditor1.GetHoveredNode();
            if (node == null) return;
            
            Point currentCenter = node.Location;
            currentCenter.X += node.Width / 2;
            currentCenter.Y += node.Height / 2;

            AddAllPins(node);

            Point newCenter = node.Location;
            newCenter.X += node.Width / 2;
            newCenter.Y += node.Height / 2;

            node.SetPosition(new Point(node.Location.X + (currentCenter.X - newCenter.X), node.Location.Y + (currentCenter.Y - newCenter.Y)));
        }

        //add all possible pins to a given node
        private void AddAllPins(STNode node)
        {
            node.AddAllPins(_composite, _commands);
            UpdatePinDelayTexts(node);
        }

        //set all delay texts on a node
        private void UpdatePinDelayTexts(STNode node)
        {
            foreach (STNodeOption inputPin in node.GetInputOptions())
            {
                float delay = GetDelayForParameter(node.Entity, inputPin.Text);
                inputPin.LeftText = delay == 0.0f ? "" : delay.ToString();
            }
            foreach (STNodeOption outputPin in node.GetOutputOptions())
            {
                float delay = GetDelayForParameter(node.Entity, outputPin.Text);
                outputPin.RightText = delay == 0.0f ? "" : delay.ToString();
            }
        }

        private void removeUnusedPinsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            STNode node = GetContextNode();
            if (node == null) return;

            Point newPos = node.Location;
            newPos.X += node.Width / 2;
            newPos.Y += node.Height / 2;
            node.RemoveUnusedPins();
            node.SetPosition(newPos);
        }

        private void managePinsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            STNode node = GetContextNode();
            if (node == null)
                return;

            OpenManagePinsDialog(node);
        }

        private STNode GetContextNode()
        {
            STNode node = stNodeEditor1.GetHoveredNode();
            if (node != null)
                return node;

            STNode[] selected = stNodeEditor1.GetSelectedNode();
            if (selected != null && selected.Length == 1)
                return selected[0];

            return null;
        }

        private void OpenManagePinsDialog(STNode node)
        {
            if (_managePinsDialog != null)
                _managePinsDialog.Close();

            Point currentCenter = node.Location;
            currentCenter.X += node.Width / 2;
            currentCenter.Y += node.Height / 2;

            _managePinsDialog = new Popups.ManageEntityPins();
            _managePinsDialog.PinsSaved += savedNode =>
            {
                UpdatePinDelayTexts(savedNode);

                Point newCenter = savedNode.Location;
                newCenter.X += savedNode.Width / 2;
                newCenter.Y += savedNode.Height / 2;
                savedNode.SetPosition(new Point(
                    savedNode.Location.X + (currentCenter.X - newCenter.X),
                    savedNode.Location.Y + (currentCenter.Y - newCenter.Y)));
            };
            _managePinsDialog.PopulateOptions(node, _composite, _commands);
            _managePinsDialog.Show();
        }

        private void deleteLinkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            stNodeEditor1.RemoveHoveredLink();
        }

        //Delete right clicked node
        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            STNode node = stNodeEditor1.GetHoveredNode();
            if (node == null) return;

            if (SettingsManager.GetBool(Singleton.Settings.AskBeforeDeletingNode))
            {
                if (MessageBox.Show("Are you sure you want to remove this node?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) 
                    return;
            }

            Entity ent = node.Entity;
            stNodeEditor1.Nodes.Remove(node);

            if (SettingsManager.GetBool(Singleton.Settings.OptionToDeleteEntityWithNode))
            {
                if (Singleton.Editor.CommandsDisplay.CompositeDisplay != null && !Singleton.Editor.CommandsDisplay.CompositeDisplay.AnyFlowgraphsContainEntity(ent))
                {
                    if (MessageBox.Show("All nodes have been removed for this entity, would you like to delete the entity too?", "No nodes for entity", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        Singleton.Editor.CommandsDisplay.CompositeDisplay.DeleteEntity(ent, false);
                    }
                }
            }
        }

        //Duplicate the right clicked node
        private void duplicateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            STNode node = stNodeEditor1.GetHoveredNode();
            if (node == null) return;
            DuplicateNode(node);
        }

        private void DuplicateNode(STNode node)
        {
            STNode duplicated = EntityToNode(node.Entity);
            SetSameOptions(node, duplicated);
            duplicated.SetPosition(new Point(node.Location.X + 15, node.Location.Y + 15));
            SelectNode(duplicated);
        }

        private void SetSameOptions(STNode toCopyFrom, STNode toApplyTo, bool alsoKeepConnections = true)
        {
            {
                STNodeOption[] ins = toApplyTo.GetInputOptions();
                for (int i = 0; i < ins.Length; i++)
                    toApplyTo.RemoveInputOption(ins[i].ShortGUID);
                STNodeOption[] outs = toApplyTo.GetOutputOptions();
                for (int i = 0; i < outs.Length; i++)
                    toApplyTo.RemoveOutputOption(outs[i].ShortGUID);
                STNodeOption[] ups = toApplyTo.GetTopOptions();
                for (int i = 0; i < ups.Length; i++)
                    toApplyTo.RemoveTopOption(ups[i].ShortGUID);
                STNodeOption[] downs = toApplyTo.GetBottomOptions();
                for (int i = 0; i < downs.Length; i++)
                    toApplyTo.RemoveBottomOption(downs[i].ShortGUID);
            }
            {
                STNodeOption[] ins = toCopyFrom.GetInputOptions();
                for (int i = 0; i < ins.Length; i++)
                {
                    STNodeOption newOpt = toApplyTo.AddInputOption(ins[i].ShortGUID);
                    if (alsoKeepConnections)
                    {
                        List<STNodeOption> connections = ins[i].GetConnectedOption();
                        for (int x = 0; x < connections.Count; x++)
                            newOpt.ConnectOption(connections[x]);
                    }
                }
                STNodeOption[] outs = toCopyFrom.GetOutputOptions();
                for (int i = 0; i < outs.Length; i++)
                {
                    STNodeOption newOpt = toApplyTo.AddOutputOption(outs[i].ShortGUID);
                    if (alsoKeepConnections)
                    {
                        List<STNodeOption> connections = outs[i].GetConnectedOption();
                        for (int x = 0; x < connections.Count; x++)
                            newOpt.ConnectOption(connections[x]);
                    }
                }
                STNodeOption[] ups = toCopyFrom.GetTopOptions();
                for (int i = 0; i < ups.Length; i++)
                {
                    STNodeOption newOpt = toApplyTo.AddTopOption(ups[i].ShortGUID, ups[i].Style);
                    if (alsoKeepConnections)
                    {
                        List<STNodeOption> connections = ups[i].GetConnectedOption();
                        for (int x = 0; x < connections.Count; x++)
                            newOpt.ConnectOption(connections[x]);
                    }
                }
                STNodeOption[] downs = toCopyFrom.GetBottomOptions();
                for (int i = 0; i < downs.Length; i++)
                {
                    STNodeOption newOpt = toApplyTo.AddBottomOption(downs[i].ShortGUID);
                    if (alsoKeepConnections)
                    {
                        List<STNodeOption> connections = downs[i].GetConnectedOption();
                        for (int x = 0; x < connections.Count; x++)
                            newOpt.ConnectOption(connections[x]);
                    }
                }
            }

            UpdatePinDelayTexts(toApplyTo);
        }

        private void duplicateEntityToolStripMenuItem_Click(object sender, EventArgs e)
        {
            STNode node = stNodeEditor1.GetHoveredNode();
            Entity ent = node?.Entity;
            if (ent == null) return;

            Entity newEnt = Singleton.Editor.CommandsDisplay.CompositeDisplay.AddCopyOfEntity(ent);
            STNode newNode = AddNodeForEntity(newEnt);
            SetSameOptions(node, newNode);
            newNode.SetPosition(new Point((int)stNodeEditor1.MousePositionInCanvas.X, (int)stNodeEditor1.MousePositionInCanvas.Y));
            SelectNode(newNode);

            //note to self: this is wrong. we need to maske sure we duplicate all the nodes and connections across all flowgraphs
        }

        private void TabStripContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            deleteFGToolstripMenuItem.Text = "Delete flowgraph '" + _flowgraphName + "'";
            renameFGToolStripMenuItem.Text = "Rename flowgraph '" + _flowgraphName + "'";
        }

        private void deleteFGToolstripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to delete the flowgraph '" + _flowgraphName + "'?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            FlowgraphLayoutManager.RemoveLayout(_composite, _flowgraphName);
            this.Close();
        }
        RenameGeneric _renameFlowgraphPopup;
        private void renameFGToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_renameFlowgraphPopup != null)
            {
                _renameFlowgraphPopup.OnRenamed -= OnRenameFlowgraph;
                _renameFlowgraphPopup.FormClosed -= _renameFlowgraphPopup_FormClosed;
                _renameFlowgraphPopup.Close();
            }

            _renameFlowgraphPopup = new RenameGeneric(_flowgraphName, new RenameGeneric.RenameGenericContent()
            {
                Title = "Rename flowgraph for " + _composite.name,
                Description = "New Flowgraph Name",
                ButtonText = "Rename Flowgraph"
            });
            _renameFlowgraphPopup.Show();
            _renameFlowgraphPopup.OnRenamed += OnRenameFlowgraph;
            _renameFlowgraphPopup.FormClosed += _renameFlowgraphPopup_FormClosed;
        }
        private void OnRenameFlowgraph(string name)
        {
            List<FlowgraphMeta> layouts = FlowgraphLayoutManager.GetLayouts(_composite);
            for (int i = 0; i < layouts.Count; i++)
            {
                if (layouts[i].Name == name)
                {
                    MessageBox.Show("There's already a flowgraph named '" + name + "' in this Composite! Please pick a unique name.", "Name taken!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            layouts.FirstOrDefault(o => o.Name == _flowgraphName).Name = name;
            this.Text = name;
            _flowgraphName = name;
        }
        private void _renameFlowgraphPopup_FormClosed(object sender, FormClosedEventArgs e)
        {
            _renameFlowgraphPopup = null;
        }
        private void createNewFlowgraphToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Singleton.Editor.CommandsDisplay.CompositeDisplay.CreateFlowgraph();
        }

        //Welcome to the world of hacks
        PointF _createEntViaPopupPos = new PointF();
        BaseWindow _prevEntCreatePopup = null;
        private void createParameterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ListenForEntCreatePopup(Singleton.Editor.CommandsDisplay.CompositeDisplay.CreateEntity(EntityVariant.VARIABLE));
        }
        private void createFunctionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ListenForEntCreatePopup(Singleton.Editor.CommandsDisplay.CompositeDisplay.CreateEntity(EntityVariant.FUNCTION));
        }
        private void createInstanceOfCompositeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ListenForEntCreatePopup(Singleton.Editor.CommandsDisplay.CompositeDisplay.CreateEntity(EntityVariant.FUNCTION, true));
        }
        private void createProxyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ListenForEntCreatePopup(Singleton.Editor.CommandsDisplay.CompositeDisplay.CreateEntity(EntityVariant.PROXY));
        }
        private void createAliasToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ListenForEntCreatePopup(Singleton.Editor.CommandsDisplay.CompositeDisplay.CreateEntity(EntityVariant.ALIAS));
        }
        private void ListenForEntCreatePopup(BaseWindow window)
        {
            if (_prevEntCreatePopup != null)
                _prevEntCreatePopup.Close();

            _prevEntCreatePopup = window;
            _prevEntCreatePopup.FormClosed += EntityCreationPopupClosed;
            _createEntViaPopupPos = stNodeEditor1.MousePositionInCanvas;
            Singleton.OnEntityAdded += OnEntityAddedViaPopup;
        }
        private void OnEntityAddedViaPopup(Entity entity)
        {
            EntityCreationPopupClosed(null, null);
            STNode node = AddNodeForEntity(entity);
            node.SetPosition(new Point((int)_createEntViaPopupPos.X, (int)_createEntViaPopupPos.Y));
            SelectNode(node);
        }
        private void EntityCreationPopupClosed(object sender, FormClosedEventArgs e)
        {
            Singleton.OnEntityAdded -= OnEntityAddedViaPopup;
            _prevEntCreatePopup.FormClosed -= EntityCreationPopupClosed;
        }

        ShowCrossRefs _crossRefsDialog = null;
        private void findReferencesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_crossRefsDialog != null)
            {
                _crossRefsDialog.OnEntitySelected -= Singleton.Editor.CommandsDisplay.LoadCompositeAndEntity;
                _crossRefsDialog.OnFlowgraphSelected -= Singleton.Editor.CommandsDisplay.CompositeDisplay.SelectEntityOnFlowgraph;
                _crossRefsDialog.Close();
            }

            STNode node = stNodeEditor1.GetHoveredNode();
            if (node == null || node.Entity == null)
                return;

            _crossRefsDialog = new ShowCrossRefs(node.Entity);
            _crossRefsDialog.Show();
            _crossRefsDialog.OnEntitySelected += Singleton.Editor.CommandsDisplay.LoadCompositeAndEntity;
            _crossRefsDialog.OnFlowgraphSelected += Singleton.Editor.CommandsDisplay.CompositeDisplay.SelectEntityOnFlowgraph;
        }
        private void goToNextNodeInFlowgraphToolStripMenuItem_Click(object sender, EventArgs e)
        {
            STNode startNode = stNodeEditor1.GetHoveredNode();
            if (startNode == null || startNode.Entity == null)
                return;

            bool startListening = false;
            foreach (STNode node in stNodeEditor1.Nodes)
            {
                if (node == startNode)
                {
                    startListening = true;
                    continue;
                }

                if (!startListening)
                    continue;

                if (node.Entity == startNode.Entity)
                {
                    SelectNode(node);
                    return;
                }
            }
            foreach (STNode node in stNodeEditor1.Nodes)
            {
                if (node.Entity == startNode.Entity)
                {
                    SelectNode(node);
                    return;
                }

                if (node == startNode)
                    break;
            }
        }

        SetPinDelay _pinDelayDialog = null;
        private void setDelayToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_pinDelayDialog != null)
            {
                _pinDelayDialog.OnDelaySet -= OnPinDelaySet;
                _pinDelayDialog.Close();
            }

            STNodeOption pin = stNodeEditor1.GetHoveredPin();
            if (pin == null || pin?.Owner?.Entity == null)
                return;

            _pinDelayDialog = new SetPinDelay(pin.Owner.Entity, pin.Text, GetDelayForParameter(pin.Owner.Entity, pin.Text), pin.Location);
            _pinDelayDialog.Show();
            _pinDelayDialog.OnDelaySet += OnPinDelaySet;
        }
        private void OnPinDelaySet(Entity entity, string parameter, float delay, PinLocation location)
        {
            entity.RemoveParameter(parameter);
            entity.AddParameter(parameter, new cFloat(delay), location == PinLocation.Left ? ParameterVariant.METHOD_PIN : ParameterVariant.TARGET_PIN);

            foreach (STNode node in stNodeEditor1.Nodes)
            {
                if (node.Entity != entity)
                    continue;
                UpdatePinDelayTexts(node);
            }
        }

        private float GetDelayForParameter(Entity entity, string parameter)
        {
            float delay = 0.0f;
            Parameter delayParam = entity.GetParameter(parameter);
            if (delayParam != null && delayParam.content != null)
            {
                switch (delayParam.content.dataType)
                {
                    case DataType.FLOAT:
                        delay = ((cFloat)delayParam.content).value;
                        break;
                    case DataType.INTEGER:
                        delay = ((cInteger)delayParam.content).value;
                        break;
                }
            }
            return delay;
        }

        private void clearDelayToolStripMenuItem_Click(object sender, EventArgs e)
        {
            STNodeOption pin = stNodeEditor1.GetHoveredPin();
            if (pin == null || pin?.Owner?.Entity == null)
                return;

            pin.Owner.Entity.RemoveParameter(pin.Text);
            UpdatePinDelayTexts(pin.Owner);
        }
    }
}
