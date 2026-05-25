using AlienPAK;
using Assimp;
using CATHODE;
using CathodeLib;
using OpenCAGE;
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
        private readonly Dictionary<int, Materials.Material> _meshMaterials = new Dictionary<int, Materials.Material>();

        public Models.CS2 ResultCs2 { get; private set; }

        public ModelImportPreview(Scene scene, string sourceFilePath, Materials materials = null)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            _sourceFileName = Path.GetFileNameWithoutExtension(sourceFilePath ?? "");
            _materials = materials;
            InitializeComponent();
            Icon = SharedFormIcon.Icon;

            hierarchyTree.CheckBoxes = true;
            _previewControl = (ModelImportPreviewWPF)previewHost.Child;
            BuildHierarchyTree();
            SetAllMeshNodesChecked(hierarchyTree.Nodes, true);
            hierarchyTree.ExpandAll();
            UpdatePreviewFromSelection();
            UpdatePickMaterialButton();
            hierarchyTree.AfterSelect += (s, e) => { UpdatePreviewFromSelection(); UpdatePickMaterialButton(); };
            hierarchyTree.AfterCheck += (s, e) => UpdatePreviewFromSelection();
            importBtn.Click += ImportBtn_Click;
            cancelBtn.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            pickMaterialBtn.Click += PickMaterialBtn_Click;
            this.Text = "Import model: " + (_sourceFileName ?? "Model");
        }

        private void BuildHierarchyTree()
        {
            hierarchyTree.Nodes.Clear();
            if (_scene.RootNode == null) return;
            var root = new TreeNode(_sourceFileName.Length > 0 ? _sourceFileName : "Scene");
            root.Tag = null;
            AddNodeToTree(root, _scene.RootNode);
            hierarchyTree.Nodes.Add(root);
        }

        private void AddNodeToTree(TreeNode parent, Node node)
        {
            var nodeLabel = string.IsNullOrEmpty(node.Name) ? "Node" : node.Name;
            var tn = new TreeNode(nodeLabel);
            tn.Tag = null;
            foreach (int meshIndex in node.MeshIndices)
            {
                if (meshIndex < 0 || meshIndex >= _scene.MeshCount) continue;
                var mesh = _scene.Meshes[meshIndex];
                var meshNode = new TreeNode($"Mesh {meshIndex} ({mesh.VertexCount} verts)");
                meshNode.Tag = meshIndex;
                tn.Nodes.Add(meshNode);
            }
            foreach (var child in node.Children)
                AddNodeToTree(tn, child);
            parent.Nodes.Add(tn);
        }

        private void SetAllMeshNodesChecked(TreeNodeCollection nodes, bool checkedState)
        {
            foreach (TreeNode n in nodes)
            {
                if (n.Tag is int)
                    n.Checked = checkedState;
                SetAllMeshNodesChecked(n.Nodes, checkedState);
            }
        }

        private int? GetSelectedMeshIndex()
        {
            if (hierarchyTree.SelectedNode?.Tag is int meshIndex)
                return meshIndex;
            return null;
        }

        private void UpdatePickMaterialButton()
        {
            pickMaterialBtn.Enabled = _materials != null && GetSelectedMeshIndex().HasValue;
        }

        private void PickMaterialBtn_Click(object sender, EventArgs e)
        {
            var meshIndex = GetSelectedMeshIndex();
            if (!meshIndex.HasValue || _materials == null) return;
            _meshMaterials.TryGetValue(meshIndex.Value, out var currentMaterial);
            var materialEditor = new EditMaterial(currentMaterial, true);
            Action<Materials.Material> onSelected = material =>
            {
                if (!meshIndex.HasValue) return;
                if (material != null)
                    _meshMaterials[meshIndex.Value] = material;
                else
                    _meshMaterials.Remove(meshIndex.Value);
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
            var meshIndices = GetCheckedMeshIndicesInOrder();
            var group = new Model3DGroup();
            foreach (int i in meshIndices)
            {
                if (i < 0 || i >= _scene.MeshCount) continue;
                var geom = _scene.Meshes[i].ToGeometryModel3D();
                if (geom?.Geometry == null) continue;
                if (_meshMaterials.TryGetValue(i, out var material) && material != null)
                    MaterialApplier.ApplyMaterial(geom, material);
                group.Children.Add(geom);
            }
            _previewControl.SetModelPreview(group);
        }

        private List<int> GetCheckedMeshIndicesInOrder()
        {
            var list = new List<int>();
            CollectCheckedMeshes(hierarchyTree.Nodes, list);
            return list;
        }

        private void CollectCheckedMeshes(TreeNodeCollection nodes, List<int> list)
        {
            foreach (TreeNode n in nodes)
            {
                if (n.Tag is int meshIndex && n.Checked)
                    list.Add(meshIndex);
                CollectCheckedMeshes(n.Nodes, list);
            }
        }

        private void ImportBtn_Click(object sender, EventArgs e)
        {
            var meshIndices = GetCheckedMeshIndicesInOrder();
            if (meshIndices.Count == 0)
            {
                MessageBox.Show("Select at least one mesh to import (check the boxes in the hierarchy).", "No meshes selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ushort biggestSF = 0;
            foreach (int i in meshIndices)
            {
                if (i < 0 || i >= _scene.MeshCount) continue;
                ushort sf = _scene.Meshes[i].CalculateScaleFactor();
                if (sf > biggestSF) biggestSF = sf;
            }
            var cs2 = new Models.CS2();
            cs2.Name = _sourceFileName + ".cs2";
            cs2.Components.Add(new Models.CS2.Component());
            cs2.Components[0].LODs.Add(new Models.CS2.Component.LOD(_sourceFileName));
            foreach (int i in meshIndices)
            {
                if (i < 0 || i >= _scene.MeshCount) continue;
                var submesh = _scene.Meshes[i].ToSubmesh(biggestSF);
                if (submesh == null)
                {
                    MessageBox.Show($"Mesh {i} could not be converted (e.g. exceeds {short.MaxValue} verts or invalid geometry).", "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (_meshMaterials.TryGetValue(i, out var material))
                    submesh.Material = material;
                else
                    submesh.Material = Singleton.FallbackMaterial;
                cs2.Components[0].LODs[0].Submeshes.Add(submesh);
            }
            ResultCs2 = cs2;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
