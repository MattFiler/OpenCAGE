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
using System.IO;
using Brainiac.Design.Nodes;
using Brainiac.Design.Attributes;

namespace Brainiac.Design.Exporters
{
	/// <summary>
	/// This exporter generates .cs files which generate a static variable which holds the behaviour tree.
	/// </summary>
	public class ExporterCs : Brainiac.Design.Exporters.Exporter
	{
		// the namespace the behaviours are exported to
		protected const string _usedNamespace= "Brainiac.Behaviors";

		public ExporterCs(BehaviorNode node, string outputFolder, string filename) : base(node, outputFolder, filename +".cs")
		{
		}

		/// <summary>
		/// Exports a behaviour to the given file.
		/// </summary>
		/// <param name="file">The file we want to export to.</param>
		/// <param name="behavior">The behaviour we want to export.</param>
		protected void ExportBehavior(StreamWriter file, BehaviorNode behavior)
		{
			string namspace= GetNamespace(_usedNamespace, _filename);
			string classname= Path.GetFileNameWithoutExtension(behavior.FileManager.Filename).Replace(" ", string.Empty);

			// write comments
			file.Write( string.Format("// Exported behavior: {0}\r\n", _filename) );
			file.Write( string.Format("// Exported file:     {0}\r\n\r\n", behavior.FileManager.Filename) );

			// create namespace and class
			file.Write( string.Format("namespace {0}\r\n{{\r\n", namspace) );
			file.Write( string.Format("\tpublic sealed class {0} : {1}\r\n\t{{\r\n", classname, ((Node)behavior).ExportClass) );

			// create instance and accessors
			file.Write( string.Format("\t\tprivate static {0} _instance = null;\r\n", classname) );
			file.Write( string.Format("\t\tpublic static {0} Instance\r\n\t\t{{\r\n", classname) );
			file.Write("\t\t\tget\r\n\t\t\t{\r\n");
			file.Write("\t\t\t\tif(_instance == null)\r\n");
			file.Write( string.Format("\t\t\t\t\t_instance = new {0}();\r\n\r\n", classname) );
			file.Write("\t\t\t\treturn _instance;\r\n\t\t\t}\r\n\t\t}\r\n\r\n");

			// create constructor
			file.Write( string.Format("\t\tprivate {0}()\r\n\t\t{{\r\n", classname) );

			// export nodes
			int nodeID= 0;

			// export the children
			foreach(Node child in ((Node)behavior).Children)
				ExportNode(file, namspace, behavior, "this", child, 3, ref nodeID);

			// close constructor
			file.Write("\t\t}\r\n");

			// close namespace and class
			file.Write("\t}\r\n}\r\n");
		}

		protected virtual void ExportConstructorAndProperties(StreamWriter file, Node node, string indent, string nodeName, string classname)
		{
			// create a new instance of the node
			file.Write( string.Format("{0}\t{2} {1} = new {2}();\r\n", indent, nodeName, classname) );

			// assign all the properties
			ExportProperties(file, nodeName, node, indent);
		}

