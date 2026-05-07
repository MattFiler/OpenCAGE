using CATHODE.Scripting;
using CathodeLib;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;
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
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.Popups
{
    public partial class SelectLevel : BaseWindow
    {
        public Action<string> OnLevelSelected;

        public SelectLevel() : base()
        {
            InitializeComponent();
            EditorUtils.PopulateLevelDropdown(env_list);
        }

        private void load_commands_pak_Click(object sender, EventArgs e)
        {
            OnLevelSelected?.Invoke(env_list.SelectedItem.ToString());
            this.Close();
        }

        private void env_list_SelectedIndexChanged(object sender, EventArgs e)
        {
            SettingsManager.SetString("OPT_LoadToMap", env_list.Items[env_list.SelectedIndex].ToString());
        }
    }
}
