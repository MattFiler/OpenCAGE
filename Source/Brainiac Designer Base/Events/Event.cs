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
using System.Windows.Forms;
using System.Reflection;
using Brainiac.Design.Attributes;
using Brainiac.Design.Properties;

namespace Brainiac.Design.Events
{
	/// <summary>
	/// This class represents an event which is attached to a node.
	/// </summary>
	public class Event : NodeTag.DefaultObject, ICloneable
	{
		/// <summary>
		/// Creates an event from a given type.
		/// </summary>
		/// <param name="type">The type we want to create an event of.</param>
		/// <param name="node">The node the event will be added to.</param>
		/// <returns>Returns the created event.</returns>
		public static Event Create(Type type, Nodes.Node node)
		{
			Debug.Check(type !=null);

			Event evnt= (Event)type.InvokeMember(string.Empty, System.Reflection.BindingFlags.CreateInstance, null, null, new object[] { node });

			if(evnt ==null)
				throw new Exception(Resources.ExceptionMissingEventConstructor);

			return evnt;
		}

		protected Nodes.Node _node;

		/// <summary>
		/// The node this event belongs to.
		/// </summary>
		public Nodes.Node Node
		{
			get { return _node; }
		}

		private string _label;
		private string _baselabel;
		public string Label
		{
			get { return _label; }

			set
			{
				_label= value; //Resources.ResourceManager.GetString(value, Resources.Culture);

				// store the original label so we can automatically generate a new label when an ttribute changes.
				if(_baselabel ==string.Empty)
					_baselabel= _label;

				// when the label changes the size of the node might change as well
				if(_label !=_baselabel)
					_node.DoWasModified();
			}
		}

		protected string _description;

		/// <summary>
		/// The description of this node.
		/// </summary>
		public string Description
		{
			get { return /*Resources.ResourceManager.GetString(*/_description/*, Resources.Culture)*/; }
		}

		protected bool _blockEvent= false;

		/// <summary>
		/// The attribute which holds the info if the event is blocked or not.
		/// This is still in in case someone wants to events the old way.
		/// </summary>
		[DesignerBoolean("EventBlockEvent", "EventBlockEventDesc", "CategoryBasic", DesignerProperty.DisplayMode.NoDisplay, 0, DesignerProperty.DesignerFlags.NoFlags)]
		public bool BlockEvent
		{
			get { return _blockEvent; }
			set { _blockEvent= value; }
		}

		/// <summary>
		/// Create a new node event.
		/// </summary>
		/// <param name="node">The node this event belongs to.</param>
		/// <param name="evnt">The event we want to attach to a node.</param>
		public Event(Nodes.Node node, string label, string description)
		{
			_label= label;
			_baselabel= label;
			_description= description;
			_node= node;
		}

		/// <summary>
		/// Is called when one of the event's proterties were modified.
		/// </summary>
		/// <param name="wasModified">Holds if the event was modified.</param>
		public virtual void OnPropertyValueChanged(bool wasModified)
		{
			Label= GenerateNewLabel();

			_node.OnSubItemPropertyValueChanged(wasModified);
		}

		public override string ToString()
		{
			return _label;
		}

		public object Clone()
		{
			return Clone(_node);
		}

		public Event Clone(Nodes.Node newnode)
		{
			Event newevent= Create(GetType(), newnode);

			CloneProperties(newevent);

			newevent.OnPropertyValueChanged(false);

			return newevent;
		}

		protected virtual void CloneProperties(Event newevent)
		{
			newevent._blockEvent= _blockEvent;
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
	}
}
