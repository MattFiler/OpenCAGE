using AlienPAK;
using Assimp;
using CATHODE;
using CathodeLib;
using CathodeLib.Ubershaders;
using CATHODE.ShaderTypes;
using OpenCAGE;
using OpenCAGE.Popups.UserControls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace AlienPAK
{
    public partial class ModelImportPreview : Form
    {
        private readonly Scene _scene;
        private readonly string _sourceFileName;
        private readonly ModelImportPreviewWPF _previewControl;
        private readonly Materials _materials;
        private readonly Level _level;
        private readonly string _sourceFilePath;
        private readonly ModelIO.ImportPlan _plan;
        private readonly Dictionary<ModelIO.PlannedSubmesh, Materials.Material> _submeshMaterials = new Dictionary<ModelIO.PlannedSubmesh, Materials.Material>();

        /* Materials to build from the model itself, keyed by the assimp material index - one CATHODE
         * material per model material, however many submeshes draw with it. Carried out at Import. */
        private readonly Dictionary<int, MaterialGenerator.Plan> _generatedPlans = new Dictionary<int, MaterialGenerator.Plan>();

        public Models.CS2 ResultCs2 { get; private set; }

        private AssetNameBox _nameBox;
        private TextBox _scaleBox;

        public ModelImportPreview(Scene scene, string sourceFilePath, Materials materials = null,
                                  Func<IEnumerable<string>> takenNames = null, Level level = null)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            _sourceFilePath = sourceFilePath;
            _sourceFileName = Path.GetFileNameWithoutExtension(sourceFilePath ?? "");
            _materials = materials;
            _level = level;
            InitializeComponent();
            OpenCAGE.Theming.ThemeManager.ApplyToForm(this);
            Icon = SharedFormIcon.Icon;

            ModelIO.ModelMetadata metadata = ModelIO.TryLoadSidecar(sourceFilePath);
            _plan = ModelIO.CreateImportPlan(_scene, metadata, _sourceFileName);
            MatchMaterialsFromMetadata();
            PlanGeneratedMaterials();

            hierarchyTree.CheckBoxes = true;
            _previewControl = (ModelImportPreviewWPF)previewHost.Child;
            BuildStructureTree();
            hierarchyTree.ExpandAll();
            UpdateStatusLabel();
            UpdatePreviewFromSelection();
            UpdatePickMaterialButton();
            hierarchyTree.AfterSelect += (s, e) => { UpdatePreviewFromSelection(); UpdatePickMaterialButton(); };
            hierarchyTree.AfterCheck += HierarchyTree_AfterCheck;
            importBtn.Click += ImportBtn_Click;
            cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            pickMaterialBtn.Click += PickMaterialBtn_Click;
            generateMaterialBtn.Click += GenerateMaterialBtn_Click;
            this.Text = "Import model: " + (_sourceFileName ?? "Model");

            if (takenNames != null) BuildNameRow(takenNames);
        }

        /// <summary>
        /// The name to store the model under. Folders are typed straight into it - the model list is
        /// flat and the browser builds its tree by splitting names apart - so this is also how a new
        /// folder gets made. The clash is checked as it's typed rather than sprung afterwards.
        /// </summary>
        private void BuildNameRow(Func<IEnumerable<string>> takenNames)
        {
            Panel row = new Panel { Dock = DockStyle.Top, Height = 96, Padding = new Padding(8, 0, 8, 8) };

            _nameBox = new AssetNameBox { Dock = DockStyle.Top, Height = 44 };
            _nameBox.Bind(_plan.Name, takenNames);
            _nameBox.ValidityChanged += (s, e) => importBtn.Enabled = _nameBox.IsValid;

            row.Controls.Add(BuildScaleRow());
            row.Controls.Add(_nameBox);
            row.Controls.Add(new Label { Dock = DockStyle.Top, Height = 16, Text = "Name (use \\ for folders):" });

            /* Sits directly under the status line: docking is applied from the highest child index
             * down, so taking that index puts this immediately after it. */
            Controls.Add(row);
            Controls.SetChildIndex(row, Controls.GetChildIndex(statusLabel));

            importBtn.Enabled = _nameBox.IsValid;
        }

        /// <summary>
        /// Resize the model on the way in. This is a real resize - it is baked into the vertex
        /// positions before they are quantised, so any fraction works and nothing is thrown away. A
        /// submesh's own VertexScale can only be a whole number, because the file stores it as one.
        /// </summary>
        private Panel BuildScaleRow()
        {
            Panel row = new Panel { Dock = DockStyle.Top, Height = 30 };

            Label label = new Label { AutoSize = true, Location = new System.Drawing.Point(0, 6), Text = "Scale:" };
            _scaleBox = new TextBox { Location = new System.Drawing.Point(50, 3), Width = 70, Text = "1" };
            Label note = new Label { AutoSize = true, Location = new System.Drawing.Point(130, 6), ForeColor = SystemColors.GrayText };

            bool skinned = _plan.AllSubmeshes().Any(o => o.MeshIndex < _scene.MeshCount && _scene.Meshes[o.MeshIndex].HasBones);
            note.Text = skinned
                ? "the mesh is skinned, so resizing it will pull it away from its skeleton"
                : "1 keeps the model the size it was authored";

            _scaleBox.TextChanged += (s, e) =>
            {
                _plan.Scale = ParseScale(_scaleBox.Text);
                _scaleBox.ForeColor = _plan.Scale > 0 ? SystemColors.WindowText : System.Drawing.Color.Firebrick;
                UpdatePreviewFromSelection();
            };

            row.Controls.Add(label);
            row.Controls.Add(_scaleBox);
            row.Controls.Add(note);
            return row;
        }

        /* An empty or half-typed box means "no change" rather than an error dialog on every keystroke */
        private static float ParseScale(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 1.0f;
            if (!float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value)) return -1.0f;
            return value > 0.0f ? value : -1.0f;
        }

        /* An export knows which material each submesh used, so pre-select it if this level still has one by that name */
        private void MatchMaterialsFromMetadata()
        {
            if (_materials == null) return;
            foreach (ModelIO.PlannedSubmesh submesh in _plan.AllSubmeshes())
            {
                string name = submesh.Metadata?.Material;
                if (string.IsNullOrEmpty(name)) continue;

                Materials.Material material = _materials.Entries.FirstOrDefault(x => x.Name == name);
                if (material != null) _submeshMaterials[submesh] = material;
            }
        }

        /* Any submesh that didn't come back to a material of its own gets one built from the model's
         * own textures - which is what a model imported from anywhere but OpenCAGE always needs.
         *
         * Only the PLAN is worked out here. Generating for real adds textures to the level and an
         * entry to the shader pool, and this window can still be cancelled, so nothing is written
         * until Import. */
        private void PlanGeneratedMaterials()
        {
            if (_level?.Materials == null) return;

            foreach (ModelIO.PlannedSubmesh submesh in _plan.AllSubmeshes())
            {
                if (_submeshMaterials.ContainsKey(submesh)) continue;      //the sidecar named one that still exists

                int index = SourceMaterialIndexFor(submesh);
                if (index < 0 || _generatedPlans.ContainsKey(index)) continue;

                MaterialGenerator.Plan plan = BuildPlanFor(index, _scene.Meshes[submesh.MeshIndex], null);
                if (plan != null && plan.CanGenerate)
                    _generatedPlans[index] = plan;
            }
        }

        private MaterialGenerator.Plan BuildPlanFor(int materialIndex, Assimp.Mesh mesh, SHADER_LIST? family)
        {
            Assimp.Material source = _scene.Materials[materialIndex];
            IEnumerable<SHADER_LIST> creatable = ShaderPermutationService.CreatableFamilies(_level.Shaders, Singleton.PathToAI).Select(o => o.Family);

            return MaterialGenerator.Describe(_scene, source, mesh, _sourceFilePath, _plan.HasMetadata, _level,
                                              family ?? MaterialGenerator.SuggestFamily(mesh, creatable),
                                              SuggestedMaterialName(source), Singleton.PathToAI);
        }

        /* Name it after the model's own material where it has one, so a model with several materials
         * produces several recognisable ones rather than "MyModel", "MyModel_1"... */
        private string SuggestedMaterialName(Assimp.Material source)
        {
            return string.IsNullOrWhiteSpace(source.Name) || source.Name == "DefaultMaterial"
                ? _sourceFileName
                : _sourceFileName + "\\" + source.Name;
        }

        /* What the submesh will end up drawing with, so the tree says it without anything having to be
         * clicked - the whole point of generating by default is that it needs no attention. */
        private string DescribeMaterial(ModelIO.PlannedSubmesh submesh)
        {
            if (_submeshMaterials.TryGetValue(submesh, out Materials.Material picked))
                return "  [" + picked.Name + "]";

            int index = SourceMaterialIndexFor(submesh);
            if (index >= 0 && _generatedPlans.TryGetValue(index, out MaterialGenerator.Plan plan))
                return "  [generate " + plan.Name + "]";

            return "";
        }

        private void UpdateStatusLabel()
        {
            int submeshes = _plan.AllSubmeshes().Count();
            int lods = _plan.Components.Sum(x => x.LODs.Count);

            string structure = _plan.Components.Count + " component(s), " + lods + " LOD(s), " + submeshes + " submesh(es)";
            string text = _plan.HasMetadata
                ? "Found the metadata written alongside this model - structure, vertex formats and render flags will be restored. " + structure + "."
                : "No OpenCAGE metadata found next to this model, so everything will be imported as one component and one LOD (" + structure + ").";

            if (_generatedPlans.Count != 0)
            {
                int importing = _generatedPlans.Values.Sum(o => o.UsableTextures.Count(t => t.Existing == null));
                int reusing = _generatedPlans.Values.Sum(o => o.UsableTextures.Count(t => t.Existing != null));
                int reusedMaterials = _generatedPlans.Values.Count(o => MaterialGenerator.WouldReuse(o, _level) != null);

                text += "  " + _generatedPlans.Count + (_generatedPlans.Count == 1 ? " material" : " materials")
                      + " will be built from the model's own textures";

                //Say what is being reused as well as what is new - "0 textures" reads as "found none"
                List<string> parts = new List<string>();
                if (importing != 0) parts.Add("importing " + importing + (importing == 1 ? " texture" : " textures"));
                if (reusing != 0) parts.Add("reusing " + reusing + " already in this level");
                if (parts.Count != 0) text += ", " + string.Join(" and ", parts);
                text += ".";

                if (reusedMaterials != 0)
                    text += "  " + reusedMaterials + (reusedMaterials == 1 ? " is" : " are")
                          + " identical to a material already here and will be shared - tick 'Always create a new material' in Edit generated material to force a separate one.";
            }
            statusLabel.Text = text;
        }

        private void BuildStructureTree()
        {
            hierarchyTree.Nodes.Clear();
            TreeNode root = new TreeNode(_plan.Name) { Tag = null };

            for (int i = 0; i < _plan.Components.Count; i++)
            {
                TreeNode componentNode = new TreeNode("Component " + i) { Tag = null };
                for (int x = 0; x < _plan.Components[i].LODs.Count; x++)
                {
                    ModelIO.PlannedLOD lod = _plan.Components[i].LODs[x];
                    TreeNode lodNode = new TreeNode("LOD " + x + (string.IsNullOrEmpty(lod.Name) ? "" : ": " + lod.Name)) { Tag = null };
                    for (int y = 0; y < lod.Submeshes.Count; y++)
                    {
                        ModelIO.PlannedSubmesh submesh = lod.Submeshes[y];
                        int vertexCount = submesh.MeshIndex < _scene.MeshCount ? _scene.Meshes[submesh.MeshIndex].VertexCount : 0;
                        TreeNode submeshNode = new TreeNode("Submesh " + y + " (" + vertexCount + " verts) - " + submesh.MeshName + DescribeMaterial(submesh))
                        {
                            Tag = submesh,
                            Checked = submesh.Include,
                        };
                        lodNode.Nodes.Add(submeshNode);
                    }
                    lodNode.Checked = true;
                    componentNode.Nodes.Add(lodNode);
                }
                componentNode.Checked = true;
                root.Nodes.Add(componentNode);
            }
            root.Checked = true;
            hierarchyTree.Nodes.Add(root);
        }

        private void HierarchyTree_AfterCheck(object sender, TreeViewEventArgs e)
        {
            hierarchyTree.AfterCheck -= HierarchyTree_AfterCheck;
            SetCheckedRecursive(e.Node.Nodes, e.Node.Checked);
            hierarchyTree.AfterCheck += HierarchyTree_AfterCheck;

            ApplyChecksToPlan(hierarchyTree.Nodes);
            UpdatePreviewFromSelection();
        }

        private void SetCheckedRecursive(TreeNodeCollection nodes, bool state)
        {
            foreach (TreeNode node in nodes)
            {
                node.Checked = state;
                SetCheckedRecursive(node.Nodes, state);
            }
        }

        private void ApplyChecksToPlan(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is ModelIO.PlannedSubmesh submesh)
                    submesh.Include = node.Checked;
                ApplyChecksToPlan(node.Nodes);
            }
        }

        private ModelIO.PlannedSubmesh GetSelectedSubmesh()
        {
            return hierarchyTree.SelectedNode?.Tag as ModelIO.PlannedSubmesh;
        }

        private void UpdatePickMaterialButton()
        {
            ModelIO.PlannedSubmesh selected = GetSelectedSubmesh();
            pickMaterialBtn.Enabled = _materials != null && selected != null;
            generateMaterialBtn.Enabled = _level != null && selected != null && SourceMaterialIndexFor(selected) >= 0;
        }

        /* Which of the model's own materials a submesh draws with - what a generated material is built from. */
        private int SourceMaterialIndexFor(ModelIO.PlannedSubmesh submesh)
        {
            if (submesh == null || submesh.MeshIndex < 0 || submesh.MeshIndex >= _scene.MeshCount) return -1;
            int index = _scene.Meshes[submesh.MeshIndex].MaterialIndex;
            return (index < 0 || index >= _scene.MaterialCount) ? -1 : index;
        }

        private void GenerateMaterialBtn_Click(object sender, EventArgs e)
        {
            ModelIO.PlannedSubmesh submesh = GetSelectedSubmesh();
            if (submesh == null || _level == null) return;

            int materialIndex = SourceMaterialIndexFor(submesh);
            if (materialIndex < 0) return;

            Assimp.Material source = _scene.Materials[materialIndex];
            Assimp.Mesh mesh = _scene.Meshes[submesh.MeshIndex];

            using (GenerateMaterial dialog = new GenerateMaterial(_scene, source, mesh,
                                                                 _sourceFilePath, _plan.HasMetadata, _level, SuggestedMaterialName(source)))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result == null)
                    return;

                /* One model material becomes one CATHODE material, so this covers every submesh
                 * drawing with it - per-submesh generation would mint duplicates of the same thing.
                 * It also replaces whatever the sidecar matched, which is the point of the button. */
                _generatedPlans[materialIndex] = dialog.Result;
                foreach (ModelIO.PlannedSubmesh other in _plan.AllSubmeshes())
                    if (SourceMaterialIndexFor(other) == materialIndex)
                        _submeshMaterials.Remove(other);

                BuildStructureTree();
                hierarchyTree.ExpandAll();
                UpdateStatusLabel();
                UpdatePreviewFromSelection();
            }
        }

        private void PickMaterialBtn_Click(object sender, EventArgs e)
        {
            ModelIO.PlannedSubmesh submesh = GetSelectedSubmesh();
            if (submesh == null || _materials == null) return;

            _submeshMaterials.TryGetValue(submesh, out Materials.Material currentMaterial);
            var materialEditor = new EditMaterial(currentMaterial, true);
            Action<Materials.Material> onSelected = material =>
            {
                if (material != null)
                {
                    //An explicit pick wins over anything we would have generated for this submesh
                    _submeshMaterials[submesh] = material;
                    int index = SourceMaterialIndexFor(submesh);
                    if (index >= 0 && _plan.AllSubmeshes().All(o => SourceMaterialIndexFor(o) != index || _submeshMaterials.ContainsKey(o)))
                        _generatedPlans.Remove(index);
                }
                else
                {
                    _submeshMaterials.Remove(submesh);
                }
                BuildStructureTree();
                hierarchyTree.ExpandAll();
                UpdateStatusLabel();
                UpdatePreviewFromSelection();
            };
            materialEditor.OnMaterialSelected += onSelected;
            materialEditor.FormClosed += (s, _) =>
            {
                materialEditor.OnMaterialSelected -= onSelected;
                this.Focus();
                this.BringToFront();
            };
            materialEditor.Show(this);
        }

        private void UpdatePreviewFromSelection()
        {
            var group = new Model3DGroup();
            foreach (ModelIO.PlannedSubmesh submesh in _plan.AllSubmeshes())
            {
                if (!submesh.Include || submesh.MeshIndex < 0 || submesh.MeshIndex >= _scene.MeshCount) continue;

                //Preview in CATHODE's units, at the scale that will be imported, so what's shown is what gets built
                float previewScale = (_plan.Scale > 0 ? _plan.Scale : 1.0f) / _plan.UnitScale;
                var geom = _scene.Meshes[submesh.MeshIndex].ToGeometryModel3D(submesh.Transform * System.Numerics.Matrix4x4.CreateScale(previewScale));
                if (geom?.Geometry == null) continue;

                _submeshMaterials.TryGetValue(submesh, out Materials.Material material);
                if (material != null)
                    MaterialApplier.ApplyMaterial(geom, material);
                else if (!TryPreviewPlannedMaterial(geom, submesh))
                    MaterialApplier.ApplyMaterial(geom, Singleton.FallbackMaterial);
                group.Children.Add(geom);
            }
            _previewControl.SetModelPreview(group);
        }

        /* Build each planned material for real and point its submeshes at it. False when one failed
         * and the user chose not to carry on - anything already built stays, because it is in the
         * level's tables by then and unpicking it would be a bigger operation than it is worth. */
        private bool ExecuteGeneratedPlans()
        {
            if (_generatedPlans.Count == 0 || _level == null) return true;

            Cursor = Cursors.WaitCursor;
            try
            {
                foreach (KeyValuePair<int, MaterialGenerator.Plan> entry in _generatedPlans)
                {
                    Materials.Material material;
                    string error;
                    try
                    {
                        material = MaterialGenerator.Generate(entry.Value, _scene.Materials[entry.Key], _level, Singleton.PathToAI, out error);
                    }
                    catch (Exception ex)
                    {
                        material = null;
                        error = ex.Message;
                    }

                    if (material == null)
                    {
                        //Logged as well as shown: the message box is gone the moment it is dismissed
                        Debug.Log("Model Import", "Could not generate material '" + entry.Value.Name + "': " + (error ?? "unknown reason"));
                        Cursor = Cursors.Default;
                        if (MessageBox.Show(this,
                                "'" + entry.Value.Name + "' could not be generated:\n\n" + (error ?? "unknown reason")
                                + "\n\nImport without it? Those submeshes will use the fallback material.",
                                "Could not generate material", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                            return false;
                        Cursor = Cursors.WaitCursor;
                        continue;
                    }

                    foreach (ModelIO.PlannedSubmesh submesh in _plan.AllSubmeshes())
                        if (SourceMaterialIndexFor(submesh) == entry.Key && !_submeshMaterials.ContainsKey(submesh))
                            _submeshMaterials[submesh] = material;
                }

                Singleton.OnResourceModified?.Invoke();
                return true;
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        /* A planned material does not exist yet, so there is no CATHODE material to shade the preview
         * with - and falling through to the fallback paints it the magenta that means "no material",
         * which reads as the generation having failed. Show the base colour the plan is going to use
         * instead, straight off disk. */
        private bool TryPreviewPlannedMaterial(GeometryModel3D geometry, ModelIO.PlannedSubmesh submesh)
        {
            int index = SourceMaterialIndexFor(submesh);
            if (index < 0 || !_generatedPlans.TryGetValue(index, out MaterialGenerator.Plan plan)) return false;

            MaterialGenerator.PlannedTexture diffuse = plan.UsableTextures.FirstOrDefault(o => o.Role.Sampler == "DIFFUSE_MAP");
            if (diffuse == null) return false;

            ImageSource image = PreviewImage(diffuse);
            if (image == null) return false;

            geometry.Material = new DiffuseMaterial(new ImageBrush(image) { ViewportUnits = BrushMappingMode.Absolute });
            geometry.BackMaterial = geometry.Material;
            return true;
        }

        /* Cached per texture: a base colour map can be 12 MB of PNG and the preview is rebuilt on
         * every selection change and every keystroke in the scale box. */
        private readonly Dictionary<MaterialGenerator.PlannedTexture, ImageSource> _previewImages = new Dictionary<MaterialGenerator.PlannedTexture, ImageSource>();

        private ImageSource PreviewImage(MaterialGenerator.PlannedTexture texture)
        {
            if (_previewImages.TryGetValue(texture, out ImageSource cached)) return cached;

            ImageSource image = null;
            try
            {
                if (texture.Existing != null)
                {
                    //Already in the level, so it decodes the same way every other material preview does
                    System.Drawing.Bitmap bitmap = texture.Existing.ToBitmap();
                    if (bitmap != null) using (bitmap) image = bitmap.ToImageSource();
                }
                else if (texture.SourcePath != null)
                {
                    BitmapImage loaded = new BitmapImage();
                    loaded.BeginInit();
                    loaded.UriSource = new Uri(texture.SourcePath);
                    loaded.CacheOption = BitmapCacheOption.OnLoad;
                    //Preview only - a 4K base colour map costs more to decode than it is worth here
                    loaded.DecodePixelWidth = 512;
                    loaded.EndInit();
                    loaded.Freeze();
                    image = loaded;
                }
            }
            catch
            {
                //An image we cannot show is not a reason to fail the import - the fallback will do
            }

            _previewImages[texture] = image;
            return image;
        }

        private void ImportBtn_Click(object sender, EventArgs e)
        {
            if (!_plan.AllSubmeshes().Any(x => x.Include))
            {
                MessageBox.Show("Select at least one submesh to import (check the boxes in the tree).", "No meshes selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_plan.Scale <= 0)
            {
                MessageBox.Show("The scale has to be a positive number.", "Scale", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _scaleBox?.Focus();
                return;
            }

            /* Generation happens here rather than when the plan was made: it adds textures to the
             * level and an entry to the shader pool, and until Import is pressed this window can
             * still be cancelled. */
            if (!ExecuteGeneratedPlans())
                return;

            foreach (KeyValuePair<ModelIO.PlannedSubmesh, Materials.Material> picked in _submeshMaterials)
                picked.Key.Material = picked.Value;

            Models.CS2 cs2 = ModelIO.BuildCS2(_scene, _plan,
                name => _materials?.Entries.FirstOrDefault(x => x.Name == name),
                Singleton.FallbackMaterial,
                out List<string> warnings);

            if (cs2.Components.Count == 0)
            {
                MessageBox.Show("None of the selected meshes could be converted." + (warnings.Count == 0 ? "" : "\n\n" + string.Join("\n", warnings)), "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (warnings.Count != 0)
            {
                string message = string.Join("\n", warnings.Take(15));
                if (warnings.Count > 15) message += "\n(and " + (warnings.Count - 15) + " more)";
                if (MessageBox.Show(message + "\n\nImport anyway?", "Import warnings", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            //the name the user settled on, folders and all, rather than the one the file arrived with
            if (_nameBox != null) cs2.Name = _nameBox.Value;

            ResultCs2 = cs2;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
