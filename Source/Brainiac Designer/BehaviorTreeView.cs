////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2009, Daniel Kollmann
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification, are permitted
// provided that the following conditions are met:
//
// - Redistributions of source code must retain the above copyright notice, this list of conditions
//   and the following disclaimer.
//
// - Redistributions in binary form must reproduce the above copyright notice, this list of
//   conditions and the following disclaimer in the documentation and/or other materials provided
//   with the distribution.
//
// - Neither the name of Daniel Kollmann nor the names of its contributors may be used to endorse
//   or promote products derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR
// IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
// WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY
// WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Data;
using System.Text;
using System.Windows.Forms;
using Brainiac.Design.Nodes;
using Brainiac.Design.Properties;

namespace Brainiac.Design
{
	/// <summary>
	/// This control shows a graph of a behaviour and allows you to edit it.
	/// </summary>
	internal partial class BehaviorTreeView : UserControl
	{
		public BehaviorTreeView()
		{
			InitializeComponent();
		}

		/// <summary>
		/// Registers referenced behaviours so when a referenced behaviour changes, this view gets updated, as the root node will not change.
		/// </summary>
		/// <param name="node">The node we want to register on and its children.</param>
		private void RegisterReferencedBehaviors(Node node)
		{
			// check if this is a referenced behaviour. If so register the view on the ReferencedBehaviorWasModified event.
			if(node is ReferencedBehaviorNode)
			{
				ReferencedBehaviorNode refnode= (ReferencedBehaviorNode) node;
				refnode.ReferencedBehaviorWasModified+= new ReferencedBehavior.ReferencedBehaviorWasModifiedEventDelegate(refnode_ReferencedBehaviorWasModified);
			}

			// check the children
			foreach(Node child in node.Children)
				RegisterReferencedBehaviors(child);
		}

		/// <summary>
		/// Holds the instance of the error check dialogue for this view/behaviour.
		/// </summary>
		private static ErrorCheckDialog _errorCheckDialog= null;

		/// <summary>
		/// The node layout manager used to layout the behaviour and its children.
		/// </summary>
		private NodeLayoutManager _nodeLayoutManager;

		/// <summary>
		/// Forces the view to be completely redrawn.
		/// </summary>
		internal void UpdateLayout()
		{
			_nodeLayoutManager.MarkLayoutChanged();
			Invalidate();
		}

		private NodeViewData _rootNode;

		/// <summary>
		/// The behaviour visualised in this view.
		/// </summary>
		public Nodes.BehaviorNode RootNode
		{
			get { return _rootNode.RootBehavior; }

			set
			{
				// assign the new behaviour
				_rootNode= ((Node)value).CreateNodeViewData(null, value);
				_rootNode.WasModified+= new NodeViewData.WasModifiedEventDelegate(nodeViewData_WasModified);
				_nodeLayoutManager= new NodeLayoutManager(_rootNode, Pens.Blue, Pens.Red, false);

				// register the view to be updated when any referenced behaviour changes
				RegisterReferencedBehaviors(_rootNode.Node);

				// automtically centre the behaviour
				_pendingCentreBehavior= true;
			}
		}

		/// <summary>
		/// Handles when the root node changes.
		/// </summary>
		/// <param name="node">The node which was changed, in our case always the root node.</param>
		void nodeViewData_WasModified(NodeViewData node)
		{
			LayoutChanged();
		}

		/// <summary>
		/// The last known transformed mouse position.
		/// </summary>
		private PointF _lastMousePosition;

		/// <summary>
		/// Used to prevent any movment in the graph when the focus was lost.
		/// </summary>
		private bool _lostFocus= false;

		/// <summary>
		/// Defines if the last mouse position should be kept.
		/// </summary>
		private bool _maintainMousePosition= false;

		/// <summary>
		/// Defines if the position of a node should be kept.
		/// </summary>
		private NodeViewData _maintainNodePosition= null;

		private NodeViewData _selectedNode= null;

		/// <summary>
		/// The currently selected node.
		/// </summary>
		public NodeViewData SelectedNode
		{
			get { return _selectedNode; }

			set
			{
				if(_selectedNode !=value)
				{
					// set new selected node and update the graph
					_selectedNode= value;

					propertiesButton.Enabled= _selectedNode !=null;

					Invalidate();
				}
			}
		}

		/// <summary>
		/// Stores the node you want to be selected when no layout currenty exists for the node.
		/// </summary>
		private Node _selectedNodePending= null;

		/// <summary>
		/// Stores the parent of the node you want to be selected when no layout currenty exists for the node.
		/// </summary>
		private NodeViewData _selectedNodePendingParent= null;

		/// <summary>
		/// The node the mouse is currently hovering over.
		/// </summary>
		private NodeViewData _currentNode= null;

		/// <summary>
		/// The defaults of the node which is dragged in from the node explorer. This is needed for the child mode data.
		/// </summary>
		private NodeTag.DefaultObject _dragNodeDefaults= null;

		/// <summary>
		/// The node another node is dragged over which should always be the same as _currentNode.
		/// </summary>
		private NodeViewData _dragTargetNode= null;

		/// <summary>
		/// The connector another node is dragged over.
		/// </summary>
		private Node.Connector _dragTargetConnector= null;

		/// <summary>
		/// The way the dragged node is supposed to be attached to the _dragTargetNode.
		/// </summary>
		private NodeAttachMode _dragAttachMode= NodeAttachMode.None;

		private BehaviorTreeList _behaviorTreeList;

		/// <summary>
		/// The BehaviorTreeList of the editor which manages all the behaviours.
		/// </summary>
		internal BehaviorTreeList BehaviorTreeList
		{
			get { return _behaviorTreeList; }

			set
			{
				_behaviorTreeList= value;

				// update the status of the buttons
				saveButton.Enabled= _behaviorTreeList.HasFileManagers();
				saveAsButton.Enabled= _behaviorTreeList.HasFileManagers();
				exportButton.Enabled= _behaviorTreeList.HasExporters();
			}
		}

		/// <summary>
		/// The pen which is used to draw the edges connecting the nodes.
		/// </summary>
		internal Pen EdgePen
		{
			get { return _nodeLayoutManager.EdgePen; }
			set { _nodeLayoutManager.EdgePen= value; }
		}

		/// <summary>
		/// The pen which is used to draw the edges connecting sub-referenced nodes.
		/// </summary>
		internal Pen EdgePenReadOnly
		{
			get { return _nodeLayoutManager.EdgePenReadOnly; }
			set { _nodeLayoutManager.EdgePenReadOnly= value; }
		}

		/// <summary>
		/// The padding between the nodes.
		/// </summary>
		internal SizeF NodePadding
		{
			get { return _nodeLayoutManager.Padding; }
			set { _nodeLayoutManager.Padding= value; }
		}

