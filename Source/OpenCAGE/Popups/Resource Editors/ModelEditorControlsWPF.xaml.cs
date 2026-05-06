using CATHODE;
using CommandsEditor;
using OpenCAGE;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;

namespace AlienPAK
{
    public partial class ModelEditorControlsWPF : UserControl
    {
        public Action OnDeleteRequested;
        public Action OnReplaceRequested;
        public Action<SelectedModelType> OnAddRequested;
        public Action OnEditMaterialRequested;

        public Action<bool> OnMaterialRenderCheckChanged;

        public Action<int> OnScaleFactorChanged;

        public ModelEditorControlsWPF()
        {
            InitializeComponent();
            renderMaterials.IsChecked = SettingsManager.GetBool(Singleton.Settings.ShowTexOpt);
        }

        public void SetModelPreview(Model3DGroup content, string filename, int vertCount, string material, int sf = -1, bool doZoom = true)
        {
            filePreviewModel.Content = content;
            if (doZoom)
            {
                filePreviewModelContainer.ModelUpDirection = new Vector3D(0, 1, 0);
                filePreviewModelContainer.Camera.UpDirection = new Vector3D(0, 1, 0);
                filePreviewModelContainer.Camera.LookDirection = new Vector3D(-0.5, -0.5, -1.0f);
                filePreviewModelContainer.ZoomExtents();
            }

            fileNameText.Text = filename;
            vertexCount.Text = vertCount.ToString();
            materialLabel.Visibility = material != "" ? Visibility.Visible : Visibility.Collapsed;
            materialInfo.Text = material;
            materialInfo.Visibility = materialLabel.Visibility;
            scaleFactorLabel.Visibility = sf != -1 ? Visibility.Visible : Visibility.Collapsed;
            if (sf != -1) scaleFactor.Text = sf.ToString();
            scaleFactor.Visibility = scaleFactorLabel.Visibility;
        }

        public void ShowContextualButtons(SelectedModelType type)
        {
            replaceBtn.Visibility = type == SelectedModelType.SUBMESH ? Visibility.Visible : Visibility.Collapsed;
            editMaterialBtn.Visibility = type == SelectedModelType.SUBMESH ? Visibility.Visible : Visibility.Collapsed;
            deleteBtn.Visibility = type != SelectedModelType.CS2 && type != SelectedModelType.NONE ? Visibility.Visible : Visibility.Collapsed;
            addComponentBtn.Visibility = type == SelectedModelType.CS2 ? Visibility.Visible : Visibility.Collapsed;
            addLODBtn.Visibility = type == SelectedModelType.COMPONENT ? Visibility.Visible : Visibility.Collapsed;
            addSubmeshBtn.Visibility = type == SelectedModelType.LOD ? Visibility.Visible : Visibility.Collapsed;
        }

        private void DeleteBtn(object sender, RoutedEventArgs e)
        {
            OnDeleteRequested?.Invoke();
        }
        private void ReplaceBtn(object sender, RoutedEventArgs e)
        {
            OnReplaceRequested?.Invoke();
        }
        private void AddComponentBtn(object sender, RoutedEventArgs e)
        {
            OnAddRequested?.Invoke(SelectedModelType.COMPONENT);
        }
        private void AddLODBtn(object sender, RoutedEventArgs e)
        {
            OnAddRequested?.Invoke(SelectedModelType.LOD);
        }
        private void AddSubmeshBtn(object sender, RoutedEventArgs e)
        {
            OnAddRequested?.Invoke(SelectedModelType.SUBMESH);
        }
        private void EditMaterialBtn(object sender, RoutedEventArgs e)
        {
            OnEditMaterialRequested?.Invoke();
        }

        private void OnRenderMaterialsChecked(object sender, RoutedEventArgs e)
        {
            OnMaterialRenderCheckChanged?.Invoke(renderMaterials.IsChecked == true);
        }

        private void scaleFactor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (scaleFactor.Text == "-1") scaleFactor.Text = "0";
            scaleFactor.Text = scaleFactor.Text.ForceStringNumeric();
            OnScaleFactorChanged?.Invoke(Convert.ToInt32(scaleFactor.Text));
        }
    }

    public enum SelectedModelType
    {
        NONE,
        CS2,
        COMPONENT,
        LOD,
        SUBMESH
    }
}
