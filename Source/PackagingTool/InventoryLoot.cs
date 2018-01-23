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
            IEnumerable<XElement> ammos = ChrAttributeXML.XPathSelectElements("//item_database/objects/ammo");
            foreach (XElement el in ammos)
            {
                Inv_Ammo.Items.Add(el.Attribute("name").Value.ToString());
            }
            Inv_IED.Items.Clear();
            IEnumerable<XElement> ieds = ChrAttributeXML.XPathSelectElements("//item_database/objects/ied");
            foreach (XElement el in ieds)
            {
                Inv_IED.Items.Add(el.Attribute("name").Value.ToString());
            }
            Inv_Lights.Items.Clear();
            IEnumerable<XElement> lights = ChrAttributeXML.XPathSelectElements("//item_database/objects/light");
            foreach (XElement el in lights)
            {
                Inv_Lights.Items.Add(el.Attribute("name").Value.ToString());
            }
            Inv_MedKit.Items.Clear();
            IEnumerable<XElement> medkits = ChrAttributeXML.XPathSelectElements("//item_database/objects/medikit");
            foreach (XElement el in medkits)
            {
                Inv_MedKit.Items.Add(el.Attribute("name").Value.ToString());
            }
            Inv_Objects.Items.Clear();
            IEnumerable<XElement> objects = ChrAttributeXML.XPathSelectElements("//item_database/objects/object");
            foreach (XElement el in objects)
            {
                Inv_Objects.Items.Add(el.Attribute("name").Value.ToString());
            }
            Inv_Weapons.Items.Clear();
            IEnumerable<XElement> weapons = ChrAttributeXML.XPathSelectElements("//item_database/objects/weapon");
            foreach (XElement el in weapons)
            {
                Inv_Weapons.Items.Add(el.Attribute("name").Value.ToString());
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

            //Get selected item
            IEnumerable<XElement> objects = ChrAttributeXML.XPathSelectElements("//item_database/objects/"+loadedObject);
            foreach (XElement el in objects)
            {
                if (el.Attribute("name").Value.ToString() == listbox.GetItemText(listbox.SelectedItem))
                {
                    testDev.Text = el.ToString();
                }
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {

        }
    }
}
