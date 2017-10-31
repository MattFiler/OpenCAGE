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
using System.Xml;
using System.Windows.Forms;
using System.Globalization;
using System.IO;
using Brainiac.Design.Attributes;
using Brainiac.Design.Nodes;
using System.Reflection;
using Brainiac.Design.Properties;

namespace Brainiac.Design.FileManagers
{
	/// <summary>
	/// This is the default file manager which saves and loads XML files.
	/// </summary>
	public class FileManagerXML : FileManager
	{
		protected XmlDocument _xmlfile= new XmlDocument();

		/// <summary>
		/// Creates a new XML file manager to load and save behaviours.
		/// </summary>
		/// <param name="filename">The file we want to load from or save to.</param>
		/// <param name="node">The node we want to save. When loading, use null.</param>
		public FileManagerXML(string filename, BehaviorNode node) : base(filename, node)
		{
		}

		/// <summary>
		/// Retrieves an attribute from a XML node. If the attribute does not exist an exception is thrown.
		/// </summary>
		/// <param name="node">The XML node we want to get the attribute from.</param>
		/// <param name="att">The name of the attribute we want.</param>
		/// <returns>Returns the attributes value. Is always valid.</returns>
		protected string GetAttribute(XmlNode node, string att)
		{
			XmlNode value= node.Attributes.GetNamedItem(att);

			// maintain compatibility to version 1
			if(value ==null)
				value= node.Attributes.GetNamedItem(att.ToLowerInvariant());

			if(value !=null && value.NodeType ==XmlNodeType.Attribute)
				return value.Value;

			throw new Exception( string.Format(Resources.ExceptionFileManagerXMLMissingAttribute, att) );
		}

		/// <summary>
		/// Retrieves an attribute from a XML node. Attribute does not need to exist.
		/// </summary>
		/// <param name="node">The XML node we want to get the attribute from.</param>
		/// <param name="att">The name of the attribute we want.</param>
		/// <param name="result">The value of the attribute found. Is string.Empty if the attribute does not exist.</param>
		/// <returns>Returns true if the attribute exists.</returns>
		protected bool GetAttribute(XmlNode node, string att, out string result)
		{
			XmlNode value= node.Attributes.GetNamedItem(att);
			if(value !=null && value.NodeType ==XmlNodeType.Attribute)
			{
				result= value.Value;
				return true;
			}

			result= string.Empty;
			return false;
		}

		/// <summary>
		/// Initialises a property on a given node.
		/// </summary>
		/// <param name="xml">The XML element containing the attribute we want to get.</param>
		/// <param name="node">The node whose property we want to set.</param>
		/// <param name="property">The property on the node we want to set.</param>
		protected void InitProperty(XmlNode xml, Node node, DesignerPropertyInfo property)
		{
			string value;
			if(GetAttribute(xml, property.Property.Name, out value))
				property.SetValueFromString(node, value);
		}

		/// <summary>
		/// Initialises a property on a given event.
		/// </summary>
		/// <param name="xml">The XML element containing the attribute we want to get.</param>
		/// <param name="node">The event whose property we want to set.</param>
		/// <param name="property">The property on the event we want to set.</param>
		protected void InitProperty(XmlNode xml, Events.Event evnt, DesignerPropertyInfo property)
		{
			string value;
			if(GetAttribute(xml, property.Property.Name, out value))
				property.SetValueFromString(evnt, value);
		}

