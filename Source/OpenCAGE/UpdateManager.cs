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
                //Get current Github version
                string branch_name = "master";
                if (SettingsManager.GetBool("CONFIG_UseStagingBranch")) branch_name = "staging";
                Stream webStream = _webClient.OpenRead("https://raw.githubusercontent.com/MattFiler/OpenCAGE/" + branch_name + "/Source/OpenCAGE/Properties/AssemblyInfo.cs?v=" + ProductVersion + "&r = " + _random.Next(5000).ToString());
                string[] LatestVersionArray = new StreamReader(webStream).ReadToEnd().Split(new[] { "AssemblyFileVersion(\"" }, StringSplitOptions.None);
                string LatestVersionNumber = LatestVersionArray[1].Substring(0, LatestVersionArray[1].Length - 4);

                //Check to see if update is required
                return (ProductVersion != LatestVersionNumber);
            }
            catch { }
            return false;
        }
    }
}
