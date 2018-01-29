using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Alien_Isolation_Mod_Tools.ayz_Pack_Tools
{
    class AlienModPack
    {
        //Main Directories
        string gameDirectory = File.ReadAllText(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz"); //Our game's dir

        //Define pack data
        byte[] AlienModPackArray = new byte[2000000]; //~2mb - we should get down to ~888kb when finished.
        string AlienModManifest = "";
        int PackOffset = 0;


        //---------------------------------------------------------------


        /*
         * Access Level: Public
         * Description: Save a mod to an archive file
         * Return value: True if saved, false if error
        */
        public bool SaveModPack(string ModName, string ModDescription, string ModAuthor)
        {
            try
            {
                //Add default info to manifest
                AlienModManifest = ModName + Environment.NewLine +
                                   ModDescription + Environment.NewLine +
                                   ModAuthor + Environment.NewLine;

                //Read in ENGINE_SETTINGS
                AddToPack(gameDirectory + "/DATA/ENGINE_SETTINGS.XML");

                //Read in GBL_ITEM files
                AddToPack(gameDirectory + "/DATA/GBL_ITEM.BML");
                AddToPack(gameDirectory + "/DATA/GBL_ITEM.XML");

                //Read in generic lighting/shading files
                AddToPack(gameDirectory + "/DATA/HAIR_SHADING_SETTINGS.TXT");
                AddToPack(gameDirectory + "/DATA/RADIOSITY_SETTINGS.TXT");
                AddToPack(gameDirectory + "/DATA/SKIN_SHADING_SETTINGS.TXT");

                //Read in ALIENCONFIGS
                foreach (string filename in Directory.GetFiles(gameDirectory + "/DATA/ALIENCONFIGS/"))
                {
                    AddToPack(filename);
                }

                //Read in BINARY_BEHAVIOUR
                AddToPack(gameDirectory + "/DATA/BINARY_BEHAVIOR/_DIRECTORY_CONTENTS.BML");

                //Read in CHR_INFO
                foreach (string filename in Directory.GetFiles(gameDirectory + "/DATA/CHR_INFO/ATTRIBUTES/"))
                {
                    AddToPack(filename);
                }

                //Read in DIFFICULTYSETTINGS
                foreach (string filename in Directory.GetFiles(gameDirectory + "/DATA/DIFFICULTYSETTINGS/"))
                {
                    AddToPack(filename);
                }

                //Read in VIEW_CONE_SETS
                foreach (string filename in Directory.GetFiles(gameDirectory + "/DATA/VIEW_CONE_SETS/"))
                {
                    AddToPack(filename);
                }

                //Read in WEAPON_INFO
                foreach (string filename in Directory.GetFiles(gameDirectory + "/DATA/WEAPON_INFO/AMMO/"))
                {
                    AddToPack(filename);
                }

                //Resize array to the right size - don't want a huge pack file :)
                Array.Resize(ref AlienModPackArray, PackOffset);

                //Create directory
                Directory.CreateDirectory(gameDirectory + "/DATA/MODS/" + ModName);

                //Save to file
                File.WriteAllBytes(gameDirectory + "/DATA/MODS/" + ModName + "/" + ModName + ".ayz", AlienModPackArray);
                File.WriteAllText(gameDirectory + "/DATA/MODS/" + ModName + "/" + ModName + "_manifest.ayz", AlienModManifest);

                return true;
            }
            catch
            {
                return false;
            }
        }


        /*
         * Access Level: Private
         * Description: Add file to mod tool pack byte array
         * Return value: null
        */
        private void AddToPack(string filename)
        {
            byte[] currentFile = File.ReadAllBytes(filename); //Load in current file bytes
            currentFile.CopyTo(AlienModPackArray, PackOffset); //Copy byte contents of file to pack
            AlienModManifest += filename.Split(new[] { "/DATA/" }, StringSplitOptions.None)[1] + Environment.NewLine + PackOffset; //Write filename and start position to manifest
            PackOffset += currentFile.Length; //Update offset
            AlienModManifest += Environment.NewLine + PackOffset + Environment.NewLine; //Write end position to manifest
        }


        //---------------------------------------------------------------


        /*
         * Access Level: Public
         * Description: Load a mod from an archive file
         * Return value: True if loaded, false if error
        */
        public bool LoadModPack(string ModName)
        {
            try
            {
                //Get filepaths to mod files
                string ModPackPath = gameDirectory + "/DATA/MODS/" + ModName + "/" + ModName + ".ayz";
                string ModManifestPath = gameDirectory + "/DATA/MODS/" + ModName + "/" + ModName + "_manifest.ayz";

                //Load in main pack and manifest
                byte[] ModPack = File.ReadAllBytes(ModPackPath);
                string[] ModManifest = File.ReadAllLines(ModManifestPath);

                //Read manifest and extract files
                int ManifestCount = 0;
                int ArrayCount = -1;
                bool ManifestDataLoaded = false;
                string ModTitle = "";
                string ModDescription = "";
                string ModAuthor = "";
                string[] FilesToExtract = new string[999];
                int[] FileStartPositions = new int[999];
                int[] FileEndPositions = new int[999];
                foreach (string ManifestLine in ModManifest)
                {
                    //Load Mod Data
                    if (ManifestCount == 0 && !ManifestDataLoaded)
                    {
                        ModTitle = ManifestLine;
                    } else if (ManifestCount == 1 && !ManifestDataLoaded)
                    {
                        ModDescription = ManifestLine;
                    } else if (ManifestCount == 2 && !ManifestDataLoaded)
                    {
                        ModAuthor = ManifestLine;
                        ManifestDataLoaded = true;
                        ManifestCount = -1;
                    }

                    //Load File Data
                    if (ManifestCount == 0 && ManifestDataLoaded)
                    {
                        ArrayCount++;
                        FilesToExtract[ArrayCount] = ManifestLine;
                    } else if (ManifestCount == 1 && ManifestDataLoaded)
                    {
                        FileStartPositions[ArrayCount] = Convert.ToInt32(ManifestLine);
                    } else if (ManifestCount == 2 && ManifestDataLoaded)
                    {
                        FileEndPositions[ArrayCount] = Convert.ToInt32(ManifestLine);
                        ManifestCount = -1;
                    }

                    ManifestCount++;
                }

                //Resize file arrays to correct count
                Array.Resize(ref FilesToExtract, ArrayCount);
                Array.Resize(ref FileStartPositions, ArrayCount);
                Array.Resize(ref FileEndPositions, ArrayCount);

                //Extract files to game
                for (int i = 0; i < ArrayCount; i++)
                {
                    File.WriteAllBytes(gameDirectory + "/DATA/" + FilesToExtract[i], File.ReadAllBytes(ModPackPath).Skip(FileStartPositions[i]).Take(FileEndPositions[i] - FileStartPositions[i]).ToArray());
                }

                //Update UI text
                string[] UiText = File.ReadAllLines(gameDirectory + "/DATA/TEXT/ENGLISH/UI.TXT");
                bool HasFoundPressToStart = false;
                bool HasFoundPressToStartKeyboard = false;
                bool HasFoundPlayGameDesc = false;
                int LineCounter = 0;
                foreach (string UiLine in UiText)
                {
                    //Update attribute if found
                    if (HasFoundPressToStart || HasFoundPressToStartKeyboard)
                    {
                        UiText[LineCounter] = "{Running mod: \"" + ModName + "\"}";
                        if (HasFoundPressToStart) { HasFoundPressToStart = false; } else { HasFoundPressToStartKeyboard = false; }
                    }
                    if (HasFoundPlayGameDesc)
                    {
                        UiText[LineCounter] = "{"+ModDescription+"}";
                        HasFoundPlayGameDesc = false;
                    }

                    //Has found attribute
                    if (UiLine == "[AI_UI_FRONTEND_PRESS_TO_START]")
                    {
                        HasFoundPressToStart = true;
                    }
                    if (UiLine == "[AI_UI_FRONTEND_PRESS_TO_START_KB]")
                    {
                        HasFoundPressToStart = true;
                    }
                    if (UiLine == "[AI_UI_FRONTEND_PLAY_GAME_DESC]")
                    {
                        HasFoundPlayGameDesc = true;
                    }

                    LineCounter++;
                }
                File.WriteAllLines(gameDirectory + "/DATA/TEXT/ENGLISH/UI.TXT", UiText, Encoding.Unicode);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
