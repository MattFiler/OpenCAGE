using CommandsEditor;
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
            File.WriteAllBytes("OpenCAGE Updater.exe", CommandsEditor.Properties.Resources.OpenCAGE_Updater);
            Process.Start("OpenCAGE Updater.exe");
            Application.Exit();
            Environment.Exit(0);
        }
#endif
    }
}
