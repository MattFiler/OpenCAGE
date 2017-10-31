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
using System.IO;
using Brainiac.Design.Attributes;
using Brainiac.Design.Properties;

namespace Brainiac.Design.Nodes
{
	public interface ReferencedBehaviorNode
	{
		BehaviorNode Reference { get; }
		string ReferenceFilename { get; set; }
		event ReferencedBehavior.ReferencedBehaviorWasModifiedEventDelegate ReferencedBehaviorWasModified;
		Node.Connector GenericChildren { get; }
	}

	/// <summary>
	/// This node represents a referenced behaviour which can be attached to the behaviour tree.
	/// </summary>
	public class ReferencedBehavior : StyledNode, ReferencedBehaviorNode
	{
		private static Brush _theBackgroundBrush= new SolidBrush( Color.FromArgb(119,147,60) );
		private static Brush _theDraggedBackgroundBrush= new SolidBrush( Color.FromArgb(99,122,50) );

		protected Connector _genericChildren;
		public Connector GenericChildren
		{
			get { return _genericChildren; }
		}

		/// <summary>
		/// We use this to store the connector which belongs to this node.
		/// </summary>
		protected ConnectorMultiple _genericChildrenLocal;

		protected BehaviorNode _referencedBehavior;

		/// <summary>
		/// The behaviour which is referenced by this node.
		/// </summary>
		public BehaviorNode Reference
		{
			get
			{
				if(_referencedBehavior ==null)
					LoadReferencedBehavior();

				return _referencedBehavior;
			}
		}

		protected string _referenceFilename;

		/// <summary>
		/// The filename of the referenced behaviour.
		/// </summary>
		[DesignerString("ReferencedBehaviorFilename", "ReferencedBehaviorFilenameDesc", "CategoryBasic", DesignerProperty.DisplayMode.NoDisplay, 0, DesignerProperty.DesignerFlags.ReadOnly|DesignerProperty.DesignerFlags.NoExport)]
		public string ReferenceFilename
		{
			get { return _referenceFilename; }
			set { _referenceFilename= value; }
		}

		/// <summary>
		/// Creates a new referenced behaviour.
		/// </summary>
		/// <param name="rootBehavior">The behaviour this node belongs not. NOT the one is references.</param>
		/// <param name="referencedBehavior">The behaviour you want to reference.</param>
		public ReferencedBehavior(BehaviorNode rootBehavior, BehaviorNode referencedBehavior) : base(null, _theBackgroundBrush, _theDraggedBackgroundBrush, ((Node)referencedBehavior).Label, true, Resources.ReferencedBehaviorDesc)
		{
			_genericChildrenLocal= new ConnectorMultiple(_children, string.Empty, "GenericChildren", 1, int.MaxValue);
			_genericChildren= _genericChildrenLocal;

			// when this node is saved, the children won't as they belong to another behaviour
			_saveChildren= false;

			_referencedBehavior= referencedBehavior;
			_referenceFilename= _referencedBehavior.FileManager.Filename;

			((Node)_referencedBehavior).WasModified+= new WasModifiedEventDelegate(referencedBehavior_WasModified);
		}

		/// <summary>
		/// Creates a new referenced behaviour. The behaviour which will be referenced is read from the Reference attribute.
		/// </summary>
		public ReferencedBehavior() : base(null, _theBackgroundBrush, _theDraggedBackgroundBrush, string.Empty, true, Resources.ReferencedBehaviorDesc)
		{
			_genericChildrenLocal= new ConnectorMultiple(_children, string.Empty, "GenericChildren", 1, int.MaxValue);
			_genericChildren= _genericChildrenLocal;

			// when this node is saved, the children won't as they belong to another behaviour
			_saveChildren= false;

			_referencedBehavior= null;
		}

		/// <summary>
		/// Creates a new instance of a referenced behaviour node for a sub-reference graph.
		/// </summary>
		/// <param name="impulse">The original non-sub-reference graph referenced behaviour node.</param>
		public ReferencedBehavior(ReferencedBehavior referencedBehavior) : base(null, _theBackgroundBrush, _theDraggedBackgroundBrush, referencedBehavior.BaseLabel, false, referencedBehavior.Description)
		{
			_genericChildrenLocal= new ConnectorMultiple(_children, string.Empty, "GenericChildren", 1, int.MaxValue);
			_genericChildren= _genericChildrenLocal;

			// when this node is saved, the children won't as they belong to another behaviour
			_saveChildren= false;

			_referencedBehavior= null;

			referencedBehavior.CloneProperties(this);

			OnPropertyValueChanged(false);

			CopyEventHandlers(referencedBehavior);

#if DEBUG
			_debugIsSubreferencedGraphNode= true;
#endif
		}

