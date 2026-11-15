using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE
{
    /// <summary>
    /// A log (the Runtime Error Log, the Game Crash Log) shown docked in the main window's left area, in a pane under the
    /// composite browser's tabs, or popped out as a window of its own that can be minimised. Pop Out and Dock move the same
    /// contents between the two, so nothing in the log is lost, and where it was last put is remembered: it opens there the
    /// next time (docked until it is popped out). Two logs docked at once share the pane as tabs. Closing the pane or the
    /// window closes the log.
    /// </summary>
    public abstract class DockableLog : UserControl
    {
        //Every log that is open, docked or not
        private static readonly List<DockableLog> _logs = new List<DockableLog>();

        //The share of the composite browser's pane a log's pane takes under it, kept as the user last left it
        private const double DefaultDockProportion = 0.4;
        private static double _dockProportion = DefaultDockProportion;

        private readonly string _title;
        private readonly string _dockedSetting;
        private readonly string _documentation;
        private readonly Size _windowSize;
        private readonly Size _windowMinimumSize;

        private LogPanel _panel;
        private LogWindow _window;
        //Out of its pane while the main window's panels are laid out again, to dock again after (see RedockReleased)
        private bool _released;
        private bool _releasedAutoHidden;
        private bool _closed;

        /// <summary>Pop Out while docked, Dock while in a window of its own: for the right of the log's toolbar.</summary>
        protected readonly ToolStripButton PlaceButton = new ToolStripButton { DisplayStyle = ToolStripItemDisplayStyle.Text, Alignment = ToolStripItemAlignment.Right, Overflow = ToolStripItemOverflow.Never };
        /// <summary>Opens the log's page of the documentation: for the right of the log's toolbar.</summary>
        protected readonly ToolStripButton DocsButton = new ToolStripButton("Help") { DisplayStyle = ToolStripItemDisplayStyle.Text, Alignment = ToolStripItemAlignment.Right };

        protected DockableLog(string title, string dockedSetting, string documentation, Size windowSize, Size windowMinimumSize)
        {
            _title = title;
            _dockedSetting = dockedSetting;
            _documentation = documentation;
            _windowSize = windowSize;
            _windowMinimumSize = windowMinimumSize;
            Dock = DockStyle.Fill;

            DocsButton.ToolTipText = "Open the documentation for the " + title + ".";
            DocsButton.Click += (s, e) => OpenDocumentation();
            PlaceButton.Click += (s, e) =>
            {
                if (_window != null)
                    DockInMainWindow();
                else
                    PopOut();
            };
            UpdatePlaceButton();
            Enter += (s, e) => OnHostActivated();
        }

        /// <summary>Whether the log is still open: once closed it is never shown again.</summary>
        protected bool IsLogOpen => !_closed && !IsDisposed;

        /// <summary>Whether the log is docked in the main window, rather than in a window of its own.</summary>
        public bool IsDocked => _panel != null || _released;

        /// <summary>The log closed (its pane or window was closed, or OpenCAGE is closing): let go of what it holds.</summary>
        protected abstract void OnLogClosed();

        /// <summary>The log's window was activated, or the focus came into the log.</summary>
        protected virtual void OnHostActivated() { }

        #region SHOWING
        /// <summary>Show the log where it was last put (docked unless it was popped out), or bring it forward. UI thread.</summary>
        protected void ShowLog()
        {
            if (!IsLogOpen)
                return;
            if (!_logs.Contains(this))
                _logs.Add(this);

            if (_panel != null)
            {
                _panel.Activate();
                return;
            }
            if (_window != null)
            {
                if (_window.WindowState == FormWindowState.Minimized)
                    _window.WindowState = FormWindowState.Normal;
                _window.Show();
                _window.BringToFront();
                return;
            }
            if (_released)
                return;

            if (!SettingsManager.GetBool(_dockedSetting, true) || !TryDock())
                ShowInWindow();
        }

        /// <summary>Move the log into a window of its own, and open it that way from now on. UI thread.</summary>
        public void PopOut()
        {
            if (!IsLogOpen || _window != null)
                return;
            SettingsManager.SetBool(_dockedSetting, false);

            LogPanel panel = _panel;
            _panel = null;
            _released = false;
            if (panel != null)
                KeepProportion(panel);
            ShowInWindow();
            if (panel != null)
                CloseHost(panel);
        }

        /// <summary>Move the log into the main window, docked under the composite browser's tabs, and open it that way from now on. UI thread.</summary>
        public void DockInMainWindow()
        {
            if (!IsLogOpen || IsDocked)
                return;

            LogWindow window = _window;
            if (!TryDock())
            {
                //The main window can't take it: it stays in its window
                if (window != null && !window.IsDisposed && Parent != window)
                    window.Controls.Add(this);
                return;
            }
            SettingsManager.SetBool(_dockedSetting, true);
            _window = null;
            UpdatePlaceButton();
            if (window != null)
                CloseHost(window);
        }

        /* Dock the log in the main window. False, with the log out of any pane, if the main window can't take it */
        private bool TryDock()
        {
            CommandsEditor editor = Singleton.Editor;
            DockPanel dockPanel = editor?.DockPanel;
            if (editor == null || editor.IsDisposed || editor.Disposing || dockPanel == null || dockPanel.IsDisposed)
                return false;

            LogPanel panel = new LogPanel(this);
            try
            {
                Place(panel, dockPanel);
            }
            catch (Exception ex)
            {
                Debug.Log("Logs", "Docking the " + _title + " failed: " + ex.Message);
                CloseHost(panel);
                return false;
            }
            _panel = panel;
            UpdatePlaceButton();
            return true;
        }

        private void ShowInWindow()
        {
            _window = new LogWindow(this);
            UpdatePlaceButton();
            _window.Show();
        }

        /* Into the main window's left area: beside a log already docked there, else in a pane of its own under the composite
           browser's tabs (or under another pane there when the browser is pinned away), or by itself while the area is empty */
        private static void Place(LogPanel panel, DockPanel dockPanel)
        {
            DockPane shared = _logs.Select(o => o._panel?.Pane).FirstOrDefault(o => o != null && !o.IsDisposed && o.DockPanel == dockPanel &&
                (o.DockState == DockState.DockLeft || o.DockState == DockState.DockLeftAutoHide));
            if (shared != null)
            {
                panel.Show(shared, null);
                return;
            }

            DockPane under = PaneToDockUnder(dockPanel);
            if (under != null)
                panel.Show(under, DockAlignment.Bottom, _dockProportion);
            else
                panel.Show(dockPanel, DockState.DockLeft);
        }

        private static DockPane PaneToDockUnder(DockPanel dockPanel)
        {
            DockContent browser = Singleton.Editor?.CompositeBrowser;
            if (browser != null && !browser.IsDisposed && browser.DockPanel == dockPanel && browser.DockState == DockState.DockLeft && browser.Pane != null)
                return browser.Pane;
            return dockPanel.Panes.LastOrDefault(o => !o.IsDisposed && o.DockState == DockState.DockLeft && o.DisplayingContents.Count != 0 && !o.Contents.OfType<LogPanel>().Any());
        }

        /* The share of the browser's pane the user dragged a log's pane to, for the next log docked under the browser's tabs */
        private static void KeepProportion(LogPanel panel)
        {
            DockPane pane = panel.Pane;
            if (pane == null || pane.IsDisposed || pane.DockState != DockState.DockLeft)
                return;
            NestedDockingStatus status = pane.NestedDockingStatus;
            DockContent browser = Singleton.Editor?.CompositeBrowser;
            if (browser != null && status.PreviousPane != browser.Pane)
                return;
            if (status.PreviousPane != null && status.Alignment == DockAlignment.Bottom && status.Proportion > 0.05 && status.Proportion < 0.95)
                _dockProportion = status.Proportion;
        }

        private void UpdatePlaceButton()
        {
            bool windowed = _window != null;
            PlaceButton.Text = windowed ? "Dock" : "Pop Out";
            PlaceButton.ToolTipText = windowed ?
                "Dock the log in the main window, under the composite browser (it opens there from now on)." :
                "Move the log into a window of its own, which can be minimised (it opens that way from now on).";
        }

        private void OpenDocumentation()
        {
            try
            {
                Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
                System.Diagnostics.Process.Start(_documentation);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to open documentation: " + ex.Message);
            }
        }

        /// <summary>
        /// Keep <paramref name="details"/> (docked to the bottom, under the list) at <paramref name="height"/>, but never over half
        /// the room <paramref name="bars"/> leave: docked, the log's pane is often short.
        /// </summary>
        protected void FitDetails(Control details, int height, params Control[] bars)
        {
            void Fit()
            {
                int room = ClientSize.Height;
                foreach (Control bar in bars)
                    room -= bar.Height;
                details.Height = Math.Min(height, Math.Max(40, room / 2));
            }
            Resize += (s, e) => Fit();
            Fit();
        }
        #endregion

        #region THE MAIN WINDOW'S LAYOUT
        /// <summary>
        /// Take every docked log out of the main window's panels while they are laid out again, or made again for a theme:
        /// <see cref="RedockReleased"/> docks them again after, with everything in them kept. UI thread.
        /// </summary>
        public static void ReleaseDocked()
        {
            foreach (DockableLog log in _logs.ToList())
            {
                LogPanel panel = log._panel;
                if (panel == null)
                    continue;
                log._panel = null;
                log._released = true;
                log._releasedAutoHidden = !panel.IsDisposed && DockHelper.IsDockStateAutoHide(panel.DockState);
                KeepProportion(panel);
                log.CloseHost(panel);
            }
        }

        /// <summary>Dock again the logs <see cref="ReleaseDocked"/> took out, under the composite browser's tabs. UI thread.</summary>
        public static void RedockReleased()
        {
            foreach (DockableLog log in _logs.ToList())
            {
                if (!log._released)
                    continue;
                log._released = false;
                if (!log.IsLogOpen)
                    continue;
                if (!log.TryDock())
                {
                    log.ShowInWindow();
                    continue;
                }
                //Pinned away as it was
                DockPane pane = log._panel.Pane;
                if (log._releasedAutoHidden && pane != null && pane.DockState == DockState.DockLeft)
                {
                    pane.DockState = DockState.DockLeftAutoHide;
                    pane.DockPanel.ActiveAutoHideContent = null;
                }
            }
        }
        #endregion

        #region HOSTS
        /* A pane or window the log has left closes, without closing the log */
        private void CloseHost(Form host)
        {
            if (host is LogPanel panel)
                panel.Log = null;
            else if (host is LogWindow window)
                window.Log = null;
            if (host.Controls.Contains(this))
                host.Controls.Remove(this);
            if (host.IsDisposed)
                return;

            try
            {
                if (host is DockContent content)
                {
                    content.Hide();
                    if (content.DockHandler.DockPanel != null)
                        content.DockHandler.Close();
                }
                else
                {
                    host.Close();
                }
            }
            catch (Exception ex)
            {
                Debug.Log("Logs", "Closing the " + _title + "'s old " + (host is DockContent ? "pane" : "window") + " failed: " + ex.Message);
            }
            if (!host.IsDisposed)
                host.Dispose();
        }

        /* The log's pane or window closed with the log in it: the log closes with it */
        private void OnHostClosed(Form host)
        {
            if (host == _panel)
                _panel = null;
            else if (host == _window)
                _window = null;
            Shutdown();
        }

        private void Shutdown()
        {
            if (_closed)
                return;
            _closed = true;
            _released = false;
            _logs.Remove(this);
            OnLogClosed();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Shutdown();
            base.Dispose(disposing);
        }

        /* The pane a docked log shows in, under the composite browser's tabs */
        private sealed class LogPanel : DockContent
        {
            public DockableLog Log;

            public LogPanel(DockableLog log)
            {
                Log = log;
                Text = log._title;
                Icon = SharedFormIcon.Icon;
                DockAreas = DockAreas.DockLeft;
                ShowHint = DockState.DockLeft;
                AllowEndUserDocking = false;
                CloseButton = true;
                CloseButtonVisible = true;
                HideOnClose = false;
                Controls.Add(log);
                Theming.ThemeManager.ApplyToForm(this);
                FormClosed += (s, e) => Log?.OnHostClosed(this);
            }
        }

        /* The window a popped-out log shows in: a window of its own, with a taskbar button, that can be minimised */
        private sealed class LogWindow : BaseWindow
        {
            public DockableLog Log;

            public LogWindow(DockableLog log) : base(WindowClosesOn.NONE)
            {
                Log = log;
                Text = log._title;
                Icon = SharedFormIcon.Icon;
                ClientSize = log._windowSize;
                MinimumSize = log._windowMinimumSize;
                StartPosition = FormStartPosition.CenterScreen;
                ShowInTaskbar = true;
                MinimizeBox = true;
                MaximizeBox = true;
                Controls.Add(log);
                Activated += (s, e) => Log?.OnHostActivated();
                FormClosed += (s, e) => Log?.OnHostClosed(this);
            }
        }
        #endregion
    }
}
