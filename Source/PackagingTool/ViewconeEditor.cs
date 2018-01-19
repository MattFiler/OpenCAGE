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
    public partial class ViewconeEditor : Form
    {
        //Main Directories
        string workingDirectory = Directory.GetCurrentDirectory() + @"\Attribute Editor Directory\"; //Our working dir
        string gameDirectory = File.ReadAllText(Directory.GetCurrentDirectory() + @"\packagingtool_locales.ayz"); //Our game's dir

        //Common file paths
        string pathToWorkingBML;
        string pathToGameBML;
        string pathToWorkingXML;

        public ViewconeEditor()
        {
            InitializeComponent();

            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        //load viewcone set
        private void loadSet_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Save selected viewcone set name
            string selectedClass = viewconeSets.Text;

            if (selectedClass == "")
            {
                //No viewcone set selected, can't load anything
                MessageBox.Show("Please select a viewcone set first.");
            }
            else
            {
                //Set common file paths
                pathToWorkingBML = workingDirectory + selectedClass + ".BML";
                pathToGameBML = gameDirectory + @"\DATA\VIEW_CONE_SETS\" + selectedClass + ".BML";
                pathToWorkingXML = workingDirectory + selectedClass + ".xml";

                //Copy correct BML to working directory
                File.Copy(pathToGameBML, pathToWorkingBML);

                //Convert BML to XML
                new AlienConverter(pathToWorkingBML, pathToWorkingXML).Run();

                //Delete BML
                File.Delete(pathToWorkingBML);


                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Get types
                viewconeTypes.Items.Clear();
                IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//ViewconeSettings/ViewconeSetting/ViewconeSettings_type");
                foreach (XElement el in elements)
                {
                    viewconeTypes.Items.Add(el.Value.ToString());
                    viewconeTypes.Enabled = true;
                    loadType.Enabled = true;
                }
            }
            
            Length.Text = "";
            SmokeLengthModifier.Text = "";
            VerticalAngle.Text = "";
            HorizontalAngle.Text = "";
            ExposureEffectLower.Text = "";
            ExposureEffectUpper.Text = "";
            StanceEffectLower.Text = "";
            StanceEffectUpper.Text = "";
            MovementEffectLower.Text = "";
            MovementEffectUpper.Text = "";
            SmokeEffectLower.Text = "";
            SmokeEffectUpper.Text = "";
            DistanceEffectLower.Text = "";
            DistanceEffectUpper.Text = "";
            Light_meter_dark_level.Text = "";
            Light_meter_partially_lit_level.Text = "";
            Light_meter_fully_lit_level.Text = "";
            Length.Enabled = false;
            SmokeLengthModifier.Enabled = false;
            VerticalAngle.Enabled = false;
            HorizontalAngle.Enabled = false;
            ExposureEffectLower.Enabled = false;
            ExposureEffectUpper.Enabled = false;
            StanceEffectLower.Enabled = false;
            StanceEffectUpper.Enabled = false;
            MovementEffectLower.Enabled = false;
            MovementEffectUpper.Enabled = false;
            SmokeEffectLower.Enabled = false;
            SmokeEffectUpper.Enabled = false;
            DistanceEffectLower.Enabled = false;
            DistanceEffectUpper.Enabled = false;
            Light_meter_dark_level.Enabled = false;
            Light_meter_partially_lit_level.Enabled = false;
            Light_meter_fully_lit_level.Enabled = false;

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //load viewcone type
        private void loadType_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Save selected viewcone set name
            string selectedType = viewconeTypes.Text;

            if (selectedType == "")
            {
                //No viewcone set selected, can't load anything
                MessageBox.Show("Please select a viewcone type first.");
            }
            else
            {
                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Get all data from type
                IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//ViewconeSettings/ViewconeSetting");
                foreach (XElement el in elements)
                {
                    if (el.Element("ViewconeSettings_type").Value == selectedType)
                    {
                        loadAttributeValue("Length", el, Length);
                        loadAttributeValue("SmokeLengthModifier", el, SmokeLengthModifier);
                        loadAttributeValue("VerticalAngle", el, VerticalAngle);
                        loadAttributeValue("HorizontalAngle", el, HorizontalAngle);
                        loadAttributeValue("ExposureEffectLower", el, ExposureEffectLower);
                        loadAttributeValue("ExposureEffectUpper", el, ExposureEffectUpper);
                        loadAttributeValue("StanceEffectLower", el, StanceEffectLower);
                        loadAttributeValue("StanceEffectUpper", el, StanceEffectUpper);
                        loadAttributeValue("MovementEffectLower", el, MovementEffectLower);
                        loadAttributeValue("MovementEffectUpper", el, MovementEffectUpper);
                        loadAttributeValue("SmokeEffectLower", el, SmokeEffectLower);
                        loadAttributeValue("SmokeEffectUpper", el, SmokeEffectUpper);
                        loadAttributeValue("DistanceEffectLower", el, DistanceEffectLower);
                        loadAttributeValue("DistanceEffectUpper", el, DistanceEffectUpper);
                        loadAttributeValue("Light_meter_dark_level", el, Light_meter_dark_level);
                        loadAttributeValue("Light_meter_partially_lit_level", el, Light_meter_partially_lit_level);
                        loadAttributeValue("Light_meter_fully_lit_level", el, Light_meter_fully_lit_level);
                    }
                }
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }
        
        //Save viewcone type
        private void btnSave_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Save selected viewcone set name
            string selectedType = viewconeTypes.Text;

            if (selectedType == "")
            {
                //No viewcone set selected, can't load anything
                MessageBox.Show("Please load a viewcone type first.");
            }
            else
            {
                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Get all data from type
                IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//ViewconeSettings/ViewconeSetting");
                foreach (XElement el in elements)
                {
                    if (el.Element("ViewconeSettings_type").Value == selectedType)
                    {
                        saveAttributeValue("Length", el, Length);
                        saveAttributeValue("SmokeLengthModifier", el, SmokeLengthModifier);
                        saveAttributeValue("VerticalAngle", el, VerticalAngle);
                        saveAttributeValue("HorizontalAngle", el, HorizontalAngle);
                        saveAttributeValue("ExposureEffectLower", el, ExposureEffectLower);
                        saveAttributeValue("ExposureEffectUpper", el, ExposureEffectUpper);
                        saveAttributeValue("StanceEffectLower", el, StanceEffectLower);
                        saveAttributeValue("StanceEffectUpper", el, StanceEffectUpper);
                        saveAttributeValue("MovementEffectLower", el, MovementEffectLower);
                        saveAttributeValue("MovementEffectUpper", el, MovementEffectUpper);
                        saveAttributeValue("SmokeEffectLower", el, SmokeEffectLower);
                        saveAttributeValue("SmokeEffectUpper", el, SmokeEffectUpper);
                        saveAttributeValue("DistanceEffectLower", el, DistanceEffectLower);
                        saveAttributeValue("DistanceEffectUpper", el, DistanceEffectUpper);
                        saveAttributeValue("Light_meter_dark_level", el, Light_meter_dark_level);
                        saveAttributeValue("Light_meter_partially_lit_level", el, Light_meter_partially_lit_level);
                        saveAttributeValue("Light_meter_fully_lit_level", el, Light_meter_fully_lit_level);
                    }
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
                MessageBox.Show("Saved new viewcone type settings.");
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Return XML value
        private void loadAttributeValue(string specificAttribute, XElement el, TextBox textboxToSet)
        {
            try
            {
                if (el.Element(specificAttribute).Value == "")
                {
                    textboxToSet.Text = "";
                    textboxToSet.Enabled = false;
                }
                else
                {
                    textboxToSet.Text = el.Element(specificAttribute).Value;
                    textboxToSet.Enabled = true;
                }
            }
            catch
            {
                textboxToSet.Text = "";
                textboxToSet.Enabled = false;
            }
        }
        
        //Set XML value
        private void saveAttributeValue(string specificAttribute, XElement el, TextBox textboxToSet)
        {
            try
            {
                el.Element(specificAttribute).Value = textboxToSet.Text;
            }
            catch
            {
                //Can't save, hopefully because doesnt exist (should be).
            }
        }
    }
}
