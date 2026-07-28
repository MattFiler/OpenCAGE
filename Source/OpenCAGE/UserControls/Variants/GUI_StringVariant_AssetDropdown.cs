using System;
using System.Linq;
using System.Windows.Forms;
using CATHODE;
using CATHODE.Scripting;

namespace OpenCAGE.UserControls
{
    public partial class GUI_StringVariant_AssetDropdown : ParameterUserControl
    {
        cEnumString _stringVal = null;
        bool _allowTypeSelect = false;

        public GUI_StringVariant_AssetDropdown() : base()
        {
            InitializeComponent();
            this.ContextMenuStrip = contextMenuStrip1;
            this.deleteToolStripMenuItem.Click += new EventHandler(deleteToolStripMenuItem_Click);
        }

        public void PopulateUI(cEnumString cString, string paramID, bool allowTypeSelect)
        {
            _stringVal = cString;
            _allowTypeSelect = allowTypeSelect;

            label1.Text = paramID;
            textBox1.Text = cString.value;
            this.deleteToolStripMenuItem.Text = "Delete '" + paramID + "'";

            _hasDoneSetup = true;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            _stringVal.value = textBox1.Text;
            HighlightAsModified();
        }

        SelectEnumString _popup = null;
        EditMaterial _materialPopup = null;

        private void SelectStr_Click(object sender, EventArgs e)
        {
            if (_stringVal.enumID == EnumStringType.MATERIAL)
            {
                OpenMaterialPicker();
                return;
            }

            if (_popup != null)
            {
                _popup.OnSelected -= OnStringSelected;
                _popup.Close();
            }

            _popup = new SelectEnumString(label1.Text, _stringVal, _allowTypeSelect);
            _popup.OnSelected += OnStringSelected;
            _popup.Show();
        }

        private void OpenMaterialPicker()
        {
            if (_materialPopup != null)
            {
                _materialPopup.OnMaterialSelected -= OnMaterialSelected;
                _materialPopup.FormClosed -= MaterialPopup_FormClosed;
                _materialPopup.Close();
            }

            Materials.Material initial = ResolveCurrentMaterial();
            _materialPopup = new EditMaterial(initial, showSelectBtn: true);
            _materialPopup.OnMaterialSelected += OnMaterialSelected;
            _materialPopup.FormClosed += MaterialPopup_FormClosed;
            _materialPopup.Show();
        }

        private Materials.Material ResolveCurrentMaterial()
        {
            if (Content?.Level?.Materials?.Entries == null || string.IsNullOrWhiteSpace(_stringVal?.value))
                return null;

            string current = _stringVal.value.Trim();
            return Content.Level.Materials.Entries.FirstOrDefault(m =>
                m?.Name != null &&
                (m.Name.Equals(current, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(Content.Level.Materials.GetMaterialName(m), current, StringComparison.OrdinalIgnoreCase)));
        }

        private void MaterialPopup_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (_materialPopup != null)
            {
                _materialPopup.OnMaterialSelected -= OnMaterialSelected;
                _materialPopup.FormClosed -= MaterialPopup_FormClosed;
                _materialPopup = null;
            }
        }

        private void OnMaterialSelected(Materials.Material material)
        {
            if (material == null)
                return;

            textBox1.Text = material.Name;
            _stringVal.value = material.Name;
            HighlightAsModified();
        }

        private void OnStringSelected(string str)
        {
            textBox1.Text = str;
            _stringVal.value = str;
            HighlightAsModified();
        }

        public override void HighlightAsModified(bool updateDatabase = true, Control fontToUpdate = null)
        {
            base.HighlightAsModified(updateDatabase, label1);
        }
    }
}
