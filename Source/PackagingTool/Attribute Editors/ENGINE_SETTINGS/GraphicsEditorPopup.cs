using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alien_Isolation_Mod_Tools.Attribute_Editors.ENGINE_SETTINGS
{
    public partial class GraphicsEditorPopup : Form
    {
        public GraphicsEditorPopup(string titleOne, string titleTwo, string titleThree)
        {
            InitializeComponent();

            TitleOne.Text = titleOne;
            TitleTwo.Text = titleTwo;
            TitleThree.Text = titleThree;

            if (titleThree == "")
            {
                textBox3.Visible = false;
                textBox3.Enabled = false;
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            GraphicsEditor graphicsForm = (GraphicsEditor)Application.OpenForms["GraphicsEditor"];
            graphicsForm.getDataFromPopup(textBox1.Text, textBox2.Text, textBox3.Text);

            this.Close();
        }
    }
}
