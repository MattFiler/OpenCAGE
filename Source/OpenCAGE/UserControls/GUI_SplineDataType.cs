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
using CATHODE;
using CathodeLib;
using CommandsEditor.DockPanels;
using CATHODE.Scripting.Internal;

namespace CommandsEditor.UserControls
{
    public partial class GUI_SplineDataType : ParameterUserControl
    {
        private Entity _entity;

        public GUI_SplineDataType(Entity entity) : base()
        {
            _entity = entity;
            InitializeComponent();
            this.ContextMenuStrip = contextMenuStrip1;
            this.deleteToolStripMenuItem.Click += new EventHandler(deleteToolStripMenuItem_Click);
        }

        private cSpline spline = null;
        public void PopulateUI(cSpline cSpline, string paramID)
        {
            SPLINE_CONTAINER.Text = paramID;
            spline = cSpline;
            this.deleteToolStripMenuItem.Text = "Delete '" + paramID + "'";

            _hasDoneSetup = true;
        }

        private void openSplineEditor_Click(object sender, EventArgs e)
        {
            EditSpline splineEditor = new EditSpline(spline, _entity.GetParameter("loop"));
            splineEditor.Show();
            splineEditor.OnSaved += OnSplineEditorSaved;
            splineEditor.FormClosed += SplineEditor_FormClosed;
        } 
        private void OnSplineEditorSaved(cSpline newSpline)
        {
            spline.splinePoints = newSpline.splinePoints;
            HighlightAsModified();
        }
        private void SplineEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            this.BringToFront();
            this.Focus();
        }

        public override void HighlightAsModified(bool updateDatabase = true, Control fontToUpdate = null)
        {
            base.HighlightAsModified(updateDatabase, SPLINE_CONTAINER);
        }
    }
}
