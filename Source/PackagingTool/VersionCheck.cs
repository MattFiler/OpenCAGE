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
        Random random = new Random();

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
                //Stream webStream = webClient.OpenRead("https://raw.githubusercontent.com/MattFiler/LegendPlugin/master/Source/version.txt?random=" + random.Next(5000).ToString());
                Stream webStream = webClient.OpenRead("https://raw.githubusercontent.com/MattFiler/LegendPlugin/master/Source/PackagingTool/Properties/AssemblyInfo.cs?v=" + ProductVersion + "&r = " + random.Next(5000).ToString());
                StreamReader webRead = new StreamReader(webStream);
                string[] LatestVersionArray = webRead.ReadToEnd().Split(new[] { "AssemblyFileVersion(\"" }, StringSplitOptions.None);
                string LatestVersionNumber = LatestVersionArray[1].Substring(0, LatestVersionArray[1].Length - 4);

                //Check against current version
                if (ProductVersion != LatestVersionNumber)
                {
                    //Out of date
                    if (AlienDirectories.GameDirectoryRoot() == "")
                    {
                        throw new System.InvalidOperationException("The path is not of a legal form.");
                    }
                    string NewEXE = AlienDirectories.GameDirectoryRoot() + "/Mod Tools V" + LatestVersionNumber + ".exe";
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
                        MessageBox.Show("A new version of the Alien: Isolation Mod Tools is available." + Environment.NewLine + "The latest version will be downloaded to your game directory.", "Alien: Isolation Mod Tools Updater", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
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
                    Landing_Main MainLander = new Landing_Main();
                    MainLander.Show();
                    this.Close();
                }
            }
            catch (Exception errormessage)
            {
                if (errormessage.Message.ToString() != "The path is not of a legal form.")
                {
                    //Probably no internet connection
                    MessageBox.Show("Failed to check for updates." + Environment.NewLine + "Continuing with version " + ProductVersion, "Alien: Isolation Mod Tools Updater", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    //File error - means we're loading for the first time ... but have to have a messagebox here else we'll crash
                    if (File.Exists("modtools_locales.ayz"))
                    {
                        MessageBox.Show("Alien: Isolation Mod Tools running version " + ProductVersion, "Alien: Isolation Mod Tools Updater", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Welcome to the Alien: Isolation Mod Tools!", "Mod Tools Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                Landing_Main MainLander = new Landing_Main();
                MainLander.Show();
                this.Close(); 
            }
        }

        //Remove old mod tool exes from game directory
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
