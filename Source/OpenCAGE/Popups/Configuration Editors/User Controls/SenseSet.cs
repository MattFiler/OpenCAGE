using CATHODE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace OpenCAGE.ConfigEditors
{
    public partial class SenseSet : UserControl
    {
        public SenseSet()
        {
            InitializeComponent();

            BML viewconeTypes = new BML(Singleton.PathToAI + "\\DATA\\VIEW_CONE_SETS\\VIEWCONESETS.BML");
            var viewcones = viewconeTypes.Content["ViewconeSets"];
            viewcone_set.BeginUpdate();
            foreach (XmlElement viewcone in viewcones)
            {
                string name = viewcone["Name"].InnerText;
                switch (name)
                {
                    case "VIEWCONESET_STANDARD":
                    case "VIEWCONESET_HUMAN":
                    case "VIEWCONESET_SLEEPING": //unused? it doesnt have all sets.
                    case "VIEWCONESET_ANDROID":
                        break;
                    default:
                        // It appears the game skips any other than the ones above, so ignore them.
                        continue;
                }
                viewcone_set.Items.Add(name);
            }
            viewcone_set.EndUpdate();
        }

        public void Populate(List<BML> configs, string setName, params string[] pathPrefix)
        {
            ConfigEditorUtils.SetCombo(configs, viewcone_set, pathPrefix.Concat(new[] { "viewcone_set_" + setName }).ToArray());
            ConfigEditorUtils.SetNumber(configs, max_hearing_distance, pathPrefix.Concat(new[] { "max_hearing_distance_" + setName }).ToArray());
            ConfigEditorUtils.SetNumber(configs, max_damage_distance_scale_to, pathPrefix.Concat(new[] { "max_damage_distance_scale_to_" + setName }).ToArray());

            visualSense.Populate(configs, setName, "visual", pathPrefix.Concat(new[] { "Visual_Sense" }).ToArray());
            weaponSense.Populate(configs, setName, "weapon_sound", pathPrefix.Concat(new[] { "Weapon_Sound_Sense" }).ToArray());
            movementSense.Populate(configs, setName, "movement_sound", pathPrefix.Concat(new[] { "Movement_Sound_Sense" }).ToArray());
            damageSense.Populate(configs, setName, "damage_caused", pathPrefix.Concat(new[] { "Damage_Caused_Sense" }).ToArray());
            touchedSense.Populate(configs, setName, "touched", pathPrefix.Concat(new[] { "Touched_Sense" }).ToArray());
            flashlightSense.Populate(configs, setName, "see_flash_light", pathPrefix.Concat(new[] { "See_Flash_light_Sense" }).ToArray());
            flamethrowerSense.Populate(configs, setName, "affected_by_flame_thrower", pathPrefix.Concat(new[] { "Affected_by_Flame_Thrower_Sense" }).ToArray());
            combinedSense.Populate(configs, setName, "combined_sense", pathPrefix.Concat(new[] { "Combined_Sense" }).ToArray());
        }

        public void Save(XmlElement sense, string setName)
        {
            ConfigEditorUtils.EnsureChildElements(sense, "viewcone_set_" + setName).InnerText = viewcone_set.Text;
            ConfigEditorUtils.EnsureChildElements(sense, "max_hearing_distance_" + setName).InnerText = max_hearing_distance.Text;
            ConfigEditorUtils.EnsureChildElements(sense, "max_damage_distance_scale_to_" + setName).InnerText = max_damage_distance_scale_to.Text;

            visualSense.Save(ConfigEditorUtils.EnsureChildElements(sense, "Visual_Sense"), setName, "visual");
            weaponSense.Save(ConfigEditorUtils.EnsureChildElements(sense, "Weapon_Sound_Sense"), setName, "weapon_sound");
            movementSense.Save(ConfigEditorUtils.EnsureChildElements(sense, "Movement_Sound_Sense"), setName, "movement_sound");
            damageSense.Save(ConfigEditorUtils.EnsureChildElements(sense, "Damage_Caused_Sense"), setName, "damage_caused");
            touchedSense.Save(ConfigEditorUtils.EnsureChildElements(sense, "Touched_Sense"), setName, "touched");
            flashlightSense.Save(ConfigEditorUtils.EnsureChildElements(sense, "See_Flash_light_Sense"), setName, "see_flash_light");
            flamethrowerSense.Save(ConfigEditorUtils.EnsureChildElements(sense, "Affected_by_Flame_Thrower_Sense"), setName, "affected_by_flame_thrower");
            combinedSense.Save(ConfigEditorUtils.EnsureChildElements(sense, "Combined_Sense"), setName, "combined_sense");
        }
    }
}
