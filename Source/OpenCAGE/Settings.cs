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

            if (SettingsManager.GetString("CONFIG_RemoteBranch") == "")
            {
                if (SettingsManager.GetBool("CONFIG_UseStagingBranch"))
                    SettingsManager.SetString("CONFIG_RemoteBranch", "staging");
                else
                    SettingsManager.SetString("CONFIG_RemoteBranch", "master");
            }

            useStaging.Checked = SettingsManager.GetString("CONFIG_RemoteBranch") == "staging";
            showPlatform.Checked = SettingsManager.GetBool("CONFIG_ShowPlatform");
        }

        private void saveConfig_Click(object sender, EventArgs e)
        {
            if (useStaging.Checked != (SettingsManager.GetString("CONFIG_RemoteBranch") == "staging"))
            {
                _updatedConfig = true;
                SettingsManager.SetString("CONFIG_RemoteBranch", useStaging.Checked ? "staging" : "master");
                MessageBox.Show("OpenCAGE will now restart to apply your updated settings.", "Settings saved.", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            SettingsManager.SetBool("CONFIG_ShowPlatform", showPlatform.Checked);
            this.Close();
        }
    }
}
