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
using System.Windows.Controls;
using System.Windows.Forms;
using System.Xml;

namespace OpenCAGE.ConfigEditors
{
    public partial class AttributesEditor : BaseWindow
    {
        List<BML> _selectedCharacter;

        public AttributesEditor() : base()
        {
            InitializeComponent();
            ConfigEditorUtils.ExpandNumericRanges(this.Controls);

            BML behaviourTrees = new BML(Singleton.PathToAI + "\\DATA\\BINARY_BEHAVIOR\\_DIRECTORY_CONTENTS.BML");
            Behavior_Tree.BeginUpdate();
            try
            {
                var behaviours = behaviourTrees.Loaded ? behaviourTrees.Content?["DIR"] : null;
                if (behaviours != null)
                {
                    foreach (XmlElement behaviour in behaviours)
                    {
                        if (behaviour.Name != "File")
                            continue;

                        Behavior_Tree.Items.Add(Path.GetFileNameWithoutExtension(behaviour.GetAttribute("name")));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("AttributesEditor", "Failed to enumerate behaviour trees: " + ex.Message);
            }
            Behavior_Tree.EndUpdate();

            Character_Sound.BeginUpdate();
            Character_Sound.Items.Add("PLAYER1");
            Character_Sound.Items.Add("PLAYER2");
            Character_Sound.Items.Add("ALIEN");
            Character_Sound.Items.Add("FACEHUGGER");
            Character_Sound.Items.Add("ANDROID");
            Character_Sound.Items.Add("CIVILIAN");
            Character_Sound.Items.Add("SECURITY_GUARD");
            Character_Sound.EndUpdate();

            TargetingSystem.BeginUpdate();
            TargetingSystem.Items.Add("TM_NONE");
            TargetingSystem.Items.Add("TM_ALIEN");
            TargetingSystem.Items.Add("TM_FACEHUGGER");
            TargetingSystem.Items.Add("TM_ANDROID");
            TargetingSystem.Items.Add("TM_HUMAN");
            TargetingSystem.Items.Add("TM_PLAYER");
            TargetingSystem.EndUpdate();

            ATTACK_GROUP.BeginUpdate();
            ATTACK_GROUP.Items.Add("AT_NONE");
            ATTACK_GROUP.Items.Add("AT_FHUGGER");
            ATTACK_GROUP.Items.Add("AT_ANDROID");
            ATTACK_GROUP.Items.Add("AT_HUMAN");
            ATTACK_GROUP.Items.Add("AT_ALIEN");
            ATTACK_GROUP.EndUpdate();

            BML attributeTypes = new BML(Singleton.PathToAI + "\\DATA\\CHR_INFO\\ATTRIBUTES\\ATTRIBUTES.BML");
            characters.BeginUpdate();
            try
            {
                var attributes = attributeTypes.Loaded ? attributeTypes.Content?["Attributes"] : null;
                if (attributes != null)
                {
                    foreach (XmlElement attribute in attributes)
                    {
                        string name = attribute?["Name"]?.InnerText;
                        if (!string.IsNullOrEmpty(name))
                            characters.Items.Add(name);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log("AttributesEditor", "Failed to enumerate attributes: " + ex.Message);
            }
            characters.EndUpdate();

            if (characters.Items.Count == 0)
            {
                MessageBox.Show(
                    "Could not load character attribute definitions from ATTRIBUTES.BML.",
                    "Attributes Editor",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            this.FormClosing += AttributesEditor_FormClosing;
            Singleton.OnResetConfigs += () => { this.Close(); };
        }

        private void AttributesEditor_Load(object sender, EventArgs e)
        {
            if (characters.Items.Count == 0)
                return;

            // Ensure missing attributes exist on each character file, but don't crash the whole editor on one bad BML
            for (int i = 0; i < characters.Items.Count; i++)
            {
                try
                {
                    characters.SelectedIndex = i;
                    if (_selectedCharacter == null || _selectedCharacter.Count == 0 || !_selectedCharacter[0].Loaded)
                        continue;
                    Save(null, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    Debug.Log("AttributesEditor", "Migrate failed for " + characters.Items[i] + ": " + ex.Message);
                }
            }

            try
            {
                characters.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                Debug.Log("AttributesEditor", "Failed to select first character: " + ex.Message);
            }
        }

        private void AttributesEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(this.Controls, Save);
            this.FormClosing -= AttributesEditor_FormClosing;
        }

        private void characters_SelectedIndexChanged(object sender, EventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(this.Controls, Save);

            _selectedCharacter = new List<BML>();
            if (string.IsNullOrWhiteSpace(characters.Text))
                return;

            string attributePath = Singleton.PathToAI + "\\DATA\\CHR_INFO\\ATTRIBUTES\\" + characters.Text + ".BML";
            BML primary = new BML(attributePath);
            _selectedCharacter.Add(primary);

            if (!primary.Loaded)
            {
                MessageBox.Show(
                    "Failed to load character attributes:\n" + attributePath,
                    "Attributes load failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                while (true)
                {
                    XmlElement attributeRoot = _selectedCharacter[_selectedCharacter.Count - 1].Content?["Attribute"];
                    string template = attributeRoot?["Template_Name"]?.InnerText;
                    if (string.IsNullOrEmpty(template))
                        break;

                    string templatePath = Singleton.PathToAI + "\\DATA\\CHR_INFO\\ATTRIBUTES\\" + template + ".BML";
                    BML templateBml = new BML(templatePath);
                    if (!templateBml.Loaded)
                        break;

                    _selectedCharacter.Add(templateBml);
                }
            }
            catch (Exception ex)
            {
                Debug.Log("AttributesEditor", "Template chain failed: " + ex.Message);
            }

            ConfigEditorUtils.SetNumber(_selectedCharacter, Max_Health, "Attribute", "Health", "Max_Health");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Injured_State_1, "Attribute", "Health", "Injured_State_1");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Injured_State_2, "Attribute", "Health", "Injured_State_2");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Injured_State_3, "Attribute", "Health", "Injured_State_3");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Health_Regeneration_Rate, "Attribute", "Health", "Health_Regeneration_Rate");
            ConfigEditorUtils.SetCombo(_selectedCharacter, Behavior_Tree, "Attribute", "Behavior", "Behavior_Tree");
            ConfigEditorUtils.SetCombo(_selectedCharacter, ATTACK_GROUP, "Attribute", "Behavior", "ATTACK_GROUP");
            ConfigEditorUtils.SetCombo(_selectedCharacter, TargetingSystem, "Attribute", "Behavior", "TargetingSystem");
            ConfigEditorUtils.SetNumber(_selectedCharacter, alien_stun_damage_guage_decrease_per_sec, "Attribute", "Defence", "alien_stun_damage_guage_decrease_per_sec");
            ConfigEditorUtils.SetNumber(_selectedCharacter, EMP_Stunned_Damage_Taken_Multiplier, "Attribute", "Defence", "EMP_Stunned_Damage_Taken_Multiplier");
            ConfigEditorUtils.SetNumber(_selectedCharacter, put_melee_weapon_away_time, "Attribute", "Putting_weapons_away", "put_melee_weapon_away_time");
            ConfigEditorUtils.SetNumber(_selectedCharacter, percentage_chance_of_shooting_over_cover_vs_side, "Attribute", "Attack", "NPC_Shooting", "Cover", "percentage_chance_of_shooting_over_cover_vs_side");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Min_Non_Shooting_In_Cover_Interval_Time, "Attribute", "Attack", "NPC_Shooting", "Cover", "Min_Non_Shooting_In_Cover_Interval_Time");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Max_Non_Shooting_In_Cover_Interval_Time, "Attribute", "Attack", "NPC_Shooting", "Cover", "Max_Non_Shooting_In_Cover_Interval_Time");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Min_Shooting_In_Cover_Interval_Time, "Attribute", "Attack", "NPC_Shooting", "Cover", "Min_Shooting_In_Cover_Interval_Time");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Max_Shooting_In_Cover_Interval_Time, "Attribute", "Attack", "NPC_Shooting", "Cover", "Max_Shooting_In_Cover_Interval_Time");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Damage_From_Hit, "Attribute", "Attack", "Melee_Damage", "Damage_From_Hit");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Damage_From_Grapple_Break, "Attribute", "Attack", "Melee_Damage", "Damage_From_Grapple_Break");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Grapple_Damage_Per_Tick, "Attribute", "Attack", "Melee_Damage", "Grapple_Damage_Per_Tick");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Grapple_Attack_Interval, "Attribute", "Attack", "Melee_Damage", "Grapple_Attack_Interval");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Grapple_Attack_Interval_After_Block, "Attribute", "Attack", "Melee_Damage", "Grapple_Attack_Interval_After_Block");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Grapple_Gauge_Decay_Rate, "Attribute", "Attack", "Melee_Damage", "Grapple_Gauge_Decay_Rate");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Grapple_Gauge_Presses_Required, "Attribute", "Attack", "Melee_Damage", "Grapple_Gauge_Presses_Required");
            ConfigEditorUtils.SetNumber(_selectedCharacter, min_time_between_shots, "Attribute", "Attack", "NPC_Shooting", "Rate_of_Fire", "min_time_between_shots");
            ConfigEditorUtils.SetNumber(_selectedCharacter, max_time_between_shots, "Attribute", "Attack", "NPC_Shooting", "Rate_of_Fire", "max_time_between_shots");
            ConfigEditorUtils.SetNumber(_selectedCharacter, min_time_between_shots_for_shotgun, "Attribute", "Attack", "NPC_Shooting", "Rate_of_Fire", "min_time_between_shots_for_shotgun");
            ConfigEditorUtils.SetNumber(_selectedCharacter, max_time_between_shots_for_shotgun, "Attribute", "Attack", "NPC_Shooting", "Rate_of_Fire", "max_time_between_shots_for_shotgun");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Max_Aiming_Accuracy_Normalised_Value, "Attribute", "Attack", "NPC_Shooting", "Aiming", "Max_Aiming_Accuracy_Normalised_Value");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Min_Aiming_Accuracy_Normalised_Value, "Attribute", "Attack", "NPC_Shooting", "Aiming", "Min_Aiming_Accuracy_Normalised_Value");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Min_Shooting_Interval_Time, "Attribute", "Attack", "NPC_Shooting", "Interval", "Min_Shooting_Interval_Time");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Max_Shooting_Interval_Time, "Attribute", "Attack", "NPC_Shooting", "Interval", "Max_Shooting_Interval_Time");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Max_Non_Shooting_Interval_Time, "Attribute", "Attack", "NPC_Shooting", "Interval", "Max_Non_Shooting_Interval_Time");
            ConfigEditorUtils.SetNumber(_selectedCharacter, Min_Non_Shooting_Interval_Time, "Attribute", "Attack", "NPC_Shooting", "Interval", "Min_Non_Shooting_Interval_Time");
            ConfigEditorUtils.SetNumber(_selectedCharacter, stop_shooting_if_no_visual_after_time, "Attribute", "Attack", "NPC_Shooting", "stop_shooting_if_no_visual_after_time");
            ConfigEditorUtils.SetNumber(_selectedCharacter, min_delay_before_shooting, "Attribute", "Attack", "min_delay_before_shooting");
            ConfigEditorUtils.SetNumber(_selectedCharacter, max_delay_before_shooting, "Attribute", "Attack", "max_delay_before_shooting");
            ConfigEditorUtils.SetNumber(_selectedCharacter, cover_job_minimum_distance_to_player, "Attribute", "Cover", "cover_job_minimum_distance_to_player");
            ConfigEditorUtils.SetNumber(_selectedCharacter, footstep_loudness_maxed_out_speed, "Attribute", "Footsteps_sense_generation", "footstep_loudness_maxed_out_speed");
            ConfigEditorUtils.SetNumber(_selectedCharacter, footstep_loudness_min_speed_considered, "Attribute", "Footsteps_sense_generation", "footstep_loudness_min_speed_considered");
            ConfigEditorUtils.SetNumber(_selectedCharacter, footstep_loudness_min_clamped_input_loudness, "Attribute", "Footsteps_sense_generation", "footstep_loudness_min_clamped_input_loudness");
            ConfigEditorUtils.SetNumber(_selectedCharacter, footstep_loudness_max_clamped_input_loudness, "Attribute", "Footsteps_sense_generation", "footstep_loudness_max_clamped_input_loudness");
            ConfigEditorUtils.SetNumber(_selectedCharacter, footstep_loudness_min_scaled_output_loudness, "Attribute", "Footsteps_sense_generation", "footstep_loudness_min_scaled_output_loudness");
            ConfigEditorUtils.SetNumber(_selectedCharacter, footstep_loudness_max_scaled_output_loudness, "Attribute", "Footsteps_sense_generation", "footstep_loudness_max_scaled_output_loudness");
            ConfigEditorUtils.SetNumber(_selectedCharacter, footstep_loudness_crouch_multiplier, "Attribute", "Footsteps_sense_generation", "footstep_loudness_crouch_multiplier");
            ConfigEditorUtils.SetNumber(_selectedCharacter, hiding_max_search_time, "Attribute", "Hiding", "hiding_max_search_time");
            ConfigEditorUtils.SetNumber(_selectedCharacter, hiding_search_radius, "Attribute", "Hiding", "hiding_search_radius");
            ConfigEditorUtils.SetNumber(_selectedCharacter, hiding_post_search_exclusion_radius, "Attribute", "Hiding", "hiding_post_search_exclusion_radius");
            ConfigEditorUtils.SetNumber(_selectedCharacter, hiding_max_QTE_selection_weighting, "Attribute", "Hiding", "hiding_max_QTE_selection_weighting");
            ConfigEditorUtils.SetNumber(_selectedCharacter, hiding_hearing_range, "Attribute", "Hiding", "hiding_hearing_range");
            ConfigEditorUtils.SetCombo(_selectedCharacter, Character_Sound, "Attribute", "Sound_Attributes", "Character_Sound");
            ConfigEditorUtils.SetNumber(_selectedCharacter, max_range_to_search_for_IDLE_job, "Attribute", "Job_Behaviour", "max_range_to_search_for_IDLE_job");
            ConfigEditorUtils.SetNumber(_selectedCharacter, min_time_between_idles_for_stalk, "Attribute", "Job_Behaviour", "min_time_between_idles_for_stalk");

            ConfigEditorUtils.Subscribe(this.Controls, Save);
        }

        private void Save(object sender, EventArgs e)
        {
            if (_selectedCharacter == null || _selectedCharacter.Count == 0 || _selectedCharacter[0] == null || !_selectedCharacter[0].Loaded)
                return;

            var doc = _selectedCharacter[0].Content;
            if (doc == null)
                return;

            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Health", "Max_Health").InnerText = Max_Health.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Health", "Injured_State_1").InnerText = Injured_State_1.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Health", "Injured_State_2").InnerText = Injured_State_2.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Health", "Injured_State_3").InnerText = Injured_State_3.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Health", "Health_Regeneration_Rate").InnerText = Health_Regeneration_Rate.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Behavior", "Behavior_Tree").InnerText = Behavior_Tree.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Behavior", "ATTACK_GROUP").InnerText = ATTACK_GROUP.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Behavior", "TargetingSystem").InnerText = TargetingSystem.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Defence", "alien_stun_damage_guage_decrease_per_sec").InnerText = alien_stun_damage_guage_decrease_per_sec.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Defence", "EMP_Stunned_Damage_Taken_Multiplier").InnerText = EMP_Stunned_Damage_Taken_Multiplier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Putting_weapons_away", "put_melee_weapon_away_time").InnerText = put_melee_weapon_away_time.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "NPC_Shooting", "Cover", "percentage_chance_of_shooting_over_cover_vs_side").InnerText = percentage_chance_of_shooting_over_cover_vs_side.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "NPC_Shooting", "Cover", "Min_Non_Shooting_In_Cover_Interval_Time").InnerText = Min_Non_Shooting_In_Cover_Interval_Time.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "NPC_Shooting", "Cover", "Max_Non_Shooting_In_Cover_Interval_Time").InnerText = Max_Non_Shooting_In_Cover_Interval_Time.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "NPC_Shooting", "Cover", "Min_Shooting_In_Cover_Interval_Time").InnerText = Min_Shooting_In_Cover_Interval_Time.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "NPC_Shooting", "Cover", "Max_Shooting_In_Cover_Interval_Time").InnerText = Max_Shooting_In_Cover_Interval_Time.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "Melee_Damage", "Damage_From_Hit").InnerText = Damage_From_Hit.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "Melee_Damage", "Damage_From_Grapple_Break").InnerText = Damage_From_Grapple_Break.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "Melee_Damage", "Grapple_Damage_Per_Tick").InnerText = Grapple_Damage_Per_Tick.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "Melee_Damage", "Grapple_Attack_Interval").InnerText = Grapple_Attack_Interval.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "Melee_Damage", "Grapple_Attack_Interval_After_Block").InnerText = Grapple_Attack_Interval_After_Block.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "Melee_Damage", "Grapple_Gauge_Decay_Rate").InnerText = Grapple_Gauge_Decay_Rate.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "Melee_Damage", "Grapple_Gauge_Presses_Required").InnerText = Grapple_Gauge_Presses_Required.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "NPC_Shooting", "Rate_of_Fire", "min_time_between_shots").InnerText = min_time_between_shots.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "NPC_Shooting", "Rate_of_Fire", "max_time_between_shots").InnerText = max_time_between_shots.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "NPC_Shooting", "Rate_of_Fire", "min_time_between_shots_for_shotgun").InnerText = min_time_between_shots_for_shotgun.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "NPC_Shooting", "Rate_of_Fire", "max_time_between_shots_for_shotgun").InnerText = max_time_between_shots_for_shotgun.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "NPC_Shooting", "Aiming", "Max_Aiming_Accuracy_Normalised_Value").InnerText = Max_Aiming_Accuracy_Normalised_Value.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "NPC_Shooting", "Aiming", "Min_Aiming_Accuracy_Normalised_Value").InnerText = Min_Aiming_Accuracy_Normalised_Value.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "NPC_Shooting", "Interval", "Min_Shooting_Interval_Time").InnerText = Min_Shooting_Interval_Time.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "NPC_Shooting", "Interval", "Max_Shooting_Interval_Time").InnerText = Max_Shooting_Interval_Time.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "NPC_Shooting", "Interval", "Max_Non_Shooting_Interval_Time").InnerText = Max_Non_Shooting_Interval_Time.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "NPC_Shooting", "Interval", "Min_Non_Shooting_Interval_Time").InnerText = Min_Non_Shooting_Interval_Time.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "NPC_Shooting", "stop_shooting_if_no_visual_after_time").InnerText = stop_shooting_if_no_visual_after_time.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "min_delay_before_shooting").InnerText = min_delay_before_shooting.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Attack", "max_delay_before_shooting").InnerText = max_delay_before_shooting.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Cover", "cover_job_minimum_distance_to_player").InnerText = cover_job_minimum_distance_to_player.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Footsteps_sense_generation", "footstep_loudness_maxed_out_speed").InnerText = footstep_loudness_maxed_out_speed.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Footsteps_sense_generation", "footstep_loudness_min_speed_considered").InnerText = footstep_loudness_min_speed_considered.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Footsteps_sense_generation", "footstep_loudness_min_clamped_input_loudness").InnerText = footstep_loudness_min_clamped_input_loudness.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Footsteps_sense_generation", "footstep_loudness_max_clamped_input_loudness").InnerText = footstep_loudness_max_clamped_input_loudness.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Footsteps_sense_generation", "footstep_loudness_min_scaled_output_loudness").InnerText = footstep_loudness_min_scaled_output_loudness.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Footsteps_sense_generation", "footstep_loudness_max_scaled_output_loudness").InnerText = footstep_loudness_max_scaled_output_loudness.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Footsteps_sense_generation", "footstep_loudness_crouch_multiplier").InnerText = footstep_loudness_crouch_multiplier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Hiding", "hiding_max_search_time").InnerText = hiding_max_search_time.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Hiding", "hiding_search_radius").InnerText = hiding_search_radius.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Hiding", "hiding_post_search_exclusion_radius").InnerText = hiding_post_search_exclusion_radius.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Hiding", "hiding_max_QTE_selection_weighting").InnerText = hiding_max_QTE_selection_weighting.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Hiding", "hiding_hearing_range").InnerText = hiding_hearing_range.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Sound_Attributes", "Character_Sound").InnerText = Character_Sound.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Job_Behaviour", "max_range_to_search_for_IDLE_job").InnerText = max_range_to_search_for_IDLE_job.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Job_Behaviour", "min_time_between_idles_for_stalk").InnerText = min_time_between_idles_for_stalk.Text;

            _selectedCharacter[0].Content = doc;
            _selectedCharacter[0].Save();

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void helpBtn_Click(object sender, EventArgs e)
        {
            Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
            Process.Start("https://opencage.co.uk/docs/configs/attributes");
        }
    }
}