		/// <summary>
		/// Loads an event which is attached to a node.
		/// </summary>
		/// <param name="node">The node the event is created for.</param>
		/// <param name="xml">The XML node the event retrieves its name and attributes from.</param>
		/// <returns>Returns the created SubItems.Event.</returns>
		protected Events.Event CreateEvent(Node node, XmlNode xml)
		{
			// get the type of the event and create it

			// maintain compatibility with version 1
			//string clss= GetAttribute(xml, "Class");
			string clss;
			if(!GetAttribute(xml, "Class", out clss))
			{
				string find= ".Events."+ GetAttribute(xml, "name");
				Type found= Plugin.FindType(find);
				if(found !=null)
					clss= found.FullName;
			}

			Type t= Plugin.GetType(clss);

			if(t ==null)
				throw new Exception( string.Format(Resources.ExceptionUnknownEventType, clss) );

			Events.Event evnt= Events.Event.Create(t, node);

			// initialise the events properties
			IList<DesignerPropertyInfo> properties= evnt.GetDesignerProperties();
			for(int p= 0; p <properties.Count; ++p)
			{
				if(properties[p].Attribute.HasFlags(DesignerProperty.DesignerFlags.NoSave))
					continue;

				InitProperty(xml, evnt, properties[p]);
			}

			// update event with attributes
			evnt.OnPropertyValueChanged(false);

			return evnt;
		}

		/// <summary>
		/// Loads a node from a given XML node.
		/// </summary>
		/// <param name="xml">The XML node we want to create the node from.</param>
		/// <returns>Returns the created node.</returns>
		protected Node CreateNode(XmlNode xml)
		{
			// get the type of the node and create it
			string clss= GetAttribute(xml, "Class");
			Type t= Plugin.GetType(clss);

			if(t ==null)
				throw new Exception( string.Format(Resources.ExceptionUnknownNodeType, clss) );

			Node node= Nodes.Node.Create(t);

			// update the loaded behaviour member
			if(node is BehaviorNode && _loadedBehavior ==null)
				_loadedBehavior= (BehaviorNode)node;

			// initialise the properties
			IList<DesignerPropertyInfo> properties= node.GetDesignerProperties();
			foreach(DesignerPropertyInfo property in properties)
			{
				if(property.Attribute.HasFlags(DesignerProperty.DesignerFlags.NoSave))
					continue;

				InitProperty(xml, node, property);
				node.PostPropertyInit(property);
			}

			// maintain compatibility with version 1
			if(node is ReferencedBehaviorNode)
			{
				ReferencedBehaviorNode refbehavior= (ReferencedBehaviorNode)node;
				if(refbehavior.ReferenceFilename ==null)
					refbehavior.ReferenceFilename= GetAttribute(xml, "Reference");
			}

			// update node with properties
			node.OnPropertyValueChanged(false);

			// load child objects
			foreach(XmlNode xnode in xml.ChildNodes)
			{
				if(xnode.NodeType ==XmlNodeType.Element)
				{
					switch(xnode.Name.ToLowerInvariant())
					{
						// maintain compatibility with version 1
						case("node"):
							node.AddChild(node.DefaultConnector, CreateNode(xnode));
						break;

						case("event"):
							node.AddSubItem( new Node.SubItemEvent( CreateEvent(node, xnode) ) );
						break;

						case("comment"):
							// create a comment object
							node.CommentText= "temp";

							// initialise the attributes
							properties= node.CommentObject.GetDesignerProperties();
							foreach(DesignerPropertyInfo property in properties)
							{
								if(property.Attribute.HasFlags(DesignerProperty.DesignerFlags.NoSave))
									continue;

								string value;
								if(GetAttribute(xnode, property.Property.Name, out value))
									property.SetValueFromString(node.CommentObject, value);
							}
						break;

						case("connector"):
							string identifier= GetAttribute(xnode, "Identifier");
							Nodes.Node.Connector connector= node.GetConnector(identifier);

							foreach(XmlNode connected in xnode.ChildNodes)
							{
								if( connected.NodeType ==XmlNodeType.Element &&
									connected.Name.ToLowerInvariant() =="node" )
								{
									node.AddChildNotModified(connector, CreateNode(connected));
								}
							}
						break;
					}
				}
			}

			// update events with attributes
			if(node.SubItems.Count >0)
			{
				foreach(Nodes.Node.SubItem sub in node.SubItems)
				{
					node.SelectedSubItem= sub;

					node.OnSubItemPropertyValueChanged(false);
				}

				node.SelectedSubItem= null;
			}

			return node;
		}

		/// <summary>
		/// This method allows nodes to process the loaded attributes.
		/// </summary>
		/// <param name="node">The node which is processed.</param>
		protected void DoPostLoad(Node node)
		{
			node.PostLoad(_node);

			foreach(Node child in node.Children)
				DoPostLoad(child);
		}

