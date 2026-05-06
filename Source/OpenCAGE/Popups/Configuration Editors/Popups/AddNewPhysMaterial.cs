using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CommandsEditor.ConfigEditors
{
    public partial class AddNewPhysMaterial : Form
    {
        public Action<string> OnMaterialAdded;

        public AddNewPhysMaterial()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OnMaterialAdded?.Invoke(textBox1.Text);
            this.Close();
        }
    }
}
