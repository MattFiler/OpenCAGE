using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CATHODE.Scripting;
using CathodeLib;
using CATHODE;
using Newtonsoft.Json;
using OpenCAGE;

namespace CommandsEditor.UserControls
{
    public partial class GUI_VectorVariant_Colour : ParameterUserControl
    {
        cVector3 vector = null;

        public GUI_VectorVariant_Colour()
        {
            InitializeComponent();
            this.ContextMenuStrip = contextMenuStrip1;
            this.deleteToolStripMenuItem.Click += new EventHandler(deleteToolStripMenuItem_Click);
        }

        public void PopulateUI(cVector3 cVec, string paramID)
        {
            vector = cVec;
            GUID_VARIABLE_DUMMY.Text = paramID;
            this.deleteToolStripMenuItem.Text = "Delete '" + paramID + "'";

            UpdateUI();

            _hasDoneSetup = true;
        }

        private void UpdateUI()
        {
            pictureBox1.BackColor = VectorToColour();
        }

        private Color VectorToColour()
        {
            return Color.FromArgb((int)vector.value.X, (int)vector.value.Y, (int)vector.value.Z);
        }
        private void SetVectorFromColour(Color colour)
        {
            vector.value.X = colour.R;
            vector.value.Y = colour.G;
            vector.value.Z = colour.B;
        }

        private void openColourPicker_Click(object sender, EventArgs e)
        {
            ColorDialog colourPicker = new ColorDialog();
            colourPicker.Color = VectorToColour();
            colourPicker.CustomColors = SettingsManager.GetIntegerArray(Singleton.Settings.CustomColours);

            if (colourPicker.ShowDialog() == DialogResult.OK)
            {
                SetVectorFromColour(colourPicker.Color);
                pictureBox1.BackColor = VectorToColour();
                SettingsManager.SetIntegerArray(Singleton.Settings.CustomColours, colourPicker.CustomColors);
                HighlightAsModified();
            }
        }

        private void copyTransformToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(JsonConvert.SerializeObject(vector));
        }

        private void pasteTransformToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!pictureBox1.Enabled) return;

            string val = Clipboard.GetText()?.ToString();
            cVector3 newVector = null;
            try
            {
                newVector = JsonConvert.DeserializeObject<cVector3>(val);
            }
            catch { }
            if (newVector == null)
            {
                MessageBox.Show("Failed to paste vector.", "Invalid clipboard", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            vector.value.X = newVector.value.X;
            vector.value.Y = newVector.value.Y;
            vector.value.Z = newVector.value.Z;

            UpdateUI();
            HighlightAsModified();
        }

        public override void HighlightAsModified(bool updateDatabase = true, Control fontToUpdate = null)
        {
            base.HighlightAsModified(updateDatabase, GUID_VARIABLE_DUMMY);
        }
    }
}
