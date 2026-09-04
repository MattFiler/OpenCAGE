using CATHODE;
using OpenCAGE;
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

        public Action<float> OnScaleFactorChanged;

        private bool _applyingExternalSettings;

        public ModelEditorControlsWPF()
        {
            InitializeComponent();
            renderMaterials.IsChecked = SettingsManager.GetBool(Settings.ShowTexOpt);
            SettingsManager.SettingsChanged += OnSettingsChanged;
            Unloaded += (s, e) => SettingsManager.SettingsChanged -= OnSettingsChanged;
        }

        private void OnSettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            if (!e.ExternalChange || !SettingsChangedEventArgs.ContainsKey(e.ChangedKeys, Settings.ShowTexOpt))
                return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                _applyingExternalSettings = true;
                try
                {
                    renderMaterials.IsChecked = SettingsManager.GetBool(Settings.ShowTexOpt);
                }
                finally
                {
                    _applyingExternalSettings = false;
                }
            }));
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
            /* The box is a resize factor, not a readout of anything: applying one bakes it into the
             * geometry, so it goes back to 1 rather than showing the submesh's quantisation range. */
            scaleFactorLabel.Visibility = sf != -1 ? Visibility.Visible : Visibility.Collapsed;
            if (sf != -1) scaleFactor.Text = "1";
            scaleFactor.Visibility = scaleFactorLabel.Visibility;
            applyScale.Visibility = scaleFactorLabel.Visibility;
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
            if (_applyingExternalSettings)
                return;

            OnMaterialRenderCheckChanged?.Invoke(renderMaterials.IsChecked == true);
        }

        /* Resizing is destructive - the geometry is rewritten - so it happens on the button rather
         * than on every keystroke, which would apply "1", then "1.", then "1.5" as three resizes. */
        private void ApplyScaleBtn(object sender, RoutedEventArgs e)
        {
            float factor;
            if (!float.TryParse((scaleFactor.Text ?? "").Trim(), System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out factor) || factor <= 0.0f)
            {
                MessageBox.Show("Enter a positive number to resize by - 2 doubles the submesh, 0.5 halves it.",
                    "Resize", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            OnScaleFactorChanged?.Invoke(factor);
            scaleFactor.Text = "1";     //the factor is baked into the geometry now, so we are back at 1:1
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
