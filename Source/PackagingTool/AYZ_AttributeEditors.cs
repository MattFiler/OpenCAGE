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
                    textboxToSet.Text = nodeValue;
                    if (nodeValue == "")
                    {
                        textboxToSet.Enabled = false;
                    }
                    else
                    {
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
