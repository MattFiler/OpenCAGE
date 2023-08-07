using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Packager
{
    class Program
    {
        private static string _outputPath = "../../Assets/";
        private static JArray _archiveMetadatas = new JArray();
        private static List<string> _archiveFiles = new List<string>();

        /*
         * 
         * Tool to package resources for OpenCAGE. These packaged resources form "archive" banks.
         * A manifest file is generated on each run which lists the banks and their sizes.
         * Banks are pushed to Github and auto downloaded via the OpenCAGE updater by querying the manifest for changes.
         * 
         * To add a new resource folder for OpenCAGE to use, call WriteFilesToArchive below.
         * This tool auto builds and runs every time OpenCAGE is built, so banks will always be up to date.
         * 
        */

        static void Main(string[] args)
        {
            //LIST ALL RESOURCE FOLDERS TO INCLUDE HERE
            WriteFilesToArchive("Source/Dependencies/AssetEditor/Build/", "asseteditor");
            WriteFilesToArchive("Source/Dependencies/BehaviourTreeEditor/Build/", "legendplugin");
            WriteFilesToArchive("Source/Dependencies/CommandsEditor/Build/", "scripteditor");
            WriteFilesToArchive("Source/Dependencies/ConfigEditor/Build/", "configeditor");
            WriteFilesToArchive("Source/Dependencies/CinematicTools/Build/", "cinematictools");
            WriteFilesToArchive("Source/Dependencies/RuntimeUtils/build/", "runtimeutils");
            WriteFilesToArchive("Source/Dependencies/LaunchGame/Build/", "launchgame");
            WriteFilesToArchive("Source/Dependencies/LevelBackup/Builds/", "levelbackup");
            //END OF LIST

            Console.WriteLine("PACKAGER: Saving manifest.");
            JObject manifest = JObject.Parse("{}");
            manifest["archives"] = _archiveMetadatas;
            File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + _outputPath + "assets.manifest", manifest.ToString(Formatting.Indented));

            string[] files = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + _outputPath, "*.*", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (fileName != "assets" && !_archiveFiles.Contains(fileName)) File.Delete(file);
            }
        }

        static void WriteFilesToArchive(string originalPath, string archiveName)
        {
            Console.WriteLine("PACKAGER: Creating archive: " + archiveName);
            _archiveFiles.Add(archiveName);

            string exceptionsFile = "OPENCAGE_EXCEPTIONS";
            string folderPath = AppDomain.CurrentDomain.BaseDirectory + "../../" + originalPath;

            List<string> fileExceptions = new List<string>();
            if (File.Exists(folderPath + exceptionsFile)) fileExceptions.AddRange(File.ReadAllLines(folderPath + exceptionsFile));
            fileExceptions.Add(exceptionsFile);

            string archivePath = AppDomain.CurrentDomain.BaseDirectory + _outputPath + archiveName + ".archive";
            using (MemoryStream stream = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(stream);
                writer.BaseStream.SetLength(0);
                writer.Write(0);
                int writeCount = 0;
                string[] files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    string filepathLocal = file.Replace(folderPath, "");
                    if (fileExceptions.Contains(filepathLocal)) continue;
                    byte[] fileContent = File.ReadAllBytes(file);
                    writer.Write(archiveName + "/" + filepathLocal);
                    writer.Write(fileContent.Length);
                    writer.Write(fileContent);
                    writeCount++;
                }
                writer.BaseStream.Position = 0;
                writer.Write(writeCount);
                writer.Close();

                File.WriteAllBytes(archivePath, stream.ToArray());
            }

            MD5 md5 = MD5.Create();
            byte[] contentBytes = File.ReadAllBytes(archivePath);
            md5.TransformBlock(contentBytes, 0, contentBytes.Length, contentBytes, 0);
            md5.TransformFinalBlock(new byte[0], 0, 0);
            string archiveHash = BitConverter.ToString(md5.Hash).Replace("-", "").ToLower();

            _archiveMetadatas.Add(JObject.Parse("{\"name\":\"" + archiveName + "\",\"size\":\"" + contentBytes.Length + "\",\"hash\":\"" + archiveHash + "\"}"));
        }
    }
}
