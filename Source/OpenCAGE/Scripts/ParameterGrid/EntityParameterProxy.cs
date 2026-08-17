using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
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
        }

        /* Find this proxy's descriptor for a parameter by display name */
        public ParameterGridDescriptor GetParameterDescriptor(string name)
        {
            return GetProperties().Find(name, false) as ParameterGridDescriptor;
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
            //for flowgraph pins, not actual parameters (kept in sync with ModifyParameters)
            bool filterParams = Host != null && Host.FilterPinParameters;
            List<ShortGuid> visibleParams = null;
            HashSet<ShortGuid> dynamicPinParams = null;
            if (filterParams)
            {
                visibleParams = new List<ShortGuid>();
                List<(ShortGuid, ParameterVariant, DataType)> allParameters = commands.Utils.GetAllParameters(Entity, Composite);
                foreach ((ShortGuid, ParameterVariant, DataType) parameter in allParameters)
                {
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
                dynamicPinParams = NodeUtils.GetDynamicPinParameters(Entity, Composite, commands);
            }

            Entity.parameters = Entity.parameters.OrderBy(o => o.name.ToString()).ToList();
            bool hasGroups = ParameterGroupProvider.HasGroups(Entity);
            for (int i = 0; i < Entity.parameters.Count; i++)
            {
                Parameter parameter = Entity.parameters[i];
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

        private PropertyDescriptor CreateDescriptor(Parameter parameter, bool hasGroups, Commands commands)
        {
            string paramName = parameter.name.ToString();
            Attribute[] attributes = BuildAttributes(parameter, paramName, hasGroups);

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
