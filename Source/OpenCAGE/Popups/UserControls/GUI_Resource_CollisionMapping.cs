using CATHODE;
using CATHODE.Scripting;
using System;
using System.Windows.Forms;
using static CATHODE.CollisionMaps;

namespace OpenCAGE.Popups.UserControls
{
    public partial class GUI_Resource_CollisionMapping : ResourceUserControl
    {
        private ResourceReference _resourceRef;
        private COLLISION_MAPPING _currentCollisionMapping;
        private EditMaterial _matEditor;
        private EditMaterialMapping _mappingEditor;
        private EditCollisionProxy _proxyEditor;

        public GUI_Resource_CollisionMapping() : base()
        {
            InitializeComponent();
            Disposed += GUI_Resource_CollisionMapping_Disposed;
#if !DEBUG
            SetEditingEnabled(false);
#endif
        }

        private void GUI_Resource_CollisionMapping_Disposed(object sender, EventArgs e)
        {
            _matEditor?.Close();
            _mappingEditor?.Close();
            _proxyEditor?.Close();
        }

#if !DEBUG
        private void SetEditingEnabled(bool enabled)
        {
            btnSetHavok.Enabled = enabled;
            btnClearHavok.Enabled = enabled;
            btnSetMaterial.Enabled = enabled;
            btnSetMaterialMapping.Enabled = enabled;
            btnClearMaterialMapping.Enabled = enabled;
        }
#endif

        public override void PopulateUI(ResourceReference resource)
        {
            _resourceRef = resource;
            _currentCollisionMapping = resource.CollisionMapping;
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            havokName.Text = FormatProxy(_currentCollisionMapping?.CollisionProxy);
            materialName.Text = _currentCollisionMapping?.Material?.Name ?? "";
            materialMappingName.Text = _currentCollisionMapping?.MaterialMapping?.Name ?? "";
#if DEBUG
            btnClearHavok.Enabled = _currentCollisionMapping?.CollisionProxy != null;
            btnClearMaterialMapping.Enabled = _currentCollisionMapping?.MaterialMapping != null;
#else
            SetEditingEnabled(false);
#endif
        }

        private static string FormatProxy(HavokPackfile.StaticCompoundShape proxy)
        {
            if (proxy == null)
                return "(none)";
            int instances = proxy.Instances?.Count ?? 0;
            return "Proxy #" + proxy.ProxyIndex + " · " + instances + " instance" + (instances == 1 ? "" : "s");
        }

        private void NotifyModified()
        {
            Singleton.OnResourceModified?.Invoke();
        }

        private void btnSetHavok_Click(object sender, EventArgs e)
        {
            if (_currentCollisionMapping == null)
                return;

            _proxyEditor?.Close();
            _proxyEditor = new EditCollisionProxy(_currentCollisionMapping.CollisionProxy);
            _proxyEditor.FormClosed += (s, args) => _proxyEditor = null;
            _proxyEditor.OnCollisionProxySelected += SetCollisionProxy;
            _proxyEditor.Show();
        }

        private void btnClearHavok_Click(object sender, EventArgs e)
        {
            SetCollisionProxy(null);
        }

        private void SetCollisionProxy(HavokPackfile.StaticCompoundShape proxy)
        {
            if (_currentCollisionMapping == null)
                return;
            _currentCollisionMapping.CollisionProxy = proxy;
            // Template rows don't own a world-host instance slot — clear any stale binding.
            _currentCollisionMapping.CollisionInstance = null;
            RefreshDisplay();
            NotifyModified();
            BringToFront();
            Focus();
        }

        private void btnSetMaterial_Click(object sender, EventArgs e)
        {
            if (_currentCollisionMapping == null)
                return;

            _matEditor?.Close();
            _matEditor = new EditMaterial(_currentCollisionMapping.Material);
            _matEditor.FormClosed += (s, args) => _matEditor = null;
            _matEditor.OnMaterialSelected += SetMaterial;
            _matEditor.Show();
        }

        private void SetMaterial(Materials.Material material)
        {
            if (_currentCollisionMapping == null)
                return;
            _currentCollisionMapping.Material = material;
            RefreshDisplay();
            NotifyModified();
            BringToFront();
            Focus();
        }

        private void btnSetMaterialMapping_Click(object sender, EventArgs e)
        {
            if (_currentCollisionMapping == null)
                return;

            _mappingEditor?.Close();
            _mappingEditor = new EditMaterialMapping(_currentCollisionMapping.MaterialMapping);
            _mappingEditor.FormClosed += (s, args) => _mappingEditor = null;
            _mappingEditor.OnMaterialMappingSelected += SetMaterialMapping;
            _mappingEditor.Show();
        }

        private void btnClearMaterialMapping_Click(object sender, EventArgs e)
        {
            SetMaterialMapping(null);
        }

        private void SetMaterialMapping(MaterialMappings.MaterialMapping mapping)
        {
            if (_currentCollisionMapping == null)
                return;
            _currentCollisionMapping.MaterialMapping = mapping;
            RefreshDisplay();
            NotifyModified();
            BringToFront();
            Focus();
        }
    }
}
