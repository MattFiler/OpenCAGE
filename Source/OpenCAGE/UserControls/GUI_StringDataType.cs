using System;
using System.Drawing;
using System.Windows.Forms;
using CATHODE.Scripting;

namespace CommandsEditor.UserControls
{
    public partial class GUI_StringDataType : ParameterUserControl
    {
        cString stringVal = null;

        public GUI_StringDataType()
        {
            InitializeComponent();
            this.ContextMenuStrip = contextMenuStrip1;
            this.deleteToolStripMenuItem.Click += new EventHandler(deleteToolStripMenuItem_Click);
        }

        public void PopulateUI(cString cString, string paramID)
        {
            stringVal = cString;
            label1.Text = paramID;
            textBox1.Text = cString.value;
            this.deleteToolStripMenuItem.Text = "Delete '" + paramID + "'";

            _hasDoneSetup = true;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            stringVal.value = textBox1.Text;
            HighlightAsModified();
        }

        public override void HighlightAsModified(bool updateDatabase = true, Control fontToUpdate = null)
        {
            base.HighlightAsModified(updateDatabase, label1);
        }
    }
}
