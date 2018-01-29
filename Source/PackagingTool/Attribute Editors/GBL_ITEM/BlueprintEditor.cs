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
    public partial class BlueprintEditor : Form
    {
        Directories AlienDirectories = new Directories();

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

                        removeInputItem.Enabled = true;
                        removeOutputItem.Enabled = true;
                        addNewItemRequired.Enabled = true;
                        addNewOutputItem.Enabled = true;

                        btnSave.Enabled = true;
                    }
                }
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Add new required item
        private void addNewItemRequired_Click(object sender, EventArgs e)
        {
            BlueprintEditorPopup editorPopup = new BlueprintEditorPopup(1);
            editorPopup.Show();
        }

        //add new output item
        private void button3_Click(object sender, EventArgs e)
        {
            BlueprintEditorPopup editorPopup = new BlueprintEditorPopup(2);
            editorPopup.Show();
        }

        //Get data from other forms and add to listboxes
        public void getDataFromPopup(string NEW_QUANTITY, string NEW_ITEM, int DATA_TYPE)
        {
            if (DATA_TYPE == 1)
            {
                craft_itemname.Items.Add(NEW_ITEM);
                craft_quantity.Items.Add(NEW_QUANTITY);
            }
            else
            {
                output_itemname.Items.Add(NEW_ITEM);
                output_quantity.Items.Add(NEW_QUANTITY);
            }
        }

        //remove data from listbox OUTPUT
        private void removeOutputItem_Click(object sender, EventArgs e)
        {
            try
            {
                output_quantity.Items.RemoveAt(output_itemname.SelectedIndex);
                output_itemname.Items.RemoveAt(output_itemname.SelectedIndex);
            }
            catch
            {
                MessageBox.Show("Please select an output item to remove.");
            }
        }

        //remove data from listbox INPUT
        private void removeInputItem_Click(object sender, EventArgs e)
        {
            try
            {
                craft_quantity.Items.RemoveAt(craft_itemname.SelectedIndex);
                craft_itemname.Items.RemoveAt(craft_itemname.SelectedIndex);
            }
            catch
            {
                MessageBox.Show("Please select an input resource to remove.");
            }
        }

        //Save
        private void btnSave_Click(object sender, EventArgs e)
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
                        //remove original input
                        IEnumerable<XElement> inputItem = el.XPathSelectElements("input");
                        foreach (XElement item in inputItem)
                        {
                            item.Remove();
                        }

                        //remove original output
                        IEnumerable<XElement> outputItem = el.XPathSelectElements("output");
                        foreach (XElement item in outputItem)
                        {
                            item.Remove();
                        }

                        //Compile new input
                        string newRecipeCompiledInput = "<input>";
                        int itemCounter = 0;
                        foreach (string item in craft_itemname.Items)
                        {
                            newRecipeCompiledInput = newRecipeCompiledInput + "<item name=\"" + item + "\" quantity=\"" + craft_quantity.Items[itemCounter] + "\" />";
                            itemCounter++;
                        }
                        newRecipeCompiledInput = newRecipeCompiledInput + "</input>";
                        el.Add(XElement.Parse(newRecipeCompiledInput));

                        //Compile new output
                        string newRecipeCompiledOutput = "<output>";
                        itemCounter = 0;
                        foreach (string item in output_itemname.Items)
                        {
                            newRecipeCompiledOutput = newRecipeCompiledOutput + "<item name=\"" + item + "\" quantity=\"" + output_quantity.Items[itemCounter] + "\" />";
                            itemCounter++;
                        }
                        newRecipeCompiledOutput = newRecipeCompiledOutput + "</output>";
                        el.Add(XElement.Parse(newRecipeCompiledOutput));
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
                MessageBox.Show("Saved new blueprint recipe.");
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }
    }
}
