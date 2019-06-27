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
        //Load shared scripts
        Directories AlienDirectories = new Directories();

        /*
         * Access Level: Public
         * Description: Convert requested file to XML along with any templates
         * Return value: Working path to parent output XML
        */
        public string loadXML(string fileName, string filePath, bool shouldRecurse = true)
        {
            //Convert first requested file to XML
            convertBMLtoXML(fileName, filePath);

            //Exit early if there's no motivation to recurse
            if (!shouldRecurse)
            {
                return AlienDirectories.ToolWorkingDirectory() + fileName + ".xml";
            }

            //Attempt to recurse back into templates (only bother trying 5, pretty sure none go further than that)
            try
            {
                string firstTemplateName = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + fileName + ".xml").Root.Element("Template_Name").Value;
                convertBMLtoXML(firstTemplateName, filePath);

                try
                {
                    string secondTemplateName = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + firstTemplateName + ".xml").Root.Element("Template_Name").Value;
                    convertBMLtoXML(secondTemplateName, filePath);

                    try
                    {
                        string thirdTemplateName = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + secondTemplateName + ".xml").Root.Element("Template_Name").Value;
                        convertBMLtoXML(thirdTemplateName, filePath);

                        try
                        {
                            string fourthTemplateName = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + thirdTemplateName + ".xml").Root.Element("Template_Name").Value;
                            convertBMLtoXML(fourthTemplateName, filePath);

                            try
                            {
                                string fifthTemplateName = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + fourthTemplateName + ".xml").Root.Element("Template_Name").Value;
                                convertBMLtoXML(fifthTemplateName, filePath);
                            }
                            catch { }
                        }
                        catch { }
                    }
                    catch { }
                }
                catch { }
            }
            catch { }

            //Return working XML path (for parent without templates)
            return AlienDirectories.ToolWorkingDirectory() + fileName + ".xml";
        }


        /*
         * Access Level: Private
         * Description: Convert the requested BML file to a readable XML
         * Return value: null
        */
        private void convertBMLtoXML(string fileName, string filePath)
        {
            //Required filepaths
            string filepath_GameBML = AlienDirectories.GameDirectoryRoot() + filePath + fileName + ".BML"; //Game BML file
            string filepath_WorkingBML = AlienDirectories.ToolWorkingDirectory() + fileName + ".BML"; //Working BML file
            string filepath_WorkingXML = AlienDirectories.ToolWorkingDirectory() + fileName + ".xml"; //Working XML file

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
                //Required filepaths
                string filepath_GameBML = AlienDirectories.GameDirectoryRoot() + filePath + fileName + ".BML"; //Game BML file
                string filepath_WorkingBML = AlienDirectories.ToolWorkingDirectory() + fileName + ".BML"; //Working BML file
                string filepath_WorkingXML = AlienDirectories.ToolWorkingDirectory() + fileName + ".xml"; //Working XML file

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
                    string parentValue = getAttributeDataFromParents(nodePath, nodeAttribute, loadedXML);
                    if (parentValue == "")
                    {
                        comboboxToSet.SelectedIndex = -1;
                    }
                    else
                    {
                        comboboxToSet.Text = parentValue;
                    }
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
                    textboxToSet.Text = getAttributeDataFromParents(nodePath, nodeAttribute, loadedXML);
                    textboxToSet.Enabled = false;
                }
            }
        }


        /*
         * Access Level: Private
         * Description: A super shitty way of getting node attribute data from parent files
         * Return value: string
        */
        private string getAttributeDataFromParents(string nodePath, string nodeAttribute, XDocument loadedXML)
        {
            string valueFromXML = "";
            try
            {
                XDocument firstParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + loadedXML.Root.Element("Template_Name").Value + ".xml");
                valueFromXML = firstParent.XPathSelectElement(nodePath).Attribute(nodeAttribute).Value;
                return valueFromXML;
            }
            catch
            {
                try
                {
                    XDocument firstParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + loadedXML.Root.Element("Template_Name").Value + ".xml");
                    XDocument secondParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + firstParent.Root.Element("Template_Name").Value + ".xml");
                    valueFromXML = secondParent.XPathSelectElement(nodePath).Attribute(nodeAttribute).Value;
                    return valueFromXML;
                }
                catch
                {
                    try
                    {
                        XDocument firstParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + loadedXML.Root.Element("Template_Name").Value + ".xml");
                        XDocument secondParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + firstParent.Root.Element("Template_Name").Value + ".xml");
                        XDocument thirdParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + secondParent.Root.Element("Template_Name").Value + ".xml");
                        valueFromXML = thirdParent.XPathSelectElement(nodePath).Attribute(nodeAttribute).Value;
                        return valueFromXML;
                    }
                    catch
                    {
                        try
                        {
                            XDocument firstParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + loadedXML.Root.Element("Template_Name").Value + ".xml");
                            XDocument secondParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + firstParent.Root.Element("Template_Name").Value + ".xml");
                            XDocument thirdParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + secondParent.Root.Element("Template_Name").Value + ".xml");
                            XDocument fourthParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + thirdParent.Root.Element("Template_Name").Value + ".xml");
                            valueFromXML = fourthParent.XPathSelectElement(nodePath).Attribute(nodeAttribute).Value;
                            return valueFromXML;
                        }
                        catch
                        {
                            try
                            {
                                XDocument firstParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + loadedXML.Root.Element("Template_Name").Value + ".xml");
                                XDocument secondParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + firstParent.Root.Element("Template_Name").Value + ".xml");
                                XDocument thirdParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + secondParent.Root.Element("Template_Name").Value + ".xml");
                                XDocument fourthParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + thirdParent.Root.Element("Template_Name").Value + ".xml");
                                XDocument fifthParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + fourthParent.Root.Element("Template_Name").Value + ".xml");
                                valueFromXML = fifthParent.XPathSelectElement(nodePath).Attribute(nodeAttribute).Value;
                                return valueFromXML;
                            }
                            catch { }
                        }
                    }
                }
            }
            return "";
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
                    comboboxToSet.Text = nodeValue;
                    comboboxToSet.Enabled = true;
                    if (comboboxToSet.Name == "Template_Name")
                    {
                        comboboxToSet.Enabled = false;
                    }
                }
                else
                {
                    //Textbox value
                    textboxToSet.Text = nodeValue;
                    textboxToSet.Enabled = true;
                }
            }
            catch
            {
                if (textboxToSet == null)
                {
                    //Clear/get combobox value
                    string parentValue = getNodeDataFromParents(nodePath, nodeName, loadedXML);
                    if (parentValue == "")
                    {
                        comboboxToSet.SelectedIndex = -1;
                    }
                    else
                    {
                        comboboxToSet.Text = parentValue;
                    }
                    comboboxToSet.Enabled = false;
                }
                else
                { 
                    //Clear textbox value
                    textboxToSet.Text = getNodeDataFromParents(nodePath, nodeName, loadedXML);
                    textboxToSet.Enabled = false;
                }
            }
        }


        /*
         * Access Level: Private
         * Description: A super shitty way of getting node data from parent files
         * Return value: string
        */
        private string getNodeDataFromParents(string nodePath, string nodeName, XDocument loadedXML)
        {
            string valueFromXML = "";
            try
            {
                XDocument firstParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + loadedXML.Root.Element("Template_Name").Value + ".xml");
                valueFromXML = firstParent.XPathSelectElement("//" + nodePath + "/" + nodeName).Value;
                return valueFromXML;
            }
            catch
            {
                try
                {
                    XDocument firstParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + loadedXML.Root.Element("Template_Name").Value + ".xml");
                    XDocument secondParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + firstParent.Root.Element("Template_Name").Value + ".xml");
                    valueFromXML = secondParent.XPathSelectElement("//" + nodePath + "/" + nodeName).Value;
                    return valueFromXML;
                }
                catch
                {
                    try
                    {
                        XDocument firstParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + loadedXML.Root.Element("Template_Name").Value + ".xml");
                        XDocument secondParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + firstParent.Root.Element("Template_Name").Value + ".xml");
                        XDocument thirdParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + secondParent.Root.Element("Template_Name").Value + ".xml");
                        valueFromXML = thirdParent.XPathSelectElement("//" + nodePath + "/" + nodeName).Value;
                        return valueFromXML;
                    }
                    catch
                    {
                        try
                        {
                            XDocument firstParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + loadedXML.Root.Element("Template_Name").Value + ".xml");
                            XDocument secondParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + firstParent.Root.Element("Template_Name").Value + ".xml");
                            XDocument thirdParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + secondParent.Root.Element("Template_Name").Value + ".xml");
                            XDocument fourthParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + thirdParent.Root.Element("Template_Name").Value + ".xml");
                            valueFromXML = fourthParent.XPathSelectElement("//" + nodePath + "/" + nodeName).Value;
                            return valueFromXML;
                        }
                        catch
                        {
                            try
                            {
                                XDocument firstParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + loadedXML.Root.Element("Template_Name").Value + ".xml");
                                XDocument secondParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + firstParent.Root.Element("Template_Name").Value + ".xml");
                                XDocument thirdParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + secondParent.Root.Element("Template_Name").Value + ".xml");
                                XDocument fourthParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + thirdParent.Root.Element("Template_Name").Value + ".xml");
                                XDocument fifthParent = XDocument.Load(AlienDirectories.ToolWorkingDirectory() + fourthParent.Root.Element("Template_Name").Value + ".xml");
                                valueFromXML = fifthParent.XPathSelectElement("//" + nodePath + "/" + nodeName).Value;
                                return valueFromXML;
                            }
                            catch { }
                        }
                    }
                }
            }
            return "";
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