		/// <summary>
		/// Marks the current layout as obsolete and causes the view to redraw the graph.
		/// </summary>
		private void LayoutChanged()
		{
			_nodeLayoutManager.MarkLayoutChanged();
			Invalidate();
		}

		/// <summary>
		/// The way a dragged node is supposed to be attached to another node.
		/// </summary>
		private enum NodeAttachMode { None, Left, Right, Top, Bottom, Event };

		/// <summary>
		/// Attaches a dragged node from the node explorer to an existing node.
		/// </summary>
		/// <param name="nvd">The node the new node will be attached to.</param>
		/// <param name="mode">The way the new node will be attached.</param>
		/// <param name="nodetag">The tag of the you want to create.</param>
		/// <param name="label">The label of the new node.</param>
		private void InsertNewNode(NodeViewData nvd, NodeAttachMode mode, NodeTag nodetag)
		{
			// check if the attach mode is valid
			if(mode ==NodeAttachMode.Event || mode ==NodeAttachMode.None)
				throw new Exception("A node cannot be created with the given attach mode");

			if(nodetag.Type !=NodeTagType.Behavior && nodetag.Type !=NodeTagType.Node)
				throw new Exception("Only behaviours and nodes can be attached to a behaviour tree");

			Node node= nvd.Node;

			Node newnode;
			// when we attach a behaviour we must create a special referenced behaviour node
			if(nodetag.Type ==NodeTagType.Behavior)
			{
				// reset any previously loaded behaviour
				FileManagers.FileManager.ResetLoadedBehavior();

				// get the behaviour we want to reference
				BehaviorNode behavior= _behaviorTreeList.LoadBehavior(nodetag.Filename);

				// a behaviour may not reference itself
				if(behavior ==_rootNode.RootBehavior)
					return;

				// create the referenced behaviour node for the behaviour
				ReferencedBehavior refnode= new ReferencedBehavior(_rootNode.RootBehavior, behavior);

				// register the view so it gets updated when the referenced behaviour gets updated.
				refnode.ReferencedBehaviorWasModified+= new ReferencedBehavior.ReferencedBehaviorWasModifiedEventDelegate(refnode_ReferencedBehaviorWasModified);

				newnode= refnode;
			}
			else
			{
				// simply create the node which is supposed to be created.
				newnode= Node.Create(nodetag.NodeType);
			}

			// update label
			newnode.OnPropertyValueChanged(false);

			// attach the new node with the correct mode
			switch(mode)
			{
				// the new node is inserted in front of the target node
				case(NodeAttachMode.Left):
					Node parent= node.Parent;

					int k= parent.Children.IndexOf(node);

					Node.Connector conn= node.ParentConnector;
					Debug.Check(conn !=null);

					parent.RemoveChild(conn, node);
					parent.AddChild(conn, newnode, k);

					Node.Connector newconn= newnode.GetConnector(conn.Identifier);
					Debug.Check(newconn !=null);
					newnode.AddChild(newconn, node);

					// automatically select the new node
					_selectedNodePending= newnode;
					_selectedNodePendingParent= nvd.Parent;
				break;

				// the new node is simply added to the target node's children
				case(NodeAttachMode.Right):
					node.AddChild(_dragTargetConnector, newnode);

					// automatically select the new node
					_selectedNodePending= newnode;
					_selectedNodePendingParent= nvd;
				break;

				// the new node is placed above the target node
				case(NodeAttachMode.Top):
					int n= _dragTargetNode.Node.ParentConnector.GetChildIndex(node);
					node.Parent.AddChild(_dragTargetNode.Node.ParentConnector, newnode, n);

					// automatically select the new node
					_selectedNodePending= newnode;
					_selectedNodePendingParent= nvd.Parent;
				break;

				// the new node is placed below the target node
				case(NodeAttachMode.Bottom):
					int m= _dragTargetNode.Node.ParentConnector.GetChildIndex(node);
					node.Parent.AddChild(_dragTargetNode.Node.ParentConnector, newnode, m +1);

					// automatically select the new node
					_selectedNodePending= newnode;
					_selectedNodePendingParent= nvd.Parent;
				break;
			}

			// the layout needs to be recalculated
			LayoutChanged();
		}

		/// <summary>
		/// Handles when a referenced behaviour is modified.
		/// </summary>
		/// <param name="node">The referenced behaviour node whose referenced behaviour is modified.</param>
		void refnode_ReferencedBehaviorWasModified(ReferencedBehaviorNode node)
		{
			LayoutChanged();
		}

		/// <summary>
		/// The position which will be kept when _maintainMousePosition or _maintainNodePosition is set.
		/// </summary>
		private PointF _graphOrigin= new PointF(0.0f, 0.0f);

		/// <summary>
		/// Handles the drawing and updating of the graph.
		/// </summary>
		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			// calculate the mouse position in the graph
			PointF graphMousePos= _nodeLayoutManager.ViewToGraph(_lastMousePosition);

			// when the layout was changed it needs to be recalculated
			bool layoutChanged= _nodeLayoutManager.LayoutChanged;
			if(layoutChanged)
				_nodeLayoutManager.UpdateLayout(e.Graphics);

			// centre the root behaviour if requested
			if(_pendingCentreBehavior)
			{
				_pendingCentreBehavior= false;

				CenterNode(_rootNode);
			}

			// select the pending node
			if(_selectedNodePending !=null)
			{
				SelectedNode= _selectedNodePendingParent.GetChild(_selectedNodePending);

				_selectedNodePending= null;
				_selectedNodePendingParent= null;

				if(ClickNode !=null)
					ClickNode(_selectedNode);
			}

			// check if we must keep the original position of the mouse
			if(_maintainMousePosition)
			{
				_maintainMousePosition= false;

				// move the graph so that _graphOrigin is at the same position in the view as it was before
				float mouseX= (graphMousePos.X - _graphOrigin.X) * _nodeLayoutManager.Scale + _nodeLayoutManager.Offset.X;
				float mouseY= (graphMousePos.Y - _graphOrigin.Y) * _nodeLayoutManager.Scale + _nodeLayoutManager.Offset.Y;
				_nodeLayoutManager.Offset= new PointF(mouseX, mouseY);
			}
			// check if we must keep the original position of _maintainNodePosition
			else if(_maintainNodePosition !=null)
			{
				// move the graph so that _graphOrigin is at the same position in the view as it was before
				RectangleF bbox= _maintainNodePosition.BoundingBox;

				PointF viewpos= new PointF(bbox.Location.X * _nodeLayoutManager.Scale, bbox.Location.Y * _nodeLayoutManager.Scale);

				_nodeLayoutManager.Offset= new PointF(_graphOrigin.X - viewpos.X, _graphOrigin.Y - viewpos.Y);
			}

