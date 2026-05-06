using OpenCAGE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CommandsEditor.Popups
{
    public partial class SetNumericStep : Form
    {
        public SetNumericStep()
        {
            InitializeComponent();

            posStep.Value = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStep);
            rotStep.Value = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStepRot);
        }

        private void posStep_ValueChanged(object sender, EventArgs e)
        {
            SettingsManager.SetFloat(Singleton.Settings.NumericStep, (float)posStep.Value);
        }

        private void rotStep_ValueChanged(object sender, EventArgs e)
        {
            SettingsManager.SetFloat(Singleton.Settings.NumericStepRot, (float)rotStep.Value);
        }
    }
}
