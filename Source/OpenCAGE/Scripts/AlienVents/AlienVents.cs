using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using CathodeLib.NavMesh;
using CATHODE.Enums;
using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;

namespace OpenCAGE
{
    /// <summary>One of retail's alien vent composites: a PathfindingAlienBackstageNode with the alien's clips into and out of a ceiling vent.</summary>
    internal sealed class VentKind
    {
        /// <summary>Short key: "4m", "6m", "9m".</summary>
        public string Key;
        public string Label;
        /// <summary>The composite's path, the same in every retail level that has it.</summary>
        public string Path;
        public ShortGuid Id;
        /// <summary>How far above the composite's origin (the vent, TopMarker) its node stands: the floor.</summary>
        public float Height;
        /// <summary>The floor-to-ceiling heights this kind is picked for (auto).</summary>
        public float MinCeiling, MaxCeiling;
        /// <summary>The clips: up into the vent, down out of it.</summary>
        public string Enter, Exit;
        public override string ToString() => Label;
    }

    /// <summary>A vent the level places: its node, where it stands, which backstage network it is in.</summary>
    internal sealed class PlacedVent
    {
        /// <summary>The PathfindingAlienBackstageNode, and the instances from an entry point down to it.</summary>
        public FunctionEntity Node;
        public List<Entity> Chain;
        /// <summary>The composite holding the node (the vent composite), and the vent instance with the composite holding it.</summary>
        public Composite NodeComposite;
        public FunctionEntity Instance;
        public Composite Holder;
        /// <summary>Null for a vent composite that is not one of retail's three.</summary>
        public VentKind Kind;
        /// <summary>The node's world position: the floor the alien leaves from and lands on.</summary>
        public Vector3 Bottom;
        public int Network;
        /// <summary>The user made or changed the composite that places it (retail's own vents play as shipped).</summary>
        public bool User;
        /// <summary>"composite / instance" for messages.</summary>
        public string Where;
        /// <summary>A Backstage link in the saved navmesh starts at it (null: not looked).</summary>
        public bool? Baked;
        /// <summary>Its bottom meets the ground navmesh, so the alien can walk to it (null: not looked, or not baked).</summary>
        public bool? OnNavmesh;
    }

    /// <summary>What the alien vents helper offers for one spot.</summary>
    internal sealed class VentSpot
    {
        /// <summary>On the navmesh: where the node goes (the alien leaves from and lands here).</summary>
        public Vector3 Floor;
        /// <summary>Floor to ceiling (m).</summary>
        public float Ceiling;
        public VentKind Kind;
        /// <summary>Degrees about +Y the vent faces (the most open way out).</summary>
        public float Yaw;
        /// <summary>How far is clear that way (m, up to 12).</summary>
        public float OpenFor;
        public int Island;
        public bool Use = true;
        public Vector3 Top => Floor + new Vector3(0, Kind?.Height ?? 6f, 0);
    }

    /// <summary>The helper's settings.</summary>
    internal sealed class VentOptions
    {
        /// <summary>How many vents to add (0: as many as the walkable area calls for, 3-12 with the level's own).</summary>
        public int Count;
        /// <summary>The least distance between two vents, new or old (m).</summary>
        public float Spacing = 8f;
        /// <summary>One kind for every vent, or null to pick each by the ceiling over it.</summary>
        public VentKind Kind;
        /// <summary>Leave the vents' kill traps on (retail's default: the alien can drag a player standing under a vent up into it).</summary>
        public bool Killtraps;
        /// <summary>The backstage network (Network_ID): vents only join up with their own network's.</summary>
        public int Network;
        /// <summary>The composite the vents go in (null: the root).</summary>
        public Composite Into;
        /// <summary>Only spots inside this box (world), or anywhere on the navmesh.</summary>
        public Vector3? RegionMin, RegionMax;
        /// <summary>Instance names: prefix + number.</summary>
        public string NamePrefix = "AlienVent";
        /// <summary>The flowgraph page the instances' nodes go on.</summary>
        public string Page = "AlienVents";
        public int Seed = 1;
    }

    /// <summary>What <see cref="AlienVents.Propose"/> found.</summary>
    internal sealed class VentProposal
    {
        public List<VentSpot> Spots = new List<VentSpot>();
        /// <summary>Spots that passed every test (before picking).</summary>
        public int Candidates;
        public int Samples;
        /// <summary>Why the other samples were turned down, with how many.</summary>
        public Dictionary<string, int> Rejected = new Dictionary<string, int>();
        public double WalkableArea;
        public int Wanted;
        /// <summary>The level's own vents in the network (spacing kept from them; they join the sheet).</summary>
        public List<PlacedVent> Existing = new List<PlacedVent>();
        public AlienVents.Sheet Sheet;
        public List<string> Notes = new List<string>();
        public bool NavmeshStale;
    }

    /// <summary>What placing vents did.</summary>
    internal sealed class VentsPlaced
    {
        public List<FunctionEntity> Instances = new List<FunctionEntity>();
        public List<string> Names = new List<string>();
        public int Ported;
        public List<string> PortedFrom = new List<string>();
        public Composite Into;
        public List<string> Notes = new List<string>();
    }

