using CATHODE;
using CATHODE.Scripting;
using System;
using System.Windows.Forms;

namespace OpenCAGE.Popups.UserControls
{
    public partial class GUI_Resource_DynamicPhysicsSystem : ResourceUserControl
    {
        private ResourceReference _resourceRef;
        private EditPhysicsSystem _picker;

        public GUI_Resource_DynamicPhysicsSystem() : base()
        {
            InitializeComponent();
            Disposed += (s, e) => _picker?.Close();
        }

        public override void PopulateUI(ResourceReference resource)
        {
            _resourceRef = resource;
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            HavokPackfile.PhysicsSystem system = _resourceRef?.PhysicsSystem;
            if (system == null)
            {
                physicsName.Text = "(none)";
                btnClear.Enabled = false;
                return;
            }

            string name = system.Name ?? "";
            int slash = Math.Max(name.LastIndexOf('\\'), name.LastIndexOf('/'));
            string leaf = slash >= 0 && slash < name.Length - 1 ? name.Substring(slash + 1) : name;
            physicsName.Text = "System #" + system.SystemIndex
                + (string.IsNullOrEmpty(leaf) ? "" : " · " + leaf);
            btnClear.Enabled = true;
        }

        private void btnSet_Click(object sender, EventArgs e)
        {
            if (_resourceRef == null)
                return;
            _picker?.Close();
            _picker = new EditPhysicsSystem(_resourceRef.PhysicsSystem);
            _picker.FormClosed += (s, args) => _picker = null;
            _picker.OnPhysicsSystemSelected += SetPhysicsSystem;
            _picker.Show();
        }

        private void btnClear_Click(object sender, EventArgs e) => SetPhysicsSystem(null);

        private void SetPhysicsSystem(HavokPackfile.PhysicsSystem system)
        {
            if (_resourceRef == null)
                return;

            _resourceRef.PhysicsSystem = system;
            _resourceRef.PhysicsSystemIndex = system?.SystemIndex ?? -1;

            RefreshDisplay();
            Singleton.OnResourceModified?.Invoke();
            BringToFront();
            Focus();
        }
    }
}
