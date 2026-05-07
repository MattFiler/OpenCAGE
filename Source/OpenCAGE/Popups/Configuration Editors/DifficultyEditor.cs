using CATHODE;
using OpenCAGE.Popups.Base;
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
            Singleton.OnResetConfigs += () => { this.Close(); };
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
            ConfigEditorUtils.Subscribe(tabPage1.Controls, SaveAlienConfig);

            characterTypesSense.SelectedIndex = 0;
            characterTypesSense_SelectedIndexChanged(null, null);

            characterTypesAttribute.SelectedIndex = 0;
            characterTypesAttribute_SelectedIndexChanged(null, null);

            viewconeSets.SelectedIndex = 0;
            viewconeSet_SelectedIndexChanged(null, null);
        }

        private void characterTypesSense_SelectedIndexChanged(object sender, EventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(tabPage2.Controls, SaveSenseConfig);
            ConfigEditorUtils.SetNumber(_selectedDifficulty, max_hearing_distance_modifier, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "max_hearing_distance_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, weapon_sound_sense_activation_modifier, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "weapon_sound_sense_activation_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, weapon_sound_combined_sense_activation_modifier, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "weapon_sound_combined_sense_activation_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, movement_sound_sense_activation_modifier, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "movement_sound_sense_activation_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, movement_sound_combined_sense_activation_modifier, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "movement_sound_combined_sense_activation_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, flash_light_sense_activation_modifier, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "flash_light_sense_activation_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, flash_light_combined_sense_activation_modifier, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "flash_light_combined_sense_activation_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, visual_sense_activation_modifier, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "visual_sense_activation_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, visual_combined_sense_activation_modifier, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "visual_combined_sense_activation_modifier");
            ConfigEditorUtils.Subscribe(tabPage2.Controls, SaveSenseConfig);
        }

        private void characterTypesAttribute_SelectedIndexChanged(object sender, EventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(tabPage4.Controls, SaveAttributeConfig);
            ConfigEditorUtils.SetNumber(_selectedDifficulty, damage_dealt_scalar, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "damage_dealt_scalar");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, damage_received_scalar, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "damage_received_scalar");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, attack_pace_modifier_per_npc, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "attack_pace_modifier_per_npc");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, attack_pace_modifier_max, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "attack_pace_modifier_max");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, shooting_in_cover_duration_modifier, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "shooting_in_cover_duration_modifier");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, time_between_shots_scalar, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "time_between_shots_scalar");
            ConfigEditorUtils.SetNumber(_selectedDifficulty, suspicious_item_loop_scalar, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "suspicious_item_loop_scalar");
            ConfigEditorUtils.Subscribe(tabPage4.Controls, SaveAttributeConfig);
        }

        private void viewconeSet_SelectedIndexChanged(object sender, EventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(viewconeDifficultySet1.Controls, SaveViewconeConfig);
            viewconeDifficultySet1.Populate(_selectedDifficulty, viewconeSets.Text);
            ConfigEditorUtils.Subscribe(viewconeDifficultySet1.Controls, SaveViewconeConfig);
        }
        
        private void SaveAlienConfig(object sender, EventArgs e)
        {
            var doc = _selectedDifficulty[0].Content;

            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "decrease_sweep_duration_modifier").InnerText = decrease_sweep_duration_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "increase_sweep_duration_modifier").InnerText = increase_sweep_duration_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "near_target_exclusion_radius_first_stalk_min_modifier").InnerText = near_target_exclusion_radius_first_stalk_min_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "near_target_exclusion_radius_first_stalk_max_modifier").InnerText = near_target_exclusion_radius_first_stalk_max_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "near_target_exclusion_radius_subsequent_stalk_min_modifier").InnerText = near_target_exclusion_radius_subsequent_stalk_min_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "near_target_exclusion_radius_subsequent_stalk_max_modifier").InnerText = near_target_exclusion_radius_subsequent_stalk_max_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "near_objective_exclusion_radius_first_stalk_min_modifier").InnerText = near_objective_exclusion_radius_first_stalk_min_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "near_objective_exclusion_radius_first_stalk_max_modifier").InnerText = near_objective_exclusion_radius_first_stalk_max_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "near_objective_exclusion_radius_subsequent_stalk_min_modifier").InnerText = near_objective_exclusion_radius_subsequent_stalk_min_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "near_objective_exclusion_radius_subsequent_stalk_max_modifier").InnerText = near_objective_exclusion_radius_subsequent_stalk_max_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "menace_gauge_decrease_time_modifier").InnerText = menace_gauge_decrease_time_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "meance_deemed_time_modifier").InnerText = meance_deemed_time_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "max_menaces_modifier").InnerText = max_menaces_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "menace_gauge_seconds_to_fill_modifier").InnerText = menace_gauge_seconds_to_fill_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_role_timeout_modifier").InnerText = backstage_area_sweep_role_timeout_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_min_distance_modifier").InnerText = backstage_area_sweep_min_distance_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_max_distance_modifier").InnerText = backstage_area_sweep_max_distance_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_min_idle_time_modifier").InnerText = backstage_area_sweep_min_idle_time_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_max_idle_time_modifier").InnerText = backstage_area_sweep_max_idle_time_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_killtrap_time_modifier").InnerText = backstage_area_sweep_killtrap_time_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "backstage_area_sweep_ambush_timeout_modifier").InnerText = backstage_area_sweep_ambush_timeout_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "sweep_box_half_length_modifier").InnerText = sweep_box_half_length_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "sweep_box_half_width_modifier").InnerText = sweep_box_half_width_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "Vent_Attract_Time_Min").InnerText = Vent_Attract_Time_Min.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "Alien", "AlienConfig", "Vent_Attract_Time_Max").InnerText = Vent_Attract_Time_Max.Text;

            _selectedDifficulty[0].Content = doc;
            _selectedDifficulty[0].Save();

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void SaveSenseConfig(object sender, EventArgs e)
        {
            var doc = _selectedDifficulty[0].Content;

            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "max_hearing_distance_modifier").InnerText = max_hearing_distance_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "weapon_sound_sense_activation_modifier").InnerText = weapon_sound_sense_activation_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "weapon_sound_combined_sense_activation_modifier").InnerText = weapon_sound_combined_sense_activation_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "movement_sound_sense_activation_modifier").InnerText = movement_sound_sense_activation_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "movement_sound_combined_sense_activation_modifier").InnerText = movement_sound_combined_sense_activation_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "flash_light_sense_activation_modifier").InnerText = flash_light_sense_activation_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "flash_light_combined_sense_activation_modifier").InnerText = flash_light_combined_sense_activation_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "visual_sense_activation_modifier").InnerText = visual_sense_activation_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesSense.Text, "Senses", "visual_combined_sense_activation_modifier").InnerText = visual_combined_sense_activation_modifier.Text;

            _selectedDifficulty[0].Content = doc;
            _selectedDifficulty[0].Save();

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void SaveAttributeConfig(object sender, EventArgs e)
        {
            var doc = _selectedDifficulty[0].Content;

            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "damage_dealt_scalar").InnerText = damage_dealt_scalar.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "damage_received_scalar").InnerText = damage_received_scalar.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "attack_pace_modifier_per_npc").InnerText = attack_pace_modifier_per_npc.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "attack_pace_modifier_max").InnerText = attack_pace_modifier_max.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "shooting_in_cover_duration_modifier").InnerText = shooting_in_cover_duration_modifier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "time_between_shots_scalar").InnerText = time_between_shots_scalar.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "DifficultySetting", "NPC_Generic", characterTypesAttribute.Text, "General", "suspicious_item_loop_scalar").InnerText = suspicious_item_loop_scalar.Text;

            _selectedDifficulty[0].Content = doc;
            _selectedDifficulty[0].Save();

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

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void helpBtn_Click(object sender, EventArgs e)
        {
            Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
            Process.Start("https://opencage.co.uk/docs/configs/difficulty-settings");
        }
    }
}
