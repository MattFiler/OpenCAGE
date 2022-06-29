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
        private Random _random = new Random();
        private string _assetPath = "/DATA/MODTOOLS/REMOTE_ASSETS/";
        private string _downloadURL = "https://raw.githubusercontent.com/MattFiler/OpenCAGE/";
        private List<DownloadData> _downloadData = new List<DownloadData>();

        public Updater()
        {
            InitializeComponent();
        }

        private void Updater_Load(object sender, EventArgs e)
        {
            this.TopMost = true; 
            
            //Attempt to fix SSL/TLS issue
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
                    | SecurityProtocolType.Tls11
                    | SecurityProtocolType.Tls12
                    | SecurityProtocolType.Ssl3;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            //"staging" = beta, "master" = ship
            if (OpenCAGE.SettingsManager.GetBool("CONFIG_UseStagingBranch")) _downloadURL += "staging/";
            else _downloadURL += "master/";

            //Read path to A:I and prepend it to PathToAssets (modtools_locales is for legacy support)
            string pathToAI = OpenCAGE.SettingsManager.GetString("PATH_GameRoot");
            if (File.Exists("modtools_locales.ayz"))
            {
                BinaryReader reader = new BinaryReader(File.OpenRead("modtools_locales.ayz"));
                reader.BaseStream.Position += 2; //Skip version
                pathToAI = reader.ReadString();
                reader.Close();
            }
            _assetPath = pathToAI + _assetPath;
            Directory.CreateDirectory(_assetPath);

            //Kill all OpenCAGE processes
            List<Process> allProcesses = new List<Process>(Process.GetProcessesByName("OpenCAGE"));
            List<string> processNames = new List<string>(Directory.GetFiles(_assetPath, "*.exe", SearchOption.AllDirectories));
            for (int i = 0; i < processNames.Count; i++) allProcesses.AddRange(Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processNames[i])));
            for (int i = 0; i < allProcesses.Count; i++) try { allProcesses[i].Kill(); } catch { }

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

            try
            {
                //Download the current manifest
                WebClient client = new WebClient();
                JObject localManifest = ReadAssetsManifest();
                client.DownloadProgressChanged += (s, progress) =>
                {
                    UpdateProgress.Value = progress.ProgressPercentage;
                    this.Refresh();
                };
                client.DownloadFileCompleted += (s, progress) =>
                {
                    if (progress.Error != null)
                    {
                        MessageBox.Show("Encountered an error while downloading update manifest!\n" + progress.Error.Message, "Error fetching manifest!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Application.Exit();
                        Environment.Exit(0);
                    }
                    else
                    {
                        UpdateProgress.Value = 100;

                        //Check to see if we need to download any new assets
                        JObject remoteManifest = ReadAssetsManifest();
                        foreach (JObject remoteArchive in remoteManifest["archives"])
                        {
                            bool upToDate = false;
                            foreach (JObject localArchive in localManifest["archives"])
                            {
                                if (localArchive["name"].Value<string>() == remoteArchive["name"].Value<string>())
                                {
                                    upToDate = (localArchive["size"].Value<int>() == remoteArchive["size"].Value<int>());
                                    break;
                                }
                            }
                            if (upToDate) continue;
                            string localPath = _assetPath + remoteArchive["name"] + ".archive";
                            Directory.CreateDirectory(localPath.Substring(0, localPath.Length - Path.GetFileName(localPath).Length));
                            _downloadData.Add(new DownloadData(_downloadURL + "Assets/" + remoteArchive["name"] + ".archive?v=" + _random.Next(5000), localPath));
                        }

                        //Obviously, we also need to download the OpenCAGE update!
                        _downloadData.Add(new DownloadData(_downloadURL + "OpenCAGE.exe?v=" + _random.Next(5000), "OpenCAGE.exe"));

                        //Start downloading
                        DownloadAsync();
                    }
                };
                client.DownloadFileAsync(new Uri(_downloadURL + "Assets/assets.manifest?v=" + _random.Next(5000)), _assetPath + "assets.manifest");
            }
            catch
            {
                MessageBox.Show("Update and configuration failed!\nPlease ensure you are connected to the internet.", "OpenCAGE Updater Failure!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private JObject ReadAssetsManifest()
        {
            string manifestContent = "";
            if (File.Exists(_assetPath + "assets.manifest")) manifestContent = File.ReadAllText(_assetPath + "assets.manifest");
            if (manifestContent == "") manifestContent = "{\"archives\":[]}";
            return JObject.Parse(manifestContent);
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
            WebClient client = new WebClient();
            client.DownloadProgressChanged += (s, progress) =>
            {
                UpdateProgress.Value = progress.ProgressPercentage;
                this.Refresh();
            };
            client.DownloadFileCompleted += (s, progress) =>
            {
                UpdateProgress.Value = 100;
                _downloadData.RemoveAt(0);

                if (_downloadData.Count == 0)
                {
                    //If any new updates downloaded, extract them
                    string[] archives = Directory.GetFiles(_assetPath, "*.archive", SearchOption.TopDirectoryOnly);
                    foreach (string archive in archives)
                    {
                        BinaryReader reader = new BinaryReader(File.OpenRead(archive));
                        int file_count = reader.ReadInt32();
                        for (int i = 0; i < file_count; i++)
                        {
                            string fileName = reader.ReadString();
                            int fileLength = reader.ReadInt32();
                            byte[] fileContent = reader.ReadBytes(fileLength);
                            
                            Directory.CreateDirectory((_assetPath + fileName).Substring(0, (_assetPath + fileName).Length - Path.GetFileName(_assetPath + fileName).Length));
                            if (File.Exists(_assetPath + fileName)) File.Delete(_assetPath + fileName);
                            File.WriteAllBytes(_assetPath + fileName, fileContent);
                        }
                        reader.Close();
                        File.Delete(archive);
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
            client.DownloadFileAsync(new Uri(_downloadData[0].URL), _downloadData[0].PATH);
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
