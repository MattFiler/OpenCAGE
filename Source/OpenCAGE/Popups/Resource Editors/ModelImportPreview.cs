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

        /// <summary>
        /// What to do about the rig and the animation the file carries. Carried out by the caller
        /// after the model lands, because it writes to the game's animation data rather than the
        /// level - the import window is where they are settled.
        /// </summary>
        public ModelImportRig.Outcome ResultRig { get; private set; }
        public ModelImportRig.Situation RigSituation { get { return _rig; } }

        private AssetNameBox _nameBox;
        private TextBox _scaleBox;

        private ModelImportRig.Situation _rig;

        private CheckBox _compositeCheck;
        private TextBox _compositeNameBox;
        private Label _compositeStatus;
        private bool _compositeValid;
        private bool _compositeNameEdited;
        private bool _resultCaptured;
        private string _resultCompositeName;

        /// <summary>
        /// The composite to place the model in, or null when none was asked for. A model skinned to
        /// one of the game's skeletons gets a DisplayModel; anything else a composite named after it.
        /// Settled when Import is pressed, so it does not depend on the controls outliving the window.
        /// </summary>
        public string CompositeName
        {
            get
            {
                if (_resultCaptured) return _resultCompositeName;
                if (_compositeCheck == null || !_compositeCheck.Checked || !_compositeValid) return null;
                return ModelCompositeBuilder.Normalise(_compositeNameBox.Text);
            }
        }

        /// <summary>The game skeleton the mesh is skinned to, when it is on one; null otherwise.</summary>
        public Skeleton CompositeSkeleton
        {
            get { return _rig != null && _rig.FitsAGameRig ? _rig.BestFit?.Skeleton : null; }
        }



        private CheckBox _animImport;

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
            _nameBox.ValidityChanged += (s, e) => { FollowModelName(); RefreshImportEnabled(); };

            /* The rig panel only appears for a file that has one, and it is a good deal taller than
             * the name and scale it sits under - so the window grows to take it rather than squeezing
             * the preview, which is the half someone is actually looking at. */
            Panel rig = BuildRigRow();
            if (rig != null)
            {
                row.Height += rig.Height;
                row.Controls.Add(rig);
                Height += rig.Height;
                MinimumSize = new System.Drawing.Size(MinimumSize.Width, MinimumSize.Height + rig.Height);
            }

            /* The composite row needs the rig settled, since a mesh on a game skeleton gets a
             * DisplayModel rather than an ordinary composite. It sits under the rig, above the scale. */
            Panel compositeRow = BuildCompositeRow();
            if (compositeRow != null)
            {
                row.Height += compositeRow.Height;
                row.Controls.Add(compositeRow);
                Height += compositeRow.Height;
                MinimumSize = new System.Drawing.Size(MinimumSize.Width, MinimumSize.Height + compositeRow.Height);
            }

            row.Controls.Add(BuildScaleRow());
            row.Controls.Add(_nameBox);
            row.Controls.Add(new Label { Dock = DockStyle.Top, Height = 16, Text = "Name (use \\ for folders):" });

            /* Sits directly under the status line: docking is applied from the highest child index
             * down, so taking that index puts this immediately after it. */
            Controls.Add(row);
            Controls.SetChildIndex(row, Controls.GetChildIndex(statusLabel));

            RefreshImportEnabled();
        }

        /// <summary>
        /// Offer to place the model in a composite of its own, so it can be dragged into the level
        /// the moment it lands. A mesh skinned to one of the game's skeletons becomes a DisplayModel,
        /// which is what a character's display_model looks up, so its name carries that prefix and
        /// cannot sit in a folder; anything else is named after the model, folders and all.
        /// </summary>
        private Panel BuildCompositeRow()
        {
            if (_level?.Commands == null) return null;

            bool displayModel = CompositeSkeleton != null;
            Panel panel = new Panel { Dock = DockStyle.Top, Height = 72, Padding = new Padding(0, 6, 0, 0) };

            _compositeCheck = new CheckBox
            {
                Dock = DockStyle.Top,
                Height = 22,
                Checked = true,
                Text = displayModel
                    ? "Also create a DisplayModel composite for it, with an EnvironmentModelReference on " + CompositeSkeleton.Name + ":"
                    : "Also create a composite that places it (use \\ for folders):",
            };
            _compositeNameBox = new TextBox { Dock = DockStyle.Top, Text = ModelCompositeBuilder.DefaultName(_nameBox.Value, displayModel) };
            _compositeStatus = new Label { Dock = DockStyle.Top, Height = 20, AutoEllipsis = true, Padding = new Padding(1, 3, 0, 0) };

            _compositeCheck.CheckedChanged += (s, e) => { _compositeNameBox.Enabled = _compositeCheck.Checked; RevalidateComposite(); RefreshImportEnabled(); };
            _compositeNameBox.TextChanged += (s, e) =>
            {
                //Once it has been typed in it stays put; until then it follows the model's name
                if (_compositeNameBox.Focused) _compositeNameEdited = true;
                RevalidateComposite();
                RefreshImportEnabled();
            };

            //docking runs from the highest child index down, so add bottom-up
            panel.Controls.Add(_compositeStatus);
            panel.Controls.Add(_compositeNameBox);
            panel.Controls.Add(_compositeCheck);
            RevalidateComposite();
            return panel;
        }

        private void FollowModelName()
        {
            if (_compositeNameBox == null || _compositeNameEdited || _nameBox == null) return;
            _compositeNameBox.Text = ModelCompositeBuilder.DefaultName(_nameBox.Value, CompositeSkeleton != null);
        }

        private void RevalidateComposite()
        {
            if (_compositeNameBox == null) return;
            if (!_compositeCheck.Checked)
            {
                _compositeValid = true;
                _compositeStatus.Text = "";
                return;
            }

            string problem = ModelCompositeBuilder.Problem(_compositeNameBox.Text, CompositeSkeleton != null, _level.Commands);
            _compositeValid = problem == null;
            if (!_compositeValid)
            {
                _compositeStatus.ForeColor = System.Drawing.Color.FromArgb(210, 90, 80);
                _compositeStatus.Text = problem;
            }
            else
            {
                string tidy = ModelCompositeBuilder.Normalise(_compositeNameBox.Text);
                _compositeStatus.ForeColor = SystemColors.GrayText;
                _compositeStatus.Text = string.Equals(tidy, _compositeNameBox.Text, StringComparison.Ordinal) ? "" : "Will be stored as  " + tidy;
            }
        }

        private void RefreshImportEnabled()
        {
            importBtn.Enabled = (_nameBox == null || _nameBox.IsValid) && (_compositeCheck == null || !_compositeCheck.Checked || _compositeValid);
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

        /// <summary>
        /// The rig and the animation the file arrived with. A model file that carries a skeleton
        /// usually carries the clips authored on it too, and the two are one decision: a clip needs a
        /// skeleton to play on, so what happens to the rig decides what can happen to the animation.
        ///
        /// Nothing is shown for a file with neither, which is most of them.
        /// </summary>
        private Panel BuildRigRow()
        {
            _rig = ModelImportRig.Examine(_scene, _plan, Singleton.Animations);
            if (!_rig.Skinned && !_rig.HasAnimations) return null;

            Panel panel = new Panel { Dock = DockStyle.Top, Height = 26, Padding = new Padding(0, 6, 0, 0) };
            int y = 6;

            //Sized to the text: with an animation in the file the description runs to a second line
            Label summary = new Label
            {
                AutoSize = false,
                Location = new System.Drawing.Point(0, y),
                Width = 540,
                Text = ModelImportRig.Describe(_rig),
            };
            summary.Height = WrappedHeight(summary);
            panel.Controls.Add(summary);
            y += summary.Height + 4;

            bool canAddToGame = Singleton.AnimationsLoaded;
            if (!canAddToGame)
            {
                panel.Controls.Add(new Label
                {
                    AutoSize = true,
                    Location = new System.Drawing.Point(0, y),
                    ForeColor = SystemColors.GrayText,
                    Text = "The game's animation data isn't loaded, so no skeleton or animation can be added.",
                });
                panel.Height = y + 24;
                return panel;
            }

            /* A mesh on a rig the game doesn't have keeps its shape but loses its skinning: there is
             * nowhere to put a new skeleton. A rig can be built out of the file and is a perfectly
             * valid skeleton, but not a usable character - the engine wants bone groups, a mirror
             * table, a ragdoll and an animation set carrying HUMANOID's contexts before it will spawn
             * one, and none of that can be authored from here. Say so plainly rather than offering a
             * choice that cannot lead anywhere. */
            if (_rig.Skinned && !_rig.FitsAGameRig)
            {
                Label warning = new Label
                {
                    AutoSize = false,
                    Location = new System.Drawing.Point(0, y),
                    Width = 540,
                    ForeColor = System.Drawing.Color.Firebrick,
                    Text = "This is skinned to a skeleton the game doesn't have, so it will be imported"
                         + " unskinned - the mesh comes in, the bone weights don't. To bring a character in"
                         + " with its skinning intact, bind it to one of the game's skeletons before"
                         + " importing it.",
                };
                warning.Height = WrappedHeight(warning);
                panel.Controls.Add(warning);
                y += warning.Height + 4;
            }

            if (_rig.HasAnimations)
            {
                _animImport = new CheckBox
                {
                    Location = new System.Drawing.Point(0, y),
                    Width = 420,
                    Checked = true,
                    Text = AnimationLabel(),
                };
                panel.Controls.Add(_animImport);
                y += 24;

                panel.Controls.Add(new Label
                {
                    AutoSize = true,
                    Location = new System.Drawing.Point(20, y),
                    ForeColor = SystemColors.GrayText,
                    Text = "The import window asks which set and rig to build it against, once per animation.",
                });
                y += 20;
            }

            panel.Height = y + 4;
            return panel;
        }

        /* The height a fixed-width label needs to show all of its text, wrapped */
        private static int WrappedHeight(Label label)
        {
            return TextRenderer.MeasureText(label.Text, label.Font, new System.Drawing.Size(label.Width, 0), TextFormatFlags.WordBreak).Height + 2;
        }

        private string AnimationLabel()
        {
            int count = _rig.Animations.Count;
            return count == 1
                ? "Also import the animation '" + Shorten(_rig.Animations[0]) + "'"
                : "Also import all " + count + " animations";
        }

        private static string Shorten(string name)
        {
            name = name ?? "";
            return name.Length <= 22 ? name : name.Substring(0, 21) + "…";
        }

        /* Which set to offer first: the one named after the rig the mesh is already on, and failing
         * that the humanoid one, which is where a clip off an unrecognised rig has the best chance of
         * being useful. The rig itself is settled in the import window, not here. */
        private string SuggestedSetName()
        {
            if (_rig.FitsAGameRig && _rig.BestFit?.Skeleton != null) return _rig.BestFit.Skeleton.Name;
            return _rig.LooksHumanoid ? "MALE" : "";
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

            /* The rig is settled before anything is built, because re-skinning the mesh onto a game
             * rig rewrites the scene the builder reads - the vertices move into that rig's bind pose
             * and the bones are renamed. Doing it after would build the mesh in the wrong place. */
            ResultRig = CollectRigChoice();


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

            /* The rig panel has already said, in red and before anything was imported, that a mesh on
             * a skeleton the game doesn't have loses its skinning. Repeating it once per mesh - as a
             * question, after the fact - tells nobody anything they weren't told already. */
            if (_rig != null && _rig.Skinned && !_rig.FitsAGameRig)
                warnings.RemoveAll(ModelIO.IsDroppedSkinning);

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
            _resultCompositeName = CompositeName;
            _resultCaptured = true;

            ResultCs2 = cs2;
            DialogResult = DialogResult.OK;
            Close();
        }

        /* Re-skin onto the chosen game rig, if that is what was asked for and the mesh is not already
         * on it. Returns false only when it was asked for and could not be done - which is worth
         * stopping on, since the alternative is importing a mesh bound to the wrong skeleton. */
        /* What the rig panel was left set to. Null when the file had no rig and no animation, which
         * is how the caller knows there is nothing to do to the animation data. */
        private ModelImportRig.Outcome CollectRigChoice()
        {
            if (_rig == null || (!_rig.Skinned && !_rig.HasAnimations) || !Singleton.AnimationsLoaded) return null;

            return new ModelImportRig.Outcome
            {
                ImportAnimations = _animImport != null && _animImport.Checked,

                /* Both of these are only where the import window starts. It asks, and it is the one
                 * place either question is put - a mesh already on a game rig just answers the rig
                 * half in advance. */
                AnimationSetName = SuggestedSetName(),
                PreferredRig = _rig.FitsAGameRig ? _rig.BestFit?.Skeleton?.Name ?? "" : "",
            };
        }

    }
}
