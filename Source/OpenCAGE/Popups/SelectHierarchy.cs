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
        private CompositePath _path = new CompositePath();

        public bool ApplyDefaultParams => applyDefaultParams.Visible && applyDefaultParams.Checked;

        //PROXIES can only point to FunctionEntities - ALIASES can point to FunctionEntities, ProxyEntities, VariableEntities
        // ShowCheckboxes enables multi-select. With follow-through, checked entities at the current path depth
        // each get a hierarchy of (path + entity + Invalid).
        public SelectHierarchy(Composite startingComposite, CompositeEntityList.DisplayOptions displayOptions, bool allowFollowThrough = true) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            InitializeComponent();

            _multiselect = displayOptions.ShowCheckboxes;
            if (_multiselect)
            {
                this.Text = "Select Entities";
                SelectEntity.Text = "Select Checked Entities";
            }

            compositeEntityList1.Setup(startingComposite, displayOptions);
            compositeEntityList1.SelectedEntityChanged += OnSelectedEntityChanged;

            LoadComposite(startingComposite);
            FollowEntityThrough.Visible = allowFollowThrough;

            if (displayOptions.ShowApplyDefaults)
            {
                applyDefaultParams.Checked = SettingsManager.GetBool(Settings.PreviouslySearchedParamPopulationProxyOrAlias);
            }
            else
            {
                applyDefaultParams.Visible = false;
            }

            SettingsManager.SettingsChanged += OnSettingsChanged;
            FormClosed += (s, e) => SettingsManager.SettingsChanged -= OnSettingsChanged;
        }

        private void OnSettingsChanged(object sender, SettingsChangedEventArgs e)
        {
            if (!e.ExternalChange || IsDisposed || !applyDefaultParams.Visible)
                return;

            if (!SettingsChangedEventArgs.ContainsKey(e.ChangedKeys, Settings.PreviouslySearchedParamPopulationProxyOrAlias))
                return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnSettingsChanged(sender, e)));
                return;
            }

            applyDefaultParams.Checked = SettingsManager.GetBool(Settings.PreviouslySearchedParamPopulationProxyOrAlias);
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
            if (selectedEntity == null) return;
            if (selectedEntity.variant != EntityVariant.FUNCTION) return;

            Composite composite = Content.Level.Commands.GetComposite(((FunctionEntity)selectedEntity).function);
            if (composite == null) return;

            _path.StepForwards(selectedComposite, selectedEntity);
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
                if (applyDefaultParams.Visible)
                    SettingsManager.SetBool(Settings.PreviouslySearchedParamPopulationProxyOrAlias, applyDefaultParams.Checked);

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
    }
}
