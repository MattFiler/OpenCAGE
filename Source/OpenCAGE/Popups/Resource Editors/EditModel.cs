using AlienPAK;
using Assimp;
using CATHODE;
using CathodeLib;
using CommandsEditor.Popups.Base;
using CommandsEditor.Popups.UserControls;
using OpenCAGE;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CommandsEditor
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
        private readonly List<(Models.CS2.Component.LOD.RenderingFlag flag, CheckBox cb)> _renderFlagChecks = new List<(Models.CS2.Component.LOD.RenderingFlag, CheckBox)>();
        private bool _suppressRenderFlagChange;

        public Action<Models.CS2.Component> OnModelSelected;

        public EditModel(Models.CS2.Component.LOD.Submesh defaultSubmesh = null, bool showSelectBtn = true) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();

            splitContainer2.FixedPanel = FixedPanel.Panel2;
            splitContainer2.IsSplitterFixed = true;
            splitContainer2.Resize += SplitContainer2_Resize;
            UpdateFilterPanelWidth();

            useMaterials.Checked = SettingsManager.GetBool(Singleton.Settings.ShowTexOpt);
            PopulateRenderFlagCheckboxes();

            treeHelper = new TreeUtility(FileTree, TreeType.MODELS);
            RebuildModelFileTree(Content.Level.Models.FindModelLOD(defaultSubmesh));
            UpdateModelToolsState();

            modelViewer = new GUI_ModelViewer();
            modelRendererHost.Child = modelViewer;

            submeshFilterPanel.VerticalScroll.Visible = true;
            selectModelBtn.Visible = showSelectBtn;

            this.Disposed += SelectModel_Disposed;
            FileTree.ImageList = imageList1;
        }

        private void SelectModel_Disposed(object sender, EventArgs e)
        {
            ClearSubmeshCheckboxes();
            allSubmeshes.Clear();
            
            treeHelper?.ForceClearTree();
            treeHelper = null;

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

        private void RebuildModelFileTree(Models.CS2.Component.LOD selectedLOD = null)
        {
            if (Content?.Level?.Models == null || treeHelper == null) return;
            List<string> allModelFileNames = new List<string>();
            List<Models.CS2.Component.LOD> allModelTagsModels = new List<Models.CS2.Component.LOD>();
            foreach (Models.CS2 mesh in Content.Level.Models.Entries)
            {
                foreach (Models.CS2.Component component in mesh.Components)
                {
                    if (component.LODs.Count == 0)
                        continue;

                    Models.CS2.Component.LOD lod0 = component.LODs[0];

                    if (lod0.Submeshes.Count == 0)
                        continue;

                    allModelFileNames.Add(CreateTagForMesh(mesh, lod0, component));
                    allModelTagsModels.Add(lod0);
                }
            }
            treeHelper.UpdateFileTree(allModelFileNames, null, null, allModelTagsModels);
            treeHelper.SelectNode(selectedLOD);
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
            picker.Filter = "FBX Model|*.fbx|GLTF Model|*.gltf|OBJ Model|*.obj";
            if (picker.ShowDialog() != DialogResult.OK) return;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                Scene importScene;
                using (AssimpContext importer = new AssimpContext())
                {
                    importScene = importer.ImportFile(picker.FileName,
                        PostProcessSteps.Triangulate | PostProcessSteps.FindDegenerates | PostProcessSteps.LimitBoneWeights |
                        PostProcessSteps.GenerateBoundingBoxes | PostProcessSteps.FlipUVs | PostProcessSteps.FlipWindingOrder | PostProcessSteps.MakeLeftHanded);
                }
                if (importScene == null || importScene.MeshCount == 0)
                {
                    MessageBox.Show("Failed to load model or no mesh data found. Ensure meshes are under the scene root.", "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                Models.CS2.Component.LOD toSelect = null;
                using (var previewForm = new ModelImportPreview(importScene, picker.FileName, Content.Level.Materials))
                {
                    if (previewForm.ShowDialog(this) != DialogResult.OK || previewForm.ResultCs2 == null)
                        return;
                    Content.Level.Models.Entries.Add(previewForm.ResultCs2);
                    if (previewForm.ResultCs2.Components.Count > 0 && previewForm.ResultCs2.Components[0].LODs.Count > 0)
                        toSelect = previewForm.ResultCs2.Components[0].LODs[0];
                }
                RebuildModelFileTree(toSelect);
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

        private void exportCs2Btn_Click(object sender, EventArgs e)
        {
            if (!TryGetSelectedCs2(out Models.CS2 cs2)) return;
            SaveFileDialog picker = new SaveFileDialog();
            picker.Filter = "FBX Model|*.fbx|GLTF Model|*.gltf|OBJ Model|*.obj";
            picker.FileName = Path.GetFileNameWithoutExtension(cs2.Name ?? "model");
            if (picker.ShowDialog() != DialogResult.OK) return;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                cs2.ExportMesh(picker.FileName);
                MessageBox.Show("Successfully exported file.", "Export complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            if (MessageBox.Show("Are you sure you want to delete '" + cs2.Name + "'?", "About to delete...", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            Content.Level.Models.Entries.Remove(cs2);
            RebuildModelFileTree();
        }

        private void editGeometryBtn_Click(object sender, EventArgs e)
        {
            if (!TryGetSelectedCs2(out Models.CS2 cs2)) return;
            var editor = new ModelEditor(cs2);
            editor.FormClosed += (s, ev) =>
            {
                RebuildModelFileTree();
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
                            AddComponent(Content.Level.Models.FindModelComponent(lod));
                            modelPreviewArea.Text = CreateTagForMesh(lod);
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

                    bool isEnabled = (x == 0);
                    CreateSubmeshCheckbox(component.LODs[x].Submeshes[y], x + baseIndex, y, component.LODs[x].Submeshes.Count, isEnabled, yOffset);
                    yOffset += 25;
                }
            }
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
            lodGroup.Width = 185;
            
            Button selectAllBtn = new Button();
            selectAllBtn.Text = "Show All";
            selectAllBtn.Size = new Size(80, 23);
            selectAllBtn.Location = new Point(5, 15);
            selectAllBtn.Tag = lodIndex;
            selectAllBtn.Click += (s, e) => {
                int lod = (int)((Button)s).Tag;
                CheckAllSubmeshes(lod, true);
            };
            lodGroup.Controls.Add(selectAllBtn);

            Button deselectAllBtn = new Button();
            deselectAllBtn.Text = "Hide All";
            deselectAllBtn.Size = new Size(80, 23);
            deselectAllBtn.Location = new Point(90, 15);
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
            checkbox.AutoSize = true;
            checkbox.Checked = isChecked;
            
            GroupBox lodGroup = lodGroups[lodIndex];
            checkbox.Location = new Point(10, 45 + yOffset);
            checkbox.Text = "Submesh " + submeshIndex + " (" + model.VertexCount + " verts)";
            checkbox.Tag = model;
            
            checkbox.CheckedChanged += SubmeshCheckbox_CheckedChanged;
            
            submeshCheckboxes.Add(checkbox);
            checkboxToModelIndex[checkbox] = model;
            lodToCheckboxes[lodIndex].Add(checkbox);
            lodGroup.Controls.Add(checkbox);
        }

        private void UpdateLODGroupLayouts()
        {
            int currentYPos = 5;
            foreach (var kvp in lodGroups.OrderBy(x => x.Key))
            {
                int lodIndex = kvp.Key;
                GroupBox lodGroup = kvp.Value;
                
                int checkboxCount = lodToCheckboxes[lodIndex].Count;
                int groupHeight = 45 + (checkboxCount * 25) + 5; 
                lodGroup.Height = groupHeight;
                lodGroup.Location = new Point(5, currentYPos);
                
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
            OnModelSelected?.Invoke(Content.Level.Models.FindModelComponent(((TreeItem)FileTree.SelectedNode.Tag).Model_Value));
            this.Close();
        }

        private void useMaterials_CheckedChanged(object sender, EventArgs e)
        {
            SettingsManager.SetBool(Singleton.Settings.ShowTexOpt, useMaterials.Checked);
            UpdateFilteredModel(false);
        }

        private void SplitContainer2_Resize(object sender, EventArgs e)
        {
            UpdateFilterPanelWidth();
        }

        private void UpdateFilterPanelWidth()
        {
            const int filterPanelWidth = 220;
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

            foreach (string name in Enum.GetNames(typeof(Models.CS2.Component.LOD.RenderingFlag)))
            {
                var flag = (Models.CS2.Component.LOD.RenderingFlag)Enum.Parse(typeof(Models.CS2.Component.LOD.RenderingFlag), name);
                if (!IsPowerOfTwoEnumMember(flag))
                    continue;

                CheckBox cb = new CheckBox
                {
                    Text = name.Replace("_", " "),
                    AutoSize = true,
                    Margin = new Padding(0, 0, 8, 2),
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
