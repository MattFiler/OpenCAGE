using Newtonsoft.Json.Linq;
using OpenCAGE.MapAssets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.MCP
{
    /// <summary>The Tab map helper (Scripts\MapAssets): generate a level's map, check its map items' floors. 'world' toolset.</summary>
    internal static class McpMapAssetTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "generate_map_assets",
                Title = "Generate map assets",
                Description = "Give the open level a Tab map, as File > Generate Map Assets does: the floors are found from the walk mesh as last built (the biggest walkable area, so roofs " +
                    "and the tops of furniture are left out; a floor per height most of the floor area is at) or given, each drawn in the game's map style, and written to the level's UI " +
                    "folder: UI\\HUB.GFX (the game's hub, loading the map page from the level's files) and UI\\FILES\\HUB_MAP.GFX + one movie per floor (Launch Game puts them in UI.PAK). " +
                    "The game only opens a map for a keyframe on its own list, so the level BORROWS the game's: RnD_HzdLab_1 for the first floor, RnD_HzdLab_2, And_Lab_0... for the next. " +
                    "In the root composite, as one undo step, it places MapAssets_Anchor (MapAnchor + PositionMarker, set by a ThinkOnce) and per floor PlayerTriggerBoxes (20 m squares over its area: a bigger box doesn't fire everywhere in it) firing its " +
                    "Map_Floor_Change, and sets map_keyframe on every map item (doors, terminals, pickups...) to the floor it stands on (per placement, by an alias, for one inside a composite placed on several floors); a second run replaces the first. " +
                    "Save the level and launch it through OpenCAGE to see the map. dry_run returns the floors, anchor and items, changing nothing.",
                InputSchema = McpSchema.Object(
                    McpSchema.Array("floors", "The floors, lowest first (default: found). Each {name, from, to}: the walkable heights (m) it takes, above 'from' up to 'to'; the lowest floor takes everything below, the highest everything above. 6 at most.",
                        new JObject()
                        {
                            ["type"] = "object",
                            ["properties"] = new JObject() { ["name"] = new JObject() { ["type"] = "string" }, ["from"] = new JObject() { ["type"] = "number" }, ["to"] = new JObject() { ["type"] = "number" } },
                        }),
                    McpSchema.Number("scale", "map_scale (default: chosen so most of the walkable area fits the map screen). Bigger draws the level bigger."),
                    McpSchema.String("map_name", "The map's name on the map page (default: the level's)."),
                    McpSchema.Boolean("dry_run", "Only work out the map and say what would be written and set.")),
                Destructive = true,
                Toolset = "world",
                Run = Generate,
            };

            yield return new McpTool()
            {
                Name = "check_map_items",
                Title = "Check map items",
                Description = "Every map item in the open level (the MapItem icons on the Tab map: doors, terminals, hack panels, pickups...) with the floor it stands on and the map keyframe it " +
                    "shows on, by the level's MapAnchor floors and generated floor boxes. An item whose map_keyframe is not its floor's shows on the wrong floor of the map, or on none. " +
                    "generate_map_assets sets them all.",
                InputSchema = McpSchema.Object(
                    McpSchema.Integer("max_items", "List at most this many (default 100).")),
                ReadOnly = true,
                Idempotent = true,
                Toolset = "world",
                Run = Check,
            };
        }

        private static object Generate(McpCall call)
        {
            bool dryRun = call.Bool("dry_run");
            MapAssetsOptions options = new MapAssetsOptions() { Scale = (float)call.Num("scale", 0), MapName = call.Str("map_name") };
            if (options.Scale < 0 || options.Scale > 10) throw McpError.Invalid("'scale' is above 0, up to 10.");
            if (call.Has("floors"))
            {
                JArray given = call.Array("floors");
                if (given.Count == 0 || given.Count > MapPlanner.MaxFloors) throw McpError.Invalid("'floors' takes 1 to " + MapPlanner.MaxFloors + ".");
                options.Floors = new List<MapFloor>();
                foreach (JToken f in given)
                {
                    float from = f["from"] != null ? (float)f["from"] : float.NegativeInfinity, to = f["to"] != null ? (float)f["to"] : float.PositiveInfinity;
                    options.Floors.Add(new MapFloor() { Name = (string)f["name"], From = from, To = to, Level = float.IsInfinity(from) ? (float.IsInfinity(to) ? 0 : to - 1) : (float.IsInfinity(to) ? from + 1 : (from + to) / 2) });
                }
            }
            LevelContent content = null;
            MapAssetsPlan plan = null;
            using (McpEditorTools.Heartbeat(call, "Working out the map"))
                McpEditor.UI(() =>
                {
                    content = McpEditor.RequireLevel(forEditing: !dryRun);
                    if (!dryRun) McpEditor.RequireUndoIdle();
                    plan = MapAssetGenerator.Propose(content, options);
                });
            foreach (string note in plan.Notes) call.Note(note);
            JObject result = Describe(plan);
            if (dryRun)
            {
                result["dry_run"] = true;
                return result;
            }
            call.ThrowIfCancelled();
            List<string> done;
            using (McpEditorTools.Heartbeat(call, "Writing the map"))
                done = MapAssetGenerator.Apply(content, plan, "AI: ");
            result["done"] = new JArray(done);
            return result;
        }

        private static JObject Describe(MapAssetsPlan plan)
        {
            return new JObject()
            {
                ["level"] = plan.Level,
                ["walkable_polygons"] = plan.Walkable.Count + " of " + plan.NavPolygons,
                ["floors"] = new JArray(plan.Floors.Select((f, i) => new JObject()
                {
                    ["name"] = f.Name,
                    ["keyframe"] = f.Keyframe,
                    ["from"] = float.IsNegativeInfinity(f.From) ? null : (JToken)Math.Round(f.From, 2),
                    ["to"] = float.IsPositiveInfinity(f.To) ? null : (JToken)Math.Round(f.To, 2),
                    ["level_m"] = Math.Round(f.Level, 2),
                    ["area_m2"] = Math.Round(f.Area),
                    ["outlines"] = plan.Drawn.Count > i ? plan.Drawn[i].Outlines.Count : 0,
                })),
                ["anchor"] = new JObject() { ["world_pos"] = new JArray(plan.Anchor.WorldX, plan.Anchor.WorldY, plan.Anchor.WorldZ), ["map_north"] = new JArray(plan.Anchor.NorthX, 0, plan.Anchor.NorthZ), ["map_scale"] = plan.Anchor.Scale },
                ["files"] = new JArray(plan.Files.Select(o => "UI\\" + o.Key.Replace('/', '\\') + " (" + o.Value.Length + " bytes)")),
                ["map_items"] = new JObject()
                {
                    ["total"] = plan.Items.Count,
                    ["to_set"] = plan.Items.Count(o => o.Wrong),
                    ["set_per_placement"] = new JArray(plan.Items.Where(o => o.Mixed).Select(o => o.Name)),
                },
            };
        }

        private static object Check(McpCall call)
        {
            int max = call.Int("max_items", 100);
            return McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                List<MapFloor> floors = MapItemCheck.FloorsOf(content.Level.Commands);
                List<MapItemFloor> items = MapAssetGenerator.MapItems(content.Level.Commands, floors);
                List<MapItemFloor> wrong = items.Where(o => o.Wrong).ToList();
                return new JObject()
                {
                    ["floors"] = new JArray(floors.Select(f => f.Keyframe + " (" + f.Name + ")")),
                    ["items"] = items.Count,
                    ["on_the_wrong_floor"] = wrong.Count,
                    ["list"] = new JArray(items.Take(max).Select(o => new JObject()
                    {
                        ["entity"] = o.Name,
                        ["composite"] = o.Composite.name,
                        ["at"] = new JArray(Math.Round(o.World.X, 2), Math.Round(o.World.Y, 2), Math.Round(o.World.Z, 2)),
                        ["map_keyframe"] = o.Was,
                        ["floor"] = o.Keyframe,
                        ["ok"] = !o.Wrong,
                        ["mixed"] = o.Mixed,
                        ["placements"] = o.Placements.Count <= 1 ? null : new JArray(o.Placements.Select(p => new JObject()
                        {
                            ["at"] = new JArray(Math.Round(p.World.X, 2), Math.Round(p.World.Y, 2), Math.Round(p.World.Z, 2)),
                            ["map_keyframe"] = p.Was,
                            ["floor"] = p.Keyframe,
                            ["by_alias"] = p.Alias != null,
                        })),
                    })),
                };
            });
        }
    }
}
