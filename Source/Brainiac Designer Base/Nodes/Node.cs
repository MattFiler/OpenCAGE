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
using System.Reflection;
using System.Windows.Forms;
using Brainiac.Design.Attributes;
using Brainiac.Design.Properties;

namespace Brainiac.Design.Nodes
{
	/// <summary>
	/// This enumeration defines the shape of the ode when it is showed in the graph.
	/// </summary>
	public enum NodeShape { Rectangle, RoundedRectangle, Capsule, Ellipse };

	/// <summary>
	/// This is the base class for all nodes which are part of a behaviour tree.
	/// </summary>
	public partial class Node : NodeTag.DefaultObject, ICloneable
	{
		/// <summary>
		/// Calculates the exact size of a string.
		/// Code taken from http://www.codeproject.com/KB/GDI-plus/measurestring.aspx
		/// </summary>
		/// <param name="graphics">The graphics object used to calculate the string's size.</param>
		/// <param name="font">The font which will be used to draw the string.</param>
		/// <param name="text">The actual string which will be drawn.</param>
		/// <returns>Returns the untransformed size of the string when being drawn.</returns>
		static public SizeF MeasureDisplayStringWidth(Graphics graphics, string text, Font font)
		{
			// set something to generate the minimum size
			bool minimum= false;
			if(text ==string.Empty)
			{
				minimum= true;
				text= " ";
			}

			System.Drawing.StringFormat format = new System.Drawing.StringFormat();
			System.Drawing.RectangleF rect = new System.Drawing.RectangleF(0, 0, 1000, 1000);
			System.Drawing.CharacterRange[] ranges = { new System.Drawing.CharacterRange(0, text.Length) };
			System.Drawing.Region[] regions = new System.Drawing.Region[1];

			format.SetMeasurableCharacterRanges(ranges);

			regions = graphics.MeasureCharacterRanges(text, font, rect, format);
			rect = regions[0].GetBounds(graphics);

			return minimum ? new SizeF(0.0f, rect.Height) : rect.Size;
		}

		protected List<SubItem> _subItems= new List<SubItem>();

		/// <summary>
		/// The list of subitems handled by this node.
		/// </summary>
		public IList<SubItem> SubItems
		{
			get { return _subItems.AsReadOnly(); }
		}

		/// <summary>
		/// Sorts the subitems so the parallel ones are drawn last, otherwise we get glitches with the backgrounds drawn by the non-prallel subitems.
		/// </summary>
		protected void SortSubItems()
		{
			// find the last parallel subitem from the beginning on
			int lastParallelIndex= -1;
			for(int i= 0; i <_subItems.Count; ++i)
			{
				if(_subItems[i].ShowParallelToLabel)
					lastParallelIndex= i;
				else break;  // once we found a subitem which is not parallel we quit
			}

			// sort the subitems
			for(int i= 0; i <_subItems.Count; ++i)
			{
				// if we found a parallel past the last one we sort it
				if(_subItems[i].ShowParallelToLabel && i >lastParallelIndex)
				{
					SubItem parallel= _subItems[i];
					_subItems.RemoveAt(i--);
					_subItems.Insert(++lastParallelIndex, parallel);
				}
			}
		}

		/// <summary>
		/// Attaches a subitem to this node.
		/// </summary>
		/// <param name="sub">The node subitem we want to attach.</param>
		public void AddSubItem(SubItem sub)
		{
			_subItems.Add(sub);

			SortSubItems();

			_labelChanged= true;
		}

		/// <summary>
		/// Attaches a subitem to this node.
		/// </summary>
		/// <param name="sub">The node subitem we want to attach.</param>
		/// <param name="index">The index where you want to insert the subitem.</param>
		public void AddSubItem(SubItem sub, int index)
		{
			_subItems.Insert(index, sub);

			SortSubItems();

			_labelChanged= true;
		}

		/// <summary>
		/// Removes a subitem from the node.
		/// </summary>
		/// <param name="sub">The subitem which will be removed.</param>
		public void RemoveSubItem(SubItem sub)
		{
			int index= _subItems.IndexOf(sub);

			if(sub ==_selectedSubItem)
			{
				_selectedSubItem.IsSelected= false;
				_selectedSubItem= null;
			}

			if(index <0)
				throw new Exception(Resources.ExceptionSubItemIsNoChild);

			_subItems.RemoveAt(index);
		}

		/// <summary>
		/// Removes the selected event from the node.
		/// </summary>
		public bool RemoveSelectedSubItem()
		{
			if(!_selectedSubItem.CanBeDeleted)
				return false;

			RemoveSubItem(_selectedSubItem);
			return true;
		}

		/// <summary>
		/// The the currently selected subitem.
		/// </summary>
		protected SubItem _selectedSubItem;

		/// <summary>
		/// Returns the currently selected subitem. Is null if no subitem is selected.
		/// </summary>
		public SubItem SelectedSubItem
		{
			get { return _selectedSubItem; }

			set
			{
				if(_selectedSubItem !=null)
					_selectedSubItem.IsSelected= false;

				_selectedSubItem= value;

				if(_selectedSubItem !=null)
					_selectedSubItem.IsSelected= true;
			}
		}

		protected bool _acceptsEvents;

		/// <summary>
		/// Determines if events can be attached to this node or not.
		/// </summary>
		public bool AcceptsEvents
		{
			get { return _acceptsEvents; }
		}

		/// <summary>
		/// Creates a node from a given type.
		/// </summary>
		/// <param name="type">The type we want to create a node of.</param>
		/// <returns>Returns the created node.</returns>
		public static Node Create(Type type)
		{
			Debug.Check(type !=null);

			Node node= (Node)type.InvokeMember(string.Empty, BindingFlags.CreateInstance, null, null, new object[0]);

			if(node ==null)
				throw new Exception(Resources.ExceptionMissingNodeConstructor);

			IList<DesignerPropertyInfo> properties= node.GetDesignerProperties();
			for(int p= 0; p <properties.Count; ++p)
			{
				DesignerProperty att= properties[p].Attribute;

				if(att.Display ==DesignerProperty.DisplayMode.List)
					node.AddSubItem( new Node.SubItemProperty(node, properties[p].Property, att) );
			}

			return node;
		}

