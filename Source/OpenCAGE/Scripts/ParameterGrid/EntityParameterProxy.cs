using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.DockPanels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using static CathodeLib.CathodeEnumTable;

namespace OpenCAGE
{
    /// <summary>
    /// Wraps an Entity for display in the inspector's parameter grid, exposing its visible parameters
    /// as PropertyDescriptors. One proxy per entity - the PropertyGrid merges same-named rows when
    /// multiple proxies are selected, which is what powers multi-entity editing.
    /// </summary>
    public class EntityParameterProxy : ICustomTypeDescriptor
    {
        //VECTOR parameters that represent colours (shown with a colour picker) - keep in sync with the legacy inspector list
        private static readonly HashSet<string> _colourParams = new HashSet<string>()
        {
            "AMBIENT_LIGHTING_COLOUR", "COLOUR_TINT_START", "COLOUR_TINT_MID", "COLOUR_TINT_END",
            "COLOUR_TINT", "COLOUR_TINT_OUTER", "DEPTH_INTERSECT_COLOUR_VALUE", "DEPTH_INTERSECT_INITIAL_COLOUR",
            "DEPTH_INTERSECT_MIDPOINT_COLOUR", "DEPTH_INTERSECT_END_COLOUR", "DEPTH_FOG_INITIAL_COLOUR",
            "DEPTH_FOG_MIDPOINT_COLOUR", "DEPTH_FOG_END_COLOUR", "ColourFactor", "lens_flare_colour",
            "light_shaft_colour", "initial_colour", "near_colour", "far_colour", "colour", "Colour"
        };

        public Entity Entity { get; }
        public Composite Composite { get; }
        public LevelContent Content { get; }
        public ParameterGridPanel Host { get; }

        private PropertyDescriptorCollection _properties;

        //Parameter context highlights: params fed by flowgraph links (blue) and params overridden by aliases (orange)
        private readonly HashSet<ShortGuid> _linkedInputParams = new HashSet<ShortGuid>();
        private readonly HashSet<ShortGuid> _aliasOverriddenParams = new HashSet<ShortGuid>();
        private bool _statusesComputed = false;

        public EntityParameterProxy(ParameterGridPanel host, Entity entity, Composite composite, LevelContent content)
        {
            Host = host;
            Entity = entity;
            Composite = composite;
            Content = content;
        }

        /* Force parameter rows to be rebuilt on next access */
        public void InvalidateProperties()
        {
            _properties = null;
            _statusesComputed = false;
        }

        /* Find this proxy's descriptor for a parameter by display name */
        public ParameterGridDescriptor GetParameterDescriptor(string name)
        {
            return GetProperties().Find(name, false) as ParameterGridDescriptor;
        }

        /* Contextual status for a parameter, used to colour its row in the grid (not shown in multi-edit) */
        public ParameterStatus GetParameterStatus(ShortGuid parameter)
        {
            if (Host == null || Host.IsMultiEditing)
                return ParameterStatus.None;
            if (!_statusesComputed)
                RefreshParameterStatuses();

            //On an alias, rows with a real override are orange - virtual rows just show the target's value
            if (Entity.variant == EntityVariant.ALIAS)
                return Entity.GetParameter(parameter) != null ? ParameterStatus.AliasOverride : ParameterStatus.None;

            //A flowgraph-fed pin means the inspector value is ignored entirely, so it wins over alias overrides
            if (_linkedInputParams.Contains(parameter))
                return ParameterStatus.LinkedInput;
            if (_aliasOverriddenParams.Contains(parameter))
                return ParameterStatus.AliasOverride;

            return ParameterStatus.None;
        }

        /* Recompute the linked-pin highlight state (called live as flowgraph connections change).
           Returns true if the set of linked parameters actually changed. */
        public bool RefreshLinkedPinStatuses()
        {
            HashSet<ShortGuid> previous = new HashSet<ShortGuid>(_linkedInputParams);

            _linkedInputParams.Clear();
            if (Host == null || Host.IsMultiEditing || Entity == null || Composite == null)
                return previous.Count != 0;

            //Live flowgraph state reflects connections the user is making right now - but pages are
            //built lazily, so if this entity has no nodes on any built page yet, fall back to the
            //loaded entity links instead (otherwise vanilla links would never highlight)
            CompositeDisplay display = Host.Inspector?.CompositeDisplay;
            if (display != null && display.SupportsFlowgraphs && display.AnyFlowgraphsContainEntity(Entity))
            {
                display.CollectConnectedInputPins(Entity, _linkedInputParams);
            }
            else
            {
                foreach (Entity ent in Composite.GetEntities())
                {
                    foreach (EntityConnector link in ent.childLinks)
                    {
                        if (link.linkedEntityID == Entity.shortGUID)
                            _linkedInputParams.Add(link.linkedParamID);
                    }
                }
            }

            return !previous.SetEquals(_linkedInputParams);
        }

