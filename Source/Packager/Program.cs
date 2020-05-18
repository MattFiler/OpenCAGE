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

        static void Main(string[] args)
        {
            WriteFilesToArchive("FONTS/", "fonts");
            WriteFilesToArchive("Resources/", "builtdata");
            WriteFilesToArchive("Reset Files/", "resetfiles");

            Console.WriteLine("PACKAGER: Saving manifest.");
            JObject manifest_config = JObject.Parse("{}");
            manifest_config["archives"] = manifest_files;
            File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + output_path + "assets.manifest", manifest_config.ToString(Formatting.Indented));
        }

        static void WriteFilesToArchive(string originalPath, string outputFilename)
        {
            Console.WriteLine("PACKAGER: Creating archive: " + outputFilename);

            string[] files = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + originalPath, "*.*", SearchOption.AllDirectories);
            BinaryWriter writer = new BinaryWriter(File.OpenWrite(AppDomain.CurrentDomain.BaseDirectory + output_path + outputFilename + ".archive"));
            writer.BaseStream.SetLength(0);
            writer.Write(files.Length);
            foreach (string file in files)
            {
                if (Path.GetFileName(file) == "OpenCAGE Updater.exe") continue; //Hard-coded exception for the updater: this is an embedded resource.
                byte[] this_file = File.ReadAllBytes(file);
                writer.Write(file.Replace(AppDomain.CurrentDomain.BaseDirectory, ""));
                writer.Write(this_file.Length);
                writer.Write(this_file);
            }
            int file_length = (int)writer.BaseStream.Length;
            writer.Close();

            manifest_files.Add(JObject.Parse("{\"name\":\"" + outputFilename + "\",\"size\":\"" + file_length + "\"}"));
        }
    }
}
