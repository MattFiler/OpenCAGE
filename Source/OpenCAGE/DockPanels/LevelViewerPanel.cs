using OpenCAGE;
using OpenCAGE.UnityConnection;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
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
            InitializeViewerToolbar();

            CloseButtonVisible = true;
            AllowEndUserDocking = false;
            FormClosing += LevelViewerPanel_FormClosing;
            embeddedWindowHost.EmbedFailed += EmbeddedWindowHost_EmbedFailed;
        }

        protected override string GetPersistString()
        {
            return "LevelViewerPanel";
        }

        public void Launch(bool focusAfterEmbed = true)
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
                    "Viewport",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            _launching = true;
            loadingLabel.Visible = true;
            loadingLabel.Text = "Initialising viewport...";

            try
            {
                if (!Send.Started && !Send.Start())
                {
                    MessageBox.Show(
                        "Failed to start the viewport connection.\nCould not bind a local websocket port.",
                        "Viewport",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                int port = Send.Port;

                // Start off-screen so the top-level window is not visible before embedding.
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    WorkingDirectory = editorPath,
                    Arguments = "--opencage-embedded --verbose --position -32000,-32000 --opencage-ws-port " + port,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    CreateNoWindow = true,
                };
                startInfo.EnvironmentVariables["OPENCAGE_EMBEDDED"] = "1";
                startInfo.EnvironmentVariables["OPENCAGE_WS_PORT"] = port.ToString();
                _process = Process.Start(startInfo);

                if (_process == null)
                    return;

                _process.EnableRaisingEvents = true;
                _process.Exited += Process_Exited;
                _process.OutputDataReceived += Process_OutputDataReceived;
                _process.ErrorDataReceived += Process_ErrorDataReceived;
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                if (!embeddedWindowHost.IsHandleCreated)
                    embeddedWindowHost.CreateControl();

                loadingLabel.Text = "Embedding viewport...";
                if (!embeddedWindowHost.TryEmbedProcess(_process))
                {
                    MessageBox.Show(
                        "The viewport started but could not be embedded into OpenCAGE.",
                        "Viewport",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    Stop();
                    return;
                }

                loadingLabel.Visible = false;
                Text = "Viewport";
                if (focusAfterEmbed)
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

            if (!IsCursorOverEmbeddedHost())
                return;

            embeddedWindowHost.FocusEmbeddedWindow();
        }

        private bool IsCursorOverEmbeddedHost()
        {
            if (!embeddedWindowHost.IsHandleCreated)
                return false;

            Point clientPoint = embeddedWindowHost.PointToClient(Cursor.Position);
            return embeddedWindowHost.ClientRectangle.Contains(clientPoint);
        }

        public bool IsCursorOverViewport() => IsCursorOverEmbeddedHost();

        public void UndockForLayoutReset()
        {
            try
            {
                if (DockHandler.DockPanel == null)
                    return;

                // Hide while still attached; calling Hide after clearing DockPanel
                // crashes in DockContentHandler.ContentFocusManager.
                Hide();
                DockHandler.DockPanel = null;
            }
            catch
            {
            }
        }

        public void RefreshEmbeddedBounds()
        {
            embeddedWindowHost.RefreshEmbeddedBounds();
        }

        public void Stop()
        {
            embeddedWindowHost.Detach();

            if (_process == null)
                return;

            _process.Exited -= Process_Exited;
            _process.OutputDataReceived -= Process_OutputDataReceived;
            _process.ErrorDataReceived -= Process_ErrorDataReceived;

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

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            RelayProcessLog(e?.Data, false);
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            RelayProcessLog(e?.Data, true);
        }

        private void RelayProcessLog(string line, bool isError)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => RelayProcessLog(line, isError)));
                return;
            }

            ViewerLogRelay.Write(line, isError);
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

            loadingLabel.Text = "Failed to embed viewport.";
        }

        private void LevelViewerPanel_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            Stop();
            Hide();
        }

        public static bool IsFeatureEnabled()
        {
            if (Singleton.DisableViewport)
                return false;
#if !SHIP_BUILD
            return true;
#else
            if (Singleton.IsSteamworks)
                return true;
            return SettingsManager.GetBool(Settings.LevelViewerEnabled);
#endif
        }

        public static bool IsInstalled()
        {
            return File.Exists(GetExecutablePath());
        }

        public static bool IsAvailable()
        {
            return IsFeatureEnabled() && IsInstalled();
        }

        public static string GetInstallDirectory()
        {
#if DEBUG
            return Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "Source", "Dependencies", "LevelViewer", "Build"));
#else
            return Path.Combine(Singleton.PathToAI, "DATA", "MODTOOLS", "REMOTE_ASSETS", "levelviewer");
#endif
        }

        public static string GetExecutablePath()
        {
            return Path.Combine(GetInstallDirectory(), "CathodeEditorGodot.exe");
        }
    }
}
