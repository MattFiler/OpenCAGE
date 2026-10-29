using AlienPAK;
using CATHODE;
using CATHODE.Scripting;
using CATHODE.ShaderTypes;
using CathodeLib;
using OpenCAGE.DockPanels;
using OpenCAGE.Scripts;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Resources;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace OpenCAGE.Popups.UserControls
{
    /// <summary>
    /// Interaction logic for GUI_ModelViewer.xaml
    /// </summary>
    public partial class GUI_ModelViewer : UserControl
    {
        protected LevelContent Content => Singleton.Editor?.CompositeBrowser?.Content;
        private readonly Model3DGroup _opaqueGroup = new Model3DGroup();

        public GUI_ModelViewer()
        {
            InitializeComponent();
            modelPreview.Content = _opaqueGroup;
        }

        public void ShowModel(List<Model> models)
        {
            ShowModel(models, true);
        }

        public void ShowModel(List<Model> models, bool zoomExtents)
        {
            RebuildSceneModels(models ?? new List<Model>());

            if (zoomExtents)
            {
                myView.ModelUpDirection = new Vector3D(0, 1, 0);
                myView.Camera.UpDirection = new Vector3D(0, 1, 0);
                myView.Camera.LookDirection = new Vector3D(-0.5, -0.5, -1.0f);
                myView.ZoomExtents();
            }
        }

        /// <summary>
        /// Show a raw triangle mesh (e.g. Havok collision/physics preview) with a flat material.
        /// </summary>
        public void ShowPreviewMesh(HavokPackfile.PreviewMesh mesh, bool zoomExtents = true)
        {
            _opaqueGroup.Children.Clear();
            transparentSorter.Children.Clear();

            if (mesh != null && mesh.Positions != null && mesh.Indices != null
                && mesh.Positions.Count > 0 && mesh.Indices.Count > 0)
            {
                var positions = new Point3DCollection(mesh.Positions.Count);
                for (int i = 0; i < mesh.Positions.Count; i++)
                {
                    Vector3 p = mesh.Positions[i];
                    //Alien Isolation is opposite-handed to Helix/WPF: Z is flipped and the winding reversed, the same
                    //pair of moves the CS2 preview makes, so a collision mesh sits over the model it was made from
                    positions.Add(new Point3D(p.X, p.Y, -p.Z));
                }

                // Alien Isolation is opposite-handed to Helix/WPF; reverse winding so faces front correctly.
                var indices = new Int32Collection(mesh.Indices.Count);
                for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
                {
                    indices.Add(mesh.Indices[i]);
                    indices.Add(mesh.Indices[i + 2]);
                    indices.Add(mesh.Indices[i + 1]);
                }

                var geometry = new MeshGeometry3D
                {
                    Positions = positions,
                    TriangleIndices = indices,
                };

                var brush = new SolidColorBrush(Color.FromRgb(80, 170, 220));
                var model = new GeometryModel3D
                {
                    Geometry = geometry,
                    Material = new DiffuseMaterial(brush),
                    BackMaterial = new DiffuseMaterial(brush),
                };
                _opaqueGroup.Children.Add(model);
            }

            if (zoomExtents)
            {
                myView.ModelUpDirection = new Vector3D(0, 1, 0);
                myView.Camera.UpDirection = new Vector3D(0, 1, 0);
                myView.Camera.LookDirection = new Vector3D(-0.5, -0.5, -1.0f);
                myView.ZoomExtents();
            }
        }
        
        /// <summary>
        /// A picture of submeshes with their own materials, drawn off screen with no window (for tools): a PNG
        /// <paramref name="width"/> pixels wide and three quarters as tall, looking along <paramref name="view"/> (in the game's
        /// axes: Y up, an entity facing +Z) at all of them. Null when there is nothing to draw. UI thread.
        /// </summary>
        public static byte[] RenderPreview(List<Models.CS2.Component.LOD.Submesh> submeshes, int width, Vector3 view)
        {
            int height = Math.Max(1, width * 3 / 4);
            Model3DGroup models = new Model3DGroup();
            using (MaterialApplier.ShareDerivedImages())
            {
                foreach (Models.CS2.Component.LOD.Submesh submesh in submeshes)
                {
                    GeometryModel3D geometry = submesh?.ToGeometryModel3D(true);
                    if (geometry?.Geometry == null) continue;
                    models.Children.Add(geometry);
                }
            }
            Rect3D bounds = models.Bounds;
            if (models.Children.Count == 0 || bounds.IsEmpty)
                return null;

            //Framed as ZoomExtents frames it: the whole box in view, from outside it
            Point3D centre = new Point3D(bounds.X + bounds.SizeX / 2, bounds.Y + bounds.SizeY / 2, bounds.Z + bounds.SizeZ / 2);
            double radius = Math.Max(0.01, Math.Sqrt(bounds.SizeX * bounds.SizeX + bounds.SizeY * bounds.SizeY + bounds.SizeZ * bounds.SizeZ) / 2);
            Vector3D look = new Vector3D(view.X, view.Y, -view.Z); //the preview is opposite-handed, as the meshes are
            look.Normalize();
            const double fov = 45;
            double distance = radius / Math.Sin(fov * Math.PI / 360.0) * 1.05;
            Vector3D up = Math.Abs(look.Y) > 0.99 ? new Vector3D(0, 0, -1) : new Vector3D(0, 1, 0);
            PerspectiveCamera camera = new PerspectiveCamera(centre - look * distance, look, up, fov) { NearPlaneDistance = distance * 0.01, FarPlaneDistance = distance * 4 };

            Model3DGroup scene = new Model3DGroup();
            scene.Children.Add(new AmbientLight(Color.FromRgb(96, 96, 96)));
            scene.Children.Add(new DirectionalLight(Color.FromRgb(220, 220, 220), look + new Vector3D(0.3, -0.6, 0.2)));
            scene.Children.Add(new DirectionalLight(Color.FromRgb(90, 90, 110), new Vector3D(-look.X, 0.4, -look.Z)));
            scene.Children.Add(models);

            Viewport3D viewport = new Viewport3D() { Camera = camera, Width = width, Height = height, ClipToBounds = true };
            viewport.Children.Add(new ModelVisual3D() { Content = scene });
            Border frame = new Border() { Background = new SolidColorBrush(Color.FromRgb(48, 52, 58)), Width = width, Height = height, Child = viewport };
            frame.Measure(new Size(width, height));
            frame.Arrange(new Rect(0, 0, width, height));
            frame.UpdateLayout();
            System.Windows.Media.Imaging.RenderTargetBitmap bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(frame);
            System.Windows.Media.Imaging.PngBitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
            {
                encoder.Save(stream);
                return stream.ToArray();
            }
        }

        private Model3DGroup OffsetModel(Models.CS2.Component.LOD.Submesh submesh, Vector3D position, Vector3D rotation, Materials.Material material)
        {
            //Get mesh and material data
            GeometryModel3D submeshGeo = submesh.ToGeometryModel3D(SettingsManager.GetBool(Settings.ShowTexOpt));

            //Get transform data - the geometry is already at full size (ModelUtility.ToMesh applies the submesh's VertexScale),
            //so scaling by it again put each submesh at VertexScale times its real position, apart from those beside it
            Transform3DGroup transform = new Transform3DGroup();
            System.Numerics.Quaternion q = System.Numerics.Quaternion.CreateFromYawPitchRoll((float)(rotation.Y * Math.PI / 180.0f), (float)(rotation.X * Math.PI / 180.0f), (float)(rotation.Z * Math.PI / 180.0f));
            transform.Children.Add(new RotateTransform3D(new QuaternionRotation3D(new System.Windows.Media.Media3D.Quaternion(q.X, q.Y, q.Z, q.W))));
            transform.Children.Add(new TranslateTransform3D(position.X, position.Y, position.Z));

            //Submit
            Model3DGroup model = new Model3DGroup();
            model.Transform = transform;
            model.Children.Add(submeshGeo);
            return model;
        }

        private void RebuildSceneModels(List<Model> models)
        {
            _opaqueGroup.Children.Clear();
            transparentSorter.Children.Clear();

            //Submeshes sharing a material share the images made from it, rather than each holding full-size copies
            using (MaterialApplier.ShareDerivedImages())
            for (int i = 0; i < models.Count; i++)
            {
                Model3DGroup model = OffsetModel(models[i].Submesh, models[i].Position, models[i].Rotation, models[i].Material);
                GeometryModel3D geometry = model.Children.OfType<GeometryModel3D>().FirstOrDefault();
                bool isTransparent = MaterialApplier.GetIsTransparent(geometry);
                if (isTransparent)
                {
                    transparentSorter.Children.Add(new ModelVisual3D { Content = model });
                }
                else
                {
                    _opaqueGroup.Children.Add(model);
                }
            }
        }

        public class Model
        {
            public Model(Models.CS2.Component.LOD.Submesh submesh, Materials.Material material = null)
            {
                Create(submesh, new Vector3D(0, 0, 0), new Vector3D(0, 0, 0), material == null ? submesh.Material : material);
            }
            public Model(Models.CS2.Component.LOD.Submesh submesh, cTransform transform, Materials.Material material = null)
            {
                Create(submesh, new Vector3D(transform.position.X, transform.position.Y, transform.position.Z), new Vector3D(transform.rotation.X, transform.rotation.Y, transform.rotation.Z), material == null ? submesh.Material : material);
            }

            private void Create(Models.CS2.Component.LOD.Submesh submesh, Vector3D position, Vector3D rotation, Materials.Material material)
            {
                this.Submesh = submesh;
                this.Material = material;
                this.Position = position;
                this.Rotation = rotation;
            }

            public Models.CS2.Component.LOD.Submesh Submesh;
            public Materials.Material Material;
            public Vector3D Position;
            public Vector3D Rotation;
        }
    }
}
