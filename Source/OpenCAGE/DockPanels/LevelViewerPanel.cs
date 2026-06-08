using OpenCAGE;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.DockPanels
{
    public partial class LevelViewerPanel : DockContent
    {
        private Process _process;
        private bool _launching;

        public event EventHandler ProcessExited;

        public bool IsRunning
        {
            get
            {
                if (_process == null)
                    return false;

                try
                {
                    return !_process.HasExited;
                }
                catch
                {
                    return false;
                }
            }
        }

        public LevelViewerPanel()
        {
            InitializeComponent();

            CloseButtonVisible = true;
            AllowEndUserDocking = false;
            FormClosing += LevelViewerPanel_FormClosing;
            embeddedWindowHost.EmbedFailed += EmbeddedWindowHost_EmbedFailed;
        }

        protected override string GetPersistString()
        {
            return "LevelViewerPanel";
        }

        public void Launch()
        {
            if (IsRunning || _launching)
                return;

            Stop();

            string editorPath = GetInstallDirectory();
            string executablePath = GetExecutablePath();
            if (!File.Exists(executablePath))
            {
                MessageBox.Show(
                    "Could not find CathodeEditorGodot.exe.\nExpected path:\n" + executablePath,
                    "Level Viewer",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            _launching = true;
            loadingLabel.Visible = true;
            loadingLabel.Text = "Starting Level Viewer...";

            try
            {
                // Start off-screen so the top-level window is not visible before embedding.
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    WorkingDirectory = editorPath,
                    Arguments = "--opencage-embedded --position -32000,-32000",
                    UseShellExecute = false,
                };
                startInfo.EnvironmentVariables["OPENCAGE_EMBEDDED"] = "1";
                _process = Process.Start(startInfo);

                if (_process == null)
                    return;

                _process.EnableRaisingEvents = true;
                _process.Exited += Process_Exited;

                if (!embeddedWindowHost.IsHandleCreated)
                    embeddedWindowHost.CreateControl();

                loadingLabel.Text = "Embedding Level Viewer...";
                if (!embeddedWindowHost.TryEmbedProcess(_process))
                {
                    MessageBox.Show(
                        "The Level Viewer started but could not be embedded into OpenCAGE.",
                        "Level Viewer",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    Stop();
                    return;
                }

                loadingLabel.Visible = false;
                Text = "Level Viewer";
                embeddedWindowHost.FocusEmbeddedWindow();
            }
            finally
            {
                _launching = false;
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            RestoreInputFocus();
        }

        public void RestoreInputFocus()
        {
            if (!IsRunning || NativeMouseInput.IsAnyMouseButtonPressed)
                return;

            embeddedWindowHost.FocusEmbeddedWindow();
        }

        public void Stop()
        {
            embeddedWindowHost.Detach();

            if (_process == null)
                return;

            _process.Exited -= Process_Exited;

            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(2000);
                }
            }
            catch
            {
            }

            try
            {
                _process.Dispose();
            }
            catch
            {
            }

            _process = null;
            loadingLabel.Visible = false;
            ProcessExited?.Invoke(this, EventArgs.Empty);
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => Process_Exited(sender, e)));
                return;
            }

            embeddedWindowHost.Detach();
            _process = null;
            loadingLabel.Visible = false;
            Hide();
            ProcessExited?.Invoke(this, EventArgs.Empty);
        }

        private void EmbeddedWindowHost_EmbedFailed(object sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => EmbeddedWindowHost_EmbedFailed(sender, e)));
                return;
            }

            loadingLabel.Text = "Failed to embed Level Viewer.";
        }

        private void LevelViewerPanel_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            Stop();
            Hide();
        }

        private static string GetInstallDirectory()
        {
#if DEBUG
            return Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "Source", "Dependencies", "LevelViewer", "Build"));
#else
            return Path.Combine(Singleton.PathToAI, "DATA", "MODTOOLS", "REMOTE_ASSETS", "levelviewer");
#endif
        }

        private static string GetExecutablePath()
        {
            return Path.Combine(GetInstallDirectory(), "CathodeEditorGodot.exe");
        }
    }
}
