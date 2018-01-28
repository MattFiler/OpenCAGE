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

namespace PackagingTool
{
    public partial class BehaviourPackerSettings : Form
    {
        //Settings
        string openFolderOnExport = "1";
        string openGameOnImport = "0";
        string showMessageBoxes = "1";

        public BehaviourPackerSettings()
        {
            InitializeComponent();

            /* GET CURRENT SETTINGS */
            int loopCount = 0;
            foreach (var line in File.ReadLines(Directory.GetCurrentDirectory() + @"\modtools_settings.ayz"))
            {
                switch (line)
                {
                    case "0":
                        if (loopCount == 0)
                        {
                            openFolderOnExport = "0";
                        }
                        if (loopCount == 1)
                        {
                            openGameOnImport = "0";
                        }
                        if (loopCount == 2)
                        {
                            showMessageBoxes = "0";
                        }
                        break;
                    case "1":
                        if (loopCount == 0)
                        {
                            openFolderOnExport = "1";
                        }
                        if (loopCount == 1)
                        {
                            openGameOnImport = "1";
                        }
                        if (loopCount == 2)
                        {
                            showMessageBoxes = "1";
                        }
                        break;
                    default:
                        break;
                }
                loopCount += 1;
            }

            /* APPLY CURRENT SETTINGS */
            if (openFolderOnExport == "1")
            {
                setting_OpenOutputFolder.Checked = true;
            }
            else
            {
                setting_OpenOutputFolder.Checked = false;
            }
            if (openGameOnImport == "1")
            {
                setting_RunGame.Checked = true;
            }
            else
            {
                setting_RunGame.Checked = false;
            }
            if (showMessageBoxes == "1")
            {
                settings_showMessageBox.Checked = true;
            }
            else
            {
                settings_showMessageBox.Checked = false;
            }
        }

        /* OPEN OUTPUT FOLDER? */
        private void setting_OpenOutputFolder_CheckedChanged(object sender, EventArgs e)
        {
            if (setting_OpenOutputFolder.Checked == true)
            {
                openFolderOnExport = "1";
            }
            else
            {
                openFolderOnExport = "0";
            }
        }

        /* RUN GAME ON IMPORT? */
        private void setting_RunGame_CheckedChanged(object sender, EventArgs e)
        {
            if (setting_RunGame.Checked == true)
            {
                openGameOnImport = "1";
            }
            else
            {
                openGameOnImport = "0";
            }
        }

        /* SHOW MESSAGEBOXES? */
        private void settings_showMessageBox_CheckedChanged(object sender, EventArgs e)
        {
            if (settings_showMessageBox.Checked == true)
            {
                showMessageBoxes = "1";
            }
            else
            {
                showMessageBoxes = "0";
            }
        }

        /* SAVE SETTINGS */
        private void saveSettings_Click(object sender, EventArgs e)
        {
            File.WriteAllText(Directory.GetCurrentDirectory() + @"\modtools_settings.ayz", openFolderOnExport+"\n"+openGameOnImport+"\n"+showMessageBoxes);
            this.Close();
        }
    }
}
