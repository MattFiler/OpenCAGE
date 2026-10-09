using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace OpenCAGE.MCP
{
    /// <summary>World-space questions about the level as it stands in the editor (unsaved edits included): collision raycasts, bounds, room sampling, world/local transforms, finding places by name.</summary>
    /// <remarks>
    /// Conventions every tool here states: positions [x,y,z] in metres, Y up; rotations [x,y,z] in degrees, x pitch, y yaw,
    /// z roll, applied yaw then pitch then roll; an entity faces its local +Z and a positive pitch looks down, so facing a
    /// direction d is [-asin(d.y/|d|), atan2(d.x, d.z), 0] (<see cref="InstanceTransform.LookRotation"/>).
    /// </remarks>
    internal static class McpSpatialTools
    {
        private const string Source = "the script as it is in the editor (unsaved changes included)";
        private const string Conventions = "Metres, Y up; rotations [pitch, yaw, roll] in degrees (an entity faces its +Z; positive pitch looks down).";

        private static JObject Prop(string type, string description) => new JObject() { ["type"] = type, ["description"] = description };

        private static JObject VectorProp(string description) => new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" }, ["minItems"] = 3, ["maxItems"] = 3, ["description"] = description };

        private static JObject Item(string description, JObject properties, params string[] required)
        {
            JObject item = new JObject() { ["type"] = "object", ["description"] = description, ["properties"] = properties };
            if (required.Length != 0) item["required"] = new JArray(required);
            return item;
        }

        private static readonly JObject PointsSchema = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" } } };

        private static readonly JObject FrameSchema = new JObject()
        {
            ["type"] = new JArray("object", "string"),
            ["properties"] = new JObject()
            {
                ["composite"] = Prop("string", "Inside this composite (path or id), at one of its placements: the space its entities' positions are written in."),
                ["placement"] = Prop("integer", "Which placement of 'composite' (0-based, get_placements order); needed when it is placed more than once."),
                ["path"] = new JObject() { ["description"] = "Or: an entity by its path from the root (ids/names): the space ITS position is written in (the composite holding it, at that placement)." },
            },
        };

        private const string FrameHelp = "{\"composite\": path, \"placement\": n} (the space of entities inside that composite placement), {\"path\": [ids from root]} (the space that entity's position is written in), or \"world\".";

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "find_places",
                Title = "Find places",
                Description = "Find 'the room called X': ranks zones, composites, instances and models whose names match a phrase (any case and word order, '_' and spaces alike, small misspellings tolerated), " +
                    "each with its world centre and size and a 'region' argument ready for get_bounds, sample_space and raycast. A zone ranks ahead of a composite or instance of the same name: zones are how retail levels mark rooms. " + Conventions,
                InputSchema = McpSchema.Object(
                    McpSchema.String("name", "What to look for, e.g. 'canteen' or 'observation room'.", required: true),
                    McpSchema.Nested("kinds", "Only these kinds (default all).", new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "string", ["enum"] = new JArray(McpRegion.PlaceKinds) } }),
                    McpSchema.Boolean("bounds", "Give each candidate's world centre and size (default true; false is faster)."),
                    McpSchema.Integer("limit", "At most this many candidates (default 8).")),
                ReadOnly = true,
                Idempotent = true,
                Run = FindPlaces,
            };

            yield return new McpTool()
            {
                Name = "get_bounds",
                Title = "Get bounds",
                Description = "How big a place is: the world AABB of a region's collision and/or models, from the placements as they are in the editor (unsaved edits included), per placement and in all, " +
                    "with each placement's tight box in its own axes ('local_box', for a rotated room), the floor and ceiling heights read off the collision, and the volume entities inside (PlayerTriggerBox, barriers and anything sized by half_dimensions) as boxes. " +
                    "A box volume stands on its position: it spans +-half_dimensions.x/z and 0..2*half_dimensions.y up, in its own rotation. " + Conventions,
                InputSchema = McpSchema.Object(
                    McpSchema.Nested("region", McpRegion.Help, McpRegion.Schema, required: true),
                    McpSchema.String("source", "collision, render (model bounds) or both (default).", options: new[] { "collision", "render", "both" }),
                    McpSchema.Boolean("volumes", "List the volume entities in the region (default true)."),
                    McpSchema.Integer("limit", "At most this many placements and volumes listed (default 20).")),
                ReadOnly = true,
                Idempotent = true,
                Run = GetBounds,
            };

            yield return new McpTool()
            {
                Name = "raycast",
                Title = "Raycast",
                Description = "Rays against the level's collision as it stands in the editor (each placed model's collision at its current transform, barriers as boxes; unsaved edits included, no viewport or save needed): " +
                    "'rays' give the first hit (point, distance, normal, floor/wall/ceiling and what was hit), 'floor_under' the floor and ceiling over points, 'line_of_sight' whether two points see each other. " +
                    "Up to 2000 queries a call. " + Conventions,
                InputSchema = McpSchema.Object(
                    McpSchema.Array("rays", "Rays: {from, direction | to, max_distance? (m, default 100; with 'to' the distance to it)}.", Item("A ray.", new JObject()
                    {
                        ["from"] = VectorProp("Start [x,y,z] (world)."),
                        ["direction"] = VectorProp("Direction (any length)."),
                        ["to"] = VectorProp("Or a point to aim at."),
                        ["max_distance"] = Prop("number", "Metres."),
                    }, "from")),
                    McpSchema.Nested("floor_under", "Points [[x,y,z], ...]: the floor below each (a ray down from 5 cm above it) and the ceiling over it.", PointsSchema),
                    McpSchema.Array("line_of_sight", "Pairs {from, to}: clear or what blocks the straight line.", Item("Two points.", new JObject() { ["from"] = VectorProp("[x,y,z]"), ["to"] = VectorProp("[x,y,z]") }, "from", "to")),
                    McpSchema.String("collision", McpCollision.FilterHelp, options: McpCollision.Filters),
                    McpSchema.Nested("region", "Optional: the collision to load. Default: everything of the level in a box round the queries. A zone or placement loads everything in its extent (grown 2 m); with content_only only its own collision. " + McpRegion.Help, McpRegion.Schema),
                    McpSchema.Boolean("content_only", "With a region of placements or a zone: only the collision those placements hold, not their surroundings."),
                    McpSchema.Nested("ignore", "Placements whose collision rays pass through: paths from the root (arrays of ids/names, or 'a/b/c' strings).", new JObject() { ["type"] = "array" }),
                    McpSchema.Boolean("all_hits", "rays: every hit along each ray (up to 16), nearest first."),
                    McpSchema.Boolean("through_windows", "line_of_sight: see-through colliders (windows) do not block (default true)."),
                    McpSchema.Number("max_drop", "floor_under: how far down and up to look (m, default 20)."),
                    McpSchema.String("detail", "full (default up to 50 queries: what was hit, its path, the normal), brief (default up to 400: what was hit by name) or points (default beyond: just each hit point, floor height or true/false).", options: new[] { "full", "brief", "points" })),
                ReadOnly = true,
                Idempotent = true,
                Run = Raycast,
            };

            yield return new McpTool()
            {
                Name = "sample_space",
                Title = "Sample space",
                Description = "Points inside a room from its collision as it is in the editor (unsaved edits included): 'loop' a ring of points round the room at eye height, inset from the walls, each in line of sight of the next, " +
                    "with rotations looking at the centre (or along the path) and, given a duration, keys ready for animate_parameters (a camera track round the room); 'centre' a free spot in the middle at eye height; " +
                    "'grid' free standing points on the floor; 'volume' a box (position, rotation, half_dimensions) that fills the room, ready for create_entities. " +
                    "Points are world space, and also in 'space' when given (pass the space the animated or created entity's position is written in). " + Conventions,
                InputSchema = McpSchema.Object(
                    McpSchema.Nested("region", McpRegion.Help, McpRegion.Schema, required: true),
                    McpSchema.String("pattern", "loop (default), centre, grid or volume.", options: new[] { "loop", "centre", "grid", "volume" }),
                    McpSchema.Integer("count", "loop: points round the room (default 12)."),
                    McpSchema.Number("height", "Eye height above the floor (m, default 1.6)."),
                    McpSchema.Number("inset", "loop: how far in from the walls (m, default 0.75)."),
                    McpSchema.Number("smooth", "loop: doorways and alcoves narrower than this are passed over, not followed (m, default 3; 0 follows every one)."),
                    McpSchema.Nested("look_at", "loop: 'centre' (default; the room's centre at look_height), 'next' (along the path), 'none', or a point [x,y,z].", new JObject()),
                    McpSchema.Number("look_height", "loop: height above the floor of the centre looked at (m, default: height)."),
                    McpSchema.Vector("centre", "Where to stand (world [x,y,z]); default: the freest spot near the middle."),
                    McpSchema.Number("floor_y", "The floor to use (world Y) in a room with several levels; default the one with the most floor."),
                    McpSchema.Number("spacing", "grid: metres between points (default 1; widened, and said so, where the area would take over 40000)."),
                    McpSchema.Number("clearance", "grid: free distance needed round a point (m, default 0.4)."),
                    McpSchema.Number("duration", "loop: seconds for one lap; adds 'keys' [{time, position, rotation}] at a steady speed."),
                    McpSchema.Number("start_time", "loop: time of the first key (s, default 0)."),
                    McpSchema.Boolean("close_loop", "loop: the keys end back at the first point (default true)."),
                    McpSchema.Nested("space", "Also give points (and keys) in this space: " + FrameHelp, FrameSchema),
                    McpSchema.String("collision", McpCollision.FilterHelp, options: McpCollision.Filters),
                    McpSchema.Integer("max_points", "grid: at most this many (default 300).")),
                ReadOnly = true,
                Idempotent = true,
                Run = SampleSpace,
            };

            yield return new McpTool()
            {
                Name = "transform_points",
                Title = "Transform points",
                Description = "Convert positions and rotations between world space and the space an entity's position is written in (a composite's placement): every writer (create_entities, set_parameters, place_model, animate_parameters keys) takes positions relative to the composite, while get_placements, raycast and sample_space give world positions. " +
                    "Also 'look_at': the rotation an entity at one point needs to face another. A composite placed several times needs 'placement'. " + Conventions,
                InputSchema = McpSchema.Object(
                    McpSchema.Nested("frame", "The space: " + FrameHelp, FrameSchema, required: true),
                    McpSchema.String("to", "'local' (world -> frame) or 'world' (frame -> world).", required: true, options: new[] { "local", "world" }),
                    McpSchema.Nested("points", "Positions [[x,y,z], ...].", PointsSchema),
                    McpSchema.Array("transforms", "Transforms {position, rotation}: rotations are converted too (yaw unwrapped along the list).", Item("A transform.", new JObject() { ["position"] = VectorProp("[x,y,z]"), ["rotation"] = VectorProp("[pitch, yaw, roll] degrees") })),
                    McpSchema.Array("look_at", "{from, to} in the space you are converting FROM: the position and rotation, in the 'to' space, of an entity at 'from' facing 'to'.", Item("Look from a point at another.", new JObject() { ["from"] = VectorProp("[x,y,z]"), ["to"] = VectorProp("[x,y,z]") }, "from", "to")),
                    McpSchema.Boolean("unwrap_yaw", "Keep each yaw within 180 degrees of the one before (default true for 'transforms', a key sequence; false for 'look_at').")),
                ReadOnly = true,
                Idempotent = true,
                Run = TransformPoints,
            };

            yield return new McpTool()
            {
                Name = "drop_to_floor",
                Title = "Drop to floor",
                Description = "Move entities down (or up) onto the floor under them, against the level's collision as it is in the editor - works with the viewport off and with unsaved changes. " +
                    "Give 'composite' + 'entities' (their own positions change, or the alias's where an alias overrides one's position; a composite placed several times needs 'placement' to say which placement's floor to use), or 'paths' from the root for nested placements (an alias override moves just that placement). " +
                    "One undo step. The entity's own collision is ignored.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite the entities are in (path or id)."),
                    McpSchema.Strings("entities", "Entities of it (ids or names)."),
                    McpSchema.Integer("placement", "Which placement of the composite to measure the floor in (0-based), when it is placed more than once."),
                    McpSchema.Nested("paths", "Or: placements as paths from the root (each an array of ids/names, or 'a/b/c').", new JObject() { ["type"] = "array" }),
                    McpSchema.String("mode", "pivot (default: the entity's origin lands on the floor) or bounds_bottom (the bottom of its model's bounds does).", options: new[] { "pivot", "bounds_bottom" }),
                    McpSchema.Number("offset", "Metres above the floor to leave it (default 0)."),
                    McpSchema.Boolean("align_to_slope", "Tilt it to the floor's slope (default false: rotation kept)."),
                    McpSchema.Number("max_drop", "How far down to look (m, default 10)."),
                    McpSchema.Number("start_above", "The ray down starts this far above the entity's bottom (m, default 0.1): raise it (say 0.5) for something sunk into the floor, which then rises onto it."),
                    McpSchema.String("collision", "Which collision is floor (default walkable). " + McpCollision.FilterHelp, options: McpCollision.Filters)),
                Destructive = false,
                Run = DropToFloor,
            };

            yield return new McpTool()
            {
                Name = "export_region_collision",
                Title = "Export region collision",
                Description = "Write a region's collision as it is in the editor (each placed collider at its current world transform, unsaved edits included) to an OBJ file at an absolute path, one object per placed entity named by its path - for checking in Blender. " +
                    "Units 'opencage' (default: centimetres, Z mirrored, as export_collision_mesh writes) or 'metres' (world metres as they are). Refuses to replace a file unless overwrite. Changes nothing in the level.",
                InputSchema = McpSchema.Object(
                    McpSchema.Nested("region", McpRegion.Help, McpRegion.Schema, required: true),
                    McpSchema.String("path", "Absolute path of the .obj to write.", required: true),
                    McpSchema.Boolean("overwrite", "Replace the file if it exists."),
                    McpSchema.String("collision", McpCollision.FilterHelp, options: McpCollision.Filters),
                    McpSchema.Boolean("content_only", "With a zone or placements: only their own collision (default true; false takes everything in their extent)."),
                    McpSchema.String("units", "opencage (default) or metres.", options: new[] { "opencage", "metres" })),
                Destructive = true,
                Idempotent = true,
                Run = ExportRegionCollision,
            };
        }

        #region Shared reading
        private static Vector3 ReadPoint(JToken token, string what) => McpValues.ReadVector(token, what, null);

        private static List<Vector3> ReadPoints(McpCall call, string name, int max)
        {
            JArray array = call.Array(name);
            if (array.Count > max) throw new McpError("'" + name + "' takes at most " + max + " a call.");
            List<Vector3> points = new List<Vector3>();
            for (int i = 0; i < array.Count; i++) points.Add(ReadPoint(array[i], name + "[" + i + "]"));
            return points;
        }

        /// <summary>A composite placement's frame (the world transform its entities' positions are written relative to). UI thread.</summary>
        public static cTransform ReadFrame(Commands commands, JToken token, string what, out JObject described)
        {
            Composite root = commands.EntryPoints[0];
            described = new JObject();
            if (token == null || token.Type == JTokenType.Null)
                throw new McpError("'" + what + "' is required: " + FrameHelp);
            if (token.Type == JTokenType.String && (string.Equals((string)token, "world", StringComparison.OrdinalIgnoreCase) || string.Equals((string)token, "root", StringComparison.OrdinalIgnoreCase)))
            {
                described["composite"] = root.name;
                described["note"] = "world space (the root composite's space)";
                return new cTransform(Vector3.Zero, Vector3.Zero);
            }
            if (!(token is JObject obj))
                throw new McpError("'" + what + "' is " + FrameHelp);
            if (obj["path"] != null)
            {
                if (obj["composite"] != null) throw new McpError("'" + what + "' takes 'path' or 'composite', not both.");
                List<Entity> chain = McpRegion.ChainFromNames(commands, McpScript.PathSteps(obj["path"], what + ".path"));
                List<Entity> parent = chain.Take(chain.Count - 1).ToList();
                cTransform frame = parent.Count == 0 ? new cTransform(Vector3.Zero, Vector3.Zero) : McpRegion.Walker(commands).Evaluate(root, parent).World ?? new cTransform(Vector3.Zero, Vector3.Zero);
                Composite holder = root;
                foreach (Entity step in parent) holder = McpScript.InstancedComposite(commands, step);
                described["composite"] = holder?.name;
                described["entity"] = McpRegion.DescribeChain(commands, chain);
                described["placement_path"] = new JArray(parent.Select(o => McpScript.Id(o.shortGUID)));
                described["position"] = McpCollision.V(frame.position);
                described["rotation"] = McpCollision.V(frame.rotation);
                return frame;
            }
            if (obj["composite"] == null)
                throw new McpError("'" + what + "' is " + FrameHelp);
            Composite composite = McpScript.FindComposite(commands, McpValues.ReadString(obj["composite"]));
            int? placement = obj["placement"] != null ? McpValues.ReadInt(obj["placement"], what + ".placement") : (int?)null;
            cTransform placed = FrameOf(commands, composite, placement, "'" + what + "': {\"composite\": ..., \"placement\": n}", out JObject of);
            foreach (JProperty property in of.Properties()) described[property.Name] = property.Value;
            return placed;
        }

        /// <summary>
        /// The world transform of one placement of <paramref name="composite"/>: the frame its entities' positions are written in.
        /// Identity for the root. A composite placed more than once needs <paramref name="placement"/> (0-based, get_placements
        /// order of the real placements); the refusal lists them, naming <paramref name="argument"/> as how to pass one. UI thread.
        /// For writers that take world positions: InstanceTransform.ToLocal(FrameOf(...), worldTransform) is what to write.
        /// </summary>
        public static cTransform FrameOf(Commands commands, Composite composite, int? placement, string argument, out JObject described)
        {
            Composite root = commands.EntryPoints[0];
            described = new JObject() { ["composite"] = composite.name };
            if (composite == root)
            {
                described["note"] = "world space (the root composite's space)";
                return new cTransform(Vector3.Zero, Vector3.Zero);
            }
            McpPlacements walker = McpRegion.Walker(commands);
            List<McpPlacements.Placement> found = new List<McpPlacements.Placement>();
            int total = walker.PlacementsOf(root, composite, null, found, 1000, default(System.Threading.CancellationToken), realOnly: true);
            if (total == 0)
                throw new McpError(composite.name + " is not placed in the level, so it has no world frame. get_placements shows where things are placed.");
            int index = placement ?? 0;
            if (placement == null && total > 1)
                throw new McpError(composite.name + " is placed " + total + " times: pass " + argument + " with n 0-" + (total - 1) + ": " +
                    string.Join("; ", found.Take(10).Select((o, i) => i + ": " + McpRegion.DescribeChain(commands, o.Chain) + (o.World != null ? " at " + McpRegion.Format(o.World.position) : ""))) + ".");
            if (index < 0 || index >= found.Count)
                throw new McpError("The placement is 0-" + (Math.Min(total, found.Count) - 1) + " (" + composite.name + " is placed " + total + " time" + (total == 1 ? "" : "s") + ").");
            cTransform world = found[index].World ?? new cTransform(Vector3.Zero, Vector3.Zero);
            described["placement"] = index;
            described["placement_path"] = new JArray(found[index].Chain.Select(o => McpScript.Id(o.shortGUID)));
            described["position"] = McpCollision.V(world.position);
            described["rotation"] = McpCollision.V(world.rotation);
            return world;
        }

        /// <summary>Ignore-lists: paths from the root whose placements' collision does not count.</summary>
        private static List<List<Entity>> ReadIgnore(Commands commands, JToken token)
        {
            List<List<Entity>> ignore = new List<List<Entity>>();
            if (token == null || token.Type == JTokenType.Null) return ignore;
            JArray array = token as JArray ?? new JArray(token);
            //One path given as a bare array of names, rather than an array of paths
            if (array.Count != 0 && array.All(o => o.Type == JTokenType.String) && array.Count > 1 && !array.Any(o => ((string)o).Contains("/")))
            {
                try { ignore.Add(McpRegion.ChainFromNames(commands, array.Select(o => (string)o).ToList())); return ignore; }
                catch (McpError) { }
            }
            foreach (JToken path in array)
                ignore.Add(McpRegion.ChainFromNames(commands, McpScript.PathSteps(path)));
            return ignore;
        }

        private static bool StartsWith(List<Entity> chain, List<Entity> prefix)
        {
            if (chain == null || prefix.Count > chain.Count) return false;
            for (int i = 0; i < prefix.Count; i++)
                if (chain[i] != prefix[i]) return false;
            return true;
        }

        /// <summary>A box round points, snapped outward to an 8 m grid so nearby calls share one cached soup.</summary>
        private static McpRegion BoxAround(IEnumerable<Vector3> points, float grow)
        {
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            foreach (Vector3 p in points) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            if (min.X > max.X) throw new McpError("Nothing to cast: give rays, floor_under or line_of_sight.");
            min -= new Vector3(grow);
            max += new Vector3(grow);
            const float grid = 8f;
            min = new Vector3((float)Math.Floor(min.X / grid) * grid, (float)Math.Floor(min.Y / grid) * grid, (float)Math.Floor(min.Z / grid) * grid);
            max = new Vector3((float)Math.Ceiling(max.X / grid) * grid, (float)Math.Ceiling(max.Y / grid) * grid, (float)Math.Ceiling(max.Z / grid) * grid);
            return McpRegion.Box(min, max, "the level round the queries");
        }

        /// <summary>
        /// The soup for a region: its own content, or everything in its extent (grown by <paramref name="grow"/>). For the
        /// extent of a region of placements, <paramref name="gathered"/> is what it holds (null otherwise).
        /// </summary>
        private static McpCollision.Soup SoupOf(McpCall call, McpRegion region, string filter, bool contentOnly, float grow, out McpRegion used, out Vector3 contentMin, out Vector3 contentMax, out McpCollision.Gathered gathered)
        {
            contentMin = contentMax = Vector3.Zero;
            used = region;
            gathered = null;
            if (region.Roots.Count == 0 || contentOnly)
            {
                if (region.HasClip) { contentMin = region.ClipMin.Value; contentMax = region.ClipMax.Value; }
                McpCollision.Soup own = McpCollision.SoupFor(call, region, filter);
                if (!region.HasClip) { contentMin = own.Min; contentMax = own.Max; }
                return own;
            }
            Vector3 min = Vector3.Zero, max = Vector3.Zero;
            McpCollision.Gathered held = null;
            bool found = McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                return McpCollision.RegionBox(call, content.Level.Commands, content.Level, region, out min, out max, out held);
            });
            gathered = held;
            if (!found)
                throw new McpError(region.Label + " places nothing with collision or a model, so it has no extent to measure.");
            contentMin = min;
            contentMax = max;
            used = McpRegion.Box(min - new Vector3(grow), max + new Vector3(grow), region.Label + " and its surroundings");
            return McpCollision.SoupFor(call, used, filter);
        }

        private static JObject SoupSummary(McpCollision.Soup soup, McpRegion region)
        {
            JObject summary = new JObject()
            {
                ["source"] = Source,
                ["region"] = region.Label,
                ["collision"] = soup.Filter,
                ["colliders"] = soup.Records.Count,
                ["triangles"] = soup.TriangleCount,
            };
            if (soup.Truncated) summary["truncated"] = true;
            return summary;
        }
        #endregion

        #region find_places
        private static object FindPlaces(McpCall call)
        {
            string name = call.Str("name", required: true).Trim();
            List<string> kinds = call.StrList("kinds").Select(o => o.Trim().ToLowerInvariant()).ToList();
            foreach (string kind in kinds)
                if (!McpRegion.PlaceKinds.Contains(kind)) throw new McpError("'kinds' takes " + string.Join(", ", McpRegion.PlaceKinds) + ".");
            int limit = Math.Max(1, Math.Min(50, call.Int("limit", 8)));
            bool bounds = call.Bool("bounds", true);
            using (McpEditorTools.Heartbeat(call, "Finding places called '" + name + "'"))
            return McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                Commands commands = content.Level.Commands;
                McpCollision.Hook();
                List<McpRegion.Place> places = McpRegion.FindPlaces(call, commands, content.Level, name, kinds, limit);
                JArray candidates = new JArray();
                foreach (McpRegion.Place place in places)
                {
                    JObject candidate = new JObject()
                    {
                        ["kind"] = place.Kind,
                        ["name"] = place.Name,
                        ["detail"] = place.Detail,
                        ["match"] = place.Match,
                        ["region"] = place.Spec,
                    };
                    if (bounds)
                    {
                        try
                        {
                            McpRegion region = McpRegion.Resolve(null, commands, content.Level, place.Spec);
                            if (QuickBox(commands, content.Level, region, out Vector3 min, out Vector3 max, out bool extent))
                            {
                                candidate["centre"] = McpCollision.V((min + max) * 0.5f);
                                if (extent) candidate["size"] = McpCollision.V(max - min);
                                else candidate["extent"] = "none: no models or collision, just " + (region.Roots.Count == 1 ? "a position" : "positions");
                            }
                            if (region.Roots.Count > 1 && place.Kind != "zone") candidate["placements"] = region.Roots.Count;
                        }
                        catch (McpError e)
                        {
                            candidate["note"] = e.Message;
                        }
                    }
                    candidates.Add(candidate);
                }
                JObject result = new JObject() { ["query"] = name, ["candidates"] = candidates };
                if (candidates.Count == 0)
                    result["note"] = "Nothing matched. get_zones lists the zones, find_composites the composites, find_entities searches entity names.";
                else
                    result["hint"] = "Pass a candidate's 'region' to get_bounds, sample_space or raycast. Sizes are approximate (the content's extent, nested instances included).";
                if (McpRegion.Index(call, commands, content.Level).Truncated) result["truncated"] = "The level is too large to index fully; some instances may be missing.";
                return result;
            });
        }

        /// <summary>A region's approximate world box: each root's content extent (memoised per composite) at its placement. UI thread.</summary>
        private static bool QuickBox(Commands commands, Level level, McpRegion region, out Vector3 min, out Vector3 max, out bool extent)
        {
            min = new Vector3(float.MaxValue);
            max = new Vector3(float.MinValue);
            extent = false;
            if (region.Roots.Count == 0)
            {
                if (!region.HasClip) return false;
                min = region.ClipMin.Value;
                max = region.ClipMax.Value;
                extent = true;
                return true;
            }
            McpPlacements walker = McpRegion.Walker(commands);
            Composite root = commands.EntryPoints[0];
            foreach (List<Entity> chain in region.Roots.Take(200))
            {
                cTransform world = walker.Evaluate(root, chain).World;
                if (world == null) continue;
                Entity last = chain[chain.Count - 1];
                Composite child = McpScript.InstancedComposite(commands, last);
                McpCollision.LocalBounds local = null;
                if (child != null)
                    local = McpCollision.Bounds(commands, level, child);
                else if (last is FunctionEntity function && function.function.IsFunctionType)
                    local = EntityBounds(level, function);
                if (local == null || local.Empty)
                {
                    min = Vector3.Min(min, world.position);
                    max = Vector3.Max(max, world.position);
                    continue;
                }
                McpCollision.WorldBox(local.Min, local.Max, world, out Vector3 a, out Vector3 b);
                min = Vector3.Min(min, a);
                max = Vector3.Max(max, b);
                extent = true;
            }
            return min.X <= max.X;
        }

        /// <summary>A function entity's own extent (model bounds, collision, or its box), in its own space.</summary>
        private static McpCollision.LocalBounds EntityBounds(Level level, FunctionEntity function)
        {
            McpCollision.LocalBounds bounds = new McpCollision.LocalBounds();
            FunctionType type = function.function.AsFunctionType;
            if (type == FunctionType.ModelReference)
            {
                foreach (RenderableElements.Element element in function.GetResource(ResourceType.RENDERABLE_INSTANCE, true)?.RenderableInstance ?? new List<RenderableElements.Element>())
                {
                    if (element?.Model == null) continue;
                    bounds.Add(element.Model.MinBounds);
                    bounds.Add(element.Model.MaxBounds);
                }
                HavokPackfile.StaticCompoundShape proxy = function.GetResource(ResourceType.COLLISION_MAPPING, true)?.CollisionMapping?.CollisionProxy;
                if (proxy != null && level.Collision != null)
                {
                    McpCollision.ProxyMesh mesh = McpCollision.Mesh(level, proxy);
                    if (mesh.Positions.Length != 0) { bounds.Add(mesh.Min); bounds.Add(mesh.Max); }
                }
            }
            else if (McpCollision.IsBoxType(type) || function.GetParameter("half_dimensions") != null)
            {
                Vector3 half = McpCollision.HalfExtentsOf(function);
                bounds.Add(new Vector3(-half.X, 0, -half.Z));
                bounds.Add(new Vector3(half.X, 2 * half.Y, half.Z));
            }
            return bounds;
        }
        #endregion

        #region get_bounds
        private static object GetBounds(McpCall call)
        {
            string source = (call.Str("source") ?? "both").Trim().ToLowerInvariant();
            if (source != "collision" && source != "render" && source != "both")
                throw new McpError("'source' is collision, render or both.");
            bool withVolumes = call.Bool("volumes", true);
            int limit = Math.Max(1, call.Int("limit", 20));
            bool wantCollision = source != "render", wantRender = source != "collision";

            Commands commands = null;
            McpRegion region = null;
            McpCollision.Gathered gathered = null;
            List<cTransform> frames = new List<cTransform>();
            using (McpEditorTools.Heartbeat(call, "Measuring"))
            {
                McpEditor.UI(() =>
                {
                    LevelContent content = McpEditor.RequireLevel(forEditing: false);
                    commands = content.Level.Commands;
                    region = McpRegion.Resolve(call, commands, content.Level, call.Token("region"));
                    gathered = McpCollision.Gather(call, commands, content.Level, region, wantCollision ? "solid" : null, wantRender, withVolumes);
                    McpPlacements walker = McpRegion.Walker(commands);
                    foreach (List<Entity> chain in region.Roots)
                        frames.Add(walker.Evaluate(commands.EntryPoints[0], chain).World ?? new cTransform(Vector3.Zero, Vector3.Zero));
                });
            }

            //Per placement: which root each item came from (items are gathered root by root, so by chain prefix)
            int roots = Math.Max(1, region.Roots.Count);
            Box[] world = new Box[roots], local = new Box[roots];
            Box unionWorld = new Box(), unionLocal = new Box();
            int[] triangles = new int[roots], models = new int[roots], colliders = new int[roots];
            int RootOf(List<Entity> chain)
            {
                if (region.Roots.Count <= 1) return 0;
                for (int i = 0; i < region.Roots.Count; i++)
                    if (StartsWith(chain, region.Roots[i])) return i;
                return 0;
            }
            for (int i = 0; i < roots; i++) { world[i] = new Box(); local[i] = new Box(); }
            cTransform single = region.Roots.Count == 1 ? frames[0] : region.Frame;

            McpCollision.Soup soup = null;
            if (wantCollision && gathered.Colliders.Count != 0)
            {
                soup = McpCollision.Build(gathered, null, "solid", region.Label, bvh: false);
                foreach (McpCollision.Record record in soup.Records)
                {
                    int r = RootOf(record.Chain);
                    cTransform frame = region.Roots.Count > 0 ? frames[r] : null;
                    triangles[r] += record.TriangleCount;
                    colliders[r]++;
                    for (int t = record.FirstTriangle; t < record.FirstTriangle + record.TriangleCount; t++)
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            Vector3 p = soup.Vertex(soup.Faces[t * 3 + k]);
                            if (region.HasClip && !region.InClip(p)) continue;
                            world[r].Add(p);
                            unionWorld.Add(p);
                            if (frame != null) local[r].Add(InstanceTransform.PointToLocal(frame, p));
                            if (single != null) unionLocal.Add(InstanceTransform.PointToLocal(single, p));
                        }
                    }
                }
            }
            if (wantRender)
            {
                foreach (McpCollision.RenderBox box in gathered.Render)
                {
                    int r = RootOf(box.Chain);
                    cTransform frame = region.Roots.Count > 0 ? frames[r] : null;
                    models[r]++;
                    for (int c = 0; c < 8; c++)
                    {
                        Vector3 corner = new Vector3((c & 1) == 0 ? box.Min.X : box.Max.X, (c & 2) == 0 ? box.Min.Y : box.Max.Y, (c & 4) == 0 ? box.Min.Z : box.Max.Z);
                        Vector3 p = InstanceTransform.PointToWorld(box.World, corner);
                        if (region.HasClip && !region.InClip(p)) continue;
                        world[r].Add(p);
                        unionWorld.Add(p);
                        if (frame != null) local[r].Add(InstanceTransform.PointToLocal(frame, p));
                        if (single != null) unionLocal.Add(InstanceTransform.PointToLocal(single, p));
                    }
                }
            }
            if (unionWorld.Empty && (!withVolumes || gathered.Volumes.Count == 0))
                throw new McpError(region.Label + " has no " + (source == "collision" ? "collision" : source == "render" ? "models" : "collision or models") + (gathered.NotReal != 0 ? " (what it holds is deleted or templates)" : "") + ". " +
                    (source != "both" ? "Try source 'both'. " : "") + "get_placements shows what is placed there.");

            JObject result = new JObject() { ["source"] = Source, ["region"] = region.Label, ["space"] = "world", ["measured"] = source };
            JObject union = new JObject();
            if (!unionWorld.Empty) union["world_aabb"] = McpCollision.Aabb(unionWorld.Min, unionWorld.Max);
            if (single != null && !unionLocal.Empty && single.rotation != Vector3.Zero)
                union["local_box"] = LocalBox(unionLocal, single);
            if (soup != null && !unionWorld.Empty)
            {
                McpCollision.FloorAndCeiling(soup, unionWorld.Min, unionWorld.Max, out float? floor, out float? ceiling);
                if (floor != null) union["floor_y"] = McpCollision.R(floor.Value);
                if (ceiling != null) union["ceiling_y"] = McpCollision.R(ceiling.Value);
                if (floor != null && ceiling != null) union["floor_to_ceiling"] = McpCollision.R(ceiling.Value - floor.Value);
                union["collision_triangles"] = soup.TriangleCount;
                union["colliders"] = soup.Records.Count;
            }
            if (wantRender) union["models"] = gathered.Render.Count;
            result["bounds"] = union;

            if (region.Roots.Count > 1)
            {
                JArray placements = new JArray();
                for (int i = 0; i < region.Roots.Count && placements.Count < limit; i++)
                {
                    JObject placement = McpRegion.ChainJson(commands, region.Roots[i]);
                    placement["position"] = McpCollision.V(frames[i].position);
                    placement["rotation"] = McpCollision.V(frames[i].rotation);
                    if (!world[i].Empty) placement["world_aabb"] = McpCollision.Aabb(world[i].Min, world[i].Max);
                    if (!local[i].Empty && frames[i].rotation != Vector3.Zero) placement["local_box"] = LocalBox(local[i], frames[i]);
                    if (wantCollision) placement["collision_triangles"] = triangles[i];
                    if (wantRender) placement["models"] = models[i];
                    placements.Add(placement);
                }
                result["placements"] = placements;
                if (region.Roots.Count > limit) result["placements_not_listed"] = region.Roots.Count - limit;
            }
            else if (region.Roots.Count == 1)
            {
                result["placement"] = McpRegion.ChainJson(commands, region.Roots[0]);
                result["placement"]["position"] = McpCollision.V(frames[0].position);
                result["placement"]["rotation"] = McpCollision.V(frames[0].rotation);
            }

            if (withVolumes && gathered.Volumes.Count != 0)
            {
                JArray volumes = new JArray();
                foreach (McpCollision.Volume volume in gathered.Volumes.Take(limit))
                {
                    Vector3 centre = McpCollision.BoxCentre(volume.World, volume.HalfExtents);
                    McpCollision.WorldBox(-volume.HalfExtents, volume.HalfExtents, new cTransform(centre, volume.World.rotation), out Vector3 a, out Vector3 b);
                    JObject chain = McpRegion.ChainJson(commands, volume.Chain);
                    volumes.Add(new JObject()
                    {
                        ["entity"] = McpScript.EntityName(commands, volume.Composite, volume.Entity),
                        ["type"] = volume.Type,
                        ["composite"] = volume.Composite?.name,
                        ["path"] = chain["path"],
                        ["ids"] = chain["ids"],
                        ["position"] = McpCollision.V(volume.World.position),
                        ["rotation"] = McpCollision.V(volume.World.rotation),
                        ["half_dimensions"] = McpCollision.V(volume.HalfExtents),
                        ["centre"] = McpCollision.V(centre),
                        ["world_aabb"] = McpCollision.Aabb(a, b),
                    });
                }
                result["volumes"] = volumes;
                if (gathered.Volumes.Count > limit) result["volumes_not_listed"] = gathered.Volumes.Count - limit;
                result["volume_convention"] = "A box volume stands on its position: centre = position + rotation*(0, half_dimensions.y, 0); it spans +-half_dimensions.x/z and 0..2*half_dimensions.y up.";
            }
            if (gathered.NotReal != 0) result["left_out"] = gathered.NotReal + " deleted, template or shared-repeat entities were left out.";
            foreach (string note in region.Notes.Concat(gathered.Warnings)) call.Note(note);
            return result;
        }

        private sealed class Box
        {
            public Vector3 Min = new Vector3(float.MaxValue), Max = new Vector3(float.MinValue);
            public bool Empty => Min.X > Max.X;
            public void Add(Vector3 p) { Min = Vector3.Min(Min, p); Max = Vector3.Max(Max, p); }
        }

        /// <summary>A box in a placement's own axes, with the world centre and the transform that places it.</summary>
        private static JObject LocalBox(Box box, cTransform frame) => new JObject()
        {
            ["min"] = McpCollision.V(box.Min),
            ["max"] = McpCollision.V(box.Max),
            ["size"] = McpCollision.V(box.Max - box.Min),
            ["world_centre"] = McpCollision.V(InstanceTransform.PointToWorld(frame, (box.Min + box.Max) * 0.5f)),
            ["axes_rotation"] = McpCollision.V(frame.rotation),
        };
        #endregion

        #region raycast
        private sealed class RaySpec
        {
            public Vector3 From, Direction;
            public float Max;
        }

        private static object Raycast(McpCall call)
        {
            string filter = McpCollision.ReadFilter(call);
            JArray rayTokens = call.Array("rays"), losTokens = call.Array("line_of_sight");
            List<Vector3> floorPoints = ReadPoints(call, "floor_under", 2000);
            if (rayTokens.Count > 2000 || losTokens.Count > 2000) throw new McpError("At most 2000 rays and 2000 line_of_sight pairs a call.");
            if (rayTokens.Count + losTokens.Count + floorPoints.Count == 0) throw new McpError("Give 'rays', 'floor_under' or 'line_of_sight'.");
            float maxDrop = (float)call.Num("max_drop", 20);
            if (maxDrop <= 0) throw new McpError("'max_drop' must be more than zero.");
            bool allHits = call.Bool("all_hits");
            bool throughWindows = call.Bool("through_windows", true);
            int queries = rayTokens.Count + losTokens.Count + floorPoints.Count;
            string detail = (call.Str("detail") ?? (queries <= 50 ? "full" : queries <= 400 ? "brief" : "points")).Trim().ToLowerInvariant();
            if (detail != "full" && detail != "brief" && detail != "points") throw new McpError("'detail' is full, brief or points.");
            bool brief = detail != "full", points = detail == "points";

            List<RaySpec> rays = new List<RaySpec>();
            for (int i = 0; i < rayTokens.Count; i++)
            {
                if (!(rayTokens[i] is JObject ray)) throw new McpError("rays[" + i + "] is {from, direction | to, max_distance?}.");
                Vector3 from = ReadPoint(ray["from"] ?? throw new McpError("rays[" + i + "] needs 'from'."), "rays[" + i + "].from");
                Vector3 direction;
                float max = ray["max_distance"] != null ? (float)McpValues.ReadDouble(ray["max_distance"], "rays[" + i + "].max_distance") : 100f;
                if (ray["to"] != null)
                {
                    if (ray["direction"] != null) throw new McpError("rays[" + i + "] takes 'direction' or 'to', not both.");
                    Vector3 to = ReadPoint(ray["to"], "rays[" + i + "].to");
                    direction = to - from;
                    if (ray["max_distance"] == null) max = direction.Length();
                }
                else if (ray["direction"] != null)
                    direction = ReadPoint(ray["direction"], "rays[" + i + "].direction");
                else
                    throw new McpError("rays[" + i + "] needs 'direction' or 'to'.");
                if (direction.Length() < 1e-6f) throw new McpError("rays[" + i + "] has no direction (zero length).");
                if (max <= 0) throw new McpError("rays[" + i + "].max_distance must be more than zero.");
                rays.Add(new RaySpec() { From = from, Direction = Vector3.Normalize(direction), Max = Math.Min(max, 5000f) });
            }
            List<(Vector3 from, Vector3 to)> sights = new List<(Vector3, Vector3)>();
            for (int i = 0; i < losTokens.Count; i++)
            {
                if (!(losTokens[i] is JObject pair) || pair["from"] == null || pair["to"] == null) throw new McpError("line_of_sight[" + i + "] is {from, to}.");
                sights.Add((ReadPoint(pair["from"], "line_of_sight[" + i + "].from"), ReadPoint(pair["to"], "line_of_sight[" + i + "].to")));
            }

            //The collision to load: the caller's region, or a box round every query
            Commands commands = null;
            McpRegion region = null;
            List<List<Entity>> ignore = null;
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                commands = content.Level.Commands;
                ignore = ReadIgnore(commands, call.Token("ignore"));
                if (call.Has("region"))
                    region = McpRegion.Resolve(call, commands, content.Level, call.Token("region"));
            });
            if (region == null)
            {
                List<Vector3> extent = new List<Vector3>();
                foreach (RaySpec ray in rays) { extent.Add(ray.From); extent.Add(ray.From + ray.Direction * Math.Min(ray.Max, 200f)); }
                foreach (Vector3 p in floorPoints) { extent.Add(p + new Vector3(0, maxDrop, 0)); extent.Add(p - new Vector3(0, maxDrop, 0)); }
                foreach ((Vector3 from, Vector3 to) in sights) { extent.Add(from); extent.Add(to); }
                region = BoxAround(extent, 2f);
                if (rays.Any(o => o.Max > 200f)) call.Note("Rays are only tested against collision within 200 m of their start unless you pass a region.");
            }
            McpCollision.Soup soup = SoupOf(call, region, filter, call.Bool("content_only"), 2f, out McpRegion used, out Vector3 _, out Vector3 _, out McpCollision.Gathered _);
            Func<McpCollision.Record, bool> skip = ignore.Count == 0 ? (Func<McpCollision.Record, bool>)null : r => ignore.Any(o => StartsWith(r.Chain, o));

            JObject result = SoupSummary(soup, used);
            if (soup.TriangleCount == 0)
                call.Note("There is no " + filter + " collision in " + used.Label + ": every ray misses. Try collision 'all', or a larger region.");

            //What was hit is named on the UI thread, all at once at the end
            List<(JObject into, string key, McpCollision.Record record)> describe = new List<(JObject, string, McpCollision.Record)>();
            JObject HitJson(McpCollision.Hit hit)
            {
                JObject h = new JObject()
                {
                    ["hit"] = true,
                    ["distance"] = McpCollision.R(hit.Distance),
                    ["point"] = McpCollision.V(hit.Point),
                };
                if (!brief) h["normal"] = McpCollision.V(hit.Normal);
                h["facing"] = McpCollision.Facing(hit.Normal);
                if (hit.Record != null) describe.Add((h, "hit_by", hit.Record));
                return h;
            }

            if (rays.Count != 0)
            {
                JArray results = new JArray();
                foreach (RaySpec ray in rays)
                {
                    if (allHits)
                    {
                        List<McpCollision.Hit> hits = soup.CastAll(ray.From, ray.Direction, ray.Max, 16, skip);
                        results.Add(points ? (JToken)new JArray(hits.Select(o => McpCollision.V(o.Point))) : new JObject() { ["hits"] = new JArray(hits.Select(HitJson)) });
                    }
                    else if (soup.Cast(ray.From, ray.Direction, ray.Max, out McpCollision.Hit hit, skip))
                        results.Add(points ? (JToken)McpCollision.V(hit.Point) : HitJson(hit));
                    else
                        results.Add(points ? JValue.CreateNull() : (JToken)new JObject() { ["hit"] = false });
                }
                result["rays"] = results;
            }
            if (floorPoints.Count != 0)
            {
                JArray results = new JArray();
                foreach (Vector3 p in floorPoints)
                {
                    bool found = soup.FloorUnder(p, out McpCollision.Hit floor, 0.05f, maxDrop, skip);
                    if (points)
                    {
                        results.Add(found ? (JToken)McpCollision.R(floor.Point.Y) : JValue.CreateNull());
                        continue;
                    }
                    JObject r = new JObject() { ["point"] = McpCollision.V(p) };
                    if (found)
                    {
                        JObject f = HitJson(floor);
                        f.Remove("hit");
                        f["y"] = McpCollision.R(floor.Point.Y);
                        f["slope_deg"] = McpCollision.SlopeDegrees(floor.Normal);
                        r["floor"] = f;
                        r["height_above_floor"] = McpCollision.R(p.Y - floor.Point.Y);
                    }
                    else
                        r["floor"] = null;
                    Vector3 from = found ? new Vector3(p.X, Math.Max(p.Y, floor.Point.Y + 0.05f), p.Z) : p;
                    if (soup.CeilingOver(from, out McpCollision.Hit ceiling, maxDrop, skip))
                    {
                        JObject c = HitJson(ceiling);
                        c.Remove("hit");
                        c["y"] = McpCollision.R(ceiling.Point.Y);
                        r["ceiling"] = c;
                        if (found) r["headroom"] = McpCollision.R(ceiling.Point.Y - floor.Point.Y);
                    }
                    results.Add(r);
                }
                result["floor_under"] = results;
                if (points) result["floor_under_is"] = "the floor's height (y) under each point, or null where there is none";
            }
            if (sights.Count != 0)
            {
                JArray results = new JArray();
                foreach ((Vector3 from, Vector3 to) in sights)
                {
                    float distance = Vector3.Distance(from, to);
                    bool clear = soup.Clear(from, to, out McpCollision.Hit blocker, throughWindows, skip);
                    if (points)
                        results.Add(clear);
                    else if (clear)
                        results.Add(new JObject() { ["clear"] = true, ["distance"] = McpCollision.R(distance) });
                    else
                    {
                        JObject blocked = new JObject() { ["clear"] = false, ["distance"] = McpCollision.R(distance), ["blocked_at"] = McpCollision.V(blocker.Point), ["distance_to_blocker"] = McpCollision.R(blocker.Distance) };
                        if (blocker.Record != null) describe.Add((blocked, "blocker", blocker.Record));
                        results.Add(blocked);
                    }
                }
                result["line_of_sight"] = results;
            }
            if (points) result["detail"] = "points: rays give the hit point or null, floor_under the floor's y or null, line_of_sight true when clear";            if (describe.Count != 0)
                McpEditor.UI(() =>
                {
                    foreach ((JObject into, string key, McpCollision.Record record) in describe)
                        into[key] = McpCollision.Describe(commands, record, brief);
                });
            foreach (string note in (region?.Notes ?? new List<string>()).Concat(soup.Warnings)) call.Note(note);
            return result;
        }
        #endregion

        #region sample_space
        private static object SampleSpace(McpCall call)
        {
            string pattern = (call.Str("pattern") ?? "loop").Trim().ToLowerInvariant();
            if (pattern != "loop" && pattern != "centre" && pattern != "center" && pattern != "grid" && pattern != "volume")
                throw new McpError("'pattern' is loop, centre, grid or volume.");
            if (pattern == "center") pattern = "centre";
            string filter = McpCollision.ReadFilter(call);
            int count = call.Int("count", 12);
            if (count < 3 || count > 200) throw new McpError("'count' is 3 to 200.");
            float height = (float)call.Num("height", 1.6);
            float inset = (float)call.Num("inset", 0.75);
            float lookHeight = (float)call.Num("look_height", height);
            float spacing = (float)call.Num("spacing", 1.0);
            float clearance = (float)call.Num("clearance", 0.4);
            float smooth = (float)call.Num("smooth", 3.0);
            if (smooth < 0) throw new McpError("'smooth' cannot be negative (0 keeps every opening).");
            int maxPoints = Math.Max(1, Math.Min(2000, call.Int("max_points", 300)));
            if (height <= 0 || height > 20) throw new McpError("'height' is metres above the floor, more than 0.");
            if (inset < 0) throw new McpError("'inset' cannot be negative.");
            if (spacing < 0.1f) throw new McpError("'spacing' is at least 0.1 m.");
            double? duration = call.Has("duration") ? call.Num("duration") : (double?)null;
            if (duration != null && duration <= 0) throw new McpError("'duration' is seconds, more than 0.");
            double startTime = call.Num("start_time", 0);
            bool closeLoop = call.Bool("close_loop", true);
            Vector3? givenCentre = call.Has("centre") ? McpValues.ReadVector(call.Token("centre"), "centre", null) : (Vector3?)null;
            float? givenFloor = call.Has("floor_y") ? (float)call.Num("floor_y") : (float?)null;
            JToken lookToken = call.Token("look_at");
            string lookMode = "centre";
            Vector3? lookPoint = null;
            if (lookToken != null)
            {
                if (lookToken is JArray) { lookMode = "point"; lookPoint = McpValues.ReadVector(lookToken, "look_at", null); }
                else
                {
                    lookMode = McpValues.ReadString(lookToken).Trim().ToLowerInvariant();
                    if (lookMode == "center") lookMode = "centre";
                    if (lookMode == "path") lookMode = "next";
                    if (lookMode != "centre" && lookMode != "next" && lookMode != "none") throw new McpError("'look_at' is 'centre', 'next', 'none' or a point [x,y,z].");
                }
            }

            McpRegion region = null;
            cTransform space = null;
            JObject spaceDescribed = null;
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                Commands commands = content.Level.Commands;
                region = McpRegion.Resolve(call, commands, content.Level, call.Token("region"));
                if (call.Has("space"))
                {
                    space = ReadFrame(commands, call.Token("space"), "space", out JObject described);
                    spaceDescribed = described;
                }
            });
            McpCollision.Soup soup = SoupOf(call, region, filter, false, 3f, out McpRegion used, out Vector3 bmin, out Vector3 bmax, out McpCollision.Gathered held);
            if (soup.TriangleCount == 0)
                throw new McpError("There is no " + filter + " collision in or round " + region.Label + ", so its space cannot be measured. Try collision 'all', or check get_bounds.");

            JObject result = SoupSummary(soup, used);
            result["region"] = region.Label;
            result["space"] = "world";
            result["room"] = McpCollision.Aabb(bmin, bmax);
            List<string> warnings = new List<string>();

            //The floor: given, under the given centre, or the height with the most floor in the room
            float floorY;
            McpCollision.FloorAndCeiling(soup, bmin, bmax, out float? histogramFloor, out float? histogramCeiling);
            if (givenFloor != null) floorY = givenFloor.Value;
            else if (givenCentre != null && soup.FloorUnder(givenCentre.Value, out McpCollision.Hit under, 0.05f, 20f)) floorY = under.Point.Y;
            else if (histogramFloor != null) floorY = histogramFloor.Value;
            else
            {
                Vector3 middle = (bmin + bmax) * 0.5f;
                if (!soup.Cast(new Vector3(middle.X, bmax.Y, middle.Z), -Vector3.UnitY, bmax.Y - bmin.Y + 1, out McpCollision.Hit down))
                    throw new McpError("No floor found in " + region.Label + ". Pass 'floor_y' or 'centre'.");
                floorY = down.Point.Y;
            }
            result["floor_y"] = McpCollision.R(floorY);
            if (histogramCeiling != null) result["ceiling_y"] = McpCollision.R(histogramCeiling.Value);
            result["eye_height"] = height;

            //Where to stand: the given centre, or the freest spot near the middle that stands on this floor
            Vector3 eye;
            float centreClearance;
            if (givenCentre != null)
            {
                eye = new Vector3(givenCentre.Value.X, floorY + height, givenCentre.Value.Z);
                centreClearance = Clearance(soup, eye, 16, Math.Max(bmax.X - bmin.X, bmax.Z - bmin.Z) + 5);
            }
            else if (!FreeCentre(soup, bmin, bmax, floorY, height, inset, out eye, out centreClearance))
                throw new McpError("No free spot at eye height (" + height + " m) on the floor at y=" + floorY.ToString("0.##", CultureInfo.InvariantCulture) + " found in " + region.Label + ". Pass 'centre' (a point in the room) or 'floor_y'.");
            result["centre"] = McpCollision.V(eye);
            result["centre_clearance"] = McpCollision.R(centreClearance);
            if (soup.CeilingOver(eye, out McpCollision.Hit over, 30f)) result["headroom_over_centre"] = McpCollision.R(over.Point.Y - floorY);

            JArray Local(Vector3 p) => space == null ? null : McpCollision.V(InstanceTransform.PointToLocal(space, p));

            //The doors placed round the room: where its ways in and out are
            if (pattern == "loop" || pattern == "centre")
            {
                JArray doors = McpEditor.UI(() =>
                {
                    LevelContent content = McpEditor.RequireLevel(forEditing: false);
                    //Doors on this floor only: a room's box can take in the storeys above and below
                    return DoorsIn(call, content.Level.Commands, content.Level, new Vector3(bmin.X - 0.75f, floorY - 1.0f, bmin.Z - 0.75f), new Vector3(bmax.X + 0.75f, floorY + Math.Max(2.0f, height), bmax.Z + 0.75f));
                });
                if (doors.Count != 0) result["doors"] = doors;
            }

            if (pattern == "centre")
            {
                result["floor_point"] = McpCollision.V(new Vector3(eye.X, floorY, eye.Z));
                if (space != null)
                {
                    result["in_space"] = spaceDescribed;
                    result["centre_local"] = Local(eye);
                    result["floor_point_local"] = Local(new Vector3(eye.X, floorY, eye.Z));
                }
            }
            else if (pattern == "grid")
                Grid(soup, result, bmin, bmax, floorY, height, spacing, clearance, maxPoints, space, spaceDescribed);
            else if (pattern == "volume")
                Volume(soup, result, region, held, bmin, bmax, floorY, histogramCeiling, space, spaceDescribed);
            else
                Loop(soup, result, warnings, bmin, bmax, eye, floorY, height, inset, count, lookMode, lookPoint, lookHeight, duration, startTime, closeLoop, space, spaceDescribed, smooth);

            if (warnings.Count != 0) result["warnings"] = new JArray(warnings);
            foreach (string note in region.Notes.Concat(soup.Warnings)) call.Note(note);
            return result;
        }

        /// <summary>
        /// Door instances placed in a world box: an instance of a composite taking a zone link that does not hand it up to the
        /// composite holding it (that one's instance is the door), and is not inside another door - as the zone tools find doors. UI thread.
        /// </summary>
        private static JArray DoorsIn(McpCall call, Commands commands, Level level, Vector3 min, Vector3 max)
        {
            McpRegion box = McpRegion.Box(min, max);
            Dictionary<Composite, ShortGuid> pins = new Dictionary<Composite, ShortGuid>();
            List<List<Entity>> found = new List<List<Entity>>();
            JArray doors = new JArray();
            McpCollision.WalkRegion(call, commands, level, box, true, step =>
            {
                if (!(step.Entity is FunctionEntity function) || function.function.IsFunctionType || !step.Real) return true;
                Composite placed = commands.GetComposite(function.function);
                if (placed == null) return true;
                if (!pins.TryGetValue(placed, out ShortGuid pin)) pins[placed] = pin = McpZoneTools.ZoneLinkPinOf(commands, placed);
                if (pin == ShortGuid.Invalid || McpZoneTools.HandsPinUp(step.Composite, function, pin)) return true;
                if (found.Any(o => StartsWith(step.Chain, o))) return true;
                cTransform world = step.World;
                if (world == null || !box.InClip(world.position)) return true;
                found.Add(new List<Entity>(step.Chain));
                McpCollision.LocalBounds extent = McpCollision.Bounds(commands, level, placed);
                JObject chain = McpRegion.ChainJson(commands, step.Chain);
                JObject door = new JObject()
                {
                    ["name"] = McpScript.EntityName(commands, step.Composite, function),
                    ["composite"] = placed.name,
                    ["ids"] = chain["ids"],
                    ["position"] = McpCollision.V(world.position),
                    ["rotation"] = McpCollision.V(world.rotation),
                };
                if (!extent.Empty)
                {
                    //Across the doorway is the narrower of its own X and Z
                    Vector3 size = extent.Max - extent.Min;
                    bool alongX = size.X >= size.Z;
                    door["width"] = McpCollision.R(alongX ? size.X : size.Z, 2);
                    door["height"] = McpCollision.R(size.Y, 2);
                    door["passage_axis"] = McpCollision.V(InstanceTransform.DirectionToWorld(new cTransform(Vector3.Zero, world.rotation), alongX ? Vector3.UnitZ : Vector3.UnitX));
                }
                doors.Add(door);
                return doors.Count < 40;
            }, out int _, out bool _);
            return doors;
        }

        /// <summary>The nearest hit of <paramref name="rays"/> horizontal rays round a point.</summary>
        private static float Clearance(McpCollision.Soup soup, Vector3 at, int rays, float reach)
        {
            float nearest = reach;
            for (int i = 0; i < rays; i++)
            {
                double a = 2 * Math.PI * i / rays;
                if (soup.Cast(at, new Vector3((float)Math.Sin(a), 0, (float)Math.Cos(a)), reach, out McpCollision.Hit hit))
                    nearest = Math.Min(nearest, hit.Distance);
            }
            return nearest;
        }

        /// <summary>The freest spot at eye height over a grid of the room that stands on the chosen floor, favouring the middle.</summary>
        private static bool FreeCentre(McpCollision.Soup soup, Vector3 bmin, Vector3 bmax, float floorY, float height, float inset, out Vector3 best, out float bestClearance)
        {
            best = Vector3.Zero;
            bestClearance = 0;
            double bestScore = double.MinValue;
            Vector3 middle = (bmin + bmax) * 0.5f;
            const int steps = 11;
            for (int ix = 0; ix < steps; ix++)
            {
                for (int iz = 0; iz < steps; iz++)
                {
                    float x = bmin.X + (bmax.X - bmin.X) * (ix + 0.5f) / steps, z = bmin.Z + (bmax.Z - bmin.Z) * (iz + 0.5f) / steps;
                    Vector3 eye = new Vector3(x, floorY + height, z);
                    //Standing on this floor (not on a table, not over a hole)
                    if (!soup.Cast(eye, -Vector3.UnitY, height + 0.6f, out McpCollision.Hit down) || Math.Abs(down.Point.Y - floorY) > 0.6f) continue;
                    if (soup.Cast(new Vector3(x, floorY + 0.3f, z), Vector3.UnitY, height - 0.25f, out McpCollision.Hit _)) continue;
                    //Enough rays to catch a pole or a lamp flex near the spot, not just the walls; each ray only as far as the room goes
                    const int rays = 48;
                    float[] distance = new float[rays];
                    for (int i = 0; i < rays; i++)
                    {
                        double a = 2 * Math.PI * i / rays;
                        Vector3 dir = new Vector3((float)Math.Sin(a), 0, (float)Math.Cos(a));
                        float exit = ExitDistance(eye, dir, bmin - new Vector3(0.5f), bmax + new Vector3(0.5f));
                        distance[i] = soup.Cast(eye, dir, exit, out McpCollision.Hit hit) ? hit.Distance : exit;
                    }
                    float clear = distance.Min();
                    if (clear < Math.Max(0.3f, inset * 0.8f)) continue;
                    //Seeing much of the room (a wide loop round it) counts most, then room to stand, then being near the middle
                    float median = distance.OrderBy(o => o).ElementAt(rays / 2);
                    double fromMiddle = Math.Sqrt((x - middle.X) * (x - middle.X) + (z - middle.Z) * (z - middle.Z));
                    double score = median + 0.5 * Math.Min(clear, 2.0) - 0.1 * fromMiddle;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = eye;
                        bestClearance = clear;
                    }
                }
            }
            return bestScore > double.MinValue;
        }

        private static void Loop(McpCollision.Soup soup, JObject result, List<string> warnings, Vector3 bmin, Vector3 bmax, Vector3 eye, float floorY, float height, float inset, int count,
            string lookMode, Vector3? lookPoint, float lookHeight, double? duration, double startTime, bool closeLoop, cTransform space, JObject spaceDescribed, float smooth)
        {
            //Fan rays from the centre at eye height, to the wall (or the room's edge, where nothing is hit)
            int fan = Math.Max(96, count * 8);
            float pad = 0.5f;
            Vector3[] points = new Vector3[fan], dirs = new Vector3[fan];
            float[] wall = new float[fan];
            bool[] open = new bool[fan];
            for (int i = 0; i < fan; i++)
            {
                double a = 2 * Math.PI * i / fan;
                dirs[i] = new Vector3((float)Math.Sin(a), 0, (float)Math.Cos(a));
                float exit = ExitDistance(eye, dirs[i], bmin - new Vector3(pad), bmax + new Vector3(pad));
                bool hit = soup.Cast(eye, dirs[i], exit, out McpCollision.Hit h);
                open[i] = !hit;
                wall[i] = hit ? h.Distance : exit;
            }

            /* Bridge over doorways and alcoves narrower than 'smooth': a morphological opening of the distance round the ring
               (the smallest distance over a window, then the largest of those), which cuts narrow spikes and keeps broad walls.
               Every point stays in sight of the centre, since it is never further out than the ray's own hit. */
            float[] reach = (float[])wall.Clone();
            if (smooth > 0)
            {
                float median = wall.OrderBy(o => o).ElementAt(fan / 2);
                int k = Math.Max(1, Math.Min(fan / 8, (int)Math.Ceiling(fan * smooth * 0.5 / (2 * Math.PI * Math.Max(0.5f, median)))));
                float[] eroded = new float[fan];
                for (int i = 0; i < fan; i++)
                {
                    float least = float.MaxValue;
                    for (int j = -k; j <= k; j++) least = Math.Min(least, wall[(i + j + fan) % fan]);
                    eroded[i] = least;
                }
                for (int i = 0; i < fan; i++)
                {
                    float most = 0;
                    for (int j = -k; j <= k; j++) most = Math.Max(most, eroded[(i + j + fan) % fan]);
                    reach[i] = Math.Min(wall[i], most);
                }

                /* And the other way: a pillar or lamp narrower than 'smooth' between the centre and the wall leaves a notch that
                   would pull the loop in towards the centre. Close it (the largest over a window, then the smallest) where the
                   point it gives is free, stands over the floor, and sees the points either side. */
                float[] dilated = new float[fan], closed = new float[fan];
                for (int i = 0; i < fan; i++)
                {
                    float most = 0;
                    for (int j = -k; j <= k; j++) most = Math.Max(most, reach[(i + j + fan) % fan]);
                    dilated[i] = most;
                }
                for (int i = 0; i < fan; i++)
                {
                    float least = float.MaxValue;
                    for (int j = -k; j <= k; j++) least = Math.Min(least, dilated[(i + j + fan) % fan]);
                    closed[i] = least;
                }
                float[] filled = (float[])reach.Clone();
                for (int i = 0; i < fan; i++)
                {
                    if (closed[i] < reach[i] + 0.3f) continue;
                    Vector3 p = eye + dirs[i] * Math.Max(0.3f, closed[i] - inset);
                    if (Clearance(soup, p, 16, Math.Max(0.3f, inset)) < Math.Max(0.25f, inset * 0.6f)) continue;
                    if (!soup.Cast(p, -Vector3.UnitY, eye.Y - floorY + 0.6f, out McpCollision.Hit down) || Math.Abs(down.Point.Y - floorY) > 0.6f) continue;
                    filled[i] = closed[i];
                }
                for (int i = 0; i < fan; i++)
                {
                    if (filled[i] == reach[i]) continue;
                    Vector3 p = eye + dirs[i] * Math.Max(0.3f, filled[i] - inset);
                    int before = (i + fan - k - 1) % fan, after = (i + k + 1) % fan;
                    Vector3 a = eye + dirs[before] * Math.Max(0.3f, filled[before] - inset), b = eye + dirs[after] * Math.Max(0.3f, filled[after] - inset);
                    if (Sees(soup, a, p) && Sees(soup, p, b)) reach[i] = filled[i];
                }
            }
            for (int i = 0; i < fan; i++)
                points[i] = eye + dirs[i] * Math.Max(Math.Min(0.3f, reach[i] * 0.5f), reach[i] - inset);

            //Openings: runs of rays that reach the room's edge with nothing in the way (doorways, open sides)
            JArray openings = new JArray();
            for (int i = 0; i < fan; i++)
            {
                if (!open[i] || open[(i + fan - 1) % fan]) continue;
                int j = i, length = 0;
                while (open[j % fan] && length < fan) { j++; length++; }
                int mid = (i + length / 2) % fan;
                double a = 2 * Math.PI * mid / fan;
                Vector3 dir = new Vector3((float)Math.Sin(a), 0, (float)Math.Cos(a));
                openings.Add(new JObject()
                {
                    ["toward"] = McpCollision.V(eye + dir * wall[mid]),
                    ["yaw"] = McpCollision.R(a * 180 / Math.PI, 1),
                    ["approx_width"] = McpCollision.R(2 * Math.PI * length / fan * wall[mid], 1),
                });
            }
            if (open.All(o => o))
                warnings.Add("Nothing was hit round the centre at eye height: the room's walls are not in its collision (or it is open). The points follow the room's extent.");
            float ringMedian = reach.OrderBy(o => o).ElementAt(fan / 2);
            float roomHalf = Math.Min(bmax.X - bmin.X, bmax.Z - bmin.Z) * 0.5f;
            if (ringMedian < roomHalf * 0.35f && roomHalf > 2f)
                warnings.Add("The loop keeps close to the centre (half its points within " + ringMedian.ToString("0.#", CultureInfo.InvariantCulture) + " m, in a room " + (roomHalf * 2).ToString("0.#", CultureInfo.InvariantCulture) +
                    " m across): something at eye height rings the centre. A higher 'height' (over the clutter), another 'centre' or a larger 'smooth' may give a wider loop.");

            //Pick 'count' of the fan's points evenly by distance round the ring (each is in sight of the centre)
            double[] arc = new double[fan + 1];
            for (int i = 0; i < fan; i++) arc[i + 1] = arc[i] + Vector3.Distance(points[i], points[(i + 1) % fan]);
            double perimeter = arc[fan];
            List<int> chosen = new List<int>();
            int at = 0;
            for (int k = 0; k < count; k++)
            {
                double target = perimeter * k / count;
                while (at < fan - 1 && Math.Abs(arc[at + 1] - target) <= Math.Abs(arc[at] - target)) at++;
                if (chosen.Count == 0 || chosen[chosen.Count - 1] != at) chosen.Add(at);
            }

            //Each point must see the next: where a corner hides it, take fan points in between until it does
            List<int> path = new List<int>();
            JArray blocked = new JArray();
            int segments = closeLoop ? chosen.Count : chosen.Count - 1;
            for (int k = 0; k < chosen.Count; k++)
            {
                path.Add(chosen[k]);
                if (k >= segments) continue;
                int a = chosen[k], b = chosen[(k + 1) % chosen.Count];
                int span = (b - a + fan) % fan;
                if (span == 0) continue;
                List<int> between = Bridge(soup, points, a, span, fan, 0);
                path.AddRange(between);
                int last = between.Count == 0 ? a : between[between.Count - 1];
                if (!Sees(soup, points[last], points[b]))
                    blocked.Add(new JObject() { ["from"] = McpCollision.V(points[last]), ["to"] = McpCollision.V(points[b]) });
            }
            //Points bunched together (a corner bridged with several) thinned out, as long as the loop still sees round
            for (int k = 1; k < path.Count - 1; k++)
            {
                if (Vector3.Distance(points[path[k - 1]], points[path[k]]) >= 0.5f) continue;
                if (!Sees(soup, points[path[k - 1]], points[path[k + 1]])) continue;
                path.RemoveAt(k);
                k--;
            }
            if (blocked.Count != 0)
                warnings.Add(blocked.Count + " stretch" + (blocked.Count == 1 ? "" : "es") + " of the loop pass through collision even with points added (the room is not open round its centre there): see 'blocked'. Try another 'centre', a smaller 'inset' or more points.");
            if (path.Count > count)
                warnings.Add((path.Count - count) + " point" + (path.Count - count == 1 ? " was" : "s were") + " added round corners so each point sees the next.");

            //Rotations, yaw unwrapped along the loop
            Vector3 centreLook = new Vector3(eye.X, floorY + lookHeight, eye.Z);
            List<Vector3> positions = path.Select(i => points[i]).ToList();
            List<Vector3> rotations = new List<Vector3>();
            for (int k = 0; k < positions.Count; k++)
            {
                Vector3 target;
                switch (lookMode)
                {
                    case "next": target = positions[(k + 1) % positions.Count]; if (!closeLoop && k == positions.Count - 1) target = positions[k] + (positions[k] - positions[k - 1]); break;
                    case "point": target = lookPoint.Value; break;
                    case "none": target = positions[k] + Vector3.UnitZ; break;
                    default: target = centreLook; break;
                }
                rotations.Add(lookMode == "none" ? Vector3.Zero : InstanceTransform.LookAt(positions[k], target));
            }
            InstanceTransform.UnwrapRotations(rotations);

            double along = 0;
            for (int k = 1; k < positions.Count; k++) along += Vector3.Distance(positions[k - 1], positions[k]);
            double loopLength = along + (closeLoop ? Vector3.Distance(positions[positions.Count - 1], positions[0]) : 0);
            result["loop_length"] = McpCollision.R(loopLength, 2);

            JArray pointsJson = new JArray();
            Vector3 lastLocal = Vector3.Zero;
            for (int k = 0; k < positions.Count; k++)
            {
                JObject p = new JObject() { ["position"] = McpCollision.V(positions[k]) };
                if (lookMode != "none") p["rotation"] = McpCollision.V(rotations[k]);
                p["clearance"] = McpCollision.R(Clearance(soup, positions[k], 16, 10f), 2);
                if (space != null)
                {
                    cTransform local = InstanceTransform.ToLocal(space, new cTransform(positions[k], rotations[k]));
                    Vector3 rotation = k == 0 ? local.rotation : new Vector3(local.rotation.X, InstanceTransform.UnwrapAngle(lastLocal.Y, local.rotation.Y), InstanceTransform.UnwrapAngle(lastLocal.Z, local.rotation.Z));
                    lastLocal = rotation;
                    p["local"] = new JObject() { ["position"] = McpCollision.V(local.position) };
                    if (lookMode != "none") p["local"]["rotation"] = McpCollision.V(rotation);
                }
                pointsJson.Add(p);
            }
            result["points"] = pointsJson;
            if (openings.Count != 0) result["openings"] = openings;
            if (blocked.Count != 0) result["blocked"] = blocked;
            if (space != null) result["in_space"] = spaceDescribed;

            if (duration != null)
            {
                //A steady speed: each key's time by distance along the loop; closed, the last key is the first again, yaw carried on
                JArray keys = new JArray();
                List<Vector3> keyPositions = new List<Vector3>(positions), keyRotations = new List<Vector3>(rotations);
                if (closeLoop)
                {
                    keyPositions.Add(positions[0]);
                    Vector3 back = rotations[0];
                    Vector3 previous = rotations[rotations.Count - 1];
                    keyRotations.Add(new Vector3(back.X, InstanceTransform.UnwrapAngle(previous.Y, back.Y), InstanceTransform.UnwrapAngle(previous.Z, back.Z)));
                }
                double travelled = 0;
                Vector3 previousLocal = Vector3.Zero;
                for (int k = 0; k < keyPositions.Count; k++)
                {
                    if (k > 0) travelled += Vector3.Distance(keyPositions[k - 1], keyPositions[k]);
                    double time = startTime + (loopLength > 0 ? duration.Value * travelled / loopLength : duration.Value * k / Math.Max(1, keyPositions.Count - 1));
                    cTransform key = new cTransform(keyPositions[k], keyRotations[k]);
                    if (space != null)
                    {
                        cTransform local = InstanceTransform.ToLocal(space, key);
                        //ToLocal re-derives the angles: keep the unwrapped turn going
                        Vector3 previous = k == 0 ? local.rotation : previousLocal;
                        key = new cTransform(local.position, new Vector3(local.rotation.X, InstanceTransform.UnwrapAngle(previous.Y, local.rotation.Y), InstanceTransform.UnwrapAngle(previous.Z, local.rotation.Z)));
                        previousLocal = key.rotation;
                    }
                    JObject k2 = new JObject() { ["time"] = McpCollision.R(time, 3), ["position"] = McpCollision.V(key.position) };
                    if (lookMode != "none") k2["rotation"] = McpCollision.V(key.rotation);
                    keys.Add(k2);
                }
                result["keys"] = keys;
                result["keys_space"] = space == null ? "world (the same as local for an entity in the root composite or any composite placed at the origin unturned; pass 'space' for another)" : "the 'space' given";
                result["keys_use"] = "animate_parameters set: [{\"target\": [camera], \"parameter\": \"position\", \"keys\": <keys>, \"tangents\": \"smooth\"}] with length " + McpCollision.R(startTime + duration.Value, 3) + ".";
            }
        }

        private static Vector3 ReadVector(JToken token) => McpValues.ReadVector(token, "rotation", null);

        /// <summary>Fan points between two to keep a loop in sight round a corner: the middle one, recursively, while the straight line is blocked.</summary>
        private static List<int> Bridge(McpCollision.Soup soup, Vector3[] points, int from, int span, int fan, int depth)
        {
            int to = (from + span) % fan;
            if (span <= 1 || depth > 8 || Sees(soup, points[from], points[to])) return new List<int>();
            int middle = (from + span / 2) % fan;
            List<int> left = Bridge(soup, points, from, span / 2, fan, depth + 1);
            List<int> right = Bridge(soup, points, middle, span - span / 2, fan, depth + 1);
            List<int> all = new List<int>(left) { middle };
            all.AddRange(right);
            return all;
        }

        /// <summary>A clear line between two points, and a hand's width either side of it.</summary>
        private static bool Sees(McpCollision.Soup soup, Vector3 a, Vector3 b)
        {
            if (!soup.Clear(a, b, out McpCollision.Hit _, throughSeeThrough: false)) return false;
            Vector3 d = b - a;
            Vector3 side = Vector3.Cross(Vector3.UnitY, d);
            if (side.LengthSquared() < 1e-8f) return true;
            side = Vector3.Normalize(side) * 0.15f;
            return soup.Clear(a + side, b + side, out McpCollision.Hit _, throughSeeThrough: false) && soup.Clear(a - side, b - side, out McpCollision.Hit _, throughSeeThrough: false);
        }

        /// <summary>How far along a horizontal ray from inside a box until it leaves the box's XZ extent.</summary>
        private static float ExitDistance(Vector3 from, Vector3 dir, Vector3 min, Vector3 max)
        {
            float t = float.MaxValue;
            if (dir.X > 1e-6f) t = Math.Min(t, (max.X - from.X) / dir.X);
            else if (dir.X < -1e-6f) t = Math.Min(t, (min.X - from.X) / dir.X);
            if (dir.Z > 1e-6f) t = Math.Min(t, (max.Z - from.Z) / dir.Z);
            else if (dir.Z < -1e-6f) t = Math.Min(t, (min.Z - from.Z) / dir.Z);
            return Math.Max(0.1f, t == float.MaxValue ? 50f : t);
        }

        private static void Grid(McpCollision.Soup soup, JObject result, Vector3 bmin, Vector3 bmax, float floorY, float height, float spacing, float clearance, int maxPoints, cTransform space, JObject spaceDescribed)
        {
            JArray points = new JArray();
            int free = 0;
            //At most MaxCells tested: a finer grid than that over the room is widened to fit, so all of the room is still covered
            const double MaxCells = 40000;
            float asked = spacing, dx = Math.Max(0f, bmax.X - bmin.X), dz = Math.Max(0f, bmax.Z - bmin.Z);
            double cells = Math.Ceiling(dx / spacing) * Math.Ceiling(dz / spacing);
            if (cells > MaxCells)
            {
                spacing = (float)Math.Sqrt(dx * (double)dz / MaxCells);
                while (Math.Ceiling(dx / spacing) * Math.Ceiling(dz / spacing) > MaxCells) spacing *= 1.01f;
            }
            for (float x = bmin.X + spacing * 0.5f; x < bmax.X; x += spacing)
            {
                for (float z = bmin.Z + spacing * 0.5f; z < bmax.Z; z += spacing)
                {
                    Vector3 probe = new Vector3(x, floorY + 0.5f, z);
                    if (!soup.Cast(probe, -Vector3.UnitY, 1.0f, out McpCollision.Hit down) || Math.Abs(down.Point.Y - floorY) > 0.4f) continue;
                    Vector3 stand = down.Point;
                    if (down.Normal.Y < 0.7f) continue;
                    if (soup.Cast(stand + new Vector3(0, 0.05f, 0), Vector3.UnitY, Math.Max(height, 1.0f), out McpCollision.Hit _)) continue;
                    if (Clearance(soup, stand + new Vector3(0, 0.5f, 0), 8, clearance) < clearance) continue;
                    if (Clearance(soup, stand + new Vector3(0, Math.Min(height, 1.5f), 0), 8, clearance) < clearance) continue;
                    free++;
                    if (points.Count >= maxPoints) continue;
                    JObject p = new JObject() { ["position"] = McpCollision.V(stand) };
                    if (space != null) p["local"] = McpCollision.V(InstanceTransform.PointToLocal(space, stand));
                    points.Add(p);
                }
            }
            result["spacing"] = McpCollision.R(spacing);
            if (spacing != asked)
                result["spacing_widened"] = "A " + asked.ToString("0.###", CultureInfo.InvariantCulture) + " m grid over this room is " + cells.ToString("0", CultureInfo.InvariantCulture) + " points, over the " + MaxCells +
                    " a call tests: it was widened to " + spacing.ToString("0.###", CultureInfo.InvariantCulture) + " m to cover the whole room. Give a smaller region for a finer grid.";
            result["free_points"] = free;
            result["free_area_m2"] = McpCollision.R(free * spacing * spacing, 1);
            result["points"] = points;
            if (free > points.Count) result["points_not_listed"] = free - points.Count;
            if (space != null) result["in_space"] = spaceDescribed;
            result["note"] = "Standing points on the floor at y~" + floorY.ToString("0.##", CultureInfo.InvariantCulture) + " with " + clearance + " m free round them at 0.5 m and " + Math.Min(height, 1.5f).ToString("0.##", CultureInfo.InvariantCulture) + " m up, and headroom to " + Math.Max(height, 1.0f).ToString("0.##", CultureInfo.InvariantCulture) + " m.";
        }

        private static void Volume(McpCollision.Soup soup, JObject result, McpRegion region, McpCollision.Gathered held, Vector3 bmin, Vector3 bmax, float floorY, float? ceilingY, cTransform space, JObject spaceDescribed)
        {
            //In the room's own axes when it is one turned placement, else world axes
            bool turned = region.Frame != null && region.Frame.rotation != Vector3.Zero && region.Roots.Count == 1;
            cTransform frame = turned ? new cTransform(Vector3.Zero, new Vector3(0, region.Frame.rotation.Y, 0)) : new cTransform(Vector3.Zero, Vector3.Zero);
            Box box = new Box();
            if (turned && held != null)
            {
                //From the content itself, as get_bounds' local_box: the world box's corners turned into the room's axes would be far too big
                foreach (McpCollision.Placed placed in held.Colliders)
                {
                    if (placed.Record.Ghosted) continue;
                    if (placed.IsBox)
                    {
                        Vector3 h = placed.HalfExtents;
                        for (int c = 0; c < 8; c++)
                            box.Add(InstanceTransform.PointToLocal(frame, Vector3.Transform(new Vector3((c & 1) == 0 ? -h.X : h.X, (c & 2) == 0 ? -h.Y : h.Y, (c & 4) == 0 ? -h.Z : h.Z), placed.Matrix)));
                    }
                    else
                    {
                        foreach (Vector3 p in placed.Mesh.Positions)
                            box.Add(InstanceTransform.PointToLocal(frame, Vector3.Transform(p, placed.Matrix)));
                    }
                }
                foreach (McpCollision.RenderBox render in held.Render)
                {
                    for (int c = 0; c < 8; c++)
                    {
                        Vector3 corner = new Vector3((c & 1) == 0 ? render.Min.X : render.Max.X, (c & 2) == 0 ? render.Min.Y : render.Max.Y, (c & 4) == 0 ? render.Min.Z : render.Max.Z);
                        box.Add(InstanceTransform.PointToLocal(frame, InstanceTransform.PointToWorld(render.World, corner)));
                    }
                }
                if (!box.Empty && region.Margin > 0)
                {
                    box.Add(box.Min - new Vector3(region.Margin));
                    box.Add(box.Max + new Vector3(region.Margin));
                }
            }
            if (box.Empty)
            {
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3((i & 1) == 0 ? bmin.X : bmax.X, (i & 2) == 0 ? bmin.Y : bmax.Y, (i & 4) == 0 ? bmin.Z : bmax.Z);
                    box.Add(InstanceTransform.PointToLocal(frame, corner));
                }
            }
            float top = ceilingY ?? bmax.Y;
            Vector3 half = new Vector3((box.Max.X - box.Min.X) * 0.5f, Math.Max(0.1f, (top - floorY) * 0.5f), (box.Max.Z - box.Min.Z) * 0.5f);
            Vector3 bottomCentre = InstanceTransform.PointToWorld(frame, new Vector3((box.Min.X + box.Max.X) * 0.5f, 0, (box.Min.Z + box.Max.Z) * 0.5f));
            bottomCentre.Y = floorY;
            JObject volume = new JObject()
            {
                ["position"] = McpCollision.V(bottomCentre),
                ["rotation"] = McpCollision.V(frame.rotation),
                ["half_dimensions"] = McpCollision.V(half),
                ["convention"] = "The box stands on its position: +-half_dimensions.x/z across, 0..2*half_dimensions.y up, in its rotation.",
            };
            if (ceilingY == null) volume["note"] = "No ceiling found: the height is the room's extent.";
            if (space != null)
            {
                cTransform local = InstanceTransform.ToLocal(space, new cTransform(bottomCentre, frame.rotation));
                volume["local"] = new JObject() { ["position"] = McpCollision.V(local.position), ["rotation"] = McpCollision.V(local.rotation) };
                result["in_space"] = spaceDescribed;
            }
            result["volume"] = volume;
        }
        #endregion

        #region transform_points
        private static object TransformPoints(McpCall call)
        {
            string to = (call.Str("to", required: true)).Trim().ToLowerInvariant();
            if (to != "local" && to != "world") throw new McpError("'to' is 'local' (world -> frame) or 'world' (frame -> world).");
            List<Vector3> points = ReadPoints(call, "points", 10000);
            JArray transformTokens = call.Array("transforms"), lookTokens = call.Array("look_at");
            if (points.Count + transformTokens.Count + lookTokens.Count == 0) throw new McpError("Give 'points', 'transforms' or 'look_at'.");
            if (transformTokens.Count > 10000 || lookTokens.Count > 10000) throw new McpError("At most 10000 of each a call.");

            JObject described = null;
            cTransform frame = McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: false);
                cTransform f = ReadFrame(commands, call.Token("frame"), "frame", out JObject d);
                described = d;
                return f;
            });
            bool toLocal = to == "local";
            JObject result = new JObject() { ["frame"] = described, ["to"] = toLocal ? "local (the frame's space)" : "world" };
            if (points.Count != 0)
                result["points"] = new JArray(points.Select(p => McpCollision.V(toLocal ? InstanceTransform.PointToLocal(frame, p) : InstanceTransform.PointToWorld(frame, p))));
            if (transformTokens.Count != 0)
            {
                List<cTransform> converted = new List<cTransform>();
                for (int i = 0; i < transformTokens.Count; i++)
                {
                    cTransform t = McpValues.ReadTransform(transformTokens[i], "transforms[" + i + "]", null);
                    converted.Add(toLocal ? InstanceTransform.ToLocal(frame, t) : InstanceTransform.Compose(frame, t));
                }
                List<Vector3> rotations = converted.Select(o => o.rotation).ToList();
                if (call.Bool("unwrap_yaw", true)) InstanceTransform.UnwrapRotations(rotations);
                result["transforms"] = new JArray(converted.Select((o, i) => new JObject() { ["position"] = McpCollision.V(o.position), ["rotation"] = McpCollision.V(rotations[i]) }));
            }
            if (lookTokens.Count != 0)
            {
                JArray looks = new JArray();
                List<Vector3> rotations = new List<Vector3>();
                List<Vector3> positions = new List<Vector3>();
                for (int i = 0; i < lookTokens.Count; i++)
                {
                    if (!(lookTokens[i] is JObject look) || look["from"] == null || look["to"] == null) throw new McpError("look_at[" + i + "] is {from, to}.");
                    Vector3 from = ReadPoint(look["from"], "look_at[" + i + "].from"), target = ReadPoint(look["to"], "look_at[" + i + "].to");
                    //Facing is worked out in the space the points are given in, then the transform converted
                    cTransform given = new cTransform(from, InstanceTransform.LookAt(from, target));
                    cTransform converted = toLocal ? InstanceTransform.ToLocal(frame, given) : InstanceTransform.Compose(frame, given);
                    positions.Add(converted.position);
                    rotations.Add(converted.rotation);
                }
                if (call.Bool("unwrap_yaw", false)) InstanceTransform.UnwrapRotations(rotations);
                for (int i = 0; i < positions.Count; i++)
                    looks.Add(new JObject() { ["position"] = McpCollision.V(positions[i]), ["rotation"] = McpCollision.V(rotations[i]) });
                result["look_at"] = looks;
            }
            return result;
        }
        #endregion

        #region drop_to_floor
        private sealed class DropTarget
        {
            public Composite Composite;      // holds the entity (or the alias, for a path)
            public Entity Entity;
            public List<Entity> Chain;       // from the root to the entity
            public cTransform World;         // where it sits now
            public cTransform Parent;        // the space its position is written in
            public float Bottom;             // world height of what lands on the floor (pivot, or its bounds' bottom)
            public AliasEntity Override;     // an alias already moving this placement
            public Composite OverrideOwner;
            public string OverrideLabel;
            public string Label;
        }

        private static object DropToFloor(McpCall call)
        {
            bool byPaths = call.Has("paths");
            if (byPaths == (call.Has("composite") || call.Has("entities")))
                throw new McpError("Give 'composite' + 'entities', or 'paths' (from the root), not both.");
            string mode = (call.Str("mode") ?? "pivot").Trim().ToLowerInvariant();
            if (mode != "pivot" && mode != "bounds_bottom") throw new McpError("'mode' is pivot or bounds_bottom.");
            float offset = (float)call.Num("offset", 0);
            float maxDrop = (float)call.Num("max_drop", 10);
            float startAbove = (float)call.Num("start_above", 0.1);
            if (maxDrop <= 0) throw new McpError("'max_drop' must be more than zero.");
            if (startAbove < 0) throw new McpError("'start_above' cannot be negative.");
            bool align = call.Bool("align_to_slope");
            string filter = call.Has("collision") ? McpCollision.ReadFilter(call) : "walkable";

            List<DropTarget> targets = new List<DropTarget>();
            Composite focus = null;
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: true);
                Commands commands = content.Level.Commands;
                Composite root = commands.EntryPoints[0];
                McpPlacements walker = McpRegion.Walker(commands);
                if (byPaths)
                {
                    JArray paths = call.Array("paths");
                    if (paths.Count == 0) throw new McpError("'paths' is empty.");
                    //One path given bare, as an array of names, rather than an array of paths
                    if (paths.All(o => o.Type == JTokenType.String) && paths.Count > 1 && !paths.Any(o => ((string)o).Contains("/")))
                    {
                        try
                        {
                            McpRegion.ChainFromNames(commands, paths.Select(o => (string)o).ToList());
                            paths = new JArray(new JArray(paths));
                        }
                        catch (McpError) { }
                    }
                    foreach (JToken token in paths)
                    {
                        List<Entity> chain = McpRegion.ChainFromNames(commands, McpScript.PathSteps(token, "paths"));
                        McpPlacements.Placement placement = walker.Evaluate(root, chain);
                        if (placement.World == null) throw new McpError(McpRegion.DescribeChain(commands, chain) + " has no position to drop.");
                        Composite holder = root;
                        foreach (Entity step in chain.Take(chain.Count - 1)) holder = McpScript.InstancedComposite(commands, step);
                        targets.Add(new DropTarget()
                        {
                            Composite = holder,
                            Entity = chain[chain.Count - 1],
                            Chain = chain,
                            World = placement.World,
                            Parent = chain.Count == 1 ? null : walker.Evaluate(root, chain.Take(chain.Count - 1).ToList()).World,
                            Override = placement.OverriddenBy,
                            OverrideOwner = placement.OverrideOwner,
                            Label = McpRegion.DescribeChain(commands, chain),
                        });
                    }
                    focus = root;
                }
                else
                {
                    Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                    List<string> names = call.StrList("entities", required: true);
                    if (names.Count == 0) throw new McpError("'entities' is empty.");
                    focus = composite;
                    //The placement of the composite whose floor counts
                    cTransform frame = null;
                    List<Entity> prefix = new List<Entity>();
                    if (composite != root)
                    {
                        List<McpPlacements.Placement> found = new List<McpPlacements.Placement>();
                        int total = walker.PlacementsOf(root, composite, null, found, 1000, call.Cancel, realOnly: true);
                        if (total == 0) throw new McpError(composite.name + " is not placed in the level, so there is no floor under its entities. Place an instance of it first (or use 'paths').");
                        int index = call.Has("placement") ? call.Int("placement") : 0;
                        if (!call.Has("placement") && total > 1)
                            throw new McpError(composite.name + " is placed " + total + " times; its entities' positions are shared by every placement. Pass 'placement' (0-" + (total - 1) + ") to say whose floor to use: " +
                                string.Join("; ", found.Take(10).Select((o, i) => i + ": " + McpRegion.DescribeChain(commands, o.Chain) + (o.World != null ? " at " + McpRegion.Format(o.World.position) : ""))) + ". Or use 'paths' to move one placement only.");
                        if (index < 0 || index >= found.Count) throw new McpError("'placement' is 0-" + (found.Count - 1) + ".");
                        frame = found[index].World;
                        prefix = found[index].Chain;
                    }
                    foreach (string name in names)
                    {
                        Entity entity = McpScript.FindEntity(commands, composite, name);
                        if (entity is AliasEntity || entity is ProxyEntity || entity is VariableEntity)
                            throw new McpError(McpScript.EntityName(commands, composite, entity) + " is " + McpScript.Kind(entity) + ": it has no position of its own to drop. Use 'paths' to the entity it stands for.");
                        List<Entity> chain = new List<Entity>(prefix) { entity };
                        McpPlacements.Placement placement = walker.Evaluate(root, chain);
                        if (placement.World == null) throw new McpError(McpScript.EntityName(commands, composite, entity) + " has no position.");
                        targets.Add(new DropTarget()
                        {
                            Composite = composite,
                            Entity = entity,
                            Chain = chain,
                            World = placement.World,
                            Parent = frame,
                            //An alias overriding its position wins over its own: that is what has to move
                            Override = placement.OverriddenBy,
                            OverrideOwner = placement.OverrideOwner,
                            Label = McpScript.EntityName(commands, composite, entity),
                        });
                    }
                }
                foreach (DropTarget target in targets)
                {
                    if (target.Override != null)
                        target.OverrideLabel = "alias " + McpScript.EntityName(commands, target.OverrideOwner, target.Override) + " (" + McpScript.Id(target.Override.shortGUID) + ") in " + target.OverrideOwner.name;
                    target.Bottom = target.World.position.Y;
                    if (mode != "bounds_bottom") continue;
                    McpCollision.LocalBounds local = null;
                    Composite child = McpScript.InstancedComposite(commands, target.Entity);
                    if (child != null) local = McpCollision.Bounds(commands, content.Level, child);
                    else if (target.Entity is FunctionEntity function && function.function.IsFunctionType) local = EntityBounds(content.Level, function);
                    if (local == null || local.Empty) continue;
                    McpCollision.WorldBox(local.Min, local.Max, target.World, out Vector3 a, out Vector3 _);
                    target.Bottom = a.Y;
                }
            });

            //Everything under the targets, with their own collision left out
            List<Vector3> extent = new List<Vector3>();
            foreach (DropTarget target in targets)
            {
                extent.Add(target.World.position + new Vector3(0, 1.5f, 0));
                extent.Add(new Vector3(target.World.position.X, target.Bottom - maxDrop - 1, target.World.position.Z));
            }
            McpRegion box = BoxAround(extent, 1f);
            McpCollision.Soup soup = McpCollision.SoupFor(call, box, filter);

            JArray moved = new JArray(), notMoved = new JArray();
            List<(DropTarget target, cTransform local)> writes = new List<(DropTarget, cTransform)>();
            foreach (DropTarget target in targets)
            {
                Func<McpCollision.Record, bool> own = r => StartsWith(r.Chain, target.Chain);
                Vector3 start = new Vector3(target.World.position.X, target.Bottom + startAbove, target.World.position.Z);
                if (!soup.Cast(start, -Vector3.UnitY, maxDrop + startAbove, out McpCollision.Hit floor, own))
                {
                    notMoved.Add(new JObject() { ["entity"] = target.Label, ["reason"] = "no " + filter + " floor within " + maxDrop + " m below it (max_drop; start_above if it is sunk into the floor)" });
                    continue;
                }
                float rise = floor.Point.Y + offset - target.Bottom;
                Vector3 position = target.World.position + new Vector3(0, rise, 0);
                Vector3 rotation = target.World.rotation;
                if (align && floor.Normal.Y > 0.2f)
                {
                    Vector3 n = Vector3.Normalize(floor.Normal);
                    Vector3 axis = Vector3.Cross(Vector3.UnitY, n);
                    Quaternion tilt = axis.LengthSquared() < 1e-10f ? Quaternion.Identity : Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), (float)Math.Acos(Math.Max(-1f, Math.Min(1f, n.Y))));
                    rotation = InstanceTransform.ToEulerDegrees(Quaternion.Normalize(tilt * InstanceTransform.ToQuaternion(rotation)));
                }
                if (Math.Abs(rise) < 0.002f && !align)
                {
                    notMoved.Add(new JObject() { ["entity"] = target.Label, ["reason"] = "already on the floor at y " + McpCollision.R(floor.Point.Y) });
                    continue;
                }
                cTransform world = new cTransform(position, rotation);
                cTransform local = InstanceTransform.ToLocal(target.Parent, world);
                writes.Add((target, local));
                JObject entry = new JObject()
                {
                    ["entity"] = target.Label,
                    ["from"] = McpCollision.V(target.World.position),
                    ["to"] = McpCollision.V(position),
                    ["moved_by"] = McpCollision.R(rise),
                    ["floor_y"] = McpCollision.R(floor.Point.Y),
                    ["local_position"] = McpCollision.V(local.position),
                };
                if (align) entry["rotation"] = McpCollision.V(rotation);
                if (target.Override != null) entry["written_to"] = target.OverrideLabel + ": it overrides this placement's position, so the move is written there";
                if (floor.Record != null) entry["floor_is"] = McpEditor.UI(() => McpCollision.Describe(McpEditor.RequireCommands(false), floor.Record, true));
                moved.Add(entry);
            }
            if (writes.Count == 0)
            {
                if (notMoved.All(o => ((string)o["reason"]).StartsWith("already", StringComparison.Ordinal)))
                    return new JObject() { ["moved"] = moved, ["not_moved"] = notMoved, ["note"] = "Everything is already on the floor: nothing changed (no undo step)." };
                throw new McpError("Nothing was moved: " + string.Join("; ", notMoved.Select(o => o["entity"] + " - " + o["reason"])) + ".");
            }

            JArray aliases = new JArray();
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run(writes.Count == 1 ? "Drop " + writes[0].target.Label + " to the floor" : "Drop " + writes.Count + " entities to the floor", focus, edit =>
            {
                Commands commands = edit.Commands;
                Composite root = commands.EntryPoints[0];
                foreach ((DropTarget target, cTransform local) in writes)
                {
                    JObject value = new JObject() { ["position"] = McpValues.Vector(local.position), ["rotation"] = McpValues.Vector(local.rotation) };
                    //An alias overriding the placement's position first: the entity's own would not move it (the alias's transform is in the same space)
                    if (target.Override != null)
                        edit.SetParameter(target.OverrideOwner, target.Override, "position", value, allowCustom: true);
                    else if (!byPaths || target.Chain.Count == 1)
                        edit.SetParameter(target.Composite, target.Entity, "position", value, allowCustom: true);
                    else
                    {
                        //Just this placement: an alias in the root composite overriding its position (the one on that path, when
                        //there is one already: two aliases on a path apply in no set order)
                        AliasEntity alias = edit.FindOrAddAlias(root, target.Chain.Select(o => o.shortGUID).Concat(new[] { ShortGuid.Invalid }).ToArray(), McpScript.EntityName(commands, target.Composite, target.Entity) + "_Dropped", out bool created);
                        edit.SetParameter(root, alias, "position", value, allowCustom: true);
                        aliases.Add(new JObject() { ["for"] = target.Label, ["alias"] = McpScript.Id(alias.shortGUID), ["composite"] = root.name, ["reused"] = !created });
                    }
                }
            });
            JObject result = new JObject() { ["moved"] = moved, ["source"] = Source, ["collision"] = filter };
            if (notMoved.Count != 0) result["not_moved"] = notMoved;
            if (aliases.Count != 0) result["aliases_made"] = aliases;
            if (!outcome.Changed) result["note"] = "Everything was already on the floor: nothing changed.";
            else result["undo"] = "One undo step: '" + outcome.Label + "'.";
            return result;
        }
        #endregion

        #region export_region_collision
        private static object ExportRegionCollision(McpCall call)
        {
            string path = call.Str("path", required: true).Trim();
            if (!Path.IsPathRooted(path)) throw new McpError("'path' must be an absolute path.");
            if (!string.Equals(Path.GetExtension(path), ".obj", StringComparison.OrdinalIgnoreCase)) throw new McpError("The file has to be an .obj.");
            if (File.Exists(path) && !call.Bool("overwrite")) throw new McpError(path + " already exists: pass overwrite: true to replace it.");
            string folder = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) throw new McpError("The folder " + folder + " does not exist.");
            string filter = McpCollision.ReadFilter(call);
            string units = (call.Str("units") ?? "opencage").Trim().ToLowerInvariant();
            if (units != "opencage" && units != "metres" && units != "meters") throw new McpError("'units' is opencage or metres.");
            bool opencage = units == "opencage";

            Commands commands = null;
            McpRegion region = null;
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                commands = content.Level.Commands;
                region = McpRegion.Resolve(call, commands, content.Level, call.Token("region"));
            });
            McpCollision.Soup soup = SoupOf(call, region, filter, call.Bool("content_only", true), 0f, out McpRegion used, out Vector3 _, out Vector3 _, out McpCollision.Gathered _);
            if (soup.TriangleCount == 0)
                throw new McpError("There is no " + filter + " collision in " + used.Label + ". Try collision 'all'.");

            Dictionary<McpCollision.Record, string> names = McpEditor.UI(() => soup.Records.ToDictionary(o => o, o => McpRegion.DescribeChain(commands, o.Chain)));
            StringBuilder obj = new StringBuilder();
            obj.AppendLine("# Collision of " + used.Label + " (" + soup.Filter + "), exported by OpenCAGE from " + Source + ": " + soup.TriangleCount + " triangles, " + soup.Records.Count + " colliders.");
            obj.AppendLine(opencage ? "# Centimetres, Z mirrored as OpenCAGE writes OBJ (so it re-imports at the same size and place)." : "# World metres, Y up, as the level has them.");
            string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
            for (int i = 0; i < soup.Vertices.Length; i += 3)
            {
                if (opencage) obj.Append("v ").Append(F(soup.Vertices[i] * 100f)).Append(' ').Append(F(soup.Vertices[i + 1] * 100f)).Append(' ').Append(F(-soup.Vertices[i + 2] * 100f)).AppendLine();
                else obj.Append("v ").Append(F(soup.Vertices[i])).Append(' ').Append(F(soup.Vertices[i + 1])).Append(' ').Append(F(soup.Vertices[i + 2])).AppendLine();
            }
            foreach (McpCollision.Record record in soup.Records)
            {
                string name = new string((names[record] + " " + McpScript.Id(record.Entity.shortGUID)).Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_').ToArray());
                obj.Append("o ").AppendLine(name);
                for (int t = record.FirstTriangle; t < record.FirstTriangle + record.TriangleCount; t++)
                {
                    int a = soup.Faces[t * 3] + 1, b = soup.Faces[t * 3 + 1] + 1, c = soup.Faces[t * 3 + 2] + 1;
                    if (opencage) obj.Append("f ").Append(a).Append(' ').Append(c).Append(' ').Append(b).AppendLine();
                    else obj.Append("f ").Append(a).Append(' ').Append(b).Append(' ').Append(c).AppendLine();
                }
            }
            try
            {
                File.WriteAllText(path, obj.ToString());
            }
            catch (Exception e)
            {
                throw new McpError("Could not write " + path + ": " + e.Message);
            }
            JObject result = SoupSummary(soup, used);
            result["file"] = Path.GetFullPath(path);
            result["objects"] = soup.Records.Count;
            result["units"] = opencage ? "opencage (cm, Z mirrored)" : "metres";
            result["world_aabb"] = McpCollision.Aabb(soup.Min, soup.Max);
            return result;
        }
        #endregion
    }
}
