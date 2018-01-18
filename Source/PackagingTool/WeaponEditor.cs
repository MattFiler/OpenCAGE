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

namespace PackagingTool
{
    public partial class WeaponEditor : Form
    {
        //Main Directories
        string workingDirectory = Directory.GetCurrentDirectory() + @"\Attribute Editor Directory\"; //Our working dir
        string gameDirectory = File.ReadAllText(Directory.GetCurrentDirectory() + @"\packagingtool_locales.ayz"); //Our game's dir

        //Common file paths
        string pathToWorkingBML;
        string pathToGameBML;
        string pathToWorkingXML;

        //On Load
        public WeaponEditor()
        {
            InitializeComponent();

            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;

            //Base Ammo Values
            Template_Name.Enabled = false;

            //Hand_Weapon_Data Values
            Projectile.Enabled = false;
            Flamethrower.Enabled = false;
            damage_rays_per_shot.Enabled = false;
            damage_rays_blocked_by_characters.Enabled = false;
            use_fixed_accuracy.Enabled = false;
            fixed_accuracy.Enabled = false;
            npc_accuracy_multiplier.Enabled = false;
            min_accuracy_radius_at_10_metres.Enabled = false;
            max_accuracy_radius_at_10_metres.Enabled = false;
            is_fuel.Enabled = false;
            fuel_units_consumed_per_second_if_firing.Enabled = false;
            fuel_units_consumed_per_second_if_switched_on.Enabled = false;
            projectile_units_consumed_per_shot.Enabled = false;

            //damage_ranges Values
            min_distance.Enabled = false;
            damageRanges.Enabled = false;
            loadRange.Enabled = false;

            //Physics_response_at_impact_point Values
            has_physics_response.Enabled = false;
            impulse_radius.Enabled = false;
            impulse_at_centre_of_blast.Enabled = false;
            impulse_fall_off_power.Enabled = false;
            character_wavefront_speed.Enabled = false;

            //Range Values
            vs_NPC.Enabled = false;
            vsPlayer.Enabled = false;
            vsAndroid.Enabled = false;
            vsAndroidHeavy.Enabled = false;
            vsFHugger.Enabled = false;
            vsPhysics.Enabled = false;
            headshot.Enabled = false;
            Damage_1.Enabled = false;
            Damage_2.Enabled = false;
            Damage_3.Enabled = false;
            Ragdoll.Enabled = false;
            vsAlien.Enabled = false;
            AlienStun.Enabled = false;
            StunDuration.Enabled = false;
            EMPDuration.Enabled = false;
            BlindDuration.Enabled = false;
            saveRange.Enabled = false;
        }

        //Load Ammo Type
        private void btnSelectClass_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Save selected ammo name
            string selectedClass = classSelection.Text;

