using CATHODE;
using CATHODE.Scripting;
using OpenCAGE.Undo;
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

        /// <summary>The entity whose resources these are, and its composite: a ModelReference's "WORLD + BALLISTIC" row is made as retail's pair (<see cref="CollisionPairs"/>).</summary>
        public Composite HostComposite;
        public FunctionEntity HostEntity;

        public GUI_Resource_CollisionMapping() : base()
        {
            InitializeComponent();
            Disposed += GUI_Resource_CollisionMapping_Disposed;
        }

        private void GUI_Resource_CollisionMapping_Disposed(object sender, EventArgs e)
        {
            _matEditor?.Close();
            _mappingEditor?.Close();
            _proxyEditor?.Close();
        }

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
            btnClearHavok.Enabled = _currentCollisionMapping?.CollisionProxy != null;
            btnClearMaterialMapping.Enabled = _currentCollisionMapping?.MaterialMapping != null;
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
            //A proxy imported from the picker carries what retail gives one for this row: the physics material's
            //write index as its userData, and the collision type its storage class implies
            LevelContent content = Singleton.Editor?.CompositeBrowser?.Content;
            int material = _currentCollisionMapping.Material != null && content?.Level?.Materials != null ? content.Level.Materials.GetWriteIndexOrPending(_currentCollisionMapping.Material) : -1;
            _proxyEditor.DefaultUserData = material >= 0 ? (uint)material : 0u;
            _proxyEditor.DefaultFilterInfo = (_currentCollisionMapping.Flags & CollisionFlags.WORLD) != 0 ? 3u : 9u;
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
            MakePair("Set collision proxy of ");
            RefreshDisplay();
            NotifyModified();
            BringToFront();
            Focus();
        }

        /* The row a new Collision Mapping gets asks for WORLD and BALLISTIC storage, which the game cannot use on one row:
           once it has a proxy it is made as retail makes it - this entity's WORLD row, and a '<name>_Ballistic' sibling with
           the BALLISTIC row on its own copy of the proxy - and every later change here keeps that sibling in step. */
        private void MakePair(string label, bool materialChanged = false)
        {
            LevelContent content = Singleton.Editor?.CompositeBrowser?.Content;
            if (content?.Level == null || HostComposite == null || HostEntity == null || !CollisionPairs.IsModelReference(HostEntity))
                return;
            if (HostComposite.GetEntityByID(HostEntity.shortGUID) != HostEntity)
                return;
            try
            {
                //Every entity sharing this row gets its own half; a physics material just set is carried by the proxies of both halves
                string name = UndoLabels.Entity(HostComposite, HostEntity);
                using (UndoStack.Current.BeginGroup(label + name))
                    CollisionPairs.Normalise(content.Level, HostComposite, HostEntity, _currentCollisionMapping, CollisionPairs.Undoable(content.Level, label + name), materialChanged);
            }
            catch (Exception e)
            {
                MessageBox.Show("The collision was set, but its ballistic half (what bullets, footstep sounds and zones use) could not be made: " + e.Message, "Collision pair", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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
            MakePair("Set physics material of ", materialChanged: true);
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
            MakePair("Set collision material mapping of ");
            RefreshDisplay();
            NotifyModified();
            BringToFront();
            Focus();
        }
    }
}
