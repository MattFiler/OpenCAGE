using AlienPAK;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using MediaPoint = System.Windows.Point;

namespace OpenCAGE.Popups.UserControls
{
    public partial class GUI_CubemapViewer : UserControl
    {
        private readonly Model3DGroup _cubeGroup = new Model3DGroup();

        public GUI_CubemapViewer()
        {
            InitializeComponent();
            cubeVisual.Content = _cubeGroup;
            viewport.ModelUpDirection = new Vector3D(0, 1, 0);
            viewport.Camera.UpDirection = new Vector3D(0, 1, 0);
            viewport.Camera.Position = new Point3D(2.8, 1.6, 2.8);
            viewport.Camera.LookDirection = new Vector3D(-2.8, -1.6, -2.8);
        }

        /// <summary>
        /// Show a cubemap as a textured cube. Faces are +X,-X,+Y,-Y,+Z,-Z. Caller retains ownership of bitmaps.
        /// </summary>
        public void ShowCubemap(Bitmap[] faces)
        {
            _cubeGroup.Children.Clear();
            if (faces == null || faces.Length < 6)
            {
                hintText.Text = "Unable to decode cubemap faces";
                return;
            }

            ImageSource[] sources = new ImageSource[6];
            for (int i = 0; i < 6; i++)
            {
                if (faces[i] == null)
                {
                    hintText.Text = "Unable to decode cubemap faces";
                    return;
                }
                ImageSource src = faces[i].ToImageSource();
                if (src is Freezable freezable && freezable.CanFreeze)
                    freezable.Freeze();
                sources[i] = src;
            }

            const double s = 1.0;
            // DDS order: +X, -X, +Y, -Y, +Z, -Z — outward-facing quads
            _cubeGroup.Children.Add(MakeFace(
                new Point3D(s, -s, -s), new Point3D(s, -s, s), new Point3D(s, s, s), new Point3D(s, s, -s), sources[0])); // +X
            _cubeGroup.Children.Add(MakeFace(
                new Point3D(-s, -s, s), new Point3D(-s, -s, -s), new Point3D(-s, s, -s), new Point3D(-s, s, s), sources[1])); // -X
            _cubeGroup.Children.Add(MakeFace(
                new Point3D(-s, s, -s), new Point3D(s, s, -s), new Point3D(s, s, s), new Point3D(-s, s, s), sources[2])); // +Y
            _cubeGroup.Children.Add(MakeFace(
                new Point3D(-s, -s, s), new Point3D(s, -s, s), new Point3D(s, -s, -s), new Point3D(-s, -s, -s), sources[3])); // -Y
            _cubeGroup.Children.Add(MakeFace(
                new Point3D(s, -s, s), new Point3D(-s, -s, s), new Point3D(-s, s, s), new Point3D(s, s, s), sources[4])); // +Z
            _cubeGroup.Children.Add(MakeFace(
                new Point3D(-s, -s, -s), new Point3D(s, -s, -s), new Point3D(s, s, -s), new Point3D(-s, s, -s), sources[5])); // -Z

            hintText.Text = "Drag to rotate · scroll to zoom";
            viewport.ZoomExtents();
        }

        public void Clear()
        {
            _cubeGroup.Children.Clear();
            hintText.Text = "Drag to rotate · scroll to zoom";
        }

        private static GeometryModel3D MakeFace(Point3D p0, Point3D p1, Point3D p2, Point3D p3, ImageSource image)
        {
            var mesh = new MeshGeometry3D
            {
                Positions = new Point3DCollection { p0, p1, p2, p3 },
                TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 },
                TextureCoordinates = new PointCollection
                {
                    new MediaPoint(0, 1),
                    new MediaPoint(1, 1),
                    new MediaPoint(1, 0),
                    new MediaPoint(0, 0),
                }
            };

            var brush = new ImageBrush(image)
            {
                Stretch = Stretch.Fill,
                TileMode = TileMode.None,
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top,
            };
            if (brush.CanFreeze)
                brush.Freeze();

            var material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(brush));
            material.Children.Add(new EmissiveMaterial(brush));
            if (material.CanFreeze)
                material.Freeze();

            return new GeometryModel3D(mesh, material)
            {
                BackMaterial = material
            };
        }
    }
}
