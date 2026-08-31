using AlienPAK;
using CATHODE;
using CATHODE.ShaderTypes;
using CathodeLib;
using CathodeLib.Ubershaders;
using CathodeLib.ObjectExtensions;
using OpenCAGE.Modding;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using WpfBorder = System.Windows.Controls.Border;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfGroupBox = System.Windows.Controls.GroupBox;
using WpfImage = System.Windows.Controls.Image;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfScrollViewer = System.Windows.Controls.ScrollViewer;
using WpfStackPanel = System.Windows.Controls.StackPanel;
using WpfTabItem = System.Windows.Controls.TabItem;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfTextBox = System.Windows.Controls.TextBox;
using System.Windows.Media;
using static CATHODE.Materials.Material;

namespace OpenCAGE
{
    public partial class EditMaterial : BaseWindow
    {
        List<Materials.Material> _sortedMaterials = new List<Materials.Material>();

        //Sampler information: (sampler name, sampler index, texture reference index)
        List<Tuple<string, int, int>> _samplerInfo = new List<Tuple<string, int, int>>();

        MaterialInfoWPF _controls = null;

        public Action<Materials.Material> OnMaterialSelected;

        public EditMaterial(Materials.Material material = null, bool showSelectBtn = true) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();

            this.Text = "Material Editor";

            _controls = (MaterialInfoWPF)elementHost1.Child;
            _controls.OnSamplerSelected += OnSamplerSelected;
            _controls.OnPickTexture += OnPickTexture;

            PopulateUI(material);

            selectMaterialBtn.Visible = showSelectBtn;
        }

        private void EditMaterial_Load(object sender, EventArgs e)
        {
            if (materialList.SelectedItems.Count > 0)
            {
                materialList.SelectedItems[0].EnsureVisible();
                materialList.EnsureVisible(materialList.SelectedItems[0].Index);
            }
        }

        private void OnSamplerSelected(int samplerTabIndex)
        {
        }

