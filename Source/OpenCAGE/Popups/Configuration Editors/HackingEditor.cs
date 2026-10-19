using CATHODE;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.ConfigEditors
{
    public partial class HackingEditor : BaseWindow
    {
        private readonly BML _gblItem;

        public HackingEditor() : base()
        {
            InitializeComponent();
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/configs/hack-tool-difficulties/");
            ConfigEditorUtils.ExpandNumericRanges(this.Controls);

            _gblItem = new BML(Singleton.PathToAI + @"\DATA\GBL_ITEM.BML");

            var difficulites = _gblItem.Content["item_database"]["hacking_game_difficulties"];
            PopulateDifficulties(lvl0Max, difficulites);
            PopulateDifficulties(lvl1Max, difficulites);
            PopulateDifficulties(lvl2Max, difficulites);
            PopulateDifficulties(lvl3Max, difficulites);
            PopulateDifficulties(lvl99Max, difficulites);
            PopulateDifficulties(hackDifficulties, difficulites);

            var gatingLevels = _gblItem.Content["item_database"]["hacking_gating_levels"];
            foreach (XmlElement level in gatingLevels)
            {
                if (level.GetAttribute("tool_level") == "0")
                    lvl0Max.SelectedItem = level.GetAttribute("max_difficulty");
                else if (level.GetAttribute("tool_level") == "1")
                    lvl1Max.SelectedItem = level.GetAttribute("max_difficulty");
                else if (level.GetAttribute("tool_level") == "2")
                    lvl2Max.SelectedItem = level.GetAttribute("max_difficulty");
                else if (level.GetAttribute("tool_level") == "3")
                    lvl3Max.SelectedItem = level.GetAttribute("max_difficulty");
                else if (level.GetAttribute("tool_level") == "99")
                    lvl99Max.SelectedItem = level.GetAttribute("max_difficulty");
            }

            this.FormClosing += HackingEditor_FormClosing;
            CloseOnConfigReset();
        }

        private void PopulateDifficulties(ComboBox combo, XmlElement difficulties)
        {
            combo.BeginUpdate();
            foreach (XmlElement difficulty in difficulties)
            {
                combo.Items.Add(difficulty.GetAttribute("difficulty"));
            }
            combo.EndUpdate();
        }

        private void HackingEditor_Load(object sender, EventArgs e)
        {
            if (hackDifficulties.Items.Count != 0)
                hackDifficulties.SelectedIndex = 0;
        }

        private void HackingEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(this.Controls, Save);
            this.FormClosing -= HackingEditor_FormClosing;
        }

        private void hackDifficulties_SelectedIndexChanged(object sender, EventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(this.Controls, Save);

            var difficulites = _gblItem.Content["item_database"]["hacking_game_difficulties"];
            foreach (XmlElement difficulty in difficulites)
            {
                if (difficulty.GetAttribute("difficulty") != hackDifficulties.Text)
                    continue;

                ConfigEditorUtils.SetNumericFromText(inner_selection_angle_in_deg, difficulty.GetAttribute("inner_selection_angle_in_deg"));
                ConfigEditorUtils.SetNumericFromText(outer_selection_angle_in_deg, difficulty.GetAttribute("outer_selection_angle_in_deg"));
                ConfigEditorUtils.SetNumericFromText(selection_angle_increase_in_deg, difficulty.GetAttribute("selection_angle_increase_in_deg"));
                ConfigEditorUtils.SetNumericFromText(number_of_rounds, difficulty.GetAttribute("number_of_rounds"));
                ConfigEditorUtils.SetNumericFromText(length_of_keycode, difficulty.GetAttribute("length_of_keycode"));
                ConfigEditorUtils.SetNumericFromText(number_of_alarms, difficulty.GetAttribute("number_of_alarms"));
                ConfigEditorUtils.SetNumericFromText(timer_countdown_seconds, difficulty.GetAttribute("timer_countdown_seconds"));
            }

            ConfigEditorUtils.Subscribe(this.Controls, Save);
        }

        private void Save(object sender, EventArgs e)
        {
            var doc = _gblItem.Content;

            var gatingLevels = doc["item_database"]["hacking_gating_levels"];
            gatingLevels.RemoveAll();
            CreateMaxDifficulty(doc, "0", lvl0Max.Text, gatingLevels);
            CreateMaxDifficulty(doc, "1", lvl1Max.Text, gatingLevels);
            CreateMaxDifficulty(doc, "2", lvl2Max.Text, gatingLevels);
            CreateMaxDifficulty(doc, "3", lvl3Max.Text, gatingLevels);
            CreateMaxDifficulty(doc, "99", lvl99Max.Text, gatingLevels);

            var difficulites = doc["item_database"]["hacking_game_difficulties"];
            foreach (XmlElement difficulty in difficulites)
            {
                if (difficulty.GetAttribute("difficulty") != hackDifficulties.Text)
                    continue;

                difficulty.SetAttribute("inner_selection_angle_in_deg", ConfigEditorUtils.NumberText(inner_selection_angle_in_deg));
                difficulty.SetAttribute("outer_selection_angle_in_deg", ConfigEditorUtils.NumberText(outer_selection_angle_in_deg));
                difficulty.SetAttribute("selection_angle_increase_in_deg", ConfigEditorUtils.NumberText(selection_angle_increase_in_deg));
                difficulty.SetAttribute("number_of_rounds", ConfigEditorUtils.NumberText(number_of_rounds));
                difficulty.SetAttribute("length_of_keycode", ConfigEditorUtils.NumberText(length_of_keycode));
                difficulty.SetAttribute("number_of_alarms", ConfigEditorUtils.NumberText(number_of_alarms));
                difficulty.SetAttribute("timer_countdown_seconds", ConfigEditorUtils.NumberText(timer_countdown_seconds)); 
            }

            _gblItem.Content = doc;
            _gblItem.Save();

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void CreateMaxDifficulty(XmlDocument doc, string toolLvl, string maxDiff, XmlElement parent)
        {
            XmlElement max = doc.CreateElement("hack_max_difficulty");
            max.SetAttribute("tool_level", toolLvl);
            max.SetAttribute("max_difficulty", maxDiff);
            parent.AppendChild(max);
        }
    }
}
