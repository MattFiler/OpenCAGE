using CATHODE;
using CATHODE.Animations;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
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
using static OpenCAGE.AnimTreeLayouts;

namespace OpenCAGE.AnimTrees
{
    public partial class AnimationTreeGraph : DockContent
    {
        private AnimationNodeEditor _editor = null;
        private AnimTreeDB _currentDb = null;
        private AnimationTree _currentTree = null;
        private AnimTreeCanvas _canvas = null;
        private Point _contextMenuCanvasPos = Point.Empty;
        private STNode _contextMenuNode = null;

        //The layout key this graph last stored the tree under: a renamed tree's old entry goes when it is stored again
        private uint _storedSet, _storedTree;

        //The view to show once the canvas has its size (a graph is filled before it is shown)
        private Action _viewPending = null;

        /// <summary>The tree this graph shows (null until one is opened).</summary>
        public AnimationTree Tree => _currentTree;
        public AnimTreeDB Database => _currentDb;
        internal AnimTreeCanvas Canvas => _canvas;

        public AnimationTreeGraph()
        {
            InitializeComponent();
            Theming.ThemeManager.ApplyToForm(this);
            EditorIcons.Bind(arrangeTreeToolStripMenuItem, EditorIcon.ArrangePage);
            CloseButton = false;
            CloseButtonVisible = false;

            this.VisibleChanged += AnimationTree_VisibleChanged;
            this.FormClosed += AnimationTree_FormClosed;

            _editor = new AnimationNodeEditor();
            _editor.NodeNameChanged += OnNodeNameChanged;
            _editor.Show(AnimTreeEditor.DockPanel, DockState.DockRight);

            stNodeEditor1.LoadAssembly(Application.ExecutablePath);
            stNodeEditor1.AllowSameOwnerConnections = true;
            stNodeEditor1.SelectedChanged += StNodeEditor1_SelectedChanged;
            stNodeEditor1.NodesMoved += StNodeEditor1_NodesMoved;
            stNodeEditor1.OptionConnecting += StNodeEditor1_OptionConnecting;
            stNodeEditor1.OptionConnected += StNodeEditor1_OptionConnected;
            //Zoomed out past where a node's text can be read, each node is its colour with its type's icon on it, as on script pages
            stNodeEditor1.NodeIconZoom = EditorIcons.NodeIconZoom;
            stNodeEditor1.NodeIconProvider = EditorIcons.ForZoomedOutNode;
            AnimTreeLayoutManager.Changed += OnLayoutChanged;
            //Closing the whole editor window disposes its graphs without closing them: the static event must let go then too
            this.Disposed += (s, e) =>
            {
                AnimTreeLayoutManager.Changed -= OnLayoutChanged;
                if (_editor != null)
                    _editor.NodeNameChanged -= OnNodeNameChanged;
            };

            BuildAddNodeMenu();
        }

        /// <summary>The graph showing this tree in the Animation Tree Editor, if it is open there.</summary>
        internal static AnimationTreeGraph FindOpen(AnimationTree tree)
        {
            if (tree == null || AnimTreeEditor.DockPanel == null)
                return null;
            //Contents, not Documents: a graph the user floated or docked to a side is still showing the tree
            return AnimTreeEditor.DockPanel.Contents.OfType<AnimationTreeGraph>().FirstOrDefault(o => !o.IsDisposed && ReferenceEquals(o._currentTree, tree));
        }

        private void StNodeEditor1_SelectedChanged(object sender, EventArgs e)
        {
            STNode[] nodes = stNodeEditor1.GetSelectedNode();
            if (nodes.Length > 0)
                _editor.PopulateData(nodes[0].AnimationNode, _currentTree);
            else
                _editor.PopulateData(null, _currentTree);
        }

        private void AnimationTree_VisibleChanged(object sender, EventArgs e)
        {
            if (Visible && _viewPending != null && IsHandleCreated)
            {
                Action view = _viewPending;
                _viewPending = null;
                BeginInvoke(view);
            }
        }

