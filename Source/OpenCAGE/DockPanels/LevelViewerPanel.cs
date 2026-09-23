using OpenCAGE;
using OpenCAGE.UnityConnection;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.DockPanels
{
    public partial class LevelViewerPanel : DockContent
    {
        private Process _process;
        private bool _launching;

        /* The viewer's stdout/stderr only ever reached Debug.Log, which compiles out of release builds, so
         * when the process died there was nothing to show for it - not even the exit code. Keep the tail
         * of its output so an unexpected exit can be reported with the engine's own error text. */
        private const int ViewerOutputTailLines = 120;
        private readonly Queue<string> _viewerOutputTail = new Queue<string>();

        /* A viewer whose .NET runtime files are missing says so in its output and then faults a few
         * dozen lines later (see ViewerStartupFailure). Latched off the relayed lines as they arrive,
         * on the pipe's own thread, rather than read back off the tail when the exit is handled: how
         * many lines the engine prints in between is its business. Reported once per session, since
         * every level load relaunches the viewer and each attempt fails the same way. */
        private volatile bool _dotNetRuntimeMissing;
        private static bool _dotNetRuntimeMissingReported;

        public event EventHandler ProcessExited;

        /// <summary>The viewer window is currently parented inside this panel. False once the host handle was
        /// destroyed (a dock/layout change): the process is alive but nothing is on screen, and the next
        /// level load relaunches it rather than re-embedding into a session that may have diverged.</summary>
        public bool IsEmbedded => embeddedWindowHost.IsEmbedded;

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
            _dotNetRuntimeMissing = false;

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
                    /* Keep implicit Vulkan layers out of the viewer. When OpenCAGE is launched through Steam the
                     * child inherits Steam's launch environment, and the Steam overlay layer (plus OBS's capture
                     * hook, and whatever else is registered) then injects into a Godot process it was never
                     * meant for - measured: SteamOverlayVulkanLayer64.dll and graphics-hook64.dll load without
                     * this, neither loads with it. Third-party code inside the renderer is a silent-crash class
                     * we cannot log our way out of. The second variable is the Steam layer's own switch, for
                     * loaders older than 1.3.234 that don't understand the first. */
                    { "VK_LOADER_LAYERS_DISABLE", "~implicit~" },
                    { "DISABLE_VK_LAYER_VALVE_steam_overlay_1", "1" },
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

        /// <summary>
        /// Where a screen point lands in the viewport, as a 0-1 fraction of its size, or false if it
        /// isn't over a viewport that's running and on screen. A fraction rather than pixels because
        /// the viewer window is another process and doesn't have to share our DPI scaling.
        /// </summary>
        public bool TryGetViewportFraction(Point screenPoint, out float x, out float y)
        {
            x = 0f;
            y = 0f;

            if (!IsRunning || !IsEmbedded || !Visible || !embeddedWindowHost.IsHandleCreated)
                return false;

            Rectangle bounds = embeddedWindowHost.ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return false;

            Point clientPoint = embeddedWindowHost.PointToClient(screenPoint);
            if (!bounds.Contains(clientPoint))
                return false;

            x = clientPoint.X / (float)bounds.Width;
            y = clientPoint.Y / (float)bounds.Height;
            return true;
        }

        /// <summary>
        /// The screen point a 0-1 fraction of the viewport lands on - the inverse of
        /// <see cref="TryGetViewportFraction"/>, for a position the viewer reports in its own terms (a right
        /// click wanting its context menu). False if the viewport isn't running and on screen.
        /// </summary>
        public bool TryGetViewportScreenPoint(float x, float y, out Point screenPoint)
        {
            screenPoint = Point.Empty;

            if (!IsRunning || !IsEmbedded || !Visible || !embeddedWindowHost.IsHandleCreated)
                return false;

            Rectangle bounds = embeddedWindowHost.ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return false;

            Point clientPoint = new Point(
                (int)Math.Round(Math.Max(0f, Math.Min(1f, x)) * bounds.Width),
                (int)Math.Round(Math.Max(0f, Math.Min(1f, y)) * bounds.Height));
            screenPoint = embeddedWindowHost.PointToScreen(clientPoint);
            return true;
        }

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

            if (!_dotNetRuntimeMissing && ViewerStartupFailure.IsMissingDotNetRuntime(line))
                _dotNetRuntimeMissing = true;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => RelayProcessLog(line, isError)));
                return;
            }

            lock (_viewerOutputTail)
            {
                _viewerOutputTail.Enqueue((isError ? "ERR " : "    ") + line);
                while (_viewerOutputTail.Count > ViewerOutputTailLines)
                    _viewerOutputTail.Dequeue();
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

            int? exitCode = null;
            try
            {
                if (_process != null)
                    exitCode = _process.ExitCode;
            }
            catch
            {
            }

            embeddedWindowHost.Detach();
            _process = null;
            loadingLabel.Visible = false;
            Hide();
            ProcessExited?.Invoke(this, EventArgs.Empty);

            //Zero is the viewer choosing to close (the editor went away); anything else died on us. Report it
            //the same way an OpenCAGE crash is reported, as its own entry, so it shows up in the crash stats.
            if (exitCode.HasValue && exitCode.Value != 0)
            {
                //A viewer that could not bring up its runtime is an installation problem, not a crash: told
                //to the user, and not filed as a report. The fault it dies on would count as one otherwise.
                if (_dotNetRuntimeMissing)
                {
                    ReportMissingDotNetRuntime();
                    return;
                }

                /* Not straight away: the pipe readers hand the last lines over by BeginInvoke, as this
                   was, and the exit can overtake them - a report taken now can miss the very line that
                   says what happened. Windows also takes a moment to record the fault. */
                int code = exitCode.Value;
                DateTime died = DateTime.Now;
                Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    if (_dotNetRuntimeMissing)
                    {
                        ReportMissingDotNetRuntime();
                        return;
                    }
                    string tail;
                    lock (_viewerOutputTail)
                        tail = string.Join("\n", _viewerOutputTail);
                    Program.ReportViewportCrash(code, tail, died);
                });
            }
        }

        private void ReportMissingDotNetRuntime()
        {
            /* The 3 s recheck arrives on a pool thread, and a panel that went with the level in the meantime
               has no handle to marshal by (InvokeRequired is false without one). The dialog is not raised
               from the pool: the main window's thread outlives the level, and if that is gone too the
               message waits for the next launch, which fails the same way. */
            Control marshaller = IsHandleCreated ? (Control)this : Singleton.Editor;
            if (marshaller == null || marshaller.IsDisposed || !marshaller.IsHandleCreated)
                return;
            if (marshaller.InvokeRequired)
            {
                try
                {
                    marshaller.BeginInvoke(new Action(ReportMissingDotNetRuntime));
                }
                catch
                {
                    //the handle went between the check and the call: left for the next launch, as above
                }
                return;
            }

            if (_dotNetRuntimeMissingReported)
                return;
            _dotNetRuntimeMissingReported = true;

            MessageBox.Show(
                ViewerStartupFailure.DescribeMissingDotNetRuntime(Singleton.ViewportExecutablePath),
                "Viewport",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
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
            //An application exit goes through (see CloseReasons), taking the viewer process with it; a user close hides the panel instead
            if (CloseReasons.IsApplicationShutdown(e))
            {
                Stop();
                return;
            }
            e.Cancel = true;
            Stop();
            Hide();
        }
    }
}
