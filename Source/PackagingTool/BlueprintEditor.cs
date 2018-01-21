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
    public partial class BlueprintEditor : Form
    {
        //Main Directories
        string workingDirectory = Directory.GetCurrentDirectory() + @"\Attribute Editor Directory\"; //Our working dir
        string gameDirectory = File.ReadAllText(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz"); //Our game's dir

        //Common file paths
        string pathToWorkingBML;
        string pathToGameBML;
        string pathToGameXML;
        string pathToWorkingXML;

        public BlueprintEditor()
        {
            InitializeComponent();

            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;

            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Set common file paths
            pathToWorkingBML = workingDirectory + "GBL_ITEM.BML";
            pathToGameBML = gameDirectory + @"\DATA\GBL_ITEM.BML";
            pathToGameXML = gameDirectory + @"\DATA\GBL_ITEM.XML";
            pathToWorkingXML = workingDirectory + "GBL_ITEM.xml";

            //Copy correct XML to working directory and fix bug
            StreamWriter updateXmlContents = new StreamWriter(pathToWorkingXML);
            updateXmlContents.WriteLine(File.ReadAllText(pathToGameXML).Replace(" xmlns=\"http://www.w3schools.com\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation=\"http://www.w3schools.com gbl_item.xsd\"", ""));
            updateXmlContents.Close();


            //Load-in XML data
            var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

            //Get playlists
            blueprints.Items.Clear();
            IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//item_database/recipes/recipe");
            foreach (XElement el in elements)
            {
                blueprints.Items.Add(el.Attribute("name").Value.ToString());
                blueprints.Enabled = true;
                btnSelectClass.Enabled = true;
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //load
        private void btnSelectClass_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Save selected blueprint name
            string selectedType = blueprints.Text;

            if (selectedType == "")
            {
                //No blueprint selected, can't load anything
                MessageBox.Show("Please select a blueprint first.");
            }
            else
            {
                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Get all data from type
                IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//item_database/recipes/recipe");
                foreach (XElement el in elements)
                {
                    if (el.Attribute("name").Value.ToString() == selectedType)
                    {
                        //clear
                        craft_itemname.Items.Clear();
                        craft_quantity.Items.Clear();
                        output_itemname.Items.Clear();
                        output_quantity.Items.Clear();

                        //input
                        IEnumerable<XElement> inputItem = el.XPathSelectElements("input/item");
                        foreach (XElement item in inputItem)
                        {
                            craft_itemname.Items.Add(item.Attribute("name").Value.ToString());
                            craft_quantity.Items.Add(item.Attribute("quantity").Value.ToString());
                        }

                        //output
                        IEnumerable<XElement> outputItem = el.XPathSelectElements("output/item");
                        foreach (XElement item in outputItem)
                        {
                            output_itemname.Items.Add(item.Attribute("name").Value.ToString());
                            output_quantity.Items.Add(item.Attribute("quantity").Value.ToString());
                        }
                    }
                }
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }
    }
}
