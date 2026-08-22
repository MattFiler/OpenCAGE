using OpenCAGE;
using OpenCAGE.UnityConnection;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
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
            Theming.ThemeManager.ApplyToForm(this);
            InitializeViewerToolbar();

            CloseButton = false;
            CloseButtonVisible = false;
            AllowEndUserDocking = false;
            FormClosing += LevelViewerPanel_FormClosing;
            embeddedWindowHost.EmbedFailed += EmbeddedWindowHost_EmbedFailed;
        }

        public void Launch(bool focusAfterEmbed = true)
        {
            if (IsRunning || _launching)
                return;

            Stop();

            string executablePath = Singleton.ViewportExecutablePath;
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

                /* Started with its window suppressed by Windows itself, so it cannot appear on the
                 * desktop before we take it - see HiddenProcessLauncher. The previous attempt at this
                 * was to ask Godot for a position off screen, which doesn't work: Godot clamps it to
                 * the nearest monitor, so -32000,-32000 arrived at -8,-31 and sat in the top left
                 * corner, more visible rather than less. */
                string workingDirectory = executablePath.Substring(0, executablePath.Length - Path.GetFileName(executablePath).Length);
                Dictionary<string, string> environment = new Dictionary<string, string>
                {
                    { "OPENCAGE_EMBEDDED", "1" },
                    { "OPENCAGE_WS_PORT", port.ToString() },
                };

                _process = HiddenProcessLauncher.Start(
                    executablePath,
                    "--opencage-embedded --verbose --opencage-ws-port " + port,
                    workingDirectory,
                    environment,
                    RelayProcessLog);

                if (_process == null)
                {
                    MessageBox.Show(
                        "The viewport could not be started.\n" + executablePath,
                        "Viewport",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    loadingLabel.Visible = false;
                    return;
                }

                _process.EnableRaisingEvents = true;
                _process.Exited += Process_Exited;

                if (!embeddedWindowHost.IsHandleCreated)
                    embeddedWindowHost.CreateControl();

                loadingLabel.Text = "Embedding viewport...";
                ViewerEmbedCoordinator.BeginEmbedding();
                try
                {
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
                }
                finally
                {
                    ViewerEmbedCoordinator.EndEmbedding();
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
    }
}
