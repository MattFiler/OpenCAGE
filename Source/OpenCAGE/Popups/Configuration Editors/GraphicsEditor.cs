using CommandsEditor.Popups.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Windows.Input;
using System.Xml.Linq;
using System.Xml.XPath;
using WeifenLuo.WinFormsUI.Docking;

namespace CommandsEditor.ConfigEditors
{
    public partial class GraphicsEditor : BaseWindow
    {
        string _path;
        XDocument _xml;

        public GraphicsEditor() : base()
        {
            InitializeComponent();

            _path = Singleton.PathToAI + @"\DATA\ENGINE_SETTINGS.XML";

            //The GRAPHICS_SETTINGS.XML file commonly distributed in the community has a broken comment, so lets fix it.
            File.WriteAllText(_path, File.ReadAllText(_path).Replace("<!– resolution in pixels. –>", " "));

            _xml = XDocument.Load(_path);

            //Get settings
            IEnumerable<XElement> elements = _xml.XPathSelectElements("//Settings/Setting/Setting/*");
            foreach (XElement el in elements)
            {
                switch (el.Parent.Attribute("name").Value.ToString())
                {
                    case "Field Of View":
                        {
                            ListViewItem item = new ListViewItem(el.Attribute("name").Value);
                            item.SubItems.Add(el.Attribute("float").Value);
                            fovPresets.Items.Add(item);
                            break;
                        }
                    case "Level of Detail":
                        {
                            ListViewItem item = new ListViewItem(el.Attribute("name").Value);
                            item.SubItems.Add(el.Attribute("float").Value);
                            lodPresets.Items.Add(item);
                            break;
                        }
                    case "ShadowMapResolution":
                        {
                            ListViewItem item = new ListViewItem(el.Attribute("name").Value);
                            item.SubItems.Add(el.Attribute("int").Value);
                            shadowMapResolutionPresets.Items.Add(item);
                            break;
                        }
                    case "ShadowMapping":
                        {
                            ListViewItem item = new ListViewItem(el.Attribute("name").Value);
                            item.SubItems.Add(el.Attribute("int").Value);
                            shadowMapFilterQualityPresets.Items.Add(item);
                            break;
                        }
                    case "Stereo Mode":
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

                            break;
                        }
                    case "Planar Reflections":
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

                            break;
                        }
                    case "Full Screen":
                        {
                            foreach (XElement elChild in el.XPathSelectElements("Quality"))
                            {
                                if (elChild.Parent.Attribute("name").Value.ToString() == "Windowed Resolution")
                                {
                                    try
                                    {
                                        ListViewItem item = new ListViewItem(elChild.Attribute("name").Value);
                                        item.SubItems.Add(elChild.Attribute("resolutionX").Value);
                                        item.SubItems.Add(elChild.Attribute("resolutionY").Value);
                                        windowedResolutionPresets.Items.Add(item);
                                    }
                                    catch { }
                                }
                            }

                            break;
                        }
                }
            }

