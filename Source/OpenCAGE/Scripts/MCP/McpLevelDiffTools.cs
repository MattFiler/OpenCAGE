using Newtonsoft.Json.Linq;
using OpenCAGE.LevelDiff;
using OpenCAGE.Recipes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Compare Levels and Record / Replay Edits for AI assistants: compare_levels, record_edits, replay_recipe.
    /// Registered from McpPortingTools.Tools(); each names its own toolset.
    /// </summary>
    internal static class McpLevelDiffTools
    {
        private const string SideHelp = "saved (the open level as saved on disk), open (the open level now, unsaved edits included: script, models, materials, textures and mappings only - the rest is made by a Save & Build), " +
            "level:NAME (another level of this install, as list_levels names it), backup:ID (a backup of the open level, list_backups), or the full path of a level folder (another game install or a copy; it must be inside a DATA\\ENV folder)";

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "compare_levels",
                Title = "Compare two levels",
                Toolset = "core",
                Description = "Compare two copies of a level beyond the script (diff_level is script only): script (composites, entities, parameters, links and their order, resources, CAGEAnimations, TriggerSequences), " +
                    "models, materials, textures, material mappings, placements (movers), lights, collision, physics, zones, navigation (navmesh, cover, spotting, assault), sound data, text, the level's other data, its files byte for byte, " +
                    "and the configs when the two are in different game folders. Each category lists what was added, removed and changed, with the fields that changed (before -> now). " +
                    "Things are matched by id (script) or by name plus a hash of what they hold (models, materials, collision proxies), so renumbered tables don't show as changes; differences no one made " +
                    "(float rounding, global textures a save copies in, defaults the editor fills in, resource positions a build rewrites) are summed up, not listed. Loads both levels (tens of seconds); the last report " +
                    "is kept, so 'item' drills into it without comparing again. Read-only.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("before", "The older side: " + SideHelp + ".", required: true),
                    McpSchema.String("after", "The newer side, same forms (default saved)."),
                    McpSchema.Strings("categories", "Only these: " + string.Join(", ", LevelComparer.Categories.Select(o => o.Key)) + " (default all)."),
                    McpSchema.String("item", "Drill into one item of the last report (same before / after): its ref from a result (\"entity:ID/ID\", \"materials:NAME\", \"file:WORLD\\\\COMMANDS.PAK\" ...) or words in its text; lists every detail."),
                    McpSchema.Integer("depth", "How deep each item's details go (default 2: item and its details; 1: items only)."),
                    McpSchema.Boolean("refresh", "Compare again even if the last report is for the same sides."),
                    McpSchema.Limit(30, "items per category"),
                    McpSchema.Offset("items per category")),
                ReadOnly = true,
                Idempotent = true,
                Run = CompareLevels,
            };

            yield return new McpTool()
            {
                Name = "record_edits",
                Title = "Record edits as a recipe",
                Toolset = "script",
                Description = "Record the open level's edits - made in the editor or by these tools - as a recipe that replay_recipe can apply to another copy of the level, or another level. " +
                    "start snapshots the level (a second or two); stop compares it against the snapshot and writes the NET change (what was undone while recording is not in it) as a .ocrecipe JSONL file: " +
                    "composites and entities added (with their ids), removed and renamed, parameters (before and after), links and their order, resources, CAGEAnimations, TriggerSequences, pin types, material values. " +
                    "Models, textures, mappings, collision and physics made while recording are listed as not_carried (import or port them into the target first). status says what is being recorded; discard drops it. " +
                    "A level closing while recording stops it; the next stop hands that recipe over.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("action", "start, stop, status or discard.", required: true, options: new[] { "start", "stop", "status", "discard" }),
                    McpSchema.String("name", "stop: the recipe's name (default 'Recorded edits')."),
                    McpSchema.String("path", "stop: where to write it (default DATA\\MODTOOLS\\RECIPES\\<name>_<time>.ocrecipe in the game folder)."),
                    McpSchema.Boolean("include_steps", "stop: list each recipe step in the result (default false: counts only).")),
                Run = RecordEdits,
            };

            yield return new McpTool()
            {
                Name = "replay_recipe",
                Title = "Replay a recipe",
                Toolset = "script",
                Description = "Apply a recipe (record_edits, or the Record Edits window) to the open level as ONE undo step 'Replay: <name>'. Each step is checked first: ready (the level is as the recipe found it), " +
                    "already done, conflict (something else is there now: skipped unless conflicts 'overwrite'), or missing (its composite, entity, model or material isn't here). dry_run only reports. " +
                    "ids: on a copy of the level it was recorded in, what it adds keeps its recorded ids (so later recipes from the same level still find it); on another level, or when an id is taken here, " +
                    "everything it adds gets new ids and every link and path to it follows - the result's ids_why says which and why ('auto'), or force keep / remap. Save (save_level) afterwards.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("path", "The recipe file (.ocrecipe)."),
                    McpSchema.String("recipe", "Or the recipe's text itself (JSONL)."),
                    McpSchema.Boolean("dry_run", "Report what would happen, changing nothing."),
                    McpSchema.String("ids", "auto (default), keep or remap.", options: new[] { "auto", "keep", "remap" }),
                    McpSchema.String("conflicts", "skip (default) or overwrite.", options: new[] { "skip", "overwrite" }),
                    McpSchema.Boolean("all_steps", "List every step, not only those that are not simply ready.")),
                Destructive = true,
                Run = ReplayRecipe,
            };
        }

        #region compare_levels
        private static LevelDiffReport _last;
        private static string _lastKey;
        private static DateTime _lastAt;

        private static object CompareLevels(McpCall call)
        {
            string before = call.Str("before", required: true), after = call.Str("after") ?? "saved";
            List<string> categories = call.Has("categories") ? call.StrList("categories") : null;
            if (categories != null)
                foreach (string c in categories)
                    if (!LevelComparer.Categories.Any(o => string.Equals(o.Key, c, StringComparison.OrdinalIgnoreCase)))
                        throw McpError.Invalid("Unknown category '" + c + "': " + string.Join(", ", LevelComparer.Categories.Select(o => o.Key)) + ".");
            int depth = Math.Max(1, call.Int("depth", 2)), limit = Math.Max(1, call.Int("limit", 30)), offset = Math.Max(0, call.Int("offset", 0));
            string key = before + "|" + after + "|" + (categories == null ? "*" : string.Join(",", categories.OrderBy(o => o, StringComparer.OrdinalIgnoreCase)));
            //Live sides change under the report: only on-disk pairs are kept for drill-down
            bool live = IsLive(before) || IsLive(after);
            LevelDiffReport report;
            if (!call.Bool("refresh") && !live && _last != null && _lastKey == key && DateTime.Now - _lastAt < TimeSpan.FromMinutes(30))
                report = _last;
            else
            {
                LevelSide a, b;
                try
                {
                    Func<string, string> resolve = name => McpLevels.Resolve(name, allowFrontend: true, call: call);
                    a = LevelSides.Parse(before, resolve);
                    if (a.Live == null && a.Folder == null) throw McpError.Invalid("before: nothing to compare");
                    if (before.Trim().StartsWith("backup:", StringComparison.OrdinalIgnoreCase)) call.Progress("Unpacked the backup");
                    b = LevelSides.Parse(after, resolve);
                }
                catch (ArgumentException e) { throw McpError.Invalid(e.Message); }
                CompareOptions options = new CompareOptions()
                {
                    Only = categories == null ? null : new HashSet<string>(categories, StringComparer.OrdinalIgnoreCase),
                    Progress = text => call.Progress(text),
                    Cancel = call.Cancel,
                };
                report = LevelSides.Compare(a, b, options, read => McpEditor.UI(read));
                if (!live) { _last = report; _lastKey = key; _lastAt = DateTime.Now; }
            }

            if (call.Has("item"))
            {
                string item = call.Str("item");
                DiffNode node = report.Find(null, item, out DiffCategory found);
                if (node == null)
                {
                    List<(DiffCategory category, DiffNode node)> hits = report.Search(null, item, 20);
                    if (hits.Count == 0) throw new McpError(McpErrorCodes.NotFound, "No item of the report has the ref or words '" + item + "'. Refs are in each item's 'ref'.");
                    if (hits.Count > 1)
                        return new JObject() { ["matches"] = new JArray(hits.Select(o => new JObject() { ["category"] = o.category.Key, ["text"] = o.node.Text, ["ref"] = o.node.Ref })), ["note"] = "Several items match: pass one's ref as 'item'." };
                    node = hits[0].node; found = hits[0].category;
                }
                return new JObject() { ["before"] = report.Before, ["after"] = report.After, ["category"] = found.Key, ["item"] = node.ToJson(10, 2000) };
            }
            JObject result = report.ToJson(categories, depth, limit, 40, offset);
            result["took_seconds"] = Math.Round(report.Took.TotalSeconds, 1);
            if (report.CategoriesDiffering == 0) call.Note("No differences in game data" + (report.Categories.Any(o => o.HasDifferences) ? " (only editor-side or file-level ones)" : "") + ".");
            return result;
        }

        private static bool IsLive(string side) => side != null && (side.Trim().Equals("open", StringComparison.OrdinalIgnoreCase) || side.Trim().Equals("now", StringComparison.OrdinalIgnoreCase));
        #endregion

        #region record_edits
        public static string RecipeFolder => Path.Combine(Singleton.PathToAI, "DATA", "MODTOOLS", "RECIPES");

        public static string DefaultRecipePath(string name)
        {
            string safe = new string((name ?? "Recorded edits").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim();
            if (safe.Length == 0) safe = "recipe";
            return Path.Combine(RecipeFolder, safe + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + Recipe.Extension);
        }

        private static object RecordEdits(McpCall call)
        {
            string action = (call.Str("action", required: true) ?? "").Trim().ToLowerInvariant();
            switch (action)
            {
                case "start":
                    return McpEditor.UI(() =>
                    {
                        LevelContent content = McpEditor.RequireLevel();
                        try { RecipeRecorder.Start(content.Level); }
                        catch (RecipeException e) { throw new McpError(McpErrorCodes.Conflict, "Can't start: " + e.Message + "."); }
                        return new JObject() { ["recording"] = true, ["level"] = content.Level.Name, ["note"] = "Edits from now on are recorded (the editor's and these tools'). record_edits stop writes the recipe." };
                    });
                case "status":
                    return McpEditor.UI(() => new JObject()
                    {
                        ["recording"] = RecipeRecorder.Recording,
                        ["level"] = RecipeRecorder.LevelName,
                        ["since"] = RecipeRecorder.Recording ? RecipeRecorder.Started.ToString("yyyy-MM-ddTHH:mm:ss") : null,
                        ["undo_steps_so_far"] = RecipeRecorder.StepCount,
                        ["steps"] = new JArray(RecipeRecorder.StepLabels().Take(50).ToArray()),
                        ["waiting_recipe"] = RecipeRecorder.Orphan != null ? "a recording stopped when its level closed: stop hands it over" : null,
                    });
                case "discard":
                    McpEditor.UI(() => RecipeRecorder.Discard());
                    return new JObject() { ["recording"] = false, ["note"] = "Discarded." };
                case "stop":
                    {
                        string name = call.Str("name") ?? "Recorded edits";
                        Recipe recipe = McpEditor.UI(() =>
                        {
                            if (!RecipeRecorder.Recording)
                                return RecipeRecorder.TakeOrphan() ?? throw new McpError(McpErrorCodes.Conflict, "Not recording: record_edits start first.");
                            return RecipeRecorder.Stop(name);
                        });
                        string path = call.Str("path") ?? DefaultRecipePath(recipe.Name);
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                        recipe.Save(path);
                        JObject result = new JObject()
                        {
                            ["path"] = Path.GetFullPath(path),
                            ["name"] = recipe.Name,
                            ["level"] = recipe.Level,
                            ["summary"] = recipe.Summary(),
                            ["undo_steps_recorded"] = new JArray(recipe.Steps.Take(50).ToArray()),
                        };
                        if (recipe.NotCarried.Count != 0) result["not_carried"] = new JArray(recipe.NotCarried.Take(50).ToArray());
                        if (call.Bool("include_steps")) result["steps"] = new JArray(recipe.Ops.Take(300).Select(RecipeText.Describe).ToArray());
                        if (recipe.Ops.Count == 0) call.Note("Nothing changed while recording.");
                        return result;
                    }
                default:
                    throw McpError.Invalid("'action' takes start, stop, status or discard.");
            }
        }
        #endregion

        #region replay_recipe
        private static object ReplayRecipe(McpCall call)
        {
            if (call.Has("path") == call.Has("recipe")) throw McpError.Invalid("Give 'path' (a recipe file) or 'recipe' (its text), one of them.");
            Recipe recipe;
            try
            {
                if (call.Has("path"))
                {
                    string path = call.Str("path");
                    if (!File.Exists(path)) throw new McpError(McpErrorCodes.NotFound, "There is no recipe at " + path + ".");
                    recipe = Recipe.Read(path);
                }
                else recipe = Recipe.Parse(call.Str("recipe"));
            }
            catch (RecipeException e) { throw McpError.Invalid("That is not a recipe this OpenCAGE reads: " + e.Message + "."); }
            string ids = (call.Str("ids") ?? "auto").Trim().ToLowerInvariant();
            if (ids != "auto" && ids != "keep" && ids != "remap") throw McpError.Invalid("'ids' takes auto, keep or remap.");
            string conflicts = (call.Str("conflicts") ?? "skip").Trim().ToLowerInvariant();
            if (conflicts != "skip" && conflicts != "overwrite") throw McpError.Invalid("'conflicts' takes skip or overwrite.");
            bool all = call.Bool("all_steps");

            RecipePlan plan = RecipeReplay.Plan(recipe, ids);
            if (call.Bool("dry_run"))
            {
                JObject dry = plan.ToJson(300, all);
                dry["dry_run"] = true;
                return dry;
            }
            RecipeReplay.Outcome outcome;
            //An AI tool's step says so, as every tool's does ("AI: Replay: name")
            try { outcome = RecipeReplay.Apply(plan, conflicts == "overwrite", "AI: Replay: "); }
            catch (RecipeException e) { throw new McpError(McpErrorCodes.Conflict, e.Message + "."); }
            JObject result = plan.ToJson(300, all);
            result["made"] = outcome.Made;
            result["undo_step"] = outcome.Changed ? outcome.Label : null;
            foreach (string note in outcome.Notes.Take(5)) call.Note(note);
            if (outcome.Made == 0) call.Note("Nothing was changed.");
            else call.Note("One undo step ('" + outcome.Label + "'). save_level writes it (build: true when it placed or moved models, lights or collision).");
            return result;
        }
        #endregion
    }
}
