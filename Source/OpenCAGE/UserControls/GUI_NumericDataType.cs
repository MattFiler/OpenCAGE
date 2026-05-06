using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CATHODE.Scripting;
using CATHODE;
using CathodeLib;
using OpenCAGE;

namespace CommandsEditor.UserControls
{
    public partial class GUI_NumericDataType : ParameterUserControl
    {
        cFloat floatVal = null;
        cInteger intVal = null;
        bool isIntInput = false;

        public GUI_NumericDataType()
        {
            InitializeComponent();
            this.ContextMenuStrip = contextMenuStrip1;
            this.deleteToolStripMenuItem.Click += new EventHandler(deleteToolStripMenuItem_Click);
        }

        public void PopulateUI_Float(cFloat cFloat, string paramID)
        {
            isIntInput = false;
            floatVal = cFloat;
            label1.Text = paramID;

            numericUpDown1.DecimalPlaces = 6;
            numericUpDown1.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStep);
            numericUpDown1.Maximum = (decimal)3.4E+28m;
            numericUpDown1.Minimum = (decimal)-3.4E+28m;

            this.deleteToolStripMenuItem.Text = "Delete '" + paramID + "'";
            numericUpDown1.Value = (decimal)cFloat.value;

            _hasDoneSetup = true;
        }

        public void PopulateUI_Int(cInteger cInt, string paramID)
        {
            isIntInput = true;
            intVal = cInt;
            label1.Text = paramID;

            numericUpDown1.DecimalPlaces = 0;
            numericUpDown1.Increment = 1;
            numericUpDown1.Maximum = int.MaxValue;
            numericUpDown1.Minimum = int.MinValue;

            this.deleteToolStripMenuItem.Text = "Delete '" + paramID + "'";
            numericUpDown1.Value = (decimal)cInt.value;

            _hasDoneSetup = true;
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            if (isIntInput)
            {
                intVal.value = (int)numericUpDown1.Value;
            }
            else
            {
                floatVal.value = (float)numericUpDown1.Value;
            }
            HighlightAsModified();
        }

        public override void HighlightAsModified(bool updateDatabase = true, Control fontToUpdate = null)
        {
            base.HighlightAsModified(updateDatabase, label1);
        }
    }
}
