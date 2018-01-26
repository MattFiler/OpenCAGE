/*
 * 
 * Created by Matt Filer
 * www.mattfiler.co.uk
 * 
 */

using Alien_Isolation_Mod_Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Xml.XPath;

namespace PackagingTool
{
    //Main Form
    public partial class CharEd : Form
    {
        //Load shared scripts
        AYZ_AttributeEditors AlienAttribute = new AYZ_AttributeEditors();

        //Common file paths
        string pathToWorkingXML;

        //On Load
        public CharEd()
        {
            InitializeComponent();

            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        //Load Class
        private void btnSelectClass_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Save selected class name
            string selectedClass = classSelection.Text;

            if (selectedClass == "")
            {
                //No class selected, can't load anything
                MessageBox.Show("Please select a class.");
            }
            else
            {
                //Load in XML
                pathToWorkingXML = AlienAttribute.convertCharacterBML(selectedClass);

                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Set base Attribute Values
                AlienAttribute.getNode("Attribute", "Template_Name", ChrAttributeXML, null, Template_Name);

                //Set Health Values
                AlienAttribute.getNode("Health", "Max_Health", ChrAttributeXML, Max_Health, null);
                AlienAttribute.getNode("Health", "Injured_State_1", ChrAttributeXML, Injured_State_1, null);
                AlienAttribute.getNode("Health", "Injured_State_2", ChrAttributeXML, Injured_State_2, null);
                AlienAttribute.getNode("Health", "Injured_State_3", ChrAttributeXML, Injured_State_3, null);
                AlienAttribute.getNode("Health", "Health_Regeneration_Rate", ChrAttributeXML, Health_Regeneration_Rate, null);

                //Set Physical_Attributes Values
                AlienAttribute.getNode("Physical_Attributes", "Mass", ChrAttributeXML, Mass, null);
                AlienAttribute.getNode("Physical_Attributes", "Default_Alliance_Group", ChrAttributeXML, null, Default_Alliance_Group);

                //Set Behavior Values
                AlienAttribute.getNode("Behavior", "Behavior_Tree", ChrAttributeXML, null, Behavior_Tree);
                AlienAttribute.getNode("Behavior", "ATTACK_GROUP", ChrAttributeXML, null, ATTACK_GROUP);
                AlienAttribute.getNode("Behavior", "TargetingSystem", ChrAttributeXML, null, TargetingSystem);

                //Set Sound_Attributes Values
                AlienAttribute.getNode("Sound_Attributes", "Character_Sound", ChrAttributeXML, null, Character_Sound);

                //Set Attack Values - Melee_Damage
                AlienAttribute.getNode("Attack/Melee_Damage", "Damage_From_Hit", ChrAttributeXML, Damage_From_Hit, null);
                AlienAttribute.getNode("Attack/Melee_Damage", "Damage_From_Grapple_Break", ChrAttributeXML, Damage_From_Grapple_Break, null);
                AlienAttribute.getNode("Attack/Melee_Damage", "Damage_From_Stealth_KO", ChrAttributeXML, Damage_From_Stealth_KO, null);

                //Set Attack Values - NPC_Shooting/Rate_of_Fire
                AlienAttribute.getNode("Attack/NPC_Shooting/Rate_of_Fire", "min_time_between_shots", ChrAttributeXML, min_time_between_shots, null);
                AlienAttribute.getNode("Attack/NPC_Shooting/Rate_of_Fire", "max_time_between_shots", ChrAttributeXML, max_time_between_shots, null);
                AlienAttribute.getNode("Attack/NPC_Shooting/Rate_of_Fire", "min_time_between_shots_for_shotgun", ChrAttributeXML, min_time_between_shots_for_shotgun, null);
                AlienAttribute.getNode("Attack/NPC_Shooting/Rate_of_Fire", "max_time_between_shots_for_shotgun", ChrAttributeXML, max_time_between_shots_for_shotgun, null);

                //Set Attack Values - NPC_Shooting/Cover
                AlienAttribute.getNode("Attack/NPC_Shooting/Cover", "percentage_chance_of_shooting_over_cover_vs_side", ChrAttributeXML, percentage_chance_of_shooting_over_cover_vs_side, null);
                AlienAttribute.getNode("Attack/NPC_Shooting/Cover", "Min_Non_Shooting_In_Cover_Interval_Time", ChrAttributeXML, Min_Non_Shooting_In_Cover_Interval_Time, null);
                AlienAttribute.getNode("Attack/NPC_Shooting/Cover", "Max_Non_Shooting_In_Cover_Interval_Time", ChrAttributeXML, Max_Non_Shooting_In_Cover_Interval_Time, null);

                //Set Attack Values - NPC_Shooting/Aiming
                AlienAttribute.getNode("Attack/NPC_Shooting/Aiming", "Max_Aiming_Accuracy_Normalised_Value", ChrAttributeXML, Max_Aiming_Accuracy_Normalised_Value, null);
                AlienAttribute.getNode("Attack/NPC_Shooting/Aiming", "Aiming_Accuracy_Radius_Multiplier", ChrAttributeXML, Aiming_Accuracy_Radius_Multiplier, null);
                //AlienAttribute.getNode("Attack/NPC_Shooting/Aiming", "Start_Shooting_Accuracy_Threshold", ChrAttributeXML, Start_Shooting_Accuracy_Threshold, null);
                //AlienAttribute.getNode("Attack/NPC_Shooting/Aiming", "Stop_Shooting_Accuracy_Threshold", ChrAttributeXML, Stop_Shooting_Accuracy_Threshold, null);
                AlienAttribute.getNode("Attack/NPC_Shooting/Aiming", "Min_Aiming_Accuracy_Normalised_Value", ChrAttributeXML, Min_Aiming_Accuracy_Normalised_Value, null);

                //Set Attack Values - NPC_Shooting/Interval
                AlienAttribute.getNode("Attack/NPC_Shooting/Interval", "Min_Shooting_Interval_Time", ChrAttributeXML, Min_Shooting_Interval_Time, null);
                AlienAttribute.getNode("Attack/NPC_Shooting/Interval", "Max_Shooting_Interval_Time", ChrAttributeXML, Max_Shooting_Interval_Time, null);
                AlienAttribute.getNode("Attack/NPC_Shooting/Interval", "Max_Non_Shooting_Interval_Time", ChrAttributeXML, Max_Non_Shooting_Interval_Time, null);
                AlienAttribute.getNode("Attack/NPC_Shooting/Interval", "Min_Non_Shooting_Interval_Time", ChrAttributeXML, Min_Non_Shooting_Interval_Time, null);

                //Set Attack Values - NPC_Shooting
                AlienAttribute.getNode("Attack/NPC_Shooting", "stop_shooting_if_no_visual_after_time", ChrAttributeXML, stop_shooting_if_no_visual_after_time, null);

                //Set Attack Values
                AlienAttribute.getNode("Attack", "min_delay_before_shooting", ChrAttributeXML, min_delay_before_shooting, null);
                AlienAttribute.getNode("Attack", "max_delay_before_shooting", ChrAttributeXML, max_delay_before_shooting, null);

                //Set Peeking Values
                AlienAttribute.getNode("Peeking", "max_vertical_peek", ChrAttributeXML, max_vertical_peek, null);
                AlienAttribute.getNode("Peeking", "max_horizontal_peek", ChrAttributeXML, max_horizontal_peek, null);
                AlienAttribute.getNode("Peeking", "max_peek_control_velocity", ChrAttributeXML, max_peek_control_velocity, null);
                AlienAttribute.getNode("Peeking", "max_auto_return_from_peek_velocity", ChrAttributeXML, max_auto_return_from_peek_velocity, null);
                AlienAttribute.getNode("Peeking", "vertical_peek_exposed_threshold", ChrAttributeXML, vertical_peek_exposed_threshold, null);
                AlienAttribute.getNode("Peeking", "horizontal_peek_exposed_threshold", ChrAttributeXML, horizontal_peek_exposed_threshold, null);
                AlienAttribute.getNode("Peeking", "grace_time_in_exposed_peek_region", ChrAttributeXML, grace_time_in_exposed_peek_region, null);
                AlienAttribute.getNode("Peeking", "peek_control_scaling", ChrAttributeXML, peek_control_scaling, null);

                //Set Locomotion Values
                AlienAttribute.getNode("Locomotion", "permittedLocomotionModulation", ChrAttributeXML, permittedLocomotionModulation, null);
                AlienAttribute.getNode("Locomotion", "capsuleRadius", ChrAttributeXML, capsuleRadius, null);

                //Set Aggrovation_Settings Values - aggro_rate
                AlienAttribute.getNode("Aggrovation_Settings/aggro_rate", "aggro_gun_aimed_rate", ChrAttributeXML, aggro_gun_aimed_rate, null);
                AlienAttribute.getNode("Aggrovation_Settings/aggro_rate", "aggro_warning_rate", ChrAttributeXML, aggro_warning_rate, null);
                AlienAttribute.getNode("Aggrovation_Settings/aggro_rate", "aggro_interrogative_rate", ChrAttributeXML, aggro_interrogative_rate, null);
                AlienAttribute.getNode("Aggrovation_Settings/aggro_rate", "aggro_standdown_rate", ChrAttributeXML, aggro_standdown_rate, null);

                //Set Aggrovation_Settings Values - aggro_distances
                AlienAttribute.getNode("Aggrovation_Settings/aggro_distances", "aggro_aggressive_distance", ChrAttributeXML, aggro_aggressive_distance, null);
                AlienAttribute.getNode("Aggrovation_Settings/aggro_distances", "aggro_warning_distance", ChrAttributeXML, aggro_warning_distance, null);
                AlienAttribute.getNode("Aggrovation_Settings/aggro_distances", "aggro_interrogative_distance", ChrAttributeXML, aggro_interrogative_distance, null);
                AlienAttribute.getNode("Aggrovation_Settings/aggro_distances", "aggro_standdown_distance", ChrAttributeXML, aggro_standdown_distance, null);

                //Set Cover Values
                AlienAttribute.getNode("Cover", "cover_job_minimum_distance_to_player", ChrAttributeXML, cover_job_minimum_distance_to_player, null);

                //Set Hiding Values
                AlienAttribute.getNode("Hiding", "hiding_search_radius", ChrAttributeXML, hiding_search_radius, null);
                AlienAttribute.getNode("Hiding", "hiding_post_search_exclusion_radius", ChrAttributeXML, hiding_post_search_exclusion_radius, null);
                AlienAttribute.getNode("Hiding", "hiding_hearing_range", ChrAttributeXML, hiding_hearing_range, null);
                AlienAttribute.getNode("Hiding", "hiding_max_search_time", ChrAttributeXML, hiding_max_search_time, null);
                AlienAttribute.getNode("Hiding", "hiding_max_QTE_selection_weighting", ChrAttributeXML, hiding_max_QTE_selection_weighting, null);
                AlienAttribute.getNode("Hiding", "chance_hiding_not_chosen", ChrAttributeXML, chance_hiding_not_chosen, null);

                //Set Job_Behaviour Values
                AlienAttribute.getNode("Job_Behaviour", "max_range_to_search_for_IDLE_job", ChrAttributeXML, max_range_to_search_for_IDLE_job, null);
                AlienAttribute.getNode("Job_Behaviour", "max_range_to_search_for_ESCALATION_jobs", ChrAttributeXML, max_range_to_search_for_ESCALATION_jobs, null);
                AlienAttribute.getNode("Job_Behaviour", "min_time_between_idles_for_stalk", ChrAttributeXML, min_time_between_idles_for_stalk, null);

                //Set Defence Values
                AlienAttribute.getNode("Defence", "defense_gauge_decay_rate", ChrAttributeXML, defense_gauge_decay_rate, null);
                AlienAttribute.getNode("Defence", "defense_gauge_decay_delay", ChrAttributeXML, defense_gauge_decay_delay, null);
                AlienAttribute.getNode("Defence", "alien_stun_damage_guage_decrease_per_sec", ChrAttributeXML, alien_stun_damage_guage_decrease_per_sec, null);
                AlienAttribute.getNode("Defence", "EMP_Stunned_Damage_Taken_Multiplier", ChrAttributeXML, EMP_Stunned_Damage_Taken_Multiplier, null);
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }


        //Save Class Attributes
        private void btnSave_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Save selected class name
            string selectedClass = classSelection.Text;

            if (pathToWorkingXML == null)
            {
                //No class loaded - can't save
                MessageBox.Show("Please load a class first!");
            }
            else
            { 
                //Load-in XML to edit
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Set base Attribute Values
                AlienAttribute.setNode("Attribute", "Template_Name", ChrAttributeXML, null, Template_Name);

                //Set Health Values
                AlienAttribute.setNode("Health", "Max_Health", ChrAttributeXML, Max_Health, null);
                AlienAttribute.setNode("Health", "Injured_State_1", ChrAttributeXML, Injured_State_1, null);
                AlienAttribute.setNode("Health", "Injured_State_2", ChrAttributeXML, Injured_State_2, null);
                AlienAttribute.setNode("Health", "Injured_State_3", ChrAttributeXML, Injured_State_3, null);
                AlienAttribute.setNode("Health", "Health_Regeneration_Rate", ChrAttributeXML, Health_Regeneration_Rate, null);

                //Set Physical_Attributes Values
                AlienAttribute.setNode("Physical_Attributes", "Mass", ChrAttributeXML, Mass, null);
                AlienAttribute.setNode("Physical_Attributes", "Default_Alliance_Group", ChrAttributeXML, null, Default_Alliance_Group);

                //Set Behavior Values
                AlienAttribute.setNode("Behavior", "Behavior_Tree", ChrAttributeXML, null, Behavior_Tree);
                AlienAttribute.setNode("Behavior", "ATTACK_GROUP", ChrAttributeXML, null, ATTACK_GROUP);
                AlienAttribute.setNode("Behavior", "TargetingSystem", ChrAttributeXML, null, TargetingSystem);

                //Set Sound_Attributes Values
                AlienAttribute.setNode("Sound_Attributes", "Character_Sound", ChrAttributeXML, null, Character_Sound);

                //Set Attack Values - Melee_Damage
                AlienAttribute.setNode("Attack/Melee_Damage", "Damage_From_Hit", ChrAttributeXML, Damage_From_Hit, null);
                AlienAttribute.setNode("Attack/Melee_Damage", "Damage_From_Grapple_Break", ChrAttributeXML, Damage_From_Grapple_Break, null);
                AlienAttribute.setNode("Attack/Melee_Damage", "Damage_From_Stealth_KO", ChrAttributeXML, Damage_From_Stealth_KO, null);

                //Set Attack Values - NPC_Shooting/Rate_of_Fire
                AlienAttribute.setNode("Attack/NPC_Shooting/Rate_of_Fire", "min_time_between_shots", ChrAttributeXML, min_time_between_shots, null);
                AlienAttribute.setNode("Attack/NPC_Shooting/Rate_of_Fire", "max_time_between_shots", ChrAttributeXML, max_time_between_shots, null);
                AlienAttribute.setNode("Attack/NPC_Shooting/Rate_of_Fire", "min_time_between_shots_for_shotgun", ChrAttributeXML, min_time_between_shots_for_shotgun, null);
                AlienAttribute.setNode("Attack/NPC_Shooting/Rate_of_Fire", "max_time_between_shots_for_shotgun", ChrAttributeXML, max_time_between_shots_for_shotgun, null);

                //Set Attack Values - NPC_Shooting/Cover
                AlienAttribute.setNode("Attack/NPC_Shooting/Cover", "percentage_chance_of_shooting_over_cover_vs_side", ChrAttributeXML, percentage_chance_of_shooting_over_cover_vs_side, null);
                AlienAttribute.setNode("Attack/NPC_Shooting/Cover", "Min_Non_Shooting_In_Cover_Interval_Time", ChrAttributeXML, Min_Non_Shooting_In_Cover_Interval_Time, null);
                AlienAttribute.setNode("Attack/NPC_Shooting/Cover", "Max_Non_Shooting_In_Cover_Interval_Time", ChrAttributeXML, Max_Non_Shooting_In_Cover_Interval_Time, null);

                //Set Attack Values - NPC_Shooting/Aiming
                AlienAttribute.setNode("Attack/NPC_Shooting/Aiming", "Max_Aiming_Accuracy_Normalised_Value", ChrAttributeXML, Max_Aiming_Accuracy_Normalised_Value, null);
                AlienAttribute.setNode("Attack/NPC_Shooting/Aiming", "Aiming_Accuracy_Radius_Multiplier", ChrAttributeXML, Aiming_Accuracy_Radius_Multiplier, null);
                //AlienAttribute.setNode("Attack/NPC_Shooting/Aiming", "Start_Shooting_Accuracy_Threshold", ChrAttributeXML, Start_Shooting_Accuracy_Threshold, null);
                //AlienAttribute.setNode("Attack/NPC_Shooting/Aiming", "Stop_Shooting_Accuracy_Threshold", ChrAttributeXML, Stop_Shooting_Accuracy_Threshold, null);
                AlienAttribute.setNode("Attack/NPC_Shooting/Aiming", "Min_Aiming_Accuracy_Normalised_Value", ChrAttributeXML, Min_Aiming_Accuracy_Normalised_Value, null);

                //Set Attack Values - NPC_Shooting/Interval
                AlienAttribute.setNode("Attack/NPC_Shooting/Interval", "Min_Shooting_Interval_Time", ChrAttributeXML, Min_Shooting_Interval_Time, null);
                AlienAttribute.setNode("Attack/NPC_Shooting/Interval", "Max_Shooting_Interval_Time", ChrAttributeXML, Max_Shooting_Interval_Time, null);
                AlienAttribute.setNode("Attack/NPC_Shooting/Interval", "Max_Non_Shooting_Interval_Time", ChrAttributeXML, Max_Non_Shooting_Interval_Time, null);
                AlienAttribute.setNode("Attack/NPC_Shooting/Interval", "Min_Non_Shooting_Interval_Time", ChrAttributeXML, Min_Non_Shooting_Interval_Time, null);

                //Set Attack Values - NPC_Shooting
                AlienAttribute.setNode("Attack/NPC_Shooting", "stop_shooting_if_no_visual_after_time", ChrAttributeXML, stop_shooting_if_no_visual_after_time, null);

                //Set Attack Values
                AlienAttribute.setNode("Attack", "min_delay_before_shooting", ChrAttributeXML, min_delay_before_shooting, null);
                AlienAttribute.setNode("Attack", "max_delay_before_shooting", ChrAttributeXML, max_delay_before_shooting, null);

                //Set Peeking Values
                AlienAttribute.setNode("Peeking", "max_vertical_peek", ChrAttributeXML, max_vertical_peek, null);
                AlienAttribute.setNode("Peeking", "max_horizontal_peek", ChrAttributeXML, max_horizontal_peek, null);
                AlienAttribute.setNode("Peeking", "max_peek_control_velocity", ChrAttributeXML, max_peek_control_velocity, null);
                AlienAttribute.setNode("Peeking", "max_auto_return_from_peek_velocity", ChrAttributeXML, max_auto_return_from_peek_velocity, null);
                AlienAttribute.setNode("Peeking", "vertical_peek_exposed_threshold", ChrAttributeXML, vertical_peek_exposed_threshold, null);
                AlienAttribute.setNode("Peeking", "horizontal_peek_exposed_threshold", ChrAttributeXML, horizontal_peek_exposed_threshold, null);
                AlienAttribute.setNode("Peeking", "grace_time_in_exposed_peek_region", ChrAttributeXML, grace_time_in_exposed_peek_region, null);
                AlienAttribute.setNode("Peeking", "peek_control_scaling", ChrAttributeXML, peek_control_scaling, null);

                //Set Locomotion Values
                AlienAttribute.setNode("Locomotion", "permittedLocomotionModulation", ChrAttributeXML, permittedLocomotionModulation, null);
                AlienAttribute.setNode("Locomotion", "capsuleRadius", ChrAttributeXML, capsuleRadius, null);

                //Set Aggrovation_Settings Values - aggro_rate
                AlienAttribute.setNode("Aggrovation_Settings/aggro_rate", "aggro_gun_aimed_rate", ChrAttributeXML, aggro_gun_aimed_rate, null);
                AlienAttribute.setNode("Aggrovation_Settings/aggro_rate", "aggro_warning_rate", ChrAttributeXML, aggro_warning_rate, null);
                AlienAttribute.setNode("Aggrovation_Settings/aggro_rate", "aggro_interrogative_rate", ChrAttributeXML, aggro_interrogative_rate, null);
                AlienAttribute.setNode("Aggrovation_Settings/aggro_rate", "aggro_standdown_rate", ChrAttributeXML, aggro_standdown_rate, null);

                //Set Aggrovation_Settings Values - aggro_distances
                AlienAttribute.setNode("Aggrovation_Settings/aggro_distances", "aggro_aggressive_distance", ChrAttributeXML, aggro_aggressive_distance, null);
                AlienAttribute.setNode("Aggrovation_Settings/aggro_distances", "aggro_warning_distance", ChrAttributeXML, aggro_warning_distance, null);
                AlienAttribute.setNode("Aggrovation_Settings/aggro_distances", "aggro_interrogative_distance", ChrAttributeXML, aggro_interrogative_distance, null);
                AlienAttribute.setNode("Aggrovation_Settings/aggro_distances", "aggro_standdown_distance", ChrAttributeXML, aggro_standdown_distance, null);

                //Set Cover Values
                AlienAttribute.setNode("Cover", "cover_job_minimum_distance_to_player", ChrAttributeXML, cover_job_minimum_distance_to_player, null);

                //Set Hiding Values
                AlienAttribute.setNode("Hiding", "hiding_search_radius", ChrAttributeXML, hiding_search_radius, null);
                AlienAttribute.setNode("Hiding", "hiding_post_search_exclusion_radius", ChrAttributeXML, hiding_post_search_exclusion_radius, null);
                AlienAttribute.setNode("Hiding", "hiding_hearing_range", ChrAttributeXML, hiding_hearing_range, null);
                AlienAttribute.setNode("Hiding", "hiding_max_search_time", ChrAttributeXML, hiding_max_search_time, null);
                AlienAttribute.setNode("Hiding", "hiding_max_QTE_selection_weighting", ChrAttributeXML, hiding_max_QTE_selection_weighting, null);
                AlienAttribute.setNode("Hiding", "chance_hiding_not_chosen", ChrAttributeXML, chance_hiding_not_chosen, null);

                //Set Job_Behaviour Values
                AlienAttribute.setNode("Job_Behaviour", "max_range_to_search_for_IDLE_job", ChrAttributeXML, max_range_to_search_for_IDLE_job, null);
                AlienAttribute.setNode("Job_Behaviour", "max_range_to_search_for_ESCALATION_jobs", ChrAttributeXML, max_range_to_search_for_ESCALATION_jobs, null);
                AlienAttribute.setNode("Job_Behaviour", "min_time_between_idles_for_stalk", ChrAttributeXML, min_time_between_idles_for_stalk, null);

                //Set Defence Values
                AlienAttribute.setNode("Defence", "defense_gauge_decay_rate", ChrAttributeXML, defense_gauge_decay_rate, null);
                AlienAttribute.setNode("Defence", "defense_gauge_decay_delay", ChrAttributeXML, defense_gauge_decay_delay, null);
                AlienAttribute.setNode("Defence", "alien_stun_damage_guage_decrease_per_sec", ChrAttributeXML, alien_stun_damage_guage_decrease_per_sec, null);
                AlienAttribute.setNode("Defence", "EMP_Stunned_Damage_Taken_Multiplier", ChrAttributeXML, EMP_Stunned_Damage_Taken_Multiplier, null);

                //Save values
                if (AlienAttribute.saveCharacterBML(selectedClass, ChrAttributeXML))
                {
                    MessageBox.Show("Saved new attribute values.");
                }
                else
                {
                    MessageBox.Show("An error occured while saving.");
                }
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }
    }
}
