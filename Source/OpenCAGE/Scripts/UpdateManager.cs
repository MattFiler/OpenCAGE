using Newtonsoft.Json.Linq;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    static class UpdateManager
    {
#if SHIP_BUILD
        static private Random _random = new Random();
        static private string _url;

        static UpdateManager()
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
                    | SecurityProtocolType.Tls11
                    | SecurityProtocolType.Tls12
                    | SecurityProtocolType.Ssl3;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            if (SettingsManager.GetString(Singleton.Settings.RemoteBranch) == "")
            {
                if (SettingsManager.GetBool(Singleton.Settings.UseStagingBranch))
                    SettingsManager.SetString(Singleton.Settings.RemoteBranch, "staging");
                else
                    SettingsManager.SetString(Singleton.Settings.RemoteBranch, "master");
            }

            _url = "http://opencage.mattfiler.co.uk/download/" + SettingsManager.GetString(Singleton.Settings.RemoteBranch) + "/";
        }

        static public bool IsUpdateAvailable(string ProductVersion)
        {
            try
            {
                if (!File.Exists(Singleton.PathToAI + "/DATA/MODTOOLS/REMOTE_ASSETS/assets.manifest"))
                    return true;
                JObject offlineManifest = JObject.Parse(File.ReadAllText(Singleton.PathToAI + "/DATA/MODTOOLS/REMOTE_ASSETS/assets.manifest"));
                foreach (JObject offlineArchive in offlineManifest["archives"])
                {
                    if (offlineArchive["name"].Value<string>() == "levelviewer" && !SettingsManager.GetBool(Singleton.Settings.LevelViewerEnabled))
                        continue;

                    if (!Directory.Exists(Singleton.PathToAI + "/DATA/MODTOOLS/REMOTE_ASSETS/" + offlineArchive["name"]))
                    {
                        File.Delete(Singleton.PathToAI + "/DATA/MODTOOLS/REMOTE_ASSETS/assets.manifest");
                        return true;
                    }
                }

            }
            catch { }

            try
            {
                string remoteVersion = "";
                byte[] content = (new WebClient()).DownloadData(_url + "version.bin?v=" + _random.Next(5000));
                using (BinaryReader reader = new BinaryReader(new MemoryStream(content)))
                {
                    int l = reader.ReadByte();
                    for (int i = 0; i < l; i++)
                    {
                        remoteVersion += (reader.ReadInt16()).ToString();
                        if (i < l - 1) remoteVersion += ".";
                    }
                }
                return (ProductVersion != remoteVersion);
            }
            catch (Exception e)
            {
                Console.WriteLine("UpdateManager::IsUpdateAvailable: Checking for update FAILED!\n" + e.ToString());
            }
            return false;
        }

        static public void DoUpdate()
        {
            try
            {
                byte[] content = (new WebClient()).DownloadData(_url + "OpenCAGE Updater.exe?v=" + _random.Next(5000));
                File.WriteAllBytes("OpenCAGE Updater.exe", content);
                Process.Start("OpenCAGE Updater.exe");
            }
            catch (Exception e)
            {
                MessageBox.Show("Update failed!\n" + e.Message, "Update failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
#endif
    }
}
