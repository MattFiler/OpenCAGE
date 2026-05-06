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
        static private WebClient _webClient = new WebClient();

        static public bool IsUpdateAvailable(string ProductVersion)
        {
            if (SettingsManager.GetBool(Singleton.Settings.SkipUpdate)) return false;
            try
            {
                if (SettingsManager.GetString(Singleton.Settings.RemoteBranch) == "")
                {
                    if (SettingsManager.GetBool(Singleton.Settings.UseStagingBranch))
                        SettingsManager.SetString(Singleton.Settings.RemoteBranch, "staging");
                    else
                        SettingsManager.SetString(Singleton.Settings.RemoteBranch, "master");
                }

                //Get current Github version
                Stream webStream = _webClient.OpenRead("https://raw.githubusercontent.com/MattFiler/OpenCAGE/" + SettingsManager.GetString(Singleton.Settings.RemoteBranch) + "/Source/OpenCAGE/Properties/AssemblyInfo.cs?v=" + ProductVersion + "&r=" + _random.Next(5000).ToString());
                string[] LatestVersionArray = new StreamReader(webStream).ReadToEnd().Split(new[] { "AssemblyFileVersion(\"" }, StringSplitOptions.None);
                string LatestVersionNumber = LatestVersionArray[1].Substring(0, LatestVersionArray[1].Length - 4);

                //Check to see if update is required
                return (ProductVersion != LatestVersionNumber);
            }
            catch (Exception e)
            {
                Console.WriteLine("UpdateManager::IsUpdateAvailable: Checking for update FAILED!\n" + e.ToString());
            }
            return false;
        }

        static public void DoUpdate()
        {
            if (SettingsManager.GetString(Singleton.Settings.RemoteBranch) == "")
            {
                if (SettingsManager.GetBool(Singleton.Settings.UseStagingBranch))
                    SettingsManager.SetString(Singleton.Settings.RemoteBranch, "staging");
                else
                    SettingsManager.SetString(Singleton.Settings.RemoteBranch, "master");
            }

            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
                    | SecurityProtocolType.Tls11
                    | SecurityProtocolType.Tls12
                    | SecurityProtocolType.Ssl3;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            try
            {
                if (File.Exists("OpenCAGE Updater.exe"))
                    File.Delete("OpenCAGE Updater.exe");
            }
            catch { }

            try
            {
                WebClient client = new WebClient();
                client.DownloadFileCompleted += (s, progress) =>
                {
                    if (progress.Error == null)
                    {
                        Process.Start("OpenCAGE Updater.exe");
                        Application.Exit();
                        Environment.Exit(0);
                    }
                    else
                    {
                        MessageBox.Show("Update failed!\n" + progress.Error.Message, "Update failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };
                client.DownloadFileAsync(new Uri("http://opencage.mattfiler.co.uk/download/" + SettingsManager.GetString(Singleton.Settings.RemoteBranch) + "/OpenCAGE Updater.exe?v=" + _random.Next(5000)), "OpenCAGE Updater.exe");
            }
            catch (Exception e)
            {
                MessageBox.Show("Update failed!\n" + e.Message, "Update failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
#endif
    }
}
