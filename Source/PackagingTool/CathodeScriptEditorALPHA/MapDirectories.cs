using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alien_Isolation_Mod_Tools
{
    class MapDirectories
    {
        private static List<string> all_env_dirs = new List<string>();
        static MapDirectories()
        {
            all_env_dirs.Clear();

            all_env_dirs.Add("BSP_LV426_PT01");
            all_env_dirs.Add("BSP_LV426_PT02");
            all_env_dirs.Add("BSP_TORRENS");
            AddIfHasDLC("DLC/BSPNOSTROMO_RIPLEY");
            AddIfHasDLC("DLC/BSPNOSTROMO_TWOTEAMS");
            AddIfHasDLC("DLC/CHALLENGEMAP1");
            AddIfHasDLC("DLC/CHALLENGEMAP3");
            AddIfHasDLC("DLC/CHALLENGEMAP4");
            AddIfHasDLC("DLC/CHALLENGEMAP5");
            AddIfHasDLC("DLC/CHALLENGEMAP7");
            AddIfHasDLC("DLC/CHALLENGEMAP9");
            AddIfHasDLC("DLC/CHALLENGEMAP11");
            AddIfHasDLC("DLC/CHALLENGEMAP12");
            AddIfHasDLC("DLC/CHALLENGEMAP14");
            AddIfHasDLC("DLC/CHALLENGEMAP16");
            AddIfHasDLC("DLC/SALVAGEMODE1");
            AddIfHasDLC("DLC/SALVAGEMODE2");
            all_env_dirs.Add("ENG_ALIEN_NEST");
            all_env_dirs.Add("ENG_REACTORCORE");
            all_env_dirs.Add("ENG_TOWPLATFORM");
            all_env_dirs.Add("FRONTEND");
            all_env_dirs.Add("HAB_AIRPORT");
            all_env_dirs.Add("HAB_CORPORATEPENT");
            all_env_dirs.Add("HAB_SHOPPINGCENTRE");
            all_env_dirs.Add("SCI_ANDROIDLAB");
            all_env_dirs.Add("SCI_HOSPITALLOWER");
            all_env_dirs.Add("SCI_HOSPITALUPPER");
            all_env_dirs.Add("SCI_HUB");
            all_env_dirs.Add("SOLACE");
            all_env_dirs.Add("TECH_COMMS");
            all_env_dirs.Add("TECH_HUB");
            all_env_dirs.Add("TECH_MUTHRCORE");
            all_env_dirs.Add("TECH_RND");
            all_env_dirs.Add("TECH_RND_HZDLAB");
        }
        private static void AddIfHasDLC(string MapName)
        {
            ToolPaths Folders = new ToolPaths();
            if (File.Exists(Folders.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + "/DATA/ENV/PRODUCTION/" + MapName + "/WORLD/COMMANDS.PAK"))
            {
                all_env_dirs.Add(MapName);
            }
        }

        public static List<string> GetAvailable()
        {
            return all_env_dirs;
        }
    }
}
