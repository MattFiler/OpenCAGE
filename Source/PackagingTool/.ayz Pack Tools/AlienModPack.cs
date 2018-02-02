using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alien_Isolation_Mod_Tools.ayz_Pack_Tools
{
    class AlienModPack
    {
        Directories AlienDirectories = new Directories();

        //Define pack data
        byte[] AlienModPackArray = new byte[2000000]; //~2mb - we should get down to ~899kb when finished (if user includes all files).
        string AlienModManifest = "";
        int PackOffset = 0;


        //---------------------------------------------------------------


        /*
         * Access Level: Public
         * Description: Save a mod to an archive file
         * Return value: True if saved, false if error
        */
        public bool SaveModPack(string ModName, string ModDescription, string ModAuthor, string ModPreviewPicFilepath, string[] CheckboxArray)
        {
            try
            {
                //Shouldn't have 0 - but just in case.
                if (CheckboxArray.Count() == 0)
                {
                    return false;
                }

                //Add default info to manifest
                AlienModManifest = ModName + Environment.NewLine +
                                   ModDescription + Environment.NewLine +
                                   ModAuthor + Environment.NewLine;

                //Read in ENGINE_SETTINGS
                if (CheckboxArray.Contains("ENGINE_SETTINGS")) { AddToPack(AlienDirectories.GameDirectoryRoot() + "/DATA/ENGINE_SETTINGS.XML"); }

                //Read in GBL_ITEM files
                if (CheckboxArray.Contains("GBL_ITEMS"))
                {
                    AddToPack(AlienDirectories.GameDirectoryRoot() + "/DATA/GBL_ITEM.BML");
                    AddToPack(AlienDirectories.GameDirectoryRoot() + "/DATA/GBL_ITEM.XML");
                }

                //Read in generic lighting/shading files
                if (CheckboxArray.Contains("LIGHTING"))
                {
                    AddToPack(AlienDirectories.GameDirectoryRoot() + "/DATA/HAIR_SHADING_SETTINGS.TXT");
                    AddToPack(AlienDirectories.GameDirectoryRoot() + "/DATA/RADIOSITY_SETTINGS.TXT");
                    AddToPack(AlienDirectories.GameDirectoryRoot() + "/DATA/SKIN_SHADING_SETTINGS.TXT");
                }

                //Read in ALIENCONFIGS
                if (CheckboxArray.Contains("ALIENCONFIGS"))
                {
                    foreach (string filename in Directory.GetFiles(AlienDirectories.GameDirectoryRoot() + "/DATA/ALIENCONFIGS/"))
                    {
                        AddToPack(filename);
                    }
                }

                //Read in BINARY_BEHAVIOUR
                if (CheckboxArray.Contains("BEHAVIOR")) { AddToPack(AlienDirectories.GameDirectoryRoot() + "/DATA/BINARY_BEHAVIOR/_DIRECTORY_CONTENTS.BML"); }

                //Read in CHR_INFO
                if (CheckboxArray.Contains("CHR_INFO"))
                {
                    foreach (string filename in Directory.GetFiles(AlienDirectories.GameDirectoryRoot() + "/DATA/CHR_INFO/ATTRIBUTES/"))
                    {
                        AddToPack(filename);
                    }
                }

                //Read in DIFFICULTYSETTINGS
                if (CheckboxArray.Contains("DIFFICULTYSETTINGS"))
                {
                    foreach (string filename in Directory.GetFiles(AlienDirectories.GameDirectoryRoot() + "/DATA/DIFFICULTYSETTINGS/"))
                    {
                        AddToPack(filename);
                    }
                }

                //Read in VIEW_CONE_SETS
                if (CheckboxArray.Contains("VIEW_CONE_SETS"))
                {
                    foreach (string filename in Directory.GetFiles(AlienDirectories.GameDirectoryRoot() + "/DATA/VIEW_CONE_SETS/"))
                    {
                        AddToPack(filename);
                    }
                }

                //Read in WEAPON_INFO
                if (CheckboxArray.Contains("WEAPON_INFO"))
                {
                    foreach (string filename in Directory.GetFiles(AlienDirectories.GameDirectoryRoot() + "/DATA/WEAPON_INFO/AMMO/"))
                    {
                        AddToPack(filename);
                    }
                }

                //Resize array to the right size - don't want a huge pack file :)
                Array.Resize(ref AlienModPackArray, PackOffset);

                //Create directory
                Directory.CreateDirectory(AlienDirectories.ToolModInstallDirectory() + ModName);

                //Save to file
                File.WriteAllBytes(AlienDirectories.ToolModInstallDirectory() + ModName + "/" + ModName + ".ayz", AlienModPackArray);
                File.WriteAllText(AlienDirectories.ToolModInstallDirectory() + ModName + "/" + ModName + "_manifest.ayz", AlienModManifest);

                //Copy preview image
                File.Copy(ModPreviewPicFilepath, AlienDirectories.ToolModInstallDirectory() + ModName + "/" + ModName  + "_preview" + Path.GetExtension(ModPreviewPicFilepath));

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
         * Description: Get mod info before loading
         * Return value: Information array
        */
        public string[] GetModInfo(string ModName)
        {
            string[] ModInfo = { "", "", "" };

            try
            {
                //Read in manifest & split out info
                string ModManifestPath = AlienDirectories.ToolModInstallDirectory() + ModName + "/" + ModName + "_manifest.ayz";
                string[] ModManifest = File.ReadAllLines(ModManifestPath);
                ModInfo[0] = ModManifest[0];
                ModInfo[1] = ModManifest[1];
                ModInfo[2] = ModManifest[2];
            }
            catch
            {
                ModInfo[0] = "Error";
                ModInfo[1] = "Error";
                ModInfo[2] = "Error";
            }

            //Return info
            return ModInfo;
        }


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
                string ModPackPath = AlienDirectories.ToolModInstallDirectory() + ModName + "/" + ModName + ".ayz";
                string ModManifestPath = AlienDirectories.ToolModInstallDirectory() + ModName + "/" + ModName + "_manifest.ayz";

                //Load in main pack and manifest
                byte[] ModPack = File.ReadAllBytes(ModPackPath);
                string[] ModManifest = File.ReadAllLines(ModManifestPath);

                //Read manifest and extract files
                int ManifestCount = 0;
                int ArrayCount = -1;
                bool ManifestDataLoaded = false;
                string ModTitle = "";
                string ModDescription = "";
                string ModIngameDescription = "";
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
                        ModIngameDescription = ManifestLine;
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

                //First up, reset everything to defaults
                ResetFiles("ALL",true);

                //Extract files to game
                for (int i = 0; i < ArrayCount; i++)
                {
                    File.WriteAllBytes(AlienDirectories.GameDirectoryRoot() + "/DATA/" + FilesToExtract[i], File.ReadAllBytes(ModPackPath).Skip(FileStartPositions[i]).Take(FileEndPositions[i] - FileStartPositions[i]).ToArray());
                }

                //Update UI text
                string[] UiText = File.ReadAllLines(AlienDirectories.GameDirectoryRoot() + "/DATA/TEXT/ENGLISH/UI.TXT");
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
                        UiText[LineCounter] = "{"+ModIngameDescription+"}";
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
                File.WriteAllLines(AlienDirectories.GameDirectoryRoot() + "/DATA/TEXT/ENGLISH/UI.TXT", UiText, Encoding.Unicode);

                return true;
            }
            catch
            {
                return false;
            }
        }


        //---------------------------------------------------------------


        /*
         * Access Level: Public
         * Description: Reset the requested files back to default
         * Return value: void
        */
        public void ResetFiles(string toReset, bool isAccessingInFunction)
        {
            try
            {
                DialogResult Confirmation = DialogResult.Yes;

                if (!isAccessingInFunction)
                {
                    Confirmation = MessageBox.Show("Are you sure?" + Environment.NewLine + Environment.NewLine + "- This will undo anything you have edited yourself." + Environment.NewLine + "- This will uninstall the selected installed mod files.", "Resetting Files", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                }

                if (Confirmation == DialogResult.Yes)
                {
                    if (toReset == "GRAPHICS" || toReset == "ALL")
                    {
                        ResetFileStandard("ENGINE_SETTINGS.XML", Properties.Resources.ENGINE_SETTINGS);
                    }

                    if (toReset == "LIGHTING" || toReset == "ALL")
                    {
                        ResetFileStandard("HAIR_SHADING_SETTINGS.TXT", Properties.Resources.HAIR_SHADING_SETTINGS);
                        ResetFileStandard("RADIOSITY_SETTINGS.TXT", Properties.Resources.RADIOSITY_SETTINGS);
                        ResetFileStandard("SKIN_SHADING_SETTINGS.TXT", Properties.Resources.SKIN_SHADING_SETTINGS);
                    }

                    if (toReset == "ALIENCONFIGS" || toReset == "ALL")
                    {
                        ResetFileBytes("ALIENCONFIGS/ALIENCONFIGS.BML", Properties.Resources.ALIENCONFIGS);
                        ResetFileBytes("ALIENCONFIGS/BACKSTAGEALERT.BML", Properties.Resources.BACKSTAGEALERT);
                        ResetFileBytes("ALIENCONFIGS/BACKSTAGEHOLD.BML", Properties.Resources.BACKSTAGEHOLD);
                        ResetFileBytes("ALIENCONFIGS/BACKSTAGEHOLD_MILD.BML", Properties.Resources.BACKSTAGEHOLD_MILD);
                        ResetFileBytes("ALIENCONFIGS/BACKSTAGEHOLD_VCLOSE.BML", Properties.Resources.BACKSTAGEHOLD_VCLOSE);
                        ResetFileBytes("ALIENCONFIGS/BACSTAGEHOLD_CLOSE.BML", Properties.Resources.BACSTAGEHOLD_CLOSE);
                        ResetFileBytes("ALIENCONFIGS/CANTEEN.BML", Properties.Resources.CANTEEN);
                        ResetFileBytes("ALIENCONFIGS/CREWEXPENDABLE_VENT.BML", Properties.Resources.CREWEXPENDABLE_VENT);
                        ResetFileBytes("ALIENCONFIGS/DEFAULT.BML", Properties.Resources.DEFAULT);
                        ResetFileBytes("ALIENCONFIGS/INTENSE.BML", Properties.Resources.INTENSE);
                        ResetFileBytes("ALIENCONFIGS/MILD.BML", Properties.Resources.MILD);
                        ResetFileBytes("ALIENCONFIGS/MODERATE.BML", Properties.Resources.MODERATE);
                        ResetFileBytes("ALIENCONFIGS/MODERATELY_INTENSE.BML", Properties.Resources.MODERATELY_INTENSE);
                    }

                    if (toReset == "BEHAVIOURS" || toReset == "ALL")
                    {
                        ResetFileBytes("BINARY_BEHAVIOR/_DIRECTORY_CONTENTS.BML", Properties.Resources._DIRECTORY_CONTENTS);
                    }

                    if (toReset == "DIFFICULTIES" || toReset == "ALL")
                    {
                        ResetFileBytes("DIFFICULTYSETTINGS/DIFFICULTYSETTINGS.BML", Properties.Resources.DIFFICULTYSETTINGS);
                        ResetFileBytes("DIFFICULTYSETTINGS/EASY.BML", Properties.Resources.EASY);
                        ResetFileBytes("DIFFICULTYSETTINGS/HARD.BML", Properties.Resources.HARD);
                        ResetFileBytes("DIFFICULTYSETTINGS/IRON.BML", Properties.Resources.IRON);
                        ResetFileBytes("DIFFICULTYSETTINGS/MEDIUM.BML", Properties.Resources.MEDIUM);
                        ResetFileBytes("DIFFICULTYSETTINGS/NOVICE.BML", Properties.Resources.NOVICE);
                    }

                    if (toReset == "VIEWCONES" || toReset == "ALL")
                    {
                        ResetFileBytes("VIEW_CONE_SETS/VIEWCONESET_ANDROID.BML", Properties.Resources.VIEWCONESET_ANDROID);
                        ResetFileBytes("VIEW_CONE_SETS/VIEWCONESET_HUMAN.BML", Properties.Resources.VIEWCONESET_HUMAN);
                        ResetFileBytes("VIEW_CONE_SETS/VIEWCONESET_HUMAN_HEIGHTENED.BML", Properties.Resources.VIEWCONESET_HUMAN_HEIGHTENED);
                        ResetFileBytes("VIEW_CONE_SETS/VIEWCONESET_NONE.BML", Properties.Resources.VIEWCONESET_NONE);
                        ResetFileBytes("VIEW_CONE_SETS/VIEWCONESET_SLEEPING.BML", Properties.Resources.VIEWCONESET_SLEEPING);
                        ResetFileBytes("VIEW_CONE_SETS/VIEWCONESET_STANDARD.BML", Properties.Resources.VIEWCONESET_STANDARD);
                        ResetFileBytes("VIEW_CONE_SETS/VIEWCONESETS.BML", Properties.Resources.VIEWCONESETS);
                    }

                    if (toReset == "AMMO" || toReset == "ALL")
                    {
                        ResetFileBytes("WEAPON_INFO/AMMO/ACID_BURST_LARGE.BML", Properties.Resources.ACID_BURST_LARGE);
                        ResetFileBytes("WEAPON_INFO/AMMO/ACID_BURST_SMALL.BML", Properties.Resources.ACID_BURST_SMALL);
                        ResetFileBytes("WEAPON_INFO/AMMO/AMMOTYPES.BML", Properties.Resources.AMMOTYPES);
                        ResetFileBytes("WEAPON_INFO/AMMO/BOLTGUN_NORMAL.BML", Properties.Resources.BOLTGUN_NORMAL);
                        ResetFileBytes("WEAPON_INFO/AMMO/CATALYST_FIRE_LARGE.BML", Properties.Resources.CATALYST_FIRE_LARGE);
                        ResetFileBytes("WEAPON_INFO/AMMO/CATALYST_FIRE_SMALL.BML", Properties.Resources.CATALYST_FIRE_SMALL);
                        ResetFileBytes("WEAPON_INFO/AMMO/CATALYST_HE_LARGE.BML", Properties.Resources.CATALYST_HE_LARGE);
                        ResetFileBytes("WEAPON_INFO/AMMO/CATALYST_HE_SMALL.BML", Properties.Resources.CATALYST_HE_SMALL);
                        ResetFileBytes("WEAPON_INFO/AMMO/CATTLEPROD_POWERPACK.BML", Properties.Resources.CATTLEPROD_POWERPACK);
                        ResetFileBytes("WEAPON_INFO/AMMO/EMP_BURST_LARGE.BML", Properties.Resources.EMP_BURST_LARGE);
                        ResetFileBytes("WEAPON_INFO/AMMO/EMP_BURST_LARGE_TIER2.BML", Properties.Resources.EMP_BURST_LARGE_TIER2);
                        ResetFileBytes("WEAPON_INFO/AMMO/EMP_BURST_LARGE_TIER3.BML", Properties.Resources.EMP_BURST_LARGE_TIER3);
                        ResetFileBytes("WEAPON_INFO/AMMO/EMP_BURST_SMALL.BML", Properties.Resources.EMP_BURST_SMALL);
                        ResetFileBytes("WEAPON_INFO/AMMO/ENVIRONMENT_FLAME.BML", Properties.Resources.ENVIRONMENT_FLAME);
                        ResetFileBytes("WEAPON_INFO/AMMO/FLAMETHROWER_AERATED.BML", Properties.Resources.FLAMETHROWER_AERATED);
                        ResetFileBytes("WEAPON_INFO/AMMO/FLAMETHROWER_HIGH_DAMAGE.BML", Properties.Resources.FLAMETHROWER_HIGH_DAMAGE);
                        ResetFileBytes("WEAPON_INFO/AMMO/FLAMETHROWER_NORMAL.BML", Properties.Resources.FLAMETHROWER_NORMAL);
                        ResetFileBytes("WEAPON_INFO/AMMO/GRENADE_FIRE.BML", Properties.Resources.GRENADE_FIRE);
                        ResetFileBytes("WEAPON_INFO/AMMO/GRENADE_FIRE_TIER2.BML", Properties.Resources.GRENADE_FIRE_TIER2);
                        ResetFileBytes("WEAPON_INFO/AMMO/GRENADE_FIRE_TIER3.BML", Properties.Resources.GRENADE_FIRE_TIER3);
                        ResetFileBytes("WEAPON_INFO/AMMO/GRENADE_HE.BML", Properties.Resources.GRENADE_HE);
                        ResetFileBytes("WEAPON_INFO/AMMO/GRENADE_HE_TIER2.BML", Properties.Resources.GRENADE_HE_TIER2);
                        ResetFileBytes("WEAPON_INFO/AMMO/GRENADE_HE_TIER3.BML", Properties.Resources.GRENADE_HE_TIER3);
                        ResetFileBytes("WEAPON_INFO/AMMO/GRENADE_SMOKE.BML", Properties.Resources.GRENADE_SMOKE);
                        ResetFileBytes("WEAPON_INFO/AMMO/GRENADE_STUN.BML", Properties.Resources.GRENADE_STUN);
                        ResetFileBytes("WEAPON_INFO/AMMO/GRENADE_STUN_TIER2.BML", Properties.Resources.GRENADE_STUN_TIER2);
                        ResetFileBytes("WEAPON_INFO/AMMO/GRENADE_STUN_TIER3.BML", Properties.Resources.GRENADE_STUN_TIER3);
                        ResetFileBytes("WEAPON_INFO/AMMO/IMPACT.BML", Properties.Resources.IMPACT);
                        ResetFileBytes("WEAPON_INFO/AMMO/MELEE_CROW_AXE.BML", Properties.Resources.MELEE_CROW_AXE);
                        ResetFileBytes("WEAPON_INFO/AMMO/PISTOL_DUM_DUM.BML", Properties.Resources.PISTOL_DUM_DUM);
                        ResetFileBytes("WEAPON_INFO/AMMO/PISTOL_NORMAL.BML", Properties.Resources.PISTOL_NORMAL);
                        ResetFileBytes("WEAPON_INFO/AMMO/PISTOL_NORMAL_NPC.BML", Properties.Resources.PISTOL_NORMAL_NPC);
                        ResetFileBytes("WEAPON_INFO/AMMO/PISTOL_TAZER.BML", Properties.Resources.PISTOL_TAZER);
                        ResetFileBytes("WEAPON_INFO/AMMO/PUSH.BML", Properties.Resources.PUSH);
                        ResetFileBytes("WEAPON_INFO/AMMO/SHOTGUN_INCENDIARY.BML", Properties.Resources.SHOTGUN_INCENDIARY);
                        ResetFileBytes("WEAPON_INFO/AMMO/SHOTGUN_NORMAL.BML", Properties.Resources.SHOTGUN_NORMAL);
                        ResetFileBytes("WEAPON_INFO/AMMO/SHOTGUN_NORMAL_NPC.BML", Properties.Resources.SHOTGUN_NORMAL_NPC);
                        ResetFileBytes("WEAPON_INFO/AMMO/SHOTGUN_SLUG.BML", Properties.Resources.SHOTGUN_SLUG);
                        ResetFileBytes("WEAPON_INFO/AMMO/SMG_DUM_DUM.BML", Properties.Resources.SMG_DUM_DUM);
                        ResetFileBytes("WEAPON_INFO/AMMO/SMG_NORMAL.BML", Properties.Resources.SMG_NORMAL);
                    }

                    if (toReset == "GBL_ITEM" || toReset == "ALL")
                    {
                        ResetFileStandard("GBL_ITEM.XML", Properties.Resources.GBL_ITEM1);
                        ResetFileBytes("GBL_ITEM.BML", Properties.Resources.GBL_ITEM);
                    }

                    if (toReset == "CHR_INFO" || toReset == "ALL")
                    {
                        ResetFileBytes("CHR_INFO/ATTRIBUTES/ALIEN.BML", Properties.Resources.ALIEN);
                        ResetFileBytes("CHR_INFO/ATTRIBUTES/ANDROID.BML", Properties.Resources.ANDROID);
                        ResetFileBytes("CHR_INFO/ATTRIBUTES/ANDROID_HEAVY.BML", Properties.Resources.ANDROID_HEAVY);
                        ResetFileBytes("CHR_INFO/ATTRIBUTES/ATTRIBUTES.BML", Properties.Resources.ATTRIBUTES);
                        ResetFileBytes("CHR_INFO/ATTRIBUTES/BASE_HUMAN.BML", Properties.Resources.BASE_HUMAN);
                        ResetFileBytes("CHR_INFO/ATTRIBUTES/CIVILIAN.BML", Properties.Resources.CIVILIAN);
                        ResetFileBytes("CHR_INFO/ATTRIBUTES/CUTSCENE.BML", Properties.Resources.CUTSCENE);
                        ResetFileBytes("CHR_INFO/ATTRIBUTES/CUTSCENE_ANDROID.BML", Properties.Resources.CUTSCENE_ANDROID);
                        ResetFileBytes("CHR_INFO/ATTRIBUTES/DEFAULTS.BML", Properties.Resources.DEFAULTS);
                        ResetFileBytes("CHR_INFO/ATTRIBUTES/FACEHUGGER.BML", Properties.Resources.FACEHUGGER);
                        ResetFileBytes("CHR_INFO/ATTRIBUTES/INNOCENT.BML", Properties.Resources.INNOCENT);
                        ResetFileBytes("CHR_INFO/ATTRIBUTES/MELEE_HUMAN.BML", Properties.Resources.MELEE_HUMAN);
                        ResetFileBytes("CHR_INFO/ATTRIBUTES/RIOT_GUARD.BML", Properties.Resources.RIOT_GUARD);
                        ResetFileBytes("CHR_INFO/ATTRIBUTES/SECURITY_GUARD.BML", Properties.Resources.SECURITY_GUARD);
                        ResetFileBytes("CHR_INFO/ATTRIBUTES/SPACESUIT_NPC.BML", Properties.Resources.SPACESUIT_NPC);
                        ResetFileBytes("CHR_INFO/ATTRIBUTES/THE_PLAYER.BML", Properties.Resources.THE_PLAYER);
                    }

                    if (!isAccessingInFunction)
                    {
                        //Reset UI text
                        string[] UiText = File.ReadAllLines(AlienDirectories.GameDirectoryRoot() + "/DATA/TEXT/ENGLISH/UI.TXT");
                        bool HasFoundPressToStart = false;
                        bool HasFoundPressToStartKeyboard = false;
                        bool HasFoundPlayGameDesc = false;
                        int LineCounter = 0;
                        foreach (string UiLine in UiText)
                        {
                            //Update attribute if found
                            if (HasFoundPressToStart)
                            {
                                UiText[LineCounter] = "{Press @confirm@}";
                                HasFoundPressToStart = false;
                            }
                            if (HasFoundPressToStartKeyboard)
                            {
                                UiText[LineCounter] = "{Press any key}";
                                HasFoundPressToStartKeyboard = false;
                            }
                            if (HasFoundPlayGameDesc)
                            {
                                UiText[LineCounter] = "{Select Game Mode}";
                                HasFoundPlayGameDesc = false;
                            }

                            //Has found attribute
                            if (UiLine == "[AI_UI_FRONTEND_PRESS_TO_START]")
                            {
                                HasFoundPressToStart = true;
                            }
                            if (UiLine == "[AI_UI_FRONTEND_PRESS_TO_START_KB]")
                            {
                                HasFoundPressToStartKeyboard = true;
                            }
                            if (UiLine == "[AI_UI_FRONTEND_PLAY_GAME_DESC]")
                            {
                                HasFoundPlayGameDesc = true;
                            }

                            LineCounter++;
                        }
                        File.WriteAllLines(AlienDirectories.GameDirectoryRoot() + "/DATA/TEXT/ENGLISH/UI.TXT", UiText, Encoding.Unicode);

                        MessageBox.Show("The requested files have been reset to defaults.", "Reset complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    if (!isAccessingInFunction)
                    {
                        MessageBox.Show("The requested files were not reset.", "Reset cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch
            {
                if (!isAccessingInFunction)
                {
                    MessageBox.Show("An unexpected error occured while resetting." + Environment.NewLine + "Make sure no game files/editors are open.", "An error occured", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }


        /*
         * Access Level: Private
         * Description: Reset the requested file to the requested resource in bytes
         * Return value: void
        */
        private void ResetFileBytes(string resetFilePath, byte[] resource)
        {
            File.Delete(AlienDirectories.GameDirectoryRoot() + @"\DATA\" + resetFilePath);
            File.WriteAllBytes(AlienDirectories.GameDirectoryRoot() + @"\DATA\" + resetFilePath, resource);
        }


        /*
         * Access Level: Private
         * Description: Reset the requested file to the requested resource as a string
         * Return value: void
        */
        private void ResetFileStandard(string resetFilePath, string resource)
        {
            File.Delete(AlienDirectories.GameDirectoryRoot() + @"\DATA\" + resetFilePath);
            File.WriteAllLines(AlienDirectories.GameDirectoryRoot() + @"\DATA\" + resetFilePath, new[] { resource });
        }
    }
}
