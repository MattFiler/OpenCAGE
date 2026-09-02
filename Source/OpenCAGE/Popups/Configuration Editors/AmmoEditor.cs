using CATHODE;
using CATHODE.Enums;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        int _selectedRangeIndex = -1;
        bool _suppressRangeUi;

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
            RefreshDamageRangeList(0);

            ConfigEditorUtils.SetCheckbox(_selectedAmmo, has_physics_response, "Ammo", "Physics_response_at_impact_point", "has_physics_response");
            ConfigEditorUtils.SetNumber(_selectedAmmo, impulse_radius, "Ammo", "Physics_response_at_impact_point", "impulse_radius");
            ConfigEditorUtils.SetNumber(_selectedAmmo, impulse_at_centre_of_blast, "Ammo", "Physics_response_at_impact_point", "impulse_at_centre_of_blast");
            ConfigEditorUtils.SetNumber(_selectedAmmo, impulse_fall_off_power, "Ammo", "Physics_response_at_impact_point", "impulse_fall_off_power");
            ConfigEditorUtils.SetNumber(_selectedAmmo, character_wavefront_speed, "Ammo", "Physics_response_at_impact_point", "character_wavefront_speed");

            ConfigEditorUtils.Subscribe(this.Controls, Save);
        }

        private void RefreshDamageRangeList(int selectedIndex)
        {
            _suppressRangeUi = true;
            damageRanges.BeginUpdate();
            damageRanges.Items.Clear();
            XmlElement list = GetRangeDamageList();
            if (list != null)
            {
                foreach (XmlElement range_damage in list)
                    damageRanges.Items.Add(range_damage.GetAttribute("range"));
            }
            damageRanges.EndUpdate();

            if (damageRanges.Items.Count == 0)
            {
                _selectedRangeIndex = -1;
                range_distance.Enabled = false;
                _suppressRangeUi = false;
                return;
            }

            range_distance.Enabled = true;
            if (selectedIndex < 0 || selectedIndex >= damageRanges.Items.Count)
                selectedIndex = 0;
            damageRanges.SelectedIndex = selectedIndex;
            _selectedRangeIndex = selectedIndex;
            _suppressRangeUi = false;
            LoadSelectedDamageRange();
        }

        private XmlElement GetRangeDamageList()
        {
            if (_selectedAmmo == null || _selectedAmmo.Count == 0)
                return null;
            return _selectedAmmo[0].Content?["Ammo"]?["damage_ranges"]?["range_damage_list"];
        }

        private XmlElement GetSelectedRangeElement(XmlDocument doc = null)
        {
            XmlElement list = doc != null
                ? doc["Ammo"]?["damage_ranges"]?["range_damage_list"]
                : GetRangeDamageList();
            if (list == null || _selectedRangeIndex < 0)
                return null;

            int i = 0;
            foreach (XmlElement range_damage in list)
            {
                if (i == _selectedRangeIndex)
                    return range_damage;
                i++;
            }
            return null;
        }

        private void damageRanges_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressRangeUi)
                return;

            _selectedRangeIndex = damageRanges.SelectedIndex;
            LoadSelectedDamageRange();
        }

        private void LoadSelectedDamageRange()
        {
            XmlElement range_damage = GetSelectedRangeElement();
            if (range_damage == null)
                return;

            _suppressRangeUi = true;
            ConfigEditorUtils.SetNumericFromText(range_distance, range_damage.GetAttribute("range"));
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
            _suppressRangeUi = false;
        }

        private void Save(object sender, EventArgs e)
        {
            if (_suppressRangeUi || _selectedAmmo == null || _selectedAmmo.Count == 0)
                return;

            var doc = _selectedAmmo[0].Content;

            if (Projectile.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "Projectile").InnerText = Projectile.Checked.ToString();
            if (Flamethrower.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "Flamethrower").InnerText = Flamethrower.Checked.ToString();
            if (damage_rays_per_shot.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "damage_rays_per_shot").InnerText = damage_rays_per_shot.Text;
            if (damage_rays_blocked_by_characters.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "damage_rays_blocked_by_characters").InnerText = damage_rays_blocked_by_characters.Checked.ToString();
            if (use_fixed_accuracy.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "use_fixed_accuracy").InnerText = use_fixed_accuracy.Checked.ToString();
            if (fixed_accuracy.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "fixed_accuracy").InnerText = fixed_accuracy.Text;
            if (npc_accuracy_multiplier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "npc_accuracy_multiplier").InnerText = npc_accuracy_multiplier.Text;
            if (min_accuracy_radius_at_10_metres.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "min_accuracy_radius_at_10_metres").InnerText = min_accuracy_radius_at_10_metres.Text;
            if (max_accuracy_radius_at_10_metres.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "max_accuracy_radius_at_10_metres").InnerText = max_accuracy_radius_at_10_metres.Text;
            if (is_fuel.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "is_fuel").InnerText = is_fuel.Checked.ToString();
            if (fuel_units_consumed_per_second_if_firing.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "fuel_units_consumed_per_second_if_firing").InnerText = fuel_units_consumed_per_second_if_firing.Text;
            if (fuel_units_consumed_per_second_if_switched_on.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "fuel_units_consumed_per_second_if_switched_on").InnerText = fuel_units_consumed_per_second_if_switched_on.Text;
            if (projectile_units_consumed_per_shot.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "projectile_units_consumed_per_shot").InnerText = projectile_units_consumed_per_shot.Text;

            if (min_distance.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "damage_ranges", "min_distance").InnerText = min_distance.Text;

            if (has_physics_response.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Physics_response_at_impact_point", "has_physics_response").InnerText = has_physics_response.Checked.ToString();
            if (impulse_radius.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Physics_response_at_impact_point", "impulse_radius").InnerText = impulse_radius.Text;
            if (impulse_at_centre_of_blast.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Physics_response_at_impact_point", "impulse_at_centre_of_blast").InnerText = impulse_at_centre_of_blast.Text;
            if (impulse_fall_off_power.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Physics_response_at_impact_point", "impulse_fall_off_power").InnerText = impulse_fall_off_power.Text;
            if (character_wavefront_speed.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Physics_response_at_impact_point", "character_wavefront_speed").InnerText = character_wavefront_speed.Text;

            XmlElement range_damage = GetSelectedRangeElement(doc);
            string newRange = null;
            if (range_damage != null)
            {
                newRange = FormatRangeDistance(range_distance.Value);
                range_damage.SetAttribute("range", newRange);
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

            if (newRange != null
                && _selectedRangeIndex >= 0
                && _selectedRangeIndex < damageRanges.Items.Count
                && damageRanges.Items[_selectedRangeIndex].ToString() != newRange)
            {
                _suppressRangeUi = true;
                damageRanges.Items[_selectedRangeIndex] = newRange;
                _suppressRangeUi = false;
            }

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private static string FormatRangeDistance(decimal value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private void helpBtn_Click(object sender, EventArgs e)
        {
            Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
            Process.Start("https://opencage.co.uk/docs/configs/ammo");
        }
    }
}