		protected bool _saveChildren= true;

		/// <summary>
		/// Determines if the children of this node will be saved. Required for referenced behaviours.
		/// </summary>
		public bool SaveChildren
		{
			get { return _saveChildren; }
		}

		private Node _parent;

		/// <summary>
		/// The parent of this node. Can be null for root.
		/// </summary>
		public Node Parent
		{
			get { return _parent; }
		}

		protected Connector _connector;

		/// <summary>
		/// The connector the ode is connected to its parent with.
		/// </summary>
		public Connector ParentConnector
		{
			get { return _connector; }
		}

		/// <summary>
		/// Holds a list of all children connected to this node by a connector.
		/// </summary>
		protected ConnectedChildren _children;

		/// <summary>
		/// Gets the default connector of the node, used to maintain compatibility with version 1 files.
		/// </summary>
		public Connector DefaultConnector
		{
			get { return _children.DefaultConnector; }
		}

		/// <summary>
		/// The child nodes of this node. Is never null.
		/// </summary>
		public IList<Node> Children
		{
			get { return _children.Children; }
		}

		/// <summary>
		/// Gets a connector of the node by an indentifier.
		/// </summary>
		/// <param name="identifier">The identifier of the connector we are looking for.</param>
		/// <returns>Returns null if no connector could be found.</returns>
		public Connector GetConnector(string identifier)
		{
			return _children.GetConnector(identifier);
		}

		/// <summary>
		/// Gets a connector of the node by a child connected to it.
		/// </summary>
		/// <param name="child">The child of the connector we are looking for.</param>
		/// <returns>Returns null if no connector could be found.</returns>
		public Connector GetConnector(Node child)
		{
			return _children.GetConnector(child);
		}

		/// <summary>
		/// A list of all connectors registered on the node.
		/// </summary>
		public IList<Connector> Connectors
		{
			get { return _children.Connectors; }
		}

		protected string _description;

		/// <summary>
		/// The description of this node.
		/// </summary>
		public string Description
		{
			get { return /*Resources.ResourceManager.GetString(*/_description/*, Resources.Culture)*/; }
		}

		/// <summary>
		/// The tooltip for this node which is shown if the option is enabled in the settings.
		/// </summary>
		public virtual string ToolTip
		{
			get { return _description; }
		}

		/// <summary>
		/// The name of the class we want to use for the exporter. This is usually the implemented node of the game.
		/// </summary>
		public virtual string ExportClass
		{
			get { return GetType().FullName; }
		}

		private Comment _comment;

		/// <summary>
		/// The comment object of the node.
		/// </summary>
		public Comment CommentObject
		{
			get { return _comment; }
		}

		/// <summary>
		/// The text of the comment shown for the node and its children.
		/// </summary>
		[DesignerString("NodeCommentText", "NodeCommentTextDesc", "CategoryComment", DesignerProperty.DisplayMode.NoDisplay, 10, DesignerProperty.DesignerFlags.NoExport|DesignerProperty.DesignerFlags.NoSave)]
		public string CommentText
		{
			get { return _comment ==null ? string.Empty : _comment.Text; }

			set
			{
				string str= value.Trim();

				if(str.Length <1)
				{
					_comment= null;
				}
				else
				{
					if(_comment ==null)
						_comment= new Comment(str);
					else _comment.Text= str;
				}
			}
		}

		/// <summary>
		/// The color of the comment shown for the node and its children.
		/// </summary>
		[DesignerEnum("NodeCommentBackground", "NodeCommentBackgroundDesc", "CategoryComment", DesignerProperty.DisplayMode.NoDisplay, 20, DesignerProperty.DesignerFlags.NoExport|DesignerProperty.DesignerFlags.NoSave, null)]
		public CommentColor CommentBackground
		{
			get { return _comment ==null ? CommentColor.NoColor : _comment.Background; }

			set
			{
				if(_comment !=null)
					_comment.Background= value;
			}
		}

		/// <summary>
		/// Add a new child node.
		/// </summary>
		/// <param name="connector">The connector the node will be added to. Use null for default connector.</param>
		/// <param name="node">The node you want to append.</param>
		/// <returns>Returns true if the child could be added.</returns>
		public virtual bool AddChild(Connector connector, Node node)
		{
			if(!AddChildNotModified(connector, node))
				return false;

			// behaviours must be saved
			BehaviorWasModified();

			return true;
		}

		/// <summary>
		/// Add a new child but the behaviour does not need to be saved.
		/// Used for collapsed referenced behaviours which show the behaviours they reference.
		/// </summary>
		/// <param name="connector">The connector the node will be added to. Use null for default connector.</param>
		/// <param name="node">The node you want to append.</param>
		/// <returns>Returns true if the child could be added.</returns>
		public virtual bool AddChildNotModified(Connector connector, Node node)
		{
			Debug.Check(connector !=null && _children.HasConnector(connector));

			if(!connector.AcceptsChildren(1))
				throw new Exception(Resources.ExceptionNodeHasTooManyChildren);

			if(!connector.AddChild(node))
				return false;

			node._parent= this;

			return true;
		}

		/// <summary>
		/// Add a new child node.
		/// </summary>
		/// <param name="connector">The connector the node will be added to. Use null for default connector.</param>
		/// <param name="node">The node you want to append.</param>
		/// <param name="index">The index of the new node.</param>
		/// <returns>Returns true if the child could be added.</returns>
		public virtual bool AddChild(Connector connector, Node node, int index)
		{
			Debug.Check(connector !=null && _children.HasConnector(connector));

			if(!connector.AcceptsChildren(1))
				throw new Exception(Resources.ExceptionNodeHasTooManyChildren);

			if(!connector.AddChild(node, index))
				return false;

			node._parent= this;

			BehaviorWasModified();

			return true;
		}

