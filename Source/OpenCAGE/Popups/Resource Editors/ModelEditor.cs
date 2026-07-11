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
using static CATHODE.Models;
using static CATHODE.Models.CS2;
using static CATHODE.Models.CS2.Component;
using static CATHODE.Models.CS2.Component.LOD;

namespace AlienPAK
{
    public partial class ModelEditor : Form
    {
        private TreeUtility _treeHelper;
        private CS2 _model = null;
        private List<StringMeshLookup> _treeLookup = new List<StringMeshLookup>();

        private const string _fileFilter = "FBX Model|*.fbx|GLTF Model|*.gltf|OBJ Model|*.obj";

        ModelEditorControlsWPF _controls;

        public ModelEditor(CS2 model = null)
        {
            _model = model;

            InitializeComponent();
            Icon = SharedFormIcon.Icon;

            _treeHelper = new TreeUtility(FileTree, TreeType.MODELS);
            if (_model == null) return;
            this.Text = _model.Name;

            RefreshTree();

            _controls = (ModelEditorControlsWPF)elementHost1.Child;
            _controls.renderMaterials.IsChecked = SettingsManager.GetBool(Settings.ShowTexOpt);
            _controls.OnEditMaterialRequested += OnEditMaterialRequested;
            _controls.OnMaterialRenderCheckChanged += OnMaterialRenderCheckChanged;
            _controls.OnAddRequested += OnAddRequested;
            _controls.OnReplaceRequested += OnReplaceRequested;
            _controls.OnDeleteRequested += OnDeleteRequested;
            _controls.OnScaleFactorChanged += OnScaleFactorChanged;
        }

        private void OnScaleFactorChanged(int scaleFactor)
        {
            StringMeshLookup lookup = _treeLookup.FirstOrDefault(o => o.String == FileTree.SelectedNode?.FullPath);
            if (lookup == null || lookup.submesh == null) return;
            lookup.submesh.VertexScale = (ushort)scaleFactor;
        }

        private void OnDeleteRequested()
        {
            StringMeshLookup lookup = _treeLookup.FirstOrDefault(o => o.String == FileTree.SelectedNode?.FullPath);
            if (lookup == null) return;

            if (lookup.cs2 != null)
            {
                lookup.cs2.Components.Clear();
            }
            else if (lookup.component != null)
            {
                _model.Components.Remove(lookup.component);
            }
            else if (lookup.lod != null && lookup.component != null)
            {
                lookup.component.LODs.Remove(lookup.lod);
            }
            else if (lookup.submesh != null)
            {
                for (int i = 0; i < _model.Components.Count; i++)
                    for (int x = 0; x < _model.Components[i].LODs.Count; x++)
                        _model.Components[i].LODs[x].Submeshes.Remove(lookup.submesh);
            }

            RefreshTree();
            RefreshSelectedModelPreview();
            Singleton.OnResourceModified?.Invoke();
        }

