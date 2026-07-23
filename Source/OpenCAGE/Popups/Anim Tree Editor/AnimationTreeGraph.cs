using CATHODE;
using CATHODE.Animations;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.AnimTrees
{
    public partial class AnimationTreeGraph : DockContent
    {
        private Dictionary<AnimationNode, STNode> _nodeLookups = new Dictionary<AnimationNode, STNode>();
        private AnimationNodeEditor _editor = null;

        public AnimationTreeGraph()
        {
            InitializeComponent();

            this.VisibleChanged += AnimationTree_VisibleChanged;
            this.FormClosed += AnimationTree_FormClosed;

            _editor = new AnimationNodeEditor();
            _editor.Show(AnimTreeEditor.DockPanel, DockState.DockRight);

            stNodeEditor1.LoadAssembly(Application.ExecutablePath);
            stNodeEditor1.AllowSameOwnerConnections = true;
            stNodeEditor1.SelectedChanged += StNodeEditor1_SelectedChanged;
        }

        private void StNodeEditor1_SelectedChanged(object sender, EventArgs e)
        {
            STNode[] nodes = stNodeEditor1.GetSelectedNode();
            if (nodes.Length > 0)
                _editor.PopulateData(nodes[0].AnimationNode);
            else
                _editor.PopulateData(null);
        }

        private void AnimationTree_VisibleChanged(object sender, EventArgs e)
        {

        }

        private void AnimationTree_FormClosed(object sender, FormClosedEventArgs e)
        {
            this.VisibleChanged -= AnimationTree_VisibleChanged;
            this.FormClosed -= AnimationTree_FormClosed;

            if (_editor != null)
                _editor.Close();
        }

        public void CommitPendingEdits()
        {
            _editor?.CommitPendingEdits();
        }

        public void PopulateGraph(AnimationTree animTree)
        {
            this.Text = animTree.Name;

            stNodeEditor1.SuspendLayout();
            stNodeEditor1.Nodes.Clear();

            STNode treeNode = CreateAnimNodeNode(animTree);
            foreach (AnimationNode animNode in animTree.Nodes)
                CreateAnimNodeNode(animNode);

            foreach (STNode node in stNodeEditor1.Nodes)
                SetupConnections(node);

            PositionAllNodes(treeNode);

            stNodeEditor1.ResumeLayout();
        }

        private STNode CreateAnimNodeNode(AnimationNode animNode)
        {
            //todo - when is this hit
            if (_nodeLookups.TryGetValue(animNode, out STNode existingNode))
                return existingNode;

            STNode node = new STNode();
            node.AnimationNode = animNode;
            stNodeEditor1.Nodes.Add(node);
            _nodeLookups.Add(animNode, node);

            if (!(animNode is ParameterNode) && !(animNode is PropertyNode) && !(animNode is PropertyListenerNode) && animNode.Type != NodeType.ANIM_Callback && animNode.Type != NodeType.ANIM_Event_Callback) //todo where should event_callback be created from?
                node.AddInputOption(ShortGuidUtils.Generate("trigger"));

            //it seems we always have a "mirror_" counterpart for ANIM_Property_Listener which can be removed -> see HUMANOID::Persistent_Act_Aim_Blindfire_Low

            switch (animNode.Type)
            {
                case NodeType.ANIM_Animation:
                case NodeType.ANIM_2DParametric:
                case NodeType.ANIM_3DParametric:
                    //todo: 4d and regular parametric here too?
                case NodeType.ANIM_AutoFloatParameter:
                case NodeType.ANIM_Parameter:
                case NodeType.ANIM_FloatInterpolator:
                case NodeType.ANIM_Callback:
                case NodeType.ANIM_Property:
                case NodeType.ANIM_Event_Callback:
                    node.AddBottomOption(ShortGuidUtils.Generate("value"));
                    break;
                case NodeType.ANIM_Selector:
                case NodeType.ANIM_Enumerated_Selector:
                case NodeType.ANIM_Ranged_Selector:
                case NodeType.ANIM_Parametric:
                    node.AddOutputOption(ShortGuidUtils.Generate("State_01"));
                    node.AddOutputOption(ShortGuidUtils.Generate("State_02"));
                    node.AddOutputOption(ShortGuidUtils.Generate("State_03"));
                    node.AddOutputOption(ShortGuidUtils.Generate("State_04"));
                    node.AddOutputOption(ShortGuidUtils.Generate("State_05"));
                    node.AddOutputOption(ShortGuidUtils.Generate("State_06"));
                    node.AddOutputOption(ShortGuidUtils.Generate("State_07"));
                    node.AddOutputOption(ShortGuidUtils.Generate("State_08"));
                    if (animNode.Type == NodeType.ANIM_Ranged_Selector)
                        break;
                    node.AddOutputOption(ShortGuidUtils.Generate("State_09"));
                    node.AddOutputOption(ShortGuidUtils.Generate("State_10"));
                    node.AddOutputOption(ShortGuidUtils.Generate("State_11"));
                    node.AddOutputOption(ShortGuidUtils.Generate("State_12"));
                    node.AddOutputOption(ShortGuidUtils.Generate("State_13"));
                    node.AddOutputOption(ShortGuidUtils.Generate("State_14"));
                    node.AddOutputOption(ShortGuidUtils.Generate("State_15"));
                    node.AddOutputOption(ShortGuidUtils.Generate("State_16"));
                    break;
                case NodeType.ANIM_Foot_Sync_Selector:
                    node.AddOutputOption(ShortGuidUtils.Generate("LeftStrikeChild"));
                    node.AddOutputOption(ShortGuidUtils.Generate("RightStrikeChild"));
                    break;
                case NodeType.ANIM_Additive_Blend:
                case NodeType.ANIM_Parametric_Additive_Blend:
                    node.AddOutputOption(ShortGuidUtils.Generate("base_node"));
                    node.AddOutputOption(ShortGuidUtils.Generate("additive_node"));
                    break;
                case NodeType.ANIM_Tree_Top_Level:
                    node.AddOutputOption(ShortGuidUtils.Generate("NODES"));
                    break;
                case NodeType.ANIM_Weighted:
                    node.AddOutputOption(ShortGuidUtils.Generate("child"));
                    break;
            }

            switch (animNode.Type)
            {
                case NodeType.ANIM_Animation:
                    node.AddTopOption(ShortGuidUtils.Generate("Callback"));
                    break;
                case NodeType.ANIM_2DParametric:
                case NodeType.ANIM_3DParametric:
                    node.AddTopOption(ShortGuidUtils.Generate("ParameterBindingX"));
                    node.AddTopOption(ShortGuidUtils.Generate("ParameterBindingY"));
                    if (animNode.Type == NodeType.ANIM_3DParametric)
                        node.AddTopOption(ShortGuidUtils.Generate("ParameterBindingZ"));
                    node.AddTopOption(ShortGuidUtils.Generate("OverflowCallback"));
                    break;
                case NodeType.ANIM_Selector:
                case NodeType.ANIM_Enumerated_Selector:
                case NodeType.ANIM_Ranged_Selector:
                case NodeType.ANIM_Parametric:
                    node.AddTopOption(ShortGuidUtils.Generate("ParameterBinding"));
                    break;
                case NodeType.ANIM_IK:
                    node.AddTopOption(ShortGuidUtils.Generate("IkEffector"));
                    break;
                case NodeType.ANIM_Parametric_Additive_Blend:
                    node.AddTopOption(ShortGuidUtils.Generate("WeightControlParameter"));
                    break;
                case NodeType.ANIM_Weighted:
                    node.AddTopOption(ShortGuidUtils.Generate("Parameter"));
                    break;
                case NodeType.ANIM_FloatInterpolator:
                    node.AddTopOption(ShortGuidUtils.Generate("SourceParameter"));
                    break;
                case NodeType.ANIM_Property_Listener:
                    node.AddTopOption(ShortGuidUtils.Generate("LeafNode"));
                    break;
                case NodeType.ANIM_Randomised_Animation:
                    node.AddTopOption(ShortGuidUtils.Generate("Callback"));
                    node.AddTopOption(ShortGuidUtils.Generate("RandomCallback")); //this is auto generated - we should hide this node. it's the node name with #@RAND@# at the end
                    break;
            }

            return node;
        }

        private void SetupConnections(STNode node)
        {
            AnimationNode animNode = node.AnimationNode;
            switch (animNode.Type)
            {
                case NodeType.ANIM_Animation:
                    {
                        LeafNode leafNode = (LeafNode)animNode;
                        if (leafNode.Callback != null && _nodeLookups.TryGetValue(leafNode.Callback, out STNode callbackNode))
                            callbackNode.GetBottomOption(ShortGuidUtils.Generate("value")).ConnectOption(node.GetTopOption(ShortGuidUtils.Generate("Callback")));
                    }
                    break;
                case NodeType.ANIM_Property_Listener:
                    {
                        PropertyListenerNode listener = (PropertyListenerNode)animNode;
                        if (listener.LeafNode != null && _nodeLookups.TryGetValue(listener.LeafNode, out STNode leafNode))
                            leafNode.GetBottomOption(ShortGuidUtils.Generate("value")).ConnectOption(node.GetTopOption(ShortGuidUtils.Generate("LeafNode")));
                    }
                    break;
                case NodeType.ANIM_2DParametric:
                    {
                        Parametric2DNode parametric2D = (Parametric2DNode)animNode;
                        if (parametric2D.ParameterBindingX != null && _nodeLookups.TryGetValue(parametric2D.ParameterBindingX, out STNode paramXNode))
                            paramXNode.GetBottomOption(ShortGuidUtils.Generate("value")).ConnectOption(node.GetTopOption(ShortGuidUtils.Generate("ParameterBindingX")));
                        if (parametric2D.ParameterBindingY != null && _nodeLookups.TryGetValue(parametric2D.ParameterBindingY, out STNode paramYNode))
                            paramYNode.GetBottomOption(ShortGuidUtils.Generate("value")).ConnectOption(node.GetTopOption(ShortGuidUtils.Generate("ParameterBindingY")));
                        if (parametric2D.BlendSet != null && _nodeLookups.TryGetValue(parametric2D.BlendSet, out STNode blendNode))
                            node.GetOutputOption(ShortGuidUtils.Generate("triggered")).ConnectOption(blendNode.GetInputOption(ShortGuidUtils.Generate("trigger")));
                        if (parametric2D.OverflowCallback != null && _nodeLookups.TryGetValue(parametric2D.OverflowCallback, out STNode callbackNode))
                            callbackNode.GetBottomOption(ShortGuidUtils.Generate("value")).ConnectOption(node.GetTopOption(ShortGuidUtils.Generate("OverflowCallback")));
                    }
                    break;
                case NodeType.ANIM_3DParametric:
                    {
                        Parametric3DNode parametric3D = (Parametric3DNode)animNode;
                        if (parametric3D.ParameterBindingX != null && _nodeLookups.TryGetValue(parametric3D.ParameterBindingX, out STNode paramXNode))
                            paramXNode.GetBottomOption(ShortGuidUtils.Generate("value")).ConnectOption(node.GetTopOption(ShortGuidUtils.Generate("ParameterBindingX")));
                        if (parametric3D.ParameterBindingY != null && _nodeLookups.TryGetValue(parametric3D.ParameterBindingY, out STNode paramYNode))
                            paramYNode.GetBottomOption(ShortGuidUtils.Generate("value")).ConnectOption(node.GetTopOption(ShortGuidUtils.Generate("ParameterBindingY")));
                        if (parametric3D.ParameterBindingZ != null && _nodeLookups.TryGetValue(parametric3D.ParameterBindingZ, out STNode paramZNode))
                            paramZNode.GetBottomOption(ShortGuidUtils.Generate("value")).ConnectOption(node.GetTopOption(ShortGuidUtils.Generate("ParameterBindingZ")));
                        if (parametric3D.BlendSet != null && _nodeLookups.TryGetValue(parametric3D.BlendSet, out STNode blendNode))
                            node.GetOutputOption(ShortGuidUtils.Generate("triggered")).ConnectOption(blendNode.GetInputOption(ShortGuidUtils.Generate("trigger")));
                        if (parametric3D.OverflowCallback != null && _nodeLookups.TryGetValue(parametric3D.OverflowCallback, out STNode callbackNode))
                            callbackNode.GetBottomOption(ShortGuidUtils.Generate("value")).ConnectOption(node.GetTopOption(ShortGuidUtils.Generate("OverflowCallback")));
                    }
                    break;
                case NodeType.ANIM_Selector:
                case NodeType.ANIM_Enumerated_Selector:
                    {
                        SelectorNode selector = (SelectorNode)animNode;
                        for (int i = 0; i < selector.States.Length; i++)
                        {
                            if (selector.States[i]?.Node == null)
                                continue;
                            
                            if (_nodeLookups.TryGetValue(selector.States[i].Node, out STNode targetNode))
                            {
                                string outPin = "State_" + (i < 10 ? "0" : "") + (i + 1).ToString();
                                node.GetOutputOption(ShortGuidUtils.Generate(outPin)).ConnectOption(targetNode.GetInputOption(ShortGuidUtils.Generate("trigger")));
                            }
                        }
                        if (selector.ParameterBinding != null && _nodeLookups.TryGetValue(selector.ParameterBinding, out STNode paramNode))
                        {
                            paramNode.GetBottomOption(ShortGuidUtils.Generate("value")).ConnectOption(node.GetTopOption(ShortGuidUtils.Generate("ParameterBinding")));
                        }
                    }
                    break;
                case NodeType.ANIM_Ranged_Selector:
                    {
                        RangedSelectorNode selector = (RangedSelectorNode)animNode;
                        for (int i = 0; i < selector.States.Length; i++)
                        {
                            if (selector.States[i]?.Node == null)
                                continue;
                            
                            if (_nodeLookups.TryGetValue(selector.States[i].Node, out STNode targetNode))
                            {
                                string outPin = "State_" + (i < 10 ? "0" : "") + (i + 1).ToString();
                                node.GetOutputOption(ShortGuidUtils.Generate(outPin)).ConnectOption(targetNode.GetInputOption(ShortGuidUtils.Generate("trigger")));
                            }
                        }
                        if (selector.ParameterBinding != null && _nodeLookups.TryGetValue(selector.ParameterBinding, out STNode paramNode))
                        {
                            paramNode.GetBottomOption(ShortGuidUtils.Generate("value")).ConnectOption(node.GetTopOption(ShortGuidUtils.Generate("ParameterBinding")));
                        }
                    }
                    break;
                case NodeType.ANIM_Parametric:
                    {
                        ParametricNode parametric = (ParametricNode)animNode;
                        for (int i = 0; i < parametric.States.Length; i++)
                        {
                            if (parametric.States[i]?.Node == null)
                                continue;
                            
                            if (_nodeLookups.TryGetValue(parametric.States[i].Node, out STNode targetNode))
                            {
                                string outPin = "State_" + (i < 10 ? "0" : "") + (i + 1).ToString();
                                node.GetOutputOption(ShortGuidUtils.Generate(outPin)).ConnectOption(targetNode.GetInputOption(ShortGuidUtils.Generate("trigger")));
                            }
                        }
                        if (parametric.ParameterBinding != null && _nodeLookups.TryGetValue(parametric.ParameterBinding, out STNode paramNode))
                        {
                            paramNode.GetBottomOption(ShortGuidUtils.Generate("value")).ConnectOption(node.GetTopOption(ShortGuidUtils.Generate("ParameterBinding")));
                        }
                    }
                    break;
                case NodeType.ANIM_Foot_Sync_Selector:
                    {
                        FootSyncSelectorNode footSync = (FootSyncSelectorNode)animNode;
                        if (footSync.LeftStrikeChild != null && _nodeLookups.TryGetValue(footSync.LeftStrikeChild, out STNode leftNode))
                            node.GetOutputOption(ShortGuidUtils.Generate("LeftStrikeChild")).ConnectOption(leftNode.GetInputOption(ShortGuidUtils.Generate("trigger")));
                        if (footSync.RightStrikeChild != null && _nodeLookups.TryGetValue(footSync.RightStrikeChild, out STNode rightNode))
                            node.GetOutputOption(ShortGuidUtils.Generate("RightStrikeChild")).ConnectOption(rightNode.GetInputOption(ShortGuidUtils.Generate("trigger")));
                    }
                    break;
                case NodeType.ANIM_Additive_Blend:
                    {
                        AdditiveBlendNode additive = (AdditiveBlendNode)animNode;
                        if (additive.BaseNode != null && _nodeLookups.TryGetValue(additive.BaseNode, out STNode baseNode))
                            node.GetOutputOption(ShortGuidUtils.Generate("base_node")).ConnectOption(baseNode.GetInputOption(ShortGuidUtils.Generate("trigger")));
                        if (additive.AdditiveNode != null && _nodeLookups.TryGetValue(additive.AdditiveNode, out STNode additiveNode))
                            node.GetOutputOption(ShortGuidUtils.Generate("additive_node")).ConnectOption(additiveNode.GetInputOption(ShortGuidUtils.Generate("trigger")));
                    }
                    break;
                case NodeType.ANIM_Parametric_Additive_Blend:
                    {
                        ParametricAdditiveBlendNode parametricAdditive = (ParametricAdditiveBlendNode)animNode;
                        if (parametricAdditive.BaseNode != null && _nodeLookups.TryGetValue(parametricAdditive.BaseNode, out STNode baseNode))
                            node.GetOutputOption(ShortGuidUtils.Generate("base_node")).ConnectOption(baseNode.GetInputOption(ShortGuidUtils.Generate("trigger")));
                        if (parametricAdditive.AdditiveNode != null && _nodeLookups.TryGetValue(parametricAdditive.AdditiveNode, out STNode additiveNode))
                            node.GetOutputOption(ShortGuidUtils.Generate("additive_node")).ConnectOption(additiveNode.GetInputOption(ShortGuidUtils.Generate("trigger")));
                        if (parametricAdditive.WeightControlParameter != null && _nodeLookups.TryGetValue(parametricAdditive.WeightControlParameter, out STNode weightParamNode))
                            weightParamNode.GetBottomOption(ShortGuidUtils.Generate("value")).ConnectOption(node.GetTopOption(ShortGuidUtils.Generate("WeightControlParameter")));
                    }
                    break;
                case NodeType.ANIM_IK:
                    {
                        IkNode ik = (IkNode)animNode;
                        if (ik.IkEffector != null && _nodeLookups.TryGetValue(ik.IkEffector, out STNode ikEffectorNode))
                            ikEffectorNode.GetBottomOption(ShortGuidUtils.Generate("value")).ConnectOption(node.GetTopOption(ShortGuidUtils.Generate("IkEffector")));
                    }
                    break;
                case NodeType.ANIM_FloatInterpolator:
                    {
                        FloatInterpolatorNode floatInterp = (FloatInterpolatorNode)animNode;
                        if (floatInterp.SourceParameter != null && _nodeLookups.TryGetValue(floatInterp.SourceParameter, out STNode sourceParam))
                            sourceParam.GetBottomOption(ShortGuidUtils.Generate("value")).ConnectOption(node.GetTopOption(ShortGuidUtils.Generate("SourceParameter")));
                    }
                    break;
                case NodeType.ANIM_Randomised_Animation:
                    {
                        RandomisedLeafNode randomisedLeaf = (RandomisedLeafNode)animNode;
                        if (randomisedLeaf.Callback != null && _nodeLookups.TryGetValue(randomisedLeaf.Callback, out STNode callbackNode))
                            callbackNode.GetBottomOption(ShortGuidUtils.Generate("value")).ConnectOption(node.GetTopOption(ShortGuidUtils.Generate("Callback")));
                        if (randomisedLeaf.RandomCallback != null && _nodeLookups.TryGetValue(randomisedLeaf.RandomCallback, out STNode randomCallbackNode))
                            randomCallbackNode.GetBottomOption(ShortGuidUtils.Generate("value")).ConnectOption(node.GetTopOption(ShortGuidUtils.Generate("RandomCallback")));
                    }
                    break;
                case NodeType.ANIM_Tree_Top_Level:
                    foreach (AnimationNode childNode in ((AnimationTree)animNode).Children)
                    {
                        if (_nodeLookups.TryGetValue(childNode, out STNode childSTNode))
                            node.GetOutputOption(ShortGuidUtils.Generate("NODES")).ConnectOption(childSTNode.GetInputOption(ShortGuidUtils.Generate("trigger")));
                    }
                    break;
                case NodeType.ANIM_Weighted:
                    {
                        WeightedNode weighted = (WeightedNode)animNode;
                        if (weighted.Parameter != null && _nodeLookups.TryGetValue(weighted.Parameter, out STNode paramNode))
                            paramNode.GetBottomOption(ShortGuidUtils.Generate("value")).ConnectOption(node.GetTopOption(ShortGuidUtils.Generate("Parameter")));
                        if (weighted.Child != null)
                            node.GetOutputOption(ShortGuidUtils.Generate("child")).ConnectOption(_nodeLookups[weighted.Child].GetInputOption(ShortGuidUtils.Generate("trigger")));
                    }
                    break;
            }
        }

        private void PositionAllNodes(STNode treeNode)
        {
            var allNodes = stNodeEditor1.Nodes;
            var positionedNodes = new HashSet<STNode>();
            var nodeBounds = new Dictionary<STNode, Rectangle>();
            
            int horizontalSpacing = 200;
            int verticalSpacing = 120;  
            int parameterColumnX = 50;  
            int gridColumns = 4;        
            int gridSpacing = 150;      
            
            foreach (STNode node in allNodes)
                nodeBounds[node] = new Rectangle(0, 0, node.Width, node.Height);

            int maxY = PositionMainTree(treeNode, positionedNodes, nodeBounds, horizontalSpacing, verticalSpacing);
            int paramY = PositionParameterNodes(allNodes, positionedNodes, nodeBounds, parameterColumnX, maxY + verticalSpacing, verticalSpacing);
            PositionUnconnectedNodes(allNodes, positionedNodes, nodeBounds, paramY + verticalSpacing, gridColumns, gridSpacing, verticalSpacing);
        }

        private int PositionMainTree(STNode treeNode, HashSet<STNode> positionedNodes, Dictionary<STNode, Rectangle> nodeBounds, int horizontalSpacing, int verticalSpacing)
        {
            int startX = 200;
            int startY = 50;
            int maxY = startY;
            
            treeNode.SetPosition(new Point(startX, startY));
            positionedNodes.Add(treeNode);
            nodeBounds[treeNode] = new Rectangle(startX, startY, treeNode.Width, treeNode.Height);
            
            var queue = new Queue<(STNode node, int column, int y)>();
            queue.Enqueue((treeNode, 0, startY));
            while (queue.Count > 0)
            {
                var (currentNode, column, currentY) = queue.Dequeue();
                int nextColumn = column + 1;
                int nextX = startX + (nextColumn * horizontalSpacing);
                
                var outputOptions = currentNode.GetOutputOptions();
                var connectedNodes = new List<STNode>();
                foreach (var output in outputOptions)
                {
                    var connections = output.GetConnectedOption();
                    foreach (var connection in connections)
                    {
                        if (!positionedNodes.Contains(connection.Owner))
                        {
                            connectedNodes.Add(connection.Owner);
                        }
                    }
                }
                
                int nodeY = currentY;
                foreach (var connectedNode in connectedNodes)
                {
                    if (!positionedNodes.Contains(connectedNode))
                    {
                        nodeY = FindNonOverlappingY(nodeY, connectedNode, nodeBounds, nextX, verticalSpacing);
                        
                        connectedNode.SetPosition(new Point(nextX, nodeY));
                        positionedNodes.Add(connectedNode);
                        nodeBounds[connectedNode] = new Rectangle(nextX, nodeY, connectedNode.Width, connectedNode.Height);
                        
                        maxY = Math.Max(maxY, nodeY + connectedNode.Height);
                        
                        queue.Enqueue((connectedNode, nextColumn, nodeY));
                        
                        nodeY += connectedNode.Height + verticalSpacing;
                    }
                }
            }
            
            return maxY;
        }
        
        private int PositionParameterNodes(STNodeCollection allNodes, HashSet<STNode> positionedNodes, Dictionary<STNode, Rectangle> nodeBounds, int x, int startY, int verticalSpacing)
        {
            var parameterConnections = new List<(STNode paramNode, STNode targetNode)>();
            
            foreach (STNode node in allNodes)
            {
                if (positionedNodes.Contains(node))
                    continue;
                    
                foreach (STNode otherNode in allNodes)
                {
                    if (!positionedNodes.Contains(otherNode))
                        continue;

                    var topOptions = otherNode.GetTopOptions();
                    foreach (var topOption in topOptions)
                    {
                        var connections = topOption.GetConnectedOption();
                        foreach (var connection in connections)
                        {
                            if (connection.Owner == node)
                            {
                                parameterConnections.Add((node, otherNode));
                                break;
                            }
                        }
                    }
                }
            }
            
            var targetGroups = parameterConnections.GroupBy(conn => conn.targetNode).ToList();
            
            int maxY = startY;
            foreach (var group in targetGroups)
            {
                STNode targetNode = group.Key;
                var paramNodes = group.Select(g => g.paramNode).ToList();
                
                int baseY = targetNode.Location.Y - (verticalSpacing / 2);
                int currentY = baseY;
                
                foreach (var paramNode in paramNodes)
                {
                    if (positionedNodes.Contains(paramNode))
                        continue;
                    
                    int targetX = targetNode.Location.X;
                    int finalY = FindNonOverlappingY(currentY - paramNode.Height, paramNode, nodeBounds, targetX, verticalSpacing);
                    
                    paramNode.SetPosition(new Point(targetX, finalY));
                    positionedNodes.Add(paramNode);
                    nodeBounds[paramNode] = new Rectangle(targetX, finalY, paramNode.Width, paramNode.Height);
                    
                    maxY = Math.Max(maxY, finalY + paramNode.Height);
                    
                    currentY = finalY - verticalSpacing;
                }
            }
            
            return maxY;
        }
        
        private void PositionUnconnectedNodes(STNodeCollection allNodes, HashSet<STNode> positionedNodes, Dictionary<STNode, Rectangle> nodeBounds, int startY, int gridColumns, int gridSpacing, int verticalSpacing)
        {
            var unconnectedNodes = new List<STNode>();
            
            foreach (STNode node in allNodes)
            {
                if (positionedNodes.Contains(node))
                    continue;
                    
                bool hasConnections = false;
                
                var inputOptions = node.GetInputOptions();
                foreach (var input in inputOptions)
                {
                    if (input.GetConnectedOption().Count > 0)
                    {
                        hasConnections = true;
                        break;
                    }
                }
                
                if (!hasConnections)
                {
                    var outputOptions = node.GetOutputOptions();
                    foreach (var output in outputOptions)
                    {
                        if (output.GetConnectedOption().Count > 0)
                        {
                            hasConnections = true;
                            break;
                        }
                    }
                }
                
                if (!hasConnections)
                {
                    var topOptions = node.GetTopOptions();
                    foreach (var top in topOptions)
                    {
                        if (top.GetConnectedOption().Count > 0)
                        {
                            hasConnections = true;
                            break;
                        }
                    }
                }
                
                if (!hasConnections)
                {
                    var bottomOptions = node.GetBottomOptions();
                    foreach (var bottom in bottomOptions)
                    {
                        if (bottom.GetConnectedOption().Count > 0)
                        {
                            hasConnections = true;
                            break;
                        }
                    }
                }
                
                if (!hasConnections)
                    unconnectedNodes.Add(node);
            }
            
            int currentX = 50;
            int currentY = startY;
            int currentColumn = 0;
            
            foreach (var node in unconnectedNodes)
            {
                Point position = FindNonOverlappingGridPosition(currentX, currentY, node, nodeBounds, gridSpacing, verticalSpacing);
                node.SetPosition(position);
                positionedNodes.Add(node);
                nodeBounds[node] = new Rectangle(position.X, position.Y, node.Width, node.Height);
                
                currentColumn++;
                if (currentColumn >= gridColumns)
                {
                    currentColumn = 0;
                    currentX = 50;
                    currentY += node.Height + verticalSpacing;
                }
                else
                {
                    currentX += gridSpacing;
                }
            }
        }
        
        private int FindNonOverlappingY(int preferredY, STNode node, Dictionary<STNode, Rectangle> nodeBounds, int x, int verticalSpacing)
        {
            Rectangle testBounds = new Rectangle(x, preferredY, node.Width, node.Height);
            while (HasOverlap(testBounds, nodeBounds))
            {
                preferredY += verticalSpacing;
                testBounds.Y = preferredY;
            }
            return preferredY;
        }
        
        private Point FindNonOverlappingGridPosition(int preferredX, int preferredY, STNode node, Dictionary<STNode, Rectangle> nodeBounds, int gridSpacing, int verticalSpacing)
        {
            Rectangle testBounds = new Rectangle(preferredX, preferredY, node.Width, node.Height);
            
            int attempts = 0;
            while (HasOverlap(testBounds, nodeBounds) && attempts < 100)
            {
                preferredX += gridSpacing;
                if (preferredX > 1000) 
                {
                    preferredX = 50;
                    preferredY += node.Height + verticalSpacing;
                }
                testBounds.X = preferredX;
                testBounds.Y = preferredY;
                attempts++;
            }
            
            return new Point(preferredX, preferredY);
        }
        
        private bool HasOverlap(Rectangle testBounds, Dictionary<STNode, Rectangle> nodeBounds)
        {
            foreach (var bounds in nodeBounds.Values)
            {
                if (testBounds.IntersectsWith(bounds))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
