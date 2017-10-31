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
using System.Resources;
using Brainiac.Design.Properties;
using System.IO;
using Brainiac.Design.Nodes;

namespace Brainiac.Design
{
	/// <summary>
	/// This enumeration decribes what type of node is used in the node explorer.
	/// It is only used for displaying the nodes in the explorer and to handle drag & drop actions.
	/// </summary>
	public enum NodeTagType { Behavior, BehaviorFolder, Node, NodeFolder, Event };

	/// <summary>
	/// The NodeTag is used to identify nodes in the explorer. Each TreeViewItem.Tag is a NodeTag.
	/// It is only used for displaying the nodes in the explorer and to handle drag & drop actions.
	/// </summary>
	public class NodeTag
	{
		public interface DefaultObject
		{
			string Description { get; }
			string Label { get; }
		}

		protected NodeTagType _type;

		/// <summary>
		/// The type of the node in the node explorer.
		/// </summary>
		public NodeTagType Type
		{
			get { return _type; }
		}

		protected Type _nodetype;

		/// <summary>
		/// The type of the node which will be created in the graph.
		/// </summary>
		public Type NodeType
		{
			get { return _nodetype; }
		}

		protected string _filename;

		/// <summary>
		/// The filename of the behaviour which will be loaded when we double-click it.
		/// </summary>
		public string Filename
		{
			get { return _filename; }
			set { _filename= value; }
		}

		protected DefaultObject _defaults;

		/// <summary>
		/// A default instance of a node, used to get its description and things like these.
		/// The instance is automatically created for each node in the node explorer.
		/// </summary>
		public DefaultObject Defaults
		{
			get { return _defaults; }
		}

		/// <summary>
		/// Used to replace the default object with a behaviour once loaded.
		/// </summary>
		/// <param name="behavior">The behaviour we have loaded.</param>
		public void AssignLoadedBehavior(BehaviorNode behavior)
		{
			Debug.Check(_type ==NodeTagType.Behavior);
			Debug.Check(_filename ==behavior.FileManager.Filename);

			_defaults= (DefaultObject)behavior;
		}

		/// <summary>
		/// Creates a new NodeTag and an instance of the node for the defaults.
		/// </summary>
		/// <param name="type">The type of the node in the node explorer.</param>
		/// <param name="nodetype">The type of the node which will be added to the behaviour tree.</param>
		/// <param name="filename">The filename of the behaviour we want to load. Use string.Empty if the node is not a behaviour.</param>
		public NodeTag(NodeTagType type, Type nodetype, string filename)
		{
			if((type ==NodeTagType.BehaviorFolder || type ==NodeTagType.NodeFolder) && nodetype !=null)
				throw new Exception(Resources.ExceptionWrongNodeTagType);

			_type= type;
			_nodetype= nodetype;
			_filename= filename;

			if(nodetype ==null)
			{
				_defaults= null;
			}
			else
			{
				//if(!nodetype.IsSubclassOf(typeof(DefaultObject)))
				//	throw new Exception(Resources.ExceptionNotImplementDefaultObject);

				if(nodetype.IsSubclassOf(typeof(Events.Event)) && type !=NodeTagType.Event)
					throw new Exception(Resources.ExceptionWrongNodeTagType);

				if(nodetype.IsSubclassOf(typeof(Nodes.Node)) && type !=NodeTagType.Node && type !=NodeTagType.Behavior)
					throw new Exception(Resources.ExceptionWrongNodeTagType);

				_defaults=type ==NodeTagType.Event ? (DefaultObject)Brainiac.Design.Events.Event.Create(nodetype, null) : (DefaultObject)Nodes.Node.Create(nodetype);
			}
		}
	}

	/// <summary>
	/// This enumeration represents the icons which are available for the nodes in the explorer. The order and number must be the same as for the ImageList in the node exploerer.
	/// </summary>
	public enum NodeIcon { FlagBlue, FlagGreen, FlagRed, Behavior, BehaviorLoaded, BehaviorModified, Condition, Impulse, Action, Decorator, Sequence, Selector, Parallel, FolderClosed, FolderOpen, Event };

