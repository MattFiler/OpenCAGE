using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using CATHODE.Scripting;
using CathodeLib;
using static CathodeLib.CathodeEnumTable;

namespace CommandsEditor.UserControls
{
    public partial class GUI_EnumDataType : ParameterUserControl
    {
        cEnum enumVal = null;

        public GUI_EnumDataType()
        {
            InitializeComponent();

            this.ContextMenuStrip = contextMenuStrip1;
            this.deleteToolStripMenuItem.Click += new EventHandler(deleteToolStripMenuItem_Click);
        }

        public void PopulateUI(cEnum cEnum, string paramID, bool allowTypeSelection)
        {
            groupBox1.Text = paramID;

            enumVal = cEnum;
            this.deleteToolStripMenuItem.Text = "Delete '" + paramID + "'";

            if (allowTypeSelection)
            {
                comboBox1.BeginUpdate();
                comboBox1.Items.Clear();
                List<string> orderedEnums = new List<string>();
                foreach (EnumType enumType in Enum.GetValues(typeof(EnumType)))
                {
                    orderedEnums.Add(enumType.ToString());
                }
                orderedEnums.Sort();
                foreach (string enumType in orderedEnums)
                {
                    comboBox1.Items.Add(enumType);
                }
                comboBox1.EndUpdate();
                comboBox1.SelectedIndex = 0;

                this.Height = 80;
                groupBox1.Height = 75;
                groupBox1.Location = new Point(0, 0);
                comboBox1.Visible = true;
                comboBox1.Location = new Point(6, 19);
                comboBox2.Location = new Point(6, 48);
                
                if (enumVal.enumID == ShortGuid.Invalid)
                {
                    //if this entity has no default enum applied, apply one
                    EnumDescriptor enumDesc = Content.Level.Commands.Utils.GetEnum(ShortGuidUtils.Generate(comboBox1.Text));
                    EnumDescriptor.Entry enumEntry = enumDesc.Entries.FirstOrDefault(o => o.Index == -1);
                    comboBox2.SelectedItem = enumEntry.Name;
                }
                else
                {
                    comboBox1.SelectedItem = enumVal.enumID.ToString();
                    EnumDescriptor enumDesc = Content.Level.Commands.Utils.GetEnum(enumVal.enumID);
                    EnumDescriptor.Entry enumEntry = enumDesc.Entries.FirstOrDefault(o => o.Index == enumVal.enumIndex);
                    comboBox2.SelectedItem = enumEntry.Name;
                }
            }
            else
            {
                EnumDescriptor enumDesc = Content.Level.Commands.Utils.GetEnum(cEnum.enumID);
                comboBox1.Items.Add(enumDesc.Name);
                comboBox1.Text = enumDesc.Name;

                EnumDescriptor.Entry enumEntry = enumDesc.Entries.FirstOrDefault(o => o.Index == cEnum.enumIndex);
                if (enumEntry == null)
                    MessageBox.Show("Failed to match index " + cEnum.enumIndex + " for " + enumDesc.Name + "!\nThis behaviour is unexpected.\nPlease report this on GitHub!", "Error!!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                else
                    comboBox2.SelectedItem = enumEntry.Name;
            }

            _hasDoneSetup = true;
        }

        private void UpdateEnumOptions(EnumDescriptor enumDesc)
        {
            comboBox2.BeginUpdate();
            comboBox2.Items.Clear();
            foreach (EnumDescriptor.Entry entry in enumDesc.Entries)
                comboBox2.Items.Add(entry.Name);
            comboBox2.EndUpdate();
            comboBox2.SelectedIndex = 0;

            toolTip1.SetToolTip(comboBox2, enumDesc.Name + " values");
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            EnumDescriptor enumDesc = Content.Level.Commands.Utils.GetEnum(comboBox1.Text);
            UpdateEnumOptions(enumDesc);
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            EnumDescriptor enumDesc = Content.Level.Commands.Utils.GetEnum(comboBox1.Text);
            UpdateEnum(enumDesc);
        }

        private void UpdateEnum(EnumDescriptor enumDesc)
        {
            if (!_hasDoneSetup)
                return;

            EnumDescriptor.Entry enumEntry = enumDesc.Entries.FirstOrDefault(o => o.Name == comboBox2.SelectedItem.ToString());
            enumVal.enumID = enumDesc.ID;
            enumVal.enumIndex = enumEntry.Index;
            HighlightAsModified();
        }

        public override void HighlightAsModified(bool updateDatabase = true, Control fontToUpdate = null)
        {
            base.HighlightAsModified(updateDatabase, groupBox1);
        }
    }
}
