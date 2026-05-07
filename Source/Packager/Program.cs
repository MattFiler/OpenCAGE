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
        private static JArray _archiveMetadatas = new JArray();
        private static List<string> _archiveFiles = new List<string>();

        static void Main(string[] args)
        {
            _outputPath = AppDomain.CurrentDomain.BaseDirectory + _outputPath;
            if (Directory.Exists(_outputPath + "../"))
                Directory.Delete(_outputPath + "../", true);
            Directory.CreateDirectory(_outputPath);

            WriteFilesToArchive("Source/Dependencies/BehaviourTreeEditor/Build/", "legendplugin");
            WriteFilesToArchive("Source/Dependencies/CinematicTools/Build/", "cinematictools");
            WriteFilesToArchive("Source/Dependencies/RuntimeUtils/build/", "runtimeutils");
            WriteFilesToArchive("Source/Dependencies/LevelViewer/CathodeEditorUnity/", "levelviewer");

            Console.WriteLine("PACKAGER: Saving manifest.");
            JObject manifest = JObject.Parse("{}");
            manifest["archives"] = _archiveMetadatas;
            manifest["version"] = "1";
            File.WriteAllText(_outputPath + "assets.manifest", manifest.ToString(Formatting.Indented));

            File.Copy(AppDomain.CurrentDomain.BaseDirectory + "../../Build/OpenCAGE.exe", _outputPath + "../OpenCAGE.exe", true);

            if (args.Contains("-STEAM"))
            {
                File.Copy(AppDomain.CurrentDomain.BaseDirectory + "../steam_api64.dll", _outputPath + "../steam_api64.dll", true);

                RunCommand("SteamCMD +login MattFiler +run_app_build \"" + AppDomain.CurrentDomain.BaseDirectory + "/../../appbuild.vdf\" +quit");
            }
            else
            {
                File.Copy(AppDomain.CurrentDomain.BaseDirectory + "/OpenCAGE Updater.exe", _outputPath + "../OpenCAGE Updater.exe", true);
                CompressFile(_outputPath + "../OpenCAGE.exe");

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
                if (version == "") throw new Exception("Failed to parse version info.");

                using (BinaryWriter writer = new BinaryWriter(File.Create(_outputPath + "../version.bin")))
                {
                    string[] v = version.Split('.');
                    writer.Write((byte)v.Length);
                    for (int i = 0; i < v.Length; i++)
                        writer.Write((short)int.Parse(v[i]));
                    Console.WriteLine("PACKAGER: Updated version to " + version);
                }

                //RunCommand("scp -r \"" + AppDomain.CurrentDomain.BaseDirectory + "/../../BuildFinal/\"* root@opencage.mattfiler.co.uk:/var/www/websites/opencage.mattfiler.co.uk/download/staging/");
                //RunCommand("ssh root@opencage.mattfiler.co.uk \"chmod -R 755 /var/www/websites/opencage.mattfiler.co.uk/download/staging/\"");
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

            string archivePath = _outputPath + archiveName + ".archive";
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.BaseStream.SetLength(0);
                    writer.Write(0);
                    int writeCount = 0;
                    string[] files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
                    foreach (string file in files)
                    {
                        string filepathLocal = file.Replace(folderPath, "");
                        if (fileExceptions.Contains(filepathLocal))
                        {
                            Console.WriteLine("\tSkipping: " + filepathLocal);
                            continue;
                        }
                        byte[] fileContent = File.ReadAllBytes(file);
                        writer.Write(archiveName + "/" + filepathLocal);
                        writer.Write(fileContent.Length);
                        writer.Write(fileContent);
                        writeCount++;
                    }
                    writer.BaseStream.Position = 0;
                    writer.Write(writeCount);
                }

                File.WriteAllBytes(archivePath, stream.ToArray());
            }

            MD5 md5 = MD5.Create();
            byte[] contentBytes = File.ReadAllBytes(archivePath);
            md5.TransformBlock(contentBytes, 0, contentBytes.Length, contentBytes, 0);
            md5.TransformFinalBlock(new byte[0], 0, 0);
            string archiveHash = BitConverter.ToString(md5.Hash).Replace("-", "").ToLower();

            _archiveMetadatas.Add(JObject.Parse("{\"name\":\"" + archiveName + "\",\"size\":\"" + contentBytes.Length + "\",\"hash\":\"" + archiveHash + "\"}"));

            CompressFile(archivePath);
        }

        static void RunCommand(string command)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(error))
                Debug.WriteLine($"Error: {error}");
        }

        static void CompressFile(string filepath)
        {
            byte[] content = File.ReadAllBytes(filepath);
            using (GZipStream gzipStream = new GZipStream(File.Create(filepath), CompressionMode.Compress))
                gzipStream.Write(content, 0, content.Length);
        }
    }
}
