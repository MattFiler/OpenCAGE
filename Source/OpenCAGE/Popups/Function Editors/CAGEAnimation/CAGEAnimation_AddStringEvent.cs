using OpenCAGE.Popups.Base;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class CAGEAnimation_AddStringEvent : BaseWindow
    {
        public Action<string> OnEventNameEntered;

        public CAGEAnimation_AddStringEvent(float time) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_CAGEANIM_EDITOR_OPENED | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();
            Icon = SharedFormIcon.Icon;

            _prompt.Text = "Event name at " + time.ToString("0.###") + "s:";
        }

        private void CAGEAnimation_AddStringEvent_Shown(object sender, EventArgs e)
        {
            _eventName.Focus();
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            Close();
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
