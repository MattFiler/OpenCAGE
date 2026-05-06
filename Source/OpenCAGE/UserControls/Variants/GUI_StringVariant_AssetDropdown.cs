using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using CATHODE;
using CATHODE.Scripting;
using static CommandsEditor.SelectEnumString;

namespace CommandsEditor.UserControls
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
        private void SelectStr_Click(object sender, EventArgs e)
        {
            if (_popup != null)
            {
                _popup.OnSelected -= OnStringSelected;
                _popup.Close();
            }

            _popup = new SelectEnumString(label1.Text, _stringVal, _allowTypeSelect);
            _popup.OnSelected += OnStringSelected;
            _popup.Show();
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
