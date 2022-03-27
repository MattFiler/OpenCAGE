using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            if (MapName == "Frontend")
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
                writer.Write(mapStringByteArray);
                writer.Close();
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
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
