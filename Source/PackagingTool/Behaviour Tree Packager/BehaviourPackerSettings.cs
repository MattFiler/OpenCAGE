/*
 * 
 * Created by Matt Filer
 * www.mattfiler.co.uk
 * 
 */

 using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using Alien_Isolation_Mod_Tools;

namespace PackagingTool
{
    public partial class BehaviourPackerSettings : Form
    {
        ToolSettings Settings = new ToolSettings();

        public BehaviourPackerSettings()
        {
            InitializeComponent();
            
            //Apply current settings to GUI
            setting_RunGame.Checked = Settings.GetSetting(ToolSettings.Settings.SETTING_OPEN_GAME_ON_IMPORT);
            settings_showMessageBox.Checked = Settings.GetSetting(ToolSettings.Settings.SETTING_SHOW_MESSAGE_BOXES);
        }

        /* RUN GAME ON IMPORT? */
        private void setting_RunGame_CheckedChanged(object sender, EventArgs e)
        {
            Settings.SetSetting(ToolSettings.Settings.SETTING_OPEN_GAME_ON_IMPORT, setting_RunGame.Checked);
        }

        /* SHOW MESSAGEBOXES? */
        private void settings_showMessageBox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.SetSetting(ToolSettings.Settings.SETTING_SHOW_MESSAGE_BOXES, settings_showMessageBox.Checked);
        }
    }
}
