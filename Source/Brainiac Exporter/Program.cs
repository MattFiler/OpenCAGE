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
using System.Reflection;
using System.IO;
using Brainiac.Design.Nodes;
using Brainiac.Design.Exporters;

namespace Brainiac.Design
{
	/// <summary>
	/// The main class of the application.
	/// </summary>
	class Program
	{
		/// <summary>
		/// This class is a BehaviorManagerInterface which must be available when loading the behaviours.
		/// </summary>
		private class Manager : BehaviorManagerInterface
		{
			public BehaviorNode GetBehavior(string filename)
			{
				throw new Exception("The method or operation is not implemented.");
			}

			public BehaviorNode LoadBehavior(string filename)
			{
				BehaviorNode behavior= null;
				FileManagers.FileManager filemanager= null;

				// search the file manager who is able to handle this file extension
				foreach(FileManagerInfo info in _fileManagers)
				{
					if(filename.ToLowerInvariant().EndsWith(info.FileExtension))
					{
						// create the file manager and load the behaviour
						filemanager= info.Create(filename, null);
						filemanager.Load();
						behavior= filemanager.Node;

						// assign the file's name to the root node
						((Node)behavior).Label= Path.GetFileNameWithoutExtension(filename);

						break;
					}
				}

				return behavior;
			}

			public string SaveBehavior(BehaviorNode node, bool saveas)
			{
				throw new Exception("The method or operation is not implemented.");
			}
		}

		/// <summary>
		/// This struct represents a file we want to export which is required when exporting folders.
		/// </summary>
		private struct FileToExport
		{
			/// <summary>
			/// The folder the file was found in. Is string.Empty if a file was given instead of a folder.
			/// </summary>
			internal string BaseFolder;

			/// <summary>
			/// The absolute filename we want to export
			/// </summary>
			internal string File;

			internal FileToExport(string file)
			{
				BaseFolder= string.Empty;
				File= file;
			}

			internal FileToExport(string folder, string file)
			{
				BaseFolder= folder;
				File= file;
			}
		}

		/// <summary>
		/// Create an instance of the behaviour manager.
		/// </summary>
		private static Manager _behaviorManager= new Manager();

		private static List<FileManagerInfo> _fileManagers= new List<FileManagerInfo>();
		private static List<ExporterInfo> _exporters= new List<ExporterInfo>();

		/// <summary>
		/// Output a text message.
		/// </summary>
		/// <param name="msg">The message you want to display.</param>
		internal static void LogMessage(string msg)
		{
			Console.Write( string.Format("{0}\r\n", msg) );
		}

		/// <summary>
		/// Output a warning
		/// </summary>
		/// <param name="msg">The message you want to display.</param>
		internal static void LogWarning(string msg)
		{
			Console.ForegroundColor= ConsoleColor.Yellow;
			Console.Write( string.Format("Warning: {0}\r\n", msg) );
			Console.ForegroundColor= ConsoleColor.White;
		}

		/// <summary>
		/// Output an error.
		/// </summary>
		/// <param name="msg">The message you want to display.</param>
		internal static void LogError(string msg)
		{
			Console.ForegroundColor= ConsoleColor.Red;
			Console.Write( string.Format("Error:   {0}\r\n", msg) );
			Console.ForegroundColor= ConsoleColor.White;
		}

		/// <summary>
		/// Load the plugins from a given folder.
		/// </summary>
		/// <param name="path">The folder the plugins will be loaded from.</param>
		private static void LoadPlugins(string path)
		{
			// get all the DLL files
			string[] files= Directory.GetFiles(path, "*.dll", SearchOption.TopDirectoryOnly);

			// for each DLL file
			for(int i= 0; i <files.Length; ++i)
			{
				Assembly assembly= Assembly.LoadFile( Path.GetFullPath(files[i]) );
				string classname= Path.GetFileNameWithoutExtension(files[i]);
				Plugin plugin= (Plugin) assembly.CreateInstance(classname +"."+ classname);

				// if it is a plugin, add its content
				if(plugin !=null)
				{
					Plugin.AddLoadedPlugin(assembly);

					_fileManagers.AddRange(plugin.FileManagers);
					_exporters.AddRange(plugin.Exporters);
				}
			}

			// check if there were any file managers
			if(_fileManagers.Count <1)
				throw new Exception("No file managers found, behaviors cannot be loaded.");

			// check if there were any exporters
			if(_exporters.Count <1)
				throw new Exception("No exporters found, behaviors cannot be exported");
		}

