using Alien_Isolation_Mod_Tools.ayz_Pack_Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Alien_Isolation_Mod_Tools
{
    public partial class Filemanager_ResetMod : Form
    {
        Directories AlienDirectories = new Directories();
        AlienModPack AlienPacker = new AlienModPack();

        public Filemanager_ResetMod()
        {
            InitializeComponent();

            //Load fonts
            PrivateFontCollection ModToolFont = new PrivateFontCollection();
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Isolation.ttf");
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Jixellation.ttf");
            ModToolFont.AddFontFile(AlienDirectories.ToolResourceDirectory() + "Nostromo.ttf");

            //Set fonts & parents
            HeaderText.Font = new Font(ModToolFont.Families[1], 80);
            HeaderText.Parent = HeaderImage;
            Title1.Font = new Font(ModToolFont.Families[0], 20);
            Title2.Font = new Font(ModToolFont.Families[0], 20);
        }

        //Close form
        private void CloseButton_Click(object sender, EventArgs e)
        {
            try { GC.Collect(); GC.WaitForPendingFinalizers(); } catch { }
            Landing_Main LandingForm = new Landing_Main();
            LandingForm.Show();
            this.Hide();
        }

        //reset all
        private void SelectMod_Click(object sender, EventArgs e)
        {
            //reset ui text too
            ResetFiles("ALL");
        }

        //reset individual
        private void ResetGraphics_Click(object sender, EventArgs e)
        {
            ResetFiles("GRAPHICS");
        }
        private void ResetLighting_Click(object sender, EventArgs e)
        {
            ResetFiles("LIGHTING");
        }
        private void ResetAlienConfigs_Click(object sender, EventArgs e)
        {
            ResetFiles("ALIENCONFIGS");
        }
        private void ResetTrees_Click(object sender, EventArgs e)
        {
            ResetFiles("BEHAVIOURS");
        }
        private void ResetDifficulties_Click(object sender, EventArgs e)
        {
            ResetFiles("DIFFICULTIES");
        }
        private void ResetViewconesets_Click(object sender, EventArgs e)
        {
            ResetFiles("VIEWCONES");
        }
        private void ResetAmmo_Click(object sender, EventArgs e)
        {
            ResetFiles("AMMO");
        }
        private void ResetGblItem_Click(object sender, EventArgs e)
        {
            ResetFiles("GBL_ITEM");
        }
        private void ResetChrInfo_Click(object sender, EventArgs e)
        {
            ResetFiles("CHR_INFO");
        }

        //Reset functions
        private void ResetFiles(string toReset)
        {
            try
            {
                DialogResult Confirmation = MessageBox.Show("Are you sure?" + Environment.NewLine + Environment.NewLine + "- This will undo anything you have edited yourself." + Environment.NewLine + "- This will uninstall the selected installed mod files.", "Resetting Files", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

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

                    MessageBox.Show("The requested files have been reset to defaults.", "Reset complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("The requested files were not reset.", "Reset cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch
            {
                MessageBox.Show("An unexpected error occured while resetting." + Environment.NewLine + "Make sure no game files/editors are open.", "An error occured", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void ResetFileBytes(string resetFilePath, byte[] resource)
        {
            File.Delete(AlienDirectories.GameDirectoryRoot() + @"\DATA\" + resetFilePath);
            File.WriteAllBytes(AlienDirectories.GameDirectoryRoot() + @"\DATA\" + resetFilePath, resource);
        }
        private void ResetFileStandard(string resetFilePath, string resource)
        {
            File.Delete(AlienDirectories.GameDirectoryRoot() + @"\DATA\" + resetFilePath);
            File.WriteAllLines(AlienDirectories.GameDirectoryRoot() + @"\DATA\" + resetFilePath, new[] { resource });
        }
    }
}
