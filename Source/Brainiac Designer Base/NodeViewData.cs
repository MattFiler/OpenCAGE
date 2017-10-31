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
using System.Text;
using System.Drawing;
using Brainiac.Design.Nodes;

namespace Brainiac.Design
{
	/// <summary>
	/// This class represents a node which is drawn in a view.
	/// </summary>
	public class NodeViewData
	{
		protected Node _node;

		/// <summary>
		/// The node this view is for.
		/// </summary>
		public Node Node
		{
			get { return _node; }
		}

		protected NodeViewData _parent;

		/// <summary>
		/// The parent NodeViewData of this node.
		/// </summary>
		public NodeViewData Parent
		{
			get { return _parent; }
		}

		protected BehaviorNode _rootBehavior;

		/// <summary>
		/// The behaviour which owns this view as it is the root of the shown graph.
		/// </summary>
		public BehaviorNode RootBehavior
		{
			get { return _rootBehavior; }
		}

		protected List<NodeViewData> _children= new List<NodeViewData>();

		/// <summary>
		/// The views for the children of the node this view is for.
		/// </summary>
		public IList<NodeViewData> Children
		{
			get { return _children.AsReadOnly(); }
		}

		/// <summary>
		/// Returns the first NodeViewData which is associated with the given node. Notice that there might be other NodeViewDatas which are ignored.
		/// </summary>
		/// <param name="node">The node you want to get the NodeViewData for.</param>
		/// <returns>Returns the first NodeViewData found.</returns>
		public virtual NodeViewData FindNodeViewData(Node node)
		{
			// check if this is a fitting view
			if(_node ==node)
				return this;

			// search the children
			foreach(NodeViewData child in _children)
			{
				NodeViewData result= child.FindNodeViewData(node);
				if(result !=null)
					return result;
			}

			return null;
		}

		/// <summary>
		/// This function adapts the children of the view that they represent the children of the node this view is for.
		/// Children are added and removed.
		/// </summary>
		/// <param name="processedBehaviors">A list of previously processed behaviours to deal with circular references.</param>
		public virtual void SynchronizeWithNode(IList<BehaviorNode> processedBehaviors)
		{
			// allow the node to add some final children or remove some
			_node.PreLayoutUpdate(this);

			// if the counts do not fit we must rebuild
			bool rebuild= _node.Children.Count !=_children.Count;

			// check if all children are associated to the correct children of the node
			if(!rebuild)
			{
				for(int i= 0; i <_node.Children.Count; ++i)
				{
					// this view is associated to another node than the child of the node
					if(_children[i].Node !=_node.Children[i])
					{
						rebuild= true;
						break;
					}
				}
			}

			// check if we must rebuild the child list
			if(rebuild)
			{
				// store the old children
				List<NodeViewData> oldChildren= _children;

				// create a new list
				_children= new List<NodeViewData>();

				// update the children
				foreach(Node child in _node.Children)
				{
					// check if our old child list already contains a view for this node. If so copy it.
					NodeViewData nvd= child.FindNodeViewData(oldChildren);
					if(nvd !=null)
					{
						Debug.Check(_rootBehavior ==nvd.RootBehavior);

						_children.Add(nvd);
					}
					// otherwise create a new view
					else
					{
						_children.Add( child.CreateNodeViewData(this, _rootBehavior) );
					}
				}
			}

			// synchronise the children as well
			foreach(NodeViewData child in _children)
			{
				Debug.Check(child.RootBehavior ==_rootBehavior);

				child.SynchronizeWithNode(processedBehaviors);
			}
		}

		/// <summary>
		/// Creates a new view for a given node.
		/// </summary>
		/// <param name="parent">The parent of the new NodeViewData.</param>
		/// <param name="rootBehavior">The behaviour which is the root of the graph the given node is shown in.</param>
		/// <param name="node">The node the view is created for.</param>
		public NodeViewData(NodeViewData parent, BehaviorNode rootBehavior, Node node)
		{
			Debug.Check(rootBehavior !=null);

			_parent= parent;
			_rootBehavior= rootBehavior;
			_node= node;

			if(parent !=null)
				WasModified= parent.WasModified;

			_node.WasModified+= new Node.WasModifiedEventDelegate(node_WasModified);
		}

		protected RectangleF _boundingBox;

		/// <summary>
		/// The untransformed bounding box of the node.
		/// </summary>
		public RectangleF BoundingBox
		{
			get { return _boundingBox; }
		}

		protected RectangleF _displayBoundingBox;

		/// <summary>
		/// The transformed bounding box of the node.
		/// </summary>
		public RectangleF DisplayBoundingBox
		{
			get { return _displayBoundingBox; }
		}

		protected RectangleF _layoutRectangle;

		/// <summary>
		/// The layout rectangle of the node.
		/// </summary>
		public RectangleF LayoutRectangle
		{
			get { return _layoutRectangle; }
		}

		/// <summary>
		/// The upper left corner of the layout rectangle. For internal use only.
		/// </summary>
		protected PointF Location
		{
			get { return _layoutRectangle.Location; }
			set { _layoutRectangle.Location= value; }
		}

