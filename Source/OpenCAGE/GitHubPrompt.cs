using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class GitHubPrompt : Form
    {
        private Timer timerToEnableClick = new Timer();

        public GitHubPrompt()
        {
            InitializeComponent();

            OpenGitHub.Parent = LandingBackground;
            OpenGitHub.Enabled = false;

            timerToEnableClick.Interval = 250;
            timerToEnableClick.Tick += new EventHandler(EnableClick);
            timerToEnableClick.Start();

            this.BringToFront();
            this.Focus();
        }

        private void OpenGitHub_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/MattFiler/OpenCAGE");
            this.Close();
        }

        private void EnableClick(object sender, EventArgs e)
        {
            timerToEnableClick.Tick -= new EventHandler(EnableClick);
            timerToEnableClick.Dispose();
            timerToEnableClick = null;

            OpenGitHub.Enabled = true;
        }
    }
}