			// reset the node whose position we want to keep
			_maintainNodePosition= null;

			// draw the graph to the view
			_nodeLayoutManager.DrawGraph(e.Graphics, _currentNode, _selectedNode, graphMousePos);

			// check if we are currently dragging a node and we must draw additional data
			if(_dragTargetNode !=null && _dragTargetNode.Node !=_movedNode)
			{
				if(_dragAttachMode == NodeAttachMode.Event)
				{
					// we could draw some stuff for events here
				}
				else
				{
					// draw the arrows for the attach modes

					// get the bounding box of the node
					RectangleF bbox= _dragTargetNode.BoundingBox;

					// get the bounding box of the connector
					_dragTargetConnector= null;

					// the depth of the area for the mouse
					const float offset= 20.0f;

					// the distance of the arrow from the border and its height
					const float innerOffset= 6.0f;

					// the horizintal middle of the node
					float centerX= bbox.Left + bbox.Width *0.5f;

					// the half width of the arrow depending of the node's height
					float arrowHalfWidth= (bbox.Height - innerOffset - innerOffset) *0.5f;

					// calculate the mouse areas for the different attach modes
					RectangleF top= new RectangleF(bbox.X + offset, bbox.Top, bbox.Width - offset - offset, offset);
					RectangleF bottom= new RectangleF(bbox.X + offset, bbox.Bottom - offset, bbox.Width - offset - offset, offset);
					RectangleF left= new RectangleF(bbox.X, bbox.Y, offset, bbox.Height);

					// update for dragging in a new node
					BehaviorNode behavior= _dragNodeDefaults as BehaviorNode;
					if(behavior !=null && behavior.FileManager ==null)
						behavior= null;

					bool hasParentBehavior= _dragTargetNode.HasParentBehavior(behavior);
					bool parentHasParentBehavior= _dragTargetNode.Parent !=null && _dragTargetNode.Parent.HasParentBehavior(behavior);

					bool mayTop= _dragTargetNode.Node.Parent !=null && !parentHasParentBehavior && _dragTargetNode.Node.ParentConnector.AcceptsChildren(1);

					bool mayBottom= _dragTargetNode.Node.Parent !=null && !parentHasParentBehavior && _dragTargetNode.Node.ParentConnector.AcceptsChildren(1);

					bool mayLeft= _dragTargetNode.Parent !=null && !parentHasParentBehavior && !hasParentBehavior &&
									!_dragTargetNode.Node.ParentConnector.IsReadOnly &&
									_dragNodeDefaults is Node &&
									((Node)_dragNodeDefaults).CanAdoptNode(_dragTargetNode.Node) &&
									!(_dragNodeDefaults.GetType() is BehaviorNode);

					// update for moving an existing node
					bool dragTargetHasParentMovedNode= false;
					if(_movedNode !=null)
					{
						dragTargetHasParentMovedNode= _keyShiftIsDown && _dragTargetNode.HasParent(_movedNode);

						// a node may not dragged on itself and may not dragged on one of its own children
						if(_dragTargetNode.Node ==_movedNode || dragTargetHasParentMovedNode)
						{
							mayTop= false;
							mayBottom= false;
							mayLeft= false;
						}
						else
						{
							// a dragged node cannot be placed in the same position again
							mayTop= mayTop && _dragTargetNode.Node.PreviousNode !=_movedNode;
							mayBottom= mayBottom && _dragTargetNode.Node.NextNode !=_movedNode;
							mayLeft= mayLeft && _movedNode.CanAdoptChildren(_dragTargetNode.Node) && (!_keyShiftIsDown || _movedNode.Children.Count ==0);
						}
					}
					else if(_copiedNode !=null)
					{
						mayLeft= mayLeft && _copiedNode.CanAdoptChildren(_dragTargetNode.Node) && (!_keyShiftIsDown || _copiedNode.Children.Count ==0);
					}

					// reset the attach mode
					_dragAttachMode= NodeAttachMode.None;

					// the vertices needed to draw the arrows
					PointF[] vertices= new PointF[3];

					// draw the top arrow if this action is allowed
					if(mayTop)
					{
						vertices[0]= new PointF(centerX - arrowHalfWidth, top.Bottom - innerOffset);
						vertices[1]= new PointF(centerX, top.Top + innerOffset);
						vertices[2]= new PointF(centerX + arrowHalfWidth, top.Bottom - innerOffset);
						if(top.Contains(graphMousePos))
						{
							_dragAttachMode= NodeAttachMode.Top;
							e.Graphics.FillPolygon(Brushes.White, vertices);
						}
						else e.Graphics.FillPolygon(Brushes.Black, vertices);
					}

					// draw the bottom arrow if this action is allowed
					if(mayBottom)
					{
						vertices[0]= new PointF(centerX - arrowHalfWidth, bottom.Top + innerOffset);
						vertices[1]= new PointF(centerX + arrowHalfWidth, bottom.Top + innerOffset);
						vertices[2]= new PointF(centerX, bottom.Bottom - innerOffset);
						if(_dragAttachMode ==NodeAttachMode.None && bottom.Contains(graphMousePos))
						{
							_dragAttachMode= NodeAttachMode.Bottom;
							e.Graphics.FillPolygon(Brushes.White, vertices);
						}
						else e.Graphics.FillPolygon(Brushes.Black, vertices);
					}

					// draw the left arrow if this action is allowed
					if(mayLeft)
					{
						vertices[0]= new PointF(left.Right - innerOffset, left.Top + innerOffset);
						vertices[1]= new PointF(left.Right - innerOffset, left.Bottom - innerOffset);
						vertices[2]= new PointF(left.Left + innerOffset, left.Top + left.Height *0.5f);
						if(_dragAttachMode ==NodeAttachMode.None && left.Contains(graphMousePos))
						{
							_dragAttachMode= NodeAttachMode.Left;
							e.Graphics.FillPolygon(Brushes.White, vertices);
						}
						else e.Graphics.FillPolygon(Brushes.Black, vertices);
					}

					// draw the right arrow if this action is allowed
					foreach(Node.Connector connector in _dragTargetNode.Node.Connectors)
					{
						RectangleF bboxConnector= _dragTargetNode.Node.GetConnectorBoundingBox(bbox, connector);

						//e.Graphics.DrawRectangle(Pens.Red, bboxConnector.X, bboxConnector.Y, bboxConnector.Width, bboxConnector.Height);

						RectangleF right= new RectangleF(bboxConnector.Right - offset, bboxConnector.Y, offset, bboxConnector.Height);

						bool mayRight= !dragTargetHasParentMovedNode && !hasParentBehavior && connector.AcceptsChildren(1);

						if(mayRight && _movedNode !=null && connector ==_movedNode.ParentConnector)
							mayRight= false;

						if(mayRight)
						{
							float inOffset= bboxConnector.Height >innerOffset *4.0f ? innerOffset : 3.0f;

							vertices[0]= new PointF(right.Left + inOffset, right.Top + inOffset);
							vertices[1]= new PointF(right.Right - inOffset, right.Top + right.Height *0.5f);
							vertices[2]= new PointF(right.Left + inOffset, right.Bottom - inOffset);
							if(_dragAttachMode ==NodeAttachMode.None && right.Contains(graphMousePos))
							{
								_dragTargetConnector= connector;
								_dragAttachMode= NodeAttachMode.Right;
								e.Graphics.FillPolygon(Brushes.White, vertices);
							}
							else e.Graphics.FillPolygon(Brushes.Black, vertices);
						}
					}
				}
			}