		/// <summary>
		/// After loading this node we must update the label and transform the path of the reference into an abolsute path so it can be loaded.
		/// </summary>
		/// <param name="behavior">The behaviour this node belongs to. NOT the referenced one.</param>
		public override void PostLoad(BehaviorNode behavior)
		{
			base.PostLoad(behavior);

			// transform referenced behaviour into an abolute path
			_referenceFilename= behavior.MakeAbsolute(_referenceFilename);

			// update the label
			Label= Path.GetFileNameWithoutExtension(_referenceFilename);
		}

		/// <summary>
		/// Before saving we must change the reference into a relative path.
		/// </summary>
		/// <param name="behavior">The behaviour this node belongs to. NOT the referenced one.</param>
		public override void PreSave(BehaviorNode behavior)
		{
			base.PreSave(behavior);

			// make the path of the reference relative
			_referenceFilename= behavior.MakeRelative(_referenceFilename);
		}

		protected override void CopyEventHandlers(Node from)
		{
			base.CopyEventHandlers(from);
			ReferencedBehaviorWasModified= ((ReferencedBehavior)from).ReferencedBehaviorWasModified;
		}

		/// <summary>
		/// Adds nodes to the referenced behaviour which represent sub-referenced behaviours.
		/// </summary>
		/// <param name="rootBehavior">The behaviour this node belongs to. NOT the referenced one.</param>
		/// <param name="parent">The node the sub-referenced behaviours will be added to.</param>
		/// <param name="node">The current node we are checking.</param>
		protected void GenerateReferencedBehaviorsTree(BehaviorNode rootBehavior, Node parent, Node node)
		{
			// check if this is a referenced behaviour
			if(node is ReferencedBehavior)
			{
				ReferencedBehavior rbnode= (ReferencedBehavior)node;

				// create the dummy node and add it without marking the behaviour as being modified as these are no REAL nodes.
				ReferencedBehavior rb= new ReferencedBehavior(rbnode);

				parent.AddChildNotModified(parent.DefaultConnector, rb);

				// we have a circular reference here. Skip the children
				if(rbnode._referencedBehavior ==rootBehavior)
				{
					rb._genericChildren.IsReadOnly= true;
					return;
				}

				// do the same for all the children
				foreach(Node child in node.Children)
					GenerateReferencedBehaviorsTree(rootBehavior, rb, child);

				rb._genericChildren.IsReadOnly= true;
			}
			else if(node is Impulse)
			{
				// create the dummy node and add it without marking the behaviour as being modified as these are no REAL nodes.
				Impulse ip= new Impulse( (Impulse)node );

				// do the same for all the children
				foreach(Node child in node.Children)
					GenerateReferencedBehaviorsTree(rootBehavior, ip, child);

				if(ip.Children.Count >0)
				{
					parent.AddChildNotModified(parent.DefaultConnector, ip);
					ip.GenericChildren.IsReadOnly= true;
				}
			}
			else
			{
				// do the same for all the children
				foreach(Node child in node.Children)
					GenerateReferencedBehaviorsTree(rootBehavior, parent, child);
			}
		}

		/// <summary>
		/// Loads the referenced behaviour of this node.
		/// </summary>
		protected void LoadReferencedBehavior()
		{
			// if we find a circular reference here we must skip it
			if(FileManagers.FileManager.LoadedBehavior !=null && _referenceFilename ==FileManagers.FileManager.LoadedBehavior.FileManager.Filename)
				return;

			// load the behaviour
			_referencedBehavior= BehaviorManager.Instance.LoadBehavior(_referenceFilename);

			if(_referencedBehavior !=null)
				((Node)_referencedBehavior).WasModified+= new WasModifiedEventDelegate(referencedBehavior_WasModified);
		}

