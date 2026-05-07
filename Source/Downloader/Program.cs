using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace Downloader
{
    internal class Program
    {
        static private Random _random = new Random();

        [STAThread]
        static void Main(string[] args)
        {
            CultureInfo newCulture = CultureInfo.CreateSpecificCulture("en-GB");
            Thread.CurrentThread.CurrentUICulture = newCulture;
            Thread.CurrentThread.CurrentCulture = newCulture;

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

            if (!File.Exists(SettingsManager.GetString("PATH_GameRoot") + "/AI.exe"))
            {
                if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "/../Alien Isolation/AI.exe"))
                {
                    SettingsManager.SetString("PATH_GameRoot", Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory + "/../Alien Isolation/"));
                }
                else if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + "/AI.exe"))
                {
                    SettingsManager.SetString("PATH_GameRoot", Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory));
                }
                else
                {
                    MessageBox.Show("Please locate your Alien: Isolation executable (AI.exe).", "OpenCAGE Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    using (OpenFileDialog dialog = new OpenFileDialog())
                    {
                        dialog.Filter = "Applications (*.exe)|AI.exe";
                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            SettingsManager.SetString("PATH_GameRoot", Path.GetDirectoryName(dialog.FileName));
                        }
                        else
                        {
                            Application.Exit();
                            Environment.Exit(0);
                            return;
                        }
                    }
                }
            }

            try
            {
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
