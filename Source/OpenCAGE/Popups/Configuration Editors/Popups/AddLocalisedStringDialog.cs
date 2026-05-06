using System;
using System.Windows.Forms;

namespace CommandsEditor.ConfigEditors
{
    internal sealed partial class AddLocalisedStringDialog : Form
    {
        readonly TextBox[] _langBoxes;

        public string SelectedMissionId { get; private set; }
        public string NewTextId { get; private set; }
        public string[] TextsPerLanguage { get; private set; }

        public AddLocalisedStringDialog(LocalisationHandler handler)
        {
            InitializeComponent();
            _langBoxes = new[]
            {
                textLangCzech,
                textLangEnglish,
                textLangFrench,
                textLangGerman,
                textLangItalian,
                textLangPolish,
                textLangPortuguese,
                textLangRussian,
                textLangSpanish
            };
            foreach (string m in handler.GetSortedMissionIds(LocalisationHandler.AYZ_Lang.ENGLISH))
                comboMission.Items.Add(m);
            if (comboMission.Items.Count > 0)
                comboMission.SelectedIndex = 0;
        }

        private void AddLocalisedStringDialog_Shown(object sender, EventArgs e)
        {
            LayoutLangTableWidths();
        }

        private void panelLangScroll_Resize(object sender, EventArgs e)
        {
            LayoutLangTableWidths();
        }

        private void LayoutLangTableWidths()
        {
            int w = Math.Max(200, panelLangScroll.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8);
            tableLayoutLang.Width = w;
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            if (comboMission.SelectedItem == null || comboMission.Items.Count == 0)
            {
                MessageBox.Show(this, "Select a text file to add the string to.", "Add string", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(textStringId.Text))
            {
                MessageBox.Show(this, "Enter a string ID.", "Add string", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                textStringId.Focus();
                return;
            }

            SelectedMissionId = comboMission.SelectedItem.ToString();
            NewTextId = textStringId.Text.Trim();
            TextsPerLanguage = new string[_langBoxes.Length];
            for (int i = 0; i < _langBoxes.Length; i++)
                TextsPerLanguage[i] = _langBoxes[i].Text;

            DialogResult = DialogResult.OK;
        }
    }
}