		/// <summary>
		/// Exapnds and collapses the referenced node.
		/// </summary>
		/// <param name="nvdrb">The view data for this node.</param>
		public void ExpandNode(NodeViewDataReferencedBehavior nvdrb)
		{
			// if the referenced behaviours was not yet loaded, try so
			if(_referencedBehavior ==null)
			{
				LoadReferencedBehavior();

				// if we could not load it, skip
				if(_referencedBehavior ==null)
					return;
			}

			// this code can be called without a change of IsExpanded so you cannot assume that the current children were from the other state
			if(nvdrb.IsExpanded)
			{
				// clear the generated sub-reference graph
				_genericChildrenLocal.ClearChildren();

				// remove any remaining subitems because of minimum count
				for(int i= 0; i <_subItems.Count; ++i)
				{
					SubItemConnector subconn= _subItems[i] as SubItemConnector;
					if(subconn !=null && subconn.Connector ==_genericChildren)
					{
						RemoveSubItem(subconn);
						--i;
					}
				}

				// assign the connector of the behaviour
				_genericChildren= _referencedBehavior.GenericChildren;
				_children.SetConnector(_genericChildren);

				// add all the subitems needed
				int count= Math.Max(_genericChildren.ChildCount, _genericChildren.MinCount);
				for(int i= 0; i <count; ++i)
				{
					Node child= i <_genericChildren.ChildCount ? _genericChildren.GetChild(i) : null;
					AddSubItem(new SubItemConnector(_genericChildren, child, i));
				}
			}
			else
			{
				// remove all subitems of the behaviour
				for(int i= 0; i <_subItems.Count; ++i)
				{
					SubItemConnector subconn= _subItems[i] as SubItemConnector;
					if(subconn !=null && subconn.Connector ==_genericChildren)
					{
						RemoveSubItem(subconn);
						--i;
					}
				}

				// assign the connector for the sub-reference graph
				_genericChildren= _genericChildrenLocal;
				_children.SetConnector(_genericChildren);

				// make the connector writable to edit it
				_genericChildren.IsReadOnly= false;

				// add all the subitems needed
				int count= Math.Max(_genericChildren.ChildCount, _genericChildren.MinCount);
				for(int i= 0; i <count; ++i)
				{
					Node child= i <_genericChildren.ChildCount ? _genericChildren.GetChild(i) : null;
					AddSubItem(new SubItemConnector(_genericChildren, child, i));
				}

				// clear the generated sub-reference graph
				_genericChildren.ClearChildren();

				// generate the dummy nodes for the sub-referenced behaviours
				foreach(Node child in ((Node)_referencedBehavior).Children)
					GenerateReferencedBehaviorsTree(nvdrb.RootBehavior, this, child);

				// make the connector read-only so the user cannot modify it
				_genericChildren.IsReadOnly= true;
			}
		}

		/// <summary>
		/// Is called when the node was double-clicked. Used for referenced behaviours.
		/// </summary>
		/// <param name="nvd">The view data of the node in the current view.</param>
		/// <param name="layoutChanged">Does the layout need to be recalculated?</param>
		/// <returns>Returns if the node handled the double click or not.</returns>
		public override bool OnDoubleClick(NodeViewData nvd, out bool layoutChanged)
		{
			NodeViewDataReferencedBehavior nvdrb= (NodeViewDataReferencedBehavior) nvd;
			nvdrb.IsExpanded= !nvdrb.IsExpanded;
			nvdrb.RebuildChildren= true;

			layoutChanged= true;
			return true;
		}

		public delegate void ReferencedBehaviorWasModifiedEventDelegate(ReferencedBehaviorNode node);

		/// <summary>
		/// Event is triggered when the behaviour referenced by this node is modified.
		/// </summary>
		public event ReferencedBehaviorWasModifiedEventDelegate ReferencedBehaviorWasModified;

		/// <summary>
		/// Handles when the behaviour referenced by this node is modified.
		/// </summary>
		/// <param name="node">The referenced behaviour node whose behaviour was modified.</param>
		void referencedBehavior_WasModified(Node node)
		{
			// update the filename and the label
			if( _referencedBehavior !=null &&
				_referencedBehavior.FileManager !=null &&
				_referencedBehavior.FileManager.Filename !=string.Empty )
			{
				_referenceFilename= _referencedBehavior.FileManager.Filename;
				Label= Path.GetFileNameWithoutExtension(_referenceFilename);
			}

			// call the event
			if(ReferencedBehaviorWasModified !=null)
				ReferencedBehaviorWasModified(this);
		}

		public override void CheckForErrors(BehaviorNode rootBehavior, List<ErrorCheck> result)
		{
			if(_referencedBehavior ==null)
				LoadReferencedBehavior();

			// if we have a circular reference we must stop here
			if(_referencedBehavior ==null || _referencedBehavior ==rootBehavior)
			{
				result.Add( new Node.ErrorCheck(this, ErrorCheckLevel.Error, Resources.ReferencedBehaviorCircularReferenceError) );
				return;
			}

			// if our referenced behaviour could be loaded, check it as well for errors
			if(_referencedBehavior !=null)
			{
				foreach(Node child in ((Node)_referencedBehavior).Children)
					child.CheckForErrors(rootBehavior, result);
			}
		}

