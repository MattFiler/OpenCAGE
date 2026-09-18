using CathodeLib;
using OpenCAGE.Popups.Base;
using Microsoft.WindowsAPICodePack.Taskbar;
using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class ProgressUI : BaseWindow
    {
        private int _counter = 0;
        private Level _level;
        private bool _instancingMarquee;

        public ProgressUI() : base()
        {
            InitializeComponent();

            progressBar1.Maximum = 100;
            progressBar1.Refresh();

            FormClosing += ProgressUI_FormClosing;
        }

        /* Never the active window: it reports, it is not worked in. Closing an active window gives
           focus back to the window that had it - the embedded viewer, after a click in the viewport -
           and giving the viewer focus means waiting on its thread, which after an import or a reload
           is busy for seconds. A window that never took focus has nothing to give back. */
        protected override bool ShowWithoutActivation => true;
        protected override bool ActivateOnShown => false;

        /// <summary>
        /// Put the window back on top without making it the active window. BringToFront on a top-level
        /// window activates it, which is what this window must never be (see above).
        /// </summary>
        public void KeepOnTop()
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
                return;
            SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010;
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        private void ProgressUI_FormClosing(object sender, FormClosingEventArgs e)
        {
            SetEditorTaskbarProgress(TaskbarProgressBarState.NoProgress);

            FormClosing -= ProgressUI_FormClosing;

            _instancingMarquee = false;

            if (_level != null)
            {
                _level.OnLoadTick -= UpdateProgressBar;
                _level.OnSaveTick -= UpdateProgressBar;
                _level = null;
            }
        }

        private void SetEditorTaskbarProgress(TaskbarProgressBarState state)
        {
            try
            {
                Form editor = Singleton.Editor;
                if (editor == null || editor.IsDisposed || editor.Disposing || !editor.IsHandleCreated)
                    return;

                TaskbarManager.Instance.SetProgressState(state, editor.Handle);
            }
            catch (ObjectDisposedException) { }
        }

        public void ShowLevelLoading(Level level) => ShowLevel(level, true);
        public void ShowLevelSaving(Level level, bool withInstancing = false) => ShowLevel(level, false, withInstancing);

        private void ShowLevel(Level level, bool loading, bool withInstancing = false)
        {
            _level = level;
            _counter = 0;
            _instancingMarquee = !loading && withInstancing;

            if (_level == null)
            {
                Close();
                return;
            }

            SetEditorTaskbarProgress(TaskbarProgressBarState.Indeterminate);

            if (_instancingMarquee)
            {
                Text = "Instancing " + level.Name + "...";
                progressBar1.Style = ProgressBarStyle.Marquee;
            }
            else
            {
                Text = (loading ? "Loading " : "Saving ") + level.Name + "...";
                progressBar1.Style = ProgressBarStyle.Continuous;
                progressBar1.Value = 0;
            }
            progressBar1.Refresh();

            if (loading)
                _level.OnLoadTick += UpdateProgressBar;
            else
                _level.OnSaveTick += UpdateProgressBar;

            PresentOnTop();
        }

        public void ShowTransferring(string titlebar)
        {
            Text = titlebar;

            TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Indeterminate, Handle);

            progressBar1.Style = ProgressBarStyle.Marquee;
            progressBar1.Refresh();

            PresentOnTop();
        }

        public void ShowViewerPopulating(string displayLabel)
        {
            if (_level != null)
            {
                _level.OnLoadTick -= UpdateProgressBar;
                _level.OnSaveTick -= UpdateProgressBar;
                _level = null;
            }

            string label = string.IsNullOrWhiteSpace(displayLabel) ? "level" : displayLabel;
            Text = "Populating " + label + " in viewport...";

            SetEditorTaskbarProgress(TaskbarProgressBarState.Indeterminate);

            progressBar1.Style = ProgressBarStyle.Marquee;
            progressBar1.Refresh();

            PresentOnTop();
        }

        private void PresentOnTop()
        {
            if (IsDisposed || Disposing)
                return;

            Form editor = Singleton.Editor;
            if (editor != null && !editor.IsDisposed)
            {
                StartPosition = FormStartPosition.Manual;
                Rectangle bounds = editor.Bounds;
                Location = new Point(
                    bounds.Left + Math.Max(0, (bounds.Width - Width) / 2),
                    bounds.Top + Math.Max(0, (bounds.Height - Height) / 2));
            }
            else
            {
                StartPosition = FormStartPosition.CenterScreen;
            }

            /* Not Form.TopMost: WinForms re-applies that inside CreateHandle with a SetWindowPos that
               activates the window, so the first Show would make this the active window after all.
               Shown without activation, then raised with SetWindowPos and SWP_NOACTIVATE. */
            ShowInTaskbar = false;
            Show();
            KeepOnTop();
        }

        public void DoRefresh()
        {
            progressBar1.Refresh();
        }

        private void UpdateProgressBar()
        {
            int currentCount = Interlocked.Increment(ref _counter);
            int progress = Math.Min(100, (currentCount * 100) / Level.NumberOfTicks);
            if (progressBar1.InvokeRequired)
            {
                if (!IsDisposed && !Disposing)
                    progressBar1.BeginInvoke(new Action(() => ApplySaveOrLoadProgress(progress)));
            }
            else
            {
                ApplySaveOrLoadProgress(progress);
            }
        }

        private void ApplySaveOrLoadProgress(int progress)
        {
            if (IsDisposed || Disposing || progressBar1 == null)
                return;

            if (_instancingMarquee)
            {
                _instancingMarquee = false;
                Text = "Saving " + _level.Name + "...";
                progressBar1.Style = ProgressBarStyle.Continuous;
                progressBar1.Value = 0;
            }

            progressBar1.Value = progress;
            progressBar1.Refresh();
        }
    }
}
