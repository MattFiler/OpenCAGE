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
using System.Data;
using System.Drawing;
using System.Text;
using System.Xml;
using System.IO;
using System.Windows.Forms;

namespace Brainiac.Design
{
	/// <summary>
	/// This window allows the user to manage the workspaces.
	/// </summary>
	internal partial class WorkspaceDialog : Form
	{
		internal WorkspaceDialog()
		{
			InitializeComponent();
		}

		/// <summary>
		/// The filename which stores the workspaces.
		/// </summary>
		private string _workspacesFilename= File.Exists("debug_workspaces.xml") ? "debug_workspaces.xml" :
												Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) +"\\Brainiac Designer\\workspaces.xml";

		/// <summary>
		/// Holds if the workspaces need to be saved.
		/// </summary>
		private bool _needsToSaveFile= false;

		/// <summary>
		/// The workspace which was selected the last time the editor saved the workspace file.
		/// </summary>
		private string _recentlySelectedWorkspace= string.Empty;

		/// <summary>
		/// The list of available workspaces.
		/// </summary>
		private List<Workspace> _workspaces= new List<Workspace>();

		/// <summary>
		/// The workspace selected by the user.
		/// </summary>
		internal Workspace Workspace
		{
			get { return (Workspace) comboBox.SelectedItem; }
		}

		internal Workspace SelectedWorkspace
		{
			get { return (Workspace) comboBox.SelectedItem; }
		}

		/// <summary>
		/// Creates the Xml file to store the workspaces in.
		/// </summary>
		/// <param name="filename">The name of the Xml file saved.</param>
		private void CreateWorkspacesFile(string filename)
		{
			XmlDocument xml= new XmlDocument();

			// create the xml declaration
			XmlDeclaration declaration= xml.CreateXmlDeclaration("1.0", "utf-8", null);
			xml.AppendChild(declaration);

			// creates the root node
			XmlElement root= xml.CreateElement("workspaces");

			if(SelectedWorkspace !=null)
				root.SetAttribute("selected", SelectedWorkspace.Name);

			xml.AppendChild(root);

			// add the workspaces
			foreach(Workspace ws in _workspaces)
			{
				// create workspace node
				XmlElement workspace= xml.CreateElement("workspace");
				workspace.SetAttribute("name", ws.Name);
				workspace.SetAttribute("folder", ws.RelativeFolder);
				workspace.SetAttribute("export", ws.RelativeDefaultExportFolder);
				root.AppendChild(workspace);

				// create plugin nodes
				foreach(string plugin in ws.Plugins)
				{
					XmlElement p= xml.CreateElement("plugin");
					p.InnerText= plugin;
					workspace.AppendChild(p);
				}
			}

			FileManagers.FileManager.MakeWritable(filename);

			// save workspaces
			xml.Save(filename);
		}

		/// <summary>
		/// Retrieves an attribute from a XML node. If the attribute does not exist an exception is thrown.
		/// </summary>
		/// <param name="node">The XML node we want to get the attribute from.</param>
		/// <param name="att">The name of the attribute we want.</param>
		/// <returns>Returns the attributes value. Is always valid.</returns>
		private string GetAttribute(XmlNode node, string att)
		{
			XmlNode value= node.Attributes.GetNamedItem(att);
			if(value !=null && value.NodeType ==XmlNodeType.Attribute)
				return value.Value;

			throw new Exception( string.Format("Missing attribute \"{0}\"", att) );
		}

		/// <summary>
		/// Retrieves an attribute from a XML node. Attribute does not need to exist.
		/// </summary>
		/// <param name="node">The XML node we want to get the attribute from.</param>
		/// <param name="att">The name of the attribute we want.</param>
		/// <param name="result">The value of the attribute found. Is string.Empty if the attribute does not exist.</param>
		/// <returns>Returns true if the attribute exists.</returns>
		private bool GetAttribute(XmlNode node, string att, out string result)
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
		/// Loads the present workspaces.
		/// </summary>
		/// <param name="filename">The name of the Xml file loaded.</param>
		private void LoadWorkspacesFile(string filename)
		{
			XmlDocument xml= new XmlDocument();
			xml.Load(filename);

			XmlNode root= xml.ChildNodes[1];

			GetAttribute(root, "selected", out _recentlySelectedWorkspace);

			// load workspaces
			foreach(XmlNode node in root)
			{
				if(node.NodeType ==XmlNodeType.Element && node.Name =="workspace")
				{
					string name= GetAttribute(node, "name");
					string folder= GetAttribute(node, "folder");

					// we get this as an optional parameter to stay compatible with the old files
					string defaultExportFolder;
					GetAttribute(node, "export", out defaultExportFolder);

					Workspace ws= new Workspace(name, folder, defaultExportFolder);

					// load plugins
					foreach(XmlNode subnode in node)
					{
						if(subnode.NodeType ==XmlNodeType.Element && subnode.Name =="plugin")
						{
							if(subnode.InnerText.Trim() !=string.Empty)
								ws.AddPlugin(subnode.InnerText.Trim());
						}
					}

					// add the loaded workspace to the lists
					_workspaces.Add(ws);
					comboBox.Items.Add(ws);

					if(ws.Name ==_recentlySelectedWorkspace)
						comboBox.SelectedItem= ws;
				}
			}
		}

