using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.MCP
{
    /// <summary>The AI markup tool (Scripts\AIMarkup): place search / area sweep jobs and melee high cover, check them against a level's own. 'world' toolset.</summary>
    internal static class McpAIMarkupTools
    {
        private static readonly string[] KindKeys = { "search", "sweep", "highcover" };

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "place_ai_markup",
                Title = "Place AI markup",
                Description = "Place the AI markup NPCs find by position, as File > Place AI Markup does: retail's Archetypes\\Jobs\\SearchJob (spots an NPC hunting for a lost target searches at; the alien's systematic search), " +
                    "Archetypes\\Jobs\\AreaSweepJob (spots it sweeps over while stalking) and Archetypes\\Jobs\\MeleeContext\\MeleeContext_HighCover (where an android throws the player against a wall; only levels with androids need it). " +
                    "Spots come from the navmesh as last built, spaced as retail spaces them: search and sweep jobs on standing-height floor NPCs walk, 0.35 m or more from its edge (the more open first), 5.5 m apart per kind; " +
                    "high cover 2.15 m back from a flat wall that is high in the collision, facing it, 4.5 m apart. New spots keep their kind's spacing from the level's own markup unless keep_clear_of_existing is false. " +
                    "Brings the composites the level lacks in from a retail level of this install (as port_composites: NOT undoable), then places every spot as ONE undo step ('AI: Place AI Markup'): instances SearchJob_N / AreaSweepJob_N / MeleeContext_HighCover_N. " +
                    "No bake is needed. dry_run lists the spots, changing nothing; compare scores the tool against the level's own markup instead (the retail check).",
                InputSchema = McpSchema.Object(
                    McpSchema.Array("kinds", "Which markup (default [\"search\",\"sweep\"]): search, sweep, highcover.", new JObject() { ["type"] = "string", ["enum"] = new JArray(KindKeys) }),
                    McpSchema.Number("search_spacing", "Least distance between two search jobs (m, default 5.5)."),
                    McpSchema.Number("sweep_spacing", "Least distance between two area sweep jobs (m, default 5.5)."),
                    McpSchema.Number("highcover_spacing", "Least distance between two high cover contexts (m, default 4.5)."),
                    McpSchema.Boolean("keep_clear_of_existing", "Keep each kind's spacing from the markup the level already has (default true)."),
                    McpSchema.String("composite", "The composite to put the markup in (path or id; placed once, or the root). " + McpSchema.CompositeDefaultRoot),
                    McpSchema.Nested("region", "Only find spots here (default: the whole navmesh). " + McpRegion.Help, McpRegion.Schema),
                    McpSchema.Boolean("indoors_only", "Only spots with a ceiling over them in the collision (default false): leaves out the outdoor floor of a level an alien hunts indoors."),
                    McpSchema.Nested("spots", "Instead of finding spots: place these, [{kind, position:[x,y,z], yaw}, ...] (world; yaw only matters for highcover).", new JObject()
                    {
                        ["type"] = "array",
                        ["items"] = new JObject() { ["type"] = "object", ["properties"] = new JObject() { ["kind"] = new JObject() { ["type"] = "string" }, ["position"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" } }, ["yaw"] = new JObject() { ["type"] = "number" } } },
                    }),
                    McpSchema.String("search_prefix", "Instance names for search jobs: prefix_N (default SearchJob)."),
                    McpSchema.String("sweep_prefix", "Instance names for area sweep jobs: prefix_N (default AreaSweepJob)."),
                    McpSchema.String("highcover_prefix", "Instance names for high cover: prefix_N (default MeleeContext_HighCover)."),
                    McpSchema.String("page", "The flowgraph page their nodes go on (default AIMarkup)."),
                    McpSchema.Integer("seed", "Sampling seed (default 1: the same level gives the same spots)."),
                    McpSchema.Integer("max_listed", "List at most this many spots (default 2000)."),
                    McpSchema.Boolean("show", "Take the editor to them (default false)."),
                    McpSchema.Boolean("dry_run", "Only find the spots and say what would be ported and placed."),
                    McpSchema.Boolean("compare", "Change nothing: for each kind the level already places, find spots again round its own markup (that markup ignored) and score them against it.")),
                Destructive = true,
                Toolset = "world",
                Run = PlaceMarkup,
            };

            yield return new McpTool()
            {
                Name = "check_ai_markup",
                Title = "Check AI markup",
                Description = "The open level's AI markup as the game's NPCs use it: every search job, area sweep job and melee high cover context the level places (position, facing, whether the user's), " +
                    "counts and spacing per kind, and whether each stands on the NPCs' navmesh (as last built). Markup off the navmesh, or none at all where an alien or android hunts, leaves NPCs nowhere to search or sweep. " +
                    "place_ai_markup adds it.",
                InputSchema = McpSchema.Object(
                    McpSchema.Integer("max_listed", "List at most this many (default 100).")),
                ReadOnly = true,
                Idempotent = true,
                Toolset = "world",
                Run = CheckMarkup,
            };
        }

        private static MarkupOptions ReadOptions(McpCall call, Commands commands, Level level)
        {
            MarkupOptions options = new MarkupOptions() { Seed = call.Int("seed", 1), KeepClearOfExisting = !call.Has("keep_clear_of_existing") || call.Bool("keep_clear_of_existing"), IndoorsOnly = call.Bool("indoors_only") };
            if (call.Has("kinds"))
            {
                List<MarkupKind> kinds = call.Array("kinds").Select(o => AIMarkup.FindKind(o.ToString())).ToList();
                options.Search = kinds.Contains(AIMarkup.SearchJob);
                options.Sweep = kinds.Contains(AIMarkup.AreaSweepJob);
                options.HighCover = kinds.Contains(AIMarkup.HighCover);
            }
            foreach (MarkupKind kind in AIMarkup.Kinds)
            {
                if (call.Has(kind.Key + "_spacing"))
                {
                    float s = (float)call.Num(kind.Key + "_spacing", kind.Spacing);
                    if (s < 1 || s > 50) throw McpError.Invalid("'" + kind.Key + "_spacing' is 1 to 50 m.");
                    options.Spacing[kind.Key] = s;
                }
                if (call.Has(kind.Key + "_prefix")) options.NamePrefix[kind.Key] = call.Str(kind.Key + "_prefix");
            }
            if (call.Has("page")) options.Page = call.Str("page");
            if (call.Has("composite")) options.Into = McpScript.FindComposite(commands, call.Str("composite"));
            if (call.Has("region"))
            {
                McpRegion region = McpRegion.Resolve(call, commands, level, call.Token("region"));
                if (!McpCollision.RegionBox(call, commands, level, region, out Vector3 min, out Vector3 max, out McpCollision.Gathered _))
                    throw new McpError(region.Label + " places nothing with collision or a model, so it has no extent to look in.");
                options.RegionMin = min - new Vector3(0, 2, 0);
                options.RegionMax = max + new Vector3(0, 2, 0);
            }
            return options;
        }

        private static JObject Spot(MarkupSpot spot)
        {
            JObject o = new JObject() { ["kind"] = spot.Kind.Key, ["position"] = McpCollision.V(spot.Position), ["clearance_m"] = spot.Clearance };
            if (spot.Kind == AIMarkup.HighCover) { o["yaw"] = spot.Yaw; o["wall_m"] = spot.Wall; }
            return o;
        }

        private static object PlaceMarkup(McpCall call)
        {
            bool dryRun = call.Bool("dry_run") || call.Bool("compare");
            MarkupOptions options = null;
            LevelContent content = null;
            McpEditor.UI(() =>
            {
                content = McpEditor.RequireLevel(forEditing: !dryRun);
                if (!dryRun) McpEditor.RequireUndoIdle();
                options = ReadOptions(call, content.Level.Commands, content.Level);
            });
            int maxListed = Math.Max(0, call.Int("max_listed", 2000));
            JObject result = new JObject();

            if (call.Bool("compare"))
            {
                List<MarkupComparison> results;
                using (McpEditorTools.Heartbeat(call, "Comparing with the level's own markup"))
                    results = AIMarkup.Compare(call, content, options);
                if (results.Count == 0) call.Note("The level places none of the kinds asked for, so there is nothing to compare with.");
                result["compared"] = new JArray(results.Select(c => new JObject()
                {
                    ["kind"] = c.Kind.Key,
                    ["level_has"] = c.Level,
                    ["tool_finds"] = c.Ours,
                    ["area_m2"] = Math.Round(c.Area),
                    ["match_within_m"] = c.Within,
                    ["recall"] = Math.Round(c.Recall, 3),
                    ["precision"] = Math.Round(c.Precision, 3),
                    ["f1"] = Math.Round(c.F1, 3),
                    ["chance_recall"] = Math.Round(c.Chance, 3),
                    ["level_spacing_m"] = Math.Round(c.LevelSpacing, 2),
                    ["tool_spacing_m"] = Math.Round(c.OurSpacing, 2),
                    ["level_clearance_m"] = Math.Round(c.LevelClearance, 2),
                    ["tool_clearance_m"] = Math.Round(c.OurClearance, 2),
                    ["summary"] = c.Summary(),
                }));
                return result;
            }

            List<MarkupSpot> spots = new List<MarkupSpot>();
            MarkupProposal proposal = null;
            using (McpEditorTools.Heartbeat(call, "Finding spots for AI markup"))
            {
                if (call.Has("spots"))
                {
                    JArray given = call.Array("spots");
                    if (given.Count == 0 || given.Count > 5000) throw McpError.Invalid("'spots' takes 1 to 5000 spots.");
                    for (int i = 0; i < given.Count; i++)
                    {
                        if (!(given[i] is JObject g)) throw McpError.Invalid("spots[" + i + "] is not an object {kind, position, yaw}.");
                        spots.Add(new MarkupSpot()
                        {
                            Kind = AIMarkup.FindKind(g["kind"]?.ToString()) ?? throw McpError.Invalid("spots[" + i + "] needs a kind."),
                            Position = McpValues.ReadVector(g["position"], "spots[" + i + "].position", null),
                            Yaw = g["yaw"] == null ? 0f : (float)g["yaw"],
                        });
                    }
                }
                else
                {
                    proposal = AIMarkup.Propose(call, content, options);
                    spots = proposal.Spots;
                }
            }

            if (proposal != null)
            {
                result["walkable_area_m2"] = Math.Round(proposal.Area);
                result["samples"] = proposal.Samples;
                result["rejected"] = JObject.FromObject(proposal.Rejected.OrderByDescending(o => o.Value).ToDictionary(o => o.Key, o => o.Value));
                result["existing"] = JObject.FromObject(AIMarkup.Kinds.ToDictionary(k => k.Key, k => proposal.Existing.Count(o => o.Kind == k)));
                foreach (string note in proposal.Notes) call.Note(note);
            }
            result["found"] = JObject.FromObject(AIMarkup.Kinds.ToDictionary(k => k.Key, k => spots.Count(o => o.Kind == k)));
            result["spots"] = new JArray(spots.Take(maxListed).Select(Spot));
            if (spots.Count > maxListed) result["spots_not_listed"] = spots.Count - maxListed;
            if (spots.Count == 0)
            {
                result["placed"] = 0;
                return result;
            }
            List<MarkupKind> missing = McpEditor.UI(() => AIMarkup.Missing(content.Level.Commands, spots.Select(o => o.Kind)));
            if (dryRun)
            {
                result["dry_run"] = true;
                if (missing.Count != 0) result["would_port"] = new JArray(missing.Select(o => o.Path + " from " + (AIMarkup.SourceFor() ?? "(no level of this install has it)")));
                result["into"] = McpEditor.UI(() => (options.Into ?? content.Level.Commands.EntryPoints[0]).name);
                return result;
            }

            call.ThrowIfCancelled();
            if (missing.Count != 0) call.Progress("Bringing " + string.Join(", ", missing.Select(o => o.Path)) + " in from the install");
            MarkupPlaced placed;
            using (McpEditorTools.Heartbeat(call, "Placing " + spots.Count + " markup instances"))
                placed = McpEditor.UI(() => AIMarkup.Place(content, spots, options, "AI: ", call.Bool("show")));
            result["placed"] = placed.Names.Count;
            result["names"] = new JArray(placed.Names.Take(maxListed));
            result["into"] = placed.Into.name;
            if (placed.Ported != 0)
            {
                result["ported_composites"] = placed.Ported;
                result["ported_from"] = new JArray(placed.PortedFrom);
            }
            foreach (string note in placed.Notes) call.Note(note);
            call.Note("Porting cannot be undone; the markup is one undo step ('AI: Place AI Markup').");
            return result;
        }

        private static object CheckMarkup(McpCall call)
        {
            int max = Math.Max(1, call.Int("max_listed", 100));
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: false).Level;
                List<PlacedMarkup> markup = AIMarkup.Survey(level.Commands);
                List<MarkupStanding> standing = AIMarkup.OnNavmesh(level, markup);
                JObject result = new JObject() { ["level"] = level.Name };
                JArray kinds = new JArray();
                foreach (MarkupKind kind in AIMarkup.Kinds)
                {
                    List<PlacedMarkup> mine = markup.Where(o => o.Kind == kind).ToList();
                    JObject k = new JObject() { ["kind"] = kind.Key, ["count"] = mine.Count, ["users"] = mine.Count(o => o.User) };
                    if (mine.Count > 1) k["median_spacing_m"] = Math.Round(AIMarkup.MedianSpacing(mine.Select(o => o.Position).ToList()), 2);
                    List<MarkupStanding> off = standing.Where(o => o.Markup.Kind == kind && o.OnNavmesh == false).ToList();
                    if (off.Count != 0) k["off_navmesh"] = off.Count;
                    kinds.Add(k);
                }
                result["kinds"] = kinds;
                if (standing.Count == 0 && markup.Count != 0) result["navmesh"] = "none (Save & Build bakes one)";
                result["list"] = new JArray(markup.Take(max).Select(o =>
                {
                    JObject v = new JObject() { ["kind"] = o.Kind.Key, ["where"] = o.Where, ["position"] = McpCollision.V(o.Position), ["users"] = o.User };
                    if (o.Kind == AIMarkup.HighCover) v["yaw"] = o.Yaw;
                    MarkupStanding s = standing.FirstOrDefault(q => q.Markup == o);
                    if (s?.OnNavmesh != null) v["on_navmesh"] = s.OnNavmesh;
                    return v;
                }));
                if (markup.Count > max) result["not_listed"] = markup.Count - max;
                if (markup.Count == 0) call.Note("The level places no AI markup: NPCs that lose their target have no search spots, and the alien no sweep spots when it stalks.");
                if (BuildTracker.NeedsBuild) call.Note("The level has changes a Save & Build has not taken yet: the navmesh checks read the navmesh as last built.");
                return result;
            });
        }
    }
}
