using OpenCAGE.Popups.Base;
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class CAGEAnimation_AddKeyframeAtTime : BaseWindow
    {
        public Action<float> OnTimeEntered;

        public CAGEAnimation_AddKeyframeAtTime(float defaultTime, float animLength) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_CAGEANIM_EDITOR_OPENED | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();
            Icon = SharedFormIcon.Icon;

            if (animLength > 0f)
                _prompt.Text = "Keyframe time (seconds, anim length " + animLength.ToString("0.###") + "s):";
            _timeBox.Text = defaultTime.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private void CAGEAnimation_AddKeyframeAtTime_Shown(object sender, EventArgs e)
        {
            _timeBox.Focus();
            _timeBox.SelectAll();
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            Close();
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