        /* Recompute all contextual statuses (linked pins + alias overrides) */
        public void RefreshParameterStatuses()
        {
            _statusesComputed = true;
            RefreshLinkedPinStatuses();

            _aliasOverriddenParams.Clear();
            if (Host == null || Host.IsMultiEditing || Entity == null || Composite == null)
                return;
            if (Entity.variant == EntityVariant.ALIAS)
                return;

            Commands commands = Content?.Level?.Commands;
            if (commands?.Utils == null)
                return;

            foreach (Composite composite in commands.Entries)
            {
                foreach (AliasEntity alias in composite.aliases)
                {
                    if (alias.parameters.Count == 0)
                        continue;
                    //Fast pre-filter on the pointed entity ID before doing the full resolution walk
                    if (alias.alias.GetPointedEntityID() != Entity.shortGUID)
                        continue;
                    (Composite resolvedComposite, Entity resolvedEntity) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveAlias(alias, composite));
                    if (resolvedEntity != Entity)
                        continue;

                    foreach (Parameter parameter in alias.parameters)
                        _aliasOverriddenParams.Add(parameter.name);
                }
            }
        }

        private PropertyDescriptorCollection GetProperties()
        {
            if (_properties == null)
                _properties = BuildProperties();
            return _properties;
        }

        private PropertyDescriptorCollection BuildProperties()
        {
            List<PropertyDescriptor> descriptors = new List<PropertyDescriptor>();
            Commands commands = Content?.Level?.Commands;
            if (commands?.Utils == null || Composite == null || Entity == null)
                return new PropertyDescriptorCollection(descriptors.ToArray(), true);

            //Figure out what parameters we should show - input/output pin values are 'delay' values
            //for flowgraph pins, not actual parameters
            bool filterParams = Host != null && Host.FilterPinParameters;
            HashSet<ShortGuid> visibleParams = new HashSet<ShortGuid>();
            Dictionary<ShortGuid, ParameterVariant> parameterVariants = new Dictionary<ShortGuid, ParameterVariant>();
            foreach ((ShortGuid, ParameterVariant, DataType) parameter in commands.Utils.GetAllParameters(Entity, Composite))
            {
                if (!parameterVariants.ContainsKey(parameter.Item1))
                    parameterVariants.Add(parameter.Item1, parameter.Item2);

                switch (parameter.Item2)
                {
                    case ParameterVariant.INTERNAL: //NOTE: still showing internal until values are handled better (resources, spline points, etc)
                    case ParameterVariant.INPUT_PIN:
                    case ParameterVariant.PARAMETER:
                    case ParameterVariant.STATE_PARAMETER:
                        visibleParams.Add(parameter.Item1);
                        break;
                }
            }
            HashSet<ShortGuid> dynamicPinParams = NodeUtils.GetDynamicPinParameters(Entity, Composite, commands);

            //'name' is the entity's name - always editable, on every named entity type
            visibleParams.Add(ShortGuids.name);
            dynamicPinParams.Remove(ShortGuids.name);
            EnsureNameParameter();

            //Statuses drive the row highlights, and the linked set below, so resolve them up front
            RefreshParameterStatuses();
            EnsureRowsForLinkedParameters(commands, visibleParams, dynamicPinParams, parameterVariants);

            List<Parameter> displayParameters;
            if (Entity.variant == EntityVariant.ALIAS)
            {
                //Aliases display the pointed-to entity's full parameter set: rows without an override are
                //"virtual" (showing the target's value) and become real overrides the moment they're edited
                displayParameters = BuildAliasParameterList(commands);
            }
            else
            {
                Entity.parameters = Entity.parameters.OrderBy(o => o.name.ToString()).ToList();
                displayParameters = Entity.parameters;
            }

            bool hasGroups = ParameterGroupProvider.HasGroups(Entity);
            for (int i = 0; i < displayParameters.Count; i++)
            {
                Parameter parameter = displayParameters[i];
                if (parameter?.content == null)
                    continue;
                if (EntityParameterVisibility.IsHiddenFromEditor(Entity, parameter.name))
                    continue;
                if (filterParams && (!visibleParams.Contains(parameter.name) || dynamicPinParams.Contains(parameter.name)))
                    continue;

                //"resource" is always edited via the Resources button
                if (parameter.name == ShortGuids.resource && parameter.content.dataType == DataType.RESOURCE)
                    continue;

                //Use our metadata to update any wrongly typed cEnumStrings to get the nice UI
                if (parameter.content.dataType == DataType.STRING)
                {
                    ParameterData data = commands.Utils.CreateDefaultParameterData(Entity, Composite, parameter.name);
                    if (data != null && data.dataType == DataType.ENUM_STRING)
                    {
                        ((cEnumString)data).value = ((cString)parameter.content).value;
                        parameter.content = data;
                    }
                }

                PropertyDescriptor descriptor = CreateDescriptor(parameter, hasGroups, commands);
                if (descriptor != null)
                    descriptors.Add(descriptor);
            }

            return new PropertyDescriptorCollection(descriptors.ToArray(), true);
        }

