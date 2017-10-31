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
using System.Globalization;
using System.IO;

namespace Brainiac.Design.FileManagers
{
	/// <summary>
	/// This is the base class for a file manager which allows you to load and save behaviours.
	/// </summary>
	public abstract class FileManager
	{
		protected static Nodes.BehaviorNode _loadedBehavior= null;

		/// <summary>
		/// Holds the currently loaded behaviour so you can resolve circular references when loading.
		/// </summary>
		public static Nodes.BehaviorNode LoadedBehavior
		{
			get { return _loadedBehavior; }
		}

		/// <summary>
		/// Resets the loaded behaviour. This must be done before every user triggered loading process.
		/// </summary>
		public static void ResetLoadedBehavior()
		{
			_loadedBehavior= null;
		}

		protected Nodes.BehaviorNode _node;

		/// <summary>
		/// The node which will be saved or was loaded.
		/// </summary>
		public Nodes.BehaviorNode Node
		{
			get { return _node; }
		}

		protected string _filename;

		/// <summary>
		/// The filename which will be loaded from or saved to.
		/// </summary>
		public string Filename
		{
			get { return _filename; }
			set { _filename= Path.GetFullPath(value); }
		}

		/// <summary>
		/// Creates a new file manager.
		/// </summary>
		/// <param name="filename">The filename we want to load from or save to.</param>
		/// <param name="node">The behaviour we want to save. For loading use null.</param>
		public FileManager(string filename, Nodes.BehaviorNode node)
		{
			_filename= Path.GetFullPath(filename);
			_node= node;
		}

		/// <summary>
		/// Makes a given file writable.
		/// </summary>
		/// <param name="filename">The file which is supposed to be writable.</param>
		public static void MakeWritable(string filename)
		{
			if(filename !=string.Empty && File.Exists(filename))
			{
				FileAttributes fatts= File.GetAttributes(filename);
				if((fatts & FileAttributes.ReadOnly) !=0)
				{
					fatts-= FileAttributes.ReadOnly;
					File.SetAttributes(filename, fatts);
				}
			}
		}

		/// <summary>
		/// Generates a list of all files and folders which contain behaviours which may be processed.
		/// </summary>
		/// <param name="fileManagers">A list of all avilable file managers which could load a behaviour.</param>
		/// <param name="rootFolder">The folder to start in.</param>
		/// <param name="files">A list of all files found.</param>
		/// <param name="folders">A list of all folders found.</param>
		public static void CollectBehaviors(List<FileManagerInfo> fileManagers, string rootFolder, out IList<string> files, out IList<string> folders)
		{
			folders= new List<string>();
			files= new List<string>();

			// search all folders for behaviours which must be added to the tree
			folders.Add(rootFolder);
			for(int i= 0; i <folders.Count; ++i)
			{
				// add any subfolders
				string[] subfolders= Directory.GetDirectories(folders[i]);
				for(int s= 0; s <subfolders.Length; ++s)
				{
					// we skip hidden and system folders
					if((File.GetAttributes(subfolders[s]) & (FileAttributes.Hidden|FileAttributes.System)) ==0)
						folders.Add(subfolders[s]);
				}

				// search the files of the current folder
				string[] foundFiles= Directory.GetFiles(folders[i], "*.*", SearchOption.TopDirectoryOnly);
				for(int f= 0; f <foundFiles.Length; ++f)
				{
					// we only add files which can be loaded by a file manager
					bool hasFileManger= false;
					foreach(FileManagerInfo fileman in fileManagers)
					{
						if(foundFiles[f].ToLowerInvariant().EndsWith(fileman.FileExtension))
						{
							hasFileManger= true;
							break;
						}
					}

					// if there is no filemanager for this file, we skip it
					if(!hasFileManger)
						continue;

					// we skip hidden and system files
					if((File.GetAttributes(foundFiles[f]) & (FileAttributes.Hidden|FileAttributes.System)) !=0)
						continue;

					// add to list
					files.Add(foundFiles[f]);
				}
			}

			files= ((List<string>)files).AsReadOnly();
			folders= ((List<string>)folders).AsReadOnly();
		}

		/// <summary>
		/// Removed the read-only attribute from the given file.
		/// </summary>
		protected void MakeWritable()
		{
			MakeWritable(_filename);
		}

		/// <summary>
		/// Loads the behaviour of the given file and stores it in the Node member.
		/// </summary>
		public abstract void Load();

		/// <summary>
		/// Saves the given behaviour into the given file.
		/// </summary>
		public abstract void Save();
	}
}
