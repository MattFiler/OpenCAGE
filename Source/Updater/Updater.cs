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

namespace Updater
{
    public partial class Updater : Form
    {
        public Updater()
        {
            InitializeComponent();
        }

        private WebClient webClient = new WebClient();
        private void Form1_Load(object sender, EventArgs e)
        {
            if (File.Exists("Mod Tools.exe")) File.Delete("Mod Tools.exe");
            if (File.Exists("OpenCAGE.exe")) File.Delete("OpenCAGE.exe");
            this.TopMost = true;
            webClient.DownloadProgressChanged += (s, clientprogress) =>
            {
                UpdateProgress.Value = clientprogress.ProgressPercentage;
            };
            webClient.DownloadFileCompleted += (s, clientprogress) =>
            {
                UpdateProgress.Value = 100;
                Process.Start("OpenCAGE.exe");
                Application.Exit();
                Environment.Exit(0);
            };
            webClient.DownloadFileAsync(new Uri("https://raw.githubusercontent.com/MattFiler/OpenCAGE/master/OpenCAGE.exe"), "OpenCAGE.exe");
        }
    }
}