	/// <summary>
	/// This class describes a group which will be shown in the node explorer.
	/// </summary>
	public class NodeGroup
	{
		protected string _name;

		/// <summary>
		/// The name of the group which will be displayed in the node explorer.
		/// </summary>
		public string Name
		{
			get { return _name; }
		}

		protected NodeIcon _icon;

		/// <summary>
		/// The icon of the node and its children which will be displayed in the node explorer.
		/// Notice that for behaviours, other icons than the given one will be used.
		/// </summary>
		public NodeIcon Icon
		{
			get { return _icon; }
		}

		protected List<NodeGroup> _children= new List<NodeGroup>();

		/// <summary>
		/// Groups which will be shown below this one.
		/// </summary>
		public IList<NodeGroup> Children
		{
			get { return _children.AsReadOnly(); }
		}

		protected List<Type> _items= new List<Type>();

		/// <summary>
		/// Nodes which will be shown in this group.
		/// </summary>
		public List<Type> Items
		{
			get { return _items; }
		}

		/// <summary>
		/// Adds this NodeGroup to the TreeView of the node explorer.
		/// </summary>
		/// <param name="pool">The TreeNodeCollection the group and its sub-groups and childrens will be added.</param>
		public void Register(TreeNodeCollection pool)
		{
			// check if this NodeGroup already exists in the node explorer
			TreeNode tnode= null;
			foreach(TreeNode node in pool)
			{
				if(node.Text ==_name)
				{
					tnode= node;
					break;
				}
			}

			// create a new group if it does not yet exist
			if(tnode ==null)
			{
				tnode= new TreeNode(_name, (int) _icon, (int) _icon);
				tnode.Tag= new NodeTag(NodeTagType.NodeFolder, null, string.Empty);
				pool.Add(tnode);
			}

			// add the nodes which will be shown in this group
			foreach(Type item in _items)
			{
				NodeTag nodetag= new NodeTag(item.IsSubclassOf(typeof(Nodes.Node)) ? NodeTagType.Node : NodeTagType.Event, item, string.Empty);

				TreeNode inode= new TreeNode(nodetag.Defaults.Label, (int) _icon, (int) _icon);
				inode.Tag= nodetag;
				inode.ToolTipText= nodetag.Defaults.Description;

				tnode.Nodes.Add(inode);
			}

			// add any sub-group
			foreach(NodeGroup group in _children)
				group.Register(tnode.Nodes);
		}

		/// <summary>
		/// Defines a new NodeGroup which will be shown in the node explorer.
		/// </summary>
		/// <param name="name">The displayed name of the group.</param>
		/// <param name="icon">The displayed icon of the group and its children.</param>
		/// <param name="parent">The parent of the group, can be null.</param>
		public NodeGroup(string name, NodeIcon icon, NodeGroup parent)
		{
			_name= name;
			_icon= icon;

			if(parent !=null)
				parent.Children.Add(this);
		}
	}

	/// <summary>
	/// This class holds information about a file manager which can be used to load and save behaviours.
	/// </summary>
	public class FileManagerInfo
	{
		/// <summary>
		/// Returns a file manager which can be used to save or load a behaviour.
		/// </summary>
		/// <param name="filename">The name of the file we want to load or we want to save to.</param>
		/// <param name="node">The behaviour we want to load or save.</param>
		/// <returns>Returns the file manager which will be created.</returns>
		public FileManagers.FileManager Create(string filename, Nodes.BehaviorNode node)
		{
			object[] prms= new object[2] { filename, node };
			return (FileManagers.FileManager) _type.InvokeMember(string.Empty, System.Reflection.BindingFlags.CreateInstance, null, null, prms);
		}

