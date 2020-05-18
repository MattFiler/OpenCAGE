using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alien_Isolation_Mod_Tools
{
    class ToolSettings
    {
        private string Path_To_Settings = AppDomain.CurrentDomain.BaseDirectory + @"\modtools_settings.ayz";

        /* 
         * Setting History:
         *  18/05/20 - V3:
         *      - No changes: bumped to fix option reset issue
         *  16/08/19 - V2:
         *      - Open Folder On Export (t) / Open Game On Import (f) / Show Message Boxes (t) / Did Setup Folders (f)
         *  16/08/19 - V1:
         *      - Open Folder On Export (t) / Open Game On Import (f) / Show Message Boxes (t)
         */
        private Int16 Expected_Setting_Version = 3;
        public enum Settings {
            SETTING_OPEN_FOLDER_ON_EXPORT,
            SETTING_OPEN_GAME_ON_IMPORT,
            SETTING_SHOW_MESSAGE_BOXES,
            SETTING_INTERNAL_DID_SETUP_FOLDERS
        };
        private static bool[] Settings_Values =
        {
            true,
            false,
            true,
            false
        };

        /* Initialise Settings */
        public ToolSettings()
        {
            //First, if config doesn't already exist, write defaults
            if (!File.Exists(Path_To_Settings))
            {
                SaveSettings();
                return;
            }

            //If config does exist
            BinaryReader Setting_Reader = new BinaryReader(File.OpenRead(Path_To_Settings));
            if (Setting_Reader.ReadInt16() != Expected_Setting_Version)
            {
                //Config exists but is outdated - revert to defaults
                MessageBox.Show("Due to an OpenCAGE update, your preferences have been reset to defaults.", "Settings reset!", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Setting_Reader.Close();
                SaveSettings();
                return;
            }
            //Read values from valid config
            for (int i = 0; i < Settings_Values.Length; i++)
            {
                Settings_Values[i] = Setting_Reader.ReadBoolean();
            }
            Setting_Reader.Close();
        }

        /* Set a Setting */
        public void SetSetting(Settings Setting, bool Value)
        {
            Settings_Values[(int)Setting] = Value;
            SaveSettings();
        }

        /* Get a Setting */
        public bool GetSetting(Settings Setting)
        {
            return Settings_Values[(int)Setting];
        }

        /* Save Settings */
        private void SaveSettings()
        {
            BinaryWriter Settings_Writer = new BinaryWriter(File.OpenWrite(Path_To_Settings));
            Settings_Writer.Write(Expected_Setting_Version);
            for (int i = 0; i < Settings_Values.Length; i++)
            {
                Settings_Writer.Write(Settings_Values[i]);
            }
            Settings_Writer.Close();
        }
    }
}