		/// <summary>
		/// Creates a view for this node. Allows you to return your own class and store additional data.
		/// </summary>
		/// <param name="rootBehavior">The root of the graph of the current view.</param>
		/// <param name="parent">The parent of the NodeViewData created.</param>
		/// <returns>Returns a new NodeViewData object for this node.</returns>
		public override NodeViewData CreateNodeViewData(NodeViewData parent, BehaviorNode rootBehavior)
		{
			return new NodeViewDataReferencedBehavior(parent, rootBehavior, this);
		}

		public override void PreLayoutUpdate(NodeViewData nvd)
		{
			// check if we must update the children
			NodeViewDataReferencedBehavior nvdrb= (NodeViewDataReferencedBehavior) nvd;
			if( nvdrb.RebuildChildren ||
				_referencedBehavior !=null && !nvdrb.IsExpanded && nvdrb.ReferencedBehaviorLastModification !=_referencedBehavior.ModificationID )
			{
				nvdrb.RebuildChildren= false;

				ExpandNode(nvdrb);

				if(_referencedBehavior !=null)
					nvdrb.ReferencedBehaviorLastModification= _referencedBehavior.ModificationID;
			}

			base.PreLayoutUpdate(nvd);
		}

		/// <summary>
		/// Searches a list for NodeViewData for this node. Internal use only.
		/// </summary>
		/// <param name="list">The list which is searched for the NodeViewData.</param>
		/// <returns>Returns null if no fitting NodeViewData could be found.</returns>
		public override NodeViewData FindNodeViewData(List<NodeViewData> list)
		{
			foreach(NodeViewData nvd in list)
			{
				if(nvd.Node is ReferencedBehavior)
				{
					ReferencedBehavior refnode= (ReferencedBehavior) nvd.Node;

						// if both nodes reference the same behaviour we copy the view related data
					if( _referencedBehavior !=null && refnode.Reference ==_referencedBehavior ||
						ReferenceFilename ==refnode.ReferenceFilename )
					{
						NodeViewDataReferencedBehavior nvdrb= (NodeViewDataReferencedBehavior) nvd;
						NodeViewDataReferencedBehavior newdata= (NodeViewDataReferencedBehavior) CreateNodeViewData(nvd.Parent, nvd.RootBehavior);

						// copy data
						newdata.IsExpanded= nvdrb.IsExpanded;
						newdata.ReferencedBehaviorLastModification= nvdrb.ReferencedBehaviorLastModification;

						// return new data
						return newdata;
					}
				}

				if(nvd.Node ==this)
					return nvd;
			}

			return null;
		}

		public override bool AddChild(Connector connector, Node node)
		{
			Debug.Check(connector.Owner ==this || connector.Owner ==_referencedBehavior);

			if(connector.Owner ==this)
				return base.AddChild(connector, node);

			return ((Node)_referencedBehavior).AddChild(connector, node);
		}

		public override bool AddChild(Connector connector, Node node, int index)
		{
			Debug.Check(connector.Owner ==this || connector.Owner ==_referencedBehavior);

			if(connector.Owner ==this)
				return base.AddChild(connector, node, index);

			return ((Node)_referencedBehavior).AddChild(connector, node, index);
		}

		public override bool AddChildNotModified(Node.Connector connector, Node node)
		{
			Debug.Check(connector.Owner ==this || connector.Owner ==_referencedBehavior);

			if(connector.Owner ==this)
				return base.AddChildNotModified(connector, node);

			return ((Node)_referencedBehavior).AddChildNotModified(connector, node);
		}

		public override void RemoveChild(Connector connector, Node node)
		{
			Debug.Check(connector.Owner ==this || connector.Owner ==_referencedBehavior);

			if(connector.Owner ==this)
			{
				base.RemoveChild(connector, node);
				return;
			}

			((Node)_referencedBehavior).RemoveChild(connector, node);
		}

		protected static Brush _defaultBrushCollapsed= new SolidBrush( Color.FromArgb(158, 190, 94) );

		public override void Draw(Graphics graphics, NodeViewData nvd, bool isCurrent, bool isSelected, bool isDragged, PointF graphMousePos)
		{
			Brush defBrush= _defaultStyle.Background;

			NodeViewDataReferencedBehavior nvdrb= (NodeViewDataReferencedBehavior) nvd;
			if(_genericChildren.IsReadOnly)
				_defaultStyle.Background= _defaultBrushCollapsed;

			base.Draw(graphics, nvd, isCurrent, isSelected, isDragged, graphMousePos);

			_defaultStyle.Background= defBrush;
		}

		protected override void CloneProperties(Node newnode)
		{
			base.CloneProperties(newnode);

			ReferencedBehavior refbehav= (ReferencedBehavior)newnode;
			refbehav._referenceFilename= _referenceFilename;
			refbehav._referencedBehavior= _referencedBehavior;
			refbehav.Label= Label;
		}
	}
}
