using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using CATHODE.Scripting.Internal;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.MCP
{
    /// <summary>The alien vents helper (Scripts\AlienVents): add vents, check a level's vents. 'world' toolset (with the navmesh tools).</summary>
    internal static class McpAlienVentTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "add_alien_vents",
                Title = "Add alien vents",
                Description = "Give the alien vents to go backstage through, as File > Alien Vents does: a level with an alien and no PathfindingAlienBackstageNode crashes the game the first time the alien retreats (flamed, sent away, after an ambush), " +
                    "and without vents it never uses the backstage. Finds spots on the navmesh (as last built) under a ceiling a retail vent fits - 4m VentTraversalMarker (ceilings 3.2-5.1 m), 6m VentTraversalMarker6M (5.1-7.6 m), 9m VentTraversalMarker9m (7.6-12 m) - " +
                    "with a clear column up, flat floor and no wall within 1 m, spread 'spacing' apart and from the level's own vents; or takes 'spots' you give. Brings the vent composites the level lacks in from a retail level of this install (as port_composites: NOT undoable), " +
                    "then places every vent as ONE undo step: instances AlienVent_N whose origin is the vent (its kind's height over the floor point), kill traps off on reset unless 'killtraps'. " +
                    "The backstage is baked by the next Save & Build with the navmesh baker (save_level build=true; bakers {cover:false, job_positions:false, sound:false} is enough for vents alone): a sheet 6 m over each network's vents (3 or more, not in a line) and a link up into each. " +
                    "check_alien_vents (and check_level's alien_backstage) then confirms they are baked and on the navmesh. dry_run lists the spots and what would be ported, changing nothing.",
                InputSchema = McpSchema.Object(
                    McpSchema.Integer("count", "How many vents to add (default: as many as the walkable area calls for, 3-12 counting the network's own)."),
                    McpSchema.Number("spacing", "The least distance between two vents, new or the level's own (m, default 8)."),
                    McpSchema.String("kind", "auto (default: by the ceiling over each spot), 4m, 6m or 9m.", options: new[] { "auto", "4m", "6m", "9m" }),
                    McpSchema.Boolean("killtraps", "Leave the vents' kill traps on (retail's default: the alien can drag a player standing under a vent up into it). Default false: DisableKilltrapOnReset is set."),
                    McpSchema.Integer("network_id", "The backstage network (Network_ID, default 0): a vent only joins vents of its own network, and each network needs 3 or more."),
                    McpSchema.String("composite", "The composite to put the vents in (path or id; placed once, or the root). " + McpSchema.CompositeDefaultRoot),
                    McpSchema.Nested("region", "Only find spots here (default: the whole navmesh). " + McpRegion.Help, McpRegion.Schema),
                    McpSchema.Nested("spots", "Instead of finding spots: floor points [[x,y,z], ...] (world) to put vents over; each is snapped onto the alien's navmesh (within 1.5 m) and tested like a found spot.", new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" } } }),
                    McpSchema.Boolean("strict", "With 'spots': refuse a spot that fails the tests (default true). false places it anyway on the navmesh point, its kind by the ceiling (6m when none is found)."),
                    McpSchema.String("name_prefix", "Instance names: prefix_N (default AlienVent)."),
                    McpSchema.String("page", "The flowgraph page their nodes go on (default AlienVents)."),
                    McpSchema.Integer("seed", "Sampling seed (default 1: the same level gives the same spots)."),
                    McpSchema.Boolean("show", "Take the editor to them (default false)."),
                    McpSchema.Boolean("dry_run", "Only find the spots and say what would be ported and placed.")),
                Destructive = true,
                Toolset = "world",
                Run = AddVents,
            };

            yield return new McpTool()
            {
                Name = "check_alien_vents",
                Title = "Check alien vents",
                Description = "The open level's alien vents as the game and the navmesh baker use them: every PathfindingAlienBackstageNode the level places (kind, network, floor point, whether the user's or retail's), " +
                    "each network's backstage sheet (vents, triangles the baker would make, spread), what the saved navmesh has (Backstage links, sheet polygons, each vent baked and on the alien's ground navmesh), whether an alien can spawn, " +
                    "and the issues: no vents with an alien (the game crashes), a network of fewer than 3 vents or in a line (no sheet), vents within 10 m of each other, vents not baked yet (Save & Build), vents off the navmesh. " +
                    "Issues only count vents the user placed or changed (retail's play as shipped). add_alien_vents fixes the first ones.",
                InputSchema = McpSchema.Object(
                    McpSchema.Integer("max_vents", "List at most this many vents (default 60).")),
                ReadOnly = true,
                Idempotent = true,
                Toolset = "world",
                Run = CheckVents,
            };
        }

        private static VentOptions ReadOptions(McpCall call, Commands commands, Level level)
        {
            VentOptions options = new VentOptions()
            {
                Count = call.Int("count", 0),
                Spacing = (float)call.Num("spacing", 8),
                Kind = AlienVents.FindKind(call.Str("kind")),
                Killtraps = call.Bool("killtraps"),
                Network = call.Int("network_id", 0),
                Seed = call.Int("seed", 1),
            };
            if (options.Count < 0 || options.Count > 64) throw McpError.Invalid("'count' is 1 to 64 (or leave it out).");
            if (options.Spacing < 1 || options.Spacing > 200) throw McpError.Invalid("'spacing' is 1 to 200 m.");
            if (call.Has("name_prefix")) options.NamePrefix = call.Str("name_prefix");
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

        private static JObject Spot(VentSpot spot, int index) => new JObject()
        {
            ["n"] = index,
            ["floor"] = McpCollision.V(spot.Floor),
            ["vent"] = McpCollision.V(spot.Top),
            ["ceiling_height"] = Math.Round(spot.Ceiling, 2),
            ["kind"] = spot.Kind?.Key,
            ["composite"] = spot.Kind?.Path,
            ["yaw"] = spot.Yaw,
            ["open_ahead_m"] = Math.Round(spot.OpenFor, 1),
        };

        private static JObject Sheet(AlienVents.Sheet sheet)
        {
            if (sheet == null) return null;
            JObject o = new JObject() { ["network"] = sheet.Network, ["vents"] = sheet.Vents, ["triangles"] = sheet.Triangles, ["spread_m"] = Math.Round(sheet.Spread, 1) };
            if (sheet.Warning != null) o["warning"] = sheet.Warning;
            return o;
        }

        private static object AddVents(McpCall call)
        {
            bool dryRun = call.Bool("dry_run");
            VentOptions options = null;
            LevelContent content = null;
            McpEditor.UI(() =>
            {
                content = McpEditor.RequireLevel(forEditing: !dryRun);
                if (!dryRun) McpEditor.RequireUndoIdle();
                options = ReadOptions(call, content.Level.Commands, content.Level);
            });

            List<VentSpot> spots = new List<VentSpot>();
            VentProposal proposal = null;
            JArray refused = new JArray();
            using (McpEditorTools.Heartbeat(call, "Finding spots for vents"))
            {
                if (call.Has("spots"))
                {
                    JArray given = call.Array("spots");
                    if (given.Count == 0 || given.Count > 64) throw McpError.Invalid("'spots' takes 1 to 64 points.");
                    bool strict = !call.Has("strict") || call.Bool("strict");
                    for (int i = 0; i < given.Count; i++)
                    {
                        Vector3 point = McpValues.ReadVector(given[i], "spots[" + i + "]", null);
                        VentSpot spot = AlienVents.SpotAt(call, content, point, options.Kind, out string why);
                        if (spot == null && !strict)
                            spot = Loose(call, content, point, options.Kind, ref why);
                        if (spot == null) { refused.Add(new JObject() { ["spot"] = McpCollision.V(point), ["why"] = why }); continue; }
                        spots.Add(spot);
                    }
                    if (refused.Count != 0 && (spots.Count == 0 || strict))
                        throw new McpError("No vents placed: " + string.Join("; ", refused.Select(o => o["spot"].ToString(Newtonsoft.Json.Formatting.None) + " " + o["why"])) +
                            ". Move the spot (it must be on the alien's navmesh, under a ceiling 3.2-12 m up), or pass strict: false to place it anyway.");
                }
                else
                {
                    proposal = AlienVents.Propose(call, content, options);
                    spots = proposal.Spots;
                }
            }

            JObject result = new JObject();
            if (proposal != null)
            {
                result["walkable_area_m2"] = proposal.WalkableArea;
                result["wanted"] = proposal.Wanted;
                result["samples"] = proposal.Samples;
                result["candidates"] = proposal.Candidates;
                result["rejected"] = JObject.FromObject(proposal.Rejected.OrderByDescending(o => o.Value).ToDictionary(o => o.Key, o => o.Value));
                result["existing_vents_in_network"] = proposal.Existing.Count;
                if (proposal.Sheet != null) result["sheet_after"] = Sheet(proposal.Sheet);
                foreach (string note in proposal.Notes) call.Note(note);
            }
            result["spots"] = new JArray(spots.Select((o, i) => Spot(o, i + 1)));
            if (refused.Count != 0) result["refused"] = refused;
            if (spots.Count == 0)
            {
                result["placed"] = 0;
                if (proposal != null && proposal.Wanted > 0)
                    call.Note("No spot passed: " + AlienVents.Describe(proposal.Rejected) + ". Give 'spots' by hand, lower 'spacing', or try kind 4m/6m/9m.");
                return result;
            }
            List<VentKind> missing = McpEditor.UI(() => AlienVents.Missing(content.Level.Commands, spots.Select(o => o.Kind)));
            if (dryRun)
            {
                result["dry_run"] = true;
                result["would_port"] = new JArray(missing.Select(o => o.Path + " from " + (AlienVents.SourceFor(o) ?? "(no level of this install has it)")));
                result["into"] = McpEditor.UI(() => (options.Into ?? content.Level.Commands.EntryPoints[0]).name);
                if (proposal == null)
                    result["sheet_after"] = Sheet(McpEditor.UI(() =>
                    {
                        List<Vector3> bottoms = AlienVents.Survey(content.Level.Commands).Where(o => o.Network == options.Network).Select(o => o.Bottom).ToList();
                        bottoms.AddRange(spots.Select(o => o.Floor));
                        return AlienVents.SheetFor(options.Network, bottoms);
                    }));
                return result;
            }

            call.ThrowIfCancelled();
            if (missing.Count != 0) call.Progress("Bringing " + string.Join(", ", missing.Select(o => o.Path)) + " in from the install");
            VentsPlaced placed;
            using (McpEditorTools.Heartbeat(call, "Placing " + spots.Count + " vents"))
                placed = McpEditor.UI(() => AlienVents.Place(content, spots, options, "AI: ", call.Bool("show")));
            result["placed"] = placed.Names.Count;
            result["names"] = new JArray(placed.Names);
            result["into"] = placed.Into.name;
            if (placed.Ported != 0)
            {
                result["ported_composites"] = placed.Ported;
                result["ported_from"] = new JArray(placed.PortedFrom);
            }
            if (proposal == null)
                result["sheet_after"] = Sheet(McpEditor.UI(() =>
                {
                    List<Vector3> bottoms = AlienVents.Survey(content.Level.Commands).Where(o => o.Network == options.Network).Select(o => o.Bottom).ToList();
                    return AlienVents.SheetFor(options.Network, bottoms);
                }));
            result["next_step"] = "save_level build=true (bakers {\"cover\":false,\"job_positions\":false,\"sound\":false} keeps those bakes) to bake the backstage, then check_alien_vents.";
            foreach (string note in placed.Notes) call.Note(note);
            call.Note("Porting cannot be undone; the vents are one undo step ('AI: Add Alien Vents').");
            return result;
        }

        /* strict: false - the navmesh point under the spot, its kind by the ceiling there (6 m when there is none or no kind fits), facing +Z */
        private static VentSpot Loose(McpCall call, LevelContent content, Vector3 point, VentKind kind, ref string why)
        {
            CathodeLib.NavMesh.NavigationMeshQuery query = AlienVents.QueryOf(content.Level, out NavigationMesh mesh);
            if (query == null) return null;
            CathodeLib.NavMesh.NavigationMeshQuery.Nearest at = query.FindNearest(point, new CathodeLib.NavMesh.NavigationMeshQuery.Options() { Classes = (int)CATHODE.Enums.NAVIGATION_CHARACTER_CLASS_COMBINATION.ALIEN, Extents = new Vector3(1.5f, 2f, 1.5f) });
            if (!at.Found || AlienVents.IsSheet(mesh, at.Polygon)) return null;
            McpCollision.Soup soup = McpCollision.SoupFor(call, McpRegion.Box(at.Point - new Vector3(2, 1, 2), at.Point + new Vector3(2, AlienVents.HighestCeiling + 2, 2), "over " + McpRegion.Format(at.Point)), "solid");
            float height = soup.CeilingOver(at.Point + new Vector3(0, 0.3f, 0), out McpCollision.Hit hit, AlienVents.HighestCeiling + 1f) ? hit.Point.Y - at.Point.Y : 0f;
            why += " (placed anyway: strict false)";
            return new VentSpot() { Floor = at.Point, Ceiling = height, Kind = kind ?? AlienVents.ForCeiling(height) ?? AlienVents.Vent6, Island = at.Island };
        }

        private static object CheckVents(McpCall call)
        {
            int max = Math.Max(1, call.Int("max_vents", 60));
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: false).Level;
                VentReport report = AlienVents.Check(level);
                AlienBackstageCheck.Result aliens = AlienBackstageCheck.Check(level.Commands);
                JObject result = new JObject()
                {
                    ["level"] = level.Name,
                    ["vents"] = report.Vents.Count,
                    ["users_vents"] = report.Vents.Count(o => o.User),
                    ["networks"] = new JArray(report.Sheets.Select(Sheet)),
                };
                if (aliens.Aliens.Count != 0) result["aliens_without_vents"] = new JArray(aliens.Aliens.Select(o => o.name));
                if (aliens.Crashes) result["crashes"] = AlienBackstageCheck.Warning(level.Commands);
                if (report.Baked != null)
                    result["navmesh"] = report.Baked.HasNavmesh
                        ? new JObject() { ["backstage_links"] = report.Baked.LinkBottoms.Count, ["backstage_polygons"] = report.Baked.SheetPolygons }
                        : (JToken)"none (Save & Build bakes one)";
                result["list"] = new JArray(report.Vents.Take(max).Select(o =>
                {
                    JObject v = new JObject()
                    {
                        ["where"] = o.Where,
                        ["kind"] = o.Kind?.Key ?? o.NodeComposite?.name,
                        ["network"] = o.Network,
                        ["floor"] = McpCollision.V(o.Bottom),
                        ["users"] = o.User,
                    };
                    if (o.Baked != null) v["baked"] = o.Baked;
                    if (o.OnNavmesh != null) v["on_navmesh"] = o.OnNavmesh;
                    return v;
                }));
                if (report.Vents.Count > max) result["vents_not_listed"] = report.Vents.Count - max;
                result["issues"] = new JArray(report.Issues.Select(o => new JObject()
                {
                    ["kind"] = o.Kind,
                    ["severity"] = o.Severity == LevelCheckSeverity.Crash ? "crash" : o.Severity == LevelCheckSeverity.Broken ? "broken" : "warning",
                    ["title"] = o.Title,
                    ["text"] = o.Text,
                    ["vents"] = AlienVents.List(o.Vents),
                }));
                if (report.Issues.Count == 0 && !aliens.Crashes) call.Note(report.Vents.Count == 0 ? "The level places no vents (and no alien that needs them)." : "Nothing found.");
                if (McpEditor.UI(() => BuildTracker.NeedsBuild)) call.Note("The level has changes a Save & Build has not taken yet: the navmesh checks read the navmesh as last built.");
                return result;
            });
        }
    }
}
