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
                Description = "Look inside any level (it does not have to be open): find composites by path, or entities by function type / name / parameter, to decide what to port. Searching composites by path is instant; searching entities loads the level (tens of seconds the first time, then remembered for port_composites).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("level", "The level, as list_levels names it (e.g. 'PRODUCTION/SCI_ANDROIDLAB').", required: true),
                    McpSchema.String("composites", "Words the composite path must contain (e.g. 'corridor', 'Archetypes Script Mission')."),
                    McpSchema.String("function_type", "Find entities of this built-in type (e.g. 'Character', 'SpawnPositionSelect' is a composite - use composites for that)."),
                    McpSchema.String("entity_name", "Find entities whose name contains this."),
                    McpSchema.String("parameter", "Find entities with this parameter set (e.g. 'spawn_on_reset')."),
                    McpSchema.Boolean("placed_only", "Only composites that are actually placed in that level."),
                    McpSchema.Integer("limit", "At most this many results (default 100).")),
                ReadOnly = true,
                Idempotent = true,
                Run = SearchLevel,
            };

            yield return new McpTool()
            {
                Name = "describe_level_composite",
                Title = "Describe a composite in another level",
                Description = "What a composite in another level holds (its entities, variables and what it instances), before porting it. Loads that level if it is not loaded yet.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("level", "The level.", required: true),
                    McpSchema.String("composite", "The composite's path or id there.", required: true),
                    McpSchema.String("filter", "Only entities whose name or type contains this."),
                    McpSchema.Integer("limit", "At most this many entities (default 200).")),
                ReadOnly = true,
                Idempotent = true,
                Run = call =>
                {
                    Level source = McpLevelCache.Get(call, Normalise(call.Str("level", required: true)));
                    Commands commands = source.Commands;
                    Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                    int limit = Math.Max(1, call.Int("limit", 200));
                    string filter = call.Str("filter");
                    JObject result = McpScript.CompositeSummary(commands, composite);
                    result["level"] = source.Name;
                    result["entity_count"] = composite.GetEntities().Count;
                    result["entities"] = new JArray(composite.GetEntities().Where(o => !(o is VariableEntity) && (filter == null
                        || McpScript.EntityName(commands, composite, o).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                        || McpScript.TypeName(commands, composite, o).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)).Take(limit).Select(o => McpScript.Brief(commands, composite, o)));
                    result["variables"] = new JArray(composite.variables.Select(o => McpScript.EntityName(commands, composite, o) + " (" + McpScript.TypeName(commands, composite, o) + ")"));
                    //The composites it places directly (each once); what those place in turn is theirs to describe
                    result["instances_of"] = new JArray(composite.functions.Where(o => !o.function.IsFunctionType).Select(o => commands.GetComposite(o.function))
                        .Where(o => o != null && o != composite).Distinct().Select(o => o.name).Take(100));
                    result["placed_in"] = new JArray(McpBrowseTools.PlacedIn(commands, composite).Take(20));
                    return result;
                },
            };

            yield return new McpTool()
            {
                Name = "port_composites",
                Title = "Port composites from another level",
                Description = "Copy composites from another level into the open one with everything they use (child composites, models, materials, textures, collision, physics, pages, Characters' display models). Existing ones (same id) are kept unless overwrite_composites, which also clears the undo history. Not undoable; saved by save_level. Place them with create_entities.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("level", "The level to copy from.", required: true),
                    McpSchema.Strings("composites", "Their paths or ids in that level.", required: true),
                    McpSchema.Boolean("include_children", "Also bring the composites they place (default true - they need them to work)."),
                    McpSchema.Boolean("include_display_models", "Also bring the DisplayModel composites their Characters use (default true)."),
                    McpSchema.Boolean("overwrite_composites", "Replace composites the open level already has with the same id (default false)."),
                    McpSchema.Boolean("overwrite_assets", "Replace models/textures/materials the open level already has with the same name (default false)."),
                    McpSchema.Boolean("open", "Show the first ported composite in the editor afterwards.")),
                Run = PortComposites,
            };

            yield return new McpTool()
            {
                Name = "export_composites_to_levels",
                Title = "Port composites into other levels",
                Description = "Port composites of the open level (with what they place and use) into other levels on disk, as File > Export Composites > To Level: each destination is loaded, ported into and saved (build: Save & Build). Written at once, not undoable; closes the game. Unsaved changes here are included. dry_run reports without writing.",
                InputSchema = McpSchema.Object(
                    McpSchema.Strings("composites", "Composites of the open level to port, by path or id.", required: true),
                    McpSchema.Strings("levels", "Destination levels, as list_levels names them (not the open one).", required: true),
                    McpSchema.Boolean("overwrite_composites", "Replace composites a destination already has with the same id (default false: keep theirs)."),
                    McpSchema.Boolean("overwrite_assets", "Replace same-named models/textures/materials in the destinations (default false)."),
                    McpSchema.Boolean("build", "Save & Build each destination after porting (slow: every baker runs). Default false: a plain save."),
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
            int limit = Math.Max(1, call.Int("limit", 100));
            string words = call.Str("composites");
            string functionType = call.Str("function_type");
            string entityName = call.Str("entity_name");
            string parameter = call.Str("parameter");
            bool entitySearch = functionType != null || entityName != null || parameter != null;
            if (!entitySearch && words == null)
                throw new McpError("Give 'composites' to search composite paths, or function_type / entity_name / parameter to search entities.");

            HashSet<ShortGuid> inOpenLevel = McpEditor.UI(() =>
            {
                Commands open = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
                return open == null ? new HashSet<ShortGuid>() : new HashSet<ShortGuid>(open.Entries.Where(o => o != null).Select(o => o.shortGUID));
            });
            string[] terms = (words ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            bool MatchesPath(string path) => terms.All(t => (path ?? "").IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);

            JObject result = new JObject() { ["level"] = level };
            if (!entitySearch && !call.Bool("placed_only"))
            {
                //The script's own index: no load needed
                List<CompositeIndexEntry> index = CompositeIndexCache.Get(level);
                List<CompositeIndexEntry> hits = index.Where(o => MatchesPath(o.Name)).ToList();
                result["count"] = hits.Count;
                result["composites"] = new JArray(hits.Take(limit).Select(o => new JObject()
                {
                    ["id"] = McpScript.Id(o.ID),
                    ["path"] = o.Name,
                    ["places"] = o.Instances?.Count ?? 0,
                    ["in_open_level"] = inOpenLevel.Contains(o.ID),
                }));
                return result;
            }

            Level source = McpLevelCache.Get(call, level);
            Commands commands = source.Commands;
            HashSet<Composite> placed = call.Bool("placed_only") ? McpBrowseTools.Reachable(commands) : null;
            List<Composite> scope = commands.Entries.Where(o => o != null && MatchesPath(o.name) && (placed == null || placed.Contains(o))).ToList();

            if (!entitySearch)
            {
                result["count"] = scope.Count;
                result["composites"] = new JArray(scope.Take(limit).Select(o => new JObject() { ["id"] = McpScript.Id(o.shortGUID), ["path"] = o.name, ["entities"] = o.GetEntities().Count, ["in_open_level"] = inOpenLevel.Contains(o.shortGUID) }));
                return result;
            }

            FunctionType? type = null;
            if (functionType != null)
            {
                if (!Enum.TryParse(functionType.Trim(), true, out FunctionType parsed) || !Enum.IsDefined(typeof(FunctionType), parsed))
                    throw new McpError("There is no function type '" + functionType + "'. (If it is a composite, search with 'composites' instead.)");
                type = parsed;
            }
            ShortGuid? parameterId = parameter == null ? (ShortGuid?)null : McpScript.ParamId(parameter);
            JArray found = new JArray();
            int total = 0;
            foreach (Composite composite in scope)
            {
                foreach (Entity entity in composite.GetEntities())
                {
                    if (type != null && !(entity is FunctionEntity f && f.function == type.Value)) continue;
                    if (entityName != null && McpScript.EntityName(commands, composite, entity).IndexOf(entityName, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (parameterId != null && entity.GetParameter(parameterId.Value) == null) continue;
                    total++;
                    if (found.Count >= limit) continue;
                    JObject hit = McpScript.Brief(commands, composite, entity);
                    hit["composite"] = composite.name;
                    hit["composite_id"] = McpScript.Id(composite.shortGUID);
                    hit["composite_in_open_level"] = inOpenLevel.Contains(composite.shortGUID);
                    if (parameterId != null) hit["value"] = McpValues.ToJson(entity.GetParameter(parameterId.Value).content, commands, source.Models);
                    found.Add(hit);
                }
            }
            result["count"] = total;
            result["entities"] = found;
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

            //Characters name their display model rather than placing it, so a port does not follow it by itself.
            //The player's is "PLAYER_FP", which means whichever of GLOBAL's suits is worn: those models come with it.
            List<string> addedDisplayModels = new List<string>();
            if (displayModels)
            {
                HashSet<string> models = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                bool player = false;
                foreach (Composite composite in Closure(sourceCommands, roots, children).ToList())
                {
                    foreach (FunctionEntity entity in composite.functions)
                    {
                        string model = (entity.GetParameter("display_model")?.content as cString)?.value;
                        if (!string.IsNullOrWhiteSpace(model)) models.Add(model.Trim());
                        if (entity.function == FunctionType.Character && (entity.GetParameter("is_player")?.content as cBool)?.value == true) player = true;
                    }
                }
                if (player || models.Contains("PLAYER_FP"))
                    foreach (FunctionEntity entity in sourceCommands.EntryPoints[1]?.functions ?? Enumerable.Empty<FunctionEntity>())
                    {
                        string model = (entity.GetParameter("display_model")?.content as cString)?.value;
                        if (!string.IsNullOrWhiteSpace(model)) models.Add(model.Trim());
                    }
                HashSet<ShortGuid> have = McpEditor.UI(() => new HashSet<ShortGuid>(McpEditor.RequireCommands().Entries.Where(o => o != null).Select(o => o.shortGUID)));
                foreach (string model in models)
                {
                    Composite displayModel = sourceCommands.Entries.FirstOrDefault(o => o != null && string.Equals(o.name, "DisplayModel:" + model, StringComparison.OrdinalIgnoreCase));
                    if (displayModel == null || roots.Contains(displayModel) || (have.Contains(displayModel.shortGUID) && !overwriteComposites)) continue;
                    roots.Add(displayModel);
                    addedDisplayModels.Add(displayModel.name);
                }
            }

            CompositeFlowgraphTable sourceLayouts = CustomTable.ReadTable(sourceCommands.Filepath, CustomTableType.COMPOSITE_FLOWGRAPHS) as CompositeFlowgraphTable;
            CompositeParameterModificationTable sourceModifications = CustomTable.ReadTable(sourceCommands.Filepath, CustomTableType.COMPOSITE_PARAMETER_MODIFICATION) as CompositeParameterModificationTable;
            EntityAppliedDefaultsTable sourceDefaults = CustomTable.ReadTable(sourceCommands.Filepath, CustomTableType.ENTITY_APPLIED_DEFAULTS) as EntityAppliedDefaultsTable;

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
                            Singleton.OnCompositeAdded?.Invoke(copy);
                            FlowgraphLayoutManager.ImportLayouts(copy, FlowgraphLayoutManager.GetLayoutsForPort(original, sourceLayouts, level));
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
            call.Note("Ported composites are not placed anywhere yet: add instances with create_entities ({\"composite\": \"<path>\", \"position\": [x,y,z]}). Porting cannot be undone; nothing is on disk until save_level.");
            return result;
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
            if (dryRun)
            {
                result["dry_run"] = true;
                result["destinations"] = plan;
                call.Note("Nothing was written. Each destination would be loaded, ported into and " + (build ? "saved and built" : "saved") + " in turn" + (destinations.Count > 1 ? " (a while for several levels)" : "") + "; a running game is closed first.");
                return result;
            }

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
                throw new McpError("The port failed on " + failedLevel + ": " + failure + " " +
                    (written.Count == 0 ? "No level was written before it." : "Written and saved before it: " + string.Join(", ", written.Select(o => (string)o["level"])) + ".") +
                    " " + failedLevel + " may be part-written on disk: restore it with restore_backup, or verify the game files.");
            result["written"] = written;
            if (cancelled)
                call.Note("Cancelled after " + written.Count + " of " + destinations.Count + " levels; those are written.");
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

        internal static string Normalise(string level)
        {
            string name = (level ?? "").Replace('\\', '/').Trim().Trim('/').ToUpperInvariant();
            List<string> levels = EditorUtils.GetEditableLevels();
            string match = levels.FirstOrDefault(o => string.Equals(o, name, StringComparison.OrdinalIgnoreCase))
                ?? levels.FirstOrDefault(o => o.EndsWith("/" + name, StringComparison.OrdinalIgnoreCase));
            if (match == null)
                throw new McpError("There is no level '" + level + "' (list_levels shows them).");
            return match;
        }
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
                    throw new McpError("There is no level '" + levelName + "'.");
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
