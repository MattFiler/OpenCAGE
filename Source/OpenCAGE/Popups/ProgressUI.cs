using CathodeLib;
using OpenCAGE.Popups.Base;
using Microsoft.WindowsAPICodePack.Taskbar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

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

            this.FormClosing += ProgressUI_FormClosing;
        }

        private void ProgressUI_FormClosing(object sender, FormClosingEventArgs e)
        {
            TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress, Singleton.Editor.Handle);

            this.FormClosing -= ProgressUI_FormClosing;

            if (_level != null)
            {
                _level.OnLoadTick -= UpdateProgressBar;
                _level.OnSaveTick -= UpdateProgressBar;
                _level = null;
            }
        }

        public void ShowLevelLoading(Level level) => ShowLevel(level, true);
        public void ShowLevelSaving(Level level) => ShowLevel(level, false);

        private void ShowLevel(Level level, bool loading)
        {
            _level = level;
            _counter = 0;

            if (_level == null)
            {
                this.Close();
                return;
            }

            this.Text = (loading ? "Loading " : "Saving ") + level.Name + "...";

            TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Indeterminate, Singleton.Editor.Handle);

            if (progressBar1.InvokeRequired)
            {
                progressBar1.BeginInvoke(new Action(() => {
                    progressBar1.Style = ProgressBarStyle.Continuous;
                    progressBar1.Value = 0;
                    progressBar1.Refresh();
                }));
            }
            else
            {
                progressBar1.Style = ProgressBarStyle.Continuous;
                progressBar1.Value = 0;
                progressBar1.Refresh();
            }

            if (loading)
                _level.OnLoadTick += UpdateProgressBar;
            else
                _level.OnSaveTick += UpdateProgressBar;

            this.Show();
        }

        public void ShowTransferring(string titlebar)
        {
            this.Text = titlebar;

            TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Indeterminate, this.Handle);

            progressBar1.Style = ProgressBarStyle.Marquee;
            progressBar1.Refresh();

            this.Show();
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
                if (!this.IsDisposed && !this.Disposing)
                {
                    progressBar1.BeginInvoke(new Action(() => {
                        if (!this.IsDisposed && !this.Disposing && progressBar1 != null)
                        {
                            progressBar1.Value = progress;
                            progressBar1.Refresh();
                        }
                    }));
                }
            }
            else
            {
                if (!this.IsDisposed && !this.Disposing && progressBar1 != null)
                {
                    progressBar1.Value = progress;
                    progressBar1.Refresh();
                }
            }
        }
    }
}
