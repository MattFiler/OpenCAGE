using CATHODE.Scripting;
using CathodeLib.ObjectExtensions;
using OpenCAGE.Popups.Base;
using OpenCAGE.Popups.UserControls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class EditSpline : BaseWindow
    {
        public Action<cSpline> OnSaved;

        private GUI_SplineViewer splineViewer;
        private cSpline spline;
        private bool firstUpdate = true;
        private bool isClosedLoop = false; //Default loop val is false

        public EditSpline(cSpline _spline, Parameter _closed) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();
            spline = _spline.Copy();
            if (_closed != null) isClosedLoop = ((cBool)_closed.content).value;

            splineViewer = new GUI_SplineViewer();
            modelRendererHost.Child = splineViewer;

            UpdateSplineVisual();
            pointTransform.OnValueChanged += UpdateSplineVisual;

            RefreshPointList();
        }

        private void UpdateSplineVisual()
        {
            splineViewer.ShowSpline(spline, firstUpdate, isClosedLoop);
            firstUpdate = false;
        }

        private void RefreshPointList()
        {
            splinePoints.Items.Clear();
            splinePoints.SelectedIndex = -1;
            if (spline.splinePoints.Count > 0)
            {
                for (int i = 0; i < spline.splinePoints.Count; i++)
                    splinePoints.Items.Add("Spline Point " + i);
                splinePoints.SelectedIndex = 0;
                splineViewer.HighlightPoint(0);
            }
            else
                splineViewer.ClearHighlight();
        }

        private void splinePoints_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (splinePoints.SelectedIndex == -1)
            {
                splineViewer.ClearHighlight();
                pointTransform.Visible = false;
                return;
            }
            pointTransform.Visible = true;
            splineViewer.HighlightPoint(splinePoints.SelectedIndex);
            pointTransform.PopulateUI(null, spline.splinePoints[splinePoints.SelectedIndex], "Spline Point " + splinePoints.SelectedIndex);
        }

        private void addPoint_Click(object sender, EventArgs e)
        {
            spline.splinePoints.Add(new cTransform());
            UpdateSplineVisual();
            RefreshPointList();
        }

        private void removePoint_Click(object sender, EventArgs e)
        {
            if (splinePoints.SelectedIndex == -1) return;
            spline.splinePoints.RemoveAt(splinePoints.SelectedIndex);
            UpdateSplineVisual();
            RefreshPointList();
        }

        private void saveSpline_Click(object sender, EventArgs e)
        {
            OnSaved?.Invoke(spline);
            this.Close();
        }
    }
}