			// draw last mouse pos
			//e.Graphics.DrawRectangle(Pens.Red, graphMousePos.X -1.0f, graphMousePos.Y -1.0f, 2.0f, 2.0f);

			//when we are dragging an existing node we draw a small graph representing it
			if(_movedNodeGraph !=null)
			{
				// update the layout for the graph. This happens only once inside the function.
				_movedNodeGraph.UpdateLayout(e.Graphics);

				// offset the graph to the mouse position
				_movedNodeGraph.Offset= new PointF(_nodeLayoutManager.Offset.X + graphMousePos.X * _nodeLayoutManager.Scale,
										_nodeLayoutManager.Offset.Y + graphMousePos.Y * _nodeLayoutManager.Scale - _movedNodeGraph.RootNodeLayout.LayoutRectangle.Height *0.5f * _movedNodeGraph.Scale);

				// draw the graph
				_movedNodeGraph.DrawGraph(e.Graphics, null, null, graphMousePos);
			}

			//if(_currentNode !=null && _currentNode.SubItems.Count >0)
			//	Invalidate();
		}

		/// <summary>
		/// The node copied to the clipboard.
		/// </summary>
		private Node _clipboardNode= null;

		/// <summary>
		/// Stores if Ctrl+V is currently pressed.
		/// </summary>
		private bool _clipboardPasteMode= false;

		/// <summary>
		/// The node we dragged inside the graph.
		/// </summary>
		private Node _movedNode= null;

		/// <summary>
		/// The node we want to create a copy from.
		/// </summary>
		private Node _copiedNode= null;

		/// <summary>
		/// The layout manager which draws the small graph when dragging existing nodes.
		/// </summary>
		private NodeLayoutManager _movedNodeGraph= null;

		/// <summary>
		/// Handles when the mouse is moved.
		/// </summary>
		protected override void OnMouseMove(MouseEventArgs e)
		{
			if(_lostFocus)
			{
				_lostFocus= false;

				// update the last ouse position
				_lastMousePosition= e.Location;

				base.OnMouseMove(e);

				return;
			}

			// returns the mouse under the mouse cursor
			NodeViewData nodeFound= _rootNode.IsInside(e.Location);

			// clear previously stored node which can cause problems when dragging to another view
			_dragTargetNode= null;

			// if a different node is the current one, update it
			if(nodeFound !=_currentNode)
			{
				_currentNode= nodeFound;

				// if enabled show the tooltip for the node
				if(Settings.Default.ShowNodeToolTips)
				{
					if(_currentNode ==null)
					{
						toolTip.Hide(this);
					}
					else
					{
						if(_currentNode.Node.ToolTip !=string.Empty)
							toolTip.Show(_currentNode.Node.ToolTip, this, new Point( (int)_currentNode.DisplayBoundingBox.X -20, (int)_currentNode.DisplayBoundingBox.Y -30 ));
					}
				}

				Invalidate();
			}

			// check if we are currently dragging the graph
			if(e.Button ==MouseButtons.Left && _lastMousePosition !=e.Location && !_keyControlIsDown && _copiedNode ==null)
			{
				_wasDragged= true;

				// move the graph according to the last mouse position
				_nodeLayoutManager.Offset= new PointF(_nodeLayoutManager.Offset.X - (_lastMousePosition.X - e.X), _nodeLayoutManager.Offset.Y - (_lastMousePosition.Y - e.Y));

				Invalidate();
			}
			// check if we start duplicating an existing node step 1
			else if(e.Button ==MouseButtons.Left && _keyControlIsDown && _lastMousePosition !=e.Location && _copiedNode ==null && _currentNode !=null && !(_currentNode.Node is BehaviorNode))
			{
				_copiedNode= _currentNode.Node;

				// create the layout manager used to draw the graph
				_movedNodeGraph= new NodeLayoutManager(_copiedNode.CloneBranch().CreateNodeViewData(null, _rootNode.RootBehavior), _nodeLayoutManager.EdgePen, _nodeLayoutManager.EdgePenReadOnly, true);
				_movedNodeGraph.Scale= 0.3f;
				_movedNodeGraph.RenderDepth= _keyShiftIsDown ? int.MaxValue : 0;

				// use the existing node as the node defaults
				_dragNodeDefaults= _copiedNode;

				Invalidate();
			}
			// check if we are duplicating an existing node step 2
			else if(e.Button ==MouseButtons.Left && _keyControlIsDown && _copiedNode !=null)
			{
				_movedNodeGraph.RenderDepth= _keyShiftIsDown ? int.MaxValue : 0;

				_dragTargetNode= _currentNode;

				Cursor= _currentNode ==null ? Cursors.No : Cursors.Arrow;

				//Point movedGraphGraphPos= new Point(e.Location.X + _movedNodeGraph.Offset.X, e.Location.Y + _movedNodeGraph.Offset.Y /-2);
				//_movedNodeGraph.Location= movedGraphGraphPos;

				Invalidate();
			}
			// check if we start dragging an existing node step 1
			else if(e.Button ==MouseButtons.Right && _lastMousePosition !=e.Location && _movedNode ==null && _currentNode !=null && !(_currentNode.Node is BehaviorNode) && (_keyShiftIsDown || _currentNode.Node.ParentCanAdoptChildren))
			{
				_movedNode= _currentNode.Node;

				// create the layout manager used to draw the graph
				_movedNodeGraph= new NodeLayoutManager(_movedNode.CloneBranch().CreateNodeViewData(null, _rootNode.RootBehavior), _nodeLayoutManager.EdgePen, _nodeLayoutManager.EdgePenReadOnly, true);
				_movedNodeGraph.Scale= 0.3f;
				_movedNodeGraph.RenderDepth= _keyShiftIsDown ? int.MaxValue : 0;

				// use the existing node as the node defaults
				_dragNodeDefaults= _movedNode;

				Invalidate();
			}
			// check if we are dragging an existing node step 2
			else if(e.Button ==MouseButtons.Right && _movedNode !=null)
			{
				_movedNodeGraph.RenderDepth= _keyShiftIsDown ? int.MaxValue : 0;

				_dragTargetNode= _currentNode;

				Cursor= _currentNode ==null ? Cursors.No : Cursors.Arrow;

				Invalidate();
			}
			else if(_clipboardPasteMode)
			{
				_movedNodeGraph.RenderDepth= _keyShiftIsDown ? int.MaxValue : 0;

				_dragTargetNode= _currentNode;

				Cursor= _currentNode ==null ? Cursors.No : Cursors.Arrow;

				Invalidate();
			}

			// update the last ouse position
			_lastMousePosition= e.Location;

			base.OnMouseMove(e);
		}

