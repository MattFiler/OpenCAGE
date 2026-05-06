using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CommandsEditor.Popups.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CommandsEditor
{
    public partial class CAGEAnimation_DeleteParam : BaseWindow
    {
        public Action<int> OnParamSelected;

        public CAGEAnimation_DeleteParam(List<string> parameter_tracks) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_CAGEANIM_EDITOR_OPENED | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();

            parameters.BeginUpdate();
            parameters.Items.Clear();
            for (int i = 0; i < parameter_tracks.Count; i++)
                parameters.Items.Add(parameter_tracks[i]);

            if (parameters.Items.Count == 0)
            {
                MessageBox.Show("There are no event tracks to delete!\nAdd some via the main tool window.", "No event tracks", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.Close();
                return;
            }

            parameters.SelectedIndex = 0;
            parameters.EndUpdate();
        }

        private void select_param_Click(object sender, EventArgs e)
        {
            if (parameters.SelectedIndex == -1) return;
            OnParamSelected?.Invoke(parameters.SelectedIndex);
            this.Close();
        }
    }
}