        private void AnimationTree_FormClosed(object sender, FormClosedEventArgs e)
        {
            this.VisibleChanged -= AnimationTree_VisibleChanged;
            this.FormClosed -= AnimationTree_FormClosed;
            stNodeEditor1.SelectedChanged -= StNodeEditor1_SelectedChanged;
            stNodeEditor1.NodesMoved -= StNodeEditor1_NodesMoved;
            stNodeEditor1.OptionConnecting -= StNodeEditor1_OptionConnecting;
            stNodeEditor1.OptionConnected -= StNodeEditor1_OptionConnected;
            stNodeEditor1.NodeIconProvider = null;
            AnimTreeLayoutManager.Changed -= OnLayoutChanged;

            if (_editor != null)
            {
                _editor.NodeNameChanged -= OnNodeNameChanged;
                _editor.Close();
            }
        }

        private void OnNodeNameChanged(AnimationNode node)
        {
            if (node == null || _canvas == null)
                return;

            _canvas.Retitle(node);
            if (node is AnimationTree)
                this.Text = node.Name;

            //The layout is keyed by name: stored again under the new one
            if (AnimTreeLayoutManager.Has(_currentDb, _currentTree) || IsStoredUnder(_storedSet, _storedTree))
                StoreLayout();
            stNodeEditor1.Invalidate();
        }