		/// <summary>
		/// Handles when the mouse wheel is used.
		/// </summary>
		protected override void OnMouseWheel(MouseEventArgs e)
		{
			// calculate the new scale
			float newscale= _nodeLayoutManager.Scale + 0.001f * e.Delta;

			// if the scale is too low or too high skip it
			if(newscale <0.1f || newscale >2.0f)
				return;

			// maintain the current mouse position while zooming
			_maintainMousePosition= true;
			_graphOrigin= _nodeLayoutManager.ViewToGraph(_lastMousePosition);

			// assign the new scale
			_nodeLayoutManager.Scale= newscale;
			Invalidate();

			//base.OnMouseWheel(e);
		}

		/// <summary>
		/// Holds if the control key is currently down or not.
		/// </summary>
		private bool _keyControlIsDown= false;

		/// <summary>
		/// Holds if the shift key is currently down or not.
		/// </summary>
		private bool _keyShiftIsDown= false;

		/// <summary>
		/// Handles when a mouse button is let go of.
		/// </summary>
		protected override void OnMouseUp(MouseEventArgs e)
		{
			// check if we were dragging or copying an existing node
			if(e.Button ==MouseButtons.Right && _movedNode !=null || e.Button ==MouseButtons.Left && _copiedNode !=null || e.Button ==MouseButtons.Left && _clipboardPasteMode)
			{
				// if we have a valid target node continue
				if(_dragTargetNode !=null)
				{
					Node sourcenode= null;

					if(_movedNode !=null)
						sourcenode= _movedNode;
					else if(_copiedNode !=null)
						sourcenode= _keyShiftIsDown ? (Nodes.Node)_copiedNode.CloneBranch() : (Nodes.Node)_copiedNode.Clone();
					else if(_clipboardPasteMode)
						sourcenode= _keyShiftIsDown ? (Nodes.Node)_clipboardNode.CloneBranch() : (Nodes.Node)_clipboardNode.Clone();

					// move the dragged node to the target node, according to the attach mode
					switch(_dragAttachMode)
					{
						// the node will be placed above the traget node
						case(NodeAttachMode.Top):
							if(_movedNode !=null)
							{
								if(_keyShiftIsDown)
								{
									_movedNode.Parent.RemoveChild(_movedNode.ParentConnector, _movedNode);
								}
								else
								{
									if(!_movedNode.ExtractNode())
										throw new Exception(Resources.ExceptionNodeCouldNotBeExtracted);
								}
							}

							int n= _dragTargetNode.Node.ParentConnector.GetChildIndex(_dragTargetNode.Node);
							_dragTargetNode.Node.Parent.AddChild(_dragTargetNode.Node.ParentConnector, sourcenode, n);

							LayoutChanged();
						break;

						// the node will be placed below the target node
						case(NodeAttachMode.Bottom):
							if(_movedNode !=null)
							{
								if(_keyShiftIsDown)
								{
									_movedNode.Parent.RemoveChild(_movedNode.ParentConnector, _movedNode);
								}
								else
								{
									if(!_movedNode.ExtractNode())
										throw new Exception(Resources.ExceptionNodeCouldNotBeExtracted);
								}
							}

							int m= _dragTargetNode.Node.ParentConnector.GetChildIndex(_dragTargetNode.Node);
							_dragTargetNode.Node.Parent.AddChild(_dragTargetNode.Node.ParentConnector, sourcenode, m +1);

							LayoutChanged();
						break;

						// the node will be placed in front of the target node
						case(NodeAttachMode.Left):
							if(_movedNode !=null)
							{
								if(_keyShiftIsDown)
								{
									_movedNode.Parent.RemoveChild(_movedNode.ParentConnector, _movedNode);
								}
								else
								{
									if(!_movedNode.ExtractNode())
										throw new Exception(Resources.ExceptionNodeCouldNotBeExtracted);
								}
							}

							Node parent= _dragTargetNode.Node.Parent;
							Node.Connector conn= _dragTargetNode.Node.ParentConnector;
							int o= conn.GetChildIndex(_dragTargetNode.Node);
							parent.RemoveChild(conn, _dragTargetNode.Node);
							parent.AddChild(conn, sourcenode, o);

							sourcenode.AddChild(sourcenode.DefaultConnector, _dragTargetNode.Node);
						break;

						// the node will simply attached to the target node
						case(NodeAttachMode.Right):
							if(_movedNode !=null)
							{
								if(_keyShiftIsDown)
								{
									_movedNode.Parent.RemoveChild(_movedNode.ParentConnector, _movedNode);
								}
								else
								{
									if(!_movedNode.ExtractNode())
										throw new Exception(Resources.ExceptionNodeCouldNotBeExtracted);
								}
							}

							_dragTargetNode.Node.AddChild(_dragTargetConnector, sourcenode);

							LayoutChanged();
						break;
					}

					// update the node's label
					sourcenode.OnPropertyValueChanged(false);
				}

				// reset all the drag data
				if(!_clipboardPasteMode)
				{
					_copiedNode= null;
					_movedNode= null;
					_dragTargetNode= null;
					_dragNodeDefaults= null;
					_movedNodeGraph= null;
				}

				// redraw the graph
				Invalidate();
			}

			Cursor= Cursors.Hand;

			base.OnMouseUp(e);
		}

		/// <summary>
		/// Holds whether or not the mouse was dragged.
		/// </summary>
		private bool _wasDragged= false;

		/// <summary>
		/// Handles when a mouse button was pressed.
		/// </summary>
		protected override void OnMouseDown(MouseEventArgs e)
		{
			// the graph has not yet been dragged
			_wasDragged= false;

			// update the mouse cursor when dragging nodes
			if(e.Button ==MouseButtons.Right)
			{
				if(_movedNode ==null)
					Cursor= Cursors.Arrow;
			}

			// focus the view if not focused
			if(!Focused)
			{
				// we focus twice to avoid an issue with focusing the view
				Parent.Focus();
				Focus();
			}

			base.OnMouseDown(e);
		}

