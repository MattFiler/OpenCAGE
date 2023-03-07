using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAGE
{
    static class UpdateManager
    {
        static private Random _random = new Random();
        static private WebClient _webClient = new WebClient();

        /* Check against GitHub to see if an update is available */
        static public bool IsUpdateAvailable(string ProductVersion)
        {
            if (SettingsManager.GetBool("CONFIG_SkipUpdateCheck")) return false;
            try
            {
                if (SettingsManager.GetString("CONFIG_RemoteBranch") == "")
                {
                    if (SettingsManager.GetBool("CONFIG_UseStagingBranch"))
                        SettingsManager.SetString("CONFIG_RemoteBranch", "staging");
                    else
                        SettingsManager.SetString("CONFIG_RemoteBranch", "master");
                }

                //Get current Github version
                Stream webStream = _webClient.OpenRead("https://raw.githubusercontent.com/MattFiler/OpenCAGE/" + SettingsManager.GetString("CONFIG_RemoteBranch") + "/Source/OpenCAGE/Properties/AssemblyInfo.cs?v=" + ProductVersion + "&r=" + _random.Next(5000).ToString());
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
    }
}
