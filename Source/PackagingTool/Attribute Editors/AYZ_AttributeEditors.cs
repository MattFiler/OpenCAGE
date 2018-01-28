/*
 * 
 * Created by Matt Filer
 * www.mattfiler.co.uk
 * 
 */
 
using PackagingTool; //Legacy namespace
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Alien_Isolation_Mod_Tools
{
    class AYZ_AttributeEditors
    {
        /*
         * Access Level: Public
         * Description: Convert requested file to XML along with any templates
         * Return value: Working path to parent output XML
        */
        public string loadXML(string fileName, string filePath)
        {
            //Convert first requested file to XML
            convertBMLtoXML(fileName, filePath);
            
            //Attempt to recurse back into templates (only bother trying 4, pretty sure none go further than that)
            /*
            try
            {
                string firstTemplateName = XDocument.Load(Directory.GetCurrentDirectory() + @"\Attribute Editor Directory\" + fileName + ".xml").Root.Element("Template_Name").Value;
                convertBMLtoXML(firstTemplateName, filePath);

                try
                {
                    string secondTemplateName = XDocument.Load(Directory.GetCurrentDirectory() + @"\Attribute Editor Directory\" + firstTemplateName + ".xml").Root.Element("Template_Name").Value;
                    convertBMLtoXML(secondTemplateName, filePath);

                    try
                    {
                        string thirdTemplateName = XDocument.Load(Directory.GetCurrentDirectory() + @"\Attribute Editor Directory\" + secondTemplateName + ".xml").Root.Element("Template_Name").Value;
                        convertBMLtoXML(thirdTemplateName, filePath);

                        try
                        {
                            string fourthTemplateName = XDocument.Load(Directory.GetCurrentDirectory() + @"\Attribute Editor Directory\" + thirdTemplateName + ".xml").Root.Element("Template_Name").Value;
                            convertBMLtoXML(fourthTemplateName, filePath);
                        }
                        catch { }
                    }
                    catch { }
                }
                catch { }
            }
            catch { }
            */

            //Return working XML path (for parent without templates)
            return Directory.GetCurrentDirectory() + @"\Attribute Editor Directory\" + fileName + ".xml";
        }


        /*
         * Access Level: Private
         * Description: Convert the requested BML file to a readable XML
         * Return value: null
        */
        private void convertBMLtoXML(string fileName, string filePath)
        {
            //Main Directories
            string workingDirectory = Directory.GetCurrentDirectory() + @"\Attribute Editor Directory\"; //Our working dir
            string gameDirectory = File.ReadAllText(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz"); //Our game's dir

            //Required filepaths
            string filepath_GameBML = gameDirectory + filePath + fileName + ".BML"; //Game BML file
            string filepath_WorkingBML = workingDirectory + fileName + ".BML"; //Working BML file
            string filepath_WorkingXML = workingDirectory + fileName + ".xml"; //Working XML file

            //Copy correct BML to working directory
            File.Copy(filepath_GameBML, filepath_WorkingBML);

            //Convert BML to XML
            new AlienConverter(filepath_WorkingBML, filepath_WorkingXML).Run();

            //Delete BML
            File.Delete(filepath_WorkingBML);
        }


        /*
         * Access Level: Public
         * Description: Save values and convert the working XML back to BML
         * Return value: True if saved, false if error occured
        */
        public bool saveXML(string fileName, string filePath, XDocument loadedXML)
        {
            try
            {
                //Main Directories
                string workingDirectory = Directory.GetCurrentDirectory() + @"\Attribute Editor Directory\"; //Our working dir
                string gameDirectory = File.ReadAllText(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz"); //Our game's dir

                //Required filepaths
                string filepath_GameBML = gameDirectory + filePath + fileName + ".BML"; //Game BML file
                string filepath_WorkingBML = workingDirectory + fileName + ".BML"; //Working BML file
                string filepath_WorkingXML = workingDirectory + fileName + ".xml"; //Working XML file

                //Save XML values
                loadedXML.Save(filepath_WorkingXML);
                
                //Convert XML to BML
                new AlienConverter(filepath_WorkingXML, filepath_WorkingBML).Run();

                //Copy new BML to game directory (delete original first)
                File.Delete(filepath_GameBML);
                File.Copy(filepath_WorkingBML, filepath_GameBML);
                File.Delete(filepath_WorkingBML);

                //Succeeded
                return true;
            }
            catch
            {
                //An error occured
                return false;
            }
        }


        //---------------------------------------------------------------


        /*
         * Access Level: Public
         * Description: Load a requested attribute of a requested node and set to either combobox or textbox value
         * Return value: void
        */
        public void getNodeAttribute(string nodePath, string nodeAttribute, XDocument loadedXML, TextBox textboxToSet, ComboBox comboboxToSet)
        {
            if (textboxToSet == null)
            {
                //Set combobox value
                try
                {
                    comboboxToSet.Text = loadedXML.XPathSelectElement(nodePath).Attribute(nodeAttribute).Value;
                    comboboxToSet.Enabled = true;
                }
                catch
                {
                    comboboxToSet.SelectedIndex = -1;
                    comboboxToSet.Enabled = false;
                }
            }
            else
            {
                //Set textbox value
                try
                {
                    textboxToSet.Text = loadedXML.XPathSelectElement(nodePath).Attribute(nodeAttribute).Value;
                    textboxToSet.Enabled = true;
                }
                catch
                {
                    textboxToSet.Text = "";
                    textboxToSet.Enabled = false;
                }
            }
        }


        /*
         * Access Level: Public
         * Description: Save the value of a node's attribute with the data from a combobox or textbox if input is enabled
         * Return value: void
        */
        public void setNodeAttribute(string nodePath, string nodeAttribute, XDocument loadedXML, TextBox textboxToSet, ComboBox comboboxToSet)
        {
            if (textboxToSet == null)
            {
                if (comboboxToSet.Enabled == true)
                {
                    try { loadedXML.XPathSelectElement(nodePath).Attribute(nodeAttribute).Value = comboboxToSet.Text; } catch { }
                }
            }
            else
            {
                if (textboxToSet.Enabled == true)
                {
                    try { loadedXML.XPathSelectElement(nodePath).Attribute(nodeAttribute).Value = textboxToSet.Text; } catch { }
                }
            }
        }


        //---------------------------------------------------------------


        /*
         * Access Level: Public
         * Description: Load the value of a requested node and set to either combobox or textbox value
         * Return value: void
        */
        public void getNode(string nodePath, string nodeName, XDocument loadedXML, TextBox textboxToSet, ComboBox comboboxToSet)
        {
            try
            {
                string nodeValue = loadedXML.XPathSelectElement("//" + nodePath + "/" + nodeName).Value;

                if (textboxToSet == null)
                {
                    //Combobox value
                    if (nodeValue == "")
                    {
                        comboboxToSet.SelectedIndex = -1;
                        comboboxToSet.Enabled = false;
                    }
                    else
                    {
                        comboboxToSet.Text = nodeValue;
                        comboboxToSet.Enabled = true;
                    }
                }
                else
                {
                    //Textbox value
                    if (nodeValue == "")
                    {
                        textboxToSet.Text = "";
                        textboxToSet.Enabled = false;
                    }
                    else
                    {
                        textboxToSet.Text = nodeValue;
                        textboxToSet.Enabled = true;
                    }
                }
            }
            catch
            {
                if (textboxToSet == null)
                {
                    //Clear combobox value
                    comboboxToSet.SelectedIndex = -1;
                    comboboxToSet.Enabled = false;
                }
                else
                { 
                    //Clear textbox value
                    textboxToSet.Text = "";
                    textboxToSet.Enabled = false;
                }
            }
        }


        /*
         * Access Level: Public
         * Description: Save the value of a node with the data from a combobox or textbox if input is enabled
         * Return value: void
        */
        public void setNode(string nodePath, string nodeName, XDocument loadedXML, TextBox textboxToSet, ComboBox comboboxToSet)
        {
            if (textboxToSet == null)
            {
                if (comboboxToSet.Enabled == true)
                {
                    try { loadedXML.XPathSelectElement("//" + nodePath + "/" + nodeName).Value = comboboxToSet.Text; } catch { }
                }
            }
            else
            {
                if (textboxToSet.Enabled == true)
                {
                    try { loadedXML.XPathSelectElement("//" + nodePath + "/" + nodeName).Value = textboxToSet.Text; } catch { }
                }
            }
        }


        //---------------------------------------------------------------


        /*
         * Access Level: Public
         * Description: Disables and resets a requested textbox or combobox
         * Return value: void
        */
        public void disableInput(TextBox textboxToSet, ComboBox comboboxToSet)
        {
            if (textboxToSet == null)
            {
                comboboxToSet.Enabled = false;
                comboboxToSet.SelectedIndex = -1;
            }
            else
            {
                textboxToSet.Enabled = false;
                textboxToSet.Text = "";
            }
        }


        /*
         * Access Level: Public
         * Description: Enables and resets a requested textbox or combobox
         * Return value: void
        */
        public void enableInput(TextBox textboxToSet, ComboBox comboboxToSet)
        {
            if (textboxToSet == null)
            {
                comboboxToSet.Enabled = true;
                comboboxToSet.SelectedIndex = -1;
            }
            else
            {
                textboxToSet.Enabled = true;
                textboxToSet.Text = "";
            }
        }
    }
}
