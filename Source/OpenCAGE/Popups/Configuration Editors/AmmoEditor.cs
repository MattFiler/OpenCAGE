using CATHODE;
using CATHODE.Enums;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.ConfigEditors
{
    public partial class AmmoEditor : BaseWindow
    {
        List<BML> _selectedAmmo;

        public AmmoEditor() : base()
        {
            InitializeComponent();
            ConfigEditorUtils.ExpandNumericRanges(this.Controls);

            Damage_1.BeginUpdate();
            Damage_2.BeginUpdate();
            Damage_3.BeginUpdate();
            foreach (DAMAGE_EFFECT_TYPE_FLAGS flag in Enum.GetValues(typeof(DAMAGE_EFFECT_TYPE_FLAGS)))
            {
                if ((int)flag == -1)
                    continue;

                Damage_1.Items.Add(flag.ToString());
                Damage_2.Items.Add(flag.ToString());
                Damage_3.Items.Add(flag.ToString());
            }
            Damage_1.EndUpdate();
            Damage_2.EndUpdate();
            Damage_3.EndUpdate();

            BML ammoTypes = new BML(Singleton.PathToAI + "\\DATA\\WEAPON_INFO\\AMMO\\AMMOTYPES.BML");
            var ammos = ammoTypes.Content["AmmoTypes"];
            classSelection.BeginUpdate();
            foreach (XmlElement ammo in ammos)
            {
                classSelection.Items.Add(ammo["Name"].InnerText);
            }
            classSelection.EndUpdate();

            this.FormClosing += AmmoEditor_FormClosing;
            Singleton.OnResetConfigs += () => { this.Close(); };
        }

        private void AmmoEditor_Load(object sender, EventArgs e)
        {
            for (int i = 0; i < classSelection.Items.Count; i++)
            {
                classSelection.SelectedIndex = i;
                Save(null, EventArgs.Empty);
            }
            classSelection.SelectedIndex = 0;
        }

        private void AmmoEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(this.Controls, Save);
            this.FormClosing -= AmmoEditor_FormClosing;
        }

        private void classSelection_SelectedIndexChanged(object sender, EventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(this.Controls, Save);

            _selectedAmmo = new List<BML>();
            _selectedAmmo.Add(new BML(Singleton.PathToAI + "\\DATA\\WEAPON_INFO\\AMMO\\" + classSelection.Text + ".BML"));
            while (true)
            {
                string template = _selectedAmmo[_selectedAmmo.Count - 1].Content["Ammo"]["Template_Name"]?.InnerText;
                if (template == null || template == "") break;
                _selectedAmmo.Add(new BML(Singleton.PathToAI + "\\DATA\\WEAPON_INFO\\AMMO\\" + template + ".BML"));
            }

            ConfigEditorUtils.SetCheckbox(_selectedAmmo, Projectile, "Ammo", "Hand_Weapon_Data", "Projectile");
            ConfigEditorUtils.SetCheckbox(_selectedAmmo, Flamethrower, "Ammo", "Hand_Weapon_Data", "Flamethrower");
            ConfigEditorUtils.SetNumber(_selectedAmmo, damage_rays_per_shot, "Ammo", "Hand_Weapon_Data", "damage_rays_per_shot");
            ConfigEditorUtils.SetCheckbox(_selectedAmmo, damage_rays_blocked_by_characters, "Ammo", "Hand_Weapon_Data", "damage_rays_blocked_by_characters");
            ConfigEditorUtils.SetCheckbox(_selectedAmmo, use_fixed_accuracy, "Ammo", "Hand_Weapon_Data", "use_fixed_accuracy");
            ConfigEditorUtils.SetNumber(_selectedAmmo, fixed_accuracy, "Ammo", "Hand_Weapon_Data", "fixed_accuracy");
            ConfigEditorUtils.SetNumber(_selectedAmmo, npc_accuracy_multiplier, "Ammo", "Hand_Weapon_Data", "npc_accuracy_multiplier");
            ConfigEditorUtils.SetNumber(_selectedAmmo, min_accuracy_radius_at_10_metres, "Ammo", "Hand_Weapon_Data", "min_accuracy_radius_at_10_metres");
            ConfigEditorUtils.SetNumber(_selectedAmmo, max_accuracy_radius_at_10_metres, "Ammo", "Hand_Weapon_Data", "max_accuracy_radius_at_10_metres");
            ConfigEditorUtils.SetCheckbox(_selectedAmmo, is_fuel, "Ammo", "Hand_Weapon_Data", "is_fuel");
            ConfigEditorUtils.SetNumber(_selectedAmmo, fuel_units_consumed_per_second_if_firing, "Ammo", "Hand_Weapon_Data", "fuel_units_consumed_per_second_if_firing");
            ConfigEditorUtils.SetNumber(_selectedAmmo, fuel_units_consumed_per_second_if_switched_on, "Ammo", "Hand_Weapon_Data", "fuel_units_consumed_per_second_if_switched_on");
            ConfigEditorUtils.SetNumber(_selectedAmmo, projectile_units_consumed_per_shot, "Ammo", "Hand_Weapon_Data", "projectile_units_consumed_per_shot");

            ConfigEditorUtils.SetNumber(_selectedAmmo, min_distance, "Ammo", "damage_ranges", "min_distance");
            damageRanges.BeginUpdate();
            damageRanges.Items.Clear();
            foreach (XmlElement range_damage in _selectedAmmo[0].Content["Ammo"]["damage_ranges"]["range_damage_list"])
            {
                damageRanges.Items.Add(range_damage.GetAttribute("range"));
            }
            damageRanges.EndUpdate();
            damageRanges.SelectedIndex = 0;

            ConfigEditorUtils.SetCheckbox(_selectedAmmo, has_physics_response, "Ammo", "Physics_response_at_impact_point", "has_physics_response");
            ConfigEditorUtils.SetNumber(_selectedAmmo, impulse_radius, "Ammo", "Physics_response_at_impact_point", "impulse_radius");
            ConfigEditorUtils.SetNumber(_selectedAmmo, impulse_at_centre_of_blast, "Ammo", "Physics_response_at_impact_point", "impulse_at_centre_of_blast");
            ConfigEditorUtils.SetNumber(_selectedAmmo, impulse_fall_off_power, "Ammo", "Physics_response_at_impact_point", "impulse_fall_off_power");
            ConfigEditorUtils.SetNumber(_selectedAmmo, character_wavefront_speed, "Ammo", "Physics_response_at_impact_point", "character_wavefront_speed");

            ConfigEditorUtils.Subscribe(this.Controls, Save);
        }

        private void damageRanges_SelectedIndexChanged(object sender, EventArgs e)
        {
            foreach (XmlElement range_damage in _selectedAmmo[0].Content["Ammo"]["damage_ranges"]["range_damage_list"]) 
            {
                if (range_damage.GetAttribute("range") != damageRanges.Text)
                    continue;

                ConfigEditorUtils.SetNumericFromText(vs_NPC, range_damage.GetAttribute("vs_NPC"));
                ConfigEditorUtils.SetNumericFromText(vsPlayer, range_damage.GetAttribute("vsPlayer"));
                ConfigEditorUtils.SetNumericFromText(vsAndroid, range_damage.GetAttribute("vsAndroid"));
                ConfigEditorUtils.SetNumericFromText(vsAndroidHeavy, range_damage.GetAttribute("vsAndroidHeavy"));
                ConfigEditorUtils.SetNumericFromText(vsFHugger, range_damage.GetAttribute("vsFHugger"));
                ConfigEditorUtils.SetNumericFromText(vsPhysics, range_damage.GetAttribute("vsPhysics"));
                ConfigEditorUtils.SetNumericFromText(headshot, range_damage.GetAttribute("headshot"));
                Damage_1.SelectedItem = range_damage.GetAttribute("Damage_1").ToUpper();
                Damage_2.SelectedItem = range_damage.GetAttribute("Damage_2").ToUpper();
                Damage_3.SelectedItem = range_damage.GetAttribute("Damage_3").ToUpper();
                ConfigEditorUtils.SetNumericFromText(Ragdoll, range_damage.GetAttribute("Ragdoll"));
                ConfigEditorUtils.SetNumericFromText(vsAlien, range_damage.GetAttribute("vsAlien"));
                ConfigEditorUtils.SetNumericFromText(AlienStun, range_damage.GetAttribute("AlienStun"));
                ConfigEditorUtils.SetNumericFromText(StunDuration, range_damage.GetAttribute("StunDuration"));
                ConfigEditorUtils.SetNumericFromText(EMPDuration, range_damage.GetAttribute("EMPDuration"));
                ConfigEditorUtils.SetNumericFromText(BlindDuration, range_damage.GetAttribute("BlindDuration"));
            }
        }

        private void Save(object sender, EventArgs e)
        {
            var doc = _selectedAmmo[0].Content;

            ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "Projectile").InnerText = Projectile.Checked.ToString();
            ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "Flamethrower").InnerText = Flamethrower.Checked.ToString();
            ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "damage_rays_per_shot").InnerText = damage_rays_per_shot.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "damage_rays_blocked_by_characters").InnerText = damage_rays_blocked_by_characters.Checked.ToString();
            ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "use_fixed_accuracy").InnerText = use_fixed_accuracy.Checked.ToString();
            ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "fixed_accuracy").InnerText = fixed_accuracy.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "npc_accuracy_multiplier").InnerText = npc_accuracy_multiplier.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "min_accuracy_radius_at_10_metres").InnerText = min_accuracy_radius_at_10_metres.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "max_accuracy_radius_at_10_metres").InnerText = max_accuracy_radius_at_10_metres.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "is_fuel").InnerText = is_fuel.Checked.ToString();
            ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "fuel_units_consumed_per_second_if_firing").InnerText = fuel_units_consumed_per_second_if_firing.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "fuel_units_consumed_per_second_if_switched_on").InnerText = fuel_units_consumed_per_second_if_switched_on.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "projectile_units_consumed_per_shot").InnerText = projectile_units_consumed_per_shot.Text;

            ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "damage_ranges", "min_distance").InnerText = min_distance.Text;

            ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Physics_response_at_impact_point", "has_physics_response").InnerText = has_physics_response.Checked.ToString();
            ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Physics_response_at_impact_point", "impulse_radius").InnerText = impulse_radius.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Physics_response_at_impact_point", "impulse_at_centre_of_blast").InnerText = impulse_at_centre_of_blast.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Physics_response_at_impact_point", "impulse_fall_off_power").InnerText = impulse_fall_off_power.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Physics_response_at_impact_point", "character_wavefront_speed").InnerText = character_wavefront_speed.Text;

            foreach (XmlElement range_damage in doc["Ammo"]["damage_ranges"]["range_damage_list"])
            {
                if (range_damage.GetAttribute("range") != damageRanges.Text)
                    continue;

                range_damage.SetAttribute("vs_NPC", vs_NPC.Text);
                range_damage.SetAttribute("vsPlayer", vsPlayer.Text);
                range_damage.SetAttribute("vsAndroid", vsAndroid.Text);
                range_damage.SetAttribute("vsAndroidHeavy", vsAndroidHeavy.Text);
                range_damage.SetAttribute("vsFHugger", vsFHugger.Text);
                range_damage.SetAttribute("vsPhysics", vsPhysics.Text);
                range_damage.SetAttribute("headshot", headshot.Text);
                range_damage.SetAttribute("Damage_1", Damage_1.Text);
                range_damage.SetAttribute("Damage_2", Damage_2.Text);
                range_damage.SetAttribute("Damage_3", Damage_3.Text);
                range_damage.SetAttribute("Ragdoll", Ragdoll.Text);
                range_damage.SetAttribute("vsAlien", vsAlien.Text);
                range_damage.SetAttribute("AlienStun", AlienStun.Text);
                range_damage.SetAttribute("StunDuration", StunDuration.Text);
                range_damage.SetAttribute("EMPDuration", EMPDuration.Text);
                range_damage.SetAttribute("BlindDuration", BlindDuration.Text);
            }

            _selectedAmmo[0].Content = doc;
            _selectedAmmo[0].Save();

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void helpBtn_Click(object sender, EventArgs e)
        {
            Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
            Process.Start("https://opencage.co.uk/docs/configs/ammo");
        }
    }
}
