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
using System.Windows.Forms;
using System.IO;

namespace Brainiac.Design
{
	/// <summary>
	/// This class is the window which appears when creating or editing a workspace.
	/// </summary>
	internal partial class EditWorkspaceDialog : Form
	{
		internal EditWorkspaceDialog()
		{
			InitializeComponent();

			const string folder= "plugins";

			// get any DLL file found in the plugin folder.
			string[] files= Directory.GetFiles(folder, "*.dll", SearchOption.TopDirectoryOnly);

			// add the files to the list
			for(int i= 0; i <files.Length; ++i)
				checkedListBox.Items.Add( Path.GetFileName(files[i]) );

			// set default behavior folder values
			behaviorFolderTextBox.Text= Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) +"\\Brainiac Designer\\Behaviors";
		}

		/// <summary>
		/// The workspace which is currently edited or created.
		/// </summary>
		private Workspace _workspace= null;

		/// <summary>
		/// The workspace created or edited by the dialogue.
		/// </summary>
		internal Workspace Workspace
		{
			get { return _workspace; }
		}

		/// <summary>
		/// Sets the workspace you want to edit.
		/// </summary>
		/// <param name="ws">The workspace which will be edited.</param>
		internal void EditWorkspace(Workspace ws)
		{
			// fill the form
			nameTextBox.Text= ws.Name;
			behaviorFolderTextBox.Text= ws.Folder;
			exportFolderTextBox.Text= ws.DefaultExportFolder;

			// select plugins
			for(int i= 0; i <checkedListBox.Items.Count; ++i)
			{
				if(ws.Plugins.Contains( (string) checkedListBox.Items[i] ))
					checkedListBox.SetItemChecked(i, true);
			}
		}

		/// <summary>
		/// Handles when the done button is pressed.
		/// </summary>
		private void doneButton_Click(object sender, EventArgs e)
		{
			string name= nameTextBox.Text.Trim();
			string folder= behaviorFolderTextBox.Text.Trim();
			string defaultExportFolder= exportFolderTextBox.Text.Trim();

			// create the given folder if it does not exist
			if(!Directory.Exists(folder))
				Directory.CreateDirectory(folder);

			// create the given export folder if it does not exist
			if(defaultExportFolder !=string.Empty && !Directory.Exists(defaultExportFolder))
				Directory.CreateDirectory(defaultExportFolder);

			// check if this is a valid name, if we have plugins selected and if the selected folder exists
			if(name.Length >0 && checkedListBox.CheckedItems.Count >0 && Directory.Exists(folder))
			{
				// create the updated or new workspace
				_workspace= new Workspace(name, folder, defaultExportFolder);

				// update plugins
				foreach(string plugin in checkedListBox.CheckedItems)
					_workspace.AddPlugin(plugin);

				Close();
			}
		}

		/// <summary>
		/// Handles when the cancel button is pressed.
		/// </summary>
		private void cancelButton_Click(object sender, EventArgs e)
		{
			// we did not edit or create a workspace
			_workspace= null;

			Close();
		}

		/// <summary>
		/// Handles when the browse button for the behaviour folder is clicked.
		/// </summary>
		private void browseButton_Click(object sender, EventArgs e)
		{
			// assign the user path entered b the user to the browse dialogue
			if(behaviorFolderTextBox.Text !=string.Empty)
				folderBrowserDialog.SelectedPath= Path.GetFullPath(behaviorFolderTextBox.Text);

			// assign the path selected by the user to the textbox
			if(folderBrowserDialog.ShowDialog() ==DialogResult.OK)
				behaviorFolderTextBox.Text= folderBrowserDialog.SelectedPath;
		}

		/// <summary>
		/// Handles when the browse button for the default export folder is clicked.
		/// </summary>
		private void exportFolderButton_Click(object sender, EventArgs e)
		{
			// assign the user path entered b the user to the browse dialogue
			if(exportFolderTextBox.Text !=string.Empty)
				folderBrowserDialog.SelectedPath= Path.GetFullPath(exportFolderTextBox.Text);

			// assign the path selected by the user to the textbox
			if(folderBrowserDialog.ShowDialog() ==DialogResult.OK)
				exportFolderTextBox.Text= folderBrowserDialog.SelectedPath;
		}
	}
}