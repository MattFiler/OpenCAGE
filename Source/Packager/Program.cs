using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Packager
{
    class Program
    {
        private static JArray manifest_files = new JArray();
        private static string output_path = "../../Assets/";

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
            WriteFilesToArchive("FONTS/", "fonts");
            WriteFilesToArchive("Resources/", "builtdata");
            WriteFilesToArchive("Reset Files/", "resetfiles");
            WriteFilesToArchive("Sound Resources/", "soundinfo");
            WriteFilesToArchive("CathodeEditor/", "cathodeeditor");
            //END OF LIST

            Console.WriteLine("PACKAGER: Saving manifest.");
            JObject manifest_config = JObject.Parse("{}");
            manifest_config["archives"] = manifest_files;
            File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + output_path + "assets.manifest", manifest_config.ToString(Formatting.Indented));
        }

        static void WriteFilesToArchive(string originalPath, string outputFilename)
        {
            Console.WriteLine("PACKAGER: Creating archive: " + outputFilename);

            string exceptionsFile = "OPENCAGE_EXCEPTIONS";
            string folderPath = AppDomain.CurrentDomain.BaseDirectory + originalPath;
            string filenameWithoutExtension = AppDomain.CurrentDomain.BaseDirectory + output_path + outputFilename;

            List<string> fileExceptions = new List<string>();
            if (File.Exists(folderPath + exceptionsFile)) fileExceptions.AddRange(File.ReadAllLines(folderPath + exceptionsFile));
            fileExceptions.Add(exceptionsFile);

            string[] files = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
            BinaryWriter writer = new BinaryWriter(File.OpenWrite(filenameWithoutExtension + "_temp.archive"));
            writer.BaseStream.SetLength(0);
            writer.Write(0);
            int writeCount = 0;
            foreach (string file in files)
            {
                string filepathLocal = file.Replace(AppDomain.CurrentDomain.BaseDirectory, "");
                if (fileExceptions.Contains(filepathLocal.Substring(originalPath.Length))) continue;
                byte[] this_file = File.ReadAllBytes(file);
                writer.Write(filepathLocal);
                writer.Write(this_file.Length);
                writer.Write(this_file);
                writeCount++;
            }
            writer.BaseStream.Position = 0;
            writer.Write(writeCount);
            int file_length = (int)writer.BaseStream.Length;
            writer.Close();

            FileInfo oldArchive = null;
            if (File.Exists(filenameWithoutExtension + ".archive")) oldArchive = new FileInfo(filenameWithoutExtension + ".archive");
            FileInfo newArchive = new FileInfo(filenameWithoutExtension + "_temp.archive");
            if (oldArchive == null || oldArchive.Length != newArchive.Length)
            {
                if (File.Exists(filenameWithoutExtension + ".archive")) File.Delete(filenameWithoutExtension + ".archive");
                File.Move(filenameWithoutExtension + "_temp.archive", filenameWithoutExtension + ".archive");
            }
            else
            {
                File.Delete(filenameWithoutExtension + "_temp.archive");
            }

            manifest_files.Add(JObject.Parse("{\"name\":\"" + outputFilename + "\",\"size\":\"" + file_length + "\"}"));
        }
    }
}
