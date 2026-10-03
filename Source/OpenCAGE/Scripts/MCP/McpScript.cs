using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Finding composites and entities from what a client names them, and describing them back.
    /// </summary>
    /// <remarks>
    /// Ids are ShortGuids written as their byte string ("AA-BB-CC-DD"), as OpenCAGE shows them. Entity
    /// ids are only unique within their composite, so an entity is always named together with its
    /// composite. Names are accepted wherever an id is, as long as they pick out one thing.
    /// </remarks>
    internal static class McpScript
    {
        private static readonly Regex _byteString = new Regex("^[0-9A-Fa-f]{2}(-[0-9A-Fa-f]{2}){3}$", RegexOptions.Compiled);

        public static string Id(ShortGuid guid) => guid.ToByteString();

        /// <summary>A ShortGuid written as a byte string or as its number; null if it is neither.</summary>
        public static ShortGuid? ParseId(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            text = text.Trim();
            if (_byteString.IsMatch(text)) return new ShortGuid(text.ToUpperInvariant());
            if (uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint number) && number > 0xFFFF) return new ShortGuid(number);
            return null;
        }

        /// <summary>A parameter or pin name as it is hashed ("position"), or its id if the client gives one.</summary>
        public static ShortGuid ParamId(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new McpError("A parameter name is missing.");
            ShortGuid? id = ParseId(name);
            return id ?? ShortGuidUtils.Generate(name.Trim());
        }

        public static string ParamName(ShortGuid id) => ShortGuidUtils.FindString(id);

        public static string NormalisePath(string path) => (path ?? "").Replace('/', '\\').Trim().Trim('\\');

        #region Composites
        /// <summary>
        /// A composite from its id, its path (either slash, any case), "root" / "global" / "pausemenu", or
        /// the last part of its path when only one composite ends that way.
        /// </summary>
        public static Composite FindComposite(Commands commands, string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                throw new McpError("Say which composite: its path (e.g. 'AYZ\\Science\\Corridor'), its id, or 'root'.");
            string wanted = NormalisePath(reference);

            switch (wanted.ToLowerInvariant())
            {
                case "root": return commands.EntryPoints[0];
                case "global": return commands.EntryPoints[1];
                case "pausemenu": return commands.EntryPoints[2];
            }

            ShortGuid? id = ParseId(wanted);
            if (id != null)
            {
                Composite byId = commands.GetComposite(id.Value);
                if (byId != null) return byId;
            }

            List<Composite> exact = commands.Entries.Where(o => o != null && string.Equals(NormalisePath(o.name), wanted, StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count == 1) return exact[0];
            if (exact.Count > 1)
                throw new McpError("More than one composite is called '" + wanted + "': use an id (" + string.Join(", ", exact.Select(o => Id(o.shortGUID))) + ").");

            List<Composite> ending = commands.Entries.Where(o => o != null && NormalisePath(o.name).EndsWith("\\" + wanted, StringComparison.OrdinalIgnoreCase)).ToList();
            if (ending.Count == 1) return ending[0];
            if (ending.Count > 1)
                throw new McpError("'" + wanted + "' could be any of: " + string.Join("; ", ending.Take(12).Select(o => o.name)) + (ending.Count > 12 ? " (and " + (ending.Count - 12) + " more)" : "") + ". Give the full path.");

            List<Composite> near = commands.Entries.Where(o => o != null && NormalisePath(o.name).IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0).Take(10).ToList();
            throw new McpError("No composite called '" + wanted + "' in this level." + (near.Count != 0 ? " Similar: " + string.Join("; ", near.Select(o => o.name)) + "." : " find_composites searches them."));
        }

        public static string CompositeLeaf(Composite composite) => EditorUtils.GetCompositeName(composite);

        public static JObject CompositeSummary(Commands commands, Composite composite)
        {
            JObject summary = new JObject()
            {
                ["id"] = Id(composite.shortGUID),
                ["path"] = composite.name,
            };
            if (composite == commands.EntryPoints[0]) summary["role"] = "root";
            else if (composite == commands.EntryPoints[1]) summary["role"] = "global";
            else if (composite == commands.EntryPoints[2]) summary["role"] = "pausemenu";
            return summary;
        }
        #endregion

        #region Entities
        /// <summary>An entity of the composite from its id or its name (which must pick out one entity).</summary>
        public static Entity FindEntity(Commands commands, Composite composite, string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                throw new McpError("Say which entity in " + composite.name + ": its id or its name.");
            string wanted = reference.Trim();

            ShortGuid? id = ParseId(wanted);
            if (id != null)
            {
                Entity byId = composite.GetEntityByID(id.Value);
                if (byId != null) return byId;
            }

            List<Entity> named = composite.GetEntities().Where(o => string.Equals(EntityName(commands, composite, o), wanted, StringComparison.OrdinalIgnoreCase)).ToList();
            if (named.Count == 1) return named[0];
            if (named.Count > 1)
                throw new McpError(named.Count + " entities in " + composite.name + " are called '" + wanted + "': use an id (" + string.Join(", ", named.Take(10).Select(o => Id(o.shortGUID) + " " + TypeName(commands, composite, o))) + ").");

            List<Entity> near = composite.GetEntities().Where(o => EntityName(commands, composite, o).IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0).Take(10).ToList();
            throw new McpError("No entity '" + wanted + "' in " + composite.name + "." + (near.Count != 0 ? " Similar: " + string.Join("; ", near.Select(o => EntityName(commands, composite, o) + " (" + Id(o.shortGUID) + ")")) + "." : " get_composite lists its entities."));
        }

        public static string EntityName(Commands commands, Composite composite, Entity entity)
        {
            if (entity is VariableEntity variable)
                return ShortGuidUtils.FindString(variable.name);
            return commands.Utils.GetEntityName(composite, entity);
        }

        public static string Kind(Entity entity)
        {
            switch (entity.variant)
            {
                case EntityVariant.FUNCTION: return ((FunctionEntity)entity).function.IsFunctionType ? "function" : "instance";
                case EntityVariant.VARIABLE: return "variable";
                case EntityVariant.ALIAS: return "alias";
                case EntityVariant.PROXY: return "proxy";
            }
            return entity.variant.ToString().ToLowerInvariant();
        }

        /// <summary>What an entity is: its function type, the composite it instances, its pin type, or what it stands for.</summary>
        public static string TypeName(Commands commands, Composite composite, Entity entity)
        {
            switch (entity)
            {
                case FunctionEntity function:
                    if (function.function.IsFunctionType) return function.function.AsFunctionType.ToString();
                    return commands.GetComposite(function.function)?.name ?? ("missing composite " + Id(function.function));
                case VariableEntity variable:
                    CompositePinInfoTable.PinInfo info = commands.Utils.GetPinInfo(composite, variable);
                    return info != null ? info.PinTypeGUID.AsCompositePinType.ToString() : variable.type.ToString();
                case AliasEntity alias:
                    {
                        (Composite c, Entity e) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveAlias(alias, composite));
                        return e == null ? "alias (unresolved)" : "alias of " + TypeName(commands, c, e);
                    }
                case ProxyEntity proxy:
                    {
                        (Composite c, Entity e) = commands.Utils.GetResolvedTarget(commands.Utils.ResolveProxy(proxy));
                        return e == null ? "proxy (dead)" : "proxy of " + TypeName(commands, c, e);
                    }
            }
            return entity.variant.ToString();
        }

        /// <summary>The composite an instance entity places, or null.</summary>
        public static Composite InstancedComposite(Commands commands, Entity entity)
        {
            return entity is FunctionEntity function && !function.function.IsFunctionType ? commands.GetComposite(function.function) : null;
        }

        /// <summary>The steps of an alias's or proxy's path, as names.</summary>
        public static JArray DescribePath(Commands commands, List<Tuple<Composite, Entity>> resolved, ShortGuid[] stored)
        {
            JArray steps = new JArray();
            if (resolved != null && resolved.Count != 0)
            {
                foreach (Tuple<Composite, Entity> step in resolved)
                    steps.Add(step.Item2 == null ? "?" : EntityName(commands, step.Item1, step.Item2) + " (" + Id(step.Item2.shortGUID) + ")");
                return steps;
            }
            foreach (ShortGuid id in stored ?? new ShortGuid[0])
                if (id != ShortGuid.Invalid) steps.Add(Id(id));
            return steps;
        }

        /// <summary>
        /// An alias path from names: each step names an entity in the composite the previous step instances,
        /// starting in <paramref name="from"/>. Every step but the last must be a composite instance.
        /// </summary>
        public static ShortGuid[] ResolvePath(Commands commands, Composite from, IList<string> steps, out Composite targetComposite, out Entity target)
        {
            if (steps == null || steps.Count == 0)
                throw new McpError("A path needs at least one step.");
            List<ShortGuid> path = new List<ShortGuid>();
            Composite current = from;
            target = null;
            targetComposite = null;
            for (int i = 0; i < steps.Count; i++)
            {
                Entity step = FindEntity(commands, current, steps[i]);
                path.Add(step.shortGUID);
                if (i == steps.Count - 1)
                {
                    target = step;
                    targetComposite = current;
                    break;
                }
                Composite next = InstancedComposite(commands, step);
                if (next == null)
                    throw new McpError("'" + steps[i] + "' in " + current.name + " is not a composite instance, so the path cannot go through it.");
                current = next;
            }
            path.Add(ShortGuid.Invalid);
            return path.ToArray();
        }

        /// <summary>Everything about an entity a client would want: what it is, its parameters, its links.</summary>
        public static JObject Describe(Commands commands, Composite composite, Entity entity, bool parameters = true, bool links = true, bool defaults = false)
        {
            JObject result = new JObject()
            {
                ["id"] = Id(entity.shortGUID),
                ["name"] = EntityName(commands, composite, entity),
                ["kind"] = Kind(entity),
                ["type"] = TypeName(commands, composite, entity),
            };

            switch (entity)
            {
                case VariableEntity variable:
                    result["data_type"] = variable.type.ToString();
                    break;
                case AliasEntity alias:
                    result["path"] = DescribePath(commands, commands.Utils.ResolveAlias(alias, composite), alias.alias?.path);
                    break;
                case ProxyEntity proxy:
                    result["path"] = DescribePath(commands, commands.Utils.ResolveProxy(proxy), proxy.proxy?.path);
                    break;
            }

            //A trigger sequence's entries and methods are not parameters: show them too
            List<TriggerSequence.SequenceEntry> sequence = (entity as TriggerSequence)?.sequence ?? (entity is ProxyEntity p && p.function == FunctionType.TriggerSequence ? p.sequence : null);
            List<TriggerSequence.MethodEntry> methods = (entity as TriggerSequence)?.methods ?? (entity is ProxyEntity q && q.function == FunctionType.TriggerSequence ? q.methods : null);
            if (sequence != null)
            {
                result["sequence"] = new JArray(sequence.Select(o => new JObject()
                {
                    ["path"] = DescribePath(commands, commands.Utils.ResolveEntityPath(o.connectedEntity, composite), o.connectedEntity?.path),
                    ["delay"] = Math.Round((double)o.timing, 4),
                }));
                result["methods"] = new JArray(methods.Select(o => ParamName(o.method)));
            }
            if (entity is CAGEAnimation animation)
                result["animation"] = new JObject() { ["connections"] = animation.connections.Count, ["float_tracks"] = animation.floatTracks.Count, ["event_tracks"] = animation.eventTracks.Count };

            //Pins it has from its data rather than its type (trigger methods with their relay/finished pins, animation events): linkable too
            HashSet<ShortGuid> dataPins = NodeUtils.GetDynamicPinParameters(entity, composite, commands);
            if (dataPins.Count != 0)
                result["event_pins"] = new JArray(dataPins.Select(ParamName).OrderBy(o => o, StringComparer.OrdinalIgnoreCase));

            if (parameters)
            {
                JObject values = new JObject();
                foreach (Parameter parameter in entity.parameters)
                {
                    if (parameter.name == ShortGuids.name) continue;
                    values[ParamName(parameter.name)] = McpValues.ToJson(parameter.content, commands);
                }
                result["parameters"] = values;
                if (defaults)
                {
                    JArray available = new JArray();
                    foreach ((ShortGuid name, ParameterVariant variant, DataType type) in commands.Utils.GetAllParameters(entity, composite))
                    {
                        if (entity.GetParameter(name) != null) continue;
                        available.Add(McpValues.DescribePin(commands, name, variant, type, () => commands.Utils.CreateDefaultParameterData(entity, composite, name)));
                    }
                    result["other_parameters"] = available;

                    //The relays its methods fire (LogicCounter's Up fires on_Up): link sources, though not parameters
                    Dictionary<ShortGuid, ShortGuid> relays = NodeUtils.GetMethodRelays(entity, composite, commands);
                    if (relays.Count != 0)
                        result["method_relays"] = new JObject(relays.Select(o => new JProperty(ParamName(o.Value), ParamName(o.Key))));
                }
            }

            if (links)
            {
                JArray outgoing = new JArray();
                foreach (EntityConnector link in entity.childLinks)
                {
                    Entity other = composite.GetEntityByID(link.linkedEntityID);
                    outgoing.Add(new JObject()
                    {
                        ["param"] = ParamName(link.thisParamID),
                        ["to"] = Id(link.linkedEntityID),
                        ["to_name"] = other == null ? "(missing)" : EntityName(commands, composite, other),
                        ["to_param"] = ParamName(link.linkedParamID),
                    });
                }
                JArray incoming = new JArray();
                foreach (Entity other in composite.GetEntities())
                {
                    foreach (EntityConnector link in other.childLinks)
                    {
                        if (link.linkedEntityID != entity.shortGUID) continue;
                        incoming.Add(new JObject()
                        {
                            ["from"] = Id(other.shortGUID),
                            ["from_name"] = EntityName(commands, composite, other),
                            ["from_param"] = ParamName(link.thisParamID),
                            ["param"] = ParamName(link.linkedParamID),
                        });
                    }
                }
                result["links_out"] = outgoing;
                result["links_in"] = incoming;
            }
            return result;
        }

        /// <summary>One line per entity, for listings.</summary>
        public static JObject Brief(Commands commands, Composite composite, Entity entity)
        {
            JObject brief = new JObject()
            {
                ["id"] = Id(entity.shortGUID),
                ["name"] = EntityName(commands, composite, entity),
                ["kind"] = Kind(entity),
                ["type"] = TypeName(commands, composite, entity),
            };
            if (entity.GetParameter(ShortGuids.position)?.content is cTransform transform)
            {
                brief["position"] = McpValues.Vector(transform.position);
                if (transform.rotation != Vector3.Zero)
                    brief["rotation"] = McpValues.Vector(transform.rotation);
            }
            return brief;
        }
        #endregion
    }

    /// <summary>Parameter values to JSON and back.</summary>
    internal static class McpValues
    {
        public static JArray Vector(Vector3 v) => new JArray(Round(v.X), Round(v.Y), Round(v.Z));

        private static double Round(float value) => Math.Round((double)value, 5);

        public static JToken ToJson(ParameterData data, Commands commands, Models models = null)
        {
            switch (data)
            {
                case null: return JValue.CreateNull();
                case cBool b: return b.value;
                case cInteger i: return i.value;
                case cFloat f: return Round(f.value);
                case cEnumString es: return es.value;
                case cString s: return s.value;
                case cVector3 v: return Vector(v.value);
                case cTransform t: return new JObject() { ["position"] = Vector(t.position), ["rotation"] = Vector(t.rotation) };
                case cEnum e:
                    {
                        CathodeEnumTable.EnumDescriptor descriptor = commands?.Utils.GetEnum(e.enumID);
                        string entry = descriptor?.Entries.FirstOrDefault(o => o.Index == e.enumIndex)?.Name;
                        return entry ?? (JToken)e.enumIndex;
                    }
                case cSpline spline:
                    return new JObject() { ["points"] = new JArray(spline.splinePoints.Select(o => ToJson(o, commands, models))) };
                case cResource resource:
                    {
                        //A material "mapping" parameter holds no resources, only the id of one of the level's mapping sets
                        if ((resource.value == null || resource.value.Count == 0) && resource.shortGUID != ShortGuid.Invalid)
                        {
                            Level open = Singleton.Editor?.CompositeBrowser?.Content?.Level;
                            string mapping = open != null && open.Commands == commands ? open.MaterialMappings?.Entries?.FirstOrDefault(o => o.ID == resource.shortGUID)?.Name : null;
                            if (mapping != null) return mapping;
                            return new JObject() { ["resource"] = new JArray(), ["id"] = McpScript.Id(resource.shortGUID) };
                        }
                        JArray references = new JArray();
                        foreach (ResourceReference reference in resource.value ?? new List<ResourceReference>())
                        {
                            JObject item = new JObject() { ["type"] = reference.resource_type.ToString() };
                            if (reference.resource_type == ResourceType.RENDERABLE_INSTANCE)
                                item["models"] = new JArray(reference.RenderableInstance.Select(o => DescribeRenderable(o, models)).Distinct().Take(20));
                            references.Add(item);
                        }
                        return new JObject() { ["resource"] = references };
                    }
            }
            return data.dataType.ToString();
        }

        private static string DescribeRenderable(RenderableElements.Element element, Models models)
        {
            string model = element.Model == null ? "?" : McpAssets.SubmeshName(element.Model, models);
            string material = element.Material == null ? "?" : element.Material.Name;
            return model + " [" + material + "]";
        }

        /// <summary>A pin of an entity or function type, for listings: its name, kind, type and default.</summary>
        public static JObject DescribePin(Commands commands, ShortGuid name, ParameterVariant variant, DataType type, Func<ParameterData> defaultValue)
        {
            JObject pin = new JObject()
            {
                ["name"] = McpScript.ParamName(name),
                ["kind"] = PinKind(variant),
            };
            bool valueless = variant == ParameterVariant.METHOD_PIN || variant == ParameterVariant.METHOD_FUNCTION || variant == ParameterVariant.TARGET_PIN || variant == ParameterVariant.REFERENCE_PIN;
            if (!valueless)
            {
                ParameterData fallback = null;
                try { fallback = defaultValue?.Invoke(); } catch { }
                pin["type"] = fallback is cEnum ce ? "enum " + (commands.Utils.GetEnum(ce.enumID)?.Name ?? McpScript.Id(ce.enumID))
                            : fallback is cEnumString ces ? "enum_string " + ShortGuidUtils.FindString(ces.enumID)
                            : type.ToString().ToLowerInvariant();
                if (fallback != null)
                    pin["default"] = ToJson(fallback, commands);
                else if (CommandsUtils.IsPointerType(type) || type == DataType.RESOURCE || type == DataType.SPLINE)
                    pin["link_only"] = true;
            }
            return pin;
        }

        public static string PinKind(ParameterVariant variant)
        {
            switch (variant)
            {
                case ParameterVariant.METHOD_PIN: return "method";
                case ParameterVariant.METHOD_FUNCTION: return "method_function";
                case ParameterVariant.TARGET_PIN: return "target";
                case ParameterVariant.REFERENCE_PIN: return "reference";
                case ParameterVariant.INPUT_PIN: return "input";
                case ParameterVariant.OUTPUT_PIN: return "output";
                case ParameterVariant.STATE_PARAMETER: return "state";
                case ParameterVariant.INTERNAL: return "internal";
                case ParameterVariant.PARAMETER: return "parameter";
            }
            return variant.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// The value a JSON token describes, as the kind of data <paramref name="template"/> is (the parameter's
        /// current value or its default). With no template the kind is guessed from the JSON.
        /// </summary>
        public static ParameterData FromJson(JToken value, ParameterData template, string parameterName, Commands commands)
        {
            if (value == null || value.Type == JTokenType.Null)
                throw new McpError("No value given for '" + parameterName + "'.");

            //The editor treats any parameter called "mapping" as a composite's material mapping set, picked by name
            if (string.Equals(parameterName, "mapping", StringComparison.OrdinalIgnoreCase) && value.Type == JTokenType.String)
                return ReadMapping((string)value, commands);

            switch (template)
            {
                case null: return Guess(value, parameterName);
                case cBool _: return new cBool(ReadBool(value, parameterName));
                case cInteger _: return new cInteger(ReadInt(value, parameterName));
                case cFloat _: return new cFloat((float)ReadDouble(value, parameterName));
                case cEnumString es: return new cEnumString(es.enumID, ReadString(value));
                case cString _: return new cString(ReadString(value));
                case cVector3 v: return new cVector3(ReadVector(value, parameterName, v.value));
                case cTransform t: return ReadTransform(value, parameterName, t);
                case cEnum e: return ReadEnum(value, parameterName, e, commands);
                case cSpline _: return ReadSpline(value, parameterName);
                case cResource _:
                    throw new McpError("'" + parameterName + "' is a resource and cannot be written as a value: use set_renderable (model and materials), set_collision, set_physics_system or set_animated_model (get_entity_resources shows them); place_model places a new model, and port_composites brings content in with its models.");
            }
            throw new McpError("'" + parameterName + "' is a " + template.dataType + ", which cannot be set from JSON.");
        }

        /// <summary>A material mapping set of the open level, by name or id ("" or "none" for no mapping), as the "mapping" parameter holds it.</summary>
        private static ParameterData ReadMapping(string text, Commands commands)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0 || text.Equals("none", StringComparison.OrdinalIgnoreCase))
                return new cResource(null, ShortGuid.Invalid);
            Level open = Singleton.Editor?.CompositeBrowser?.Content?.Level;
            if (open == null || open.Commands != commands || open.MaterialMappings?.Entries == null)
                throw new McpError("Material mappings can only be set in the level that is open.");
            MaterialMappings.MaterialMapping map = open.MaterialMappings.Entries.FirstOrDefault(o => string.Equals(o.Name, text, StringComparison.OrdinalIgnoreCase));
            if (map == null && McpScript.ParseId(text) is ShortGuid id)
                map = open.MaterialMappings.Entries.FirstOrDefault(o => o.ID == id);
            if (map == null)
                throw new McpError("This level has no material mapping called '" + text + "' (list_material_mappings shows them; \"none\" clears the mapping).");
            return new cResource(null, map.ID);
        }

        private static ParameterData Guess(JToken value, string parameterName)
        {
            switch (value.Type)
            {
                case JTokenType.Boolean: return new cBool((bool)value);
                case JTokenType.Integer: return new cInteger(ReadInt(value, parameterName));
                case JTokenType.Float: return new cFloat((float)(double)value);
                case JTokenType.String: return new cString((string)value);
                case JTokenType.Array: return new cVector3(ReadVector(value, parameterName, null));
                case JTokenType.Object:
                    if (value["position"] != null || value["rotation"] != null) return ReadTransform(value, parameterName, null);
                    if (value["points"] != null) return ReadSpline(value, parameterName);
                    break;
            }
            throw new McpError("Could not tell what kind of value '" + parameterName + "' should be from " + value.ToString(Newtonsoft.Json.Formatting.None) + ".");
        }

        public static bool ReadBool(JToken value, string name)
        {
            switch (value.Type)
            {
                case JTokenType.Boolean: return (bool)value;
                case JTokenType.Integer: return (long)value != 0;
                case JTokenType.String:
                    string text = ((string)value).Trim().ToLowerInvariant();
                    if (text == "true" || text == "1" || text == "yes") return true;
                    if (text == "false" || text == "0" || text == "no") return false;
                    break;
            }
            throw new McpError("'" + name + "' takes true or false.");
        }

        public static int ReadInt(JToken value, string name)
        {
            if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
            {
                double number = (double)value;
                if (Math.Abs(number - Math.Round(number)) < 1e-9 && number >= int.MinValue && number <= int.MaxValue) return (int)Math.Round(number);
                throw new McpError("'" + name + "' takes a whole number between " + int.MinValue + " and " + int.MaxValue + ".");
            }
            if (value.Type == JTokenType.String && int.TryParse((string)value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)) return parsed;
            if (value.Type == JTokenType.Boolean) return (bool)value ? 1 : 0;
            throw new McpError("'" + name + "' takes a whole number.");
        }

        public static double ReadDouble(JToken value, string name)
        {
            if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float) return (double)value;
            if (value.Type == JTokenType.String && double.TryParse((string)value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)) return parsed;
            throw new McpError("'" + name + "' takes a number.");
        }

        public static string ReadString(JToken value) => value.Type == JTokenType.String ? (string)value : value.ToString(Newtonsoft.Json.Formatting.None);

        /// <summary>[x, y, z], or {"x":, "y":, "z":}; missing parts keep <paramref name="current"/>'s.</summary>
        public static Vector3 ReadVector(JToken value, string name, Vector3? current)
        {
            Vector3 v = current ?? Vector3.Zero;
            if (value is JArray array)
            {
                if (array.Count != 3) throw new McpError("'" + name + "' takes three numbers [x, y, z].");
                return new Vector3((float)ReadDouble(array[0], name), (float)ReadDouble(array[1], name), (float)ReadDouble(array[2], name));
            }
            if (value is JObject obj)
            {
                if (obj["x"] != null) v.X = (float)ReadDouble(obj["x"], name);
                if (obj["y"] != null) v.Y = (float)ReadDouble(obj["y"], name);
                if (obj["z"] != null) v.Z = (float)ReadDouble(obj["z"], name);
                return v;
            }
            throw new McpError("'" + name + "' takes three numbers [x, y, z].");
        }

        /// <summary>{"position": [x,y,z], "rotation": [x,y,z]} (either may be left out to keep it), or [x,y,z] for just the position.</summary>
        public static cTransform ReadTransform(JToken value, string name, cTransform current)
        {
            cTransform result = new cTransform(current?.position ?? Vector3.Zero, current?.rotation ?? Vector3.Zero);
            if (value is JArray)
            {
                result.position = ReadVector(value, name, null);
                return result;
            }
            if (value is JObject obj)
            {
                if (obj["position"] != null) result.position = ReadVector(obj["position"], name + ".position", result.position);
                if (obj["rotation"] != null) result.rotation = ReadVector(obj["rotation"], name + ".rotation", result.rotation);
                if (obj["position"] == null && obj["rotation"] == null && (obj["x"] != null || obj["y"] != null || obj["z"] != null))
                    result.position = ReadVector(obj, name, result.position);
                return result;
            }
            throw new McpError("'" + name + "' takes {\"position\": [x, y, z], \"rotation\": [x, y, z]} (rotation in degrees).");
        }

        private static cEnum ReadEnum(JToken value, string name, cEnum template, Commands commands)
        {
            ShortGuid enumId = template.enumID;
            JToken entryToken = value;
            if (value is JObject obj)
            {
                if (obj["enum"] != null)
                {
                    CathodeEnumTable.EnumDescriptor named = commands.Utils.GetEnum((string)obj["enum"]);
                    if (named == null) throw new McpError("There is no enum called '" + obj["enum"] + "'.");
                    enumId = named.ID;
                }
                entryToken = obj["value"] ?? obj["index"];
                if (entryToken == null) throw new McpError("'" + name + "' takes an enum entry name, e.g. \"" + name + "\": \"ENTRY\".");
            }
            CathodeEnumTable.EnumDescriptor descriptor = commands.Utils.GetEnum(enumId);
            if (entryToken.Type == JTokenType.Integer)
            {
                int index = (int)(long)entryToken;
                if (descriptor != null && !descriptor.Entries.Any(o => o.Index == index))
                    throw new McpError("'" + index + "' is not a value of " + descriptor.Name + ". It takes: " + string.Join(", ", descriptor.Entries.Select(o => o.Name)) + ".");
                return new cEnum(enumId, index);
            }
            string text = ReadString(entryToken).Trim();
            if (descriptor == null)
                throw new McpError("'" + name + "' is an enum OpenCAGE has no names for: give its number.");
            string bare = text.Contains(".") ? text.Substring(text.LastIndexOf('.') + 1) : text;
            CathodeEnumTable.EnumDescriptor.Entry entry = descriptor.Entries.FirstOrDefault(o => string.Equals(o.Name, bare, StringComparison.OrdinalIgnoreCase));
            if (entry == null && int.TryParse(bare, out int numeric))
                entry = descriptor.Entries.FirstOrDefault(o => o.Index == numeric);
            if (entry == null)
                throw new McpError("'" + text + "' is not a value of " + descriptor.Name + ". It takes: " + string.Join(", ", descriptor.Entries.Select(o => o.Name)) + ".");
            return new cEnum(enumId, entry.Index);
        }

        private static cSpline ReadSpline(JToken value, string name)
        {
            JToken points = value is JObject obj ? obj["points"] : value;
            if (!(points is JArray array))
                throw new McpError("'" + name + "' takes {\"points\": [{\"position\": [x,y,z], \"rotation\": [x,y,z]}, ...]}.");
            return new cSpline(array.Select((o, i) => ReadTransform(o, name + "[" + i + "]", null)).ToList());
        }
    }
}
