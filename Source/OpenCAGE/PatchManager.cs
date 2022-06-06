using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace OpenCAGE
{
    public class PatchManager
    {
        /* Patch the AI binary to circumvent FILE_HASHES::verify_integrity */
        public static bool PatchFileIntegrityCheck()
        {
            List<PatchBytes> hashPatches = new List<PatchBytes>();
            switch (SettingsManager.GetString("META_GameVersion"))
            {
                case "STEAM":
                    hashPatches.Add(new PatchBytes(4043685, new byte[] { 0xf7, 0x8c, 0xf9, 0xff }, new byte[] { 0x67, 0x28, 0x25, 0x00 }));
                    hashPatches.Add(new PatchBytes(6193062, new byte[] { 0xf6, 0xc0, 0xd8, 0xff }, new byte[] { 0x66, 0x5c, 0x04, 0x00 }));
                    break;
                case "EPIC_GAMES_STORE":
                    hashPatches.Add(new PatchBytes(4113398, new byte[] { 0x2d, 0x19, 0x00 }, new byte[] { 0x2d, 0x19, 0x00 }));
                    hashPatches.Add(new PatchBytes(5481335, new byte[] { 0xa9, 0xe4, 0xff }, new byte[] { 0x4e, 0x04, 0x00 }));
                    break;
            }
            try
            {
                BinaryWriter writer = new BinaryWriter(File.OpenWrite(SettingsManager.GetString("PATH_GameRoot") + "/AI.exe"));
                for (int i = 0; i < hashPatches.Count; i++)
                {
                    writer.BaseStream.Position = hashPatches[i].offset;
                    writer.Write(hashPatches[i].patched);
                }
                writer.Close();
                return true;
            }
            catch (Exception ex) 
            {
                return false;
            }
        }

        /* Patch the instruction to set Mem_Replay_Logs logging in game binary */
        public static bool PatchMemReplayLogFlag(bool shouldLog)
        {
            List<PatchBytes> memReplayPatches = new List<PatchBytes>();
            switch (SettingsManager.GetString("META_GameVersion"))
            {
                case "STEAM":
                    memReplayPatches.Add(new PatchBytes(4039327, new byte[] { 0xcd, 0x4c, 0x53 }, new byte[] { 0x6d, 0x39, 0x25 }));
                    break;
                case "EPIC_GAMES_STORE":
                    memReplayPatches.Add(new PatchBytes(4109007, new byte[] { 0x6d, 0xbe, 0x5c }, new byte[] { 0xed, 0x3e, 0x19 }));
                    break;
            }
            try
            {
                BinaryWriter writer = new BinaryWriter(File.OpenWrite(SettingsManager.GetString("PATH_GameRoot") + "/AI.exe"));
                for (int i = 0; i < memReplayPatches.Count; i++)
                {
                    writer.BaseStream.Position = memReplayPatches[i].offset;
                    if (shouldLog) writer.Write(memReplayPatches[i].patched);
                    else writer.Write(memReplayPatches[i].original);
                }
                writer.Close();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /* Patch the cUI UI perf stats flag in the game binary */
        public static bool PatchUIPerfFlag(bool shouldShow)
        {
            try
            {
                BinaryWriter writer = new BinaryWriter(File.OpenWrite(SettingsManager.GetString("PATH_GameRoot") + "/AI.exe"));
                switch (SettingsManager.GetString("META_GameVersion"))
                {
                    case "STEAM":
                        writer.BaseStream.Position = 4430526;
                        break;
                    case "EPIC_GAMES_STORE":
                        writer.BaseStream.Position = 4500590;
                        break;
                }
                writer.Write((shouldShow) ? (byte)0x01 : (byte)0x00);
                writer.Close();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /* Patch the game binary to allow us to launch directly to a map */
        public static bool PatchLaunchMode(string MapName = "Frontend")
        {
            //This is the level the benchmark function loads into - we can overwrite it to change
            byte[] mapStringByteArray = { 0x54, 0x45, 0x43, 0x48, 0x5F, 0x52, 0x4E, 0x44, 0x5F, 0x48, 0x5A, 0x44, 0x4C, 0x41, 0x42, 0x00, 0x00, 0x65, 0x6E, 0x67, 0x69, 0x6E, 0x65, 0x5F, 0x73, 0x65, 0x74, 0x74, 0x69, 0x6E, 0x67, 0x73 };

            //These are the original/edited setters in the benchmark function to enable benchmark mode - if we're just loading a level, we want to change them
            List<PatchBytes> benchmarkPatches = new List<PatchBytes>();
            switch (SettingsManager.GetString("META_GameVersion"))
            {
                case "STEAM":
                    benchmarkPatches.Add(new PatchBytes(3842041, new byte[] { 0xe3, 0x48, 0x26 }, new byte[] { 0x13, 0x3c, 0x28 }));
                    benchmarkPatches.Add(new PatchBytes(3842068, new byte[] { 0xce, 0x0c, 0x6f }, new byte[] { 0x26, 0x0f, 0x64 }));
                    benchmarkPatches.Add(new PatchBytes(3842146, new byte[] { 0xcb, 0x0c, 0x6f }, new byte[] { 0x26, 0x0f, 0x64 }));
                    break;
                case "EPIC_GAMES_STORE":
                    benchmarkPatches.Add(new PatchBytes(3911321, new byte[] { 0x13, 0x5f, 0x1a }, new byte[] { 0x23, 0x43, 0x1c }));
                    benchmarkPatches.Add(new PatchBytes(3911348, new byte[] { 0xee, 0xd1, 0x70 }, new byte[] { 0xe6, 0xce, 0x65 }));
                    benchmarkPatches.Add(new PatchBytes(3911426, new byte[] { 0xeb, 0xd1, 0x70 }, new byte[] { 0xe6, 0xce, 0x65 }));
                    break;
            }

            //Frontend acts as a reset
            bool shouldPatch = true;
            if (MapName.ToUpper() == "FRONTEND")
            {
                MapName = "Tech_RnD_HzdLab";
                shouldPatch = false;
            }

            //Update vanilla byte array with selection
            for (int i = 0; i < MapName.Length; i++)
            {
                mapStringByteArray[i] = (byte)MapName[i];
            }
            mapStringByteArray[MapName.Length] = 0x00;

            //Edit game EXE with selected option & hack out the benchmark mode
            try
            {
                BinaryWriter writer = new BinaryWriter(File.OpenWrite(SettingsManager.GetString("PATH_GameRoot") + "/AI.exe"));
                for (int i = 0; i < benchmarkPatches.Count; i++)
                {
                    writer.BaseStream.Position = benchmarkPatches[i].offset;
                    if (shouldPatch) writer.Write(benchmarkPatches[i].patched);
                    else writer.Write(benchmarkPatches[i].original);
                }
                switch (SettingsManager.GetString("META_GameVersion"))
                {
                    case "STEAM":
                        writer.BaseStream.Position = 15676275;
                        break;
                    case "EPIC_GAMES_STORE":
                        writer.BaseStream.Position = 15773411;
                        break;
                }
                if (writer.BaseStream.Position != 0)
                    writer.Write(mapStringByteArray);
                writer.Close();
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
        
        /* Update the list of levels in PACKAGES MAIN.PKG to account for any custom levels */
        public static void UpdateLevelListInPackages()
        {
            if (SettingsManager.GetString("META_GameVersion") != "STEAM" && SettingsManager.GetString("META_GameVersion") != "EPIC_GAMES_STORE") return;

            string pathToPackages = SettingsManager.GetString("PATH_GameRoot") + "/DATA/PACKAGES/MAIN.PKG";
            XDocument packagesXML = XDocument.Load(pathToPackages);
            XElement levelsRootNode = packagesXML.XPathSelectElement("//package/game_data/levels");
            levelsRootNode.RemoveNodes();
            foreach (string file in Directory.GetFiles(SettingsManager.GetString("PATH_GameRoot") + "/DATA/ENV/PRODUCTION/", "COMMANDS.PAK", SearchOption.AllDirectories))
            {
                string[] fileSplit = file.Split(new[] { "PRODUCTION" }, StringSplitOptions.None);
                string mapName = fileSplit[fileSplit.Length - 1].Substring(1, fileSplit[fileSplit.Length - 1].Length - 20);

                //Ignore maps included in the base game or other PKGs
                if (mapName.ToUpper() == "BSP_LV426_PT01" ||
                    mapName.ToUpper() == "BSP_LV426_PT02" ||
                    mapName.ToUpper() == "BSP_TORRENS" ||
                    mapName.ToUpper() == @"DLC\BSPNOSTROMO_RIPLEY" ||
                    mapName.ToUpper() == @"DLC\BSPNOSTROMO_RIPLEY_PATCH" ||
                    mapName.ToUpper() == @"DLC\BSPNOSTROMO_TWOTEAMS" ||
                    mapName.ToUpper() == @"DLC\BSPNOSTROMO_TWOTEAMS_PATCH" ||
                    mapName.ToUpper() == @"DLC\CHALLENGEMAP1" ||
                    mapName.ToUpper() == @"DLC\CHALLENGEMAP11" ||
                    mapName.ToUpper() == @"DLC\CHALLENGEMAP12" ||
                    mapName.ToUpper() == @"DLC\CHALLENGEMAP14" ||
                    mapName.ToUpper() == @"DLC\CHALLENGEMAP3" ||
                    mapName.ToUpper() == @"DLC\CHALLENGEMAP4" ||
                    mapName.ToUpper() == @"DLC\CHALLENGEMAP5" ||
                    mapName.ToUpper() == @"DLC\CHALLENGEMAP7" ||
                    mapName.ToUpper() == @"DLC\CHALLENGEMAP9" ||
                    mapName.ToUpper() == @"DLC\SALVAGEMODE1" ||
                    mapName.ToUpper() == @"DLC\SALVAGEMODE2" ||
                    mapName.ToUpper() == "ENG_ALIEN_NEST" ||
                    mapName.ToUpper() == "ENG_REACTORCORE" ||
                    mapName.ToUpper() == "ENG_TOWPLATFORM" ||
                    mapName.ToUpper() == "HAB_AIRPORT" ||
                    mapName.ToUpper() == "HAB_CORPORATEPENT" ||
                    mapName.ToUpper() == "HAB_SHOPPINGCENTRE" ||
                    mapName.ToUpper() == "SCI_ANDROIDLAB" ||
                    mapName.ToUpper() == "SCI_HOSPITALLOWER" ||
                    mapName.ToUpper() == "SCI_HOSPITALUPPER" ||
                    mapName.ToUpper() == "SCI_HUB" ||
                    mapName.ToUpper() == "SOLACE" ||
                    mapName.ToUpper() == "TECH_COMMS" ||
                    mapName.ToUpper() == "TECH_HUB" ||
                    mapName.ToUpper() == "TECH_MUTHRCORE" ||
                    mapName.ToUpper() == "TECH_RND" ||
                    mapName.ToUpper() == "TECH_RND_HZDLAB")
                    continue;

                levelsRootNode.Add(XElement.Parse("<level id=\"Production\\" + mapName + "\" path=\"data\\ENV\\Production\\" + mapName + "\" />"));
            }
            File.WriteAllText(pathToPackages, packagesXML.ToString());
        }

        struct PatchBytes
        {
            public PatchBytes(int _o, byte[] _orig, byte[] _patch)
            {
                offset = _o;
                original = _orig;
                patched = _patch;
            }
            public int offset;
            public byte[] original;
            public byte[] patched;
        }
    }
}
