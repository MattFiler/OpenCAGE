using CATHODE;
using CATHODE.Scripting;
using OpenCAGE.Popups.Base;
using OpenCAGE.Popups.UserControls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;

namespace OpenCAGE
{
    public partial class EditPhysicsSystem : BaseWindow
    {
        public Action<HavokPackfile.PhysicsSystem> OnPhysicsSystemSelected;

        private readonly List<HavokPackfile.PhysicsSystem> _allSystems = new List<HavokPackfile.PhysicsSystem>();
        private HavokPackfile.PhysicsSystem _current;
        private GUI_ModelViewer _modelViewer;

        public EditPhysicsSystem(HavokPackfile.PhysicsSystem current = null, bool showSelectBtn = true)
            : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();
            _current = current;
            selectButton.Visible = showSelectBtn;

            _modelViewer = new GUI_ModelViewer();
            modelRendererHost.Child = _modelViewer;
            // Detach before components.Dispose(); Disposed runs too late and ElementHost.Child can NRE.
            FormClosing += (s, e) => DetachModelViewer();
            Disposed += (s, e) => DetachModelViewer();

            PopulateList();
            UpdatePreview(_current);
        }

        private void PopulateList()
        {
            _allSystems.Clear();
            systemList.Items.Clear();

            HavokPackfile hkx = Content?.Level?.Physics;
            if (hkx == null || !hkx.Loaded)
            {
                statusLabel.Text = "No PHYSICS.HKX loaded for this level.";
                selectButton.Enabled = false;
                return;
            }

            _allSystems.AddRange(hkx.PhysicsSystems);
            ApplyFilter();

            if (_current != null)
            {
                foreach (ListViewItem item in systemList.Items)
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
            systemList.BeginUpdate();
            systemList.Items.Clear();

            foreach (HavokPackfile.PhysicsSystem system in _allSystems)
            {
                string name = system.Name ?? "";
                string idx = system.SystemIndex.ToString();
                if (filter.Length > 0)
                {
                    string haystack = idx + " " + name;
                    if (haystack.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }

                string leaf = name;
                int slash = Math.Max(name.LastIndexOf('\\'), name.LastIndexOf('/'));
                if (slash >= 0 && slash < name.Length - 1)
                    leaf = name.Substring(slash + 1);

                systemList.Items.Add(new ListViewItem(new[] { idx, leaf, name }) { Tag = system });
            }

            systemList.EndUpdate();
            statusLabel.Text = systemList.Items.Count + " / " + _allSystems.Count + " systems";
            selectButton.Enabled = systemList.SelectedItems.Count > 0;
        }

        private void searchBox_TextChanged(object sender, EventArgs e) => ApplyFilter();

        private void systemList_SelectedIndexChanged(object sender, EventArgs e)
        {
            selectButton.Enabled = systemList.SelectedItems.Count > 0;
            if (systemList.SelectedItems.Count == 0)
            {
                UpdatePreview(null);
                return;
            }
            UpdatePreview(systemList.SelectedItems[0].Tag as HavokPackfile.PhysicsSystem);
        }

        private void UpdatePreview(HavokPackfile.PhysicsSystem system)
        {
            if (_modelViewer == null)
                return;

            bodyList.Items.Clear();
            bodyDetailLabel.Text = "Select a rigid body for details.";

            HavokPackfile hkx = Content?.Level?.Physics;
            if (system == null || hkx == null)
            {
                _modelViewer.ShowPreviewMesh(null);
                previewStatus.Text = "No selection";
                return;
            }

            HavokPackfile.PreviewMesh mesh = hkx.BuildPreviewMesh(system);
            _modelViewer.ShowPreviewMesh(mesh);

            List<HavokPackfile.RigidBodyInfo> bodies = hkx.GetRigidBodies(system);
            bodyList.BeginUpdate();
            foreach (HavokPackfile.RigidBodyInfo body in bodies)
            {
                string shape = ShortClassName(body.ShapeClassName);
                bodyList.Items.Add(new ListViewItem(new[]
                {
                    string.IsNullOrEmpty(body.Name) ? "(unnamed)" : body.Name,
                    shape,
                    body.MotionTypeName ?? "",
                    FormatMass(body.Mass),
                    "0x" + body.CollisionFilterInfo.ToString("X"),
                    body.ObjectRadius.ToString("0.###", CultureInfo.InvariantCulture),
                    body.LinearDamping.ToString("0.###", CultureInfo.InvariantCulture),
                })
                { Tag = body });
            }
            bodyList.EndUpdate();
            if (bodyList.Items.Count > 0)
                bodyList.Items[0].Selected = true;

            string geom = mesh.TriangleCount == 0
                ? "No preview geometry"
                : mesh.TriangleCount.ToString("N0") + " triangle" + (mesh.TriangleCount == 1 ? "" : "s")
                    + " / " + mesh.ShapeCount + " shape" + (mesh.ShapeCount == 1 ? "" : "s");
            previewStatus.Text = bodies.Count + " rigid body" + (bodies.Count == 1 ? "" : "ies")
                + "  ·  " + geom;
        }

        private void bodyList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (bodyList.SelectedItems.Count == 0)
            {
                bodyDetailLabel.Text = "Select a rigid body for details.";
                return;
            }

            var body = bodyList.SelectedItems[0].Tag as HavokPackfile.RigidBodyInfo;
            if (body == null)
                return;

            bodyDetailLabel.Text = string.Format(
                CultureInfo.InvariantCulture,
                "MassInv={0:0.####}  InertiaInv=({1:0.##}, {2:0.##}, {3:0.##})  MaxLinVel={4:0.##}  Gravity={5:0.###}  @0x{6:X}",
                body.MassInv,
                body.InertiaInvLocal.X,
                body.InertiaInvLocal.Y,
                body.InertiaInvLocal.Z,
                body.MaxLinearVelocity,
                body.GravityFactor,
                body.DataOffset);
        }

        private static string FormatMass(float mass)
        {
            if (float.IsPositiveInfinity(mass) || float.IsInfinity(mass))
                return "∞";
            if (mass >= 10f)
                return mass.ToString("0.#", CultureInfo.InvariantCulture);
            return mass.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string ShortClassName(string className)
        {
            if (string.IsNullOrEmpty(className))
                return "?";
            if (className.StartsWith("hkp", StringComparison.Ordinal))
                return className.Substring(3);
            return className;
        }

        private void systemList_DoubleClick(object sender, EventArgs e) => SelectCurrent();
        private void selectButton_Click(object sender, EventArgs e) => SelectCurrent();

        private void SelectCurrent()
        {
            if (systemList.SelectedItems.Count == 0)
                return;
            var selected = systemList.SelectedItems[0].Tag as HavokPackfile.PhysicsSystem;
            if (selected == null)
                return;
            OnPhysicsSystemSelected?.Invoke(selected);
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
