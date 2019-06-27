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

namespace Alien_Isolation_Mod_Tools.Attribute_Editors.MATERIAL_DATA
{
    public partial class MaterialEditor : Form
    {
        //Load shared scripts
        AYZ_AttributeEditors AlienAttribute = new AYZ_AttributeEditors();

        //XML content
        XDocument materialXML;
        IEnumerable<XElement> materialElements;

        public MaterialEditor()
        {
            InitializeComponent();

            this.WindowState = FormWindowState.Minimized;
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        /* Form load */
        private void MaterialEditor_Load(object sender, EventArgs e)
        {
            //Load-in XML data
            materialXML = XDocument.Load(AlienAttribute.loadXML("MATERIALS", @"\DATA\MATERIAL_DATA\", false));
            materialElements = materialXML.XPathSelectElements("//Materials/Material");


            //Add each material element to the GUI
            foreach (XElement material in materialElements)
            {
                materialList.Items.Add(material.Attribute("name").Value);
            }
            
            populateDropdowns();


            DEBUG_RIP("ballisitcs", "visual_effect");
            DEBUG_RIP("ballisitcs", "sound_effect");
            DEBUG_RIP("projectile_vfx", "impact_effect");
            DEBUG_RIP("projectile_vfx", "debris_effect");
            DEBUG_RIP("projectile_vfx", "spark_effect");
            DEBUG_RIP("projectile_vfx", "decal_effect");
        }

        /* Load selected material into editor */
        private void loadSelectedMaterial_Click(object sender, EventArgs e)
        {
            if (materialList.SelectedIndex == -1)
            {
                MessageBox.Show("Select a material from the list to load!", "No material selected.", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            XElement material = materialElements.ElementAt(materialList.SelectedIndex);
            XElement stdMaterial = materialElements.ElementAt(0);

            //General
            Template_Name.Text = material.Element("Template_Name").Value;
            Identifier.Text = material.Element("Identifier").Attribute("name").Value;

            //Physics
            friction.Text = material.Element("physics").Attribute("friction").Value;
            density.Text = material.Element("physics").Attribute("density").Value;
            flammable.Checked = (material.Element("physics").Attribute("flammable").Value == "True");
            collision_type.Text = material.Element("physics").Attribute("collision_type").Value;
            material_type.Text = material.Element("physics").Attribute("material_type").Value;
            mechanic_type.Text = material.Element("physics").Attribute("mechanic_type").Value;
            filter.Text = material.Element("physics").Attribute("filter").Value;
            sound_group.Text = material.Element("physics").Attribute("sound_group").Value;
            is_hard_surface.Checked = (material.Element("physics").Attribute("is_hard_surface").Value == "True");

            //Ballistics (or "ballisitcs" in the world of Creative Assembly)
            visual_effect.Text = material.Element("ballisitcs").Attribute("visual_effect").Value;
            ballistic_absorption.Text = material.Element("ballisitcs").Attribute("ballistic_absorption").Value;
            richochet.Text = material.Element("ballisitcs").Attribute("richochet").Value;
            shatter_force.Text = material.Element("ballisitcs").Attribute("shatter_force").Value;
            sound_effect.Text = material.Element("ballisitcs").Attribute("sound_effect").Value;

            //Projectile VFX
            loadProjectileInfo(debris_effect);
            loadProjectileInfo(spark_effect);
            loadProjectileInfo(min_linear_debris_velocity);
            loadProjectileInfo(max_linear_debris_velocity);
            loadProjectileInfo(debris_elements_to_emit);
            loadProjectileInfo(decal_effect);
        }

        /* Load projectile info, fall back to standard if blank */
        private void loadProjectileInfo(Control gui_control)
        {
            try { gui_control.Text = materialElements.ElementAt(materialList.SelectedIndex).Element("projectile_vfx").Attribute(gui_control.Name).Value; gui_control.Enabled = true; }
            catch { gui_control.Text = materialElements.ElementAt(0).Element("projectile_vfx").Attribute(gui_control.Name).Value; gui_control.Enabled = false; }
            //If falling back to standard, the input is disabled to force a template edit
        }

        /* Populate dropdowns in the GUI */
        private void populateDropdowns()
        {
            //Populate dropdowns - collision types [complete]
            collision_type.Items.Add("COL_DYNAMIC_KEYFRAMED");
            collision_type.Items.Add("COL_CHARACTER_TRIGGERS");
            collision_type.Items.Add("COL_HIGH_PRIORITY_UI_TEST");
            collision_type.Items.Add("COL_HIGH_PRIORITY_UI");
            collision_type.Items.Add("COL_PLAYER_ONLY");
            collision_type.Items.Add("COL_THROWN");
            collision_type.Items.Add("COL_PLAYER");
            collision_type.Items.Add("COL_SOUND_BARRIER");
            collision_type.Items.Add("COL_SUPPORT_TEST");
            collision_type.Items.Add("COL_MATERIAL_TEST");
            collision_type.Items.Add("COL_UI_TEST");
            collision_type.Items.Add("COL_UI");
            collision_type.Items.Add("COL_GHOSTED");
            collision_type.Items.Add("COL_NOTHING");
            collision_type.Items.Add("COL_SOUND");
            collision_type.Items.Add("COL_WATER");
            collision_type.Items.Add("COL_RAGDOLL");
            collision_type.Items.Add("COL_DYNAMIC_SIMULATED");
            collision_type.Items.Add("COL_PATH_CLOSED");
            collision_type.Items.Add("COL_PATH_OPEN");
            collision_type.Items.Add("COL_BALLISTIC_TEST");
            collision_type.Items.Add("COL_LINE_OF_SIGHT");
            collision_type.Items.Add("COL_BALLISTICS");
            collision_type.Items.Add("COL_DEBRIS");
            collision_type.Items.Add("COL_DYNAMIC_TRANSPARENT");
            collision_type.Items.Add("COL_AGAINST_DYNAMIC_SIMULATED");
            collision_type.Items.Add("COL_REAL_CHAR");
            collision_type.Items.Add("COL_TRANSPARENT");
            collision_type.Items.Add("COL_STANDARD");
            collision_type.Items.Add("COL_CAMERA");
            collision_type.Items.Add("COL_TRIGGERS");
            collision_type.Items.Add("COL_EVERYTHING");

            //Populate dropdowns - mechanic types [complete]
            mechanic_type.Items.Add("MECH_CLIMBABLE");
            mechanic_type.Items.Add("MECH_WALKABLE");
            mechanic_type.Items.Add("MECH_PRESSURISED");
            mechanic_type.Items.Add("MECH_NONE");

            //Populate dropdowns - material types [complete]
            material_type.Items.Add("MAT_STONE");
            material_type.Items.Add("MAT_EARTH");
            material_type.Items.Add("MAT_WOOD");
            material_type.Items.Add("MAT_GLASS");
            material_type.Items.Add("MAT_PLASTIC");
            material_type.Items.Add("MAT_METAL");
            material_type.Items.Add("MAT_NONE");

            //Filter - ripped from XML
            filter.Items.Add("FILT_NPC_ONLY");
            filter.Items.Add("FILT_PLAYER_ONLY");
            filter.Items.Add("FILT_NONE");

            //Sound group - ripped from XML
            sound_group.Items.Add("Standard");
            sound_group.Items.Add("CAMERA_ONLY");
            sound_group.Items.Add("COLLISION_ONLY");
            sound_group.Items.Add("TRANSPARENT");
            sound_group.Items.Add("PLAYER_ONLY");
            sound_group.Items.Add("NPC_ONLY");
            sound_group.Items.Add("PATHFINDING_ONLY");
            sound_group.Items.Add("STEAM_PIPE");
            sound_group.Items.Add("METAL_GRATE");
            sound_group.Items.Add("STRUCTURAL_METAL");
            sound_group.Items.Add("PLEXIGLASS");
            sound_group.Items.Add("VINYL_UPHOLSTERY");
            sound_group.Items.Add("DECAL_PLASTIC_SHEETING");
            sound_group.Items.Add("HEAVY_PLASTIC");
            sound_group.Items.Add("VDU_GLASS");
            sound_group.Items.Add("GAS_BOTTLE_EXPLOSION");
            sound_group.Items.Add("FIRE_EXTINGUISHER");
            sound_group.Items.Add("BLOCKING");
            sound_group.Items.Add("DECAL_PAPER");
            sound_group.Items.Add("TECH_IMPACT");
            sound_group.Items.Add("OXYGEN_BOTTLE_EXPLOSION");
            sound_group.Items.Add("METAL");
            sound_group.Items.Add("PLEXIGLASS_MOBILE");
            sound_group.Items.Add("VINYL_UPHOLSTERY_MOBILE");
            sound_group.Items.Add("GAS_BOTTLE_SMALL");
            sound_group.Items.Add("WIRING");
            sound_group.Items.Add("OXYGEN_PIPE");
            sound_group.Items.Add("WATER_PIPE");
            sound_group.Items.Add("LIGHT");
            sound_group.Items.Add("HUMAN_IMPACT");
            sound_group.Items.Add("ANDROID_IMPACT");
            sound_group.Items.Add("ALIEN_IMPACT");
            sound_group.Items.Add("CEREAL");
            sound_group.Items.Add("KETCHUP_BOTTLE");
            sound_group.Items.Add("FRUIT");
            sound_group.Items.Add("ANDROID_NECK_IMPACT");
            sound_group.Items.Add("ANDROID_HEAD_IMPACT");
            sound_group.Items.Add("HUMAN_HEAD_IMPACT");
            sound_group.Items.Add("HUMAN_NECK_IMPACT");
            sound_group.Items.Add("SOUND_ONLY");
            sound_group.Items.Add("METAL_GIRDER");
            sound_group.Items.Add("METAL_PIPE");
            sound_group.Items.Add("HOLLOW_METAL");
            sound_group.Items.Add("RUBBER");
            sound_group.Items.Add("ALIEN_HIVE");
            sound_group.Items.Add("WATER");
            sound_group.Items.Add("DEEP_WATER");
            sound_group.Items.Add("ICE");
            sound_group.Items.Add("METAL_SOLID");
            sound_group.Items.Add("METAL_SOLID_WET");
            sound_group.Items.Add("METAL_CONTAINER");
            sound_group.Items.Add("METAL_PANEL");
            sound_group.Items.Add("METAL_VENT");
            sound_group.Items.Add("METAL_GRATE_WET");
            sound_group.Items.Add("METAL_GRATE_LOOSE");
            sound_group.Items.Add("LV426_ROCK");
            sound_group.Items.Add("LV426_GRAVEL");
            sound_group.Items.Add("LV426_BUILDER_MATERIAL");
            sound_group.Items.Add("PLASTIC_SOLID");
            sound_group.Items.Add("PLASTIC_CONTAINER");
            sound_group.Items.Add("RUBBER_SOLID");
            sound_group.Items.Add("DECAL_GLASS_BROKEN");
            sound_group.Items.Add("DECAL_WATER_PUDDLE");
            sound_group.Items.Add("DECAL_BLOOD");
            sound_group.Items.Add("DECAL_ICE");
            sound_group.Items.Add("GLASS");
            sound_group.Items.Add("WOOD_PANEL");
            sound_group.Items.Add("CARPET");
            sound_group.Items.Add("LINOLEUM");
            sound_group.Items.Add("PROP_CARDBOARD");
            sound_group.Items.Add("PROP_METAL_HOLLOW_LARGE");
            sound_group.Items.Add("PROP_METAL_HOLLOW_SMALL");
            sound_group.Items.Add("PROP_METAL_SOLID_LARGE");
            sound_group.Items.Add("PROP_METAL_SOLID_MEDIUM");
            sound_group.Items.Add("PROP_METAL_SOLID_SMALL");
            sound_group.Items.Add("PROP_PLASTIC_LARGE");
            sound_group.Items.Add("PROP_PLASTIC_SMALL");
            sound_group.Items.Add("PROP_PLEXIGLASS");
            sound_group.Items.Add("PROP_CROW_AXE");
            sound_group.Items.Add("PROP_FLARE");
            sound_group.Items.Add("PROP_IED_EMP");
            sound_group.Items.Add("PROP_IED_NOISEMAKER");
            sound_group.Items.Add("PROP_IED_SMOKE_BOMB");
            sound_group.Items.Add("PROP_IED_FLASHBANG");
            sound_group.Items.Add("PROP_IED_MOLOTOV");
            sound_group.Items.Add("PROP_IED_PIPE_BOMB");
            sound_group.Items.Add("PROP_ROCK_LARGE");
            sound_group.Items.Add("PROP_ROCK_MEDIUM");
            sound_group.Items.Add("PROP_ROCK_SMALL");
            sound_group.Items.Add("LV426_CORRIDOR_MATERIAL");
            sound_group.Items.Add("PROP_PLASTIC_MEDIUM");
            sound_group.Items.Add("PROP_LOCKERDOOR");
            sound_group.Items.Add("PROP_GUITAR");
            sound_group.Items.Add("PROP_BOOK");
            sound_group.Items.Add("PROP_SANDALS");
            sound_group.Items.Add("PROP_SHOES");
            sound_group.Items.Add("PROP_GASCANISTER_SMALL");
            sound_group.Items.Add("PROP_GASCANISTER_LARGE");
            sound_group.Items.Add("PROP_BAG");
            sound_group.Items.Add("PROP_RUBBERFLAPS");
            sound_group.Items.Add("PROP_PLASTICTOY");
            sound_group.Items.Add("PROP_SKELETON");
            sound_group.Items.Add("PROP_BEERCAN");
            sound_group.Items.Add("PROP_SNACKBAR");

            //Sound effect - ripped from XML
            sound_effect.Items.Add("");
            sound_effect.Items.Add("steam_hiss");
            sound_effect.Items.Add("metal_hit");
            sound_effect.Items.Add("Play_SDX_BulletFleshyImpact");
            sound_effect.Items.Add("Play_PHD_BULLLET_HIT_ANDROID");
            sound_effect.Items.Add("LINOLEUM");

            //Visual effect - ripped from XML
            visual_effect.Items.Add("");
            visual_effect.Items.Add("steam_blast");
            visual_effect.Items.Add("spark");
            visual_effect.Items.Add("steam");

            //Debris effect - ripped from XML
            debris_effect.Items.Add("");
            debris_effect.Items.Add("Impact_Bolt_Metal_Debris");
            debris_effect.Items.Add("Impact_Bolt_Plastic_debris");
            debris_effect.Items.Add("Impact_Bolt_Plastic_Debris");
            debris_effect.Items.Add("Impact_Bolt_Paper_Debris");

            //Decal effect - ripped from XML
            decal_effect.Items.Add("");

            //Impact effect - ripped from XML
            impact_effect.Items.Add("");
            impact_effect.Items.Add("Impact_Bolt_Metal_Smoke");
            impact_effect.Items.Add("BoltGun_Bolt");
            impact_effect.Items.Add("Gas_bottle_gas_jet");
            impact_effect.Items.Add("Impact_Bolt_Metal_smoke");

            //Spark effect - ripped from XML
            spark_effect.Items.Add("");
            spark_effect.Items.Add("Impact_Bolt_Metal");
            spark_effect.Items.Add("Impact_Bolt_Vinyl");
            spark_effect.Items.Add("Impact_Bolt_Plastic");
            spark_effect.Items.Add("Impact_gas_bottle");
            spark_effect.Items.Add("Impact_fire_extinguisher");
            spark_effect.Items.Add("Impact_Bolt_Paper");
            spark_effect.Items.Add("Impact_Bolt_tech");
        }

        /* DEBUG RIP */
        private void DEBUG_RIP(string element, string attribute, bool prefix = true)
        {
            List<string> rip = new List<string>();
            foreach (XElement material in materialElements)
            {
                try
                {
                    string to_rip = material.Element(element).Attribute(attribute).Value;
                    if (prefix)
                    {
                        to_rip = attribute + ".Items.Add(\"" + to_rip + "\");";
                    }
                    if (!rip.Contains(to_rip))
                    {
                        rip.Add(to_rip);
                    }
                }
                catch {}
            }
            File.WriteAllLines("rip_" + element + "_" + attribute + ".txt", rip);
        }
        /* ****** */
    }
}
