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
	internal partial class ExportDialog : Form
	{
		internal static string LastUsedExportFolder= string.Empty;

		public ExportDialog()
		{
			InitializeComponent();

			textBox.Text= LastUsedExportFolder;
			folderBrowserDialog.SelectedPath= LastUsedExportFolder;
		}

		/// <summary>
		/// Handles when the brows button is pressed.
		/// </summary>
		private void browseButton_Click(object sender, EventArgs e)
		{
			// assign the path entered by the user to the browse dialogue
			if(textBox.Text !=string.Empty)
				folderBrowserDialog.SelectedPath= Path.GetFullPath(textBox.Text);

			// if the user selected a path assign it to the textbox

			// DEVELOPER: If you get a LoaderLock exception here, turn off Debug -> Exceptions -> Managed Debugging Assistants -> LoaderLock
			if(folderBrowserDialog.ShowDialog() ==DialogResult.OK)
			{
				LastUsedExportFolder= folderBrowserDialog.SelectedPath;
				textBox.Text= folderBrowserDialog.SelectedPath;
			}
		}

		/// <summary>
		/// Keeps the check code from looping because of its own checking
		/// </summary>
		private bool _isCheckingNodes= false;

		/// <summary>
		/// Checks or unchecks all the child nodes.
		/// </summary>
		/// <param name="node">The node whose children will be checked or unchecked.</param>
		/// <param name="check">Determines whether to check or to uncheck the nodes.</param>
		private void CheckNodes(TreeNode node, bool check)
		{
			node.Checked= check;

			foreach(TreeNode child in node.Nodes)
				CheckNodes(child, check);
		}

		/// <summary>
		/// Handles actions that occur after the user checked/unchecked an item.
		/// </summary>
		private void treeView_AfterCheck(object sender, TreeViewEventArgs e)
		{
			// if the code is currently updating the checking we skip any post actions.
			if(_isCheckingNodes)
				return;

			// check if the user check a folder
			NodeTag nodetag= (NodeTag) e.Node.Tag;
			if(nodetag.Type ==NodeTagType.BehaviorFolder)
			{
				// disable the post actions
				_isCheckingNodes= true;

				// check/uncheck all the children
				CheckNodes(e.Node, e.Node.Checked);

				// enable post actions
				_isCheckingNodes= false;
			}
			else
			{
				// behaviour post stuff here
			}
		}
	}
}