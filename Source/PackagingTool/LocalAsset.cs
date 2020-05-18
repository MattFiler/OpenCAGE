using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alien_Isolation_Mod_Tools
{
    class LocalAsset
    {
        private static ToolPaths paths = new ToolPaths();
        public static byte[] GetAsBytes(string subSet, string assetName)
        {
            return File.ReadAllBytes(paths.GetPath(ToolPaths.Paths.FOLDER_ASSETS) + "/" + subSet + "/" + assetName);
        }
        public static string GetAsString(string subSet, string assetName)
        {
            return File.ReadAllText(paths.GetPath(ToolPaths.Paths.FOLDER_ASSETS) + "/" + subSet + "/" + assetName);
        }
    }
}
