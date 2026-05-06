using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace Downloader
{
    internal class Program
    {
        static private Random _random = new Random();

        static void Main(string[] args)
        {
            if (SettingsManager.GetString("CONFIG_RemoteBranch") == "")
            {
                if (SettingsManager.GetBool("CONFIG_UseStagingBranch"))
                    SettingsManager.SetString("CONFIG_RemoteBranch", "staging");
                else
                    SettingsManager.SetString("CONFIG_RemoteBranch", "master");
            }

            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
                    | SecurityProtocolType.Tls11
                    | SecurityProtocolType.Tls12
                    | SecurityProtocolType.Ssl3;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            try
            {
                string url = "http://opencage.mattfiler.co.uk/download/" + SettingsManager.GetString("CONFIG_RemoteBranch") + "/OpenCAGE Updater.exe?v=" + _random.Next(5000);
                byte[] content = (new WebClient()).DownloadData("http://opencage.mattfiler.co.uk/download/" + SettingsManager.GetString("CONFIG_RemoteBranch") + "/OpenCAGE Updater.exe?v=" + _random.Next(5000));
                File.WriteAllBytes("OpenCAGE Updater.exe", content);
                Process.Start("OpenCAGE Updater.exe");
            }
            catch (Exception e)
            {
                MessageBox.Show("Update failed!\n" + e.Message, "Update failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
