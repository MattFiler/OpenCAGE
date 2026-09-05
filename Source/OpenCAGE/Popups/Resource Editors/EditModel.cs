using AlienPAK;
using Assimp;
using CATHODE;
using CathodeLib;
using OpenCAGE.ModelExport;
using OpenCAGE.Popups.Base;
using OpenCAGE.Popups.UserControls;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class EditModel : BaseWindow
    {
        GUI_ModelViewer modelViewer = null;
        TreeUtility treeHelper;

        private Dictionary<Models.CS2.Component.LOD.Submesh, GUI_ModelViewer.Model> allSubmeshes = new Dictionary<Models.CS2.Component.LOD.Submesh, GUI_ModelViewer.Model>();
        private List<CheckBox> submeshCheckboxes = new List<CheckBox>();
        private Dictionary<CheckBox, Models.CS2.Component.LOD.Submesh> checkboxToModelIndex = new Dictionary<CheckBox, Models.CS2.Component.LOD.Submesh>();
        private Dictionary<int, List<CheckBox>> lodToCheckboxes = new Dictionary<int, List<CheckBox>>();
        private Dictionary<int, GroupBox> lodGroups = new Dictionary<int, GroupBox>();
        private Dictionary<CheckBox, Button> submeshRowInfoButtons = new Dictionary<CheckBox, Button>();
        private readonly List<(Models.CS2.Component.LOD.RenderingFlag flag, CheckBox cb)> _renderFlagChecks = new List<(Models.CS2.Component.LOD.RenderingFlag, CheckBox)>();
        private bool _suppressRenderFlagChange;
        private bool _applyingExternalSettings;

        public Action<Models.CS2.Component> OnModelSelected;

        /// <summary>
        /// Raised instead of <see cref="OnModelSelected"/> when the window is picking whole models.
        /// </summary>
        public Action<Models.CS2> OnWholeModelSelected;

        /* Some callers want a model, not one of its components. In that mode the tree stops at the
         * .cs2 and selecting it hands back the whole thing, since picking a single component of a
         * character is never what's wanted. */
        private readonly bool _wholeModelsOnly;
        private readonly Func<Models.CS2, bool> _modelFilter;

        //FBX first: it's the only one of the three that round trips multiple UV channels and skinning
        private static string ModelFileFilter { get { return OpenCAGE.ModelExport.ModelExporter.Filter(false); } }

        /// <summary>Importing does not need a format chosen, so it opens on everything we read.</summary>
        private static string ModelImportFilter { get { return OpenCAGE.ModelExport.ModelExporter.ImportFilter(false); } }

        /// <summary>
        /// Open the model browser. Pass <paramref name="wholeModelsOnly"/> to pick a whole .cs2 rather
        /// than one component of one, and <paramref name="modelFilter"/> to narrow what's listed.
        /// </summary>
        public EditModel(Models.CS2.Component.LOD.Submesh defaultSubmesh = null, bool showSelectBtn = true,
                         bool wholeModelsOnly = false, Func<Models.CS2, bool> modelFilter = null)
            : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            _wholeModelsOnly = wholeModelsOnly;
            _modelFilter = modelFilter;
            InitializeComponent();

            splitContainer2.FixedPanel = FixedPanel.Panel2;
            splitContainer2.IsSplitterFixed = true;
            splitContainer2.Resize += SplitContainer2_Resize;
            if (splitContainer2.Panel2 != null)
                splitContainer2.Panel2.SizeChanged += SplitContainer2_Panel2_SizeChanged;
            UpdateFilterPanelWidth();
            LayoutSplitContainer2Panel2Widths();

            useMaterials.Checked = SettingsManager.GetBool(Settings.ShowTexOpt);
            PopulateRenderFlagCheckboxes();

            SettingsManager.SettingsChanged += OnSettingsChanged;
            FormClosed += (s, e) => SettingsManager.SettingsChanged -= OnSettingsChanged;

            treeHelper = new TreeUtility(FileTree, TreeType.MODELS);
            RebuildModelFileTree(Content.Level.Models.FindModelLOD(defaultSubmesh));
            UpdateModelToolsState();

            modelViewer = new GUI_ModelViewer();
            modelRendererHost.Child = modelViewer;

            submeshFilterPanel.VerticalScroll.Visible = true;
            submeshFilterPanel.HorizontalScroll.Enabled = false;
            submeshFilterPanel.SizeChanged += SubmeshFilterPanel_SizeChanged;
            if (submeshFilterGroup != null)
                submeshFilterGroup.SizeChanged += SubmeshFilterGroup_SizeChanged;
            selectModelBtn.Visible = showSelectBtn;

            this.Disposed += SelectModel_Disposed;
            FileTree.ImageList = imageList1;

            this.Load += EditModel_LoadSyncPanel2Widths;
        }

        private void EditModel_LoadSyncPanel2Widths(object sender, EventArgs e)
        {
            this.Load -= EditModel_LoadSyncPanel2Widths;
            LayoutSplitContainer2Panel2Widths();
            LayoutSubmeshLodGroupWidths();
        }

        private void SelectModel_Disposed(object sender, EventArgs e)
        {
            ClearSubmeshCheckboxes();
            allSubmeshes.Clear();
            
            treeHelper?.ForceClearTree();
            treeHelper = null;

            if (submeshFilterPanel != null)
                submeshFilterPanel.SizeChanged -= SubmeshFilterPanel_SizeChanged;
            if (submeshFilterGroup != null)
                submeshFilterGroup.SizeChanged -= SubmeshFilterGroup_SizeChanged;
            if (splitContainer2?.Panel2 != null)
                splitContainer2.Panel2.SizeChanged -= SplitContainer2_Panel2_SizeChanged;

            modelViewer = null;

            if (modelRendererHost != null)
                modelRendererHost.Dispose();
        }

        private string CreateTagForMesh(Models.CS2 cs2, Models.CS2.Component.LOD lod, Models.CS2.Component component)
        {
            string tag = cs2.Name.Replace('\\', '/') + "/[" + cs2.Components.IndexOf(component).ToString("00") + "] " + lod.Name.Replace('\\', '/');
            if (tag.Length > 0 && tag[0] == '/')
                tag = tag.Substring(1);
            return tag;
        }
        private string CreateTagForMesh(Models.CS2.Component.LOD lod)
        {
            Models.CS2.Component component = Content.Level.Models.FindModelComponent(lod);
            Models.CS2 cs2 = Content.Level.Models.FindModel(component);
            string tag = cs2.Name.Replace('\\', '/') + "/[" + cs2.Components.IndexOf(component).ToString("00") + "] " + lod.Name.Replace('\\', '/');
            if (tag.Length > 0 && tag[0] == '/')
                tag = tag.Substring(1);
            return tag;
        }

        private void RebuildModelFileTree(Models.CS2.Component.LOD selectedLOD = null, string filter = null)
        {
            if (Content?.Level?.Models == null || treeHelper == null) return;
            string trimmedFilter = string.IsNullOrWhiteSpace(filter) ? null : filter.Trim();
            List<string> allModelFileNames = new List<string>();
            List<Models.CS2.Component.LOD> allModelTagsModels = new List<Models.CS2.Component.LOD>();

            if (_wholeModelsOnly)
            {
                foreach (Models.CS2 mesh in Content.Level.Models.Entries)
                {
                    if (_modelFilter != null && !_modelFilter(mesh)) continue;
                    if (trimmedFilter != null && (mesh.Name ?? "").IndexOf(trimmedFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    //the tree keys off the path, so naming the leaf after the .cs2 stops it at the model
                    Models.CS2.Component.LOD lod = mesh.Components
                        .FirstOrDefault(x => x.LODs.Count != 0 && x.LODs[0].Submeshes.Count != 0)?.LODs[0];
                    if (lod == null) continue;

                    allModelFileNames.Add((mesh.Name ?? "").Replace('\\', '/').TrimStart('/'));
                    allModelTagsModels.Add(lod);
                }
                treeHelper.UpdateFileTree(allModelFileNames, null, null, allModelTagsModels);
                treeHelper.SelectNode(selectedLOD);
                return;
            }

            foreach (Models.CS2 mesh in Content.Level.Models.Entries)
            {
                if (_modelFilter != null && !_modelFilter(mesh)) continue;
                foreach (Models.CS2.Component component in mesh.Components)
                {
                    if (component.LODs.Count == 0)
                        continue;

                    Models.CS2.Component.LOD lod0 = component.LODs[0];

                    if (lod0.Submeshes.Count == 0)
                        continue;

                    string tag = CreateTagForMesh(mesh, lod0, component);
                    if (trimmedFilter != null &&
                        tag.IndexOf(trimmedFilter, StringComparison.OrdinalIgnoreCase) < 0 &&
                        (string.IsNullOrEmpty(mesh.Name) || mesh.Name.IndexOf(trimmedFilter, StringComparison.OrdinalIgnoreCase) < 0))
                        continue;

                    allModelFileNames.Add(tag);
                    allModelTagsModels.Add(lod0);
                }
            }
            treeHelper.UpdateFileTree(allModelFileNames, null, null, allModelTagsModels);
            treeHelper.SelectNode(selectedLOD);
        }

        private void ApplyModelSearch()
        {
            Models.CS2.Component.LOD selectedLOD = null;
            if (FileTree.SelectedNode != null)
                selectedLOD = ((TreeItem)FileTree.SelectedNode.Tag).Model_Value;

            RebuildModelFileTree(selectedLOD, modelSearchTextBox.Text);
        }

        private void modelSearchButton_Click(object sender, EventArgs e)
        {
            ApplyModelSearch();
        }

        private void modelSearchClearButton_Click(object sender, EventArgs e)
        {
            Models.CS2.Component.LOD selectedLOD = null;
            if (FileTree.SelectedNode != null)
                selectedLOD = ((TreeItem)FileTree.SelectedNode.Tag).Model_Value;

            modelSearchTextBox.Text = string.Empty;
            RebuildModelFileTree(selectedLOD);
        }

        private void modelSearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                modelSearchButton.PerformClick();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void UpdateModelToolsState()
        {
            bool canExportOrEdit = TryGetSelectedCs2(out _);
            exportCs2Btn.Enabled = canExportOrEdit;
            editGeometryBtn.Enabled = canExportOrEdit;
            deleteBtn.Enabled = canExportOrEdit;
            importModelBtn.Enabled = Content?.Level?.Models != null;
            SetRenderFlagCheckboxesEnabled(canExportOrEdit && allSubmeshes.Count > 0);
        }

        private bool TryGetSelectedCs2(out Models.CS2 cs2)
        {
            cs2 = null;
            if (Content?.Level?.Models == null || FileTree.SelectedNode == null) return false;
            var tag = (TreeItem)FileTree.SelectedNode.Tag;
            if (tag.Item_Type == TreeItemType.EXPORTABLE_FILE)
            {
                cs2 = Content.Level.Models.FindModel(tag.Model_Value);
                return cs2 != null;
            }
            if (tag.Item_Type == TreeItemType.DIRECTORY)
            {
                if (!(FileTree.SelectedNode.Nodes.Count > 0 && FileTree.SelectedNode.Nodes[0].Nodes.Count == 0))
                    return false;
                cs2 = Content.Level.Models.FindModel(tag.Model_Value);
                return cs2 != null;
            }
            return false;
        }

        private void importModelBtn_Click(object sender, EventArgs e)
        {
            if (Content?.Level?.Models == null) return;
            OpenFileDialog picker = new OpenFileDialog();
            picker.Filter = ModelImportFilter;
            picker.FilterIndex = 1;
            picker.DefaultExt = "fbx";
            if (picker.ShowDialog() != DialogResult.OK) return;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                Scene importScene;
                using (AssimpContext importer = new AssimpContext())
                {
                    importScene = importer.ImportFile(picker.FileName, ModelIO.ImportPostProcessSteps);
                }
                if (importScene == null || importScene.MeshCount == 0)
                {
                    MessageBox.Show("Failed to load model or no mesh data found. Ensure meshes are under the scene root.", "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                Models.CS2.Component.LOD toSelect = null;
                /* The name, and any folders it should live in, are settled in the preview - and the
                 * clash is checked there as it's typed, so nothing gets silently renamed after the
                 * fact any more. */
                using (var previewForm = new ModelImportPreview(importScene, picker.FileName, Content.Level.Materials,
                    () => Content.Level.Models.Entries.Select(x => x.Name), Content.Level))
                {
                    if (previewForm.ShowDialog(this) != DialogResult.OK || previewForm.ResultCs2 == null)
                        return;

                    /* Belt and braces: models are keyed by name when the level is read back, so two
                     * sharing one would merge into a single entry. */
                    string requestedName = previewForm.ResultCs2.Name;
                    previewForm.ResultCs2.Name = MakeModelNameUnique(requestedName);
                    if (previewForm.ResultCs2.Name != requestedName)
                        MessageBox.Show("This level already contains a model called '" + requestedName + "', so the import has been named '" + previewForm.ResultCs2.Name + "'.\n\n" +
                            "Anything already placed in the level still points at the original model - use the entity's Resource to point it at this one.",
                            "Imported under a new name", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    Content.Level.Models.Entries.Add(previewForm.ResultCs2);
                    if (previewForm.ResultCs2.Components.Count > 0 && previewForm.ResultCs2.Components[0].LODs.Count > 0)
                        toSelect = previewForm.ResultCs2.Components[0].LODs[0];

                    /* The skeleton and any animation go into the game's animation data rather than
                     * this level, so they are added here once the model itself is safely in. */
                    ApplyImportedRig(previewForm.ResultRig, previewForm.RigSituation, picker.FileName);

                    //Placed in a composite of its own, if asked, so it can be dragged into the level straight away
                    if (previewForm.CompositeName != null)
                        CreateCompositeForImport(previewForm.ResultCs2, previewForm.CompositeName, previewForm.CompositeSkeleton);
                }
                RebuildModelFileTree(toSelect, modelSearchTextBox.Text);
                Singleton.OnResourceModified?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        /// <summary>
        /// Build the composite an import asked for. The model is already in the level by now, so a
        /// failure here is reported rather than fatal.
        /// </summary>
        private void CreateCompositeForImport(Models.CS2 model, string compositeName, Skeleton skeleton)
        {
            try
            {
                Singleton.OnCompositeAddPending?.Invoke();
                ModelCompositeBuilder.Result result = ModelCompositeBuilder.Create(Content.Level, model, compositeName, skeleton, skeleton?.Name);
                Singleton.OnCompositeAdded?.Invoke(result.Composite);

                string message = "Created composite '" + result.Composite.name + "' with " + result.ModelReferences
                    + (result.ModelReferences == 1 ? " ModelReference" : " ModelReferences") + ".";
                if (result.Notes.Count != 0)
                    message += "\r\n\r\n" + string.Join("\r\n", result.Notes);
                MessageBox.Show(message, "Composite created", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("The model was imported, but its composite could not be created:\r\n\r\n" + ex.Message, "Composite not created", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Add the clips a model import asked for. These live in ANIMATION.PAK, which is shared by
        /// every level rather than held in this one, so they are written out here rather than left
        /// for the animation browser - and the model is already in by this point, so a failure here
        /// is reported rather than fatal.
        /// </summary>
        private void ApplyImportedRig(ModelImportRig.Outcome outcome, ModelImportRig.Situation situation, string sourceFile)
        {
            if (outcome == null || situation == null) return;

            List<string> report = new List<string>(outcome.Report);
            string problem = null;
            int imported = 0;
            bool written = true;
            HashSet<string> sets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (outcome.ImportAnimations)
            {
                if (Singleton.Animations == null)
                    problem = "The game's animation data isn't loaded, so nothing could be added to it.";
                else
                {
                    /* One pass of the animation browser's own import window per clip in the file. A
                     * model import is not a reason to make the same choices a second time somewhere
                     * worse: this is the window carrying the set, the rig, the frame rate, the root
                     * handling and the preview, started on whatever the model import already knows. */
                    Cursor.Current = Cursors.Default;
                    for (int i = 0; i < situation.Animations.Count; i++)
                        using (ImportAnimation import = new ImportAnimation(Singleton.Animations, null, sourceFile,
                                                                            outcome.PreferredRig, i,
                                                                            situation.Animations[i], outcome.AnimationSetName))
                        {
                            if (import.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(import.ImportedName)) continue;
                            imported++;
                            sets.Add(import.ImportedSet);
                            Singleton.RegisterAnimation(import.ImportedSet, import.ImportedName);
                        }
                }
            }

            if (imported != 0)
            {
                report.Add("Imported " + imported + " animation" + (imported == 1 ? "" : "s")
                    + " into '" + string.Join("', '", sets) + "'.");
                Singleton.OnAnimationsModified?.Invoke();

                /* Written out straight away, the same way the animation browser writes its own
                 * imports. An import nobody saves has done nothing at all, and leaving it to be saved
                 * somewhere else is asking someone to finish a job they didn't know they had. */
                written = SaveAnimations(out string writeProblem);
                report.Add(written
                    ? "ANIMATION.PAK has been written."
                    : "ANIMATION.PAK could not be written, so this is only in memory for now - try saving"
                      + " from the animation browser.\r\n\r\n" + writeProblem);
            }

            if (report.Count == 0 && problem == null) return;
            bool ok = written && problem == null;
            MessageBox.Show(string.Join("\r\n", report)
                    + (problem == null ? "" : (report.Count == 0 ? "" : "\r\n\r\n") + problem),
                ok ? "Model imported" : "Partly imported", MessageBoxButtons.OK,
                ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        /* Write ANIMATION.PAK back - the same two steps the animation browser takes, giving the mod
         * services their chance to record what the file looked like before it changed. */
        private bool SaveAnimations(out string problem)
        {
            problem = null;
            try
            {
                Modding.ModServices.CaptureBeforeWrite(Singleton.Animations.PAK.Filepath);
                if (Singleton.Animations.Save()) return true;
                problem = "The file could not be written to.";
                return false;
            }
            catch (Exception ex)
            {
                problem = ex.Message;
                return false;
            }
        }

        private string MakeModelNameUnique(string name)
        {
            if (string.IsNullOrEmpty(name) || Content?.Level?.Models == null) return name;
            if (!Content.Level.Models.Entries.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
                return name;

            string extension = Path.GetExtension(name);
            string baseName = name.Substring(0, name.Length - extension.Length);
            for (int i = 1; ; i++)
            {
                string candidate = baseName + "_" + i + extension;
                if (!Content.Level.Models.Entries.Any(x => string.Equals(x.Name, candidate, StringComparison.OrdinalIgnoreCase)))
                    return candidate;
            }
        }

        private void exportCs2Btn_Click(object sender, EventArgs e)
        {
            if (!TryGetSelectedCs2(out Models.CS2 cs2)) return;

            /* A skinned model is only half a model without the rig its weights point at, so offer
             * the skeleton up front - the file dialog is no use to anyone if we get this wrong. */
            Skeleton skeleton = null;
            if (Skeleton.RequiredBoneCount(cs2) != 0 && Singleton.Global?.Skeletons != null)
            {
                using (SkeletonPicker skeletonPicker = new SkeletonPicker(cs2))
                {
                    if (skeletonPicker.ShowDialog(this) != DialogResult.OK) return;
                    skeleton = skeletonPicker.Result;
                }
            }

            SaveFileDialog picker = new SaveFileDialog();

            /* FBX and glTF both carry everything we write. Open on whichever was picked last, so a
             * run of exports doesn't mean choosing the format again every time. */
            picker.Filter = ModelFileFilter;
            picker.FilterIndex = ModelExporter.FilterIndex(SettingsManager.GetString(Settings.ModelExportFormat, ".fbx"), false);
            picker.DefaultExt = "fbx";
            picker.AddExtension = true;
            picker.FileName = Path.GetFileNameWithoutExtension(cs2.Name ?? "model") + ".fbx";
            if (picker.ShowDialog() != DialogResult.OK) return;

            SettingsManager.SetString(Settings.ModelExportFormat, ModelExporter.For(picker.FileName).Extension);
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                cs2.ExportMesh(picker.FileName, skeleton);
                MessageBox.Show("Successfully exported file.\n\nA '" + ModelIO.SidecarExtension + "' file has been written alongside it, holding the parts of the model the mesh format can't store. " +
                    "Keep it next to the model (and keep the object names intact) and the model can be imported straight back in." +
                    (skeleton == null ? "" : "\n\nThe '" + skeleton.Name + "' skeleton has been written in with the mesh bound to it."),
                    "Export complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }
        }

        private void deleteBtn_Click(object sender, EventArgs e)
        {
            if (!TryGetSelectedCs2(out Models.CS2 cs2)) return;

            //The engine indexes the required models positionally at the head of the pak, and the
            //instancing pass points fog volumes, light proxies, particles and decals at them by
            //that position. Removing one takes the mesh out from under every mover that uses it.
            if (RequiredModels.IsRequiredEntry(Content.Level.Models, cs2))
            {
                MessageBox.Show("'" + cs2.Name + "' is one of the models every level is required to carry. "
                    + "Fog volumes, light proxies, particles and decals are all drawn with it.",
                    "Required model", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show("Are you sure you want to delete '" + cs2.Name + "'?", "About to delete...", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            Content.Level.Models.Entries.Remove(cs2);
            RebuildModelFileTree(filter: modelSearchTextBox.Text);
        }

        private void editGeometryBtn_Click(object sender, EventArgs e)
        {
            if (!TryGetSelectedCs2(out Models.CS2 cs2)) return;
            var editor = new ModelEditor(cs2);
            editor.FormClosed += (s, ev) =>
            {
                RebuildModelFileTree(filter: modelSearchTextBox.Text);
                Singleton.OnResourceModified?.Invoke();
            };
            editor.Show(this);
        }

        private void FileTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            ClearSubmeshCheckboxes();
            selectModelBtn.Enabled = false;
            allSubmeshes.Clear();
            modelPreviewArea.Text = "";
            modelViewer.ShowModel(new List<GUI_ModelViewer.Model>());
            ResetRenderFlagCheckboxes();

            try
            {
                if (FileTree.SelectedNode == null) return;

                Models.CS2.Component.LOD lod = ((TreeItem)FileTree.SelectedNode.Tag).Model_Value;

                switch (((TreeItem)FileTree.SelectedNode.Tag).Item_Type)
                {
                    case TreeItemType.EXPORTABLE_FILE:
                        {
                            if (_wholeModelsOnly)
                            {
                                //the leaf is the whole model here, so show all of it at once
                                Models.CS2 whole = Content.Level.Models.FindModel(lod);
                                int at = 0;
                                foreach (Models.CS2.Component c in whole.Components)
                                {
                                    AddComponent(c, at);
                                    at += c.LODs.Count;
                                }
                                modelPreviewArea.Text = whole.Name.Replace('\\', '/');
                            }
                            else
                            {
                                AddComponent(Content.Level.Models.FindModelComponent(lod));
                                modelPreviewArea.Text = CreateTagForMesh(lod);
                            }
                            selectModelBtn.Enabled = true;
                        }
                        break;
                    case TreeItemType.DIRECTORY:
                        {
                            if (!(FileTree.SelectedNode.Nodes.Count > 0 && FileTree.SelectedNode.Nodes[0].Nodes.Count == 0))
                                return;

                            Models.CS2 mesh = Content.Level.Models.FindModel(lod);
                            int index = 0;
                            foreach (Models.CS2.Component c in mesh.Components)
                            {
                                AddComponent(c, index);
                                index += c.LODs.Count;
                            }
                            modelPreviewArea.Text = mesh.Name.Replace('\\', '/');
                        }
                        break;
                    default:
                        return;
                }

                UpdateFilteredModel(true);
                LayoutSplitContainer2Panel2Widths();
                UpdateLODGroupLayouts();
                ApplyRenderFlagsUiFromSelection();
            }
            finally
            {
                UpdateModelToolsState();
            }
        }

        private void AddComponent(Models.CS2.Component component, int baseIndex = 0)
        {
            for (int x = baseIndex; x < component.LODs.Count + baseIndex; x++)
                CreateLODGroup(x, component.LODs[x - baseIndex].Name);

            for (int x = 0; x < component.LODs.Count; x++)
            {
                int yOffset = 0;
                for (int y = 0; y < component.LODs[x].Submeshes.Count; y++)
                {
                    allSubmeshes[component.LODs[x].Submeshes[y]] = new GUI_ModelViewer.Model(component.LODs[x].Submeshes[y]);

                    /* Only the first LOD shows by default, and not the collision hull even then -
                     * it's bound to the same rig as the body and sits right on top of it. */
                    bool isEnabled = (x == 0) && !IsCollisionMesh(component.LODs[x], component.LODs[x].Submeshes[y]);
                    CreateSubmeshCheckbox(component.LODs[x].Submeshes[y], x + baseIndex, y, component.LODs[x].Submeshes.Count, isEnabled, yOffset);
                    yOffset += 25;
                }
            }
        }

        /* CA name their collision meshes and the materials on them "COL_" - the COL_MALE hull on
         * every human character, COL_ANDROID on the androids. Nothing else uses the prefix. */
        private static bool IsCollisionMesh(Models.CS2.Component.LOD lod, Models.CS2.Component.LOD.Submesh submesh)
        {
            return HasCollisionPrefix(lod?.Name) || HasCollisionPrefix(submesh?.Material?.Name);
        }

        private static bool HasCollisionPrefix(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            string last = name.Substring(name.LastIndexOfAny(new[] { '\\', '/' }) + 1);
            return last.StartsWith("COL_", StringComparison.OrdinalIgnoreCase)
                || last.IndexOf("_COL_", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ClearSubmeshCheckboxes()
        {
            foreach (CheckBox cb in submeshCheckboxes)
            {
                cb.CheckedChanged -= SubmeshCheckbox_CheckedChanged;
                checkboxToModelIndex.Remove(cb);
                cb.Dispose();
            }
            submeshCheckboxes.Clear();
            checkboxToModelIndex.Clear();
            submeshRowInfoButtons.Clear();

            foreach (var lodGroup in lodGroups.Values)
            {
                lodGroup.Dispose();
            }
            lodGroups.Clear();
            lodToCheckboxes.Clear();
            submeshFilterPanel.Controls.Clear();
            submeshFilterPanel.VerticalScroll.Visible = true;
        }

        private void CreateLODGroup(int lodIndex, string lodName)
        {
            GroupBox lodGroup = new GroupBox();
            lodGroup.Text = lodName;
            lodGroup.AutoSize = false;
            lodGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            
            Button selectAllBtn = new Button();
            selectAllBtn.Text = "Show All";
            selectAllBtn.Size = new Size(80, 23);
            selectAllBtn.Location = new Point(6, 15);
            selectAllBtn.Tag = lodIndex;
            selectAllBtn.Click += (s, e) => {
                int lod = (int)((Button)s).Tag;
                CheckAllSubmeshes(lod, true);
            };
            lodGroup.Controls.Add(selectAllBtn);

            Button deselectAllBtn = new Button();
            deselectAllBtn.Text = "Hide All";
            deselectAllBtn.Size = new Size(80, 23);
            deselectAllBtn.Location = new Point(92, 15);
            deselectAllBtn.Tag = lodIndex;
            deselectAllBtn.Click += (s, e) => {
                int lod = (int)((Button)s).Tag;
                CheckAllSubmeshes(lod, false);
            };
            lodGroup.Controls.Add(deselectAllBtn);

            lodGroups[lodIndex] = lodGroup;
            lodToCheckboxes[lodIndex] = new List<CheckBox>();
        }

        private void CheckAllSubmeshes(int lodIndex, bool state)
        {
            if (lodToCheckboxes.ContainsKey(lodIndex))
            {
                foreach (CheckBox cb in lodToCheckboxes[lodIndex])
                    cb.CheckedChanged -= SubmeshCheckbox_CheckedChanged;
                foreach (CheckBox cb in lodToCheckboxes[lodIndex])
                    cb.Checked = state;
                foreach (CheckBox cb in lodToCheckboxes[lodIndex])
                    cb.CheckedChanged += SubmeshCheckbox_CheckedChanged;

                UpdateFilteredModel(false);
            }
        }

        private void CreateSubmeshCheckbox(Models.CS2.Component.LOD.Submesh model, int lodIndex, int submeshIndex, int totalSubmeshes, bool isChecked, int yOffset)
        {
            CheckBox checkbox = new CheckBox();
            checkbox.AutoSize = false;
            checkbox.Checked = isChecked;
            
            GroupBox lodGroup = lodGroups[lodIndex];
            const int rowLeft = 6;
            const int infoBtnW = 22;
            const int gap = 3;
            int rowInnerW = lodGroup.ClientSize.Width - rowLeft - gap;
            int cbW = rowInnerW - infoBtnW - gap;

            checkbox.Location = new Point(rowLeft, 45 + yOffset);
            checkbox.Size = new Size(Math.Max(40, cbW), 22);
            checkbox.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            checkbox.Text = "Submesh " + submeshIndex + " (" + model.VertexCount + " verts)";
            checkbox.Tag = model;
            
            checkbox.CheckedChanged += SubmeshCheckbox_CheckedChanged;

            Button infoBtn = new Button();
            infoBtn.Text = "i";
            infoBtn.Size = new Size(infoBtnW, 22);
            infoBtn.Location = new Point(rowLeft + checkbox.Width + gap, 44 + yOffset);
            infoBtn.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            infoBtn.TabStop = true;
            infoBtn.FlatStyle = FlatStyle.Flat;
            infoBtn.UseVisualStyleBackColor = true;
            infoBtn.Font = new Font(infoBtn.Font.FontFamily, 8.25f, FontStyle.Bold);
            infoBtn.Click += (s, e) => SubmeshInfoForm.ShowFor(this, model);
            
            submeshCheckboxes.Add(checkbox);
            checkboxToModelIndex[checkbox] = model;
            lodToCheckboxes[lodIndex].Add(checkbox);
            lodGroup.Controls.Add(checkbox);
            lodGroup.Controls.Add(infoBtn);
            submeshRowInfoButtons[checkbox] = infoBtn;
            LayoutSubmeshRow(lodGroup, checkbox, infoBtn);
        }

        private static void LayoutSubmeshRow(GroupBox lodGroup, CheckBox checkbox, Button infoBtn)
        {
            const int rowLeft = 6;
            const int marginRight = 12;
            const int infoBtnW = 22;
            const int gap = 4;
            int rowTop = checkbox.Top;
            infoBtn.Location = new Point(lodGroup.ClientSize.Width - marginRight - infoBtnW, rowTop);
            int cbWidth = Math.Max(48, infoBtn.Left - gap - rowLeft);
            checkbox.Location = new Point(rowLeft, rowTop);
            checkbox.Size = new Size(cbWidth, 22);
        }

        private void LayoutLodGroupTopButtons(GroupBox lodGroup)
        {
            Button showAll = null;
            Button hideAll = null;
            foreach (Control c in lodGroup.Controls)
            {
                if (c is Button b && b.Text == "Show All")
                    showAll = b;
                else if (c is Button b2 && b2.Text == "Hide All")
                    hideAll = b2;
            }
            if (showAll == null || hideAll == null)
                return;
            const int topY = 15;
            const int edge = 12;
            showAll.Location = new Point(6, topY);
            hideAll.Location = new Point(lodGroup.ClientSize.Width - hideAll.Width - edge, topY);
        }

        private int GetSubmeshLodGroupTargetWidth(int horizontalMargin)
        {
            if (submeshFilterPanel == null)
                return 200;

            int inner = submeshFilterPanel.ClientSize.Width;
            if (submeshFilterGroup != null && submeshFilterGroup.IsHandleCreated)
                inner = Math.Min(inner, submeshFilterGroup.ClientSize.Width);

            const int paintSafety = 2;
            int available = inner - horizontalMargin * 2 - paintSafety;
            return Math.Max(80, available);
        }

        private void RelayoutLodGroupContents(int lodIndex)
        {
            if (!lodGroups.TryGetValue(lodIndex, out GroupBox lodGroup))
                return;
            LayoutLodGroupTopButtons(lodGroup);
            if (!lodToCheckboxes.TryGetValue(lodIndex, out List<CheckBox> list))
                return;
            foreach (CheckBox cb in list)
            {
                if (submeshRowInfoButtons.TryGetValue(cb, out Button infoBtn))
                    LayoutSubmeshRow(lodGroup, cb, infoBtn);
            }
        }

        private void SubmeshFilterPanel_SizeChanged(object sender, EventArgs e)
        {
            LayoutSubmeshLodGroupWidths();
        }

        private void SubmeshFilterGroup_SizeChanged(object sender, EventArgs e)
        {
            LayoutSubmeshLodGroupWidths();
        }

        private void LayoutSubmeshLodGroupWidths()
        {
            if (lodGroups.Count == 0)
                return;
            const int marginH = 6;
            int groupW = GetSubmeshLodGroupTargetWidth(marginH);
            foreach (var kvp in lodGroups.OrderBy(x => x.Key))
            {
                GroupBox g = kvp.Value;
                g.Width = groupW;
                g.Left = marginH;
                RelayoutLodGroupContents(kvp.Key);
            }
        }

        private void UpdateLODGroupLayouts()
        {
            const int marginH = 6;
            int groupW = GetSubmeshLodGroupTargetWidth(marginH);
            int currentYPos = 5;
            foreach (var kvp in lodGroups.OrderBy(x => x.Key))
            {
                int lodIndex = kvp.Key;
                GroupBox lodGroup = kvp.Value;

                int checkboxCount = lodToCheckboxes[lodIndex].Count;
                int groupHeight = 45 + (checkboxCount * 25) + 5;
                lodGroup.Width = groupW;
                lodGroup.Left = marginH;
                lodGroup.Height = groupHeight;
                lodGroup.Location = new Point(marginH, currentYPos);

                RelayoutLodGroupContents(lodIndex);

                currentYPos += groupHeight + 5;

                submeshFilterPanel.Controls.Add(lodGroup);
            }
        }

        private void SubmeshCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateFilteredModel(false);
        }

        private void UpdateFilteredModel(bool zoomExtents)
        {
            if (allSubmeshes.Count == 0)
                return;

            List<GUI_ModelViewer.Model> filteredModels = new List<GUI_ModelViewer.Model>();
            
            foreach (CheckBox cb in submeshCheckboxes)
            {
                if (cb.Checked && checkboxToModelIndex.ContainsKey(cb))
                {
                    if (allSubmeshes.TryGetValue(checkboxToModelIndex[cb], out GUI_ModelViewer.Model model))
                        filteredModels.Add(model);
                }
            }

            modelViewer.ShowModel(filteredModels, zoomExtents);
        }

        private void selectModel_Click(object sender, EventArgs e)
        {
            Models.CS2.Component.LOD lod = ((TreeItem)FileTree.SelectedNode.Tag).Model_Value;
            if (_wholeModelsOnly) OnWholeModelSelected?.Invoke(Content.Level.Models.FindModel(lod));
            else OnModelSelected?.Invoke(Content.Level.Models.FindModelComponent(lod));
            this.Close();
        }

        private void useMaterials_CheckedChanged(object sender, EventArgs e)
        {
            if (_applyingExternalSettings)
                return;

            SettingsManager.SetBool(Settings.ShowTexOpt, useMaterials.Checked);
            UpdateFilteredModel(false);
        }

        private void OnSettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            if (!e.ExternalChange || IsDisposed)
                return;

            if (!SettingsChangedEventArgs.ContainsKey(e.ChangedKeys, Settings.ShowTexOpt))
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnSettingsChanged(sender, e)));
                return;
            }

            _applyingExternalSettings = true;
            try
            {
                useMaterials.Checked = SettingsManager.GetBool(Settings.ShowTexOpt);
                UpdateFilteredModel(false);
            }
            finally
            {
                _applyingExternalSettings = false;
            }
        }

        private void SplitContainer2_Resize(object sender, EventArgs e)
        {
            UpdateFilterPanelWidth();
            LayoutSplitContainer2Panel2Widths();
            LayoutSubmeshLodGroupWidths();
        }

        private void SplitContainer2_Panel2_SizeChanged(object sender, EventArgs e)
        {
            LayoutSplitContainer2Panel2Widths();
            LayoutSubmeshLodGroupWidths();
        }

        private void LayoutSplitContainer2Panel2Widths()
        {
            if (splitContainer2?.Panel2 == null || !splitContainer2.Panel2.IsHandleCreated)
                return;
            int w = Math.Max(1, splitContainer2.Panel2.ClientSize.Width);
            if (submeshFilterGroup != null)
            {
                submeshFilterGroup.Left = 0;
                submeshFilterGroup.Width = w;
            }
            if (renderFlagsGroup != null)
            {
                renderFlagsGroup.Left = 0;
                renderFlagsGroup.Width = w;
            }
            if (tableLayoutPanel2 != null)
            {
                tableLayoutPanel2.Left = 0;
                tableLayoutPanel2.Width = w;
            }
        }

        private void UpdateFilterPanelWidth()
        {
            const int filterPanelWidth = 262;
            const int splitterWidth = 5;
            
            if (splitContainer2.Width > filterPanelWidth + splitterWidth)
            {
                splitContainer2.SplitterDistance = splitContainer2.Width - filterPanelWidth - splitterWidth;
            }
        }

        private void PopulateRenderFlagCheckboxes()
        {
            if (_renderFlagChecks.Count > 0)
                return;

            if (renderFlagsPanel != null)
                renderFlagsPanel.Padding = new Padding(10, 2, 4, 2);

            foreach (string name in Enum.GetNames(typeof(Models.CS2.Component.LOD.RenderingFlag)))
            {
                var flag = (Models.CS2.Component.LOD.RenderingFlag)Enum.Parse(typeof(Models.CS2.Component.LOD.RenderingFlag), name);
                if (!IsPowerOfTwoEnumMember(flag))
                    continue;

                CheckBox cb = new CheckBox
                {
                    Text = name.Replace("_", " "),
                    AutoSize = true,
                    Margin = new Padding(2, 0, 8, 2),
                    Enabled = false
                };
                cb.CheckedChanged += RenderFlag_CheckedChanged;
                renderFlagsPanel.Controls.Add(cb);
                _renderFlagChecks.Add((flag, cb));
            }
        }

        private static bool IsPowerOfTwoEnumMember(Enum value)
        {
            ulong u = Convert.ToUInt64(value);
            if (u == 0)
                return false;
            return (u & (u - 1)) == 0;
        }

        private List<Models.CS2.Component.LOD.Submesh> GetSelectedSubmeshes()
        {
            return allSubmeshes.Keys.ToList();
        }

        private void ApplyRenderFlagsUiFromSelection()
        {
            List<Models.CS2.Component.LOD.Submesh> selectedSubmeshes = GetSelectedSubmeshes();
            if (selectedSubmeshes.Count == 0)
            {
                ResetRenderFlagCheckboxes();
                return;
            }

            _suppressRenderFlagChange = true;
            foreach (var pair in _renderFlagChecks)
            {
                bool allHaveFlag = selectedSubmeshes.All(submesh => submesh.RenderFlags.HasFlag(pair.flag));
                pair.cb.Checked = allHaveFlag;
            }
            _suppressRenderFlagChange = false;
            SetRenderFlagCheckboxesEnabled(true);
        }

        private void ResetRenderFlagCheckboxes()
        {
            _suppressRenderFlagChange = true;
            foreach (var pair in _renderFlagChecks)
            {
                pair.cb.Checked = false;
                pair.cb.Enabled = false;
            }
            _suppressRenderFlagChange = false;
        }

        private void SetRenderFlagCheckboxesEnabled(bool enabled)
        {
            foreach (var pair in _renderFlagChecks)
                pair.cb.Enabled = enabled;
        }

        private void RenderFlag_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressRenderFlagChange)
                return;

            if (!(sender is CheckBox changedCheck))
                return;

            var changedPair = _renderFlagChecks.FirstOrDefault(x => x.cb == changedCheck);
            if (changedPair.cb == null)
                return;

            List<Models.CS2.Component.LOD.Submesh> selectedSubmeshes = GetSelectedSubmeshes();
            if (selectedSubmeshes.Count == 0)
                return;

            foreach (Models.CS2.Component.LOD.Submesh submesh in selectedSubmeshes)
            {
                if (changedCheck.Checked)
                    submesh.RenderFlags |= changedPair.flag;
                else
                    submesh.RenderFlags &= ~changedPair.flag;
            }

            Singleton.OnResourceModified?.Invoke();
        }
    }
}
