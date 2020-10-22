using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alien_Isolation_Mod_Tools
{
    public partial class CSE_Alpha_EditParam : Form
    {
        private CathodeParameter param_to_edit = null;

        /* Initialise */
        public CSE_Alpha_EditParam(CathodeParameter param)
        {
            param_to_edit = param;
            InitializeComponent();
            SetupUI();
            PopulateFields();
        }

        /* Realign UI items */
        private void SetupUI()
        {
            switch (param_to_edit.dataType)
            {
                case CathodeDataType.TRANSFORM:
                    transform_edit_panel.Visible = true;
                    transform_edit_panel.Location = new Point(12, 12);
                    save_edit.Location = new Point(351, 150);
                    alpha_warning.Location = new Point(9, 185);
                    Size = new Size(473, 274);
                    break;
                case CathodeDataType.VECTOR3:
                    vector_edit_panel.Visible = true;
                    vector_edit_panel.Location = new Point(12, 12);
                    save_edit.Location = new Point(351, 98);
                    alpha_warning.Location = new Point(9, 134);
                    Size = new Size(473, 225);
                    break;
                case CathodeDataType.INTEGER:
                case CathodeDataType.STRING:
                case CathodeDataType.FLOAT:
                    misc_edit_panel.Visible = true;
                    misc_edit_panel.Location = new Point(12, 12);
                    save_edit.Location = new Point(351, 98);
                    alpha_warning.Location = new Point(9, 134);
                    Size = new Size(473, 225);
                    break;
            }
        }

        /* Populate the edit UI with the original parameter values */
        private void PopulateFields()
        {
            switch (param_to_edit.dataType)
            {
                case CathodeDataType.TRANSFORM:
                    CathodeTransform cTransform = (CathodeTransform)param_to_edit;
                    transform_pos_x.Text = cTransform.position.x.ToString();
                    transform_pos_y.Text = cTransform.position.y.ToString();
                    transform_pos_z.Text = cTransform.position.z.ToString();
                    transform_rot_x.Text = cTransform.rotation.x.ToString();
                    transform_rot_y.Text = cTransform.rotation.y.ToString();
                    transform_rot_z.Text = cTransform.rotation.z.ToString();
                    break;
                case CathodeDataType.VECTOR3:
                    CathodeVector3 cVector = (CathodeVector3)param_to_edit;
                    vector_x.Text = cVector.value.x.ToString();
                    vector_y.Text = cVector.value.y.ToString();
                    vector_z.Text = cVector.value.z.ToString();
                    break;
                case CathodeDataType.INTEGER:
                    CathodeInteger cInt = (CathodeInteger)param_to_edit;
                    parameter_input.Text = cInt.value.ToString();
                    break;
                case CathodeDataType.STRING:
                    CathodeString cString = (CathodeString)param_to_edit;
                    parameter_input.MaxLength = cString.initial_length;
                    parameter_input.Text = cString.value.ToString();
                    break;
                case CathodeDataType.FLOAT:
                    CathodeFloat cFloat = (CathodeFloat)param_to_edit;
                    parameter_input.Text = cFloat.value.ToString();
                    break;
            }
        }

        /* Save the edited parameter */
        private void save_edit_Click(object sender, EventArgs e)
        {
            switch (param_to_edit.dataType)
            {
                case CathodeDataType.TRANSFORM:
                    if (!IsInputNumeric(transform_pos_x) || !IsInputNumeric(transform_pos_y) || !IsInputNumeric(transform_pos_z) ||
                        !IsInputNumeric(transform_rot_x) || !IsInputNumeric(transform_rot_y) || !IsInputNumeric(transform_rot_z))
                    {
                        MessageBox.Show("All inputs must be numeric, cannot save!", "Invalid input.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        break;
                    }
                    CathodeTransform paramTransform = (CathodeTransform)param_to_edit;
                    paramTransform.position.x = (float)Convert.ToDouble(transform_pos_x.Text);
                    paramTransform.position.y = (float)Convert.ToDouble(transform_pos_y.Text);
                    paramTransform.position.z = (float)Convert.ToDouble(transform_pos_z.Text);
                    paramTransform.rotation.x = (float)Convert.ToDouble(transform_rot_x.Text);
                    paramTransform.rotation.y = (float)Convert.ToDouble(transform_rot_y.Text);
                    paramTransform.rotation.z = (float)Convert.ToDouble(transform_rot_z.Text);
                    this.Close();
                    break;
                case CathodeDataType.VECTOR3:
                    if (!IsInputNumeric(vector_x) || !IsInputNumeric(vector_y) || !IsInputNumeric(vector_z))
                    {
                        MessageBox.Show("All inputs must be numeric, cannot save!", "Invalid input.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        break;
                    }
                    CathodeVector3 cVector = (CathodeVector3)param_to_edit;
                    cVector.value.x = (float)Convert.ToDouble(vector_x.Text);
                    cVector.value.y = (float)Convert.ToDouble(vector_y.Text);
                    cVector.value.z = (float)Convert.ToDouble(vector_z.Text);
                    this.Close();
                    break;
                case CathodeDataType.INTEGER:
                    if (!IsInputNumeric(parameter_input, false))
                    {
                        MessageBox.Show("Input must be a whole number, cannot save!", "Invalid input.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        break;
                    }
                    CathodeInteger cInt = (CathodeInteger)param_to_edit;
                    cInt.value = Convert.ToInt32(parameter_input.Text);
                    this.Close();
                    break;
                case CathodeDataType.STRING:
                    CathodeString cString = (CathodeString)param_to_edit;
                    cString.value = parameter_input.Text;
                    this.Close();
                    break;
                case CathodeDataType.FLOAT:
                    if (!IsInputNumeric(parameter_input))
                    {
                        MessageBox.Show("Input must be numeric, cannot save!", "Invalid input.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        break;
                    }
                    CathodeFloat cFloat = (CathodeFloat)param_to_edit;
                    cFloat.value = (float)Convert.ToDouble(parameter_input.Text);
                    this.Close();
                    break;
            }
        }

        /* Check for validity on input field */
        private bool IsInputNumeric(TextBox input, bool canHaveDecimal = true)
        {
            decimal temp_num = 0;
            bool canConvert = decimal.TryParse(input.Text, out temp_num);
            if (!canHaveDecimal && (temp_num % 1) != 0) return false;
            return canConvert;
        }
    }
}
