using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using CATHODE;
using CATHODE.Scripting;
using static OpenCAGE.SelectEnumString;

namespace OpenCAGE.UserControls
{
    public partial class GUI_StringVariant_MappingSelect : ParameterUserControl
    {
        cResource _mappingVal = null;
        MaterialMappings.MaterialMapping _map = null;

        public GUI_StringVariant_MappingSelect() : base()
        {
            InitializeComponent();
            this.ContextMenuStrip = contextMenuStrip1;
            this.deleteToolStripMenuItem.Click += new EventHandler(deleteToolStripMenuItem_Click);
        }

        public void PopulateUI(cResource mappingVal)
        {
            _mappingVal = mappingVal;

            textBox1.Text = "";
            foreach (MaterialMappings.MaterialMapping map in Content.Level.MaterialMappings.Entries)
            {
                if (map.ID != _mappingVal.shortGUID)
                    continue;

                textBox1.Text = map.Name;
                _map = map;
                break;
            }

            label1.Text = "mapping";
            this.deleteToolStripMenuItem.Text = "Delete 'mapping'";

            _hasDoneSetup = true;
        }

        EditMaterialMapping _popup = null;
        private void SelectStr_Click(object sender, EventArgs e)
        {
            if (_popup != null)
            {
                _popup.OnMaterialMappingSelected -= OnMappingSelected;
                _popup.Close();
            }

            _popup = new EditMaterialMapping(_map, true);
            _popup.OnMaterialMappingSelected += OnMappingSelected;
            _popup.Show();
        }
        private void OnMappingSelected(MaterialMappings.MaterialMapping map)
        {
            textBox1.Text = map.Name;
            _map = map;
            _mappingVal.shortGUID = map.ID;
            HighlightAsModified();
        }

        public override void HighlightAsModified(bool updateDatabase = true, Control fontToUpdate = null)
        {
            base.HighlightAsModified(updateDatabase, label1);
        }
    }
}
