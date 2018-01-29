/*
 * 
 * Created by Matt Filer
 * www.mattfiler.co.uk
 * 
 */

 using PackagingTool;
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

namespace Alien_Isolation_Mod_Tools
{
    public partial class HackingEditor : Form
    {
        Directories AlienDirectories = new Directories();

        //Common file paths
        string pathToWorkingBML;
        string pathToGameBML;
        string pathToGameXML;
        string pathToWorkingXML;

        public HackingEditor()
        {
            InitializeComponent();

            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;

            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Set common file paths
            pathToWorkingBML = AlienDirectories.ToolWorkingDirectory() + "GBL_ITEM.BML";
            pathToGameBML = AlienDirectories.GameDirectoryRoot() + @"\DATA\GBL_ITEM.BML";
            pathToGameXML = AlienDirectories.GameDirectoryRoot() + @"\DATA\GBL_ITEM.XML";
            pathToWorkingXML = AlienDirectories.ToolWorkingDirectory() + "GBL_ITEM.xml";

            //Copy correct XML to working directory and fix bug
            StreamWriter updateXmlContents = new StreamWriter(pathToWorkingXML);
            updateXmlContents.WriteLine(File.ReadAllText(pathToGameXML).Replace(" xmlns=\"http://www.w3schools.com\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"http://www.w3schools.com gbl_item.xsd\"", ""));
            updateXmlContents.Close();


            //Load-in XML data
            var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

            //Get difficulty ranges
            IEnumerable<XElement> diffsMaxMin = ChrAttributeXML.XPathSelectElements("//item_database/hacking_gating_levels/hack_max_difficulty");
            foreach (XElement el in diffsMaxMin)
            {
                if (el.Attribute("tool_level").Value.ToString() == "0")
                {
                    max_difficulty_0.Enabled = true;
                    max_difficulty_0.Text = el.Attribute("max_difficulty").Value.ToString();
                }
                if (el.Attribute("tool_level").Value.ToString() == "1")
                {
                    max_difficulty_1.Enabled = true;
                    max_difficulty_1.Text = el.Attribute("max_difficulty").Value.ToString();
                }
                if (el.Attribute("tool_level").Value.ToString() == "2")
                {
                    max_difficulty_2.Enabled = true;
                    max_difficulty_2.Text = el.Attribute("max_difficulty").Value.ToString();
                }
                if (el.Attribute("tool_level").Value.ToString() == "3")
                {
                    max_difficulty_3.Enabled = true;
                    max_difficulty_3.Text = el.Attribute("max_difficulty").Value.ToString();
                }
                if (el.Attribute("tool_level").Value.ToString() == "99")
                {
                    min_difficulty_99.Enabled = true;
                    min_difficulty_99.Text = el.Attribute("min_difficulty").Value.ToString();
                    max_difficulty_99.Enabled = true;
                    max_difficulty_99.Text = el.Attribute("max_difficulty").Value.ToString();
                }
            }

            //Get difficulties
            hackDifficulties.Items.Clear();
            IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//item_database/hacking_game_difficulties/hack_difficulty");
            foreach (XElement el in elements)
            {
                hackDifficulties.Items.Add(el.Attribute("difficulty").Value.ToString());
                hackDifficulties.Enabled = true;
                btnSelectClass.Enabled = true;
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Load
        private void btnSelectClass_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Save selected difficulty level name
            string selectedType = hackDifficulties.Text;

            if (selectedType == "")
            {
                //No difficulty set selected, can't load anything
                MessageBox.Show("Please select a difficulty level first.");
            }
            else
            {
                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Get all data from type
                IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//item_database/hacking_game_difficulties/hack_difficulty");
                foreach (XElement el in elements)
                {
                    if (el.Attribute("difficulty").Value.ToString() == selectedType)
                    {
                        inner_selection_angle_in_deg.Enabled = true;
                        inner_selection_angle_in_deg.Text = el.Attribute("inner_selection_angle_in_deg").Value.ToString();
                        outer_selection_angle_in_deg.Enabled = true;
                        outer_selection_angle_in_deg.Text = el.Attribute("outer_selection_angle_in_deg").Value.ToString();
                        selection_angle_increase_in_deg.Enabled = true;
                        selection_angle_increase_in_deg.Text = el.Attribute("selection_angle_increase_in_deg").Value.ToString();
                        number_of_rounds.Enabled = true;
                        number_of_rounds.Text = el.Attribute("number_of_rounds").Value.ToString();
                        length_of_keycode.Enabled = true;
                        length_of_keycode.Text = el.Attribute("length_of_keycode").Value.ToString();
                        number_of_alarms.Enabled = true;
                        number_of_alarms.Text = el.Attribute("number_of_alarms").Value.ToString();
                        timer_countdown_seconds.Enabled = true;
                        timer_countdown_seconds.Text = el.Attribute("timer_countdown_seconds").Value.ToString();
                    }
                }
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Save
        private void btnSave_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Save selected viewcone set name
            string selectedType = hackDifficulties.Text;

            if (selectedType == "")
            {
                //No difficulty level selected, can't load anything
                MessageBox.Show("Please load a difficulty level first.");
            }
            else
            {
                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Save difficulty ranges
                IEnumerable<XElement> diffsMaxMin = ChrAttributeXML.XPathSelectElements("//item_database/hacking_gating_levels/hack_max_difficulty");
                foreach (XElement el in diffsMaxMin)
                {
                    if (el.Attribute("tool_level").Value.ToString() == "0")
                    {
                        el.Attribute("max_difficulty").Value = max_difficulty_0.Text;
                    }
                    if (el.Attribute("tool_level").Value.ToString() == "1")
                    {
                        el.Attribute("max_difficulty").Value = max_difficulty_1.Text;
                    }
                    if (el.Attribute("tool_level").Value.ToString() == "2")
                    {
                        el.Attribute("max_difficulty").Value = max_difficulty_2.Text;
                    }
                    if (el.Attribute("tool_level").Value.ToString() == "3")
                    {
                        el.Attribute("max_difficulty").Value = max_difficulty_3.Text;
                    }
                    if (el.Attribute("tool_level").Value.ToString() == "99")
                    {
                        el.Attribute("min_difficulty").Value = min_difficulty_99.Text;
                        el.Attribute("max_difficulty").Value = max_difficulty_99.Text;
                    }
                }

                //Save all data from settings
                IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//item_database/hacking_game_difficulties/hack_difficulty");
                foreach (XElement el in elements)
                {
                    if (el.Attribute("difficulty").Value.ToString() == selectedType)
                    {
                        el.Attribute("inner_selection_angle_in_deg").Value = inner_selection_angle_in_deg.Text;
                        el.Attribute("outer_selection_angle_in_deg").Value = outer_selection_angle_in_deg.Text;
                        el.Attribute("selection_angle_increase_in_deg").Value = selection_angle_increase_in_deg.Text;
                        el.Attribute("number_of_rounds").Value = number_of_rounds.Text;
                        el.Attribute("length_of_keycode").Value = length_of_keycode.Text;
                        el.Attribute("number_of_alarms").Value = number_of_alarms.Text;
                        el.Attribute("timer_countdown_seconds").Value = timer_countdown_seconds.Text; 
                    }
                }

                //Save all to XML
                ChrAttributeXML.Save(pathToWorkingXML);

                //Convert XML to BML
                new AlienConverter(pathToWorkingXML, pathToWorkingBML).Run();

                //Copy new BML to game directory & remove working files
                File.Delete(pathToGameBML);
                File.Copy(pathToWorkingBML, pathToGameBML);
                File.Delete(pathToGameXML);
                File.Copy(pathToWorkingXML, pathToGameXML);
                File.Delete(pathToWorkingBML);
                //File.Delete(pathToWorkingXML);

                //Done
                MessageBox.Show("Saved new hack difficulty settings.");
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }
    }
}
