using OpenCAGE.Popups.Base;
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace OpenCAGE
{
    public class CAGEAnimation_AddKeyframeAtTime : BaseWindow
    {
        public Action<float> OnTimeEntered;

        private TextBox _timeBox;
        private Button _ok;
        private Button _cancel;

        public CAGEAnimation_AddKeyframeAtTime(float defaultTime, float animLength) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_CAGEANIM_EDITOR_OPENED | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            Text = "Add Keyframe";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(320, 120);
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;

            Label prompt = new Label();
            prompt.AutoSize = true;
            prompt.Location = new Point(12, 14);
            prompt.Text = animLength > 0f
                ? "Keyframe time (seconds, anim length " + animLength.ToString("0.###") + "s):"
                : "Keyframe time (seconds):";

            _timeBox = new TextBox();
            _timeBox.Location = new Point(12, 38);
            _timeBox.Size = new Size(292, 23);
            _timeBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _timeBox.Text = defaultTime.ToString("0.###", CultureInfo.InvariantCulture);

            _ok = new Button();
            _ok.Text = "Add";
            _ok.Location = new Point(148, 78);
            _ok.Size = new Size(75, 26);
            _ok.Click += Ok_Click;

            _cancel = new Button();
            _cancel.Text = "Cancel";
            _cancel.Location = new Point(229, 78);
            _cancel.Size = new Size(75, 26);
            _cancel.Click += (s, e) => Close();

            AcceptButton = _ok;
            CancelButton = _cancel;

            Controls.Add(prompt);
            Controls.Add(_timeBox);
            Controls.Add(_ok);
            Controls.Add(_cancel);

            Shown += (s, e) =>
            {
                _timeBox.Focus();
                _timeBox.SelectAll();
            };
        }

        private void Ok_Click(object sender, EventArgs e)
        {
            string raw = EditorUtils.ForceStringNumeric((_timeBox.Text ?? "").Trim(), true);
            _timeBox.Text = raw;
            float time;
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out time)
                && !float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out time))
            {
                MessageBox.Show("Enter a valid time in seconds.", "Invalid time", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (time < 0f)
            {
                MessageBox.Show("Time cannot be negative.", "Invalid time", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            OnTimeEntered?.Invoke(time);
            Close();
        }
    }
}