            if (selectedClass == "")
            {
                //No ammo selected, can't load anything
                MessageBox.Show("Please select an ammo type first.");
            }
            else
            {
                //Set common file paths
                pathToWorkingBML = workingDirectory + selectedClass + ".BML";
                pathToGameBML = gameDirectory + @"\DATA\WEAPON_INFO\AMMO\" + selectedClass + ".BML";
                pathToWorkingXML = workingDirectory + selectedClass + ".xml";

                //Copy correct BML to working directory
                File.Copy(pathToGameBML, pathToWorkingBML);

                //Convert BML to XML
                new AlienConverter(pathToWorkingBML, pathToWorkingXML).Run();

                //Delete BML
                File.Delete(pathToWorkingBML);


                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Set base Ammo Values
                loadAttributeValue("Ammo", "Template_Name", ChrAttributeXML, null, Template_Name);

                //Set Hand_Weapon_Data Values
                loadAttributeValue("Hand_Weapon_Data", "Projectile", ChrAttributeXML, null, Projectile);
                loadAttributeValue("Hand_Weapon_Data", "Flamethrower", ChrAttributeXML, null, Flamethrower);
                loadAttributeValue("Hand_Weapon_Data", "damage_rays_per_shot", ChrAttributeXML, damage_rays_per_shot, null);
                loadAttributeValue("Hand_Weapon_Data", "damage_rays_blocked_by_characters", ChrAttributeXML, null, damage_rays_blocked_by_characters);
                loadAttributeValue("Hand_Weapon_Data", "use_fixed_accuracy", ChrAttributeXML, null, use_fixed_accuracy);
                loadAttributeValue("Hand_Weapon_Data", "fixed_accuracy", ChrAttributeXML, fixed_accuracy, null);
                loadAttributeValue("Hand_Weapon_Data", "npc_accuracy_multiplier", ChrAttributeXML, npc_accuracy_multiplier, null);
                loadAttributeValue("Hand_Weapon_Data", "min_accuracy_radius_at_10_metres", ChrAttributeXML, min_accuracy_radius_at_10_metres, null);
                loadAttributeValue("Hand_Weapon_Data", "max_accuracy_radius_at_10_metres", ChrAttributeXML, max_accuracy_radius_at_10_metres, null);
                loadAttributeValue("Hand_Weapon_Data", "is_fuel", ChrAttributeXML, null, is_fuel);
                loadAttributeValue("Hand_Weapon_Data", "fuel_units_consumed_per_second_if_firing", ChrAttributeXML, fuel_units_consumed_per_second_if_firing, null);
                loadAttributeValue("Hand_Weapon_Data", "fuel_units_consumed_per_second_if_switched_on", ChrAttributeXML, fuel_units_consumed_per_second_if_switched_on, null);
                loadAttributeValue("Hand_Weapon_Data", "projectile_units_consumed_per_shot", ChrAttributeXML, projectile_units_consumed_per_shot, null);

                //Set damage_ranges Values
                loadAttributeValue("damage_ranges", "min_distance", ChrAttributeXML, min_distance, null);

                //Set Physics_response_at_impact_point Values
                loadAttributeValue("Physics_response_at_impact_point", "has_physics_response", ChrAttributeXML, null, has_physics_response);
                loadAttributeValue("Physics_response_at_impact_point", "impulse_radius", ChrAttributeXML, impulse_radius, null);
                loadAttributeValue("Physics_response_at_impact_point", "impulse_at_centre_of_blast", ChrAttributeXML, impulse_at_centre_of_blast, null);
                loadAttributeValue("Physics_response_at_impact_point", "impulse_fall_off_power", ChrAttributeXML, impulse_fall_off_power, null);
                loadAttributeValue("Physics_response_at_impact_point", "character_wavefront_speed", ChrAttributeXML, character_wavefront_speed, null);

                //Get ranges
                damageRanges.Items.Clear();
                IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//damage_ranges/range_damage_list/range_damage");
                foreach (XElement el in elements)
                {
                    damageRanges.Items.Add(el.Attribute("range").Value.ToString());
                    damageRanges.Enabled = true;
                }

                //Enable range loader
                loadRange.Enabled = true;

                //Clear existing range values in form
                vs_NPC.Text = "";
                vsPlayer.Text = "";
                vsAndroid.Text = "";
                vsAndroidHeavy.Text = "";
                vsFHugger.Text = "";
                vsPhysics.Text = "";
                headshot.Text = "";
                Damage_1.SelectedIndex = -1;
                Damage_2.SelectedIndex = -1;
                Damage_3.SelectedIndex = -1;
                Ragdoll.Text = "";
                vsAlien.Text = "";
                AlienStun.Text = "";
                StunDuration.Text = "";
                EMPDuration.Text = "";
                BlindDuration.Text = "";
                vs_NPC.Enabled = false;
                vsPlayer.Enabled = false;
                vsAndroid.Enabled = false;
                vsAndroidHeavy.Enabled = false;
                vsFHugger.Enabled = false;
                vsPhysics.Enabled = false;
                headshot.Enabled = false;
                Damage_1.Enabled = false;
                Damage_2.Enabled = false;
                Damage_3.Enabled = false;
                Ragdoll.Enabled = false;
                vsAlien.Enabled = false;
                AlienStun.Enabled = false;
                StunDuration.Enabled = false;
                EMPDuration.Enabled = false;
                BlindDuration.Enabled = false;
                saveRange.Enabled = false;
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Load selected range into the form
        private void loadRange_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            if (pathToWorkingXML != null && damageRanges.Text != "")
            {
                //Load-in XML data (again)
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Get range data
                IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//damage_ranges/range_damage_list/range_damage");
                foreach (XElement el in elements)
                {
                    //Only set data if selected range
                    if (el.Attribute("range").Value.ToString() == damageRanges.Text)
                    {
                        vs_NPC.Text = el.Attribute("vs_NPC").Value.ToString();
                        vsPlayer.Text = el.Attribute("vsPlayer").Value.ToString();
                        vsAndroid.Text = el.Attribute("vsAndroid").Value.ToString();
                        vsAndroidHeavy.Text = el.Attribute("vsAndroidHeavy").Value.ToString();
                        vsFHugger.Text = el.Attribute("vsFHugger").Value.ToString();
                        vsPhysics.Text = el.Attribute("vsPhysics").Value.ToString();
                        headshot.Text = el.Attribute("headshot").Value.ToString();
                        Damage_1.Text = el.Attribute("Damage_1").Value.ToString();
                        Damage_2.Text = el.Attribute("Damage_2").Value.ToString();
                        Damage_3.Text = el.Attribute("Damage_3").Value.ToString();
                        Ragdoll.Text = el.Attribute("Ragdoll").Value.ToString();
                        vsAlien.Text = el.Attribute("vsAlien").Value.ToString();
                        AlienStun.Text = el.Attribute("AlienStun").Value.ToString();
                        StunDuration.Text = el.Attribute("StunDuration").Value.ToString();
                        EMPDuration.Text = el.Attribute("EMPDuration").Value.ToString();
                        BlindDuration.Text = el.Attribute("BlindDuration").Value.ToString();

                        vs_NPC.Enabled = true;
                        vsPlayer.Enabled = true;
                        vsAndroid.Enabled = true;
                        vsAndroidHeavy.Enabled = true;
                        vsFHugger.Enabled = true;
                        vsPhysics.Enabled = true;
                        headshot.Enabled = true;
                        Damage_1.Enabled = true;
                        Damage_2.Enabled = true;
                        Damage_3.Enabled = true;
                        Ragdoll.Enabled = true;
                        vsAlien.Enabled = true;
                        AlienStun.Enabled = true;
                        StunDuration.Enabled = true;
                        EMPDuration.Enabled = true;
                        BlindDuration.Enabled = true;
                        saveRange.Enabled = true;

                        //Done
                        MessageBox.Show("Loaded data for range " + damageRanges.Text);
                    }
                }
            }
            else
            {
                if (damageRanges.Text == "")
                {
                    //No damage selected, can't load anything
                    MessageBox.Show("Please select a damage range first.");
                }
                else
                {
                    //No ammo selected, can't load anything
                    MessageBox.Show("Please select an ammo type first.");
                }
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Save selected range from form
        private void saveRange_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            if (pathToWorkingXML != null && damageRanges.Text != "")
            {
                //Load-in XML data (again again)
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Save data
                IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//damage_ranges/range_damage_list/range_damage");
                foreach (XElement el in elements)
                {
                    //Only save data if selected range
                    if (el.Attribute("range").Value.ToString() == damageRanges.Text)
                    {
                        //Save to attributes
                        el.Attribute("vs_NPC").Value = vs_NPC.Text;
                        el.Attribute("vsPlayer").Value = vsPlayer.Text;
                        el.Attribute("vsAndroid").Value = vsAndroid.Text;
                        el.Attribute("vsAndroidHeavy").Value = vsAndroidHeavy.Text;
                        el.Attribute("vsFHugger").Value = vsFHugger.Text;
                        el.Attribute("vsPhysics").Value = vsPhysics.Text;
                        el.Attribute("headshot").Value = headshot.Text;
                        el.Attribute("Damage_1").Value = Damage_1.Text;
                        el.Attribute("Damage_2").Value = Damage_2.Text;
                        el.Attribute("Damage_3").Value = Damage_3.Text;
                        el.Attribute("Ragdoll").Value = Ragdoll.Text;
                        el.Attribute("vsAlien").Value = vsAlien.Text;
                        el.Attribute("AlienStun").Value = AlienStun.Text;
                        el.Attribute("StunDuration").Value = StunDuration.Text;
                        el.Attribute("EMPDuration").Value = EMPDuration.Text;
                        el.Attribute("BlindDuration").Value = BlindDuration.Text;

                        //Reset form
                        vs_NPC.Text = "";
                        vsPlayer.Text = "";
                        vsAndroid.Text = "";
                        vsAndroidHeavy.Text = "";
                        vsFHugger.Text = "";
                        vsPhysics.Text = "";
                        headshot.Text = "";
                        Damage_1.SelectedIndex = -1;
                        Damage_2.SelectedIndex = -1;
                        Damage_3.SelectedIndex = -1;
                        Ragdoll.Text = "";
                        vsAlien.Text = "";
                        AlienStun.Text = "";
                        StunDuration.Text = "";
                        EMPDuration.Text = "";
                        BlindDuration.Text = "";
                        vs_NPC.Enabled = false;
                        vsPlayer.Enabled = false;
                        vsAndroid.Enabled = false;
                        vsAndroidHeavy.Enabled = false;
                        vsFHugger.Enabled = false;
                        vsPhysics.Enabled = false;
                        headshot.Enabled = false;
                        Damage_1.Enabled = false;
                        Damage_2.Enabled = false;
                        Damage_3.Enabled = false;
                        Ragdoll.Enabled = false;
                        vsAlien.Enabled = false;
                        AlienStun.Enabled = false;
                        StunDuration.Enabled = false;
                        EMPDuration.Enabled = false;
                        BlindDuration.Enabled = false;
                        saveRange.Enabled = false;

                        //Save all to XML
                        ChrAttributeXML.Save(pathToWorkingXML);

                        //Done
                        MessageBox.Show("Saved data for range " + damageRanges.Text);
                    }
                }
            }
            else
            {
                if (damageRanges.Text == "")
                {
                    //No damage selected, can't load anything
                    MessageBox.Show("Please select a damage range first.");
                }
                else
                {
                    //No ammo selected, can't load anything
                    MessageBox.Show("Please select an ammo type first.");
                }
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Return XML value
        private void loadAttributeValue(string attributeGroup, string specificAttribute, XDocument ChrAttributeXML, TextBox textboxToSet, ComboBox comboboxToSet)
        {
            if (textboxToSet == null)
            {
                try
                {
                    string tempVal = ChrAttributeXML.XPathSelectElement("//" + attributeGroup + "/" + specificAttribute).Value;
                    if (tempVal == "")
                    {
                        comboboxToSet.SelectedIndex = -1;
                        comboboxToSet.Enabled = false;
                    }
                    else
                    {
                        comboboxToSet.Text = tempVal;
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
                try
                {
                    textboxToSet.Text = ChrAttributeXML.XPathSelectElement("//" + attributeGroup + "/" + specificAttribute).Value;
                    textboxToSet.Enabled = true;
                }
                catch
                {
                    textboxToSet.Text = "";
                    textboxToSet.Enabled = false;
                }
            }
        }


        //Set XML value
        private void saveAttributeValue(string attributeGroup, string specificAttribute, XDocument ChrAttributeXML, string newValue)
        {
            try
            {
                ChrAttributeXML.XPathSelectElement("//" + attributeGroup + "/" + specificAttribute).Value = newValue;
            }
            catch
            {
                //Can't save, hopefully because doesnt exist (should be).
            }
        }

        private void WeaponEditor_Load(object sender, EventArgs e)
        {
            //unused
        }
    }
}