		/// <summary>
		/// Removes a child node.
		/// </summary>
		/// <param name="connector">The connector the child is attached to.</param>
		/// <param name="node">The child node we want to remove.</param>
		public virtual void RemoveChild(Connector connector, Node node)
		{
			Debug.Check(connector !=null && _children.HasConnector(connector));

			if(!connector.RemoveChild(node))
				throw new Exception(Resources.ExceptionNodeIsNoChild);

			node._parent= null;

			BehaviorWasModified();
		}

		private string _baselabel;

		/// <summary>
		/// The label used to gerenate the final label which can include parameters and other stuff.
		/// </summary>
		protected string BaseLabel
		{
			get { return _baselabel; }
		}

		private string _label;

		/// <summary>
		/// The label shown on the node.
		/// </summary>
		public string Label
		{
			get { return _label; }

			set
			{
				_label= value; //Resources.ResourceManager.GetString(value, Resources.Culture);
				_labelChanged= true;

				// store the original label so we can automatically generate a new label when an ttribute changes.
				if(_baselabel ==string.Empty)
					_baselabel= _label;

				// when the label changes the size of the node might change as well
				if(WasModified !=null && _label !=_baselabel)
					WasModified(this);
			}
		}

		public override string ToString()
		{
			return _label;
		}

		protected NodeShape _shape;

		/// <summary>
		/// The shape of the node.
		/// </summary>
		public NodeShape Shape
		{
			get { return _shape; }
		}

		protected int _minHeight;

		/// <summary>
		/// The minimum height of the node. Can be expanded by events and the label.
		/// </summary>
		public int MinHeight
		{
			get { return _minHeight; }
		}

		protected int _minWidth;

		/// <summary>
		/// The minimum width of the node. Can be expanded by events and the label.
		/// </summary>
		public int MinWidth
		{
			get { return _minWidth; }
		}

		protected Font _font;
		protected SizeF _labelSize, _realLabelSize;
		protected float _subItemParallelWidth;

		protected Style _defaultStyle;

		/// <summary>
		/// The default style of the node when it is neither hover over or selected.
		/// </summary>
		public Style DefaultStyle
		{
			get { return _defaultStyle; }
		}

		protected Style _currentStyle;

		/// <summary>
		/// The style of the node when the mouse is hovering over it.
		/// </summary>
		public Style CurrentStyle
		{
			get { return _currentStyle; }
		}

		protected Style _selectedStyle;

		/// <summary>
		/// The style of the node when it is selected.
		/// </summary>
		public Style SelectedStyle
		{
			get { return _selectedStyle; }
		}

		protected Style _draggedStyle;

		/// <summary>
		/// The style of the node when it is moved inside the tree and shown as a small tree which is attached to the mouse.
		/// </summary>
		public Style DraggedStyle
		{
			get { return _draggedStyle; }
		}

		protected SizeF _finalSize;

		/// <summary>
		/// The final size of the node in the untransformed graph.
		/// </summary>
		public SizeF FinalSize
		{
			get { return _finalSize; }
		}

		protected bool _labelChanged= true;

		/// <summary>
		/// Creates a new node and attaches the default attributes DebugName and ExportType.
		/// </summary>
		/// <param name="shape">The shape of the node when being rendered.</param>
		/// <param name="defaultStyle">The stle of the node when being neither hovered over nor selected.</param>
		/// <param name="currentStyle">The style of the node when the mouse is hovering over it.</param>
		/// <param name="selectedStyle">The style of the node when it is selected.</param>
		/// <param name="draggedStyle">The style of the node when it is attached to the mouse cursor when moving nodes in the graph.</param>
		/// <param name="label">The default label of the node.</param>
		/// <param name="font">The font used for the label.</param>
		/// <param name="minWidth">The minimum width of the node.</param>
		/// <param name="minHeight">The minimum height of the node.</param>
		/// <param name="acceptsEvents">Defines if events may be attached to this node.</param>
		/// <param name="description">The description of the node shown to the designer.</param>
		public Node(NodeShape shape, Style defaultStyle, Style currentStyle, Style selectedStyle, Style draggedStyle, string label, Font font, int minWidth, int minHeight, bool acceptsEvents, string description)
		{
			_children= new ConnectedChildren(this);

			_label= label;
			_baselabel= label;
			_shape= shape;
			_font= font;
			_minWidth= minWidth;
			_minHeight= minHeight;

			_description= description;

			if(defaultStyle ==null)
				throw new Exception(Resources.ExceptionDefaultStyleNull);

			_defaultStyle= defaultStyle;
			_currentStyle= currentStyle;
			_selectedStyle= selectedStyle;
			_draggedStyle= draggedStyle;

			_acceptsEvents= acceptsEvents;
		}

		/// <summary>
		/// Updates the with of the node. For internal use only. Used to give all children the same width.
		/// </summary>
		/// <param name="width">The untransformed with.</param>
		internal void SetWidth(float width)
		{
			_finalSize.Width= width;
		}

		const float Padding= 6.0f;