            Singleton.OnResetConfigs += () => { this.Close(); };
        }

        //Remove from list
        private void remove_res_Click(object sender, EventArgs e)
        {
            if (windowedResolutionPresets.SelectedItems.Count != 1)
            {
                MessageBox.Show("Please select a resolution to remove!", "None selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            windowedResolutionPresets.Items.RemoveAt(windowedResolutionPresets.SelectedItems[0].Index);
        }
        private void remove_fov_Click(object sender, EventArgs e)
        {
            if (fovPresets.SelectedItems.Count != 1)
            {
                MessageBox.Show("Please select an FOV preset to remove!", "None selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            fovPresets.Items.RemoveAt(fovPresets.SelectedItems[0].Index);
        }
        private void remove_shadowmap_Click(object sender, EventArgs e)
        {
            if (shadowMapResolutionPresets.SelectedItems.Count != 1)
            {
                MessageBox.Show("Please select a shadow map resolution to remove!", "None selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            shadowMapResolutionPresets.Items.RemoveAt(shadowMapResolutionPresets.SelectedItems[0].Index);
        }
        private void remove_filter_Click(object sender, EventArgs e)
        {
            if (shadowMapFilterQualityPresets.SelectedItems.Count != 1)
            {
                MessageBox.Show("Please select a shadow filter quality to remove!", "None selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            shadowMapFilterQualityPresets.Items.RemoveAt(shadowMapFilterQualityPresets.SelectedItems[0].Index);
        }
        private void remove_lod_Click(object sender, EventArgs e)
        {
            if (lodPresets.SelectedItems.Count != 1)
            {
                MessageBox.Show("Please select a LOD preset to remove!", "None selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            lodPresets.Items.RemoveAt(lodPresets.SelectedItems[0].Index);
        }
        
        //Add to list
        private void add_res_Click(object sender, EventArgs e)
        {
            ShowGraphicsEditorPopup("Resolution Size Name", "Resolution Width", "Resolution Height");
        }
        private void add_fov_Click(object sender, EventArgs e)
        {
            ShowGraphicsEditorPopup("FOV Setting Name", "FOV Value");
        }
        private void add_shadowmap_Click(object sender, EventArgs e)
        {
            ShowGraphicsEditorPopup("Shadowmap Quality Name", "Resolution (px)");
        }
        private void add_filter_Click(object sender, EventArgs e)
        {
            ShowGraphicsEditorPopup("Shadow Filter Name", "PCF Kernel");
        }
        private void add_lod_Click(object sender, EventArgs e)
        {
            ShowGraphicsEditorPopup("LOD Setting Name", "LOD Value");
        }
        private void ShowGraphicsEditorPopup(string val1, string val2, string val3 = "")
        {
            GraphicsEditorPopup editorPopup = new GraphicsEditorPopup(val1, val2, val3);
            editorPopup.Show();
            editorPopup.OnSaved += HandleEditorPopupSave;
        }
        public void HandleEditorPopupSave(string inputOne, string inputTwo, string inputThree, string titleOne)
        {
            ListViewItem item = new ListViewItem(inputOne);
            item.SubItems.Add(inputTwo);

            switch (titleOne)
            {
                case "Resolution Size Name":
                    item.SubItems.Add(inputThree);
                    windowedResolutionPresets.Items.Insert(0, item);
                    break;
                case "FOV Setting Name":
                    fovPresets.Items.Insert(0, item);
                    break;
                case "Shadowmap Quality Name":
                    shadowMapResolutionPresets.Items.Add(item);
                    break;
                case "Shadow Filter Name":
                    shadowMapFilterQualityPresets.Items.Add(item);
                    break;
                case "LOD Setting Name":
                    lodPresets.Items.Insert(0, item);
                    break;
            }
        }

        //Save all
        private void btnSave_Click_1(object sender, EventArgs e)
        {
            //Save new data - super messy, but it works...
            IEnumerable<XElement> elements5 = _xml.XPathSelectElements("//Settings/Setting/Setting[5]/Setting[2]");
            foreach (XElement el in elements5)
            {
                if (el.Attribute("name").Value.ToString() == "Windowed Resolution")
                {
                    el.RemoveNodes();
                    el.Add(XElement.Parse("<Dependency setting=\"Full Screen\" name=\"Windowed\" />"));

                    for (int i = 0; i < windowedResolutionPresets.Items.Count; i++)
                        el.Add(XElement.Parse("<Quality name=\"" + windowedResolutionPresets.Items[i].SubItems[0].Text + "\" resolutionX=\"" + windowedResolutionPresets.Items[i].SubItems[1].Text + "\" resolutionY=\"" + windowedResolutionPresets.Items[i].SubItems[2].Text + "\" precedence=\"" + (windowedResolutionPresets.Items.Count - i) + "\" />"));
                }
            }
            IEnumerable<XElement> elements6 = _xml.XPathSelectElements("//Settings/Setting/Setting[1]");
            foreach (XElement el in elements6)
            {
                if (el.Attribute("name").Value.ToString() == "Stereo Mode")
                {
                    el.RemoveNodes();
                    el.Add(XElement.Parse("<Quality name=\"Off\" precedence=\"4\" />"));

                    if (checkAnaglph.Checked)
                        el.Add(XElement.Parse("<Quality name=\"Anaglyph Red/Blue\" 	precedence=\"3\"/>"));
                    if (check3D.Checked)
                        el.Add(XElement.Parse("<Quality name=\"3DTV\" 	precedence=\"2\"/>"));
                    if (checkRift.Checked)
                        el.Add(XElement.Parse("<Quality name=\"Rift\" 	precedence=\"1\"/>"));
                }
            }
            IEnumerable<XElement> elements7 = _xml.XPathSelectElements("//Settings/Setting/Setting[13]");
            foreach (XElement el in elements7)
            {
                if (el.Attribute("name").Value.ToString() == "Planar Reflections")
                {
                    el.RemoveNodes();
                    el.Add(XElement.Parse("<Quality name=\"Off\" int=\"0\" precedence=\"1\" />"));
                    el.Add(XElement.Parse("<Quality name=\"On\" int=\"3\" precedence=\"2\" />"));

                    if (checkHighGloss.Checked)
                        el.Add(XElement.Parse("<Quality name=\"High Gloss\"  int=\"4\"		precedence=\"3\"/>"));
                }
            }
            IEnumerable<XElement> elements = _xml.XPathSelectElements("//Settings/Setting/Setting[18]");
            foreach (XElement el in elements)
            {
                if (el.Attribute("name").Value.ToString() == "Field Of View")
                {
                    el.RemoveNodes();
                    for (int i = 0; i < fovPresets.Items.Count; i++)
                        el.Add(XElement.Parse("<Quality name=\"" + fovPresets.Items[i].SubItems[0].Text + "\" float=\"" + fovPresets.Items[i].SubItems[1].Text + "\" precedence=\"" + (fovPresets.Items.Count - i) + "\" />"));
                }
            }
            IEnumerable<XElement> elements2 = _xml.XPathSelectElements("//Settings/Setting[@name='Graphics']/Setting[3]");
            foreach (XElement el in elements2)
            {
                if (el.Attribute("name").Value.ToString() == "Level of Detail")
                {
                    el.RemoveNodes();
                    for (int i = 0; i < lodPresets.Items.Count; i++)
                        el.Add(XElement.Parse("<Quality name=\"" + lodPresets.Items[i].SubItems[0].Text + "\" float=\"" + lodPresets.Items[i].SubItems[1].Text + "\" precedence=\"" + (lodPresets.Items.Count - i) + "\" />"));
                }
            }
            IEnumerable<XElement> elements3 = _xml.XPathSelectElements("//Settings/Setting/Setting[8]");
            foreach (XElement el in elements3)
            {
                if (el.Attribute("name").Value.ToString() == "ShadowMapResolution")
                {
                    el.RemoveNodes();
                    for (int i = 0; i < shadowMapResolutionPresets.Items.Count; i++)
                        el.Add(XElement.Parse("<Quality name=\"" + shadowMapResolutionPresets.Items[i].SubItems[0].Text + "\" int=\"" + shadowMapResolutionPresets.Items[i].SubItems[1].Text + "\" precedence=\"" + (i+1) + "\" />"));
                }
            }
            IEnumerable<XElement> elements4 = _xml.XPathSelectElements("//Settings/Setting/Setting[9]");
            foreach (XElement el in elements4)
            {
                if (el.Attribute("name").Value.ToString() == "ShadowMapping")
                {
                    el.RemoveNodes();
                    for (int i = 0; i < shadowMapFilterQualityPresets.Items.Count; i++)
                        el.Add(XElement.Parse("<Quality name=\"" + shadowMapFilterQualityPresets.Items[i].SubItems[0].Text + "\" int=\"" + shadowMapFilterQualityPresets.Items[i].SubItems[1].Text + "\" precedence=\"" + (i+1) + "\" />"));
                }
            }

            //Save all to XML
            _xml.Save(_path);

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);

            //Done
            MessageBox.Show("Saved new graphics settings.");
            MessageBox.Show("You may need to delete your game's settings file to avoid crashes." + Environment.NewLine + "The game can't load settings that don't exist anymore!", "Warning...", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
