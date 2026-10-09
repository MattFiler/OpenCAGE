using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.DockPanels;
using OpenCAGE.Undo;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE.MCP
{
    /// <summary>Other levels: searching them, and bringing their composites (with everything they use) into the open one.</summary>
    internal static class McpPortingTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "search_level",
                Title = "Search another level",
                Description = "Look inside any level (it does not have to be open): find composites by path, or entities by function type / name / parameter, to decide what to port. Composite hits say whether the level places them (placed, reachable from its root; is_root) and which composites place them. Searching composites is instant, placed_only included; searching entities loads the level (tens of seconds the first time, then remembered for port_composites). find_in_levels searches every level at once.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("level", "The level, as list_levels names it (e.g. 'PRODUCTION/SCI_ANDROIDLAB').", required: true),
                    McpSchema.String("composites", "Words the composite path must contain (e.g. 'corridor', 'Archetypes Script Mission')."),
                    McpSchema.String("function_type", "Find entities of this built-in type (e.g. 'Character'; 'SpawnPositionSelect' is a composite - use composites for that)."),
                    McpSchema.String("entity_name", "Find entities whose name contains this."),
                    McpSchema.String("parameter", "Find entities with this parameter set (e.g. 'spawn_on_reset')."),
                    McpSchema.Boolean("placed_only", "Only composites that are actually placed in that level (reachable from its root)."),
                    McpSchema.Limit(100, "results"),
                    McpSchema.Offset("results")),
                ReadOnly = true,
                Idempotent = true,
                Run = SearchLevel,
            };

            yield return new McpTool()
            {
                Name = "find_in_levels",
                Title = "Find a composite or entity across levels",
                Description = "Which levels have something, without opening any. composite (a path, id or words of its path): per level whether it is there, its id, whether that level places it (reachable from its root) and what places it - from each level's script index, in seconds for every level. function_type / entity_name / parameter (+ value): reads each level's script (seconds per level; progress shown) and counts the matching entities, with the first few. Read from disk: unsaved changes in the open level are not seen.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "A composite path or id (e.g. 'Archetypes\\Script\\Mission\\SpawnPositionSelect'), or words its path contains."),
                    McpSchema.String("function_type", "Entities of this built-in type (e.g. 'SwitchLevel', 'Speech')."),
                    McpSchema.String("entity_name", "Entities whose name contains this."),
                    McpSchema.String("parameter", "Entities with this parameter set (e.g. 'level_name')."),
                    McpSchema.Any("value", "With parameter: only those whose value contains this text (strings) or equals it."),
                    McpSchema.Strings("levels", "Only these levels (default every level, FRONTEND included)."),
                    McpSchema.Boolean("placed_only", "composite: only levels that place it."),
                    McpSchema.Integer("limit", "Entity search: at most this many hits listed per level (default 5).")),
                ReadOnly = true,
                Idempotent = true,
                Run = FindInLevels,
            };

            yield return new McpTool()
            {
                Name = "describe_level_composite",
                Title = "Describe a composite in another level",
                Description = "Read a composite in another level in depth without opening it: its entities, variables and what it instances; links:true adds each listed entity's links; entity: one entity in full (every parameter value, links in and out, as describe_entity gives it); placements: where that level places the composite, with world transforms (as get_placements gives them), and what refers to it. The level is loaded (tens of seconds the first time, then remembered for port_composites). For the open level, read from disk: unsaved changes are not seen.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("level", "The level.", required: true),
                    McpSchema.String("composite", "The composite's path or id there.", required: true),
                    McpSchema.String("entity", "Describe this entity of the composite (id or name) in full instead of listing them."),
                    McpSchema.Boolean("links", "List each entity's links out (default false)."),
                    McpSchema.Boolean("placements", "Also list where the level places the composite, with world positions and rotations (default false)."),
                    McpSchema.String("filter", "Only entities whose name or type contains this."),
                    McpSchema.Limit(200, "entities"),
                    McpSchema.Offset("entities")),
                ReadOnly = true,
                Idempotent = true,
                Run = DescribeLevelComposite,
            };

            yield return new McpTool()
            {
                Name = "port_composites",
                Title = "Port composites from another level",
                Description = "Copy composites from another level into the open one with everything they use (child composites, models, materials, textures, collision, physics, pages, Characters' display models). Existing ones (same id) are kept unless overwrite_composites, which also clears the undo history. The port itself is not undoable (saved by save_level; save first to be able to go back with load_level discard_unsaved). place puts an instance of what was ported in the level in the same call, as one undo step: where you say, or like_source - where it sits in the source level (carry_overrides also brings that instance's own parameters and the overrides its parents hold into it). dry_run lists what would come (composites new / already here / replaced, display models, models and materials that clash) without porting.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("level", "The level to copy from.", required: true),
                    McpSchema.Strings("composites", "Their paths or ids in that level.", required: true),
                    McpSchema.Boolean("include_children", "Also bring the composites they place (default true - they need them to work)."),
                    McpSchema.Boolean("include_display_models", "Also bring the DisplayModel composites their Characters use (default true)."),
                    McpSchema.Boolean("overwrite_composites", "Replace composites the open level already has with the same id (default false)."),
                    McpSchema.Boolean("overwrite_assets", "Replace models/textures/materials the open level already has with the same name (default false)."),
                    McpSchema.Nested("place", "After porting, place one instance (one undo step).", McpSchema.Object(
                        McpSchema.String("composite", "Which ported composite to place (path or id in the source level). Default: the first of 'composites'."),
                        McpSchema.String("into", "The composite of the open level to place it in. " + McpSchema.CompositeDefaultRoot),
                        McpSchema.Integer("placement", "like_source into a composite placed more than once: which placement of 'into' (0-based, get_placements order) the world transform is converted for."),
                        McpSchema.Position("position", "Where, in 'into's space (the root's is world)."),
                        McpSchema.Rotation("rotation", "Its rotation, in 'into's space."),
                        McpSchema.Strings("like_source", "Instead of position: the instance entities (names or ids) to step through from the source level's root to the placement to copy, e.g. ['ENVIRONMENT', 'Room_A']; its world transform there becomes its transform here."),
                        McpSchema.String("name", "The new instance's name (default: the source instance's, or numbered)."))),
                    McpSchema.Boolean("carry_overrides", "With place like_source: copy the source instance's parameters and recreate the aliases its parents hold into it (what was overridden inside it there)."),
                    McpSchema.Boolean("open", "Show the first ported composite in the editor afterwards."),
                    McpSchema.Boolean("dry_run", "Report what would be ported and placed, changing nothing (loads the source level).")),
                Destructive = true,
                Run = PortComposites,
            };

            yield return new McpTool()
            {
                Name = "export_composites_to_levels",
                Title = "Port composites into other levels",
                Description = "Port composites of the open level (with what they place and use) into other levels on disk, as File > Export Composites > To Level: each destination is backed up (backup_first, default true), loaded, ported into and saved (build: Save & Build). Written at once, not undoable except by restore_backup with the backup ids returned; closes this install's game. Unsaved changes here are included. dry_run reports without writing.",
                InputSchema = McpSchema.Object(
                    McpSchema.Strings("composites", "Composites of the open level to port, by path or id.", required: true),
                    McpSchema.Strings("levels", "Destination levels, as list_levels names them (not the open one).", required: true),
                    McpSchema.Boolean("overwrite_composites", "Replace composites a destination already has with the same id (default false: keep theirs)."),
                    McpSchema.Boolean("overwrite_assets", "Replace same-named models/textures/materials in the destinations (default false)."),
                    McpSchema.Boolean("build", "Save & Build each destination after porting (slow: every baker runs). Default false: a plain save."),
                    McpSchema.Boolean("backup_first", "Back up each destination before writing it (default true); the result lists the backup ids."),
                    McpSchema.Boolean("dry_run", "Check everything and report what each destination would receive, writing nothing.")),
                Destructive = true,
                Run = ExportToLevels,
            };

            yield return new McpTool()
            {
                Name = "release_level_cache",
                Title = "Release loaded source level",
                Description = "Free the memory of the other level search_level / port_composites keep loaded.",
                Idempotent = true,
                Run = call =>
                {
                    string released = McpLevelCache.Release();
                    return new JObject() { ["released"] = released };
                },
            };
        }

        #region Search
        private static object SearchLevel(McpCall call)
        {
            string level = Normalise(call.Str("level", required: true));
            string words = call.Str("composites");
            string functionType = call.Str("function_type");
            string entityName = call.Str("entity_name");
            string parameter = call.Str("parameter");
            bool entitySearch = functionType != null || entityName != null || parameter != null;
            if (!entitySearch && words == null)
                throw McpError.Invalid("Give 'composites' to search composite paths, or function_type / entity_name / parameter to search entities.");

            HashSet<ShortGuid> inOpenLevel = McpEditor.UI(() =>
            {
                Commands open = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
                return open == null ? new HashSet<ShortGuid>() : new HashSet<ShortGuid>(open.Entries.Where(o => o != null).Select(o => o.shortGUID));
            });
            string[] terms = (words ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            bool MatchesPath(string path) => terms.All(t => (path ?? "").IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);

            //The script's own index answers which composites the level places (reachable from its root), with no load
            List<CompositeIndexEntry> index = CompositeIndexCache.Get(level);
            IndexGraph graph = new IndexGraph(index);
            JObject result = new JObject() { ["level"] = level };
            if (!entitySearch)
            {
                List<CompositeIndexEntry> hits = index.Where(o => MatchesPath(o.Name) && (!call.Bool("placed_only") || graph.Placed.Contains(o.ID))).ToList();
                result["count"] = hits.Count;
                McpPaging.Page(call, hits, result, "composites", o =>
                {
                    JObject hit = new JObject()
                    {
                        ["id"] = McpScript.Id(o.ID),
                        ["path"] = o.Name,
                        ["places"] = o.Instances?.Count ?? 0,
                        ["placed"] = graph.Placed.Contains(o.ID),
                        ["in_open_level"] = inOpenLevel.Contains(o.ID),
                    };
                    if (o.IsRoot) hit["is_root"] = true;
                    List<string> by = graph.PlacedBy(o.ID).Take(4).ToList();
                    if (by.Count != 0) hit["placed_by"] = new JArray(by);
                    return hit;
                }, 100);
                if (hits.Count == 0)
                {
                    List<string> near = McpNames.Similar(index.Select(o => o.Name), words, 6);
                    call.Note("No composite path in " + level + " contains every word of '" + words + "'" + (call.Bool("placed_only") ? " among those it places" : "") + "." + (near.Count != 0 ? " Did you mean " + McpNames.Quote(near) + "?" : ""));
                }
                return result;
            }

            Level source = McpLevelCache.Get(call, level);
            Commands commands = source.Commands;
            List<Composite> scope = commands.Entries.Where(o => o != null && MatchesPath(o.name) && (!call.Bool("placed_only") || graph.Placed.Contains(o.shortGUID))).ToList();

            FunctionType? type = null;
            if (functionType != null)
            {
                if (!Enum.TryParse(functionType.Trim(), true, out FunctionType parsed) || !Enum.IsDefined(typeof(FunctionType), parsed))
                    throw McpError.NotFound("function type", functionType, Enum.GetNames(typeof(FunctionType)), "If it is a composite, search with 'composites' instead.");
                type = parsed;
            }
            ShortGuid? parameterId = parameter == null ? (ShortGuid?)null : McpScript.ParamId(parameter);
            List<Tuple<Composite, Entity>> matches = new List<Tuple<Composite, Entity>>();
            foreach (Composite composite in scope)
            {
                foreach (Entity entity in composite.GetEntities())
                {
                    if (type != null && !(entity is FunctionEntity f && f.function == type.Value)) continue;
                    if (entityName != null && McpScript.EntityName(commands, composite, entity).IndexOf(entityName, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (parameterId != null && entity.GetParameter(parameterId.Value) == null) continue;
                    matches.Add(Tuple.Create(composite, entity));
                }
            }
            result["count"] = matches.Count;
            McpPaging.Page(call, matches, result, "entities", o =>
            {
                JObject hit = McpScript.Brief(commands, o.Item1, o.Item2);
                hit["composite"] = o.Item1.name;
                hit["composite_id"] = McpScript.Id(o.Item1.shortGUID);
                hit["composite_placed"] = graph.Placed.Contains(o.Item1.shortGUID);
                hit["composite_in_open_level"] = inOpenLevel.Contains(o.Item1.shortGUID);
                if (parameterId != null) hit["value"] = McpValues.ToJson(o.Item2.GetParameter(parameterId.Value).content, commands, source.Models);
                return hit;
            }, 100);
            return result;
        }

        /// <summary>
        /// A level's composites as its script index has them: which are placed (reachable from the root over instances, never
        /// into GLOBAL or the pause menu) and which place which. No level load.
        /// </summary>
        internal sealed class IndexGraph
        {
            public readonly HashSet<ShortGuid> Placed = new HashSet<ShortGuid>();
            public readonly CompositeIndexEntry? Root;
            private readonly Dictionary<ShortGuid, CompositeIndexEntry> _byId = new Dictionary<ShortGuid, CompositeIndexEntry>();
            private readonly Dictionary<ShortGuid, List<CompositeIndexEntry>> _parents = new Dictionary<ShortGuid, List<CompositeIndexEntry>>();

            public IndexGraph(List<CompositeIndexEntry> index)
            {
                foreach (CompositeIndexEntry entry in index)
                {
                    _byId[entry.ID] = entry;
                    if (entry.IsRoot) Root = entry;
                    foreach (ShortGuid child in (entry.Instances ?? new List<ShortGuid>()).Distinct())
                    {
                        if (!_parents.TryGetValue(child, out List<CompositeIndexEntry> parents)) _parents[child] = parents = new List<CompositeIndexEntry>();
                        if (!parents.Any(o => o.ID == entry.ID)) parents.Add(entry);
                    }
                }
                if (Root == null) return;
                Queue<ShortGuid> todo = new Queue<ShortGuid>();
                todo.Enqueue(Root.Value.ID);
                while (todo.Count != 0)
                {
                    ShortGuid id = todo.Dequeue();
                    if (!Placed.Add(id) || !_byId.TryGetValue(id, out CompositeIndexEntry entry)) continue;
                    foreach (ShortGuid child in entry.Instances ?? new List<ShortGuid>())
                        if (child != GlobalId && child != PauseMenuId && !Placed.Contains(child))
                            todo.Enqueue(child);
                }
            }

            /// <summary>The composites that place this one directly (names), placed ones first.</summary>
            public IEnumerable<string> PlacedBy(ShortGuid id) =>
                _parents.TryGetValue(id, out List<CompositeIndexEntry> parents) ? parents.OrderBy(o => Placed.Contains(o.ID) ? 0 : 1).Select(o => o.Name) : Enumerable.Empty<string>();

            public CompositeIndexEntry? Get(ShortGuid id) => _byId.TryGetValue(id, out CompositeIndexEntry entry) ? entry : (CompositeIndexEntry?)null;
        }

        /// <summary>The index entries of these composites and (with <paramref name="children"/>) everything they place, stopping at GLOBAL and the pause menu.</summary>
        internal static List<CompositeIndexEntry> IndexClosure(List<CompositeIndexEntry> index, IEnumerable<ShortGuid> roots, bool children)
        {
            IndexGraph graph = new IndexGraph(index);
            HashSet<ShortGuid> seen = new HashSet<ShortGuid>();
            List<CompositeIndexEntry> closure = new List<CompositeIndexEntry>();
            Stack<ShortGuid> todo = new Stack<ShortGuid>(roots);
            while (todo.Count != 0)
            {
                ShortGuid id = todo.Pop();
                CompositeIndexEntry? found = graph.Get(id);
                if (found == null || !seen.Add(id)) continue;
                CompositeIndexEntry entry = found.Value;
                closure.Add(entry);
                if (!children) continue;
                foreach (ShortGuid child in entry.Instances ?? new List<ShortGuid>())
                    if (child != GlobalId && child != PauseMenuId) todo.Push(child);
            }
            return closure;
        }

        /// <summary>
        /// A level's script alone, read from its file (COMMANDS.PAK, or BIN): no models, collision or animation data is
        /// loaded, so resource references come back empty. Seconds, and no McpLevelCache slot. Any thread.
        /// </summary>
        internal static Commands ReadScript(string path)
        {
            string none = Path.Combine(Path.GetTempPath(), "OpenCAGE_no_file_" + Guid.NewGuid().ToString("N"));
            Commands commands = new Commands(path, new EnvironmentAnimations(none, null), new CollisionMaps(none, null, null), new RenderableElements(none, null, null));
            if (!commands.Loaded)
                throw new McpError(McpErrorCodes.Failed, "The script at " + path + " could not be read.");
            return commands;
        }

        private static object FindInLevels(McpCall call)
        {
            string wanted = call.Str("composite");
            string functionType = call.Str("function_type");
            string entityName = call.Str("entity_name");
            string parameter = call.Str("parameter");
            bool entitySearch = functionType != null || entityName != null || parameter != null;
            if ((wanted == null) == !entitySearch)
                throw McpError.Invalid(wanted == null ? "Give 'composite' (a path, id or words), or function_type / entity_name / parameter to search entities." : "Give 'composite' or an entity search (function_type / entity_name / parameter), not both.");
            if (call.Has("value") && parameter == null)
                throw McpError.Invalid("'value' goes with 'parameter'.");
            List<string> levels = call.Has("levels") ? call.StrList("levels").Select(o => McpLevels.Resolve(o, allowFrontend: true, call: call, argument: "levels")).Distinct().ToList()
                : Level.GetLevels(Singleton.PathToAI).OrderBy(o => o, StringComparer.OrdinalIgnoreCase).ToList();
            JObject result = new JObject() { ["levels_searched"] = levels.Count };
            JArray rows = new JArray();
            List<string> failed = new List<string>();

            if (!entitySearch)
            {
                ShortGuid? id = McpScript.ParseId(wanted);
                string path = McpScript.NormalisePath(wanted);
                string[] terms = wanted.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int present = 0, placed = 0;
                for (int i = 0; i < levels.Count; i++)
                {
                    call.ThrowIfCancelled();
                    call.Progress("Reading " + levels[i] + "'s script index", i, levels.Count);
                    List<CompositeIndexEntry> index;
                    try { index = CompositeIndexCache.Get(levels[i]); }
                    catch (Exception e) { failed.Add(levels[i] + ": " + e.Message); continue; }
                    //By id; else the whole path or its end; else every word of the path
                    List<CompositeIndexEntry> hits = id != null ? index.Where(o => o.ID == id.Value).ToList() : new List<CompositeIndexEntry>();
                    if (hits.Count == 0) hits = index.Where(o => string.Equals(McpScript.NormalisePath(o.Name), path, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (hits.Count == 0) hits = index.Where(o => McpScript.NormalisePath(o.Name).EndsWith("\\" + path, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (hits.Count == 0) hits = index.Where(o => terms.All(t => (o.Name ?? "").IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                    if (hits.Count == 0) continue;
                    IndexGraph graph = new IndexGraph(index);
                    bool anyPlaced = hits.Any(o => graph.Placed.Contains(o.ID));
                    if (call.Bool("placed_only") && !anyPlaced) continue;
                    present++;
                    if (anyPlaced) placed++;
                    JObject row = new JObject() { ["level"] = levels[i], ["placed"] = anyPlaced };
                    if (hits.Count == 1)
                    {
                        row["id"] = McpScript.Id(hits[0].ID);
                        row["path"] = hits[0].Name;
                        if (hits[0].IsRoot) row["is_root"] = true;
                        List<string> by = graph.PlacedBy(hits[0].ID).Take(4).ToList();
                        if (by.Count != 0) row["placed_by"] = new JArray(by);
                    }
                    else
                        row["matches"] = new JArray(hits.Take(6).Select(o => new JObject() { ["id"] = McpScript.Id(o.ID), ["path"] = o.Name, ["placed"] = graph.Placed.Contains(o.ID) }));
                    rows.Add(row);
                }
                result["levels_with_it"] = present;
                result["levels_placing_it"] = placed;
                result["levels"] = rows;
                if (present == 0)
                    call.Note("No level searched has a composite matching '" + wanted + "'. search_level with 'composites' (words) browses one level's paths.");
            }
            else
            {
                FunctionType? type = null;
                if (functionType != null)
                {
                    if (!Enum.TryParse(functionType.Trim(), true, out FunctionType parsed) || !Enum.IsDefined(typeof(FunctionType), parsed))
                        throw McpError.NotFound("function type", functionType, Enum.GetNames(typeof(FunctionType)), "If it is a composite, search with 'composite' instead.");
                    type = parsed;
                }
                ShortGuid? parameterId = parameter == null ? (ShortGuid?)null : McpScript.ParamId(parameter);
                JToken value = call.Token("value");
                int limit = Math.Max(1, Math.Min(50, call.Int("limit", 5)));
                int total = 0;
                for (int i = 0; i < levels.Count; i++)
                {
                    call.ThrowIfCancelled();
                    call.Progress("Searching " + levels[i] + " (" + (i + 1) + " of " + levels.Count + ")", i, levels.Count);
                    Commands commands;
                    try { commands = ReadScript(new Level(Singleton.PathToAI + "/DATA/ENV/" + levels[i], Singleton.Global, false).CommandsFilepath); }
                    catch (Exception e) { failed.Add(levels[i] + ": " + e.Message); continue; }
                    int count = 0;
                    JArray first = new JArray();
                    foreach (Composite composite in commands.Entries)
                    {
                        if (composite == null) continue;
                        foreach (Entity entity in composite.GetEntities())
                        {
                            if (type != null && !(entity is FunctionEntity f && f.function == type.Value)) continue;
                            if (entityName != null && McpScript.EntityName(commands, composite, entity).IndexOf(entityName, StringComparison.OrdinalIgnoreCase) < 0) continue;
                            JToken found = null;
                            if (parameterId != null)
                            {
                                ParameterData data = entity.GetParameter(parameterId.Value)?.content;
                                if (data == null) continue;
                                try { found = McpValues.ToJson(data, commands); } catch { found = data.dataType.ToString(); }
                                if (value != null && !ValueMatches(found, value)) continue;
                            }
                            count++;
                            if (first.Count >= limit) continue;
                            JObject hit = new JObject()
                            {
                                ["composite"] = composite.name,
                                ["entity"] = McpScript.EntityName(commands, composite, entity),
                                ["id"] = McpScript.Id(entity.shortGUID),
                                ["type"] = McpScript.TypeName(commands, composite, entity),
                            };
                            if (found != null) hit["value"] = found;
                            first.Add(hit);
                        }
                    }
                    commands = null;
                    if (count == 0) continue;
                    total += count;
                    rows.Add(new JObject() { ["level"] = levels[i], ["count"] = count, ["first"] = first });
                }
                result["total"] = total;
                result["levels"] = rows;
                GC.Collect();
                if (total == 0)
                    call.Note("No entity matches in the levels searched.");
            }
            if (failed.Count != 0)
                result["could_not_read"] = new JArray(failed.Take(20));
            return result;
        }

        /// <summary>A parameter value (as JSON) matches what find_in_levels' value asks for: text contained, or equal.</summary>
        private static bool ValueMatches(JToken found, JToken wanted)
        {
            if (found == null) return false;
            if (wanted.Type == JTokenType.String && found.Type == JTokenType.String)
                return ((string)found).IndexOf((string)wanted, StringComparison.OrdinalIgnoreCase) >= 0;
            if (wanted.Type == JTokenType.String)
                return found.ToString(Newtonsoft.Json.Formatting.None).IndexOf((string)wanted, StringComparison.OrdinalIgnoreCase) >= 0;
            return JToken.DeepEquals(found, wanted) || (found.Type == JTokenType.Float || found.Type == JTokenType.Integer) && (wanted.Type == JTokenType.Float || wanted.Type == JTokenType.Integer) && Math.Abs((double)found - (double)wanted) < 1e-4;
        }

        private static object DescribeLevelComposite(McpCall call)
        {
            Level source = McpLevelCache.Get(call, Normalise(call.Str("level", required: true)));
            Commands commands = source.Commands;
            Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
            JObject result;
            if (call.Has("entity"))
            {
                if (call.Has("filter") || call.Has("links"))
                    throw McpError.Invalid("'entity' describes one entity in full: leave out filter and links.");
                Entity entity = McpScript.FindEntity(commands, composite, call.Str("entity"));
                result = McpScript.Describe(commands, composite, entity, defaults: true);
                result["composite"] = composite.name;
                result["level"] = source.Name;
                //Links into it from the other entities of its composite
                JArray into = new JArray();
                foreach (Entity other in composite.GetEntities())
                    foreach (EntityConnector link in other.childLinks)
                        if (link.linkedEntityID == entity.shortGUID && into.Count < 100)
                            into.Add(McpScript.EntityName(commands, composite, other) + "." + McpScript.ParamName(link.thisParamID) + " -> " + McpScript.ParamName(link.linkedParamID));
                if (into.Count != 0 && result["links_in"] == null) result["links_in"] = into;
            }
            else
            {
                string filter = call.Str("filter");
                bool links = call.Bool("links");
                result = McpScript.CompositeSummary(commands, composite);
                result["level"] = source.Name;
                result["entity_count"] = composite.GetEntities().Count;
                List<Entity> listed = composite.GetEntities().Where(o => !(o is VariableEntity) && (filter == null
                    || McpScript.EntityName(commands, composite, o).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || McpScript.TypeName(commands, composite, o).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                McpPaging.Page(call, listed, result, "entities", o =>
                {
                    JObject brief = McpScript.Brief(commands, composite, o);
                    if (links && o.childLinks.Count != 0)
                        brief["links"] = new JArray(o.childLinks.Take(40).Select(l =>
                        {
                            Entity target = composite.GetEntityByID(l.linkedEntityID);
                            return McpScript.ParamName(l.thisParamID) + " -> " + (target != null ? McpScript.EntityName(commands, composite, target) : McpScript.Id(l.linkedEntityID)) + "." + McpScript.ParamName(l.linkedParamID);
                        }));
                    return brief;
                }, 200);
                result["variables"] = new JArray(composite.variables.Select(o => McpScript.EntityName(commands, composite, o) + " (" + McpScript.TypeName(commands, composite, o) + ")"));
                //The composites it places directly (each once); what those place in turn is theirs to describe
                result["instances_of"] = new JArray(composite.functions.Where(o => !o.function.IsFunctionType).Select(o => commands.GetComposite(o.function))
                    .Where(o => o != null && o != composite).Distinct().Select(o => o.name).Take(100));
            }
            result["placed_in"] = new JArray(McpBrowseTools.PlacedIn(commands, composite).Take(20));
            if (call.Bool("placements") && !call.Has("entity"))
            {
                Composite root = commands.EntryPoints[0];
                List<McpPlacements.Placement> found = new List<McpPlacements.Placement>();
                int total = composite == root ? 0 : new McpPlacements(commands) { PlaceUnpositioned = true }.PlacementsOf(root, composite, null, found, 50, call.Cancel, realOnly: true);
                result["placements"] = new JArray(found.Select(o => { JObject placed = McpEditingTools.DescribePlacement(commands, root, o); placed["space"] = "world"; return placed; }));
                result["placement_count"] = total;
                if (composite == root) call.Note(composite.name + " is the level's root: it is not placed, it is the world.");
                else if (total == 0) call.Note(composite.name + " is not placed in " + source.Name + " (or only as a template, deleted or shared repeat).");
                call.Note("port_composites place.like_source takes a placement's 'path' to put a ported copy where it sits there.");
            }
            return result;
        }
        #endregion

        #region Port
        //GLOBAL and PAUSEMENU are the same in every level and are the destination's own: never replaced by a port
        internal static readonly ShortGuid GlobalId = new ShortGuid("1D-2E-CE-E5");
        internal static readonly ShortGuid PauseMenuId = new ShortGuid("FE-7B-FE-B3");

        private static object PortComposites(McpCall call)
        {
            string level = Normalise(call.Str("level", required: true));
            bool children = call.Bool("include_children", true);
            bool displayModels = call.Bool("include_display_models", true);
            bool overwriteComposites = call.Bool("overwrite_composites");
            bool overwriteAssets = call.Bool("overwrite_assets");

            string openLevel = McpEditor.UI(() => McpEditor.RequireLevel().Level.Name);
            if (string.Equals(openLevel, level, StringComparison.OrdinalIgnoreCase))
                throw new McpError(level + " is the level that is open: its composites are already here.");

            Level source = McpLevelCache.Get(call, level);
            Commands sourceCommands = source.Commands;
            List<Composite> roots = call.StrList("composites", required: true).Select(o => McpScript.FindComposite(sourceCommands, o)).Distinct().ToList();
            //GLOBAL and PAUSEMENU are the destination's own. Another level's root can be ported (it arrives as an ordinary
            //composite), but never over one of this level's entry points.
            List<ShortGuid> ownEntryPoints = McpEditor.UI(() => McpEditor.RequireCommands().EntryPoints.Where(o => o != null).Select(o => o.shortGUID).ToList());
            foreach (Composite root in roots)
            {
                if (root.shortGUID == GlobalId || root.shortGUID == PauseMenuId)
                    throw new McpError(root.name + " is GLOBAL or PAUSEMENU, which every level has its own copy of, and cannot be ported. Port the composites it places instead.");
                if (overwriteComposites && ownEntryPoints.Contains(root.shortGUID))
                    throw new McpError(root.name + " has the same id as one of this level's entry points: overwrite_composites would replace it. Port it without overwrite_composites, or port the composites it places instead.");
            }

            //Characters name their display model rather than placing it, so a port does not follow it by itself
            List<string> addedDisplayModels = new List<string>();
            Composite toPlace = roots[0];
            if (displayModels)
            {
                HashSet<ShortGuid> have = McpEditor.UI(() => new HashSet<ShortGuid>(McpEditor.RequireCommands().Entries.Where(o => o != null).Select(o => o.shortGUID)));
                foreach (Composite displayModel in DisplayModelsFor(sourceCommands, roots, children, have, overwriteComposites))
                {
                    if (roots.Contains(displayModel)) continue;
                    roots.Add(displayModel);
                    addedDisplayModels.Add(displayModel.name);
                }
            }

            //Where an instance goes afterwards, checked before anything is ported
            JObject placeSpec = call.Object("place");
            PlacePlan placement = placeSpec == null ? null : PlanPlacement(call, placeSpec, sourceCommands, roots, toPlace);
            if (call.Bool("carry_overrides") && placement?.SourceChain == null)
                throw McpError.Invalid("carry_overrides needs place.like_source: the source placement whose parameters and overrides to bring.");

            if (call.Bool("dry_run"))
                return PortPlan(call, source, sourceCommands, roots, children, overwriteComposites, overwriteAssets, addedDisplayModels, placement);

            CompositeFlowgraphTable sourceLayouts = CustomTable.ReadTable(sourceCommands.Filepath, CustomTableType.COMPOSITE_FLOWGRAPHS) as CompositeFlowgraphTable;
            CompositeParameterModificationTable sourceModifications = CustomTable.ReadTable(sourceCommands.Filepath, CustomTableType.COMPOSITE_PARAMETER_MODIFICATION) as CompositeParameterModificationTable;
            EntityAppliedDefaultsTable sourceDefaults = CustomTable.ReadTable(sourceCommands.Filepath, CustomTableType.ENTITY_APPLIED_DEFAULTS) as EntityAppliedDefaultsTable;
            CompositePreviewTable sourcePreviews = CustomTable.ReadTable(sourceCommands.Filepath, CustomTableType.COMPOSITE_PREVIEWS) as CompositePreviewTable;

            //The last point a client that gave up (loading the source level can take a while) stops it with nothing changed:
            //once it starts, a port runs to the end, since one stopped part way leaves some composites and not others
            call.ThrowIfCancelled();
            call.Progress("Porting " + roots.Count + " composite(s) from " + level);
            JObject result = null;
            using (McpEditorTools.Heartbeat(call, "Porting from " + level))
            {
                result = McpEditor.UI(() =>
                {
                    LevelContent content = McpEditor.RequireLevel();
                    Level destination = content.Level;
                    //Where the user is, to put them back if the port closes the view (stepped down as they were, the same selection)
                    CompositePath.Place shown = Singleton.Editor.CompositeDisplay?.CapturePlace();
                    McpEditor.RequireUndoIdle();
                    Dictionary<ShortGuid, Composite> existing = destination.Commands.Entries.Where(o => o != null).GroupBy(o => o.shortGUID).ToDictionary(o => o.Key, o => o.First());
                    List<Composite> ported = new List<Composite>();
                    List<string> kept = new List<string>();
                    List<string> replacedNames = new List<string>();
                    CompositePorter porter = null;

                    Send.BeginSceneBatch();
                    try
                    {
                        if (overwriteComposites && Closure(sourceCommands, roots, children).Any(o => existing.ContainsKey(o.shortGUID)))
                            Singleton.Editor.CompositeBrowser.CloseAllChildTabs();
                        Singleton.OnCompositeAddPending?.Invoke();

                        porter = new CompositePorter(source, destination)
                        {
                            OverwriteComposites = overwriteComposites,
                            OverwriteAssets = overwriteAssets,
                            Recurse = children,
                        };
                        porter.DoNotDescendInto.Add(GlobalId);
                        porter.DoNotDescendInto.Add(PauseMenuId);
                        if (destination.Commands.EntryPoints[1] != null) porter.DoNotDescendInto.Add(destination.Commands.EntryPoints[1].shortGUID);
                        if (destination.Commands.EntryPoints[2] != null) porter.DoNotDescendInto.Add(destination.Commands.EntryPoints[2].shortGUID);
                        porter.OnCompositePorted = (original, copy) =>
                        {
                            ported.Add(copy);
                            if (existing.TryGetValue(copy.shortGUID, out Composite replaced) && !ReferenceEquals(replaced, copy))
                            {
                                replacedNames.Add(replaced.name);
                                Singleton.OnCompositeDeleted?.Invoke(replaced);
                            }
                            //The source level's own preview of it shows what arrives, so it comes too rather than being retaken
                            CompositePreviewManager.CarryPreview(copy.shortGUID, sourcePreviews);
                            Singleton.OnCompositeAdded?.Invoke(copy);
                            FlowgraphLayoutManager.ImportLayouts(copy, FlowgraphLayoutManager.GetLayoutsForPort(original, sourceLayouts, FlowgraphLayoutManager.BundledLevelName(sourceCommands, level), sourceCommands));
                            ParameterModificationTracker.ImportCompositeRows(copy.shortGUID, sourceModifications, sourceDefaults);
                        };
                        foreach (Composite root in roots)
                        {
                            if (!overwriteComposites && existing.ContainsKey(root.shortGUID))
                                kept.Add(root.name);
                            porter.Port(root);
                        }
                    }
                    catch (Exception e)
                    {
                        //What arrived before the failure stays (a port is not undoable): say so, and put the editor back in order
                        ForgetReplacedHistory(call, replacedNames);
                        bool putBackAfterFailure = Singleton.Editor.CompositeDisplay?.Composite == null && shown != null;
                        try
                        {
                            //Inside the batch, as below
                            if (putBackAfterFailure)
                                CompositeImporter.ReopenClosedPlace(shown);
                        }
                        finally
                        {
                            Send.EndSceneBatch();
                        }
                        ViewerResourceSync.SyncImmediately();
                        if (!putBackAfterFailure)
                            Singleton.Editor.CompositeBrowser?.RefreshList();
                        throw new McpError("Porting from " + level + " failed part way: " + e.Message + " " + ported.Count + " composite(s) had already been added" +
                            (ported.Count != 0 ? " (" + string.Join(", ", ported.Take(10).Select(o => o.name)) + (ported.Count > 10 ? ", ..." : "") + ")" : "") +
                            ". A port cannot be undone: to drop them, load_level with discard_unsaved: true (losing any other unsaved changes).");
                    }
                    //Opened, or put back where the user was, inside the batch, as the import windows do it: the rebuild that this asks
                    //the viewer for is then the batch's only one. Ended with nothing open, the batch had the viewer rebuild the old scene first.
                    bool open = call.Bool("open") && ported.Count != 0;
                    bool putBack = !open && Singleton.Editor.CompositeDisplay?.Composite == null && shown != null;
                    try
                    {
                        if (open)
                            CompositeImporter.OpenPortedComposite(ported[0]);
                        else if (putBack)
                            CompositeImporter.ReopenClosedPlace(shown);
                    }
                    finally
                    {
                        Send.EndSceneBatch();
                    }
                    ForgetReplacedHistory(call, replacedNames);

                    ViewerResourceSync.SyncImmediately();
                    if (!open && !putBack)
                        Singleton.Editor.CompositeBrowser?.RefreshList();

                    DeadProxyReport dead = DeadProxyReport.Of(destination.Commands, ported);
                    JObject summary = new JObject()
                    {
                        ["from"] = level,
                        ["ported"] = new JArray(ported.Take(200).Select(o => McpScript.CompositeSummary(destination.Commands, o))),
                        ["ported_count"] = ported.Count,
                        ["already_here"] = new JArray(kept),
                        ["models"] = porter.RenderablesPorted,
                        ["collision"] = porter.CollisionMappingsPorted,
                        ["physics_systems"] = porter.PhysicsSystemsPorted,
                        ["animated_models"] = porter.AnimatedModelsPorted,
                    };
                    if (addedDisplayModels.Count != 0) summary["display_models_added"] = new JArray(addedDisplayModels);
                    if (replacedNames.Count != 0) summary["replaced"] = new JArray(replacedNames.Take(200));
                    if (dead.Any) summary["dead_proxies"] = dead.Describe("this level");
                    return summary;
                });
            }
            if (placement != null)
            {
                try { result["placed"] = Place(call, placement, sourceCommands, call.Bool("carry_overrides"), source.AccessorySets); }
                catch (McpError e)
                {
                    throw new McpError(e.Code, "The composites were ported (" + result["ported_count"] + "; that cannot be undone), but placing " + placement.Source.name + " failed: " + e.Message + " Place it with create_entities, or call port_composites again with the same place (what is already here is kept).");
                }
                call.Note("Porting cannot be undone; the placement is one undo step. Nothing is on disk until save_level (with build=true: placed geometry needs a build).");
            }
            else
                call.Note("Ported composites are not placed anywhere yet: pass 'place' (where, or like_source), or add instances with create_entities ({\"composite\": \"<path>\", \"position\": [x,y,z]}). Porting cannot be undone; nothing is on disk until save_level.");
            return result;
        }

        /// <summary>
        /// The DisplayModel composites the Characters among <paramref name="roots"/> (and, with children, what they place) wear:
        /// Characters name their display model rather than placing it, so a port does not follow it by itself. The player's is
        /// "PLAYER_FP", which means whichever of GLOBAL's suits is worn: those models come with it. Leaves out what
        /// <paramref name="have"/> holds unless <paramref name="overwrite"/>.
        /// </summary>
        internal static List<Composite> DisplayModelsFor(Commands source, List<Composite> roots, bool children, ICollection<ShortGuid> have, bool overwrite)
        {
            HashSet<string> models = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool player = false;
            foreach (Composite composite in Closure(source, roots, children).ToList())
            {
                foreach (FunctionEntity entity in composite.functions)
                {
                    string model = (entity.GetParameter("display_model")?.content as cString)?.value;
                    if (!string.IsNullOrWhiteSpace(model)) models.Add(model.Trim());
                    if (entity.function == FunctionType.Character && (entity.GetParameter("is_player")?.content as cBool)?.value == true) player = true;
                }
            }
            if (player || models.Contains("PLAYER_FP"))
                foreach (FunctionEntity entity in source.EntryPoints[1]?.functions ?? Enumerable.Empty<FunctionEntity>())
                {
                    string model = (entity.GetParameter("display_model")?.content as cString)?.value;
                    if (!string.IsNullOrWhiteSpace(model)) models.Add(model.Trim());
                }
            List<Composite> found = new List<Composite>();
            foreach (string model in models)
            {
                Composite displayModel = source.Entries.FirstOrDefault(o => o != null && string.Equals(o.name, "DisplayModel:" + model, StringComparison.OrdinalIgnoreCase));
                if (displayModel == null || roots.Contains(displayModel) || found.Contains(displayModel) || (have.Contains(displayModel.shortGUID) && !overwrite)) continue;
                found.Add(displayModel);
            }
            return found;
        }

        /// <summary>port_composites' place, worked out before the port.</summary>
        private sealed class PlacePlan
        {
            public Composite Source;
            public string Into;
            public int? IntoPlacement;
            public cTransform Local;
            public cTransform World;
            public List<Entity> SourceChain;
            public string Name;
        }

        private static PlacePlan PlanPlacement(McpCall call, JObject spec, Commands source, List<Composite> roots, Composite first)
        {
            PlacePlan plan = new PlacePlan() { Into = spec["into"]?.Type == JTokenType.String ? (string)spec["into"] : "root", Name = spec["name"]?.Type == JTokenType.String ? (string)spec["name"] : null };
            if (spec["placement"] != null && spec["placement"].Type != JTokenType.Null)
                plan.IntoPlacement = spec["placement"].Value<int>();
            HashSet<Composite> ported = Closure(source, roots, call.Bool("include_children", true));
            Composite named = spec["composite"]?.Type == JTokenType.String ? McpScript.FindComposite(source, (string)spec["composite"]) : null;
            JToken like = spec["like_source"];
            bool given = spec["position"] != null || spec["rotation"] != null;
            if (like != null && given)
                throw McpError.Invalid("place takes like_source (where it sits in the source level) or position/rotation, not both.");
            if (like != null)
            {
                if (like is JArray none && none.Count == 0)
                    throw McpError.Invalid("place.like_source is empty: give the instances from the source level's root down to the placement (describe_level_composite placements lists them).");
                //Any shape a path is given in: an array, 'A/B', or the {path, ids} describe_level_composite gives a placement in
                List<string> steps = McpScript.PathSteps(like, "place.like_source");
                List<Entity> chain = new List<Entity>();
                Composite root = source.EntryPoints[0];
                Composite end = McpEditorTools.ResolveInstancePath(source, root, steps, chain);
                if (named != null && named != end)
                    throw McpError.Invalid("place.like_source leads to an instance of " + end.name + ", not " + named.name + ".");
                plan.Source = end;
                plan.SourceChain = chain;
                plan.World = new McpPlacements(source) { PlaceUnpositioned = true }.Evaluate(root, chain).World ?? new cTransform(Vector3.Zero, Vector3.Zero);
                if (plan.Name == null)
                    plan.Name = McpScript.EntityName(source, chain.Count == 1 ? root : McpScript.InstancedComposite(source, chain[chain.Count - 2]), chain[chain.Count - 1]);
            }
            else
            {
                if (!given)
                    throw McpError.Invalid("place needs position (and rotation) in 'into's space, or like_source to put it where it sits in the source level.");
                plan.Source = named ?? first;
                plan.Local = new cTransform(spec["position"] != null ? McpValues.ReadVector(spec["position"], "place.position", null) : Vector3.Zero,
                    spec["rotation"] != null ? McpValues.ReadVector(spec["rotation"], "place.rotation", null) : Vector3.Zero);
            }
            if (!ported.Contains(plan.Source))
                throw McpError.Invalid(plan.Source.name + " is not among what this call ports: add it to 'composites' to place it.");
            return plan;
        }

        /// <summary>Place one instance of a ported composite (one undo step), carrying the source placement's overrides when asked.</summary>
        private static JObject Place(McpCall call, PlacePlan plan, Commands source, bool carry, CharacterAccessorySets sourceLooks)
        {
            JObject result = new JObject();
            List<string> skipped = new List<string>();
            int parametersCopied = 0, aliasesMade = 0;
            McpScriptEdit.Outcome outcome = null;
            Composite into = null, target = null;
            cTransform local = plan.Local;
            JObject frame = null;
            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                into = McpScript.FindComposite(commands, plan.Into);
                target = commands.GetComposite(plan.Source.shortGUID) ?? throw new McpError(McpErrorCodes.NotFound, plan.Source.name + " is not in the open level after the port.");
                if (plan.World != null)
                    local = InstanceTransform.ToLocal(McpSpatialTools.FrameOf(commands, into, plan.IntoPlacement, "'place.placement'", out frame), plan.World);
            });
            Entity sourceInstance = plan.SourceChain?.LastOrDefault();
            Composite sourceRoot = source.EntryPoints[0];
            Composite sourceParent = plan.SourceChain == null ? null : plan.SourceChain.Count == 1 ? sourceRoot : McpScript.InstancedComposite(source, plan.SourceChain[plan.SourceChain.Count - 2]);
            FunctionEntity made = null;
            outcome = McpScriptEdit.Run("Place " + target.name, into, edit =>
            {
                made = edit.AddInstance(into, target, plan.Name);
                edit.SetParameter(into, made, "position", new JObject() { ["position"] = McpValues.Vector(local.position), ["rotation"] = McpValues.Vector(local.rotation) });
                if (!carry || sourceInstance == null) return;

                //Its own parameters there (not where it is: that is worked out above)
                Dictionary<ShortGuid, ParameterData> own = sourceInstance.parameters.Where(o => o?.content != null).GroupBy(o => o.name).ToDictionary(o => o.Key, o => o.First().content);
                //What its parents override inside it: aliases whose path runs through it, inner first so the outer ones win
                Dictionary<string, KeyValuePair<List<ShortGuid>, Dictionary<ShortGuid, ParameterData>>> inside = new Dictionary<string, KeyValuePair<List<ShortGuid>, Dictionary<ShortGuid, ParameterData>>>();
                Dictionary<string, string> aliasNames = new Dictionary<string, string>();
                List<Entity> chain = plan.SourceChain;
                for (int k = chain.Count - 1; k >= 0; k--)
                {
                    Composite owner = k == 0 ? sourceRoot : McpScript.InstancedComposite(source, chain[k - 1]);
                    if (owner == null) continue;
                    List<ShortGuid> prefix = chain.Skip(k).Select(o => o.shortGUID).ToList();
                    foreach (AliasEntity alias in owner.aliases)
                    {
                        List<ShortGuid> path = (alias.alias?.path ?? new ShortGuid[0]).Where(o => o != ShortGuid.Invalid).ToList();
                        if (path.Count < prefix.Count || !path.Take(prefix.Count).SequenceEqual(prefix)) continue;
                        List<ShortGuid> rest = path.Skip(prefix.Count).ToList();
                        Dictionary<ShortGuid, ParameterData> into_;
                        if (rest.Count == 0)
                            into_ = own;
                        else
                        {
                            string key = string.Join("/", rest.Select(o => o.AsUInt32));
                            if (!inside.TryGetValue(key, out KeyValuePair<List<ShortGuid>, Dictionary<ShortGuid, ParameterData>> entry))
                                inside[key] = entry = new KeyValuePair<List<ShortGuid>, Dictionary<ShortGuid, ParameterData>>(rest, new Dictionary<ShortGuid, ParameterData>());
                            into_ = entry.Value;
                            aliasNames[key] = McpScript.EntityName(source, owner, alias);
                        }
                        foreach (Parameter parameter in alias.parameters)
                            if (parameter?.content != null) into_[parameter.name] = parameter.content;
                    }
                }
                foreach (KeyValuePair<ShortGuid, ParameterData> parameter in own)
                {
                    string name = McpScript.ParamName(parameter.Key);
                    if (name == "position" || name == "name") continue;
                    if (Copy(edit, into, made, name, parameter.Value, source, skipped)) parametersCopied++;
                }
                foreach (KeyValuePair<string, KeyValuePair<List<ShortGuid>, Dictionary<ShortGuid, ParameterData>>> entry in inside)
                {
                    ShortGuid[] path = new[] { made.shortGUID }.Concat(entry.Value.Key).Concat(new[] { ShortGuid.Invalid }).ToArray();
                    AliasEntity alias = edit.AddAlias(into, path, aliasNames.TryGetValue(entry.Key, out string aliasName) ? aliasName : null);
                    aliasesMade++;
                    foreach (KeyValuePair<ShortGuid, ParameterData> parameter in entry.Value.Value)
                        if (Copy(edit, into, alias, McpScript.ParamName(parameter.Key), parameter.Value, source, skipped)) parametersCopied++;
                }
            });

            result["composite"] = target.name;
            result["into"] = into.name;
            result["instance"] = new JObject() { ["id"] = McpScript.Id(made.shortGUID), ["name"] = McpEditor.UI(() => McpScript.EntityName(McpEditor.RequireCommands(false), into, made)) };
            result["position"] = McpValues.Vector(local.position);
            result["rotation"] = McpValues.Vector(local.rotation);
            result["space"] = into == McpEditor.UI(() => McpEditor.RequireCommands(false).EntryPoints[0]) ? "world" : "composite";
            if (plan.World != null)
            {
                result["source_world"] = new JObject() { ["position"] = McpValues.Vector(plan.World.position), ["rotation"] = McpValues.Vector(plan.World.rotation), ["space"] = "world" };
                if (frame != null && frame["placement"] != null) result["into_placement"] = frame["placement"];
            }
            if (outcome != null) result["undo_step"] = "AI: " + outcome.Label;
            if (carry)
            {
                JObject carried = new JObject() { ["parameters"] = parametersCopied, ["aliases"] = aliasesMade };
                if (skipped.Count != 0) carried["skipped"] = new JArray(skipped.Distinct().Take(30));
                result["carried"] = carried;
            }
            if (sourceInstance != null && sourceParent != null)
            {
                //What stays behind: the source placement's links to and from the rest of its level, and its zones
                JArray links = new JArray();
                foreach (EntityConnector link in sourceInstance.childLinks)
                {
                    Entity other = sourceParent.GetEntityByID(link.linkedEntityID);
                    links.Add(McpScript.EntityName(source, sourceParent, sourceInstance) + "." + McpScript.ParamName(link.thisParamID) + " -> " + (other != null ? McpScript.EntityName(source, sourceParent, other) : McpScript.Id(link.linkedEntityID)) + "." + McpScript.ParamName(link.linkedParamID));
                }
                foreach (Entity other in sourceParent.GetEntities())
                    foreach (EntityConnector link in other.childLinks)
                        if (link.linkedEntityID == sourceInstance.shortGUID)
                            links.Add(McpScript.EntityName(source, sourceParent, other) + "." + McpScript.ParamName(link.thisParamID) + " -> " + McpScript.EntityName(source, sourceParent, sourceInstance) + "." + McpScript.ParamName(link.linkedParamID));
                JObject left = new JObject() { ["zones"] = "it is in no zone here: create_zone or auto_zone claims it" };
                if (links.Count != 0) left["links_in_source"] = new JArray(links.Take(40));
                //Characters' looks are the source level's, keyed by where each was placed there: listed, to be set again here
                bool intoRoot = McpEditor.UI(() => into == McpEditor.RequireCommands(false).EntryPoints[0]);
                JArray looks = SourceAppearances(source, sourceLooks, plan.SourceChain, made, McpEditor.UI(() => McpScript.EntityName(McpEditor.RequireCommands(false), into, made)), intoRoot);
                if (looks.Count != 0)
                {
                    left["appearances"] = looks;
                    call.Note("Characters' looks (accessory sets) are stored per level and keyed by where each Character is placed, so the port did not bring them: " +
                        "not_carried.appearances lists the " + looks.Count + " the source placement had. set_character_appearance with each one's " + (intoRoot ? "path" : "composite and placement") + " and its components, accessory_indexes and attributes sets it again here (its component composites must be in this level: port them if they are not).");
                }
                result["not_carried"] = left;
            }
            return result;
        }

        /// <summary>
        /// The accessory sets the source level gives the Characters inside a placement (<paramref name="sourceChain"/>, from its root
        /// to the placed instance), at any depth, each with where that Character now is under <paramref name="made"/> - the path from
        /// the root when the instance was placed there - and the set in the shape set_character_appearance takes. At most 40.
        /// </summary>
        private static JArray SourceAppearances(Commands source, CharacterAccessorySets sets, List<Entity> sourceChain, Entity made, string madeName, bool intoRoot)
        {
            JArray rows = new JArray();
            List<CharacterAccessorySets.CharacterAttributes> entries = sets?.Entries?.Where(o => o?.character != null).ToList();
            Composite placed = sourceChain == null || sourceChain.Count == 0 ? null : McpScript.InstancedComposite(source, sourceChain[sourceChain.Count - 1]);
            if (entries == null || entries.Count == 0 || placed == null)
                return rows;
            string[] parts = { "torso", "legs", "shoes", "head", "arms", "collision" };
            List<ShortGuid> outer = sourceChain.Select(o => o.shortGUID).ToList();

            void Walk(Composite composite, List<Entity> inner, List<string> names, int depth)
            {
                foreach (FunctionEntity entity in composite.functions)
                {
                    if (rows.Count >= 40) return;
                    if (entity.function == FunctionType.Character)
                    {
                        //The instance the Character runs in, as the game keys its set: the ids down to its composite's placement
                        ShortGuid[] path = outer.Concat(inner.Select(o => o.shortGUID)).Concat(new[] { entity.shortGUID, ShortGuid.Invalid }).ToArray();
                        ShortGuid instance = path.GenerateCompositeInstanceID();
                        List<CharacterAccessorySets.CharacterAttributes> inInstance = entries.Where(o => o.character.composite_instance_id == instance).ToList();
                        CharacterAccessorySets.CharacterAttributes set = inInstance.FirstOrDefault(o => o.character.entity_id == entity.shortGUID)
                            ?? inInstance.FirstOrDefault(o => !composite.functions.Any(f => f.shortGUID == o.character.entity_id && f.function == FunctionType.Character));
                        if (set == null) continue;
                        List<string> ids = new[] { McpScript.Id(made.shortGUID) }.Concat(inner.Select(o => McpScript.Id(o.shortGUID))).Concat(new[] { McpScript.Id(entity.shortGUID) }).ToList();
                        CharacterAccessorySets.CharacterAttributes.Components.Component[] components = { set.components.Torso, set.components.Legs, set.components.Shoes, set.components.Head, set.components.Arms, set.components.Collision };
                        JObject row = new JObject()
                        {
                            ["character"] = string.Join(" > ", new[] { madeName }.Concat(names).Concat(new[] { McpScript.EntityName(source, composite, entity) })),
                            ["composite"] = composite.name,
                            ["entity"] = McpScript.Id(entity.shortGUID),
                        };
                        if (intoRoot) row["path"] = new JArray(ids);
                        else row["ids_from_instance"] = new JArray(ids);
                        row["components"] = new JObject(parts.Select((o, i) => new JProperty(o, components[i].Composite == ShortGuid.Invalid ? JValue.CreateNull()
                            : (JToken)(source.GetComposite(components[i].Composite)?.name ?? McpScript.Id(components[i].Composite)))));
                        row["accessory_indexes"] = new JObject(parts.Select((o, i) => new JProperty(o, components[i].AccessoryIndex)));
                        row["attributes"] = new JObject()
                        {
                            ["gender_skeleton"] = set.gender_skeleton,
                            ["face_skeleton"] = set.face_skeleton,
                            ["asset_type"] = set.asset_type.ToString(),
                            ["voice_actor"] = set.voice_actor.ToString(),
                            ["gender"] = set.gender.ToString(),
                            ["ethnicity"] = set.ethnicity.ToString(),
                            ["build"] = set.build.ToString(),
                            ["foley_torso"] = set.foley.Torso.ToString(),
                            ["foley_leg"] = set.foley.Leg.ToString(),
                            ["foley_footwear"] = set.foley.Footwear.ToString(),
                        };
                        rows.Add(row);
                    }
                    else if (!entity.function.IsFunctionType && depth < 16)
                    {
                        Composite next = source.GetComposite(entity.function);
                        if (next == null) continue;
                        Walk(next, inner.Concat(new[] { (Entity)entity }).ToList(), names.Concat(new[] { McpScript.EntityName(source, composite, entity) }).ToList(), depth + 1);
                    }
                }
            }

            Walk(placed, new List<Entity>(), new List<string>(), 0);
            return rows;
        }

        /// <summary>One parameter value from the source level, written through the edit; false (and the name in skipped) when it cannot travel.</summary>
        private static bool Copy(McpScriptEdit edit, Composite composite, Entity entity, string name, ParameterData value, Commands source, List<string> skipped)
        {
            if (value is cResource)
            {
                skipped.Add(name + " (a resource)");
                return false;
            }
            try
            {
                edit.SetParameter(composite, entity, name, McpValues.ToJson(value, source), allowCustom: true);
                return true;
            }
            catch (McpError)
            {
                skipped.Add(name);
                return false;
            }
        }

        /// <summary>port_composites' dry run: what would come, from the loaded source level, without porting.</summary>
        private static JObject PortPlan(McpCall call, Level source, Commands sourceCommands, List<Composite> roots, bool children, bool overwriteComposites, bool overwriteAssets, List<string> displayModels, PlacePlan placement)
        {
            HashSet<Composite> closure = Closure(sourceCommands, roots, children);
            HashSet<ShortGuid> have = null;
            HashSet<string> models = null, materials = null;
            McpEditor.UI(() =>
            {
                Level open = McpEditor.RequireLevel(forEditing: false).Level;
                have = new HashSet<ShortGuid>(open.Commands.Entries.Where(o => o != null).Select(o => o.shortGUID));
                models = new HashSet<string>(open.Models.Entries.Where(o => o != null).Select(o => o.Name), StringComparer.OrdinalIgnoreCase);
                materials = new HashSet<string>(open.Materials.Entries.Where(o => o != null).Select(o => o.Name), StringComparer.OrdinalIgnoreCase);
            });
            //Which models and materials the closure draws with: the submesh's model, found once for the whole source
            Dictionary<object, string> modelOf = new Dictionary<object, string>();
            foreach (Models.CS2 model in source.Models.Entries)
                foreach (Models.CS2.Component component in model.Components)
                    foreach (Models.CS2.Component.LOD lod in component.LODs)
                        foreach (Models.CS2.Component.LOD.Submesh submesh in lod.Submeshes)
                            if (submesh != null && !modelOf.ContainsKey(submesh)) modelOf[submesh] = model.Name;
            HashSet<string> usedModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase), usedMaterials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Composite composite in closure)
                foreach (FunctionEntity entity in composite.functions)
                    foreach (ResourceReference resource in entity.resources)
                        foreach (RenderableElements.Element element in resource?.RenderableInstance ?? new List<RenderableElements.Element>())
                        {
                            if (element?.Model != null && modelOf.TryGetValue(element.Model, out string modelName)) usedModels.Add(modelName);
                            if (element?.Material != null) usedMaterials.Add(element.Material.Name);
                        }

            List<string> fresh = closure.Where(o => !have.Contains(o.shortGUID)).Select(o => o.name).OrderBy(o => o).ToList();
            List<string> here = closure.Where(o => have.Contains(o.shortGUID)).Select(o => o.name).OrderBy(o => o).ToList();
            List<string> modelClash = usedModels.Where(models.Contains).OrderBy(o => o).ToList();
            List<string> materialClash = usedMaterials.Where(materials.Contains).OrderBy(o => o).ToList();
            JObject plan = new JObject()
            {
                ["dry_run"] = true,
                ["from"] = source.Name,
                ["composites"] = closure.Count,
                ["new"] = new JArray(fresh.Take(150)),
                [overwriteComposites ? "would_replace" : "already_here_kept"] = new JArray(here.Take(150)),
                ["models"] = usedModels.Count,
                ["materials"] = usedMaterials.Count,
            };
            if (displayModels.Count != 0) plan["display_models_added"] = new JArray(displayModels);
            if (modelClash.Count != 0) plan[overwriteAssets ? "models_replaced" : "models_already_here_used_instead"] = new JArray(modelClash.Take(100));
            if (materialClash.Count != 0) plan[overwriteAssets ? "materials_replaced" : "materials_already_here_used_instead"] = new JArray(materialClash.Take(100));
            if (placement != null)
            {
                JObject place = new JObject() { ["composite"] = placement.Source.name, ["into"] = placement.Into };
                if (placement.World != null) place["source_world"] = new JObject() { ["position"] = McpValues.Vector(placement.World.position), ["rotation"] = McpValues.Vector(placement.World.rotation), ["space"] = "world" };
                if (placement.Local != null) place["position"] = McpValues.Vector(placement.Local.position);
                plan["would_place"] = place;
            }
            if (overwriteComposites && here.Count != 0)
                call.Note("overwrite_composites would replace " + here.Count + " composite(s) and clear the undo history.");
            call.Note("Textures are not listed: they come with the materials that use them.");
            return plan;
        }

        /// <summary>
        /// The undo history's steps hold composites and entities themselves. Once a port has swapped a composite for a
        /// copy, undoing an earlier step would rewrite the detached original while the editor shows the copy - and report
        /// success. So a port that replaced anything drops the history, as a level reload would. UI thread.
        /// </summary>
        internal static void ForgetReplacedHistory(McpCall call, List<string> replaced)
        {
            if (replaced.Count == 0)
                return;
            UndoStack stack = UndoStack.Current;
            bool hadHistory = stack.UndoLabel != null || stack.RedoLabel != null;
            stack.Clear();
            if (hadHistory)
                call.Note("The undo history was cleared: this port replaced " + replaced.Count + " composite(s) (" + string.Join(", ", replaced.Take(5)) + (replaced.Count > 5 ? ", ..." : "") +
                    ") that earlier steps referred to, so those steps could not be undone correctly any more.");
        }

        /// <summary>File > Export Composites > To Level: port composites of the open level into other levels on disk, one at a time.</summary>
        private static object ExportToLevels(McpCall call)
        {
            bool overwriteComposites = call.Bool("overwrite_composites");
            bool overwriteAssets = call.Bool("overwrite_assets");
            bool build = call.Bool("build");
            bool dryRun = call.Bool("dry_run");
            List<string> destinations = call.StrList("levels", required: true).Select(Normalise).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (destinations.Count == 0)
                throw new McpError("'levels' is empty: name the levels to port into (list_levels shows them).");

            Level source = null;
            List<Composite> composites = null;
            Dictionary<ShortGuid, string> closure = null;
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel();
                McpEditor.RequireUndoIdle();
                Commands commands = content.Level.Commands;
                composites = call.StrList("composites", required: true).Select(o => McpScript.FindComposite(commands, o)).Distinct().ToList();
                if (composites.Count == 0)
                    throw new McpError("'composites' is empty: name the composites of the open level to port.");
                foreach (Composite composite in composites)
                    if (composite.shortGUID == GlobalId || composite.shortGUID == PauseMenuId)
                        throw new McpError(composite.name + " is GLOBAL or PAUSEMENU, which every level has its own copy of, and cannot be ported. Port the composites it places instead.");
                closure = Closure(commands, composites, true).GroupBy(o => o.shortGUID).ToDictionary(o => o.Key, o => o.First().name);
                source = content.Level;
            });
            foreach (string level in destinations)
                if (string.Equals(level, source.Name, StringComparison.OrdinalIgnoreCase))
                    throw new McpError(level + " is the level that is open: its composites are already there. Name other levels.");

            //What each destination already has, from its script's composite index (no load)
            JArray plan = new JArray();
            foreach (string level in destinations)
            {
                List<CompositeIndexEntry> index;
                try { index = CompositeIndexCache.Get(level); }
                catch (Exception e) { throw new McpError(level + "'s script could not be read (" + e.Message + "). Nothing was written."); }
                HashSet<ShortGuid> there = new HashSet<ShortGuid>(index.Select(o => o.ID));
                List<string> clash = closure.Where(o => there.Contains(o.Key)).Select(o => o.Value).OrderBy(o => o).ToList();
                JObject entry = new JObject() { ["level"] = level, ["new_composites"] = closure.Count - clash.Count, ["already_there"] = clash.Count };
                if (clash.Count != 0) entry[overwriteComposites ? "replaced" : "kept_as_they_are"] = new JArray(clash.Take(50));
                plan.Add(entry);
            }
            JObject result = new JObject()
            {
                ["composites"] = new JArray(composites.Select(o => o.name)),
                ["including_placed"] = closure.Count,
                ["build"] = build,
            };
            bool backupFirst = call.Bool("backup_first", true);
            if (dryRun)
            {
                result["dry_run"] = true;
                result["destinations"] = plan;
                call.Note("Nothing was written. Each destination would be " + (backupFirst ? "backed up, " : "") + "loaded, ported into and " + (build ? "saved and built" : "saved") + " in turn" + (destinations.Count > 1 ? " (a while for several levels)" : "") + "; a running game is closed first.");
                return result;
            }

            //A way back for every level about to be written, before any is
            Dictionary<string, long> backups = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (backupFirst)
            {
                foreach (string level in destinations)
                {
                    call.ThrowIfCancelled();
                    global::OpenCAGE.Backups.AlienLevel.AlienBackup backup = McpEditorTools.MakeBackup(call, level, "Before porting " + string.Join(", ", composites.Select(o => McpScript.NormalisePath(o.name).Split('\\').Last()).Take(3)) + " from " + source.Name);
                    if (backup != null) backups[level] = backup.ID;
                }
                result["backups"] = new JObject(backups.Select(o => new JProperty(o.Key, o.Value)));
            }
            bool gameWasRunning = EditorUtils.ThisInstallsGameRunning();

            //The composite on screen keeps its links in its live pages until compiled: compile it so the porter reads them.
            //Then hold off everything that could edit the open level underneath the porter, as the Export window does.
            McpEditor.UI(() =>
            {
                if (McpEditor.RequireLevel().Level != source)
                    throw new McpError("A different level was opened while this was being checked. Nothing was written; try again.");
                McpEditor.RequireUndoIdle();
                Composite shown = Singleton.Editor.CompositeDisplay?.Composite;
                if (shown != null) McpBrowseTools.CompileIfShown(shown);
                Singleton.Editor.CloseLevelPicker();
                UndoStack.Current.Blocked = true;
                Singleton.Editor.Enabled = false;
            });
            JArray written = new JArray();
            string failedLevel = null, failure = null;
            bool cancelled = false;
            try
            {
                for (int i = 0; i < destinations.Count; i++)
                {
                    if (call.Cancel.IsCancellationRequested) { cancelled = true; break; }
                    string level = destinations[i];
                    call.Progress("Porting into " + level + " (" + (i + 1) + " of " + destinations.Count + ")", i, destinations.Count);
                    try
                    {
                        using (McpEditorTools.Heartbeat(call, (build ? "Porting into and building " : "Porting into ") + level))
                        {
                            int ported = ExportComposite.PortCompositesToLevel(source, composites, level, overwriteComposites, overwriteAssets, build, out DeadProxyReport dead);
                            JObject done = new JObject() { ["level"] = level, ["ported"] = ported };
                            if (dead != null && dead.Any) done["dead_proxies"] = dead.Describe(level);
                            written.Add(done);
                        }
                    }
                    catch (Exception e)
                    {
                        //Levels before this one are saved; this one may be part-written. Stop, as the Export window does.
                        failedLevel = level;
                        failure = e.Message;
                        break;
                    }
                }
            }
            finally
            {
                McpEditor.UI(() =>
                {
                    UndoStack.Current.Blocked = false;
                    Singleton.Editor.Enabled = true;
                });
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
            }

            if (failedLevel != null)
                throw new McpError(McpErrorCodes.Failed, "The port failed on " + failedLevel + ": " + failure + " " +
                    (written.Count == 0 ? "No level was written before it." : "Written and saved before it: " + string.Join(", ", written.Select(o => (string)o["level"])) + ".") +
                    " " + failedLevel + " may be part-written on disk: " + (backups.TryGetValue(failedLevel, out long id) ? "restore_backup level '" + failedLevel + "' id " + id + " puts it back as it was before this call." : "restore it from a backup (list_backups), or verify the game files."));
            result["written"] = written;
            if (gameWasRunning && !EditorUtils.ThisInstallsGameRunning())
                result["closed_game"] = true;
            if (cancelled)
                call.Note("Cancelled after " + written.Count + " of " + destinations.Count + " levels; those are written.");
            if (backups.Count != 0)
                call.Note("Each destination was backed up first: restore_backup with its id in 'backups' undoes this for that level.");
            return result;
        }

        /// <summary>The composites and everything they place (when <paramref name="children"/>), stopping at GLOBAL and PAUSEMENU.</summary>
        private static HashSet<Composite> Closure(Commands commands, IEnumerable<Composite> roots, bool children)
        {
            HashSet<Composite> seen = new HashSet<Composite>();
            Stack<Composite> todo = new Stack<Composite>(roots);
            while (todo.Count != 0)
            {
                Composite composite = todo.Pop();
                if (composite == null || !seen.Add(composite) || !children) continue;
                foreach (FunctionEntity instance in composite.functions)
                {
                    if (instance.function.IsFunctionType || instance.function == GlobalId || instance.function == PauseMenuId) continue;
                    Composite child = commands.GetComposite(instance.function);
                    if (child != null && !seen.Contains(child)) todo.Push(child);
                }
            }
            return seen;
        }

        /// <summary>A level name as list_levels gives it (see <see cref="McpLevels"/>), refusing unknown and ambiguous ones with candidates.</summary>
        internal static string Normalise(string level) => McpLevels.Resolve(level);
        #endregion
    }

    /// <summary>
    /// The one other level kept loaded, so a search and then a port from it load it once. Another level
    /// replaces it; saving a level to its folder drops it.
    /// </summary>
    internal static class McpLevelCache
    {
        private static readonly object _lock = new object();
        private static Level _level;
        private static DateTime _written;

        public static Level Get(McpCall call, string levelName)
        {
            string name = levelName.Replace('\\', '/').Trim('/').ToUpperInvariant();
            string open = McpEditor.UI(() => Singleton.Editor?.CompositeBrowser?.Content?.Level?.Name);
            lock (_lock)
            {
                string commandsPath = new Level(Singleton.PathToAI + "/DATA/ENV/" + name, Singleton.Global, false).CommandsFilepath;
                DateTime written = File.Exists(commandsPath) ? File.GetLastWriteTimeUtc(commandsPath) : DateTime.MinValue;
                if (_level != null && string.Equals(_level.Name, name, StringComparison.OrdinalIgnoreCase) && _written == written)
                    return _level;

                _level = null;
                GC.Collect();
                if (!Level.GetLevels(Singleton.PathToAI).Any(o => string.Equals(o, name, StringComparison.OrdinalIgnoreCase)))
                    throw McpError.NotFound("level", levelName, Level.GetLevels(Singleton.PathToAI), "list_levels shows them.");
                if (string.Equals(open, name, StringComparison.OrdinalIgnoreCase))
                    call.Note(name + " is open in the editor: this reads its files on disk, without unsaved changes.");

                call.Progress("Loading " + name);
                using (McpEditorTools.Heartbeat(call, "Loading " + name))
                {
                    //Read without taking in GLOBAL's textures: that flips flags on the shared entries, which the open
                    //level's own load and save also do. A port leaves global texture references pointing at global anyway.
                    Level level = new Level(Singleton.PathToAI + "/DATA/ENV/" + name, Singleton.Global, false) { AbsorbGlobalTextures = false };
                    level.Load();
                    if (level.Commands == null || !level.Commands.Loaded)
                        throw new McpError(name + "'s script could not be read.");
                    _level = level;
                    _written = written;
                }
                return _level;
            }
        }

        /// <summary>The level held now (null if none).</summary>
        public static string Held { get { lock (_lock) return _level?.Name; } }

        public static string Release()
        {
            lock (_lock)
            {
                string name = _level?.Name;
                _level = null;
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                return name;
            }
        }
    }
}
