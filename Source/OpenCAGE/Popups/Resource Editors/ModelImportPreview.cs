using AlienPAK;
using Assimp;
using CATHODE;
using CathodeLib;
using OpenCAGE;
using OpenCAGE.Popups.UserControls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Media.Media3D;

namespace AlienPAK
{
    public partial class ModelImportPreview : Form
    {
        private readonly Scene _scene;
        private readonly string _sourceFileName;
        private readonly ModelImportPreviewWPF _previewControl;
        private readonly Materials _materials;
        private readonly ModelIO.ImportPlan _plan;
        private readonly Dictionary<ModelIO.PlannedSubmesh, Materials.Material> _submeshMaterials = new Dictionary<ModelIO.PlannedSubmesh, Materials.Material>();

        public Models.CS2 ResultCs2 { get; private set; }

        private AssetNameBox _nameBox;

        public ModelImportPreview(Scene scene, string sourceFilePath, Materials materials = null,
                                  Func<IEnumerable<string>> takenNames = null)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            _sourceFileName = Path.GetFileNameWithoutExtension(sourceFilePath ?? "");
            _materials = materials;
            InitializeComponent();
            OpenCAGE.Theming.ThemeManager.ApplyToForm(this);
            Icon = SharedFormIcon.Icon;

            ModelIO.ModelMetadata metadata = ModelIO.TryLoadSidecar(sourceFilePath);
            _plan = ModelIO.CreateImportPlan(_scene, metadata, _sourceFileName);
            MatchMaterialsFromMetadata();

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
            Panel row = new Panel { Dock = DockStyle.Top, Height = 66, Padding = new Padding(8, 0, 8, 8) };

            _nameBox = new AssetNameBox { Dock = DockStyle.Top, Height = 44 };
            _nameBox.Bind(_plan.Name, takenNames);
            _nameBox.ValidityChanged += (s, e) => importBtn.Enabled = _nameBox.IsValid;

            row.Controls.Add(_nameBox);
            row.Controls.Add(new Label { Dock = DockStyle.Top, Height = 16, Text = "Name (use \\ for folders):" });

            /* Sits directly under the status line: docking is applied from the highest child index
             * down, so taking that index puts this immediately after it. */
            Controls.Add(row);
            Controls.SetChildIndex(row, Controls.GetChildIndex(statusLabel));

            importBtn.Enabled = _nameBox.IsValid;
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

        private void UpdateStatusLabel()
        {
            int submeshes = _plan.AllSubmeshes().Count();
            int lods = _plan.Components.Sum(x => x.LODs.Count);

            string structure = _plan.Components.Count + " component(s), " + lods + " LOD(s), " + submeshes + " submesh(es)";
            statusLabel.Text = _plan.HasMetadata
                ? "Found the metadata written alongside this model - structure, vertex formats and render flags will be restored. " + structure + "."
                : "No OpenCAGE metadata found next to this model, so everything will be imported as one component and one LOD (" + structure + ").";
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
                        TreeNode submeshNode = new TreeNode("Submesh " + y + " (" + vertexCount + " verts) - " + submesh.MeshName)
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
            pickMaterialBtn.Enabled = _materials != null && GetSelectedSubmesh() != null;
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
                    _submeshMaterials[submesh] = material;
                else
                    _submeshMaterials.Remove(submesh);
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

                //Preview in CATHODE's units, so what's shown is what gets built
                var geom = _scene.Meshes[submesh.MeshIndex].ToGeometryModel3D(submesh.Transform * System.Numerics.Matrix4x4.CreateScale(1.0f / _plan.UnitScale));
                if (geom?.Geometry == null) continue;

                _submeshMaterials.TryGetValue(submesh, out Materials.Material material);
                MaterialApplier.ApplyMaterial(geom, material ?? Singleton.FallbackMaterial);
                group.Children.Add(geom);
            }
            _previewControl.SetModelPreview(group);
        }

        private void ImportBtn_Click(object sender, EventArgs e)
        {
            if (!_plan.AllSubmeshes().Any(x => x.Include))
            {
                MessageBox.Show("Select at least one submesh to import (check the boxes in the tree).", "No meshes selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

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
