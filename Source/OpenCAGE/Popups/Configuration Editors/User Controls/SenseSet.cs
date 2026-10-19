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
        private string _unlistedViewconeSet = null;

        public SenseSet()
        {
            InitializeComponent();
            ConfigEditorUtils.ExpandNumericRanges(this.Controls);

            //Every set the game lists. This used to keep only STANDARD, HUMAN, SLEEPING and ANDROID (the Viewcone Editor's list),
            //but characters use the others too: THE_PLAYER's normal set is VIEWCONESET_NONE
            BML viewconeTypes = new BML(Singleton.PathToAI + "\\DATA\\VIEW_CONE_SETS\\VIEWCONESETS.BML");
            var viewcones = viewconeTypes.Content["ViewconeSets"];
            viewcone_set.BeginUpdate();
            foreach (XmlElement viewcone in viewcones)
            {
                viewcone_set.Items.Add(viewcone["Name"].InnerText);
            }
            viewcone_set.EndUpdate();
        }

        public void Populate(List<BML> configs, string setName, params string[] pathPrefix)
        {
            string[] viewconePath = pathPrefix.Concat(new[] { "viewcone_set_" + setName }).ToArray();
            string viewcone = null;
            foreach (BML config in configs)
            {
                XmlNode node = config.Content;
                foreach (string name in viewconePath)
                    node = node?[name];
                if (node == null)
                    continue;
                viewcone = node.InnerText;
                break;
            }

            //The list can only show what it holds, and saving writes what it shows: a set it doesn't have (a mod's own, say) would
            //show, and save, the previous character's instead. So it's added as it is, for as long as this character is shown.
            if (_unlistedViewconeSet != null)
                viewcone_set.Items.Remove(_unlistedViewconeSet);
            _unlistedViewconeSet = null;
            if (viewcone != null && !viewcone_set.Items.Contains(viewcone))
            {
                viewcone_set.Items.Add(viewcone);
                _unlistedViewconeSet = viewcone;
            }
            viewcone_set.Enabled = viewcone != null;
            if (viewcone != null)
                viewcone_set.SelectedItem = viewcone; //Not Text, which picks the first item that matches ignoring case

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
            //A field no file in the template chain has is disabled, and still shows the previous character's value
            if (viewcone_set.Enabled) ConfigEditorUtils.EnsureChildElements(sense, "viewcone_set_" + setName).InnerText = viewcone_set.Text;
            if (max_hearing_distance.Enabled) ConfigEditorUtils.EnsureChildElements(sense, "max_hearing_distance_" + setName).InnerText = ConfigEditorUtils.NumberText(max_hearing_distance);
            if (max_damage_distance_scale_to.Enabled) ConfigEditorUtils.EnsureChildElements(sense, "max_damage_distance_scale_to_" + setName).InnerText = ConfigEditorUtils.NumberText(max_damage_distance_scale_to);

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
