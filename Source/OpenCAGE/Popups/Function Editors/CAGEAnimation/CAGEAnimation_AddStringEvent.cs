using OpenCAGE.Popups.Base;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace OpenCAGE
{
    public class CAGEAnimation_AddStringEvent : BaseWindow
    {
        public Action<string> OnEventNameEntered;

        private TextBox _eventName;
        private Button _ok;
        private Button _cancel;

        public CAGEAnimation_AddStringEvent(float time) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_CAGEANIM_EDITOR_OPENED | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            Text = "Add String Event";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(420, 120);
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;

            Label prompt = new Label();
            prompt.AutoSize = true;
            prompt.Location = new Point(12, 14);
            prompt.Text = "Event name at " + time.ToString("0.###") + "s:";

            _eventName = new TextBox();
            _eventName.Location = new Point(12, 38);
            _eventName.Size = new Size(392, 23);
            _eventName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            _ok = new Button();
            _ok.Text = "Add";
            _ok.Location = new Point(248, 78);
            _ok.Size = new Size(75, 26);
            _ok.Click += Ok_Click;

            _cancel = new Button();
            _cancel.Text = "Cancel";
            _cancel.Location = new Point(329, 78);
            _cancel.Size = new Size(75, 26);
            _cancel.Click += (s, e) => Close();

            AcceptButton = _ok;
            CancelButton = _cancel;

            Controls.Add(prompt);
            Controls.Add(_eventName);
            Controls.Add(_ok);
            Controls.Add(_cancel);

            Shown += (s, e) => _eventName.Focus();
        }

        private void Ok_Click(object sender, EventArgs e)
        {
            string name = (_eventName.Text ?? "").Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Enter an event name.", "Missing name", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            OnEventNameEntered?.Invoke(name);
            Close();
        }
    }
}
