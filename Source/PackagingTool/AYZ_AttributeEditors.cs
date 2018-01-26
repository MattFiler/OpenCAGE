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
         * Description: Convert the requested character class BML file to a readable XML
         * Return value: Working path to character class XML
        */
        public string convertCharacterBML(string characterClass)
        {
            //Main Directories
            string workingDirectory = Directory.GetCurrentDirectory() + @"\Attribute Editor Directory\"; //Our working dir
            string gameDirectory = File.ReadAllText(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz"); //Our game's dir

            //Required filepaths
            string filepath_GameBML = gameDirectory + @"\DATA\CHR_INFO\ATTRIBUTES\" + characterClass + ".BML"; //Game BML file
            string filepath_WorkingBML = workingDirectory + characterClass + ".BML"; //Working BML file
            string filepath_WorkingXML = workingDirectory + characterClass + ".xml"; //Working XML file

            //Copy correct BML to working directory
            File.Copy(filepath_GameBML, filepath_WorkingBML);

            //Convert BML to XML
            new AlienConverter(filepath_WorkingBML, filepath_WorkingXML).Run();

            //Delete BML
            File.Delete(filepath_WorkingBML);

            //Return working XML path
            return filepath_WorkingXML;
        }


        /*
         * Description: Save values and convert the working XML back to BML
         * Return value: True if saved, false if error occured
        */
        public bool saveCharacterBML(string characterClass, XDocument ChrAttributeXML)
        {
            try
            {
                //Main Directories
                string workingDirectory = Directory.GetCurrentDirectory() + @"\Attribute Editor Directory\"; //Our working dir
                string gameDirectory = File.ReadAllText(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz"); //Our game's dir

                //Required filepaths
                string filepath_GameBML = gameDirectory + @"\DATA\CHR_INFO\ATTRIBUTES\" + characterClass + ".BML"; //Game BML file
                string filepath_WorkingBML = workingDirectory + characterClass + ".BML"; //Working BML file
                string filepath_WorkingXML = workingDirectory + characterClass + ".xml"; //Working XML file

                //Save XML values
                ChrAttributeXML.Save(filepath_WorkingXML);
                
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
         * Description: Load a requested attribute of a requested node and set to either combobox or textbox value
         * Return value: void
        */
        public void getNodeAttribute(string nodePath, string nodeAttribute, XDocument ChrAttributeXML, TextBox textboxToSet, ComboBox comboboxToSet)
        {
            if (textboxToSet == null)
            {
                //Set combobox value
                try
                {
                    comboboxToSet.Text = ChrAttributeXML.XPathSelectElement(nodePath).Attribute(nodeAttribute).Value;
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
                    textboxToSet.Text = ChrAttributeXML.XPathSelectElement(nodePath).Attribute(nodeAttribute).Value;
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
         * Description: Save the value of a node's attribute with the data from a combobox or textbox if input is enabled
         * Return value: void
        */
        public void setNodeAttribute(string nodePath, string nodeAttribute, XDocument ChrAttributeXML, TextBox textboxToSet, ComboBox comboboxToSet)
        {
            if (textboxToSet == null)
            {
                if (comboboxToSet.Enabled == true)
                {
                    try { ChrAttributeXML.XPathSelectElement(nodePath).Attribute(nodeAttribute).Value = comboboxToSet.Text; } catch { }
                }
            }
            else
            {
                if (textboxToSet.Enabled == true)
                {
                    try { ChrAttributeXML.XPathSelectElement(nodePath).Attribute(nodeAttribute).Value = textboxToSet.Text; } catch { }
                }
            }
        }


        //---------------------------------------------------------------


        /*
         * Description: Load the value of a requested node and set to either combobox or textbox value
         * Return value: void
        */
        public void getNode(string nodePath, string nodeName, XDocument ChrAttributeXML, TextBox textboxToSet, ComboBox comboboxToSet)
        {
            if (textboxToSet == null)
            {
                //Set combobox value
                try
                {
                    string nodeValue = ChrAttributeXML.XPathSelectElement("//" + nodePath + "/" + nodeName).Value;
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
                    string nodeValue = ChrAttributeXML.XPathSelectElement("//" + nodePath + "/" + nodeName).Value;
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
                catch
                {
                    textboxToSet.Text = "";
                    textboxToSet.Enabled = false;
                }
            }
        }


        /*
         * Description: Save the value of a node with the data from a combobox or textbox if input is enabled
         * Return value: void
        */
        public void setNode(string nodePath, string nodeName, XDocument ChrAttributeXML, TextBox textboxToSet, ComboBox comboboxToSet)
        {
            if (textboxToSet == null)
            {
                if (comboboxToSet.Enabled == true)
                {
                    try { ChrAttributeXML.XPathSelectElement("//" + nodePath + "/" + nodeName).Value = comboboxToSet.Text; } catch { }
                }
            }
            else
            {
                if (textboxToSet.Enabled == true)
                {
                    try { ChrAttributeXML.XPathSelectElement("//" + nodePath + "/" + nodeName).Value = textboxToSet.Text; } catch { }
                }
            }
        }


        //---------------------------------------------------------------


        /*
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