        /// <summary>
        /// The name an alias/proxy inherits from the entity it points at (null when it has no name to inherit).
        /// </summary>
        public string GetInheritedName()
        {
            if (Entity.variant != EntityVariant.ALIAS && Entity.variant != EntityVariant.PROXY)
                return null;

            Commands commands = Content?.Level?.Commands;
            if (commands?.Utils == null || Composite == null)
                return null;

            string resolved = commands.Utils.GetEntityName(Composite, Entity);
            //GetEntityName falls back to the GUID when nothing is named - don't show that as a name
            if (string.IsNullOrEmpty(resolved) || resolved == Entity.shortGUID.ToByteString())
                return null;
            return resolved;
        }

        /// <summary>
        /// Entity names live in a 'name' parameter, so make sure there's always a row to edit - an empty one
        /// for entities that have no name yet. Variables are excluded: their name is their ShortGuid (the pin name).
        /// </summary>
        private void EnsureNameParameter()
        {
            if (Entity.variant == EntityVariant.VARIABLE)
                return;
            if (Entity.GetParameter(ShortGuids.name) != null)
                return;

            Entity.AddParameter(ShortGuids.name, new cString(""), ParameterVariant.PARAMETER);
        }

        /// <summary>
        /// A parameter fed by a flowgraph link usually has no value stored on the entity (the link supplies it),
        /// so it would have no row to highlight blue. Add the missing rows from defaults for linked parameters
        /// that are genuine data parameters - method/logic pins (trigger etc.) are deliberately excluded, as
        /// they aren't editable values.
        /// </summary>
        private void EnsureRowsForLinkedParameters(Commands commands, HashSet<ShortGuid> visibleParams,
            HashSet<ShortGuid> dynamicPinParams, Dictionary<ShortGuid, ParameterVariant> parameterVariants)
        {
            if (_linkedInputParams.Count == 0 || Entity.variant == EntityVariant.ALIAS)
                return;

            foreach (ShortGuid linkedParam in _linkedInputParams)
            {
                if (!visibleParams.Contains(linkedParam) || dynamicPinParams.Contains(linkedParam))
                    continue;
                if (Entity.GetParameter(linkedParam) != null)
                    continue;
                if (EntityParameterVisibility.IsHiddenFromEditor(Entity, linkedParam))
                    continue;

                ParameterData defaultData = commands.Utils.CreateDefaultParameterData(Entity, Composite, linkedParam);
                if (defaultData == null)
                    continue;

                ParameterVariant variant = parameterVariants.TryGetValue(linkedParam, out ParameterVariant found)
                    ? found : ParameterVariant.PARAMETER;
                Entity.parameters.Add(new Parameter(linkedParam, defaultData, variant));
            }
        }

        /* The alias's own overrides plus virtual rows for every other parameter on the resolved target */
        private List<Parameter> BuildAliasParameterList(Commands commands)
        {
            List<Parameter> result = new List<Parameter>(Entity.parameters);

            (Composite targetComposite, Entity targetEntity) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveAlias((AliasEntity)Entity, Composite));
            if (targetEntity != null && targetComposite != null)
            {
#if AUTO_POPULATE_PARAMS
                //Make sure the target carries its full default set so the alias shows everything available
                DockPanels.EntityInspector.EnsureDefaultsApplied(targetEntity, targetComposite, Content);
#endif
                foreach (Parameter targetParameter in targetEntity.parameters)
                {
                    if (targetParameter?.content == null)
                        continue;
                    if (targetParameter.name == ShortGuids.name)
                        continue; //the alias has its own name row - blank there means "inherit the target's name"
                    if (Entity.GetParameter(targetParameter.name) != null)
                        continue; //an override exists - show that instead

                    result.Add(new Parameter(targetParameter.name, (ParameterData)targetParameter.content.Clone(), targetParameter.variant));
                }
            }

