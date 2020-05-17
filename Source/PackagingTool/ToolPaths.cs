using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alien_Isolation_Mod_Tools
{
    class ToolPaths
    {
        private string Path_To_Config = AppDomain.CurrentDomain.BaseDirectory + @"\modtools_locales.ayz";

        /* 
         * Path History:
         *  16/08/19 - V1:
         *      - Game / Brainiac / Trees / Working Files / Resources / Mod Directory / LegendPlugin DLL / Brainiac EXE
         *  17/05/20 - V2:
         *      - Added texconv
         */
        private Int16 Expected_Config_Version = 2;
        public enum Paths
        {
            FOLDER_ALIEN_ISOLATION,
            FOLDER_BRAINIAC,
            FOLDER_BEHAVIOUR_TREES,
            FOLDER_WORKING_FILES,
            FOLDER_TOOL_RESOURCES,
            FOLDER_MOD_INSTALL_LOCATION,
            FOLDER_TEXCONV,
            FILE_LEGENDPLUGIN_DLL,
            FILE_BRAINIAC_EXE,
            FILE_TEXCONV
        };
        private static string[] Path_Values =
        {
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            ""
        };
        
        /* Load Path Config */
        public ToolPaths()
        {
            //Path config doesn't exist
            if (!File.Exists(Path_To_Config))
            {
                SavePaths();
                return;
            }

            //If config does exist
            BinaryReader Config_Reader = new BinaryReader(File.OpenRead(Path_To_Config));
            if (Config_Reader.ReadInt16() != Expected_Config_Version)
            {
                //Config exists but is outdated - revert to defaults
                Config_Reader.Close();
                ToolSettings Settings = new ToolSettings();
                Settings.SetSetting(ToolSettings.Settings.SETTING_INTERNAL_DID_SETUP_FOLDERS, false);
                SavePaths();
                return;
            }
            //Read values from valid config
            for (int i = 0; i < Path_Values.Length; i++)
            {
                Path_Values[i] = Config_Reader.ReadString();
            }
            Config_Reader.Close();
        }

        /* Set a Path */
        public void SetPath(Paths Path, string NewPath)
        {
            Path_Values[(int)Path] = NewPath;
            SavePaths();
        }

        /* Get a Path */
        public string GetPath(Paths Path)
        {
            return Path_Values[(int)Path];
        }

        /* Save Path Config */
        private void SavePaths()
        {
            BinaryWriter Config_Writer = new BinaryWriter(File.OpenWrite(Path_To_Config));
            Config_Writer.Write(Expected_Config_Version);
            for (int i = 0; i < Path_Values.Length; i++)
            {
                Config_Writer.Write(Path_Values[i]);
            }
            Config_Writer.Close();
        }
    }
}
