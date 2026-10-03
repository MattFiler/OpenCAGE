using CATHODE.Scripting;
using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace OpenCAGE.Popups.UserControls
{
    /// <summary>
    /// The spline editor's preview: the points numbered in order (the first in green), straight segments between them as
    /// the viewport draws them with an arrow along each, the selected point picked out, and a floor grid underneath. Shown
    /// in the viewport's handedness (Cathode's Z flipped) so it reads the same way round as the level does.
    /// </summary>
    public partial class GUI_SplineViewer : UserControl
    {
        private static readonly Color PathColour = Color.FromRgb(255, 140, 26);
        private static readonly Color FirstPointColour = Color.FromRgb(90, 200, 90);
        private static readonly Color SelectedColour = Color.FromRgb(64, 216, 255);

        public GUI_SplineViewer()
        {
            InitializeComponent();
            DataContext = this;
            ApplyTheme();

            //Y up, as the data is: the viewport's default camera is Z up whatever ModelUpDirection says, which stood the floor on its edge
            myView.Camera = new PerspectiveCamera
            {
                Position = new Point3D(6, 5, 6),
                LookDirection = new Vector3D(-6, -5, -6),
                UpDirection = new Vector3D(0, 1, 0),
                FieldOfView = 45,
            };
        }

        private void ApplyTheme()
        {
            bool dark = Theming.ThemeManager.IsDark;
            myView.Background = new SolidColorBrush(dark ? Color.FromRgb(30, 31, 34) : Color.FromRgb(243, 244, 246));
            summaryText.Foreground = new SolidColorBrush(dark ? Color.FromRgb(200, 202, 208) : Color.FromRgb(60, 62, 68));
        }

        public void ShowSpline(cSpline spline, bool zoomExtents, bool isClosedLoop, int selected = -1)
        {
            sceneContent.Children.Clear();

            List<Point3D> points = new List<Point3D>();
            for (int i = 0; i < spline.splinePoints.Count; i++)
                points.Add(ToPreview(spline.splinePoints[i].position));

            //Everything is sized off the spline's own extent, so a 2 m ladder and a 200 m camera flight both read
            Rect3D bounds = Rect3D.Empty;
            foreach (Point3D point in points)
                bounds.Union(point);
            double extent = bounds.IsEmpty ? 1 : Math.Max(1, Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ)));
            double pointRadius = extent * 0.012;
            double lineDiameter = extent * 0.004;

            AddGrid(bounds, extent);

            double length = 0;
            int segments = points.Count < 2 ? 0 : (isClosedLoop ? points.Count : points.Count - 1);
            for (int i = 0; i < segments; i++)
            {
                Point3D from = points[i];
                Point3D to = points[(i + 1) % points.Count];
                Vector3D along = to - from;
                length += along.Length;
                if (along.Length < 1e-6)
                    continue;

                sceneContent.Children.Add(new TubeVisual3D
                {
                    Path = new Point3DCollection { from, to },
                    Diameter = lineDiameter,
                    ThetaDiv = 12,
                    Fill = new SolidColorBrush(PathColour),
                });

                //The way the path runs: an arrow head part way along, no longer than the gap leaves room for
                double headLength = Math.Min(along.Length * 0.35, extent * 0.05);
                double headDiameter = lineDiameter * 3.5;
                Vector3D direction = along / along.Length;
                Point3D tip = from + along * 0.62;
                sceneContent.Children.Add(new ArrowVisual3D
                {
                    Point1 = tip - direction * headLength,
                    Point2 = tip,
                    Diameter = headDiameter,
                    HeadLength = headLength / headDiameter, //Helix measures the head in diameters: all of this arrow is head
                    ThetaDiv = 12,
                    Fill = new SolidColorBrush(PathColour),
                });
            }

            for (int i = 0; i < points.Count; i++)
            {
                bool isSelected = i == selected;
                Color colour = isSelected ? SelectedColour : i == 0 ? FirstPointColour : PathColour;
                sceneContent.Children.Add(new SphereVisual3D
                {
                    Center = points[i],
                    Radius = isSelected ? pointRadius * 1.5 : pointRadius,
                    ThetaDiv = 20,
                    PhiDiv = 10,
                    Fill = new SolidColorBrush(colour),
                });
                sceneContent.Children.Add(new BillboardTextVisual3D
                {
                    Position = points[i] + new Vector3D(pointRadius * 2, pointRadius * 2, 0),
                    Text = i.ToString(),
                    Foreground = new SolidColorBrush(isSelected ? SelectedColour : Theming.ThemeManager.IsDark ? Color.FromRgb(225, 227, 232) : Color.FromRgb(40, 42, 48)),
                    FontSize = isSelected ? 15 : 13,
                    FontWeight = System.Windows.FontWeights.Bold,
                    DepthOffset = 0.01,
                });
            }

            summaryText.Text = points.Count == 0 ? "No points - add one to start the path"
                : points.Count + (points.Count == 1 ? " point" : " points") + "   " + length.ToString("0.0") + " m"
                  + (isClosedLoop ? "   loops back to point 0" : "");

            if (zoomExtents)
                myView.ZoomExtents();
        }

        /* A floor grid under the spline, on the lowest point's height, a little wider than the spline itself */
        private void AddGrid(Rect3D bounds, double extent)
        {
            bool dark = Theming.ThemeManager.IsDark;
            double step = Math.Pow(10, Math.Floor(Math.Log10(extent / 2)));
            double size = Math.Ceiling(extent * 1.3 / step) * step;
            Point3D centre = bounds.IsEmpty ? new Point3D() : new Point3D(bounds.X + bounds.SizeX / 2, bounds.Y, bounds.Z + bounds.SizeZ / 2);
            sceneContent.Children.Add(new GridLinesVisual3D
            {
                Center = centre,
                Normal = new Vector3D(0, 1, 0),
                LengthDirection = new Vector3D(1, 0, 0),
                Width = size,
                Length = size,
                MinorDistance = step,
                MajorDistance = step * 5,
                Thickness = extent * 0.0015,
                Fill = new SolidColorBrush(dark ? Color.FromRgb(70, 72, 78) : Color.FromRgb(196, 199, 205)),
            });
        }

        private static Point3D ToPreview(System.Numerics.Vector3 position)
        {
            return new Point3D(position.X, position.Y, -position.Z);
        }
    }
}