		public delegate void ClickNodeEventDelegate(NodeViewData node);

		/// <summary>
		/// This event is called when the user clicks on a node in the graph which is not selected.
		/// </summary>
		public event ClickNodeEventDelegate ClickNode;

		public delegate void ClickEventEventDelegate(NodeViewData node);

		/// <summary>
		/// This event is called when the user clicks on a node in the graph which is already selected which has events.
		/// </summary>
		public event ClickEventEventDelegate ClickEvent;

		/// <summary>
		/// Handles when the user performs a click.
		/// </summary>
		protected override void OnMouseClick(MouseEventArgs e)
		{
			// check if the user did not drag the graph and clicked it instead
			if(!_wasDragged && e.Button ==MouseButtons.Left)
			{
				// if the clicked ode is already selected and has events, we click the event instead of the node
				bool clickEvent= _selectedNode !=null && _selectedNode ==_currentNode && _selectedNode.Node.SubItems.Count >0;

				// assign the new selected node. Checks if the selected node is already this one.
				SelectedNode= _currentNode;

				// check if we click the event
				if(clickEvent)
				{
					// perform click event on the node as the node's have to handle their events.
					_selectedNode.Node.ClickEvent(_selectedNode, _nodeLayoutManager.ViewToGraph(e.Location));
					Invalidate();

					// call the ClickEvent event handler
					if(ClickEvent !=null)
					{
						ClickEvent(_selectedNode);
						return;
					}
				}

				// call the click node event handler
				if(ClickNode !=null)
				{
					ClickNode(_selectedNode);
					return;
				}
			}

			base.OnMouseClick(e);
		}

		/// <summary>
		/// This event is called when a node is double-clicked.
		/// </summary>
		public event ClickNodeEventDelegate DoubleClickNode;

		/// <summary>
		/// Handles when the user doucle-clicks.
		/// </summary>
		protected override void OnMouseDoubleClick(MouseEventArgs e)
		{
			// when the user double-clicked a node and the graph was not dragged and it was the left mouse button, continue
			if(_currentNode !=null && DoubleClickNode !=null && !_wasDragged && e.Button ==MouseButtons.Left)
			{
				// call double-clicked on the node
				bool layoutChanged;
				if(_currentNode.Node.OnDoubleClick(_currentNode, out layoutChanged))
				{
					// check if the node requires the layout to be updated, for example when expanding or collapsing referenced behaviours
					if(layoutChanged)
					{
						// keep the position of the current node
						_maintainNodePosition= _currentNode;

						_graphOrigin= _maintainNodePosition.DisplayBoundingBox.Location;

						LayoutChanged();
					}
				}
				else
				{
					// if the node did not handle the double-click the developer may
					if(DoubleClickNode !=null)
						DoubleClickNode(_currentNode);
				}
			}
			else base.OnMouseDoubleClick(e);
		}

		/// <summary>
		/// Handles when a key is released.
		/// </summary>
		protected override void OnKeyUp(KeyEventArgs e)
		{
			switch(e.KeyCode)
			{
				// store when the control key is released
				case(Keys.ControlKey):
					_keyControlIsDown= false;

					if(_copiedNode ==null && _movedNode ==null)
						Cursor= Cursors.Hand;
				break;

				// store when the shift key is released
				case(Keys.ShiftKey):
					_keyShiftIsDown= false;

					// update the drawn graph for dragging and duplicating
					if(_movedNodeGraph !=null)
					{
						_movedNodeGraph.RenderDepth= _keyShiftIsDown ? int.MaxValue : 0;
						Invalidate();
					}
				break;

				// paste from clipboard
				case(Keys.V):
					_clipboardPasteMode= false;

					// reset all the drag data
					_copiedNode= null;
					_movedNode= null;
					_dragTargetNode= null;
					_dragNodeDefaults= null;
					_movedNodeGraph= null;

					// redraw the graph
					Invalidate();
				break;

				default: base.OnKeyUp(e); break;
			}
		}

		/// <summary>
		/// Handles when a key is pressed.
		/// </summary>
		protected override void OnKeyDown(KeyEventArgs e)
		{
			switch(e.KeyCode)
			{
				// store when the control key is pressed
				case(Keys.ControlKey):
					_keyControlIsDown= true;

					if(_copiedNode ==null && _movedNode ==null)
						Cursor= Cursors.Arrow;
				break;

				// store when the shift key is pressed
				case(Keys.ShiftKey):
					_keyShiftIsDown= true;

					// update the drawn graph for dragging and duplicating
					if(_movedNodeGraph !=null)
					{
						_movedNodeGraph.RenderDepth= _keyShiftIsDown ? int.MaxValue : 0;
						Invalidate();
					}
				break;

				// copy to clipboard
				case(Keys.C):
					if(_keyControlIsDown && _selectedNode !=null)
					{
						_clipboardNode= _keyShiftIsDown ? (Node)_selectedNode.Node.CloneBranch() : (Node)_selectedNode.Node.Clone();
					}
				break;

				// paste from clipboard
				case(Keys.V):
					if(!_clipboardPasteMode)
					{
						_clipboardPasteMode= _keyControlIsDown && _clipboardNode !=null;

						if(_clipboardPasteMode)
						{
							// create the layout manager used to draw the graph
							_movedNodeGraph= new NodeLayoutManager(_clipboardNode.CreateNodeViewData(null, _rootNode.RootBehavior), _nodeLayoutManager.EdgePen, _nodeLayoutManager.EdgePenReadOnly, true);
							_movedNodeGraph.Scale= 0.3f;
							_movedNodeGraph.RenderDepth= _keyShiftIsDown ? int.MaxValue : 0;

							// use the existing node as the node defaults
							_dragNodeDefaults= _clipboardNode;

							Invalidate();
						}
					}
				break;

				// cut to clipboard
				case(Keys.X):
					if(_keyControlIsDown && _selectedNode !=null)
					{
						_clipboardNode= _keyShiftIsDown ? (Node)_selectedNode.Node.CloneBranch() : (Node)_selectedNode.Node.Clone();

						// store the selected node
						Node node= _selectedNode.Node;

						// clear the selected and current node
						_selectedNode= null;
						_currentNode= null;

						if(_keyShiftIsDown)
						{
							// remove the node
							node.Parent.RemoveChild(node.ParentConnector, node);

							// call the ClickNode event to delselect the node in the editor
							if(ClickNode !=null)
								ClickNode(null);
						}
						else
						{
							if(node.ExtractNode())
							{
								// call the ClickNode event to delselect the node in the editor
								if(ClickNode !=null)
									ClickNode(null);
							}
						}
					}
				break;

				// handle when the delete key is pressed
				case(Keys.Delete):
					// when we have a node selected which is not the root node, continue
					if(_selectedNode !=null && _selectedNode.Node.Parent !=null)
					{
						// check whether we have to delete an event or a node
						if(_selectedNode.Node.SelectedSubItem ==null)
						{
							// store the selected node
							Node node= _selectedNode.Node;

							// clear the selected and current node
							_selectedNode= null;
							_currentNode= null;

							if(_keyShiftIsDown)
							{
								// remove the node
								node.Parent.RemoveChild(node.ParentConnector, node);

								// call the ClickNode event to delselect the node in the editor
								if(ClickNode !=null)
									ClickNode(null);
							}
							else
							{
								if(node.ExtractNode())
								{
									// call the ClickNode event to delselect the node in the editor
									if(ClickNode !=null)
										ClickNode(null);
								}
							}
						}
						else
						{
							// just let the node delete the selected subitem
							if(_selectedNode.Node.RemoveSelectedSubItem())
							{
								_selectedNode.Node.BehaviorWasModified();

								// call the ClickNode event to select the node instead of the deleted subitem
								if(ClickNode !=null)
									ClickNode(_selectedNode);
							}
						}

						// the layout needs to be recalculated
						LayoutChanged();
					}
				break;

				default: base.OnKeyDown(e); break;
			}
		}

