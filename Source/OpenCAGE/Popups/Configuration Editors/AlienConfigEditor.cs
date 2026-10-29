using CATHODE;
using OpenCAGE.Popups.Base;
using OpenCAGE.RuntimeUtilsConnection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.ConfigEditors
{
    public partial class AlienConfigEditor : BaseWindow
    {
        List<BML> _selectedConfig;

        public AlienConfigEditor() : base()
        {
            InitializeComponent();
            ConfigEditorUtils.ExpandNumericRanges(this.Controls);

            BML ammoTypes = new BML(Singleton.PathToAI + "\\DATA\\ALIENCONFIGS\\ALIENCONFIGS.BML");
            var configs = ammoTypes.Content["AlienConfigs"];
            classSelection.BeginUpdate();
            foreach (XmlElement config in configs)
            {
                classSelection.Items.Add(config["Name"].InnerText);
            }
            classSelection.EndUpdate();

            this.FormClosing += AlienConfigEditor_FormClosing;
            CloseOnConfigReset();
            EnableDocumentation("https://opencage.co.uk/docs/configs/alien-configs/");
        }

        private void AlienConfigEditor_Load(object sender, EventArgs e)
        {
            for (int i = 0; i < classSelection.Items.Count; i++)
            {
                classSelection.SelectedIndex = i;
                Save(null, EventArgs.Empty);
            }
            classSelection.SelectedIndex = 0;

            //The game reads these when it starts; with Live Link connected each edit has it read them again
            LiveSettings.AttachReload(this, liveStatus, LiveSettings.ReloadableConfig.AlienConfigs);
        }

        private void AlienConfigEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(this.Controls, Save);
            this.FormClosing -= AlienConfigEditor_FormClosing;
        }

        private void classSelection_SelectedIndexChanged(object sender, EventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(this.Controls, Save);

            _selectedConfig = new List<BML>();
            _selectedConfig.Add(new BML(Singleton.PathToAI + "\\DATA\\ALIENCONFIGS\\" + classSelection.Text + ".BML"));
            while (true)
            {
                string template = _selectedConfig[_selectedConfig.Count - 1].Content["AlienConfig"]["Template_Name"]?.InnerText;
                if (template == null || template == "") break;
                _selectedConfig.Add(new BML(Singleton.PathToAI + "\\DATA\\ALIENCONFIGS\\" + template + ".BML"));
            }

            ConfigEditorUtils.SetNumber(_selectedConfig, decrease_sweep_duration, "AlienConfig", "AreaSweep", "decrease_sweep_duration");
            ConfigEditorUtils.SetNumber(_selectedConfig, increase_sweep_duration, "AlienConfig", "AreaSweep", "increase_sweep_duration");
            ConfigEditorUtils.SetNumber(_selectedConfig, near_target_exclusion_radius_first_stalk_min, "AlienConfig", "AreaSweep", "near_target_exclusion_radius_first_stalk_min");
            ConfigEditorUtils.SetNumber(_selectedConfig, near_target_exclusion_radius_first_stalk_max, "AlienConfig", "AreaSweep", "near_target_exclusion_radius_first_stalk_max");
            ConfigEditorUtils.SetNumber(_selectedConfig, near_target_exclusion_radius_subsequent_stalk_min, "AlienConfig", "AreaSweep", "near_target_exclusion_radius_subsequent_stalk_min");
            ConfigEditorUtils.SetNumber(_selectedConfig, near_target_exclusion_radius_subsequent_stalk_max, "AlienConfig", "AreaSweep", "near_target_exclusion_radius_subsequent_stalk_max");
            ConfigEditorUtils.SetNumber(_selectedConfig, near_objective_exclusion_radius_first_stalk_min, "AlienConfig", "AreaSweep", "near_objective_exclusion_radius_first_stalk_min");
            ConfigEditorUtils.SetNumber(_selectedConfig, near_objective_exclusion_radius_first_stalk_max, "AlienConfig", "AreaSweep", "near_objective_exclusion_radius_first_stalk_max");
            ConfigEditorUtils.SetNumber(_selectedConfig, near_objective_exclusion_radius_subsequent_stalk_min, "AlienConfig", "AreaSweep", "near_objective_exclusion_radius_subsequent_stalk_min");
            ConfigEditorUtils.SetNumber(_selectedConfig, near_objective_exclusion_radius_subsequent_stalk_max, "AlienConfig", "AreaSweep", "near_objective_exclusion_radius_subsequent_stalk_max");
            ConfigEditorUtils.SetNumber(_selectedConfig, menace_gauge_decrease_time, "AlienConfig", "AreaSweep", "menace_gauge_decrease_time");
            ConfigEditorUtils.SetNumber(_selectedConfig, meance_deemed_time, "AlienConfig", "AreaSweep", "meance_deemed_time");
            ConfigEditorUtils.SetNumber(_selectedConfig, max_menaces, "AlienConfig", "AreaSweep", "max_menaces");
            ConfigEditorUtils.SetNumber(_selectedConfig, menace_gauge_seconds_to_fill, "AlienConfig", "AreaSweep", "menace_gauge_seconds_to_fill");
            ConfigEditorUtils.SetNumber(_selectedConfig, sweep_box_half_width, "AlienConfig", "AreaSweep", "sweep_box_half_width");
            ConfigEditorUtils.SetNumber(_selectedConfig, sweep_box_min_half_length, "AlienConfig", "AreaSweep", "sweep_box_min_half_length");
            ConfigEditorUtils.SetNumber(_selectedConfig, Vent_Attract_Time_Min, "AlienConfig", "AreaSweep", "Vent_Attract_Time_Min");
            ConfigEditorUtils.SetNumber(_selectedConfig, Vent_Attract_Time_Max, "AlienConfig", "AreaSweep", "Vent_Attract_Time_Max");

            ConfigEditorUtils.SetNumber(_selectedConfig, role_timeout_min, "AlienConfig", "BackstageAreaSweep", "role_timeout_min");
            ConfigEditorUtils.SetNumber(_selectedConfig, role_timeout_max, "AlienConfig", "BackstageAreaSweep", "role_timeout_max");
            ConfigEditorUtils.SetNumber(_selectedConfig, min_distance, "AlienConfig", "BackstageAreaSweep", "min_distance");
            ConfigEditorUtils.SetNumber(_selectedConfig, max_distance, "AlienConfig", "BackstageAreaSweep", "max_distance");
            ConfigEditorUtils.SetNumber(_selectedConfig, min_idle_time, "AlienConfig", "BackstageAreaSweep", "min_idle_time");
            ConfigEditorUtils.SetNumber(_selectedConfig, max_idle_time, "AlienConfig", "BackstageAreaSweep", "max_idle_time");
            ConfigEditorUtils.SetNumber(_selectedConfig, killtrap_time, "AlienConfig", "BackstageAreaSweep", "killtrap_time");
            ConfigEditorUtils.SetNumber(_selectedConfig, ambush_timeout, "AlienConfig", "BackstageAreaSweep", "ambush_timeout");

            ConfigEditorUtils.Subscribe(this.Controls, Save);
        }

        private void Save(object sender, EventArgs e)
        {
            var doc = _selectedConfig[0].Content;

            if (decrease_sweep_duration.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "decrease_sweep_duration").InnerText = ConfigEditorUtils.NumberText(decrease_sweep_duration);
            if (increase_sweep_duration.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "increase_sweep_duration").InnerText = ConfigEditorUtils.NumberText(increase_sweep_duration);
            if (near_target_exclusion_radius_first_stalk_min.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "near_target_exclusion_radius_first_stalk_min").InnerText = ConfigEditorUtils.NumberText(near_target_exclusion_radius_first_stalk_min);
            if (near_target_exclusion_radius_first_stalk_max.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "near_target_exclusion_radius_first_stalk_max").InnerText = ConfigEditorUtils.NumberText(near_target_exclusion_radius_first_stalk_max);
            if (near_target_exclusion_radius_subsequent_stalk_min.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "near_target_exclusion_radius_subsequent_stalk_min").InnerText = ConfigEditorUtils.NumberText(near_target_exclusion_radius_subsequent_stalk_min);
            if (near_target_exclusion_radius_subsequent_stalk_max.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "near_target_exclusion_radius_subsequent_stalk_max").InnerText = ConfigEditorUtils.NumberText(near_target_exclusion_radius_subsequent_stalk_max);
            if (near_objective_exclusion_radius_first_stalk_min.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "near_objective_exclusion_radius_first_stalk_min").InnerText = ConfigEditorUtils.NumberText(near_objective_exclusion_radius_first_stalk_min);
            if (near_objective_exclusion_radius_first_stalk_max.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "near_objective_exclusion_radius_first_stalk_max").InnerText = ConfigEditorUtils.NumberText(near_objective_exclusion_radius_first_stalk_max);
            if (near_objective_exclusion_radius_subsequent_stalk_min.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "near_objective_exclusion_radius_subsequent_stalk_min").InnerText = ConfigEditorUtils.NumberText(near_objective_exclusion_radius_subsequent_stalk_min);
            if (near_objective_exclusion_radius_subsequent_stalk_max.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "near_objective_exclusion_radius_subsequent_stalk_max").InnerText = ConfigEditorUtils.NumberText(near_objective_exclusion_radius_subsequent_stalk_max);
            if (menace_gauge_decrease_time.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "menace_gauge_decrease_time").InnerText = ConfigEditorUtils.NumberText(menace_gauge_decrease_time);
            if (meance_deemed_time.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "meance_deemed_time").InnerText = ConfigEditorUtils.NumberText(meance_deemed_time);
            if (max_menaces.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "max_menaces").InnerText = ConfigEditorUtils.NumberText(max_menaces);
            if (menace_gauge_seconds_to_fill.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "menace_gauge_seconds_to_fill").InnerText = ConfigEditorUtils.NumberText(menace_gauge_seconds_to_fill);
            if (sweep_box_half_width.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "sweep_box_half_width").InnerText = ConfigEditorUtils.NumberText(sweep_box_half_width);
            if (sweep_box_min_half_length.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "sweep_box_min_half_length").InnerText = ConfigEditorUtils.NumberText(sweep_box_min_half_length);
            if (Vent_Attract_Time_Min.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "Vent_Attract_Time_Min").InnerText = ConfigEditorUtils.NumberText(Vent_Attract_Time_Min);
            if (Vent_Attract_Time_Max.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "Vent_Attract_Time_Max").InnerText = ConfigEditorUtils.NumberText(Vent_Attract_Time_Max);

            if (role_timeout_min.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "BackstageAreaSweep", "role_timeout_min").InnerText = ConfigEditorUtils.NumberText(role_timeout_min);
            if (role_timeout_max.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "BackstageAreaSweep", "role_timeout_max").InnerText = ConfigEditorUtils.NumberText(role_timeout_max);
            if (min_distance.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "BackstageAreaSweep", "min_distance").InnerText = ConfigEditorUtils.NumberText(min_distance);
            if (max_distance.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "BackstageAreaSweep", "max_distance").InnerText = ConfigEditorUtils.NumberText(max_distance);
            if (min_idle_time.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "BackstageAreaSweep", "min_idle_time").InnerText = ConfigEditorUtils.NumberText(min_idle_time);
            if (max_idle_time.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "BackstageAreaSweep", "max_idle_time").InnerText = ConfigEditorUtils.NumberText(max_idle_time);
            if (killtrap_time.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "BackstageAreaSweep", "killtrap_time").InnerText = ConfigEditorUtils.NumberText(killtrap_time);
            if (ambush_timeout.Enabled) ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "BackstageAreaSweep", "ambush_timeout").InnerText = ConfigEditorUtils.NumberText(ambush_timeout);

            _selectedConfig[0].Content = doc;
            _selectedConfig[0].Save();

            //Only an edit goes to the game: the saves made as the editor opens, and as another setup is picked, change nothing it reads
            if (sender != null && sender != classSelection)
                LiveSettings.ReloadAfterEdit(LiveSettings.ReloadableConfig.AlienConfigs);

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }
    }
}
