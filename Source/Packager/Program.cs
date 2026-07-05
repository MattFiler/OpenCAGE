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
        private static string _outputPath = "../../BuildFinal/Assets/";

        static void Main(string[] args)
        {
            _outputPath = AppDomain.CurrentDomain.BaseDirectory + _outputPath;
            if (Directory.Exists(_outputPath + "../"))
                Directory.Delete(_outputPath + "../", true);
            Directory.CreateDirectory(_outputPath);

            CopyProjectToBuild("Source/Dependencies/BehaviourTreeEditor/Build/", "legendplugin");
            CopyProjectToBuild("Source/Dependencies/CinematicTools/Build/", "cinematictools");
            CopyProjectToBuild("Source/Dependencies/RuntimeUtils/build/", "runtimeutils");
            CopyProjectToBuild("Source/Dependencies/LevelViewer/Build/", "levelviewer"); //requires manual local build in godot

            File.Copy(AppDomain.CurrentDomain.BaseDirectory + "../../Build/OpenCAGE.exe", _outputPath + "../OpenCAGE.exe", true);
            File.Copy(AppDomain.CurrentDomain.BaseDirectory + "../steam_api64.dll", _outputPath + "../steam_api64.dll", true);
        }

        static void CopyProjectToBuild(string originalPath, string archiveName)
        {
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
                    if (filepathLocal.ToUpper().StartsWith(exclusion.Key.ToUpper()))
                    {
                        exclusions[exclusion.Key]++;
                        excluded = true;
                        break;
                    }
                }
                if (excluded)
                    continue;

                File.Copy(file, _outputPath + "/" + archiveName + "/" + filepathLocal);
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
    }
}