		/// <summary>
		/// Returns the node a given location is in.
		/// </summary>
		/// <param name="location">The location you want to check.</param>
		/// <returns>Returns null if the position is not inside any node.</returns>
		public NodeViewData IsInside(PointF location)
		{
			if(_displayBoundingBox.Contains(location))
				return this;

			foreach(NodeViewData node in _children)
			{
				NodeViewData insidenode= node.IsInside(location);
				if(insidenode !=null)
					return insidenode;
			}

			return null;
		}

		/// <summary>
		/// Copies the ode's size as the size of the bounding box.
		/// </summary>
		public virtual void UpdateExtent()
		{
			foreach(NodeViewData node in _children)
				node.UpdateExtent();

			_boundingBox.Size= _node.FinalSize;
		}

		/// <summary>
		/// Adds an offset to the height and Y position of the  layout rectangle.
		/// Used when the parent is higher than the children.
		/// </summary>
		/// <param name="offset">The off set which will be added.</param>
		protected void OffsetLayoutSize(float offset)
		{
			float yoffset= 0.0f;
			foreach(NodeViewData node in _children)
			{
				node._layoutRectangle.Height+= offset;
				node._layoutRectangle.Y+= yoffset;

				yoffset+= offset;

				node.OffsetLayoutSize(offset);
			}
		}

		/// <summary>
		/// Calculates the layout rectangles for this node and its children.
		/// </summary>
		/// <param name="padding">The padding which is used between the nodes.</param>
		public void CalculateLayoutSize(SizeF padding)
		{
			// update size for children
			foreach(NodeViewData node in _children)
				node.CalculateLayoutSize(padding);

			// calculate my layout size
			_layoutRectangle.Height=_boundingBox.Height;
			_layoutRectangle.Width= _boundingBox.Width;

			// calculate the size my children have
			float childHeight= 0.0f;
			foreach(NodeViewData node in _children)
				childHeight+= node.LayoutRectangle.Height;

			// if we have multiple children, add the padding we keep between them.
			if(_children.Count >1)
				childHeight+= (_children.Count -1) * padding.Height;

			if(_layoutRectangle.Height >childHeight)
			{
				_layoutRectangle.Height= _layoutRectangle.Height;

				// if this node is higher than its children we have to update them
				float heightDiff= _layoutRectangle.Height - childHeight;
				float offset= heightDiff / _children.Count;

				OffsetLayoutSize(offset);
			}
			else
			{
				_layoutRectangle.Height= childHeight;
			}
		}

		/// <summary>
		/// Aligns the different layout rectangles in the graph.
		/// </summary>
		/// <param name="padding">The padding you want to keep between the layout rectangles.</param>
		public void Layout(SizeF padding)
		{
			// the upper left position of the children
			PointF pos= new PointF(_layoutRectangle.Right + padding.Width, _layoutRectangle.Y);

			// align children
			foreach(NodeViewData node in _children)
			{
				// set the node to the correct position
				node.Location= pos;

				// adjust the location for the next child to come
				pos.Y+= node.LayoutRectangle.Height + padding.Height;

				// layout the children of this node.
				node.Layout(padding);
			}
		}

		/// <summary>
		/// Centers this and its children node in front of its/their children.
		/// </summary>
		public virtual void UpdateLocation()
		{
			// move the node to the left centre of its layout
			_boundingBox.X= _layoutRectangle.X;
			_boundingBox.Y= _layoutRectangle.Y + _layoutRectangle.Height *0.5f - _boundingBox.Height *0.5f;

			// update the location for the children as well
			foreach(NodeViewData node in _children)
				node.UpdateLocation();
		}

		/// <summary>
		/// Calculates the display bounding box for this node.
		/// </summary>
		/// <param name="offsetX">The X offset of the graph.</param>
		/// <param name="offsetY">The Y offset of the graph.</param>
		/// <param name="scale">The scale of the graph.</param>
		public virtual void UpdateDisplay(float offsetX, float offsetY, float scale)
		{
			// transform the bounding box.
			_displayBoundingBox.X= _boundingBox.X * scale + offsetX;
			_displayBoundingBox.Y= _boundingBox.Y * scale + offsetY;
			_displayBoundingBox.Width= _boundingBox.Width * scale;
			_displayBoundingBox.Height= _boundingBox.Height * scale;

			// transform the children's bounding boxes.
			foreach(NodeViewData node in _children)
				node.UpdateDisplay(offsetX, offsetY, scale);
		}

		/// <summary>
		/// Returns the width of this node and all its child nodes. Internal use only.
		/// </summary>
		/// <param name="paddingWidth">The width kept between the nodes.</param>
		/// <param name="depth">Defines how deep the search will be.</param>
		/// <returns>Returns untransformed width.</returns>
		private float GetTotalWidth(float paddingWidth, int depth)
		{
			if(depth <1)
				return _layoutRectangle.Width;

			float width= _layoutRectangle.Width;

			// if we have children we must keep our distance
			if(_children.Count >0)
				width+= paddingWidth;

			// find the child with the highest width
			float childwidth= 0.0f;
			foreach(NodeViewData node in _children)
				childwidth= Math.Max(childwidth, node.GetTotalWidth(paddingWidth, depth -1));

			// add both
			return width + childwidth;
		}

