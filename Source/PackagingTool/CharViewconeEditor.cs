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
        //Main Directories
        string workingDirectory = Directory.GetCurrentDirectory() + @"\Attribute Editor Directory\"; //Our working dir
        string gameDirectory = File.ReadAllText(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz"); //Our game's dir

        //Common file paths
        string pathToWorkingBML;
        string pathToGameBML;
        string pathToWorkingXML;

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
            squad_sense_activation_delay.Text = "";
            description_set_1_normal.Text = "";
            viewcone_set_set_1_normal.SelectedIndex = -1;
            max_hearing_distance_set_1_normal.Text = "";
            max_damage_distance_scale_to_set_1_normal.Text = "";
            min_raw_activation.Text = "";
            max_raw_activation.Text = "";
            activation_scalar.Text = "";
            combined_sense_min_raw_activation.Text = "";
            combined_sense_max_raw_activation.Text = "";
            combined_sense_activation_scalar.Text = "";
            trace_threshold.Text = "";
            lower_threshold.Text = "";
            activation_threshold.Text = "";
            upper_threshold.Text = "";
            decay_per_second.Text = "";
            min_activated_time.Text = "";
            last_sensed_expire_time.Text = "";
            positional_accuracy_scalar.Text = "";
            Template_Name.SelectedIndex = -1;
            Template_Name.Enabled = false;
            senseSets.Enabled = false;
            loadSet.Enabled = false;
            senseType.Enabled = false;
            loadType.Enabled = false;
            squad_sense_activation_delay.Enabled = false;
            description_set_1_normal.Enabled = false;
            viewcone_set_set_1_normal.Enabled = false;
            max_hearing_distance_set_1_normal.Enabled = false;
            max_damage_distance_scale_to_set_1_normal.Enabled = false;
            min_raw_activation.Enabled = false;
            max_raw_activation.Enabled = false;
            activation_scalar.Enabled = false;
            combined_sense_min_raw_activation.Enabled = false;
            combined_sense_max_raw_activation.Enabled = false;
            combined_sense_activation_scalar.Enabled = false;
            trace_threshold.Enabled = false;
            lower_threshold.Enabled = false;
            activation_threshold.Enabled = false;
            upper_threshold.Enabled = false;
            decay_per_second.Enabled = false;
            min_activated_time.Enabled = false;
            last_sensed_expire_time.Enabled = false;
            positional_accuracy_scalar.Enabled = false;
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

                //Load template name
                IEnumerable<XElement> template = ChrAttributeXML.XPathSelectElements("//Attribute/Template_Name");
                foreach (XElement el in template)
                {
                    if (el.Value.ToString() != "")
                    {
                        Template_Name.Enabled = true;
                        Template_Name.Text = el.Value.ToString();
                    }
                }

                //Get all sense types for character
                senseSets.Items.Clear();
                squad_sense_activation_delay.Text = "";
                squad_sense_activation_delay.Enabled = false;
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
            //Clear all inputs below this
            min_raw_activation.Text = "";
            max_raw_activation.Text = "";
            activation_scalar.Text = "";
            combined_sense_min_raw_activation.Text = "";
            combined_sense_max_raw_activation.Text = "";
            combined_sense_activation_scalar.Text = "";
            trace_threshold.Text = "";
            lower_threshold.Text = "";
            activation_threshold.Text = "";
            upper_threshold.Text = "";
            decay_per_second.Text = "";
            min_activated_time.Text = "";
            last_sensed_expire_time.Text = "";
            positional_accuracy_scalar.Text = "";
            senseType.Enabled = false;
            loadType.Enabled = false;
            min_raw_activation.Enabled = false;
            max_raw_activation.Enabled = false;
            activation_scalar.Enabled = false;
            combined_sense_min_raw_activation.Enabled = false;
            combined_sense_max_raw_activation.Enabled = false;
            combined_sense_activation_scalar.Enabled = false;
            trace_threshold.Enabled = false;
            lower_threshold.Enabled = false;
            activation_threshold.Enabled = false;
            upper_threshold.Enabled = false;
            decay_per_second.Enabled = false;
            min_activated_time.Enabled = false;
            last_sensed_expire_time.Enabled = false;
            positional_accuracy_scalar.Enabled = false;
            senseBox.Text = "Sense Settings";

            //Load-in XML data
            var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

            //Set sense set variables
            full_sense_set_name = senseSets.Text;
            trimmed_sense_set_name = full_sense_set_name.Substring(8).ToLower();

            //Get all sense types for character
            senseType.Items.Clear();
            description_set_1_normal.Text = "";
            description_set_1_normal.Enabled = false;
            viewcone_set_set_1_normal.SelectedIndex = -1;
            viewcone_set_set_1_normal.Enabled = false;
            max_hearing_distance_set_1_normal.Text = "";
            max_hearing_distance_set_1_normal.Enabled = false;
            max_damage_distance_scale_to_set_1_normal.Text = "";
            max_damage_distance_scale_to_set_1_normal.Enabled = false;
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

            //Load sense data
            senseBox.Text = "";
            foreach (string sense_type in trimmed_sense_type_name.Replace("_", " ").Split(' '))
            {
                senseBox.Text = senseBox.Text + sense_type.Substring(0, 1).ToUpper() + sense_type.Substring(1, sense_type.Length - 1).ToLower() + " ";
            }
            senseBox.Text = senseBox.Text + "Settings";
            loadAttributeValueForSenseType(senseType.Text, "min_raw_activation", ChrAttributeXML, min_raw_activation, null);
            loadAttributeValueForSenseType(senseType.Text, "max_raw_activation", ChrAttributeXML, max_raw_activation, null);
            loadAttributeValueForSenseType(senseType.Text, "activation_scalar", ChrAttributeXML, activation_scalar, null);
            loadAttributeValueForSenseType(senseType.Text, "combined_sense_min_raw_activation", ChrAttributeXML, combined_sense_min_raw_activation, null);
            loadAttributeValueForSenseType(senseType.Text, "combined_sense_max_raw_activation", ChrAttributeXML, combined_sense_max_raw_activation, null);
            loadAttributeValueForSenseType(senseType.Text, "combined_sense_activation_scalar", ChrAttributeXML, combined_sense_activation_scalar, null);
            loadAttributeValueForSenseType(senseType.Text, "trace_threshold", ChrAttributeXML, trace_threshold, null);
            loadAttributeValueForSenseType(senseType.Text, "lower_threshold", ChrAttributeXML, lower_threshold, null);
            loadAttributeValueForSenseType(senseType.Text, "activation_threshold", ChrAttributeXML, activation_threshold, null);
            loadAttributeValueForSenseType(senseType.Text, "upper_threshold", ChrAttributeXML, upper_threshold, null);
            loadAttributeValueForSenseType(senseType.Text, "decay_per_second", ChrAttributeXML, decay_per_second, null);
            loadAttributeValueForSenseType(senseType.Text, "min_activated_time", ChrAttributeXML, min_activated_time, null);
            loadAttributeValueForSenseType(senseType.Text, "last_sensed_expire_time", ChrAttributeXML, last_sensed_expire_time, null);
            loadAttributeValueForSenseType(senseType.Text, "positional_accuracy_scalar", ChrAttributeXML, positional_accuracy_scalar, null);
        }

        //Save
        private void btnSave_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            if (pathToWorkingXML != null)
            {
                //Load-in XML to edit
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //template name
                IEnumerable<XElement> template = ChrAttributeXML.XPathSelectElements("//Attribute/Template_Name");
                foreach (XElement el in template)
                {
                    if (el.Value.ToString() != "")
                    {
                        el.Value = Template_Name.Text;
                    }
                }

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
                saveAttributeValueForSenseType(senseType.Text, "min_raw_activation", ChrAttributeXML, min_raw_activation);
                saveAttributeValueForSenseType(senseType.Text, "max_raw_activation", ChrAttributeXML, max_raw_activation);
                saveAttributeValueForSenseType(senseType.Text, "activation_scalar", ChrAttributeXML, activation_scalar);
                saveAttributeValueForSenseType(senseType.Text, "combined_sense_min_raw_activation", ChrAttributeXML, combined_sense_min_raw_activation);
                saveAttributeValueForSenseType(senseType.Text, "combined_sense_max_raw_activation", ChrAttributeXML, combined_sense_max_raw_activation);
                saveAttributeValueForSenseType(senseType.Text, "combined_sense_activation_scalar", ChrAttributeXML, combined_sense_activation_scalar);
                saveAttributeValueForSenseType(senseType.Text, "trace_threshold", ChrAttributeXML, trace_threshold);
                saveAttributeValueForSenseType(senseType.Text, "lower_threshold", ChrAttributeXML, lower_threshold);
                saveAttributeValueForSenseType(senseType.Text, "activation_threshold", ChrAttributeXML, activation_threshold);
                saveAttributeValueForSenseType(senseType.Text, "upper_threshold", ChrAttributeXML, upper_threshold);
                saveAttributeValueForSenseType(senseType.Text, "decay_per_second", ChrAttributeXML, decay_per_second);
                saveAttributeValueForSenseType(senseType.Text, "min_activated_time", ChrAttributeXML, min_activated_time);
                saveAttributeValueForSenseType(senseType.Text, "last_sensed_expire_time", ChrAttributeXML, last_sensed_expire_time);
                saveAttributeValueForSenseType(senseType.Text, "positional_accuracy_scalar", ChrAttributeXML, positional_accuracy_scalar);

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
                MessageBox.Show("Saved new sense settings.");
            }
            else
            {
                //No class loaded - can't save
                MessageBox.Show("Please load a character first!");
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Return XML value (for sense types)
        private void loadAttributeValueForSenseType(string senseType, string specificAttribute, XDocument ChrAttributeXML, TextBox textboxToSet, ComboBox comboboxToSet)
        {
            if (textboxToSet == null)
            {
                try
                {
                    string tempVal = ChrAttributeXML.XPathSelectElement("//Attribute/Senses/" + full_sense_set_name + "/" + senseType + "/" + trimmed_sense_type_name + "_" + specificAttribute + "_" + trimmed_sense_set_name).Value;
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
                    textboxToSet.Text = ChrAttributeXML.XPathSelectElement("//Attribute/Senses/" + full_sense_set_name + "/" + senseType + "/" + trimmed_sense_type_name + "_" + specificAttribute + "_" + trimmed_sense_set_name).Value;
                    textboxToSet.Enabled = true;
                }
                catch
                {
                    textboxToSet.Text = "";
                    textboxToSet.Enabled = false;
                }
            }
        }

        //Set XML value (for sense types)
        private void saveAttributeValueForSenseType(string senseType, string specificAttribute, XDocument ChrAttributeXML, TextBox textboxValue)
        {
            try
            {
                ChrAttributeXML.XPathSelectElement("//Attribute/Senses/" + full_sense_set_name + "/" + senseType + "/" + trimmed_sense_type_name + "_" + specificAttribute + "_" + trimmed_sense_set_name).Value = textboxValue.Text;
            }
            catch
            {
                //Can't save, hopefully because doesnt exist (should be).
            }
        }
    }
}
