using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class GitHubPrompt : Form
    {
        public GitHubPrompt()
        {
            InitializeComponent();

            OpenGitHub.Parent = LandingBackground;

            this.BringToFront();
            this.Focus();
        }

        private void OpenGitHub_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/MattFiler/OpenCAGE");
        }
    }
}