    /// <summary>
    /// Finds, places and checks alien vents (retail's Archetypes\Jobs\VentTraversalMarker composites, 4.25 m, 6M and 9m). With no
    /// PathfindingAlienBackstageNode the game crashes the first time the alien retreats (<see cref="AlienBackstageCheck"/>); the navmesh
    /// baker triangulates a backstage sheet 6 m over each network's nodes (3 or more, not in a line) and links each node up to it.
    /// UI thread unless said.
    /// </summary>
    internal static partial class AlienVents
    {
        public static readonly VentKind Vent4 = new VentKind()
        {
            Key = "4m", Label = "4.25 m (VentTraversalMarker)", Path = @"Archetypes\Jobs\VentTraversalMarker", Id = new ShortGuid("98-62-21-CD"), Height = 4.25f,
            MinCeiling = 3.2f, MaxCeiling = 5.1f, Enter = "enter_ceiling_vent_3m", Exit = "exit_ceiling_vent_6m_fast",
        };
        public static readonly VentKind Vent6 = new VentKind()
        {
            Key = "6m", Label = "6 m (VentTraversalMarker6M)", Path = @"Archetypes\Jobs\VentTraversalMarker6M", Id = new ShortGuid("C2-45-FE-F7"), Height = 6f,
            MinCeiling = 5.1f, MaxCeiling = 7.6f, Enter = "enter_ceiling_vent_6m", Exit = "exit_ceiling_vent_6m_fast",
        };
        public static readonly VentKind Vent9 = new VentKind()
        {
            Key = "9m", Label = "9 m (VentTraversalMarker9m)", Path = @"Archetypes\Jobs\VentTraversalMarker9m", Id = new ShortGuid("8E-63-64-AB"), Height = 9f,
            MinCeiling = 7.6f, MaxCeiling = 12f, Enter = "enter_ceiling_vent_9m", Exit = "exit_ceiling_vent_9m",
        };
        public static readonly VentKind[] Kinds = { Vent4, Vent6, Vent9 };

        /// <summary>Lowest and highest ceilings a vent goes under.</summary>
        public const float LowestCeiling = 3.2f, HighestCeiling = 12f;
        /// <summary>The breakout sends the alien backstage to a vent more than this far from where it went in.</summary>
        public const float BreakoutDistance = 10f;

        /* Retail levels with each kind, the first a level that has all three (one load brings any of them). */
        private static readonly (string Level, VentKind[] Has)[] Sources =
        {
            ("PRODUCTION/TECH_HUB", new[] { Vent4, Vent6, Vent9 }),
            ("PRODUCTION/SCI_HUB", new[] { Vent6 }),
            ("PRODUCTION/SCI_HOSPITALLOWER", new[] { Vent4, Vent6 }),
            ("PRODUCTION/ENG_ALIEN_NEST", new[] { Vent4, Vent9 }),
            ("PRODUCTION/TECH_RND", new[] { Vent4, Vent6 }),
            ("PRODUCTION/SCI_ANDROIDLAB", new[] { Vent4, Vent6 }),
            ("PRODUCTION/HAB_SHOPPINGCENTRE", new[] { Vent6 }),
        };

        private static readonly ShortGuid NetworkId = ShortGuidUtils.Generate("network_id");
        private static readonly ShortGuid NetworkPin = ShortGuidUtils.Generate("Network_ID");
        private static readonly ShortGuid BuildIntoNavmesh = ShortGuidUtils.Generate("build_into_navmesh");
        private const int AlienClass = (int)NAVIGATION_CHARACTER_CLASS_COMBINATION.ALIEN;
        private const uint BackstageMarkup = 1u << (int)NavigationMesh.NavMeshAreaType.Backstage;

        /// <summary>A kind by key ("4m", "6m", "9m", "4.25"), composite path or leaf name; null for "auto" or nothing.</summary>
        public static VentKind FindKind(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string t = text.Trim().ToLowerInvariant().Replace(" ", "");
            if (t == "auto") return null;
            foreach (VentKind kind in Kinds)
            {
                if (t == kind.Key || t == kind.Key.TrimEnd('m') || t == kind.Height.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ||
                    t == kind.Path.ToLowerInvariant() || t == kind.Path.Substring(kind.Path.LastIndexOf('\\') + 1).ToLowerInvariant())
                    return kind;
            }
            if (t == "4" || t == "4.25m" || t == "ventmarker") return Vent4;
            throw new McpError("'" + text + "' is not a vent kind: auto, 4m (VentTraversalMarker, 4.25 m), 6m (VentTraversalMarker6M) or 9m (VentTraversalMarker9m).");
        }

        /// <summary>The kind for a floor-to-ceiling height (null: no kind fits).</summary>
        public static VentKind ForCeiling(float height) => Kinds.FirstOrDefault(o => height >= o.MinCeiling && height < o.MaxCeiling);

        /// <summary>Whether a forced kind may go under a ceiling: up to 1.75 m above the ceiling (its clip goes through it, hidden backstage) or 3 m below.</summary>
        public static bool Fits(VentKind kind, float height) => height >= Math.Max(LowestCeiling, kind.Height - 1.75f) && height <= kind.Height + 3f;

        public static VentKind KindOf(Composite composite) => composite == null ? null : Kinds.FirstOrDefault(o => o.Id == composite.shortGUID);

