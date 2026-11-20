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
        /// <summary>Melee cover: the wall or surface it faces, measured from the spot (m).</summary>
        public float Wall;
        /// <summary>Low cover: how high the surface's top stands over the spot (m).</summary>
        public float Top;
        /// <summary>A corpse trap: the composite it goes in (the android's), that composite's world frame, and the android it guards.</summary>
        public Composite Into;
        public cTransform Frame;
        public string Guards;
        public bool Use = true;
    }

    /// <summary>The tool's settings.</summary>
    internal sealed class MarkupOptions
    {
        public bool Search = true, Sweep = true, HighCover = false, LowCover = false;
        /// <summary>Corpse traps round every android set to play dead that has none (the level needs them).</summary>
        public bool CorpseTraps = true;
        /// <summary>Find corpse traps for androids that already have them too (the retail check).</summary>
        public bool IgnoreExistingCorpseTraps;
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
        public bool Wants(MarkupKind kind) =>
            kind == AIMarkup.SearchJob ? Search : kind == AIMarkup.AreaSweepJob ? Sweep : kind == AIMarkup.HighCover ? HighCover : kind == AIMarkup.LowCover ? LowCover : kind == AIMarkup.CorpseTrap && CorpseTraps;
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
    /// Archetypes\Jobs\AreaSweepJob (the spots they sweep over), Archetypes\Jobs\MeleeContext\MeleeContext_HighCover and MeleeContext
    /// (where an android throws the player against a wall, or onto a desk) and MeleeContext_CorpseTrap (round an android playing dead).
    /// The game gathers them by position; nothing links to them. UI thread unless said.
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
        public static readonly MarkupKind LowCover = new MarkupKind()
        {
            Key = "lowcover", Label = "Melee low cover (MeleeContext)", Path = @"Archetypes\Jobs\MeleeContext\MeleeContext", Id = new ShortGuid("1A-7A-E2-07"),
            NamePrefix = "MeleeContext", Spacing = 5f, Classes = AndroidClass,
        };
        public static readonly MarkupKind CorpseTrap = new MarkupKind()
        {
            Key = "corpsetrap", Label = "Corpse traps (MeleeContext_CorpseTrap)", Path = @"Archetypes\Jobs\MeleeContext\MeleeContext_CorpseTrap", Id = new ShortGuid("9C-23-CD-C9"),
            NamePrefix = "MeleeContext_CorpseTrap", Spacing = 0f, Classes = AndroidClass,
        };
        public static readonly MarkupKind[] Kinds = { SearchJob, AreaSweepJob, HighCover, LowCover, CorpseTrap };

        /* Retail placement, measured over the shipped levels: jobs stand on standing-height floor 0.3 m or more from its edge (median
           0.85 m), as many of each kind as a 5.5 m spacing gives; a high cover context faces a flat, high wall 2.5 m in front of it, measured
           to the wall's face (its markup's T). The navmesh stops about its walkable radius short of a wall, so on the navmesh that is
           about 2.15 m back from the floor's edge. A low cover context faces a desk, bed or counter 1.25 m in front (to its face, median
           1.3 m), in the middle of the face: its top 0.65-1.4 m up, running on 0.25 m or more, with a metre or more of room over it. */
        public const float MinClearance = 0.35f;
        public const float PreferredClearance = 1.2f;
        public const float HighCoverWallDistance = Markup.BuiltInMarkup.HighCoverWallDistance;
        /// <summary>How far off the wall a placed high cover context may end up (m).</summary>
        public const float HighCoverWallTolerance = 0.1f;
        public const float LowCoverDistance = Markup.BuiltInMarkup.LowCoverDistance;
        /// <summary>How far off the surface a placed low cover context may end up (m).</summary>
        public const float LowCoverTolerance = 0.1f;
        /// <summary>The height range of a low cover surface's top over the floor (m).</summary>
        public const float LowCoverMinTop = 0.6f, LowCoverMaxTop = 1.4f;
        /// <summary>Corpse traps: how far from the android, how many, and how near an existing one counts as its own (m).</summary>
        public const float CorpseTrapDistance = 0.9f;
        public const int CorpseTrapCount = 3;
        public const float CorpseTrapReach = 2f;
        public const float MinIslandArea = 25f;
        /// <summary>How far round its own markup the retail check looks for spots (m).</summary>
        public const float ArenaRadius = 6f;
        /// <summary>About the most search and sweep jobs a shipped level places.</summary>
        public const int MaxRetailJobs = 330;

        /* Retail levels that place the kinds (the first places all five, so one load brings any of them) */
        private static readonly string[] Sources = { "PRODUCTION/TECH_COMMS", "PRODUCTION/TECH_HUB", "PRODUCTION/TECH_RND", "PRODUCTION/HAB_SHOPPINGCENTRE", "PRODUCTION/SCI_HOSPITALUPPER" };

        /* The android composites, and the instance parameter that has one play dead until the player comes close */
        private static readonly ShortGuid[] AndroidIds = { new ShortGuid("3C-D3-BF-A1"), new ShortGuid("2D-C9-79-2F") };
        private static readonly string[] AndroidPaths = { @"Archetypes\NPCs\Android\Android_NPC", @"Archetypes\NPCs\Android\Android_Heavy_NPC" };
        private static readonly ShortGuid CorpseTrapFlag = ShortGuidUtils.Generate("is_corpse_trap_on_start");

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
            if (t == "lowcovermelee" || t == "meleelowcover") return LowCover;
            if (t == "corpsetraps" || t == "corpse" || t == "meleecontextcorpsetrap") return CorpseTrap;
            throw new McpError("'" + text + "' is not a kind of AI markup: search, sweep, highcover, lowcover or corpsetrap.");
        }

        public static MarkupKind KindOf(Composite composite) => composite == null ? null : Kinds.FirstOrDefault(o => o.Id == composite.shortGUID);

        #region Survey
        /// <summary>
        /// The markup the level places, from the entry points down: every search and area sweep job (JOB_SystematicSearch, JOB_AreaSweep)
        /// and every melee context (NPC_MeleeContext: high cover, low cover, corpse trap), however it is wrapped. Any thread.
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
                    //Unset is the type's default, low cover
                    int type = function.GetParameter(ContextType)?.content is cEnum given ? given.enumIndex : (int)MELEE_CONTEXT_TYPE.ANDROID_LOW_COVER_GRAPPLE;
                    switch ((MELEE_CONTEXT_TYPE)type)
                    {
                        case MELEE_CONTEXT_TYPE.ANDROID_HIGH_COVER_GRAPPLE: return HighCover;
                        case MELEE_CONTEXT_TYPE.ANDROID_LOW_COVER_GRAPPLE: return LowCover;
                        case MELEE_CONTEXT_TYPE.ANDROID_CORPSE_TRAP_GRAPPLE: return CorpseTrap;
                    }
                    return null;
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
        /// high cover contexts stand <see cref="HighCoverWallDistance"/> back from the face of a flat wall (high enough in the collision) and face it,
        /// low cover contexts <see cref="LowCoverDistance"/> back from a waist-high surface; corpse traps go round each android set to play dead
        /// that has none. UI thread or a tool's thread.
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
                ProposeCover(call, proposal, HighCover, mesh, query, options, min, max, Reject, Soup, indoors);
            if (options.LowCover)
                ProposeCover(call, proposal, LowCover, mesh, query, options, min, max, Reject, Soup, indoors);
            if (options.CorpseTraps)
                ProposeCorpseTraps(call, proposal, level.Commands, mesh, query, options, min, max, Reject, Soup);

            foreach (MarkupKind kind in Kinds.Where(options.Wants))
            {
                if (kind == CorpseTrap) continue;
                int n = proposal.Spots.Count(o => o.Kind == kind);
                if (n != 0) continue;
                if (options.KeepClearOfExisting && proposal.Existing.Any(o => o.Kind == kind))
                    proposal.Notes.Add("No new spot for " + kind.NamePrefix + ": what passed is within " + options.SpacingOf(kind) + " m of the level's own; the rest: " + Describe(proposal.Rejected) + ".");
                else
                    proposal.Notes.Add("No spot for " + kind.NamePrefix + " passed: " + Describe(proposal.Rejected) + ".");
            }
            //The game's own levels hold at most about 330 search and sweep jobs between them
            int jobs = proposal.Spots.Count(o => o.Kind == SearchJob || o.Kind == AreaSweepJob) + (options.KeepClearOfExisting ? proposal.Existing.Count(o => o.Kind == SearchJob || o.Kind == AreaSweepJob) : 0);
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

        /* Melee cover: walk the androids' floor edges every 0.5 m. High cover: where the collision has a flat wall just past the edge at
           waist and head height, a spot stands HighCoverWallDistance from that wall's face, facing it, on standing floor that runs straight
           up to the wall across 1.2 m. Low cover: where it has a waist-high surface (SurfaceAhead), a spot stands LowCoverDistance from its
           face. The distance is to the face, not the edge: how far short of it the navmesh stops varies */
        private static void ProposeCover(McpCall call, MarkupProposal proposal, MarkupKind kind, NavigationMesh mesh, NavigationMeshQuery query, MarkupOptions options, Vector3? min, Vector3? max, Action<string> reject,
            Func<McpCollision.Soup> soupOf, Func<Vector3, bool> indoors)
        {
            bool high = kind == HighCover;
            float distance = high ? HighCoverWallDistance : LowCoverDistance, tolerance = high ? HighCoverWallTolerance : LowCoverTolerance;
            string what = high ? "wall" : "surface";
            Floor floor = FloorFor(mesh, query, AndroidClass);
            if (!floor.Allowed.Any(o => o))
            {
                proposal.Notes.Add("No navmesh admits androids, so there is nowhere for melee " + (high ? "high" : "low") + " cover.");
                return;
            }
            McpCollision.Soup soup = soupOf();
            bool Ahead(Vector3 from, Vector3 n, float reach, out float face, out float top)
            {
                top = 0;
                if (high) return WallAhead(soup, from, n, reach, out face);
                return SurfaceAhead(soup, from, n, reach, out face, out top, reject);
            }
            NavigationMeshQuery.Options androids = new NavigationMeshQuery.Options() { Classes = AndroidClass, Extents = new Vector3(0.3f, 1.0f, 0.3f) };
            Random random = new Random(options.Seed * 7919 + (high ? 3 : 4));
            List<(Vector3 Point, float Clearance, double Score, float Yaw, float Wall, float Top)> candidates = new List<(Vector3, float, double, float, float, float)>();
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
                    //The face just past the edge
                    if (!Ahead(rim, n, 1.2f, out float gap, out float _)) { if (high) reject("no high wall in the collision past the edge"); continue; }
                    Vector3 target = rim - n * (distance - gap);
                    NavigationMeshQuery.Nearest at = query.FindNearest(target, androids);
                    if (!at.Found || at.Height != NavigationMesh.AreaHeight.Standing || !floor.Allowed[at.Polygon] || Flat(at.Point - target) > 0.15f || Math.Abs(at.Point.Y - rim.Y) > 0.6f)
                    { reject("no standing floor " + distance + " m back from the " + what); continue; }
                    Vector3 p = at.Point;
                    if (options.Within != null && !options.Within(kind, p)) continue;
                    //Measured again from the spot: nothing in between, and the face where the throw ends
                    if (!Ahead(p, n, distance + 1.2f, out float wall, out float top) || Math.Abs(wall - distance) > tolerance)
                    { reject("the " + what + " is not " + distance + " m in front"); continue; }
                    //The floor runs straight up to it
                    float front = floor.Edges.Ray(p, n, distance + 1.5f);
                    if (front < wall - 0.6f || front > wall + 0.05f) { reject("the floor in front is not clear up to the " + what); continue; }
                    float spread = 0;
                    if (high)
                    {
                        //Across the width of the throw
                        Vector3 side = new Vector3(n.Z, 0, -n.X);
                        bool flat = true;
                        foreach (float l in new[] { -0.6f, 0.6f })
                        {
                            float d = floor.Edges.Ray(p + side * l, n, distance + 1.5f);
                            spread = Math.Max(spread, Math.Abs(d - front));
                            if (Math.Abs(d - front) > 0.35f) flat = false;
                        }
                        if (!flat) { reject("the wall is not flat across the throw"); continue; }
                    }
                    else
                    {
                        //In the middle of the surface's face, where the game puts its own
                        FaceSpan(soup, p, n, wall, top, out float left, out float right);
                        if (Math.Abs(right - left) > 0.01f)
                        {
                            Vector3 shift = new Vector3(n.Z, 0, -n.X) * ((right - left) * 0.5f);
                            NavigationMeshQuery.Nearest middle = query.FindNearest(p + shift, androids);
                            if (middle.Found && middle.Height == NavigationMesh.AreaHeight.Standing && floor.Allowed[middle.Polygon] && Flat(middle.Point - (p + shift)) <= 0.15f &&
                                Ahead(middle.Point, n, distance + 1.2f, out float wall2, out float top2) && Math.Abs(wall2 - distance) <= tolerance)
                            {
                                p = middle.Point;
                                wall = wall2;
                                top = top2;
                                left = right = (left + right) * 0.5f;
                            }
                        }
                        spread = Math.Abs(wall - distance) * 2 + Math.Abs(right - left) - (left + right) * 0.25f;
                    }
                    float clearance = floor.Edges.Nearest(p, 1.5f);
                    if (clearance < (high ? 0.3f : 0.2f)) { reject("hemmed in at the sides"); continue; }
                    float yaw = (float)(Math.Atan2(n.X, n.Z) * 180.0 / Math.PI);
                    float snapped = (float)(Math.Round(yaw / 45.0) * 45.0);
                    if (Math.Abs(yaw - snapped) < 4f) yaw = snapped;
                    if (yaw <= -180f) yaw += 360f;
                    candidates.Add((p, clearance, 1.0 - spread + random.NextDouble() * 0.5, (float)Math.Round(yaw, 1), wall, top));
                }
            }
            List<Vector3> keepClear = options.KeepClearOfExisting ? proposal.Existing.Where(o => o.Kind == kind).Select(o => o.Position).ToList() : new List<Vector3>();
            Dictionary<Vector3, (float Yaw, float Wall, float Top)> facing = new Dictionary<Vector3, (float, float, float)>();
            foreach (var c in candidates) facing[c.Point] = (c.Yaw, c.Wall, c.Top);
            foreach (var pick in Spread(candidates.OrderByDescending(o => o.Score).Select(o => (o.Point, o.Clearance)), keepClear, options.SpacingOf(kind), indoors))
                proposal.Spots.Add(new MarkupSpot()
                {
                    Kind = kind, Position = Round(pick.Point), Clearance = (float)Math.Round(pick.Clearance, 2), Yaw = facing[pick.Point].Yaw,
                    Wall = (float)Math.Round(facing[pick.Point].Wall, 2), Top = (float)Math.Round(facing[pick.Point].Top, 2),
                });
        }

        /* How far a low surface's face runs to each side of the throw, just under its top (up to a metre each way, in quarter metres):
           the game centres its low cover on the surface */
        private static void FaceSpan(McpCollision.Soup soup, Vector3 from, Vector3 n, float face, float top, out float left, out float right)
        {
            Vector3 side = new Vector3(n.Z, 0, -n.X);
            float[] reach = new float[2];
            for (int s = 0; s < 2; s++)
                for (float l = 0.25f; l <= 1.001f; l += 0.25f)
                {
                    if (!soup.Cast(from + side * (s == 0 ? -l : l) + Vector3.UnitY * (top - 0.08f), n, face + 0.3f, out McpCollision.Hit hit) || Math.Abs(hit.Distance - face) > 0.1f) break;
                    reach[s] = l;
                }
            left = reach[0];
            right = reach[1];
        }

        /* A low cover surface in the collision along n from a floor point: solid at knee height within reach, its top (a ray down just past
           the face) LowCoverMinTop-LowCoverMaxTop up and running on 0.25 m, upright up to the top (the face is measured just under it), open
           over the top (nothing at chest height for half a metre past the face, a metre of room above it) */
        private static bool SurfaceAhead(McpCollision.Soup soup, Vector3 from, Vector3 n, float reach, out float face, out float top, Action<string> reject)
        {
            face = top = 0;
            Vector3 up = Vector3.UnitY;
            if (!soup.Cast(from + up * 0.4f, n, reach, out McpCollision.Hit low)) { reject("no low surface in the collision past the edge"); return false; }
            Vector3 over = from + n * (low.Distance + 0.15f);
            if (!soup.Cast(over + up * (LowCoverMaxTop + 0.3f), -up, LowCoverMaxTop + 0.3f, out McpCollision.Hit lid) || lid.Normal.Y < 0.7f)
            { reject("the surface has no flat top"); return false; }
            top = lid.Point.Y - from.Y;
            if (top < LowCoverMinTop || top > LowCoverMaxTop) { reject("the surface's top is not " + LowCoverMinTop + "-" + LowCoverMaxTop + " m up"); return false; }
            //The face, just under the top
            if (!soup.Cast(from + up * (top - 0.08f), n, reach, out McpCollision.Hit lip) || Vector3.Dot(lip.Normal, -n) < 0.7f || lip.Distance > low.Distance + 0.05f || lip.Distance < low.Distance - 0.4f)
            { reject("the surface is not upright up to its top"); return false; }
            face = lip.Distance;
            if (!soup.Cast(from + n * (face + 0.35f) + up * (top + 0.3f), -up, 0.6f, out McpCollision.Hit on) || Math.Abs(on.Point.Y - lid.Point.Y) > 0.1f)
            { reject("the surface's top is too shallow"); return false; }
            if (soup.Cast(from + up * (top + 0.25f), n, face + 0.5f, out McpCollision.Hit _))
            { reject("something stands on or just past the surface"); return false; }
            if (soup.Cast(from + n * (face + 0.15f) + up * (top + 0.05f), up, 1.0f, out McpCollision.Hit _))
            { reject("no room over the surface"); return false; }
            return true;
        }

        #region Corpse traps
        /// <summary>An android the level places, with the corpse traps beside it in its own composite.</summary>
        internal sealed class PlacedAndroid
        {
            public FunctionEntity Instance;
            /// <summary>The composite holding the instance, and where that composite stands in the world (the first placement of it).</summary>
            public Composite Holder;
            public cTransform Frame, World;
            /// <summary>Set to play dead (is_corpse_trap_on_start).</summary>
            public bool PlaysDead;
            /// <summary>Corpse traps in its composite within <see cref="CorpseTrapReach"/> of it.</summary>
            public int Traps;
            public string Name, Where;
        }

        /// <summary>Every android instance the level places (once per composite it is in), whether it plays dead and its corpse traps. UI thread.</summary>
        public static List<PlacedAndroid> Androids(Commands commands)
        {
            List<PlacedAndroid> found = new List<PlacedAndroid>();
            if (commands?.Entries == null || commands.EntryPoints == null || commands.EntryPoints.Length == 0 || commands.EntryPoints[0] == null)
                return found;
            HashSet<ShortGuid> androidIds = new HashSet<ShortGuid>(AndroidIds);
            foreach (Composite c in commands.Entries)
                if (AndroidPaths.Any(o => string.Equals(c.name, o, StringComparison.OrdinalIgnoreCase))) androidIds.Add(c.shortGUID);
            bool IsAndroid(FunctionEntity f) => !f.function.IsFunctionType && androidIds.Contains(f.function);

            Dictionary<Composite, bool> holds = new Dictionary<Composite, bool>();
            bool Holds(Composite composite)
            {
                if (composite == null) return false;
                if (holds.TryGetValue(composite, out bool known)) return known;
                holds[composite] = false;
                bool result = composite.functions.Any(IsAndroid);
                if (!result)
                    foreach (FunctionEntity function in composite.functions)
                        if (!function.function.IsFunctionType && !androidIds.Contains(function.function) && Holds(commands.GetComposite(function.function))) { result = true; break; }
                holds[composite] = result;
                return result;
            }

            Composite root = commands.EntryPoints[0];
            HashSet<(Composite, FunctionEntity)> seen = new HashSet<(Composite, FunctionEntity)>();
            List<(McpPlacements.Placement Placement, cTransform Local)> placements = new List<(McpPlacements.Placement, cTransform)>();
            McpPlacements walker = McpRegion.Walker(commands);
            walker.Walk(root, step =>
            {
                if (step.Entity is FunctionEntity function && IsAndroid(function))
                    placements.Add((McpPlacements.Placement.Of(step), step.Local == null ? null : new cTransform(step.Local.position, step.Local.rotation)));
                return true;
            }, o => !androidIds.Contains(o.shortGUID) && Holds(o));
            foreach ((McpPlacements.Placement placement, cTransform local) in placements)
            {
                if (!placement.Real || placement.World == null) continue;
                FunctionEntity instance = (FunctionEntity)placement.Chain[placement.Chain.Count - 1];
                List<Composite> along = McpScript.CompositesAlong(commands, root, placement.Chain);
                Composite holder = along[placement.Chain.Count - 1];
                if (holder == null || !seen.Add((holder, instance))) continue;
                PlacedAndroid android = new PlacedAndroid()
                {
                    Instance = instance,
                    Holder = holder,
                    World = placement.World,
                    Frame = InstanceTransform.Compose(placement.World, InstanceTransform.Inverse(local ?? new cTransform(Vector3.Zero, Vector3.Zero))),
                    PlaysDead = instance.GetParameter(CorpseTrapFlag)?.content is cBool flag && flag.value,
                };
                cTransform at = InstanceTransform.TransformOf(instance) ?? new cTransform(Vector3.Zero, Vector3.Zero);
                android.Traps = holder.functions.Count(o => !o.function.IsFunctionType && o.function == CorpseTrap.Id && o != instance &&
                    Vector3.Distance((InstanceTransform.TransformOf(o) ?? new cTransform(Vector3.Zero, Vector3.Zero)).position, at.position) <= CorpseTrapReach);
                try { android.Name = McpScript.EntityName(commands, holder, instance); } catch { android.Name = instance.shortGUID.ToByteString(); }
                android.Where = EditorUtils.GetCompositeName(holder) + " / " + android.Name;
                found.Add(android);
            }
            return found;
        }

        /* Corpse traps: round each android set to play dead with none of its own, up to CorpseTrapCount of them CorpseTrapDistance out, 120
           degrees apart (the first in front of it), each facing away from it, at its floor height. A spot must be on the androids' navmesh with
           nothing in the collision between it and the android; a blocked one turns up to 45 degrees or moves in or out, or is left out */
        private static void ProposeCorpseTraps(McpCall call, MarkupProposal proposal, Commands commands, NavigationMesh mesh, NavigationMeshQuery query, MarkupOptions options, Vector3? min, Vector3? max,
            Action<string> reject, Func<McpCollision.Soup> soupOf)
        {
            List<PlacedAndroid> dead = McpEditor.UI(() => Androids(commands)).Where(o => o.PlaysDead).ToList();
            if (dead.Count == 0)
            {
                proposal.Notes.Add("No android is set to play dead (is_corpse_trap_on_start), so no corpse traps are needed.");
                return;
            }
            List<PlacedAndroid> need = dead.Where(o => options.IgnoreExistingCorpseTraps || o.Traps == 0).ToList();
            if (need.Count == 0) return;
            McpCollision.Soup soup = soupOf();
            Floor floor = FloorFor(mesh, query, AndroidClass);
            NavigationMeshQuery.Options androids = new NavigationMeshQuery.Options() { Classes = AndroidClass, Extents = new Vector3(0.3f, 0.6f, 0.3f) };
            int tight = 0;
            foreach (PlacedAndroid android in need)
            {
                call?.ThrowIfCancelled();
                Vector3 a = android.World.position;
                if (min != null && (a.X < min.Value.X || a.Y < min.Value.Y || a.Z < min.Value.Z || a.X > max.Value.X || a.Y > max.Value.Y || a.Z > max.Value.Z)) continue;
                if (options.Within != null && !options.Within(CorpseTrap, a)) continue;
                float facing = YawOf(android.World);
                List<float> taken = new List<float>();
                for (int slot = 0; slot < CorpseTrapCount; slot++)
                {
                    bool placed = false;
                    foreach (float turn in new[] { 0f, 15f, -15f, 30f, -30f, 45f, -45f })
                    {
                        float angle = Wrap(facing + slot * 360f / CorpseTrapCount + turn);
                        if (taken.Any(o => AngleBetween(o, angle) < 75f)) continue;
                        Vector3 dir = new Vector3((float)Math.Sin(angle * Math.PI / 180.0), 0, (float)Math.Cos(angle * Math.PI / 180.0));
                        foreach (float reach in new[] { CorpseTrapDistance, CorpseTrapDistance - 0.15f, CorpseTrapDistance + 0.2f })
                        {
                            Vector3 p = a + dir * reach;
                            NavigationMeshQuery.Nearest at = query.FindNearest(p, androids);
                            if (!at.Found || !floor.Allowed[at.Polygon] || Flat(at.Point - p) > 0.2f || Math.Abs(at.Point.Y - p.Y) > 0.35f) { reject("off the androids' navmesh"); continue; }
                            if (soup.Cast(a + Vector3.UnitY * 0.3f, dir, reach, out McpCollision.Hit _) || soup.Cast(a + Vector3.UnitY * 0.9f, dir, reach, out McpCollision.Hit _))
                            { reject("collision between it and the android"); continue; }
                            taken.Add(angle);
                            proposal.Spots.Add(new MarkupSpot()
                            {
                                Kind = CorpseTrap, Position = Round(p), Yaw = (float)Math.Round(angle, 1), Clearance = (float)Math.Round(floor.Edges.Nearest(p, 1.5f), 2),
                                Wall = (float)Math.Round(reach, 2), Into = android.Holder, Frame = android.Frame, Guards = android.Where,
                            });
                            placed = true;
                            break;
                        }
                        if (placed) break;
                    }
                }
                if (taken.Count < CorpseTrapCount) tight++;
            }
            int skipped = dead.Count - need.Count;
            proposal.Notes.Add(dead.Count + " android(s) play dead" + (skipped != 0 ? ", " + skipped + " with corpse traps already" : "") + ": " + proposal.Spots.Count(o => o.Kind == CorpseTrap) + " corpse trap(s) for " + (need.Count) + "." +
                (tight != 0 ? " " + tight + " had room for fewer than " + CorpseTrapCount + "." : ""));
        }

        private static float Wrap(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            if (angle <= -180f) angle += 360f;
            return angle;
        }
        #endregion

        /* A wall in the collision along n from a floor point, at waist and head height: facing back the way it was looked for, upright (the
           two heights agree) and within reach. Its distance is the nearer of the two */
        private static bool WallAhead(McpCollision.Soup soup, Vector3 from, Vector3 n, float reach, out float distance)
        {
            distance = float.MaxValue;
            float first = -1;
            foreach (float h in new[] { 1.0f, 1.8f })
            {
                if (!soup.Cast(from + Vector3.UnitY * h, n, reach, out McpCollision.Hit hit) || Vector3.Dot(hit.Normal, -n) < 0.7f)
                    return false;
                if (first >= 0 && Math.Abs(hit.Distance - first) > 0.2f)
                    return false;
                first = hit.Distance;
                distance = Math.Min(distance, hit.Distance);
            }
            return true;
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
        /// instance stands at its spot, melee cover facing its wall or surface; a corpse trap goes in its android's composite. UI thread or a tool's.
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
            if (use.Any(o => o.Into != null && !commands.Entries.Contains(o.Into)))
                throw new McpError("An android's composite the corpse traps go in is no longer in the level: find the spots again.");
            cTransform frame = into == root || use.All(o => o.Into != null) ? new cTransform(Vector3.Zero, Vector3.Zero) : McpSpatialTools.FrameOf(commands, into, null, "a composite placed once (or the root)", out JObject _);
            Composite focus = use.Any(o => o.Into == null) ? into : use[0].Into;
            List<Composite> touched = use.Select(o => o.Into ?? into).Distinct().ToList();

            MarkupPlaced placed = new MarkupPlaced() { Into = focus };
            placed.Ported = EnsureKinds(content, use.Select(o => o.Kind), placed.Notes, placed.PortedFrom);

            Dictionary<Composite, HashSet<string>> names = new Dictionary<Composite, HashSet<string>>();
            Dictionary<(Composite, MarkupKind), int> next = new Dictionary<(Composite, MarkupKind), int>();
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run("Place AI Markup (" + use.Count + ")", focus, edit =>
            {
                foreach (MarkupSpot spot in use)
                {
                    Composite kind = commands.GetComposite(spot.Kind.Id) ?? throw new McpError(spot.Kind.Path + " is not in the level.");
                    Composite target = spot.Into ?? into;
                    if (!names.TryGetValue(target, out HashSet<string> taken))
                        names[target] = taken = new HashSet<string>(target.functions.Select(o => SafeName(commands, target, o)), StringComparer.OrdinalIgnoreCase);
                    string prefix = options.NamePrefix.TryGetValue(spot.Kind.Key, out string p) && !string.IsNullOrWhiteSpace(p) ? p.Trim() : spot.Kind.NamePrefix;
                    int number = next.TryGetValue((target, spot.Kind), out int n) ? n : 1;
                    while (taken.Contains(prefix + "_" + number)) number++;
                    next[(target, spot.Kind)] = number + 1;
                    string name = prefix + "_" + number;
                    taken.Add(name);
                    FunctionEntity instance = edit.AddInstance(target, kind, name);
                    cTransform local = InstanceTransform.ToLocal(spot.Into != null ? spot.Frame : frame, new cTransform(spot.Position, new Vector3(0, spot.Yaw, 0)));
                    edit.SetParameter(target, instance, "position", new JObject()
                    {
                        ["position"] = new JArray(Math.Round(local.position.X, 4), Math.Round(local.position.Y, 4), Math.Round(local.position.Z, 4)),
                        ["rotation"] = new JArray(Math.Round(local.rotation.X, 3), Math.Round(local.rotation.Y, 3), Math.Round(local.rotation.Z, 3)),
                    });
                    edit.PlaceNode(instance, options.Page, null);
                    placed.Instances.Add(instance);
                    placed.Names.Add(target == into ? name : EditorUtils.GetCompositeName(target) + " / " + name);
                    if (target == focus) edit.Select.Add(instance);
                }
                if (edit.Select.Count > 500) edit.Select.RemoveRange(500, edit.Select.Count - 500);
            }, new McpScriptEdit.RunOptions() { Show = show, LabelPrefix = labelPrefix ?? "", AlsoTouches = touched.Where(o => o != focus).ToList() });
            placed.Notes.AddRange(outcome.Notes);
            int traps = use.Count(o => o.Kind == CorpseTrap);
            if (traps != 0)
                placed.Notes.Add(traps + " corpse trap(s) went in their androids' composites: " + string.Join(", ", use.Where(o => o.Kind == CorpseTrap && o.Into != null).Select(o => EditorUtils.GetCompositeName(o.Into)).Distinct()) + ".");
            return placed;
        }

        private static string SafeName(Commands commands, Composite composite, Entity entity)
        {
            try { return McpScript.EntityName(commands, composite, entity); } catch { return ""; }
        }
        #endregion
    }
}
