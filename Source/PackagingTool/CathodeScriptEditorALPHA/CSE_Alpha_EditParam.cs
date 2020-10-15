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
        public CSE_Alpha_EditParam(CathodeParameter param)
        {
            param_to_edit = param;
            InitializeComponent();

            transform_edit_panel.Visible = true;
            vector_edit_panel.Visible = false;
            misc_edit_panel.Visible = false;

            switch (param_to_edit.dataType)
            {
                case CathodeDataType.TRANSFORM:
                    transform_edit_panel.Visible = true;
                    break;
                case CathodeDataType.VECTOR3:
                    vector_edit_panel.Visible = true;
                    break;
                case CathodeDataType.INTEGER:
                case CathodeDataType.STRING:
                case CathodeDataType.FLOAT:
                    misc_edit_panel.Visible = true;
                    break;
            }
        }

        private void save_edit_Click(object sender, EventArgs e)
        {
            /*
            switch (param_to_edit.dataType)
            {
                case CathodeDataType.TRANSFORM:
                    if ()
                    CathodeTransform paramTransform = (CathodeTransform)param_to_edit;
                    paramTransform.position.x = transform_pos_x.Text
                    transform_edit_panel.Visible = true;
                    break;
                case CathodeDataType.VECTOR3:
                    vector_edit_panel.Visible = true;
                    break;
                case CathodeDataType.INTEGER:
                case CathodeDataType.STRING:
                case CathodeDataType.FLOAT:
                    misc_edit_panel.Visible = true;
                    break;
            }
            */
        }
    }
}
