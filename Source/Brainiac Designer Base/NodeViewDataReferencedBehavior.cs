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
using Brainiac.Design.Nodes;

namespace Brainiac.Design
{
	/// <summary>
	/// Special NodeViewData for referenced behaviours
	/// </summary>
	public class NodeViewDataReferencedBehavior : NodeViewData
	{
		public NodeViewDataReferencedBehavior(NodeViewData parent, Nodes.BehaviorNode rootBehavior, Nodes.ReferencedBehaviorNode node) : base(parent, rootBehavior, (Node)node)
		{
		}

		protected bool _isExpanded= false;

		/// <summary>
		/// Determines if the node is expanded or not.
		/// </summary>
		public bool IsExpanded
		{
			get { return _isExpanded; }
			set { _isExpanded= value; }
		}

		protected bool _rebuildChildren= true;

		/// <summary>
		/// Determines the childen should be rebuilt depending on IsExpanded;
		/// </summary>
		public bool RebuildChildren
		{
			get { return _rebuildChildren; }
			set { _rebuildChildren= value; }
		}

		protected int _referencedBehaviourLastModification= -1;

		/// <summary>
		/// Holds the modification ID of the referenced behaviour.
		/// </summary>
		public int ReferencedBehaviorLastModification
		{
			get { return _referencedBehaviourLastModification; }
			set { _referencedBehaviourLastModification= value; }
		}

		/// <summary>
		/// This function adapts the children of the view that they represent the children of the node this view is for.
		/// Children are added and removed.
		/// </summary>
		/// <param name="processedBehaviors">A list of previously processed behaviours to deal with circular references.</param>
		public override void SynchronizeWithNode(IList<BehaviorNode> processedBehaviors)
		{
			// if we have a circular reference, we must skip it
			ReferencedBehaviorNode rb= (ReferencedBehaviorNode) _node;

			if(rb.Reference ==null || processedBehaviors.Contains(rb.Reference))
			{
				_children.Clear();
				return;
			}

			List<BehaviorNode> newProcessedBehaviors= new List<BehaviorNode>(processedBehaviors);
			newProcessedBehaviors.Add(rb.Reference);

			base.SynchronizeWithNode(newProcessedBehaviors.AsReadOnly());
		}

		/// <summary>
		/// Returns the first NodeViewData which is associated with the given node. Notice that there might be other NodeViewDatas which are ignored.
		/// </summary>
		/// <param name="node">The node you want to get the NodeViewData for.</param>
		/// <returns>Returns the first NodeViewData found.</returns>
		public override NodeViewData FindNodeViewData(Node node)
		{
			if(node is ReferencedBehaviorNode)
			{
				ReferencedBehaviorNode refnode= (ReferencedBehaviorNode) _node;
				ReferencedBehaviorNode refnode2= (ReferencedBehaviorNode) node;

				if(refnode.Reference ==refnode2.Reference)
					return this;
			}

			return base.FindNodeViewData(node);
		}

		/// <summary>
		/// Returns if any of the node's parents is a given behaviour.
		/// </summary>
		/// <param name="behavior">The behavior we want to check if it is an ancestor of this node.</param>
		/// <returns>Returns true if this node is a descendant of the given behavior.</returns>
		public override bool HasParentBehavior(BehaviorNode behavior)
		{
			if(behavior ==null)
				return false;

			ReferencedBehaviorNode refb= (ReferencedBehaviorNode)_node;
			Debug.Check(refb.Reference !=null);

			if(refb.Reference ==behavior)
				return true;

			if(_parent ==null)
				return false;

			return _parent.HasParentBehavior(behavior);
		}
	}
}