		/// <summary>
		/// Returns the total size of the node and its child nodes.
		/// </summary>
		/// <param name="paddingWidth">The width kept between the nodes.</param>
		/// <param name="depth">Defines how deep the search will be.</param>
		/// <returns>Returns the untransformed size.</returns>
		public SizeF GetTotalSize(float paddingWidth, int depth)
		{
			return new SizeF(GetTotalWidth(paddingWidth, depth), _layoutRectangle.Height);
		}

		public NodeViewData GetChild(Node node)
		{
			foreach(NodeViewData child in _children)
			{
				if(child.Node ==node)
					return child;
			}

			return null;
		}

		/// <summary>
		/// Draws the edges connecting the nodes.
		/// </summary>
		/// <param name="graphics">The graphics object we render to.</param>
		/// <param name="edgePen">The pen we use for physical nodes.</param>
		/// <param name="edgePenReadOnly">The pen we use for sub-referenced nodes.</param>
		/// <param name="renderDepth">The depth which is still rendered.</param>
		public virtual void DrawEdges(Graphics graphics, Pen edgePen, Pen edgePenReadOnly, int renderDepth)
		{
			_node.DrawEdges(graphics, this, edgePen, edgePenReadOnly);

			// draw children
			if(renderDepth >0)
			{
				foreach(NodeViewData child in _children)
					child.DrawEdges(graphics, edgePen, edgePenReadOnly, renderDepth -1);
			}
		}

		/// <summary>
		/// Draws the node to the graph.
		/// </summary>
		/// <param name="graphics">The graphics object we render to.</param>
		/// <param name="currentNode">The current node under the mouse cursor.</param>
		/// <param name="selectedNode">The currently selected node.</param>
		/// <param name="isDragged">Determines if the node is currently being dragged.</param>
		/// <param name="graphMousePos">The mouse position in the untransformed graph.</param>
		/// <param name="renderDepth">The depth which is still rendered.</param>
		public virtual void Draw(Graphics graphics, NodeViewData currentNode, NodeViewData selectedNode, bool isDragged, PointF graphMousePos, int renderDepth)
		{
			_node.Draw(graphics, this, currentNode ==null ? false : this.Node ==currentNode.Node, selectedNode ==null ? false : this.Node ==selectedNode.Node, isDragged, graphMousePos);

			// draw children
			if(renderDepth >0)
			{
				foreach(NodeViewData child in _children)
					child.Draw(graphics, currentNode, selectedNode, isDragged, graphMousePos, renderDepth -1);
			}
		}

		/// <summary>
		/// Draws the background of the node's comment.
		/// </summary>
		/// <param name="graphics">The graphics object we render to.</param>
		/// <param name="renderDepth">The depth which is still rendered.</param>
		/// <param name="padding">The padding between the nodes.</param>
		public void DrawCommentBackground(Graphics graphics, int renderDepth, SizeF padding)
		{
			// draw comment backgrounds
			_node.DrawCommentBackground(graphics, this, renderDepth, padding);

			// draw children
			if(renderDepth >0)
			{
				foreach(NodeViewData child in _children)
					child.DrawCommentBackground(graphics, renderDepth -1, padding);
			}
		}

		/// <summary>
		/// Draws the text of the node's comment.
		/// </summary>
		/// <param name="graphics">The graphics object we render to.</param>
		/// <param name="renderDepth">The depth which is still rendered.</param>
		public void DrawCommentText(Graphics graphics, int renderDepth)
		{
			// draw comment backgrounds
			_node.DrawCommentText(graphics, this);

			// draw children
			if(renderDepth >0)
			{
				foreach(NodeViewData child in _children)
					child.DrawCommentText(graphics, renderDepth -1);
			}
		}

		public delegate void WasModifiedEventDelegate(NodeViewData node);

		/// <summary>
		/// Is called when the node was modified.
		/// </summary>
		public event WasModifiedEventDelegate WasModified;

		protected void node_WasModified(Node node)
		{
			if(WasModified !=null)
				WasModified(this);
		}

		/// <summary>
		/// Returns if any of the node's parents is a given node.
		/// </summary>
		/// <param name="parent">The node we want to check if it is an ancestor of this node.</param>
		/// <returns>Returns true if this node is a descendant of the given node.</returns>
		public bool HasParent(Node parent)
		{
			if(_parent ==null)
				return false;

			if(_parent.Node ==parent)
				return true;

			return _parent.HasParent(parent);
		}

		/// <summary>
		/// Returns if any of the node's parents is a given behaviour.
		/// </summary>
		/// <param name="behavior">The behavior we want to check if it is an ancestor of this node.</param>
		/// <returns>Returns true if this node is a descendant of the given behavior.</returns>
		public virtual bool HasParentBehavior(BehaviorNode behavior)
		{
			if(behavior ==null)
				return false;

			if(_node ==behavior)
				return true;

			if(_parent ==null)
				return false;

			return _parent.HasParentBehavior(behavior);
		}
	}
}