		/// <summary>
		/// Exports the found behaviour files.
		/// </summary>
		/// <param name="exporterinfo">The exporter info of the exporter we want to use.</param>
		/// <param name="files">The behaviour files.</param>
		/// <param name="outputFolder">The folder the behaviours will be exported into.</param>
		/// <param name="exportIntoOneFolder">Defines if we export all behaviours into the output folder directly or if we create any sub folders found.</param>
		private static void ExportBehaviors(ExporterInfo exporterinfo, List<FileToExport> files, string outputFolder, bool exportIntoOneFolder)
		{
			Exporter exporter= null;

			for(int i= 0; i <files.Count; ++i)
			{
				FileToExport file= files[i];

				LogMessage( string.Format("Exporting \"{0}\"...",file.File) );

				// reset previously loaded behavior
				FileManagers.FileManager.ResetLoadedBehavior();

				// load the behaviour file
				BehaviorNode behavior= _behaviorManager.LoadBehavior(file.File);
				if(behavior ==null)
					throw new Exception( string.Format("Could not load behavior {0}", file.File) );

				// check if the file we exported comes from a folder or a given file
				string relativefile;
				if(file.BaseFolder ==string.Empty || exportIntoOneFolder)
				{
					// simply use the filename as it is
					relativefile= Path.GetFileName(file.File);
				}
				else
				{
					// create a relative filename of the behaviour to the folder it comes from
					Uri root= new Uri( Path.GetFullPath(file.BaseFolder) +'\\');
					Uri thefile= new Uri( Path.GetFullPath(file.File) );

					Uri relative= root.MakeRelativeUri(thefile);

					relativefile= Uri.UnescapeDataString(relative.OriginalString).Replace('/', '\\');
				}

				// generate the export filename
				string targetfile= Path.GetFullPath(outputFolder +'\\'+ relativefile);

				// ensure that the export folder exists
				string finalOutputFolder= Path.GetDirectoryName(targetfile);
				Directory.CreateDirectory(finalOutputFolder);

				// remove the file extension
				//targetfile= targetfile.Substring(0, targetfile.Length - Path.GetExtension(targetfile).Length);
				relativefile= relativefile.Substring(0, relativefile.Length - Path.GetExtension(relativefile).Length);

				// export the behaviour
				bool firstRun= exporter ==null;
				exporter= exporterinfo.Create(behavior, outputFolder, relativefile);

				// check if we must call pre export
				if(firstRun)
				{
					if(!exporter.PreExport(true))
						throw new Exception("Export process was aborted");
				}

				exporter.Export();
			}

			// call post export
			if(exporter !=null)
				exporter.PostExport(true);
		}

		/// <summary>
		/// Initialise the exporter.
		/// </summary>
		/// <param name="args">Command-line arguments.</param>
		static void Main(string[] args)
		{
			// display the application name and version
			Version version= Assembly.GetExecutingAssembly().GetName().Version;

			string title= "Brainiac Exporter";
			title+= " "+ version.Major +"."+ version.Minor;

			if(version.Build !=0)
				title+= (char) (version.Build +0x60);

			if(version.Revision !=0)
				title+= string.Format(" ({0})", version.Revision);

			LogMessage(title);
			LogMessage("\r\nUsage:\r\n  export -exporter exporterid -input files & folders -output output_dir [-nosubfolders] [-pause]");

			// assign our behaviour manager
			BehaviorManager.Instance= _behaviorManager;

			try
			{
				// load the plugins
				LoadPlugins("plugins");

				// evaluate all the arguments
				List<FileToExport> files= new List<FileToExport>();
				string outputFolder= string.Empty;
				bool exportIntoOneFolder= false;
				string exporterid= string.Empty;
				bool pause= false;

				int i= 0;
				while(i <args.Length)
				{
					if(args[i] =="-nosubfolders")
					{
						exportIntoOneFolder= true;
						i++;
					}
					else if(args[i] =="-output")
					{
						if(i ==args.Length -1)
							throw new Exception("-output cannot be the last argument");

						outputFolder= args[i +1];
						i+= 2;
					}
					else if(args[i] =="-input")
					{
						if(i ==args.Length -1)
							throw new Exception("-output cannot be the last argument");

						// as long as we do not find another argument, keep adding files and folders
						while(++i <args.Length)
						{
							if(args[i].StartsWith("-"))
								break;

							string obj= args[i];
							if(Directory.Exists(obj))
							{
								// add the files from the given folder
								IList<string> foundFiles, foundFolders;
								FileManagers.FileManager.CollectBehaviors(_fileManagers, obj, out foundFiles, out foundFolders);
								for(int f= 0; f <foundFiles.Count; ++f)
									files.Add( new FileToExport(obj, foundFiles[f]) );
							}
							else if(File.Exists(obj))
							{
								// add the given file
								files.Add( new FileToExport(obj) );
							}
							else throw new Exception( string.Format("Invalid file/folder found: {0}", obj) );
						}
					}
					else if(args[i] =="-exporter")
					{
						if(i ==args.Length -1)
							throw new Exception("-exporter cannot be the last argument");

						exporterid= args[i +1];
						i+= 2;
					}
					else if(args[i] =="-pause")
					{
						pause= true;
						i++;
					}
					else throw new Exception("Invalid argument found: "+ args[i]);
				}

				// validate the read arguments
				if(files.Count <1)
					throw new Exception("No files to export");

				if(outputFolder ==String.Empty)
					throw new Exception("No output folder set");

				if(!Directory.Exists(outputFolder))
					throw new Exception("Output folder does not exist "+ outputFolder);

				if(exporterid ==string.Empty)
					throw new Exception("No exporterinfo given");

				// check if the stated exporter could be found in one of the plugins
				ExporterInfo exporterinfo= null;
				foreach(ExporterInfo ei in _exporters)
				{
					if(ei.ID.Equals(exporterid, StringComparison.OrdinalIgnoreCase))
					{
						exporterinfo= ei;
						break;
					}
				}

				if(exporterinfo ==null)
					throw new Exception( string.Format("No such exporterinfo: {0}", exporterid) );

				LogMessage("Exporting to: \""+ outputFolder +'"');

				// export the behaviours
				ExportBehaviors(exporterinfo, files, outputFolder, exportIntoOneFolder);

				// wait if the user asked for it
				if(pause)
				{
					LogMessage("\r\nPress a key to continue");
					Console.ReadKey();
				}
			}
			catch(Exception e) { LogError(e.Message); }
		}
	}
}
