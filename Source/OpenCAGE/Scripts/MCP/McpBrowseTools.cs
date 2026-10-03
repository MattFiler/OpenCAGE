using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.DockPanels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.MCP
{
    /// <summary>Reading the open level's script, and what the engine's entity types and enums are.</summary>
    internal static class McpBrowseTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "find_composites",
                Title = "Find composites",
                Description = "Search the open level's composites by path. Paths are folders separated by backslashes, e.g. 'AYZ\\Science\\Corridors\\...' for level geometry, 'Archetypes\\...' for reusable gameplay (NPCs, doors, pickups, mission scripts), 'DisplayModel:...' for character models. Use search_level to look in a level that is not open.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Words the path must contain (all of them, any case). Leave out to list everything."),
                    McpSchema.Boolean("placed_only", "Only composites that are placed somewhere in the level (reachable from its root)."),
                    McpSchema.Integer("limit", "At most this many (default 200).")),
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() =>
                {
                    LevelContent content = McpEditor.RequireLevel(forEditing: false);
                    Commands commands = content.Level.Commands;
                    string[] words = (call.Str("filter") ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    int limit = Math.Max(1, call.Int("limit", 200));
                    HashSet<Composite> placed = call.Bool("placed_only") ? Reachable(commands) : null;
                    List<Composite> matches = commands.Entries.Where(o => o != null
                        && words.All(w => (o.name ?? "").IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0 || (w.Length == 11 && McpScript.Id(o.shortGUID).Equals(w, StringComparison.OrdinalIgnoreCase)))
                        && (placed == null || placed.Contains(o))).OrderBy(o => o.name).ToList();
                    return new JObject()
                    {
                        ["count"] = matches.Count,
                        ["composites"] = new JArray(matches.Take(limit).Select(o =>
                        {
                            JObject summary = McpScript.CompositeSummary(commands, o);
                            summary["entities"] = o.GetEntities().Count;
                            return summary;
                        })),
                    };
                }),
            };

            yield return new McpTool()
            {
                Name = "get_composite",
                Title = "Get composite",
                Description = "A composite's contents: its entities (id, name, kind, type, position), its variables (the pins its instances expose), its links (with links: true), whether its script is shown as flowgraph pages, and where it is placed. 'limit' caps each list; totals are always given.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "Path or id; 'root' for the level's root.", required: true),
                    McpSchema.String("filter", "Only entities whose name or type contains this."),
                    McpSchema.Boolean("links", "Also list the composite's links."),
                    McpSchema.Integer("limit", "At most this many entities, and this many links (default 300).")),
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() =>
                {
                    LevelContent content = McpEditor.RequireLevel(forEditing: false);
                    Commands commands = content.Level.Commands;
                    Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                    CompileIfShown(composite);
                    string filter = call.Str("filter");
                    int limit = Math.Max(1, call.Int("limit", 300));

                    JObject result = McpScript.CompositeSummary(commands, composite);
                    result["script_view"] = FlowgraphLayoutManager.HasCompatibilityInfo(composite) ? (FlowgraphLayoutManager.IsCompatible(composite) ? "pages" : "links") : (FlowgraphLayoutManager.HasLayout(composite) ? "pages (not yet checked)" : "none yet");
                    result["pages"] = new JArray(FlowgraphLayoutManager.GetLayouts(composite).Select(o => o.Name));

                    List<Entity> entities = composite.GetEntities().Where(o => !(o is VariableEntity) && (filter == null
                        || McpScript.EntityName(commands, composite, o).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                        || McpScript.TypeName(commands, composite, o).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                    result["entity_count"] = composite.GetEntities().Count;
                    result["entities"] = new JArray(entities.Take(limit).Select(o => McpScript.Brief(commands, composite, o)));
                    if (entities.Count > limit)
                        result["entities_note"] = "Showing " + limit + " of " + entities.Count + (filter == null ? "" : " matching") + ": narrow with 'filter' or raise 'limit'.";
                    result["variables"] = new JArray(composite.variables.Take(limit).Select(o => new JObject()
                    {
                        ["id"] = McpScript.Id(o.shortGUID),
                        ["name"] = McpScript.EntityName(commands, composite, o),
                        ["pin_type"] = McpScript.TypeName(commands, composite, o),
                    }));
                    int linkCount = composite.GetEntities().Sum(o => o.childLinks.Count);
                    result["link_count"] = linkCount;
                    if (call.Bool("links"))
                    {
                        JArray links = new JArray();
                        foreach (Entity owner in composite.GetEntities())
                        {
                            foreach (EntityConnector link in owner.childLinks)
                            {
                                if (links.Count >= limit) break;
                                Entity target = composite.GetEntityByID(link.linkedEntityID);
                                links.Add(McpScript.EntityName(commands, composite, owner) + "." + McpScript.ParamName(link.thisParamID) + " -> " +
                                    (target == null ? McpScript.Id(link.linkedEntityID) : McpScript.EntityName(commands, composite, target)) + "." + McpScript.ParamName(link.linkedParamID));
                            }
                            if (links.Count >= limit) break;
                        }
                        result["links"] = links;
                        if (linkCount > links.Count)
                            result["links_note"] = "Showing " + links.Count + " of " + linkCount + " links: raise 'limit', or describe_entity / get_flowgraph for one entity's or page's links.";
                    }
                    List<JObject> placedIn = PlacedIn(commands, composite).ToList();
                    result["placed_in"] = new JArray(placedIn.Take(50));
                    if (placedIn.Count > 50) result["placed_in_count"] = placedIn.Count;
                    return result;
                }),
            };

            yield return new McpTool()
            {
                Name = "describe_entity",
                Title = "Describe entity",
                Description = "Everything about one entity: what it is, its parameter values, its links in and out, every other pin it has (with kind, type and default), the relay each method fires (method_relays), event pins (trigger methods, animation events), its resource types and the CAGEAnimations that drive it. 'derived_parameters' are worked out on save/build and not set by hand.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite it is in (path or id).", required: true),
                    McpSchema.String("entity", "Its id or name.", required: true)),
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() =>
                {
                    Commands commands = McpEditor.RequireCommands(forEditing: false);
                    Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                    CompileIfShown(composite);
                    Entity entity = McpScript.FindEntity(commands, composite, call.Str("entity", required: true));
                    JObject result = McpScript.Describe(commands, composite, entity, defaults: true);
                    result["composite"] = composite.name;

                    //What the inspector hides: values the save or build writes, which set_parameters refuses
                    List<string> derived = entity.parameters.Where(o => EntityParameterVisibility.IsHiddenFromEditor(entity, o.name)).Select(o => McpScript.ParamName(o.name)).ToList();
                    if (derived.Count != 0)
                        result["derived_parameters"] = new JArray(derived);
                    if (result["other_parameters"] is JArray others)
                        result["other_parameters"] = new JArray(others.Where(o => !(o is JObject pin && pin["name"]?.Type == JTokenType.String && EntityParameterVisibility.IsHiddenFromEditor(entity, McpScript.ParamId((string)pin["name"])))));

                    //Where the rest lives: its resources (model, collision, physics, animated model) and what animates it
                    if (entity is FunctionEntity function && function.resources != null && function.resources.Count != 0)
                        result["resources"] = new JObject()
                        {
                            ["types"] = new JArray(function.resources.Select(o => o.resource_type.ToString()).Distinct()),
                            ["details"] = "get_entity_resources",
                        };
                    JArray animatedBy = McpCageAnimationTools.AnimatedBy(commands, composite, entity, 20);
                    if (animatedBy.Count != 0)
                        result["animated_by"] = animatedBy;
                    if (entity is CAGEAnimation)
                        result["animation_details"] = "get_cage_animation";
                    return result;
                }),
            };

            yield return new McpTool()
            {
                Name = "find_entities",
                Title = "Find entities",
                Description = "Search the level's script for entities by name, type (a function type such as 'Character', or text in the path of the composite an instance places), parameter value, or place. 'within' searches a composite and everything placed under it, giving each hit's instance path and position; near + radius or box_min + box_max search by place (within the root by default).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("name", "Text the entity's name contains."),
                    McpSchema.String("type", "A function type (e.g. 'Character'), or text the path of the composite an instance places contains. Both are matched."),
                    McpSchema.String("parameter", "It has this parameter."),
                    McpSchema.Any("value", "...with this value (numbers, vectors and transforms compared within a small tolerance; a transform may give only position)."),
                    McpSchema.String("composite", "Only look in this composite (path or id)."),
                    McpSchema.String("within", "Look in this composite and everything placed under it ('root' for the whole placed level), with instance paths."),
                    McpSchema.Vector("near", "Only entities within 'radius' metres of this point (positions from 'within')."),
                    McpSchema.Number("radius", "Metres, for 'near'."),
                    McpSchema.Vector("box_min", "Only entities inside the box from box_min to box_max (positions from 'within')."),
                    McpSchema.Vector("box_max", "The box's other corner."),
                    McpSchema.Integer("limit", "At most this many (default 100).")),
                ReadOnly = true,
                Idempotent = true,
                Run = FindEntities,
            };

            yield return new McpTool()
            {
                Name = "find_references",
                Title = "Find references",
                Description = "What refers to an entity: links to and from it, aliases (with the parameters each overrides) and proxies pointing at it, TriggerSequences (and proxies' sequences) listing it, CAGEAnimations animating it, and the script pages drawing it. With no entity: every instance of the composite. 'scope' limits the search.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id).", required: true),
                    McpSchema.String("entity", "The entity (id or name); leave out to find instances of the composite itself."),
                    McpSchema.String("scope", "Only look in this composite and the composites placed under it (e.g. 'root' for what the level places)."),
                    McpSchema.Integer("limit", "At most this many references or instances (default 200).")),
                ReadOnly = true,
                Idempotent = true,
                Run = FindReferences,
            };

            yield return new McpTool()
            {
                Name = "list_function_types",
                Title = "List function types",
                Description = "The built-in entity types (FunctionType) the engine provides, e.g. ModelReference, Character, TriggerBox, LogicGate, ThinkOnce, CMD_GoTo. Filter by name or category (categories need a level open).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Text the type name contains."),
                    McpSchema.String("category", "Only this flowgraph category (e.g. 'Logic', 'Character', 'Triggers').")),
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() =>
                {
                    string filter = call.Str("filter");
                    string category = call.Str("category");
                    bool levelOpen = Singleton.Editor?.CompositeBrowser?.Content?.IsLevelDataLoaded == true;
                    if (category != null && !levelOpen)
                        throw new McpError("Categories need a level open (load_level first); 'filter' works without one.");
                    JArray types = new JArray();
                    foreach (FunctionType type in Enum.GetValues(typeof(FunctionType)).Cast<FunctionType>().OrderBy(o => o.ToString()))
                    {
                        string name = type.ToString();
                        if (filter != null && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        string cat = null;
                        if (levelOpen) { try { cat = FlowgraphLayoutManager.GetCategoryForFunctionType(type); } catch { } }
                        if (category != null && !string.Equals(cat, category, StringComparison.OrdinalIgnoreCase)) continue;
                        types.Add(cat == null ? (JToken)name : new JObject() { ["type"] = name, ["category"] = cat });
                    }
                    return new JObject() { ["count"] = types.Count, ["types"] = types };
                }),
            };

            yield return new McpTool()
            {
                Name = "describe_function_type",
                Title = "Describe function type",
                Description = "A function type's pins, including those it inherits: methods (triggered by links), relays/targets (fire links out), parameters/inputs/states (values, with type and default), outputs and references. Needs a level open.",
                InputSchema = McpSchema.Object(McpSchema.String("type", "The FunctionType name, e.g. 'ThinkOnce'.", required: true)),
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() =>
                {
                    Commands commands = McpEditor.RequireCommands(forEditing: false);
                    string name = call.Str("type", required: true);
                    if (!Enum.TryParse(name.Trim(), true, out FunctionType type) || !Enum.IsDefined(typeof(FunctionType), type))
                    {
                        List<string> near = Enum.GetNames(typeof(FunctionType)).Where(o => o.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0).Take(15).ToList();
                        throw new McpError("There is no function type '" + name + "'." + (near.Count != 0 ? " Similar: " + string.Join(", ", near) + "." : " list_function_types lists them.") +
                            (commands.Entries.Any(o => o != null && McpScript.NormalisePath(o.name).EndsWith(name, StringComparison.OrdinalIgnoreCase)) ? " There is a composite with that name - place it as an instance." : ""));
                    }
                    JArray chain = new JArray();
                    JArray pins = new JArray();
                    HashSet<ShortGuid> seen = new HashSet<ShortGuid>();
                    FunctionType? current = type;
                    while (current != null)
                    {
                        //An inherited pin's default lives with the type it comes from: the derived type's own block does not hold it
                        FunctionType owner = current.Value;
                        chain.Add(owner.ToString());
                        foreach ((ShortGuid pin, ParameterVariant variant, DataType dataType) in commands.Utils.GetAllParameters(owner))
                        {
                            if (!seen.Add(pin)) continue;
                            JObject described = McpValues.DescribePin(commands, pin, variant, dataType, () => commands.Utils.CreateDefaultParameterData(owner, pin, variant) ?? (owner == type ? null : commands.Utils.CreateDefaultParameterData(type, pin, variant)));
                            if (variant == ParameterVariant.METHOD_PIN)
                            {
                                ShortGuid relay = commands.Utils.GetRelay(pin);
                                if (relay != ShortGuid.Invalid) described["relay"] = McpScript.ParamName(relay);
                            }
                            if (owner != type) described["from"] = owner.ToString();
                            pins.Add(described);
                        }
                        try { current = commands.Utils.GetInheritedFunction(owner); } catch { current = null; }
                    }
                    JObject result = new JObject() { ["type"] = type.ToString(), ["inherits"] = new JArray(chain.Skip(1)), ["pins"] = pins };
                    try { result["category"] = FlowgraphLayoutManager.GetCategoryForFunctionType(type); } catch { }
                    result["docs"] = "https://opencage.co.uk/docs/cathode-entities/#" + type;
                    return result;
                }),
            };

            yield return new McpTool()
            {
                Name = "list_enums",
                Title = "List enums",
                Description = "The engine's enums (the types enum parameters take), optionally with their values, and the enum-string types (list_enum_string_values lists each one's values).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Text the enum's name contains."),
                    McpSchema.Boolean("values", "Include each enum's values (at most 200 each)."),
                    McpSchema.Integer("limit", "At most this many enums (default 200).")),
                ReadOnly = true,
                Idempotent = true,
                Run = call =>
                {
                    string filter = call.Str("filter");
                    bool values = call.Bool("values");
                    int limit = Math.Max(1, call.Int("limit", 200));
                    const int valuesLimit = 200;
                    List<CathodeEnumTable.EnumDescriptor> enums = CustomTable.Vanilla.CathodeEnums.enums.Where(o => filter == null || o.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(o => o.Name).ToList();
                    JToken Describe(CathodeEnumTable.EnumDescriptor descriptor)
                    {
                        if (!values) return descriptor.Name;
                        JObject described = new JObject() { ["name"] = descriptor.Name, ["values"] = new JArray(descriptor.Entries.Take(valuesLimit).Select(e => e.Name)) };
                        if (descriptor.Entries.Count > valuesLimit) described["values_total"] = descriptor.Entries.Count;
                        return described;
                    }
                    return new JObject()
                    {
                        ["count"] = enums.Count,
                        ["enums"] = new JArray(enums.Take(limit).Select(Describe)),
                        ["note"] = enums.Count > limit ? "Showing " + limit + " of " + enums.Count + ": narrow with 'filter' or raise 'limit'." : null,
                        ["enum_string_types"] = filter == null ? new JArray(Enum.GetNames(typeof(EnumStringType))) : null,
                    };
                },
            };
        }

        #region Finding entities
        private static object FindEntities(McpCall call)
        {
            string name = call.Str("name");
            string type = call.Str("type");
            string parameter = call.Str("parameter");
            JToken value = call.Token("value");
            int limit = Math.Max(1, call.Int("limit", 100));
            bool near = call.Has("near");
            bool box = call.Has("box_min") || call.Has("box_max");
            if (near && !call.Has("radius"))
                throw new McpError("'near' needs 'radius' (metres).");
            if (call.Has("radius") && !near)
                throw new McpError("'radius' goes with 'near'.");
            if (box && !(call.Has("box_min") && call.Has("box_max")))
                throw new McpError("A box needs both 'box_min' and 'box_max'.");
            if (near && box)
                throw new McpError("Search near a point or inside a box, not both.");
            bool spatial = near || box;
            if (name == null && type == null && parameter == null && !spatial)
                throw new McpError("Give at least one of name, type, parameter, near (with radius) or box_min/box_max.");
            if (value != null && parameter == null)
                throw new McpError("'value' goes with 'parameter'.");
            if (call.Has("composite") && call.Has("within"))
                throw new McpError("Give 'composite' (that composite only) or 'within' (it and everything placed under it), not both.");
            if (spatial && call.Has("composite"))
                throw new McpError("A search by place needs positions in the level: use 'within' (default: the root) rather than 'composite'.");

            Vector3 centre = near ? McpValues.ReadVector(call.Token("near"), "near", null) : Vector3.Zero;
            double radius = near ? call.Num("radius") : 0;
            if (near && radius < 0)
                throw new McpError("'radius' cannot be negative.");
            Vector3 min = Vector3.Zero, max = Vector3.Zero;
            if (box)
            {
                Vector3 a = McpValues.ReadVector(call.Token("box_min"), "box_min", null);
                Vector3 b = McpValues.ReadVector(call.Token("box_max"), "box_max", null);
                min = Vector3.Min(a, b);
                max = Vector3.Max(a, b);
            }
            bool Inside(cTransform world)
            {
                if (world == null) return false;
                if (near) return Vector3.Distance(world.position, centre) <= radius;
                return world.position.X >= min.X && world.position.Y >= min.Y && world.position.Z >= min.Z
                    && world.position.X <= max.X && world.position.Y <= max.Y && world.position.Z <= max.Z;
            }

            //A type name picks that type; the same text also picks instances of composites whose path holds it ('Door' finds both)
            string functionName = type == null ? null : Enum.GetNames(typeof(FunctionType)).FirstOrDefault(o => string.Equals(o, type.Trim(), StringComparison.OrdinalIgnoreCase));
            FunctionType? function = functionName == null ? (FunctionType?)null : (FunctionType)Enum.Parse(typeof(FunctionType), functionName);
            ShortGuid? parameterId = parameter == null ? (ShortGuid?)null : McpScript.ParamId(parameter);
            string parameterName = parameterId == null ? null : McpScript.ParamName(parameterId.Value);

            //A walk from the root of a big level visits over a million entities
            using (McpEditorTools.Heartbeat(call, "Searching the level"))
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                bool Matches(Composite composite, Entity entity)
                {
                    if (type != null)
                    {
                        bool ofType = function != null && entity is FunctionEntity f && f.function == function.Value;
                        if (!ofType)
                        {
                            Composite instanced = McpScript.InstancedComposite(commands, entity);
                            if (instanced == null || (instanced.name ?? "").IndexOf(type.Trim(), StringComparison.OrdinalIgnoreCase) < 0) return false;
                        }
                    }
                    if (name != null && McpScript.EntityName(commands, composite, entity).IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0)
                        return false;
                    if (parameterId != null)
                    {
                        Parameter p = entity.GetParameter(parameterId.Value);
                        if (p == null) return false;
                        if (value != null && !SameValue(p.content, value, parameterName, commands)) return false;
                    }
                    return true;
                }

                JArray found = new JArray();
                int total = 0;
                JObject result = new JObject();
                if (call.Has("within") || spatial)
                {
                    Composite start = call.Has("within") ? McpScript.FindComposite(commands, call.Str("within")) : commands.EntryPoints[0];
                    bool fromRoot = start == commands.EntryPoints[0];
                    McpPlacements walker = new McpPlacements(commands);
                    walker.Walk(start, step =>
                    {
                        if (!Matches(step.Composite, step.Entity)) return true;
                        if (spatial && !Inside(step.World)) return true;
                        total++;
                        if (found.Count >= limit) return true;
                        McpPlacements.Placement placement = McpPlacements.Placement.Of(step);
                        JObject described = McpEditingTools.DescribePlacement(commands, start, placement);
                        JObject brief = McpScript.Brief(commands, step.Composite, step.Entity);
                        brief["composite"] = step.Composite.name;
                        brief["path"] = described["path"];
                        brief["ids"] = described["ids"];
                        if (described["position"] != null)
                        {
                            brief[fromRoot ? "world_position" : "within_position"] = described["position"];
                            brief[fromRoot ? "world_rotation" : "within_rotation"] = described["rotation"];
                        }
                        if (described["position_from"] != null) brief["position_from"] = described["position_from"];
                        found.Add(brief);
                        return true;
                    }, cancel: call.Cancel);
                    result["within"] = start.name;
                    if (walker.Truncated)
                        result["truncated"] = "Stopped after " + walker.Visited + " entities: search within a smaller composite.";
                }
                else
                {
                    List<Composite> scope = call.Has("composite") ? new List<Composite>() { McpScript.FindComposite(commands, call.Str("composite")) } : commands.Entries.Where(o => o != null).ToList();
                    foreach (Composite composite in scope)
                    {
                        foreach (Entity entity in composite.GetEntities())
                        {
                            if (!Matches(composite, entity)) continue;
                            total++;
                            if (found.Count >= limit) continue;
                            JObject brief = McpScript.Brief(commands, composite, entity);
                            brief["composite"] = composite.name;
                            found.Add(brief);
                        }
                    }
                }
                result["count"] = total;
                result["entities"] = found;
                return result;
            });
        }

        /// <summary>
        /// Whether a parameter holds the value a client wrote: the JSON read as the parameter's own kind of data (a
        /// transform given as a position keeps the parameter's rotation), then compared with a small tolerance.
        /// </summary>
        private static bool SameValue(ParameterData actual, JToken wanted, string name, Commands commands)
        {
            ParameterData target;
            try
            {
                target = McpValues.FromJson(wanted, actual, name, commands);
            }
            catch (Exception)
            {
                //Not something the parameter could hold as that JSON: compare as text
                JToken json = McpValues.ToJson(actual, commands);
                string text = json.Type == JTokenType.String ? (string)json : json.ToString(Newtonsoft.Json.Formatting.None);
                return string.Equals(text, McpValues.ReadString(wanted), StringComparison.OrdinalIgnoreCase);
            }
            return SameData(actual, target);
        }

        private static bool Close(float a, float b) => Math.Abs(a - b) <= 1e-3f * Math.Max(1f, Math.Abs(a));
        private static bool Close(Vector3 a, Vector3 b) => Close(a.X, b.X) && Close(a.Y, b.Y) && Close(a.Z, b.Z);

        private static bool SameData(ParameterData a, ParameterData b)
        {
            if (a is cBool bool1) return b is cBool bool2 && bool1.value == bool2.value;
            if (a is cInteger int1) return b is cInteger int2 && int1.value == int2.value;
            if (a is cFloat float1) return b is cFloat float2 && Close(float1.value, float2.value);
            if (a is cEnumString enumString1) return b is cEnumString enumString2 && string.Equals(enumString1.value, enumString2.value, StringComparison.OrdinalIgnoreCase);
            if (a is cString string1) return b is cString string2 && string.Equals(string1.value, string2.value, StringComparison.OrdinalIgnoreCase);
            if (a is cVector3 vector1) return b is cVector3 vector2 && Close(vector1.value, vector2.value);
            if (a is cTransform transform1) return b is cTransform transform2 && Close(transform1.position, transform2.position) && Close(transform1.rotation, transform2.rotation);
            if (a is cEnum enum1) return b is cEnum enum2 && enum1.enumIndex == enum2.enumIndex;
            if (a is cResource resource1) return b is cResource resource2 && resource1.shortGUID == resource2.shortGUID;
            return false;
        }
        #endregion

        #region References
        private static object FindReferences(McpCall call)
        {
            int limit = Math.Max(1, call.Int("limit", 200));
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                HashSet<Composite> scope = call.Has("scope") ? ReachableFrom(commands, McpScript.FindComposite(commands, call.Str("scope"))) : null;
                if (!call.Has("entity"))
                {
                    List<JObject> instances = new List<JObject>();
                    foreach (Composite other in commands.Entries)
                    {
                        if (other == null || (scope != null && !scope.Contains(other))) continue;
                        foreach (FunctionEntity instance in other.functions)
                            if (instance.function == composite.shortGUID)
                                instances.Add(new JObject() { ["composite"] = other.name, ["id"] = McpScript.Id(instance.shortGUID), ["name"] = McpScript.EntityName(commands, other, instance) });
                    }
                    return new JObject()
                    {
                        ["composite"] = composite.name,
                        ["instance_count"] = instances.Count,
                        ["instances"] = new JArray(instances.Take(limit)),
                        ["hint"] = instances.Count == 0 ? null : "get_placements gives each placement's path from the root and its position.",
                    };
                }

                CompileIfShown(composite);
                Entity entity = McpScript.FindEntity(commands, composite, call.Str("entity"));
                JObject described = McpScript.Describe(commands, composite, entity, parameters: false);
                bool PointsAt(EntityPath path, Composite from) => path?.path != null && commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(path, from)) == (composite, entity);

                JArray pointers = new JArray();
                int total = 0;
                void Add(JObject pointer)
                {
                    total++;
                    if (pointers.Count < limit) pointers.Add(pointer);
                }
                JObject Pointer(Composite other, Entity by, string kind) => new JObject() { ["composite"] = other.name, ["id"] = McpScript.Id(by.shortGUID), ["kind"] = kind, ["name"] = McpScript.EntityName(commands, other, by) };

                foreach (Composite other in commands.Entries)
                {
                    if (other == null || (scope != null && !scope.Contains(other))) continue;
                    foreach (AliasEntity alias in other.aliases)
                    {
                        if (commands.Utils.GetResolvedTarget(commands.Utils.ResolveAlias(alias, other)) != (composite, entity)) continue;
                        JObject pointer = Pointer(other, alias, "alias");
                        List<string> overrides = alias.parameters.Where(o => o.name != ShortGuids.name).Select(o => McpScript.ParamName(o.name)).ToList();
                        if (overrides.Count != 0) pointer["overrides"] = new JArray(overrides);
                        Add(pointer);
                    }
                    foreach (ProxyEntity proxy in other.proxies)
                    {
                        if (commands.Utils.GetResolvedTarget(commands.Utils.ResolveProxy(proxy)) == (composite, entity))
                            Add(Pointer(other, proxy, "proxy"));
                        //A proxy of a TriggerSequence carries a sequence of its own
                        int carried = proxy.sequence?.Count(o => PointsAt(o?.connectedEntity, other)) ?? 0;
                        if (carried != 0)
                        {
                            JObject pointer = Pointer(other, proxy, "proxy_trigger_sequence");
                            pointer["entries"] = carried;
                            Add(pointer);
                        }
                    }
                    foreach (TriggerSequence sequence in other.functions.OfType<TriggerSequence>())
                    {
                        int entries = sequence.sequence.Count(o => PointsAt(o?.connectedEntity, other));
                        if (entries == 0) continue;
                        JObject pointer = Pointer(other, sequence, "trigger_sequence");
                        pointer["entries"] = entries;
                        Add(pointer);
                    }
                    foreach (CAGEAnimation animation in other.functions.OfType<CAGEAnimation>())
                    {
                        List<CAGEAnimation.Connection> bindings = animation.connections.Where(o => PointsAt(o?.connectedEntity, other)).ToList();
                        if (bindings.Count == 0) continue;
                        JObject pointer = Pointer(other, animation, "animation");
                        pointer["parameters"] = new JArray(bindings.Select(o => McpScript.ParamName(o.target_param)).Distinct());
                        Add(pointer);
                    }
                }
                described["pointed_at_by"] = pointers;
                described["pointer_count"] = total;
                if (total > pointers.Count)
                    described["pointers_note"] = "Showing " + pointers.Count + " of " + total + ": raise 'limit' or narrow 'scope'.";

                //The script pages that draw it (the Flowgraphs tab of the References window)
                JArray pages = new JArray();
                foreach (var page in FlowgraphLayoutManager.GetLayouts(composite))
                {
                    int nodes = page.Nodes.Count(o => o.EntityGUID == entity.shortGUID);
                    if (nodes != 0) pages.Add(new JObject() { ["page"] = page.Name, ["nodes"] = nodes });
                }
                described["pages"] = pages;
                return described;
            });
        }
        #endregion

        /// <summary>The composite on screen holds its links in its live pages until they are compiled: compile them before reading.</summary>
        public static void CompileIfShown(Composite composite)
        {
            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            if (display != null && !display.IsDisposed && display.Populated && display.Composite == composite && !Undo.UndoStack.Current.Blocked)
                display.SaveAllFlowgraphs();
        }

        /// <summary>Where a composite is placed: each instance of it, in each composite.</summary>
        public static IEnumerable<JObject> PlacedIn(Commands commands, Composite composite)
        {
            foreach (Composite other in commands.Entries)
            {
                if (other == null) continue;
                foreach (FunctionEntity instance in other.functions)
                {
                    if (instance.function != composite.shortGUID) continue;
                    yield return new JObject() { ["composite"] = other.name, ["id"] = McpScript.Id(instance.shortGUID), ["name"] = McpScript.EntityName(commands, other, instance) };
                }
            }
        }

        /// <summary>Every composite the level's entry points reach through instances.</summary>
        public static HashSet<Composite> Reachable(Commands commands)
        {
            HashSet<Composite> seen = new HashSet<Composite>();
            foreach (Composite entry in commands.EntryPoints.Where(o => o != null))
                seen.UnionWith(ReachableFrom(commands, entry));
            return seen;
        }

        /// <summary>A composite and every composite it reaches through instances.</summary>
        public static HashSet<Composite> ReachableFrom(Commands commands, Composite start)
        {
            HashSet<Composite> seen = new HashSet<Composite>();
            Stack<Composite> todo = new Stack<Composite>();
            todo.Push(start);
            while (todo.Count != 0)
            {
                Composite composite = todo.Pop();
                if (!seen.Add(composite)) continue;
                foreach (FunctionEntity instance in composite.functions)
                {
                    if (instance.function.IsFunctionType) continue;
                    Composite child = commands.GetComposite(instance.function);
                    if (child != null && !seen.Contains(child)) todo.Push(child);
                }
            }
            return seen;
        }
    }
}
