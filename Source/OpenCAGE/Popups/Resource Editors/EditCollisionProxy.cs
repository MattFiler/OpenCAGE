using CATHODE;
using OpenCAGE.Popups.Base;
using OpenCAGE.ModelExport;
using OpenCAGE.Popups.UserControls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class EditCollisionProxy : BaseWindow
    {
        public Action<HavokPackfile.StaticCompoundShape> OnCollisionProxySelected;

        private readonly List<HavokPackfile.StaticCompoundShape> _allCompounds = new List<HavokPackfile.StaticCompoundShape>();
        private HavokPackfile.StaticCompoundShape _current;
        /// <summary>False when opened as the View menu's editor: there is no mapping to hand a proxy back to.</summary>
        private readonly bool _selectionMode;
        private HavokPackfile.StaticCompoundShape _worldPrimary;
        private HavokPackfile.StaticCompoundShape _worldSecondary;
        private GUI_ModelViewer _modelViewer;
        private EditModel _modelPicker;

        /// <summary>
        /// What a proxy made here carries as its Havok userData: retail stores the write index of the row's
        /// physics material. The caller that knows the row sets it; the picker on its own has no row.
        /// </summary>
        public uint DefaultUserData = 0;

        /// <summary>The collision type of the new proxy's own instance: 3 (STANDARD) for world collision rows, 9 (BALLISTICS) for the rest, as retail.</summary>
        public uint DefaultFilterInfo = 9;

        public EditCollisionProxy(HavokPackfile.StaticCompoundShape current = null, bool showSelectBtn = true)
            : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();
            _current = current;
            selectButton.Visible = showSelectBtn;
            _selectionMode = showSelectBtn;
            if (!showSelectBtn)
            {
                //Opened from View as an editor: nothing to pick, so the status line takes the button's room
                Text = "Collision Editor";
                statusLabel.Width = selectButton.Right - statusLabel.Left;
            }

            _modelViewer = new GUI_ModelViewer();
            modelRendererHost.Child = _modelViewer;
            // Detach before components.Dispose(); Disposed runs too late and ElementHost.Child can NRE.
            FormClosing += (s, e) => { DetachModelViewer(); _modelPicker?.Close(); };
            Disposed += (s, e) => DetachModelViewer();

            PopulateList();
            UpdatePreview(_current);
        }

        private void PopulateList()
        {
            _allCompounds.Clear();
            compoundList.Items.Clear();

            HavokPackfile hkx = Content?.Level?.Collision;
            if (hkx == null || !hkx.Loaded)
            {
                statusLabel.Text = "No COLLISION.HKX loaded for this level.";
                selectButton.Enabled = false;
                return;
            }

            _worldPrimary = hkx.WorldHostPrimary;
            _worldSecondary = hkx.WorldHostSecondary;
            _allCompounds.AddRange(hkx.StaticCompoundShapes.OrderBy(c => c.ProxyIndex));
            ApplyFilter();

            if (_current != null)
            {
                foreach (ListViewItem item in compoundList.Items)
                {
                    if (ReferenceEquals(item.Tag, _current))
                    {
                        item.Selected = true;
                        item.EnsureVisible();
                        break;
                    }
                }
            }
        }

        private void ApplyFilter()
        {
            string filter = (searchBox.Text ?? "").Trim();
            compoundList.BeginUpdate();
            compoundList.Items.Clear();

            foreach (HavokPackfile.StaticCompoundShape compound in _allCompounds)
            {
                string role = HostLabel(compound);
                string proxy = compound.ProxyIndex.ToString();
                string instances = compound.Instances?.Count.ToString() ?? "0";
                string offset = "0x" + compound.DataOffset.ToString("X");

                if (filter.Length > 0)
                {
                    string haystack = proxy + " " + instances + " " + offset + " " + role;
                    if (haystack.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }

                var item = new ListViewItem(new[] { proxy, instances, offset, role })
                {
                    Tag = compound
                };
                if (role.Length > 0)
                    item.ForeColor = SystemColors.GrayText;
                compoundList.Items.Add(item);
            }

            compoundList.EndUpdate();
            statusLabel.Text = compoundList.Items.Count + " / " + _allCompounds.Count + " compounds";
            selectButton.Enabled = compoundList.SelectedItems.Count > 0;
        }

        private string HostLabel(HavokPackfile.StaticCompoundShape compound)
        {
            if (ReferenceEquals(compound, _worldPrimary) && ReferenceEquals(compound, _worldSecondary))
                return "World host";
            if (ReferenceEquals(compound, _worldPrimary))
                return "World host (primary)";
            if (ReferenceEquals(compound, _worldSecondary))
                return "World host (secondary)";
            return "";
        }

        private void searchBox_TextChanged(object sender, EventArgs e) => ApplyFilter();

        private void compoundList_SelectedIndexChanged(object sender, EventArgs e)
        {
            selectButton.Enabled = compoundList.SelectedItems.Count > 0;
            if (compoundList.SelectedItems.Count == 0)
            {
                UpdatePreview(null);
                return;
            }
            UpdatePreview(compoundList.SelectedItems[0].Tag as HavokPackfile.StaticCompoundShape);
        }

        private void UpdatePreview(HavokPackfile.StaticCompoundShape compound)
        {
            if (_modelViewer == null)
                return;

            HavokPackfile hkx = Content?.Level?.Collision;
            if (compound == null || hkx == null)
            {
                _modelViewer.ShowPreviewMesh(null);
                previewStatus.Text = "No selection";
                return;
            }

            HavokPackfile.PreviewMesh mesh = hkx.BuildPreviewMesh(compound);
            _modelViewer.ShowPreviewMesh(mesh);

            int instances = compound.Instances?.Count ?? 0;
            string geom = mesh.TriangleCount == 0
                ? "No preview geometry"
                : mesh.TriangleCount.ToString("N0") + " triangle" + (mesh.TriangleCount == 1 ? "" : "s")
                    + " / " + mesh.ShapeCount + " shape" + (mesh.ShapeCount == 1 ? "" : "s");

            string domain = "";
            if (compound.DomainMin.X <= compound.DomainMax.X
                && !float.IsInfinity(compound.DomainMin.X) && !float.IsInfinity(compound.DomainMax.X))
            {
                domain = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "  ·  domain ({0:0.#},{1:0.#},{2:0.#})–({3:0.#},{4:0.#},{5:0.#})",
                    compound.DomainMin.X, compound.DomainMin.Y, compound.DomainMin.Z,
                    compound.DomainMax.X, compound.DomainMax.Y, compound.DomainMax.Z);
            }

            string shapes = "";
            if (compound.Instances != null && compound.Instances.Count > 0)
            {
                var groups = compound.Instances
                    .GroupBy(i => string.IsNullOrEmpty(i.ShapeClassName) ? "?" : ShortHavokClass(i.ShapeClassName))
                    .OrderByDescending(g => g.Count())
                    .Take(4)
                    .Select(g => g.Count() + "×" + g.Key);
                shapes = "  ·  " + string.Join(", ", groups);
            }

            previewStatus.Text = instances.ToString("N0") + " instance" + (instances == 1 ? "" : "s")
                + "  ·  " + geom + domain + shapes;
        }

        private static string ShortHavokClass(string className)
        {
            if (className.StartsWith("hkp", StringComparison.Ordinal))
                return className.Substring(3);
            return className;
        }

        /* A new proxy from a model file: the same importer and post-processing as a model import, so the mesh
           lands in the game's space the way a model would. */
        private void importButton_Click(object sender, EventArgs e)
        {
            if (!CanImport(out string why))
            {
                MessageBox.Show(this, why, "Import collision mesh", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (OpenFileDialog picker = new OpenFileDialog())
            {
                picker.Filter = ModelExporter.ImportFilter(false);
                picker.FilterIndex = 1;
                picker.Title = "Import a mesh as a new collision proxy";
                if (picker.ShowDialog(this) != DialogResult.OK)
                    return;

                CollisionProxyImporter.MeshSource source;
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    source = CollisionProxyImporter.FromModelFile(picker.FileName);
                }
                catch (Exception ex)
                {
                    Cursor.Current = Cursors.Default;
                    MessageBox.Show(this, "Could not read the mesh: " + ex.Message, "Import collision mesh", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                Cursor.Current = Cursors.Default;
                CommitImport(source);
            }
        }

        /* A new proxy from a model the level already holds: its first LOD, every submesh, in the model's own space. */
        private void fromModelButton_Click(object sender, EventArgs e)
        {
            if (!CanImport(out string why))
            {
                MessageBox.Show(this, why, "Import collision mesh", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _modelPicker?.Close();
            _modelPicker = new EditModel(null, true);
            _modelPicker.FormClosed += (s, args) => _modelPicker = null;
            _modelPicker.OnModelSelected += component =>
            {
                CollisionProxyImporter.MeshSource source;
                try
                {
                    source = CollisionProxyImporter.FromComponent(component, Content?.Level?.Models?.FindModel(component.LODs[0])?.Name);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Could not read the model: " + ex.Message, "Import collision mesh", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                BringToFront();
                CommitImport(source);
            };
            _modelPicker.Show();
        }

        private bool CanImport(out string why)
        {
            HavokPackfile hkx = Content?.Level?.Collision;
            why = null;
            if (hkx == null || !hkx.Loaded)
                why = "No COLLISION.HKX is loaded for this level.";
            else if (hkx.IsTagfile)
                why = "New collision meshes can only be written to the PC collision files, not a mobile or Switch level.";
            return why == null;
        }

        /* Show what is about to go in, ask, then write it into both collision packfiles and list it */
        private void CommitImport(CollisionProxyImporter.MeshSource source)
        {
            _modelViewer?.ShowPreviewMesh(source.ToPreviewMesh());
            previewStatus.Text = source.TriangleCount.ToString("N0") + " triangle" + (source.TriangleCount == 1 ? "" : "s") + " from " + source.Name + "  \u00B7  not yet a proxy";
            DialogResult answer = MessageBox.Show(this,
                "Create a new collision proxy from " + source.TriangleCount.ToString("N0") + " triangles (" + source.Name + ")?\n\nIt goes into COLLISION.HKX and COLLISION.HKX64 when the level is next saved.",
                "Import collision mesh", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                UpdatePreview(compoundList.SelectedItems.Count > 0 ? compoundList.SelectedItems[0].Tag as HavokPackfile.StaticCompoundShape : null);
                return;
            }

            HavokPackfile.StaticCompoundShape created;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                created = CollisionProxyImporter.Import(Content.Level, source, DefaultUserData, DefaultFilterInfo);
            }
            catch (Exception ex)
            {
                Cursor.Current = Cursors.Default;
                MessageBox.Show(this, "The proxy could not be created: " + ex.Message, "Import collision mesh", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdatePreview(compoundList.SelectedItems.Count > 0 ? compoundList.SelectedItems[0].Tag as HavokPackfile.StaticCompoundShape : null);
                return;
            }
            Cursor.Current = Cursors.Default;

            _current = created;
            searchBox.Text = "";
            PopulateList();
            UpdatePreview(created);
            statusLabel.Text = "Proxy #" + created.ProxyIndex + " created from " + source.Name + "  \u00B7  " + compoundList.Items.Count + " / " + _allCompounds.Count + " compounds";
            Singleton.OnResourceModified?.Invoke();
        }

        private void compoundList_DoubleClick(object sender, EventArgs e) => SelectCurrent();

        private void selectButton_Click(object sender, EventArgs e) => SelectCurrent();

        private void SelectCurrent()
        {
            //A double-click in the editor only selects the row; it must not pick it and close the window
            if (!_selectionMode || compoundList.SelectedItems.Count == 0)
                return;
            var selected = compoundList.SelectedItems[0].Tag as HavokPackfile.StaticCompoundShape;
            if (selected == null)
                return;
            OnCollisionProxySelected?.Invoke(selected);
            Close();
        }

        private void DetachModelViewer()
        {
            try
            {
                if (modelRendererHost != null && !modelRendererHost.IsDisposed)
                    modelRendererHost.Child = null;
            }
            catch
            {
                // ElementHost / Helix teardown can race while the viewport is mid-render.
            }
            _modelViewer = null;
        }
    }
}