        private void OnReplaceRequested()
        {
            StringMeshLookup lookup = _treeLookup.FirstOrDefault(o => o.String == FileTree.SelectedNode?.FullPath);
            if (lookup == null || lookup.submesh == null) return;

            OpenFileDialog filePicker = new OpenFileDialog();
            filePicker.Filter = _fileFilter;
            if (filePicker.ShowDialog() != DialogResult.OK) return;

            Models.CS2.Component.LOD.Submesh submesh = null;
            try
            {
                using (AssimpContext importer = new AssimpContext())
                {
                    Scene model = importer.ImportFile(filePicker.FileName, PostProcessSteps.Triangulate | PostProcessSteps.FindDegenerates | PostProcessSteps.LimitBoneWeights | PostProcessSteps.GenerateBoundingBoxes | PostProcessSteps.FlipUVs | PostProcessSteps.FlipWindingOrder | PostProcessSteps.MakeLeftHanded);
                    submesh = model.Meshes[0].ToSubmesh();
                }
            }
            catch { }
            if (submesh == null)
            {
                MessageBox.Show("An error occurred while generating the CS2 submesh!\nPlease try again, or use a different model.\nYour model must contain a single mesh in the root node.", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            lookup.submesh.Data = submesh.Data;
            lookup.submesh.IndexCount = submesh.IndexCount;
            lookup.submesh.VertexCount = submesh.VertexCount;
            lookup.submesh.VertexFormatFull = submesh.VertexFormatFull;
            lookup.submesh.VertexFormatPartial = submesh.VertexFormatPartial;
            lookup.submesh.VertexScale = submesh.VertexScale;
            lookup.submesh.MaxBounds = submesh.MaxBounds;
            lookup.submesh.MinBounds = submesh.MinBounds;
            RefreshTree();
            RefreshSelectedModelPreview();
            Singleton.OnResourceModified?.Invoke();
        }

        private void RefreshTree()
        {
            _treeLookup.Clear();
            List<string> contents = new List<string>();
            string componentString = "";
            string lodString = "";
            string submeshString = "";
            _treeLookup.Add(new StringMeshLookup() { String = Path.GetFileName(_model.Name), cs2 = _model });
            for (int i = 0; i < _model.Components.Count; i++)
            {
                componentString = Path.GetFileName(_model.Name) + "\\Component " + i;
                _treeLookup.Add(new StringMeshLookup() { String = componentString, component = _model.Components[i] });
                for (int x = 0; x < _model.Components[i].LODs.Count; x++)
                {
                    lodString = componentString + "\\Part " + x + ": " + _model.Components[i].LODs[x].Name;
                    _treeLookup.Add(new StringMeshLookup() { String = lodString, lod = _model.Components[i].LODs[x], component = _model.Components[i] });
                    for (int y = 0; y < _model.Components[i].LODs[x].Submeshes.Count; y++)
                    {
                        submeshString = lodString + "\\Mesh " + y;
                        _treeLookup.Add(new StringMeshLookup() { String = submeshString, submesh = _model.Components[i].LODs[x].Submeshes[y] });
                        contents.Add(submeshString);
                    }
                }
            }
            _treeHelper.UpdateFileTree(contents);
            FileTree.ExpandAll();
        }

        private void OnAddRequested(SelectedModelType type)
        {
            StringMeshLookup lookup = _treeLookup.FirstOrDefault(o => o.String == FileTree.SelectedNode?.FullPath);
            if (lookup == null) return;

            OpenFileDialog filePicker = new OpenFileDialog();
            filePicker.Filter = _fileFilter;
            if (filePicker.ShowDialog() != DialogResult.OK) return;

            Models.CS2.Component.LOD.Submesh submesh = null;
            using (AssimpContext importer = new AssimpContext())
            {
                Scene model = importer.ImportFile(filePicker.FileName, PostProcessSteps.Triangulate | PostProcessSteps.FindDegenerates | PostProcessSteps.LimitBoneWeights | PostProcessSteps.GenerateBoundingBoxes | PostProcessSteps.FlipUVs | PostProcessSteps.FlipWindingOrder | PostProcessSteps.MakeLeftHanded);
                submesh = model.Meshes[0].ToSubmesh();
            }
            if (submesh == null)
            {
                MessageBox.Show("An error occurred while generating the CS2 submesh!\nPlease try again, or use a different model.\nYour model must contain a single mesh in the root node.", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            switch (type)
            {
                case SelectedModelType.COMPONENT:
                    if (lookup.cs2 != null)
                    {
                        CS2.Component newComponent = new CS2.Component();
                        LOD newLOD = new LOD(Path.GetFileNameWithoutExtension(filePicker.FileName));
                        newLOD.Submeshes.Add(submesh);
                        newComponent.LODs.Add(newLOD);
                        lookup.cs2.Components.Add(newComponent);
                    }
                    break;
                case SelectedModelType.LOD:
                    if (lookup.component != null)
                    {
                        LOD newLOD = new LOD(Path.GetFileNameWithoutExtension(filePicker.FileName));
                        newLOD.Submeshes.Add(submesh);
                        lookup.component.LODs.Add(newLOD);
                    }
                    break;
                case SelectedModelType.SUBMESH:
                    if (lookup.lod != null)
                        lookup.lod.Submeshes.Add(submesh);
                    break;
            }
            RefreshTree();
            Singleton.OnResourceModified?.Invoke();
        }

        private void OnEditMaterialRequested()
        {
            if (FileTree.SelectedNode == null) return;
            StringMeshLookup lookup = _treeLookup.FirstOrDefault(o => o.String == FileTree.SelectedNode.FullPath);
            if (lookup == null || lookup.submesh == null) return;
            Materials.Material material = lookup.submesh.Material;
            if (material == null) return;

            EditMaterial materialEditor = new EditMaterial(material, true);
            Action<Materials.Material> onSelected = OnMaterialSelected;
            materialEditor.OnMaterialSelected += onSelected;
            materialEditor.FormClosed += (s, _) =>
            {
                materialEditor.OnMaterialSelected -= onSelected;
                RefreshSelectedModelPreview(false);
                this.Focus();
                this.BringToFront();
            };
            materialEditor.Show();
        }

        private void OnMaterialSelected(Materials.Material material)
        {
            StringMeshLookup lookup = _treeLookup.FirstOrDefault(o => o.String == FileTree.SelectedNode.FullPath);
            Models.CS2.Component.LOD.Submesh target = lookup?.submesh;
            if (target == null) return;
            target.Material = material;
            RefreshSelectedModelPreview(false);
            Singleton.OnResourceModified?.Invoke();

            this.Focus();
            this.BringToFront();
        }

        private void OnMaterialRenderCheckChanged(bool check)
        {
            RefreshSelectedModelPreview(false);
        }

        private void FileTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            RefreshSelectedModelPreview();
        }

        private void RefreshSelectedModelPreview(bool doZoom = true)
        {
            _controls.ShowContextualButtons(SelectedModelType.NONE);
            _controls.SetModelPreview(null, "", 0, "");

            if (FileTree.SelectedNode == null) return;
            StringMeshLookup lookup = _treeLookup.FirstOrDefault(o => o.String == FileTree.SelectedNode.FullPath);

            _controls.ShowContextualButtons(lookup?.component != null ? SelectedModelType.COMPONENT : lookup?.lod != null ? SelectedModelType.LOD : lookup?.submesh != null ? SelectedModelType.SUBMESH : SelectedModelType.CS2);

            Model3DGroup model = new Model3DGroup();
            int vertCount = 0;
            string materialInfo = "";
            foreach (Models.CS2.Component component in _model.Components)
            {
                if (lookup != null && lookup.component != null && component != lookup.component) continue;
                foreach (Models.CS2.Component.LOD lod in component.LODs)
                {
                    if (lookup != null && lookup.lod != null && lod != lookup.lod) continue;
                    foreach (Models.CS2.Component.LOD.Submesh sm in lod.Submeshes)
                    {
                        if (lookup != null && lookup.submesh != null && sm != lookup.submesh) continue;
                        GeometryModel3D mdl = sm.ToGeometryModel3D(_controls.renderMaterials.IsChecked == true);
                        vertCount += sm.VertexCount;
                        model.Children.Add(mdl);
                    }
                }
            }

            string[] nameContents = (FileTree.SelectedNode.FullPath + "\\").Split('\\');
            _controls.SetModelPreview(model, nameContents[nameContents.Length - 2], vertCount, materialInfo, lookup?.submesh == null ? -1 : (int)lookup.submesh.VertexScale, doZoom);
        }

        private class StringMeshLookup
        {
            public string String;

            public Models.CS2.Component.LOD.Submesh submesh = null;
            public Models.CS2.Component.LOD lod = null;
            public Models.CS2.Component component = null;
            public Models.CS2 cs2 = null;
        }
    }
}
