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
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/configs/ammo/");
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
            CloseOnConfigReset();
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
            //The game reads damage types ignoring case, and its files mostly don't use the list's capitals ("Impact")
            ConfigEditorUtils.SelectListed(Damage_1, range_damage.GetAttribute("Damage_1"), true);
            ConfigEditorUtils.SelectListed(Damage_2, range_damage.GetAttribute("Damage_2"), true);
            ConfigEditorUtils.SelectListed(Damage_3, range_damage.GetAttribute("Damage_3"), true);
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
            if (damage_rays_per_shot.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "damage_rays_per_shot").InnerText = ConfigEditorUtils.NumberText(damage_rays_per_shot);
            if (damage_rays_blocked_by_characters.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "damage_rays_blocked_by_characters").InnerText = damage_rays_blocked_by_characters.Checked.ToString();
            if (use_fixed_accuracy.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "use_fixed_accuracy").InnerText = use_fixed_accuracy.Checked.ToString();
            if (fixed_accuracy.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "fixed_accuracy").InnerText = ConfigEditorUtils.NumberText(fixed_accuracy);
            if (npc_accuracy_multiplier.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "npc_accuracy_multiplier").InnerText = ConfigEditorUtils.NumberText(npc_accuracy_multiplier);
            if (min_accuracy_radius_at_10_metres.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "min_accuracy_radius_at_10_metres").InnerText = ConfigEditorUtils.NumberText(min_accuracy_radius_at_10_metres);
            if (max_accuracy_radius_at_10_metres.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "max_accuracy_radius_at_10_metres").InnerText = ConfigEditorUtils.NumberText(max_accuracy_radius_at_10_metres);
            if (is_fuel.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "is_fuel").InnerText = is_fuel.Checked.ToString();
            if (fuel_units_consumed_per_second_if_firing.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "fuel_units_consumed_per_second_if_firing").InnerText = ConfigEditorUtils.NumberText(fuel_units_consumed_per_second_if_firing);
            if (fuel_units_consumed_per_second_if_switched_on.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "fuel_units_consumed_per_second_if_switched_on").InnerText = ConfigEditorUtils.NumberText(fuel_units_consumed_per_second_if_switched_on);
            if (projectile_units_consumed_per_shot.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Hand_Weapon_Data", "projectile_units_consumed_per_shot").InnerText = ConfigEditorUtils.NumberText(projectile_units_consumed_per_shot);

            if (min_distance.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "damage_ranges", "min_distance").InnerText = ConfigEditorUtils.NumberText(min_distance);

            if (has_physics_response.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Physics_response_at_impact_point", "has_physics_response").InnerText = has_physics_response.Checked.ToString();
            if (impulse_radius.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Physics_response_at_impact_point", "impulse_radius").InnerText = ConfigEditorUtils.NumberText(impulse_radius);
            if (impulse_at_centre_of_blast.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Physics_response_at_impact_point", "impulse_at_centre_of_blast").InnerText = ConfigEditorUtils.NumberText(impulse_at_centre_of_blast);
            if (impulse_fall_off_power.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Physics_response_at_impact_point", "impulse_fall_off_power").InnerText = ConfigEditorUtils.NumberText(impulse_fall_off_power);
            if (character_wavefront_speed.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "Ammo", "Physics_response_at_impact_point", "character_wavefront_speed").InnerText = ConfigEditorUtils.NumberText(character_wavefront_speed);

            XmlElement range_damage = GetSelectedRangeElement(doc);
            string newRange = null;
            if (range_damage != null)
            {
                newRange = FormatRangeDistance(range_distance.Value);
                range_damage.SetAttribute("range", newRange);
                range_damage.SetAttribute("vs_NPC", ConfigEditorUtils.NumberText(vs_NPC));
                range_damage.SetAttribute("vsPlayer", ConfigEditorUtils.NumberText(vsPlayer));
                range_damage.SetAttribute("vsAndroid", ConfigEditorUtils.NumberText(vsAndroid));
                range_damage.SetAttribute("vsAndroidHeavy", ConfigEditorUtils.NumberText(vsAndroidHeavy));
                range_damage.SetAttribute("vsFHugger", ConfigEditorUtils.NumberText(vsFHugger));
                range_damage.SetAttribute("vsPhysics", ConfigEditorUtils.NumberText(vsPhysics));
                range_damage.SetAttribute("headshot", ConfigEditorUtils.NumberText(headshot));
                SetDamage(range_damage, "Damage_1", Damage_1);
                SetDamage(range_damage, "Damage_2", Damage_2);
                SetDamage(range_damage, "Damage_3", Damage_3);
                range_damage.SetAttribute("Ragdoll", ConfigEditorUtils.NumberText(Ragdoll));
                range_damage.SetAttribute("vsAlien", ConfigEditorUtils.NumberText(vsAlien));
                range_damage.SetAttribute("AlienStun", ConfigEditorUtils.NumberText(AlienStun));
                range_damage.SetAttribute("StunDuration", ConfigEditorUtils.NumberText(StunDuration));
                range_damage.SetAttribute("EMPDuration", ConfigEditorUtils.NumberText(EMPDuration));
                range_damage.SetAttribute("BlindDuration", ConfigEditorUtils.NumberText(BlindDuration));
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

        /* The list shows the file's damage type in the list's capitals: it's only written when another type was picked, so
         * an edit to anything else keeps the file's own spelling */
        private static void SetDamage(XmlElement range_damage, string name, ComboBox damage)
        {
            if (!string.Equals(range_damage.GetAttribute(name), damage.Text, StringComparison.OrdinalIgnoreCase))
                range_damage.SetAttribute(name, damage.Text);
        }

        private static string FormatRangeDistance(decimal value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
