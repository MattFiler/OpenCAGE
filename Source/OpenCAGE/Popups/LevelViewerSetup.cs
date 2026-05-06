using Microsoft.Win32;
using Newtonsoft.Json.Linq;
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

namespace CommandsEditor
{
    public partial class LevelViewerSetup: Form
    {
        public static Process UnityProcess;

        public static bool Installed
        {
            get
            {
                string editorPath = InstallationPath;
                return editorPath != "" && File.Exists(editorPath);
            }
        }

        public static string InstallationPath
        {
            get
            {
                using (RegistryKey regKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Unity Technologies\\Installer\\Unity 2022.3.9f1"))
                {
                    if (regKey != null)
                    {
                        var location = regKey.GetValue("Location x64") as string;
                        if (!string.IsNullOrEmpty(location))
                            return location + "\\Editor\\Unity.exe";
                    }
                }

                return null;
            }
        }

        public LevelViewerSetup()
        {
            InitializeComponent();
        }

        private void LevelViewerSetup_Load(object sender, EventArgs e)
        {
            if (Installed)
                ShowSuccess();

            string installerPath = Path.Combine(Path.GetTempPath(), "UnitySetup.exe");
            if (File.Exists(installerPath))
                File.Delete(installerPath);

            Cursor.Current = Cursors.WaitCursor;

            label1.Text = "Downloading Unity...";
            label1.Refresh();

            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
                    | SecurityProtocolType.Tls11
                    | SecurityProtocolType.Tls12
                    | SecurityProtocolType.Ssl3;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            try
            {
                WebClient client = new WebClient();
                client.DownloadProgressChanged += (s, progress) =>
                {
                    progressBar1.Value = progress.ProgressPercentage - 20 < 0 ? 0 : progress.ProgressPercentage - 20;
                    progressBar1.Refresh();
                };
                client.DownloadFileCompleted += (s, progress) =>
                {
                    if (progress.Error != null)
                    {
                        MessageBox.Show("Encountered an error while downloading Unity!\n" + progress.Error.Message, "Unity Download Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        ShowFail();
                    }
                    else
                    {
                        label1.Text = "Installing Unity...";
                        label1.Refresh();

                        progressBar1.Value = 80;
                        progressBar1.Refresh();

                        var process = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = installerPath,
                                //Arguments = "/S",
                                //UseShellExecute = true,
                                Verb = "runas"
                            }
                        };
                        process.Start();
                        process.WaitForExit();

                        progressBar1.Value = 100;
                        progressBar1.Refresh();

                        if (Installed)
                        {
                            label1.Text = "Finished!";
                            label1.Refresh();
                            ShowSuccess();
                        }
                        else
                        {
                            label1.Text = "Installer failed!";
                            label1.Refresh();
                            ShowFail();
                        }
                    }
                };
                client.DownloadFileAsync(new Uri("https://download.unity3d.com/download_unity/ea401c316338/Windows64EditorInstaller/UnitySetup64-2022.3.9f1.exe"), installerPath);
            }
            catch
            {
                MessageBox.Show("Unity download failed!\nPlease ensure you are connected to the internet.", "Unity Download Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ShowFail();
            }
        }

        private void ShowSuccess()
        {
            Cursor.Current = Cursors.Default;
            MessageBox.Show("Successfully set-up the Level Viewer!", "Setup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.Close();
        }
        private void ShowFail()
        {
            Cursor.Current = Cursors.Default;
            MessageBox.Show("Level Viewer setup failed!\nPlease try again.", "Setup Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            this.Close();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int CP_NOCLOSE_BUTTON = 0x200;
                var cp = base.CreateParams;
                cp.ClassStyle |= CP_NOCLOSE_BUTTON;
                return cp;
            }
        }
    }
}