		/// <summary>
		/// Defines a file manager which can be used to load and save behaviours.
		/// </summary>
		/// <param name="filemanager">The tpe of the file manager which will be created by this info.</param>
		/// <param name="filter">The text displayed in the save dialogue when selecting the file format.</param>
		/// <param name="fileextension">The file extension used to identify which file manager can handle the given file.</param>
		public FileManagerInfo(Type filemanager, string filter, string fileextension)
		{
			_filter= filter;
			_fileExtension= fileextension.ToLowerInvariant();
			_type= filemanager;

			// the file extension must always start with a dot
			if(_fileExtension[0] !='.')
				_fileExtension= '.'+ _fileExtension;
		}

		protected Type _type;

		/// <summary>
		/// The type of the file manager which will be created.
		/// </summary>
		public Type Type
		{
			get { return _type; }
		}

		protected string _filter;

		/// <summary>
		/// The displayed text in the save dialogue when selecting the file format.
		/// </summary>
		public string Filter
		{
			get { return _filter; }
		}

		protected string _fileExtension;

		/// <summary>
		/// The extension used to determine which file manager can handle the given file.
		/// </summary>
		public string FileExtension
		{
			get { return _fileExtension; }
		}
	}

	/// <summary>
	/// This class holds information about an exporter which can be used to export a behaviour into a format which can be used by the workflow of your game.
	/// </summary>
	public class ExporterInfo
	{
		/// <summary>
		/// Creates an instance of an exporter which will be used to export a behaviour.
		/// To export the behaviour, the Export() method must be called.
		/// </summary>
		/// <param name="node">The behaviour you want to export.</param>
		/// <param name="outputFolder">The folder you want to export the behaviour to.</param>
		/// <param name="filename">The relative filename you want to export the behaviour to.</param>
		/// <returns>Returns the created exporter.</returns>
		public Exporters.Exporter Create(Nodes.BehaviorNode node, string outputFolder, string filename)
		{
			object[] prms= new object[] { node, outputFolder, filename };
			return (Exporters.Exporter) _type.InvokeMember(string.Empty, System.Reflection.BindingFlags.CreateInstance, null, null, prms);
		}

		/// <summary>
		/// Defines an exporter which can be used to export a behaviour.
		/// </summary>
		/// <param name="exporter">The type of the exporter which will be created.</param>
		/// <param name="description">The description which will be shown when the user must select the exporter (s)he wants to use.</param>
		/// <param name="mayExportAll">Determines if this exporter may be used to export multiple behaviours.
		/// This can be important if the output requires further user actions.</param>
		/// <param name="id">The id of the exporter which will be used by the Brainiac Exporter to identify which exporter to use.</param>
		public ExporterInfo(Type exporter, string description, bool mayExportAll, string id)
		{
			_description= description;
			_type= exporter;
			_mayExportAll= mayExportAll;
			_id= id;
		}

		protected Type _type;

		/// <summary>
		/// The type of the exporter which will be created.
		/// </summary>
		public Type Type
		{
			get { return _type; }
		}

		protected string _description;

		/// <summary>
		/// The displayed text when the user must select which exporter to use.
		/// </summary>
		public string Description
		{
			get { return _description; }
		}

		/// <summary>
		/// This is needed for the drop down list in the export dialogue to show the correct text.
		/// </summary>
		/// <returns>Returns the description of the exporter.</returns>
		public override string ToString()
		{
			return _description;
		}

		protected bool _mayExportAll;

		/// <summary>
		/// Determines if the exporter can be used when exporting multiple files. For example if further user actions are required.
		/// </summary>
		public bool MayExportAll
		{
			get { return _mayExportAll; }
		}

		private string _id;

		/// <summary>
		/// The id used to identify an exporter when being used with the Brainiac Exporter.
		/// </summary>
		public string ID
		{
			get { return _id; }
		}
	}

