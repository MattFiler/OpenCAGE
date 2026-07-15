using CATHODE;
using OpenCAGE.Popups.Base;
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
            Singleton.OnResetConfigs += () => { this.Close(); };
        }

        private void AlienConfigEditor_Load(object sender, EventArgs e)
        {
            for (int i = 0; i < classSelection.Items.Count; i++)
            {
                classSelection.SelectedIndex = i;
                Save(null, EventArgs.Empty);
            }
            classSelection.SelectedIndex = 0;
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

            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "decrease_sweep_duration").InnerText = decrease_sweep_duration.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "increase_sweep_duration").InnerText = increase_sweep_duration.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "near_target_exclusion_radius_first_stalk_min").InnerText = near_target_exclusion_radius_first_stalk_min.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "near_target_exclusion_radius_first_stalk_max").InnerText = near_target_exclusion_radius_first_stalk_max.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "near_target_exclusion_radius_subsequent_stalk_min").InnerText = near_target_exclusion_radius_subsequent_stalk_min.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "near_target_exclusion_radius_subsequent_stalk_max").InnerText = near_target_exclusion_radius_subsequent_stalk_max.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "near_objective_exclusion_radius_first_stalk_min").InnerText = near_objective_exclusion_radius_first_stalk_min.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "near_objective_exclusion_radius_first_stalk_max").InnerText = near_objective_exclusion_radius_first_stalk_max.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "near_objective_exclusion_radius_subsequent_stalk_min").InnerText = near_objective_exclusion_radius_subsequent_stalk_min.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "near_objective_exclusion_radius_subsequent_stalk_max").InnerText = near_objective_exclusion_radius_subsequent_stalk_max.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "menace_gauge_decrease_time").InnerText = menace_gauge_decrease_time.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "meance_deemed_time").InnerText = meance_deemed_time.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "max_menaces").InnerText = max_menaces.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "menace_gauge_seconds_to_fill").InnerText = menace_gauge_seconds_to_fill.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "sweep_box_half_width").InnerText = sweep_box_half_width.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "sweep_box_min_half_length").InnerText = sweep_box_min_half_length.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "Vent_Attract_Time_Min").InnerText = Vent_Attract_Time_Min.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "AreaSweep", "Vent_Attract_Time_Max").InnerText = Vent_Attract_Time_Max.Text;

            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "BackstageAreaSweep", "role_timeout_min").InnerText = role_timeout_min.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "BackstageAreaSweep", "role_timeout_max").InnerText = role_timeout_max.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "BackstageAreaSweep", "min_distance").InnerText = min_distance.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "BackstageAreaSweep", "max_distance").InnerText = max_distance.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "BackstageAreaSweep", "min_idle_time").InnerText = min_idle_time.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "BackstageAreaSweep", "max_idle_time").InnerText = max_idle_time.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "BackstageAreaSweep", "killtrap_time").InnerText = killtrap_time.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "AlienConfig", "BackstageAreaSweep", "ambush_timeout").InnerText = ambush_timeout.Text;

            _selectedConfig[0].Content = doc;
            _selectedConfig[0].Save();

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void helpBtn_Click(object sender, EventArgs e)
        {
            Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
            Process.Start("https://opencage.co.uk/docs/configs/alien-configs");
        }
    }
}
