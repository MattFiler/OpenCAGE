using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using CathodeLib.NavMesh;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace OpenCAGE.MCP
{
    /// <summary>Navigation mesh queries.</summary>
    /// <remarks>
    /// Over the navmesh the last Save &amp; Build wrote (a STATE's NAV_MESH, loaded with the level), read back into Detour
    /// (CathodeLib's NavigationMeshQuery). It does not follow unsaved edits: every result says how it may be out of date.
    /// </remarks>
    internal static class McpNavTools
    {
        private static readonly string[] Actions = { "info", "nearest", "path", "reachable", "sample" };
        private static readonly string[] Classes = { "any", "player", "alien", "android", "human_npc", "facehugger" };

        private static JObject Prop(string type, string description) => new JObject() { ["type"] = type, ["description"] = description };

        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "query_navmesh",
                Title = "Query navmesh",
                Description = "Ask the level's navigation mesh (as the last Save & Build wrote it - unsaved edits are not in it): 'nearest' the closest point on it to each point (is it on the navmesh?), " +
                    "'path' / 'reachable' whether a character can walk from one point to another (path corners, length, off-mesh links such as ladders and vents used), 'sample' walkable points spread over a region, 'info' its size and islands. " +
                    "Areas that start disabled (closed doors' barriers) are not crossed unless include_disabled; a path blocked only by them says so. Metres, world space, Y up.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("action", "info, nearest, path, reachable or sample. Default: nearest with 'points', path with 'from'/'to' or 'pairs', sample with 'region', else info.", options: Actions),
                    McpSchema.Nested("points", "nearest: points [[x,y,z], ...] (up to 1000).", new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" } } }),
                    McpSchema.Vector("from", "path / reachable: start [x,y,z]."),
                    McpSchema.Vector("to", "path / reachable: end [x,y,z]."),
                    McpSchema.Array("pairs", "path / reachable: several {from, to} at once (up to 100).", new JObject()
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject() { ["from"] = Prop("array", "[x,y,z]"), ["to"] = Prop("array", "[x,y,z]") },
                        ["required"] = new JArray("from", "to"),
                    }),
                    McpSchema.Nested("region", "sample (and info): where. " + McpRegion.Help, McpRegion.Schema),
                    McpSchema.Integer("count", "sample: how many points (default 20, at most 1000)."),
                    McpSchema.Integer("seed", "sample: random seed (default 1; the same seed gives the same points)."),
                    McpSchema.String("character_class", "Whose navmesh: any (default) or one class (areas admit some classes only).", options: Classes),
                    McpSchema.Boolean("include_disabled", "Cross areas that start disabled (doors, barriers opened by script). Default false."),
                    McpSchema.Vector("search_extents", "Half size [x,y,z] of the box searched round a point for the mesh (m, default [2,4,2])."),
                    McpSchema.Integer("state", "Which STATE's navmesh (default 0, the level's own).")),
                ReadOnly = true,
                Idempotent = true,
                Run = QueryNavmesh,
            };
        }

        #region Cache
        private static readonly object _lock = new object();
        private static NavigationMeshQuery _cached;
        private static NavigationMesh _cachedFor;

        /// <summary>Forget the cached query (the level is closing: it must not keep the old navmesh alive).</summary>
        internal static void DropCaches()
        {
            lock (_lock)
            {
                _cached = null;
                _cachedFor = null;
            }
        }

        /// <summary>A query over a STATE's saved navmesh, cached until the mesh changes (a build or another level gives a new one; other tools share it). UI thread.</summary>
        internal static NavigationMeshQuery QueryFor(Level level, int state, out NavigationMesh mesh)
        {
            //Closing the level drops the cache (McpCollision.DropAll)
            McpCollision.Hook();
            if (level.StateResources == null || level.StateResources.Count == 0)
                throw new McpError("This level has no STATE data loaded, so there is no navmesh to ask. Save & Build (save_level with build=true) makes one.");
            if (state < 0 || state >= level.StateResources.Count)
                throw new McpError(level.StateResources.Count == 1 ? "This level has one STATE (0): 'state' is 0." : "'state' is 0-" + (level.StateResources.Count - 1) + ".");
            mesh = level.StateResources[state]?.NavMesh;
            if (mesh?.Polygons == null || mesh.Polygons.Length == 0)
                throw new McpError("STATE_" + state + " has no navmesh (or an empty one). Save & Build (save_level with build=true) bakes it.");
            lock (_lock)
            {
                if (_cached != null && _cachedFor == mesh)
                    return _cached;
            }
            NavigationMeshQuery query;
            try
            {
                query = new NavigationMeshQuery(mesh);
            }
            catch (InvalidOperationException e)
            {
                throw new McpError("The navmesh could not be read for queries: " + e.Message);
            }
            lock (_lock)
            {
                _cached = query;
                _cachedFor = mesh;
            }
            return query;
        }
        #endregion

        private static object QueryNavmesh(McpCall call)
        {
            string action = call.Str("action");
            if (action == null)
                action = call.Has("points") ? "nearest" : call.Has("from") || call.Has("to") || call.Has("pairs") ? "path" : call.Has("region") ? "sample" : "info";
            action = action.Trim().ToLowerInvariant();
            if (!Actions.Contains(action)) throw new McpError("'action' is one of " + string.Join(", ", Actions) + ".");
            NavigationMeshQuery.Options options = new NavigationMeshQuery.Options() { IncludeDisabled = call.Bool("include_disabled") };
            string characterClass = (call.Str("character_class") ?? "any").Trim().ToLowerInvariant();
            int classIndex = Array.IndexOf(Classes, characterClass);
            if (classIndex < 0) throw new McpError("'character_class' is one of " + string.Join(", ", Classes) + ".");
            if (classIndex > 0) options.Classes = 1 << (classIndex - 1);
            if (call.Has("search_extents"))
            {
                Vector3 extents = McpValues.ReadVector(call.Token("search_extents"), "search_extents", null);
                if (extents.X <= 0 || extents.Y <= 0 || extents.Z <= 0) throw new McpError("'search_extents' are half sizes, each more than zero.");
                options.Extents = extents;
            }
            int state = call.Int("state", 0);

            NavigationMeshQuery query = null;
            McpRegion region = null;
            Vector3 boxMin = Vector3.Zero, boxMax = Vector3.Zero;
            bool haveBox = false, needsBuild = false, navmeshSkipped = false;
            List<string> reasons = null;
            int reasonCount = 0, stateCount = 0;
            McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                McpCollision.Hook();
                query = QueryFor(content.Level, state, out NavigationMesh _);
                stateCount = content.Level.StateResources.Count;
                //Unsaved logic edits leave the navmesh as it is: only changes a build reads make it out of date
                needsBuild = BuildTracker.NeedsBuild;
                if (needsBuild) reasons = BuildTracker.Reasons(3, out reasonCount);
                navmeshSkipped = BuildTracker.LastBuildSkipped.Any(o => o.IndexOf("nav", StringComparison.OrdinalIgnoreCase) >= 0);
                if (call.Has("region"))
                {
                    region = McpRegion.Resolve(call, content.Level.Commands, content.Level, call.Token("region"));
                    haveBox = McpCollision.RegionBox(call, content.Level.Commands, content.Level, region, out boxMin, out boxMax, out McpCollision.Gathered _);
                    if (!haveBox) throw new McpError(region.Label + " places nothing with collision or a model, so it has no extent to sample.");
                }
            });

            JObject result = new JObject()
            {
                ["navmesh"] = "STATE_" + state + " as last built" + (stateCount > 1 ? " (" + stateCount + " states)" : ""),
                ["character_class"] = characterClass,
            };
            if (needsBuild)
                result["stale"] = "Older than the script: the level has changes only a Save & Build gives the game (" + string.Join("; ", reasons) + (reasonCount > reasons.Count ? "; and " + (reasonCount - reasons.Count) + " more" : "") +
                    " - get_editor_state level.build lists them). save_level build=true (or build: 'auto') rebuilds the navmesh; a plain save does not.";
            else if (navmeshSkipped)
                result["stale"] = "The last Save & Build skipped the navmesh baker, so this is the navmesh from before it. save_level build=true with the navmesh baker rebuilds it.";

            switch (action)
            {
                case "info":
                    {
                        query.IslandOf(0, options);
                        result["polygons"] = query.PolygonCount;
                        result["ground_polygons"] = query.GroundPolygonCount;
                        result["off_mesh_links"] = query.OffMeshCount;
                        result["disabled_polygons"] = query.DisabledCount;
                        result["islands"] = query.IslandCount;
                        result["walkable_area_m2"] = Math.Round(query.Area(haveBox ? boxMin : (Vector3?)null, haveBox ? boxMax : (Vector3?)null, options), 1);
                        result["bounds"] = McpCollision.Aabb(query.BoundsMin, query.BoundsMax);
                        result["agent"] = new JObject() { ["height"] = Math.Round(query.WalkableHeight, 3), ["radius"] = Math.Round(query.WalkableRadius, 3), ["climb"] = Math.Round(query.WalkableClimb, 3) };
                        if (region != null) result["region"] = region.Label;
                        break;
                    }
                case "nearest":
                    {
                        JArray points = call.Array("points", required: true);
                        if (points.Count == 0 || points.Count > 1000) throw new McpError("'points' takes 1 to 1000 points.");
                        JArray results = new JArray();
                        for (int i = 0; i < points.Count; i++)
                        {
                            Vector3 p = McpValues.ReadVector(points[i], "points[" + i + "]", null);
                            NavigationMeshQuery.Nearest nearest = query.FindNearest(p, options);
                            if (!nearest.Found)
                            {
                                results.Add(new JObject() { ["point"] = McpCollision.V(p), ["on_navmesh"] = false, ["nearest"] = null, ["note"] = "No navmesh within " + options.Extents + " (search_extents) of it." });
                                continue;
                            }
                            bool on = nearest.OverPoly && Math.Abs(nearest.Point.Y - p.Y) <= 0.5f;
                            JObject r = new JObject()
                            {
                                ["point"] = McpCollision.V(p),
                                ["on_navmesh"] = on,
                                ["nearest"] = McpCollision.V(nearest.Point),
                                ["distance"] = Math.Round(nearest.Distance, 3),
                                ["island"] = nearest.Island,
                                ["polygon"] = nearest.Polygon,
                            };
                            if (!nearest.Enabled) r["area_disabled"] = true;
                            if (nearest.Height != NavigationMesh.AreaHeight.Standing) r["height_class"] = nearest.Height.ToString();
                            r["admits"] = new JArray(ClassNames(nearest.Classes));
                            results.Add(r);
                        }
                        result["points"] = results;
                        result["note"] = "on_navmesh: over a polygon and within 0.5 m of it vertically. Points on the same island can reach each other, both ways; " +
                            "a one-way link (a drop or a one-way ladder) leaves its two ends on different islands, so 'path' says whether one reaches the other.";
                        break;
                    }
                case "path":
                case "reachable":
                    {
                        List<(Vector3 from, Vector3 to)> pairs = new List<(Vector3, Vector3)>();
                        if (call.Has("from") || call.Has("to"))
                        {
                            if (!call.Has("from") || !call.Has("to")) throw new McpError("Give both 'from' and 'to'.");
                            pairs.Add((McpValues.ReadVector(call.Token("from"), "from", null), McpValues.ReadVector(call.Token("to"), "to", null)));
                        }
                        JArray given = call.Array("pairs");
                        if (given.Count > 100) throw new McpError("At most 100 pairs a call.");
                        for (int i = 0; i < given.Count; i++)
                        {
                            if (!(given[i] is JObject pair) || pair["from"] == null || pair["to"] == null) throw new McpError("pairs[" + i + "] is {from, to}.");
                            pairs.Add((McpValues.ReadVector(pair["from"], "pairs[" + i + "].from", null), McpValues.ReadVector(pair["to"], "pairs[" + i + "].to", null)));
                        }
                        if (pairs.Count == 0) throw new McpError("Give 'from' and 'to', or 'pairs'.");
                        JArray results = new JArray();
                        foreach ((Vector3 from, Vector3 to) in pairs)
                            results.Add(Path(query, from, to, options, action == "path"));
                        result[pairs.Count == 1 ? "path" : "paths"] = pairs.Count == 1 ? results[0] : results;
                        break;
                    }
                case "sample":
                    {
                        int count = call.Int("count", 20);
                        if (count < 1 || count > 1000) throw new McpError("'count' is 1 to 1000.");
                        List<Vector3> points = query.SamplePoints(count, haveBox ? boxMin : (Vector3?)null, haveBox ? boxMax : (Vector3?)null, options, call.Int("seed", 1));
                        if (points.Count == 0)
                            throw new McpError("No walkable navmesh" + (region != null ? " in " + region.Label : "") + " for " + characterClass + (options.IncludeDisabled ? "" : " (disabled areas left out; try include_disabled)") + ".");
                        result["points"] = new JArray(points.Select(McpCollision.V));
                        if (region != null) result["region"] = region.Label;
                        result["walkable_area_m2"] = Math.Round(query.Area(haveBox ? boxMin : (Vector3?)null, haveBox ? boxMax : (Vector3?)null, options), 1);
                        break;
                    }
            }
            return result;
        }

        private static JObject Path(NavigationMeshQuery query, Vector3 from, Vector3 to, NavigationMeshQuery.Options options, bool corners)
        {
            NavigationMeshQuery.PathResult path = query.FindPath(from, to, options);
            JObject result = new JObject() { ["from"] = McpCollision.V(from), ["to"] = McpCollision.V(to), ["reachable"] = path.Reachable };
            if (path.Failure != null && path.Corners.Count == 0)
                result["why_not"] = path.Failure + " (search_extents widens the search round the points)";
            else
            {
                result["start_on_mesh"] = McpCollision.V(path.Start);
                result["end_on_mesh"] = McpCollision.V(path.End);
                result["length"] = Math.Round(path.Length, 2);
                if (path.OffMeshLinks != 0) result["off_mesh_links_used"] = path.OffMeshLinks;
                if (path.Partial)
                {
                    result["gets_as_close_as"] = path.Corners.Count != 0 ? McpCollision.V(path.Corners[path.Corners.Count - 1]) : null;
                    result["gap"] = path.Corners.Count != 0 ? Math.Round(Vector3.Distance(path.Corners[path.Corners.Count - 1], path.End), 2) : (double?)null;
                }
                if (corners)
                {
                    result["corners"] = new JArray(path.Corners.Take(200).Select(McpCollision.V));
                    if (path.Corners.Count > 200) result["corners_not_listed"] = path.Corners.Count - 200;
                }
            }
            //Blocked only by an area that starts disabled (a closed door): say so
            if (!path.Reachable && !options.IncludeDisabled)
            {
                NavigationMeshQuery.Options open = new NavigationMeshQuery.Options() { Classes = options.Classes, IncludeDisabled = true, Extents = options.Extents };
                NavigationMeshQuery.PathResult through = query.FindPath(from, to, open);
                if (through.Reachable)
                    result["reachable_when_open"] = "Yes, through areas that start disabled (doors or barriers script opens): " + Math.Round(through.Length, 1) + " m.";
            }
            return result;
        }

        private static IEnumerable<string> ClassNames(int classes)
        {
            if ((classes & NavigationMeshQuery.AllClasses) == NavigationMeshQuery.AllClasses) { yield return "all"; yield break; }
            for (int i = 1; i < Classes.Length; i++)
                if ((classes & (1 << (i - 1))) != 0) yield return Classes[i];
        }
    }
}
