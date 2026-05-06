using AlienPAK;
using CATHODE;
using CATHODE.Scripting;
using CATHODE.ShaderTypes;
using CathodeLib;
using CommandsEditor.DockPanels;
using CommandsEditor.Scripts;
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

namespace CommandsEditor.Popups.UserControls
{
    /// <summary>
    /// Interaction logic for GUI_ModelViewer.xaml
    /// </summary>
    public partial class GUI_ModelViewer : UserControl
    {
        protected LevelContent Content => Singleton.Editor?.CommandsDisplay?.Content;
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
        
        private Model3DGroup OffsetModel(Models.CS2.Component.LOD.Submesh submesh, Vector3D position, Vector3D rotation, Materials.Material material)
        {
            //Get mesh and material data
            GeometryModel3D submeshGeo = submesh.ToGeometryModel3D(SettingsManager.GetBool(Singleton.Settings.ShowTexOpt));

            //Get transform data
            Transform3DGroup transform = new Transform3DGroup();
            transform.Children.Add(new ScaleTransform3D(submesh.VertexScale, submesh.VertexScale, submesh.VertexScale));
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
