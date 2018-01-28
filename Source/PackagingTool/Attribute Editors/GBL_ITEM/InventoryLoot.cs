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
    public partial class InventoryLoot : Form
    {
        //Main Directories
        string workingDirectory = Directory.GetCurrentDirectory() + @"\Attribute Editor Directory\"; //Our working dir
        string gameDirectory = File.ReadAllText(Directory.GetCurrentDirectory() + @"\modtools_locales.ayz"); //Our game's dir

        //Common file paths
        string pathToWorkingBML;
        string pathToGameBML;
        string pathToGameXML;
        string pathToWorkingXML;

        //Load type
        string loadedType = "";

        public InventoryLoot()
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

            //Update object lists
            Inv_Ammo.Items.Clear();
            keyframe.Items.Clear();
            target_weapon.Items.Clear();
            thrown_object_name.Items.Clear();
            held_object_name.Items.Clear();
            IEnumerable<XElement> ammos = ChrAttributeXML.XPathSelectElements("//item_database/objects/ammo");
            foreach (XElement el in ammos)
            {
                Inv_Ammo.Items.Add(el.Attribute("name").Value.ToString());
                keyframe.Items.Add(el.Attribute("name").Value.ToString());
                try { held_object_name.Items.Add(el.Attribute("held_object_name").Value.ToString()); } catch { }
                try { thrown_object_name.Items.Add(el.Attribute("thrown_object_name").Value.ToString()); } catch { }
            }
            Inv_IED.Items.Clear();
            IEnumerable<XElement> ieds = ChrAttributeXML.XPathSelectElements("//item_database/objects/ied");
            foreach (XElement el in ieds)
            {
                Inv_IED.Items.Add(el.Attribute("name").Value.ToString());
                keyframe.Items.Add(el.Attribute("name").Value.ToString());
                try { held_object_name.Items.Add(el.Attribute("held_object_name").Value.ToString()); } catch { }
                try { thrown_object_name.Items.Add(el.Attribute("thrown_object_name").Value.ToString()); } catch { }
            }
            Inv_Lights.Items.Clear();
            IEnumerable<XElement> lights = ChrAttributeXML.XPathSelectElements("//item_database/objects/light");
            foreach (XElement el in lights)
            {
                Inv_Lights.Items.Add(el.Attribute("name").Value.ToString());
                keyframe.Items.Add(el.Attribute("name").Value.ToString());
                try { held_object_name.Items.Add(el.Attribute("held_object_name").Value.ToString()); } catch { }
                try { thrown_object_name.Items.Add(el.Attribute("thrown_object_name").Value.ToString()); } catch { }
            }
            Inv_MedKit.Items.Clear();
            IEnumerable<XElement> medkits = ChrAttributeXML.XPathSelectElements("//item_database/objects/medikit");
            foreach (XElement el in medkits)
            {
                Inv_MedKit.Items.Add(el.Attribute("name").Value.ToString());
                keyframe.Items.Add(el.Attribute("name").Value.ToString());
                try { held_object_name.Items.Add(el.Attribute("held_object_name").Value.ToString()); } catch { }
                try { thrown_object_name.Items.Add(el.Attribute("thrown_object_name").Value.ToString()); } catch { }
            }
            Inv_Objects.Items.Clear();
            IEnumerable<XElement> objects = ChrAttributeXML.XPathSelectElements("//item_database/objects/object");
            foreach (XElement el in objects)
            {
                Inv_Objects.Items.Add(el.Attribute("name").Value.ToString());
                keyframe.Items.Add(el.Attribute("name").Value.ToString());
                try { held_object_name.Items.Add(el.Attribute("held_object_name").Value.ToString()); } catch { }
                try { thrown_object_name.Items.Add(el.Attribute("thrown_object_name").Value.ToString()); } catch { }
            }
            Inv_Weapons.Items.Clear();
            IEnumerable<XElement> weapons = ChrAttributeXML.XPathSelectElements("//item_database/objects/weapon");
            foreach (XElement el in weapons)
            {
                Inv_Weapons.Items.Add(el.Attribute("name").Value.ToString());
                keyframe.Items.Add(el.Attribute("name").Value.ToString());
                target_weapon.Items.Add(el.Attribute("name").Value.ToString());
                try { held_object_name.Items.Add(el.Attribute("held_object_name").Value.ToString()); } catch { }
                try { thrown_object_name.Items.Add(el.Attribute("thrown_object_name").Value.ToString()); } catch { }
            }
            special_slot.Items.Clear();
            IEnumerable<XElement> slots = ChrAttributeXML.XPathSelectElements("//item_database/special_slots/slot");
            foreach (XElement el in slots)
            {
                special_slot.Items.Add(el.Attribute("name").Value.ToString());
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        private void edit_objects_Click(object sender, EventArgs e)
        {
            loadItem(Inv_Objects, "object");
        }

        private void edit_weapons_Click(object sender, EventArgs e)
        {
            loadItem(Inv_Weapons, "weapon");
        }

        private void edit_ammo_Click(object sender, EventArgs e)
        {
            loadItem(Inv_Ammo, "ammo");
        }

        private void edit_medikit_Click(object sender, EventArgs e)
        {
            loadItem(Inv_MedKit, "medikit");
        }

        private void edit_ied_Click(object sender, EventArgs e)
        {
            loadItem(Inv_IED, "ied");
        }

        private void edit_light_Click(object sender, EventArgs e)
        {
            loadItem(Inv_Lights, "light");
        }

        //load selected item
        private void loadItem(ListBox listbox, string loadedObject)
        {
            //Load-in XML data
            var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

            //Let everyone know
            loadedType = loadedObject;

            //Get selected item
            IEnumerable<XElement> objects = ChrAttributeXML.XPathSelectElements("//item_database/objects/"+loadedObject);
            foreach (XElement el in objects)
            {
                if (el.Attribute("name").Value.ToString() == listbox.GetItemText(listbox.SelectedItem))
                {
                    setAttributeString("name", el, name, null);
                    setAttributeString("thrown_object_name", el, null, thrown_object_name);
                    setAttributeString("target_weapon", el, null, target_weapon);
                    setAttributeString("ammo_type", el, ammo_type, null);
                    setAttributeString("held_object_name", el, null, held_object_name);
                    setAttributeString("keyframe", el, null, keyframe);
                    setAttributeString("default_quantity", el, default_quantity, null);
                    setAttributeString("stack_limit", el, stack_limit, null);
                    setAttributeString("consume_when", el, null, consume_when);
                    setAttributeString("composite", el, composite, null);
                    setAttributeString("droppable_when_held", el, null, droppable_when_held);
                    setAttributeString("special_slot", el, null, special_slot);
                    setAttributeString("display_ammo_as_percentage", el, null, display_ammo_as_percentage);
                    setAttributeString("vanish_when_collected", el, null, vanish_when_collected);
                    setAttributeString("display_quantity", el, null, display_quantity);
                    setAttributeString("radial_menu_order_index", el, radial_menu_order_index, null);
                    setAttributeString("crafting_resource", el, null, crafting_resource);
                    setAttributeString("localisation_tag", el, localisation_tag, null);
                    setAttributeString("activated_by", el, null, activated_by);
                }
            }
        }

        //return attribute as string (and handle nulls)
        private void setAttributeString(string attributeName, XElement el, TextBox textbox, ComboBox combobox)
        {
            if (textbox == null)
            {
                try
                {
                    combobox.Text = el.Attribute(attributeName).Value.ToString();
                    combobox.Enabled = true;
                }
                catch
                {
                    combobox.SelectedIndex = -1;
                    combobox.Enabled = false;
                }
            }
            else
            {
                try
                {
                    textbox.Text = el.Attribute(attributeName).Value.ToString();
                    textbox.Enabled = true;
                    textbox.ReadOnly = false;
                    if (attributeName == "name")
                    {
                        textbox.ReadOnly = true;
                        textbox.BackColor = Color.White;
                    }
                }
                catch
                {
                    textbox.Text = "";
                    textbox.Enabled = false;
                }
            }
        }

        //Save
        private void btnSave_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            if (name.Text == "")
            {
                //No playlist selected, can't load anything
                MessageBox.Show("Please load an inventory item first.");
            }
            else
            {
                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Get all data from type
                IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//item_database/objects/"+loadedType);
                foreach (XElement el in elements)
                {
                    if (el.Attribute("name").Value.ToString() == name.Text)
                    {
                        try { el.Attribute("thrown_object_name").Value = thrown_object_name.Text; } catch { }
                        try { el.Attribute("target_weapon").Value = target_weapon.Text; } catch { }
                        try { el.Attribute("ammo_type").Value = ammo_type.Text; } catch { }
                        try { el.Attribute("held_object_name").Value = held_object_name.Text; } catch { }
                        try { el.Attribute("keyframe").Value = keyframe.Text; } catch { }
                        try { el.Attribute("default_quantity").Value = default_quantity.Text; } catch { }
                        try { el.Attribute("stack_limit").Value = stack_limit.Text; } catch { }
                        try { el.Attribute("consume_when").Value = consume_when.Text; } catch { }
                        try { el.Attribute("composite").Value = composite.Text; } catch { }
                        try { el.Attribute("droppable_when_held").Value = droppable_when_held.Text; } catch { }
                        try { el.Attribute("special_slot").Value = special_slot.Text; } catch { }
                        try { el.Attribute("display_ammo_as_percentage").Value = display_ammo_as_percentage.Text; } catch { }
                        try { el.Attribute("vanish_when_collected").Value = vanish_when_collected.Text; } catch { }
                        try { el.Attribute("display_quantity").Value = display_quantity.Text; } catch { }
                        try { el.Attribute("radial_menu_order_index").Value = radial_menu_order_index.Text; } catch { }
                        try { el.Attribute("crafting_resource").Value = crafting_resource.Text; } catch { }
                        try { el.Attribute("localisation_tag").Value = localisation_tag.Text; } catch { }
                        try { el.Attribute("activated_by").Value = activated_by.Text; } catch { }
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
                MessageBox.Show("Saved new inventory item configuration.");
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }
    }
}
