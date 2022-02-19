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
        private Random random = new Random();

        public Updater()
        {
            InitializeComponent();
        }

        private string PathToAssets = "/DATA/MODTOOLS/REMOTE_ASSETS/";
        private string GithubPath = "https://raw.githubusercontent.com/MattFiler/OpenCAGE/";

        private List<DownloadData> download_data = new List<DownloadData>();

        private void Updater_Load(object sender, EventArgs e)
        {
            this.TopMost = true;

            //"staging" = beta, "master" = ship
            if (OpenCAGE.SettingsManager.GetBool("CONFIG_UseStagingBranch")) GithubPath += "staging/";
            else GithubPath += "master/";

            //Read path to A:I and prepend it to PathToAssets (modtools_locales is for legacy support)
            string pathToAI = OpenCAGE.SettingsManager.GetString("PATH_GameRoot");
            if (File.Exists("modtools_locales.ayz"))
            {
                BinaryReader reader = new BinaryReader(File.OpenRead("modtools_locales.ayz"));
                reader.BaseStream.Position += 2; //Skip version
                pathToAI = reader.ReadString();
                reader.Close();
            }
            PathToAssets = pathToAI + PathToAssets;

            //Kill all OpenCAGE processes
            List<Process> allProcesses = new List<Process>(Process.GetProcessesByName("OpenCAGE"));
            List<string> processNames = new List<string>(Directory.GetFiles(PathToAssets, "*.exe", SearchOption.AllDirectories));
            for (int i = 0; i < processNames.Count; i++) allProcesses.AddRange(Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processNames[i])));
            for (int i = 0; i < allProcesses.Count; i++) try { allProcesses[i].Kill(); } catch { }

            try
            {
                //Download the current manifest
                WebClient downloadManifestClient = new WebClient();
                Directory.CreateDirectory(PathToAssets);
                JObject asset_manifest_current = ReadAssetsManifest();
                downloadManifestClient.DownloadProgressChanged += (s, clientprogress) =>
                {
                    UpdateProgress.Value = clientprogress.ProgressPercentage;
                    this.Refresh();
                };
                downloadManifestClient.DownloadFileCompleted += (s, clientprogress) =>
                {
                    if (clientprogress.Error != null)
                    {
                        MessageBox.Show("Encountered an error while downloading update manifest!\nPlease check your firewall.\n\n" + clientprogress.Error.Message, "Error fetching manifest!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        //Remove the old OpenCAGE version
                        try
                        {
                            if (File.Exists("Mod Tools.exe")) File.Delete("Mod Tools.exe");
                            if (File.Exists("OpenCAGE.exe")) File.Delete("OpenCAGE.exe");
                        }
                        catch
                        {
                            ErrorMessageAndQuit("Please close OpenCAGE and run the OpenCAGE Updater."); //Shouldn't hit this, unless we have a permissions issue.
                            return;
                        }
                        UpdateProgress.Value = 100;

                        //Check to see if we need to download any new assets
                        JObject asset_manifest_new = ReadAssetsManifest();
                        foreach (JObject manifest_entry_new in asset_manifest_new["archives"])
                        {
                            bool upToDate = false;
                            foreach (JObject manifest_entry_current in asset_manifest_current["archives"])
                            {
                                if (manifest_entry_current["name"].Value<string>() == manifest_entry_new["name"].Value<string>())
                                {
                                    upToDate = (manifest_entry_current["size"].Value<int>() == manifest_entry_new["size"].Value<int>());
                                    break;
                                }
                            }
                            if (upToDate) continue;
                            string local_file_path = PathToAssets + manifest_entry_new["name"] + ".archive";
                            Directory.CreateDirectory(local_file_path.Substring(0, local_file_path.Length - Path.GetFileName(local_file_path).Length));
                            download_data.Add(new DownloadData(GithubPath + "Assets/" + manifest_entry_new["name"] + ".archive?v=" + random.Next(5000), local_file_path));
                        }

                        //Obviously, we also need to download the OpenCAGE update!
                        download_data.Add(new DownloadData(GithubPath + "OpenCAGE.exe?v=" + random.Next(5000), "OpenCAGE.exe"));

                        //Start downloading
                        DownloadAsync();
                    }
                };
                downloadManifestClient.DownloadFileAsync(new Uri(GithubPath + "Assets/assets.manifest?v=" + random.Next(5000)), PathToAssets + "assets.manifest");
            }
            catch
            {
                MessageBox.Show("Update and configuration failed!\nPlease ensure you are connected to the internet.", "OpenCAGE Updater Failure!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private JObject ReadAssetsManifest()
        {
            string asset_manifest_text = "";
            if (File.Exists(PathToAssets + "assets.manifest")) asset_manifest_text = File.ReadAllText(PathToAssets + "assets.manifest");
            if (asset_manifest_text == "") asset_manifest_text = "{\"archives\":[]}";
            return JObject.Parse(asset_manifest_text);
        }

        /* Show error msg */
        private void ErrorMessageAndQuit(string message)
        {
            MessageBox.Show(message, "Update failed!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
            Environment.Exit(0);
        }

        /* Recursively download the required files async */
        private void DownloadAsync()
        {
            WebClient downloadToolClient = new WebClient();
            downloadToolClient.DownloadProgressChanged += (s, clientprogress) =>
            {
                UpdateProgress.Value = clientprogress.ProgressPercentage;
            };
            downloadToolClient.DownloadFileCompleted += (s, clientprogress) =>
            {
                UpdateProgress.Value = 100;
                download_data.RemoveAt(0);

                if (download_data.Count == 0)
                {
                    //If any new updates downloaded, extract them
                    string[] update_files = Directory.GetFiles(PathToAssets, "*.archive", SearchOption.TopDirectoryOnly);
                    foreach (string update_file in update_files)
                    {
                        BinaryReader reader = new BinaryReader(File.OpenRead(update_file));
                        int file_count = reader.ReadInt32();
                        for (int i = 0; i < file_count; i++)
                        {
                            string file_name = reader.ReadString();
                            int file_length = reader.ReadInt32();
                            byte[] file_content = reader.ReadBytes(file_length);
                            Directory.CreateDirectory((PathToAssets + file_name).Substring(0, (PathToAssets + file_name).Length - Path.GetFileName(PathToAssets + file_name).Length));
                            if (File.Exists(PathToAssets + file_name)) File.Delete(PathToAssets + file_name);
                            File.WriteAllBytes(PathToAssets + file_name, file_content);
                        }
                        reader.Close();
                        File.Delete(update_file);
                    }

                    //Launch OpenCAGE and close us
                    try { Process.Start("OpenCAGE.exe"); } catch { }
                    Application.Exit();
                    Environment.Exit(0);
                }
                else
                {
                    DownloadAsync();
                }
            };
            downloadToolClient.DownloadFileAsync(new Uri(download_data[0].URL), download_data[0].PATH);
        }
    }

    class DownloadData
    { 
        public DownloadData(string _u, string _p)
        {
            URL = _u;
            PATH = _p;
        }
        public string URL;
        public string PATH;
    }
}
