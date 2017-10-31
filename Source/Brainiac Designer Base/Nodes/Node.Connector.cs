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
using Brainiac.Design.Properties;

namespace Brainiac.Design.Nodes
{
	public partial class Node
	{
		/// <summary>
		/// A connector which is used to allow a node to have children.
		/// </summary>
		public abstract class Connector
		{
			/// <summary>
			/// The number of children connected.
			/// </summary>
			public abstract int ChildCount { get; }

			/// <summary>
			/// Gets a child with a given index.
			/// </summary>
			/// <param name="index">The index of the child we want to get.</param>
			/// <returns>Returns the requested child.</returns>
			public abstract Node GetChild(int index);

			/// <summary>
			/// Adds a child to this connector.
			/// </summary>
			/// <param name="node">The node we want to add.</param>
			/// <returns>Returns false if the connector does not allow any more nodes to be added.</returns>
			public abstract bool AddChild(Node node);

			/// <summary>
			/// Adds a child to this connector on a given position.
			/// </summary>
			/// <param name="node">The node we want to add.</param>
			/// <param name="index">The position we want to add the node at.</param>
			/// <returns>Returns false if the connector does not allow any more nodes to be added.</returns>
			public abstract bool AddChild(Node node, int index);

			/// <summary>
			/// Checks if the connector can connect to more children.
			/// </summary>
			/// <param name="count">The number of children we want to connect.</param>
			/// <returns>Returns true if the given number of children can be connected.</returns>
			public abstract bool AcceptsChildren(int count);

			/// <summary>
			/// Removes a child from this connector.
			/// </summary>
			/// <param name="node">The child we want to remove.</param>
			/// <returns>Returns false if the connector is read-only.</returns>
			public abstract bool RemoveChild(Node node);

			/// <summary>
			/// Clears all connected children.
			/// </summary>
			public abstract void ClearChildren();

			/// <summary>
			/// Clears all connected children but does not clear the stored connector of the children. Used to improve performance in some situations.
			/// </summary>
			public abstract void ClearChildrenInternal();

			/// <summary>
			/// Gets the index of a child.
			/// </summary>
			/// <param name="node">The child whose index we want to have.</param>
			/// <returns>Returns -1 if the child could not be found.</returns>
			public abstract int GetChildIndex(Node node);

			/// <summary>
			/// Allows direct access to the connected children.
			/// </summary>
			/// <param name="n">The index of the child we want to get.</param>
			/// <returns>Returns the requested child.</returns>
			public Node this[int n]
			{
				get { return GetChild(n); }
			}

			/// <summary>
			/// The ConnectedChildren the connector is registered at.
			/// </summary>
			protected ConnectedChildren _connectedChildren;

			protected int _minCount;

			/// <summary>
			/// The minimum number of connectors shown on the node.
			/// </summary>
			public int MinCount
			{
				get { return _minCount; }
			}

			protected int _maxCount;

			/// <summary>
			/// The maximum number of children accepted by the connector.
			/// </summary>
			public int MaxCount
			{
				get { return _maxCount; }
			}

			protected string _label;

			/// <summary>
			/// Stores if the label contains {0} for the index
			/// </summary>
			protected bool _labelIncludesIndex;

			/// <summary>
			/// The label used to generate the individual label for each connected child.
			/// </summary>
			public string BaseLabel
			{
				get { return _label; }

				set
				{
					_label= value;
					_labelIncludesIndex= value.Contains("{0}");
				}
			}

			protected string _identifier;

			/// <summary>
			/// The identifier of the connector.
			/// </summary>
			public string Identifier
			{
				get { return _identifier; }
			}

			protected bool _isReadOnly= false;

			/// <summary>
			/// Stores if the connector is read-only, used for displaying sub-referenced behaviours and impulses.
			/// </summary>
			public bool IsReadOnly
			{
				get { return _isReadOnly; }
				set { _isReadOnly= value; }
			}

			/// <summary>
			/// The node this connector belongs to.
			/// </summary>
			public Node Owner
			{
				get { return _connectedChildren.Owner; }
			}

			/// <summary>
			/// Creates a new connector.
			/// </summary>
			/// <param name="connectedChildren">Usually the _children member of a node.</param>
			/// <param name="label">The label which is used to generate the individual label for each connected child. May contain {0} to include the index.</param>
			/// <param name="identifier">The identifier of the connector.</param>
			/// <param name="minCount">The minimum number of subitems shown for the connector.</param>
			/// <param name="maxCount">The maximum number of children the connector can have.</param>
			public Connector(ConnectedChildren connectedChildren, string label, string identifier, int minCount, int maxCount)
			{
				Debug.Check(connectedChildren !=null);
				Debug.Check(minCount >=1);
				Debug.Check(maxCount >=minCount);

				_connectedChildren= connectedChildren;
				BaseLabel= label;
				_identifier= identifier;
				_minCount= minCount;
				_maxCount= maxCount;

				// register the connector
				_connectedChildren.RegisterConnector(this);
			}

			/// <summary>
			/// Gnerates an individual label for a conencted child.
			/// </summary>
			/// <param name="index">The index of the child connected to this connector.</param>
			/// <returns>Returns individual label for connected child.</returns>
			public virtual string GetLabel(int index)
			{
				return _labelIncludesIndex ? string.Format(_label, index +1) : _label;
			}

