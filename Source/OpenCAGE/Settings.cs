using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class Settings : Form
    {
        public bool DidActuallyUpdateSettings { get { return _updatedConfig; } }
        private bool _updatedConfig = false;

        public Settings()
        {
            InitializeComponent();
            useStaging.Checked = SettingsManager.GetBool("CONFIG_UseStagingBranch");
        }

        private void saveConfig_Click(object sender, EventArgs e)
        {
            if (useStaging.Checked != SettingsManager.GetBool("CONFIG_UseStagingBranch"))
            {
                _updatedConfig = true;
                SettingsManager.SetBool("CONFIG_UseStagingBranch", useStaging.Checked);
                MessageBox.Show("OpenCAGE will now restart to apply your updated settings.", "Settings saved.", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            this.Close();
        }
    }
}
