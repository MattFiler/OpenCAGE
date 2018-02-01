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

namespace Alien_Isolation_Mod_Tools
{
    public partial class VersionCheck : Form
    {
        Directories AlienDirectories = new Directories();

        public VersionCheck()
        {
            InitializeComponent();
        }

        private void VersionCheck_Load(object sender, EventArgs e)
        {
            try
            {
                //Get current Github version
                WebClient webClient = new WebClient();
                Stream webStream = webClient.OpenRead("https://raw.githubusercontent.com/MattFiler/LegendPlugin/master/Source/version.txt");
                StreamReader webRead = new StreamReader(webStream);
                string LatestVersion = webRead.ReadToEnd();

                //Check against current version
                if (ProductVersion != LatestVersion)
                {
                    //Out of date
                    string NewEXE = AlienDirectories.GameDirectoryRoot() + "/Mod Tools V" + LatestVersion + ".exe";
                    if (File.Exists(NewEXE))
                    { 
                        //Start new exe
                        Process.Start(NewEXE);

                        //Close app
                        Application.Exit();
                        Environment.Exit(0);
                    }
                    else
                    {
                        //New update needs to be downloaded
                        MessageBox.Show("A new version of the Alien: Isolation Mod Tools is available." + Environment.NewLine + "The latest version will be downloaded to your game directory.", "Update required.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        this.TopMost = true;
                        webClient.DownloadProgressChanged += (s, clientprogress) =>
                        {
                            UpdateProgress.Value = clientprogress.ProgressPercentage;
                        };
                        webClient.DownloadFileCompleted += (s, clientprogress) =>
                        {
                            //Finish progress bar
                            UpdateProgress.Value = 100;

                            //Start new exe
                            Process.Start(NewEXE);

                            //Close app
                            Application.Exit();
                            Environment.Exit(0);
                        };
                        webClient.DownloadFileAsync(new Uri("https://raw.githubusercontent.com/MattFiler/LegendPlugin/master/Mod%20Tools/Mod%20Tools.exe"), NewEXE);
                    }
                }
                else
                {
                    //Clean up from previous updates
                    DeleteOldEXE();

                    //Close version checker
                    this.Close();
                }
            }
            catch 
            {
                //Probably no internet connection
                MessageBox.Show("Failed to check for updates.","Update message.", MessageBoxButtons.OK, MessageBoxIcon.Error); //Should fix crash
                this.Close(); 
            }
        }

        private void DeleteOldEXE()
        {
            foreach (string file in Directory.GetFiles(AlienDirectories.GameDirectoryRoot()))
            { 
                if ((Path.GetFileName(file).Count() > 9) && (Path.GetFileName(file).Substring(0, 9) == "Mod Tools"))
                {
                    if (System.AppDomain.CurrentDomain.FriendlyName != Path.GetFileName(file))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
        }
    }
}
