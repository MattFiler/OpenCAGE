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


        /// <summary>Replacing or adding a mesh reads a file, so it opens on every format we read.</summary>
        private static string _importFilter { get { return OpenCAGE.ModelExport.ModelExporter.ImportFilter(false); } }

        ModelEditorControlsWPF _controls;

        public ModelEditor(CS2 model = null)
        {
            _model = model;

            InitializeComponent();
            OpenCAGE.Popups.Base.BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/model-editor/");
            OpenCAGE.Theming.ThemeManager.ApplyToForm(this);
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

        /* A real resize of the geometry, so any fraction works. The submesh's own VertexScale can only
         * be a whole number - the file stores it as one - which is why setting that directly could
         * never do this. Everything the selection shows is resized: one submesh, an LOD, a component,
         * or the whole model when the model itself is selected. */
        private void OnScaleFactorChanged(float factor)
        {
            StringMeshLookup lookup = _treeLookup.FirstOrDefault(o => o.String == FileTree.SelectedNode?.FullPath);
            List<Submesh> submeshes = SelectedSubmeshes(lookup);
            if (submeshes.Count == 0) return;

            //More than one submesh can't be undone either, so say what it covers before doing it
            if (submeshes.Count > 1)
            {
                string what = lookup?.lod != null ? "this part" : lookup?.component != null ? "this component" : Path.GetFileName(_model.Name);
                string message = "Resize all " + submeshes.Count.ToString("N0") + " meshes of " + what + " by " + factor.ToString(System.Globalization.CultureInfo.InvariantCulture) + "?\n\n" +
                    "This can't be undone. Collision made from the model isn't resized with it, and entities placed around it stay where they are.";
                if (submeshes.Any(o => o.Bones.Count != 0))
                    message += "\n\nSome of these meshes are skinned to a skeleton: the skeleton keeps its size, so they will no longer fit it.";
                if (MessageBox.Show(message, "Resize", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            }

            int resized = 0;
            foreach (Submesh submesh in submeshes)
                if (ModelIO.RescaleSubmesh(submesh, factor)) resized++;
            if (resized == 0)
            {
                MessageBox.Show(submeshes.Count == 1 ? "This mesh has no position data to resize." : "None of these meshes has position data to resize.", "Resize", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MarkModelChanged();
            Singleton.OnResourceModified?.Invoke();
            RefreshSelectedModelPreview(false);
        }

        /* The submeshes the tree selection covers - the same ones the preview draws for it */
        private List<Submesh> SelectedSubmeshes(StringMeshLookup lookup)
        {
            List<Submesh> submeshes = new List<Submesh>();
            if (_model == null) return submeshes;
            foreach (Models.CS2.Component component in _model.Components)
            {
                if (lookup != null && lookup.component != null && component != lookup.component) continue;
                foreach (Models.CS2.Component.LOD lod in component.LODs)
                {
                    if (lookup != null && lookup.lod != null && lod != lookup.lod) continue;
                    foreach (Submesh sm in lod.Submeshes)
                    {
                        if (lookup != null && lookup.submesh != null && sm != lookup.submesh) continue;
                        submeshes.Add(sm);
                    }
                }
            }
            return submeshes;
        }

        private void MarkModelChanged()
        {
            EditHistory.AssetChanged(Singleton.Editor?.CompositeBrowser?.Content?.Level, AssetModificationInfoTable.AssetKind.MODEL, _model?.Name);
        }

        private void OnDeleteRequested()
        {
            StringMeshLookup lookup = _treeLookup.FirstOrDefault(o => o.String == FileTree.SelectedNode?.FullPath);
            if (lookup == null) return;

            //A part's entry holds its component too, so the most specific selection has to be checked first
            if (lookup.submesh != null)
            {
                for (int i = 0; i < _model.Components.Count; i++)
                    for (int x = 0; x < _model.Components[i].LODs.Count; x++)
                        _model.Components[i].LODs[x].Submeshes.Remove(lookup.submesh);
            }
            else if (lookup.lod != null && lookup.component != null)
            {
                lookup.component.LODs.Remove(lookup.lod);
            }
            else if (lookup.component != null)
            {
                _model.Components.Remove(lookup.component);
            }
            else if (lookup.cs2 != null)
            {
                lookup.cs2.Components.Clear();
            }

            RefreshTree();
            RefreshSelectedModelPreview();
            MarkModelChanged();
            Singleton.OnResourceModified?.Invoke();
        }

        private void OnReplaceRequested()
        {
            StringMeshLookup lookup = _treeLookup.FirstOrDefault(o => o.String == FileTree.SelectedNode?.FullPath);
            if (lookup == null || lookup.submesh == null) return;

            OpenFileDialog filePicker = new OpenFileDialog();
            filePicker.Filter = _importFilter;
            filePicker.FilterIndex = 1;
            filePicker.DefaultExt = "fbx";
            if (filePicker.ShowDialog() != DialogResult.OK) return;

            Models.CS2.Component.LOD.Submesh submesh = null;
            int triangles = 0, vertices = 0;
            try
            {
                using (AssimpContext importer = new AssimpContext())
                {
                    Scene model = importer.ImportFile(filePicker.FileName, ModelIO.ImportPostProcessSteps);
                    triangles = model.Meshes[0].FaceCount;
                    vertices = model.Meshes[0].VertexCount;

                    //Keep the properties of the submesh we're standing in for, so it still renders the same way
                    submesh = model.Meshes[0].ToSubmesh(lookup.submesh);
                }
            }
            catch { }
            if (submesh == null && (triangles > ModelIO.MaxTrianglesPerSubmesh || vertices > ushort.MaxValue))
            {
                MessageBox.Show("This mesh has " + triangles.ToString("N0") + " triangles and " + vertices.ToString("N0") + " vertices, but one mesh can hold at most " + ModelIO.MaxTrianglesPerSubmesh.ToString("N0") + " triangles and " + ushort.MaxValue.ToString("N0") + " vertices.\nUse a smaller mesh, or use Add New Submesh instead, which cuts it into several.", "Mesh too large", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
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
            lookup.submesh.Bones.Clear();
            lookup.submesh.Bones.AddRange(submesh.Bones);
            RefreshTree();
            RefreshSelectedModelPreview();
            MarkModelChanged();
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
            filePicker.Filter = _importFilter;
            filePicker.FilterIndex = 1;
            filePicker.DefaultExt = "fbx";
            if (filePicker.ShowDialog() != DialogResult.OK) return;

            //A mesh over one submesh's limits comes back cut into several
            List<Models.CS2.Component.LOD.Submesh> submeshes = new List<Models.CS2.Component.LOD.Submesh>();
            using (AssimpContext importer = new AssimpContext())
            {
                Scene model = importer.ImportFile(filePicker.FileName, ModelIO.ImportPostProcessSteps);
                if (model?.Meshes.Count > 0)
                    submeshes = ModelIO.ToSubmeshes(model.Meshes[0], System.Numerics.Matrix4x4.Identity, ModelIO.DescribeSubmesh(null), out List<string> warnings);
            }
            if (submeshes.Count == 0)
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
                        newLOD.Submeshes.AddRange(submeshes);
                        newComponent.LODs.Add(newLOD);
                        lookup.cs2.Components.Add(newComponent);
                    }
                    break;
                case SelectedModelType.LOD:
                    if (lookup.component != null)
                    {
                        LOD newLOD = new LOD(Path.GetFileNameWithoutExtension(filePicker.FileName));
                        newLOD.Submeshes.AddRange(submeshes);
                        lookup.component.LODs.Add(newLOD);
                    }
                    break;
                case SelectedModelType.SUBMESH:
                    if (lookup.lod != null)
                        lookup.lod.Submeshes.AddRange(submeshes);
                    break;
            }
            RefreshTree();
            MarkModelChanged();
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
            materialEditor.CloseWith(this);
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
            MarkModelChanged();
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

            _controls.ShowContextualButtons(lookup?.submesh != null ? SelectedModelType.SUBMESH : lookup?.lod != null ? SelectedModelType.LOD : lookup?.component != null ? SelectedModelType.COMPONENT : SelectedModelType.CS2);

            Model3DGroup model = new Model3DGroup();
            int vertCount = 0;
            string materialInfo = "";
            //Submeshes sharing a material share the images made from it, rather than each holding full-size copies
            using (MaterialApplier.ShareDerivedImages())
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
            _controls.SetModelPreview(model, nameContents[nameContents.Length - 2], vertCount, materialInfo, model.Children.Count != 0, doZoom);
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
