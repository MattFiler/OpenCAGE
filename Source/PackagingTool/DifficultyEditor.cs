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
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Xml.XPath;

namespace PackagingTool
{
    public partial class DifficultyEditor : Form
    {
        //Main Directories
        string workingDirectory = Directory.GetCurrentDirectory() + @"\Attribute Editor Directory\"; //Our working dir
        string gameDirectory = File.ReadAllText(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz"); //Our game's dir

        //Common file paths
        string pathToWorkingBML;
        string pathToGameBML;
        string pathToWorkingXML;

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
                //Set common file paths
                pathToWorkingBML = workingDirectory + selectedConfig + ".BML";
                pathToGameBML = gameDirectory + @"\DATA\DIFFICULTYSETTINGS\" + selectedConfig + ".BML";
                pathToWorkingXML = workingDirectory + selectedConfig + ".xml";

                //Copy correct BML to working directory
                File.Copy(pathToGameBML, pathToWorkingBML);

                //Convert BML to XML
                new AlienConverter(pathToWorkingBML, pathToWorkingXML).Run();

                //Delete BML
                File.Delete(pathToWorkingBML);


                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Set base DifficultySetting Values
                loadAttributeValue("DifficultySetting", "Template_Name", ChrAttributeXML, null, Template_Name);

                //Set AlienConfig Values
                loadAttributeValue("Alien/AlienConfig", "decrease_sweep_duration_modifier", ChrAttributeXML, decrease_sweep_duration_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "increase_sweep_duration_modifier", ChrAttributeXML, increase_sweep_duration_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "near_target_exclusion_radius_first_stalk_min_modifier", ChrAttributeXML, near_target_exclusion_radius_first_stalk_min_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "near_target_exclusion_radius_first_stalk_max_modifier", ChrAttributeXML, near_target_exclusion_radius_first_stalk_max_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "near_target_exclusion_radius_subsequent_stalk_min_modifier", ChrAttributeXML, near_target_exclusion_radius_subsequent_stalk_min_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "near_target_exclusion_radius_subsequent_stalk_max_modifier", ChrAttributeXML, near_target_exclusion_radius_subsequent_stalk_max_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "near_objective_exclusion_radius_first_stalk_min_modifier", ChrAttributeXML, near_objective_exclusion_radius_first_stalk_min_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "near_objective_exclusion_radius_first_stalk_max_modifier", ChrAttributeXML, near_objective_exclusion_radius_first_stalk_max_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "near_objective_exclusion_radius_subsequent_stalk_min_modifier", ChrAttributeXML, near_objective_exclusion_radius_subsequent_stalk_min_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "near_objective_exclusion_radius_subsequent_stalk_max_modifier", ChrAttributeXML, near_objective_exclusion_radius_subsequent_stalk_max_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "menace_gauge_decrease_time_modifier", ChrAttributeXML, menace_gauge_decrease_time_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "menace_cool_down_time_modifier", ChrAttributeXML, menace_cool_down_time_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "meance_deemed_time_modifier", ChrAttributeXML, meance_deemed_time_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "max_menaces_modifier", ChrAttributeXML, max_menaces_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "menace_gauge_seconds_to_fill_modifier", ChrAttributeXML, menace_gauge_seconds_to_fill_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "backstage_area_sweep_role_timeout_modifier", ChrAttributeXML, backstage_area_sweep_role_timeout_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "backstage_area_sweep_min_distance_modifier", ChrAttributeXML, backstage_area_sweep_min_distance_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "backstage_area_sweep_max_distance_modifier", ChrAttributeXML, backstage_area_sweep_max_distance_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "backstage_area_sweep_min_idle_time_modifier", ChrAttributeXML, backstage_area_sweep_min_idle_time_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "backstage_area_sweep_max_idle_time_modifier", ChrAttributeXML, backstage_area_sweep_max_idle_time_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "backstage_area_sweep_killtrap_time_modifier", ChrAttributeXML, backstage_area_sweep_killtrap_time_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "backstage_area_sweep_ambush_timeout_mofisier", ChrAttributeXML, backstage_area_sweep_ambush_timeout_mofisier, null); //nice spelling CA :)
                loadAttributeValue("Alien/AlienConfig", "sweep_box_half_length_modifier", ChrAttributeXML, sweep_box_half_length_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "sweep_box_half_width_modifier", ChrAttributeXML, sweep_box_half_width_modifier, null);
                loadAttributeValue("Alien/AlienConfig", "Vent_Attract_Time_Min", ChrAttributeXML, Vent_Attract_Time_Min, null);
                loadAttributeValue("Alien/AlienConfig", "Vent_Attract_Time_Max", ChrAttributeXML, Vent_Attract_Time_Max, null);

                //Enable dropdowns/buttons
                characterTypes.Enabled = true;
                characterTypes.SelectedIndex = -1;
                loadNPC.Enabled = true;
                viewconeSet.Enabled = true;
                viewconeSet.SelectedIndex = -1;
                loadViewconeSet.Enabled = true;
                viewconeType.Enabled = false;
                viewconeType.SelectedIndex = -1;
                loadViewconeType.Enabled = false;

                //Reset NPC textboxes
                max_hearing_distance_modifier.Enabled = false;
                max_hearing_distance_modifier.Text = "";
                visual_sense_activation_modifier.Enabled = false;
                visual_sense_activation_modifier.Text = "";
                visual_combined_sense_activation_modifier.Enabled = false;
                visual_combined_sense_activation_modifier.Text = "";
                weapon_sound_sense_activation_modifier.Enabled = false;
                weapon_sound_sense_activation_modifier.Text = "";
                weapon_sound_combined_sense_activation_modifier.Enabled = false;
                weapon_sound_combined_sense_activation_modifier.Text = "";
                movement_sound_sense_activation_modifier.Enabled = false;
                movement_sound_sense_activation_modifier.Text = "";
                movement_sound_combined_sense_activation_modifier.Enabled = false;
                movement_sound_combined_sense_activation_modifier.Text = "";
                flash_light_sense_activation_modifier.Enabled = false;
                flash_light_sense_activation_modifier.Text = "";
                flash_light_combined_sense_activation_modifier.Enabled = false;
                flash_light_combined_sense_activation_modifier.Text = "";

                //Reset NPC textboxes pt2
                damage_dealt_scalar.Enabled = false;
                damage_dealt_scalar.Text = "";
                damage_received_scalar.Enabled = false;
                damage_received_scalar.Text = "";
                suspicious_item_loop_scalar.Enabled = false;
                suspicious_item_loop_scalar.Text = "";
                attack_pace_modifier.Enabled = false;
                attack_pace_modifier.Text = "";
                attack_pace_modifier_per_npc.Enabled = false;
                attack_pace_modifier_per_npc.Text = "";
                attack_pace_modifier_max.Enabled = false;
                attack_pace_modifier_max.Text = "";
                shooting_in_cover_duration_modifier.Enabled = false;
                shooting_in_cover_duration_modifier.Text = "";
                time_between_shots_scalar.Enabled = false;
                time_between_shots_scalar.Text = "";

                //Reset viewconeset textboxes
                visual_sense_exposure_effect_lower_modifier.Enabled = false;
                visual_sense_exposure_effect_lower_modifier.Text = "";
                visual_sense_exposure_effect_upper_modifier.Enabled = false;
                visual_sense_exposure_effect_upper_modifier.Text = "";
                visual_sense_stance_effect_lower_modifier.Enabled = false;
                visual_sense_stance_effect_lower_modifier.Text = "";
                visual_sense_stance_effect_upper_modifier.Enabled = false;
                visual_sense_stance_effect_upper_modifier.Text = "";
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
                loadAttributeValue("NPC_Generic/" + selectedConfig + "/Senses", "max_hearing_distance_modifier", ChrAttributeXML, max_hearing_distance_modifier, null);
                loadAttributeValue("NPC_Generic/" + selectedConfig + "/Senses", "visual_sense_activation_modifier", ChrAttributeXML, visual_sense_activation_modifier, null);
                loadAttributeValue("NPC_Generic/" + selectedConfig + "/Senses", "visual_combined_sense_activation_modifier", ChrAttributeXML, visual_combined_sense_activation_modifier, null);
                loadAttributeValue("NPC_Generic/" + selectedConfig + "/Senses", "weapon_sound_sense_activation_modifier", ChrAttributeXML, weapon_sound_sense_activation_modifier, null);
                loadAttributeValue("NPC_Generic/" + selectedConfig + "/Senses", "weapon_sound_combined_sense_activation_modifier", ChrAttributeXML, weapon_sound_combined_sense_activation_modifier, null);
                loadAttributeValue("NPC_Generic/" + selectedConfig + "/Senses", "movement_sound_sense_activation_modifier", ChrAttributeXML, movement_sound_sense_activation_modifier, null);
                loadAttributeValue("NPC_Generic/" + selectedConfig + "/Senses", "movement_sound_combined_sense_activation_modifier", ChrAttributeXML, movement_sound_combined_sense_activation_modifier, null);
                loadAttributeValue("NPC_Generic/" + selectedConfig + "/Senses", "flash_light_sense_activation_modifier", ChrAttributeXML, flash_light_sense_activation_modifier, null);
                loadAttributeValue("NPC_Generic/" + selectedConfig + "/Senses", "flash_light_combined_sense_activation_modifier", ChrAttributeXML, flash_light_combined_sense_activation_modifier, null);

                //Set NPC_Generic General Values
                loadAttributeValue("NPC_Generic/" + selectedConfig + "/General", "damage_dealt_scalar", ChrAttributeXML, damage_dealt_scalar, null);
                loadAttributeValue("NPC_Generic/" + selectedConfig + "/General", "damage_received_scalar", ChrAttributeXML, damage_received_scalar, null);
                loadAttributeValue("NPC_Generic/" + selectedConfig + "/General", "suspicious_item_loop_scalar", ChrAttributeXML, suspicious_item_loop_scalar, null);
                loadAttributeValue("NPC_Generic/" + selectedConfig + "/General", "attack_pace_modifier", ChrAttributeXML, attack_pace_modifier, null);
                loadAttributeValue("NPC_Generic/" + selectedConfig + "/General", "attack_pace_modifier_per_npc", ChrAttributeXML, attack_pace_modifier_per_npc, null);
                loadAttributeValue("NPC_Generic/" + selectedConfig + "/General", "attack_pace_modifier_max", ChrAttributeXML, attack_pace_modifier_max, null);
                loadAttributeValue("NPC_Generic/" + selectedConfig + "/General", "shooting_in_cover_duration_modifier", ChrAttributeXML, shooting_in_cover_duration_modifier, null);
                loadAttributeValue("NPC_Generic/" + selectedConfig + "/General", "time_between_shots_scalar", ChrAttributeXML, time_between_shots_scalar, null);
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Unlock viewcone type buttons
        private void loadViewconeSet_Click(object sender, EventArgs e)
        {
            viewconeType.Enabled = true;
            loadViewconeType.Enabled = true;

            visual_sense_exposure_effect_lower_modifier.Text = "";
            visual_sense_exposure_effect_upper_modifier.Text = "";
            visual_sense_stance_effect_lower_modifier.Text = "";
            visual_sense_stance_effect_upper_modifier.Text = "";

            visual_sense_exposure_effect_lower_modifier.Enabled = false;
            visual_sense_exposure_effect_upper_modifier.Enabled = false;
            visual_sense_stance_effect_lower_modifier.Enabled = false;
            visual_sense_stance_effect_upper_modifier.Enabled = false;
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
                loadAttributeValue(viewconeXmlPath, "visual_sense_exposure_effect_lower_modifier", ChrAttributeXML, visual_sense_exposure_effect_lower_modifier, null);
                loadAttributeValue(viewconeXmlPath, "visual_sense_exposure_effect_upper_modifier", ChrAttributeXML, visual_sense_exposure_effect_upper_modifier, null);
                loadAttributeValue(viewconeXmlPath, "visual_sense_stance_effect_lower_modifier", ChrAttributeXML, visual_sense_stance_effect_lower_modifier, null);
                loadAttributeValue(viewconeXmlPath, "visual_sense_stance_effect_upper_modifier", ChrAttributeXML, visual_sense_stance_effect_upper_modifier, null);
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
                saveAttributeValue("DifficultySetting", "Template_Name", ChrAttributeXML, null, Template_Name);

                //save AlienConfig Values
                saveAttributeValue("Alien/AlienConfig", "decrease_sweep_duration_modifier", ChrAttributeXML, decrease_sweep_duration_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "increase_sweep_duration_modifier", ChrAttributeXML, increase_sweep_duration_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "near_target_exclusion_radius_first_stalk_min_modifier", ChrAttributeXML, near_target_exclusion_radius_first_stalk_min_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "near_target_exclusion_radius_first_stalk_max_modifier", ChrAttributeXML, near_target_exclusion_radius_first_stalk_max_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "near_target_exclusion_radius_subsequent_stalk_min_modifier", ChrAttributeXML, near_target_exclusion_radius_subsequent_stalk_min_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "near_target_exclusion_radius_subsequent_stalk_max_modifier", ChrAttributeXML, near_target_exclusion_radius_subsequent_stalk_max_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "near_objective_exclusion_radius_first_stalk_min_modifier", ChrAttributeXML, near_objective_exclusion_radius_first_stalk_min_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "near_objective_exclusion_radius_first_stalk_max_modifier", ChrAttributeXML, near_objective_exclusion_radius_first_stalk_max_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "near_objective_exclusion_radius_subsequent_stalk_min_modifier", ChrAttributeXML, near_objective_exclusion_radius_subsequent_stalk_min_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "near_objective_exclusion_radius_subsequent_stalk_max_modifier", ChrAttributeXML, near_objective_exclusion_radius_subsequent_stalk_max_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "menace_gauge_decrease_time_modifier", ChrAttributeXML, menace_gauge_decrease_time_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "menace_cool_down_time_modifier", ChrAttributeXML, menace_cool_down_time_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "meance_deemed_time_modifier", ChrAttributeXML, meance_deemed_time_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "max_menaces_modifier", ChrAttributeXML, max_menaces_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "menace_gauge_seconds_to_fill_modifier", ChrAttributeXML, menace_gauge_seconds_to_fill_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "backstage_area_sweep_role_timeout_modifier", ChrAttributeXML, backstage_area_sweep_role_timeout_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "backstage_area_sweep_min_distance_modifier", ChrAttributeXML, backstage_area_sweep_min_distance_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "backstage_area_sweep_max_distance_modifier", ChrAttributeXML, backstage_area_sweep_max_distance_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "backstage_area_sweep_min_idle_time_modifier", ChrAttributeXML, backstage_area_sweep_min_idle_time_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "backstage_area_sweep_max_idle_time_modifier", ChrAttributeXML, backstage_area_sweep_max_idle_time_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "backstage_area_sweep_killtrap_time_modifier", ChrAttributeXML, backstage_area_sweep_killtrap_time_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "backstage_area_sweep_ambush_timeout_mofisier", ChrAttributeXML, backstage_area_sweep_ambush_timeout_mofisier, null); //nice spelling CA :)
                saveAttributeValue("Alien/AlienConfig", "sweep_box_half_length_modifier", ChrAttributeXML, sweep_box_half_length_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "sweep_box_half_width_modifier", ChrAttributeXML, sweep_box_half_width_modifier, null);
                saveAttributeValue("Alien/AlienConfig", "Vent_Attract_Time_Min", ChrAttributeXML, Vent_Attract_Time_Min, null);
                saveAttributeValue("Alien/AlienConfig", "Vent_Attract_Time_Max", ChrAttributeXML, Vent_Attract_Time_Max, null);

                if (selectedConfigNPC != "")
                {
                    //save NPC_Generic Senses Values
                    saveAttributeValue("NPC_Generic/" + selectedConfigNPC + "/Senses", "max_hearing_distance_modifier", ChrAttributeXML, max_hearing_distance_modifier, null);
                    saveAttributeValue("NPC_Generic/" + selectedConfigNPC + "/Senses", "visual_sense_activation_modifier", ChrAttributeXML, visual_sense_activation_modifier, null);
                    saveAttributeValue("NPC_Generic/" + selectedConfigNPC + "/Senses", "visual_combined_sense_activation_modifier", ChrAttributeXML, visual_combined_sense_activation_modifier, null);
                    saveAttributeValue("NPC_Generic/" + selectedConfigNPC + "/Senses", "weapon_sound_sense_activation_modifier", ChrAttributeXML, weapon_sound_sense_activation_modifier, null);
                    saveAttributeValue("NPC_Generic/" + selectedConfigNPC + "/Senses", "weapon_sound_combined_sense_activation_modifier", ChrAttributeXML, weapon_sound_combined_sense_activation_modifier, null);
                    saveAttributeValue("NPC_Generic/" + selectedConfigNPC + "/Senses", "movement_sound_sense_activation_modifier", ChrAttributeXML, movement_sound_sense_activation_modifier, null);
                    saveAttributeValue("NPC_Generic/" + selectedConfigNPC + "/Senses", "movement_sound_combined_sense_activation_modifier", ChrAttributeXML, movement_sound_combined_sense_activation_modifier, null);
                    saveAttributeValue("NPC_Generic/" + selectedConfigNPC + "/Senses", "flash_light_sense_activation_modifier", ChrAttributeXML, flash_light_sense_activation_modifier, null);
                    saveAttributeValue("NPC_Generic/" + selectedConfigNPC + "/Senses", "flash_light_combined_sense_activation_modifier", ChrAttributeXML, flash_light_combined_sense_activation_modifier, null);

                    //save NPC_Generic General Values
                    saveAttributeValue("NPC_Generic/" + selectedConfigNPC + "/General", "damage_dealt_scalar", ChrAttributeXML, damage_dealt_scalar, null);
                    saveAttributeValue("NPC_Generic/" + selectedConfigNPC + "/General", "damage_received_scalar", ChrAttributeXML, damage_received_scalar, null);
                    saveAttributeValue("NPC_Generic/" + selectedConfigNPC + "/General", "suspicious_item_loop_scalar", ChrAttributeXML, suspicious_item_loop_scalar, null);
                    saveAttributeValue("NPC_Generic/" + selectedConfigNPC + "/General", "attack_pace_modifier", ChrAttributeXML, attack_pace_modifier, null);
                    saveAttributeValue("NPC_Generic/" + selectedConfigNPC + "/General", "attack_pace_modifier_per_npc", ChrAttributeXML, attack_pace_modifier_per_npc, null);
                    saveAttributeValue("NPC_Generic/" + selectedConfigNPC + "/General", "attack_pace_modifier_max", ChrAttributeXML, attack_pace_modifier_max, null);
                    saveAttributeValue("NPC_Generic/" + selectedConfigNPC + "/General", "shooting_in_cover_duration_modifier", ChrAttributeXML, shooting_in_cover_duration_modifier, null);
                    saveAttributeValue("NPC_Generic/" + selectedConfigNPC + "/General", "time_between_shots_scalar", ChrAttributeXML, time_between_shots_scalar, null);
                }

                if (viewconeSetSaved != "" && viewconeTypeSaved != "")
                {
                    //save viewcone Values
                    string viewconeXmlPath = "ViewconeSets/" + viewconeSetSaved + "/" + viewconeTypeSaved;
                    saveAttributeValue(viewconeXmlPath, "visual_sense_exposure_effect_lower_modifier", ChrAttributeXML, visual_sense_exposure_effect_lower_modifier, null);
                    saveAttributeValue(viewconeXmlPath, "visual_sense_exposure_effect_upper_modifier", ChrAttributeXML, visual_sense_exposure_effect_upper_modifier, null);
                    saveAttributeValue(viewconeXmlPath, "visual_sense_stance_effect_lower_modifier", ChrAttributeXML, visual_sense_stance_effect_lower_modifier, null);
                    saveAttributeValue(viewconeXmlPath, "visual_sense_stance_effect_upper_modifier", ChrAttributeXML, visual_sense_stance_effect_upper_modifier, null);
                }

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
                MessageBox.Show("Saved configuration changes.");
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Return XML value
        private void loadAttributeValue(string attributeGroup, string specificAttribute, XDocument ChrAttributeXML, TextBox textboxToSet, ComboBox comboboxToSet)
        {
            if (textboxToSet == null)
            {
                //Try grab and set data from normal config
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
                //Try grab and set data from normal config
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
        private void saveAttributeValue(string attributeGroup, string specificAttribute, XDocument ChrAttributeXML, TextBox textboxToSet, ComboBox comboboxToSet)
        {
            try
            {
                ChrAttributeXML.XPathSelectElement("//" + attributeGroup + "/" + specificAttribute).Value = textboxToSet.Text;
            }
            catch
            {
                //Can't save, hopefully because doesnt exist (should be).
            }
        }


        private void near_objective_exclusion_radius_first_stalk_max_TextChanged(object sender, EventArgs e)
        {
            //unused
        }
    }
}