		/// <summary>
		/// Exports a node to the given file.
		/// </summary>
		/// <param name="file">The file we want to export to.</param>
		/// <param name="namspace">The namespace of the behaviour we are currently exporting.</param>
		/// <param name="behavior">The behaviour we are currently exporting.</param>
		/// <param name="parentName">The name of the variable of the node which is the parent of this node.</param>
		/// <param name="node">The node we want to export.</param>
		/// <param name="indentDepth">The indent of the ocde we are exporting.</param>
		/// <param name="nodeID">The current id used for generating the variables for the nodes.</param>
		protected void ExportNode(StreamWriter file, string namspace, BehaviorNode behavior, string parentName, Node node, int indentDepth, ref int nodeID)
		{
			// generate some data
			string classname= node.ExportClass;
			string nodeName= string.Format("node{0}", ++nodeID);

			// generate the indent string
			string indent= string.Empty;
			for(int i= 0; i <indentDepth; ++i)
				indent+= '\t';

			// we have to handle a referenced behaviour differently
			if(node is ReferencedBehaviorNode)
			{
				// generate the namespace and name of the behaviour we are referencing
				string refRelativeFilename= behavior.MakeRelative(((ReferencedBehaviorNode)node).ReferenceFilename);
				string refNamespace= GetNamespace(namspace, refRelativeFilename);
				string refBehaviorName= Path.GetFileNameWithoutExtension(((ReferencedBehaviorNode)node).ReferenceFilename.Replace(" ", string.Empty));

				// simply add the instance of the behaviours we are referencing
				file.Write( string.Format("{0}{1}.AddChild({1}.GetConnector(\"{2}\"), {3}.{4}.Instance);\r\n", indent, parentName, node.ParentConnector.Identifier, refNamespace, refBehaviorName) );
			}
			else
			{
				// open some brackets for a better formatting in the generated code
				file.Write( string.Format("{0}{{\r\n", indent) );

				// export the constructor and the properties
				ExportConstructorAndProperties(file, node, indent, nodeName, classname);

				// add the node to its parent
				file.Write( string.Format("{0}\t{1}.AddChild({1}.GetConnector(\"{2}\"), {3});\r\n", indent, parentName, node.ParentConnector.Identifier, nodeName) );

				// export the child nodes
				foreach(Node child in node.Children)
					ExportNode(file, namspace, behavior, nodeName, child, indentDepth +1, ref nodeID);

				// close the brackets for a better formatting in the generated code
				file.Write( string.Format("{0}}}\r\n", indent) );
			}
		}

		/// <summary>
		/// Exports all the properties of a ode and assigns them.
		/// </summary>
		/// <param name="file">The file we are exporting to.</param>
		/// <param name="nodeName">The name of the node we are setting the properties for.</param>
		/// <param name="node">The node whose properties we are exporting.</param>
		/// <param name="indent">The indent for the currently generated code.</param>
		protected void ExportProperties(StreamWriter file, string nodeName, Node node, string indent)
		{
			// export all the properties
			IList<DesignerPropertyInfo> properties= node.GetDesignerProperties();
			for(int p= 0; p <properties.Count; ++p)
			{
				// we skip properties which are not marked to be exported
				if(properties[p].Attribute.HasFlags(DesignerProperty.DesignerFlags.NoExport))
					continue;

				// create the code which assigns the value to the node's property
				file.Write( string.Format("{0}\t{1}.{2} = {3};\r\n", indent, nodeName, properties[p].Property.Name, properties[p].GetExportValue(node)) );
			}
		}

		/// <summary>
		/// Generates the namespace used for a referenced behaviour.
		/// </summary>
		/// <param name="currentNamespace">The namespace we are currently at.</param>
		/// <param name="relativeFilename">The relative filename of the ebhaviour we are referencing.</param>
		/// <returns></returns>
		protected string GetNamespace(string currentNamespace, string relativeFilename)
		{
			// if we stay in the same folder/namespace, just return the current one.
			if(Path.GetFileName(relativeFilename) ==relativeFilename)
				return currentNamespace;

			// generate the namespace we are using
			string file= Path.GetDirectoryName( currentNamespace.Replace('.', '\\') +'\\'+ relativeFilename );

			// make sure we remove any ..\ we found
			string full= Path.GetFullPath(file);
			string folder= Path.GetFullPath(".");

			Debug.Check(full.StartsWith(folder));

			// get a relative path for the namespace
			string namespaceFile= full.Substring(folder.Length +1);

			// turn the path into a namespace and remove all spaces
			return namespaceFile.Replace('\\', '.').Replace(" ", string.Empty);
		}

		/// <summary>
		/// Export the assigned node to the assigned file.
		/// </summary>
		public override void Export()
		{
			// get the abolute folder of the file we want toexport
			string folder= Path.GetDirectoryName(_outputFolder +'\\'+ _filename);

			// if the directory does not exist, create it
			if(!Directory.Exists(folder))
				Directory.CreateDirectory(folder);

			// export to the file
			StreamWriter file= new StreamWriter(_outputFolder +'\\'+ _filename);
			ExportBehavior(file, _node);
			file.Close();
		}
	}
}