		/// <summary>
		/// Handles when a tree node is dragged on the view.
		/// </summary>
		private void BehaviorTreeView_DragOver(object sender, DragEventArgs e)
		{
			// get the node we are dragging over
			Point pt= PointToClient(new Point(e.X, e.Y));
			NodeViewData nodeFound= _rootNode.IsInside(new PointF(pt.X, pt.Y));

			// update the current node
			if(nodeFound !=_currentNode)
			{
				_currentNode= nodeFound;
				Invalidate();
			}
			// when we are moving on a node we must keep drawing as the ttach ode might change but not the node
			else if(nodeFound !=null)
			{
				Invalidate();
			}

			// store the target node
			_dragTargetNode= _currentNode;

			// deny drop by default
			e.Effect= DragDropEffects.None;

			// make sure the correct drag attach mode is set
			if(_dragTargetNode !=null)
			{
				if(_dragNodeDefaults is Nodes.Node && _dragAttachMode ==NodeAttachMode.Event)
					_dragAttachMode= NodeAttachMode.None;
				else if(_dragNodeDefaults is Events.Event && _dragAttachMode !=NodeAttachMode.Event)
					_dragAttachMode= NodeAttachMode.Event;
			}

			// check if we are trying to drop a node on another one
			if(_dragTargetNode !=null && (e.KeyState & 1/*left mouse button*/) >0 && _dragNodeDefaults is Nodes.Node)
			{
				e.Effect= DragDropEffects.Move;
			}
			// check if we are trying attach an event to a node
			else if(_dragTargetNode !=null && (e.KeyState & 1/*left mouse button*/) >0 && _dragNodeDefaults is Events.Event && _dragTargetNode.Node.AcceptsEvents)
			{
				e.Effect= DragDropEffects.Move;
			}

			// update last know mouse position
			_lastMousePosition= new PointF(pt.X, pt.Y);
		}

		/// <summary>
		/// Handles when dropping a tree node on the view.
		/// </summary>
		private void BehaviorTreeView_DragDrop(object sender, DragEventArgs e)
		{
			//make sure the view is focused
			Focus();

			// get source node
			TreeNode sourceNode= (TreeNode) e.Data.GetData("System.Windows.Forms.TreeNode");
			NodeTag sourceNodeTag= (NodeTag)sourceNode.Tag;

			// keep the current node position
			_maintainNodePosition= _dragTargetNode;
			_graphOrigin= _maintainNodePosition.DisplayBoundingBox.Location;

			// check if we are dropping an event
			if(_dragAttachMode ==NodeAttachMode.Event)
			{
				// add the event to the target node
				Events.Event evnt= Brainiac.Design.Events.Event.Create(sourceNodeTag.NodeType, _dragTargetNode.Node);
				evnt.OnPropertyValueChanged(false);
				Nodes.Node.SubItemEvent sub= new Nodes.Node.SubItemEvent(evnt);
				_dragTargetNode.Node.AddSubItem(sub);
				_dragTargetNode.Node.BehaviorWasModified();

				SelectedNode= _dragTargetNode;
				_selectedNode.Node.SelectedSubItem= sub;

				// call the ClickEvent event handler
				if(ClickEvent !=null)
					ClickEvent(_selectedNode);

				LayoutChanged();
			}
			else if(_dragAttachMode !=NodeAttachMode.None)
			{
				// attach a new node to the target node
				InsertNewNode(_dragTargetNode, _dragAttachMode, sourceNodeTag);
			}

			// reset drag stuff
			_dragTargetNode= null;
			_dragNodeDefaults= null;
			_dragAttachMode= NodeAttachMode.None;

			Invalidate();
		}

		/// <summary>
		/// Handles when a tree node is dragged into the view.
		/// </summary>
		private void BehaviorTreeView_DragEnter(object sender, DragEventArgs e)
		{
			TreeNode sourceNode= (TreeNode) e.Data.GetData("System.Windows.Forms.TreeNode");
			if(sourceNode ==null)
				return;

			NodeTag sourceNodeTag= sourceNode.Tag as NodeTag;
			if(sourceNodeTag ==null)
				return;

			// store the tree node's defaults
			_dragNodeDefaults= sourceNodeTag.Defaults;
		}

		/// <summary>
		/// Holds if we have the position of a previous dialogue stored.
		/// </summary>
		private static bool _hasOldCheckDialogPosition= false;

		/// <summary>
		/// The previous position of the check dialogue.
		/// </summary>
		private static Point _previousCheckDialogPosition;

