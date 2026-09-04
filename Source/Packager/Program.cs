using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

namespace Packager
{
    class Program
    {
        private static string _outputPath = "../../BuildFinal/";

        static void Main(string[] args)
        {
            _outputPath = AppDomain.CurrentDomain.BaseDirectory + _outputPath;

            /* Everything under here is written by this tool, so it starts empty. Now that the app
             * ships as an exe beside its libraries rather than as one bundled exe, a DLL left behind
             * by an earlier build would otherwise be uploaded alongside the one that replaced it. */
            if (Directory.Exists(_outputPath)) Directory.Delete(_outputPath, true);
            Directory.CreateDirectory(_outputPath);

            CopyProjectToBuild("Source/Dependencies/BehaviourTreeEditor/Build/", "legendplugin");
            CopyProjectToBuild("Source/Dependencies/CinematicTools/Build/", "cinematictools");
            CopyProjectToBuild("Source/Dependencies/RuntimeUtils/build/", "runtimeutils");
            CopyProjectToBuild("Source/Dependencies/LevelViewer/Build/", "levelviewer"); //requires manual local build in godot

            CopyBuildOutput("Build/");
            File.Copy(AppDomain.CurrentDomain.BaseDirectory + "../steam_api64.dll", _outputPath + "steam_api64.dll", true);
            File.Copy(AppDomain.CurrentDomain.BaseDirectory + "../../THIRD-PARTY-NOTICES.md", _outputPath + "THIRD-PARTY-NOTICES.md", true);

            string version = "";
            {
                string[] v = File.ReadAllLines(AppDomain.CurrentDomain.BaseDirectory + "/Properties/AssemblyInfo.cs");
                foreach (string l in v)
                {
                    if (l.Contains("AssemblyFileVersion"))
                    {
                        string[] lS = l.Split('"');
                        version = lS[1];
                    }
                }
            }
            Console.WriteLine("PACKAGER: Finished copying for version: " + version);
        }

        static void CopyProjectToBuild(string originalPath, string archiveName)
        {
            if (Directory.Exists(_outputPath + "/" + archiveName))
                Directory.Delete(_outputPath + "/" + archiveName, true);
            Directory.CreateDirectory(_outputPath + "/" + archiveName);

            Console.WriteLine("PACKAGER: Copying files for: " + archiveName);

            string exclusionsFile = "OPENCAGE_EXCEPTIONS";
            string folderPath = AppDomain.CurrentDomain.BaseDirectory + "../../" + originalPath;

            Dictionary<string, int> exclusions = new Dictionary<string, int>();
            if (File.Exists(folderPath + exclusionsFile))
            {
                string[] exclusionsFileContent = File.ReadAllLines(folderPath + exclusionsFile);
                foreach (string exclusion in exclusionsFileContent)
                {
                    exclusions.Add(exclusion, 0);
                }
                exclusions.Add(exclusionsFile, 0);
            }

            string[] files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
            int copyCount = 0;
            foreach (string file in files)
            {
                string filepathLocal = file.Replace(folderPath, "");
                bool excluded = false;
                foreach (KeyValuePair<string, int> exclusion in exclusions)
                {
                    if (filepathLocal.ToUpper().StartsWith(exclusion.Key.ToUpper()) || filepathLocal.ToUpper().EndsWith(exclusion.Key.ToUpper()))
                    {
                        exclusions[exclusion.Key]++;
                        excluded = true;
                        break;
                    }
                }
                if (excluded)
                    continue;

                string filepathDestination = _outputPath + "/" + archiveName + "/" + filepathLocal;
                Directory.CreateDirectory(filepathDestination.Substring(0, filepathDestination.Length - Path.GetFileName(filepathDestination).Length));
                File.Copy(file, filepathDestination);
                copyCount++;
            }

            foreach (KeyValuePair<string, int> exclusion in exclusions)
            {
                if (exclusion.Value == 0)
                    continue;
                Console.WriteLine("\tSkipped " + exclusion.Value + " file(s) under rule: " + exclusion.Key);
            }
            Console.WriteLine("\tCopied " + copyCount + " files to build.");

        }

        /* What the program is made of. Anything else MSBuild leaves in the build folder - debug
         * symbols, the API documentation that comes with a NuGet package, the settings file the app
         * writes beside itself - is a developer's business and not a user's. */
        private static readonly string[] _shippedExtensions = new string[] { ".exe", ".dll", ".config" };

        /* Subfolders of the build output that are part of the program: the native binaries the app
         * ships beside itself, and the one MSBuild makes - runtimes\<rid>\native, where AssimpNet
         * looks for its native library. The list is opt-in so that a folder someone left behind while
         * working, a test harness or a scratch build, stays behind rather than being uploaded because
         * it happened to contain a DLL. */
        private static readonly string[] _shippedFolders = new string[] { "Native", "runtimes" };

        /// <summary>
        /// Copy the built application into the root of the build, by rule rather than by name, so a
        /// library added to the project from now on travels without this tool having to be touched.
        /// </summary>
        static void CopyBuildOutput(string originalPath)
        {
            string folderPath = AppDomain.CurrentDomain.BaseDirectory + "../../" + originalPath;

            Console.WriteLine("PACKAGER: Copying files for: OpenCAGE");

            List<string> files = new List<string>(Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly));
            foreach (string folder in _shippedFolders)
            {
                if (Directory.Exists(folderPath + folder))
                    files.AddRange(Directory.GetFiles(folderPath + folder, "*.*", SearchOption.AllDirectories));
            }

            int copyCount = 0;
            int skipCount = 0;
            foreach (string file in files)
            {
                if (!_shippedExtensions.Contains(Path.GetExtension(file).ToLower()))
                {
                    skipCount++;
                    continue;
                }

                string filepathDestination = _outputPath + "/" + file.Replace(folderPath, "");
                Directory.CreateDirectory(Path.GetDirectoryName(filepathDestination));
                File.Copy(file, filepathDestination, true);
                copyCount++;
            }

            if (skipCount != 0)
                Console.WriteLine("\tSkipped " + skipCount + " file(s) that aren't part of the program.");
            Console.WriteLine("\tCopied " + copyCount + " files to build.");
        }
    }
}