        #region Survey
        /// <summary>
        /// The vents the level places (every PathfindingAlienBackstageNode the game makes, from the entry points down), with their networks. Nodes
        /// whose build_into_navmesh is off are left out (the baker does). <paramref name="users"/> says which composites are the user's (null: every
        /// composite OpenCAGE's edit history marks). Any thread; the script is only read.
        /// </summary>
        public static List<PlacedVent> Survey(Commands commands, Func<Composite, bool> users = null)
        {
            List<PlacedVent> vents = new List<PlacedVent>();
            if (commands?.Entries == null || commands.EntryPoints == null || commands.EntryPoints.Length == 0 || commands.EntryPoints[0] == null)
                return vents;
            if (users == null)
            {
                HashSet<ShortGuid> modified = new HashSet<ShortGuid>();
                try
                {
                    foreach (CompositeModificationInfoTable.ModificationInfo info in commands.Utils?.AllModificationInfo ?? new List<CompositeModificationInfoTable.ModificationInfo>())
                        if (info != null) modified.Add(info.composite_id);
                }
                catch { }
                users = o => o != null && (LevelCheck.LookAtRetail || modified.Contains(o.shortGUID));
            }

            //Only into composites that hold a node, themselves or below
            Dictionary<Composite, bool> holds = new Dictionary<Composite, bool>();
            bool Holds(Composite composite)
            {
                if (composite == null) return false;
                if (holds.TryGetValue(composite, out bool known)) return known;
                holds[composite] = false;
                bool result = composite.functions.Any(IsNode);
                if (!result)
                    foreach (FunctionEntity function in composite.functions)
                        if (!function.function.IsFunctionType && Holds(commands.GetComposite(function.function))) { result = true; break; }
                holds[composite] = result;
                return result;
            }

            HashSet<Composite> started = new HashSet<Composite>();
            foreach (Composite entry in commands.EntryPoints)
            {
                if (entry == null || !started.Add(entry)) continue;
                List<McpPlacements.Placement> found = new List<McpPlacements.Placement>();
                McpPlacements walker = McpRegion.Walker(commands);
                walker.Walk(entry, step =>
                {
                    if (step.Entity is FunctionEntity function && IsNode(function))
                        found.Add(McpPlacements.Placement.Of(step));
                    return true;
                }, Holds);
                foreach (McpPlacements.Placement placement in found)
                {
                    if (!placement.Real || placement.World == null) continue;
                    FunctionEntity node = (FunctionEntity)placement.Chain[placement.Chain.Count - 1];
                    if ((node.GetParameter(BuildIntoNavmesh)?.content as cBool)?.value == false) continue;
                    List<Composite> along = McpScript.CompositesAlong(commands, entry, placement.Chain);
                    PlacedVent vent = new PlacedVent()
                    {
                        Node = node,
                        Chain = placement.Chain,
                        NodeComposite = along.Count >= placement.Chain.Count ? along[placement.Chain.Count - 1] : null,
                        Bottom = placement.World.position,
                    };
                    if (placement.Chain.Count >= 2 && placement.Chain[placement.Chain.Count - 2] is FunctionEntity instance)
                    {
                        vent.Instance = instance;
                        vent.Holder = along[placement.Chain.Count - 2];
                    }
                    vent.Kind = KindOf(vent.NodeComposite);
                    vent.Network = NetworkOf(vent);
                    vent.User = users(vent.Holder) || (vent.Kind == null && users(vent.NodeComposite));
                    Entity named = (Entity)vent.Instance ?? node;
                    Composite namedIn = vent.Instance != null ? vent.Holder : vent.NodeComposite;
                    string name;
                    try { name = McpScript.EntityName(commands, namedIn, named); } catch { name = named.shortGUID.ToByteString(); }
                    vent.Where = (namedIn != null ? EditorUtils.GetCompositeName(namedIn) : "?") + " / " + name;
                    vents.Add(vent);
                }
            }
            return vents;
        }

        private static bool IsNode(FunctionEntity function) => function.function.IsFunctionType && function.function.AsFunctionType == FunctionType.PathfindingAlienBackstageNode;

        /* The node's network_id: its own, or (retail's vents) the vent composite's Network_ID pin it is linked to - the instance's value for
           the pin, else the pin's default. */
        private static int NetworkOf(PlacedVent vent)
        {
            Parameter own = vent.Node.GetParameter(NetworkId);
            foreach (EntityConnector link in vent.Node.childLinks)
            {
                if (link.thisParamID != NetworkId || vent.NodeComposite == null) continue;
                VariableEntity pin = vent.NodeComposite.variables.FirstOrDefault(o => o.shortGUID == link.linkedEntityID);
                if (pin == null) continue;
                int? value = IntOf(vent.Instance?.GetParameter(pin.name)?.content);
                if (value == null) value = IntOf(pin.GetParameter(pin.name)?.content);
                if (value != null) return value.Value;
            }
            return IntOf(own?.content) ?? 0;
        }

        private static int? IntOf(ParameterData data)
        {
            if (data is cInteger i) return i.value;
            if (data is cFloat f) return (int)f.value;
            return null;
        }
        #endregion

        #region The backstage sheet
        /// <summary>The sheet the navmesh baker would triangulate over a network's vents.</summary>
        public sealed class Sheet
        {
            public int Network;
            public int Vents;
            public int Triangles;
            public bool Colinear;
            /// <summary>The farthest apart two of its vents are (m).</summary>
            public float Spread;
            /// <summary>Why there is no sheet, or what is odd about it (the baker's own words).</summary>
            public string Warning;
            public bool Builds => Triangles > 0;
        }

