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

namespace Alien_Isolation_Mod_Tools.Attribute_Editors.ENGINE_SETTINGS
{
    public partial class GraphicsEditor : Form
    {
        Directories AlienDirectories = new Directories();

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
            pathToGameXML = AlienDirectories.GameDirectoryRoot() + @"\DATA\ENGINE_SETTINGS.XML";
            pathToWorkingXML = AlienDirectories.ToolWorkingDirectory() + "ENGINE_SETTINGS.xml";

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

        //Save all
        private void btnSave_Click_1(object sender, EventArgs e)
        {

        }


        private void btnSave_Click(object sender, EventArgs e)
        {
            //old?
        }
    }
}
