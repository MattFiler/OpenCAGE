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
    public partial class CAGEAnimation_SelectParameter : BaseWindow
    {
        public Action<Parameter> OnParamSelected;

        private Entity _entity;

        public CAGEAnimation_SelectParameter(Entity entity) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_CAGEANIM_EDITOR_OPENED | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();

            _entity = entity;

            parameters.BeginUpdate();
            parameters.Items.Clear();
            for (int i = 0; i < _entity.parameters.Count; i++)
                parameters.Items.Add(_entity.parameters[i].name.ToString());

            if (parameters.Items.Count == 0)
            {
                MessageBox.Show("This entity has no parameters to animate!\nAdd some via the main tool window.", "No parameters on entity", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                this.Close();
                return;
            }

            parameters.SelectedIndex = 0;
            parameters.EndUpdate();
        }

        private void select_param_Click(object sender, EventArgs e)
        {
            if (parameters.SelectedIndex == -1) return;
            OnParamSelected?.Invoke(_entity.parameters[parameters.SelectedIndex]);
            this.Close();
        }
    }
}
