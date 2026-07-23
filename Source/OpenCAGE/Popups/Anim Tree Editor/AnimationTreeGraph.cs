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

        private const int LayoutPadding = 14;
        private const int LayoutSiblingGap = 24;
        private const int LayoutColumnGap = 80;
        private const int LayoutParamGap = 24;
        // Root column — flow tree starts here; params sit above children, not left of root
        private const int LayoutStartX = 80;
        // Headroom for params stacked above the first flow column
        private const int LayoutStartY = 200;
        // Fixed column pitch so one wide node doesn't stretch the whole graph
        private const int LayoutColumnStep = 200;

        private void PositionAllNodes(STNode treeNode)
        {
            var allNodes = stNodeEditor1.Nodes;
            var positioned = new HashSet<STNode>();
            var bounds = new Dictionary<STNode, Rectangle>();
            var flowNodes = new HashSet<STNode>();

            PositionMainTree(treeNode, positioned, bounds, flowNodes);
            PositionSideNodes(allNodes, positioned, bounds, minX: LayoutStartX);
            PositionAttachedConsumers(allNodes, positioned, bounds);
            PositionUnconnectedNodes(allNodes, positioned, bounds);
            // Never shove the main flow — only nudge params / orphans
            ResolveOverlaps(positioned, bounds, movable: positioned.Where(n => !flowNodes.Contains(n)).ToHashSet());
            EnsureRootIsLeftmost(treeNode, positioned, bounds);
        }

        private List<STNode> GetFlowChildren(STNode node)
        {
            var children = new List<STNode>();
            var seen = new HashSet<STNode>();
            foreach (var output in node.GetOutputOptions())
            {
                foreach (var connection in output.GetConnectedOption())
                {
                    STNode child = connection.Owner;
                    if (child != null && child != node && seen.Add(child))
                        children.Add(child);
                }
            }
            return children;
        }

        private static int GetConnectingOutputPinCenterY(STNode parent, STNode child)
        {
            foreach (var output in parent.GetOutputOptions())
            {
                foreach (var connection in output.GetConnectedOption())
                {
                    if (connection.Owner == child)
                        return output.DotTop + Math.Max(1, output.DotSize) / 2;
                }
            }
            return parent.Top + parent.Height / 2;
        }


        private void PositionMainTree(STNode root, HashSet<STNode> positioned, Dictionary<STNode, Rectangle> bounds, HashSet<STNode> flowNodes)
        {
            var childrenMap = new Dictionary<STNode, List<STNode>>();
            var visited = new HashSet<STNode> { root };

            void Build(STNode node)
            {
                var claimed = new List<STNode>();
                foreach (var kid in GetFlowChildren(node))
                {
                    if (!visited.Add(kid))
                        continue;
                    claimed.Add(kid);
                    Build(kid);
                }
                childrenMap[node] = claimed;
            }

            Build(root);

            int columnStep = LayoutColumnStep;

            // Pin-aligned placement: each child prefers the Y of the parent output that feeds it,
            // then siblings compact downward so large subtrees don't scatter above the parent.
            int Place(STNode node, int column, int y)
            {
                if (positioned.Contains(node))
                    return bounds[node].Bottom;

                int x = LayoutStartX + column * columnStep;
                PlaceNode(node, x, Math.Max(20, y), positioned, bounds);
                flowNodes.Add(node);

                var kids = childrenMap[node];
                if (kids.Count == 0)
                    return bounds[node].Bottom;

                var ordered = kids
                    .Select(kid => (kid, prefY: GetConnectingOutputPinCenterY(node, kid) - kid.Height / 2))
                    .OrderBy(t => t.prefY)
                    .ToList();

                // Pack from the parent's top downward (tree reads L→R from the root, not diagonally)
                int prevBottom = Math.Min(bounds[node].Top, ordered[0].prefY) - LayoutSiblingGap;
                int subtreeBottom = bounds[node].Bottom;

                foreach (var (kid, prefY) in ordered)
                {
                    int kidY = Math.Max(prefY, prevBottom + LayoutSiblingGap);
                    // Don't start children above the parent — keeps the top-level visually on top-left
                    if (column == 0)
                        kidY = Math.Max(kidY, bounds[node].Top);
                    int kidBottom = Place(kid, column + 1, kidY);
                    prevBottom = kidBottom;
                    subtreeBottom = Math.Max(subtreeBottom, kidBottom);
                }

                return subtreeBottom;
            }

            Place(root, 0, LayoutStartY);
        }

        /// <summary>
        /// Place anything that feeds a top pin of an already-positioned node (params, interpolators,
        /// properties, …). Iterate so chains like pad_x → Interpolate → blend resolve in order.
        /// Side nodes always sit above their consumers — never to the left of the tree root.
        /// </summary>
        private void PositionSideNodes(STNodeCollection allNodes, HashSet<STNode> positioned, Dictionary<STNode, Rectangle> bounds, int minX)
        {
            // How many side nodes already stacked above each consumer (for vertical stacking)
            var aboveCount = new Dictionary<STNode, int>();

            const int maxPasses = 32;
            for (int pass = 0; pass < maxPasses; pass++)
            {
                var sideTargets = new Dictionary<STNode, List<STNode>>();

                foreach (STNode consumer in allNodes)
                {
                    if (!positioned.Contains(consumer))
                        continue;

                    foreach (var topOption in consumer.GetTopOptions())
                    {
                        foreach (var connection in topOption.GetConnectedOption())
                        {
                            STNode side = connection.Owner;
                            if (side == null || positioned.Contains(side))
                                continue;

                            if (!sideTargets.TryGetValue(side, out List<STNode> targets))
                            {
                                targets = new List<STNode>();
                                sideTargets[side] = targets;
                            }
                            if (!targets.Contains(consumer))
                                targets.Add(consumer);
                        }
                    }
                }

                if (sideTargets.Count == 0)
                    break;

                bool placedAny = false;
                foreach (var entry in sideTargets.OrderByDescending(e => e.Value.Count).ThenBy(e => e.Key.GetHashCode()))
                {
                    STNode side = entry.Key;
                    if (positioned.Contains(side))
                        continue;

                    List<STNode> targets = entry.Value;
                    // Prefer the densest/left cluster of consumers (median X), not a single far-right outlier
                    STNode primary = PickPrimaryConsumer(targets, bounds);
                    int stackIndex = aboveCount.TryGetValue(primary, out int n) ? n : 0;
                    Point preferred = PreferredAbovePosition(side, primary, bounds, stackIndex, minX);
                    Point free = FindFreeRectAbove(preferred.X, preferred.Y, side.Width, side.Height, bounds, minX);
                    PlaceNode(side, free.X, free.Y, positioned, bounds);
                    aboveCount[primary] = stackIndex + 1;
                    placedAny = true;
                }

                if (!placedAny)
                    break;
            }
        }

        private static STNode PickPrimaryConsumer(List<STNode> targets, Dictionary<STNode, Rectangle> bounds)
        {
            if (targets.Count == 1)
                return targets[0];

            // Median by X keeps shared providers near the bulk of consumers, not a far outlier
            var byX = targets.OrderBy(t => bounds[t].X).ThenBy(t => bounds[t].Y).ToList();
            return byX[byX.Count / 2];
        }

        private Point PreferredAbovePosition(STNode side, STNode target, Dictionary<STNode, Rectangle> bounds, int stackIndex, int minX)
        {
            Rectangle tb = bounds[target];
            // Same column as consumer, stacked above it
            int preferredX = tb.X + Math.Max(0, (tb.Width - side.Width) / 2);
            preferredX = Math.Max(minX, preferredX);

            int preferredY = tb.Y - side.Height - LayoutParamGap;
            preferredY -= stackIndex * (side.Height + LayoutParamGap);
            if (preferredY < 20)
                preferredY = 20;

            return new Point(preferredX, preferredY);
        }

        /// <summary>
        /// Place nodes that only consume a positioned provider via a top pin (e.g. Property_Listener
        /// hanging off a leaf) — sit them just below the provider instead of dumping as orphans.
        /// </summary>
        private void PositionAttachedConsumers(STNodeCollection allNodes, HashSet<STNode> positioned, Dictionary<STNode, Rectangle> bounds)
        {
            const int maxPasses = 16;
            for (int pass = 0; pass < maxPasses; pass++)
            {
                var pending = new List<(STNode consumer, STNode provider)>();

                foreach (STNode node in allNodes)
                {
                    if (positioned.Contains(node))
                        continue;

                    STNode bestProvider = null;
                    int bestX = int.MaxValue;
                    foreach (var top in node.GetTopOptions())
                    {
                        foreach (var connection in top.GetConnectedOption())
                        {
                            STNode provider = connection.Owner;
                            if (provider == null || !positioned.Contains(provider))
                                continue;
                            int px = bounds[provider].X;
                            if (px < bestX)
                            {
                                bestX = px;
                                bestProvider = provider;
                            }
                        }
                    }

                    if (bestProvider != null)
                        pending.Add((node, bestProvider));
                }

                if (pending.Count == 0)
                    break;

                bool placedAny = false;
                foreach (var (consumer, provider) in pending.OrderBy(p => bounds[p.provider].Y).ThenBy(p => bounds[p.provider].X))
                {
                    if (positioned.Contains(consumer))
                        continue;

                    Rectangle pb = bounds[provider];
                    int preferredX = Math.Max(LayoutStartX, pb.X);
                    int preferredY = pb.Bottom + LayoutParamGap;
                    Point free = FindFreeRectNearProvider(preferredX, preferredY, consumer.Width, consumer.Height, bounds);
                    PlaceNode(consumer, free.X, free.Y, positioned, bounds);
                    placedAny = true;
                }

                if (!placedAny)
                    break;
            }
        }

        private void PositionUnconnectedNodes(STNodeCollection allNodes, HashSet<STNode> positioned, Dictionary<STNode, Rectangle> bounds)
        {
            var orphans = new List<STNode>();
            foreach (STNode node in allNodes)
            {
                if (!positioned.Contains(node))
                    orphans.Add(node);
            }

            if (orphans.Count == 0)
                return;

            int contentBottom = LayoutStartY;
            foreach (var b in bounds.Values)
                contentBottom = Math.Max(contentBottom, b.Bottom);

            // Keep orphans under the tree, never left of the root column
            int gridX = LayoutStartX;
            int gridY = contentBottom + LayoutColumnGap;
            int col = 0;
            const int gridColumns = 4;
            int gridStepX = LayoutColumnStep;

            foreach (STNode node in orphans)
            {
                if (positioned.Contains(node))
                    continue;

                int px = gridX + col * gridStepX;
                int py = gridY;
                Point free = FindFreeRectNearProvider(px, py, node.Width, node.Height, bounds);
                PlaceNode(node, free.X, free.Y, positioned, bounds);

                col++;
                if (col >= gridColumns)
                {
                    col = 0;
                    gridY = Math.Max(gridY + node.Height + LayoutSiblingGap, free.Y + node.Height + LayoutSiblingGap);
                }
            }
        }

        private void PlaceNode(STNode node, int x, int y, HashSet<STNode> positioned, Dictionary<STNode, Rectangle> bounds)
        {
            node.SetPosition(new Point(x, y));
            positioned.Add(node);
            bounds[node] = new Rectangle(x, y, node.Width, node.Height);
        }

        /// <summary>
        /// Stack above the preferred point. Only tiny horizontal nudges — never walk sideways
        /// across the canvas (that caused the long "param chains" on aim trees).
        /// </summary>
        private Point FindFreeRectAbove(int preferredX, int preferredY, int width, int height, Dictionary<STNode, Rectangle> bounds, int minX)
        {
            preferredX = Math.Max(minX, preferredX);
            preferredY = Math.Max(20, preferredY);

            Rectangle test = new Rectangle(preferredX, preferredY, width, height);
            if (!HasOverlap(test, bounds))
                return new Point(preferredX, preferredY);

            const int step = 16;
            int maxUp = 400;
            int maxNudge = Math.Max(width + LayoutParamGap, 40);

            // Walk straight up first
            for (int dy = step; dy <= maxUp; dy += step)
            {
                int y = Math.Max(20, preferredY - dy);
                test = new Rectangle(preferredX, y, width, height);
                if (!HasOverlap(test, bounds))
                    return new Point(preferredX, y);
            }

            // Small left/right nudge at each height (stay in the consumer's column)
            for (int dy = 0; dy <= maxUp; dy += step)
            {
                int y = Math.Max(20, preferredY - dy);
                for (int dx = step; dx <= maxNudge; dx += step)
                {
                    foreach (int sign in new[] { 1, -1 })
                    {
                        int x = Math.Max(minX, preferredX + sign * dx);
                        test = new Rectangle(x, y, width, height);
                        if (!HasOverlap(test, bounds))
                            return new Point(x, y);
                    }
                }
            }

            // Last resort: directly above at y=20
            return new Point(preferredX, 20);
        }

        /// <summary>
        /// Free slot near a provider — prefer below, then slight right (for attached listeners).
        /// </summary>
        private Point FindFreeRectNearProvider(int preferredX, int preferredY, int width, int height, Dictionary<STNode, Rectangle> bounds)
        {
            preferredX = Math.Max(LayoutStartX, preferredX);
            preferredY = Math.Max(20, preferredY);

            Rectangle test = new Rectangle(preferredX, preferredY, width, height);
            if (!HasOverlap(test, bounds))
                return new Point(preferredX, preferredY);

            const int step = 16;
            for (int ring = 1; ring <= 30; ring++)
            {
                int d = ring * step;
                (int dx, int dy)[] dirs =
                {
                    (0, d), (0, -d), (d, 0), (d, d), (d, -d),
                    (-d, d), (-d, 0), (-d, -d)
                };
                foreach (var (dx, dy) in dirs)
                {
                    int x = Math.Max(LayoutStartX, preferredX + dx);
                    int y = Math.Max(20, preferredY + dy);
                    test = new Rectangle(x, y, width, height);
                    if (!HasOverlap(test, bounds))
                        return new Point(x, y);
                }
            }

            return new Point(preferredX, preferredY + height + LayoutParamGap);
        }

        private void EnsureRootIsLeftmost(STNode root, HashSet<STNode> positioned, Dictionary<STNode, Rectangle> bounds)
        {
            if (!bounds.TryGetValue(root, out Rectangle rootBounds))
                return;

            int minX = int.MaxValue;
            foreach (var b in bounds.Values)
                minX = Math.Min(minX, b.X);

            int shift = rootBounds.X - minX;
            if (shift <= 0)
                return;

            foreach (STNode node in positioned.ToList())
            {
                Rectangle b = bounds[node];
                PlaceNode(node, b.X + shift, b.Y, positioned, bounds);
            }
        }

        private void ResolveOverlaps(HashSet<STNode> positioned, Dictionary<STNode, Rectangle> bounds, HashSet<STNode> movable)
        {
            const int maxPasses = 50;
            for (int pass = 0; pass < maxPasses; pass++)
            {
                bool moved = false;
                var nodes = positioned.ToList();
                for (int i = 0; i < nodes.Count; i++)
                {
                    for (int j = i + 1; j < nodes.Count; j++)
                    {
                        STNode aNode = nodes[i];
                        STNode bNode = nodes[j];
                        Rectangle a = Inflate(bounds[aNode], LayoutPadding);
                        Rectangle b = Inflate(bounds[bNode], LayoutPadding);
                        if (!a.IntersectsWith(b))
                            continue;

                        STNode moveNode;
                        STNode stayNode;
                        if (movable.Contains(bNode) && !movable.Contains(aNode))
                        {
                            moveNode = bNode;
                            stayNode = aNode;
                        }
                        else if (movable.Contains(aNode) && !movable.Contains(bNode))
                        {
                            moveNode = aNode;
                            stayNode = bNode;
                        }
                        else if (movable.Contains(aNode) && movable.Contains(bNode))
                        {
                            if (bounds[aNode].Y >= bounds[bNode].Y)
                            {
                                moveNode = aNode;
                                stayNode = bNode;
                            }
                            else
                            {
                                moveNode = bNode;
                                stayNode = aNode;
                            }
                        }
                        else
                        {
                            continue;
                        }

                        Rectangle moveBounds = bounds[moveNode];
                        Rectangle other = bounds[stayNode];
                        int gap = LayoutPadding;

                        // Prefer pushing params further above; never left of root column
                        int newY = other.Y - moveBounds.Height - gap;
                        if (newY >= 20)
                        {
                            int x = Math.Max(LayoutStartX, moveBounds.X);
                            PlaceNode(moveNode, x, newY, positioned, bounds);
                        }
                        else
                        {
                            int newX = Math.Max(LayoutStartX, other.Right + gap);
                            PlaceNode(moveNode, newX, Math.Max(20, moveBounds.Y), positioned, bounds);
                        }
                        moved = true;
                    }
                }
                if (!moved)
                    break;
            }
        }

        private static Rectangle Inflate(Rectangle r, int padding)
        {
            return new Rectangle(r.X - padding, r.Y - padding, r.Width + padding * 2, r.Height + padding * 2);
        }

        private static bool HasOverlap(Rectangle testBounds, Dictionary<STNode, Rectangle> nodeBounds)
        {
            foreach (var placed in nodeBounds.Values)
            {
                if (testBounds.IntersectsWith(Inflate(placed, LayoutPadding)))
                    return true;
            }
            return false;
        }
    }
}