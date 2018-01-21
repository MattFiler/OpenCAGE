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
    public partial class AlienConfigEditor : Form
    {
        //Main Directories
        string workingDirectory = Directory.GetCurrentDirectory() + @"\Attribute Editor Directory\"; //Our working dir
        string gameDirectory = File.ReadAllText(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz"); //Our game's dir

        //Common file paths
        string pathToWorkingBML;
        string pathToGameBML;
        string pathToWorkingXML;

        public AlienConfigEditor()
        {
            InitializeComponent();

            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;

            //Set base AlienConfig Values
            Template_Name.Enabled = false;

            //Set AreaSweep Values
            decrease_sweep_duration.Enabled = false;
            increase_sweep_duration.Enabled = false;
            near_target_exclusion_radius_first_stalk_min.Enabled = false;
            near_target_exclusion_radius_first_stalk_max.Enabled = false;
            near_target_exclusion_radius_subsequent_stalk_min.Enabled = false;
            near_target_exclusion_radius_subsequent_stalk_max.Enabled = false;
            near_objective_exclusion_radius_first_stalk_min.Enabled = false;
            near_objective_exclusion_radius_first_stalk_max.Enabled = false;
            near_objective_exclusion_radius_subsequent_stalk_min.Enabled = false;
            near_objective_exclusion_radius_subsequent_stalk_max.Enabled = false;
            menace_gauge_decrease_time.Enabled = false;
            menace_cool_down_time.Enabled = false;
            meance_deemed_time.Enabled = false;
            max_menaces.Enabled = false;
            menace_gauge_seconds_to_fill.Enabled = false;
            sweep_box_half_width.Enabled = false;
            sweep_box_min_half_length.Enabled = false;
            Vent_Attract_Time_Min.Enabled = false;
            Vent_Attract_Time_Max.Enabled = false;

            //Set BackstageAreaSweep Values
            role_timeout_min.Enabled = false;
            role_timeout_max.Enabled = false;
            min_distance.Enabled = false;
            max_distance.Enabled = false;
            min_idle_time.Enabled = false;
            max_idle_time.Enabled = false;
            killtrap_time.Enabled = false;
            ambush_timeout.Enabled = false;
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
                //Set common file paths
                pathToWorkingBML = workingDirectory + selectedConfig + ".BML";
                pathToGameBML = gameDirectory + @"\DATA\ALIENCONFIGS\" + selectedConfig + ".BML";
                pathToWorkingXML = workingDirectory + selectedConfig + ".xml";

                //Copy correct BML to working directory
                File.Copy(pathToGameBML, pathToWorkingBML);

                //Convert BML to XML
                new AlienConverter(pathToWorkingBML, pathToWorkingXML).Run();

                //Delete BML
                File.Delete(pathToWorkingBML);


                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Set base AlienConfig Values
                loadAttributeValue("AlienConfig", "Template_Name", ChrAttributeXML, null, Template_Name);

                //Set AreaSweep Values
                loadAttributeValue("AreaSweep", "decrease_sweep_duration", ChrAttributeXML, decrease_sweep_duration, null);
                loadAttributeValue("AreaSweep", "increase_sweep_duration", ChrAttributeXML, increase_sweep_duration, null);
                loadAttributeValue("AreaSweep", "near_target_exclusion_radius_first_stalk_min", ChrAttributeXML, near_target_exclusion_radius_first_stalk_min, null);
                loadAttributeValue("AreaSweep", "near_target_exclusion_radius_first_stalk_max", ChrAttributeXML, near_target_exclusion_radius_first_stalk_max, null);
                loadAttributeValue("AreaSweep", "near_target_exclusion_radius_subsequent_stalk_min", ChrAttributeXML, near_target_exclusion_radius_subsequent_stalk_min, null);
                loadAttributeValue("AreaSweep", "near_target_exclusion_radius_subsequent_stalk_max", ChrAttributeXML, near_target_exclusion_radius_subsequent_stalk_max, null);
                loadAttributeValue("AreaSweep", "near_objective_exclusion_radius_first_stalk_min", ChrAttributeXML, near_objective_exclusion_radius_first_stalk_min, null);
                loadAttributeValue("AreaSweep", "near_objective_exclusion_radius_first_stalk_max", ChrAttributeXML, near_objective_exclusion_radius_first_stalk_max, null);
                loadAttributeValue("AreaSweep", "near_objective_exclusion_radius_subsequent_stalk_min", ChrAttributeXML, near_objective_exclusion_radius_subsequent_stalk_min, null);
                loadAttributeValue("AreaSweep", "near_objective_exclusion_radius_subsequent_stalk_max", ChrAttributeXML, near_objective_exclusion_radius_subsequent_stalk_max, null);
                loadAttributeValue("AreaSweep", "menace_gauge_decrease_time", ChrAttributeXML, menace_gauge_decrease_time, null);
                loadAttributeValue("AreaSweep", "menace_cool_down_time", ChrAttributeXML, menace_cool_down_time, null);
                loadAttributeValue("AreaSweep", "meance_deemed_time", ChrAttributeXML, meance_deemed_time, null);
                loadAttributeValue("AreaSweep", "max_menaces", ChrAttributeXML, max_menaces, null);
                loadAttributeValue("AreaSweep", "menace_gauge_seconds_to_fill", ChrAttributeXML, menace_gauge_seconds_to_fill, null);
                loadAttributeValue("AreaSweep", "sweep_box_half_width", ChrAttributeXML, sweep_box_half_width, null);
                loadAttributeValue("AreaSweep", "sweep_box_min_half_length", ChrAttributeXML, sweep_box_min_half_length, null);
                loadAttributeValue("AreaSweep", "Vent_Attract_Time_Min", ChrAttributeXML, Vent_Attract_Time_Min, null);
                loadAttributeValue("AreaSweep", "Vent_Attract_Time_Max", ChrAttributeXML, Vent_Attract_Time_Max, null);

                //Set BackstageAreaSweep Values
                loadAttributeValue("BackstageAreaSweep", "role_timeout_min", ChrAttributeXML, role_timeout_min, null);
                loadAttributeValue("BackstageAreaSweep", "role_timeout_max", ChrAttributeXML, role_timeout_max, null);
                loadAttributeValue("BackstageAreaSweep", "min_distance", ChrAttributeXML, min_distance, null);
                loadAttributeValue("BackstageAreaSweep", "max_distance", ChrAttributeXML, max_distance, null);
                loadAttributeValue("BackstageAreaSweep", "min_idle_time", ChrAttributeXML, min_idle_time, null);
                loadAttributeValue("BackstageAreaSweep", "max_idle_time", ChrAttributeXML, max_idle_time, null);
                loadAttributeValue("BackstageAreaSweep", "killtrap_time", ChrAttributeXML, killtrap_time, null);
                loadAttributeValue("BackstageAreaSweep", "ambush_timeout", ChrAttributeXML, ambush_timeout, null);
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Save Config
        private void btnSave_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            if (pathToWorkingXML != null)
            {
                //Load-in XML to edit
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Set base AlienConfig Values
                saveAttributeValue("AlienConfig", "Template_Name", ChrAttributeXML, Template_Name.Text);

                //Set AreaSweep Values
                saveAttributeValue("AreaSweep", "decrease_sweep_duration", ChrAttributeXML, decrease_sweep_duration.Text);
                saveAttributeValue("AreaSweep", "increase_sweep_duration", ChrAttributeXML, increase_sweep_duration.Text);
                saveAttributeValue("AreaSweep", "near_target_exclusion_radius_first_stalk_min", ChrAttributeXML, near_target_exclusion_radius_first_stalk_min.Text);
                saveAttributeValue("AreaSweep", "near_target_exclusion_radius_first_stalk_max", ChrAttributeXML, near_target_exclusion_radius_first_stalk_max.Text);
                saveAttributeValue("AreaSweep", "near_target_exclusion_radius_subsequent_stalk_min", ChrAttributeXML, near_target_exclusion_radius_subsequent_stalk_min.Text);
                saveAttributeValue("AreaSweep", "near_target_exclusion_radius_subsequent_stalk_max", ChrAttributeXML, near_target_exclusion_radius_subsequent_stalk_max.Text);
                saveAttributeValue("AreaSweep", "near_objective_exclusion_radius_first_stalk_min", ChrAttributeXML, near_objective_exclusion_radius_first_stalk_min.Text);
                saveAttributeValue("AreaSweep", "near_objective_exclusion_radius_first_stalk_max", ChrAttributeXML, near_objective_exclusion_radius_first_stalk_max.Text);
                saveAttributeValue("AreaSweep", "near_objective_exclusion_radius_subsequent_stalk_min", ChrAttributeXML, near_objective_exclusion_radius_subsequent_stalk_min.Text);
                saveAttributeValue("AreaSweep", "near_objective_exclusion_radius_subsequent_stalk_max", ChrAttributeXML, near_objective_exclusion_radius_subsequent_stalk_max.Text);
                saveAttributeValue("AreaSweep", "menace_gauge_decrease_time", ChrAttributeXML, menace_gauge_decrease_time.Text);
                saveAttributeValue("AreaSweep", "menace_cool_down_time", ChrAttributeXML, menace_cool_down_time.Text);
                saveAttributeValue("AreaSweep", "meance_deemed_time", ChrAttributeXML, meance_deemed_time.Text);
                saveAttributeValue("AreaSweep", "max_menaces", ChrAttributeXML, max_menaces.Text);
                saveAttributeValue("AreaSweep", "menace_gauge_seconds_to_fill", ChrAttributeXML, menace_gauge_seconds_to_fill.Text);
                saveAttributeValue("AreaSweep", "sweep_box_half_width", ChrAttributeXML, sweep_box_half_width.Text);
                saveAttributeValue("AreaSweep", "sweep_box_min_half_length", ChrAttributeXML, sweep_box_min_half_length.Text);
                saveAttributeValue("AreaSweep", "Vent_Attract_Time_Min", ChrAttributeXML, Vent_Attract_Time_Min.Text);
                saveAttributeValue("AreaSweep", "Vent_Attract_Time_Max", ChrAttributeXML, Vent_Attract_Time_Max.Text);

                //Set BackstageAreaSweep Values
                saveAttributeValue("BackstageAreaSweep", "role_timeout_min", ChrAttributeXML, role_timeout_min.Text);
                saveAttributeValue("BackstageAreaSweep", "role_timeout_max", ChrAttributeXML, role_timeout_max.Text);
                saveAttributeValue("BackstageAreaSweep", "min_distance", ChrAttributeXML, min_distance.Text);
                saveAttributeValue("BackstageAreaSweep", "max_distance", ChrAttributeXML, max_distance.Text);
                saveAttributeValue("BackstageAreaSweep", "min_idle_time", ChrAttributeXML, min_idle_time.Text);
                saveAttributeValue("BackstageAreaSweep", "max_idle_time", ChrAttributeXML, max_idle_time.Text);
                saveAttributeValue("BackstageAreaSweep", "killtrap_time", ChrAttributeXML, killtrap_time.Text);
                saveAttributeValue("BackstageAreaSweep", "ambush_timeout", ChrAttributeXML, ambush_timeout.Text);

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
            else
            {
                //No config loaded - can't save
                MessageBox.Show("Please load a configuration first!");
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

                /*
                //No data set from normal config, try set data from template (will still be DISABLED input as we can't modify templates from this config load currently).
                if (comboboxToSet.Text == "")
                {
                    try
                    {
                        string parentConfig = ChrAttributeXML.XPathSelectElement("//AlienConfig/Template_Name").Value;
                        
                        File.Copy(gameDirectory + @"\DATA\ALIENCONFIGS\" + parentConfig + ".BML", workingDirectory + parentConfig + ".BML");
                        new AlienConverter(workingDirectory + parentConfig + ".BML", workingDirectory + parentConfig + ".xml").Run();
                        File.Delete(workingDirectory + parentConfig + ".BML");

                        var parentAttributeXML = XDocument.Load(workingDirectory + parentConfig + ".xml");

                        string tempVal = parentAttributeXML.XPathSelectElement("//" + attributeGroup + "/" + specificAttribute).Value;
                        if (tempVal != "")
                        {
                            comboboxToSet.Text = tempVal;
                        }
                    }
                    catch
                    {
                        //No other config to load from, don't do anything.
                    }
                }
                */
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

                /*
                //No data set from normal config, try set data from template (will still be DISABLED input as we can't modify templates from this config load currently).
                int counter = 0;
                //RECURSE THESE BACK INTO PARENTS
                while (textboxToSet.Text == "")
                {
                    try
                    {
                        string parentConfig = ChrAttributeXML.XPathSelectElement("//AlienConfig/Template_Name").Value;

                        File.Copy(gameDirectory + @"\DATA\ALIENCONFIGS\" + parentConfig + ".BML", workingDirectory + parentConfig + ".BML");
                        new AlienConverter(workingDirectory + parentConfig + ".BML", workingDirectory + parentConfig + ".xml").Run();
                        File.Delete(workingDirectory + parentConfig + ".BML");

                        var parentAttributeXML = XDocument.Load(workingDirectory + parentConfig + ".xml");

                        string tempVal = parentAttributeXML.XPathSelectElement("//" + attributeGroup + "/" + specificAttribute).Value;
                        if (tempVal != "")
                        {
                            textboxToSet.Text = tempVal;
                        }
                    }
                    catch
                    {
                        //No other config to load from, don't do anything.
                    }
                    counter++;
                    if (counter == 3)
                    {
                        break;
                    }
                }
                */
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