        /// <summary>The sheet over these vent bottoms, as NavMeshBaker.BuildBackstageSheets makes it (node + 6 m, the same refinement).</summary>
        public static Sheet SheetFor(int network, IList<Vector3> bottoms)
        {
            NavMeshBakeSettings settings = NavMeshBakeSettings.CreateDefault();
            List<Vector3> tops = bottoms.Select(o => o + new Vector3(0, settings.BackstageNodeHeight, 0)).ToList();
            Sheet sheet = new Sheet() { Network = network, Vents = bottoms.Count };
            for (int i = 0; i < tops.Count; i++)
                for (int j = i + 1; j < tops.Count; j++)
                    sheet.Spread = Math.Max(sheet.Spread, Vector3.Distance(tops[i], tops[j]));
            BackstageMeshBuilder.Result result = BackstageMeshBuilder.Build(tops, settings.BackstageMaxEdgeLength, settings.BackstageColinearStripHalfWidth, out string warning);
            sheet.Warning = warning ?? result?.Warning;
            sheet.Triangles = result == null ? 0 : result.Triangles.Count / 3;
            sheet.Colinear = sheet.Warning != null && sheet.Warning.IndexOf("colinear", StringComparison.OrdinalIgnoreCase) >= 0;
            return sheet;
        }
        #endregion

        #region The saved navmesh
        /// <summary>What the level's saved navmesh (STATE_0, as the last Save &amp; Build wrote it) has of the backstage.</summary>
        public sealed class Baked
        {
            public bool HasNavmesh;
            public int SheetPolygons;
            public List<Vector3> LinkBottoms = new List<Vector3>();
        }

        private static readonly object _queryLock = new object();
        private static NavigationMesh _queryFor;
        private static NavigationMeshQuery _query;
        private static bool _hooked;

        /// <summary>A query over the saved navmesh (cached until it changes), or null when there is none. Any thread.</summary>
        public static NavigationMeshQuery QueryOf(Level level, out NavigationMesh mesh)
        {
            mesh = level?.StateResources != null && level.StateResources.Count != 0 ? level.StateResources[0]?.NavMesh : null;
            if (mesh?.Polygons == null || mesh.Polygons.Length == 0 || mesh.Vertices == null)
                return null;
            lock (_queryLock)
            {
                //A closing level must not stay alive through the cache
                if (!_hooked) { _hooked = true; Singleton.OnLevelClosing += _ => DropCaches(); }
                if (_queryFor == mesh && _query != null) return _query;
                try { _query = new NavigationMeshQuery(mesh); }
                catch (InvalidOperationException) { _query = null; }
                _queryFor = mesh;
                return _query;
            }
        }

        /// <summary>Forget the cached query (the level closes or is rebuilt).</summary>
        public static void DropCaches()
        {
            lock (_queryLock) { _query = null; _queryFor = null; }
        }

        public static bool IsSheet(NavigationMesh mesh, int polygon) =>
            polygon >= 0 && polygon < mesh.Polygons.Length && ((uint)mesh.Polygons[polygon].area.GetMarkupFlags() & BackstageMarkup) != 0;

        /// <summary>The backstage in the saved navmesh: its sheet polygons and where its Backstage links start.</summary>
        public static Baked BakedOf(Level level)
        {
            Baked baked = new Baked();
            NavigationMeshQuery query = QueryOf(level, out NavigationMesh mesh);
            if (query == null) return baked;
            baked.HasNavmesh = true;
            for (int i = 0; i < mesh.Polygons.Length; i++)
            {
                NavigationMesh.dt_area_t area = mesh.Polygons[i].area;
                if (area.GetPolyType() == NavigationMesh.dtPolyTypes.DT_POLYTYPE_GROUND && ((uint)area.GetMarkupFlags() & BackstageMarkup) != 0)
                    baked.SheetPolygons++;
            }
            foreach (NavigationMesh.dtOffMeshConnection con in mesh.OffMeshConnections ?? new NavigationMesh.dtOffMeshConnection[0])
            {
                if (con.pos == null || con.poly_index_within_tile >= mesh.Polygons.Length) continue;
                if (mesh.Polygons[con.poly_index_within_tile].area.GetLinkType() != NavigationMesh.OffMeshLinkType.Backstage) continue;
                baked.LinkBottoms.Add(new Vector3(con.pos[0], con.pos[1], con.pos[2]));
            }
            return baked;
        }

        /// <summary>
        /// Mark each vent baked (a Backstage link starts at its node) and on the navmesh (that node meets a ground polygon the alien walks: a
        /// link that starts off it joins nothing). Returns what the navmesh has.
        /// </summary>
        public static Baked MarkBaked(Level level, IList<PlacedVent> vents)
        {
            Baked baked = BakedOf(level);
            if (!baked.HasNavmesh) return baked;
            NavigationMeshQuery query = QueryOf(level, out NavigationMesh mesh);
            NavigationMeshQuery.Options options = new NavigationMeshQuery.Options() { Classes = AlienClass, IncludeDisabled = true, Extents = new Vector3(0.6f, 1.0f, 0.6f) };
            foreach (PlacedVent vent in vents)
            {
                vent.Baked = baked.LinkBottoms.Any(o => Flat(o - vent.Bottom) < 0.2f && Math.Abs(o.Y - vent.Bottom.Y) < 0.6f);
                vent.OnNavmesh = null;
                if (vent.Baked != true) continue;
                NavigationMeshQuery.Nearest nearest = query.FindNearest(vent.Bottom, options);
                vent.OnNavmesh = nearest.Found && !IsSheet(mesh, nearest.Polygon) && Flat(nearest.Point - vent.Bottom) <= 0.5f && Math.Abs(nearest.Point.Y - vent.Bottom.Y) <= 0.75f;
            }
            return baked;
        }

