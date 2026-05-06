using CATHODE;
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
    public partial class HackingEditor : BaseWindow
    {
        private readonly BML _gblItem;

        public HackingEditor() : base()
        {
            InitializeComponent();

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
            Singleton.OnResetConfigs += () => { this.Close(); };
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
            for (int i = 0; i < hackDifficulties.Items.Count; i++)
            {
                hackDifficulties.SelectedIndex = i;
                Save(null, EventArgs.Empty);
            }
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

                inner_selection_angle_in_deg.Text = difficulty.GetAttribute("inner_selection_angle_in_deg");
                outer_selection_angle_in_deg.Text = difficulty.GetAttribute("outer_selection_angle_in_deg");
                selection_angle_increase_in_deg.Text = difficulty.GetAttribute("selection_angle_increase_in_deg");
                number_of_rounds.Text = difficulty.GetAttribute("number_of_rounds");
                length_of_keycode.Text = difficulty.GetAttribute("length_of_keycode");
                number_of_alarms.Text = difficulty.GetAttribute("number_of_alarms");
                timer_countdown_seconds.Text = difficulty.GetAttribute("timer_countdown_seconds");
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

                difficulty.SetAttribute("inner_selection_angle_in_deg", inner_selection_angle_in_deg.Text);
                difficulty.SetAttribute("outer_selection_angle_in_deg", outer_selection_angle_in_deg.Text);
                difficulty.SetAttribute("selection_angle_increase_in_deg", selection_angle_increase_in_deg.Text);
                difficulty.SetAttribute("number_of_rounds", number_of_rounds.Text);
                difficulty.SetAttribute("length_of_keycode", length_of_keycode.Text);
                difficulty.SetAttribute("number_of_alarms", number_of_alarms.Text);
                difficulty.SetAttribute("timer_countdown_seconds", timer_countdown_seconds.Text); 
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

        private void helpBtn_Click(object sender, EventArgs e)
        {
            Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
            Process.Start("https://opencage.co.uk/docs/configs/hack-tool-difficulties");
        }
    }
}
