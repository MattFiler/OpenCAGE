using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Input;
using System.Xml.Linq;
using System.Xml.XPath;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.ConfigEditors
{
    public partial class GraphicsEditor : BaseWindow
    {
        string _path;
        XDocument _xml;

        public GraphicsEditor() : base()
        {
            InitializeComponent();
            OpenCAGE.Popups.Base.BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/configs/graphics-settings/");

            _path = Singleton.PathToAI + @"\DATA\ENGINE_SETTINGS.XML";

            //The GRAPHICS_SETTINGS.XML file commonly distributed in the community has a broken comment, so lets fix it.
            //Only that file is written: any other is left exactly as it is just for being opened
            string text = File.ReadAllText(_path);
            if (text.Contains("<!– resolution in pixels. –>"))
                File.WriteAllText(_path, text.Replace("<!– resolution in pixels. –>", " "));

            _xml = XDocument.Load(_path);

            //Get settings (each preset's list entry keeps its Quality element as its Tag, so a save updates that element where it is)
            IEnumerable<XElement> elements = _xml.XPathSelectElements("//Settings/Setting/Setting/*");
            foreach (XElement el in elements)
            {
                switch (el.Parent.Attribute("name").Value.ToString())
                {
                    case "Field Of View":
                        {
                            ListViewItem item = new ListViewItem(el.Attribute("name").Value) { Tag = el };
                            item.SubItems.Add(el.Attribute("float").Value);
                            fovPresets.Items.Add(item);
                            break;
                        }
                    case "Level of Detail":
                        {
                            ListViewItem item = new ListViewItem(el.Attribute("name").Value) { Tag = el };
                            item.SubItems.Add(el.Attribute("float").Value);
                            lodPresets.Items.Add(item);
                            break;
                        }
                    case "ShadowMapResolution":
                        {
                            ListViewItem item = new ListViewItem(el.Attribute("name").Value) { Tag = el };
                            item.SubItems.Add(el.Attribute("int").Value);
                            shadowMapResolutionPresets.Items.Add(item);
                            break;
                        }
                    case "ShadowMapping":
                        {
                            ListViewItem item = new ListViewItem(el.Attribute("name").Value) { Tag = el };
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
                                        ListViewItem item = new ListViewItem(elChild.Attribute("name").Value) { Tag = elChild };
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

            CloseOnConfigReset();
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
            //The numbers go into the file: refuse one the game couldn't read, and list it as the file spells numbers ("1,5" -> "1.5")
            bool whole = titleOne != "FOV Setting Name" && titleOne != "LOD Setting Name";
            if (!TryNumber(inputTwo, whole, out string numberTwo) || (titleOne == "Resolution Size Name" && !TryNumber(inputThree, true, out inputThree)))
            {
                MessageBox.Show("\"" + inputOne + "\" wasn't added: its " + (whole ? "values must be whole numbers." : "value must be a number."), "Not a number", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ListViewItem item = new ListViewItem(inputOne);
            item.SubItems.Add(numberTwo);

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
            //Nothing is changed unless every new number reads as one
            string problem = ApplyToDocument();
            if (problem != null)
            {
                MessageBox.Show(problem + Environment.NewLine + "Nothing was saved.", "Can't save", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            //Save all to XML
            try
            {
                _xml.Save(_path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to save ENGINE_SETTINGS.XML." + Environment.NewLine + Environment.NewLine + ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);

            //Done
            MessageBox.Show("Saved new graphics settings.");
            MessageBox.Show("You may need to delete your game's settings file to avoid crashes." + Environment.NewLine + "The game can't load settings that don't exist anymore!", "Warning...", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /* Puts the lists and ticks into the loaded file. Presets are updated where they are rather than rebuilt: one the list still
           has keeps its place, its comments and its children (the DefaultRequirements the game uses to pick a new profile's quality
           from the PC's hardware), and only its precedence is set. Presets taken off a list are removed, and new ones go in beside
           their neighbours in the list. Every new number is checked first, so a bad one leaves the file as it was.
           Returns what is wrong, or null. */
        private string ApplyToDocument()
        {
            XElement graphics = Section(_xml.Root, "Graphics");
            //Precedence counts down most lists and up the shadow ones, as the stock file has them
            var lists = new[]
            {
                new { Setting = Section(Section(graphics, "Full Screen"), "Windowed Resolution"), Presets = windowedResolutionPresets, Values = new[] { "resolutionX", "resolutionY" }, Whole = true, CountDown = true },
                new { Setting = Section(graphics, "Field Of View"), Presets = fovPresets, Values = new[] { "float" }, Whole = false, CountDown = true },
                new { Setting = Section(graphics, "Level of Detail"), Presets = lodPresets, Values = new[] { "float" }, Whole = false, CountDown = true },
                new { Setting = Section(graphics, "ShadowMapResolution"), Presets = shadowMapResolutionPresets, Values = new[] { "int" }, Whole = true, CountDown = false },
                new { Setting = Section(graphics, "ShadowMapping"), Presets = shadowMapFilterQualityPresets, Values = new[] { "int" }, Whole = true, CountDown = false },
            };

            foreach (var list in lists)
            {
                if (list.Setting == null)
                    continue;
                foreach (ListViewItem item in list.Presets.Items)
                {
                    if (item.Tag is XElement)
                        continue; //from the file, and written back as it is
                    for (int i = 0; i < list.Values.Length; i++)
                        if (!TryNumber(item.SubItems[i + 1].Text, list.Whole, out _))
                            return "\"" + item.Text + "\": \"" + item.SubItems[i + 1].Text + "\" isn't a " + (list.Whole ? "whole number." : "number.");
                }
            }

            foreach (var list in lists)
            {
                if (list.Setting != null)
                    SyncPresets(list.Setting, list.Presets, list.Values, list.Whole, list.CountDown);
            }

            XElement stereo = Section(graphics, "Stereo Mode");
            if (stereo != null)
            {
                SetFixedPreset(stereo, "Off", true, new XAttribute("precedence", 4));
                SetFixedPreset(stereo, "Anaglyph Red/Blue", checkAnaglph.Checked, new XAttribute("precedence", 3));
                SetFixedPreset(stereo, "3DTV", check3D.Checked, new XAttribute("precedence", 2));
                SetFixedPreset(stereo, "Rift", checkRift.Checked, new XAttribute("precedence", 1));
            }
            XElement planar = Section(graphics, "Planar Reflections");
            if (planar != null)
            {
                SetFixedPreset(planar, "Off", true, new XAttribute("int", 0), new XAttribute("precedence", 1));
                SetFixedPreset(planar, "On", true, new XAttribute("int", 3), new XAttribute("precedence", 2));
                SetFixedPreset(planar, "High Gloss", checkHighGloss.Checked, new XAttribute("int", 4), new XAttribute("precedence", 3));
            }
            return null;
        }

        private static XElement Section(XElement parent, string name)
        {
            return parent?.Elements("Setting").FirstOrDefault(o => (string)o.Attribute("name") == name);
        }

        //One list's presets into its setting's Quality entries (each list entry from the file has its entry as its Tag)
        private static void SyncPresets(XElement setting, ListView presets, string[] values, bool whole, bool countDown)
        {
            List<XElement> listed = new List<XElement>();
            XElement previous = null;
            for (int i = 0; i < presets.Items.Count; i++)
            {
                ListViewItem item = presets.Items[i];
                string precedence = (countDown ? presets.Items.Count - i : i + 1).ToString(CultureInfo.InvariantCulture);
                XElement quality = item.Tag as XElement;
                if (quality == null || quality.Parent != setting)
                {
                    quality = new XElement("Quality", new XAttribute("name", item.Text));
                    for (int v = 0; v < values.Length; v++)
                    {
                        TryNumber(item.SubItems[v + 1].Text, whole, out string number);
                        quality.Add(new XAttribute(values[v], number));
                    }
                    quality.Add(new XAttribute("precedence", precedence));

                    if (previous != null)
                        previous.AddAfterSelf(quality);
                    else if (setting.Elements("Quality").FirstOrDefault() is XElement first)
                        first.AddBeforeSelf(quality);
                    else
                        setting.Add(quality);
                    item.Tag = quality;
                }
                else if ((string)quality.Attribute("precedence") != precedence)
                    quality.SetAttributeValue("precedence", precedence);

                listed.Add(quality);
                previous = quality;
            }

            //The presets taken off the list
            foreach (XElement quality in setting.Elements("Quality").Where(o => !listed.Contains(o)).ToList())
                quality.Remove();
        }

        //A preset a tick box stands for: added when ticked and not there, removed when not ticked; one already there stays as it is
        private static void SetFixedPreset(XElement setting, string name, bool wanted, params XAttribute[] attributes)
        {
            List<XElement> found = setting.Elements("Quality").Where(o => (string)o.Attribute("name") == name).ToList();
            if (!wanted)
                found.ForEach(o => o.Remove());
            else if (found.Count == 0)
                setting.Add(new XElement("Quality", new XAttribute("name", name), attributes));
        }

        //A number as the file spells numbers: a point before any decimals whatever the PC's language ("1,5" typed on a German
        //Windows is 1.5), and nothing that isn't a number at all
        private static bool TryNumber(string text, bool whole, out string number)
        {
            number = null;
            string trimmed = (text ?? "").Trim();
            if (whole)
            {
                if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                    return false;
                number = value.ToString(CultureInfo.InvariantCulture);
                return true;
            }
            if (!decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal fraction)
                && !(Regex.IsMatch(trimmed, @"^[-+]?\d+,\d+$") && decimal.TryParse(trimmed.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out fraction)))
                return false;
            number = fraction.ToString(CultureInfo.InvariantCulture);
            return true;
        }
    }
}