        public void CommitPendingEdits()
        {
            _editor?.CommitPendingEdits();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (Visible && (keyData & Keys.KeyCode) == Keys.Delete)
            {
                if (TryDeleteHoveredLink())
                    return true;
                DeleteSelectedNodes((keyData & Keys.Shift) == Keys.Shift);
                return true;
            }
            if (Visible && keyData == Keys.F3)
            {
                GoToNextGhost(stNodeEditor1.GetSelectedNode().FirstOrDefault());
                return true;
            }
            if (Visible && keyData == (Keys.Control | Keys.C))
            {
                //The selection wins for the shortcut (the cursor could be anywhere): the node under the cursor only when nothing
                //that can be copied is selected (the tree's own node can't be)
                bool copyable = stNodeEditor1.GetSelectedNode().Any(o => o.AnimationNode != null && !(o.AnimationNode is AnimationTree));
                CopyNodes(copyable ? null : stNodeEditor1.GetHoveredNode());
                return true;
            }
            if (Visible && keyData == (Keys.Control | Keys.V))
            {
                PasteCopies(stNodeEditor1.MousePositionInCanvas);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        #region Drawing the tree
        /// <summary>Show a tree: as its stored layout has it, else laid out automatically and fitted to the view.</summary>
        public void PopulateGraph(AnimTreeDB database, AnimationTree animTree)
        {
            _currentDb = database;
            _currentTree = animTree;
            this.Text = animTree.Name;
            _canvas = new AnimTreeCanvas(stNodeEditor1, database, animTree, Singleton.Global.Animations.StringsDebug);
            _storedSet = _canvas.SetHash;
            _storedTree = _canvas.TreeHash;

            TreeLayout saved = AnimTreeLayoutManager.Get(_currentDb, _currentTree);
            _canvas.Populate(saved);
            ShowView(saved != null && saved.CanvasScale > 0 ? (Action)(() => _canvas.RestoreView(saved)) : _canvas.FitView);
        }

        /// <summary>Draw the tree again from what is stored for it (another tool changed it or its layout), keeping the view.</summary>
        internal void ReloadFromStore()
        {
            if (_canvas == null)
                return;
            PointF centre = stNodeEditor1.CanvasCenter;
            float scale = stNodeEditor1.CanvasScale;
            bool hadNodes = stNodeEditor1.Nodes.Count != 0;

            _canvas.Populate(AnimTreeLayoutManager.Get(_currentDb, _currentTree));
            _editor?.PopulateData(null, _currentTree);
            if (hadNodes)
                ShowView(() =>
                {
                    stNodeEditor1.ScaleCanvas(scale, 0, 0);
                    stNodeEditor1.CenterCanvasOn(centre.X, centre.Y, false);
                });
            else
                ShowView(_canvas.FitView);
        }

        /* Set the view now if the canvas is on screen, else once it is (after layout, as a fresh canvas starts at 1:1, top left) */
        private void ShowView(Action view)
        {
            if (IsHandleCreated && Visible)
                BeginInvoke(view);
            else
                _viewPending = view;
        }

        private void OnLayoutChanged(uint setHash, uint treeHash, object source)
        {
            if (source == this || _canvas == null || IsDisposed)
                return;
            if (setHash == _canvas.SetHash && treeHash == _canvas.TreeHash)
                ReloadFromStore();
        }

        /// <summary>The canvas as it is now becomes the tree's stored layout (written with the next save).</summary>
        private void StoreLayout()
        {
            if (_canvas == null)
                return;
            TreeLayout layout = _canvas.Capture();
            if (layout.SetHash != _storedSet || layout.TreeHash != _storedTree)
                AnimTreeLayoutManager.ForgetKey(_storedSet, _storedTree, this);
            _storedSet = layout.SetHash;
            _storedTree = layout.TreeHash;
            AnimTreeLayoutManager.Put(layout, this);
        }

        /* A tree change keeps the stored layout in step - but a tree laid out automatically stays that way */
        private void StoreLayoutIfKept()
        {
            if (AnimTreeLayoutManager.Has(_currentDb, _currentTree))
                StoreLayout();
        }

        private static bool IsStoredUnder(uint setHash, uint treeHash) => AnimTreeLayoutManager.Get(setHash, treeHash) != null;

        private void StNodeEditor1_NodesMoved(object sender, STNodesMovedEventArgs e)
        {
            if (_canvas == null || e?.Movements == null || !e.Movements.Any(o => o.OldLocation != o.NewLocation))
                return;
            StoreLayout();
        }
        #endregion

        #region Menus
        private void BuildAddNodeMenu()
        {
            addNodeToolStripMenuItem.DropDownItems.Clear();
            foreach (NodeType type in Enum.GetValues(typeof(NodeType)).Cast<NodeType>()
                .Where(t => t != NodeType.ANIM_Tree_Top_Level)
                .OrderBy(t => t.ToString()))
            {
                ToolStripMenuItem item = new ToolStripMenuItem(type.ToString())
                {
                    Tag = type
                };
                EditorIcons.Bind(item, EditorIcons.ForAnimNodeType(type));
                item.Click += AddNodeMenuItem_Click;
                addNodeToolStripMenuItem.DropDownItems.Add(item);
            }
        }

        private void NodeContextMenu_Opening(object sender, CancelEventArgs e)
        {
            _contextMenuCanvasPos = new Point(
                (int)stNodeEditor1.MousePositionInCanvas.X,
                (int)stNodeEditor1.MousePositionInCanvas.Y);

            STNode hoveredNode = stNodeEditor1.GetHoveredNode();
            (STNodeOption linkOut, STNodeOption linkIn) = stNodeEditor1.GetHoveredLink();

            bool onNode = hoveredNode != null
                && hoveredNode.AnimationNode != null
                && !(hoveredNode.AnimationNode is AnimationTree);
            bool onLink = linkIn != null && linkOut != null;
            //The tree's own node can't be deleted, ghosted or copied: over it, the menu is the empty canvas's
            bool onEmpty = !onNode && !onLink;
            bool hasCopies = onNode && _canvas != null && _canvas.CopiesOf(hoveredNode.AnimationNode).Count > 1;
            _contextMenuNode = onNode ? hoveredNode : null;

            addNodeToolStripMenuItem.Visible = onEmpty;
            arrangeTreeToolStripMenuItem.Visible = onEmpty;
            toolStripSeparatorAdd.Visible = onEmpty;
            pasteToolStripMenuItem.Visible = onEmpty;
            pasteToolStripMenuItem.Enabled = _currentTree != null && AnimTreeClipboard.HasContent;
            pasteReferenceToolStripMenuItem.Visible = onEmpty;
            pasteReferenceToolStripMenuItem.Enabled = AnimTreeClipboard.CanPasteReferencesInto(_currentTree);
            copyNodesToolStripMenuItem.Visible = onNode;
            toolStripSeparatorCopy.Visible = onNode;
            addGhostToolStripMenuItem.Visible = onNode;
            nextGhostToolStripMenuItem.Visible = hasCopies;
            deleteGhostToolStripMenuItem.Visible = hasCopies;
            deleteNodeToolStripMenuItem.Visible = onNode;
            deleteLinkToolStripMenuItem.Visible = onLink;

            if (!onEmpty && !onNode && !onLink)
                e.Cancel = true;
        }

        private void AddNodeMenuItem_Click(object sender, EventArgs e)
        {
            if (!(sender is ToolStripMenuItem item) || !(item.Tag is NodeType type))
                return;
            AddNodeOfType(type, _contextMenuCanvasPos);
        }

        private void arrangeTreeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_canvas == null)
                return;
            _canvas.ArrangeAll(AnimTreeCanvas.Origin);
            _canvas.FitView();
            StoreLayout();
        }

