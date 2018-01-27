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
    public partial class CharViewconeEditor : Form
    {
        //Load shared scripts
        AYZ_AttributeEditors AlienAttribute = new AYZ_AttributeEditors();

        //Common file paths
        string pathToWorkingXML;
        string gameBmlDirectory = @"\DATA\CHR_INFO\ATTRIBUTES\";

        //sense set name
        string full_sense_set_name = "";
        string trimmed_sense_set_name = "";

        //sennse type name
        string full_sense_type_name = "";
        string trimmed_sense_type_name = "";

        public CharViewconeEditor()
        {
            InitializeComponent();

            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        //Load character
        private void loadChar_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Clear all inputs below this
            AlienAttribute.disableInput(squad_sense_activation_delay, null);
            AlienAttribute.disableInput(description_set_1_normal, null);
            AlienAttribute.disableInput(null, viewcone_set_set_1_normal);
            AlienAttribute.disableInput(max_hearing_distance_set_1_normal, null);
            AlienAttribute.disableInput(max_damage_distance_scale_to_set_1_normal, null);
            AlienAttribute.disableInput(min_raw_activation, null);
            AlienAttribute.disableInput(max_raw_activation, null);
            AlienAttribute.disableInput(activation_scalar, null);
            AlienAttribute.disableInput(combined_sense_min_raw_activation, null);
            AlienAttribute.disableInput(combined_sense_max_raw_activation, null);
            AlienAttribute.disableInput(combined_sense_activation_scalar, null);
            AlienAttribute.disableInput(trace_threshold, null);
            AlienAttribute.disableInput(lower_threshold, null);
            AlienAttribute.disableInput(activation_threshold, null);
            AlienAttribute.disableInput(upper_threshold, null);
            AlienAttribute.disableInput(decay_per_second, null);
            AlienAttribute.disableInput(min_activated_time, null);
            AlienAttribute.disableInput(last_sensed_expire_time, null);
            AlienAttribute.disableInput(positional_accuracy_scalar, null);
            AlienAttribute.disableInput(null, Template_Name);
            AlienAttribute.disableInput(null, senseType);
            loadType.Enabled = false;
            AlienAttribute.disableInput(null, senseSets);
            loadSet.Enabled = false;
            senseBox.Text = "Sense Settings";

            //Save selected character name
            string selectedClass = characters.Text;

            if (selectedClass == "")
            {
                //No viewcone set selected, can't load anything
                MessageBox.Show("Please select a character first.");
            }
            else
            {
                //Load in XML
                pathToWorkingXML = AlienAttribute.loadXML(selectedClass, gameBmlDirectory);

                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Load template name
                AlienAttribute.getNode("Attribute", "Template_Name", ChrAttributeXML, null, Template_Name);

                //Get all sense types for character
                senseSets.Items.Clear();
                AlienAttribute.disableInput(squad_sense_activation_delay, null);
                IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//Attribute/Senses/*");
                foreach (XElement el in elements)
                {
                    if (el.Name.ToString() == "squad_sense_activation_delay")
                    {
                        squad_sense_activation_delay.Text = el.Value.ToString();
                        squad_sense_activation_delay.Enabled = true;
                    }
                    else
                    {
                        senseSets.Items.Add(el.Name.ToString());
                        senseSets.Enabled = true;
                        loadSet.Enabled = true;
                    }
                }
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Load sense set
        private void loadSet_Click(object sender, EventArgs e)
        {
            //Load-in XML data
            var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

            //Set sense set variables
            full_sense_set_name = senseSets.Text;
            trimmed_sense_set_name = full_sense_set_name.Substring(8).ToLower();
            
            //Clear all inputs below this
            AlienAttribute.disableInput(min_raw_activation, null);
            AlienAttribute.disableInput(max_raw_activation, null);
            AlienAttribute.disableInput(activation_scalar, null);
            AlienAttribute.disableInput(combined_sense_min_raw_activation, null);
            AlienAttribute.disableInput(combined_sense_max_raw_activation, null);
            AlienAttribute.disableInput(combined_sense_activation_scalar, null);
            AlienAttribute.disableInput(trace_threshold, null);
            AlienAttribute.disableInput(lower_threshold, null);
            AlienAttribute.disableInput(activation_threshold, null);
            AlienAttribute.disableInput(upper_threshold, null);
            AlienAttribute.disableInput(decay_per_second, null);
            AlienAttribute.disableInput(min_activated_time, null);
            AlienAttribute.disableInput(last_sensed_expire_time, null);
            AlienAttribute.disableInput(positional_accuracy_scalar, null);
            AlienAttribute.disableInput(description_set_1_normal, null);
            AlienAttribute.disableInput(null, viewcone_set_set_1_normal);
            AlienAttribute.disableInput(max_hearing_distance_set_1_normal, null);
            AlienAttribute.disableInput(max_damage_distance_scale_to_set_1_normal, null);
            AlienAttribute.disableInput(null, senseType);
            loadType.Enabled = false;
            senseBox.Text = "Sense Settings";

            //Get all sense types for character
            senseType.Items.Clear();
            IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//Attribute/Senses/"+full_sense_set_name+"/*");
            foreach (XElement el in elements)
            {
                if (el.Name.ToString() == "description_" + trimmed_sense_set_name)
                {
                    description_set_1_normal.Text = el.Value.ToString();
                    description_set_1_normal.Enabled = true;
                }
                else if (el.Name.ToString() == "viewcone_set_" + trimmed_sense_set_name)
                {
                    viewcone_set_set_1_normal.Text = el.Value.ToString();
                    viewcone_set_set_1_normal.Enabled = true;
                }
                else if (el.Name.ToString() == "max_hearing_distance_" + trimmed_sense_set_name)
                {
                    max_hearing_distance_set_1_normal.Text = el.Value.ToString();
                    max_hearing_distance_set_1_normal.Enabled = true;
                }
                else if (el.Name.ToString() == "max_damage_distance_scale_to_" + trimmed_sense_set_name)
                {
                    max_damage_distance_scale_to_set_1_normal.Text = el.Value.ToString();
                    max_damage_distance_scale_to_set_1_normal.Enabled = true;
                }
                else
                {
                    senseType.Items.Add(el.Name.ToString());
                    senseType.Enabled = true;
                    loadType.Enabled = true;
                }
            }
        }

        //Load Type
        private void loadType_Click(object sender, EventArgs e)
        {
            //Load-in XML data
            var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

            //Set sense type names
            full_sense_type_name = senseType.Text;
            if (full_sense_type_name == "Combined_Sense")
            {
                trimmed_sense_type_name = full_sense_type_name.ToLower();
            }
            else
            {
                trimmed_sense_type_name = senseType.Text.Substring(0, senseType.Text.Length - 6).ToLower();
            }

            //Set sense box header
            senseBox.Text = "";
            foreach (string sense_type in trimmed_sense_type_name.Replace("_", " ").Split(' '))
            {
                senseBox.Text = senseBox.Text + sense_type.Substring(0, 1).ToUpper() + sense_type.Substring(1, sense_type.Length - 1).ToLower() + " ";
            }
            senseBox.Text = senseBox.Text + "Settings";

            //Load sense data
            string senseNodePath = "Attribute/Senses/" + full_sense_set_name + "/" + senseType.Text;
            AlienAttribute.getNode(senseNodePath, trimmed_sense_type_name + "_min_raw_activation_" + trimmed_sense_set_name, ChrAttributeXML, min_raw_activation, null);
            AlienAttribute.getNode(senseNodePath, trimmed_sense_type_name + "_max_raw_activation_" + trimmed_sense_set_name, ChrAttributeXML, max_raw_activation, null);
            AlienAttribute.getNode(senseNodePath, trimmed_sense_type_name + "_activation_scalar_" + trimmed_sense_set_name, ChrAttributeXML, activation_scalar, null);
            AlienAttribute.getNode(senseNodePath, trimmed_sense_type_name + "_combined_sense_min_raw_activation_" + trimmed_sense_set_name, ChrAttributeXML, combined_sense_min_raw_activation, null);
            AlienAttribute.getNode(senseNodePath, trimmed_sense_type_name + "_combined_sense_max_raw_activation_" + trimmed_sense_set_name, ChrAttributeXML, combined_sense_max_raw_activation, null);
            AlienAttribute.getNode(senseNodePath, trimmed_sense_type_name + "_combined_sense_activation_scalar_" + trimmed_sense_set_name, ChrAttributeXML, combined_sense_activation_scalar, null);
            AlienAttribute.getNode(senseNodePath, trimmed_sense_type_name + "_trace_threshold_" + trimmed_sense_set_name, ChrAttributeXML, trace_threshold, null);
            AlienAttribute.getNode(senseNodePath, trimmed_sense_type_name + "_lower_threshold_" + trimmed_sense_set_name, ChrAttributeXML, lower_threshold, null);
            AlienAttribute.getNode(senseNodePath, trimmed_sense_type_name + "_activation_threshold_" + trimmed_sense_set_name, ChrAttributeXML, activation_threshold, null);
            AlienAttribute.getNode(senseNodePath, trimmed_sense_type_name + "_upper_threshold_" + trimmed_sense_set_name, ChrAttributeXML, upper_threshold, null);
            AlienAttribute.getNode(senseNodePath, trimmed_sense_type_name + "_decay_per_second_" + trimmed_sense_set_name, ChrAttributeXML, decay_per_second, null);
            AlienAttribute.getNode(senseNodePath, trimmed_sense_type_name + "_min_activated_time_" + trimmed_sense_set_name, ChrAttributeXML, min_activated_time, null);
            AlienAttribute.getNode(senseNodePath, trimmed_sense_type_name + "_last_sensed_expire_time_" + trimmed_sense_set_name, ChrAttributeXML, last_sensed_expire_time, null);
            AlienAttribute.getNode(senseNodePath, trimmed_sense_type_name + "_positional_accuracy_scalar_" + trimmed_sense_set_name, ChrAttributeXML, positional_accuracy_scalar, null);
        }

        //Save
        private void btnSave_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Save selected config name
            string selectedClass = characters.Text;

            if (pathToWorkingXML != null)
            {
                //Load-in XML to edit
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //template name
                AlienAttribute.setNode("Attribute", "Template_Name", ChrAttributeXML, null, Template_Name);

                //Squad sense delay
                IEnumerable<XElement> squadsense = ChrAttributeXML.XPathSelectElements("//Attribute/Senses/*");
                foreach (XElement el in squadsense)
                {
                    if (el.Name.ToString() == "squad_sense_activation_delay")
                    {
                        el.Value = squad_sense_activation_delay.Text;
                    }
                }

                //Generic Sense Set Values
                IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//Attribute/Senses/" + full_sense_set_name + "/*");
                foreach (XElement el in elements)
                {
                    if (el.Name.ToString() == "description_" + trimmed_sense_set_name)
                    {
                        el.Value = description_set_1_normal.Text;
                    }
                    else if (el.Name.ToString() == "viewcone_set_" + trimmed_sense_set_name)
                    {
                        el.Value = viewcone_set_set_1_normal.Text;
                    }
                    else if (el.Name.ToString() == "max_hearing_distance_" + trimmed_sense_set_name)
                    {
                        el.Value = max_hearing_distance_set_1_normal.Text;
                    }
                    else if (el.Name.ToString() == "max_damage_distance_scale_to_" + trimmed_sense_set_name)
                    {
                        el.Value = max_damage_distance_scale_to_set_1_normal.Text;
                    }
                }

                //Save all sense type attributes
                string senseNodePath = "Attribute/Senses/" + full_sense_set_name + "/" + senseType.Text;
                AlienAttribute.setNode(senseNodePath, trimmed_sense_type_name + "_min_raw_activation_" + trimmed_sense_set_name, ChrAttributeXML, min_raw_activation, null);
                AlienAttribute.setNode(senseNodePath, trimmed_sense_type_name + "_max_raw_activation_" + trimmed_sense_set_name, ChrAttributeXML, max_raw_activation, null);
                AlienAttribute.setNode(senseNodePath, trimmed_sense_type_name + "_activation_scalar_" + trimmed_sense_set_name, ChrAttributeXML, activation_scalar, null);
                AlienAttribute.setNode(senseNodePath, trimmed_sense_type_name + "_combined_sense_min_raw_activation_" + trimmed_sense_set_name, ChrAttributeXML, combined_sense_min_raw_activation, null);
                AlienAttribute.setNode(senseNodePath, trimmed_sense_type_name + "_combined_sense_max_raw_activation_" + trimmed_sense_set_name, ChrAttributeXML, combined_sense_max_raw_activation, null);
                AlienAttribute.setNode(senseNodePath, trimmed_sense_type_name + "_combined_sense_activation_scalar_" + trimmed_sense_set_name, ChrAttributeXML, combined_sense_activation_scalar, null);
                AlienAttribute.setNode(senseNodePath, trimmed_sense_type_name + "_trace_threshold_" + trimmed_sense_set_name, ChrAttributeXML, trace_threshold, null);
                AlienAttribute.setNode(senseNodePath, trimmed_sense_type_name + "_lower_threshold_" + trimmed_sense_set_name, ChrAttributeXML, lower_threshold, null);
                AlienAttribute.setNode(senseNodePath, trimmed_sense_type_name + "_activation_threshold_" + trimmed_sense_set_name, ChrAttributeXML, activation_threshold, null);
                AlienAttribute.setNode(senseNodePath, trimmed_sense_type_name + "_upper_threshold_" + trimmed_sense_set_name, ChrAttributeXML, upper_threshold, null);
                AlienAttribute.setNode(senseNodePath, trimmed_sense_type_name + "_decay_per_second_" + trimmed_sense_set_name, ChrAttributeXML, decay_per_second, null);
                AlienAttribute.setNode(senseNodePath, trimmed_sense_type_name + "_min_activated_time_" + trimmed_sense_set_name, ChrAttributeXML, min_activated_time, null);
                AlienAttribute.setNode(senseNodePath, trimmed_sense_type_name + "_last_sensed_expire_time_" + trimmed_sense_set_name, ChrAttributeXML, last_sensed_expire_time, null);
                AlienAttribute.setNode(senseNodePath, trimmed_sense_type_name + "_positional_accuracy_scalar_" + trimmed_sense_set_name, ChrAttributeXML, positional_accuracy_scalar, null);

                //Save values
                if (AlienAttribute.saveXML(selectedClass, gameBmlDirectory, ChrAttributeXML))
                {
                    MessageBox.Show("Saved new sense settings.");
                }
                else
                {
                    MessageBox.Show("An error occured while saving.");
                }
            }
            else
            {
                //No class loaded - can't save
                MessageBox.Show("Please load a character first!");
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }
    }
}
