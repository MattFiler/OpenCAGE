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
        private static readonly string[] _kinds = { "function", "instance", "variable", "alias", "proxy" };
        //Types the Save & Build's instancing bakes into the level's movers and resources: edits reach the game only after a build
        private static readonly HashSet<FunctionType> _built = new HashSet<FunctionType>()
        {
            FunctionType.ModelReference, FunctionType.EnvironmentModelReference, FunctionType.LightReference, FunctionType.ParticleEmitterReference,
            FunctionType.FogBox, FunctionType.FogSphere, FunctionType.FogPlane, FunctionType.SimpleWater, FunctionType.SimpleRefraction,
        };
        //Parameters that start an entity off by itself when the level (or its composite) starts
        private static readonly string[] _startParameters = { "start_on_reset", "enable_on_reset", "show_on_reset", "trigger_on_reset", "activate_on_reset", "spawn_on_reset", "start_on_spawn" };

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "find_composites",
                Title = "Find composites",
                Description = "Search the open level's composites by path. Paths are folders separated by backslashes, e.g. 'AYZ\\Science\\Corridors\\...' for level geometry, 'Archetypes\\...' for reusable gameplay (NPCs, doors, pickups, mission scripts), 'DisplayModel:...' for character models. " +
                    "match 'all' (default) needs every word in the path; 'any' takes paths with any of them, most words first; 'fuzzy' ranks paths by how well they fit (misspellings, '_' and spaces alike). When 'all' finds nothing the nearest paths are suggested. " +
                    "An empty folder of the composite browser is listed with kind 'folder' (its path ends in a backslash): it holds no script, and only that path, separator included, names it. " +
                    "A room is often an instance or zone rather than a composite name: find_places searches all of those. Use search_level to look in a level that is not open.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Words the path must contain (any case). Leave out to list everything."),
                    McpSchema.String("match", "'all' (default): every word; 'any': at least one word, most matches first; 'fuzzy': ranked by fit, typos tolerated.", options: new[] { "all", "any", "fuzzy" }),
                    McpSchema.Boolean("placed_only", "Only composites that are placed somewhere in the level (reachable from its root)."),
                    McpSchema.Limit(200, "composites"),
                    McpSchema.Offset("composites")),
                ReadOnly = true,
                Idempotent = true,
                Run = FindComposites,
            };

            yield return new McpTool()
            {
                Name = "get_composite",
                Title = "Get composite",
                Description = "A composite's contents: its entities (id, name, kind, type, and position/rotation relative to the composite), its variables (the pins its instances expose, each with what it links to inside), its links (links: true, as 'A.p -> B.q'), " +
                    "whether its script is shown as flowgraph pages, and where it is placed. parameters: true adds each entity's own parameter values (and which a link feeds), and 'starts' lists what runs by itself when the level starts (start_on_reset, enable_on_reset and the like) - with the pins, that is what the composite does. " +
                    "'limit'/'offset' page the entities; links are capped by 'limit' too. trace_links follows a chain across composites; get_usage_patterns shows how the level's scripts use the composite.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", McpSchema.CompositeRequired, required: true),
                    McpSchema.String("filter", "Only entities whose name or type contains this."),
                    McpSchema.Boolean("links", "Also list the composite's links."),
                    McpSchema.Boolean("parameters", "Also give each entity's own parameter values (as set on it; type defaults left out) and the parameters a link feeds."),
                    McpSchema.Limit(300, "entities (and links)"),
                    McpSchema.Offset("entities")),
                ReadOnly = true,
                Idempotent = true,
                Run = GetComposite,
            };

            yield return new McpTool()
            {
                Name = "describe_entity",
                Title = "Describe entity",
                Description = "Everything about one entity: what it is, its parameter values, its links in and out (links_out {param, to, to_name, to_param}; links_in {from, from_name, from_param, to_param}, 'param' being its own pin in both), every other pin it has (with kind, type, default and 'link': which way a link feeding it goes), the relay each method fires (method_relays), " +
                    "event pins (trigger methods, animation events), its resource types and the CAGEAnimations that drive it. 'parameters_fed_by' names the parameters a link feeds: the game reads those from the other end of the link, so a value set on them is ignored (set the source, or the composite pin on each instance). " +
                    "An alias or proxy also gives its 'target' {composite, id, name, type, values, overridden_here} (its 'other_parameters' give the type's defaults as 'type_default'). 'derived_parameters' are worked out on save/build and not set by hand. " +
                    "'placements' is how often its composite is placed: an edit to the entity changes every one (set_parameters 'path' changes one). " +
                    "Long lists (sequence entries, links) are paged by limit/offset with their totals. get_effective_parameters gives the values one placement really ends up with. " +
                    "'level' reads another level from disk, read-only (loaded once and remembered, as describe_level_composite does).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite it is in (path or id).", required: true),
                    McpSchema.String("entity", "Its id or name ('Name (id)' as results show it works too).", required: true),
                    McpSchema.String("level", "Read it in this level instead of the open one (as list_levels names it), from its files on disk."),
                    McpSchema.Limit(200, "sequence entries and links of each list"),
                    McpSchema.Offset("sequence entries and links")),
                ReadOnly = true,
                Idempotent = true,
                Run = DescribeEntity,
            };

            yield return new McpTool()
            {
                Name = "get_effective_parameters",
                Title = "Get effective parameters",
                Description = "The parameter values an entity really has in a placement, as the game works them out: a link out of a value parameter wins (the game reads the other end), then the outermost alias overriding it, then its own value, then the default; " +
                    "a composite pin is followed up to what the placing instance (or an alias of it) passes in, and a link to a Variable* function (VariableEnum, VariableFloat...) gives that function's initial value. " +
                    "Each value says where it came from ('source' own | alias | pin | link | variable | default, 'from', and 'route' when it took several steps). " +
                    "Give 'path' (one placement from the root, as get_placements or find_entities give it), or composite + entity: when that composite is placed more than once, the values that are the same everywhere are in 'common' and each placement lists only what differs. " +
                    "An alias or proxy is evaluated as what it points at. get_instance_overrides lists the aliases that override things inside an instance.",
                InputSchema = McpSchema.Object(
                    McpSchema.Any("path", "One placement: entity ids/names from the root composite through instances to the entity (an array, a string split on '/', or a result's {ids} object)."),
                    McpSchema.String("composite", "Instead of path: the composite the entity is in (path or id)."),
                    McpSchema.String("entity", "With composite: the entity (id or name)."),
                    McpSchema.Integer("placement", "With composite + entity: only this placement (0-based, in get_placements order)."),
                    McpSchema.Strings("parameters", "Only these parameters (default: every parameter set, overridden or fed by a link)."),
                    McpSchema.Boolean("all", "Also every other value parameter of its type, with its default."),
                    McpSchema.Limit(20, "placements", 200),
                    McpSchema.Offset("placements")),
                ReadOnly = true,
                Idempotent = true,
                Run = GetEffectiveParameters,
            };

            yield return new McpTool()
            {
                Name = "get_instance_overrides",
                Title = "Get instance overrides",
                Description = "The overrides a composite instance carries: every alias - in the composite holding the instance or in any composite above it - whose path runs through the instance to something inside it, " +
                    "with what it reaches (path inside the instance, entity, type), the parameters it overrides with their values, and its links. 'scope' says whether it applies to every placement of the instance or only to the ones under the alias's composite. " +
                    "Give 'path' (one placement from the root: only the aliases that apply to it) or composite + instance (every alias reaching that instance anywhere). clear_instance_overrides takes them away.",
                InputSchema = McpSchema.Object(
                    McpSchema.Any("path", "One placement of the instance: entity ids/names from the root composite through instances (an array, a string split on '/', or a result's {ids} object)."),
                    McpSchema.String("composite", "Instead of path: the composite holding the instance (path or id)."),
                    McpSchema.String("instance", "With composite: the instance entity (id or name)."),
                    McpSchema.Limit(100, "overrides"),
                    McpSchema.Offset("overrides")),
                ReadOnly = true,
                Idempotent = true,
                Run = GetInstanceOverrides,
            };

            yield return new McpTool()
            {
                Name = "find_entities",
                Title = "Find entities",
                Description = "Search the level's script for entities by name, type, kind, parameter value, or place. 'type' is a function type ('Character'); text that is not a whole type name also matches every type containing it ('Light' finds LightReference, LightingMaster...: 'matched_function_types' lists them) " +
                    "and instances of composites whose path contains it. Without a place it searches composites as they are ('composite' for one). 'within' searches a composite and everything placed under it with instance paths and positions relative to it; " +
                    "a place - 'region' (a zone, a composite's placements, a path, a box or sphere, a name), 'near' + 'radius' (a point, 'viewport' for the viewport camera, 'viewport_centre' for the surface at the centre of the 3D view), box_min + box_max, or 'inside' a volume entity's box - searches the placed level in world space. " +
                    "A 'within' that names no composite but a room (an instance or zone, as find_places finds it) searches that place the same way. " +
                    "With 'near' each hit has its 'distance' (m) and the nearest come first. test 'bounds' counts a hit whose content reaches the place, not only its position. fuzzy: true matches 'name' by fit (misspellings, word order), best first with a 'score'. " +
                    "Functions without a position sit at their composite's origin ('position_from' says so); deleted, template and shared-repeat placements are left out unless include 'all'. " + McpSchema.PositionText + ".",
                InputSchema = McpSchema.Object(
                    McpSchema.String("name", "Text the entity's name contains (with fuzzy: names that fit it, best first)."),
                    McpSchema.Boolean("fuzzy", "Match 'name' by how well it fits rather than as contained text: misspellings, word order, '_' and spaces tolerated; each hit gets a 'score' (0-1) and the best come first."),
                    McpSchema.String("type", "A function type (e.g. 'Character', or part of one, e.g. 'Light'), or text the path of the composite an instance places contains. All are matched."),
                    McpSchema.Nested("kinds", "Only these kinds of entity (applied before the limit).", new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string", ["enum"] = new JArray(_kinds) } }),
                    McpSchema.String("parameter", "It has this parameter."),
                    McpSchema.Any("value", "...with this value (numbers, vectors and transforms compared within a small tolerance; a transform may give only position)."),
                    McpSchema.String("composite", "Only look in this composite (path or id), not under it."),
                    McpSchema.String("within", "Look in this composite and everything placed under it ('root' for the whole placed level), with instance paths and positions relative to it. A room's name that is no composite's (an instance, a zone) searches that place in world space."),
                    McpSchema.Nested("region", "A place to search (world space). " + McpRegion.Help, McpRegion.Schema),
                    McpSchema.Nested("near", "Only entities within 'radius' metres of this point ([x, y, z], world space, or relative to 'within'), 'viewport' for the viewport camera's position, or 'viewport_centre' for the surface at the centre of the 3D view (what the user is looking at).",
                        new JObject() { ["anyOf"] = new JArray(new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" }, ["minItems"] = 3, ["maxItems"] = 3 }, new JObject() { ["type"] = "string", ["enum"] = new JArray("viewport", "viewport_centre") }) }),
                    McpSchema.Number("radius", "Metres, for 'near'."),
                    McpSchema.Vector("box_min", "Only entities inside the box from box_min to box_max (world space, or relative to 'within')."),
                    McpSchema.Vector("box_max", "The box's other corner."),
                    McpSchema.Nested("inside", "Only entities inside a volume entity's box (a PlayerTriggerBox or anything sized by half_dimensions: it stands on its position, +-half x/z, 0..2*half y up, in its own rotation): {\"composite\", \"entity\"} for each of its placements, or {\"path\": [...]} for one.",
                        new JObject() { ["type"] = "object", ["properties"] = new JObject() { ["composite"] = new JObject() { ["type"] = "string" }, ["entity"] = new JObject() { ["type"] = "string" }, ["path"] = new JObject() { ["description"] = "Entity ids/names from the root." } } }),
                    McpSchema.String("test", "'pivot' (default): the entity's position must be in the place; 'bounds': any of its content's box (an instance's contents, a model, a volume) touching the place counts.", options: new[] { "pivot", "bounds" }),
                    McpSchema.String("sort", "'distance' (default with near): nearest first; 'score' (default with fuzzy): best fitting name first; 'walk': the order the level is walked in.", options: new[] { "distance", "score", "walk" }),
                    McpSchema.Integer("max_depth", "With a place or 'within': at most this many instances below where the search starts (0: only entities directly in it)."),
                    McpSchema.String("include", "With a place or 'within': 'real' (default) placements the game makes; 'all' also deleted, template and shared-repeat ones, flagged.", options: new[] { "real", "all" }),
                    McpSchema.Limit(100, "entities"),
                    McpSchema.Offset("entities")),
                ReadOnly = true,
                Idempotent = true,
                Run = FindEntities,
            };

            yield return new McpTool()
            {
                Name = "find_references",
                Title = "Find references",
                Description = "What refers to an entity: links to and from it, aliases (with their 'path' and the parameters each overrides) and proxies pointing at it, TriggerSequences (and proxies' sequences) listing it, CAGEAnimations animating it, and the script pages drawing it. With no entity: every instance of the composite. 'scope' limits the search. " +
                    "Aliases sharing one path in one composite are flagged ('same_path_as'): the game applies them in no set order, so keep one. " +
                    "trace_links follows what drives it across composites; get_instance_overrides lists the aliases reaching inside an instance. 'level' reads another level from disk, read-only.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite (path or id).", required: true),
                    McpSchema.String("entity", "The entity (id or name); leave out to find instances of the composite itself."),
                    McpSchema.String("scope", "Only look in this composite and the composites placed under it (e.g. 'root' for what the level places)."),
                    McpSchema.String("level", "Look in this level instead of the open one (as list_levels names it), from its files on disk."),
                    McpSchema.Limit(200, "references or instances"),
                    McpSchema.Offset("references or instances")),
                ReadOnly = true,
                Idempotent = true,
                Run = FindReferences,
            };

            yield return new McpTool()
            {
                Name = "list_function_types",
                Title = "List function types",
                Description = "The built-in entity types (FunctionType) the engine provides, e.g. ModelReference, Character, PlayerTriggerBox, LogicGate, ThinkOnce, CMD_GoTo, each with its flowgraph category. Filter by name or category; no level needs to be open. " +
                    "describe_function_type gives one type's pins; get_usage_patterns shows how the game's own scripts wire it.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Text the type name contains (when nothing does, the nearest names are suggested)."),
                    McpSchema.String("category", "Only this flowgraph category (e.g. 'Logic', 'Character', 'Triggers').")),
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() =>
                {
                    string filter = call.Str("filter");
                    string category = call.Str("category");
                    JArray types = new JArray();
                    HashSet<string> categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (FunctionType type in Enum.GetValues(typeof(FunctionType)).Cast<FunctionType>().OrderBy(o => o.ToString()))
                    {
                        string name = type.ToString();
                        string cat = null;
                        try { cat = FlowgraphLayoutManager.TryGetCategoryForFunctionType(type) ?? "Misc"; } catch { }
                        if (cat != null) categories.Add(cat);
                        if (filter != null && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (category != null && !string.Equals(cat, category, StringComparison.OrdinalIgnoreCase)) continue;
                        types.Add(cat == null ? (JToken)name : new JObject() { ["type"] = name, ["category"] = cat });
                    }
                    JObject result = new JObject() { ["count"] = types.Count, ["types"] = types };
                    if (types.Count == 0 && category != null && !categories.Contains(category))
                        throw McpError.NotFound("category", category, categories, "The categories are: " + string.Join(", ", categories.OrderBy(o => o)) + ".");
                    if (types.Count == 0 && filter != null)
                        result["note"] = "No function type contains '" + filter + "'." + McpNames.DidYouMean(Enum.GetNames(typeof(FunctionType)), filter, 8) +
                            " A composite (an archetype such as a door or a light fixture) is found with find_composites.";
                    return result;
                }),
            };

            yield return new McpTool()
            {
                Name = "describe_function_type",
                Title = "Describe function type",
                Description = "A function type's pins, including those it inherits: methods (triggered by links), relays/targets (fire links out), parameters/inputs/states (values, with type, default and 'link': the way a link feeding it goes; colours are 'colour (RGB 0-255)'), outputs and references. " +
                    "It also says which way the type faces ('facing', lights and cameras: local +Z), its box convention ('box'), whether only Save & Build gets its changes to the game ('build') and what a character command needs ('character'). " +
                    "No level needs to be open. usage: true adds how the open level's scripts use it - how many there are, the parameters most often set with typical values, the commonest links in and out with an example each, and a few 'examples' to describe_entity (get_usage_patterns has the full picture, from other levels too).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("type", "The FunctionType name, e.g. 'ThinkOnce' (any case; near names are suggested).", required: true),
                    McpSchema.Boolean("usage", "Also summarise how the open level uses it (needs a level open).")),
                ReadOnly = true,
                Idempotent = true,
                Run = DescribeFunctionType,
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

        #region Composites
        private static object FindComposites(McpCall call)
        {
            string match = (call.Str("match") ?? "all").Trim().ToLowerInvariant();
            if (match != "all" && match != "any" && match != "fuzzy")
                throw McpError.Invalid("'match' is 'all' (every word), 'any' (at least one) or 'fuzzy' (ranked by fit).");
            string filter = call.Str("filter");
            return McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                Commands commands = content.Level.Commands;
                string[] words = (filter ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                HashSet<Composite> placed = call.Bool("placed_only") ? Reachable(commands) : null;
                List<Composite> pool = commands.Entries.Where(o => o != null && !string.IsNullOrEmpty(o.name) && (placed == null || placed.Contains(o))).ToList();
                bool Has(Composite o, string w) => o.name.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0 || (w.Length == 11 && McpScript.Id(o.shortGUID).Equals(w, StringComparison.OrdinalIgnoreCase));
                JObject Summary(Composite o)
                {
                    JObject summary = McpScript.CompositeSummary(commands, o);
                    summary["entities"] = o.GetEntities().Count;
                    return summary;
                }

                List<Composite> matches;
                if (words.Length == 0)
                    matches = pool.OrderBy(o => o.name, StringComparer.OrdinalIgnoreCase).ToList();
                else if (match == "all")
                    matches = pool.Where(o => words.All(w => Has(o, w))).OrderBy(o => o.name, StringComparer.OrdinalIgnoreCase).ToList();
                else if (match == "any")
                    matches = pool.Select(o => (composite: o, hits: words.Count(w => Has(o, w)))).Where(o => o.hits > 0)
                        .OrderByDescending(o => o.hits).ThenBy(o => o.composite.name, StringComparer.OrdinalIgnoreCase).Select(o => o.composite).ToList();
                else
                    matches = McpNames.Rank(pool, o => o.name, filter, int.MaxValue, 0.3);

                JObject result = new JObject();
                if (matches.Count == 0 && words.Length != 0)
                {
                    List<Composite> near = McpNames.Rank(pool, o => o.name, filter, 8, 0.3);
                    result["note"] = "No composite path " + (match == "fuzzy" ? "fits" : match == "any" ? "contains any of" : "contains all of") + " '" + filter + "'." +
                        (near.Count != 0 ? " Nearest below." : "") + " find_places also searches instances and zones by name; find_entities name:'...' searches entity names.";
                    if (near.Count != 0) result["nearest"] = new JArray(near.Select(Summary));
                }
                McpPaging.Page(call, matches, result, "composites", Summary, 200);
                return result;
            });
        }

        private static object GetComposite(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireLevel(forEditing: false).Level.Commands;
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                CompileIfShown(composite);
                string filter = call.Str("filter");
                bool withParameters = call.Bool("parameters");
                int limit = McpPaging.Limit(call, 300);

                JObject result = McpScript.CompositeSummary(commands, composite);
                result["script_view"] = FlowgraphLayoutManager.HasCompatibilityInfo(composite) ? (FlowgraphLayoutManager.IsCompatible(composite) ? "pages" : "links") : (FlowgraphLayoutManager.HasLayout(composite) ? "pages (not yet checked)" : "none yet");
                result["pages"] = new JArray(FlowgraphLayoutManager.GetLayouts(composite).Select(o => o.Name));
                result["space"] = "composite";

                List<Entity> entities = composite.GetEntities().Where(o => !(o is VariableEntity) && (filter == null
                    || McpScript.EntityName(commands, composite, o).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || McpScript.TypeName(commands, composite, o).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                result["entity_count"] = composite.GetEntities().Count;
                McpPaging.Page(call, entities, result, "entities", o =>
                {
                    JObject brief = McpScript.Brief(commands, composite, o);
                    if (!withParameters) return brief;
                    JObject values = new JObject();
                    foreach (Parameter parameter in o.parameters)
                    {
                        if (parameter.name == ShortGuids.name || parameter.name == ShortGuids.position || EntityParameterVisibility.IsHiddenFromEditor(o, parameter.name)) continue;
                        values[McpScript.ParamName(parameter.name)] = McpValues.ToJson(parameter.content, commands);
                    }
                    if (values.Count != 0) brief["parameters"] = values;
                    JObject fed = McpScript.FedBy(commands, composite, o);
                    if (fed != null) brief["fed_by"] = fed;
                    return brief;
                }, 300);
                if (entities.Count > limit && filter == null)
                    result["entities_note"] = "Narrow with 'filter', or page with offset/next_offset.";

                //The pins its instances expose, and what each one reaches inside
                result["variables"] = new JArray(composite.variables.Take(limit).Select(o =>
                {
                    string pin = McpScript.EntityName(commands, composite, o);
                    JObject variable = new JObject()
                    {
                        ["id"] = McpScript.Id(o.shortGUID),
                        ["name"] = pin,
                        ["pin_type"] = McpScript.PinTypeWord(McpScript.TypeName(commands, composite, o)),
                    };
                    List<string> inside = new List<string>();
                    foreach (EntityConnector link in o.childLinks)
                    {
                        Entity target = composite.GetEntityByID(link.linkedEntityID);
                        inside.Add(pin + " -> " + (target == null ? McpScript.Id(link.linkedEntityID) : McpScript.EntityName(commands, composite, target)) + "." + McpScript.ParamName(link.linkedParamID));
                    }
                    foreach (Entity source in composite.GetEntities())
                        foreach (EntityConnector link in source.childLinks)
                            if (link.linkedEntityID == o.shortGUID)
                                inside.Add(McpScript.EntityName(commands, composite, source) + "." + McpScript.ParamName(link.thisParamID) + " -> " + pin);
                    if (inside.Count != 0) variable["links"] = new JArray(inside.Take(20));
                    return variable;
                }));

                //What starts by itself: the entry points of what the composite does
                if (withParameters)
                {
                    JArray starts = new JArray();
                    foreach (Entity entity in composite.GetEntities())
                    {
                        if (!(entity is FunctionEntity)) continue;
                        List<string> on = new List<string>();
                        foreach (string name in _startParameters)
                        {
                            ShortGuid id = ShortGuidUtils.Generate(name);
                            bool? set = (entity.GetParameter(id)?.content as cBool)?.value;
                            if (set == null && (name == "start_on_reset" || name == "trigger_on_reset"))
                            {
                                try { set = (commands.Utils.CreateDefaultParameterData(entity, composite, id) as cBool)?.value; } catch { }
                            }
                            if (set == true) on.Add(name);
                        }
                        if (on.Count != 0 && starts.Count < 60)
                            starts.Add(McpScript.EntityName(commands, composite, entity) + " (" + McpScript.TypeName(commands, composite, entity) + "): " + string.Join(", ", on));
                    }
                    if (starts.Count != 0) result["starts"] = starts;
                }

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
            });
        }
        #endregion

        /// <summary>
        /// The level a reading tool's 'level' argument names, loaded read-only from disk (McpLevelCache: kept for the next call),
        /// or null when there is no 'level' and the open level is meant. Not on the UI thread.
        /// </summary>
        private static Level OtherLevel(McpCall call)
        {
            if (!call.Has("level")) return null;
            string level = McpLevels.Resolve(call.Str("level"), allowFrontend: true, call: call);
            return McpLevelCache.Get(call, level);
        }

        private static object DescribeEntity(McpCall call)
        {
            Level other = OtherLevel(call);
            return McpEditor.UI(() =>
            {
                Commands commands = other?.Commands ?? McpEditor.RequireCommands(forEditing: false);
                Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                if (other == null) CompileIfShown(composite);
                Entity entity = McpScript.FindEntity(commands, composite, call.Str("entity", required: true));
                JObject result = McpScript.Describe(commands, composite, entity, defaults: true, limit: McpPaging.Limit(call, 200), offset: McpPaging.Offset(call), paged: true);
                result["composite"] = composite.name;
                if (other != null) result["level"] = other.Name;
                if (result["parameters_fed_by"] != null)
                    call.Note("Parameters in 'parameters_fed_by' take their value from a link: set_parameters on them changes nothing in game.");

                //A composite placed many times shares this entity between its placements (0: the level does not place it at all)
                if (!commands.EntryPoints.Contains(composite))
                {
                    int placements = -1;
                    try { placements = McpAssets.PlacementCount(commands, composite); } catch (Exception) { }
                    if (placements >= 0 && placements != 1)
                        result["placements"] = placements;
                }

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
            });
        }

        #region Effective values and overrides
        /// <summary>
        /// A placement's chain (starting in <paramref name="start"/>), with an alias or proxy at its end swapped for what it
        /// stands for there. <paramref name="from"/> is the composite the returned chain starts in: <paramref name="start"/>,
        /// or where a proxy's path (or an alias path read from the root) starts.
        /// </summary>
        private static List<Entity> Resolved(Commands commands, Composite start, List<Entity> chain, out string note, out Composite from)
        {
            note = null;
            from = start;
            Entity last = chain.Count == 0 ? null : chain[chain.Count - 1];
            if (last is AliasEntity alias)
            {
                Composite holder = McpScript.CompositesAlong(commands, start, chain)[chain.Count - 1];
                List<Tuple<Composite, Entity>> path = commands.Utils.ResolveAlias(alias, holder);
                if (path == null || path.Count == 0 || path.Any(o => o.Item2 == null))
                    throw new McpError(McpScript.EntityName(commands, holder, alias) + " is an alias that points at nothing.");
                note = "The alias " + McpScript.EntityName(commands, holder, alias) + " is evaluated as what it points at.";
                //A path that only reads from the root starts there, not in the alias's composite
                if (path[0].Item1 != holder)
                {
                    from = path[0].Item1;
                    return path.Select(o => o.Item2).ToList();
                }
                return chain.Take(chain.Count - 1).Concat(path.Select(o => o.Item2)).ToList();
            }
            if (last is ProxyEntity proxy)
            {
                List<Tuple<Composite, Entity>> path = commands.Utils.ResolveProxy(proxy);
                if (path == null || path.Count == 0 || path.Any(o => o.Item2 == null))
                    throw new McpError("That proxy points at nothing (list_dead_proxies, retarget_proxy).");
                note = "The proxy is evaluated as what it points at, through its path from the root.";
                from = path[0].Item1;
                return path.Select(o => o.Item2).ToList();
            }
            return chain;
        }

        private static object GetEffectiveParameters(McpCall call)
        {
            int limit = McpPaging.Limit(call, 20, 200);
            int offset = McpPaging.Offset(call);
            bool all = call.Bool("all");
            List<string> only = call.Has("parameters") ? call.StrList("parameters") : null;
            using (McpEditorTools.Heartbeat(call, "Resolving parameters"))
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                Composite root = commands.EntryPoints[0];
                List<List<Entity>> chains = new List<List<Entity>>();
                JObject result = new JObject();
                Composite unplaced = null;
                Entity subject = null;

                if (call.Has("path"))
                {
                    if (call.Has("composite") || call.Has("entity") || call.Has("placement"))
                        throw McpError.Invalid("Give 'path' (one placement from the root), or composite + entity (+ placement), not both.");
                    chains.Add(McpScript.ChainFrom(commands, root, McpScript.PathSteps(call.Token("path"))));
                }
                else
                {
                    if (!call.Has("composite") || !call.Has("entity"))
                        throw McpError.Invalid("Give 'path' (one placement from the root, as get_placements gives it), or composite + entity.");
                    Composite composite = McpScript.FindComposite(commands, call.Str("composite"));
                    CompileIfShown(composite);
                    subject = McpScript.FindEntity(commands, composite, call.Str("entity"));
                    int? placement = call.Has("placement") ? call.Int("placement") : (int?)null;
                    McpPlacements walker = new McpPlacements(commands) { PlaceUnpositioned = true };
                    List<McpPlacements.Placement> found = new List<McpPlacements.Placement>();
                    int total = composite == root ? 1 : walker.PlacementsOf(root, composite, subject, found, Math.Max(offset + limit, (placement ?? 0) + 1), call.Cancel, realOnly: true);
                    if (composite == root)
                        found.Add(new McpPlacements.Placement() { Composite = root, Chain = new List<Entity>() { subject } });
                    if (total == 0)
                    {
                        //Not placed in the level: the composite's own view of it, with nothing passed in from outside
                        unplaced = composite;
                        chains.Add(new List<Entity>() { subject });
                        result["note"] = composite.name + " is not placed in the level, so these are the values in the composite alone: its pins get nothing from an instance.";
                    }
                    else if (placement != null)
                    {
                        if (placement.Value < 0 || placement.Value >= Math.Min(total, found.Count))
                            throw McpError.Invalid("'placement' is 0-" + (Math.Min(total, found.Count) - 1) + ": " + McpScript.EntityName(commands, composite, subject) + " is placed " + total + " time" + (total == 1 ? "" : "s") + ".");
                        chains.Add(found[placement.Value].Chain);
                    }
                    else
                    {
                        List<McpPlacements.Placement> page = found.Skip(offset).Take(limit).ToList();
                        if (page.Count == 0)
                            throw McpError.Invalid("'offset' " + offset + " is past the end: " + McpScript.EntityName(commands, composite, subject) + " is placed " + total + " time" + (total == 1 ? "" : "s") + ".");
                        chains.AddRange(page.Select(o => o.Chain));
                        McpPaging.Describe(result, total, offset, page.Count);
                    }
                }

                List<(List<Entity> chain, List<Composite> comps, JObject values)> evaluated = new List<(List<Entity>, List<Composite>, JObject)>();
                foreach (List<Entity> placed in chains)
                {
                    List<Entity> chain = Resolved(commands, unplaced ?? root, placed, out string note, out Composite start);
                    if (note != null) call.Note(note);
                    //An unplaced composite's proxy is still evaluated along its path from the root
                    if (unplaced != null && start != unplaced)
                        result["note"] = unplaced.name + " is not placed in the level, but " + McpScript.EntityName(commands, unplaced, subject) + " reaches its target through a path from the root: these are the values along that path.";
                    List<Composite> comps = McpScript.CompositesAlong(commands, start, chain);
                    int k = chain.Count - 1;
                    List<ShortGuid> names = only != null ? only.Select(McpScript.ParamId).ToList() : McpEffective.Interesting(commands, comps, chain, k, all);
                    JObject values = new JObject();
                    foreach (ShortGuid name in names)
                        values[McpScript.ParamName(name)] = McpEffective.Json(McpEffective.Of(commands, comps, chain, k, name));
                    evaluated.Add((placed, comps, values));
                }

                List<Entity> first = evaluated[0].chain;
                List<Entity> firstResolved = Resolved(commands, unplaced ?? root, first, out _, out Composite firstStart);
                Composite holder = McpScript.CompositesAlong(commands, firstStart, firstResolved)[firstResolved.Count - 1];
                JObject entity = McpScript.Brief(commands, holder, firstResolved[firstResolved.Count - 1]);
                entity["composite"] = holder.name;
                result["entity"] = entity;

                if (evaluated.Count == 1)
                {
                    if (unplaced == null) result["placement"] = McpRegion.ChainJson(commands, first);
                    result["parameters"] = evaluated[0].values;
                    return result;
                }

                //Several placements: what they share once, then what each has of its own
                JObject common = new JObject();
                foreach (JProperty property in evaluated[0].values.Properties())
                    if (evaluated.All(o => o.values[property.Name] != null && JToken.DeepEquals(o.values[property.Name]["value"], property.Value["value"]) && (string)o.values[property.Name]["from"] == (string)property.Value["from"]))
                        common[property.Name] = property.Value;
                result["common"] = common;
                result["placements"] = new JArray(evaluated.Select(o =>
                {
                    JObject placement = McpRegion.ChainJson(commands, o.chain);
                    JObject differs = new JObject();
                    foreach (JProperty property in o.values.Properties())
                        if (common[property.Name] == null) differs[property.Name] = property.Value;
                    placement["differs"] = differs;
                    return placement;
                }));
                if (evaluated.All(o => !o.values.Properties().Any(p => common[p.Name] == null)))
                    call.Note("Every placement has the same values.");
                return result;
            });
        }

        private static object GetInstanceOverrides(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                Composite root = commands.EntryPoints[0];
                List<Entity> chain = null;
                List<Composite> comps = null;
                Composite holder;
                Entity instance;
                if (call.Has("path"))
                {
                    if (call.Has("composite") || call.Has("instance"))
                        throw McpError.Invalid("Give 'path' (one placement from the root), or composite + instance, not both.");
                    chain = McpScript.ChainFrom(commands, root, McpScript.PathSteps(call.Token("path")));
                    comps = McpScript.CompositesAlong(commands, root, chain);
                    holder = comps[chain.Count - 1];
                    instance = chain[chain.Count - 1];
                }
                else
                {
                    if (!call.Has("composite") || !call.Has("instance"))
                        throw McpError.Invalid("Give 'path' (one placement of the instance from the root), or composite + instance.");
                    holder = McpScript.FindComposite(commands, call.Str("composite"));
                    instance = McpScript.FindEntity(commands, holder, call.Str("instance"));
                }
                Composite inner = McpScript.InstancedComposite(commands, instance);
                if (inner == null)
                    throw McpError.Invalid(McpScript.EntityName(commands, holder, instance) + " is " + McpScript.WithArticle(McpScript.TypeName(commands, holder, instance)) + ", not a composite instance: only an instance carries overrides of what is inside it (describe_entity shows its own parameters).");

                List<OverrideFound> found = FindOverrides(commands, root, chain, comps, holder, instance, call.Cancel);
                JObject result = new JObject()
                {
                    ["instance"] = new JObject() { ["composite"] = holder.name, ["id"] = McpScript.Id(instance.shortGUID), ["name"] = McpScript.EntityName(commands, holder, instance), ["places"] = inner.name },
                };
                if (chain != null) result["placement"] = McpRegion.ChainJson(commands, chain);
                McpPaging.Page(call, found, result, "overrides", o => o.Json, 100);
                if (found.Count == 0)
                    result["note"] = "No alias overrides anything inside this instance" + (chain != null ? " in this placement." : ".");
                return result;
            });
        }

        /// <summary>An alias that reaches inside an instance, described for results.</summary>
        internal sealed class OverrideFound
        {
            public Composite Owner;
            public AliasEntity Alias;
            public JObject Json;
        }

        /// <summary>
        /// The aliases overriding something inside <paramref name="instance"/> (in <paramref name="holder"/>): with a placement
        /// chain, those in the composites along it whose path runs through the instance; without one, every alias in the level
        /// whose path passes through it. UI thread.
        /// </summary>
        internal static List<OverrideFound> FindOverrides(Commands commands, Composite root, List<Entity> chain, List<Composite> comps, Composite holder, Entity instance, System.Threading.CancellationToken cancel)
        {
            List<OverrideFound> found = new List<OverrideFound>();
            void Add(Composite owner, AliasEntity alias, int through, string scope)
            {
                List<Tuple<Composite, Entity>> resolved = commands.Utils.ResolveAlias(alias, owner);
                Tuple<Composite, Entity> end = resolved != null && resolved.Count != 0 ? resolved[resolved.Count - 1] : null;
                JObject json = new JObject()
                {
                    ["alias"] = new JObject() { ["composite"] = owner.name, ["id"] = McpScript.Id(alias.shortGUID), ["name"] = McpScript.EntityName(commands, owner, alias) },
                };
                JArray inside = new JArray();
                if (resolved != null)
                    for (int i = through + 1; i < resolved.Count; i++)
                        inside.Add(resolved[i].Item2 == null ? "?" : McpScript.EntityName(commands, resolved[i].Item1, resolved[i].Item2) + " (" + McpScript.Id(resolved[i].Item2.shortGUID) + ")");
                JObject reaches = new JObject() { ["path_inside"] = inside };
                if (end?.Item2 != null)
                {
                    reaches["composite"] = end.Item1.name;
                    reaches["id"] = McpScript.Id(end.Item2.shortGUID);
                    reaches["type"] = McpScript.TypeName(commands, end.Item1, end.Item2);
                }
                else
                    reaches["note"] = "the path does not resolve: it points at nothing";
                json["reaches"] = reaches;
                JObject overrides = new JObject();
                foreach (Parameter parameter in alias.parameters)
                    if (parameter.name != ShortGuids.name)
                        overrides[McpScript.ParamName(parameter.name)] = McpValues.ToJson(parameter.content, commands);
                json["overrides"] = overrides;
                int links = alias.childLinks.Count + owner.GetEntities().Sum(o => o.childLinks.Count(l => l.linkedEntityID == alias.shortGUID));
                if (links != 0) json["links"] = links;
                json["scope"] = scope;
                found.Add(new OverrideFound() { Owner = owner, Alias = alias, Json = json });
            }

            if (chain != null)
            {
                int n = chain.Count - 1;
                for (int j = 0; j <= n; j++)
                {
                    int matched = n - j + 1;
                    foreach (AliasEntity alias in comps[j].aliases)
                    {
                        ShortGuid[] path = alias.alias?.path;
                        if (path == null) continue;
                        int length = path.Length > 0 && path[path.Length - 1] == ShortGuid.Invalid ? path.Length - 1 : path.Length;
                        if (length <= matched) continue;
                        bool same = true;
                        for (int i = 0; i < matched && same; i++)
                            same = path[i] == chain[j + i].shortGUID;
                        if (same)
                            Add(comps[j], alias, matched - 1, j == n ? "every placement of " + holder.name : "this placement (the alias is in " + comps[j].name + ", above the instance)");
                    }
                }
                return found;
            }

            //Every alias whose path passes through the instance, wherever it is
            HashSet<Composite> reaching = new McpPlacements(commands).Reaching(holder);
            foreach (Composite owner in commands.Entries)
            {
                if (owner == null || !reaching.Contains(owner)) continue;
                cancel.ThrowIfCancellationRequested();
                foreach (AliasEntity alias in owner.aliases)
                {
                    ShortGuid[] path = alias.alias?.path;
                    if (path == null || path.Length < 2 || !path.Contains(instance.shortGUID)) continue;
                    List<Tuple<Composite, Entity>> resolved = commands.Utils.ResolveAlias(alias, owner);
                    if (resolved == null) continue;
                    for (int i = 0; i < resolved.Count - 1; i++)
                    {
                        if (resolved[i].Item1 != holder || resolved[i].Item2 != instance) continue;
                        Add(owner, alias, i, owner == holder ? "every placement of " + holder.name : "the placements under " + owner.name + " only");
                        break;
                    }
                }
            }
            return found;
        }
        #endregion

        #region Finding entities
        private static object FindEntities(McpCall call)
        {
            string name = call.Str("name");
            string type = call.Str("type")?.Trim();
            if (type != null && type.Length == 0) type = null;
            string parameter = call.Str("parameter");
            JToken value = call.Token("value");
            List<string> kinds = call.Has("kinds") ? call.StrList("kinds").Select(o => o.Trim().ToLowerInvariant()).ToList() : null;
            if (kinds != null)
                foreach (string kind in kinds)
                    if (!_kinds.Contains(kind))
                        throw McpError.Invalid("'" + kind + "' is not a kind: 'kinds' takes " + string.Join(", ", _kinds) + ".");
            string test = (call.Str("test") ?? "pivot").Trim().ToLowerInvariant();
            if (test != "pivot" && test != "bounds")
                throw McpError.Invalid("'test' is 'pivot' (its position) or 'bounds' (its content's box).");
            string include = (call.Str("include") ?? "real").Trim().ToLowerInvariant();
            if (include != "real" && include != "all")
                throw McpError.Invalid("'include' is 'real' (placements the game makes) or 'all'.");
            int? maxDepth = call.Has("max_depth") ? Math.Max(0, call.Int("max_depth")) : (int?)null;

            bool near = call.Has("near");
            bool box = call.Has("box_min") || call.Has("box_max");
            bool region = call.Has("region");
            bool inside = call.Has("inside");
            if (near && !call.Has("radius"))
                throw McpError.Invalid("'near' needs 'radius' (metres).");
            if (call.Has("radius") && !near)
                throw McpError.Invalid("'radius' goes with 'near'.");
            if (box && !(call.Has("box_min") && call.Has("box_max")))
                throw McpError.Invalid("A box needs both 'box_min' and 'box_max'.");
            if (new[] { near, box, region, inside }.Count(o => o) > 1)
                throw McpError.Invalid("Give one place: region, near + radius, box_min + box_max, or inside.");
            bool spatial = near || box || region || inside;
            if (name == null && type == null && parameter == null && kinds == null && !spatial)
                throw McpError.Invalid("Give at least one of name, type, kinds, parameter or a place (region, near + radius, box_min/box_max, inside).");
            if (value != null && parameter == null)
                throw McpError.Invalid("'value' goes with 'parameter'.");
            if (call.Has("composite") && call.Has("within"))
                throw McpError.Invalid("Give 'composite' (that composite only) or 'within' (it and everything placed under it), not both.");
            if (spatial && call.Has("composite"))
                throw McpError.Invalid("A search by place needs positions in the level: use 'within' (default: the root) rather than 'composite'.");
            if ((region || inside) && call.Has("within"))
                throw McpError.Invalid("'region' and 'inside' are places in the level (world space): leave out 'within'.");
            if (!spatial && !call.Has("within") && (maxDepth != null || call.Has("include") || call.Has("test")))
                call.Note("'max_depth', 'include' and 'test' only apply to a search of placements (a place or 'within'): ignored.");
            bool fuzzy = call.Bool("fuzzy");
            if (fuzzy && name == null)
                throw McpError.Invalid("'fuzzy' goes with 'name': the name to match by fit.");
            string sort = (call.Str("sort") ?? (near ? "distance" : fuzzy ? "score" : "walk")).Trim().ToLowerInvariant();
            if (sort != "distance" && sort != "score" && sort != "walk")
                throw McpError.Invalid("'sort' is 'distance', 'score' or 'walk'.");
            if (sort == "score" && !fuzzy)
                throw McpError.Invalid("sort 'score' ranks names by fit: it needs fuzzy: true and 'name'.");
            int limit = McpPaging.Limit(call, 100), offset = McpPaging.Offset(call);

            //'viewport': where the viewport camera is; 'viewport_centre': the surface the view is centred on (asked of the viewer, so off the UI thread)
            Vector3 centre = Vector3.Zero;
            if (near)
            {
                JToken at = call.Token("near");
                string word = at.Type == JTokenType.String ? ((string)at).Trim().ToLowerInvariant() : null;
                if (word == "viewport")
                {
                    McpViewportTools.ViewportCamera camera = McpViewportTools.QueryCamera(call);
                    if (!camera.InLevelSpace && !call.Has("within"))
                        throw new McpError("The viewport shows " + camera.Space + ", not the level, so its camera is not at a world position: open the level's root composite in the editor, or give 'within' as that composite.");
                    centre = camera.Position;
                    call.Note("'near' is the viewport camera at " + McpRegion.Format(centre) + ".");
                }
                else if (word == "viewport_centre" || word == "viewport_center")
                {
                    JObject pick = McpViewportTools.PickPoint(call, 0.5, 0.5);
                    if (pick["hit"]?.Type != JTokenType.Boolean || !(bool)pick["hit"])
                        throw new McpError(McpErrorCodes.NotFound, "Nothing is under the centre of the 3D view: point it at the area to search, or give 'near' as a position ('viewport' is the camera's own position).");
                    string shown = (string)pick["space"];
                    if (!string.Equals(shown, "world", StringComparison.OrdinalIgnoreCase) && !call.Has("within"))
                        throw new McpError(McpErrorCodes.Refused, "The viewport shows " + shown + ", not the level, so the point it is centred on is not a world position: open the level's root composite in the editor, or give 'within' as that composite.");
                    centre = McpValues.ReadVector(pick["position"], "the viewport's point", null);
                    call.Note("'near' is the surface at the centre of the 3D view, " + McpRegion.Format(centre) + ".");
                }
                else if (word != null)
                    throw McpError.Invalid("'near' is a point [x, y, z], 'viewport' (the viewport camera's position) or 'viewport_centre' (the surface at the centre of the 3D view).");
                else
                    centre = McpValues.ReadVector(at, "near", null);
            }
            float radius = near ? (float)call.Num("radius") : 0f;
            if (near && radius < 0)
                throw McpError.Invalid("'radius' cannot be negative.");
            if (sort == "distance" && !near && !(region && call.Token("region") is JObject regionSpec && regionSpec["near"] != null))
                throw McpError.Invalid("sort 'distance' needs 'near' (or a region with near), the point to measure from.");

            //A type name picks that type; text that is part of type names picks each of them; the same text also picks instances of composites whose path holds it ('Door' finds both)
            HashSet<FunctionType> functions = new HashSet<FunctionType>();
            bool partialType = false;
            if (type != null)
            {
                string exact = Enum.GetNames(typeof(FunctionType)).FirstOrDefault(o => string.Equals(o, type, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                    functions.Add((FunctionType)Enum.Parse(typeof(FunctionType), exact));
                else
                {
                    foreach (string candidate in Enum.GetNames(typeof(FunctionType)))
                        if (candidate.IndexOf(type, StringComparison.OrdinalIgnoreCase) >= 0 || McpNames.Squash(candidate).Contains(McpNames.Squash(type)) && McpNames.Squash(type).Length >= 3)
                            functions.Add((FunctionType)Enum.Parse(typeof(FunctionType), candidate));
                    partialType = true;
                }
            }
            ShortGuid? parameterId = parameter == null ? (ShortGuid?)null : McpScript.ParamId(parameter);
            string parameterName = parameterId == null ? null : McpScript.ParamName(parameterId.Value);

            //A walk from the root of a big level visits over a million entities
            using (McpEditorTools.Heartbeat(call, "Searching the level"))
            return McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                Commands commands = content.Level.Commands;
                Composite root = commands.EntryPoints[0];
                bool anyComposite = false;
                //fuzzy: how well each name fits (worked out once per distinct name), the last match's kept for its hit
                const double fuzzyMinimum = 0.5;
                Dictionary<string, double> fits = new Dictionary<string, double>(StringComparer.Ordinal);
                double score = 0;
                bool Matches(Composite composite, Entity entity)
                {
                    if (kinds != null && !kinds.Contains(McpScript.Kind(entity)))
                        return false;
                    if (type != null)
                    {
                        bool ofType = entity is FunctionEntity f && f.function.IsFunctionType && functions.Contains(f.function.AsFunctionType);
                        if (!ofType)
                        {
                            Composite instanced = McpScript.InstancedComposite(commands, entity);
                            if (instanced == null || (instanced.name ?? "").IndexOf(type, StringComparison.OrdinalIgnoreCase) < 0) return false;
                            anyComposite = true;
                        }
                    }
                    if (name != null)
                    {
                        string entityName = McpScript.EntityName(commands, composite, entity) ?? "";
                        if (fuzzy)
                        {
                            if (!fits.TryGetValue(entityName, out score))
                                fits[entityName] = score = McpNames.Score(entityName, name);
                            if (score < fuzzyMinimum) return false;
                        }
                        else if (entityName.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0)
                            return false;
                    }
                    if (parameterId != null)
                    {
                        Parameter p = entity.GetParameter(parameterId.Value);
                        if (p == null) return false;
                        if (value != null && !SameValue(p.content, value, parameterName, commands)) return false;
                    }
                    return true;
                }

                JObject result = new JObject();
                if (partialType)
                {
                    if (functions.Count != 0)
                        result["matched_function_types"] = new JArray(functions.Select(o => o.ToString()).OrderBy(o => o).Take(25));
                    if (functions.Count > 25)
                        result["matched_function_types_count"] = functions.Count;
                }

                //What is kept: placements for a walk (described at the end, only for the page returned), entities for a scan. Sorted
                //by distance or fit, a bounded window of the best so far is kept, so the page returned is the true best.
                List<(McpPlacements.Placement placement, double distance, double score, long order)> kept = new List<(McpPlacements.Placement, double, double, long)>();
                List<(Composite composite, Entity entity, double score, long order)> scanned = new List<(Composite, Entity, double, long)>();
                int total = 0, notReal = 0;
                long order = 0;
                int window = offset + limit;
                double Rank(double distance, double fit) => sort == "score" ? -fit : distance;
                void Keep(McpPlacements.Step step, double distance)
                {
                    total++;
                    order++;
                    if (sort != "walk")
                    {
                        kept.Add((McpPlacements.Placement.Of(step), distance, score, order));
                        if (kept.Count > window * 2 + 64)
                        {
                            List<(McpPlacements.Placement, double, double, long)> best = kept.OrderBy(o => Rank(o.distance, o.score)).ThenBy(o => o.order).Take(window).ToList();
                            kept.Clear();
                            kept.AddRange(best);
                        }
                    }
                    else if (total > offset && total <= window)
                        kept.Add((McpPlacements.Placement.Of(step), distance, score, order));
                }

                //'within' naming no composite but a room (an instance's or zone's name): that place, searched as 'region' would be
                McpRegion withinPlace = null;
                Composite withinComposite = null;
                if (call.Has("within") && !region && !inside)
                {
                    string within = call.Str("within");
                    try
                    {
                        withinComposite = McpScript.FindComposite(commands, within);
                    }
                    catch (McpError missing) when (missing.Code == McpErrorCodes.NotFound && !near && !box)
                    {
                        try
                        {
                            withinPlace = McpScript.FindPlacement(call, commands, content.Level, within);
                        }
                        catch (McpError notPlace)
                        {
                            throw new McpError(missing.Code, missing.Message + " As a place's name: " + notPlace.Message) { Candidates = missing.Candidates };
                        }
                        call.Note("'" + within + "' is not a composite: searched " + withinPlace.Label + " (the place it names) in world space, as 'region' does.");
                    }
                }

                Composite start = root;
                bool walked = false, truncated = false;
                int visited = 0;
                string space = "world";
                if (region || inside || withinPlace != null || ((near || box) && !call.Has("within")))
                {
                    //A place in the level: world space, from the root
                    McpRegion place;
                    List<(cTransform frame, Vector3 half)> volumes = null;
                    if (region)
                        place = McpRegion.Resolve(call, commands, content.Level, call.Token("region"), "region");
                    else if (withinPlace != null)
                        place = withinPlace;
                    else if (near)
                        place = new McpRegion() { Kind = "sphere", Label = "within " + radius + " m of " + McpRegion.Format(centre), Centre = centre, Radius = radius, ClipMin = centre - new Vector3(radius), ClipMax = centre + new Vector3(radius) };
                    else if (box)
                        place = McpRegion.Box(McpValues.ReadVector(call.Token("box_min"), "box_min", null), McpValues.ReadVector(call.Token("box_max"), "box_max", null));
                    else
                    {
                        volumes = Volumes(call, commands, root, call.Token("inside"), out string label);
                        Vector3 low = new Vector3(float.MaxValue), high = new Vector3(float.MinValue);
                        foreach ((cTransform frame, Vector3 half) in volumes)
                        {
                            McpCollision.WorldBox(-half, half, frame, out Vector3 a, out Vector3 b);
                            low = Vector3.Min(low, a);
                            high = Vector3.Max(high, b);
                        }
                        place = McpRegion.Box(low, high, label);
                    }
                    foreach (string note in place.Notes) call.Note(note);
                    if (place.Centre != null && !near) centre = place.Centre.Value;
                    bool measure = near || place.Centre != null;
                    bool onlyRoots = place.Roots.Count != 0 && !place.HasClip;
                    bool InPlace(McpPlacements.Step step, out double distance)
                    {
                        distance = 0;
                        cTransform world = step.World;
                        if (test == "bounds" && Extent(commands, content.Level, step, out Vector3 min, out Vector3 max))
                        {
                            if (measure) distance = DistanceToBox(centre, min, max);
                            if (onlyRoots) return true;
                            if (volumes != null) return volumes.Any(o => BoxesTouch(o.frame, o.half, min, max));
                            if (!place.TouchesClip(min, max)) return false;
                            return place.Centre == null || DistanceToBox(place.Centre.Value, min, max) <= place.Radius + place.Margin;
                        }
                        if (world == null) return onlyRoots && !measure;
                        if (measure) distance = Vector3.Distance(world.position, centre);
                        if (onlyRoots) return true;
                        if (volumes != null) return volumes.Any(o => InBox(o.frame, o.half, world.position));
                        return place.InClip(world.position);
                    }
                    McpCollision.WalkRegion(call, commands, content.Level, place, test == "bounds", step =>
                    {
                        if (maxDepth != null && step.Chain.Count - 1 > maxDepth.Value) return true;
                        if (!Matches(step.Composite, step.Entity)) return true;
                        if (!InPlace(step, out double distance)) return true;
                        if (include == "real" && !step.Real) { notReal++; return true; }
                        Keep(step, distance);
                        return true;
                    }, out visited, out truncated);
                    result["region"] = place.Label;
                    if (place.Spec != null) result["region_spec"] = place.Spec;
                    walked = true;
                }
                else if (call.Has("within"))
                {
                    //A composite and everything placed under it: positions relative to it
                    start = withinComposite ?? McpScript.FindComposite(commands, call.Str("within"));
                    if (start != root) space = "composite " + start.name;
                    Vector3 min = Vector3.Zero, max = Vector3.Zero;
                    if (box)
                    {
                        Vector3 a = McpValues.ReadVector(call.Token("box_min"), "box_min", null), b = McpValues.ReadVector(call.Token("box_max"), "box_max", null);
                        min = Vector3.Min(a, b);
                        max = Vector3.Max(a, b);
                    }
                    McpPlacements walker = new McpPlacements(commands) { PlaceUnpositioned = true };
                    McpPlacements.Step current = null;
                    walker.Walk(start, step =>
                    {
                        current = step;
                        if (maxDepth != null && step.Chain.Count - 1 > maxDepth.Value) return true;
                        if (!Matches(step.Composite, step.Entity)) return true;
                        double distance = 0;
                        if (near || box)
                        {
                            Vector3 low, high;
                            if (test == "bounds" && Extent(commands, content.Level, step, out low, out high)) { }
                            else if (step.World != null) { low = step.World.position; high = step.World.position; }
                            else return true;
                            if (near)
                            {
                                distance = DistanceToBox(centre, low, high);
                                if (distance > radius) return true;
                            }
                            else if (low.X > max.X || high.X < min.X || low.Y > max.Y || high.Y < min.Y || low.Z > max.Z || high.Z < min.Z)
                                return true;
                        }
                        if (include == "real" && !step.Real) { notReal++; return true; }
                        Keep(step, distance);
                        return true;
                    }, child => maxDepth == null || current == null || current.Chain.Count - 1 < maxDepth.Value, call.Cancel);
                    visited = walker.Visited;
                    truncated = walker.Truncated;
                    result["within"] = start.name;
                    walked = true;
                }
                else
                {
                    //The composites as they are, not placements
                    List<Composite> scope = call.Has("composite") ? new List<Composite>() { McpScript.FindComposite(commands, call.Str("composite")) } : commands.Entries.Where(o => o != null).ToList();
                    foreach (Composite composite in scope)
                    {
                        foreach (Entity entity in composite.GetEntities())
                        {
                            if (!Matches(composite, entity)) continue;
                            total++;
                            order++;
                            if (fuzzy)
                            {
                                //Best fit first: keep a bounded window of the best so far
                                scanned.Add((composite, entity, score, order));
                                if (scanned.Count > window * 2 + 64)
                                {
                                    List<(Composite, Entity, double, long)> best = scanned.OrderByDescending(o => o.score).ThenBy(o => o.order).Take(window).ToList();
                                    scanned.Clear();
                                    scanned.AddRange(best);
                                }
                            }
                            else if (total > offset && total <= window)
                                scanned.Add((composite, entity, score, order));
                        }
                    }
                    if (fuzzy)
                    {
                        List<(Composite, Entity, double, long)> page = scanned.OrderByDescending(o => o.score).ThenBy(o => o.order).Skip(offset).Take(limit).ToList();
                        scanned.Clear();
                        scanned.AddRange(page);
                    }
                }

                JArray found = new JArray();
                if (walked)
                {
                    result["space"] = space;
                    IEnumerable<(McpPlacements.Placement placement, double distance, double score, long order)> page = sort != "walk"
                        ? kept.OrderBy(o => Rank(o.distance, o.score)).ThenBy(o => o.order).Skip(offset).Take(limit)
                        : kept;
                    foreach ((McpPlacements.Placement placement, double distance, double fit, long _) in page)
                    {
                        JObject described = McpEditingTools.DescribePlacement(commands, start, placement);
                        Entity entity = placement.Chain[placement.Chain.Count - 1];
                        JObject brief = McpScript.Brief(commands, placement.Composite, entity);
                        JToken local = brief["position"], localRotation = brief["rotation"];
                        brief.Remove("position");
                        brief.Remove("rotation");
                        brief["composite"] = placement.Composite.name;
                        brief["path"] = described["path"];
                        brief["ids"] = described["ids"];
                        if (described["position"] != null)
                        {
                            brief["position"] = described["position"];
                            brief["rotation"] = described["rotation"];
                        }
                        if (local != null && !JToken.DeepEquals(local, described["position"]))
                            brief["local_position"] = local;
                        if (localRotation != null && !JToken.DeepEquals(localRotation, described["rotation"]))
                            brief["local_rotation"] = localRotation;
                        if (near || sort == "distance")
                            brief["distance"] = Math.Round(distance, 2);
                        if (fuzzy)
                            brief["score"] = Math.Round(fit, 2);
                        foreach (string flag in new[] { "position_from", "position_linked", "deleted", "template", "shared_repeat", "collision_deleted", "stands_for", "note" })
                            if (described[flag] != null) brief[flag] = described[flag];
                        found.Add(brief);
                    }
                    if (truncated)
                        result["truncated"] = "Stopped after " + visited + " entities: search a smaller place or within a smaller composite.";
                    if (notReal != 0)
                        result["not_real"] = notReal + " more " + (notReal == 1 ? "match is a deleted, template or shared-repeat placement" : "matches are deleted, template or shared-repeat placements") + " (include 'all' lists them).";
                }
                else
                {
                    foreach ((Composite composite, Entity entity, double fit, long _) in scanned)
                    {
                        JObject brief = McpScript.Brief(commands, composite, entity);
                        brief["composite"] = composite.name;
                        if (fuzzy)
                            brief["score"] = Math.Round(fit, 2);
                        found.Add(brief);
                    }
                    result["space"] = "composite";
                }
                result["entities"] = found;
                McpPaging.Describe(result, total, offset, found.Count);
                if (fuzzy && total == 0)
                    result["note"] = "No entity name fits '" + name + "' well enough. find_places also matches rooms, zones and models by name; find_composites match 'fuzzy' matches composite paths.";

                if (total == 0 && type != null && functions.Count == 0 && !anyComposite)
                    result["note"] = "'" + type + "' is not a function type, part of one, or in the path of any composite instanced here." +
                        McpNames.DidYouMean(Enum.GetNames(typeof(FunctionType)).Concat(commands.Entries.Where(o => o != null && o.name != null).Select(o => McpNames.Leaf(o.name))), type, 6) +
                        " list_function_types and find_composites list them.";
                else if (partialType && functions.Count != 0)
                    call.Note("'" + type + "' is not a whole function type name: every type containing it was searched (matched_function_types), and instances of composites whose path contains it.");
                return result;
            });
        }

        /// <summary>The oriented boxes of a volume entity's placements ('inside'): each placement's world frame (centred) and half extents.</summary>
        private static List<(cTransform frame, Vector3 half)> Volumes(McpCall call, Commands commands, Composite root, JToken spec, out string label)
        {
            if (!(spec is JObject obj) || (obj["path"] == null && (obj["composite"] == null || obj["entity"] == null)))
                throw McpError.Invalid("'inside' is {\"composite\": ..., \"entity\": ...} (each placement of that volume) or {\"path\": [ids/names from the root]}.");
            List<List<Entity>> chains = new List<List<Entity>>();
            if (obj["path"] != null)
                chains.Add(McpScript.ChainFrom(commands, root, McpScript.PathSteps(obj["path"], "inside.path")));
            else
            {
                Composite composite = McpScript.FindComposite(commands, McpValues.ReadString(obj["composite"]));
                Entity entity = McpScript.FindEntity(commands, composite, McpValues.ReadString(obj["entity"]));
                List<McpPlacements.Placement> found = new List<McpPlacements.Placement>();
                int total = new McpPlacements(commands) { PlaceUnpositioned = true }.PlacementsOf(root, composite, entity, found, 200, call.Cancel, realOnly: true);
                if (total == 0)
                    throw new McpError(McpScript.EntityName(commands, composite, entity) + " is not placed in the level, so it has no box in it.");
                chains.AddRange(found.Select(o => o.Chain));
            }
            McpPlacements walker = new McpPlacements(commands) { PlaceUnpositioned = true };
            List<(cTransform, Vector3)> boxes = new List<(cTransform, Vector3)>();
            Entity volume = chains[0][chains[0].Count - 1];
            if (volume.GetParameter(ShortGuidUtils.Generate("half_dimensions")) == null)
                call.Note("The volume has no half_dimensions of its own: the default box (0.5 x 1 x 0.5 half extents) was used.");
            foreach (List<Entity> chain in chains)
            {
                cTransform world = walker.Evaluate(root, chain).World ?? new cTransform(Vector3.Zero, Vector3.Zero);
                Vector3 half = McpCollision.HalfExtentsOf(chain[chain.Count - 1]);
                boxes.Add((new cTransform(McpCollision.BoxCentre(world, half), world.rotation), half));
            }
            label = "inside " + McpRegion.DescribeChain(commands, chains[0]) + (chains.Count > 1 ? " (" + chains.Count + " placements)" : "");
            return boxes;
        }

        private static bool InBox(cTransform frame, Vector3 half, Vector3 point)
        {
            Vector3 local = InstanceTransform.PointToLocal(frame, point);
            const float slack = 1e-3f;
            return Math.Abs(local.X) <= half.X + slack && Math.Abs(local.Y) <= half.Y + slack && Math.Abs(local.Z) <= half.Z + slack;
        }

        //A box against an oriented volume, by the volume's world AABB and the box's corners and centre (close enough for a search)
        private static bool BoxesTouch(cTransform frame, Vector3 half, Vector3 min, Vector3 max)
        {
            McpCollision.WorldBox(-half, half, frame, out Vector3 a, out Vector3 b);
            if (min.X > b.X || max.X < a.X || min.Y > b.Y || max.Y < a.Y || min.Z > b.Z || max.Z < a.Z) return false;
            if (InBox(frame, half, (min + max) * 0.5f)) return true;
            for (int i = 0; i < 8; i++)
                if (InBox(frame, half, new Vector3((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z))) return true;
            return InBox(frame, half, Vector3.Clamp(frame.position, min, max));
        }

        private static double DistanceToBox(Vector3 p, Vector3 min, Vector3 max)
        {
            Vector3 nearest = Vector3.Clamp(p, min, max);
            return Vector3.Distance(p, nearest);
        }

        /// <summary>
        /// The box a step's content fills, in the walk's space: an instance's contents (models, collision, volumes), a model's
        /// meshes, a volume's box. False when it has none (logic, or content an alias moves), so the position is used instead.
        /// </summary>
        private static bool Extent(Commands commands, Level level, McpPlacements.Step step, out Vector3 min, out Vector3 max)
        {
            min = max = Vector3.Zero;
            cTransform world = step.World;
            if (world == null || !(step.Entity is FunctionEntity function)) return false;
            if (!function.function.IsFunctionType)
            {
                Composite child = commands.GetComposite(function.function);
                if (child == null) return false;
                McpCollision.LocalBounds bounds = McpCollision.Bounds(commands, level, child);
                if (bounds.Unbounded || bounds.Empty) return false;
                McpCollision.WorldBox(bounds.Min, bounds.Max, world, out min, out max);
                return true;
            }
            FunctionType type = function.function.AsFunctionType;
            if (McpCollision.IsBoxType(type) || function.GetParameter(ShortGuidUtils.Generate("half_dimensions")) != null)
            {
                Vector3 half = McpCollision.HalfExtentsOf(function);
                McpCollision.WorldBox(-half, half, new cTransform(McpCollision.BoxCentre(world, half), world.rotation), out min, out max);
                return true;
            }
            List<RenderableElements.Element> elements = function.GetResource(ResourceType.RENDERABLE_INSTANCE, true)?.RenderableInstance;
            if (elements == null || elements.Count == 0) return false;
            bool any = false;
            Vector3 low = new Vector3(float.MaxValue), high = new Vector3(float.MinValue);
            foreach (RenderableElements.Element element in elements)
            {
                if (element?.Model == null) continue;
                McpCollision.WorldBox(element.Model.MinBounds, element.Model.MaxBounds, world, out Vector3 a, out Vector3 b);
                low = Vector3.Min(low, a);
                high = Vector3.Max(high, b);
                any = true;
            }
            min = low;
            max = high;
            return any;
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
            int limit = McpPaging.Limit(call, 200), offset = McpPaging.Offset(call);
            Level level = OtherLevel(call);
            return McpEditor.UI(() =>
            {
                Commands commands = level?.Commands ?? McpEditor.RequireCommands(forEditing: false);
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
                    JObject listed = new JObject() { ["composite"] = composite.name, ["instance_count"] = instances.Count };
                    if (level != null) listed["level"] = level.Name;
                    McpPaging.Page(call, instances, listed, "instances", o => o, 200);
                    if (instances.Count != 0)
                        listed["hint"] = level != null
                            ? "describe_level_composite placements:true gives that level's placements with world positions."
                            : "get_placements gives each placement's path from the root and its position; get_usage_patterns shows how they are wired.";
                    return listed;
                }

                if (level == null) CompileIfShown(composite);
                Entity entity = McpScript.FindEntity(commands, composite, call.Str("entity"));
                JObject described = McpScript.Describe(commands, composite, entity, parameters: false);
                if (level != null) described["level"] = level.Name;
                bool PointsAt(EntityPath path, Composite from) => path?.path != null && commands.Utils.GetResolvedTarget(commands.Utils.ResolveEntityPath(path, from)) == (composite, entity);

                JArray pointers = new JArray();
                int total = 0;
                void Add(JObject pointer)
                {
                    total++;
                    if (total > offset && pointers.Count < limit) pointers.Add(pointer);
                }
                JObject Pointer(Composite other, Entity by, string kind) => new JObject() { ["composite"] = other.name, ["id"] = McpScript.Id(by.shortGUID), ["kind"] = kind, ["name"] = McpScript.EntityName(commands, other, by) };

                int sharedPaths = 0;
                foreach (Composite other in commands.Entries)
                {
                    if (other == null || (scope != null && !scope.Contains(other))) continue;
                    List<AliasEntity> reaching = new List<AliasEntity>();
                    foreach (AliasEntity alias in other.aliases)
                        if (commands.Utils.GetResolvedTarget(commands.Utils.ResolveAlias(alias, other)) == (composite, entity))
                            reaching.Add(alias);
                    foreach (AliasEntity alias in reaching)
                    {
                        List<Tuple<Composite, Entity>> resolved = commands.Utils.ResolveAlias(alias, other);
                        JObject pointer = Pointer(other, alias, "alias");
                        pointer["path"] = McpScript.DescribePath(commands, resolved, alias.alias?.path);
                        List<string> overrides = alias.parameters.Where(o => o.name != ShortGuids.name).Select(o => McpScript.ParamName(o.name)).ToList();
                        if (overrides.Count != 0) pointer["overrides"] = new JArray(overrides);
                        //Several aliases on one path: their order of application is undefined
                        List<AliasEntity> same = reaching.Where(o => o != alias && SameAliasPath(o.alias?.path, alias.alias?.path)).ToList();
                        if (same.Count != 0)
                        {
                            pointer["same_path_as"] = new JArray(same.Select(o => McpScript.Id(o.shortGUID)));
                            sharedPaths++;
                        }
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
                if (offset != 0) described["offset"] = offset;
                if (offset + pointers.Count < total)
                {
                    described["next_offset"] = offset + pointers.Count;
                    described["pointers_note"] = "Showing " + pointers.Count + " of " + total + ": page with next_offset or narrow 'scope'.";
                }
                if (sharedPaths != 0)
                    call.Note(sharedPaths + " of the aliases share a path with another alias in the same composite ('same_path_as'): the game applies such aliases in no set order, so which value wins is undefined. Keep one (delete_entities the others, or clear_instance_overrides).");

                //The script pages that draw it (the Flowgraphs tab of the References window); the page tables are the open level's
                if (level == null)
                {
                    JArray pages = new JArray();
                    foreach (var page in FlowgraphLayoutManager.GetLayouts(composite))
                    {
                        int nodes = page.Nodes.Count(o => o.EntityGUID == entity.shortGUID);
                        if (nodes != 0) pages.Add(new JObject() { ["page"] = page.Name, ["nodes"] = nodes });
                    }
                    described["pages"] = pages;
                }
                return described;
            });
        }

        /// <summary>Whether two stored alias paths name the same steps (a trailing empty step ignored).</summary>
        private static bool SameAliasPath(ShortGuid[] a, ShortGuid[] b)
        {
            if (a == null || b == null) return false;
            int lengthA = a.Length > 0 && a[a.Length - 1] == ShortGuid.Invalid ? a.Length - 1 : a.Length;
            int lengthB = b.Length > 0 && b[b.Length - 1] == ShortGuid.Invalid ? b.Length - 1 : b.Length;
            if (lengthA != lengthB) return false;
            for (int i = 0; i < lengthA; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
        #endregion

        #region Function types
        /// <summary>A FunctionType from what a client wrote: its name in any case, or with spaces and '_' ignored when that picks one.</summary>
        public static bool TryParseFunctionType(string text, out FunctionType type)
        {
            type = default(FunctionType);
            if (string.IsNullOrWhiteSpace(text)) return false;
            string wanted = text.Trim();
            if (Enum.TryParse(wanted, true, out type) && Enum.IsDefined(typeof(FunctionType), type) && !wanted.All(char.IsDigit))
                return true;
            string squashed = McpNames.Squash(wanted);
            List<string> loose = Enum.GetNames(typeof(FunctionType)).Where(o => McpNames.Squash(o) == squashed).ToList();
            if (loose.Count != 1) return false;
            type = (FunctionType)Enum.Parse(typeof(FunctionType), loose[0]);
            return true;
        }

        /// <summary>The "no such type" error, with the nearest type names and a word on composites of that name.</summary>
        public static McpError NoFunctionType(Commands commands, string name)
        {
            string hint = "list_function_types lists them.";
            Composite composite = commands?.Entries.FirstOrDefault(o => o != null && o.name != null && McpNames.Squash(McpNames.Leaf(o.name)) == McpNames.Squash(name));
            if (composite != null)
                hint = composite.name + " is a composite, not a function type: place it as an instance (create_entities 'composite'), and get_usage_patterns shows how it is used. " + hint;
            return McpError.NotFound("function type", name, Enum.GetNames(typeof(FunctionType)), hint);
        }

        private static object DescribeFunctionType(McpCall call)
        {
            string name = call.Str("type", required: true).Trim();
            bool usage = call.Bool("usage");
            JObject result = McpEditor.UI(() =>
            {
                //Pins, defaults, relays and inheritance come from the vanilla tables: no level is needed
                LevelContent content = Singleton.Editor?.CompositeBrowser?.Content;
                Commands commands = content != null && content.IsLevelDataLoaded ? content.Level?.Commands : null;
                CommandsUtils utils = commands?.Utils ?? McpValues.Vanilla;
                if (!TryParseFunctionType(name, out FunctionType type))
                    throw NoFunctionType(commands, name);
                JArray chain = new JArray();
                JArray pins = new JArray();
                HashSet<ShortGuid> seen = new HashSet<ShortGuid>();
                FunctionType? current = type;
                while (current != null)
                {
                    //An inherited pin's default lives with the type it comes from: the derived type's own block does not hold it
                    FunctionType owner = current.Value;
                    chain.Add(owner.ToString());
                    foreach ((ShortGuid pin, ParameterVariant variant, DataType dataType) in utils.GetAllParameters(owner))
                    {
                        if (!seen.Add(pin)) continue;
                        JObject described = McpValues.DescribePin(commands, pin, variant, dataType, () => utils.CreateDefaultParameterData(owner, pin, variant) ?? (owner == type ? null : utils.CreateDefaultParameterData(type, pin, variant)));
                        if (variant == ParameterVariant.METHOD_PIN)
                        {
                            ShortGuid relay = utils.GetRelay(pin);
                            if (relay != ShortGuid.Invalid) described["relay"] = McpScript.ParamName(relay);
                        }
                        if (owner != type) described["from"] = owner.ToString();
                        pins.Add(described);
                    }
                    try { current = utils.GetInheritedFunction(owner); } catch { current = null; }
                }
                JObject described2 = new JObject() { ["type"] = type.ToString(), ["inherits"] = new JArray(chain.Skip(1)), ["pins"] = pins };
                try { described2["category"] = FlowgraphLayoutManager.TryGetCategoryForFunctionType(type) ?? "Misc"; } catch { }
                described2["docs"] = "https://opencage.co.uk/docs/cathode-entities/#" + type;
                described2["links"] = McpValues.LinkDirections;
                HashSet<string> lineage = new HashSet<string>(chain.Select(o => (string)o));
                if (McpCollision.IsBoxType(type) || seen.Contains(ShortGuidUtils.Generate("half_dimensions")))
                    described2["box"] = "A box volume stands on its position: it spans +-half_dimensions.x/z and 0..2*half_dimensions.y up, in its own rotation.";
                //Which way it points: its local +Z, through the rotation convention every transform uses
                if (lineage.Contains(nameof(FunctionType.LightReference)))
                    described2["facing"] = "A light (a spot's cone) shines along its local +Z. Rotation is [pitch, yaw, roll] in degrees (Y yaw, X pitch, Z roll): to shine along (x, y, z), yaw = atan2(x, z) and pitch = -asin(y / length). add_light takes look_at or direction and works this out.";
                else if (lineage.Contains(nameof(FunctionType.CameraResource)))
                    described2["facing"] = "A camera looks along its local +Z with +Y up. Rotation is [pitch, yaw, roll] in degrees (Y yaw, X pitch, Z roll): to look along (x, y, z), yaw = atan2(x, z) and pitch = -asin(y / length). create_camera_animation and animate_camera_path aim it with look_at.";
                //What the build bakes: edits show in the viewport at once, in game only after Save & Build
                if (_built.Contains(type) || lineage.Overlaps(_built.Select(o => o.ToString())))
                    described2["build"] = "Save & Build bakes this into the level (movers, lights, resources): a change shows in the viewport at once but reaches the game only after save_level build=true. " +
                        "These usually sit in composites placed many times, where an edit changes every placement (describe_entity 'placements'); set_parameters 'path'" + (lineage.Contains(nameof(FunctionType.LightReference)) ? " or set_lights scope 'placement'" : "") + " changes one.";
                //Character commands do nothing until a character is bound to them
                if (lineage.Contains(nameof(FunctionType.CharacterCommand)) && type != FunctionType.CharacterCommand)
                    described2["character"] = "Needs a bound character: fire it from TriggerBindCharacter.bound_trigger (characters -> the NPC's npc_reference), or from the NPC's own finished_spawning; play_character_animation and create_npc_route build this.";
                return described2;
            });
            if (usage)
            {
                if (!TryParseFunctionType(name, out FunctionType type)) throw NoFunctionType(null, name);
                result["usage"] = McpUsageTools.Summary(call, type);
            }
            return result;
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