        private void OnPickTexture()
        {
            if (materialList.SelectedItems.Count == 0 || _controls.SamplerTabControl.SelectedIndex < 0)
                return;

            Materials.Material material = materialList.SelectedItems[0].Tag as Materials.Material;
            if (material == null)
                return;
            int samplerTabIndex = _controls.SamplerTabControl.SelectedIndex;
            if (samplerTabIndex >= _samplerInfo.Count)
                return;

            var samplerInfo = _samplerInfo[samplerTabIndex];
            int samplerIndex = samplerInfo.Item2;
            int textureRefIndex = samplerInfo.Item3;

            Textures levelTex = Content.Level.Textures;
            Textures globalTex = Singleton.Global?.Textures;

            if (levelTex == null && globalTex == null)
            {
                System.Windows.Forms.MessageBox.Show("No texture data is loaded for this material.", "Cannot pick texture", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Textures.TEX4 currentTexture = null;
            if (textureRefIndex != 255 && textureRefIndex < material.TextureReferences.Count)
            {
                var textureRef = material.TextureReferences[textureRefIndex];
                currentTexture = textureRef?.Texture;
            }

            Textures.TEX4 chosenTexture = null;
            using (var textureEditor = new EditTexture(currentTexture, showSelectBtn: true))
            {
                void OnChosen(Textures.TEX4 tex) { chosenTexture = tex; }
                textureEditor.OnTextureSelected += OnChosen;
                textureEditor.ShowDialog(this);
                textureEditor.OnTextureSelected -= OnChosen;
            }

            if (chosenTexture == null)
                return;

            if (textureRefIndex != 255 && textureRefIndex < material.TextureReferences.Count)
            {
                var textureRef = material.TextureReferences[textureRefIndex];
                textureRef.Texture = chosenTexture;
                textureRef.Location = TexturePtr.Source.LEVEL;
            }
            else
            {
                if (material.Shader.SamplerRemaps.Count <= samplerIndex)
                {
                    while (material.Shader.SamplerRemaps.Count <= samplerIndex)
                        material.Shader.SamplerRemaps.Add(255);
                }

                textureRefIndex = material.TextureReferences.Count;
                TexturePtr texturePtr = new TexturePtr
                {
                    Texture = chosenTexture,
                    Location = TexturePtr.Source.LEVEL
                };
                material.TextureReferences.Add(texturePtr);
                material.Shader.SamplerRemaps[samplerIndex] = textureRefIndex;

                _samplerInfo[samplerTabIndex] = new Tuple<string, int, int>(samplerInfo.Item1, samplerIndex, textureRefIndex);
            }

            Singleton.OnResourceModified?.Invoke();
            materialList_SelectedIndexChanged(null, EventArgs.Empty);
        }

        private void ClearSamplerTexture()
        {
            if (materialList.SelectedItems.Count == 0 || _controls.SamplerTabControl.SelectedIndex < 0)
                return;

            Materials.Material material = materialList.SelectedItems[0].Tag as Materials.Material;
            if (material == null)
                return;
            int samplerTabIndex = _controls.SamplerTabControl.SelectedIndex;
            if (samplerTabIndex >= _samplerInfo.Count)
                return;

            var samplerInfo = _samplerInfo[samplerTabIndex];
            int samplerIndex = samplerInfo.Item2;
            int textureRefIndex = samplerInfo.Item3;

            if (textureRefIndex != 255 && textureRefIndex < material.TextureReferences.Count)
            {
                var textureRef = material.TextureReferences[textureRefIndex];
                textureRef.Texture = null;
            }

            if (samplerIndex < material.Shader.SamplerRemaps.Count)
                material.Shader.SamplerRemaps[samplerIndex] = 255;

            _samplerInfo[samplerTabIndex] = new Tuple<string, int, int>(samplerInfo.Item1, samplerIndex, 255);

            Singleton.OnResourceModified?.Invoke();
            materialList_SelectedIndexChanged(null, EventArgs.Empty);
        }

        private bool _suppressFeatureEvents = false;

        /* Toggling a feature means binding the material to a DIFFERENT shader permutation - the
         * flags on a shader entry describe its bytecode and can't just be edited in place. Resolve
         * the new mask (level pool -> database -> recompile), rebind, and carry the material's constants 
         * over to the new entry's slot layout. */
        private void OnFeatureCheckboxChanged(Materials.Material material, int featureIndex, bool isChecked, WpfCheckBox checkBox)
        {
            if (_suppressFeatureEvents)
                return;

            Shaders.Shader oldShader = material.Shader;
            long newMask = isChecked
                ? oldShader.UbershaderFeatureFlags | (1L << featureIndex)
                : oldShader.UbershaderFeatureFlags & ~(1L << featureIndex);
            if (newMask == oldShader.UbershaderFeatureFlags)
                return;

            PermutationSource source;
            string error;
            Shaders.Shader newShader = ShaderPermutationService.Resolve(
                Content.Level.Shaders, oldShader.Ubershader, newMask, oldShader, Singleton.PathToAI, out source, out error);

            if (newShader == null)
            {
                _suppressFeatureEvents = true;
                checkBox.IsChecked = !isChecked;
                _suppressFeatureEvents = false;
                System.Windows.Forms.MessageBox.Show(
                    error ?? "This feature combination isn't available.",
                    "Cannot change feature", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!ReferenceEquals(newShader, oldShader))
            {
                ShaderPermutationService.MigrateConstants(material, oldShader, newShader);
                material.Shader = newShader;
            }

            Singleton.OnResourceModified?.Invoke();
            materialList_SelectedIndexChanged(null, EventArgs.Empty);
        }

        private static bool IsColorLikeParameter(string parameterName, UberShaderParameterType parameterType)
        {
            return (parameterType == UberShaderParameterType.Float3 || parameterType == UberShaderParameterType.Float4 ||
                    parameterType == UberShaderParameterType.Half3 || parameterType == UberShaderParameterType.Half4) &&
                   (parameterName.IndexOf("color", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    parameterName.IndexOf("colour", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    parameterName.IndexOf("tint", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string[] GetComponentLabels(int floatCount, bool isColorLike)
        {
            if (isColorLike)
            {
                switch (floatCount)
                {
                    case 2: return new[] { "R:", "G:" };
                    case 3: return new[] { "R:", "G:", "B:" };
                    case 4: return new[] { "R:", "G:", "B:", "A:" };
                }
            }

            switch (floatCount)
            {
                case 2: return new[] { "X:", "Y:" };
                case 3: return new[] { "X:", "Y:", "Z:" };
                case 4: return new[] { "X:", "Y:", "Z:", "W:" };
                default: return new[] { "Value:" };
            }
        }

        private static byte ClampColorByte(float value) =>
            (byte)Math.Max(0, Math.Min(255, (int)Math.Round(value * 255f)));

        private static Color GetWpfColorFromConstants(Materials.Material material, int remappedIndex, int floatCount)
        {
            float r = remappedIndex + 0 < material.PixelShaderConstants.Count ? material.PixelShaderConstants[remappedIndex + 0] : 0f;
            float g = remappedIndex + 1 < material.PixelShaderConstants.Count ? material.PixelShaderConstants[remappedIndex + 1] : 0f;
            float b = remappedIndex + 2 < material.PixelShaderConstants.Count ? material.PixelShaderConstants[remappedIndex + 2] : 0f;
            float a = floatCount >= 4 && remappedIndex + 3 < material.PixelShaderConstants.Count
                ? material.PixelShaderConstants[remappedIndex + 3]
                : 1f;
            return Color.FromArgb(ClampColorByte(a), ClampColorByte(r), ClampColorByte(g), ClampColorByte(b));
        }

        private WpfGroupBox BuildParameterGroupBox(Materials.Material material, string parameterName)
        {
            int parameterIndex = ShaderUtility.GetShaderFunctionalityIndex(material.Shader.Ubershader, ShaderIndexType.PARAMETERS, parameterName).Value;
            UberShaderParameterType parameterType = ShaderUtility.GetParameterType(material.Shader.Ubershader, parameterName).Value;
            int remappedIndex = material.Shader.PixelShaderParameterRemaps[parameterIndex];
            int floatCount = GetFloatCountForParameterType(parameterType);

            bool isInt = parameterType == UberShaderParameterType.Int;
            bool isColorLike = IsColorLikeParameter(parameterName, parameterType);

            var content = new WpfStackPanel
            {
                Margin = new Thickness(4)
            };

            var row = new WpfStackPanel
            {
                Orientation = WpfOrientation.Horizontal
            };

            WpfBorder colorPreview = null;
            Action updateColorPreview = null;
            if (isColorLike)
            {
                colorPreview = new WpfBorder
                {
                    Width = 36,
                    Height = 22,
                    Margin = new Thickness(0, 0, 12, 0),
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(3),
                    VerticalAlignment = VerticalAlignment.Center,
                    ToolTip = "Color preview"
                };
                updateColorPreview = () => colorPreview.Background = new SolidColorBrush(GetWpfColorFromConstants(material, remappedIndex, floatCount));
                updateColorPreview();
                row.Children.Add(colorPreview);
            }

            Func<float, string> toString = v => v.ToString("F6", CultureInfo.InvariantCulture);

            Action<WpfTextBox, int> attachFloatHandler = (box, componentOffset) =>
            {
                box.LostKeyboardFocus += (s, eArgs) =>
                {
                    if (remappedIndex + componentOffset >= material.PixelShaderConstants.Count)
                        return;

                    float current = material.PixelShaderConstants[remappedIndex + componentOffset];

                    if (isInt)
                    {
                        if (int.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intVal))
                        {
                            material.PixelShaderConstants[remappedIndex + componentOffset] = intVal;
                            box.Text = intVal.ToString(CultureInfo.InvariantCulture);
                        }
                        else
                            box.Text = ((int)current).ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        if (float.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
                        {
                            material.PixelShaderConstants[remappedIndex + componentOffset] = val;
                            box.Text = toString(val);
                        }
                        else
                            box.Text = toString(current);
                    }
                    updateColorPreview?.Invoke();
                    Singleton.OnResourceModified?.Invoke();
                };
            };

            if (floatCount == 1)
            {
                float v = remappedIndex < material.PixelShaderConstants.Count ? material.PixelShaderConstants[remappedIndex] : 0f;

                var label = new WpfTextBlock
                {
                    Text = isInt ? "Value (int):" : "Value:",
                    Margin = new Thickness(0, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                row.Children.Add(label);

                var box = new WpfTextBox
                {
                    Width = 100,
                    Text = isInt ? ((int)v).ToString(CultureInfo.InvariantCulture) : toString(v)
                };
                attachFloatHandler(box, 0);
                row.Children.Add(box);
            }
            else
            {
                string[] labels = GetComponentLabels(floatCount, isColorLike);

                for (int i = 0; i < floatCount; i++)
                {
                    float v = (remappedIndex + i) < material.PixelShaderConstants.Count
                        ? material.PixelShaderConstants[remappedIndex + i]
                        : 0f;

                    var label = new WpfTextBlock
                    {
                        Text = labels[i],
                        Margin = new Thickness((i == 0 && colorPreview == null) ? 0 : 10, 0, 5, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    row.Children.Add(label);

                    var box = new WpfTextBox
                    {
                        Width = isColorLike ? 52 : 80,
                        Text = isInt ? ((int)v).ToString(CultureInfo.InvariantCulture) : toString(v)
                    };
                    attachFloatHandler(box, i);
                    row.Children.Add(box);
                }
            }

            content.Children.Add(row);

            if (isColorLike)
            {
                var colorActionsRow = new WpfStackPanel
                {
                    Orientation = WpfOrientation.Horizontal,
                    Margin = new Thickness(0, 8, 0, 0)
                };

                var colorButton = new WpfButton
                {
                    Content = "Pick Color...",
                    Width = 100
                };

                colorButton.Click += (s, eArgs) =>
                {
                    Color currentColor = GetWpfColorFromConstants(material, remappedIndex, floatCount);

                    using (var dialog = new ColorDialog())
                    {
                        dialog.Color = System.Drawing.Color.FromArgb(
                            currentColor.A,
                            currentColor.R,
                            currentColor.G,
                            currentColor.B);

                        if (dialog.ShowDialog() != DialogResult.OK)
                            return;

                        float fromByte(byte c) => c / 255f;

                        if (remappedIndex + 0 < material.PixelShaderConstants.Count)
                            material.PixelShaderConstants[remappedIndex + 0] = fromByte(dialog.Color.R);
                        if (remappedIndex + 1 < material.PixelShaderConstants.Count)
                            material.PixelShaderConstants[remappedIndex + 1] = fromByte(dialog.Color.G);
                        if (remappedIndex + 2 < material.PixelShaderConstants.Count)
                            material.PixelShaderConstants[remappedIndex + 2] = fromByte(dialog.Color.B);
                        if (floatCount >= 4 && remappedIndex + 3 < material.PixelShaderConstants.Count)
                            material.PixelShaderConstants[remappedIndex + 3] = fromByte(dialog.Color.A);

                        Singleton.OnResourceModified?.Invoke();
                        RefreshParameterGroupBox(material, parameterName);
                    }
                };

                colorActionsRow.Children.Add(colorButton);
                content.Children.Add(colorActionsRow);
            }

            return new WpfGroupBox
            {
                Header = $"{parameterName} ({parameterType})",
                Tag = parameterName,
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10, 8, 10, 8),
                Content = content
            };
        }

        private void RefreshParameterGroupBox(Materials.Material material, string parameterName)
        {
            for (int i = 0; i < _controls.ParametersPanel.Children.Count; i++)
            {
                if (_controls.ParametersPanel.Children[i] is WpfGroupBox groupBox && groupBox.Tag as string == parameterName)
                {
                    _controls.ParametersPanel.Children.RemoveAt(i);
                    _controls.ParametersPanel.Children.Insert(i, BuildParameterGroupBox(material, parameterName));
                    return;
                }
            }
        }

        private int GetFloatCountForParameterType(UberShaderParameterType parameterType)
        {
            switch (parameterType)
            {
                case UberShaderParameterType.Float:
                case UberShaderParameterType.Half:
                case UberShaderParameterType.Int:
                    return 1;
                case UberShaderParameterType.Float2:
                case UberShaderParameterType.Half2:
                    return 2;
                case UberShaderParameterType.Float3:
                case UberShaderParameterType.Half3:
                    return 3;
                case UberShaderParameterType.Float4:
                case UberShaderParameterType.Half4:
                    return 4;
                default:
                    return 1;
            }
        }

        private void PopulateUI(Materials.Material material = null, string filter = null)
        {
            _sortedMaterials.Clear();
            IEnumerable<Materials.Material> source = Content.Level.Materials.Entries;

            Dictionary<Materials.Material, string> materialNames = new Dictionary<Materials.Material, string>();
            foreach (Materials.Material mat in Content.Level.Materials.Entries)
            {
                materialNames.Add(mat, Content.Level.Materials.GetMaterialName(mat));
            }

            if (!string.IsNullOrWhiteSpace(filter))
            {
                string trimmedFilter = filter.Trim();
                source = source.Where(m => materialNames[m].IndexOf(trimmedFilter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            _sortedMaterials.AddRange(source);
            _sortedMaterials = _sortedMaterials.OrderBy(o => materialNames[o]).ToList();

            materialList.BeginUpdate();
            materialList.Items.Clear();
            materialList.Groups.Clear();
            materialList.Columns.Clear();

            materialList.Columns.Add("Material Name", 360);

            var groupedMaterials = _sortedMaterials.Where(m => m.Shader != null).GroupBy(m => m.Shader.Ubershader).OrderBy(g => g.Key.ToString());

            _controls.Visibility = System.Windows.Visibility.Hidden;
            foreach (var group in groupedMaterials)
            {
                string groupName = group.Key.ToString();
                var listGroup = new System.Windows.Forms.ListViewGroup(groupName, groupName);
                materialList.Groups.Add(listGroup);

                foreach (var mat in group.OrderBy(m => materialNames[m]))
                {
                    var item = new System.Windows.Forms.ListViewItem(materialNames[mat]);
                    item.Group = listGroup;
                    item.Tag = mat;
                    materialList.Items.Add(item);

                    if (mat == material)
                    {
                        item.Selected = true;
                        _controls.Visibility = System.Windows.Visibility.Visible;
                    }
                }
            }
            materialList.EndUpdate();
        }

        private void ApplyMaterialSearch()
        {
            string filter = materialSearchTextBox.Text;

            Materials.Material selectedMaterial = null;
            if (materialList.SelectedItems.Count > 0)
                selectedMaterial = materialList.SelectedItems[0].Tag as Materials.Material;

            PopulateUI(selectedMaterial, filter);
        }

        private void materialSearchButton_Click(object sender, EventArgs e)
        {
            ApplyMaterialSearch();
        }

        private void materialSearchClearButton_Click(object sender, EventArgs e)
        {
            Materials.Material selectedMaterial = null;
            if (materialList.SelectedItems.Count > 0)
                selectedMaterial = materialList.SelectedItems[0].Tag as Materials.Material;

            materialSearchTextBox.Text = string.Empty;
            PopulateUI(selectedMaterial);
        }

        private void materialSearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                materialSearchButton.PerformClick();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void materialList_SelectedIndexChanged(object sender, EventArgs e)
        {
            _controls.SamplerTabControl.Items.Clear();
            _samplerInfo.Clear();
            _controls.ParametersPanel.Children.Clear();
            _controls.FeatureDetailsPanel.Children.Clear();
            _controls.MaterialNameText.Text = "";
            _controls.ShaderUbershaderText.Text = "";

            _controls.Visibility = System.Windows.Visibility.Hidden;
            if (materialList.SelectedItems.Count == 0)
                return;
            _controls.Visibility = System.Windows.Visibility.Visible;

            Materials.Material material = materialList.SelectedItems[0].Tag as Materials.Material;
            if (material == null || material.Shader == null)
                return;

            _controls.MaterialNameText.Text = Content.Level.Materials.GetMaterialName(material);
            _controls.ShaderUbershaderText.Text = material.Shader.Ubershader.ToString();

            List<string> samplers = ShaderUtility.GetSamplers(material.Shader.Ubershader);
            int firstSamplerWithTextureIndex = -1;
            for (int i = 0; i < samplers.Count; i++)
            {
                string sampler = samplers[i];
                int? samplerIndexNullable = ShaderUtility.GetShaderFunctionalityIndex(material.Shader.Ubershader, ShaderIndexType.SAMPLERS, sampler);
                if (!samplerIndexNullable.HasValue)
                    continue;

                int samplerIndex = samplerIndexNullable.Value;
                int textureRefIndex = 255;
                if (samplerIndex < material.Shader.SamplerRemaps.Count)
                    textureRefIndex = material.Shader.SamplerRemaps[samplerIndex];

                bool hasTexture = false;
                Textures.TEX4 texture = null;
                if (textureRefIndex != 255 && textureRefIndex < material.TextureReferences.Count)
                {
                    var textureRef = material.TextureReferences[textureRefIndex];
                    if (textureRef?.Texture != null)
                    {
                        hasTexture = true;
                        texture = textureRef.Texture;
                    }
                }

                if (hasTexture && firstSamplerWithTextureIndex == -1)
                    firstSamplerWithTextureIndex = _controls.SamplerTabControl.Items.Count;

                var tabHeader = new WpfTextBlock
                {
                    Text = sampler,
                    FontWeight = hasTexture ? FontWeights.Bold : FontWeights.Normal
                };

                var tabContent = new WpfStackPanel { Margin = new System.Windows.Thickness(10) };

                var textureFileText = new WpfTextBlock
                {
                    Text = $"Sampler: {sampler}",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new System.Windows.Thickness(0, 0, 0, 10)
                };

                var imageScrollViewer = new WpfScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                var texturePreview = new WpfImage
                {
                    Source = texture?.ToBitmap()?.ToImageSource(),
                    Stretch = Stretch.Uniform,
                    MaxHeight = 400
                };
                imageScrollViewer.Content = texturePreview;

                var samplerButtonsPanel = new WpfStackPanel
                {
                    Orientation = WpfOrientation.Horizontal,
                    Margin = new System.Windows.Thickness(0, 10, 0, 0)
                };

                var pickTextureButton = new WpfButton
                {
                    Content = hasTexture ? "Edit Texture..." : "Pick Texture...",
                    Margin = new System.Windows.Thickness(0, 0, 10, 0)
                };
                pickTextureButton.Click += (s, args) => OnPickTexture();

                var clearTextureButton = new WpfButton
                {
                    Content = "Clear Texture",
                    IsEnabled = textureRefIndex != 255,
                    Margin = new System.Windows.Thickness(0, 0, 0, 0)
                };
                clearTextureButton.Click += (s, args) => ClearSamplerTexture();

                samplerButtonsPanel.Children.Add(pickTextureButton);
                samplerButtonsPanel.Children.Add(clearTextureButton);

                var detailsText = new WpfTextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new System.Windows.Thickness(0, 10, 0, 0)
                };

                var details = new StringBuilder();
                details.AppendLine($"Sampler Name: {sampler}");
                details.AppendLine($"Sampler Index: {samplerIndex}");
                details.AppendLine($"Texture Reference Index: {(textureRefIndex == 255 ? "Not assigned" : textureRefIndex.ToString())}");

                if (hasTexture && texture != null)
                {
                    textureFileText.Text += $"\nTexture: {texture.Name}";
                    details.AppendLine($"Texture Name: {texture.Name}");
                    details.AppendLine($"Texture Format: {texture.Format}");
                }
                else if (textureRefIndex != 255 && textureRefIndex < material.TextureReferences.Count)
                {
                    textureFileText.Text += "\nTexture: (Empty slot)";
                    details.AppendLine("Texture: Empty slot (no texture assigned)");
                }
                else
                {
                    textureFileText.Text += "\nTexture: (Not assigned)";
                    details.AppendLine("Texture: Not assigned to this sampler");
                }

                detailsText.Text = details.ToString();

                tabContent.Children.Add(textureFileText);
                tabContent.Children.Add(imageScrollViewer);
                tabContent.Children.Add(samplerButtonsPanel);
                tabContent.Children.Add(detailsText);

                var tabItem = new WpfTabItem
                {
                    Header = tabHeader,
                    Content = tabContent
                };

                _controls.SamplerTabControl.Items.Add(tabItem);
                _samplerInfo.Add(new Tuple<string, int, int>(sampler, samplerIndex, textureRefIndex));
            }
            if (_controls.SamplerTabControl.Items.Count != 0)
            {
                int selectedIndex = firstSamplerWithTextureIndex >= 0 ? firstSamplerWithTextureIndex : 0;
                _controls.SamplerTabControl.SelectedIndex = selectedIndex;
            }

            List<string> features = ShaderUtility.GetFeatures(material.Shader.Ubershader);
            List<Tuple<string, int>> featureBits = new List<Tuple<string, int>>();
            foreach (string feature in features)
            {
                int? featureIndexNullable = ShaderUtility.GetShaderFunctionalityIndex(material.Shader.Ubershader, ShaderIndexType.FEATURES, feature);
                if (featureIndexNullable.HasValue)
                    featureBits.Add(new Tuple<string, int>(feature, featureIndexNullable.Value));
            }

            _controls.FeatureDetailsPanel.Children.Add(BuildFeatureHeader(material));

            /* Families CathodeLib ships no reconstructed master for cannot have a permutation
             * compiled for them, so their features are not free to tick - the only masks obtainable
             * are the ones the game already shipped. Show them read-only and send the user to the
             * combination picker instead. The test is derived from the shipped table, so removing a
             * family from it moves that family onto this path with no list here to update. */
            if (!ShaderPermutationService.CanBuildArbitraryPermutations(material.Shader.Ubershader))
            {
                BuildFixedFeatureList(material, featureBits);
            }
            else
            {
                Dictionary<int, PermutationSource> availability = ShaderPermutationService.AvailabilityForToggles(
                    Content.Level.Shaders, material.Shader.Ubershader, material.Shader.UbershaderFeatureFlags,
                    Singleton.PathToAI, featureBits.Select(o => o.Item2));

                //A feature we can't switch is not shown at all. Leaving a dead checkbox on screen only
                //invites someone to click it and wonder why nothing happened.
                int hiddenFeatures = 0;

                foreach (Tuple<string, int> featureBit in featureBits)
                {
                    string feature = featureBit.Item1;
                    int featureIndex = featureBit.Item2;
                    bool isEnabled = (material.Shader.UbershaderFeatureFlags & (1L << featureIndex)) != 0;

                    PermutationSource source = availability[featureIndex];
                    if (source == PermutationSource.Unimplemented || source == PermutationSource.None)
                    {
                        hiddenFeatures++;
                        continue;
                    }

                    var checkBox = new WpfCheckBox
                    {
                        Content = feature,
                        IsChecked = isEnabled,
                        Margin = new System.Windows.Thickness(0, 0, 0, 5)
                    };

                    switch (source)
                    {
                        case PermutationSource.LevelPool:
                            checkBox.ToolTip = (isEnabled ? "Disabling" : "Enabling") + " this uses a permutation already shipped in this level.";
                            break;
                        case PermutationSource.Database:
                            checkBox.ToolTip = (isEnabled ? "Disabling" : "Enabling") + " this pulls a permutation harvested from your game data.";
                            break;
                        case PermutationSource.Recompiled:
                            checkBox.ToolTip = (isEnabled ? "Disabling" : "Enabling") + " this creates a permutation reconstructed via CathodeLib.";
                            break;
                    }

                    checkBox.Checked += (s, args) => OnFeatureCheckboxChanged(material, featureIndex, true, checkBox);
                    checkBox.Unchecked += (s, args) => OnFeatureCheckboxChanged(material, featureIndex, false, checkBox);

                    _controls.FeatureDetailsPanel.Children.Add(checkBox);
                }

                if (hiddenFeatures != 0)
                {
                    var hiddenNote = new WpfTextBlock
                    {
                        Text = hiddenFeatures + (hiddenFeatures == 1 ? " feature is" : " features are") + " not listed: nothing in your game data uses "
                            + (hiddenFeatures == 1 ? "it" : "them") + ", so there is no shader to switch to."
                            + (ShaderDatabase.IsBuilt(Singleton.PathToAI) ? "" : " More may appear once the shader harvest finishes."),
                        TextWrapping = TextWrapping.Wrap,
                        FontStyle = FontStyles.Italic,
                        Foreground = System.Windows.Media.Brushes.Gray,
                        Margin = new System.Windows.Thickness(0, 6, 0, 0)
                    };
                    _controls.FeatureDetailsPanel.Children.Add(hiddenNote);
                }
            }

            List<string> parameters = ShaderUtility.GetParameters(material.Shader.Ubershader);
            foreach (string parameter in parameters)
            {
                int parameterIndex = ShaderUtility.GetShaderFunctionalityIndex(material.Shader.Ubershader, ShaderIndexType.PARAMETERS, parameter).Value;
                if (parameterIndex >= material.Shader.PixelShaderParameterRemaps.Count)
                    continue;

                int remappedIndex = material.Shader.PixelShaderParameterRemaps[parameterIndex];
                if (remappedIndex != 255 && remappedIndex < material.PixelShaderConstants.Count)
                    _controls.ParametersPanel.Children.Add(BuildParameterGroupBox(material, parameter));
            }
        }

        /* Two different things put a family on the read-only path and they want different wording:
         * CathodeLib ships nothing for it (the usual case - the table carries the instancing families only), 
         * or it does but there is no compiler on this machine to use it with (in which case nothing is 
         * recompilable and saying otherwise would send them looking for a shader problem that isn't there). */
        internal static string FixedFeatureReason(SHADER_LIST family)
        {
            if (!UberShaderRecompiler.HasMaster(family))
                return "OpenCAGE doesn't carry content for this shader type,";
            return "No HLSL compiler (d3dcompiler_43.dll) was found on this machine,";
        }

        /* The read-only half of the feature UI. Every feature is listed - including the ones no
         * shipped permutation ever turns on - because they are describing the shader rather than
         * offering a switch, and a feature silently missing from a read-only list is just confusing.
         * Changing anything goes through the combination picker. */
        private void BuildFixedFeatureList(Materials.Material material, List<Tuple<string, int>> featureBits)
        {
            List<ShaderPermutationService.Permutation> permutations = ShaderPermutationService.AvailablePermutations(
                Content.Level.Materials, Content.Level.Shaders, material.Shader.Ubershader, Singleton.PathToAI);

            var note = new WpfTextBlock
            {
                Text = FixedFeatureReason(material.Shader.Ubershader)
                     + " so its features can only be set to a combination your game data already ships. "
                     + permutations.Count
                     + (permutations.Count == 1 ? " combination is" : " combinations are") + " available."
                     + (ShaderDatabase.IsBuilt(Singleton.PathToAI) ? "" : " More may appear once the shader harvest finishes."),
                TextWrapping = TextWrapping.Wrap,
                FontStyle = FontStyles.Italic,
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new System.Windows.Thickness(0, 0, 0, 8)
            };
            _controls.FeatureDetailsPanel.Children.Add(note);

            var change = new WpfButton
            {
                Content = "Change combination...",
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Padding = new System.Windows.Thickness(10, 3, 10, 3),
                Margin = new System.Windows.Thickness(0, 0, 0, 10),
                IsEnabled = permutations.Count > 1,
                ToolTip = permutations.Count > 1
                    ? "Pick from the feature combinations shipped for this shader type."
                    : "Your game data only ships one combination for this shader type, so there is nothing to switch to."
            };
            change.Click += (s, args) => ChoosePermutation(material, permutations);
            _controls.FeatureDetailsPanel.Children.Add(change);

            foreach (Tuple<string, int> featureBit in featureBits)
            {
                _controls.FeatureDetailsPanel.Children.Add(new WpfCheckBox
                {
                    Content = featureBit.Item1,
                    IsChecked = (material.Shader.UbershaderFeatureFlags & (1L << featureBit.Item2)) != 0,
                    IsEnabled = false,
                    Margin = new System.Windows.Thickness(0, 0, 0, 5)
                });
            }
        }

        /* Same rebind as a checkbox toggle, just with the whole mask chosen at once. */
        private void ChoosePermutation(Materials.Material material, List<ShaderPermutationService.Permutation> permutations)
        {
            Shaders.Shader oldShader = material.Shader;
            SelectShaderPermutation picker = new SelectShaderPermutation(
                oldShader.Ubershader, oldShader.UbershaderFeatureFlags, permutations);
            if (picker.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;
            if (picker.Mask == oldShader.UbershaderFeatureFlags)
                return;

            PermutationSource source;
            string error;
            Shaders.Shader newShader = ShaderPermutationService.Resolve(
                Content.Level.Shaders, oldShader.Ubershader, picker.Mask, oldShader, Singleton.PathToAI, out source, out error);

            if (newShader == null)
            {
                System.Windows.Forms.MessageBox.Show(
                    error ?? "That feature combination isn't available.",
                    "Cannot change features", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!ReferenceEquals(newShader, oldShader))
            {
                ShaderPermutationService.MigrateConstants(material, oldShader, newShader);
                material.Shader = newShader;
            }

            Singleton.OnResourceModified?.Invoke();
            materialList_SelectedIndexChanged(null, EventArgs.Empty);
        }

        private WpfStackPanel BuildFeatureHeader(Materials.Material material)
        {
            var panel = new WpfStackPanel { Margin = new System.Windows.Thickness(0, 0, 0, 10) };

            var maskText = new WpfTextBlock
            {
                Text = "Permutation mask: 0x" + material.Shader.UbershaderFeatureFlags.ToString("X"),
                TextWrapping = TextWrapping.Wrap
            };
            panel.Children.Add(maskText);

            /* The database harvests itself in the background on startup, so there is normally nothing
             * to ask for here - just say which permutations are reachable right now. The only case
             * that still needs a control is a harvest that failed, where saying nothing would leave
             * someone wondering why half the combinations are missing. */
            if (!ShaderDatabase.IsBuilt(Singleton.PathToAI))
            {
                ShaderDatabase.AutoBuildState state = ShaderDatabase.AutoBuild;
                string message;
                if (state == ShaderDatabase.AutoBuildState.Running)
                    message = "Harvesting shipped shader permutations in the background"
                            + (string.IsNullOrEmpty(ShaderDatabase.AutoBuildProgress) ? "" : " (" + ShaderDatabase.AutoBuildProgress + ")")
                            + " - until it finishes, only permutations shipped with this level are available. Reopen this window to pick up the rest.";
                else if (state == ShaderDatabase.AutoBuildState.Failed)
                    message = "Couldn't harvest the shipped shader permutations: "
                            + (ShaderDatabase.AutoBuildError ?? "unknown error")
                            + ". Only permutations shipped with this level are available.";
                else
                    message = "Only permutations shipped with this level are available.";

                var note = new WpfTextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    FontStyle = FontStyles.Italic,
                    Margin = new System.Windows.Thickness(0, 6, 0, 6)
                };
                panel.Children.Add(note);

                if (state == ShaderDatabase.AutoBuildState.Failed)
                {
                    var retryButton = new WpfButton
                    {
                        Content = "Try again",
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                        Padding = new System.Windows.Thickness(10, 3, 10, 3),
                        ToolTip = "Re-scan every level's shader data (pristine files only) so any shipped feature combination becomes available here."
                    };
                    retryButton.Click += (s, args) => BuildShaderDatabase();
                    panel.Children.Add(retryButton);
                }
            }

            return panel;
        }

        /* Only reachable from the retry button after a background harvest failed, so this one is
         * modal and does report its result - the user explicitly asked for it this time. */
        private void BuildShaderDatabase()
        {
            ShaderDatabase.ResetAutoBuild();

            ProgressUI progress = new ProgressUI();
            progress.ShowTransferring("Building shader database...");

            string gameRoot = Singleton.PathToAI;
            Task.Run(() => ShaderDatabase.Build(gameRoot, msg =>
            {
                try
                {
                    progress.BeginInvoke(new Action(() => { progress.Text = "Shader database: " + msg; }));
                }
                catch { }
            })).ContinueWith(task =>
            {
                progress.Close();
                ShaderPermutationService.InvalidateDatabase();

                if (task.IsFaulted)
                {
                    System.Windows.Forms.MessageBox.Show(
                        "Failed to build the shader database:\n" + (task.Exception?.GetBaseException()?.Message ?? "unknown error"),
                        "Shader database", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    ShaderDatabase.BuildReport report = task.Result;
                    int totalMasks = report.FamilyMaskCounts.Values.Sum();
                    string summary = "Harvested " + totalMasks + " shader permutations across " + report.FamilyMaskCounts.Count +
                        " shader types, from " + report.LevelsHarvested.Count + " levels.";
                    if (report.LevelsSkipped.Count > 0)
                        summary += "\n\nSkipped " + report.LevelsSkipped.Count + " level(s) with modified or missing shader data:\n - " +
                            string.Join("\n - ", report.LevelsSkipped.Take(10)) +
                            (report.LevelsSkipped.Count > 10 ? "\n   ..." : "");
                    System.Windows.Forms.MessageBox.Show(summary, "Shader database", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                materialList_SelectedIndexChanged(null, EventArgs.Empty);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void selectMaterial_Click(object sender, EventArgs e)
        {
            if (materialList.SelectedItems.Count == 0)
                return;

            Materials.Material material = materialList.SelectedItems[0].Tag as Materials.Material;
            if (material == null)
                return;

            OnMaterialSelected?.Invoke(material);
            Close();
        }

        /* A material from nothing. The shader family is the only thing that can't be changed later -
         * everything else the material needs is reached from the editor once it exists, so the popup
         * asks for that and a name and stops there. */
        private void newMaterial_Click(object sender, EventArgs e)
        {
            List<ShaderPermutationService.Creatable> options =
                ShaderPermutationService.CreatableFamilies(Content.Level.Shaders, Singleton.PathToAI);
            if (options.Count == 0)
            {
                System.Windows.Forms.MessageBox.Show("No shader families are available to build a material on.",
                    "New material", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SHADER_LIST family;
            string name;
            using (NewMaterialPicker picker = new NewMaterialPicker(options))
            {
                if (picker.ShowDialog(this) != DialogResult.OK)
                    return;
                family = picker.Family;
                name = picker.MaterialName;
            }

            string error;
            Materials.Material material = ShaderPermutationService.CreateMaterial(
                Content.Level.Materials, Content.Level.Shaders, family, name, Singleton.PathToAI, out error);
            if (material == null)
            {
                System.Windows.Forms.MessageBox.Show(error ?? "The material couldn't be created.",
                    "New material", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Singleton.OnResourceModified?.Invoke();
            PopulateUI(material);

            foreach (System.Windows.Forms.ListViewItem item in materialList.Items)
            {
                if (ReferenceEquals(item.Tag, material))
                {
                    item.Selected = true;
                    item.EnsureVisible();
                    break;
                }
            }
        }

        private void duplicateMaterial_Click(object sender, EventArgs e)
        {
            if (materialList.SelectedItems.Count == 0)
                return;

            Materials.Material material = materialList.SelectedItems[0].Tag as Materials.Material;
            if (material == null)
                return;

            var newMaterial = new Materials.Material
            {
                Name = Content.Level.Materials.GetMaterialName(material) + " Clone",
                EngineConstants = new List<float>(material.EngineConstants),
                VertexShaderConstants = new List<float>(material.VertexShaderConstants),
                PixelShaderConstants = new List<float>(material.PixelShaderConstants),
                HullShaderConstants = new List<float>(material.HullShaderConstants),
                DomainShaderConstants = new List<float>(material.DomainShaderConstants),
                Shader = material.Shader,
                PhysicalMaterialIndex = material.PhysicalMaterialIndex,
                EnvironmentMapIndex = material.EnvironmentMapIndex,
                Priority = material.Priority
            };

            for (int i = 0; i < material.TextureReferences.Count; i++)
            {
                var texturePtr = new TexturePtr
                {
                    Texture = material.TextureReferences[i].Texture,
                    Location = material.TextureReferences[i].Location
                };
                newMaterial.TextureReferences.Add(texturePtr);
            }

            newMaterial.OfflineLightFeatures = material.OfflineLightFeatures == null
                ? null
                : (Materials.LightFlags)material.OfflineLightFeatures.Copy();

            Content.Level.Materials.Entries.Add(newMaterial);
            Singleton.OnResourceModified?.Invoke();
            PopulateUI(newMaterial);

            foreach (System.Windows.Forms.ListViewItem item in materialList.Items)
            {
                if (ReferenceEquals(item.Tag, newMaterial))
                {
                    item.Selected = true;
                    item.EnsureVisible();
                    break;
                }
            }
        }
    }
}