	/// <summary>
	/// The base class for every plugin. The class name of your plugin must be the same as your library.
	/// </summary>
	public class Plugin
	{
		/// <summary>
		/// A global list of all plugins which have been loaded. Mainly for internal use.
		/// </summary>
		private static List<Assembly> _loadedPlugins= new List<Assembly>();

		/// <summary>
		/// Add a plugin which has been loaded. Mainly for internal use.
		/// </summary>
		/// <param name="a"></param>
		public static void AddLoadedPlugin(Assembly a)
		{
			_loadedPlugins.Add(a);
		}

		/// <summary>
		/// Returns the type of a given class name. It searches all loaded plugins for this type.
		/// </summary>
		/// <param name="fullname">The name of the class we want to get the type for.</param>
		/// <returns>Returns the type if found in any loaded plugin. Retuns null if it could not be found.</returns>
		public static Type GetType(string fullname)
		{
			// search base class
			Type type= Type.GetType(fullname);
			if(type !=null)
				return type;

			// search loaded plugins
			foreach(Assembly assembly in _loadedPlugins)
			{
				type= assembly.GetType(fullname);
				if(type !=null)
					return type;
			}

			return null;
		}

		public static Type FindType(string name)
		{
			// search base class
			Type type= Type.GetType(name);
			if(type !=null)
				return type;

			// search loaded plugins
			foreach(Assembly assembly in _loadedPlugins)
			{
				string fullname= Path.GetFileNameWithoutExtension(assembly.Location) + name;
				type= assembly.GetType(fullname);
				if(type !=null)
					return type;
			}

			return null;
		}

		protected List<NodeGroup> _nodeGroups= new List<NodeGroup>();

		/// <summary>
		/// Holds a list of any root node group which will be automatically added to the node explorer.
		/// </summary>
		public IList<NodeGroup> NodeGroups
		{
			get { return _nodeGroups.AsReadOnly(); }
		}

		protected List<FileManagerInfo> _fileManagers= new List<FileManagerInfo>();

		/// <summary>
		/// Holds a listof file managers which will be automatically registered.
		/// </summary>
		public IList<FileManagerInfo> FileManagers
		{
			get { return _fileManagers.AsReadOnly(); }
		}

		protected List<ExporterInfo> _exporters= new List<ExporterInfo>();

		/// <summary>
		/// Holds a list of exporters which will be automatically registered.
		/// </summary>
		public IList<ExporterInfo> Exporters
		{
			get { return _exporters.AsReadOnly(); }
		}

		/// <summary>
		/// Adds the resource manager to the list of available resource managers.
		/// </summary>
		/// <returns>List containing local resource manager.</returns>
		private static List<ResourceManager> AddLocalResources()
		{
			List<ResourceManager> list= new List<ResourceManager>();
			list.Add(Resources.ResourceManager);
			return list;
		}

		/// <summary>
		/// This list must contain any resource manager which is avilable.
		/// </summary>
		private static List<ResourceManager> __resources= AddLocalResources();

		/// <summary>
		/// Adds a resource manager to the list of all available resource managers.
		/// </summary>
		/// <param name="manager">The manager which will be added.</param>
		public static void AddResourceManager(ResourceManager manager)
		{
			if(!__resources.Contains(manager))
				__resources.Add(manager);
		}

		/// <summary>
		/// Retrieves a string from all available resource managers.
		/// </summary>
		/// <param name="name">The string's name we want to get.</param>
		/// <returns>Returns name if resource could not be found.</returns>
		public static string GetResourceString(string name)
		{
			foreach(ResourceManager manager in __resources)
			{
				string val= manager.GetString(name, Resources.Culture);
				if(val !=null)
					return val;
			}

			return name;
		}

		protected List<AIType> _aiTypes= new List<AIType>();

		/// <summary>
		/// Holds a list of AI types available.
		/// </summary>
		public IList<AIType> AITypes
		{
			get { return _aiTypes.AsReadOnly(); }
		}
	}
}
