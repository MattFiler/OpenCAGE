using HelixToolkit.Wpf;
using System.Windows.Media.Media3D;

namespace AlienPAK
{
    public partial class ModelImportPreviewWPF
    {
        public ModelImportPreviewWPF()
        {
            InitializeComponent();
        }

        public void SetModelPreview(Model3DGroup content, bool zoomExtents = true)
        {
            modelVisual.Content = content ?? new Model3DGroup();
            viewport.ModelUpDirection = new Vector3D(0, 1, 0);
            viewport.Camera.NearPlaneDistance = 0.01;
            viewport.Camera.UpDirection = new Vector3D(0, 1, 0);
            viewport.Camera.LookDirection = new Vector3D(-0.5, -0.5, -1.0);
            if (zoomExtents && content != null && content.Children.Count > 0)
                viewport.ZoomExtents();
        }
    }
}