		/// <summary>
		/// Calculates the final size of the node.
		/// </summary>
		/// <param name="graphics">The graphics used to measure the size of the labels.</param>
		/// <param name="rootBehavior">The behaviour this node belongs to.</param>
		public virtual void UpdateFinalSize(Graphics graphics, BehaviorNode rootBehavior)
		{
#if DEBUG
			//ensure consistency
			DebugCheckIntegrity();
#endif

			// find the widest node
			float maxWidth= 0.0f;
			foreach(Node node in _children)
			{
				node.UpdateFinalSize(graphics, rootBehavior);

				maxWidth= Math.Max(maxWidth, node.FinalSize.Width);
			}

			// give all children the same width
			foreach(Node node in _children)
				node.SetWidth(maxWidth);

			// update the label if it has changed
			if(_labelChanged)
			{
				_labelChanged= false;
				_labelSize= MeasureDisplayStringWidth(graphics, _label, _font);
				_labelSize.Width+= 2.0f;

				// update the subitems
				float subItemHeight= 0.0f;
				float subItemWidth= 0.0f;
				float subItemParallelHeight= 0.0f;
				_subItemParallelWidth= 0.0f;
				foreach(SubItem subitem in _subItems)
				{
					// call update
					subitem.Update(this, graphics);

					// store the required space depending on parallel and non-parallel subitems
					if(subitem.ShowParallelToLabel)
					{
						subItemParallelHeight+= subitem.Height;
						_subItemParallelWidth= Math.Max(_subItemParallelWidth, subitem.Width);
					}
					else
					{
						subItemHeight+= subitem.Height;
						subItemWidth= Math.Max(subItemWidth, subitem.Width);
					}
				}

				// if we have no parallel subitem, we also need no extra padding
				if(_subItemParallelWidth >0.0f)
					_subItemParallelWidth+= Padding;

				// the height of the label is its own height or the height of all the parallel subitems
				_realLabelSize= _labelSize;
				_labelSize.Width= Math.Max(_labelSize.Width, Math.Max(subItemWidth, _labelSize.Width + _subItemParallelWidth));
				_labelSize.Height= Math.Max(_labelSize.Height + Padding *2.0f, subItemParallelHeight);

				// calculate the final size of the node
				_finalSize.Width= Math.Max(_minWidth, _labelSize.Width + Padding *2.0f);
				_finalSize.Height= Math.Max(_minHeight, _labelSize.Height + subItemHeight);
			}
		}

		/// <summary>
		/// Draws the background and shape of the node
		/// </summary>
		/// <param name="graphics">The grpahics object we render to.</param>
		/// <param name="boundingBox">The untransformed bounding box of the node.</param>
		/// <param name="brush">The brush used for the background.</param>
		public virtual void DrawShapeBackground(Graphics graphics, RectangleF boundingBox, Brush brush)
		{
			switch(_shape)
			{
				case(NodeShape.Rectangle):
					graphics.FillRectangle(brush, boundingBox);
				break;

				case(NodeShape.Ellipse):
					graphics.FillEllipse(brush, boundingBox);
				break;

				case(NodeShape.Capsule):
				case(NodeShape.RoundedRectangle):
					float radius= _shape ==NodeShape.RoundedRectangle ? 10.0f: boundingBox.Height;

					System.Drawing.Extended.ExtendedGraphics extended= new System.Drawing.Extended.ExtendedGraphics(graphics);

					extended.FillRoundRectangle(brush, boundingBox.X, boundingBox.Y, boundingBox.Width, boundingBox.Height, radius);
				break;

				default: throw new Exception(Resources.ExceptionUnhandledNodeShape);
			}
		}

		/// <summary>
		/// Draw the border of the node.
		/// </summary>
		/// <param name="graphics">The grpahics object we render to.</param>
		/// <param name="boundingBox">The untransformed bounding box of the node.</param>
		/// <param name="pen">The pen we use.</param>
		protected virtual void DrawShapeBorder(Graphics graphics, RectangleF boundingBox, Pen pen)
		{
			switch(_shape)
			{
				case(NodeShape.Rectangle):
					graphics.DrawRectangle(pen, boundingBox.X, boundingBox.Y, boundingBox.Width, boundingBox.Height);
				break;

				case(NodeShape.Ellipse):
					graphics.DrawEllipse(pen, boundingBox);
				break;

				case(NodeShape.Capsule):
				case(NodeShape.RoundedRectangle):
					float radius= _shape ==NodeShape.RoundedRectangle ? 10.0f: boundingBox.Height;

					System.Drawing.Extended.ExtendedGraphics extended= new System.Drawing.Extended.ExtendedGraphics(graphics);

					extended.DrawRoundRectangle(pen, boundingBox.X, boundingBox.Y, boundingBox.Width, boundingBox.Height, radius);
				break;

				default: throw new Exception(Resources.ExceptionUnhandledNodeShape);
			}
		}

		/// <summary>
		/// Calculates the untransformed bounding box of a subitem.
		/// </summary>
		/// <param name="nodeBoundingBox">The untransformed bounding box of the node.</param>
		/// <param name="n">The index of the subitem.</param>
		/// <returns>Returns the untransformed bounding box of the subitem.</returns>
		protected RectangleF GetSubItemBoundingBox(RectangleF nodeBoundingBox, int n)
		{
			SubItem subitem= _subItems[n];
			float top;
			if(subitem.ShowParallelToLabel)
			{
				// if our subitem is a parallel one, we center it around the middle of the node

				// first we collect some information about parallel shown subitems
				float totalParallelHeight= 0.0f;
				float previousParallelHeight= 0.0f;
				for(int i= 0; i <_subItems.Count; ++i)
				{
					if(_subItems[i].ShowParallelToLabel)
					{
						if(i <n)
							// store the height of all parallel subitems before the requested one
							previousParallelHeight+= _subItems[i].Height;

						// store the height of all available subitems
						totalParallelHeight+= _subItems[i].Height;
					}
					else
					{
						// all parallel subitems must be next to each other
						break;
					}
				}

				// calculate the final top
				top= nodeBoundingBox.Top + (nodeBoundingBox.Height - totalParallelHeight) *0.5f + previousParallelHeight;
			}
			else
			{
				// if our subitem is not parallel we simply add the height of the label and the height of all previous subitems which are not parallel
				top= nodeBoundingBox.Top + _labelSize.Height;
				for(int i= 0; i <n; ++i)
				{
					if(!_subItems[i].ShowParallelToLabel)
						top+= _subItems[i].Height;
				}
			}

			// return the bounding box of the requested subitem
			return new RectangleF(nodeBoundingBox.X, top, nodeBoundingBox.Width, subitem.Height);
		}

