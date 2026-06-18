using Newtonsoft.Json.Linq;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace Updater
{
    public partial class Updater : Form
    {
        private Random _random = new Random();
        private string _assetPath = "/DATA/MODTOOLS/REMOTE_ASSETS/";
        private string _downloadURL = "http://opencage.mattfiler.co.uk/download/";
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

            //Set the branch to download from
            if (OpenCAGE.SettingsManager.GetString(Settings.RemoteBranch) == "")
            {
                if (OpenCAGE.SettingsManager.GetBool(Settings.UseStagingBranch))
                    OpenCAGE.SettingsManager.SetString(Settings.RemoteBranch, "staging");
                else
                    OpenCAGE.SettingsManager.SetString(Settings.RemoteBranch, "master");
            }
            _downloadURL += OpenCAGE.SettingsManager.GetString(Settings.RemoteBranch) + "/";

            //Make sure we write to the correct location
            _assetPath = OpenCAGE.SettingsManager.GetString(Settings.GameRoot) + _assetPath;
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

            //If level viewer is disabled, clear it out
            if (!OpenCAGE.SettingsManager.GetBool(Settings.LevelViewerEnabled))
            {
                string levelViewerPath = _assetPath + "\\levelviewer";
                if (Directory.Exists(levelViewerPath))
                {
                    try
                    {
                        Directory.Delete(levelViewerPath, true);
                    }
                    catch { }
                }
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
                            if (remoteArchive["name"].Value<string>() == "levelviewer")
                            {
                                //The level viewer is opt-in, if the user hasn't, skip it entirely
                                if (!SettingsManager.GetBool(Settings.LevelViewerEnabled))
                                    continue;

                                //If the user has opted in and we've never downloaded it, skip the hash check
                                if (!SettingsManager.GetBool(Settings.DownloadedLevelViewer))
                                {
                                    SettingsManager.SetBool(Settings.DownloadedLevelViewer, true);

                                    string localPath = _assetPath + remoteArchive["name"] + ".archive";
                                    Directory.CreateDirectory(localPath.Substring(0, localPath.Length - Path.GetFileName(localPath).Length));
                                    _downloadData.Add(new DownloadData(_downloadURL + "Assets/" + remoteArchive["name"] + ".archive?v=" + _random.Next(5000), localPath));
                                    _downloadsAvailable++;
                                    continue;
                                }
                            }

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

                            {
                                string localPath = _assetPath + remoteArchive["name"] + ".archive";
                                Directory.CreateDirectory(localPath.Substring(0, localPath.Length - Path.GetFileName(localPath).Length));
                                _downloadData.Add(new DownloadData(_downloadURL + "Assets/" + remoteArchive["name"] + ".archive?v=" + _random.Next(5000), localPath));
                                _downloadsAvailable++;
                            }
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
            allProcesses.AddRange(Process.GetProcessesByName("CathodeEditorGodot"));
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
                        using (MemoryStream stream = new MemoryStream())
                        using (GZipStream gzipStream = new GZipStream(File.OpenRead(archive), CompressionMode.Decompress))
                        {
                            gzipStream.CopyTo(stream);
                            byte[] content = stream.ToArray();
                            using (BinaryReader reader = new BinaryReader(new MemoryStream(content)))
                            {
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
                            }
                        }
                        File.Delete(archive);
                    }

                    //Decompress main app
                    byte[] opencageContent = File.ReadAllBytes("OpenCAGE.exe");
                    using (MemoryStream stream = new MemoryStream())
                    using (GZipStream gzipStream = new GZipStream(new MemoryStream(opencageContent), CompressionMode.Decompress))
                    {
                        gzipStream.CopyTo(stream);
                        opencageContent = stream.ToArray();
                    }
                    File.WriteAllBytes("OpenCAGE.exe", opencageContent);

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