        private static float Flat(Vector3 d) => (float)Math.Sqrt(d.X * d.X + d.Z * d.Z);
        #endregion

        #region Proposing spots
        private static McpTool _guiTool;

        /// <summary>A call for the MCP layer's collision readers when the editor (not a tool) asks.</summary>
        public static McpCall EditorCall(Action<string> progress = null)
        {
            if (_guiTool == null) _guiTool = new McpTool() { Name = "alien_vents_window", Title = "Alien Vents" };
            return new McpCall(_guiTool, new JObject(), CancellationToken.None, (text, done, total) => { try { progress?.Invoke(text); } catch { } });
        }

        /// <summary>
        /// Spots for new vents in the open level: samples of the saved navmesh (the alien's, its backstage sheet left out) with a ceiling over
        /// them that a vent kind fits, a clear column up to it, flat floor and no wall within 1 m; the farthest apart picked, <see
        /// cref="VentOptions.Spacing"/> from each other and from the level's own vents. Reads the open level's collision (unsaved edits
        /// included) and the navmesh as last built. UI thread or a tool's thread.
        /// </summary>
        public static VentProposal Propose(McpCall call, LevelContent content, VentOptions options)
        {
            Level level = content.Level;
            VentProposal proposal = new VentProposal();
            NavigationMeshQuery query = QueryOf(level, out NavigationMesh mesh);
            if (query == null)
                throw new McpError("The level has no navmesh to put vents on (or an empty one). Save & Build with the navmesh baker first, then add vents.");
            proposal.NavmeshStale = McpEditor.UI(() => BuildTracker.NeedsBuild);
            if (proposal.NavmeshStale)
                proposal.Notes.Add("The navmesh is older than the level's last changes (a Save & Build is due): spots are found on the navmesh as last built.");

            List<PlacedVent> existing = McpEditor.UI(() => Survey(level.Commands));
            proposal.Existing = existing.Where(o => o.Network == options.Network).ToList();

            NavigationMeshQuery.Options alien = new NavigationMeshQuery.Options() { Classes = AlienClass };
            //The alien's walkable islands, by area: spots go on the big ones (not crate tops)
            Dictionary<int, double> islandArea = new Dictionary<int, double>();
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            for (int i = 0; i < mesh.Polygons.Length; i++)
            {
                NavigationMesh.dtPoly poly = mesh.Polygons[i];
                if (poly.area.GetPolyType() != NavigationMesh.dtPolyTypes.DT_POLYTYPE_GROUND || IsSheet(mesh, i)) continue;
                if (((int)poly.area.GetAdmittanceFlags() & AlienClass) == 0 || !poly.area.GetIsEnabled()) continue;
                double area = 0;
                Vector3 a = mesh.Vertices[poly.verts[0]];
                for (int v = 1; v + 1 < poly.vertCount; v++)
                {
                    Vector3 b = mesh.Vertices[poly.verts[v]], c = mesh.Vertices[poly.verts[v + 1]];
                    area += Vector3.Cross(b - a, c - a).Length() * 0.5;
                }
                for (int v = 0; v < poly.vertCount; v++)
                {
                    min = Vector3.Min(min, mesh.Vertices[poly.verts[v]]);
                    max = Vector3.Max(max, mesh.Vertices[poly.verts[v]]);
                }
                int island = query.IslandOf(i, alien);
                islandArea[island] = (islandArea.TryGetValue(island, out double had) ? had : 0) + area;
            }
            if (islandArea.Count == 0)
                throw new McpError("The navmesh has no ground the alien may walk on.");
            if (options.RegionMin != null && options.RegionMax != null)
            {
                min = Vector3.Max(min, Vector3.Min(options.RegionMin.Value, options.RegionMax.Value));
                max = Vector3.Min(max, Vector3.Max(options.RegionMin.Value, options.RegionMax.Value));
                if (min.X > max.X || min.Z > max.Z)
                    throw new McpError("The region holds none of the navmesh.");
            }
            double biggest = islandArea.Values.Max();
            HashSet<int> islands = new HashSet<int>(islandArea.Where(o => o.Value >= Math.Min(150.0, biggest * 0.5) || o.Value >= biggest * 0.15).Select(o => o.Key));
            proposal.WalkableArea = Math.Round(query.Area(min - new Vector3(0, 1, 0), max + new Vector3(0, 1, 0), alien), 1);

            int target = options.Count > 0 ? options.Count : (int)Math.Max(3, Math.Min(12, Math.Round(proposal.WalkableArea / 250.0)));
            proposal.Wanted = options.Count > 0 ? options.Count : Math.Max(Math.Max(0, 3 - proposal.Existing.Count), target - proposal.Existing.Count);
            if (proposal.Wanted == 0)
            {
                proposal.Notes.Add("Network " + options.Network + " already has " + proposal.Existing.Count + " vents, as many as " + proposal.WalkableArea + " m² of walkable floor calls for: give a count to add more.");
                proposal.Sheet = SheetFor(options.Network, proposal.Existing.Select(o => o.Bottom).ToList());
                return proposal;
            }

            int samples = (int)Math.Max(400, Math.Min(5000, proposal.WalkableArea / 1.2));
            List<Vector3> points = query.SamplePoints(samples, min - new Vector3(0, 1, 0), max + new Vector3(0, 1, 0), alien, options.Seed);
            proposal.Samples = points.Count;
            McpCollision.Soup soup = McpCollision.SoupFor(call, McpRegion.Box(min - new Vector3(3, 1, 3), max + new Vector3(3, HighestCeiling + 2, 3), "round the navmesh"), "solid");

            List<VentSpot> candidates = new List<VentSpot>();
            void Reject(string why) => proposal.Rejected[why] = (proposal.Rejected.TryGetValue(why, out int n) ? n : 0) + 1;
            foreach (Vector3 point in points)
            {
                call?.ThrowIfCancelled();
                NavigationMeshQuery.Nearest at = query.FindNearest(point, new NavigationMeshQuery.Options() { Classes = AlienClass, Extents = new Vector3(0.3f, 0.5f, 0.3f) });
                if (!at.Found || IsSheet(mesh, at.Polygon)) { Reject("not on the alien's ground navmesh"); continue; }
                if (!islands.Contains(at.Island)) { Reject("on a small island of navmesh (a crate or ledge top)"); continue; }
                if (at.Height != NavigationMesh.AreaHeight.Standing) { Reject("crouch-height floor"); continue; }
                Vector3 floor = at.Point;
                VentSpot spot = Test(soup, floor, options.Kind, Reject, (min + max) * 0.5f);
                if (spot == null) continue;
                spot.Island = at.Island;
                candidates.Add(spot);
            }
            proposal.Candidates = candidates.Count;

            //Farthest first: start at the candidate farthest from the middle, then always the one farthest from every vent so far (every
            //network's: two vents on one spot would fight over the alien)
            List<Vector3> taken = existing.Select(o => o.Bottom).ToList();
            Vector3 middle = candidates.Count != 0 ? candidates.Aggregate(Vector3.Zero, (s, o) => s + o.Floor) / candidates.Count : Vector3.Zero;
            while (proposal.Spots.Count < proposal.Wanted && candidates.Count != 0)
            {
                VentSpot best = null;
                float bestScore = float.MinValue;
                foreach (VentSpot c in candidates)
                {
                    float score = taken.Count == 0 ? Vector3.Distance(c.Floor, middle) : taken.Min(o => Vector3.Distance(o, c.Floor));
                    //A little for room to land in, so a tie goes to the open spot
                    score += Math.Min(c.OpenFor, 6f) * 0.05f;
                    if (score > bestScore) { bestScore = score; best = c; }
                }
                if (best == null || (taken.Count != 0 && taken.Min(o => Vector3.Distance(o, best.Floor)) < options.Spacing))
                    break;
                proposal.Spots.Add(best);
                taken.Add(best.Floor);
                candidates.Remove(best);
            }
            if (proposal.Spots.Count < proposal.Wanted)
                proposal.Notes.Add("Found " + proposal.Spots.Count + " of the " + proposal.Wanted + " vents asked for: no other spot is " + options.Spacing + " m from every vent (lower 'spacing', or widen the region).");
            proposal.Sheet = SheetFor(options.Network, proposal.Existing.Select(o => o.Bottom).Concat(proposal.Spots.Select(o => o.Floor)).ToList());
            if (!proposal.Sheet.Builds)
                proposal.Notes.Add("With these the network still makes no backstage sheet (" + proposal.Sheet.Warning + "): the alien needs 3 or more vents, not in a line, in one network.");
            else if (proposal.Sheet.Spread < BreakoutDistance)
                proposal.Notes.Add("Every vent is within " + BreakoutDistance + " m of the others: the alien's breakout moves backstage to a vent more than " + BreakoutDistance + " m away.");
            return proposal;
        }

