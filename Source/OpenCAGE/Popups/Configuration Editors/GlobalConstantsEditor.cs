using CATHODE;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.ConfigEditors
{
    public partial class GlobalConstantsEditor : DockContent
    {
        private BML _globalConstants;
        private SelectionOverlayParams _selectionParams;

        public GlobalConstantsEditor()
        {
            InitializeComponent();
            ConfigEditorUtils.ExpandNumericRanges(this.Controls);

            _selectionParams = new SelectionOverlayParams(Singleton.PathToAI + "/DATA/UI/SELECTIONOVERLAYPARAMS.BIN");

            glowColour.BackColor = Color.FromArgb(
                (int)(_selectionParams.Colour.W * 255),
                (int)(_selectionParams.Colour.X * 255),
                (int)(_selectionParams.Colour.Y * 255),
                (int)(_selectionParams.Colour.Z * 255)
            );
            ConfigEditorUtils.SetNumericValue(colourAlpha, (decimal)(_selectionParams.Colour.W * 100.0f));
            ConfigEditorUtils.SetNumericFromText(glowRate, _selectionParams.Rate.ToString());
            ConfigEditorUtils.SetNumericFromText(glowPower, _selectionParams.Power.ToString());

            _globalConstants = new BML(Singleton.PathToAI + @"\DATA\GLOBALCONSTANTS.BML");

            ConfigEditorUtils.SetNumericFromText(stealth_light_meter_full_dark_threshold, _globalConstants.Content["GlobalConstants"]["StealthLightMeter"]["stealth_light_meter_full_dark_threshold"].InnerText);
            ConfigEditorUtils.SetNumericFromText(stealth_light_meter_full_light_threshold, _globalConstants.Content["GlobalConstants"]["StealthLightMeter"]["stealth_light_meter_full_light_threshold"].InnerText);
            ConfigEditorUtils.SetNumericFromText(stealth_light_meter_timeout_when_detected, _globalConstants.Content["GlobalConstants"]["StealthLightMeter"]["stealth_light_meter_timeout_when_detected"].InnerText);

            ConfigEditorUtils.SetNumericFromText(interaction_distance_threshold, _globalConstants.Content["GlobalConstants"]["Interaction"]["interaction_distance_threshold"].InnerText);

            ConfigEditorUtils.SetNumericFromText(min_time_between_squad_shots_lower_bound, _globalConstants.Content["GlobalConstants"]["squad_shots"]["min_time_between_squad_shots_lower_bound"].InnerText);
            ConfigEditorUtils.SetNumericFromText(min_time_between_squad_shots_upper_bound, _globalConstants.Content["GlobalConstants"]["squad_shots"]["min_time_between_squad_shots_upper_bound"].InnerText);

            ConfigEditorUtils.SetNumericFromText(min_time_suspicious_reaction_loop, _globalConstants.Content["GlobalConstants"]["suspicious_item_reaction"]["min_time_suspicious_reaction_loop"].InnerText);
            ConfigEditorUtils.SetNumericFromText(max_time_suspicious_reaction_loop, _globalConstants.Content["GlobalConstants"]["suspicious_item_reaction"]["max_time_suspicious_reaction_loop"].InnerText);

            ConfigEditorUtils.Subscribe(this.Controls, Save);
            this.FormClosing += GlobalConstantsEditor_FormClosing;
            Singleton.OnResetConfigs += () => { this.Close(); };
        }

        private void GlobalConstantsEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(this.Controls, Save);
            this.FormClosing -= GlobalConstantsEditor_FormClosing;
        }

        private void setGlowColour_Click(object sender, EventArgs e)
        {
            ColorDialog colourPicker = new ColorDialog();
            colourPicker.Color = glowColour.BackColor;
            colourPicker.CustomColors = SettingsManager.GetIntegerArray(Settings.CustomColours);

            if (colourPicker.ShowDialog() == DialogResult.OK)
            {
                _selectionParams.Colour = new System.Numerics.Vector4((float)colourPicker.Color.R / 255.0f, (float)colourPicker.Color.G / 255.0f, (float)colourPicker.Color.B / 255.0f, (float)colourPicker.Color.A / 255.0f);
                glowColour.BackColor = colourPicker.Color;
                ConfigEditorUtils.SetNumericValue(colourAlpha, 100);
            }
        }

        private void Save(object sender, EventArgs e)
        {
            _selectionParams.Colour.W = (float)colourAlpha.Value / 100.0f;
            glowColour.BackColor = Color.FromArgb(
                (int)(_selectionParams.Colour.W * 255),
                (int)(_selectionParams.Colour.X * 255),
                (int)(_selectionParams.Colour.Y * 255),
                (int)(_selectionParams.Colour.Z * 255)
            );
            _selectionParams.Rate = (float)glowRate.Value;
            _selectionParams.Power = (float)glowPower.Value;
            _selectionParams.Save();

            var doc = _globalConstants.Content;

            doc["GlobalConstants"]["StealthLightMeter"]["stealth_light_meter_full_dark_threshold"].InnerText = stealth_light_meter_full_dark_threshold.Text;
            doc["GlobalConstants"]["StealthLightMeter"]["stealth_light_meter_full_light_threshold"].InnerText = stealth_light_meter_full_light_threshold.Text;
            doc["GlobalConstants"]["StealthLightMeter"]["stealth_light_meter_timeout_when_detected"].InnerText = stealth_light_meter_timeout_when_detected.Text;

            doc["GlobalConstants"]["Interaction"]["interaction_distance_threshold"].InnerText = interaction_distance_threshold.Text;

            doc["GlobalConstants"]["squad_shots"]["min_time_between_squad_shots_lower_bound"].InnerText = min_time_between_squad_shots_lower_bound.Text;
            doc["GlobalConstants"]["squad_shots"]["min_time_between_squad_shots_upper_bound"].InnerText = min_time_between_squad_shots_upper_bound.Text;

            doc["GlobalConstants"]["suspicious_item_reaction"]["min_time_suspicious_reaction_loop"].InnerText = min_time_suspicious_reaction_loop.Text;
            doc["GlobalConstants"]["suspicious_item_reaction"]["max_time_suspicious_reaction_loop"].InnerText = max_time_suspicious_reaction_loop.Text;

            _globalConstants.Content = doc;
            _globalConstants.Save();

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void helpBtn_Click(object sender, EventArgs e)
        {
            Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
            Process.Start("https://opencage.co.uk/docs/configs/global-constants");
        }
    }
}
