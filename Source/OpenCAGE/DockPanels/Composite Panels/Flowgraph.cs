using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.DockPanels;
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
            Theming.ThemeManager.ApplyToForm(this);
            this.VisibleChanged += Flowgraph_VisibleChanged;
            this.FormClosed += Flowgraph_FormClosed;

            stNodeEditor1.LoadAssembly(Application.ExecutablePath);
            stNodeEditor1.AllowSameOwnerConnections = true;
            stNodeEditor1.SelectedChanged += Owner_SelectedChanged;
            stNodeEditor1.MultiSelectionChanged += Owner_SelectedChanged; //rubber-band/ctrl-click selection raises this, not SelectedChanged
            stNodeEditor1.OptionConnected += StNodeEditor1_OptionConnectionChanged;
            stNodeEditor1.OptionDisconnected += StNodeEditor1_OptionConnectionChanged;
            stNodeEditor1.PinToNodeConnected += StNodeEditor1_PinToNodeConnected;
            stNodeEditor1.NodeCtrlMiddleMouseDown += StNodeEditor1_NodeCtrlMiddleMouseDown;
            stNodeEditor1.NodesMoved += StNodeEditor1_NodesMoved;
            // STNodeEditor rejects non-STNodeType drags in its own OnDragEnter; handle drops on this form instead.
            stNodeEditor1.AllowDrop = false;

            AllowDrop = true;
            DragEnter += Flowgraph_DragEnter;
            DragOver += Flowgraph_DragOver;
            DragDrop += Flowgraph_DragDrop;

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
            stNodeEditor1.MultiSelectionChanged -= Owner_SelectedChanged;
            stNodeEditor1.OptionConnected -= StNodeEditor1_OptionConnectionChanged;
            stNodeEditor1.OptionDisconnected -= StNodeEditor1_OptionConnectionChanged;
            stNodeEditor1.NodeCtrlMiddleMouseDown -= StNodeEditor1_NodeCtrlMiddleMouseDown;
            stNodeEditor1.NodesMoved -= StNodeEditor1_NodesMoved;
            DragEnter -= Flowgraph_DragEnter;
            DragOver -= Flowgraph_DragOver;
            DragDrop -= Flowgraph_DragDrop;
            Singleton.OnEntitySelected -= OnEntitySelectedGlobally;
            Singleton.OnEntityDeleted -= OnEntityDeletedGlobally;
            Singleton.OnEntityRenamed -= OnEntityRenamedGlobally;
            Singleton.OnEntityAdded -= OnEntityAddedViaPopup;
            Singleton.OnNodeStyleChanged -= OnNodeStyleChanged;

            if (_renameFlowgraphPopup != null)
                _renameFlowgraphPopup.FormClosed -= _renameFlowgraphPopup_FormClosed;
        }

        private void OnEntitySelectedGlobally(Entity entity)
        {
            if (entity == null)
                return;

            //Skip only when this entity's nodes are already exactly the selection. Comparing against a
            //remembered entity instead meant any stale value suppressed the highlight - clicking empty
            //canvas drops the selection without clearing it, so re-picking the same entity in the list
            //or the viewport did nothing at all.
            if (IsSelectionExactlyEntity(entity))
                return;

            SelectAllNodesForEntity(entity, centerCanvas: true);
        }

        /// <summary>True when every node for this entity is selected, and nothing else is.</summary>
        private bool IsSelectionExactlyEntity(Entity entity)
        {
            STNode[] selected = stNodeEditor1.GetSelectedNode();
            if (selected.Length == 0)
                return false;

            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i].ShortGUID != entity.shortGUID)
                    return false;
            }

            int total = 0;
            foreach (STNode node in stNodeEditor1.Nodes)
            {
                if (node.ShortGUID == entity.shortGUID)
                    total++;
            }

            return total != 0 && total == selected.Length;
        }

        private void OnEntityRenamedGlobally(Entity entity, string newNew)
        {
            foreach (STNode node in stNodeEditor1.Nodes)
            {
                if (node.Entity == null)
                    continue;

                //The renamed entity itself, plus any alias/proxy node that shows its name as a fallback
                bool affected = node.Entity.shortGUID == entity.shortGUID;
                if (!affected)
                {
                    switch (node.Entity.variant)
                    {
                        case EntityVariant.ALIAS:
                            affected = ((AliasEntity)node.Entity).alias.path.Contains(entity.shortGUID);
                            break;
                        case EntityVariant.PROXY:
                            affected = ((ProxyEntity)node.Entity).proxy.path.Contains(entity.shortGUID);
                            break;
                    }
                }

                if (affected)
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
        private readonly List<uint> _lastSelectionIds = new List<uint>();
        private void Owner_SelectedChanged(object sender, EventArgs e)
        {
            //Mid-rebuild states aren't real selections - see SelectAllNodesForEntity
            if (_applyingEntitySelection)
                return;

            STNode[] nodes = stNodeEditor1.GetSelectedNode();

            //Multiple nodes for the same entity still count as a single selection
            List<Entity> selectedEntities = new List<Entity>();
            foreach (STNode node in nodes)
            {
                Entity nodeEntity = _composite.GetEntityByID(node.ShortGUID);
                if (nodeEntity == null) continue;
                if (!selectedEntities.Contains(nodeEntity))
                    selectedEntities.Add(nodeEntity);
            }

            if (selectedEntities.Count == 0)
            {
                _lastSelectionIds.Clear();

                //Deselecting from a multi-selection should clear the inspector's multi state
                //(deselecting a single entity keeps it shown, matching previous behaviour)
                if (Singleton.Editor?.CompositeDisplay?.EntityDisplay?.IsMultiEditing == true)
                {
                    _previouslySelectedEntity = null;
                    _selectedNodeChanged = true;
                    Singleton.Editor?.CompositeDisplay?.ClearEntitySelection();
                    _selectedNodeChanged = false;
                }
                return;
            }

            //Rubber-band drags and ctrl-clicks fire selection events repeatedly - only act when the set changes
            bool sameSelection = selectedEntities.Count == _lastSelectionIds.Count;
            if (sameSelection)
            {
                foreach (Entity entity in selectedEntities)
                {
                    if (!_lastSelectionIds.Contains(entity.shortGUID.AsUInt32))
                    {
                        sameSelection = false;
                        break;
                    }
                }
            }
            if (sameSelection)
                return;
            _lastSelectionIds.Clear();
            foreach (Entity entity in selectedEntities)
                _lastSelectionIds.Add(entity.shortGUID.AsUInt32);

            if (selectedEntities.Count > 1)
            {
                _previouslySelectedEntity = null;
                _selectedNodeChanged = true;
                Singleton.Editor?.CompositeDisplay?.LoadEntities(selectedEntities);
                _selectedNodeChanged = false;
                return;
            }

            Entity ent = selectedEntities[0];
            if (ent == _previouslySelectedEntity) return;
            _previouslySelectedEntity = ent;

            _selectedNodeChanged = true;
            Singleton.Editor?.CompositeDisplay?.LoadEntity(ent, false);
            Singleton.OnEntitySelected?.Invoke(ent); //need to call this again b/c the activation event doesn't fire here
            _selectedNodeChanged = false;
        }

        private void StNodeEditor1_NodeCtrlMiddleMouseDown(object sender, STNodeEditorEventArgs e)
        {
            if (e?.Node?.Entity == null)
                return;

            Entity entity = e.Node.Entity;
            BeginInvoke(new Action(() => Singleton.Editor?.CompositeDisplay?.StepIntoEntity(entity)));
        }

        public void SelectAllNodesForEntity(Entity entity, bool centerCanvas = true)
        {
            if (_selectedNodeChanged) //TEMPORARY HACK FIX FOR DE-SELECTION RACE CONDITION BUG
                return;

            //Clearing and re-selecting raises a SelectedChanged per node, so the handler would see the
            //empty gap in the middle and push that back out as a real deselection - which clears the
            //inspector and, when multi-editing, resets the very state we're rebuilding. The whole
            //rebuild is one selection change as far as the rest of the UI is concerned.
            _applyingEntitySelection = true;
            try
            {
                DeselectAllNodes();

                if (entity == null)
                    return;

                STNode firstMatch = null;
                STNode[] nodes = stNodeEditor1.Nodes.ToArray();
                foreach (STNode node in nodes)
                {
                    if (node.ShortGUID != entity.shortGUID)
                        continue;
                    if (firstMatch == null)
                        firstMatch = node;
                    SelectNode(node, centerCanvas: false);
                }

                if (centerCanvas && firstMatch != null)
                    FocusCanvasOnNodes(stNodeEditor1.GetSelectedNode());
            }
            finally
            {
                _applyingEntitySelection = false;
            }

            //Owner_SelectedChanged was suppressed above, so bring its dedupe state up to date by hand -
            //leaving it describing the previous selection would make the next real click on one of
            //those nodes look like "no change" and skip updating the inspector.
            _lastSelectionIds.Clear();
            if (entity != null)
                _lastSelectionIds.Add(entity.shortGUID.AsUInt32);

            //The canvas needs repainting for the new highlight (and its off-screen arrows)
            stNodeEditor1.Invalidate();
        }

        //Set while rebuilding the selection to match an entity picked somewhere else in the editor
        private bool _applyingEntitySelection = false;

        //Building a page connects pins and can add pins, which looks identical to the user doing it. Suppress
        //dirty marking while populating, otherwise merely opening a composite would report unsaved changes.
        private int _populatingDepth = 0;
        private bool IsPopulating => _populatingDepth > 0;
        private void MarkFlowgraphEdit()
        {
            if (IsPopulating)
                return;
            DirtyTracker.MarkLevelDataModified();
        }

        /* Node positions are stored in the composite's saved flowgraph layout, so moving one is a change */
        private void StNodeEditor1_NodesMoved(object sender, STNodesMovedEventArgs e)
        {
            MarkFlowgraphEdit();
        }

        /* Keep the inspector's "fed by flowgraph" parameter highlights live as connections change */
        private void StNodeEditor1_OptionConnectionChanged(object sender, STNodeEditorOptionEventArgs e)
        {
            //Links are compiled back into the Commands data on save, so this is an unsaved change
            MarkFlowgraphEdit();

            Singleton.Editor?.CompositeDisplay?.EntityDisplay?.RefreshParameterHighlights();
        }

        /// <summary>Collect the pin IDs of this entity's input pins that have live UI connections.</summary>
        public void CollectConnectedInputPins(Entity entity, HashSet<ShortGuid> results)
        {
            if (entity == null || stNodeEditor1 == null)
                return;
            foreach (STNode node in stNodeEditor1.Nodes)
            {
                if (node?.Entity == null || node.Entity.shortGUID != entity.shortGUID)
                    continue;
                foreach (ShortGuid pinId in node.GetConnectedInputOptionIds())
                    results.Add(pinId);
            }
        }

        /// <summary>True if any graph node for this entity has live UI connections on the given pin.</summary>
        public bool HasPinConnections(Entity entity, ShortGuid pinId)
        {
            if (entity == null || stNodeEditor1 == null) return false;
            foreach (STNode node in stNodeEditor1.Nodes)
            {
                if (node?.Entity == null || node.Entity.shortGUID != entity.shortGUID)
                    continue;
                STNodeOption pin = node.GetOption(pinId);
                if (pin != null && pin.ConnectionCount > 0)
                    return true;
            }
            return false;
        }

        private void SelectNode(STNode node, bool centerCanvas = true)
        {
            _previouslySelectedEntity = node.Entity;
            Debug.Log("Flowgraph", "Select node: " + node.Title + " - " + node.Guid);

            stNodeEditor1.AddSelectedNode(node);
            node.SetSelected(true, true);
            stNodeEditor1.SetActiveNode(node);

            if (centerCanvas)
                stNodeEditor1.CenterCanvasOn(node.Location.X + (node.Width / 2), node.Location.Y + (node.Height / 2), true);
        }

        //A node the user just created is placed where they asked for it, so moving the canvas onto it
        //is opt-in (Options > Entity Display > Focus Canvas On Newly Created Node)
        private void SelectNewNode(STNode node)
        {
            SelectNode(node, centerCanvas: SettingsManager.GetBool(Settings.FocusCanvasOnNewNode));
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

        private void FocusOnSelectedEntity()
        {
            STNode[] selected = stNodeEditor1.GetSelectedNode();
            if (selected != null && selected.Length > 0)
            {
                FocusCanvasOnNodes(selected);
                return;
            }

            Entity entity = Singleton.Editor?.CompositeDisplay?.EntityListPanel?.List?.SelectedEntity;
            if (entity == null)
                entity = Singleton.Editor?.CompositeDisplay?.EntityDisplay?.Entity;
            if (entity == null)
                entity = _previouslySelectedEntity;

            if (entity != null)
                SelectAllNodesForEntity(entity, centerCanvas: true);
        }

        private void FocusCanvasOnNodes(STNode[] nodes)
        {
            if (nodes == null || nodes.Length == 0)
                return;

            if (nodes.Length == 1)
            {
                STNode node = nodes[0];
                stNodeEditor1.CenterCanvasOn(node.Location.X + (node.Width / 2), node.Location.Y + (node.Height / 2), true);
                return;
            }

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (STNode node in nodes)
            {
                minX = Math.Min(minX, node.Location.X);
                minY = Math.Min(minY, node.Location.Y);
                maxX = Math.Max(maxX, node.Location.X + node.Width);
                maxY = Math.Max(maxY, node.Location.Y + node.Height);
            }
            stNodeEditor1.CenterCanvasOn((minX + maxX) / 2, (minY + maxY) / 2, true);
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
            if (SettingsManager.GetBool(Settings.PopulateAllPinsOnCreateNode))
                AddAllPins(node);
            return node;
        }

        public STNode PlaceEntityAt(Entity entity, PointF canvasPosition)
        {
            STNode node = AddNodeForEntity(entity);
            node.SetPosition(new Point((int)canvasPosition.X, (int)canvasPosition.Y));
            SelectNewNode(node);
            RefreshNodeMarkers();
            return node;
        }

        //Recalculate the multi-node/proxy markers across all pages after nodes are added/removed
        private void RefreshNodeMarkers()
        {
            Singleton.Editor?.CompositeDisplay?.RefreshNodeMarkers();
        }

        private void Flowgraph_DragEnter(object sender, DragEventArgs e)
        {
            if (CanAcceptFlowgraphDrag(e))
                e.Effect = DragDropEffects.Copy;
        }

        private void Flowgraph_DragOver(object sender, DragEventArgs e)
        {
            if (CanAcceptFlowgraphDrag(e))
                e.Effect = DragDropEffects.Copy;
        }

        private void Flowgraph_DragDrop(object sender, DragEventArgs e)
        {
            Point editorPoint = stNodeEditor1.PointToClient(new Point(e.X, e.Y));
            PointF canvasPosition = stNodeEditor1.ControlToCanvas(editorPoint);
            CompositeDisplay compositeDisplay = Singleton.Editor?.CompositeDisplay;

            if (TryGetCompositeName(e, out string compositeName))
            {
                Entity newEntity = compositeDisplay?.CreateCompositeInstanceEntity(compositeName, null);
                if (newEntity != null)
                    PlaceEntityAt(newEntity, canvasPosition);
                return;
            }

            if (TryGetEntityListEntity(e, out Entity existingEntity))
            {
                PlaceEntityAt(existingEntity, canvasPosition);
                return;
            }

            if (TryGetCompositePinType(e, out CompositePinType pinType))
            {
                Entity variableEntity = compositeDisplay?.CreateVariableEntity(pinType, null);
                if (variableEntity != null)
                    PlaceEntityAt(variableEntity, canvasPosition);
                return;
            }

            if (!TryGetFunctionType(e, out FunctionType functionType))
                return;

            Entity functionEntity = compositeDisplay?.CreateFunctionEntity(functionType, null);
            if (functionEntity != null)
                PlaceEntityAt(functionEntity, canvasPosition);
        }

        private static bool CanAcceptFlowgraphDrag(DragEventArgs e)
        {
            return TryGetCompositeName(e, out _)
                || TryGetEntityListEntity(e, out _)
                || TryGetCompositePinType(e, out _)
                || TryGetFunctionType(e, out _);
        }

        private static bool TryGetEntityListEntity(DragEventArgs e, out Entity entity)
        {
            entity = null;
            if (!e.Data.GetDataPresent(EntityList.EntityDragFormat))
                return false;

            object data = e.Data.GetData(EntityList.EntityDragFormat);
            if (data == null)
                return false;

            uint entityId = Convert.ToUInt32(data);
            Composite composite = Singleton.Editor?.CompositeDisplay?.Composite;
            if (composite == null)
                return false;

            entity = composite.GetEntityByID(new ShortGuid(entityId));
            return entity != null;
        }

        private static bool TryGetCompositeName(DragEventArgs e, out string compositeName)
        {
            compositeName = null;
            if (!e.Data.GetDataPresent(CompositeBrowser.CompositeDragFormat))
                return false;

            compositeName = e.Data.GetData(CompositeBrowser.CompositeDragFormat) as string;
            return !string.IsNullOrEmpty(compositeName);
        }

        private static bool TryGetCompositePinType(DragEventArgs e, out CompositePinType pinType)
        {
            pinType = default;
            if (!e.Data.GetDataPresent(EntityBrowser.CompositePinTypeDragFormat))
                return false;

            string pinTypeName = e.Data.GetData(EntityBrowser.CompositePinTypeDragFormat) as string;
            return !string.IsNullOrEmpty(pinTypeName) && Enum.TryParse(pinTypeName, out pinType);
        }

        private static bool TryGetFunctionType(DragEventArgs e, out FunctionType functionType)
        {
            functionType = default;
            string functionTypeName = null;
            if (e.Data.GetDataPresent(EntityBrowser.FunctionTypeDragFormat))
                functionTypeName = e.Data.GetData(EntityBrowser.FunctionTypeDragFormat) as string;
            else if (e.Data.GetDataPresent(EntityBrowser.CompositePinTypeDragFormat))
                return false;
            else if (e.Data.GetDataPresent(DataFormats.UnicodeText))
                functionTypeName = e.Data.GetData(DataFormats.UnicodeText) as string;
            else if (e.Data.GetDataPresent(DataFormats.Text))
                functionTypeName = e.Data.GetData(DataFormats.Text) as string;
            else if (e.Data.GetDataPresent(typeof(string)))
                functionTypeName = e.Data.GetData(typeof(string)) as string;

            return !string.IsNullOrEmpty(functionTypeName) && Enum.TryParse(functionTypeName, out functionType);
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

            if (nodes.Count != 0)
                RefreshNodeMarkers();
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

            _populatingDepth++; //rebuilding the page isn't a user edit - see MarkFlowgraphEdit
            try
            {

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
                node.AlignRelayRows(composite, _commands); //catches the fallback pins added above
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
            finally
            {
                _populatingDepth--;
            }
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
                else if (keyCode == Keys.C && (keyData & Keys.Modifiers) == Keys.Control)
                {
                    //The selection wins for the shortcut (the cursor could be anywhere) - the node
                    //under the cursor is only used when nothing is selected
                    CopyNodes(stNodeEditor1.GetSelectedNode().Length == 0 ? stNodeEditor1.GetHoveredNode() : null);
                    return true;
                }
                else if (keyCode == Keys.V && (keyData & Keys.Modifiers) == Keys.Control)
                {
                    PasteClipboardClones(stNodeEditor1.MousePositionInCanvas);
                    return true;
                }
                else if (keyCode == Keys.F1)
                {
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
                else if (keyCode == Keys.Z && (keyData & Keys.Modifiers) == Keys.None)
                {
                    FocusOnSelectedEntity();
                    return true;
                }
                else if ((keyCode == Keys.Subtract || keyCode == Keys.OemMinus) && (keyData & Keys.Modifiers) == Keys.None)
                {
                    Singleton.Editor?.CompositeDisplay?.LoadParent();
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
                        node.Entity.variant == EntityVariant.PROXY ? Color.FromArgb(SettingsManager.GetInteger(Settings.NodeColour_ProxyNode)) : 
                                                                     Color.FromArgb(SettingsManager.GetInteger(Settings.NodeColour_AliasNode)), 
                        node.Entity.variant == EntityVariant.PROXY ? Color.FromArgb(SettingsManager.GetInteger(Settings.NodeColour_ProxyNodeBottom)) : 
                                                                     Color.FromArgb(SettingsManager.GetInteger(Settings.NodeColour_AliasNodeBottom)),
                        node.Entity.variant == EntityVariant.PROXY ? Color.FromArgb(SettingsManager.GetInteger(Settings.NodeColour_ProxyText)) : 
                                                                     Color.FromArgb(SettingsManager.GetInteger(Settings.NodeColour_AliasText))); 
                    switch (ent.variant)
                    {
                        case EntityVariant.FUNCTION:
                            FunctionEntity function = (FunctionEntity)ent;
                            //Both proxies and aliases can carry their own name, and fall back to the target's
                            string entName = _commands.Utils.GetEntityName(_composite, node.Entity);
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
                            Color.FromArgb(SettingsManager.GetInteger(Settings.NodeColour_FunctionNode)), 
                            Color.FromArgb(SettingsManager.GetInteger(Settings.NodeColour_FunctionNodeBottom)),
                            Color.FromArgb(SettingsManager.GetInteger(Settings.NodeColour_FunctionText)));
                        node.SetName(_commands.Utils.GetEntityName(_composite, node.Entity), funcEnt.function.AsFunctionType.ToString());
                    }
                    else
                    {
                        node.SetColour(
                            Color.FromArgb(SettingsManager.GetInteger(Settings.NodeColour_InstanceNode)),
                            Color.FromArgb(SettingsManager.GetInteger(Settings.NodeColour_InstanceNodeBottom)),
                            Color.FromArgb(SettingsManager.GetInteger(Settings.NodeColour_InstanceText)));
                        node.SetName(_commands.Utils.GetEntityName(_composite, node.Entity), Path.GetFileName(_commands.GetComposite(funcEnt.function).name));
                    }
                    break;
                case EntityVariant.VARIABLE:
                    VariableEntity varEnt = (VariableEntity)node.Entity;
                    node.SetColour(
                        Color.FromArgb(SettingsManager.GetInteger(Settings.NodeColour_VariableNode)),
                        Color.FromArgb(SettingsManager.GetInteger(Settings.NodeColour_VariableNode)),
                        Color.FromArgb(SettingsManager.GetInteger(Settings.NodeColour_VariableText)));
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
                    if (options[y] == STNodeOption.Empty)
                        continue; //a blank row holding a relay pair aligned, not a pin

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
        PointF _pasteCanvasPos = new PointF();
        private void ContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            STNode node = stNodeEditor1.GetHoveredNode();
            (STNodeOption linkIn, STNodeOption linkOut) = stNodeEditor1.GetHoveredLink();
            STNodeOption hoveredPin = stNodeEditor1.GetHoveredPin();

            if (hoveredPin?.Location == PinLocation.Top || hoveredPin?.Location == PinLocation.Bottom)
                hoveredPin = null; //Only allow right click on in/out pins

            _pasteCanvasPos = stNodeEditor1.MousePositionInCanvas;

            deleteToolStripMenuItem.Visible = node != null && hoveredPin == null;
            copyNodesToolStripMenuItem.Visible = node != null && hoveredPin == null;
            toolStripSeparator1.Visible = node != null && hoveredPin == null;
            addAllPinsToolStripMenuItem.Visible = node != null && hoveredPin == null;
            removeUnusedPinsToolStripMenuItem.Visible = node != null && hoveredPin == null;
            managePinsToolStripMenuItem.Visible = node != null && hoveredPin == null;
            toolStripSeparator4.Visible = node != null && hoveredPin == null;
            deleteEntityToolStripMenuItem.Visible = node != null && hoveredPin == null;
            toolStripSeparator5.Visible = node != null && hoveredPin == null;
            findReferencesToolStripMenuItem.Visible = node != null && hoveredPin == null;
            goToNextNodeInFlowgraphToolStripMenuItem.Visible = node != null && hoveredPin == null;

            if (node != null && hoveredPin == null)
                goToNextNodeInFlowgraphToolStripMenuItem.Enabled = HasMultipleNodesForEntity(node.Entity);

            addNodeToolStripMenuItem.Visible = node == null && linkIn == null && hoveredPin == null;
            createToolStripMenuItem.Visible = node == null && linkIn == null && hoveredPin == null;
            addNodeForSelectedEntityToolStripMenuItem.Visible = node == null && linkIn == null && hoveredPin == null;
            addNodeForSelectedEntityToolStripMenuItem.Enabled = Singleton.Editor?.CompositeDisplay?.EntityDisplay?.Entity != null;

            toolStripSeparator6.Visible = node == null && linkIn == null && hoveredPin == null;
            pasteToolStripMenuItem.Visible = node == null && linkIn == null && hoveredPin == null;
            pasteToolStripMenuItem.Enabled = EntityClipboard.HasContent;
            pasteReferenceToolStripMenuItem.Visible = node == null && linkIn == null && hoveredPin == null;
            pasteReferenceToolStripMenuItem.Enabled = EntityClipboard.HasContent
                && _composite != null
                && (EntityClipboard.SourceCompositeId == _composite.shortGUID.AsUInt32
                    || GetAliasChainToClipboardSource() != null);

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
                if (SettingsManager.GetBool(Settings.PopulateAllPinsOnCreateNode))
                    AddAllPins(node);
            }
            RefreshNodeMarkers();
        }

        //add new node for the selected entity, if one's selected
        private void addNodeForSelectedEntityToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Entity selectedEntity = Singleton.Editor?.CompositeDisplay?.EntityDisplay?.Entity;
            if (selectedEntity == null) return;
            STNode node = AddNodeForEntity(selectedEntity);
            node.SetPosition(new Point((int)stNodeEditor1.MousePositionInCanvas.X, (int)stNodeEditor1.MousePositionInCanvas.Y));
            SelectNewNode(node);
            RefreshNodeMarkers();
        }

        //delete the whole entity and associated nodes
        private void deleteEntityToolStripMenuItem_Click(object sender, EventArgs e)
        {
            STNode node = stNodeEditor1.GetHoveredNode();
            if (node == null) return;
            Entity entity = _composite.GetEntityByID(node.ShortGUID);
            if (entity == null) return;
            Singleton.Editor.CompositeDisplay.DeleteEntity(entity);
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
            MarkFlowgraphEdit(); //the pin layout is saved with the composite
        }

        //set all delay texts on a node
        private void UpdatePinDelayTexts(STNode node)
        {
            foreach (STNodeOption inputPin in node.GetInputOptions())
            {
                if (inputPin == STNodeOption.Empty)
                    continue;
                float delay = GetDelayForParameter(node.Entity, inputPin.Text);
                inputPin.LeftText = delay == 0.0f ? "" : delay.ToString();
            }
            foreach (STNodeOption outputPin in node.GetOutputOptions())
            {
                if (outputPin == STNodeOption.Empty)
                    continue;
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
            node.RemoveUnusedPins(_composite, _commands);
            node.SetPosition(newPos);
            MarkFlowgraphEdit(); //the pin layout is saved with the composite
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
                MarkFlowgraphEdit(); //the pin layout is saved with the composite

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
            stNodeEditor1.RemoveHoveredLink(); //raises OptionDisconnected, which marks the level as modified
        }

        //Delete the selected nodes - or the right-clicked one, when it isn't part of the selection
        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /* Same rule as copying: from the context menu the right-clicked node is the target unless it
             * sits inside the selection; from the Delete key (no sender) the selection is, and only when
             * nothing is selected does whatever is under the cursor stand in. */
            STNode hovered = stNodeEditor1.GetHoveredNode();
            List<STNode> nodes = new List<STNode>(stNodeEditor1.GetSelectedNode());
            if (hovered != null && (sender != null || nodes.Count == 0) && !nodes.Contains(hovered))
                nodes = new List<STNode>() { hovered };
            if (nodes.Count == 0) return;

            if (SettingsManager.GetBool(Settings.AskBeforeDeletingNode))
            {
                string what = nodes.Count == 1 ? "this node" : "these " + nodes.Count + " nodes";
                if (MessageBox.Show("Are you sure you want to remove " + what + "?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
            }

            List<Entity> entities = new List<Entity>();
            foreach (STNode node in nodes)
            {
                if (node.Entity != null && !entities.Contains(node.Entity))
                    entities.Add(node.Entity);
                stNodeEditor1.Nodes.Remove(node);
            }
            RefreshNodeMarkers();

            if (!SettingsManager.GetBool(Settings.OptionToDeleteEntityWithNode))
                return;

            CompositeDisplay display = Singleton.Editor.CompositeDisplay;
            if (display == null)
                return;
            List<Entity> orphaned = entities.Where(o => !display.AnyFlowgraphsContainEntity(o)).ToList();
            if (orphaned.Count == 0)
                return;

            string message = orphaned.Count == 1
                ? "All nodes have been removed for this entity, would you like to delete the entity too?"
                : "All nodes have been removed for " + orphaned.Count + " entities, would you like to delete those entities too?";
            if (MessageBox.Show(message, "No nodes for entity", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            foreach (Entity entity in orphaned)
                display.DeleteEntity(entity, false);
        }

        //Copy the selected node(s) to the entity clipboard. A right-clicked node outside the
        //selection copies just that node.
        private void CopyNodes(STNode contextNode = null)
        {
            List<STNode> nodes = new List<STNode>(stNodeEditor1.GetSelectedNode());
            if (contextNode != null && !nodes.Contains(contextNode))
                nodes = new List<STNode>() { contextNode };
            if (nodes.Count == 0)
                return;

            int minX = int.MaxValue, minY = int.MaxValue;
            foreach (STNode node in nodes)
            {
                minX = Math.Min(minX, node.Location.X);
                minY = Math.Min(minY, node.Location.Y);
            }

            List<EntityClipboard.Entry> entries = new List<EntityClipboard.Entry>();
            foreach (STNode node in nodes)
            {
                if (node.Entity == null)
                    continue;
                entries.Add(new EntityClipboard.Entry()
                {
                    EntityId = node.ShortGUID.AsUInt32,
                    Offset = new Point(node.Location.X - minX, node.Location.Y - minY),
                    Pins = CapturePins(node),
                });
            }
            if (entries.Count == 0)
                return;

            Singleton.Editor?.CompositeDisplay?.CopyEntitiesToClipboard(entries);
        }

        private static List<EntityClipboard.PinMeta> CapturePins(STNode node)
        {
            List<EntityClipboard.PinMeta> pins = new List<EntityClipboard.PinMeta>();
            void Capture(STNodeOption[] options)
            {
                foreach (STNodeOption option in options)
                {
                    if (option == null || option == STNodeOption.Empty)
                        continue;
                    pins.Add(new EntityClipboard.PinMeta()
                    {
                        ParameterId = option.ShortGUID.AsUInt32,
                        Location = (byte)option.Location,
                        Style = (byte)option.Style,
                    });
                }
            }
            Capture(node.GetInputOptions());
            Capture(node.GetOutputOptions());
            Capture(node.GetTopOptions());
            Capture(node.GetBottomOptions());
            return pins;
        }

        //Recreate the captured pin layout on a pasted node. Falls back to the "populate all pins"
        //setting for clipboard entries that didn't come from a flowgraph node.
        private void ApplyCopiedPins(STNode node, List<EntityClipboard.PinMeta> pins)
        {
            if (pins == null)
            {
                if (SettingsManager.GetBool(Settings.PopulateAllPinsOnCreateNode))
                    AddAllPins(node);
                return;
            }

            foreach (EntityClipboard.PinMeta pin in pins)
            {
                ShortGuid parameterId = new ShortGuid(pin.ParameterId);
                switch ((PinLocation)pin.Location)
                {
                    case PinLocation.Left:
                        node.AddInputOption(parameterId);
                        break;
                    case PinLocation.Right:
                        node.AddOutputOption(parameterId);
                        break;
                    case PinLocation.Top:
                        node.AddTopOption(parameterId, (PinStyle)pin.Style);
                        break;
                    case PinLocation.Bottom:
                        node.AddBottomOption(parameterId);
                        break;
                }
            }
            node.AlignRelayRows(_composite, _commands);
            UpdatePinDelayTexts(node);
            node.Recompute();
        }

        //Paste the clipboard as brand new entities (clones), restoring the links between them
        private void PasteClipboardClones(PointF canvasPos)
        {
            List<Tuple<EntityClipboard.Entry, Entity>> pasted = Singleton.Editor?.CompositeDisplay?.CloneClipboardEntities();
            if (pasted == null || pasted.Count == 0)
                return;

            DeselectAllNodes();

            Dictionary<uint, STNode> firstNodeByEntity = new Dictionary<uint, STNode>();
            List<STNode> newNodes = new List<STNode>();
            foreach (Tuple<EntityClipboard.Entry, Entity> pair in pasted)
            {
                STNode node = EntityToNode(pair.Item2);
                ApplyCopiedPins(node, pair.Item1.Pins);
                node.SetPosition(new Point((int)canvasPos.X + pair.Item1.Offset.X, (int)canvasPos.Y + pair.Item1.Offset.Y));
                newNodes.Add(node);
                if (!firstNodeByEntity.ContainsKey(pair.Item2.shortGUID.AsUInt32))
                    firstNodeByEntity.Add(pair.Item2.shortGUID.AsUInt32, node);
            }

            //Recreate the restored links between the pasted entities on their new nodes
            foreach (STNode node in firstNodeByEntity.Values)
            {
                foreach (EntityConnector link in node.Entity.childLinks)
                {
                    if (!firstNodeByEntity.TryGetValue(link.linkedEntityID.AsUInt32, out STNode linkedNode))
                        continue;

                    node.AddPinsForConnection(linkedNode, link.thisParamID, link.linkedParamID, _composite, _commands);
                    STNodeOption pinOut = node.GetOption(link.thisParamID) ?? node.AddOutputOption(link.thisParamID);
                    STNodeOption pinIn = linkedNode.GetOption(link.linkedParamID) ?? linkedNode.AddInputOption(link.linkedParamID);
                    ConnectionStatus status = pinOut.ConnectOption(pinIn);
                    if (status != ConnectionStatus.Connected)
                        Debug.Log("Flowgraph", "WARNING: Could not recreate pasted connection...\n\t" + node.Title + " [" + pinOut.Text + "] " + pinOut.Location + " -> " + linkedNode.Title + " [" + pinIn.Text + "] " + pinIn.Location);
                }
            }

            foreach (STNode node in newNodes)
            {
                node.AlignRelayRows(_composite, _commands); //catches pins added while re-linking above
                UpdatePinDelayTexts(node);
                node.EnsureProperNodeSizing();
                SelectNode(node, centerCanvas: false);
            }
            RefreshNodeMarkers();
        }

        /* Paste the clipboard into the middle of this page. Used when the paste came from outside the
           flowgraph UI (entity list, viewport), where there's no cursor position on the canvas: links
           between the copied entities are only kept by the composite if they exist on a page, so those
           pastes have to come through here rather than cloning the data on its own. */
        public void PasteClipboardAtCanvasCentre()
        {
            PasteClipboardClones(stNodeEditor1.ControlToCanvas(new PointF(stNodeEditor1.Width / 2f, stNodeEditor1.Height / 2f)));
        }

        //Paste the clipboard as extra nodes for the original entities (no new entities created).
        //When pasting into an ancestor composite on the drill path the copy was taken from,
        //aliases are created pointing down to the copied entities.
        private void PasteClipboardReferences(PointF canvasPos)
        {
            if (!EntityClipboard.HasContent || _composite == null)
                return;

            //Same composite: plain extra nodes for the entities themselves
            if (EntityClipboard.SourceCompositeId == _composite.shortGUID.AsUInt32)
            {
                DeselectAllNodes();

                bool anyAdded = false;
                foreach (EntityClipboard.Entry entry in EntityClipboard.Entries)
                {
                    Entity entity = _composite.GetEntityByID(new ShortGuid(entry.EntityId));
                    if (entity == null)
                        continue;

                    STNode node = EntityToNode(entity);
                    ApplyCopiedPins(node, entry.Pins);
                    node.SetPosition(new Point((int)canvasPos.X + entry.Offset.X, (int)canvasPos.Y + entry.Offset.Y));
                    SelectNode(node, centerCanvas: false);
                    anyAdded = true;
                }
                if (anyAdded)
                    RefreshNodeMarkers();
                return;
            }

            //Ancestor composite: build aliases down to the copied entities via the captured drill path
            List<uint> instanceChain = GetAliasChainToClipboardSource();
            if (instanceChain == null)
                return;

            Composite sourceComposite = _commands.GetComposite(new ShortGuid(EntityClipboard.SourceCompositeId));
            if (sourceComposite == null)
                return;

            DeselectAllNodes();

            bool addedAny = false;
            foreach (EntityClipboard.Entry entry in EntityClipboard.Entries)
            {
                if (sourceComposite.GetEntityByID(new ShortGuid(entry.EntityId)) == null)
                    continue;

                ShortGuid[] hierarchy = new ShortGuid[instanceChain.Count + 1];
                for (int i = 0; i < instanceChain.Count; i++)
                    hierarchy[i] = new ShortGuid(instanceChain[i]);
                hierarchy[instanceChain.Count] = new ShortGuid(entry.EntityId);

                //Reuse an existing alias with the same path (or one we just made for a duplicate entry)
                AliasEntity alias = _composite.aliases.FirstOrDefault(o => o.alias == new EntityPath(hierarchy));
                if (alias == null)
                {
                    Singleton.OnEntityAddPending?.Invoke();
                    alias = _composite.AddAlias(hierarchy);
                    Singleton.OnEntityAdded?.Invoke(alias);
                }

                STNode node = EntityToNode(alias);
                ApplyCopiedPins(node, entry.Pins);
                node.SetPosition(new Point((int)canvasPos.X + entry.Offset.X, (int)canvasPos.Y + entry.Offset.Y));
                SelectNode(node, centerCanvas: false);
                addedAny = true;
            }

            if (addedAny)
                RefreshNodeMarkers();
        }

        //If the current composite sits on the drill path the clipboard was copied from, returns the
        //chain of instance entity ids leading from here down to the source composite. Null otherwise.
        private List<uint> GetAliasChainToClipboardSource()
        {
            List<EntityClipboard.PathStep> steps = EntityClipboard.SourcePath;
            if (steps == null || steps.Count == 0 || _composite == null)
                return null;

            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i].CompositeId != _composite.shortGUID.AsUInt32)
                    continue;

                List<uint> chain = new List<uint>();
                for (int x = i; x < steps.Count; x++)
                    chain.Add(steps[x].InstanceEntityId);

                //The first step must still exist here for the alias to resolve
                if (chain.Count == 0 || _composite.GetEntityByID(new ShortGuid(chain[0])) == null)
                    return null;

                return chain;
            }
            return null;
        }

        private void copyNodesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyNodes(stNodeEditor1.GetHoveredNode());
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PasteClipboardClones(_pasteCanvasPos);
        }

        private void pasteReferenceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PasteClipboardReferences(_pasteCanvasPos);
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
            Singleton.Editor.CompositeDisplay.CreateFlowgraph();
        }

        //Welcome to the world of hacks
        PointF _createEntViaPopupPos = new PointF();
        BaseWindow _prevEntCreatePopup = null;
        private void createParameterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ListenForEntCreatePopup(Singleton.Editor.CompositeDisplay.CreateEntity(EntityVariant.VARIABLE));
        }
        private void createFunctionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ListenForEntCreatePopup(Singleton.Editor.CompositeDisplay.CreateEntity(EntityVariant.FUNCTION));
        }
        private void createInstanceOfCompositeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ListenForEntCreatePopup(Singleton.Editor.CompositeDisplay.CreateEntity(EntityVariant.FUNCTION, true));
        }
        private void createProxyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ListenForEntCreatePopup(Singleton.Editor.CompositeDisplay.CreateEntity(EntityVariant.PROXY));
        }
        private void createAliasToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ListenForEntCreatePopup(Singleton.Editor.CompositeDisplay.CreateEntity(EntityVariant.ALIAS));
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
            SelectNewNode(node);
            RefreshNodeMarkers();
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
                _crossRefsDialog.OnEntitySelected -= Singleton.Editor.CompositeBrowser.LoadCompositeAndEntity;
                _crossRefsDialog.OnFlowgraphSelected -= Singleton.Editor.CompositeDisplay.SelectEntityOnFlowgraph;
                _crossRefsDialog.Close();
            }

            STNode node = stNodeEditor1.GetHoveredNode();
            if (node == null || node.Entity == null)
                return;

            _crossRefsDialog = new ShowCrossRefs(node.Entity);
            _crossRefsDialog.Show();
            _crossRefsDialog.OnEntitySelected += Singleton.Editor.CompositeBrowser.LoadCompositeAndEntity;
            _crossRefsDialog.OnFlowgraphSelected += Singleton.Editor.CompositeDisplay.SelectEntityOnFlowgraph;
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
            Singleton.OnParameterModified?.Invoke(); //pin delays are stored as entity parameters

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



