using CathodeLib;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Numerics;
using CATHODE;
using CATHODE.Scripting;
using System.Net.NetworkInformation;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CommandsEditor.Popups.UserControls
{
    /// <summary>
    /// Interaction logic for GUI_SplineViewer.xaml
    /// </summary>
    public partial class GUI_SplineViewer : UserControl
    {
        public GUI_SplineViewer()
        {
            InitializeComponent();
            DataContext = this;
        }

        public void ShowSpline(cSpline spline, bool zoomExtents, bool isClosedLoop)
        {
            Point3DCollection data = new Point3DCollection(spline.splinePoints.Count);
            for (int i = 0; i < spline.splinePoints.Count; i++)
                data.Add(new Point3D(spline.splinePoints[i].position.X, spline.splinePoints[i].position.Y, spline.splinePoints[i].position.Z));
            splineVisual.Path = data;
            splineVisual.IsPathClosed = isClosedLoop;
            if (zoomExtents) myView.ZoomExtents();
        }

        public void HighlightPoint(int index)
        {
            if (splineVisual.Path == null || splineVisual.Path.Count < index) return;
            billboardText.Position = splineVisual.Path[index];
            billboardText.Text = "Spline Point " + index;
        }

        public void ClearHighlight()
        {
            billboardText.Text = "";
        }
    }
}
