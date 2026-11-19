using CATHODE;
using CATHODE.Enums;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using CathodeLib.NavMesh;
using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace OpenCAGE
{
    /// <summary>One kind of AI markup: a retail composite the game's NPCs find by position (nothing links to it).</summary>
    internal sealed class MarkupKind
    {
        /// <summary>search, sweep, highcover.</summary>
        public string Key;
        public string Label;
        /// <summary>The composite's path, the same in every retail level that has it.</summary>
        public string Path;
        public ShortGuid Id;
        /// <summary>Instance names: prefix + number.</summary>
        public string NamePrefix;
        /// <summary>The least distance between two of this kind (m), as retail spaces them.</summary>
        public float Spacing;
        /// <summary>Character classes whose navmesh it goes on (NAVIGATION_CHARACTER_CLASS_COMBINATION bits).</summary>
        public int Classes;
        public override string ToString() => Label;
    }

    /// <summary>Markup the level places: where, which kind, which instance.</summary>
    internal sealed class PlacedMarkup
    {
        public MarkupKind Kind;
        /// <summary>World position of the job (its task position) or the melee context.</summary>
        public Vector3 Position;
        /// <summary>Degrees about +Y it faces.</summary>
        public float Yaw;
        /// <summary>The instance of the markup composite, and the composite holding it (null when the job is placed some other way).</summary>
        public FunctionEntity Instance;
        public Composite Holder;
        /// <summary>The user made or changed the composite that places it.</summary>
        public bool User;
        public string Where;
    }

    /// <summary>A spot the tool offers.</summary>
    internal sealed class MarkupSpot
    {
        public MarkupKind Kind;
        /// <summary>On the navmesh: where the instance goes.</summary>
        public Vector3 Position;
        public float Yaw;
        /// <summary>How far the nearest edge of the walkable floor is (m).</summary>
        public float Clearance;
        /// <summary>High cover: the wall it faces, measured from the spot (m).</summary>
        public float Wall;
        public bool Use = true;
    }

    /// <summary>The tool's settings.</summary>
    internal sealed class MarkupOptions
    {
        public bool Search = true, Sweep = true, HighCover = false;
        /// <summary>Spacing per kind key (m); a kind not listed takes its retail spacing.</summary>
        public Dictionary<string, float> Spacing = new Dictionary<string, float>();
        /// <summary>Only spots inside this box (world), or anywhere on the navmesh.</summary>
        public Vector3? RegionMin, RegionMax;
        /// <summary>Keep new spots the kind's spacing away from the level's own markup of that kind.</summary>
        public bool KeepClearOfExisting = true;
        /// <summary>Only spots with a ceiling over them in the collision (the alien hunts indoors).</summary>
        public bool IndoorsOnly;
        /// <summary>The composite the instances go in (null: the root).</summary>
        public Composite Into;
        /// <summary>The flowgraph page their nodes go on.</summary>
        public string Page = "AIMarkup";
        /// <summary>Instance name prefixes per kind key (null: the kind's own).</summary>
        public Dictionary<string, string> NamePrefix = new Dictionary<string, string>();
        public int Seed = 1;
        /// <summary>Only spots this accepts (the retail check's area), or every spot.</summary>
        public Func<MarkupKind, Vector3, bool> Within;

        public float SpacingOf(MarkupKind kind) => Spacing.TryGetValue(kind.Key, out float s) && s > 0 ? s : kind.Spacing;
        public bool Wants(MarkupKind kind) => kind == AIMarkup.SearchJob ? Search : kind == AIMarkup.AreaSweepJob ? Sweep : kind == AIMarkup.HighCover && HighCover;
    }

    /// <summary>What <see cref="AIMarkup.Propose"/> found.</summary>
    internal sealed class MarkupProposal
    {
        public List<MarkupSpot> Spots = new List<MarkupSpot>();
        public List<PlacedMarkup> Existing = new List<PlacedMarkup>();
        /// <summary>Walkable floor the spots were looked for on (m²).</summary>
        public double Area;
        public int Samples;
        /// <summary>Why samples were turned down, with how many.</summary>
        public Dictionary<string, int> Rejected = new Dictionary<string, int>();
        public List<string> Notes = new List<string>();
        public bool NavmeshStale;
    }

    /// <summary>What placing markup did.</summary>
    internal sealed class MarkupPlaced
    {
        public List<FunctionEntity> Instances = new List<FunctionEntity>();
        public List<string> Names = new List<string>();
        public int Ported;
        public List<string> PortedFrom = new List<string>();
        public Composite Into;
        public List<string> Notes = new List<string>();
    }

    /// <summary>
    /// Finds and places AI markup over a level's navmesh: retail's Archetypes\Jobs\SearchJob (the spots NPCs search at),
    /// Archetypes\Jobs\AreaSweepJob (the spots they sweep over) and Archetypes\Jobs\MeleeContext\MeleeContext_HighCover (where an
    /// android throws the player against a wall). The game gathers them by position; nothing links to them. UI thread unless said.
    /// </summary>
    internal static partial class AIMarkup
    {
        private const int NpcClasses = (int)(NAVIGATION_CHARACTER_CLASS_COMBINATION.ALIEN | NAVIGATION_CHARACTER_CLASS_COMBINATION.ANDROID | NAVIGATION_CHARACTER_CLASS_COMBINATION.HUMAN_NPC);
        private const int AndroidClass = (int)NAVIGATION_CHARACTER_CLASS_COMBINATION.ANDROID;

        public static readonly MarkupKind SearchJob = new MarkupKind()
        {
            Key = "search", Label = "Search jobs (SearchJob)", Path = @"Archetypes\Jobs\SearchJob", Id = new ShortGuid("1B-06-54-ED"), NamePrefix = "SearchJob",
            Spacing = 5.5f, Classes = NpcClasses,
        };
        public static readonly MarkupKind AreaSweepJob = new MarkupKind()
        {
            Key = "sweep", Label = "Area sweep jobs (AreaSweepJob)", Path = @"Archetypes\Jobs\AreaSweepJob", Id = new ShortGuid("C9-28-BC-DA"), NamePrefix = "AreaSweepJob",
            Spacing = 5.5f, Classes = NpcClasses,
        };
        public static readonly MarkupKind HighCover = new MarkupKind()
        {
            Key = "highcover", Label = "Melee high cover (MeleeContext_HighCover)", Path = @"Archetypes\Jobs\MeleeContext\MeleeContext_HighCover", Id = new ShortGuid("43-AC-1D-C5"),
            NamePrefix = "MeleeContext_HighCover", Spacing = 4.5f, Classes = AndroidClass,
        };
        public static readonly MarkupKind[] Kinds = { SearchJob, AreaSweepJob, HighCover };

        /* Retail placement, measured over the shipped levels: jobs stand on standing-height floor 0.3 m or more from its edge (median
           0.85 m), as many of each kind as a 5.5 m spacing gives; a high cover context stands 2.15 m (on the navmesh) back from a flat, high
           wall and faces it. */
        public const float MinClearance = 0.35f;
        public const float PreferredClearance = 1.2f;
        public const float HighCoverWallDistance = 2.15f;
        public const float MinIslandArea = 25f;
        /// <summary>How far round its own markup the retail check looks for spots (m).</summary>
        public const float ArenaRadius = 6f;
        /// <summary>About the most search and sweep jobs a shipped level places.</summary>
        public const int MaxRetailJobs = 330;

        /* Retail levels that place all three kinds (one load brings any of them) */
        private static readonly string[] Sources = { "PRODUCTION/TECH_HUB", "PRODUCTION/TECH_RND", "PRODUCTION/HAB_SHOPPINGCENTRE", "PRODUCTION/SCI_HOSPITALUPPER" };

        private static readonly ShortGuid ContextType = ShortGuidUtils.Generate("Context_Type");

        /// <summary>A kind by key, composite path or leaf name.</summary>
        public static MarkupKind FindKind(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string t = text.Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "");
            foreach (MarkupKind kind in Kinds)
                if (t == kind.Key || t == kind.Path.ToLowerInvariant().Replace("_", "") || t == kind.NamePrefix.ToLowerInvariant().Replace("_", ""))
                    return kind;
            if (t == "searchjobs") return SearchJob;
            if (t == "areasweep" || t == "sweepjob" || t == "sweepjobs") return AreaSweepJob;
            if (t == "meleecontexthighcover" || t == "highcovermelee" || t == "melee") return HighCover;
            throw new McpError("'" + text + "' is not a kind of AI markup: search, sweep or highcover.");
        }

        public static MarkupKind KindOf(Composite composite) => composite == null ? null : Kinds.FirstOrDefault(o => o.Id == composite.shortGUID);

        #region Survey
        /// <summary>
        /// The markup the level places, from the entry points down: every search and area sweep job (JOB_SystematicSearch, JOB_AreaSweep)
        /// and every high cover melee context (NPC_MeleeContext of type ANDROID_HIGH_COVER_GRAPPLE), however it is wrapped. Any thread.
        /// </summary>
        public static List<PlacedMarkup> Survey(Commands commands, Func<Composite, bool> users = null)
        {
            List<PlacedMarkup> found = new List<PlacedMarkup>();
            if (commands?.Entries == null || commands.EntryPoints == null || commands.EntryPoints.Length == 0 || commands.EntryPoints[0] == null)
                return found;
            if (users == null)
            {
                HashSet<ShortGuid> modified = new HashSet<ShortGuid>();
                try
                {
                    foreach (CompositeModificationInfoTable.ModificationInfo info in commands.Utils?.AllModificationInfo ?? new List<CompositeModificationInfoTable.ModificationInfo>())
                        if (info != null) modified.Add(info.composite_id);
                }
                catch { }
                users = o => o != null && modified.Contains(o.shortGUID);
            }

            Dictionary<Composite, bool> holds = new Dictionary<Composite, bool>();
            bool Holds(Composite composite)
            {
                if (composite == null) return false;
                if (holds.TryGetValue(composite, out bool known)) return known;
                holds[composite] = false;
                bool result = composite.functions.Any(o => JobKindOf(o) != null);
                if (!result)
                    foreach (FunctionEntity function in composite.functions)
                        if (!function.function.IsFunctionType && Holds(commands.GetComposite(function.function))) { result = true; break; }
                holds[composite] = result;
                return result;
            }

            //A job function has no position of its own: each is placed where the instance of the composite holding it is
            Dictionary<Composite, MarkupKind> direct = new Dictionary<Composite, MarkupKind>();
            MarkupKind Direct(Composite composite)
            {
                if (composite == null) return null;
                if (!direct.TryGetValue(composite, out MarkupKind kind))
                    direct[composite] = kind = composite.functions.Select(JobKindOf).FirstOrDefault(o => o != null);
                return kind;
            }

            Composite root = commands.EntryPoints[0];
            List<(McpPlacements.Placement Placement, MarkupKind Kind)> placements = new List<(McpPlacements.Placement, MarkupKind)>();
            McpPlacements walker = McpRegion.Walker(commands);
            walker.Walk(root, step =>
            {
                if (step.Entity is FunctionEntity function && !function.function.IsFunctionType)
                {
                    MarkupKind kind = Direct(commands.GetComposite(function.function));
                    if (kind != null) placements.Add((McpPlacements.Placement.Of(step), kind));
                }
                return true;
            }, Holds);
            foreach ((McpPlacements.Placement placement, MarkupKind kind) in placements)
            {
                if (!placement.Real || placement.World == null) continue;
                List<Composite> along = McpScript.CompositesAlong(commands, root, placement.Chain);
                PlacedMarkup markup = new PlacedMarkup()
                {
                    Kind = kind,
                    Position = placement.World.position,
                    Yaw = YawOf(placement.World),
                };
                FunctionEntity holding = (FunctionEntity)placement.Chain[placement.Chain.Count - 1];
                //The instance of the kind's own composite: this one, or one above it (SearchJob wraps the composite holding its job)
                for (int i = placement.Chain.Count - 1; i >= 0; i--)
                {
                    if (!(placement.Chain[i] is FunctionEntity instance) || instance.function.IsFunctionType) continue;
                    if (KindOf(commands.GetComposite(instance.function)) != markup.Kind) continue;
                    markup.Instance = instance;
                    markup.Holder = i < along.Count ? along[i] : null;
                    break;
                }
                markup.User = users(markup.Holder ?? (along.Count >= placement.Chain.Count ? along[placement.Chain.Count - 1] : null));
                Entity named = (Entity)markup.Instance ?? holding;
                Composite namedIn = markup.Instance != null ? markup.Holder : (along.Count >= placement.Chain.Count ? along[placement.Chain.Count - 1] : null);
                string name;
                try { name = McpScript.EntityName(commands, namedIn, named); } catch { name = named.shortGUID.ToByteString(); }
                markup.Where = (namedIn != null ? EditorUtils.GetCompositeName(namedIn) : "?") + " / " + name;
                found.Add(markup);
            }
            return found;
        }

        /* The kind a job function stands for (the search / sweep base composites hold one each, the melee context composites one context) */
        private static MarkupKind JobKindOf(FunctionEntity function)
        {
            if (!function.function.IsFunctionType) return null;
            switch (function.function.AsFunctionType)
            {
                case FunctionType.JOB_SystematicSearch: return SearchJob;
                case FunctionType.JOB_AreaSweep: return AreaSweepJob;
                case FunctionType.NPC_MeleeContext:
                    return function.GetParameter(ContextType)?.content is cEnum type && type.enumIndex == (int)MELEE_CONTEXT_TYPE.ANDROID_HIGH_COVER_GRAPPLE ? HighCover : null;
            }
            return null;
        }

        private static float YawOf(cTransform world)
        {
            Vector3 forward = InstanceTransform.DirectionToWorld(new cTransform(Vector3.Zero, world.rotation), Vector3.UnitZ);
            return (float)Math.Round(Math.Atan2(forward.X, forward.Z) * 180.0 / Math.PI, 1);
        }
        #endregion

        #region The walkable floor
        /// <summary>The navmesh polygons a kind may stand on, and the edges round them.</summary>
        private sealed class Floor
        {
            public NavigationMesh Mesh;
            public NavigationMeshQuery Query;
            public bool[] Allowed;
            public int[] Island;
            public Dictionary<int, double> IslandArea = new Dictionary<int, double>();
            public Rim Edges;
        }

        private static Floor FloorFor(NavigationMesh mesh, NavigationMeshQuery query, int classes)
        {
            Floor floor = new Floor() { Mesh = mesh, Query = query, Allowed = new bool[mesh.Polygons.Length], Island = new int[mesh.Polygons.Length] };
            NavigationMeshQuery.Options options = new NavigationMeshQuery.Options() { Classes = classes };
            for (int i = 0; i < mesh.Polygons.Length; i++)
            {
                NavigationMesh.dtPoly poly = mesh.Polygons[i];
                floor.Island[i] = -1;
                if (poly.area.GetPolyType() != NavigationMesh.dtPolyTypes.DT_POLYTYPE_GROUND || AlienVents.IsSheet(mesh, i)) continue;
                if (((int)poly.area.GetAdmittanceFlags() & classes) == 0 || !poly.area.GetIsEnabled()) continue;
                floor.Allowed[i] = true;
                floor.Island[i] = query.IslandOf(i, options);
                floor.IslandArea[floor.Island[i]] = (floor.IslandArea.TryGetValue(floor.Island[i], out double had) ? had : 0) + PolyArea(mesh, i);
            }
            floor.Edges = new Rim(mesh, floor.Allowed);
            return floor;
        }

        private static double PolyArea(NavigationMesh mesh, int index)
        {
            NavigationMesh.dtPoly poly = mesh.Polygons[index];
            double area = 0;
            Vector3 a = mesh.Vertices[poly.verts[0]];
            for (int v = 1; v + 1 < poly.vertCount; v++)
                area += Vector3.Cross(mesh.Vertices[poly.verts[v]] - a, mesh.Vertices[poly.verts[v + 1]] - a).Length() * 0.5;
            return area;
        }

        /// <summary>
        /// The edges of the allowed polygons (no neighbour, or a neighbour that is not allowed: a wall, a drop, a door that starts closed),
        /// each with the way out of the floor, bucketed on a plan grid.
        /// </summary>
        private sealed class Rim
        {
            public struct Edge { public Vector3 A, B, Out; public int Polygon; }
            public readonly List<Edge> Edges = new List<Edge>();
            private const float Cell = 2f;
            private readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();

            public Rim(NavigationMesh mesh, bool[] allowed)
            {
                for (int i = 0; i < mesh.Polygons.Length; i++)
                {
                    if (!allowed[i]) continue;
                    NavigationMesh.dtPoly poly = mesh.Polygons[i];
                    Vector3 centre = Vector3.Zero;
                    for (int v = 0; v < poly.vertCount; v++) centre += mesh.Vertices[poly.verts[v]];
                    centre /= poly.vertCount;
                    for (int v = 0; v < poly.vertCount; v++)
                    {
                        int nei = poly.neis[v];
                        if (nei != 0 && (nei & 0x8000) == 0 && nei - 1 < allowed.Length && allowed[nei - 1]) continue;
                        Vector3 a = mesh.Vertices[poly.verts[v]], b = mesh.Vertices[poly.verts[(v + 1) % poly.vertCount]];
                        Vector3 e = new Vector3(b.X - a.X, 0, b.Z - a.Z);
                        if (e.LengthSquared() < 1e-6f) continue;
                        Vector3 n = Vector3.Normalize(new Vector3(e.Z, 0, -e.X));
                        if (Vector3.Dot(n, (a + b) * 0.5f - centre) < 0) n = -n;
                        Add(new Edge() { A = a, B = b, Out = n, Polygon = i });
                    }
                }
            }

            private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

            private void Add(Edge edge)
            {
                int index = Edges.Count;
                Edges.Add(edge);
                int x0 = (int)Math.Floor(Math.Min(edge.A.X, edge.B.X) / Cell), x1 = (int)Math.Floor(Math.Max(edge.A.X, edge.B.X) / Cell);
                int z0 = (int)Math.Floor(Math.Min(edge.A.Z, edge.B.Z) / Cell), z1 = (int)Math.Floor(Math.Max(edge.A.Z, edge.B.Z) / Cell);
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        if (!_cells.TryGetValue(Key(x, z), out List<int> list)) _cells[Key(x, z)] = list = new List<int>();
                        list.Add(index);
                    }
            }

            private IEnumerable<int> Near(float minX, float minZ, float maxX, float maxZ)
            {
                HashSet<int> seen = new HashSet<int>();
                for (int x = (int)Math.Floor(minX / Cell); x <= (int)Math.Floor(maxX / Cell); x++)
                    for (int z = (int)Math.Floor(minZ / Cell); z <= (int)Math.Floor(maxZ / Cell); z++)
                        if (_cells.TryGetValue(Key(x, z), out List<int> list))
                            foreach (int i in list)
                                if (seen.Add(i)) yield return i;
            }

            /// <summary>Plan distance to the nearest edge on the same floor (within <paramref name="band"/> up or down), up to <paramref name="max"/>.</summary>
            public float Nearest(Vector3 p, float max, float band = 1.2f)
            {
                float best = max;
                foreach (int i in Near(p.X - max, p.Z - max, p.X + max, p.Z + max))
                {
                    Edge e = Edges[i];
                    float d = PlanDistance(p, e.A, e.B, out Vector3 at);
                    if (d < best && Math.Abs(at.Y - p.Y) <= band) best = d;
                }
                return best;
            }

            /// <summary>How far along a plan direction the first edge on the same floor is, up to <paramref name="max"/>.</summary>
            public float Ray(Vector3 p, Vector3 direction, float max, float band = 1.2f)
            {
                Vector2 o = new Vector2(p.X, p.Z), d = Vector2.Normalize(new Vector2(direction.X, direction.Z));
                Vector2 end = o + d * max;
                float best = max;
                foreach (int i in Near(Math.Min(o.X, end.X), Math.Min(o.Y, end.Y), Math.Max(o.X, end.X), Math.Max(o.Y, end.Y)))
                {
                    Edge e = Edges[i];
                    if (Math.Abs((e.A.Y + e.B.Y) * 0.5f - p.Y) > band) continue;
                    Vector2 a = new Vector2(e.A.X, e.A.Z), s = new Vector2(e.B.X - e.A.X, e.B.Z - e.A.Z);
                    float den = d.X * s.Y - d.Y * s.X;
                    if (Math.Abs(den) < 1e-6f) continue;
                    Vector2 ao = a - o;
                    float t = (ao.X * s.Y - ao.Y * s.X) / den;
                    float u = (ao.X * d.Y - ao.Y * d.X) / den;
                    if (t > 1e-4f && t < best && u >= 0 && u <= 1) best = t;
                }
                return best;
            }

            private static float PlanDistance(Vector3 p, Vector3 a, Vector3 b, out Vector3 at)
            {
                Vector2 P = new Vector2(p.X, p.Z), A = new Vector2(a.X, a.Z), B = new Vector2(b.X, b.Z), ab = B - A;
                float t = ab.LengthSquared() < 1e-9f ? 0 : Math.Max(0, Math.Min(1, Vector2.Dot(P - A, ab) / ab.LengthSquared()));
                at = Vector3.Lerp(a, b, t);
                return Vector2.Distance(P, A + ab * t);
            }
        }

        /// <summary>
        /// A polygon's walkable surface: its detail triangles (the floor's real height, which a large polygon over steps or a slope can be a metre
        /// off), or its own fan where it has none. A detail index under the polygon's vertex count is one of its own vertices.
        /// </summary>
        private static IEnumerable<(Vector3, Vector3, Vector3)> SurfaceTriangles(NavigationMesh mesh, int index)
        {
            NavigationMesh.dtPoly poly = mesh.Polygons[index];
            if (mesh.DetailMeshes != null && index < mesh.DetailMeshes.Length && mesh.DetailMeshes[index].triCount > 0 && mesh.DetailIndices != null && mesh.DetailVertices != null)
            {
                NavigationMesh.dtPolyDetail detail = mesh.DetailMeshes[index];
                Vector3 Vertex(int k) => k < poly.vertCount ? mesh.Vertices[poly.verts[k]] : mesh.DetailVertices[detail.vertBase + k - poly.vertCount];
                for (int tri = 0; tri < detail.triCount; tri++)
                {
                    int at = (detail.triBase + tri) * 4;
                    if (at + 2 >= mesh.DetailIndices.Length) yield break;
                    yield return (Vertex(mesh.DetailIndices[at]), Vertex(mesh.DetailIndices[at + 1]), Vertex(mesh.DetailIndices[at + 2]));
                }
                yield break;
            }
            for (int v = 1; v + 1 < poly.vertCount; v++)
                yield return (mesh.Vertices[poly.verts[0]], mesh.Vertices[poly.verts[v]], mesh.Vertices[poly.verts[v + 1]]);
        }

        /// <summary>Points spread evenly over the allowed polygons (one per <paramref name="every"/> m², area weighted), with their polygon.</summary>
        private static List<(Vector3 Point, int Polygon)> Sample(Floor floor, float every, int cap, Random random, Vector3? min, Vector3? max)
        {
            List<(Vector3 a, Vector3 b, Vector3 c, int poly, double area)> triangles = new List<(Vector3, Vector3, Vector3, int, double)>();
            double total = 0;
            NavigationMesh mesh = floor.Mesh;
            for (int i = 0; i < mesh.Polygons.Length; i++)
            {
                if (!floor.Allowed[i] || floor.IslandArea[floor.Island[i]] < MinIslandArea) continue;
                if (mesh.Polygons[i].area.GetHeightLimitedAmount() != NavigationMesh.AreaHeight.Standing) continue;
                foreach ((Vector3 first, Vector3 b, Vector3 c) in SurfaceTriangles(mesh, i))
                {
                    Vector3 centre = (first + b + c) / 3f;
                    if (min != null && (centre.X < min.Value.X || centre.Y < min.Value.Y || centre.Z < min.Value.Z)) continue;
                    if (max != null && (centre.X > max.Value.X || centre.Y > max.Value.Y || centre.Z > max.Value.Z)) continue;
                    double area = Vector3.Cross(b - first, c - first).Length() * 0.5;
                    if (area < 1e-4) continue;
                    triangles.Add((first, b, c, i, area));
                    total += area;
                }
            }
            List<(Vector3, int)> points = new List<(Vector3, int)>();
            int count = (int)Math.Min(cap, Math.Ceiling(total / every));
            if (count <= 0) return points;
            //Stratified: each triangle takes its share, the remainder by chance, so small rooms are never skipped
            double perPoint = total / count, carry = 0;
            foreach (var t in triangles)
            {
                carry += t.area / perPoint;
                int n = (int)carry;
                carry -= n;
                if (random.NextDouble() < carry) { n++; carry -= 1; }
                for (int k = 0; k < n; k++)
                {
                    double r1 = random.NextDouble(), r2 = random.NextDouble();
                    if (r1 + r2 > 1) { r1 = 1 - r1; r2 = 1 - r2; }
                    points.Add((t.a + (t.b - t.a) * (float)r1 + (t.c - t.a) * (float)r2, t.poly));
                }
            }
            return points;
        }
        #endregion

        #region Proposing spots
        private static McpTool _guiTool;

        /// <summary>A call for the MCP layer's collision readers when the editor (not a tool) asks.</summary>
        public static McpCall EditorCall(Action<string> progress = null)
        {
            if (_guiTool == null) _guiTool = new McpTool() { Name = "ai_markup_window", Title = "AI Markup" };
            return new McpCall(_guiTool, new JObject(), System.Threading.CancellationToken.None, (text, done, total) => { try { progress?.Invoke(text); } catch { } });
        }

        /// <summary>
        /// Spots for the kinds the options ask for, over the saved navmesh (as last built). Search and area sweep jobs go on standing-height
        /// floor the NPCs walk, <see cref="MinClearance"/> or more from its edge and their kind's spacing apart, the more open spots first;
        /// high cover contexts stand <see cref="HighCoverWallDistance"/> back from a flat wall (high enough in the collision) and face it.
        /// UI thread or a tool's thread.
        /// </summary>
        public static MarkupProposal Propose(McpCall call, LevelContent content, MarkupOptions options)
        {
            Level level = content.Level;
            MarkupProposal proposal = new MarkupProposal();
            NavigationMeshQuery query = AlienVents.QueryOf(level, out NavigationMesh mesh);
            if (query == null)
                throw new McpError("The level has no navmesh to put markup on (or an empty one). Save & Build with the navmesh baker first.");
            proposal.NavmeshStale = McpEditor.UI(() => BuildTracker.NeedsBuild);
            if (proposal.NavmeshStale)
                proposal.Notes.Add("The navmesh is older than the level's last changes (a Save & Build is due): spots are found on the navmesh as last built.");
            proposal.Existing = McpEditor.UI(() => Survey(level.Commands));

            Vector3? min = null, max = null;
            if (options.RegionMin != null && options.RegionMax != null)
            {
                min = Vector3.Min(options.RegionMin.Value, options.RegionMax.Value);
                max = Vector3.Max(options.RegionMin.Value, options.RegionMax.Value);
            }
            void Reject(string why) => proposal.Rejected[why] = (proposal.Rejected.TryGetValue(why, out int n) ? n : 0) + 1;

            //The collision round the navmesh (or the region), read once when a pass needs it
            McpCollision.Soup soup = null;
            McpCollision.Soup Soup()
            {
                if (soup != null) return soup;
                Vector3 lo = new Vector3(float.MaxValue), hi = new Vector3(float.MinValue);
                foreach (NavigationMesh.dtPoly poly in mesh.Polygons)
                    for (int v = 0; v < poly.vertCount; v++) { lo = Vector3.Min(lo, mesh.Vertices[poly.verts[v]]); hi = Vector3.Max(hi, mesh.Vertices[poly.verts[v]]); }
                if (min != null) { lo = Vector3.Max(lo, min.Value); hi = Vector3.Min(hi, max.Value); }
                soup = McpCollision.SoupFor(call, McpRegion.Box(lo - new Vector3(3, 1, 3), hi + new Vector3(3, 16, 3), "round the navmesh"), "solid");
                return soup;
            }
            Func<Vector3, bool> indoors = null;
            if (options.IndoorsOnly)
                indoors = p =>
                {
                    if (Soup().CeilingOver(p + Vector3.UnitY * 0.5f, out McpCollision.Hit over, 15f) && over.Normal.Y < -0.3f) return true;
                    Reject("outdoors (no ceiling over it)");
                    return false;
                };

            int seed = options.Seed;
            if (options.Search || options.Sweep)
            {
                Floor floor = FloorFor(mesh, query, NpcClasses);
                List<(Vector3 Point, int Polygon)> samples = Sample(floor, 0.3f, 200000, new Random(seed), min, max);
                proposal.Samples += samples.Count;
                proposal.Area = Math.Max(proposal.Area, samples.Count * 0.3);
                //Every sample's room to stand in, once for both kinds
                List<(Vector3 Point, float Clearance)> open = new List<(Vector3, float)>();
                foreach ((Vector3 point, int polygon) in samples)
                {
                    float clearance = floor.Edges.Nearest(point, PreferredClearance + 0.5f);
                    if (clearance < MinClearance) { Reject("within " + MinClearance + " m of the edge of the floor"); continue; }
                    open.Add((point, clearance));
                }
                foreach (MarkupKind kind in new[] { SearchJob, AreaSweepJob })
                {
                    if (!options.Wants(kind)) continue;
                    call?.ThrowIfCancelled();
                    Random random = new Random(seed * 7919 + (kind == SearchJob ? 1 : 2));
                    List<(Vector3 Point, float Clearance, double Score)> scored = new List<(Vector3, float, double)>();
                    foreach ((Vector3 point, float clearance) in open)
                    {
                        if (options.Within != null && !options.Within(kind, point)) continue;
                        scored.Add((point, clearance, Math.Min(clearance, PreferredClearance) + random.NextDouble() * 0.5));
                    }
                    List<Vector3> keepClear = options.KeepClearOfExisting ? proposal.Existing.Where(o => o.Kind == kind).Select(o => o.Position).ToList() : new List<Vector3>();
                    foreach (var pick in Spread(scored.OrderByDescending(o => o.Score).Select(o => (o.Point, o.Clearance)), keepClear, options.SpacingOf(kind), indoors))
                        proposal.Spots.Add(new MarkupSpot() { Kind = kind, Position = Round(pick.Point), Clearance = (float)Math.Round(pick.Clearance, 2) });
                }
            }
            if (options.HighCover)
                ProposeHighCover(call, proposal, mesh, query, options, min, max, Reject, Soup, indoors);

            foreach (MarkupKind kind in Kinds.Where(options.Wants))
            {
                int n = proposal.Spots.Count(o => o.Kind == kind);
                if (n != 0) continue;
                if (options.KeepClearOfExisting && proposal.Existing.Any(o => o.Kind == kind))
                    proposal.Notes.Add("No new spot for " + kind.NamePrefix + ": what passed is within " + options.SpacingOf(kind) + " m of the level's own; the rest: " + Describe(proposal.Rejected) + ".");
                else
                    proposal.Notes.Add("No spot for " + kind.NamePrefix + " passed: " + Describe(proposal.Rejected) + ".");
            }
            //The game's own levels hold at most about 330 search and sweep jobs between them
            int jobs = proposal.Spots.Count(o => o.Kind != HighCover) + (options.KeepClearOfExisting ? proposal.Existing.Count(o => o.Kind != HighCover) : 0);
            if (jobs > MaxRetailJobs)
                proposal.Notes.Add("That makes " + jobs + " search and sweep jobs; no level of the game has more than about " + MaxRetailJobs + ". Place them only where NPCs hunt (a region, or untick the rest).");
            return proposal;
        }

        /* Best first, at least 'spacing' (in plan, on the same floor) from every spot taken and every point to keep clear of; 'accept' is
           asked only of a candidate that is free */
        private static IEnumerable<(Vector3 Point, float Clearance)> Spread(IEnumerable<(Vector3 Point, float Clearance)> best, List<Vector3> keepClear, float spacing, Func<Vector3, bool> accept = null)
        {
            Dictionary<long, List<Vector3>> taken = new Dictionary<long, List<Vector3>>();
            long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
            void Take(Vector3 p)
            {
                long k = Key((int)Math.Floor(p.X / spacing), (int)Math.Floor(p.Z / spacing));
                if (!taken.TryGetValue(k, out List<Vector3> list)) taken[k] = list = new List<Vector3>();
                list.Add(p);
            }
            bool Free(Vector3 p)
            {
                int cx = (int)Math.Floor(p.X / spacing), cz = (int)Math.Floor(p.Z / spacing);
                for (int x = cx - 1; x <= cx + 1; x++)
                    for (int z = cz - 1; z <= cz + 1; z++)
                        if (taken.TryGetValue(Key(x, z), out List<Vector3> list))
                            foreach (Vector3 q in list)
                                if (Math.Abs(q.Y - p.Y) < 2f && (q.X - p.X) * (q.X - p.X) + (q.Z - p.Z) * (q.Z - p.Z) < spacing * spacing)
                                    return false;
                return true;
            }
            foreach (Vector3 p in keepClear) Take(p);
            foreach (var candidate in best)
            {
                if (!Free(candidate.Point)) continue;
                if (accept != null && !accept(candidate.Point)) continue;
                Take(candidate.Point);
                yield return candidate;
            }
        }

        private static Vector3 Round(Vector3 v) => new Vector3((float)Math.Round(v.X, 2), (float)Math.Round(v.Y, 2), (float)Math.Round(v.Z, 2));

        /* High cover: walk the androids' floor edges every 0.5 m; a spot stands HighCoverWallDistance back from the edge, facing it, where
           the floor runs straight up to the edge across 1.2 m and the collision has a wall just past it at waist and head height */
        private static void ProposeHighCover(McpCall call, MarkupProposal proposal, NavigationMesh mesh, NavigationMeshQuery query, MarkupOptions options, Vector3? min, Vector3? max, Action<string> reject,
            Func<McpCollision.Soup> soupOf, Func<Vector3, bool> indoors)
        {
            Floor floor = FloorFor(mesh, query, AndroidClass);
            if (!floor.Allowed.Any(o => o))
            {
                proposal.Notes.Add("No navmesh admits androids, so there is nowhere for melee high cover.");
                return;
            }
            McpCollision.Soup soup = soupOf();
            NavigationMeshQuery.Options androids = new NavigationMeshQuery.Options() { Classes = AndroidClass, Extents = new Vector3(0.3f, 1.0f, 0.3f) };
            Vector3 up = Vector3.UnitY;
            Random random = new Random(options.Seed * 7919 + 3);
            List<(Vector3 Point, float Clearance, double Score, float Yaw, float Wall)> candidates = new List<(Vector3, float, double, float, float)>();
            foreach (Rim.Edge edge in floor.Edges.Edges)
            {
                call?.ThrowIfCancelled();
                if (floor.IslandArea[floor.Island[edge.Polygon]] < MinIslandArea) continue;
                if (mesh.Polygons[edge.Polygon].area.GetHeightLimitedAmount() != NavigationMesh.AreaHeight.Standing) continue;
                float length = new Vector2(edge.B.X - edge.A.X, edge.B.Z - edge.A.Z).Length();
                for (float along = 0.25f; along < length; along += 0.5f)
                {
                    Vector3 rim = Vector3.Lerp(edge.A, edge.B, along / length);
                    if (min != null && (rim.X < min.Value.X || rim.Y < min.Value.Y || rim.Z < min.Value.Z || rim.X > max.Value.X || rim.Y > max.Value.Y || rim.Z > max.Value.Z)) continue;
                    Vector3 n = edge.Out;
                    NavigationMeshQuery.Nearest at = query.FindNearest(rim - n * HighCoverWallDistance, androids);
                    if (!at.Found || at.Height != NavigationMesh.AreaHeight.Standing || !floor.Allowed[at.Polygon] || Flat(at.Point - (rim - n * HighCoverWallDistance)) > 0.15f || Math.Abs(at.Point.Y - rim.Y) > 0.6f)
                    { reject("no standing floor 2.15 m back from the edge"); continue; }
                    Vector3 p = at.Point;
                    if (options.Within != null && !options.Within(HighCover, p)) continue;
                    //The floor runs straight up to this edge, across the width of the throw
                    float front = floor.Edges.Ray(p, n, HighCoverWallDistance + 1.5f);
                    if (Math.Abs(front - HighCoverWallDistance) > 0.3f) { reject("the floor in front is not clear up to the wall"); continue; }
                    Vector3 side = new Vector3(n.Z, 0, -n.X);
                    float spread = 0;
                    bool flat = true;
                    foreach (float l in new[] { -0.6f, 0.6f })
                    {
                        float d = floor.Edges.Ray(p + side * l, n, HighCoverWallDistance + 1.5f);
                        spread = Math.Max(spread, Math.Abs(d - front));
                        if (Math.Abs(d - front) > 0.35f) flat = false;
                    }
                    if (!flat) { reject("the wall is not flat across the throw"); continue; }
                    float clearance = floor.Edges.Nearest(p, 1.5f);
                    if (clearance < 0.3f) { reject("hemmed in at the sides"); continue; }
                    //A wall in the collision just past the edge, at waist and head height, facing the spot
                    float wall = 0;
                    bool solid = true;
                    foreach (float h in new[] { 1.0f, 1.8f })
                    {
                        if (!soup.Cast(p + up * h, n, front + 1.2f, out McpCollision.Hit hit) || hit.Distance < front - 0.2f || Vector3.Dot(hit.Normal, -n) < 0.7f) { solid = false; break; }
                        wall = Math.Max(wall, hit.Distance);
                    }
                    if (!solid) { reject("no high wall in the collision past the edge"); continue; }
                    float yaw = (float)(Math.Atan2(n.X, n.Z) * 180.0 / Math.PI);
                    float snapped = (float)(Math.Round(yaw / 45.0) * 45.0);
                    if (Math.Abs(yaw - snapped) < 4f) yaw = snapped;
                    if (yaw <= -180f) yaw += 360f;
                    candidates.Add((p, clearance, 1.0 - spread + random.NextDouble() * 0.5, (float)Math.Round(yaw, 1), wall));
                }
            }
            List<Vector3> keepClear = options.KeepClearOfExisting ? proposal.Existing.Where(o => o.Kind == HighCover).Select(o => o.Position).ToList() : new List<Vector3>();
            Dictionary<Vector3, (float Yaw, float Wall)> facing = new Dictionary<Vector3, (float, float)>();
            foreach (var c in candidates) facing[c.Point] = (c.Yaw, c.Wall);
            foreach (var pick in Spread(candidates.OrderByDescending(o => o.Score).Select(o => (o.Point, o.Clearance)), keepClear, options.SpacingOf(HighCover), indoors))
                proposal.Spots.Add(new MarkupSpot() { Kind = HighCover, Position = Round(pick.Point), Clearance = (float)Math.Round(pick.Clearance, 2), Yaw = facing[pick.Point].Yaw, Wall = (float)Math.Round(facing[pick.Point].Wall, 2) });
        }

        private static float Flat(Vector3 d) => (float)Math.Sqrt(d.X * d.X + d.Z * d.Z);

        /// <summary>The rejection tally as text, biggest first.</summary>
        public static string Describe(Dictionary<string, int> rejected) =>
            rejected.Count == 0 ? "no samples" : string.Join("; ", rejected.OrderByDescending(o => o.Value).Select(o => o.Value + " " + o.Key));
        #endregion

        #region Bringing the composites in, placing
        /// <summary>The kinds the level does not have yet.</summary>
        public static List<MarkupKind> Missing(Commands commands, IEnumerable<MarkupKind> kinds) =>
            kinds.Where(o => o != null).Distinct().Where(o => commands.GetComposite(o.Id) == null).ToList();

        /// <summary>The level of this install the kinds would come from (null: none has them).</summary>
        public static string SourceFor()
        {
            foreach (string level in Sources)
                if (File.Exists(Path.Combine(Singleton.PathToAI ?? "", "DATA", "ENV", level, "WORLD", "COMMANDS.PAK")))
                    return level;
            return null;
        }

        /// <summary>
        /// Bring the markup composites the level lacks in from a retail level of this install, as port_composites does: not undoable,
        /// written at the next save. Returns how many composites arrived.
        /// </summary>
        public static int EnsureKinds(LevelContent content, IEnumerable<MarkupKind> kinds, List<string> notes, List<string> from = null)
        {
            Level destination = content.Level;
            List<MarkupKind> missing = Missing(destination.Commands, kinds);
            int ported = 0;
            foreach (string level in Sources)
            {
                if (missing.Count == 0) break;
                if (!File.Exists(Path.Combine(Singleton.PathToAI ?? "", "DATA", "ENV", level, "WORLD", "COMMANDS.PAK"))) continue;
                if (string.Equals(destination.Name, level, StringComparison.OrdinalIgnoreCase)) continue;
                Level source = CompositeImporter.LoadLevel(level);
                List<Composite> roots = missing.Select(o => source.Commands.GetComposite(o.Id)).Where(o => o != null).ToList();
                if (roots.Count == 0) continue;
                CompositeFlowgraphTable sourceLayouts = CustomTable.ReadTable(source.Commands.Filepath, CustomTableType.COMPOSITE_FLOWGRAPHS) as CompositeFlowgraphTable;
                List<Composite> arrived = new List<Composite>();
                Send.BeginSceneBatch();
                try
                {
                    Singleton.OnCompositeAddPending?.Invoke();
                    CompositePorter porter = new CompositePorter(source, destination) { OverwriteComposites = false, OverwriteAssets = false, Recurse = true };
                    porter.DoNotDescendInto.Add(McpPortingTools.GlobalId);
                    porter.DoNotDescendInto.Add(McpPortingTools.PauseMenuId);
                    if (destination.Commands.EntryPoints[1] != null) porter.DoNotDescendInto.Add(destination.Commands.EntryPoints[1].shortGUID);
                    if (destination.Commands.EntryPoints[2] != null) porter.DoNotDescendInto.Add(destination.Commands.EntryPoints[2].shortGUID);
                    porter.OnCompositePorted = (original, copy) =>
                    {
                        arrived.Add(copy);
                        Singleton.OnCompositeAdded?.Invoke(copy);
                        FlowgraphLayoutManager.ImportLayouts(copy, FlowgraphLayoutManager.GetLayoutsForPort(original, sourceLayouts, FlowgraphLayoutManager.BundledLevelName(source.Commands, level), source.Commands));
                        EditHistory.CompositesChanged(destination.Commands, new[] { copy });
                    };
                    foreach (Composite root in roots)
                        porter.Port(root);
                }
                finally
                {
                    Send.EndSceneBatch();
                }
                ViewerResourceSync.SyncImmediately();
                Singleton.Editor?.CompositeBrowser?.RefreshList();
                ported += arrived.Count;
                from?.Add(level);
                notes.Add("Brought " + string.Join(", ", missing.Where(o => roots.Any(r => r.shortGUID == o.Id)).Select(o => o.Path)) + " in from " + level + " (" + arrived.Count + " composite(s); not undoable, as any port).");
                source = null;
                GC.Collect();
                missing = Missing(destination.Commands, missing);
            }
            if (missing.Count != 0)
                throw new McpError("No level of this install has " + string.Join(", ", missing.Select(o => o.Path)) + " (they come from " + string.Join(", ", Sources) + "). Port it from a level that has it (port_composites).");
            return ported;
        }

        /// <summary>
        /// Place markup at spots as one undo step ("Place AI Markup"), after bringing in the kinds the level lacks (not undoable). Each
        /// instance stands at its spot, high cover facing its wall. UI thread or a tool's.
        /// </summary>
        public static MarkupPlaced Place(LevelContent content, IList<MarkupSpot> spots, MarkupOptions options, string labelPrefix, bool show)
        {
            Commands commands = content.Level.Commands;
            List<MarkupSpot> use = spots.Where(o => o != null && o.Use && o.Kind != null).ToList();
            if (use.Count == 0)
                throw new McpError("No spots to place markup at.");
            Composite root = commands.EntryPoints[0];
            Composite into = options.Into ?? root;
            if (!commands.Entries.Contains(into))
                throw new McpError("The composite to put the markup in is not in the level.");
            cTransform frame = into == root ? new cTransform(Vector3.Zero, Vector3.Zero) : McpSpatialTools.FrameOf(commands, into, null, "a composite placed once (or the root)", out JObject _);

            MarkupPlaced placed = new MarkupPlaced() { Into = into };
            placed.Ported = EnsureKinds(content, use.Select(o => o.Kind), placed.Notes, placed.PortedFrom);

            HashSet<string> names = new HashSet<string>(into.functions.Select(o => SafeName(commands, into, o)), StringComparer.OrdinalIgnoreCase);
            Dictionary<MarkupKind, int> next = Kinds.ToDictionary(o => o, o => 1);
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run("Place AI Markup (" + use.Count + ")", into, edit =>
            {
                foreach (MarkupSpot spot in use)
                {
                    Composite kind = commands.GetComposite(spot.Kind.Id) ?? throw new McpError(spot.Kind.Path + " is not in the level.");
                    string prefix = options.NamePrefix.TryGetValue(spot.Kind.Key, out string p) && !string.IsNullOrWhiteSpace(p) ? p.Trim() : spot.Kind.NamePrefix;
                    while (names.Contains(prefix + "_" + next[spot.Kind])) next[spot.Kind]++;
                    string name = prefix + "_" + next[spot.Kind];
                    names.Add(name);
                    FunctionEntity instance = edit.AddInstance(into, kind, name);
                    cTransform local = InstanceTransform.ToLocal(frame, new cTransform(spot.Position, new Vector3(0, spot.Yaw, 0)));
                    edit.SetParameter(into, instance, "position", new JObject()
                    {
                        ["position"] = new JArray(Math.Round(local.position.X, 4), Math.Round(local.position.Y, 4), Math.Round(local.position.Z, 4)),
                        ["rotation"] = new JArray(Math.Round(local.rotation.X, 3), Math.Round(local.rotation.Y, 3), Math.Round(local.rotation.Z, 3)),
                    });
                    edit.PlaceNode(instance, options.Page, null);
                    placed.Instances.Add(instance);
                    placed.Names.Add(name);
                }
                edit.Select.Clear();
                edit.Select.AddRange(placed.Instances.Take(500));
            }, new McpScriptEdit.RunOptions() { Show = show, LabelPrefix = labelPrefix ?? "" });
            placed.Notes.AddRange(outcome.Notes);
            return placed;
        }

        private static string SafeName(Commands commands, Composite composite, Entity entity)
        {
            try { return McpScript.EntityName(commands, composite, entity); } catch { return ""; }
        }
        #endregion
    }
}