        /// <summary>
        /// Whether a vent can go over a floor point, and how (null, with the reason, when not): a ceiling over it a kind fits (or the given
        /// kind), facing down, the same height a little way round (not its edge, no beam or pipe in the column), flat floor round it and no
        /// wall within 1 m. Faces the most open way.
        /// </summary>
        public static VentSpot Test(McpCollision.Soup soup, Vector3 floor, VentKind forced, Action<string> reject, Vector3? toward = null)
        {
            Vector3 up = new Vector3(0, 1, 0);
            if (!soup.CeilingOver(floor + up * 0.3f, out McpCollision.Hit ceiling, HighestCeiling + 1f)) { reject("no ceiling over it"); return null; }
            float height = ceiling.Point.Y - floor.Y;
            if (ceiling.Normal.Y > -0.6f) { reject("the ceiling over it slopes or is a wall"); return null; }
            VentKind kind = forced ?? ForCeiling(height);
            if (forced != null && !Fits(forced, height)) kind = null;
            if (kind == null)
            {
                if (height < LowestCeiling) reject("ceiling under " + LowestCeiling + " m");
                else if (height >= HighestCeiling) reject("ceiling over " + HighestCeiling + " m");
                else reject("ceiling too far from the " + forced.Key + " vent's height");
                return null;
            }
            //A column 0.7 m round: the same ceiling (not its edge, nothing hanging in the way)
            for (int i = 0; i < 6; i++)
            {
                double angle = i * Math.PI / 3;
                Vector3 ring = floor + new Vector3((float)Math.Sin(angle) * 0.7f, 0.3f, (float)Math.Cos(angle) * 0.7f);
                if (!soup.CeilingOver(ring, out McpCollision.Hit over, HighestCeiling + 1f)) { reject("at the edge of a ceiling"); return null; }
                float h = over.Point.Y - floor.Y;
                if (h < height - 0.75f) { reject("something hangs in the way up (beam, pipe, stairs)"); return null; }
                if (h > height + 1.5f) { reject("at the edge of a ceiling"); return null; }
            }
            //Flat floor round it (the alien lands here) and no wall within 1 m
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4;
                Vector3 dir = new Vector3((float)Math.Sin(angle), 0, (float)Math.Cos(angle));
                if (!soup.FloorUnder(floor + dir + up * 0.5f, out McpCollision.Hit under, 0.5f, 2f) || Math.Abs(under.Point.Y - floor.Y) > 0.35f) { reject("by a drop or a step"); return null; }
                if (!soup.Clear(floor + up * 0.6f, floor + up * 0.6f + dir, out _) || !soup.Clear(floor + up * 1.6f, floor + up * 1.6f + dir, out _)) { reject("within 1 m of a wall or prop"); return null; }
            }
            //Facing: the most open way at chest height (of ways about as open, the one nearest 'toward': the middle of the area)
            float bestOpen = -1, bestScore = float.MinValue, yaw = 0;
            Vector3 aim = toward == null ? Vector3.Zero : new Vector3(toward.Value.X - floor.X, 0, toward.Value.Z - floor.Z);
            if (aim.LengthSquared() > 1e-4f) aim = Vector3.Normalize(aim);
            for (int i = 0; i < 16; i++)
            {
                double angle = i * Math.PI / 8;
                Vector3 dir = new Vector3((float)Math.Sin(angle), 0, (float)Math.Cos(angle));
                float open = soup.Cast(floor + up * 1.2f, dir, 12f, out McpCollision.Hit wall) ? wall.Distance : 12f;
                float score = open + Vector3.Dot(dir, aim) * 1.5f;
                if (score > bestScore + 0.01f) { bestScore = score; bestOpen = open; yaw = (float)(angle * 180 / Math.PI); }
            }
            if (yaw > 180) yaw -= 360;
            return new VentSpot() { Floor = floor, Ceiling = height, Kind = kind, Yaw = (float)Math.Round(yaw, 1), OpenFor = bestOpen };
        }