		/// <summary>
		/// Calculates the untransformed bounding box of all connector subitems of a given connector.
		/// </summary>
		/// <param name="nodeBoundingBox">The untransformed bounding box of the node.</param>
		/// <param name="connector">The connector we want the counding box for.</param>
		/// <returns>Returns the untransformed bounding box of the connector's subitems.</returns>
		public RectangleF GetConnectorBoundingBox(RectangleF nodeBoundingBox, Connector connector)
		{
			// first find the first and last parallel subitem
			int firstParallel= -1;
			int lastParallel= -1;
			for(int i= 0; i <_subItems.Count; ++i)
			{
				if(_subItems[i].ShowParallelToLabel)
				{
					if(firstParallel <0)
						firstParallel= i;

					lastParallel= i;
				}
				else
				{
					// all parallel subitems must be next to each other
					break;
				}
			}

			// ensure our retrieved information is correct
			Debug.Check(firstParallel >=0 && lastParallel >=firstParallel);

			float top= -1.0f;
			float bottom= -1.0f;

			// search all subitems for the connector
			bool inConnector= false;
			for(int i= firstParallel; i <=lastParallel; ++i)
			{
				SubItemConnector subitemConn= _subItems[i] as SubItemConnector;

				// if we found a subitem for our connector and we have found none before, we have found the top of the bounding box
				if(!inConnector && subitemConn !=null && subitemConn.Connector ==connector)
				{
					inConnector= true;

					if(i ==firstParallel)
						top= nodeBoundingBox.Top;  // if this is the first parallel, simply extent it to the full height of the node
					else top= GetSubItemBoundingBox(nodeBoundingBox, i).Top;
				}

				// if we found no subitem for our connector and we have found one before, we have found the bottom of the bounding box
				if(inConnector && (subitemConn ==null || subitemConn.Connector !=connector))
				{
					// the previous subitem was the last one for our connector
					if(i -1 ==lastParallel)
						bottom= nodeBoundingBox.Bottom;  // if this is the first parallel, simply extent it to the full height of the node
					else bottom= GetSubItemBoundingBox(nodeBoundingBox, i -1).Bottom;

					break;
				}

				// when we have reached the last parallel subitem, simply extent the bounding box to the height of the node we are the last parallel subitem
				if(i ==lastParallel)
				{
					bottom= nodeBoundingBox.Bottom;
					break;
				}
			}

			// ensure our retrieved data is valid
			Debug.Check(top >=0.0f && bottom >top);

			// return the bounding box of all subitems belonging to the given connector
			return new RectangleF(nodeBoundingBox.X, top, nodeBoundingBox.Width, bottom - top);
		}

		/// <summary>
		/// Draws the node to the graph.
		/// </summary>
		/// <param name="graphics">The graphics object we render to.</param>
		/// <param name="nvd">The view data of this node for drawing.</param>
		/// <param name="isCurrent">Determines if the node is currently hovered over.</param>
		/// <param name="isSelected">Determines if the node is selected.</param>
		/// <param name="isDragged">Determines if the node is currently being dragged.</param>
		/// <param name="graphMousePos">The mouse position in the untransformed graph.</param>
		public virtual void Draw(Graphics graphics, NodeViewData nvd, bool isCurrent, bool isSelected, bool isDragged, PointF graphMousePos)
		{
#if DEBUG
			//ensure consistency
			DebugCheckIntegrity();
#endif

			RectangleF boundingBox= nvd.BoundingBox;

			// assemble the correct style
			Style style= _defaultStyle;

			if(isDragged)
				style+= _draggedStyle;
			else if(isCurrent)
				style+= _currentStyle;
			else if(isSelected)
				style+= _selectedStyle;

			if(style.Background !=null)
				DrawShapeBackground(graphics, boundingBox, style.Background);

			// if the node is dragged, do not render the events
			if(!isDragged)
			{
				// if this node is not selected, deselect the event
				if(!isSelected && _selectedSubItem !=null)
				{
					_selectedSubItem.IsSelected= false;
					_selectedSubItem= null;
				}

				if(_subItems.Count >0)
				{
					Region prevreg= graphics.Clip;

					// draw non parallel subitems first
					for(int i= 0; i <_subItems.Count; ++i)
					{
						if(!_subItems[i].ShowParallelToLabel)
						{
							// get the bounding box of the event
							RectangleF newclip= GetSubItemBoundingBox(boundingBox, i);
							graphics.Clip= new Region(newclip);

							_subItems[i].Draw(graphics, nvd, newclip);
						}
					}

					// draw parallel subitems second
					for(int i= 0; i <_subItems.Count; ++i)
					{
						if(_subItems[i].ShowParallelToLabel)
						{
							// get the bounding box of the event
							RectangleF newclip= GetSubItemBoundingBox(boundingBox, i);
							graphics.Clip= new Region(newclip);

							_subItems[i].Draw(graphics, nvd, newclip);
						}
					}

					// restore rendering area
					graphics.Clip= prevreg;
				}

				// draw the label of the node
				if(style.Label !=null)
				{
					// calculate the height of all non-parallel subitems so we can correctly center the label
					float subItemsHeight= 0.0f;
					foreach(SubItem sub in _subItems)
					{
						if(!sub.ShowParallelToLabel)
							subItemsHeight+= sub.Height;
					}

					float x= boundingBox.Left + (boundingBox.Width - _subItemParallelWidth) *0.5f - _realLabelSize.Width *0.5f;
					float y= boundingBox.Top + boundingBox.Height *0.5f - subItemsHeight *0.5f - _realLabelSize.Height *0.5f;
					graphics.DrawString(_label, _font, style.Label, x, y);

					//graphics.DrawRectangle(Pens.Red, boundingBox.X, boundingBox.Y, boundingBox.Width, boundingBox.Height);
					//graphics.DrawRectangle(Pens.Red, x, y, _realLabelSize.Width, _realLabelSize.Height);
					//graphics.DrawRectangle(Pens.Green, x, y, _labelSize.Width, _labelSize.Height);
				}
			}

			// draw the nodes border
			if(style.Border !=null)
				DrawShapeBorder(graphics, boundingBox, style.Border);

			//graphics.DrawRectangle(Pens.Red, nvd.LayoutRectangle.X, nvd.LayoutRectangle.Y, nvd.LayoutRectangle.Width, nvd.LayoutRectangle.Height);
		}

