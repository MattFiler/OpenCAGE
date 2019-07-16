using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alien_Isolation_Mod_Tools
{
    class Directories
    {
        private string GameDirectory = "";
        private string BrainiacDirectory = "";
        public Directories()
        {
            try
            {
                string[] ModToolLocales = File.ReadAllLines(AppDomain.CurrentDomain.BaseDirectory + @"\modtools_locales.ayz");
                GameDirectory = ModToolLocales[0];
                BrainiacDirectory = ModToolLocales[1];
            }
            catch { }
        }

        //Return behaviour tree directory location
        public string ToolTreeDirectory()
        {
            return GameDirectory + "/DATA/MODTOOLS/BEHAVIOUR_TREES/";
        }

        //Return working directory location
        public string ToolWorkingDirectory()
        {
            return GameDirectory + "/DATA/MODTOOLS/WORKING_FILES/";
        }

        //Return resources directory location
        public string ToolResourceDirectory()
        {
            return GameDirectory + "/DATA/MODTOOLS/RESOURCES/";
        }

        //Return mod directory location
        public string ToolModInstallDirectory()
        {
            return GameDirectory + "/DATA/MODS/";
        }

        //Return game directory location
        public string GameDirectoryRoot()
        {
            return GameDirectory;
        }

        //Return Brainiac directory location
        public string BrainiacDirectoryRoot()
        {
            return BrainiacDirectory;
        }
    }
}
