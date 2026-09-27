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

namespace OpenCAGE.Popups
{
    public partial class ResetConfigs : Form
    {
        PAK2 _backupFiles;

        public ResetConfigs()
        {
            InitializeComponent();
            Theming.ThemeManager.ApplyToForm(this);

            _backupFiles = ConfigReset.LoadBackups();
        }

        /* One button per group: the files each resets live in ConfigReset.Groups, which the MCP reset tool shares */
        private void ResetGroup(string key, string message = "Successfully reverted!")
        {
            Singleton.OnResetConfigs?.Invoke();
            EditorUtils.CloseAI();
            foreach (string file in ConfigReset.FilesToReset(ConfigReset.Find(key)))
                ConfigReset.ResetFile(_backupFiles, file);
            MessageBox.Show(message);
        }

        private void resetGblItem_Click(object sender, EventArgs e)
        {
            ResetGroup("gbl_item");
        }

        private void resetAlienConfigs_Click(object sender, EventArgs e)
        {
            ResetGroup("alien_configs");
        }

        private void resetRadiosity_Click(object sender, EventArgs e)
        {
            ResetGroup("radiosity");
        }

        private void resetHairAndSkin_Click(object sender, EventArgs e)
        {
            ResetGroup("hair_and_skin");
        }

        private void resetGraphics_Click(object sender, EventArgs e)
        {
            ResetGroup("graphics");
        }

        private void resetDifficulties_Click(object sender, EventArgs e)
        {
            ResetGroup("difficulties");
        }

        private void resetViewcones_Click(object sender, EventArgs e)
        {
            ResetGroup("viewcones");
        }

        private void resetAmmo_Click(object sender, EventArgs e)
        {
            ResetGroup("ammo");
        }

        private void resetCharAssets_Click(object sender, EventArgs e)
        {
            ResetGroup("character_assets");
        }

        private void resetCharAttributes_Click(object sender, EventArgs e)
        {
            ResetGroup("character_attributes");
        }

        private void resetPhysMats_Click(object sender, EventArgs e)
        {
            ResetGroup("physical_materials");
        }

        private void resetGlobalConst_Click(object sender, EventArgs e)
        {
            ResetGroup("global_constants");
        }

        private void resetPermaBanks_Click(object sender, EventArgs e)
        {
            ResetGroup("permanent_soundbanks");
        }

        private void resetInputs_Click(object sender, EventArgs e)
        {
            ResetGroup("inputs");
        }

        private void resetFontConfig_Click(object sender, EventArgs e)
        {
            ResetGroup("font_config");
        }

        private void resetVoiceMappings_Click(object sender, EventArgs e)
        {
            ResetGroup("voice_mappings");
        }

        private void resetLevelTextDBs_Click(object sender, EventArgs e)
        {
            //The other half of this config is each level's own TEXT/TEXT_DB_LIST.TXT, which is level data
            //rather than a DATA config - the shipped challenge maps have their own, so this tool leaves
            //them alone. Manage Backups restores those with the rest of a level.
            ResetGroup("level_text_databases", "Successfully reverted!\n\nLevel folders were left alone: a level's own TEXT/TEXT_DB_LIST.TXT ships with the level, so use Manage Backups to revert that.");
        }

        private void resetBehaviourTrees_Click(object sender, EventArgs e)
        {
            EditorUtils.CloseAI(new List<string>(ConfigReset.BehaviourTreeProcesses));
            foreach (string file in ConfigReset.FilesToReset(ConfigReset.Find(ConfigReset.BehaviourTrees)))
                ConfigReset.ResetFile(_backupFiles, file);
            ConfigReset.RegenerateBehaviourTrees();

            MessageBox.Show("Successfully reverted!");
        }
    }

    /// <summary>
    /// The vanilla copies of the config files the game ships (Resources/config_backups.dat, a gzipped PAK2 keyed
    /// by path under DATA), and the groups Revert Configs resets them in. Shared by the Reset Configs window and
    /// the MCP reset tool, so both put back exactly the same files.
    /// </summary>
    internal static class ConfigReset
    {
        internal sealed class Group
        {
            public string Key;
            public string Label;
            public string[] Files;
            /// <summary>Only reset when the install has them (FONT_CONFIG.BML exists in some installs only).</summary>
            public string[] OptionalFiles = new string[0];
        }

