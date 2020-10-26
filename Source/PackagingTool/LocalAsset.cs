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
            return File.ReadAllBytes(GetPathString(subSet, assetName));
        }
        public static string GetAsString(string subSet, string assetName)
        {
            return File.ReadAllText(GetPathString(subSet, assetName));
        }
        public static string GetPathString(string subSet, string assetName)
        {
            if (File.Exists("LOCAL_FILES")) return AppDomain.CurrentDomain.BaseDirectory + "Source/PackagingTool/" + subSet + "/" + assetName;
            return paths.GetPath(ToolPaths.Paths.FOLDER_ASSETS) + "/" + subSet + "/" + assetName;
        }
    }
}
