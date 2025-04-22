using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace Updater
{
    public partial class Updater : Form
    {
        private Random _random = new Random();
        private string _assetPath = "/DATA/MODTOOLS/REMOTE_ASSETS/";
        private string _downloadURL = "https://raw.githubusercontent.com/MattFiler/OpenCAGE/";
        private List<DownloadData> _downloadData = new List<DownloadData>();
        
        private int _downloadsAvailable = 0;
        private int _downloadsCompleted = 0;

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

            //Make sure we're using the correct config file if it's remotely managed
            if (OpenCAGE.SettingsManager.GetBool("PATH_IsRemote"))
            {
                OpenCAGE.SettingsManager.FlipToRemotePath();
            }

            //Set the branch to download from
            if (OpenCAGE.SettingsManager.GetString("CONFIG_RemoteBranch") == "")
            {
                if (OpenCAGE.SettingsManager.GetBool("CONFIG_UseStagingBranch"))
                    OpenCAGE.SettingsManager.SetString("CONFIG_RemoteBranch", "staging");
                else
                    OpenCAGE.SettingsManager.SetString("CONFIG_RemoteBranch", "master");
            }
            _downloadURL += OpenCAGE.SettingsManager.GetString("CONFIG_RemoteBranch") + "/";

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
            CloseProcesses();

            //Remove the old OpenCAGE version
            bool didRemove = false;
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    if (File.Exists("Mod Tools.exe")) File.Delete("Mod Tools.exe");
                    if (File.Exists("OpenCAGE.exe")) File.Delete("OpenCAGE.exe");
                    didRemove = true;
                    break;
                }
                catch
                {
                    CloseProcesses();
                    Thread.Sleep(1500);
                    CloseProcesses();
                }
            }
            if (!didRemove)
            {
                ErrorMessageAndQuit("Please close OpenCAGE and re-run the OpenCAGE Updater.", false); //Shouldn't hit this, unless we have a permissions issue.
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
                        ErrorMessageAndQuit("Encountered an error while downloading update manifest!\n" + progress.Error.Message, true);
                        return;
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
                                if (localArchive["name"].Value<string>() != remoteArchive["name"].Value<string>()) continue;
                                
                                if (localArchive.ContainsKey("hash") && remoteArchive.ContainsKey("hash"))
                                    upToDate = (localArchive["hash"].Value<string>() == remoteArchive["hash"].Value<string>());
                                else
                                    upToDate = (localArchive["size"].Value<int>() == remoteArchive["size"].Value<int>());
                                break;
                            }
                            if (upToDate) continue;
                            
                            string localPath = _assetPath + remoteArchive["name"] + ".archive";
                            Directory.CreateDirectory(localPath.Substring(0, localPath.Length - Path.GetFileName(localPath).Length));
                            _downloadData.Add(new DownloadData(_downloadURL + "Assets/" + remoteArchive["name"] + ".archive?v=" + _random.Next(5000), localPath));
                            _downloadsAvailable++;
                        }

                        //Obviously, we also need to download the OpenCAGE update!
                        _downloadData.Add(new DownloadData(_downloadURL + "OpenCAGE.exe?v=" + _random.Next(5000), "OpenCAGE.exe"));
                        _downloadsAvailable++;

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
        private void CloseProcesses()
        {
            List<Process> allProcesses = new List<Process>(Process.GetProcessesByName("OpenCAGE"));
            allProcesses.AddRange(Process.GetProcessesByName("Unity"));
            List<string> processNames = new List<string>(Directory.GetFiles(_assetPath, "*.exe", SearchOption.AllDirectories));
            for (int i = 0; i < processNames.Count; i++) allProcesses.AddRange(Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processNames[i])));
            for (int i = 0; i < allProcesses.Count; i++) try { allProcesses[i].Kill(); } catch { }
            Thread.Sleep(500);
        }

        /* Show error msg */
        private void ErrorMessageAndQuit(string message, bool resetManifest)
        {
            try { File.Delete(_assetPath + "assets.manifest"); } catch { }
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
                UpdateProgress.Value = ((_downloadsCompleted * 100) / _downloadsAvailable) + (progress.ProgressPercentage / _downloadsAvailable);
                this.Refresh();
            };
            client.DownloadFileCompleted += (s, progress) =>
            {
                if (progress.Error != null)
                {
                    ErrorMessageAndQuit("Encountered an error while downloading!\n" + progress.Error.Message, true);
                    return;
                }

                _downloadsCompleted++;
                UpdateProgress.Value = ((_downloadsCompleted * 100) / _downloadsAvailable);
                _downloadData.RemoveAt(0);

                if (_downloadData.Count == 0)
                {
                    //If any new updates downloaded, extract them
                    string[] archives = Directory.GetFiles(_assetPath, "*.archive", SearchOption.TopDirectoryOnly);
                    foreach (string archive in archives)
                    {
                        //Try delete the base directory to clear out old assets (if it exists)
                        string directory = _assetPath + "/" + Path.GetFileNameWithoutExtension(archive);
                        try { Directory.Delete(directory, true); } catch { }
                        try { Directory.Delete(directory, true); } catch { }

                        //Extract out the new assets
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