		/// <summary>
		/// Draws the edges connecting the nodes.
		/// </summary>
		/// <param name="graphics">The graphics object we render to.</param>
		/// <param name="nvd">The view data of this node in the current view.</param>
		/// <param name="edgePen">The pen used for normal connectors.</param>
		/// <param name="edgePenReadOnly">The pen used for read-only connectors.</param>
		public virtual void DrawEdges(Graphics graphics, NodeViewData nvd, Pen edgePen, Pen edgePenReadOnly)
		{
			RectangleF boundingBox= nvd.BoundingBox;

			// calculate an offset so we cannot see the end or beginning of the rendered edge
			float edgePenHalfWidth= edgePen.Width *0.5f;

			foreach(NodeViewData node in nvd.Children)
			{
				RectangleF nodeBoundingBox= node.BoundingBox;

				// calculate the centre between both nodes and of the edge
				float middle= boundingBox.Right + (nodeBoundingBox.Left - boundingBox.Right) *0.5f;

				// end at the middle of the other node
				float nodeHeight= nodeBoundingBox.Top + nodeBoundingBox.Height *0.5f;

				// find the correct connector for this node
				for(int i= 0; i <_subItems.Count; ++i)
				{
					SubItemConnector conn= _subItems[i] as SubItemConnector;
					if(conn !=null && conn.Child ==node.Node)
					{
						// get the bounding box of the event
						RectangleF subitemBoundingBox= GetSubItemBoundingBox(boundingBox, i);

						// start at the middle of the connector
						float connectorHeight= subitemBoundingBox.Top + subitemBoundingBox.Height *0.5f;

						graphics.DrawBezier(conn.Connector.IsReadOnly ? edgePenReadOnly : edgePen,
											boundingBox.Right - edgePenHalfWidth, connectorHeight,
											middle, connectorHeight,
											middle, nodeHeight,
											nodeBoundingBox.Left + edgePenHalfWidth, nodeHeight);

						break;
					}
				}
			}
		}

		/// <summary>
		/// Draws the background of the comment.
		/// </summary>
		/// <param name="graphics">The graphics object we render to.</param>
		/// <param name="nvd">The view data of this node in the current view.</param>
		/// <param name="renderDepth">The depth which is still rendered.</param>
		/// <param name="padding">The padding between the nodes.</param>
		public void DrawCommentBackground(Graphics graphics, NodeViewData nvd, int renderDepth, SizeF padding)
		{
			if(_comment !=null)
				_comment.DrawBackground(graphics, nvd, renderDepth, padding);
		}

		/// <summary>
		/// Draws the text of the comment.
		/// </summary>
		/// <param name="graphics">The graphics object we render to.</param>
		/// <param name="nvd">The view data of this node in the current view.</param>
		public void DrawCommentText(Graphics graphics, NodeViewData nvd)
		{
			if(_comment !=null)
				_comment.DrawText(graphics, nvd);
		}

		/// <summary>
		/// Is called when the node was double-clicked. Used for referenced behaviours.
		/// </summary>
		/// <param name="nvd">The view data of the node in the current view.</param>
		/// <param name="layoutChanged">Does the layout need to be recalculated?</param>
		/// <returns>Returns if the node handled the double click or not.</returns>
		public virtual bool OnDoubleClick(NodeViewData nvd, out bool layoutChanged)
		{
			layoutChanged= false;
			return false;
		}

		/// <summary>
		/// Generates a new label by adding the attributes to the label as arguments
		/// </summary>
		/// <returns>Returns the label with a list of arguments.</returns>
		protected string GenerateNewLabel()
		{
			// generate the new label with the arguments
			string newlabel= _baselabel +"(";
			int paramCount= 0;

			// check all properties for one which must be shown as a parameter on the node
			IList<DesignerPropertyInfo> properties= GetDesignerProperties(DesignerProperty.SortByDisplayOrder);
			for(int p= 0; p <properties.Count; ++p)
			{
				// property must be shown as a parameter on the node
				if(properties[p].Attribute.Display ==DesignerProperty.DisplayMode.Parameter)
				{
					newlabel+= properties[p].GetDisplayValue(this) +", ";
					paramCount++;
				}
			}

			// only return the new label when it contains any parameters
			return paramCount >0 ? newlabel.Substring(0, newlabel.Length -2) +")" : _baselabel;
		}

		/// <summary>
		/// Is called when a property of the selected event of this node was modified.
		/// </summary>
		/// <param name="wasModified">Holds if the node was modified.</param>
		public virtual void OnSubItemPropertyValueChanged(bool wasModified)
		{
			// when the label changes the size of the node might change as well
			if(WasModified !=null)
				WasModified(this);

			if(wasModified)
				BehaviorWasModified();
		}

		/// <summary>
		/// Is called when one of the node's proterties were modified.
		/// </summary>
		/// <param name="wasModified">Holds if the event was modified.</param>
		public virtual void OnPropertyValueChanged(bool wasModified)
		{
			Label= GenerateNewLabel();

			if(wasModified)
				BehaviorWasModified();
		}

		public delegate void WasModifiedEventDelegate(Node node);

		/// <summary>
		/// Is called when the node was modified.
		/// </summary>
		public event WasModifiedEventDelegate WasModified;

