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
    /* A message too long for a message box: a scrolling text with one or two buttons */
    public partial class ModReportDialog : Form
    {
        /// <summary>OK for the first button, Cancel for the cancel one, No for the other one (shown between them) if there is one.</summary>
        public static DialogResult Show(IWin32Window owner, string title, string text, string okText, string cancelText, string otherText = null)
        {
            using (ModReportDialog dialog = new ModReportDialog(title, text, okText, cancelText, otherText))
                return dialog.ShowDialog(owner);
        }

        private ModReportDialog(string title, string text, string okText, string cancelText, string otherText)
        {
            InitializeComponent();
            Text = title;
            Icon = SharedFormIcon.Icon;

            _body.Text = text;
            _okButton.Text = okText;
            //Without a cancel button, closing the window is the first button
            if (cancelText != null)
                _cancelButton.Text = cancelText;
            else
            {
                _cancelButton.Visible = false;
                CancelButton = _okButton;
            }
            if (otherText != null)
                _otherButton.Text = otherText;
            else
                _otherButton.Visible = false;
            //Hiding a button before the window shows doesn't lay the row out again by itself
            _buttons.PerformLayout();

            Theming.ThemeManager.ApplyToForm(this);
        }

        private void ModReportDialog_Shown(object sender, EventArgs e)
        {
            _body.SelectionStart = 0;
            _body.SelectionLength = 0;
            _okButton.Focus();
        }
    }
}
#endif
