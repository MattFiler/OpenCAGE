using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using CathodeLib.ObjectExtensions;
using CommandsEditor.DockPanels;
using CommandsEditor.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CommandsEditor.UserControls
{
    public partial class GUI_ResourceDataType : ParameterUserControl
    {
        public GUI_ResourceDataType() : base()
        {
            InitializeComponent();
            this.ContextMenuStrip = contextMenuStrip1;
            this.deleteToolStripMenuItem.Click += new EventHandler(deleteToolStripMenuItem_Click);
        }

        private EntityInspector _entDisplay = null;

        private cResource resRef = null;
        public void PopulateUI(EntityInspector entDisplay, cResource cResource, string paramID)
        {
            _entDisplay = entDisplay;
            GUID_VARIABLE_DUMMY.Text = paramID;
            resRef = cResource;
            this.deleteToolStripMenuItem.Text = "Delete '" + paramID + "'";

            _hasDoneSetup = true;
        }

        /* Edit resources referenced by the resource param */
        List<ResourceReference> _origResources = new List<ResourceReference>();
        private void openResourceEditor_Click(object sender, EventArgs e)
        {
            _origResources.Clear();
            for (int i = 0; i < resRef.value.Count; i++)
                _origResources.Add(resRef.value[i].Copy());

            AddOrEditResource resourceEditor = new AddOrEditResource(_entDisplay, resRef, GUID_VARIABLE_DUMMY.Text);
            resourceEditor.Show();
            resourceEditor.FormClosed += ResourceEditor_FormClosed;
        }
        private void ResourceEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            this.BringToFront();
            this.Focus();

            if (_origResources.Count != resRef.value.Count || !_origResources.SequenceEqual(resRef.value))
                HighlightAsModified();
        }

        public override void HighlightAsModified(bool updateDatabase = true, Control fontToUpdate = null)
        {
            base.HighlightAsModified(updateDatabase, GUID_VARIABLE_DUMMY);
        }
    }
}