		/// <summary>
		/// Loads a behaviour from the given filename
		/// </summary>
		public override void Load()
		{
			try
			{
				_xmlfile.Load(_filename);

				XmlNode root= _xmlfile.ChildNodes[1].ChildNodes[0];

				_node= (BehaviorNode) CreateNode(root);
				_node.FileManager= this;

				DoPostLoad((Node)_node);
			}
			catch
			{
				_xmlfile.RemoveAll();

				throw;
			}
		}

		/// <summary>
		/// Saves a node to the XML file.
		/// </summary>
		/// <param name="root">The XML node we want to attach the node to.</param>
		/// <param name="node">The node we want to save.</param>
		protected void SaveNode(XmlElement root, Node node)
		{
			// allow the node to process its attributes in preparation of the save
			node.PreSave(_node);

			// store the class we have to create when loading
			XmlElement elem= _xmlfile.CreateElement("Node");
			elem.SetAttribute("Class", node.GetType().FullName);

			// save attributes
			IList<DesignerPropertyInfo> properties= node.GetDesignerProperties();
			for(int p= 0; p <properties.Count; ++p)
			{
				if(!properties[p].Attribute.HasFlags(DesignerProperty.DesignerFlags.NoSave))
					elem.SetAttribute(properties[p].Property.Name, properties[p].GetStringValue(node));
			}

			// append node to root
			root.AppendChild(elem);

			// save comment
			if(node.CommentObject !=null)
			{
				XmlElement comment= _xmlfile.CreateElement("Comment");

				properties= node.CommentObject.GetDesignerProperties();
				for(int p= 0; p <properties.Count; ++p)
				{
					if(!properties[p].Attribute.HasFlags(DesignerProperty.DesignerFlags.NoSave))
						comment.SetAttribute(properties[p].Property.Name, properties[p].GetStringValue(node.CommentObject));
				}

				elem.AppendChild(comment);
			}

			// save events
			foreach(Nodes.Node.SubItem sub in node.SubItems)
			{
				if(sub is Nodes.Node.SubItemEvent)
				{
					Events.Event ne= ((Nodes.Node.SubItemEvent)sub).Event;

					XmlElement evnt= _xmlfile.CreateElement("Event");
					evnt.SetAttribute("Class", ne.GetType().FullName);

					// save attributes
					properties= ne.GetDesignerProperties();
					for(int p= 0; p <properties.Count; ++p)
					{
						if(!properties[p].Attribute.HasFlags(DesignerProperty.DesignerFlags.NoSave))
							elem.SetAttribute(properties[p].Property.Name, properties[p].GetStringValue(ne));
					}

					elem.AppendChild(evnt);
				}
			}

			// save children if allowed. Disallowed for referenced behaviours.
			if(node.SaveChildren)
			{
				// save connectors
				foreach(Nodes.Node.Connector connector in node.Connectors)
				{
					// if we have no children to store we can skip the connector
					if(connector.ChildCount <1)
						continue;

					XmlElement conn= _xmlfile.CreateElement("Connector");
					conn.SetAttribute("Identifier", connector.Identifier);
					elem.AppendChild(conn);

					// save their children
					for(int i= 0; i <connector.ChildCount; ++i)
						SaveNode(conn, connector.GetChild(i));
				}
			}
		}

		/// <summary>
		/// Save the given behaviour to the given file.
		/// </summary>
		public override void Save()
		{
			_xmlfile.RemoveAll();

			XmlDeclaration declaration= _xmlfile.CreateXmlDeclaration("1.0", "utf-8", null);
			_xmlfile.AppendChild(declaration);

			XmlElement root= _xmlfile.CreateElement("Behavior");
			_xmlfile.AppendChild(root);

			SaveNode(root, (Node)_node);

			try
			{
				MakeWritable();

				_xmlfile.Save(_filename);
			}
			catch
			{
				_xmlfile.RemoveAll();

				throw;
			}
		}
	}
}