        /// <summary>A spot at a point the user picked: snapped onto the alien's navmesh (within 1.5 m), then tested as <see cref="Test"/> does.</summary>
        public static VentSpot SpotAt(McpCall call, LevelContent content, Vector3 point, VentKind forced, out string why)
        {
            why = null;
            NavigationMeshQuery query = QueryOf(content.Level, out NavigationMesh mesh);
            if (query == null) { why = "the level has no navmesh (Save & Build first)"; return null; }
            NavigationMeshQuery.Nearest at = query.FindNearest(point, new NavigationMeshQuery.Options() { Classes = AlienClass, Extents = new Vector3(1.5f, 2f, 1.5f) });
            if (!at.Found || IsSheet(mesh, at.Polygon)) { why = "not within 1.5 m of the alien's ground navmesh"; return null; }
            McpCollision.Soup soup = McpCollision.SoupFor(call, McpRegion.Box(at.Point - new Vector3(15, 2, 15), at.Point + new Vector3(15, HighestCeiling + 2, 15), "round " + McpRegion.Format(at.Point)), "solid");
            string reason = null;
            VentSpot spot = Test(soup, at.Point, forced, o => reason = o);
            if (spot == null) { why = reason; return null; }
            spot.Island = at.Island;
            return spot;
        }
        #endregion

        #region Bringing the composites in, placing
        /// <summary>The vent kinds the level does not have yet.</summary>
        public static List<VentKind> Missing(Commands commands, IEnumerable<VentKind> kinds) =>
            kinds.Where(o => o != null).Distinct().Where(o => commands.GetComposite(o.Id) == null).ToList();

        /// <summary>The level of this install each missing kind would come from (null: none has it).</summary>
        public static string SourceFor(VentKind kind)
        {
            foreach ((string level, VentKind[] has) in Sources)
                if (has.Contains(kind) && File.Exists(Path.Combine(Singleton.PathToAI ?? "", "DATA", "ENV", level, "WORLD", "COMMANDS.PAK")))
                    return level;
            return null;
        }

        /// <summary>
        /// Bring the vent composites the level lacks in from retail levels of this install (with their VentTraversalVFX), as port_composites
        /// does: not undoable, written at the next save. Loads each source level (seconds). Returns how many composites arrived.
        /// </summary>
        public static int EnsureKinds(LevelContent content, IEnumerable<VentKind> kinds, List<string> notes, List<string> from = null)
        {
            Level destination = content.Level;
            List<VentKind> missing = Missing(destination.Commands, kinds);
            int ported = 0;
            foreach ((string level, VentKind[] has) in Sources)
            {
                if (missing.Count == 0) break;
                List<VentKind> here = missing.Where(o => has.Contains(o)).ToList();
                if (here.Count == 0) continue;
                if (!File.Exists(Path.Combine(Singleton.PathToAI ?? "", "DATA", "ENV", level, "WORLD", "COMMANDS.PAK"))) continue;
                if (string.Equals(destination.Name, level, StringComparison.OrdinalIgnoreCase)) continue;
                Level source = CompositeImporter.LoadLevel(level);
                List<Composite> roots = here.Select(o => source.Commands.GetComposite(o.Id)).Where(o => o != null).ToList();
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
                notes.Add("Brought " + string.Join(", ", here.Select(o => o.Path)) + " in from " + level + " (" + arrived.Count + " composite(s) with what they place; not undoable, as any port).");
                source = null;
                GC.Collect();
                missing = Missing(destination.Commands, missing);
            }
            if (missing.Count != 0)
                throw new McpError("No level of this install has " + string.Join(", ", missing.Select(o => o.Path)) + " (they come from " +
                    string.Join(", ", Sources.Where(o => o.Has.Any(k => missing.Contains(k))).Select(o => o.Level)) + "). Port it from a level that has it (port_composites), or pick another kind.");
            return ported;
        }

