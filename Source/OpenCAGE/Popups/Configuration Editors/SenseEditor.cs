using CATHODE;
using CommandsEditor.Popups.Base;
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

namespace CommandsEditor.ConfigEditors
{
    public partial class SenseEditor : BaseWindow
    {
        List<BML> _selectedCharacter;

        public SenseEditor() : base()
        {
            InitializeComponent();

            BML attributeTypes = new BML(Singleton.PathToAI + "\\DATA\\CHR_INFO\\ATTRIBUTES\\ATTRIBUTES.BML");
            var attributes = attributeTypes.Content["Attributes"];
            characters.BeginUpdate();
            foreach (XmlElement attribute in attributes)
            {
                characters.Items.Add(attribute["Name"].InnerText);
            }
            characters.EndUpdate();

            this.FormClosing += SenseEditor_FormClosing;
            Singleton.OnResetConfigs += () => { this.Close(); };
        }

        private void SenseEditor_Load(object sender, EventArgs e)
        {
            for (int i = 0; i < characters.Items.Count; i++)
            {
                characters.SelectedIndex = i;
                Save(null, EventArgs.Empty);
            }
            characters.SelectedIndex = 0;
        }

        private void SenseEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            ConfigEditorUtils.Unsubscribe(this.Controls, Save);
            this.FormClosing -= SenseEditor_FormClosing;
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

            set1Normal.Populate(_selectedCharacter, "set_1_normal", "Attribute", "Senses", "Sensing_Set_1_Normal");
            set1Heightened.Populate(_selectedCharacter, "set_1_heightened", "Attribute", "Senses", "Sensing_Set_1_Heightened");
            set2.Populate(_selectedCharacter, "set_2", "Attribute", "Senses", "Sensing_Set_2");
            set3.Populate(_selectedCharacter, "set_3", "Attribute", "Senses", "Sensing_Set_3");

            ConfigEditorUtils.Subscribe(this.Controls, Save);
        }

        private void Save(object sender, EventArgs e)
        {
            var doc = _selectedCharacter[0].Content;

            set1Normal.Save(ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Senses", "Sensing_Set_1_Normal"), "set_1_normal");
            set1Heightened.Save(ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Senses", "Sensing_Set_1_Heightened"), "set_1_heightened");
            set2.Save(ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Senses", "Sensing_Set_2"), "set_2");
            set3.Save(ConfigEditorUtils.EnsureChildElements(doc, "Attribute", "Senses", "Sensing_Set_3"), "set_3");

            _selectedCharacter[0].Content = doc;
            _selectedCharacter[0].Save();

            Steam.UnlockAchievement(Steam.Achievements.CONFIG_MODIFIED);
        }
    }
}
