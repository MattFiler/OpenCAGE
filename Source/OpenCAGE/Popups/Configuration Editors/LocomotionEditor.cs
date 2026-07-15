using CATHODE;
using CATHODE.Enums;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using WeifenLuo.WinFormsUI.Docking;

namespace OpenCAGE.ConfigEditors
{
    public partial class LocomotionEditor : BaseWindow
    {
        List<BML> _selectedCharacter;

        public LocomotionEditor() : base()
        {
            InitializeComponent();
            ConfigEditorUtils.ExpandNumericRanges(this.Controls);

            BML attributeTypes = new BML(Singleton.PathToAI + "\\DATA\\CHR_INFO\\ATTRIBUTES\\ATTRIBUTES.BML");
            var attributes = attributeTypes.Content["Attributes"];
            characters.BeginUpdate();
            foreach (XmlElement attribute in attributes)
            {
                characters.Items.Add(attribute["Name"].InnerText);
            }
            characters.EndUpdate();

            this.FormClosing += LocomotionEditor_FormClosing;
            Singleton.OnResetConfigs += () => { this.Close(); };
        }

        private void LocomotionEditor_Load(object sender, EventArgs e)
        {
            for (int i = 0; i < characters.Items.Count; i++)
            {
                characters.SelectedIndex = i;
                Save(null, EventArgs.Empty);
            }
            characters.SelectedIndex = 0;
        }

        private void LocomotionEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(this.Controls, Save);
            this.FormClosing -= LocomotionEditor_FormClosing;
        }

        private void characters_SelectedIndexChanged(object sender, EventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(this.Controls, Save);

            _selectedCharacter = new List<BML>();
            _selectedCharacter.Add(new BML(Singleton.PathToAI + "\\DATA\\CHR_INFO\\ATTRIBUTES\\" + characters.Text + ".BML"));
            while (true)
            {
                string template = _selectedCharacter[_selectedCharacter.Count - 1].Content["Attribute"]["Template_Name"]?.InnerText;
                if (template == null || template == "") break;
                _selectedCharacter.Add(new BML(Singleton.PathToAI + "\\DATA\\CHR_INFO\\ATTRIBUTES\\" + template + ".BML"));
            }

            ConfigEditorUtils.SetNumber(_selectedCharacter, capsuleRadius, "Attribute", "Locomotion", "capsuleRadius");
            ConfigEditorUtils.SetNumber(_selectedCharacter, capsuleHeight, "Attribute", "Locomotion", "capsuleHeight");
            ConfigEditorUtils.SetNumber(_selectedCharacter, permittedLocomotionModulation, "Attribute", "Locomotion", "permittedLocomotionModulation");

            var boundaries = _selectedCharacter[0].Content["Attribute"]["Locomotion"]["SteeringControls"];
            int i = 0;
            foreach (XmlElement boundary in boundaries)
            {
                tabControl1.TabPages[i].Controls.OfType<SteeringBoundarySet>().FirstOrDefault().Populate(boundary);
                i++;
            }
            tabControl1.SelectedIndex = 0;

            ConfigEditorUtils.Subscribe(this.Controls, Save);
        }

        private void Save(object sender, EventArgs e)
        {
            var doc = _selectedCharacter[0].Content;

            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Locomotion", "capsuleRadius").InnerText = capsuleRadius.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Locomotion", "capsuleHeight").InnerText = capsuleHeight.Text;
            ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Locomotion", "permittedLocomotionModulation").InnerText = permittedLocomotionModulation.Text;

            foreach (TabPage page in tabControl1.TabPages)
            {
                page.Controls.OfType<SteeringBoundarySet>().FirstOrDefault().Save(doc, doc["Attribute"]["Locomotion"]["SteeringControls"]);
            }

            _selectedCharacter[0].Content = doc;
            _selectedCharacter[0].Save();

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }

        private void helpBtn_Click(object sender, EventArgs e)
        {
            Steam.UnlockAchievement(Steam.Achievements.DOCUMENTATION_CHECKED);
            Process.Start("https://opencage.co.uk/docs/configs/locomotion");
        }
    }
}