        /// <summary>
        /// Place vents at spots as one undo step ("Add Alien Vents"), after bringing in the kinds the level lacks (not undoable). Each vent's
        /// origin sits its kind's height over the floor point, facing the spot's way, kill trap off unless asked. The navmesh takes them at the
        /// next Save &amp; Build. UI thread or a tool's.
        /// </summary>
        public static VentsPlaced Place(LevelContent content, IList<VentSpot> spots, VentOptions options, string labelPrefix, bool show)
        {
            Commands commands = content.Level.Commands;
            List<VentSpot> use = spots.Where(o => o != null && o.Use && o.Kind != null).ToList();
            if (use.Count == 0)
                throw new McpError("No spots to place vents at.");
            Composite root = commands.EntryPoints[0];
            Composite into = options.Into ?? root;
            if (!commands.Entries.Contains(into))
                throw new McpError("The composite to put the vents in is not in the level.");
            cTransform frame = into == root ? new cTransform(Vector3.Zero, Vector3.Zero) : McpSpatialTools.FrameOf(commands, into, null, "a composite placed once (or the root)", out JObject _);

            VentsPlaced placed = new VentsPlaced() { Into = into };
            placed.Ported = EnsureKinds(content, use.Select(o => o.Kind), placed.Notes, placed.PortedFrom);

            //Names: prefix_N past any the composite has
            HashSet<string> names = new HashSet<string>(into.functions.Select(o => SafeName(commands, into, o)), StringComparer.OrdinalIgnoreCase);
            int next = 1;
            string prefix = string.IsNullOrWhiteSpace(options.NamePrefix) ? "AlienVent" : options.NamePrefix.Trim();
            McpScriptEdit.Outcome outcome = McpScriptEdit.Run("Add Alien Vents (" + use.Count + ")", into, edit =>
            {
                foreach (VentSpot spot in use)
                {
                    Composite kind = commands.GetComposite(spot.Kind.Id) ?? throw new McpError(spot.Kind.Path + " is not in the level.");
                    while (names.Contains(prefix + "_" + next)) next++;
                    string name = prefix + "_" + next;
                    names.Add(name);
                    FunctionEntity instance = edit.AddInstance(into, kind, name);
                    cTransform local = InstanceTransform.ToLocal(frame, new cTransform(spot.Top, new Vector3(0, spot.Yaw, 0)));
                    edit.SetParameter(into, instance, "position", new JObject()
                    {
                        ["position"] = new JArray(Math.Round(local.position.X, 4), Math.Round(local.position.Y, 4), Math.Round(local.position.Z, 4)),
                        ["rotation"] = new JArray(Math.Round(local.rotation.X, 3), Math.Round(local.rotation.Y, 3), Math.Round(local.rotation.Z, 3)),
                    });
                    if (!options.Killtraps)
                        edit.SetParameter(into, instance, "DisableKilltrapOnReset", true);
                    if (options.Network != 0)
                        edit.SetParameter(into, instance, "Network_ID", options.Network);
                    edit.PlaceNode(instance, options.Page, null);
                    placed.Instances.Add(instance);
                    placed.Names.Add(name);
                }
                edit.Select.Clear();
                edit.Select.AddRange(placed.Instances);
            }, new McpScriptEdit.RunOptions() { Show = show, LabelPrefix = labelPrefix ?? "" });
            placed.Notes.AddRange(outcome.Notes);
            placed.Notes.Add("The navmesh takes the vents at the next Save & Build with the navmesh baker on (it bakes the alien's backstage sheet 6 m over them and a link up into each).");
            return placed;
        }

        private static string SafeName(Commands commands, Composite composite, Entity entity)
        {
            try { return McpScript.EntityName(commands, composite, entity); } catch { return ""; }
        }

        /// <summary>
        /// Find spots with the given options and place them, as one go (the Level Check fix and MCP fix_level_check): what was placed, or an
        /// error when no spot was found. UI thread.
        /// </summary>
        public static VentsPlaced AddAutomatically(McpCall call, LevelContent content, VentOptions options, string labelPrefix, out VentProposal proposal)
        {
            proposal = Propose(call, content, options);
            if (proposal.Spots.Count == 0)
                throw new McpError("No spot for a vent was found: " + Describe(proposal.Rejected) + ". Place vents by hand (Alien Vents window, add_alien_vents 'spots') where the alien can climb into a ceiling.");
            return Place(content, proposal.Spots, options, labelPrefix, show: false);
        }

        /// <summary>The rejection tally as text, biggest first.</summary>
        public static string Describe(Dictionary<string, int> rejected) =>
            rejected.Count == 0 ? "no samples" : string.Join("; ", rejected.OrderByDescending(o => o.Value).Select(o => o.Value + " " + o.Key));
        #endregion
    }
}