		/// <summary>
		/// For internal use only.
		/// </summary>
		public void DoWasModified()
		{
			if(WasModified !=null)
				WasModified(this);
		}

		/// <summary>
		/// Mark the behaviour this node belongs to as being modified.
		/// </summary>
		public virtual void BehaviorWasModified()
		{
			if(_parent !=null)
				_parent.BehaviorWasModified();
		}

		/// <summary>
		/// Returns if any of the node's parents is a given node.
		/// </summary>
		/// <param name="parent">The node we want to check if it is a ancestor of this node.</param>
		/// <returns>Returns true if this node is a descendant of the given node.</returns>
		public bool HasParent(Node parent)
		{
			if(_parent ==null)
				return false;

			if(_parent ==parent)
				return true;

			return _parent.HasParent(parent);
		}

		/// <summary>
		/// The sibling node before this one.
		/// </summary>
		public Node PreviousNode
		{
			get
			{
				if(_parent ==null)
					return null;

				int n= _parent.Children.IndexOf(this);
				return n >0 ? _parent.Children[n-1] : null;
			}
		}

		/// <summary>
		/// The sibling node after this one.
		/// </summary>
		public Node NextNode
		{
			get
			{
				if(_parent ==null)
					return null;

				int n= _parent.Children.IndexOf(this);
				return n <_parent.Children.Count -1 ? _parent.Children[n+1] : null;
			}
		}

		/// <summary>
		/// The behaviour this node belongs to.
		/// </summary>
		public BehaviorNode Behavior
		{
			get
			{
				Node node= this;
				while(node !=null)
				{
					if(node is BehaviorNode)
						return (BehaviorNode) node;

					node= node._parent;
				}

				return null;
			}
		}

		/// <summary>
		/// Returns if a given noe is a sibling of this node.
		/// </summary>
		/// <param name="sibling">The assumed sibling we want to check.</param>
		/// <returns>Returns true if the given node is a sibling of this node.</returns>
		public bool IsSibling(Node sibling)
		{
			if(_parent ==null)
				return false;

			return _parent.Children.Contains(sibling);
		}

		/// <summary>
		/// Returns if the given node is the parent of this node and it is its last child.
		/// </summary>
		/// <param name="node">The parent we want to check if this is its last child.</param>
		/// <returns>Returns true if this node is the last child of the given node.</returns>
		public bool IsLastChildOf(Node parent)
		{
			if(parent ==null || parent.Children.Count <1)
				return false;

			return parent.Children[parent.Children.Count -1] ==this;
		}

		/// <summary>
		/// Is called after a property of a node was initialised, allowing further processing.
		/// </summary>
		/// <param name="property">The property which was initialised.</param>
		public virtual void PostPropertyInit(DesignerPropertyInfo property)
		{
		}

		/// <summary>
		/// Is called after the behaviour was loaded.
		/// </summary>
		/// <param name="behavior">The behaviour this node belongs to.</param>
		public virtual void PostLoad(BehaviorNode behavior)
		{
		}

		/// <summary>
		/// Is called before the behaviour is saved.
		/// </summary>
		/// <param name="behavior">The behaviour this node belongs to.</param>
		public virtual void PreSave(BehaviorNode behavior)
		{
		}

		/// <summary>
		/// Returns the name of the node's type for the attribute ExportType.
		/// This is done as the class attribute can be quite long and bad to handle.
		/// </summary>
		/// <returns>Returns the value for ExportType</returns>
		protected virtual string GetExportType()
		{
			return GetType().Name;
		}

		/// <summary>
		/// Checks the current node and its children for errors.
		/// </summary>
		/// <param name="rootBehavior">The behaviour we are currently checking.</param>
		/// <param name="result">The list the errors are added to.</param>
		public virtual void CheckForErrors(BehaviorNode rootBehavior, List<ErrorCheck> result)
		{
			foreach(Node node in _children)
				node.CheckForErrors(rootBehavior, result);
		}

		/// <summary>
		/// Is called when a possible selection of an event occured.
		/// </summary>
		/// <param name="nvd">The view data of the node in the current view.</param>
		/// <param name="graphMousePos">The mouse position in the untransformed graph.</param>
		public virtual void ClickEvent(NodeViewData nvd, PointF graphMousePos)
		{
			SubItem newsub= null;

			for(int i= 0; i <_subItems.Count; ++i)
			{
				if(_subItems[i].SelectableObject !=null)
				{
					RectangleF bbox= GetSubItemBoundingBox(nvd.BoundingBox, i);
					if(bbox.Contains(graphMousePos))
					{
						newsub= _subItems[i];
						break;
					}
				}
			}

			SelectedSubItem= newsub;
		}

		/// <summary>
		/// This method is called before the layout gets updated so you can add new children or remove some. Used by referenced behaviour nodes.
		/// </summary>
		/// <param name="nvd">Thew NodeViewData of the node in the current view. Children can be invalid!</param>
		public virtual void PreLayoutUpdate(NodeViewData nvd)
		{
		}

		/// <summary>
		/// Creates a view for this node. Allows you to return your own class and store additional data.
		/// </summary>
		/// <param name="rootBehavior">The root of the graph of the current view.</param>
		/// <param name="parent">The parent of the NodeViewData created.</param>
		/// <returns>Returns a new NodeViewData object for this node.</returns>
		public virtual NodeViewData CreateNodeViewData(NodeViewData parent, BehaviorNode rootBehavior)
		{
			return new NodeViewData(parent, rootBehavior, this);
		}

		/// <summary>
		/// Searches a list for NodeViewData for this node. Internal use only.
		/// </summary>
		/// <param name="list">The list which is searched for the NodeViewData.</param>
		/// <returns>Returns null if no fitting NodeViewData could be found.</returns>
		public virtual NodeViewData FindNodeViewData(List<NodeViewData> list)
		{
			foreach(NodeViewData nvd in list)
			{
				if(nvd.Node ==this)
					return nvd;
			}

			return null;
		}