        public const string BehaviourTrees = "behaviour_trees";
        public static readonly string[] BehaviourTreeProcesses = { "BehaviourTreeEditor" };

        public static readonly Group[] Groups = new Group[]
        {
            new Group() { Key = "gbl_item", Label = "Load Movies / Inventory / Hack Tool / Blueprints", Files = new[] { "GBL_ITEM.BML" } },
            new Group() { Key = "alien_configs", Label = "Alien Configs", Files = new[] {
                "ALIENCONFIGS/ALIENCONFIGS.BML", "ALIENCONFIGS/BACKSTAGEALERT.BML", "ALIENCONFIGS/BACKSTAGEHOLD.BML", "ALIENCONFIGS/BACKSTAGEHOLD_MILD.BML",
                "ALIENCONFIGS/BACKSTAGEHOLD_VCLOSE.BML", "ALIENCONFIGS/BACSTAGEHOLD_CLOSE.BML", "ALIENCONFIGS/CANTEEN.BML", "ALIENCONFIGS/CREWEXPENDABLE_VENT.BML",
                "ALIENCONFIGS/DEFAULT.BML", "ALIENCONFIGS/INTENSE.BML", "ALIENCONFIGS/MILD.BML", "ALIENCONFIGS/MODERATE.BML", "ALIENCONFIGS/MODERATELY_INTENSE.BML" } },
            new Group() { Key = "radiosity", Label = "Radiosity Settings", Files = new[] { "RADIOSITY_SETTINGS.TXT" } },
            new Group() { Key = "hair_and_skin", Label = "Hair and Skin Shading", Files = new[] { "HAIR_SHADING_SETTINGS.TXT", "SKIN_SHADING_SETTINGS.TXT" } },
            new Group() { Key = "graphics", Label = "Graphics", Files = new[] { "ENGINE_SETTINGS.XML" } },
            new Group() { Key = "difficulties", Label = "Difficulties", Files = new[] {
                "DIFFICULTYSETTINGS/DIFFICULTYSETTINGS.BML", "DIFFICULTYSETTINGS/EASY.BML", "DIFFICULTYSETTINGS/HARD.BML", "DIFFICULTYSETTINGS/IRON.BML",
                "DIFFICULTYSETTINGS/MEDIUM.BML", "DIFFICULTYSETTINGS/NOVICE.BML" } },
            new Group() { Key = "viewcones", Label = "Viewcone Sets", Files = new[] {
                "VIEW_CONE_SETS/VIEWCONESET_ANDROID.BML", "VIEW_CONE_SETS/VIEWCONESET_HUMAN.BML", "VIEW_CONE_SETS/VIEWCONESET_HUMAN_HEIGHTENED.BML",
                "VIEW_CONE_SETS/VIEWCONESET_NONE.BML", "VIEW_CONE_SETS/VIEWCONESET_SLEEPING.BML", "VIEW_CONE_SETS/VIEWCONESET_STANDARD.BML", "VIEW_CONE_SETS/VIEWCONESETS.BML" } },
            new Group() { Key = "ammo", Label = "Ammo", Files = new[] {
                "WEAPON_INFO/AMMO/ACID_BURST_LARGE.BML", "WEAPON_INFO/AMMO/ACID_BURST_SMALL.BML", "WEAPON_INFO/AMMO/AMMOTYPES.BML", "WEAPON_INFO/AMMO/BOLTGUN_NORMAL.BML",
                "WEAPON_INFO/AMMO/CATALYST_FIRE_LARGE.BML", "WEAPON_INFO/AMMO/CATALYST_FIRE_SMALL.BML", "WEAPON_INFO/AMMO/CATALYST_HE_LARGE.BML", "WEAPON_INFO/AMMO/CATALYST_HE_SMALL.BML",
                "WEAPON_INFO/AMMO/CATTLEPROD_POWERPACK.BML", "WEAPON_INFO/AMMO/EMP_BURST_LARGE.BML", "WEAPON_INFO/AMMO/EMP_BURST_LARGE_TIER2.BML", "WEAPON_INFO/AMMO/EMP_BURST_LARGE_TIER3.BML",
                "WEAPON_INFO/AMMO/EMP_BURST_SMALL.BML", "WEAPON_INFO/AMMO/ENVIRONMENT_FLAME.BML", "WEAPON_INFO/AMMO/FLAMETHROWER_AERATED.BML", "WEAPON_INFO/AMMO/FLAMETHROWER_HIGH_DAMAGE.BML",
                "WEAPON_INFO/AMMO/FLAMETHROWER_NORMAL.BML", "WEAPON_INFO/AMMO/GRENADE_FIRE.BML", "WEAPON_INFO/AMMO/GRENADE_FIRE_TIER2.BML", "WEAPON_INFO/AMMO/GRENADE_FIRE_TIER3.BML",
                "WEAPON_INFO/AMMO/GRENADE_HE.BML", "WEAPON_INFO/AMMO/GRENADE_HE_TIER2.BML", "WEAPON_INFO/AMMO/GRENADE_HE_TIER3.BML", "WEAPON_INFO/AMMO/GRENADE_SMOKE.BML",
                "WEAPON_INFO/AMMO/GRENADE_STUN.BML", "WEAPON_INFO/AMMO/GRENADE_STUN_TIER2.BML", "WEAPON_INFO/AMMO/GRENADE_STUN_TIER3.BML", "WEAPON_INFO/AMMO/IMPACT.BML",
                "WEAPON_INFO/AMMO/MELEE_CROW_AXE.BML", "WEAPON_INFO/AMMO/PISTOL_DUM_DUM.BML", "WEAPON_INFO/AMMO/PISTOL_NORMAL.BML", "WEAPON_INFO/AMMO/PISTOL_NORMAL_NPC.BML",
                "WEAPON_INFO/AMMO/PISTOL_TAZER.BML", "WEAPON_INFO/AMMO/PUSH.BML", "WEAPON_INFO/AMMO/SHOTGUN_INCENDIARY.BML", "WEAPON_INFO/AMMO/SHOTGUN_NORMAL.BML",
                "WEAPON_INFO/AMMO/SHOTGUN_NORMAL_NPC.BML", "WEAPON_INFO/AMMO/SHOTGUN_SLUG.BML", "WEAPON_INFO/AMMO/SMG_DUM_DUM.BML", "WEAPON_INFO/AMMO/SMG_NORMAL.BML" } },
            new Group() { Key = "character_assets", Label = "Character Assets", Files = new[] { "CHR_INFO/CUSTOMCHARACTERASSETDATA.BIN" } },
            new Group() { Key = "character_attributes", Label = "Character Attributes", Files = new[] {
                "CHR_INFO/ATTRIBUTES/ALIEN.BML", "CHR_INFO/ATTRIBUTES/ANDROID.BML", "CHR_INFO/ATTRIBUTES/ANDROID_HEAVY.BML", "CHR_INFO/ATTRIBUTES/ATTRIBUTES.BML",
                "CHR_INFO/ATTRIBUTES/BASE_HUMAN.BML", "CHR_INFO/ATTRIBUTES/CIVILIAN.BML", "CHR_INFO/ATTRIBUTES/CUTSCENE.BML", "CHR_INFO/ATTRIBUTES/CUTSCENE_ANDROID.BML",
                "CHR_INFO/ATTRIBUTES/DEFAULTS.BML", "CHR_INFO/ATTRIBUTES/FACEHUGGER.BML", "CHR_INFO/ATTRIBUTES/INNOCENT.BML", "CHR_INFO/ATTRIBUTES/MELEE_HUMAN.BML",
                "CHR_INFO/ATTRIBUTES/RIOT_GUARD.BML", "CHR_INFO/ATTRIBUTES/SECURITY_GUARD.BML", "CHR_INFO/ATTRIBUTES/SPACESUIT_NPC.BML", "CHR_INFO/ATTRIBUTES/THE_PLAYER.BML" } },
            new Group() { Key = "physical_materials", Label = "Physical Materials", Files = new[] { "MATERIAL_DATA/MATERIALS.BML" } },
            new Group() { Key = "global_constants", Label = "Global Constants (and the interaction glow)", Files = new[] { "UI/SELECTIONOVERLAYPARAMS.BIN", "GLOBALCONSTANTS.BML" } },
            new Group() { Key = "permanent_soundbanks", Label = "Permanent Soundbanks", Files = new[] { "LIST_OF_PERMANENT_SOUND_BANKS.TXT" } },
            new Group() { Key = "inputs", Label = "Input Bindings", Files = new[] { "INPUT.XML" } },
            //The BML only exists in some installs; the editor keeps whichever spellings are there in step
            new Group() { Key = "font_config", Label = "Font Config", Files = new[] { "FONT_CONFIG.XML" }, OptionalFiles = new[] { "FONT_CONFIG.BML" } },
            new Group() { Key = "voice_mappings", Label = "Voice Mappings", Files = new[] { "CHR_INFO/CUSTOMCHARACTERVOICETYPEMAPPINGS.BIN" } },
            new Group() { Key = "level_text_databases", Label = "Level Text Databases", Files = new[] { "LEVEL_TEXT_DATABASES.XML" } },
            new Group() { Key = BehaviourTrees, Label = "Behaviour Trees (also regenerates DATA/BEHAVIOR)", Files = new[] { "BINARY_BEHAVIOR/_DIRECTORY_CONTENTS.BML" } },
        };

