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
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Alien_Isolation_Mod_Tools.Attribute_Editors.ENGINE_SETTINGS
{
    public partial class GraphicsEditor : Form
    {
        ToolPaths Paths = new ToolPaths();

        //Common file paths
        string pathToGameXML;
        string pathToWorkingXML;

        public GraphicsEditor()
        {
            InitializeComponent();

            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;

            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Set common file paths
            pathToGameXML = Paths.GetPath(ToolPaths.Paths.FOLDER_ALIEN_ISOLATION) + @"\DATA\ENGINE_SETTINGS.XML";
            pathToWorkingXML = Paths.GetPath(ToolPaths.Paths.FOLDER_WORKING_FILES) + "ENGINE_SETTINGS.xml";

            //Copy file for use
            if (File.Exists(pathToWorkingXML))
            {
                File.Delete(pathToWorkingXML);
            }
            File.Copy(pathToGameXML, pathToWorkingXML);

            //Load-in XML data
            var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

            //Get settings
            IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//Settings/Setting/Setting/*");
            foreach (XElement el in elements)
            {
                if (el.Parent.Attribute("name").Value.ToString() == "Field Of View")
                {
                    fieldofview_name.Items.Add(el.Attribute("name").Value.ToString());
                    fieldofview_val.Items.Add(el.Attribute("float").Value.ToString());
                }
                if (el.Parent.Attribute("name").Value.ToString() == "Level of Detail")
                {
                    lod_name.Items.Add(el.Attribute("name").Value.ToString());
                    lod_val.Items.Add(el.Attribute("float").Value.ToString());
                }
                if (el.Parent.Attribute("name").Value.ToString() == "ShadowMapResolution")
                {
                    shadowmap_name.Items.Add(el.Attribute("name").Value.ToString());
                    shadowmap_res.Items.Add(el.Attribute("int").Value.ToString());
                }
                if (el.Parent.Attribute("name").Value.ToString() == "ShadowMapping")
                {
                    shadowfilter_name.Items.Add(el.Attribute("name").Value.ToString());
                    shadowfilter_pcf.Items.Add(el.Attribute("int").Value.ToString());
                }

                if (el.Parent.Attribute("name").Value.ToString() == "Stereo Mode")
                {
                    try
                    {
                        if (el.Attribute("name").Value.ToString() == "Anaglyph Red/Blue")
                        {
                            checkAnaglph.Checked = true;
                        }
                        else
                        {
                            if (!checkAnaglph.Checked)
                                checkAnaglph.Checked = false;
                        }
                    }
                    catch
                    {
                        if (!checkAnaglph.Checked)
                            checkAnaglph.Checked = false;
                    }

                    try
                    {
                        if (el.Attribute("name").Value.ToString() == "3DTV")
                        {
                            check3D.Checked = true;
                        }
                        else
                        {
                            if (!check3D.Checked)
                                check3D.Checked = false;
                        }
                    }
                    catch
                    {
                        if (!check3D.Checked)
                            check3D.Checked = false;
                    }

                    try
                    {
                        if (el.Attribute("name").Value.ToString() == "Rift")
                        {
                            checkRift.Checked = true;
                        }
                        else
                        {
                            if (!checkRift.Checked)
                                checkRift.Checked = false;
                        }
                    }
                    catch
                    {
                        if (!checkRift.Checked)
                            checkRift.Checked = false;
                    }
                }

                if (el.Parent.Attribute("name").Value.ToString() == "Planar Reflections")
                {
                    try
                    {
                        if (el.Attribute("name").Value.ToString() == "High Gloss")
                        {
                            checkHighGloss.Checked = true;
                        }
                        else
                        {
                            if (!checkHighGloss.Checked)
                                checkHighGloss.Checked = false;
                        }
                    }
                    catch
                    {
                        if (!checkHighGloss.Checked)
                            checkHighGloss.Checked = false;
                    }
                }

                if (el.Parent.Attribute("name").Value.ToString() == "Full Screen")
                {
                    foreach (XElement elChild in el.XPathSelectElements("Quality"))
                    {
                        if(elChild.Parent.Attribute("name").Value.ToString() == "Windowed Resolution")
                        {
                            try
                            {
                                windowedres_name.Items.Add(elChild.Attribute("name").Value.ToString());
                                windowedres_x.Items.Add(elChild.Attribute("resolutionX").Value.ToString());
                                windowedres_y.Items.Add(elChild.Attribute("resolutionY").Value.ToString());
                            } catch { }
                        }
                    }
                }
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Get data from popup
        public void getDataFromPopup(string inputOne, string inputTwo, string inputThree, string titleOne)
        {
            switch (titleOne)
            {
                case "Resolution Size Name":
                    windowedres_name.Items.Add(inputOne);
                    windowedres_x.Items.Add(inputTwo);
                    windowedres_y.Items.Add(inputThree);
                    break;
                case "FOV Setting Name":
                    fieldofview_name.Items.Add(inputOne);
                    fieldofview_val.Items.Add(inputTwo);
                    break;
                case "Shadowmap Quality Name":
                    shadowmap_name.Items.Add(inputOne);
                    shadowmap_res.Items.Add(inputTwo);
                    break;
                case "Shadow Filter Name":
                    shadowfilter_name.Items.Add(inputOne);
                    shadowfilter_pcf.Items.Add(inputTwo);
                    break;
                case "LOD Setting Name":
                    lod_name.Items.Add(inputOne);
                    lod_val.Items.Add(inputTwo);
                    break;
            }
        }

        //Remove from list
        private void remove_res_Click(object sender, EventArgs e)
        {
            try
            {
                windowedres_x.Items.RemoveAt(windowedres_name.SelectedIndex);
                windowedres_y.Items.RemoveAt(windowedres_name.SelectedIndex);
                windowedres_name.Items.RemoveAt(windowedres_name.SelectedIndex);
            }
            catch
            {
                MessageBox.Show("Please select a resolution to remove.");
            }
        }
        private void remove_fov_Click(object sender, EventArgs e)
        {
            try
            {
                fieldofview_val.Items.RemoveAt(fieldofview_name.SelectedIndex);
                fieldofview_name.Items.RemoveAt(fieldofview_name.SelectedIndex);
            }
            catch
            {
                MessageBox.Show("Please select an FOV preset to remove.");
            }
        }
        private void remove_shadowmap_Click(object sender, EventArgs e)
        {
            try
            {
                shadowmap_res.Items.RemoveAt(shadowmap_name.SelectedIndex);
                shadowmap_name.Items.RemoveAt(shadowmap_name.SelectedIndex);
            }
            catch
            {
                MessageBox.Show("Please select a shadow map resolution to remove.");
            }
        }
        private void remove_filter_Click(object sender, EventArgs e)
        {
            try
            {
                shadowfilter_pcf.Items.RemoveAt(shadowfilter_name.SelectedIndex);
                shadowfilter_name.Items.RemoveAt(shadowfilter_name.SelectedIndex);
            }
            catch
            {
                MessageBox.Show("Please select a shadow filter quality to remove.");
            }
        }
        private void remove_lod_Click(object sender, EventArgs e)
        {
            try
            {
                lod_val.Items.RemoveAt(lod_name.SelectedIndex);
                lod_name.Items.RemoveAt(lod_name.SelectedIndex);
            }
            catch
            {
                MessageBox.Show("Please select a LOD preset to remove.");
            }
        }
        
        //Add to list
        private void add_res_Click(object sender, EventArgs e)
        {
            GraphicsEditorPopup editorPopup = new GraphicsEditorPopup("Resolution Size Name", "Resolution Width", "Resolution Height");
            editorPopup.Show();
        }
        private void add_fov_Click(object sender, EventArgs e)
        {
            GraphicsEditorPopup editorPopup = new GraphicsEditorPopup("FOV Setting Name", "FOV Value", "");
            editorPopup.Show();
        }
        private void add_shadowmap_Click(object sender, EventArgs e)
        {
            GraphicsEditorPopup editorPopup = new GraphicsEditorPopup("Shadowmap Quality Name", "Resolution (PX)", "");
            editorPopup.Show();
        }
        private void add_filter_Click(object sender, EventArgs e)
        {
            GraphicsEditorPopup editorPopup = new GraphicsEditorPopup("Shadow Filter Name", "PCF Kernel", "");
            editorPopup.Show();
        }
        private void add_lod_Click(object sender, EventArgs e)
        {
            GraphicsEditorPopup editorPopup = new GraphicsEditorPopup("LOD Setting Name", "LOD Value", "");
            editorPopup.Show();
        }

        //Save all
        private void btnSave_Click_1(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Load-in XML data
            var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

            //Save new data - super messy, but it works...
            IEnumerable<XElement> elements5 = ChrAttributeXML.XPathSelectElements("//Settings/Setting/Setting[5]/Setting[2]");
            foreach (XElement el in elements5)
            {
                if (el.Attribute("name").Value.ToString() == "Windowed Resolution")
                {
                    //Remove original settings
                    el.RemoveNodes();
                    el.Add(XElement.Parse("<Dependency setting=\"Full Screen\" name=\"Windowed\" />"));

                    //Compile new settings
                    int itemCounter = 0;
                    foreach (string item in windowedres_name.Items)
                    {
                        itemCounter++;
                        el.Add(XElement.Parse("<Quality name=\"" + windowedres_name.Items[itemCounter - 1].ToString() + "\" resolutionX=\"" + windowedres_x.Items[itemCounter - 1].ToString() + "\" resolutionY=\"" + windowedres_y.Items[itemCounter - 1].ToString() + "\" precedence=\"" + itemCounter + "\" />"));
                    }
                }
            }
            IEnumerable<XElement> elements6 = ChrAttributeXML.XPathSelectElements("//Settings/Setting/Setting[1]");
            foreach (XElement el in elements6)
            {
                if (el.Attribute("name").Value.ToString() == "Stereo Mode")
                {
                    //Remove original settings
                    el.RemoveNodes();
                    el.Add(XElement.Parse("<Quality name=\"Off\" precedence=\"4\" />"));

                    //Add new selected settings
                    if (checkAnaglph.Checked)
                    {
                        el.Add(XElement.Parse("<Quality name=\"Anaglyph Red/Blue\" 	precedence=\"3\"/>"));
                    }
                    if (check3D.Checked)
                    {
                        el.Add(XElement.Parse("<Quality name=\"3DTV\" 	precedence=\"2\"/>"));
                    }
                    if (checkRift.Checked)
                    {
                        el.Add(XElement.Parse("<Quality name=\"Rift\" 	precedence=\"1\"/>"));
                    }
                }
            }
            IEnumerable<XElement> elements7 = ChrAttributeXML.XPathSelectElements("//Settings/Setting/Setting[13]");
            foreach (XElement el in elements7)
            {
                if (el.Attribute("name").Value.ToString() == "Planar Reflections")
                {
                    //Remove original settings
                    el.RemoveNodes();
                    el.Add(XElement.Parse("<Quality name=\"Off\" int=\"0\" precedence=\"1\" />"));
                    el.Add(XElement.Parse("<Quality name=\"On\" int=\"3\" precedence=\"2\" />"));

                    //Add new selected settings
                    if (checkHighGloss.Checked)
                    {
                        el.Add(XElement.Parse("<Quality name=\"High Gloss\"  int=\"4\"		precedence=\"3\"/>"));
                    }
                }
            }
            IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//Settings/Setting/Setting[18]");
            foreach (XElement el in elements)
            {
                if (el.Attribute("name").Value.ToString() == "Field of View")
                {
                    //Remove original settings
                    el.RemoveNodes();

                    //Compile new settings
                    int itemCounter = 0;
                    foreach (string item in fieldofview_name.Items)
                    {
                        itemCounter++;
                        el.Add(XElement.Parse("<Quality name=\"" + fieldofview_name.Items[itemCounter - 1].ToString() + "\" float=\"" + fieldofview_val.Items[itemCounter - 1].ToString() + "\" precedence=\"" + itemCounter + "\" />"));
                    }
                }
            }
            IEnumerable<XElement> elements2 = ChrAttributeXML.XPathSelectElements("//Settings/Setting[@name='Graphics']/Setting[3]");
            foreach (XElement el in elements2)
            {
                if (el.Attribute("name").Value.ToString() == "Level of Detail")
                {
                    //Remove original settings
                    el.RemoveNodes();

                    //Compile new settings
                    int itemCounter = 0;
                    foreach (string item in lod_name.Items)
                    {
                        itemCounter++;
                        el.Add(XElement.Parse("<Quality name=\"" + lod_name.Items[itemCounter - 1].ToString() + "\" float=\"" + lod_val.Items[itemCounter - 1].ToString() + "\" precedence=\"" + itemCounter + "\" />"));
                    }
                }
            }
            IEnumerable<XElement> elements3 = ChrAttributeXML.XPathSelectElements("//Settings/Setting/Setting[8]");
            foreach (XElement el in elements3)
            {
                if (el.Attribute("name").Value.ToString() == "ShadowMapResolution")
                {
                    //Remove original settings
                    el.RemoveNodes();

                    //Compile new settings
                    int itemCounter = 0;
                    foreach (string item in shadowmap_name.Items)
                    {
                        itemCounter++;
                        el.Add(XElement.Parse("<Quality name=\"" + shadowmap_name.Items[itemCounter - 1].ToString() + "\" int=\"" + shadowmap_res.Items[itemCounter - 1].ToString() + "\" precedence=\"" + itemCounter + "\" />"));
                    }
                }
            }
            IEnumerable<XElement> elements4 = ChrAttributeXML.XPathSelectElements("//Settings/Setting/Setting[9]");
            foreach (XElement el in elements4)
            {
                if (el.Attribute("name").Value.ToString() == "ShadowMapping")
                {
                    //Remove original settings
                    el.RemoveNodes();

                    //Compile new settings
                    int itemCounter = 0;
                    foreach (string item in shadowfilter_name.Items)
                    {
                        itemCounter++;
                        el.Add(XElement.Parse("<Quality name=\"" + shadowfilter_name.Items[itemCounter - 1].ToString() + "\" int=\"" + shadowfilter_pcf.Items[itemCounter - 1].ToString() + "\" precedence=\"" + itemCounter + "\" />"));
                    }
                }
            }

            //Save all to XML
            ChrAttributeXML.Save(pathToWorkingXML);

            //Copy new BML to game directory & remove working files
            File.Delete(pathToGameXML);
            File.Copy(pathToWorkingXML, pathToGameXML);

            //Done
            MessageBox.Show("Saved new graphics settings.");
            MessageBox.Show("You may need to delete your game's settings file to avoid crashes." + Environment.NewLine + "The game can't load settings that don't exist anymore!", "Warning...", MessageBoxButtons.OK, MessageBoxIcon.Information);

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            //old?
        }
    }
}
