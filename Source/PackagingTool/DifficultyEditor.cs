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
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Xml.XPath;

namespace PackagingTool
{
    public partial class DifficultyEditor : Form
    {
        //Load shared scripts
        AYZ_AttributeEditors AlienAttribute = new AYZ_AttributeEditors();
        
        //Common file paths
        string pathToWorkingXML;
        string gameBmlDirectory = @"\DATA\DIFFICULTYSETTINGS\";

        public DifficultyEditor()
        {
            InitializeComponent();

            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        //Load difficulty
        private void btnSelectClass_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Save selected config name
            string selectedConfig = classSelection.Text;

            if (selectedConfig == "")
            {
                //No config selected, can't load anything
                MessageBox.Show("Please select a difficulty.");
            }
            else
            {
                //Load in XML
                pathToWorkingXML = AlienAttribute.loadXML(selectedConfig, gameBmlDirectory);

                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Set base DifficultySetting Values
                AlienAttribute.getNode("DifficultySetting", "Template_Name", ChrAttributeXML, null, Template_Name);

                //Set AlienConfig Values
                AlienAttribute.getNode("Alien/AlienConfig", "decrease_sweep_duration_modifier", ChrAttributeXML, decrease_sweep_duration_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "increase_sweep_duration_modifier", ChrAttributeXML, increase_sweep_duration_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "near_target_exclusion_radius_first_stalk_min_modifier", ChrAttributeXML, near_target_exclusion_radius_first_stalk_min_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "near_target_exclusion_radius_first_stalk_max_modifier", ChrAttributeXML, near_target_exclusion_radius_first_stalk_max_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "near_target_exclusion_radius_subsequent_stalk_min_modifier", ChrAttributeXML, near_target_exclusion_radius_subsequent_stalk_min_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "near_target_exclusion_radius_subsequent_stalk_max_modifier", ChrAttributeXML, near_target_exclusion_radius_subsequent_stalk_max_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "near_objective_exclusion_radius_first_stalk_min_modifier", ChrAttributeXML, near_objective_exclusion_radius_first_stalk_min_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "near_objective_exclusion_radius_first_stalk_max_modifier", ChrAttributeXML, near_objective_exclusion_radius_first_stalk_max_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "near_objective_exclusion_radius_subsequent_stalk_min_modifier", ChrAttributeXML, near_objective_exclusion_radius_subsequent_stalk_min_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "near_objective_exclusion_radius_subsequent_stalk_max_modifier", ChrAttributeXML, near_objective_exclusion_radius_subsequent_stalk_max_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "menace_gauge_decrease_time_modifier", ChrAttributeXML, menace_gauge_decrease_time_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "menace_cool_down_time_modifier", ChrAttributeXML, menace_cool_down_time_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "meance_deemed_time_modifier", ChrAttributeXML, meance_deemed_time_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "max_menaces_modifier", ChrAttributeXML, max_menaces_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "menace_gauge_seconds_to_fill_modifier", ChrAttributeXML, menace_gauge_seconds_to_fill_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "backstage_area_sweep_role_timeout_modifier", ChrAttributeXML, backstage_area_sweep_role_timeout_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "backstage_area_sweep_min_distance_modifier", ChrAttributeXML, backstage_area_sweep_min_distance_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "backstage_area_sweep_max_distance_modifier", ChrAttributeXML, backstage_area_sweep_max_distance_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "backstage_area_sweep_min_idle_time_modifier", ChrAttributeXML, backstage_area_sweep_min_idle_time_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "backstage_area_sweep_max_idle_time_modifier", ChrAttributeXML, backstage_area_sweep_max_idle_time_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "backstage_area_sweep_killtrap_time_modifier", ChrAttributeXML, backstage_area_sweep_killtrap_time_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "backstage_area_sweep_ambush_timeout_mofisier", ChrAttributeXML, backstage_area_sweep_ambush_timeout_mofisier, null); //nice spelling CA :)
                AlienAttribute.getNode("Alien/AlienConfig", "sweep_box_half_length_modifier", ChrAttributeXML, sweep_box_half_length_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "sweep_box_half_width_modifier", ChrAttributeXML, sweep_box_half_width_modifier, null);
                AlienAttribute.getNode("Alien/AlienConfig", "Vent_Attract_Time_Min", ChrAttributeXML, Vent_Attract_Time_Min, null);
                AlienAttribute.getNode("Alien/AlienConfig", "Vent_Attract_Time_Max", ChrAttributeXML, Vent_Attract_Time_Max, null);

                //Enable dropdowns/buttons
                AlienAttribute.enableInput(null, characterTypes);
                loadNPC.Enabled = true;
                AlienAttribute.enableInput(null, viewconeSet);
                loadViewconeSet.Enabled = true;
                AlienAttribute.disableInput(null, viewconeType);
                loadViewconeType.Enabled = false;

                //Reset NPC textboxes
                AlienAttribute.disableInput(max_hearing_distance_modifier, null);
                AlienAttribute.disableInput(visual_sense_activation_modifier, null);
                AlienAttribute.disableInput(visual_combined_sense_activation_modifier, null);
                AlienAttribute.disableInput(weapon_sound_sense_activation_modifier, null);
                AlienAttribute.disableInput(weapon_sound_combined_sense_activation_modifier, null);
                AlienAttribute.disableInput(movement_sound_sense_activation_modifier, null);
                AlienAttribute.disableInput(movement_sound_combined_sense_activation_modifier, null);
                AlienAttribute.disableInput(flash_light_sense_activation_modifier, null);
                AlienAttribute.disableInput(flash_light_combined_sense_activation_modifier, null);

                //Reset NPC textboxes pt2
                AlienAttribute.disableInput(damage_dealt_scalar, null);
                AlienAttribute.disableInput(damage_received_scalar, null);
                AlienAttribute.disableInput(suspicious_item_loop_scalar, null);
                AlienAttribute.disableInput(attack_pace_modifier, null);
                AlienAttribute.disableInput(attack_pace_modifier_per_npc, null);
                AlienAttribute.disableInput(attack_pace_modifier_max, null);
                AlienAttribute.disableInput(shooting_in_cover_duration_modifier, null);
                AlienAttribute.disableInput(time_between_shots_scalar, null);

                //Reset viewconeset textboxes
                AlienAttribute.disableInput(visual_sense_exposure_effect_lower_modifier, null);
                AlienAttribute.disableInput(visual_sense_exposure_effect_upper_modifier, null);
                AlienAttribute.disableInput(visual_sense_stance_effect_lower_modifier, null);
                AlienAttribute.disableInput(visual_sense_stance_effect_upper_modifier, null);
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Load character type
        private void loadNPC_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Save selected config name
            string selectedConfig = characterTypes.Text;

            if (selectedConfig == "")
            {
                //No config selected, can't load anything
                MessageBox.Show("Please select a character.");
            }
            else
            {
                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Set NPC_Generic Senses Values
                AlienAttribute.getNode("NPC_Generic/" + selectedConfig + "/Senses", "max_hearing_distance_modifier", ChrAttributeXML, max_hearing_distance_modifier, null);
                AlienAttribute.getNode("NPC_Generic/" + selectedConfig + "/Senses", "visual_sense_activation_modifier", ChrAttributeXML, visual_sense_activation_modifier, null);
                AlienAttribute.getNode("NPC_Generic/" + selectedConfig + "/Senses", "visual_combined_sense_activation_modifier", ChrAttributeXML, visual_combined_sense_activation_modifier, null);
                AlienAttribute.getNode("NPC_Generic/" + selectedConfig + "/Senses", "weapon_sound_sense_activation_modifier", ChrAttributeXML, weapon_sound_sense_activation_modifier, null);
                AlienAttribute.getNode("NPC_Generic/" + selectedConfig + "/Senses", "weapon_sound_combined_sense_activation_modifier", ChrAttributeXML, weapon_sound_combined_sense_activation_modifier, null);
                AlienAttribute.getNode("NPC_Generic/" + selectedConfig + "/Senses", "movement_sound_sense_activation_modifier", ChrAttributeXML, movement_sound_sense_activation_modifier, null);
                AlienAttribute.getNode("NPC_Generic/" + selectedConfig + "/Senses", "movement_sound_combined_sense_activation_modifier", ChrAttributeXML, movement_sound_combined_sense_activation_modifier, null);
                AlienAttribute.getNode("NPC_Generic/" + selectedConfig + "/Senses", "flash_light_sense_activation_modifier", ChrAttributeXML, flash_light_sense_activation_modifier, null);
                AlienAttribute.getNode("NPC_Generic/" + selectedConfig + "/Senses", "flash_light_combined_sense_activation_modifier", ChrAttributeXML, flash_light_combined_sense_activation_modifier, null);

                //Set NPC_Generic General Values
                AlienAttribute.getNode("NPC_Generic/" + selectedConfig + "/General", "damage_dealt_scalar", ChrAttributeXML, damage_dealt_scalar, null);
                AlienAttribute.getNode("NPC_Generic/" + selectedConfig + "/General", "damage_received_scalar", ChrAttributeXML, damage_received_scalar, null);
                AlienAttribute.getNode("NPC_Generic/" + selectedConfig + "/General", "suspicious_item_loop_scalar", ChrAttributeXML, suspicious_item_loop_scalar, null);
                AlienAttribute.getNode("NPC_Generic/" + selectedConfig + "/General", "attack_pace_modifier", ChrAttributeXML, attack_pace_modifier, null);
                AlienAttribute.getNode("NPC_Generic/" + selectedConfig + "/General", "attack_pace_modifier_per_npc", ChrAttributeXML, attack_pace_modifier_per_npc, null);
                AlienAttribute.getNode("NPC_Generic/" + selectedConfig + "/General", "attack_pace_modifier_max", ChrAttributeXML, attack_pace_modifier_max, null);
                AlienAttribute.getNode("NPC_Generic/" + selectedConfig + "/General", "shooting_in_cover_duration_modifier", ChrAttributeXML, shooting_in_cover_duration_modifier, null);
                AlienAttribute.getNode("NPC_Generic/" + selectedConfig + "/General", "time_between_shots_scalar", ChrAttributeXML, time_between_shots_scalar, null);
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Unlock viewcone type buttons
        private void loadViewconeSet_Click(object sender, EventArgs e)
        {
            viewconeType.Enabled = true;
            loadViewconeType.Enabled = true;

            AlienAttribute.disableInput(visual_sense_exposure_effect_lower_modifier, null);
            AlienAttribute.disableInput(visual_sense_exposure_effect_upper_modifier, null);
            AlienAttribute.disableInput(visual_sense_stance_effect_lower_modifier, null);
            AlienAttribute.disableInput(visual_sense_stance_effect_upper_modifier, null);
        }

        //Load viewcone set/type
        private void button3_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Save selected config name
            string viewconeSetSaved = viewconeSet.Text;
            string viewconeTypeSaved = viewconeType.Text;

            if (viewconeTypeSaved == "")
            {
                //No config selected, can't load anything
                MessageBox.Show("Please select a viewcone type.");
            }
            else
            {
                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);
                
                //Set viewcone Values
                string viewconeXmlPath = "ViewconeSets/" + viewconeSetSaved + "/" + viewconeTypeSaved;
                AlienAttribute.getNode(viewconeXmlPath, "visual_sense_exposure_effect_lower_modifier", ChrAttributeXML, visual_sense_exposure_effect_lower_modifier, null);
                AlienAttribute.getNode(viewconeXmlPath, "visual_sense_exposure_effect_upper_modifier", ChrAttributeXML, visual_sense_exposure_effect_upper_modifier, null);
                AlienAttribute.getNode(viewconeXmlPath, "visual_sense_stance_effect_lower_modifier", ChrAttributeXML, visual_sense_stance_effect_lower_modifier, null);
                AlienAttribute.getNode(viewconeXmlPath, "visual_sense_stance_effect_upper_modifier", ChrAttributeXML, visual_sense_stance_effect_upper_modifier, null);
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Save
        private void btnSave_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Save selected config name
            string selectedConfig = classSelection.Text;

            //Save selected config name NPC
            string selectedConfigNPC = characterTypes.Text;

            //Save selected config name
            string viewconeSetSaved = viewconeSet.Text;
            string viewconeTypeSaved = viewconeType.Text;

            if (selectedConfig == "")
            {
                //No config selected, can't load anything
                MessageBox.Show("Please select a difficulty.");
            }
            else
            {
                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //save base DifficultySetting Values
                AlienAttribute.setNode("DifficultySetting", "Template_Name", ChrAttributeXML, null, Template_Name);

                //save AlienConfig Values
                AlienAttribute.setNode("Alien/AlienConfig", "decrease_sweep_duration_modifier", ChrAttributeXML, decrease_sweep_duration_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "increase_sweep_duration_modifier", ChrAttributeXML, increase_sweep_duration_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "near_target_exclusion_radius_first_stalk_min_modifier", ChrAttributeXML, near_target_exclusion_radius_first_stalk_min_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "near_target_exclusion_radius_first_stalk_max_modifier", ChrAttributeXML, near_target_exclusion_radius_first_stalk_max_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "near_target_exclusion_radius_subsequent_stalk_min_modifier", ChrAttributeXML, near_target_exclusion_radius_subsequent_stalk_min_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "near_target_exclusion_radius_subsequent_stalk_max_modifier", ChrAttributeXML, near_target_exclusion_radius_subsequent_stalk_max_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "near_objective_exclusion_radius_first_stalk_min_modifier", ChrAttributeXML, near_objective_exclusion_radius_first_stalk_min_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "near_objective_exclusion_radius_first_stalk_max_modifier", ChrAttributeXML, near_objective_exclusion_radius_first_stalk_max_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "near_objective_exclusion_radius_subsequent_stalk_min_modifier", ChrAttributeXML, near_objective_exclusion_radius_subsequent_stalk_min_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "near_objective_exclusion_radius_subsequent_stalk_max_modifier", ChrAttributeXML, near_objective_exclusion_radius_subsequent_stalk_max_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "menace_gauge_decrease_time_modifier", ChrAttributeXML, menace_gauge_decrease_time_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "menace_cool_down_time_modifier", ChrAttributeXML, menace_cool_down_time_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "meance_deemed_time_modifier", ChrAttributeXML, meance_deemed_time_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "max_menaces_modifier", ChrAttributeXML, max_menaces_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "menace_gauge_seconds_to_fill_modifier", ChrAttributeXML, menace_gauge_seconds_to_fill_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "backstage_area_sweep_role_timeout_modifier", ChrAttributeXML, backstage_area_sweep_role_timeout_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "backstage_area_sweep_min_distance_modifier", ChrAttributeXML, backstage_area_sweep_min_distance_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "backstage_area_sweep_max_distance_modifier", ChrAttributeXML, backstage_area_sweep_max_distance_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "backstage_area_sweep_min_idle_time_modifier", ChrAttributeXML, backstage_area_sweep_min_idle_time_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "backstage_area_sweep_max_idle_time_modifier", ChrAttributeXML, backstage_area_sweep_max_idle_time_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "backstage_area_sweep_killtrap_time_modifier", ChrAttributeXML, backstage_area_sweep_killtrap_time_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "backstage_area_sweep_ambush_timeout_mofisier", ChrAttributeXML, backstage_area_sweep_ambush_timeout_mofisier, null); //nice spelling CA :)
                AlienAttribute.setNode("Alien/AlienConfig", "sweep_box_half_length_modifier", ChrAttributeXML, sweep_box_half_length_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "sweep_box_half_width_modifier", ChrAttributeXML, sweep_box_half_width_modifier, null);
                AlienAttribute.setNode("Alien/AlienConfig", "Vent_Attract_Time_Min", ChrAttributeXML, Vent_Attract_Time_Min, null);
                AlienAttribute.setNode("Alien/AlienConfig", "Vent_Attract_Time_Max", ChrAttributeXML, Vent_Attract_Time_Max, null);

                if (selectedConfigNPC != "")
                {
                    //save NPC_Generic Senses Values
                    AlienAttribute.setNode("NPC_Generic/" + selectedConfigNPC + "/Senses", "max_hearing_distance_modifier", ChrAttributeXML, max_hearing_distance_modifier, null);
                    AlienAttribute.setNode("NPC_Generic/" + selectedConfigNPC + "/Senses", "visual_sense_activation_modifier", ChrAttributeXML, visual_sense_activation_modifier, null);
                    AlienAttribute.setNode("NPC_Generic/" + selectedConfigNPC + "/Senses", "visual_combined_sense_activation_modifier", ChrAttributeXML, visual_combined_sense_activation_modifier, null);
                    AlienAttribute.setNode("NPC_Generic/" + selectedConfigNPC + "/Senses", "weapon_sound_sense_activation_modifier", ChrAttributeXML, weapon_sound_sense_activation_modifier, null);
                    AlienAttribute.setNode("NPC_Generic/" + selectedConfigNPC + "/Senses", "weapon_sound_combined_sense_activation_modifier", ChrAttributeXML, weapon_sound_combined_sense_activation_modifier, null);
                    AlienAttribute.setNode("NPC_Generic/" + selectedConfigNPC + "/Senses", "movement_sound_sense_activation_modifier", ChrAttributeXML, movement_sound_sense_activation_modifier, null);
                    AlienAttribute.setNode("NPC_Generic/" + selectedConfigNPC + "/Senses", "movement_sound_combined_sense_activation_modifier", ChrAttributeXML, movement_sound_combined_sense_activation_modifier, null);
                    AlienAttribute.setNode("NPC_Generic/" + selectedConfigNPC + "/Senses", "flash_light_sense_activation_modifier", ChrAttributeXML, flash_light_sense_activation_modifier, null);
                    AlienAttribute.setNode("NPC_Generic/" + selectedConfigNPC + "/Senses", "flash_light_combined_sense_activation_modifier", ChrAttributeXML, flash_light_combined_sense_activation_modifier, null);

                    //save NPC_Generic General Values
                    AlienAttribute.setNode("NPC_Generic/" + selectedConfigNPC + "/General", "damage_dealt_scalar", ChrAttributeXML, damage_dealt_scalar, null);
                    AlienAttribute.setNode("NPC_Generic/" + selectedConfigNPC + "/General", "damage_received_scalar", ChrAttributeXML, damage_received_scalar, null);
                    AlienAttribute.setNode("NPC_Generic/" + selectedConfigNPC + "/General", "suspicious_item_loop_scalar", ChrAttributeXML, suspicious_item_loop_scalar, null);
                    AlienAttribute.setNode("NPC_Generic/" + selectedConfigNPC + "/General", "attack_pace_modifier", ChrAttributeXML, attack_pace_modifier, null);
                    AlienAttribute.setNode("NPC_Generic/" + selectedConfigNPC + "/General", "attack_pace_modifier_per_npc", ChrAttributeXML, attack_pace_modifier_per_npc, null);
                    AlienAttribute.setNode("NPC_Generic/" + selectedConfigNPC + "/General", "attack_pace_modifier_max", ChrAttributeXML, attack_pace_modifier_max, null);
                    AlienAttribute.setNode("NPC_Generic/" + selectedConfigNPC + "/General", "shooting_in_cover_duration_modifier", ChrAttributeXML, shooting_in_cover_duration_modifier, null);
                    AlienAttribute.setNode("NPC_Generic/" + selectedConfigNPC + "/General", "time_between_shots_scalar", ChrAttributeXML, time_between_shots_scalar, null);
                }

                if (viewconeSetSaved != "" && viewconeTypeSaved != "")
                {
                    //save viewcone Values
                    string viewconeXmlPath = "ViewconeSets/" + viewconeSetSaved + "/" + viewconeTypeSaved;
                    AlienAttribute.setNode(viewconeXmlPath, "visual_sense_exposure_effect_lower_modifier", ChrAttributeXML, visual_sense_exposure_effect_lower_modifier, null);
                    AlienAttribute.setNode(viewconeXmlPath, "visual_sense_exposure_effect_upper_modifier", ChrAttributeXML, visual_sense_exposure_effect_upper_modifier, null);
                    AlienAttribute.setNode(viewconeXmlPath, "visual_sense_stance_effect_lower_modifier", ChrAttributeXML, visual_sense_stance_effect_lower_modifier, null);
                    AlienAttribute.setNode(viewconeXmlPath, "visual_sense_stance_effect_upper_modifier", ChrAttributeXML, visual_sense_stance_effect_upper_modifier, null);
                }

                //Save values
                if (AlienAttribute.saveXML(selectedConfig, gameBmlDirectory, ChrAttributeXML))
                {
                    MessageBox.Show("Saved configuration changes.");
                }
                else
                {
                    MessageBox.Show("An error occured while saving.");
                }
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }


        private void near_objective_exclusion_radius_first_stalk_max_TextChanged(object sender, EventArgs e)
        {
            //unused
        }
    }
}