			/// <summary>
			/// Generates a list of connector subitems.
			/// </summary>
			/// <param name="firstIndex">The first connector subitem found on the node.</param>
			/// <returns>Returns alist of all connector subitems.</returns>
			protected List<SubItemConnector> CollectSubItems(out int firstIndex)
			{
				List<SubItemConnector> list= new List<SubItemConnector>();

				firstIndex= -1;

				// for each subitem...
				for(int i= 0; i <_connectedChildren.Owner.SubItems.Count; ++i)
				{
					// check if it is a connector subitem
					SubItemConnector subconn= _connectedChildren.Owner.SubItems[i] as SubItemConnector;
					if(subconn !=null && subconn.Connector ==this)
					{
						// remember the index of the first connector subitem found
						if(firstIndex ==-1)
							firstIndex= i;

						// add subitem to list
						list.Add(subconn);
					}
					else
					{
						// subitems of a connector must be next to each other
						if(firstIndex >=0)
							break;
					}
				}

				// check if we have found any subitems
				if(list.Count <1)
					throw new Exception(Resources.ExceptionNoSubItemForConnector);

				// check that we have found enough of them and not too many
				Debug.Check(list.Count >=_minCount && list.Count <=_maxCount);

				return list;
			}

			/// <summary>
			/// Rebuilds the stored indices on the connector subitems.
			/// </summary>
			protected void RebuildSubItemIndices()
			{
				int firstIndex;
				List<SubItemConnector> subitems= CollectSubItems(out firstIndex);

				// check that we have a connector subitem for every child. Due to the minimum count, there can be more connector subitems than children
				Debug.Check(subitems.Count >=ChildCount);

				for(int i= 0; i <subitems.Count; ++i)
				{
					// assign correct index
					subitems[i].Index= i;

#if DEBUG
					// check if the stored child is the same from the connector
					if(i <ChildCount)
						Debug.Check(subitems[i].Child ==GetChild(i));
#endif
				}
			}

			/// <summary>
			/// Adds a connector subitem for a given child.
			/// </summary>
			/// <param name="child">The child we want to add a connecor subitem for.</param>
			protected void AddSubItem(Node child)
			{
				int firstIndex;
				List<SubItemConnector> subitems= CollectSubItems(out firstIndex);

				// check if there is a connector subitem with child due to the minimum count
				foreach(SubItemConnector subitem in subitems)
				{
					if(subitem.Child ==null)
					{
						// simply reuse the connector subitem
						subitem.Child= child;
						return;
					}
				}

				// add a new connector subitem
				_connectedChildren.Owner.AddSubItem(new SubItemConnector(this, child, subitems.Count), firstIndex + subitems.Count);
			}

			/// <summary>
			/// Adds a connector subitem for a given child at a given position.
			/// </summary>
			/// <param name="child">The child we want to add a connector subitem for.</param>
			/// <param name="index">The position we want to add the subitem at.</param>
			protected void AddSubItem(Node child, int index)
			{
				int firstIndex;
				List<SubItemConnector> subitems= CollectSubItems(out firstIndex);

				// we want to add inside the minimum count it can be that there is a connector subitem which has no child (due to minimum count)
				if(index <_minCount)
				{
					foreach(SubItemConnector subitem in subitems)
					{
						if(subitem.Child ==null)
						{
							// if there is a free connector subitem we drop it so we can add a new one for our child
							_connectedChildren.Owner.RemoveSubItem(subitem);
							break;
						}
					}
				}

				// add a connector subitem for the child
				_connectedChildren.Owner.AddSubItem(new SubItemConnector(this, child, subitems.Count), firstIndex + index);

				RebuildSubItemIndices();
			}

			/// <summary>
			/// Removes a connector subitem for a child.
			/// </summary>
			/// <param name="child">The child whose connector subitem we want to remove.</param>
			protected void RemoveSubItem(Node child)
			{
				int firstIndex;
				List<SubItemConnector> subitems= CollectSubItems(out firstIndex);

				// find the connector subitem for this child...
				for(int i= 0; i <subitems.Count; ++i)
				{
					SubItemConnector subitem= subitems[i];

					// when we found it...
					if(subitem.Child ==child)
					{
						// remove the subitem
						_connectedChildren.Owner.RemoveSubItem(subitem);

						// if we do not fullfil the minimum count, re add it at the end and clear the child
						if(subitems.Count -1 <_minCount)
						{
							subitem.Child= null;
							_connectedChildren.Owner.AddSubItem(subitem, firstIndex + subitems.Count -1);
						}

						// update stored indices on connector subitems
						RebuildSubItemIndices();

						return;
					}
				}

				throw new Exception(Resources.ExceptionSubItemIsNoChild);
			}

			/// <summary>
			/// Clears all connector subitems from the node.
			/// </summary>
			protected void ClearSubItems()
			{
				int firstIndex;
				List<SubItemConnector> subitems= CollectSubItems(out firstIndex);

				// for all connector subitems
				for(int i= 0; i <subitems.Count; ++i)
				{
					// clear the minimum ones and remove the others
					if(i <_minCount)
						subitems[i].Child= null;
					else _connectedChildren.Owner.RemoveSubItem(subitems[i]);
				}
			}

			/// <summary>
			/// Checks if the connector connectes to a given node.
			/// </summary>
			/// <param name="node">The node we want to check to be a child.</param>
			/// <returns>Returns true if the node ia child of this connector.</returns>
			public bool HasChild(Node node)
			{
				for(int i= 0; i <ChildCount; ++i)
				{
					if(GetChild(i) ==node)
						return true;
				}

				return false;
			}

			public override string ToString()
			{
				return _connectedChildren.Owner.ToString() +':'+ _identifier;
			}
		}
	}
}
