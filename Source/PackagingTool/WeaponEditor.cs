/*
 * 
 * Created by Matt Filer
 * www.mattfiler.co.uk
 * 
 */

using Alien_Isolation_Mod_Tools;
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
        //Load shared scripts
        AYZ_AttributeEditors AlienAttribute = new AYZ_AttributeEditors();
        
        //Common file paths
        string pathToWorkingXML;
        string gameBmlDirectory = @"\DATA\WEAPON_INFO\AMMO\";

        //On Load
        public WeaponEditor()
        {
            InitializeComponent();

            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;
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
                //Load in XML
                pathToWorkingXML = AlienAttribute.loadXML(selectedClass, gameBmlDirectory);

                //Load-in XML data
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Set base Ammo Values
                AlienAttribute.getNode("Ammo", "Template_Name", ChrAttributeXML, null, Template_Name);

                //Set Hand_Weapon_Data Values
                AlienAttribute.getNode("Hand_Weapon_Data", "Projectile", ChrAttributeXML, null, Projectile);
                AlienAttribute.getNode("Hand_Weapon_Data", "Flamethrower", ChrAttributeXML, null, Flamethrower);
                AlienAttribute.getNode("Hand_Weapon_Data", "damage_rays_per_shot", ChrAttributeXML, damage_rays_per_shot, null);
                AlienAttribute.getNode("Hand_Weapon_Data", "damage_rays_blocked_by_characters", ChrAttributeXML, null, damage_rays_blocked_by_characters);
                AlienAttribute.getNode("Hand_Weapon_Data", "use_fixed_accuracy", ChrAttributeXML, null, use_fixed_accuracy);
                AlienAttribute.getNode("Hand_Weapon_Data", "fixed_accuracy", ChrAttributeXML, fixed_accuracy, null);
                AlienAttribute.getNode("Hand_Weapon_Data", "npc_accuracy_multiplier", ChrAttributeXML, npc_accuracy_multiplier, null);
                AlienAttribute.getNode("Hand_Weapon_Data", "min_accuracy_radius_at_10_metres", ChrAttributeXML, min_accuracy_radius_at_10_metres, null);
                AlienAttribute.getNode("Hand_Weapon_Data", "max_accuracy_radius_at_10_metres", ChrAttributeXML, max_accuracy_radius_at_10_metres, null);
                AlienAttribute.getNode("Hand_Weapon_Data", "is_fuel", ChrAttributeXML, null, is_fuel);
                AlienAttribute.getNode("Hand_Weapon_Data", "fuel_units_consumed_per_second_if_firing", ChrAttributeXML, fuel_units_consumed_per_second_if_firing, null);
                AlienAttribute.getNode("Hand_Weapon_Data", "fuel_units_consumed_per_second_if_switched_on", ChrAttributeXML, fuel_units_consumed_per_second_if_switched_on, null);
                AlienAttribute.getNode("Hand_Weapon_Data", "projectile_units_consumed_per_shot", ChrAttributeXML, projectile_units_consumed_per_shot, null);

                //Set damage_ranges Values
                AlienAttribute.getNode("damage_ranges", "min_distance", ChrAttributeXML, min_distance, null);

                //Set Physics_response_at_impact_point Values
                AlienAttribute.getNode("Physics_response_at_impact_point", "has_physics_response", ChrAttributeXML, null, has_physics_response);
                AlienAttribute.getNode("Physics_response_at_impact_point", "impulse_radius", ChrAttributeXML, impulse_radius, null);
                AlienAttribute.getNode("Physics_response_at_impact_point", "impulse_at_centre_of_blast", ChrAttributeXML, impulse_at_centre_of_blast, null);
                AlienAttribute.getNode("Physics_response_at_impact_point", "impulse_fall_off_power", ChrAttributeXML, impulse_fall_off_power, null);
                AlienAttribute.getNode("Physics_response_at_impact_point", "character_wavefront_speed", ChrAttributeXML, character_wavefront_speed, null);

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
                AlienAttribute.disableInput(vs_NPC, null);
                AlienAttribute.disableInput(vsPlayer, null);
                AlienAttribute.disableInput(vsAndroid, null);
                AlienAttribute.disableInput(vsAndroidHeavy, null);
                AlienAttribute.disableInput(vsFHugger, null);
                AlienAttribute.disableInput(vsPhysics, null);
                AlienAttribute.disableInput(headshot, null);
                AlienAttribute.disableInput(null, Damage_1);
                AlienAttribute.disableInput(null, Damage_2);
                AlienAttribute.disableInput(null, Damage_3);
                AlienAttribute.disableInput(Ragdoll, null);
                AlienAttribute.disableInput(vsAlien, null);
                AlienAttribute.disableInput(AlienStun, null);
                AlienAttribute.disableInput(StunDuration, null);
                AlienAttribute.disableInput(EMPDuration, null);
                AlienAttribute.disableInput(BlindDuration, null);
                saveRange.Enabled = false;
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Save ammo type
        private void btnSave_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Selected ammo type
            string ammoType = classSelection.Text;

            if (pathToWorkingXML != null)
            {
                if (saveRange.Enabled == true)
                {
                    //Shouldn't save before range is saved
                    MessageBox.Show("Please save your range configuration first.");
                }
                else
                {
                    //Load-in XML data
                    var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                    //Set base Ammo Values
                    AlienAttribute.setNode("Ammo", "Template_Name", ChrAttributeXML, null, Template_Name);

                    //Set Hand_Weapon_Data Values
                    AlienAttribute.setNode("Hand_Weapon_Data", "Projectile", ChrAttributeXML, null, Projectile);
                    AlienAttribute.setNode("Hand_Weapon_Data", "Flamethrower", ChrAttributeXML, null, Flamethrower);
                    AlienAttribute.setNode("Hand_Weapon_Data", "damage_rays_per_shot", ChrAttributeXML, damage_rays_per_shot, null);
                    AlienAttribute.setNode("Hand_Weapon_Data", "damage_rays_blocked_by_characters", ChrAttributeXML, null, damage_rays_blocked_by_characters);
                    AlienAttribute.setNode("Hand_Weapon_Data", "use_fixed_accuracy", ChrAttributeXML, null, use_fixed_accuracy);
                    AlienAttribute.setNode("Hand_Weapon_Data", "fixed_accuracy", ChrAttributeXML, fixed_accuracy, null);
                    AlienAttribute.setNode("Hand_Weapon_Data", "npc_accuracy_multiplier", ChrAttributeXML, npc_accuracy_multiplier, null);
                    AlienAttribute.setNode("Hand_Weapon_Data", "min_accuracy_radius_at_10_metres", ChrAttributeXML, min_accuracy_radius_at_10_metres, null);
                    AlienAttribute.setNode("Hand_Weapon_Data", "max_accuracy_radius_at_10_metres", ChrAttributeXML, max_accuracy_radius_at_10_metres, null);
                    AlienAttribute.setNode("Hand_Weapon_Data", "is_fuel", ChrAttributeXML, null, is_fuel);
                    AlienAttribute.setNode("Hand_Weapon_Data", "fuel_units_consumed_per_second_if_firing", ChrAttributeXML, fuel_units_consumed_per_second_if_firing, null);
                    AlienAttribute.setNode("Hand_Weapon_Data", "fuel_units_consumed_per_second_if_switched_on", ChrAttributeXML, fuel_units_consumed_per_second_if_switched_on, null);
                    AlienAttribute.setNode("Hand_Weapon_Data", "projectile_units_consumed_per_shot", ChrAttributeXML, projectile_units_consumed_per_shot, null);

                    //Set damage_ranges Values
                    AlienAttribute.setNode("damage_ranges", "min_distance", ChrAttributeXML, min_distance, null);

                    //Set Physics_response_at_impact_point Values
                    AlienAttribute.setNode("Physics_response_at_impact_point", "has_physics_response", ChrAttributeXML, null, has_physics_response);
                    AlienAttribute.setNode("Physics_response_at_impact_point", "impulse_radius", ChrAttributeXML, impulse_radius, null);
                    AlienAttribute.setNode("Physics_response_at_impact_point", "impulse_at_centre_of_blast", ChrAttributeXML, impulse_at_centre_of_blast, null);
                    AlienAttribute.setNode("Physics_response_at_impact_point", "impulse_fall_off_power", ChrAttributeXML, impulse_fall_off_power, null);
                    AlienAttribute.setNode("Physics_response_at_impact_point", "character_wavefront_speed", ChrAttributeXML, character_wavefront_speed, null);
                    
                    //Save values
                    if (AlienAttribute.saveXML(ammoType, gameBmlDirectory, ChrAttributeXML))
                    {
                        MessageBox.Show("Saved new ammo settings.");
                    }
                    else
                    {
                        MessageBox.Show("An error occured while saving.");
                    }
                }
            }
            else
            {
                //No ammo loaded - can't save
                MessageBox.Show("Please load an ammo type first!");
            }

            //Update cursor and finish
            Cursor.Current = Cursors.Default;
        }

        //Load selected range into the form
        private void loadRange_Click(object sender, EventArgs e)
        {
            //Update cursor and begin
            Cursor.Current = Cursors.WaitCursor;

            //Damage ranges
            string damageRange = damageRanges.Text;

            if (pathToWorkingXML != null && damageRange != "")
            {
                //Load-in XML data (again)
                var ChrAttributeXML = XDocument.Load(pathToWorkingXML);

                //Get range data
                IEnumerable<XElement> elements = ChrAttributeXML.XPathSelectElements("//damage_ranges/range_damage_list/range_damage");
                foreach (XElement el in elements)
                {
                    //Only set data if selected range
                    if (el.Attribute("range").Value.ToString() == damageRange)
                    {
                        AlienAttribute.enableInput(vs_NPC, null);
                        AlienAttribute.enableInput(vsPlayer, null);
                        AlienAttribute.enableInput(vsAndroid, null);
                        AlienAttribute.enableInput(vsAndroidHeavy, null);
                        AlienAttribute.enableInput(vsFHugger, null);
                        AlienAttribute.enableInput(vsPhysics, null);
                        AlienAttribute.enableInput(headshot, null);
                        AlienAttribute.enableInput(null, Damage_1);
                        AlienAttribute.enableInput(null, Damage_2);
                        AlienAttribute.enableInput(null, Damage_3);
                        AlienAttribute.enableInput(Ragdoll, null);
                        AlienAttribute.enableInput(vsAlien, null);
                        AlienAttribute.enableInput(AlienStun, null);
                        AlienAttribute.enableInput(StunDuration, null);
                        AlienAttribute.enableInput(EMPDuration, null);
                        AlienAttribute.enableInput(BlindDuration, null);
                        saveRange.Enabled = true;

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

                        //Done
                        MessageBox.Show("Loaded data for range " + damageRange);
                    }
                }
            }
            else
            {
                if (damageRange == "")
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
                        AlienAttribute.disableInput(vs_NPC, null);
                        AlienAttribute.disableInput(vsPlayer, null);
                        AlienAttribute.disableInput(vsAndroid, null);
                        AlienAttribute.disableInput(vsAndroidHeavy, null);
                        AlienAttribute.disableInput(vsFHugger, null);
                        AlienAttribute.disableInput(vsPhysics, null);
                        AlienAttribute.disableInput(headshot, null);
                        AlienAttribute.disableInput(null, Damage_1);
                        AlienAttribute.disableInput(null, Damage_2);
                        AlienAttribute.disableInput(null, Damage_3);
                        AlienAttribute.disableInput(Ragdoll, null);
                        AlienAttribute.disableInput(vsAlien, null);
                        AlienAttribute.disableInput(AlienStun, null);
                        AlienAttribute.disableInput(StunDuration, null);
                        AlienAttribute.disableInput(EMPDuration, null);
                        AlienAttribute.disableInput(BlindDuration, null);
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


        private void WeaponEditor_Load(object sender, EventArgs e)
        {
            //unused
        }
    }
}
