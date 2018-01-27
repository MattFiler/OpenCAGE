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
    public partial class AlienConfigEditor : Form
    {
        //Load shared scripts
        AYZ_AttributeEditors AlienAttribute = new AYZ_AttributeEditors();

        //Common file paths
        string pathToWorkingXML;
        string gameBmlDirectory = @"\DATA\ALIENCONFIGS\";

        public AlienConfigEditor()
        {
            InitializeComponent();

            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        //Load Config
        private void btnSelectClass_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Save selected config name
            string selectedConfig = classSelection.Text;

            if (selectedConfig == "")
            {
                //No config selected, can't load anything
                MessageBox.Show("Please select a configuration.");
            }
            else
            {
                //Load in XML
                pathToWorkingXML = AlienAttribute.loadXML(selectedConfig, gameBmlDirectory);

                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Set base AlienConfig Values
                AlienAttribute.getNode("AlienConfig", "Template_Name", ChrAttributeXML, null, Template_Name);

                //Set AreaSweep Values
                AlienAttribute.getNode("AreaSweep", "decrease_sweep_duration", ChrAttributeXML, decrease_sweep_duration, null);
                AlienAttribute.getNode("AreaSweep", "increase_sweep_duration", ChrAttributeXML, increase_sweep_duration, null);
                AlienAttribute.getNode("AreaSweep", "near_target_exclusion_radius_first_stalk_min", ChrAttributeXML, near_target_exclusion_radius_first_stalk_min, null);
                AlienAttribute.getNode("AreaSweep", "near_target_exclusion_radius_first_stalk_max", ChrAttributeXML, near_target_exclusion_radius_first_stalk_max, null);
                AlienAttribute.getNode("AreaSweep", "near_target_exclusion_radius_subsequent_stalk_min", ChrAttributeXML, near_target_exclusion_radius_subsequent_stalk_min, null);
                AlienAttribute.getNode("AreaSweep", "near_target_exclusion_radius_subsequent_stalk_max", ChrAttributeXML, near_target_exclusion_radius_subsequent_stalk_max, null);
                AlienAttribute.getNode("AreaSweep", "near_objective_exclusion_radius_first_stalk_min", ChrAttributeXML, near_objective_exclusion_radius_first_stalk_min, null);
                AlienAttribute.getNode("AreaSweep", "near_objective_exclusion_radius_first_stalk_max", ChrAttributeXML, near_objective_exclusion_radius_first_stalk_max, null);
                AlienAttribute.getNode("AreaSweep", "near_objective_exclusion_radius_subsequent_stalk_min", ChrAttributeXML, near_objective_exclusion_radius_subsequent_stalk_min, null);
                AlienAttribute.getNode("AreaSweep", "near_objective_exclusion_radius_subsequent_stalk_max", ChrAttributeXML, near_objective_exclusion_radius_subsequent_stalk_max, null);
                AlienAttribute.getNode("AreaSweep", "menace_gauge_decrease_time", ChrAttributeXML, menace_gauge_decrease_time, null);
                AlienAttribute.getNode("AreaSweep", "menace_cool_down_time", ChrAttributeXML, menace_cool_down_time, null);
                AlienAttribute.getNode("AreaSweep", "meance_deemed_time", ChrAttributeXML, meance_deemed_time, null);
                AlienAttribute.getNode("AreaSweep", "max_menaces", ChrAttributeXML, max_menaces, null);
                AlienAttribute.getNode("AreaSweep", "menace_gauge_seconds_to_fill", ChrAttributeXML, menace_gauge_seconds_to_fill, null);
                AlienAttribute.getNode("AreaSweep", "sweep_box_half_width", ChrAttributeXML, sweep_box_half_width, null);
                AlienAttribute.getNode("AreaSweep", "sweep_box_min_half_length", ChrAttributeXML, sweep_box_min_half_length, null);
                AlienAttribute.getNode("AreaSweep", "Vent_Attract_Time_Min", ChrAttributeXML, Vent_Attract_Time_Min, null);
                AlienAttribute.getNode("AreaSweep", "Vent_Attract_Time_Max", ChrAttributeXML, Vent_Attract_Time_Max, null);

                //Set BackstageAreaSweep Values
                AlienAttribute.getNode("BackstageAreaSweep", "role_timeout_min", ChrAttributeXML, role_timeout_min, null);
                AlienAttribute.getNode("BackstageAreaSweep", "role_timeout_max", ChrAttributeXML, role_timeout_max, null);
                AlienAttribute.getNode("BackstageAreaSweep", "min_distance", ChrAttributeXML, min_distance, null);
                AlienAttribute.getNode("BackstageAreaSweep", "max_distance", ChrAttributeXML, max_distance, null);
                AlienAttribute.getNode("BackstageAreaSweep", "min_idle_time", ChrAttributeXML, min_idle_time, null);
                AlienAttribute.getNode("BackstageAreaSweep", "max_idle_time", ChrAttributeXML, max_idle_time, null);
                AlienAttribute.getNode("BackstageAreaSweep", "killtrap_time", ChrAttributeXML, killtrap_time, null);
                AlienAttribute.getNode("BackstageAreaSweep", "ambush_timeout", ChrAttributeXML, ambush_timeout, null);
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Save Config
        private void btnSave_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Save selected config name
            string selectedConfig = classSelection.Text;

            if (pathToWorkingXML != null)
            {
                //Load-in XML to edit
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Set base AlienConfig Values
                AlienAttribute.setNode("AlienConfig", "Template_Name", ChrAttributeXML, null, Template_Name);

                //Set AreaSweep Values
                AlienAttribute.setNode("AreaSweep", "decrease_sweep_duration", ChrAttributeXML, decrease_sweep_duration, null);
                AlienAttribute.setNode("AreaSweep", "increase_sweep_duration", ChrAttributeXML, increase_sweep_duration, null);
                AlienAttribute.setNode("AreaSweep", "near_target_exclusion_radius_first_stalk_min", ChrAttributeXML, near_target_exclusion_radius_first_stalk_min, null);
                AlienAttribute.setNode("AreaSweep", "near_target_exclusion_radius_first_stalk_max", ChrAttributeXML, near_target_exclusion_radius_first_stalk_max, null);
                AlienAttribute.setNode("AreaSweep", "near_target_exclusion_radius_subsequent_stalk_min", ChrAttributeXML, near_target_exclusion_radius_subsequent_stalk_min, null);
                AlienAttribute.setNode("AreaSweep", "near_target_exclusion_radius_subsequent_stalk_max", ChrAttributeXML, near_target_exclusion_radius_subsequent_stalk_max, null);
                AlienAttribute.setNode("AreaSweep", "near_objective_exclusion_radius_first_stalk_min", ChrAttributeXML, near_objective_exclusion_radius_first_stalk_min, null);
                AlienAttribute.setNode("AreaSweep", "near_objective_exclusion_radius_first_stalk_max", ChrAttributeXML, near_objective_exclusion_radius_first_stalk_max, null);
                AlienAttribute.setNode("AreaSweep", "near_objective_exclusion_radius_subsequent_stalk_min", ChrAttributeXML, near_objective_exclusion_radius_subsequent_stalk_min, null);
                AlienAttribute.setNode("AreaSweep", "near_objective_exclusion_radius_subsequent_stalk_max", ChrAttributeXML, near_objective_exclusion_radius_subsequent_stalk_max, null);
                AlienAttribute.setNode("AreaSweep", "menace_gauge_decrease_time", ChrAttributeXML, menace_gauge_decrease_time, null);
                AlienAttribute.setNode("AreaSweep", "menace_cool_down_time", ChrAttributeXML, menace_cool_down_time, null);
                AlienAttribute.setNode("AreaSweep", "meance_deemed_time", ChrAttributeXML, meance_deemed_time, null);
                AlienAttribute.setNode("AreaSweep", "max_menaces", ChrAttributeXML, max_menaces, null);
                AlienAttribute.setNode("AreaSweep", "menace_gauge_seconds_to_fill", ChrAttributeXML, menace_gauge_seconds_to_fill, null);
                AlienAttribute.setNode("AreaSweep", "sweep_box_half_width", ChrAttributeXML, sweep_box_half_width, null);
                AlienAttribute.setNode("AreaSweep", "sweep_box_min_half_length", ChrAttributeXML, sweep_box_min_half_length, null);
                AlienAttribute.setNode("AreaSweep", "Vent_Attract_Time_Min", ChrAttributeXML, Vent_Attract_Time_Min, null);
                AlienAttribute.setNode("AreaSweep", "Vent_Attract_Time_Max", ChrAttributeXML, Vent_Attract_Time_Max, null);

                //Set BackstageAreaSweep Values
                AlienAttribute.setNode("BackstageAreaSweep", "role_timeout_min", ChrAttributeXML, role_timeout_min, null);
                AlienAttribute.setNode("BackstageAreaSweep", "role_timeout_max", ChrAttributeXML, role_timeout_max, null);
                AlienAttribute.setNode("BackstageAreaSweep", "min_distance", ChrAttributeXML, min_distance, null);
                AlienAttribute.setNode("BackstageAreaSweep", "max_distance", ChrAttributeXML, max_distance, null);
                AlienAttribute.setNode("BackstageAreaSweep", "min_idle_time", ChrAttributeXML, min_idle_time, null);
                AlienAttribute.setNode("BackstageAreaSweep", "max_idle_time", ChrAttributeXML, max_idle_time, null);
                AlienAttribute.setNode("BackstageAreaSweep", "killtrap_time", ChrAttributeXML, killtrap_time, null);
                AlienAttribute.setNode("BackstageAreaSweep", "ambush_timeout", ChrAttributeXML, ambush_timeout, null);

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
            else
            {
                //No config loaded - can't save
                MessageBox.Show("Please load a configuration first!");
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }
    }
}