        private void addGhostToolStripMenuItem_Click(object sender, EventArgs e)
        {
            STNode node = stNodeEditor1.GetHoveredNode();
            if (_canvas == null || node?.AnimationNode == null)
                return;
            STNode ghost = _canvas.AddGhost(node.AnimationNode, new Point(node.Left + 40, node.Bottom + 40));
            if (ghost == null)
                return;
            stNodeEditor1.RemoveAllSelectedNodes();
            stNodeEditor1.AddSelectedNode(ghost);
            stNodeEditor1.SetActiveNode(ghost);
            StoreLayout();
            stNodeEditor1.Invalidate();
        }

        private void nextGhostToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GoToNextGhost(stNodeEditor1.GetHoveredNode());
        }

        private void deleteGhostToolStripMenuItem_Click(object sender, EventArgs e)
        {
            STNode node = stNodeEditor1.GetHoveredNode();
            if (_canvas != null && _canvas.RemoveCopy(node))
            {
                StoreLayout();
                stNodeEditor1.Invalidate();
            }
        }

        private void deleteNodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            STNode node = stNodeEditor1.GetHoveredNode();
            if (node != null)
                DeleteAnimNode(node.AnimationNode);
        }

        private void deleteLinkToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TryDeleteHoveredLink();
        }

        private void copyNodesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyNodes(_contextMenuNode);
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PasteCopies(_contextMenuCanvasPos);
        }

        private void pasteReferenceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PasteReferences(_contextMenuCanvasPos);
        }

        /// <summary>Select the next copy of a node (after this one, round to the first) and bring it into view.</summary>
        private void GoToNextGhost(STNode from)
        {
            if (_canvas == null || from?.AnimationNode == null)
                return;
            IReadOnlyList<STNode> copies = _canvas.CopiesOf(from.AnimationNode);
            if (copies.Count < 2)
                return;
            int at = -1;
            for (int i = 0; i < copies.Count; i++)
                if (copies[i] == from) at = i;
            STNode next = copies[(at + 1) % copies.Count];
            stNodeEditor1.RemoveAllSelectedNodes();
            stNodeEditor1.AddSelectedNode(next);
            stNodeEditor1.SetActiveNode(next);
            stNodeEditor1.CenterCanvasOn(next.Left + next.Width / 2f, next.Top + next.Height / 2f, true);
            _editor?.PopulateData(next.AnimationNode, _currentTree);
        }
        #endregion

        #region Links
        /* A link the user is drawing: refused before it is drawn when the tree could not hold it */
        private void StNodeEditor1_OptionConnecting(object sender, STNodeEditorOptionEventArgs e)
        {
            if (_canvas == null || _canvas.Building)
                return;
            if (!_canvas.CanDraw(e.CurrentOption, e.TargetOption, out string refusal))
            {
                e.Continue = false;
                MessageBox.Show(refusal, "Can't link these", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /* A link the user drew: the tree now holds it (or, one it held already, it is drawn from these copies now) */
        private void StNodeEditor1_OptionConnected(object sender, STNodeEditorOptionEventArgs e)
        {
            if (_canvas == null || _canvas.Building || e.Status != ConnectionStatus.Connected)
                return;
            STNodeOption a = e.CurrentOption, b = e.TargetOption;
            bool treeChanged;
            try
            {
                _canvas.ApplyDrawnLink(a, b, out treeChanged);
            }
            catch (InvalidOperationException ex)
            {
                STNodeOption output = a.Location == PinLocation.Right || a.Location == PinLocation.Bottom ? a : b;
                output.DisconnectOption(output == a ? b : a);
                stNodeEditor1.Invalidate();
                MessageBox.Show(ex.Message, "Can't link these", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            //Moving a link between copies is a layout change; a new link changes the tree, and the layout only if it is kept
            bool onGhost = _canvas.CopyIndexOf(a.Owner) > 0 || _canvas.CopyIndexOf(b.Owner) > 0;
            if (!treeChanged || onGhost)
                StoreLayout();
            else
                StoreLayoutIfKept();
            STNode[] selected = stNodeEditor1.GetSelectedNode();
            _editor?.PopulateData(selected.Length > 0 ? selected[0].AnimationNode : null, _currentTree);
            stNodeEditor1.Invalidate();
        }

        private bool TryDeleteHoveredLink()
        {
            (STNodeOption output, STNodeOption input) = stNodeEditor1.GetHoveredLink();
            if (output == null || input == null)
                return false;

            ClearLinkInModel(output, input);
            output.DisconnectOption(input);
            StoreLayoutIfKept();
            stNodeEditor1.Invalidate();
            return true;
        }

        private void ClearLinkInModel(STNodeOption output, STNodeOption input)
        {
            AnimationNode from = output?.Owner?.AnimationNode;
            AnimationNode to = input?.Owner?.AnimationNode;
            if (from == null || to == null)
                return;

            // Flow links: right output -> left trigger
            if (input.Location == PinLocation.Left)
            {
                ClearFlowChildReference(from, to, output.ShortGUID);
                return;
            }

            // Value links: bottom value -> top pin (ConnectionInfo Output is the bottom/provider side)
            if (input.Location == PinLocation.Top)
                ClearTopPinReference(to, from, input.ShortGUID);
            else if (output.Location == PinLocation.Top)
                ClearTopPinReference(from, to, output.ShortGUID);
        }

        private static void ClearFlowChildReference(AnimationNode parent, AnimationNode child, ShortGuid outputPin)
        {
            if (outputPin == ShortGuids.NODES)
            {
                parent.Children.Remove(child);
                return;
            }
            if (outputPin == ShortGuids.base_node && parent is AdditiveBlendNode additiveBase)
            {
                if (ReferenceEquals(additiveBase.BaseNode, child)) additiveBase.BaseNode = null;
                return;
            }
            if (outputPin == ShortGuids.additive_node && parent is AdditiveBlendNode additiveAdd)
            {
                if (ReferenceEquals(additiveAdd.AdditiveNode, child)) additiveAdd.AdditiveNode = null;
                return;
            }
            if (outputPin == ShortGuids.child && parent is WeightedNode weighted)
            {
                if (ReferenceEquals(weighted.Child, child)) weighted.Child = null;
                return;
            }
            if (outputPin == ShortGuids.LeftStrikeChild && parent is FootSyncSelectorNode footLeft)
            {
                if (ReferenceEquals(footLeft.LeftStrikeChild, child)) footLeft.LeftStrikeChild = null;
                return;
            }
            if (outputPin == ShortGuids.RightStrikeChild && parent is FootSyncSelectorNode footRight)
            {
                if (ReferenceEquals(footRight.RightStrikeChild, child)) footRight.RightStrikeChild = null;
                return;
            }

            if (parent is SelectorNode selector && selector.States != null)
            {
                for (int i = 0; i < selector.States.Length; i++)
                {
                    if (i < ShortGuids.States.Length
                        && outputPin == ShortGuids.States[i] && selector.States[i] != null
                        && ReferenceEquals(selector.States[i].Node, child))
                    {
                        selector.States[i].Node = null;
                        return;
                    }
                }
            }
            if (parent is ParametricNode parametric && parametric.States != null)
            {
                for (int i = 0; i < parametric.States.Length; i++)
                {
                    if (i < ShortGuids.States.Length
                        && outputPin == ShortGuids.States[i] && parametric.States[i] != null
                        && ReferenceEquals(parametric.States[i].Node, child))
                    {
                        parametric.States[i].Node = null;
                        return;
                    }
                }
            }
            if (parent is RangedSelectorNode ranged && ranged.States != null)
            {
                for (int i = 0; i < ranged.States.Length; i++)
                {
                    if (i < ShortGuids.States.Length
                        && outputPin == ShortGuids.States[i] && ranged.States[i] != null
                        && ReferenceEquals(ranged.States[i].Node, child))
                    {
                        ranged.States[i].Node = null;
                        return;
                    }
                }
            }
        }

        private static void ClearTopPinReference(AnimationNode consumer, AnimationNode provider, ShortGuid topPin)
        {
            if (topPin == ShortGuids.Callback)
            {
                if (consumer is LeafNode leaf && ReferenceEquals(leaf.Callback, provider)) leaf.Callback = null;
                if (consumer is RandomisedLeafNode rand && ReferenceEquals(rand.Callback, provider)) rand.Callback = null;
                return;
            }
            if (topPin == ShortGuids.RandomCallback && consumer is RandomisedLeafNode randCb)
            {
                if (ReferenceEquals(randCb.RandomCallback, provider)) randCb.RandomCallback = null;
                return;
            }
            if (topPin == ShortGuids.ParameterBinding)
            {
                if (consumer is SelectorNode selector && ReferenceEquals(selector.ParameterBinding, provider))
                    selector.ParameterBinding = null;
                if (consumer is ParametricNode parametric && ReferenceEquals(parametric.ParameterBinding, provider))
                    parametric.ParameterBinding = null;
                if (consumer is RangedSelectorNode ranged && ReferenceEquals(ranged.ParameterBinding, provider))
                    ranged.ParameterBinding = null;
                return;
            }
            if (topPin == ShortGuids.ParameterBindingX && consumer is Parametric2DNode p2x)
            {
                if (ReferenceEquals(p2x.ParameterBindingX, provider)) p2x.ParameterBindingX = null;
                return;
            }
            if (topPin == ShortGuids.ParameterBindingY && consumer is Parametric2DNode p2y)
            {
                if (ReferenceEquals(p2y.ParameterBindingY, provider)) p2y.ParameterBindingY = null;
                return;
            }
            if (topPin == ShortGuids.ParameterBindingZ && consumer is Parametric3DNode p3z)
            {
                if (ReferenceEquals(p3z.ParameterBindingZ, provider)) p3z.ParameterBindingZ = null;
                return;
            }
            if (topPin == ShortGuids.OverflowCallback && consumer is Parametric2DNode overflow)
            {
                if (ReferenceEquals(overflow.OverflowCallback, provider)) overflow.OverflowCallback = null;
                return;
            }
            if (topPin == ShortGuids.IkEffector && consumer is IkNode ik)
            {
                if (ReferenceEquals(ik.IkEffector, provider)) ik.IkEffector = null;
                return;
            }
            if (topPin == ShortGuids.WeightControlParameter && consumer is ParametricAdditiveBlendNode weight)
            {
                if (ReferenceEquals(weight.WeightControlParameter, provider)) weight.WeightControlParameter = null;
                return;
            }
            if (topPin == ShortGuids.Parameter && consumer is WeightedNode weighted)
            {
                if (ReferenceEquals(weighted.Parameter, provider)) weighted.Parameter = null;
                return;
            }
            if (topPin == ShortGuids.SourceParameter && consumer is FloatInterpolatorNode interp)
            {
                if (ReferenceEquals(interp.SourceParameter, provider)) interp.SourceParameter = null;
                return;
            }
            if (topPin == ShortGuids.LeafNode && consumer is PropertyListenerNode listener)
            {
                if (ReferenceEquals(listener.LeafNode, provider)) listener.LeafNode = null;
            }
        }
        #endregion

        #region Nodes
        /* Del: each selected copy goes, and a node with its last copy. Shift+Del: the selected nodes and all their copies. */
        private void DeleteSelectedNodes(bool allCopies)
        {
            STNode[] selected = stNodeEditor1.GetSelectedNode();
            if (selected == null || selected.Length == 0 || _canvas == null)
                return;

            bool layoutChanged = false;
            foreach (STNode node in selected.ToArray())
            {
                if (node.AnimationNode == null || node.AnimationNode is AnimationTree || node.Owner != stNodeEditor1)
                    continue;
                if (!allCopies && _canvas.RemoveCopy(node))
                {
                    layoutChanged = true;
                    continue;
                }
                DeleteAnimNode(node.AnimationNode, store: false);
            }
            if (layoutChanged)
                StoreLayout();
            else
                StoreLayoutIfKept();
            if (_editor != null)
                _editor.PopulateData(null, _currentTree);
            stNodeEditor1.Invalidate();
        }

        /* The node out of the tree, and every copy of it off the canvas */
        private void DeleteAnimNode(AnimationNode animNode, bool store = true)
        {
            if (animNode == null || animNode is AnimationTree || _canvas == null)
                return; // never delete the tree root from the graph

            _currentTree?.RemoveNode(animNode);
            _canvas.RemoveAllCopies(animNode);
            if (store)
            {
                StoreLayoutIfKept();
                if (_editor != null)
                    _editor.PopulateData(null, _currentTree);
            }
        }

        /* Copy the selected nodes - or, given a node outside the selection (one right-clicked), just that one, as the
           scripting flowgraph does. The tree's own node is never copied. */
        private void CopyNodes(STNode contextNode = null)
        {
            if (_currentTree == null)
                return;
            List<STNode> nodes = stNodeEditor1.GetSelectedNode().ToList();
            if (contextNode != null && !nodes.Contains(contextNode))
                nodes = new List<STNode>() { contextNode };
            AnimTreeClipboard.Copy(_currentTree, nodes);
        }

        /* Paste new nodes: a copy of each node copied (once, however many of its ghosts were), named to be unique in this
           tree, laid out as the copied ones were from where it is pasted, with the links that ran between them */
        private void PasteCopies(PointF canvasPosition)
        {
            if (_currentTree == null || _canvas == null)
                return;
            List<AnimTreeClipboard.Entry> entries = AnimTreeClipboard.LiveNodes();
            if (entries.Count == 0)
                return;

            //An animation's context and converge parameters are settings, not links (nothing draws them, so nothing could draw
            //them again): kept - the same node in the tree copied from, else the node of that name and type in this one
            AnimationTree into = _currentTree;
            bool sameTree = ReferenceEquals(AnimTreeClipboard.SourceTree, into);
            Func<AnimationNode, AnimationNode> setting = o => sameTree
                ? into.Nodes.FirstOrDefault(n => ReferenceEquals(n, o))
                : into.Nodes.FirstOrDefault(n => n != null && n.Name == o.Name && n.Type == o.Type);

            Dictionary<AnimationNode, AnimationNode> copies = AnimNodeCopier.Copy(entries.Select(o => o.Node), setting);
            List<(AnimationNode node, Point at)> placed = new List<(AnimationNode, Point)>();
            foreach (AnimTreeClipboard.Entry entry in entries)
            {
                AnimationNode copy = copies[entry.Node];
                copy.Name = MakeUniqueNodeName(entry.Node.Name);
                if (!ReferenceEquals(_currentTree.AddNode(copy), copy))
                    continue; //the tree kept a node it had under that name instead: never draw one it doesn't hold
                placed.Add((copy, new Point((int)canvasPosition.X + entry.Offset.X, (int)canvasPosition.Y + entry.Offset.Y)));
            }
            SelectPasted(_canvas.AddNodes(placed));
            StoreLayoutIfKept();
        }

        /* Paste ghosts of the copied nodes themselves (the tree they were copied from only), laid out as the copies were */
        private void PasteReferences(PointF canvasPosition)
        {
            if (_canvas == null || !AnimTreeClipboard.CanPasteReferencesInto(_currentTree))
                return;
            List<STNode> ghosts = new List<STNode>();
            foreach (AnimTreeClipboard.Entry entry in AnimTreeClipboard.Live())
            {
                STNode ghost = _canvas.AddGhost(entry.Node, new Point((int)canvasPosition.X + entry.Offset.X, (int)canvasPosition.Y + entry.Offset.Y));
                if (ghost != null)
                    ghosts.Add(ghost);
            }
            if (ghosts.Count == 0)
                return;
            SelectPasted(ghosts);
            StoreLayout();
        }

        /* What was just pasted becomes the selection (the node editor shows it when it is one node) */
        private void SelectPasted(List<STNode> pasted)
        {
            if (pasted == null || pasted.Count == 0)
                return;
            stNodeEditor1.RemoveAllSelectedNodes();
            foreach (STNode node in pasted)
                stNodeEditor1.AddSelectedNode(node);
            stNodeEditor1.SetActiveNode(pasted[0]);
            _editor?.PopulateData(pasted.Count == 1 ? pasted[0].AnimationNode : null, _currentTree);
            stNodeEditor1.Invalidate();
        }

        private STNode AddNodeOfType(NodeType type, Point canvasPosition)
        {
            if (_currentTree == null || _canvas == null)
            {
                MessageBox.Show("Open an animation tree first.", "No tree loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }

            AnimationNode animNode = CreateAnimationNodeInstance(type);
            if (animNode == null)
                return null;

            animNode.Name = MakeUniqueNodeName(SuggestNodeName(type));
            if (!ReferenceEquals(_currentTree.AddNode(animNode), animNode))
                return null;

            STNode node = _canvas.AddNode(animNode, canvasPosition);
            if (node == null)
                return null;
            stNodeEditor1.RemoveAllSelectedNodes();
            stNodeEditor1.AddSelectedNode(node);
            stNodeEditor1.SetActiveNode(node);
            _editor?.PopulateData(animNode, _currentTree);
            StoreLayoutIfKept();
            return node;
        }

        private string SuggestNodeName(NodeType type)
        {
            string name = type.ToString();
            if (name.StartsWith("ANIM_"))
                name = name.Substring(5);
            return name;
        }

        private string MakeUniqueNodeName(string baseName)
        {
            if (_currentTree == null)
                return baseName;

            //Against every node's name, whatever its type: the tree's name lookup holds one node per name, and some trees
            //repeat names across types
            bool Taken(string candidate) => _currentTree.TryGetNode(candidate, out _) || _currentTree.Nodes.Any(o => o != null && o.Name == candidate);
            string name = baseName;
            int i = 1;
            while (Taken(name))
            {
                name = baseName + "_" + i;
                i++;
            }
            return name;
        }

        internal static AnimationNode CreateAnimationNodeInstance(NodeType type)
        {
            switch (type)
            {
                case NodeType.ANIM_Animation:
                    return new LeafNode();
                case NodeType.ANIM_Randomised_Animation:
                    return new RandomisedLeafNode();
                case NodeType.ANIM_Metadata_Event_Listener:
                    return new MetadataListenerNode();
                case NodeType.ANIM_Parameter:
                    return new ParameterNode();
                case NodeType.ANIM_AutoFloatParameter:
                    return new ParameterNode { Type = NodeType.ANIM_AutoFloatParameter };
                case NodeType.ANIM_FloatInterpolator:
                    return new FloatInterpolatorNode();
                case NodeType.ANIM_Property:
                    return new PropertyNode();
                case NodeType.ANIM_Property_Listener:
                    return new PropertyListenerNode();
                case NodeType.ANIM_Selector:
                    return new SelectorNode();
                case NodeType.ANIM_Enumerated_Selector:
                    return new SelectorNode { Type = NodeType.ANIM_Enumerated_Selector };
                case NodeType.ANIM_Ranged_Selector:
                    return new RangedSelectorNode();
                case NodeType.ANIM_Foot_Sync_Selector:
                    return new FootSyncSelectorNode();
                case NodeType.ANIM_Parametric:
                    return new ParametricNode();
                case NodeType.ANIM_2DParametric:
                    return new Parametric2DNode();
                case NodeType.ANIM_3DParametric:
                    return new Parametric3DNode();
                case NodeType.ANIM_4DParametric:
                    return new Parametric4DNode();
                case NodeType.ANIM_Additive_Blend:
                    return new AdditiveBlendNode();
                case NodeType.ANIM_Parametric_Additive_Blend:
                    return new ParametricAdditiveBlendNode();
                case NodeType.ANIM_Bone_Mask:
                    return new BoneMaskNode();
                case NodeType.ANIM_IK:
                    return new IkNode();
                case NodeType.ANIM_Weighted:
                    return new WeightedNode();
                case NodeType.ANIM_Callback:
                case NodeType.ANIM_Event_Callback:
                case NodeType.ANIM_Bilinear_High_Fidelity:
                case NodeType.ANIM_Bilinear_Low_Fidelity:
                case NodeType.ANIM_Spherical:
                    return new AnimationNode { Type = type };
                default:
                    return new AnimationNode { Type = type };
            }
        }
        #endregion
    }
}