            return result.OrderBy(o => o.name.ToString()).ToList();
        }

        private PropertyDescriptor CreateDescriptor(Parameter parameter, bool hasGroups, Commands commands)
        {
            string paramName = parameter.name.ToString();
            Attribute[] attributes = BuildAttributes(parameter, paramName, hasGroups);

            //The entity name - aliases/proxies show the name they inherit when they have none of their own
            if (parameter.name == ShortGuids.name)
                return new NameParameterDescriptor(this, parameter, paramName, attributes);

            //HACK: We handle composite material mappings as a special type!
            if (paramName == "mapping")
            {
                if (parameter.content.dataType != DataType.RESOURCE)
                    parameter.content = new cResource(null, ShortGuid.Invalid);
                return new MappingParameterDescriptor(this, parameter, paramName, attributes);
            }
            if (paramName == "Texture" && Entity is FunctionEntity textureHost && textureHost.function == FunctionType.EnvironmentMap)
            {
                if (parameter.content.dataType != DataType.STRING)
                    parameter.content = new cString(parameter.content is cString existing ? existing.value : "");
                return new TexturePathParameterDescriptor(this, parameter, paramName, attributes);
            }

            switch (parameter.content.dataType)
            {
                case DataType.TRANSFORM:
                    return new TransformParameterDescriptor(this, parameter, paramName, attributes);
                case DataType.INTEGER:
                    return new IntParameterDescriptor(this, parameter, paramName, attributes);
                case DataType.FLOAT:
                    return new FloatParameterDescriptor(this, parameter, paramName, attributes);
                case DataType.BOOL:
                    return new BoolParameterDescriptor(this, parameter, paramName, attributes);
                case DataType.STRING:
                    return new StringParameterDescriptor(this, parameter, paramName, attributes);
                case DataType.ENUM_STRING:
                    return new EnumStringParameterDescriptor(this, parameter, paramName, attributes);
                case DataType.VECTOR:
                    if (_colourParams.Contains(paramName))
                        return new ColourParameterDescriptor(this, parameter, paramName, attributes);
                    return new VectorParameterDescriptor(this, parameter, paramName, attributes);
                case DataType.ENUM:
                {
                    cEnum enumData = (cEnum)parameter.content;
                    EnumDescriptor enumDescriptor = enumData.enumID == ShortGuid.Invalid ? null : commands.Utils.GetEnum(enumData.enumID);
                    return new EnumParameterDescriptor(this, parameter, paramName, attributes, enumDescriptor);
                }
                case DataType.RESOURCE:
                    return new ResourceParameterDescriptor(this, parameter, paramName, attributes);
                case DataType.SPLINE:
                    return new SplineParameterDescriptor(this, parameter, paramName, attributes);
                default:
                    return new ReadOnlyParameterDescriptor(this, parameter, paramName, attributes);
            }
        }

        private Attribute[] BuildAttributes(Parameter parameter, string paramName, bool hasGroups)
        {
            string group = hasGroups ? (ParameterGroupProvider.GetGroup(Entity, paramName) ?? ParameterGroupProvider.DefaultGroup) : ParameterGroupProvider.DefaultGroup;
            string description = parameter.content.dataType.ToString() + " (" + parameter.variant.ToString() + ")";
            return new Attribute[]
            {
                new CategoryAttribute(group),
                new DescriptionAttribute(description)
            };
        }

        #region ICustomTypeDescriptor
        public AttributeCollection GetAttributes() => AttributeCollection.Empty;
        public string GetClassName() => Entity?.variant.ToString() ?? "Entity";
        public string GetComponentName() => Entity?.shortGUID.ToString();
        public TypeConverter GetConverter() => new TypeConverter();
        public EventDescriptor GetDefaultEvent() => null;
        public PropertyDescriptor GetDefaultProperty() => null;
        public object GetEditor(Type editorBaseType) => null;
        public EventDescriptorCollection GetEvents() => EventDescriptorCollection.Empty;
        public EventDescriptorCollection GetEvents(Attribute[] attributes) => EventDescriptorCollection.Empty;
        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties() => GetProperties();
        public PropertyDescriptorCollection GetProperties(Attribute[] attributes) => GetProperties();
        public object GetPropertyOwner(PropertyDescriptor pd) => this;
        #endregion
    }
}
