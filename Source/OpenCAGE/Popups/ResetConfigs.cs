using CATHODE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace CommandsEditor.Popups
{
    public partial class ResetConfigs : Form
    {
        PAK2 _backupFiles;

        public ResetConfigs()
        {
            InitializeComponent();

            using (MemoryStream stream = new MemoryStream())
            using (GZipStream compressedStream = new GZipStream(new MemoryStream(Properties.Resources.config_backups), CompressionMode.Decompress))
            {
                compressedStream.CopyTo(stream);
                _backupFiles = new PAK2(stream.ToArray());
            }
        }

        private void ResetFile(string file)
        {
            File.WriteAllBytes(Singleton.PathToAI + "/DATA/" + file, _backupFiles.Entries.FirstOrDefault(o => o.Filename == file).Content);
        }

        private void resetGblItem_Click(object sender, EventArgs e)
        {
            Singleton.OnResetConfigs?.Invoke();
            EditorUtils.CloseAI();
            ResetFile("GBL_ITEM.BML");
            MessageBox.Show("Successfully reverted!");
        }

        private void resetAlienConfigs_Click(object sender, EventArgs e)
        {
            Singleton.OnResetConfigs?.Invoke();
            EditorUtils.CloseAI();
            ResetFile("ALIENCONFIGS/ALIENCONFIGS.BML");
            ResetFile("ALIENCONFIGS/BACKSTAGEALERT.BML");
            ResetFile("ALIENCONFIGS/BACKSTAGEHOLD.BML");
            ResetFile("ALIENCONFIGS/BACKSTAGEHOLD_MILD.BML");
            ResetFile("ALIENCONFIGS/BACKSTAGEHOLD_VCLOSE.BML");
            ResetFile("ALIENCONFIGS/BACSTAGEHOLD_CLOSE.BML");
            ResetFile("ALIENCONFIGS/CANTEEN.BML");
            ResetFile("ALIENCONFIGS/CREWEXPENDABLE_VENT.BML");
            ResetFile("ALIENCONFIGS/DEFAULT.BML");
            ResetFile("ALIENCONFIGS/INTENSE.BML");
            ResetFile("ALIENCONFIGS/MILD.BML");
            ResetFile("ALIENCONFIGS/MODERATE.BML");
            ResetFile("ALIENCONFIGS/MODERATELY_INTENSE.BML");
            MessageBox.Show("Successfully reverted!");
        }

        private void resetRadiosity_Click(object sender, EventArgs e)
        {
            Singleton.OnResetConfigs?.Invoke();
            EditorUtils.CloseAI();
            ResetFile("RADIOSITY_SETTINGS.TXT");
            MessageBox.Show("Successfully reverted!");
        }

        private void resetHairAndSkin_Click(object sender, EventArgs e)
        {
            Singleton.OnResetConfigs?.Invoke();
            EditorUtils.CloseAI();
            ResetFile("HAIR_SHADING_SETTINGS.TXT");
            ResetFile("SKIN_SHADING_SETTINGS.TXT");
            MessageBox.Show("Successfully reverted!");
        }

        private void resetGraphics_Click(object sender, EventArgs e)
        {
            Singleton.OnResetConfigs?.Invoke();
            EditorUtils.CloseAI();
            ResetFile("ENGINE_SETTINGS.XML");
            MessageBox.Show("Successfully reverted!");
        }

        private void resetDifficulties_Click(object sender, EventArgs e)
        {
            Singleton.OnResetConfigs?.Invoke();
            EditorUtils.CloseAI();
            ResetFile("DIFFICULTYSETTINGS/DIFFICULTYSETTINGS.BML");
            ResetFile("DIFFICULTYSETTINGS/EASY.BML");
            ResetFile("DIFFICULTYSETTINGS/HARD.BML");
            ResetFile("DIFFICULTYSETTINGS/IRON.BML");
            ResetFile("DIFFICULTYSETTINGS/MEDIUM.BML");
            ResetFile("DIFFICULTYSETTINGS/NOVICE.BML");
            MessageBox.Show("Successfully reverted!");
        }

        private void resetViewcones_Click(object sender, EventArgs e)
        {
            Singleton.OnResetConfigs?.Invoke();
            EditorUtils.CloseAI();
            ResetFile("VIEW_CONE_SETS/VIEWCONESET_ANDROID.BML");
            ResetFile("VIEW_CONE_SETS/VIEWCONESET_HUMAN.BML");
            ResetFile("VIEW_CONE_SETS/VIEWCONESET_HUMAN_HEIGHTENED.BML");
            ResetFile("VIEW_CONE_SETS/VIEWCONESET_NONE.BML");
            ResetFile("VIEW_CONE_SETS/VIEWCONESET_SLEEPING.BML");
            ResetFile("VIEW_CONE_SETS/VIEWCONESET_STANDARD.BML");
            ResetFile("VIEW_CONE_SETS/VIEWCONESETS.BML");
            MessageBox.Show("Successfully reverted!");
        }

        private void resetAmmo_Click(object sender, EventArgs e)
        {
            Singleton.OnResetConfigs?.Invoke();
            EditorUtils.CloseAI();
            ResetFile("WEAPON_INFO/AMMO/ACID_BURST_LARGE.BML");
            ResetFile("WEAPON_INFO/AMMO/ACID_BURST_SMALL.BML");
            ResetFile("WEAPON_INFO/AMMO/AMMOTYPES.BML");
            ResetFile("WEAPON_INFO/AMMO/BOLTGUN_NORMAL.BML");
            ResetFile("WEAPON_INFO/AMMO/CATALYST_FIRE_LARGE.BML");
            ResetFile("WEAPON_INFO/AMMO/CATALYST_FIRE_SMALL.BML");
            ResetFile("WEAPON_INFO/AMMO/CATALYST_HE_LARGE.BML");
            ResetFile("WEAPON_INFO/AMMO/CATALYST_HE_SMALL.BML");
            ResetFile("WEAPON_INFO/AMMO/CATTLEPROD_POWERPACK.BML");
            ResetFile("WEAPON_INFO/AMMO/EMP_BURST_LARGE.BML");
            ResetFile("WEAPON_INFO/AMMO/EMP_BURST_LARGE_TIER2.BML");
            ResetFile("WEAPON_INFO/AMMO/EMP_BURST_LARGE_TIER3.BML");
            ResetFile("WEAPON_INFO/AMMO/EMP_BURST_SMALL.BML");
            ResetFile("WEAPON_INFO/AMMO/ENVIRONMENT_FLAME.BML");
            ResetFile("WEAPON_INFO/AMMO/FLAMETHROWER_AERATED.BML");
            ResetFile("WEAPON_INFO/AMMO/FLAMETHROWER_HIGH_DAMAGE.BML");
            ResetFile("WEAPON_INFO/AMMO/FLAMETHROWER_NORMAL.BML");
            ResetFile("WEAPON_INFO/AMMO/GRENADE_FIRE.BML");
            ResetFile("WEAPON_INFO/AMMO/GRENADE_FIRE_TIER2.BML");
            ResetFile("WEAPON_INFO/AMMO/GRENADE_FIRE_TIER3.BML");
            ResetFile("WEAPON_INFO/AMMO/GRENADE_HE.BML");
            ResetFile("WEAPON_INFO/AMMO/GRENADE_HE_TIER2.BML");
            ResetFile("WEAPON_INFO/AMMO/GRENADE_HE_TIER3.BML");
            ResetFile("WEAPON_INFO/AMMO/GRENADE_SMOKE.BML");
            ResetFile("WEAPON_INFO/AMMO/GRENADE_STUN.BML");
            ResetFile("WEAPON_INFO/AMMO/GRENADE_STUN_TIER2.BML");
            ResetFile("WEAPON_INFO/AMMO/GRENADE_STUN_TIER3.BML");
            ResetFile("WEAPON_INFO/AMMO/IMPACT.BML");
            ResetFile("WEAPON_INFO/AMMO/MELEE_CROW_AXE.BML");
            ResetFile("WEAPON_INFO/AMMO/PISTOL_DUM_DUM.BML");
            ResetFile("WEAPON_INFO/AMMO/PISTOL_NORMAL.BML");
            ResetFile("WEAPON_INFO/AMMO/PISTOL_NORMAL_NPC.BML");
            ResetFile("WEAPON_INFO/AMMO/PISTOL_TAZER.BML");
            ResetFile("WEAPON_INFO/AMMO/PUSH.BML");
            ResetFile("WEAPON_INFO/AMMO/SHOTGUN_INCENDIARY.BML");
            ResetFile("WEAPON_INFO/AMMO/SHOTGUN_NORMAL.BML");
            ResetFile("WEAPON_INFO/AMMO/SHOTGUN_NORMAL_NPC.BML");
            ResetFile("WEAPON_INFO/AMMO/SHOTGUN_SLUG.BML");
            ResetFile("WEAPON_INFO/AMMO/SMG_DUM_DUM.BML");
            ResetFile("WEAPON_INFO/AMMO/SMG_NORMAL.BML");
            MessageBox.Show("Successfully reverted!");
        }

        private void resetCharAssets_Click(object sender, EventArgs e)
        {
            Singleton.OnResetConfigs?.Invoke();
            EditorUtils.CloseAI();
            ResetFile("CHR_INFO/CUSTOMCHARACTERASSETDATA.BIN");
            MessageBox.Show("Successfully reverted!");
        }

        private void resetCharAttributes_Click(object sender, EventArgs e)
        {
            Singleton.OnResetConfigs?.Invoke();
            EditorUtils.CloseAI();
            ResetFile("CHR_INFO/ATTRIBUTES/ALIEN.BML");
            ResetFile("CHR_INFO/ATTRIBUTES/ANDROID.BML");
            ResetFile("CHR_INFO/ATTRIBUTES/ANDROID_HEAVY.BML");
            ResetFile("CHR_INFO/ATTRIBUTES/ATTRIBUTES.BML");
            ResetFile("CHR_INFO/ATTRIBUTES/BASE_HUMAN.BML");
            ResetFile("CHR_INFO/ATTRIBUTES/CIVILIAN.BML");
            ResetFile("CHR_INFO/ATTRIBUTES/CUTSCENE.BML");
            ResetFile("CHR_INFO/ATTRIBUTES/CUTSCENE_ANDROID.BML");
            ResetFile("CHR_INFO/ATTRIBUTES/DEFAULTS.BML");
            ResetFile("CHR_INFO/ATTRIBUTES/FACEHUGGER.BML");
            ResetFile("CHR_INFO/ATTRIBUTES/INNOCENT.BML");
            ResetFile("CHR_INFO/ATTRIBUTES/MELEE_HUMAN.BML");
            ResetFile("CHR_INFO/ATTRIBUTES/RIOT_GUARD.BML");
            ResetFile("CHR_INFO/ATTRIBUTES/SECURITY_GUARD.BML");
            ResetFile("CHR_INFO/ATTRIBUTES/SPACESUIT_NPC.BML");
            ResetFile("CHR_INFO/ATTRIBUTES/THE_PLAYER.BML");
            MessageBox.Show("Successfully reverted!");
        }

        private void resetPhysMats_Click(object sender, EventArgs e)
        {
            Singleton.OnResetConfigs?.Invoke();
            EditorUtils.CloseAI();
            ResetFile("MATERIAL_DATA/MATERIALS.BML");
            MessageBox.Show("Successfully reverted!");
        }

        private void resetGlobalConst_Click(object sender, EventArgs e)
        {
            Singleton.OnResetConfigs?.Invoke();
            EditorUtils.CloseAI();
            ResetFile("UI/SELECTIONOVERLAYPARAMS.BIN");
            ResetFile("GLOBALCONSTANTS.BML");
            MessageBox.Show("Successfully reverted!");
        }

        private void resetPermaBanks_Click(object sender, EventArgs e)
        {
            Singleton.OnResetConfigs?.Invoke();
            EditorUtils.CloseAI();
            ResetFile("LIST_OF_PERMANENT_SOUND_BANKS.TXT");
            MessageBox.Show("Successfully reverted!");
        }

        private void resetBehaviourTrees_Click(object sender, EventArgs e)
        {
            EditorUtils.CloseAI(new List<string>(new string[] { "BehaviourTreeEditor" }));
            ResetFile("BINARY_BEHAVIOR/_DIRECTORY_CONTENTS.BML");

            string pathToFolder = Singleton.PathToAI + @"\DATA\BEHAVIOR\";
            if (Directory.Exists(pathToFolder)) 
                Directory.Delete(pathToFolder, true);
            Directory.CreateDirectory(pathToFolder);

            BML bml = new BML(Singleton.PathToAI + "/DATA/BINARY_BEHAVIOR/_DIRECTORY_CONTENTS.BML");
            XmlDocument xml = bml.Content;
            XmlNodeList files = xml.SelectNodes("//DIR/File");
            foreach (XmlNode file in files)
                File.WriteAllText(pathToFolder + file.Attributes["name"].Value.Substring(0, file.Attributes["name"].Value.Length - 3) + "xml", file.InnerXml);

            MessageBox.Show("Successfully reverted!");
        }
    }
}
