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

namespace Brainiac.Design
{
	/// <summary>
	/// This class represents a workspace available to the user.
	/// </summary>
	internal sealed class Workspace
	{
		private string _name;

		/// <summary>
		/// The name of the workspace.
		/// </summary>
		internal string Name
		{
			get { return _name; }
		}

		private string _folder;

		/// <summary>
		/// The folder the behaviors are saved in.
		/// </summary>
		internal string Folder
		{
			get { return _folder; }
		}

		private string _defaultExportFolder;

		/// <summary>
		/// The default folder behaviours are exported to.
		/// </summary>
		internal string DefaultExportFolder
		{
			get { return _defaultExportFolder; }
		}

		private List<string> _plugins= new List<string>();

		/// <summary>
		/// The list of plugins which will be loaded for the workspace.
		/// </summary>
		internal IList<string> Plugins
		{
			get { return _plugins.AsReadOnly(); }
		}

		private string MakeAbsolutePath(string relativePath)
		{
			string absolute= relativePath;

			absolute= absolute.Replace("$DESKTOP", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
			absolute= absolute.Replace("$PERSONAL", Environment.GetFolderPath(Environment.SpecialFolder.Personal));
			absolute= absolute.Replace("$DOCUMENTS", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
			absolute= absolute.Replace("$APPDATA", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
			absolute= absolute.Replace("$LOCALAPPDATA", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
			absolute= absolute.Replace("$COMMONAPPDATA", Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));
			absolute= absolute.Replace("$PICTURES", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));

			return absolute;
		}

		private string MakeRelativePath(string absolutePath)
		{
			string relative= absolutePath;

			relative= relative.Replace(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "$DESKTOP");
			relative= relative.Replace(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "$DOCUMENTS");
			relative= relative.Replace(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "$APPDATA");
			relative= relative.Replace(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "$LOCALAPPDATA");
			relative= relative.Replace(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "$COMMONAPPDATA");
			relative= relative.Replace(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "$PICTURES");
			relative= relative.Replace(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "$PERSONAL");

			return relative;
		}

		/// <summary>
		/// Creates a new workspace.
		/// </summary>
		/// <param name="name">The name of the workspace.</param>
		/// <param name="folder">The folder the behaviors will be loaded from.</param>
		/// <param name="defaultExportFolder">The folder behaviours are exported to by default.</param>
		internal Workspace(string name, string folder, string defaultExportFolder)
		{
			_name= name;
			_folder= MakeAbsolutePath(folder);
			_defaultExportFolder= MakeAbsolutePath(defaultExportFolder);
		}

		internal string RelativeFolder
		{
			get { return MakeRelativePath(_folder); }
		}

		internal string RelativeDefaultExportFolder
		{
			get { return MakeRelativePath(_defaultExportFolder); }
		}

		/// <summary>
		/// Adds a plugin which will be loaded to the workspace.
		/// </summary>
		/// <param name="filename">The filename of the plugin which will be loaded.</param>
		internal void AddPlugin(string filename)
		{
			if(!_plugins.Contains(filename))
				_plugins.Add(filename);
		}

		/// <summary>
		/// This is required for the combobox used in the workspace selection dialogue.
		/// </summary>
		/// <returns>Returns the name of the workspace.</returns>
		public override string ToString()
		{
			return _name;
		}
	}
}
