using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;
using OpenCAGE.Popups.UserControls;
using OpenCAGE;

namespace OpenCAGE
{
    public partial class SelectHierarchy : BaseWindow
    {
        public Action<Entity> OnFinalEntitySelected;
        public Action<List<Entity>> OnFinalEntitiesSelected;
        public Action<ShortGuid[]> OnHierarchyGenerated;
        public Action<List<ShortGuid[]>> OnHierarchiesGenerated;

        private Entity selectedEntity = null;
        private Composite selectedComposite = null;

        private bool _multiselect = false;
        private bool _allowFollowThrough = true;
        private CompositePath _path = new CompositePath();

        public ShortGuid[] CurrentPathEntities => _path.GetPath().ToArray();
        public string CurrentEntitySearch => compositeEntityList1.SearchText;

        //PROXIES can only point to FunctionEntities - ALIASES can point to FunctionEntities, ProxyEntities, VariableEntities
        // ShowCheckboxes enables multi-select. With follow-through, checked entities at the current path depth
        // each get a hierarchy of (path + entity + Invalid).
        public SelectHierarchy(Composite startingComposite, CompositeEntityList.DisplayOptions displayOptions, bool allowFollowThrough = true) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();
            StayAboveEditor = true; //picker dialog

            _multiselect = displayOptions.ShowCheckboxes;
            if (_multiselect)
            {
                this.Text = "Select Entities";
                SelectEntity.Text = "Select Checked Entities";
            }

            compositeEntityList1.Setup(startingComposite, displayOptions);
            compositeEntityList1.SelectedEntityChanged += OnSelectedEntityChanged;
            compositeEntityList1.StepIntoCompositeInstance = StepIntoCompositeInstance;

            LoadComposite(startingComposite);
            _allowFollowThrough = allowFollowThrough;
            FollowEntityThrough.Visible = allowFollowThrough;
        }

        /* Drill into saved instance path (e.g. last proxy create location). Stops early if a hop is invalid. */
        public void TryRestoreNavigation(ShortGuid[] pathEntities, string entitySearch = null)
        {
            if (pathEntities != null)
            {
                for (int i = 0; i < pathEntities.Length; i++)
                {
                    if (selectedComposite == null)
                        break;

                    Entity entity = selectedComposite.GetEntityByID(pathEntities[i]);
                    if (entity == null || entity.variant != EntityVariant.FUNCTION)
                        break;

                    Composite child = Content?.Level?.Commands?.GetComposite(((FunctionEntity)entity).function);
                    if (child == null)
                        break;

                    _path.StepForwards(selectedComposite, entity);
                    LoadComposite(child);
                }
            }

            if (!string.IsNullOrEmpty(entitySearch))
                compositeEntityList1.ApplySearch(entitySearch);
        }

        /* Select a new entity from the composite, show fall through option if available */
        private void OnSelectedEntityChanged(Entity entity)
        {
            if (entity == null) return;

            selectedEntity = entity;
            SelectEntity.Enabled = true;
            FollowEntityThrough.Enabled = false;

            if (selectedEntity.variant != EntityVariant.FUNCTION) return;
            FollowEntityThrough.Enabled = Content.Level.Commands.GetComposite(((FunctionEntity)selectedEntity).function) != null;
        }

        /* Load a composite into the UI */
        private void LoadComposite(Composite composite)
        {
            selectedEntity = null;
            if (!_multiselect)
                SelectEntity.Enabled = false;
            FollowEntityThrough.Enabled = false;

            selectedComposite = composite;
            pathDisplay.Text = _path.GetPath(composite);

            compositeEntityList1.LoadComposite(selectedComposite);
        }

        /* If selected entity is a composite instance, allow jump to it */
        private void FollowEntityThrough_Click(object sender, EventArgs e)
        {
            StepIntoCompositeInstance(selectedEntity);
        }

        /// <summary>
        /// Ctrl + middle click on a composite instance, which here means what the Follow Entity Through
        /// button means: walk this window's own path into it.
        /// </summary>
        /// <remarks>
        /// It used to send the editor behind the picker into the composite instead, which closed the
        /// picker out from under the click and left whatever opened it pointing at a composite the user
        /// was no longer in.
        /// </remarks>
        private void StepIntoCompositeInstance(Entity entity)
        {
            if (!_allowFollowThrough || entity == null || entity.variant != EntityVariant.FUNCTION) return;

            Composite composite = Content?.Level?.Commands?.GetComposite(((FunctionEntity)entity).function);
            if (composite == null) return;

            _path.StepForwards(selectedComposite, entity);
            LoadComposite(composite);
        }

        /* Generate the hierarchy */
        private void SelectEntity_Click(object sender, EventArgs e)
        {
            if (_multiselect)
            {
                List<Entity> entities = compositeEntityList1.CheckedEntities;
                if (entities.Count == 0 && compositeEntityList1.SelectedEntity != null)
                    entities.Add(compositeEntityList1.SelectedEntity);
                if (entities.Count == 0)
                    return;

                List<ShortGuid[]> hierarchies = new List<ShortGuid[]>(entities.Count);
                for (int i = 0; i < entities.Count; i++)
                    hierarchies.Add(BuildHierarchy(entities[i].shortGUID));

                OnHierarchiesGenerated?.Invoke(hierarchies);
                OnFinalEntitiesSelected?.Invoke(entities);
            }
            else
            {
                OnHierarchyGenerated?.Invoke(BuildHierarchy(selectedEntity.shortGUID));
                OnFinalEntitySelected?.Invoke(selectedEntity);
            }
            this.Close();
        }

        private ShortGuid[] BuildHierarchy(ShortGuid entityId)
        {
            //TODO: should use the proper hierarchy class here
            List<ShortGuid> hierarchy = new List<ShortGuid>();
            hierarchy.AddRange(_path.GetPath());
            hierarchy.Add(entityId);
            hierarchy.Add(ShortGuid.Invalid);
            return hierarchy.ToArray();
        }

        private void goBackOnPath_Click(object sender, EventArgs e)
        {
            if (_path.StepBackwards(out Composite composite, out Entity entity))
            {
                LoadComposite(composite);
            }
        }

        /// <summary>
        /// Offer "browse from level root", which lets the user build a path to an entity in another
        /// branch of the tree rather than only below the composite they started in.
        /// </summary>
        /// <remarks>
        /// Only valid where the stored path may be root-relative. A CAGEAnimation binding or a
        /// TriggerSequence entry may be - that is how Tech_Hub's blackbox scene reaches a prop three
        /// composites away - but an alias must stay local and a proxy path leads with a composite id,
        /// so those pickers must not offer it.
        /// </remarks>
        public bool AllowRootBrowsing
        {
            get { return browseFromRoot.Visible; }
            set
            {
                if (browseFromRoot.Visible == value) return;
                browseFromRoot.Visible = value;

                //The button and the path share a row, so hand it the space rather than letting the
                //path draw over the top of it - pathDisplay is added first and so wins the z-order.
                int left = value ? browseFromRoot.Right + 4 : goBackOnPath.Right + 4;
                pathDisplay.SetBounds(left, pathDisplay.Top, pathDisplay.Right - left, pathDisplay.Height);
            }
        }

        private void browseFromRoot_Click(object sender, EventArgs e)
        {
            Composite root = Content.Level?.Commands?.EntryPoints?[0];
            if (root == null) return;

            while (_path.StepBackwards()) { }
            LoadComposite(root);
        }
    }
}
