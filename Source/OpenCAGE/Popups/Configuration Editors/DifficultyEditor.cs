using CATHODE;
using OpenCAGE.Popups.Base;
using OpenCAGE.RuntimeUtilsConnection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.ConfigEditors
{
    public partial class DifficultyEditor : BaseWindow
    {
        List<BML> _selectedDifficulty;

        public DifficultyEditor() : base()
        {
            InitializeComponent();
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/configs/difficulty-settings/");
            ConfigEditorUtils.ExpandNumericRanges(this.Controls);

            BML viewconeTypes = new BML(Singleton.PathToAI + "\\DATA\\VIEW_CONE_SETS\\VIEWCONESETS.BML");
            var viewcones = viewconeTypes.Content["ViewconeSets"];
            viewconeSets.BeginUpdate();
            foreach (XmlElement viewcone in viewcones)
            {
                string name = viewcone["Name"].InnerText;
                switch (name)
                {
                    case "VIEWCONESET_STANDARD":
                    case "VIEWCONESET_HUMAN":
                    case "VIEWCONESET_SLEEPING": //unused? it doesnt have all sets.
                    case "VIEWCONESET_ANDROID":
                        break;
                    default:
                        // It appears the game skips any other than the ones above, so ignore them.
                        continue;
                }
                viewconeSets.Items.Add(name);
            }
            viewconeSets.EndUpdate();

            BML attributeTypes = new BML(Singleton.PathToAI + "\\DATA\\CHR_INFO\\ATTRIBUTES\\ATTRIBUTES.BML");
            var attributes = attributeTypes.Content["Attributes"];
            characterTypesSense.BeginUpdate();
            characterTypesAttribute.BeginUpdate();
            foreach (XmlElement attribute in attributes)
            {
                characterTypesSense.Items.Add(attribute["Name"].InnerText);
                characterTypesAttribute.Items.Add(attribute["Name"].InnerText);
            }
            characterTypesSense.EndUpdate();
            characterTypesAttribute.EndUpdate();

            BML difficultySettingsTypes = new BML(Singleton.PathToAI + "\\DATA\\DIFFICULTYSETTINGS\\DIFFICULTYSETTINGS.BML");
            var difficultySettings = difficultySettingsTypes.Content["DifficultySettings"];
            classSelection.BeginUpdate();
            foreach (XmlElement difficultySetting in difficultySettings)
            {
                classSelection.Items.Add(difficultySetting["Name"].InnerText);
            }
            classSelection.EndUpdate();

            this.FormClosing += DifficultyEditor_FormClosing;
            CloseOnConfigReset();
        }

        private void DifficultyEditor_Load(object sender, EventArgs e)
        {
            for (int i = 0; i < classSelection.Items.Count; i++)
            {
                classSelection.SelectedIndex = i;
                SaveAlienConfig(null, EventArgs.Empty);

                for (int x = 0; x < characterTypesSense.Items.Count; x++)
                {
                    characterTypesSense.SelectedIndex = x;
                    SaveSenseConfig(null, EventArgs.Empty);
                }

                for (int x = 0; x < characterTypesAttribute.Items.Count; x++)
                {
                    characterTypesAttribute.SelectedIndex = x;
                    SaveAttributeConfig(null, EventArgs.Empty);
                }

                for (int x = 0; x < viewconeSets.Items.Count; x++)
                {
                    viewconeSets.SelectedIndex = x;
                    SaveViewconeConfig(null, EventArgs.Empty);
                }
            }
            classSelection.SelectedIndex = 0;

            //The game reads these when it starts; with Live Link connected each edit has it read them again
            LiveSettings.AttachReload(this, liveStatus, LiveSettings.ReloadableConfig.DifficultySettings);
        }

        private void DifficultyEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(tabPage1.Controls, SaveAlienConfig);
            ConfigEditorUtils.Unsubscribe(tabPage2.Controls, SaveSenseConfig);
            ConfigEditorUtils.Unsubscribe(tabPage4.Controls, SaveAttributeConfig);
            ConfigEditorUtils.Unsubscribe(viewconeDifficultySet1.Controls, SaveViewconeConfig);
            this.FormClosing -= DifficultyEditor_FormClosing;
        }

        private void classSelection_SelectedIndexChanged(object sender, EventArgs e)
        {
            _selectedDifficulty = new List<BML>();
            _selectedDifficulty.Add(new BML(Singleton.PathToAI + "\\DATA\\DIFFICULTYSETTINGS\\" + classSelection.Text + ".BML"));
            while (true)
            {
                string template = _selectedDifficulty[_selectedDifficulty.Count - 1].Content["DifficultySetting"]["Template_Name"]?.InnerText;
                if (template == null || template == "") break;
                _selectedDifficulty.Add(new BML(Singleton.PathToAI + "\\DATA\\DIFFICULTYSETTINGS\\" + template + ".BML"));
            }

            ConfigEditorUtils.Unsubscribe(tabPage1.Controls, SaveAlienConfig);
            //Each file in the template chain is read once for all the fields, not once per field
            using (ConfigEditorUtils.ReadOnce())
                ShowAlienFields();
            ConfigEditorUtils.Subscribe(tabPage1.Controls, SaveAlienConfig);

            characterTypesSense.SelectedIndex = 0;
            characterTypesSense_SelectedIndexChanged(null, null);

            characterTypesAttribute.SelectedIndex = 0;
            characterTypesAttribute_SelectedIndexChanged(null, null);

            viewconeSets.SelectedIndex = 0;
            viewconeSet_SelectedIndexChanged(null, null);
        }

        /* Fill the Alien tab from the selected difficulty, falling back through its templates */
        private void ShowAlienFields()
        {
            ConfigEditorUtils.SetNumber(_selectedDifficulty, decrease_sweep_duration_modifier, "DifficultySetting", "Alien", "AlienConfig", "decrease_sweep_duration_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, increase_sweep_duration_modifier, "DifficultySetting", "Alien", "AlienConfig", "increase_sweep_duration_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, near_target_exclusion_radius_first_stalk_min_modifier, "DifficultySetting", "Alien", "AlienConfig", "near_target_exclusion_radius_first_stalk_min_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, near_target_exclusion_radius_first_stalk_max_modifier, "DifficultySetting", "Alien", "AlienConfig", "near_target_exclusion_radius_first_stalk_max_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, near_target_exclusion_radius_subsequent_stalk_min_modifier, "DifficultySetting", "Alien", "AlienConfig", "near_target_exclusion_radius_subsequent_stalk_min_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, near_target_exclusion_radius_subsequent_stalk_max_modifier, "DifficultySetting", "Alien", "AlienConfig", "near_target_exclusion_radius_subsequent_stalk_max_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, near_objective_exclusion_radius_first_stalk_min_modifier, "DifficultySetting", "Alien", "AlienConfig", "near_objective_exclusion_radius_first_stalk_min_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, near_objective_exclusion_radius_first_stalk_max_modifier, "DifficultySetting", "Alien", "AlienConfig", "near_objective_exclusion_radius_first_stalk_max_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, near_objective_exclusion_radius_subsequent_stalk_min_modifier, "DifficultySetting", "Alien", "AlienConfig", "near_objective_exclusion_radius_subsequent_stalk_min_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, near_objective_exclusion_radius_subsequent_stalk_max_modifier, "DifficultySetting", "Alien", "AlienConfig", "near_objective_exclusion_radius_subsequent_stalk_max_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, menace_gauge_decrease_time_modifier, "DifficultySetting", "Alien", "AlienConfig", "menace_gauge_decrease_time_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, meance_deemed_time_modifier, "DifficultySetting", "Alien", "AlienConfig", "meance_deemed_time_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, max_menaces_modifier, "DifficultySetting", "Alien", "AlienConfig", "max_menaces_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, menace_gauge_seconds_to_fill_modifier, "DifficultySetting", "Alien", "AlienConfig", "menace_gauge_seconds_to_fill_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, backstage_area_sweep_role_timeout_modifier, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_role_timeout_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, backstage_area_sweep_min_distance_modifier, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_min_distance_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, backstage_area_sweep_max_distance_modifier, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_max_distance_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, backstage_area_sweep_min_idle_time_modifier, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_min_idle_time_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, backstage_area_sweep_max_idle_time_modifier, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_max_idle_time_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, backstage_area_sweep_killtrap_time_modifier, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_killtrap_time_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, backstage_area_sweep_ambush_timeout_modifier, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_ambush_timeout_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, sweep_box_half_length_modifier, "DifficultySetting", "Alien", "AlienConfig", "sweep_box_half_length_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, sweep_box_half_width_modifier, "DifficultySetting", "Alien", "AlienConfig", "sweep_box_half_width_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, Vent_Attract_Time_Min, "DifficultySetting", "Alien", "AlienConfig", "Vent_Attract_Time_Min");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, Vent_Attract_Time_Max, "DifficultySetting", "Alien", "AlienConfig", "Vent_Attract_Time_Max");
        }

        private void characterTypesSense_SelectedIndexChanged(object sender, EventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(tabPage2.Controls, SaveSenseConfig);
            using (ConfigEditorUtils.ReadOnce())
                ShowSenseFields();
            ConfigEditorUtils.Subscribe(tabPage2.Controls, SaveSenseConfig);
        }

        /* Fill the Senses tab for the selected character type */
        private void ShowSenseFields()
        {
            ConfigEditorUtils.SetNumber(_selectedDifficulty, max_hearing_distance_modifier,"DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "max_hearing_distance_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, weapon_sound_sense_activation_modifier, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "weapon_sound_sense_activation_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, weapon_sound_combined_sense_activation_modifier, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "weapon_sound_combined_sense_activation_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, movement_sound_sense_activation_modifier, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "movement_sound_sense_activation_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, movement_sound_combined_sense_activation_modifier, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "movement_sound_combined_sense_activation_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, flash_light_sense_activation_modifier, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "flash_light_sense_activation_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, flash_light_combined_sense_activation_modifier, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "flash_light_combined_sense_activation_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, visual_sense_activation_modifier, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "visual_sense_activation_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, visual_combined_sense_activation_modifier, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "visual_combined_sense_activation_modifier");
        }

        private void characterTypesAttribute_SelectedIndexChanged(object sender, EventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(tabPage4.Controls, SaveAttributeConfig);
            using (ConfigEditorUtils.ReadOnce())
                ShowAttributeFields();
            ConfigEditorUtils.Subscribe(tabPage4.Controls, SaveAttributeConfig);
        }

        /* Fill the Attributes tab for the selected character type */
        private void ShowAttributeFields()
        {
            ConfigEditorUtils.SetNumber(_selectedDifficulty, damage_dealt_scalar,"DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "damage_dealt_scalar");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, damage_received_scalar, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "damage_received_scalar");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, attack_pace_modifier_per_npc, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "attack_pace_modifier_per_npc");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, attack_pace_modifier_max, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "attack_pace_modifier_max");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, shooting_in_cover_duration_modifier, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "shooting_in_cover_duration_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, time_between_shots_scalar, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "time_between_shots_scalar");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, suspicious_item_loop_scalar, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "suspicious_item_loop_scalar");
        }

        private void viewconeSet_SelectedIndexChanged(object sender, EventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(viewconeDifficultySet1.Controls, SaveViewconeConfig);
            using (ConfigEditorUtils.ReadOnce())
                viewconeDifficultySet1.Populate(_selectedDifficulty, viewconeSets.Text);
            ConfigEditorUtils.Subscribe(viewconeDifficultySet1.Controls, SaveViewconeConfig);
        }
        
        private void SaveAlienConfig(object sender, EventArgs e)
        {
            var doc = _selectedDifficulty[0].Content;

            //A field no file in the template chain has is disabled, and still shows the previous entry's value
            if (decrease_sweep_duration_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "decrease_sweep_duration_modifier").InnerText = ConfigEditorUtils.NumberText(decrease_sweep_duration_modifier);
            if (increase_sweep_duration_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "increase_sweep_duration_modifier").InnerText = ConfigEditorUtils.NumberText(increase_sweep_duration_modifier);
            if (near_target_exclusion_radius_first_stalk_min_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "near_target_exclusion_radius_first_stalk_min_modifier").InnerText = ConfigEditorUtils.NumberText(near_target_exclusion_radius_first_stalk_min_modifier);
            if (near_target_exclusion_radius_first_stalk_max_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "near_target_exclusion_radius_first_stalk_max_modifier").InnerText = ConfigEditorUtils.NumberText(near_target_exclusion_radius_first_stalk_max_modifier);
            if (near_target_exclusion_radius_subsequent_stalk_min_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "near_target_exclusion_radius_subsequent_stalk_min_modifier").InnerText = ConfigEditorUtils.NumberText(near_target_exclusion_radius_subsequent_stalk_min_modifier);
            if (near_target_exclusion_radius_subsequent_stalk_max_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "near_target_exclusion_radius_subsequent_stalk_max_modifier").InnerText = ConfigEditorUtils.NumberText(near_target_exclusion_radius_subsequent_stalk_max_modifier);
            if (near_objective_exclusion_radius_first_stalk_min_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "near_objective_exclusion_radius_first_stalk_min_modifier").InnerText = ConfigEditorUtils.NumberText(near_objective_exclusion_radius_first_stalk_min_modifier);
            if (near_objective_exclusion_radius_first_stalk_max_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "near_objective_exclusion_radius_first_stalk_max_modifier").InnerText = ConfigEditorUtils.NumberText(near_objective_exclusion_radius_first_stalk_max_modifier);
            if (near_objective_exclusion_radius_subsequent_stalk_min_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "near_objective_exclusion_radius_subsequent_stalk_min_modifier").InnerText = ConfigEditorUtils.NumberText(near_objective_exclusion_radius_subsequent_stalk_min_modifier);
            if (near_objective_exclusion_radius_subsequent_stalk_max_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "near_objective_exclusion_radius_subsequent_stalk_max_modifier").InnerText = ConfigEditorUtils.NumberText(near_objective_exclusion_radius_subsequent_stalk_max_modifier);
            if (menace_gauge_decrease_time_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "menace_gauge_decrease_time_modifier").InnerText = ConfigEditorUtils.NumberText(menace_gauge_decrease_time_modifier);
            if (meance_deemed_time_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "meance_deemed_time_modifier").InnerText = ConfigEditorUtils.NumberText(meance_deemed_time_modifier);
            if (max_menaces_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "max_menaces_modifier").InnerText = ConfigEditorUtils.NumberText(max_menaces_modifier);
            if (menace_gauge_seconds_to_fill_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "menace_gauge_seconds_to_fill_modifier").InnerText = ConfigEditorUtils.NumberText(menace_gauge_seconds_to_fill_modifier);
            if (backstage_area_sweep_role_timeout_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_role_timeout_modifier").InnerText = ConfigEditorUtils.NumberText(backstage_area_sweep_role_timeout_modifier);
            if (backstage_area_sweep_min_distance_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_min_distance_modifier").InnerText = ConfigEditorUtils.NumberText(backstage_area_sweep_min_distance_modifier);
            if (backstage_area_sweep_max_distance_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_max_distance_modifier").InnerText = ConfigEditorUtils.NumberText(backstage_area_sweep_max_distance_modifier);
            if (backstage_area_sweep_min_idle_time_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_min_idle_time_modifier").InnerText = ConfigEditorUtils.NumberText(backstage_area_sweep_min_idle_time_modifier);
            if (backstage_area_sweep_max_idle_time_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_max_idle_time_modifier").InnerText = ConfigEditorUtils.NumberText(backstage_area_sweep_max_idle_time_modifier);
            if (backstage_area_sweep_killtrap_time_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_killtrap_time_modifier").InnerText = ConfigEditorUtils.NumberText(backstage_area_sweep_killtrap_time_modifier);
            if (backstage_area_sweep_ambush_timeout_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_ambush_timeout_modifier").InnerText = ConfigEditorUtils.NumberText(backstage_area_sweep_ambush_timeout_modifier);
            if (sweep_box_half_length_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "sweep_box_half_length_modifier").InnerText = ConfigEditorUtils.NumberText(sweep_box_half_length_modifier);
            if (sweep_box_half_width_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "sweep_box_half_width_modifier").InnerText = ConfigEditorUtils.NumberText(sweep_box_half_width_modifier);
            if (Vent_Attract_Time_Min.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "Vent_Attract_Time_Min").InnerText = ConfigEditorUtils.NumberText(Vent_Attract_Time_Min);
            if (Vent_Attract_Time_Max.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "Vent_Attract_Time_Max").InnerText = ConfigEditorUtils.NumberText(Vent_Attract_Time_Max);

            _selectedDifficulty[0].Content = doc;
            _selectedDifficulty[0].Save();

            //Only an edit goes to the game: the saves made as the editor opens, and as another difficulty is picked, change nothing it reads
            if (sender != null)
                LiveSettings.ReloadAfterEdit(LiveSettings.ReloadableConfig.DifficultySettings);

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void SaveSenseConfig(object sender, EventArgs e)
        {
            var doc = _selectedDifficulty[0].Content;

            if (max_hearing_distance_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "max_hearing_distance_modifier").InnerText = ConfigEditorUtils.NumberText(max_hearing_distance_modifier);
            if (weapon_sound_sense_activation_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "weapon_sound_sense_activation_modifier").InnerText = ConfigEditorUtils.NumberText(weapon_sound_sense_activation_modifier);
            if (weapon_sound_combined_sense_activation_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "weapon_sound_combined_sense_activation_modifier").InnerText = ConfigEditorUtils.NumberText(weapon_sound_combined_sense_activation_modifier);
            if (movement_sound_sense_activation_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "movement_sound_sense_activation_modifier").InnerText = ConfigEditorUtils.NumberText(movement_sound_sense_activation_modifier);
            if (movement_sound_combined_sense_activation_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "movement_sound_combined_sense_activation_modifier").InnerText = ConfigEditorUtils.NumberText(movement_sound_combined_sense_activation_modifier);
            if (flash_light_sense_activation_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "flash_light_sense_activation_modifier").InnerText = ConfigEditorUtils.NumberText(flash_light_sense_activation_modifier);
            if (flash_light_combined_sense_activation_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "flash_light_combined_sense_activation_modifier").InnerText = ConfigEditorUtils.NumberText(flash_light_combined_sense_activation_modifier);
            if (visual_sense_activation_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "visual_sense_activation_modifier").InnerText = ConfigEditorUtils.NumberText(visual_sense_activation_modifier);
            if (visual_combined_sense_activation_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "visual_combined_sense_activation_modifier").InnerText = ConfigEditorUtils.NumberText(visual_combined_sense_activation_modifier);

            _selectedDifficulty[0].Content = doc;
            _selectedDifficulty[0].Save();

            //(as above; picking another character type saves too)
            if (sender != null && sender != characterTypesSense)
                LiveSettings.ReloadAfterEdit(LiveSettings.ReloadableConfig.DifficultySettings);

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void SaveAttributeConfig(object sender, EventArgs e)
        {
            var doc = _selectedDifficulty[0].Content;

            if (damage_dealt_scalar.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "damage_dealt_scalar").InnerText = ConfigEditorUtils.NumberText(damage_dealt_scalar);
            if (damage_received_scalar.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "damage_received_scalar").InnerText = ConfigEditorUtils.NumberText(damage_received_scalar);
            if (attack_pace_modifier_per_npc.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "attack_pace_modifier_per_npc").InnerText = ConfigEditorUtils.NumberText(attack_pace_modifier_per_npc);
            if (attack_pace_modifier_max.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "attack_pace_modifier_max").InnerText = ConfigEditorUtils.NumberText(attack_pace_modifier_max);
            if (shooting_in_cover_duration_modifier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "shooting_in_cover_duration_modifier").InnerText = ConfigEditorUtils.NumberText(shooting_in_cover_duration_modifier);
            if (time_between_shots_scalar.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "time_between_shots_scalar").InnerText = ConfigEditorUtils.NumberText(time_between_shots_scalar);
            if (suspicious_item_loop_scalar.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "suspicious_item_loop_scalar").InnerText = ConfigEditorUtils.NumberText(suspicious_item_loop_scalar);

            _selectedDifficulty[0].Content = doc;
            _selectedDifficulty[0].Save();

            //(as above; picking another character type saves too)
            if (sender != null && sender != characterTypesAttribute)
                LiveSettings.ReloadAfterEdit(LiveSettings.ReloadableConfig.DifficultySettings);

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void SaveViewconeConfig(object sender, EventArgs e)
        {
            var doc = _selectedDifficulty[0].Content;
            XmlElement set = ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "ViewconeSets", viewconeSets.Text);
            ConfigEditorUtils.EnsureChildElements(set, "Close");
            ConfigEditorUtils.EnsureChildElements(set, "Focused");
            ConfigEditorUtils.EnsureChildElements(set, "Normal");
            ConfigEditorUtils.EnsureChildElements(set, "Peripheral");

            viewconeDifficultySet1.Save(set);

            _selectedDifficulty[0].Content = doc;
            _selectedDifficulty[0].Save();

            //(as above)
            if (sender != null)
                LiveSettings.ReloadAfterEdit(LiveSettings.ReloadableConfig.DifficultySettings);

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }
    }
}
