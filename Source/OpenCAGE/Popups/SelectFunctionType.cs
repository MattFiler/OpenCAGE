using CATHODE.Scripting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    public partial class SelectFunctionType : Form
    {
        public Action<FunctionType> OnTypeSelected;

        public SelectFunctionType()
        {
            InitializeComponent();

            functionTypeList1.Setup();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (functionTypeList1.SelectedItem == null)
                return;

            if (Enum.TryParse<FunctionType>(functionTypeList1.SelectedItem.Text, out FunctionType type))
                OnTypeSelected.Invoke(type);

            this.Close();
        }
    }
}
