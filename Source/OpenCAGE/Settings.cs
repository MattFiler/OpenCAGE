using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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

            useStaging.Checked = SettingsManager.GetString("CONFIG_RemoteBranch") == "staging";
            showPlatform.Checked = SettingsManager.GetBool("CONFIG_ShowPlatform");
            assetFileLockWarning.Checked = SettingsManager.GetBool("CONFIG_HideAssetWarning");
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
            SettingsManager.SetBool("CONFIG_HideAssetWarning", assetFileLockWarning.Checked);
            this.Close();
        }

        private void resetAll_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Verification will re-download all OpenCAGE components - this should solve issues you may be experiencing with the tools after an update.\n\nThe process may take some time depending on your connection. Are you sure you want to continue?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                List<Process> allProcesses = new List<Process>();
                List<string> processNames = new List<string>(Directory.GetFiles(SettingsManager.GetString("PATH_GameRoot") + "/DATA/MODTOOLS/", "*.exe", SearchOption.AllDirectories));
                for (int i = 0; i < processNames.Count; i++) allProcesses.AddRange(Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processNames[i])));
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        for (int x = 0; x < allProcesses.Count; x++) try { allProcesses[x].Kill(); } catch { }
                    }
                    catch { }
                    try
                    {
                        Directory.Delete(SettingsManager.GetString("PATH_GameRoot") + "/DATA/MODTOOLS/", true);
                    }
                    catch { }
                    try
                    {
                        Directory.Delete(SettingsManager.GetString("PATH_GameRoot") + "/DATA/MODS/", true);
                    }
                    catch { }
                }
                _updatedConfig = true;
                this.Close();
            }
        }
    }
}
