using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;
using System;
using System.Windows.Forms;
using static OpenCAGE.CompositeSceneBuilder;

namespace OpenCAGE
{
    public partial class Composite3D : BaseWindow
    {
        private CompositeSceneViewer _viewer;
        private CompositeDisplay _compositeDisplay;
        private bool _subscribed;

        public Composite3D(CompositeDisplay compositeDisplay) : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            _compositeDisplay = compositeDisplay;

            InitializeComponent();
            this.Text = "3D Preview: " + _compositeDisplay.Composite.name;

            _viewer = new CompositeSceneViewer();
            modelRendererHost.Child = _viewer;

            Subscribe();
            RebuildScene();
        }

        public void BindComposite(CompositeDisplay compositeDisplay)
        {
            _compositeDisplay = compositeDisplay;
            this.Text = "3D Preview: " + _compositeDisplay.Composite.name;
            RebuildScene();
        }

        private void Subscribe()
        {
            if (_subscribed)
                return;

            _subscribed = true;
            Singleton.OnEntityReloaded += OnEntityChanged;
            Singleton.OnEntityMoved += OnEntityMoved;
            Singleton.OnEntityAdded += OnEntityStructureChanged;
            Singleton.OnEntityDeleted += OnEntityStructureChanged;
            Singleton.OnCompositeReloaded += OnCompositeReloaded;
            Singleton.OnResourceModified += OnResourceModified;
            Singleton.OnParameterModified += OnParameterModified;

            if (_compositeDisplay?.EntityListPanel != null)
                _compositeDisplay.EntityListPanel.List.SelectedEntityChanged += OnListSelectionChanged;

            this.FormClosed += Composite3D_FormClosed;
        }

        private void Composite3D_FormClosed(object sender, FormClosedEventArgs e)
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
                return;

            _subscribed = false;
            Singleton.OnEntityReloaded -= OnEntityChanged;
            Singleton.OnEntityMoved -= OnEntityMoved;
            Singleton.OnEntityAdded -= OnEntityStructureChanged;
            Singleton.OnEntityDeleted -= OnEntityStructureChanged;
            Singleton.OnCompositeReloaded -= OnCompositeReloaded;
            Singleton.OnResourceModified -= OnResourceModified;
            Singleton.OnParameterModified -= OnParameterModified;

            if (_compositeDisplay?.EntityListPanel != null)
                _compositeDisplay.EntityListPanel.List.SelectedEntityChanged -= OnListSelectionChanged;
        }

        private void OnListSelectionChanged(Entity entity)
        {
            if (entity == null)
                return;
            _viewer?.SelectEntity(entity.shortGUID, focusCamera: false);
        }

        private void OnEntityChanged(Entity entity)
        {
            if (!IsEntityInView(entity))
                return;

            if (_compositeDisplay?.EntityDisplay?.Entity == entity)
                _viewer?.SyncSelectedFromEntity();
            else
                _viewer?.SelectEntity(entity.shortGUID, focusCamera: false);
        }

        private void OnEntityMoved(cTransform transform, Entity entity)
        {
            if (!IsEntityInView(entity))
                return;

            _viewer?.SyncSelectedFromEntity();
        }

        private void OnEntityStructureChanged(Entity entity)
        {
            if (entity == null || IsEntityInView(entity))
                RebuildScene();
        }

        private void OnCompositeReloaded(Composite composite)
        {
            if (composite == _compositeDisplay?.Composite)
                RebuildScene();
        }

        private void OnResourceModified()
        {
            RebuildScene();
        }

        private void OnParameterModified()
        {
            Entity entity = _compositeDisplay?.EntityDisplay?.Entity;
            if (entity != null && entity.GetParameter("position") != null)
                _viewer?.SyncSelectedFromEntity();
        }

        private bool IsEntityInView(Entity entity)
        {
            if (entity == null || _compositeDisplay?.Composite == null)
                return false;
            return _compositeDisplay.Composite.GetEntityByID(entity.shortGUID) != null;
        }

        private void RebuildScene()
        {
            if (_compositeDisplay?.Composite == null || _compositeDisplay.Content == null)
                return;

            SceneGraph graph = CompositeSceneBuilder.Build(_compositeDisplay.Content, _compositeDisplay.Composite);
            _viewer.ShowScene(graph, _compositeDisplay);

            Entity selected = _compositeDisplay.EntityDisplay?.Entity;
            if (selected != null)
                _viewer.SelectEntity(selected.shortGUID, focusCamera: false);
        }
    }
}
