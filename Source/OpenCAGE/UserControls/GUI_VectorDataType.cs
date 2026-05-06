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
using System.Numerics;
using Newtonsoft.Json;
using OpenCAGE;

namespace CommandsEditor.UserControls
{
    public partial class GUI_VectorDataType : ParameterUserControl
    {
        cVector3 vectorVal = null;

        public GUI_VectorDataType()
        {
            InitializeComponent();
            this.ContextMenuStrip = contextMenuStrip1;
            this.deleteToolStripMenuItem.Click += new EventHandler(deleteToolStripMenuItem_Click);

            POS_X_1.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStep);
            POS_Y_1.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStep);
            POS_Z_1.Increment = (decimal)SettingsManager.GetFloat(Singleton.Settings.NumericStep);
        }

        public void PopulateUI(cVector3 cVec, string paramID)
        {
            vectorVal = cVec;
            label1.Text = paramID;
            this.deleteToolStripMenuItem.Text = "Delete '" + paramID + "'";

            UpdateUI();

            _hasDoneSetup = true;
        }

        private void UpdateUI()
        {
            POS_X_1.Value = (decimal)vectorVal.value.X;
            POS_Y_1.Value = (decimal)vectorVal.value.Y;
            POS_Z_1.Value = (decimal)vectorVal.value.Z;
        }

        private void POS_X_1_ValueChanged(object sender, EventArgs e)
        {
            vectorVal.value.X = (float)POS_X_1.Value;
            HighlightAsModified();
        }

        private void POS_Y_1_ValueChanged(object sender, EventArgs e)
        {
            vectorVal.value.Y = (float)POS_Y_1.Value;
            HighlightAsModified();
        }

        private void POS_Z_1_ValueChanged(object sender, EventArgs e)
        {
            vectorVal.value.Z = (float)POS_Z_1.Value;
            HighlightAsModified();
        }

        private void copyTransformToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(JsonConvert.SerializeObject(vectorVal));
        }

        private void pasteTransformToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!POS_X_1.Enabled) return;

            string val = Clipboard.GetText()?.ToString();
            cVector3 vector = null;
            try
            {
                vector = JsonConvert.DeserializeObject<cVector3>(val);
            }
            catch { }
            if (vector == null)
            {
                MessageBox.Show("Failed to paste vector.", "Invalid clipboard", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            vectorVal.value.X = vector.value.X;
            vectorVal.value.Y = vector.value.Y;
            vectorVal.value.Z = vector.value.Z;

            UpdateUI();
            HighlightAsModified();
        }

        public override void HighlightAsModified(bool updateDatabase = true, Control fontToUpdate = null)
        {
            base.HighlightAsModified(updateDatabase, label1);
        }
    }
}
