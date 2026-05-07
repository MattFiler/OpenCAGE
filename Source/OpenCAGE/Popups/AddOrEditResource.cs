using CATHODE;
using CATHODE.Scripting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCAGE.Popups.UserControls;
using System.Windows.Interop;
using OpenCAGE.Popups.Base;
using CATHODE.EXPERIMENTAL;
using OpenCAGE.DockPanels;
using CATHODE.Scripting.Internal;

namespace OpenCAGE
{
    //TODO: this whole resource editor needs a bit of a rework & improvement as it's not good enough.

    public partial class AddOrEditResource : BaseWindow
    {
        private ShortGuid guid_parent;
        private int current_ui_offset = 7;

        private EntityInspector _entDisplay = null;

        private FunctionEntity _entity = null;
        private cResource _parameter = null;

        public AddOrEditResource(EntityInspector entDisplay) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            _entDisplay = entDisplay;
            _entity = (FunctionEntity)entDisplay.Entity;
            guid_parent = entDisplay.Entity.shortGUID;

            InitializeComponent();

            this.Text += " - " + Content.EditorUtils.GenerateEntityName(entDisplay.Entity, entDisplay.Composite);
            resourceType.SelectedIndex = 0;

            RefreshUI();
        }

        public AddOrEditResource(EntityInspector entDisplay, cResource parameter, string windowTitle) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_ENTITY_SELECTION | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            _entDisplay = entDisplay;
            _parameter = parameter;
            guid_parent = parameter.shortGUID;

            InitializeComponent();

            this.Text += " - " + windowTitle;
            resourceType.SelectedIndex = 0;

            RefreshUI();
        }

        private void RefreshUI()
        {
            current_ui_offset = 7;
            resource_panel.Controls.Clear();

            List<ResourceReference> resources = (_parameter != null) ? _parameter.value : _entity.resources;
            for (int i = 0; i < resources.Count; i++)
            {
                ResourceUserControl resourceGroup = null;
                switch (resources[i].resource_type)
                {
                    case ResourceType.ANIMATED_MODEL:
                        if (resources[i].AnimatedModel == null)
                            continue;
                        resourceGroup = new GUI_Resource_AnimatedModel();
                        break;
                    case ResourceType.COLLISION_MAPPING:
                        if (resources[i].CollisionMapping == null)
                            continue;
                        resourceGroup = new GUI_Resource_CollisionMapping();
                        break;
                    case ResourceType.RENDERABLE_INSTANCE:
                        if (resources[i].RenderableInstance == null)
                            continue;
                        resourceGroup = new GUI_Resource_RenderableInstance();
                        break;
                    default:
                        resourceGroup = new GUI_Resource_Default();
                        break;
                }
                resourceGroup.PopulateUI(resources[i]);
                resourceGroup.Location = new Point(15, current_ui_offset);
                current_ui_offset += resourceGroup.Height + 6;
                resource_panel.Controls.Add(resourceGroup);
            }
        }

        /* Add a new resource reference to the list */
        private void addResource_Click(object sender, EventArgs e)
        {
            //todo - this should be handled better - in particular, if you make a ModelReference entity for example, the appropriate resources should be added by default

            ResourceType type = (ResourceType)Enum.Parse(typeof(ResourceType), resourceType.Items[resourceType.SelectedIndex].ToString());

            //If we don't have EntityDisplay, we can't make DynamicPhysicsSystem: this is the result of this editor being made in a crap way. really we should probs implicitly handle the resource parameter
            if (type == ResourceType.DYNAMIC_PHYSICS_SYSTEM && _entDisplay == null)
            {
                MessageBox.Show("Dynamic Physics Systems cannot currently be added as Resource parameters.", "Currently unsupported.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            //A resource reference list can only ever point to one of a type
            List<ResourceReference> resources = (_parameter != null) ? _parameter.value : _entity.resources;
            for (int i = 0; i < resources.Count; i++)
            {
                if (resources[i].resource_type == type)
                {
                    MessageBox.Show("This resource type is already referenced!\nOnly one reference to " + type + " can be added.", "Can't add duplicate type.", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            ResourceReference newReference = new ResourceReference();
            newReference.resource_id = guid_parent;
            newReference.resource_type = type;

            //We now auto create ANIMATED_MODEL entries. We should probs do the same for others too.
            if (newReference.resource_type == ResourceType.ANIMATED_MODEL)
            {
                if (Content.Level.EnvironmentAnimations.Entries.Count == 0)
                {
                    MessageBox.Show("Cannot add ANIMATED_MODEL for a level with no Environment Animations!");
                    return;
                }

                newReference.AnimatedModel = new EnvironmentAnimations.EnvironmentAnimation();
                newReference.AnimatedModel.ResourceIndex = Content.Level.EnvironmentAnimations.Entries[Content.Level.EnvironmentAnimations.Entries.Count - 1].ResourceIndex + 1;
                Content.Level.EnvironmentAnimations.Entries.Add(newReference.AnimatedModel);
            }
            
            if (newReference.resource_type == ResourceType.COLLISION_MAPPING)
            {
                newReference.CollisionMapping = new CollisionMaps.COLLISION_MAPPING();
                //TODO
                Content.Level.CollisionMaps.Entries.Add(newReference.CollisionMapping);
            }

            ((_parameter != null) ? _parameter.value : _entity.resources).Add(newReference);
            RefreshUI();
        }

        /* Delete an existing resource reference from the list */
        private void deleteResource_Click(object sender, EventArgs e)
        {
            ResourceType type = (ResourceType)Enum.Parse(typeof(ResourceType), resourceType.Items[resourceType.SelectedIndex].ToString());
            ResourceReference reference = ((_parameter != null) ? _parameter.value : _entity.resources).FirstOrDefault(o => o.resource_type == type);
            if (reference == null)
            {
                MessageBox.Show("Resource type " + type + " is not referenced!\nThere is nothing to delete.", "Type not referenced!", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                if (MessageBox.Show("You are about to remove resource reference for type " + type + ".\nThis is a destructive action - are you sure?", "Are you sure?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    ((_parameter != null) ? _parameter.value : _entity.resources).Remove(reference);
                    RefreshUI();
                }
            }
        }
    }
}
