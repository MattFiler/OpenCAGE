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

        public ProgressUI() : base()
        {
            InitializeComponent();

            progressBar1.Maximum = 100;
            progressBar1.Refresh();

            FormClosing += ProgressUI_FormClosing;
        }

        private void ProgressUI_FormClosing(object sender, FormClosingEventArgs e)
        {
            SetEditorTaskbarProgress(TaskbarProgressBarState.NoProgress);

            FormClosing -= ProgressUI_FormClosing;

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
        public void ShowLevelSaving(Level level) => ShowLevel(level, false);

        private void ShowLevel(Level level, bool loading)
        {
            _level = level;
            _counter = 0;

            if (_level == null)
            {
                Close();
                return;
            }

            Text = (loading ? "Loading " : "Saving ") + level.Name + "...";

            SetEditorTaskbarProgress(TaskbarProgressBarState.Indeterminate);

            progressBar1.Style = ProgressBarStyle.Continuous;
            progressBar1.Value = 0;
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

            ShowInTaskbar = false;
            TopMost = true;
            Show();
            BringToFront();
            Activate();
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
                {
                    progressBar1.BeginInvoke(new Action(() =>
                    {
                        if (!IsDisposed && !Disposing && progressBar1 != null)
                        {
                            progressBar1.Value = progress;
                            progressBar1.Refresh();
                        }
                    }));
                }
            }
            else if (!IsDisposed && !Disposing && progressBar1 != null)
            {
                progressBar1.Value = progress;
                progressBar1.Refresh();
            }
        }
    }
}