		/// <summary>
		/// Copies all the event handlers from one node to this one.
		/// </summary>
		/// <param name="from">Then node you want to copy the event handlers from.</param>
		protected virtual void CopyEventHandlers(Node from)
		{
			WasModified= from.WasModified;
		}

		/// <summary>
		/// Internally used by CloneBranch.
		/// </summary>
		/// <param name="newparent">The parent the clone children will be added to.</param>
		private void CloneChildNodes(Node newparent)
		{
			// we may not clone children of a referenced behavior
			if(newparent is ReferencedBehaviorNode)
				return;

			// for every connector...
			foreach(Connector connector in _children.Connectors)
			{
				// find the one from the new node...
				Connector localconn= newparent.GetConnector(connector.Identifier);
				Debug.Check(localconn !=null);

				// and duplicate its children into the new node's connector
				for(int i= 0; i <connector.ChildCount; ++i)
				{
					Node child= connector.GetChild(i);

					Node newchild= (Node)child.Clone();
					newparent.AddChild(localconn, newchild);

					// do this for the children as well
					child.CloneChildNodes(newchild);
				}
			}
		}

		/// <summary>
		/// Duplicates a node and all of its children.
		/// </summary>
		/// <returns>New node with new children.</returns>
		public Node CloneBranch()
		{
			Node newnode;
			if(this is ReferencedBehaviorNode)
			{
				// if we want to clone the branch of a referenced behaviour we have to create a new behaviour node for that.
				// this should only be used to visualise stuff, never in the behaviour tree itself!
				newnode= Create(typeof(Behavior));
				newnode.Label= Label;
			}
			else
			{
				newnode= Create(GetType());
				CloneProperties(newnode);
			}

			CloneChildNodes(newnode);

			newnode.OnPropertyValueChanged(false);

			return newnode;
		}

		/// <summary>
		/// Duplicates this node. Parent and children are not copied.
		/// </summary>
		/// <returns>New node without parent and children.</returns>
		public object Clone()
		{
			Node newnode= Create(GetType());

			CloneProperties(newnode);

			newnode.OnPropertyValueChanged(false);

			return newnode;
		}

		/// <summary>
		/// Used to duplicate all properties. Any property added must be duplicated here as well.
		/// </summary>
		/// <param name="newnode">The new node which is supposed to get a copy of the properties.</param>
		protected virtual void CloneProperties(Node newnode)
		{
			foreach(Node.SubItem sub in _subItems)
			{
				Node.SubItem newsub= sub.Clone(newnode);
				if(newsub !=null)
					newnode.AddSubItem(newsub);
			}
		}

		/// <summary>
		/// Returns if the parent of this node can adopt the children of this node.
		/// </summary>
		public bool ParentCanAdoptChildren
		{
			get { return _connector !=null && _connector.AcceptsChildren(_children.ChildCount -1); }
		}

		/// <summary>
		/// Checks if a node can be adopted by this one.
		/// </summary>
		/// <param name="child">The node we want to adopt.</param>
		/// <returns>Returns true if this node can adopt the given child.</returns>
		public bool CanAdoptNode(Node child)
		{
			return _children.CanAdoptNode(child);
		}

		public bool CanAdoptChildren(Node parentNode)
		{
			return _children.DefaultConnector !=null && _children.DefaultConnector.AcceptsChildren(parentNode.Children.Count);
		}

		/// <summary>
		/// This node will be removed from its parent and its children. The parent tries to adopt all children.
		/// </summary>
		/// <returns>Returns false if the parent cannot apobt the children and the operation fails.</returns>
		public bool ExtractNode()
		{
			// we cannot adopt children from a referenced behavior
			if(this is ReferencedBehaviorNode)
			{
				_parent.RemoveChild(_connector, this);
				return true;
			}

			// check if the parent is allowed to adopt the children
			if(ParentCanAdoptChildren)
			{
				Connector conn= _connector;
				Node parent= _parent;

				int n= conn.GetChildIndex(this);
				Debug.Check(n >=0);

				parent.RemoveChild(conn, this);

				// let the node's parent adopt all the children
				foreach(Connector connector in _children.Connectors)
				{
					for(int i= 0; i <connector.ChildCount; ++i, ++n)
						parent.AddChild(conn, connector[i], n);

					// remove the adopted children from the old connector. Do NOT clear the _connector member which already points to the new connector.
					connector.ClearChildrenInternal();
				}

				return true;
			}

			return false;
		}

		/// <summary>
		/// Returns a list of all properties which have a designer attribute attached.
		/// </summary>
		/// <returns>A list of all properties relevant to the designer.</returns>
		public IList<DesignerPropertyInfo> GetDesignerProperties()
		{
			return DesignerProperty.GetDesignerProperties(GetType());
		}

		/// <summary>
		/// Returns a list of all properties which have a designer attribute attached.
		/// </summary>
		/// <param name="comparison">The comparison used to sort the design properties.</param>
		/// <returns>A list of all properties relevant to the designer.</returns>
		public IList<DesignerPropertyInfo> GetDesignerProperties(Comparison<DesignerPropertyInfo> comparison)
		{
			return DesignerProperty.GetDesignerProperties(GetType(), comparison);
		}

#if DEBUG
		public virtual void DebugCheckIntegrity()
		{
			Debug.Check((_parent ==null) ==(_connector ==null));
			Debug.Check(_connector ==null || _connector.Owner ==_parent);
			Debug.Check(_connector ==null || _connector.HasChild(this));

			if(_parent !=null && _parent is ReferencedBehaviorNode)
				Debug.Check(_debugIsSubreferencedGraphNode);
		}

		protected bool _debugIsSubreferencedGraphNode= false;
		public bool DebugIsSubreferencedGraphNode
		{
			get { return _debugIsSubreferencedGraphNode; }
		}
#endif
	}
}