		/// <summary>
		/// Handles when the check for errors button is pressed.
		/// </summary>
		private void checkButton_Click(object sender, EventArgs e)
		{
			// store the old position of the check dialogue and close it
			if(_errorCheckDialog !=null)
				_errorCheckDialog.Close();

			// prepare the new dialogue
			_errorCheckDialog= new ErrorCheckDialog();
			_errorCheckDialog.BehaviorTreeList= _behaviorTreeList;
			_errorCheckDialog.BehaviorTreeView= this;
			_errorCheckDialog.Text= _rootNode.RootBehavior.GetPathLabel(_behaviorTreeList.BehaviorFolder) +" Error Check Results";
			_errorCheckDialog.FormClosed+= new FormClosedEventHandler(errorCheckDialog_FormClosed);

			// check the current behaviour for errors
			List<Node.ErrorCheck> result= new List<Node.ErrorCheck>();
			_rootNode.Node.CheckForErrors(_rootNode.RootBehavior, result);

			// add the errors to the check dialogue
			foreach(Node.ErrorCheck check in result)
			{
				BehaviorNode behavior= check.Node.Behavior;

				// group the errors by the behaviour their occured in

				// get the group for the error's behaviour
				ListViewGroup group= null;
				foreach(ListViewGroup grp in _errorCheckDialog.listView.Groups)
				{
					if(grp.Tag ==behavior)
					{
						group= grp;
						break;
					}
				}

				// if there is no group, create it
				if(group ==null)
				{
					group= new ListViewGroup(behavior.GetPathLabel(_behaviorTreeList.BehaviorFolder));
					group.Tag= behavior;
					_errorCheckDialog.listView.Groups.Add(group);
				}

				// create an item for the error in the group
				ListViewItem item= new ListViewItem(check.Description);
				item.Group= group;
				item.Tag= check.Node;

				switch(check.Level)
				{
					case(ErrorCheckLevel.Message):
						item.ImageIndex= 0;
					break;

					case(ErrorCheckLevel.Warning):
						item.ImageIndex= 1;
					break;

					case(ErrorCheckLevel.Error):
						item.ImageIndex= 2;
					break;
				}

				_errorCheckDialog.listView.Items.Add(item);
			}

			// if no errors were found, tell the user so
			if(result.Count <1)
				_errorCheckDialog.listView.Items.Add( new ListViewItem("No Errors Found.", 0) );

			// show the dialogue
			_errorCheckDialog.Show();

			// set its position to the position of the previous dialogue
			if(_hasOldCheckDialogPosition)
				_errorCheckDialog.Location= _previousCheckDialogPosition;
		}

		/// <summary>
		/// Handles when the error check dialogue is closed
		/// </summary>
		void errorCheckDialog_FormClosed(object sender, FormClosedEventArgs e)
		{
			// store its previous position
			_previousCheckDialogPosition= _errorCheckDialog.Location;
			_hasOldCheckDialogPosition= true;

			// we have to create a new dialogue the next time so we clear it
			_errorCheckDialog= null;
		}

		/// <summary>
		/// Handles when the export button is pressed
		/// </summary>
		private void exportButton_Click(object sender, EventArgs e)
		{
			if(_behaviorTreeList !=null)
			{
				_behaviorTreeList.ExportBehavior(_rootNode.RootBehavior);
			}
		}

		/// <summary>
		/// Handles when the save button is pressed.
		/// </summary>
		private void saveButton_Click(object sender, EventArgs e)
		{
			if(_behaviorTreeList !=null)
			{
				try { _behaviorTreeList.SaveBehavior(_rootNode.RootBehavior, false); }
				catch(Exception ex) { MessageBox.Show(ex.Message, "Save Error", MessageBoxButtons.OK); }
			}
		}

		/// <summary>
		/// Handles when the save-as button is pressed.
		/// </summary>
		private void saveAsButton_Click(object sender, EventArgs e)
		{
			if(_behaviorTreeList !=null)
			{
				try { _behaviorTreeList.SaveBehavior(_rootNode.RootBehavior, true); }
				catch(Exception ex) { MessageBox.Show(ex.Message, "Save Error", MessageBoxButtons.OK); }
			}
		}

		/// <summary>
		/// Centres the given node in the view.
		/// </summary>
		/// <param name="node">The node which will be centred.</param>
		internal void CenterNode(NodeViewData node)
		{
			// we use the existing maintain position stuff for that
			RectangleF bbox= node.BoundingBox;

			_maintainNodePosition= node;

			// if the node was not yet shown there is no bounding box so we simply use the min height for that
			float height= bbox.Height <=0.0f ? node.Node.MinHeight : bbox.Height;

			_graphOrigin= new PointF(20.0f, ClientSize.Height *0.5f - height *0.5f);

			Invalidate();
		}

		/// <summary>
		/// Stores if the root behavior is supposed to be centres or not.
		/// </summary>
		protected bool _pendingCentreBehavior= false;

		/// <summary>
		/// Centres the given node in the view.
		/// </summary>
		/// <param name="node">The node which will be centred.</param>
		/// <returns>Returns the NodeViewData of the node which was centred.</returns>
		internal NodeViewData CenterNode(Node node)
		{
			NodeViewData nvd= _rootNode.FindNodeViewData(node);
			if(nvd ==null)
				CenterNode(nvd);

			return nvd;
		}

		private void imageButton_Click(object sender, EventArgs e)
		{
			if(saveImageDialog.ShowDialog() ==DialogResult.OK)
			{
				NodeLayoutManager nlm= new NodeLayoutManager(_nodeLayoutManager.RootNodeLayout, _nodeLayoutManager.EdgePen, _nodeLayoutManager.EdgePenReadOnly, false);
				nlm.Offset= new PointF(1.0f, 1.0f);

				using(Graphics g= CreateGraphics())
				{
					nlm.UpdateLayout(g);
				}

				SizeF totalSize= nlm.RootNodeLayout.GetTotalSize(nlm.Padding.Width, int.MaxValue);
				Size newimageSize= new Size( (int) Math.Ceiling(totalSize.Width) +2, (int) Math.Ceiling(totalSize.Height) +2 );

				Graphics formGraphics= null;
				IntPtr hdc= new IntPtr();
				Image img= null;
				bool needsSave= true;
				if(saveImageDialog.FilterIndex ==1)
				{
					formGraphics= CreateGraphics();
					hdc= formGraphics.GetHdc();

					img= new Metafile(saveImageDialog.FileName, hdc);

					needsSave= false;
				}
				else if(saveImageDialog.FilterIndex ==2)
				{
					img= new Bitmap(newimageSize.Width, newimageSize.Height);
				}

				using(Graphics graphics= Graphics.FromImage(img))
				{
					nlm.DrawGraph(graphics, null, null, new PointF());
				}

				if(needsSave)
					img.Save(saveImageDialog.FileName);

				img.Dispose();

				if(formGraphics !=null)
				{
					formGraphics.ReleaseHdc(hdc);
					formGraphics.Dispose();
				}
			}
		}

		private void propertiesButton_Click(object sender, EventArgs e)
		{
			PropertiesDock dock= new PropertiesDock();
			dock.SelectedObject= _selectedNode.Node;
			dock.Show(MainWindow.DockPanel, WeifenLuo.WinFormsUI.Docking.DockState.Float);
		}

		protected override void OnGotFocus(EventArgs e)
		{
			base.OnGotFocus(e);

			PropertiesDock.InspectObject(_selectedNode ==null ? null : _selectedNode.Node);
		}

		protected override void OnLostFocus(EventArgs e)
		{
			base.OnLostFocus(e);

			_lostFocus= true;
		}
	}
}
