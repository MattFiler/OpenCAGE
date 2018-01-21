/*
 * 
 * Created by Matt Filer
 * www.mattfiler.co.uk
 * 
 */

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
        //Main Directories
        string workingDirectory = Directory.GetCurrentDirectory() + @"\Attribute Editor Directory\"; //Our working dir
        string gameDirectory = File.ReadAllText(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz"); //Our game's dir

        //Common file paths
        string pathToWorkingBML;
        string pathToGameBML;
        string pathToWorkingXML;

        //On Load
        public CharEd()
        {
            InitializeComponent();

            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;

            //Disable Health Textboxes
            Max_Health.Enabled = false;
            Injured_State_1.Enabled = false;
            Injured_State_2.Enabled = false;
            Injured_State_3.Enabled = false;
            Health_Regeneration_Rate.Enabled = false;

            //Disable Physical_Attributes Textboxes
            Mass.Enabled = false;
            Default_Alliance_Group.Enabled = false;

            //Disable Behavior Textboxes
            Behavior_Tree.Enabled = false;
            ATTACK_GROUP.Enabled = false;
            TargetingSystem.Enabled = false;

            //Disable Sound_Attributes Textboxes
            Character_Sound.Enabled = false;
            
            //Set Attack Values - Melee_Damage
            Damage_From_Hit.Enabled = false;
            Damage_From_Grapple_Break.Enabled = false;
            Damage_From_Stealth_KO.Enabled = false;

            //Set Attack Values - NPC_Shooting/Rate_of_Fire
            min_time_between_shots.Enabled = false;
            max_time_between_shots.Enabled = false;
            min_time_between_shots_for_shotgun.Enabled = false;
            max_time_between_shots_for_shotgun.Enabled = false;

            //Set Attack Values - NPC_Shooting/Cover
            percentage_chance_of_shooting_over_cover_vs_side.Enabled = false;
            Min_Non_Shooting_In_Cover_Interval_Time.Enabled = false;
            Max_Non_Shooting_In_Cover_Interval_Time.Enabled = false;

            //Set Attack Values - NPC_Shooting/Aiming
            Max_Aiming_Accuracy_Normalised_Value.Enabled = false;
            Aiming_Accuracy_Radius_Multiplier.Enabled = false;
            //Start_Shooting_Accuracy_Threshold.Enabled = false;
            //Stop_Shooting_Accuracy_Threshold.Enabled = false;
            Min_Aiming_Accuracy_Normalised_Value.Enabled = false;

            //Set Attack Values - NPC_Shooting/Interval
            Min_Shooting_Interval_Time.Enabled = false;
            Max_Shooting_Interval_Time.Enabled = false;
            Max_Non_Shooting_Interval_Time.Enabled = false;
            Min_Non_Shooting_Interval_Time.Enabled = false;

            //Set Attack Values - NPC_Shooting
            stop_shooting_if_no_visual_after_time.Enabled = false;

            //Set Attack Values
            min_delay_before_shooting.Enabled = false;
            max_delay_before_shooting.Enabled = false;

            //Set Peeking Values
            max_vertical_peek.Enabled = false;
            max_horizontal_peek.Enabled = false;
            max_peek_control_velocity.Enabled = false;
            max_auto_return_from_peek_velocity.Enabled = false;
            vertical_peek_exposed_threshold.Enabled = false;
            horizontal_peek_exposed_threshold.Enabled = false;
            grace_time_in_exposed_peek_region.Enabled = false;
            peek_control_scaling.Enabled = false;

            //Set Locomotion Values
            permittedLocomotionModulation.Enabled = false;
            capsuleRadius.Enabled = false;

            //Set Aggrovation_Settings Values - aggro_rate
            aggro_gun_aimed_rate.Enabled = false;
            aggro_warning_rate.Enabled = false;
            aggro_interrogative_rate.Enabled = false;
            aggro_standdown_rate.Enabled = false;

            //Set Aggrovation_Settings Values - aggro_distances
            aggro_aggressive_distance.Enabled = false;
            aggro_warning_distance.Enabled = false;
            aggro_interrogative_distance.Enabled = false;
            aggro_standdown_distance.Enabled = false;

            //Set Cover Values
            cover_job_minimum_distance_to_player.Enabled = false;

            //Set Hiding Values
            hiding_search_radius.Enabled = false;
            hiding_post_search_exclusion_radius.Enabled = false;
            hiding_hearing_range.Enabled = false;
            hiding_max_search_time.Enabled = false;
            hiding_max_QTE_selection_weighting.Enabled = false;
            chance_hiding_not_chosen.Enabled = false;

            //Set Job_Behaviour Values
            max_range_to_search_for_IDLE_job.Enabled = false;
            max_range_to_search_for_ESCALATION_jobs.Enabled = false;
            min_time_between_idles_for_stalk.Enabled = false;

            //Set Defence Values
            defense_gauge_decay_rate.Enabled = false;
            defense_gauge_decay_delay.Enabled = false;
            alien_stun_damage_guage_decrease_per_sec.Enabled = false;
            EMP_Stunned_Damage_Taken_Multiplier.Enabled = false;

            //Set base Attribute Values
            Template_Name.Enabled = false;
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
                //Set common file paths
                pathToWorkingBML = workingDirectory + selectedClass + ".BML";
                pathToGameBML = gameDirectory + @"\DATA\CHR_INFO\ATTRIBUTES\" + selectedClass + ".BML";
                pathToWorkingXML = workingDirectory + selectedClass + ".xml";

                //Copy correct BML to working directory
                File.Copy(pathToGameBML, pathToWorkingBML);

                //Convert BML to XML
                new AlienConverter(pathToWorkingBML, pathToWorkingXML).Run(); 

                //Delete BML
                File.Delete(pathToWorkingBML);


                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Set base Attribute Values
                loadAttributeValue("Attribute", "Template_Name", ChrAttributeXML, null, Template_Name);

                //Set Health Values
                loadAttributeValue("Health", "Max_Health", ChrAttributeXML, Max_Health, null);
                loadAttributeValue("Health", "Injured_State_1", ChrAttributeXML, Injured_State_1, null);
                loadAttributeValue("Health", "Injured_State_2", ChrAttributeXML, Injured_State_2, null);
                loadAttributeValue("Health", "Injured_State_3", ChrAttributeXML, Injured_State_3, null);
                loadAttributeValue("Health", "Health_Regeneration_Rate", ChrAttributeXML, Health_Regeneration_Rate, null);

                //Set Physical_Attributes Values
                loadAttributeValue("Physical_Attributes", "Mass", ChrAttributeXML, Mass, null);
                loadAttributeValue("Physical_Attributes", "Default_Alliance_Group", ChrAttributeXML, null, Default_Alliance_Group);

                //Set Behavior Values
                loadAttributeValue("Behavior", "Behavior_Tree", ChrAttributeXML, null, Behavior_Tree);
                loadAttributeValue("Behavior", "ATTACK_GROUP", ChrAttributeXML, null, ATTACK_GROUP);
                loadAttributeValue("Behavior", "TargetingSystem", ChrAttributeXML, null, TargetingSystem);

                //Set Sound_Attributes Values
                loadAttributeValue("Sound_Attributes", "Character_Sound", ChrAttributeXML, null, Character_Sound);

                //Set Attack Values - Melee_Damage
                loadAttributeValue("Attack/Melee_Damage", "Damage_From_Hit", ChrAttributeXML, Damage_From_Hit, null);
                loadAttributeValue("Attack/Melee_Damage", "Damage_From_Grapple_Break", ChrAttributeXML, Damage_From_Grapple_Break, null);
                loadAttributeValue("Attack/Melee_Damage", "Damage_From_Stealth_KO", ChrAttributeXML, Damage_From_Stealth_KO, null);

                //Set Attack Values - NPC_Shooting/Rate_of_Fire
                loadAttributeValue("Attack/NPC_Shooting/Rate_of_Fire", "min_time_between_shots", ChrAttributeXML, min_time_between_shots, null);
                loadAttributeValue("Attack/NPC_Shooting/Rate_of_Fire", "max_time_between_shots", ChrAttributeXML, max_time_between_shots, null);
                loadAttributeValue("Attack/NPC_Shooting/Rate_of_Fire", "min_time_between_shots_for_shotgun", ChrAttributeXML, min_time_between_shots_for_shotgun, null);
                loadAttributeValue("Attack/NPC_Shooting/Rate_of_Fire", "max_time_between_shots_for_shotgun", ChrAttributeXML, max_time_between_shots_for_shotgun, null);

                //Set Attack Values - NPC_Shooting/Cover
                loadAttributeValue("Attack/NPC_Shooting/Cover", "percentage_chance_of_shooting_over_cover_vs_side", ChrAttributeXML, percentage_chance_of_shooting_over_cover_vs_side, null);
                loadAttributeValue("Attack/NPC_Shooting/Cover", "Min_Non_Shooting_In_Cover_Interval_Time", ChrAttributeXML, Min_Non_Shooting_In_Cover_Interval_Time, null);
                loadAttributeValue("Attack/NPC_Shooting/Cover", "Max_Non_Shooting_In_Cover_Interval_Time", ChrAttributeXML, Max_Non_Shooting_In_Cover_Interval_Time, null);

                //Set Attack Values - NPC_Shooting/Aiming
                loadAttributeValue("Attack/NPC_Shooting/Aiming", "Max_Aiming_Accuracy_Normalised_Value", ChrAttributeXML, Max_Aiming_Accuracy_Normalised_Value, null);
                loadAttributeValue("Attack/NPC_Shooting/Aiming", "Aiming_Accuracy_Radius_Multiplier", ChrAttributeXML, Aiming_Accuracy_Radius_Multiplier, null);
                //loadAttributeValue("Attack/NPC_Shooting/Aiming", "Start_Shooting_Accuracy_Threshold", ChrAttributeXML, Start_Shooting_Accuracy_Threshold, null);
                //loadAttributeValue("Attack/NPC_Shooting/Aiming", "Stop_Shooting_Accuracy_Threshold", ChrAttributeXML, Stop_Shooting_Accuracy_Threshold, null);
                loadAttributeValue("Attack/NPC_Shooting/Aiming", "Min_Aiming_Accuracy_Normalised_Value", ChrAttributeXML, Min_Aiming_Accuracy_Normalised_Value, null);

                //Set Attack Values - NPC_Shooting/Interval
                loadAttributeValue("Attack/NPC_Shooting/Interval", "Min_Shooting_Interval_Time", ChrAttributeXML, Min_Shooting_Interval_Time, null);
                loadAttributeValue("Attack/NPC_Shooting/Interval", "Max_Shooting_Interval_Time", ChrAttributeXML, Max_Shooting_Interval_Time, null);
                loadAttributeValue("Attack/NPC_Shooting/Interval", "Max_Non_Shooting_Interval_Time", ChrAttributeXML, Max_Non_Shooting_Interval_Time, null);
                loadAttributeValue("Attack/NPC_Shooting/Interval", "Min_Non_Shooting_Interval_Time", ChrAttributeXML, Min_Non_Shooting_Interval_Time, null);

                //Set Attack Values - NPC_Shooting
                loadAttributeValue("Attack/NPC_Shooting", "stop_shooting_if_no_visual_after_time", ChrAttributeXML, stop_shooting_if_no_visual_after_time, null);

                //Set Attack Values
                loadAttributeValue("Attack", "min_delay_before_shooting", ChrAttributeXML, min_delay_before_shooting, null);
                loadAttributeValue("Attack", "max_delay_before_shooting", ChrAttributeXML, max_delay_before_shooting, null);

                //Set Peeking Values
                loadAttributeValue("Peeking", "max_vertical_peek", ChrAttributeXML, max_vertical_peek, null);
                loadAttributeValue("Peeking", "max_horizontal_peek", ChrAttributeXML, max_horizontal_peek, null);
                loadAttributeValue("Peeking", "max_peek_control_velocity", ChrAttributeXML, max_peek_control_velocity, null);
                loadAttributeValue("Peeking", "max_auto_return_from_peek_velocity", ChrAttributeXML, max_auto_return_from_peek_velocity, null);
                loadAttributeValue("Peeking", "vertical_peek_exposed_threshold", ChrAttributeXML, vertical_peek_exposed_threshold, null);
                loadAttributeValue("Peeking", "horizontal_peek_exposed_threshold", ChrAttributeXML, horizontal_peek_exposed_threshold, null);
                loadAttributeValue("Peeking", "grace_time_in_exposed_peek_region", ChrAttributeXML, grace_time_in_exposed_peek_region, null);
                loadAttributeValue("Peeking", "peek_control_scaling", ChrAttributeXML, peek_control_scaling, null);

                //Set Locomotion Values
                loadAttributeValue("Locomotion", "permittedLocomotionModulation", ChrAttributeXML, permittedLocomotionModulation, null);
                loadAttributeValue("Locomotion", "capsuleRadius", ChrAttributeXML, capsuleRadius, null);

                //Set Aggrovation_Settings Values - aggro_rate
                loadAttributeValue("Aggrovation_Settings/aggro_rate", "aggro_gun_aimed_rate", ChrAttributeXML, aggro_gun_aimed_rate, null);
                loadAttributeValue("Aggrovation_Settings/aggro_rate", "aggro_warning_rate", ChrAttributeXML, aggro_warning_rate, null);
                loadAttributeValue("Aggrovation_Settings/aggro_rate", "aggro_interrogative_rate", ChrAttributeXML, aggro_interrogative_rate, null);
                loadAttributeValue("Aggrovation_Settings/aggro_rate", "aggro_standdown_rate", ChrAttributeXML, aggro_standdown_rate, null);

                //Set Aggrovation_Settings Values - aggro_distances
                loadAttributeValue("Aggrovation_Settings/aggro_distances", "aggro_aggressive_distance", ChrAttributeXML, aggro_aggressive_distance, null);
                loadAttributeValue("Aggrovation_Settings/aggro_distances", "aggro_warning_distance", ChrAttributeXML, aggro_warning_distance, null);
                loadAttributeValue("Aggrovation_Settings/aggro_distances", "aggro_interrogative_distance", ChrAttributeXML, aggro_interrogative_distance, null);
                loadAttributeValue("Aggrovation_Settings/aggro_distances", "aggro_standdown_distance", ChrAttributeXML, aggro_standdown_distance, null);

                //Set Cover Values
                loadAttributeValue("Cover", "cover_job_minimum_distance_to_player", ChrAttributeXML, cover_job_minimum_distance_to_player, null);

                //Set Hiding Values
                loadAttributeValue("Hiding", "hiding_search_radius", ChrAttributeXML, hiding_search_radius, null);
                loadAttributeValue("Hiding", "hiding_post_search_exclusion_radius", ChrAttributeXML, hiding_post_search_exclusion_radius, null);
                loadAttributeValue("Hiding", "hiding_hearing_range", ChrAttributeXML, hiding_hearing_range, null);
                loadAttributeValue("Hiding", "hiding_max_search_time", ChrAttributeXML, hiding_max_search_time, null);
                loadAttributeValue("Hiding", "hiding_max_QTE_selection_weighting", ChrAttributeXML, hiding_max_QTE_selection_weighting, null);
                loadAttributeValue("Hiding", "chance_hiding_not_chosen", ChrAttributeXML, chance_hiding_not_chosen, null);

                //Set Job_Behaviour Values
                loadAttributeValue("Job_Behaviour", "max_range_to_search_for_IDLE_job", ChrAttributeXML, max_range_to_search_for_IDLE_job, null);
                loadAttributeValue("Job_Behaviour", "max_range_to_search_for_ESCALATION_jobs", ChrAttributeXML, max_range_to_search_for_ESCALATION_jobs, null);
                loadAttributeValue("Job_Behaviour", "min_time_between_idles_for_stalk", ChrAttributeXML, min_time_between_idles_for_stalk, null);

                //Set Defence Values
                loadAttributeValue("Defence", "defense_gauge_decay_rate", ChrAttributeXML, defense_gauge_decay_rate, null);
                loadAttributeValue("Defence", "defense_gauge_decay_delay", ChrAttributeXML, defense_gauge_decay_delay, null);
                loadAttributeValue("Defence", "alien_stun_damage_guage_decrease_per_sec", ChrAttributeXML, alien_stun_damage_guage_decrease_per_sec, null);
                loadAttributeValue("Defence", "EMP_Stunned_Damage_Taken_Multiplier", ChrAttributeXML, EMP_Stunned_Damage_Taken_Multiplier, null);
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }


        //Save Class Attributes
        private void btnSave_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            if (pathToWorkingXML != null)
            {
                //Load-in XML to edit
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Set base Attribute Values
                saveAttributeValue("Attribute", "Template_Name", ChrAttributeXML, Template_Name.Text);

                //Set Health Values
                saveAttributeValue("Health", "Max_Health", ChrAttributeXML, Max_Health.Text);
                saveAttributeValue("Health", "Injured_State_1", ChrAttributeXML, Injured_State_1.Text);
                saveAttributeValue("Health", "Injured_State_2", ChrAttributeXML, Injured_State_2.Text);
                saveAttributeValue("Health", "Injured_State_3", ChrAttributeXML, Injured_State_3.Text);
                saveAttributeValue("Health", "Health_Regeneration_Rate", ChrAttributeXML, Health_Regeneration_Rate.Text);

                //Set Physical_Attributes Values
                saveAttributeValue("Physical_Attributes", "Mass", ChrAttributeXML, Mass.Text);
                saveAttributeValue("Physical_Attributes", "Default_Alliance_Group", ChrAttributeXML, Default_Alliance_Group.Text);

                //Set Behavior Values
                saveAttributeValue("Behavior", "Behavior_Tree", ChrAttributeXML, Behavior_Tree.Text);
                saveAttributeValue("Behavior", "ATTACK_GROUP", ChrAttributeXML, ATTACK_GROUP.Text);
                saveAttributeValue("Behavior", "TargetingSystem", ChrAttributeXML, TargetingSystem.Text);

                //Set Sound_Attributes Values
                saveAttributeValue("Sound_Attributes", "Character_Sound", ChrAttributeXML, Character_Sound.Text);

                //Set Attack Values - Melee_Damage
                saveAttributeValue("Attack/Melee_Damage", "Damage_From_Hit", ChrAttributeXML, Damage_From_Hit.Text);
                saveAttributeValue("Attack/Melee_Damage", "Damage_From_Grapple_Break", ChrAttributeXML, Damage_From_Grapple_Break.Text);
                saveAttributeValue("Attack/Melee_Damage", "Damage_From_Stealth_KO", ChrAttributeXML, Damage_From_Stealth_KO.Text);

                //Set Attack Values - NPC_Shooting/Rate_of_Fire
                saveAttributeValue("Attack/NPC_Shooting/Rate_of_Fire", "min_time_between_shots", ChrAttributeXML, min_time_between_shots.Text);
                saveAttributeValue("Attack/NPC_Shooting/Rate_of_Fire", "max_time_between_shots", ChrAttributeXML, max_time_between_shots.Text);
                saveAttributeValue("Attack/NPC_Shooting/Rate_of_Fire", "min_time_between_shots_for_shotgun", ChrAttributeXML, min_time_between_shots_for_shotgun.Text);
                saveAttributeValue("Attack/NPC_Shooting/Rate_of_Fire", "max_time_between_shots_for_shotgun", ChrAttributeXML, max_time_between_shots_for_shotgun.Text);

                //Set Attack Values - NPC_Shooting/Cover
                saveAttributeValue("Attack/NPC_Shooting/Cover", "percentage_chance_of_shooting_over_cover_vs_side", ChrAttributeXML, percentage_chance_of_shooting_over_cover_vs_side.Text);
                saveAttributeValue("Attack/NPC_Shooting/Cover", "Min_Non_Shooting_In_Cover_Interval_Time", ChrAttributeXML, Min_Non_Shooting_In_Cover_Interval_Time.Text);
                saveAttributeValue("Attack/NPC_Shooting/Cover", "Max_Non_Shooting_In_Cover_Interval_Time", ChrAttributeXML, Max_Non_Shooting_In_Cover_Interval_Time.Text);

                //Set Attack Values - NPC_Shooting/Aiming
                saveAttributeValue("Attack/NPC_Shooting/Aiming", "Max_Aiming_Accuracy_Normalised_Value", ChrAttributeXML, Max_Aiming_Accuracy_Normalised_Value.Text);
                saveAttributeValue("Attack/NPC_Shooting/Aiming", "Aiming_Accuracy_Radius_Multiplier", ChrAttributeXML, Aiming_Accuracy_Radius_Multiplier.Text);
                //saveAttributeValue("Attack/NPC_Shooting/Aiming", "Start_Shooting_Accuracy_Threshold", ChrAttributeXML, Start_Shooting_Accuracy_Threshold.Text);
                //saveAttributeValue("Attack/NPC_Shooting/Aiming", "Stop_Shooting_Accuracy_Threshold", ChrAttributeXML, Stop_Shooting_Accuracy_Threshold.Text);
                saveAttributeValue("Attack/NPC_Shooting/Aiming", "Min_Aiming_Accuracy_Normalised_Value", ChrAttributeXML, Min_Aiming_Accuracy_Normalised_Value.Text);

                //Set Attack Values - NPC_Shooting/Interval
                saveAttributeValue("Attack/NPC_Shooting/Interval", "Min_Shooting_Interval_Time", ChrAttributeXML, Min_Shooting_Interval_Time.Text);
                saveAttributeValue("Attack/NPC_Shooting/Interval", "Max_Shooting_Interval_Time", ChrAttributeXML, Max_Shooting_Interval_Time.Text);
                saveAttributeValue("Attack/NPC_Shooting/Interval", "Max_Non_Shooting_Interval_Time", ChrAttributeXML, Max_Non_Shooting_Interval_Time.Text);
                saveAttributeValue("Attack/NPC_Shooting/Interval", "Min_Non_Shooting_Interval_Time", ChrAttributeXML, Min_Non_Shooting_Interval_Time.Text);

                //Set Attack Values - NPC_Shooting
                saveAttributeValue("Attack/NPC_Shooting", "stop_shooting_if_no_visual_after_time", ChrAttributeXML, stop_shooting_if_no_visual_after_time.Text);

                //Set Attack Values
                saveAttributeValue("Attack", "min_delay_before_shooting", ChrAttributeXML, min_delay_before_shooting.Text);
                saveAttributeValue("Attack", "max_delay_before_shooting", ChrAttributeXML, max_delay_before_shooting.Text);

                //Set Peeking Values
                saveAttributeValue("Peeking", "max_vertical_peek", ChrAttributeXML, max_vertical_peek.Text);
                saveAttributeValue("Peeking", "max_horizontal_peek", ChrAttributeXML, max_horizontal_peek.Text);
                saveAttributeValue("Peeking", "max_peek_control_velocity", ChrAttributeXML, max_peek_control_velocity.Text);
                saveAttributeValue("Peeking", "max_auto_return_from_peek_velocity", ChrAttributeXML, max_auto_return_from_peek_velocity.Text);
                saveAttributeValue("Peeking", "vertical_peek_exposed_threshold", ChrAttributeXML, vertical_peek_exposed_threshold.Text);
                saveAttributeValue("Peeking", "horizontal_peek_exposed_threshold", ChrAttributeXML, horizontal_peek_exposed_threshold.Text);
                saveAttributeValue("Peeking", "grace_time_in_exposed_peek_region", ChrAttributeXML, grace_time_in_exposed_peek_region.Text);
                saveAttributeValue("Peeking", "peek_control_scaling", ChrAttributeXML, peek_control_scaling.Text);

                //Set Locomotion Values
                saveAttributeValue("Locomotion", "permittedLocomotionModulation", ChrAttributeXML, permittedLocomotionModulation.Text);
                saveAttributeValue("Locomotion", "capsuleRadius", ChrAttributeXML, capsuleRadius.Text);

                //Set Aggrovation_Settings Values - aggro_rate
                saveAttributeValue("Aggrovation_Settings/aggro_rate", "aggro_gun_aimed_rate", ChrAttributeXML, aggro_gun_aimed_rate.Text);
                saveAttributeValue("Aggrovation_Settings/aggro_rate", "aggro_warning_rate", ChrAttributeXML, aggro_warning_rate.Text);
                saveAttributeValue("Aggrovation_Settings/aggro_rate", "aggro_interrogative_rate", ChrAttributeXML, aggro_interrogative_rate.Text);
                saveAttributeValue("Aggrovation_Settings/aggro_rate", "aggro_standdown_rate", ChrAttributeXML, aggro_standdown_rate.Text);

                //Set Aggrovation_Settings Values - aggro_distances
                saveAttributeValue("Aggrovation_Settings/aggro_distances", "aggro_aggressive_distance", ChrAttributeXML, aggro_aggressive_distance.Text);
                saveAttributeValue("Aggrovation_Settings/aggro_distances", "aggro_warning_distance", ChrAttributeXML, aggro_warning_distance.Text);
                saveAttributeValue("Aggrovation_Settings/aggro_distances", "aggro_interrogative_distance", ChrAttributeXML, aggro_interrogative_distance.Text);
                saveAttributeValue("Aggrovation_Settings/aggro_distances", "aggro_standdown_distance", ChrAttributeXML, aggro_standdown_distance.Text);

                //Set Cover Values
                saveAttributeValue("Cover", "cover_job_minimum_distance_to_player", ChrAttributeXML, cover_job_minimum_distance_to_player.Text);

                //Set Hiding Values
                saveAttributeValue("Hiding", "hiding_search_radius", ChrAttributeXML, hiding_search_radius.Text);
                saveAttributeValue("Hiding", "hiding_post_search_exclusion_radius", ChrAttributeXML, hiding_post_search_exclusion_radius.Text);
                saveAttributeValue("Hiding", "hiding_hearing_range", ChrAttributeXML, hiding_hearing_range.Text);
                saveAttributeValue("Hiding", "hiding_max_search_time", ChrAttributeXML, hiding_max_search_time.Text);
                saveAttributeValue("Hiding", "hiding_max_QTE_selection_weighting", ChrAttributeXML, hiding_max_QTE_selection_weighting.Text);
                saveAttributeValue("Hiding", "chance_hiding_not_chosen", ChrAttributeXML, chance_hiding_not_chosen.Text);

                //Set Job_Behaviour Values
                saveAttributeValue("Job_Behaviour", "max_range_to_search_for_IDLE_job", ChrAttributeXML, max_range_to_search_for_IDLE_job.Text);
                saveAttributeValue("Job_Behaviour", "max_range_to_search_for_ESCALATION_jobs", ChrAttributeXML, max_range_to_search_for_ESCALATION_jobs.Text);
                saveAttributeValue("Job_Behaviour", "min_time_between_idles_for_stalk", ChrAttributeXML, min_time_between_idles_for_stalk.Text);

                //Set Defence Values
                saveAttributeValue("Defence", "defense_gauge_decay_rate", ChrAttributeXML, defense_gauge_decay_rate.Text);
                saveAttributeValue("Defence", "defense_gauge_decay_delay", ChrAttributeXML, defense_gauge_decay_delay.Text);
                saveAttributeValue("Defence", "alien_stun_damage_guage_decrease_per_sec", ChrAttributeXML, alien_stun_damage_guage_decrease_per_sec.Text);
                saveAttributeValue("Defence", "EMP_Stunned_Damage_Taken_Multiplier", ChrAttributeXML, EMP_Stunned_Damage_Taken_Multiplier.Text);

                //Save all to XML
                ChrAttributeXML.Save(pathToWorkingXML);


                //Convert XML to BML
                new AlienConverter(pathToWorkingXML, pathToWorkingBML).Run();

                //Copy new BML to game directory & remove working files
                File.Delete(pathToGameBML);
                File.Copy(pathToWorkingBML, pathToGameBML);
                File.Delete(pathToWorkingBML);
                //File.Delete(pathToWorkingXML);

                //Done
                MessageBox.Show("Saved new attribute values.");
            }
            else
            {
                //No class loaded - can't save
                MessageBox.Show("Please load a class first!");
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }


        //Return XML value
        private void loadAttributeValue(string attributeGroup, string specificAttribute, XDocument ChrAttributeXML, TextBox textboxToSet, ComboBox comboboxToSet)
        {
            if (textboxToSet == null)
            {
                try
                {
                    string tempVal = ChrAttributeXML.XPathSelectElement("//" + attributeGroup + "/" + specificAttribute).Value;
                    if (tempVal == "")
                    {
                        comboboxToSet.SelectedIndex = -1;
                        comboboxToSet.Enabled = false;
                    }
                    else
                    {
                        comboboxToSet.Text = tempVal;
                        comboboxToSet.Enabled = true;
                    }
                }
                catch
                {
                    comboboxToSet.SelectedIndex = -1;
                    comboboxToSet.Enabled = false;
                }
            }
            else
            {
                try
                {
                    textboxToSet.Text = ChrAttributeXML.XPathSelectElement("//" + attributeGroup + "/" + specificAttribute).Value;
                    textboxToSet.Enabled = true;
                }
                catch
                {
                    textboxToSet.Text = "";
                    textboxToSet.Enabled = false;
                }
            }
        }


        //Set XML value
        private void saveAttributeValue(string attributeGroup, string specificAttribute, XDocument ChrAttributeXML, string newValue)
        {
            try
            {
                ChrAttributeXML.XPathSelectElement("//" + attributeGroup + "/" + specificAttribute).Value = newValue;
            }
            catch
            {
                //Can't save, hopefully because doesnt exist (should be).
            }
        }
    }
}
