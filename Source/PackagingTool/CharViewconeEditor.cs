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
    }
}
