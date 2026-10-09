using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// A part of the level a spatial tool works on - a zone, the placements of a composite or entity, one placement by its
    /// path from the root, a place found by name, a world box or a sphere - resolved from the shared 'region' argument.
    /// </summary>
    /// <remarks>
    /// <para>A region is the subtrees under some placements (<see cref="Roots"/>, entity chains from the level root), a world
    /// clip box (<see cref="ClipMin"/>/<see cref="ClipMax"/>), or both. A box or sphere has no roots: it is everything of the
    /// level inside it. Every position is in world space (metres, Y up) unless a field says otherwise.</para>
    /// <para>Resolve on the UI thread: it reads the live script.</para>
    /// </remarks>
    internal sealed class McpRegion
    {
        /// <summary>zone, placement (one placement of a composite or entity), placements (several), box, sphere or level.</summary>
        public string Kind;
        /// <summary>What it is, for results ("zone Zone_canteen", "AYZ\Rooms\MyRoom placement 0").</summary>
        public string Label;
        /// <summary>The placements whose subtrees make up the region, as entity chains from the level root. Empty for box, sphere and level.</summary>
        public List<List<Entity>> Roots = new List<List<Entity>>();
        /// <summary>A world box the region is limited to (a box, a sphere's box, or a root region grown by 'margin' when asked to clip).</summary>
        public Vector3? ClipMin, ClipMax;
        /// <summary>A sphere's centre and radius ('near'): <see cref="InClip"/> tests the sphere, not just its box.</summary>
        public Vector3? Centre;
        public float Radius;
        /// <summary>Metres to grow the region by (the 'margin' argument).</summary>
        public float Margin;
        /// <summary>The region as an argument a tool takes again (e.g. {"zone": "Zone_canteen"}).</summary>
        public JObject Spec;
        /// <summary>When the region is one placement: its world transform (the axes its local box and local points are given in).</summary>
        public cTransform Frame;
        public List<string> Notes = new List<string>();

        public bool HasClip => ClipMin != null && ClipMax != null;

        /// <summary>A key that changes when the region would resolve to different content (paths and box), for caches.</summary>
        public string Key
        {
            get
            {
                StringBuilder key = new StringBuilder(Kind ?? "");
                foreach (List<Entity> root in Roots)
                {
                    key.Append('|');
                    foreach (Entity entity in root) key.Append(entity.shortGUID.AsUInt32.ToString("X8"));
                }
                if (HasClip) key.Append("|box").Append(KeyOf(ClipMin.Value)).Append(KeyOf(ClipMax.Value));
                if (Centre != null) key.Append("|sphere").Append(KeyOf(Centre.Value)).Append(Radius.ToString("0.###"));
                return key.ToString();
            }
        }

        private static string KeyOf(Vector3 v) => "(" + v.X.ToString("0.###") + "," + v.Y.ToString("0.###") + "," + v.Z.ToString("0.###") + ")";

        /// <summary>Whether a world point is inside the clip (always true without one).</summary>
        public bool InClip(Vector3 p)
        {
            if (Centre != null && Vector3.Distance(p, Centre.Value) > Radius + Margin) return false;
            if (!HasClip) return true;
            Vector3 min = ClipMin.Value, max = ClipMax.Value;
            return p.X >= min.X && p.Y >= min.Y && p.Z >= min.Z && p.X <= max.X && p.Y <= max.Y && p.Z <= max.Z;
        }

        /// <summary>Whether a world box overlaps the clip (always true without one).</summary>
        public bool TouchesClip(Vector3 min, Vector3 max)
        {
            if (!HasClip) return true;
            Vector3 a = ClipMin.Value, b = ClipMax.Value;
            return min.X <= b.X && max.X >= a.X && min.Y <= b.Y && max.Y >= a.Y && min.Z <= b.Z && max.Z >= a.Z;
        }

        /// <summary>A box region (world space).</summary>
        public static McpRegion Box(Vector3 a, Vector3 b, string label = null)
        {
            Vector3 min = Vector3.Min(a, b), max = Vector3.Max(a, b);
            return new McpRegion()
            {
                Kind = "box",
                Label = label ?? "box " + Format(min) + " to " + Format(max),
                ClipMin = min,
                ClipMax = max,
                Spec = new JObject() { ["box_min"] = McpValues.Vector(min), ["box_max"] = McpValues.Vector(max) },
            };
        }

        #region Schema
        public const string Help =
            "Where: {\"name\": \"canteen\"} (a zone, composite, instance or model found by name, as find_places ranks them), {\"zone\": \"Zone_canteen\"}, " +
            "{\"composite\": path, \"placement\": n} (one placement of a composite; leave placement out for all of them), {\"composite\", \"entity\"} (an entity's placements), " +
            "{\"path\": [ids/names from the root composite]}, {\"box_min\": [x,y,z], \"box_max\": [x,y,z]} or {\"near\": [x,y,z], \"radius\": m} (world space), \"level\" for the whole level; " +
            "'margin' (m) grows it. A plain string is a name.";

        public static readonly JObject Schema = new JObject()
        {
            ["type"] = new JArray("object", "string"),
            ["properties"] = new JObject()
            {
                ["name"] = new JObject() { ["type"] = "string", ["description"] = "A place by name: zone, composite, instance or model (misspellings and word order tolerated; an ambiguous name lists candidates)." },
                ["zone"] = new JObject() { ["type"] = "string", ["description"] = "A Zone entity's name or id (get_zones lists them); 'Zone_' may be left off." },
                ["composite"] = new JObject() { ["type"] = "string", ["description"] = "A composite (path, id or the end of its path): its placements, or with 'entity' that entity's." },
                ["entity"] = new JObject() { ["type"] = "string", ["description"] = "With 'composite': an entity of it (id or name)." },
                ["placement"] = new JObject() { ["description"] = "With 'composite' or 'zone': which placement, 0-based in the order get_placements lists them (or a path from the root as an array)." },
                ["path"] = new JObject() { ["description"] = "One placement: entity ids/names from the root composite through instances (an array, or one string split on '/')." },
                ["box_min"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" }, ["description"] = "World box corner [x,y,z] (m)." },
                ["box_max"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" }, ["description"] = "World box corner [x,y,z] (m)." },
                ["near"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" }, ["description"] = "Sphere centre [x,y,z] (world, m)." },
                ["radius"] = new JObject() { ["type"] = "number", ["description"] = "Sphere radius (m)." },
                ["margin"] = new JObject() { ["type"] = "number", ["description"] = "Metres to grow the region by." },
            },
        };

        private static readonly string[] _keys = { "name", "zone", "composite", "entity", "placement", "path", "box_min", "box_max", "near", "radius", "margin" };
        #endregion

        #region Resolving
        /// <summary>
        /// The region an argument describes. UI thread. <paramref name="onePlacement"/> refuses a region of several
        /// placements (listing them, so the caller can pick one with 'placement').
        /// </summary>
        public static McpRegion Resolve(McpCall call, Commands commands, Level level, JToken spec, string what = "region", bool onePlacement = false)
        {
            if (spec == null || spec.Type == JTokenType.Null)
                throw new McpError("'" + what + "' is required. " + Help);
            JObject obj;
            if (spec.Type == JTokenType.String)
            {
                string text = ((string)spec).Trim();
                if (text.Length == 0) throw new McpError("'" + what + "' is empty. " + Help);
                obj = string.Equals(text, "level", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "root", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "all", StringComparison.OrdinalIgnoreCase)
                    ? new JObject() { ["level"] = true }
                    : new JObject() { ["name"] = text };
            }
            else if (spec is JObject given)
                obj = given;
            else
                throw new McpError("'" + what + "' must be an object or a name. " + Help);

            List<string> unknown = obj.Properties().Select(o => o.Name).Where(o => !_keys.Contains(o) && o != "level").ToList();
            if (unknown.Count != 0)
                throw new McpError("'" + what + "' does not take " + string.Join(", ", unknown.Select(o => "'" + o + "'")) + ". " + Help);

            float margin = obj["margin"] != null ? (float)McpValues.ReadDouble(obj["margin"], what + ".margin") : 0f;
            if (margin < 0) throw new McpError("'" + what + ".margin' cannot be negative.");
            bool box = obj["box_min"] != null || obj["box_max"] != null, near = obj["near"] != null || obj["radius"] != null;
            int kinds = new[] { obj["name"] != null, obj["zone"] != null, obj["composite"] != null, obj["path"] != null, box, near, obj["level"] != null }.Count(o => o);
            if (kinds != 1)
                throw new McpError("'" + what + "' takes one of name, zone, composite (with entity/placement), path, box_min+box_max, near+radius or level. " + Help);
            if (obj["entity"] != null && obj["composite"] == null)
                throw new McpError("'" + what + ".entity' needs 'composite' (the composite it is in).");
            if (obj["placement"] != null && obj["composite"] == null && obj["zone"] == null && obj["name"] == null)
                throw new McpError("'" + what + ".placement' goes with 'composite', 'zone' or 'name'.");

            McpRegion region;
            if (box)
            {
                if (obj["box_min"] == null || obj["box_max"] == null) throw new McpError("'" + what + "' needs both box_min and box_max.");
                region = Box(McpValues.ReadVector(obj["box_min"], what + ".box_min", null), McpValues.ReadVector(obj["box_max"], what + ".box_max", null));
            }
            else if (near)
            {
                if (obj["near"] == null || obj["radius"] == null) throw new McpError("'" + what + "' needs both near and radius.");
                Vector3 centre = McpValues.ReadVector(obj["near"], what + ".near", null);
                float radius = (float)McpValues.ReadDouble(obj["radius"], what + ".radius");
                if (radius <= 0) throw new McpError("'" + what + ".radius' must be more than zero.");
                region = new McpRegion()
                {
                    Kind = "sphere",
                    Label = "within " + F(radius) + " m of " + Format(centre),
                    Centre = centre,
                    Radius = radius,
                    ClipMin = centre - new Vector3(radius),
                    ClipMax = centre + new Vector3(radius),
                    Spec = new JObject() { ["near"] = McpValues.Vector(centre), ["radius"] = radius },
                };
            }
            else if (obj["level"] != null)
                region = new McpRegion() { Kind = "level", Label = "the whole level", Spec = new JObject() { ["level"] = true } };
            else if (obj["zone"] != null)
                region = ResolveZone(call, commands, McpValues.ReadString(obj["zone"]).Trim(), obj["placement"], what);
            else if (obj["path"] != null)
            {
                List<Entity> chain = ChainFromNames(commands, McpScript.PathSteps(obj["path"], what + ".path"));
                region = OfChains(commands, new List<List<Entity>>() { chain }, "placement", DescribeChain(commands, chain));
            }
            else if (obj["composite"] != null)
                region = ResolveComposite(call, commands, McpValues.ReadString(obj["composite"]).Trim(), obj["entity"] == null ? null : McpValues.ReadString(obj["entity"]).Trim(), obj["placement"], what);
            else
                region = ResolveName(call, commands, level, McpValues.ReadString(obj["name"]).Trim(), obj["placement"], what);

            region.Margin = margin;
            if (margin > 0)
            {
                region.Spec["margin"] = margin;
                if (region.HasClip)
                {
                    region.ClipMin -= new Vector3(margin);
                    region.ClipMax += new Vector3(margin);
                }
            }
            if (onePlacement && region.Roots.Count > 1)
                throw new McpError(region.Label + " is " + region.Roots.Count + " placements; this needs one. Pass 'placement' (0-" + (region.Roots.Count - 1) + "): " + ListChains(commands, region.Roots, 10) + ".");
            return region;
        }

        /// <summary>A region of placements (chains from the root): its frame when there is one.</summary>
        public static McpRegion OfChains(Commands commands, List<List<Entity>> chains, string kind, string label)
        {
            McpRegion region = new McpRegion() { Kind = chains.Count == 1 ? kind : "placements", Label = label, Roots = chains };
            if (chains.Count == 1)
            {
                region.Frame = Walker(commands).Evaluate(commands.EntryPoints[0], chains[0]).World ?? new cTransform(Vector3.Zero, Vector3.Zero);
                region.Spec = new JObject() { ["path"] = new JArray(chains[0].Select(o => McpScript.Id(o.shortGUID))) };
            }
            else if (chains.Count == 0)
                region.Frame = new cTransform(Vector3.Zero, Vector3.Zero);
            return region;
        }

        /// <summary>A placement walker that places what the game places (functions with no position at their composite's origin).</summary>
        public static McpPlacements Walker(Commands commands) => new McpPlacements(commands) { PlaceUnpositioned = true };

        private static McpRegion ResolveZone(McpCall call, Commands commands, string wanted, JToken placement, string what)
        {
            List<SyncedZone> zones = Zones(commands);
            if (zones.Count == 0)
                throw new McpError("This level has no Zone entities. Use {\"composite\": ...}, {\"path\": ...} or a box instead (find_places finds rooms by name).");
            ShortGuid? id = McpScript.ParseId(wanted);
            List<SyncedZone> matched = zones.Where(o => (id != null && o.zone_entity == id.Value.AsUInt32) || SameZoneName(ZoneName(commands, o), wanted)).ToList();
            if (matched.Count == 0)
            {
                List<(SyncedZone zone, int score)> ranked = zones.Select(o => (o, Score(wanted, StripZone(ZoneName(commands, o))))).Where(o => o.Item2 > 0).OrderByDescending(o => o.Item2).ToList();
                if (ranked.Count != 0 && ranked[0].score >= ClearScore && (ranked.Count == 1 || ranked[1].score < ranked[0].score || ZoneName(commands, ranked[1].zone) == ZoneName(commands, ranked[0].zone)))
                {
                    string took = ZoneName(commands, ranked[0].zone);
                    matched = zones.Where(o => ZoneName(commands, o) == took).ToList();
                    call?.Note("No zone is called '" + wanted + "'; took " + took + ".");
                }
                else
                    throw new McpError("No zone called '" + wanted + "'." + (ranked.Count != 0 ? " Nearest: " + string.Join(", ", ranked.Take(8).Select(o => ZoneName(commands, o.zone))) + "." : " get_zones lists them; find_places searches every kind of place.") );
            }
            //One zone entity in a composite placed more than once is a zone per placement
            if (matched.Count > 1)
            {
                int index = placement == null ? -1 : ReadIndex(placement, what + ".placement");
                if (index < 0)
                {
                    if (matched.Select(o => ZoneName(commands, o)).Distinct().Count() > 1)
                        throw new McpError("'" + wanted + "' names " + matched.Count + " zones: " + string.Join(", ", matched.Take(10).Select(o => ZoneName(commands, o))) + ". Give the full name or id.");
                    throw new McpError("Zone " + ZoneName(commands, matched[0]) + " is placed " + matched.Count + " times (its composite is instanced more than once): pass 'placement' 0-" + (matched.Count - 1) + ".");
                }
                if (index >= matched.Count) throw new McpError("'" + what + ".placement' is 0-" + (matched.Count - 1) + ".");
                matched = new List<SyncedZone>() { matched[index] };
            }
            SyncedZone zone = matched[0];
            Composite root = commands.EntryPoints[0];
            List<List<Entity>> chains = new List<List<Entity>>();
            int broken = 0;
            foreach (List<uint> ids in zone.roots)
            {
                List<Entity> chain = ChainFromIds(commands, root, ids);
                if (chain == null || chain.Count == 0) broken++;
                else chains.Add(chain);
            }
            //Two trigger sequences can name the same entity, or one inside another's: each placement once (as the viewport takes them).
            //A chain goes when an earlier one is it or leads to it, or a later one leads to it
            List<List<Entity>> kept = chains.Where((chain, i) => !chains.Where((other, j) => j != i && IsPrefix(other, chain) && (j < i || other.Count < chain.Count)).Any()).ToList();
            McpRegion region = new McpRegion()
            {
                Kind = "zone",
                Label = "zone " + ZoneName(commands, zone),
                Roots = kept,
                Spec = new JObject() { ["zone"] = ZoneName(commands, zone) },
            };
            if (broken != 0) region.Notes.Add(broken + " of the zone's entries point at nothing and were skipped.");
            if (chains.Count == 0) throw new McpError("Zone " + ZoneName(commands, zone) + " claims nothing (its trigger sequences name no content), so it has no extent. Use the room's composite or a box.");
            return region;
        }

        private static McpRegion ResolveComposite(McpCall call, Commands commands, string compositeName, string entityName, JToken placement, string what)
        {
            Composite composite;
            try
            {
                composite = McpScript.FindComposite(commands, compositeName);
            }
            catch (McpError e)
            {
                //A near miss: a misspelt or partial name with one clear match
                List<(Composite composite, int score)> ranked = commands.Entries.Where(o => o != null && !o.name.EndsWith("\\"))
                    .Select(o => (o, Math.Max(Score(compositeName, McpScript.CompositeLeaf(o)), Score(compositeName, o.name) - 50))).Where(o => o.Item2 > 0).OrderByDescending(o => o.Item2).Take(8).ToList();
                if (ranked.Count != 0 && ranked[0].score >= ClearScore && (ranked.Count == 1 || ranked[1].score < ranked[0].score))
                {
                    composite = ranked[0].composite;
                    call?.Note("No composite is called '" + compositeName + "'; took " + composite.name + ".");
                }
                else if (ranked.Count != 0)
                    throw new McpError(e.Message + " Nearest: " + string.Join("; ", ranked.Select(o => o.composite.name)) + ".");
                else
                    throw;
            }
            Entity entity = entityName == null ? null : McpScript.FindEntity(commands, composite, entityName);
            Composite root = commands.EntryPoints[0];
            string label = composite.name + (entity != null ? " / " + McpScript.EntityName(commands, composite, entity) : "");
            if (placement != null && (placement is JArray || placement is JObject || placement.Type == JTokenType.String && !int.TryParse((string)placement, out _)))
            {
                List<Entity> chain = ChainFromNames(commands, McpScript.PathSteps(placement, what + ".placement"));
                Entity last = chain[chain.Count - 1];
                bool fits = entity != null ? last == entity : McpScript.InstancedComposite(commands, last) == composite;
                if (!fits) throw new McpError("That path does not lead to " + (entity != null ? "the entity" : "an instance of " + composite.name) + ".");
                McpRegion one = OfChains(commands, new List<List<Entity>>() { chain }, "placement", label);
                return one;
            }

            if (composite == root && entity == null)
                return new McpRegion() { Kind = "level", Label = "the whole level (root)", Spec = new JObject() { ["level"] = true }, Frame = new cTransform(Vector3.Zero, Vector3.Zero) };
            McpPlacements walker = Walker(commands);
            List<McpPlacements.Placement> found = new List<McpPlacements.Placement>();
            int total = walker.PlacementsOf(root, composite, entity, found, 2000, call?.Cancel ?? default(System.Threading.CancellationToken), realOnly: true);
            if (total == 0)
                throw new McpError(label + " is not placed in the level" + (walker.NotReal != 0 ? " (its " + walker.NotReal + " placements are all deleted or templates)" : "") + ". get_placements shows where things are placed; a composite has to be instanced under the root to have a position.");
            List<List<Entity>> chains = found.Select(o => o.Chain).ToList();
            if (placement != null)
            {
                int index = ReadIndex(placement, what + ".placement");
                if (index >= chains.Count) throw new McpError(label + " has " + total + " placement" + (total == 1 ? "" : "s") + ": 'placement' is 0-" + (total - 1) + ".");
                McpRegion one = OfChains(commands, new List<List<Entity>>() { chains[index] }, "placement", label + " placement " + index);
                one.Spec = new JObject() { ["composite"] = composite.name, ["placement"] = index };
                if (entity != null) one.Spec["entity"] = McpScript.Id(entity.shortGUID);
                return one;
            }
            McpRegion region = OfChains(commands, chains, "placement", label);
            region.Spec = new JObject() { ["composite"] = composite.name };
            if (entity != null) region.Spec["entity"] = McpScript.Id(entity.shortGUID);
            if (total > chains.Count) region.Notes.Add("Only the first " + chains.Count + " of " + total + " placements are used.");
            return region;
        }

        private static McpRegion ResolveName(McpCall call, Commands commands, Level level, string name, JToken placement, string what)
        {
            List<Place> ranked = FindPlaces(call, commands, level, name, null, 12);
            if (ranked.Count == 0)
                throw new McpError("Nothing in the level is called anything like '" + name + "' (zones, composites, instances, models). find_composites and get_zones list what there is.");
            Place best = ranked[0];
            bool clear = best.Score >= ClearScore && (ranked.Count == 1 || ranked[1].Score < best.Score || SameTarget(ranked[1], best));
            if (!clear)
                throw new McpError("'" + name + "' could be several places; pass one of these as the region (find_places shows more): " + string.Join("; ", ranked.Take(8).Select(o => o.Kind + " " + o.Name + " " + o.Spec.ToString(Newtonsoft.Json.Formatting.None))) + ".");
            JObject spec = (JObject)best.Spec.DeepClone();
            if (placement != null) spec["placement"] = placement;
            McpRegion region = Resolve(call, commands, level, spec, what);
            if (!string.Equals(best.Name, name, StringComparison.OrdinalIgnoreCase))
                region.Notes.Add("'" + name + "' taken as " + best.Kind + " " + best.Name + ".");
            return region;
        }

        private static bool SameTarget(Place a, Place b) => JToken.DeepEquals(a.Spec, b.Spec);

        private static int ReadIndex(JToken token, string what)
        {
            int index = McpValues.ReadInt(token, what);
            if (index < 0) throw new McpError("'" + what + "' is 0 or more.");
            return index;
        }
        #endregion

        #region Paths
        /// <summary>Entities from the root through instances, by ids or names (a leading 'root' step is allowed). UI thread.</summary>
        public static List<Entity> ChainFromNames(Commands commands, List<string> steps) => McpScript.ChainFrom(commands, commands.EntryPoints[0], steps);

        /// <summary>Entities from <paramref name="start"/> by raw ids; null when a step is missing.</summary>
        public static List<Entity> ChainFromIds(Commands commands, Composite start, IEnumerable<uint> ids)
        {
            List<Entity> chain = new List<Entity>();
            Composite current = start;
            foreach (uint raw in ids)
            {
                if (current == null) return null;
                Entity entity = current.GetEntityByID(new ShortGuid(raw));
                if (entity == null) return null;
                chain.Add(entity);
                current = McpScript.InstancedComposite(commands, entity);
            }
            return chain;
        }

        /// <summary>Whether <paramref name="prefix"/> is <paramref name="chain"/> or a placement it lies under (the same entity ids from the root).</summary>
        private static bool IsPrefix(List<Entity> prefix, List<Entity> chain)
        {
            if (prefix.Count > chain.Count) return false;
            for (int i = 0; i < prefix.Count; i++)
                if (prefix[i].shortGUID != chain[i].shortGUID) return false;
            return true;
        }

        /// <summary>An entity's name for a path, or its id when it has none.</summary>
        public static string NameOf(Commands commands, Composite composite, Entity entity)
        {
            string name = composite == null ? null : McpScript.EntityName(commands, composite, entity);
            return string.IsNullOrWhiteSpace(name) ? McpScript.Id(entity.shortGUID) : name;
        }

        /// <summary>A position for a message: [x, y, z] to the centimetre.</summary>
        public static string Format(Vector3 v) => "[" + F(v.X) + ", " + F(v.Y) + ", " + F(v.Z) + "]";

        private static string F(float value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>A chain as "Name > Name > Name".</summary>
        public static string DescribeChain(Commands commands, IList<Entity> chain)
        {
            List<string> names = new List<string>();
            Composite current = commands.EntryPoints[0];
            foreach (Entity entity in chain)
            {
                names.Add(NameOf(commands, current, entity));
                current = McpScript.InstancedComposite(commands, entity);
            }
            return string.Join(" > ", names);
        }

        /// <summary>A chain's names and ids, for results.</summary>
        public static JObject ChainJson(Commands commands, IList<Entity> chain) => ChainJson(commands, chain, null);

        /// <summary>
        /// A chain from <paramref name="from"/> (null: the root) as results give paths - {path: names, ids}, with 'from' (the
        /// composite's path) when it does not start at the root - which tools taking a 'path' accept back as it is.
        /// </summary>
        public static JObject ChainJson(Commands commands, IList<Entity> chain, Composite from)
        {
            JArray names = new JArray(), ids = new JArray();
            Composite root = commands.EntryPoints[0];
            Composite current = from ?? root;
            foreach (Entity entity in chain)
            {
                names.Add(NameOf(commands, current, entity));
                ids.Add(McpScript.Id(entity.shortGUID));
                current = McpScript.InstancedComposite(commands, entity);
            }
            JObject json = new JObject();
            if (from != null && from != root) json["from"] = from.name;
            json["path"] = names;
            json["ids"] = ids;
            return json;
        }

        private static string ListChains(Commands commands, List<List<Entity>> chains, int limit)
        {
            McpPlacements walker = Walker(commands);
            Composite root = commands.EntryPoints[0];
            return string.Join("; ", chains.Take(limit).Select((o, i) =>
            {
                cTransform world = walker.Evaluate(root, o).World;
                return i + ": " + DescribeChain(commands, o) + (world != null ? " at " + Format(world.position) : "");
            })) + (chains.Count > limit ? " (and " + (chains.Count - limit) + " more)" : "");
        }
        #endregion

        #region Zones
        /// <summary>The level's zones as the viewport works them out (cached until the script changes).</summary>
        public static List<SyncedZone> Zones(Commands commands)
        {
            McpCollision.Hook();
            int version = McpCollision.Version;
            lock (_zoneLock)
            {
                if (_zones != null && _zonesFor == commands && _zonesVersion == version)
                    return _zones;
            }
            List<SyncedZone> zones = ZoneMembership.CalculateFrom(commands, commands.EntryPoints[0]);
            lock (_zoneLock)
            {
                _zones = zones;
                _zonesFor = commands;
                _zonesVersion = version;
            }
            return zones;
        }

        private static readonly object _zoneLock = new object();
        private static List<SyncedZone> _zones;
        private static Commands _zonesFor;
        private static int _zonesVersion;

        internal static void DropCaches()
        {
            lock (_zoneLock) { _zones = null; _zonesFor = null; }
            lock (_indexLock) { _index = null; _indexFor = null; }
        }

        public static string ZoneName(Commands commands, SyncedZone zone)
        {
            if (!string.IsNullOrWhiteSpace(zone.name)) return zone.name;
            Composite composite = commands.GetComposite(new ShortGuid(zone.zone_composite));
            Entity entity = composite?.GetEntityByID(new ShortGuid(zone.zone_entity));
            return composite != null && entity != null ? McpScript.EntityName(commands, composite, entity) : McpScript.Id(new ShortGuid(zone.zone_entity));
        }

        private static string StripZone(string name) => name != null && name.StartsWith("Zone_", StringComparison.OrdinalIgnoreCase) ? name.Substring(5) : name;

        private static bool SameZoneName(string name, string wanted) =>
            string.Equals(name, wanted, StringComparison.OrdinalIgnoreCase) || string.Equals(StripZone(name), StripZone(wanted), StringComparison.OrdinalIgnoreCase);
        #endregion

        #region Names
        /// <summary>A score from which a name counts as a clear match on its own (all of the words, or near enough).</summary>
        public const int ClearScore = 700;

        /// <summary>A name's words: split on punctuation, between a lower and an upper case letter, and between letters and digits; lower case.</summary>
        public static List<string> Tokens(string text)
        {
            List<string> tokens = new List<string>();
            if (string.IsNullOrEmpty(text)) return tokens;
            StringBuilder current = new StringBuilder();
            char previous = '\0';
            foreach (char c in text)
            {
                bool split = !char.IsLetterOrDigit(c) ||
                    (current.Length != 0 && ((char.IsUpper(c) && char.IsLower(previous)) || (char.IsDigit(c) != char.IsDigit(previous))));
                if (split && current.Length != 0)
                {
                    tokens.Add(current.ToString().ToLowerInvariant());
                    current.Clear();
                }
                if (char.IsLetterOrDigit(c)) current.Append(c);
                previous = c;
            }
            if (current.Length != 0) tokens.Add(current.ToString().ToLowerInvariant());
            return tokens;
        }

        /// <summary>
        /// How well <paramref name="name"/> answers <paramref name="query"/>, 0 (not at all) to 1000 (the same, ignoring case and
        /// separators): every query word a word of the name (800+, fewer extra words higher), words as prefixes (650+), the
        /// query inside the name (550), words within a small edit distance (400+), or most of the words (under 300).
        /// </summary>
        public static int Score(string query, string name)
        {
            List<string> q = Tokens(query), n = Tokens(name);
            if (q.Count == 0 || n.Count == 0) return 0;
            string qj = string.Concat(q), nj = string.Concat(n);
            if (qj == nj) return 1000;
            //'zone' and similar prefixes add nothing
            List<string> core = n.Where(o => o != "zone").ToList();
            if (core.Count != 0 && string.Concat(core) == qj) return 950;
            int extra = Math.Max(0, n.Count - q.Count);
            if (q.All(o => n.Contains(o))) return Math.Max(700, 900 - 20 * extra);
            //A word cut short either way ('obs' for observation, 'canteens' for canteen) - but not a short word inside a longer one ('can' in 'canteen')
            if (q.All(o => n.Any(t => (t.StartsWith(o, StringComparison.Ordinal) && o.Length >= 3) || (o.StartsWith(t, StringComparison.Ordinal) && t.Length >= 4 && t.Length * 2 >= o.Length))))
                return q.All(o => o.Length >= 4) ? Math.Max(600, 760 - 20 * extra) : 620;
            if (nj.Contains(qj)) return 550;
            int matched = 0, distance = 0;
            foreach (string word in q)
            {
                int best = int.MaxValue;
                foreach (string t in n)
                    best = Math.Min(best, Distance(word, t, 3));
                int allowed = word.Length >= 7 ? 2 : word.Length >= 4 ? 1 : 0;
                if (best <= allowed) { matched++; distance += best; }
            }
            if (matched == q.Count) return Math.Max(400, 560 - 40 * distance - 10 * extra);
            if (matched * 2 >= q.Count && matched > 0) return 100 + 200 * matched / q.Count;
            return 0;
        }

        /// <summary>Damerau-Levenshtein (optimal string alignment) distance, giving up past <paramref name="cap"/>.</summary>
        private static int Distance(string a, string b, int cap)
        {
            if (Math.Abs(a.Length - b.Length) > cap) return cap + 1;
            int[,] d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                        d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                }
            }
            return d[a.Length, b.Length];
        }

        /// <summary>A place a name may mean.</summary>
        public sealed class Place
        {
            public string Kind;          // zone, composite, instance, model
            public string Name;
            public string Detail;        // what it is: the composite an instance places, a composite's path...
            public int Count;            // placements
            public int Score;
            /// <summary>How the name matched: exact, words, partial, misspelt or some words.</summary>
            public string Match;
            public JObject Spec;         // the region argument for it
            public List<Entity> FirstChain;
        }

        /// <summary>A word for a <see cref="Score"/>.</summary>
        public static string MatchOf(int score) => score >= 950 ? "exact" : score >= ClearScore ? "words" : score >= 550 ? "partial" : score >= 400 ? "misspelt" : "some words";

        public static readonly string[] PlaceKinds = { "zone", "composite", "instance", "model" };

        /// <summary>Places ranked by how well their name answers <paramref name="name"/>. UI thread.</summary>
        public static List<Place> FindPlaces(McpCall call, Commands commands, Level level, string name, ICollection<string> kinds, int limit)
        {
            bool Want(string kind) => kinds == null || kinds.Count == 0 || kinds.Contains(kind);
            List<Place> places = new List<Place>();
            if (Want("zone"))
            {
                Dictionary<string, List<SyncedZone>> byName = new Dictionary<string, List<SyncedZone>>(StringComparer.OrdinalIgnoreCase);
                foreach (SyncedZone zone in Zones(commands))
                {
                    string zoneName = ZoneName(commands, zone);
                    if (!byName.TryGetValue(zoneName, out List<SyncedZone> list)) byName[zoneName] = list = new List<SyncedZone>();
                    list.Add(zone);
                }
                foreach (KeyValuePair<string, List<SyncedZone>> zone in byName)
                {
                    int score = Math.Max(Score(name, zone.Key), Score(name, StripZone(zone.Key)));
                    if (score == 0) continue;
                    //A zone is the place a room is: rank it ahead of a composite or instance of the same name
                    places.Add(new Place() { Kind = "zone", Name = zone.Key, Detail = zone.Value[0].roots.Count + " entries", Count = zone.Value.Count, Score = score + 30, Match = MatchOf(score), Spec = new JObject() { ["zone"] = zone.Key } });
                }
            }
            PlaceIndex index = Want("instance") || Want("model") || Want("composite") ? Index(call, commands, level) : null;
            if (Want("composite"))
            {
                foreach (Composite composite in commands.Entries)
                {
                    if (composite == null || composite.name.EndsWith("\\") || composite == commands.EntryPoints[1] || composite == commands.EntryPoints[2]) continue;
                    int score = Math.Max(Score(name, McpScript.CompositeLeaf(composite)), Score(name, composite.name) - 100);
                    if (score == 0) continue;
                    index.Composites.TryGetValue(composite, out PlaceEntry entry);
                    places.Add(new Place()
                    {
                        Kind = "composite",
                        Name = composite.name,
                        Detail = entry == null ? "not placed" : "placed " + entry.Count + " time" + (entry.Count == 1 ? "" : "s"),
                        Count = entry?.Count ?? 0,
                        Score = score - (entry == null ? 200 : 0),
                        Match = MatchOf(score),
                        Spec = new JObject() { ["composite"] = composite.name },
                        FirstChain = entry?.Chains.FirstOrDefault(),
                    });
                }
            }
            if (Want("instance"))
            {
                foreach (KeyValuePair<string, PlaceEntry> instance in index.Instances)
                {
                    int score = Score(name, instance.Key);
                    if (score == 0) continue;
                    PlaceEntry entry = instance.Value;
                    places.Add(new Place()
                    {
                        Kind = "instance",
                        Name = instance.Key,
                        Detail = "of " + entry.Detail + (entry.Count > 1 ? ", " + entry.Count + " with this name" : ""),
                        Count = entry.Count,
                        Score = score - (entry.Count > 1 ? 10 : 0),
                        Match = MatchOf(score),
                        Spec = new JObject() { ["path"] = new JArray(entry.Chains[0].Select(o => McpScript.Id(o.shortGUID))) },
                        FirstChain = entry.Chains[0],
                    });
                }
            }
            if (Want("model"))
            {
                foreach (KeyValuePair<string, PlaceEntry> model in index.Models)
                {
                    int score = Score(name, model.Key);
                    if (score == 0) continue;
                    PlaceEntry entry = model.Value;
                    places.Add(new Place()
                    {
                        Kind = "model",
                        Name = model.Key,
                        Detail = "placed by " + entry.Count + " ModelReference" + (entry.Count == 1 ? "" : "s"),
                        Count = entry.Count,
                        Score = score - 60,
                        Match = MatchOf(score),
                        Spec = new JObject() { ["path"] = new JArray(entry.Chains[0].Select(o => McpScript.Id(o.shortGUID))) },
                        FirstChain = entry.Chains[0],
                    });
                }
            }
            return places.OrderByDescending(o => o.Score).ThenByDescending(o => o.Count).ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase).Take(Math.Max(1, limit)).ToList();
        }

        /// <summary>What the level places, by name: instances by their entity name, composites by placement count, models by the ModelReferences placing them.</summary>
        public sealed class PlaceIndex
        {
            public Dictionary<string, PlaceEntry> Instances = new Dictionary<string, PlaceEntry>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, PlaceEntry> Models = new Dictionary<string, PlaceEntry>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<Composite, PlaceEntry> Composites = new Dictionary<Composite, PlaceEntry>();
            public bool Truncated;
        }

        public sealed class PlaceEntry
        {
            public int Count;
            public string Detail;
            public List<List<Entity>> Chains = new List<List<Entity>>();
        }

        private static readonly object _indexLock = new object();
        private static PlaceIndex _index;
        private static Commands _indexFor;
        private static int _indexVersion;

        /// <summary>The level's place index (one walk of the level; cached until the script changes). UI thread.</summary>
        public static PlaceIndex Index(McpCall call, Commands commands, Level level)
        {
            McpCollision.Hook();
            int version = McpCollision.Version;
            lock (_indexLock)
            {
                if (_index != null && _indexFor == commands && _indexVersion == version)
                    return _index;
            }
            PlaceIndex index = new PlaceIndex();
            McpPlacements walker = new McpPlacements(commands);
            Dictionary<Models.CS2.Component.LOD.Submesh, string> modelNames = McpCollision.ModelNames(level);
            void Add(Dictionary<string, PlaceEntry> into, string key, string detail, List<Entity> chain)
            {
                if (string.IsNullOrWhiteSpace(key)) return;
                if (!into.TryGetValue(key, out PlaceEntry entry)) into[key] = entry = new PlaceEntry() { Detail = detail };
                entry.Count++;
                if (entry.Chains.Count < 3) entry.Chains.Add(new List<Entity>(chain));
            }
            using (call == null ? null : McpEditorTools.Heartbeat(call, "Indexing the level's places"))
            {
                walker.Walk(commands.EntryPoints[0], step =>
                {
                    if (!(step.Entity is FunctionEntity function)) return true;
                    if (!function.function.IsFunctionType)
                    {
                        if (!step.Real) return true;
                        Composite placed = commands.GetComposite(function.function);
                        if (placed == null) return true;
                        Add(index.Instances, McpScript.EntityName(commands, step.Composite, function), placed.name, step.Chain);
                        if (!index.Composites.TryGetValue(placed, out PlaceEntry entry)) index.Composites[placed] = entry = new PlaceEntry() { Detail = placed.name };
                        entry.Count++;
                        if (entry.Chains.Count < 3) entry.Chains.Add(new List<Entity>(step.Chain));
                    }
                    else if (function.function == FunctionType.ModelReference && step.Real)
                    {
                        List<RenderableElements.Element> elements = function.GetResource(ResourceType.RENDERABLE_INSTANCE, true)?.RenderableInstance;
                        Models.CS2.Component.LOD.Submesh first = elements?.FirstOrDefault(o => o?.Model != null)?.Model;
                        if (first != null && modelNames.TryGetValue(first, out string model))
                            Add(index.Models, model, "model", step.Chain);
                    }
                    return true;
                }, null, call?.Cancel ?? default(System.Threading.CancellationToken));
            }
            index.Truncated = walker.Truncated;
            lock (_indexLock)
            {
                _index = index;
                _indexFor = commands;
                _indexVersion = version;
            }
            return index;
        }
        #endregion
    }
}
