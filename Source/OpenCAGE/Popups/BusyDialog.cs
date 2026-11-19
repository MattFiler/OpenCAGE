#if ENABLE_MOD_PACKAGES
using OpenCAGE.Modding;
using OpenCAGE.Modding.Merging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    /* A modal "working..." window: runs Work on a thread, shows each step it reports, closes itself when done */
    public partial class BusyDialog : Form
    {
        /// <summary>The work. It's handed a way to report what it's doing now.</summary>
        public Action<Action<string>> Work;

        private bool _finished;

        public BusyDialog(string message)
        {
            InitializeComponent();
            Icon = SharedFormIcon.Icon;
            _label.Text = message;

            Theming.ThemeManager.ApplyToForm(this);
        }

        /* The control box stays (the title bar only shows the icon with it), with its close button greyed out: the work can't be stopped part way */
        private void BusyDialog_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_finished && e.CloseReason == CloseReason.UserClosing)
                e.Cancel = true;
        }

        private void BusyDialog_Shown(object sender, EventArgs e)
        {
            Thread thread = new Thread(() =>
            {
                try { Work?.Invoke(Report); }
                finally { BeginInvoke((Action)(() => { _finished = true; Close(); })); }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        /* The close button greyed out, and Close taken off the window menu (CS_NOCLOSE) */
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ClassStyle |= 0x200;
                return parameters;
            }
        }

        private void Report(string step)
        {
            try
            {
                if (!IsDisposed && IsHandleCreated)
                    BeginInvoke((Action)(() => _label.Text = step));
            }
            catch { }
        }
    }
}
#endif