		/// <summary>
		/// Handles when the dialogue is shown.
		/// </summary>
		private void WorkspaceDialog_Shown(object sender, EventArgs e)
		{
			try
			{
				// load the existing workspaces or create a new file
				if(File.Exists(_workspacesFilename))
				{
					LoadWorkspacesFile(_workspacesFilename);

					if(_workspaces.Count >0 && comboBox.SelectedItem ==null)
						comboBox.SelectedIndex= 0;
				}
				else
				{
					CreateWorkspacesFile(_workspacesFilename);
				}
			}
			catch(Exception ex) { MessageBox.Show(ex.Message, "File Error", MessageBoxButtons.OK); }

			// check if we have any workspaces to edit
			editButton.Enabled= comboBox.SelectedItem !=null;
			removeButton.Enabled= comboBox.SelectedItem !=null;

			// focus the open button so you can simply press return
			openButton.Focus();

            //Added for Alien Isolation Mod Tools - immediately open current project
            openButton.PerformClick();
		}

		/// <summary>
		/// Handles when the open workspace button is clicked.
		/// </summary>
		private void openButton_Click(object sender, EventArgs e)
		{
			// we may only continue if we have a workspace selected
			if(comboBox.SelectedItem !=null)
			{
				// save the current list of workspaces
				if(_needsToSaveFile || SelectedWorkspace.Name !=_recentlySelectedWorkspace)
					CreateWorkspacesFile(_workspacesFilename);

				// close the dialogue
				Close();
			}
		}

		/// <summary>
		/// Handles when the new button is pressed.
		/// </summary>
		private void newButton_Click(object sender, EventArgs e)
		{
			EditWorkspaceDialog dialog= new EditWorkspaceDialog();
			dialog.ShowDialog();

			// check if we have a valid result
			if(dialog.Workspace !=null)
			{
				// add the new workspace
				_workspaces.Add(dialog.Workspace);
				comboBox.Items.Add(dialog.Workspace);

				// select the new workspace
				comboBox.SelectedItem= dialog.Workspace;

				_needsToSaveFile= true;
			}

			// check if we have any workspaces to edit
			editButton.Enabled= comboBox.SelectedItem !=null;
			removeButton.Enabled= comboBox.SelectedItem !=null;
		}

		/// <summary>
		/// Handles when the edit button is clicked.
		/// </summary>
		private void editButton_Click(object sender, EventArgs e)
		{
			EditWorkspaceDialog dialog= new EditWorkspaceDialog();
			dialog.EditWorkspace( (Workspace) comboBox.SelectedItem );
			dialog.ShowDialog();

			// check if we have a valid result
			if(dialog.Workspace !=null)
			{
				// remove the old workspace
				_workspaces.Remove( (Workspace) comboBox.SelectedItem );
				comboBox.Items.Remove(comboBox.SelectedItem);

				// add the nedited workspace
				_workspaces.Add(dialog.Workspace);
				comboBox.Items.Add(dialog.Workspace);

				// select the edited workspace
				comboBox.SelectedItem= dialog.Workspace;

				_needsToSaveFile= true;
			}
		}

		/// <summary>
		/// Handles when the remove butoon is clicked.
		/// </summary>
		private void removeButton_Click(object sender, EventArgs e)
		{
			if(comboBox.SelectedItem !=null)
			{
				// remove the selected workspace
				_workspaces.Remove( (Workspace) comboBox.SelectedItem );
				comboBox.Items.Remove(comboBox.SelectedItem);

				// check if we have any workspaces to edit
				editButton.Enabled= comboBox.SelectedItem !=null;
				removeButton.Enabled= comboBox.SelectedItem !=null;

				_needsToSaveFile= true;
			}
		}

		private void comboBox_SelectedIndexChanged(object sender, EventArgs e)
		{
			// check if we have any workspaces to edit
			editButton.Enabled= comboBox.SelectedItem !=null;
			removeButton.Enabled= comboBox.SelectedItem !=null;
		}
	}
}