        public static Group Find(string key)
        {
            Group group = Groups.FirstOrDefault(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
            if (group == null)
                throw new ArgumentException("There is no config group '" + key + "'.");
            return group;
        }

        public static PAK2 LoadBackups()
        {
            using (MemoryStream stream = new MemoryStream())
            using (GZipStream compressedStream = new GZipStream(new MemoryStream(Properties.Resources.config_backups), CompressionMode.Decompress))
            {
                compressedStream.CopyTo(stream);
                return new PAK2(stream.ToArray());
            }
        }

        /// <summary>The vanilla bytes of a file (path under DATA), or null when the backups do not hold it.</summary>
        public static byte[] Vanilla(PAK2 backups, string file)
        {
            return backups.Entries.FirstOrDefault(o => string.Equals(o.Filename, file, StringComparison.OrdinalIgnoreCase))?.Content;
        }

        /// <summary>The files a group resets in this install.</summary>
        public static IEnumerable<string> FilesToReset(Group group)
        {
            return group.Files.Concat(group.OptionalFiles.Where(o => File.Exists(Singleton.PathToAI + "/DATA/" + o)));
        }

        public static void ResetFile(PAK2 backups, string file)
        {
            byte[] content = Vanilla(backups, file);
            if (content == null)
                throw new FileNotFoundException("OpenCAGE's config backups have no vanilla copy of " + file + ".");
            File.WriteAllBytes(Singleton.PathToAI + "/DATA/" + file, content);
        }

        /// <summary>DATA/BEHAVIOR is generated from BINARY_BEHAVIOR/_DIRECTORY_CONTENTS.BML: delete it and write each tree's .xml again.</summary>
        public static int RegenerateBehaviourTrees()
        {
            string pathToFolder = Singleton.PathToAI + @"\DATA\BEHAVIOR\";
            if (Directory.Exists(pathToFolder))
                Directory.Delete(pathToFolder, true);
            Directory.CreateDirectory(pathToFolder);

            BML bml = new BML(Singleton.PathToAI + "/DATA/BINARY_BEHAVIOR/_DIRECTORY_CONTENTS.BML");
            XmlDocument xml = bml.Content;
            XmlNodeList files = xml.SelectNodes("//DIR/File");
            foreach (XmlNode file in files)
                File.WriteAllText(pathToFolder + file.Attributes["name"].Value.Substring(0, file.Attributes["name"].Value.Length - 3) + "xml", file.InnerXml);
            return files.Count;
        }
    }
}
