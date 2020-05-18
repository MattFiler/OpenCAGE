using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Updater
{
    public partial class Updater : Form
    {
        public Updater()
        {
            InitializeComponent();
        }

        private string PathToAssets = "/DATA/MODTOOLS/ASSETS/"; //THIS MUST MATCH ToolPaths.cs IN OPENCAGE
        private string GithubPath = "https://raw.githubusercontent.com/MattFiler/OpenCAGE/staging/"; //"staging" = beta, "master" = ship

        private void Updater_Load(object sender, EventArgs e)
        {
            this.TopMost = true;

            //Need OpenCAGE to have run first to generate this info
            if (!File.Exists("modtools_locales.ayz"))
            {
                ErrorMessageAndQuit("Please run OpenCAGE, not the OpenCAGE updater!");
                return;
            }

            //Read path to A:I and prepend it to PathToAssets
            BinaryReader reader = new BinaryReader(File.OpenRead("modtools_locales.ayz"));
            reader.BaseStream.Position += 2; //Skip version
            PathToAssets = reader.ReadString() + PathToAssets;
            reader.Close();

            //Remove the old OpenCAGE version
            try
            {
                if (File.Exists("Mod Tools.exe")) File.Delete("Mod Tools.exe");
                if (File.Exists("OpenCAGE.exe")) File.Delete("OpenCAGE.exe");
            }
            catch
            {
                ErrorMessageAndQuit("Please ensure OpenCAGE is not open before updating.");
                return;
            }

            //Download the current manifest
            WebClient webClient = new WebClient();
            var _downloadQueue = new Queue<Uri>();
            Directory.CreateDirectory(PathToAssets);
            JObject asset_manifest_current;
            if (File.Exists(PathToAssets + "assets.manifest")) asset_manifest_current = JObject.Parse(File.ReadAllText(PathToAssets + "assets.manifest"));
            else asset_manifest_current = JObject.Parse("{\"archives\":[]}");
            webClient.DownloadProgressChanged += (s, clientprogress) =>
            {
                UpdateProgress.Value = clientprogress.ProgressPercentage;
            };
            webClient.DownloadFileCompleted += (s, clientprogress) =>
            {
                UpdateProgress.Value = 100;

                //Check to see if we need to download any new assets
                JObject asset_manifest_new = JObject.Parse(File.ReadAllText(PathToAssets + "assets.manifest"));
                foreach (JObject manifest_entry_new in asset_manifest_new["archives"])
                {
                    bool upToDate = false;
                    foreach (JObject manifest_entry_current in asset_manifest_current["archives"])
                    {
                        if (manifest_entry_current["name"] == manifest_entry_new["name"])
                        {
                            upToDate = (manifest_entry_current["size"] == manifest_entry_new["size"]);
                            break;
                        }
                    }
                    if (upToDate) continue;
                    string local_file_path = PathToAssets + manifest_entry_new["name"] + ".archive";
                    Directory.CreateDirectory(local_file_path.Substring(0, local_file_path.Length - Path.GetFileName(local_file_path).Length));
                    webClient.DownloadFile(GithubPath + "Assets/" + manifest_entry_new["name"] + ".archive", local_file_path);
                }

                //If any new updates downloaded, extract them
                string[] update_files = Directory.GetFiles(PathToAssets, "*.archive", SearchOption.TopDirectoryOnly);
                foreach (string update_file in update_files)
                {
                    reader = new BinaryReader(File.OpenRead(update_file));
                    int file_count = reader.ReadInt32();
                    for (int i = 0; i < file_count; i++)
                    {
                        string file_name = reader.ReadString();
                        int file_length = reader.ReadInt32();
                        byte[] file_content = reader.ReadBytes(file_length);
                        if (File.Exists(PathToAssets + file_name)) File.Delete(PathToAssets + file_name);
                        File.WriteAllBytes(PathToAssets + file_name, file_content);
                    }
                    reader.Close();
                    File.Delete(update_file);
                }

                //Download the new OpenCAGE version itself
                webClient.DownloadProgressChanged += (s3, clientprogress3) =>
                {
                    UpdateProgress.Value = clientprogress3.ProgressPercentage;
                };
                webClient.DownloadFileCompleted += (s3, clientprogress3) =>
                {
                    UpdateProgress.Value = 100;
                    CloseUpdaterAndLaunchOpenCAGE();
                };
                webClient.DownloadFileAsync(new Uri(GithubPath + "OpenCAGE.exe"), "OpenCAGE.exe");
            };
            webClient.DownloadFileAsync(new Uri(GithubPath + "Assets/assets.manifest"), PathToAssets + "assets.manifest");
        }

        private void ErrorMessageAndQuit(string message)
        {
            MessageBox.Show(message, "Update failed!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
            Environment.Exit(0);
        }

        private void CloseUpdaterAndLaunchOpenCAGE()
        {
            Process.Start("OpenCAGE.exe");
            Application.Exit();
            Environment.Exit(0);
        }
    }
}
