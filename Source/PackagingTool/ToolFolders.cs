using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alien_Isolation_Mod_Tools
{
    class ToolFolders
    {
        private string Path_To_Folders_Config = AppDomain.CurrentDomain.BaseDirectory + @"\modtools_locales.ayz";

        /* 
         * Folder History:
         *  16/08/19 - V1:
         *      - Game Directory / Brainiac Directory
         */
        private Int16 Expected_Folder_Config_Version = 1;
        public enum Folders
        {
            FOLDER_GAME_DIRECTORY,
            FOLDER_BRAINIAC_DIRECTORY
        };
        private string[] Folder_Values =
        {
            "",
            ""
        };

        /* TODO */
    }
}
