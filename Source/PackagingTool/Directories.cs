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
        public Directories()
        {
            try { GameDirectory = File.ReadAllText(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz"); } catch { }
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
    }
}
