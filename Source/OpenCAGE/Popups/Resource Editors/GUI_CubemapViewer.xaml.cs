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
        private readonly Model3DGroup _sphereGroup = new Model3DGroup();

        public GUI_CubemapViewer()
        {
            InitializeComponent();
            cubeVisual.Content = _sphereGroup;
            viewport.ModelUpDirection = new Vector3D(0, 1, 0);
            viewport.Camera.UpDirection = new Vector3D(0, 1, 0);
            viewport.Camera.Position = new Point3D(2.8, 1.6, 2.8);
            viewport.Camera.LookDirection = new Vector3D(-2.8, -1.6, -2.8);
        }

        /// <summary>
        /// Show a cubemap as a textured sphere. Faces are +X,-X,+Y,-Y,+Z,-Z. Caller retains ownership of bitmaps.
        /// </summary>
        public void ShowCubemap(Bitmap[] faces)
        {
            _sphereGroup.Children.Clear();
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

            // DDS order: +X, -X, +Y, -Y, +Z, -Z mapped onto a continuous sphere
            for (int face = 0; face < 6; face++)
            {
                _sphereGroup.Children.Add(MakeSphereFace(face, sources[face]));
            }

            hintText.Text = "Drag to rotate · scroll to zoom";
            viewport.ZoomExtents();
        }

        public void Clear()
        {
            _sphereGroup.Children.Clear();
            hintText.Text = "Drag to rotate · scroll to zoom";
        }

        private static GeometryModel3D MakeSphereFace(int faceIndex, ImageSource image, int divisions = 32, double radius = 1.0)
        {
            var mesh = new MeshGeometry3D();
            int vertsPerSide = divisions + 1;

            for (int j = 0; j <= divisions; j++)
            {
                double v = (double)j / divisions;
                for (int i = 0; i <= divisions; i++)
                {
                    double u = (double)i / divisions;
                    Vector3D dir = GetCubemapDirection(faceIndex, u, v);
                    dir.Normalize();
                    mesh.Positions.Add(new Point3D(dir.X * radius, dir.Y * radius, dir.Z * radius));
                    mesh.TextureCoordinates.Add(new MediaPoint(u, v));
                }
            }

            for (int j = 0; j < divisions; j++)
            {
                for (int i = 0; i < divisions; i++)
                {
                    int tl = j * vertsPerSide + i;
                    int tr = tl + 1;
                    int bl = (j + 1) * vertsPerSide + i;
                    int br = bl + 1;

                    // Outward-facing triangles
                    mesh.TriangleIndices.Add(tl);
                    mesh.TriangleIndices.Add(bl);
                    mesh.TriangleIndices.Add(tr);

                    mesh.TriangleIndices.Add(tr);
                    mesh.TriangleIndices.Add(bl);
                    mesh.TriangleIndices.Add(br);
                }
            }

            if (mesh.CanFreeze)
                mesh.Freeze();

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

            var model = new GeometryModel3D(mesh, material)
            {
                BackMaterial = material
            };
            if (model.CanFreeze)
                model.Freeze();

            return model;
        }

        private static Vector3D GetCubemapDirection(int faceIndex, double u, double v)
        {
            // Standard DirectX cubemap face vector lookup (+X, -X, +Y, -Y, +Z, -Z)
            switch (faceIndex)
            {
                case 0: // +X
                    return new Vector3D(1.0, 1.0 - 2.0 * v, 1.0 - 2.0 * u);
                case 1: // -X
                    return new Vector3D(-1.0, 1.0 - 2.0 * v, 2.0 * u - 1.0);
                case 2: // +Y (Top)
                    return new Vector3D(2.0 * u - 1.0, 1.0, 2.0 * v - 1.0);
                case 3: // -Y (Bottom)
                    return new Vector3D(2.0 * u - 1.0, -1.0, 1.0 - 2.0 * v);
                case 4: // +Z (Front)
                    return new Vector3D(2.0 * u - 1.0, 1.0 - 2.0 * v, 1.0);
                case 5: // -Z (Back)
                    return new Vector3D(1.0 - 2.0 * u, 1.0 - 2.0 * v, -1.0);
                default:
                    return new Vector3D(0, 0, 1);
            }
        }
    }
}
