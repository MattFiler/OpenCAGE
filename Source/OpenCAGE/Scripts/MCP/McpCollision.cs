using CATHODE;
using CATHODE.Enums;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using NanoRT;
using Newtonsoft.Json.Linq;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Live collision: the level's collision rebuilt from the placements as they are in the editor (unsaved edits
    /// included), for raycasts and bounds - every placed ModelReference's COLLISION_MAPPING proxy at its composed world
    /// transform, and CollisionBarrier / NavMeshBarrier / SoundBarrier boxes as they stand in game (a CollisionBarrier
    /// counts whether the save pass prebuilds it, static_collision on, or the entity makes it at run time).
    /// </summary>
    /// <remarks>
    /// <para>The saved COLLISION.HKX world hosts are rebuilt only at save, so they miss unsaved moves; this does not. It
    /// follows the save pass where it can: deleted subtrees and repeat placements of is_shared composites are left out,
    /// is_template content and collision with enable_on_reset off is ghosted, delete_standard_collision drops the world
    /// collider and both delete flags drop the lot; a proxy's storage class (world / ballistic) comes from its template
    /// row as Instancing.ProcessEntity reads it. Measured against retail's built hosts on SCI_ANDROIDLAB it agreed on 98%
    /// of down and horizontal rays to 5 cm.</para>
    /// <para>Per-proxy local meshes are cached per level (dropped when the level closes, on a save and on collision
    /// imports). Soups are cached by (level, script version, region, filter): <see cref="Version"/> moves on every script
    /// edit, undo or resource change.</para>
    /// <para>Walks run on the UI thread; transforming and the BVH build run on the calling thread.</para>
    /// </remarks>
    internal static class McpCollision
    {
        #region Versions and caches
        private static int _version;
        private static bool _hooked;
        private static readonly object _lock = new object();

        /// <summary>Moves whenever the level's script, resources or the open level change: caches keyed on it are stale once it moves.</summary>
        public static int Version => Volatile.Read(ref _version);

        private static void Bump() => Interlocked.Increment(ref _version);

        /// <summary>Whether <see cref="Hook"/> has run: until it has, <see cref="Version"/> does not follow edits.</summary>
        public static bool Hooked => _hooked;

        /// <summary>Listen for edits (once). UI thread.</summary>
        public static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            UndoStack.Current.Changed += Bump;
            Singleton.OnCompositesModified += _ => Bump();
            Singleton.OnEntityMoved += (t, e) => Bump();
            Singleton.OnEntityParameterModified += (e, p, b) => Bump();
            Singleton.OnParameterModified += Bump;
            Singleton.OnEntityDeleted += _ => Bump();
            Singleton.OnCompositeDeleted += _ => Bump();
            Singleton.OnResourceModified += () => { DropMeshes(); Bump(); };
            Singleton.OnSaved += () => { DropMeshes(); Bump(); };
            Singleton.OnLevelClosing += _ => DropAll();
            Singleton.OnLevelLoaded += _ => DropAll();
        }

        private static void DropMeshes()
        {
            lock (_lock)
            {
                _meshes.Clear();
                _modelNames = null;
                _soups.Clear();
                _bounds.Clear();
            }
        }

        private static void DropAll()
        {
            lock (_lock)
            {
                _meshes.Clear();
                _modelNames = null;
                _modelNamesFor = null;
                _soups.Clear();
                _bounds.Clear();
                _boundsFor = null;
                _boundsVersion = -1;
                _meshLevel = null;
            }
            //Every MCP cache that holds the level's script or data: none may keep a closed level alive
            McpRegion.DropCaches();
            McpNavTools.DropCaches();
            McpCharacterTools.DropNav();
            McpEditingTools.DropPositionLinked();
            McpPlacements.DropShared();
            Bump();
        }

        private static Level _meshLevel;
        private static readonly Dictionary<HavokPackfile.StaticCompoundShape, ProxyMesh> _meshes = new Dictionary<HavokPackfile.StaticCompoundShape, ProxyMesh>();
        private static Dictionary<Models.CS2.Component.LOD.Submesh, string> _modelNames;
        private static Level _modelNamesFor;
        private static readonly List<Soup> _soups = new List<Soup>();
        private const int SoupCache = 3;

        /// <summary>A collision proxy's triangles in its own space.</summary>
        public sealed class ProxyMesh
        {
            public Vector3[] Positions;
            public int[] Indices;
            public Vector3 Min, Max;
            public uint DataOffset;
            public int TriangleCount => Indices.Length / 3;
        }

        /// <summary>A proxy's local mesh, built once per level. UI thread (it reads the level's HKX).</summary>
        public static ProxyMesh Mesh(Level level, HavokPackfile.StaticCompoundShape compound)
        {
            lock (_lock)
            {
                if (_meshLevel != level)
                {
                    _meshes.Clear();
                    _meshLevel = level;
                }
                if (_meshes.TryGetValue(compound, out ProxyMesh cached) && cached.DataOffset == compound.DataOffset)
                    return cached;
            }
            HavokPackfile.PreviewMesh built = level.Collision?.BuildBakeMesh(compound);
            ProxyMesh mesh = new ProxyMesh()
            {
                Positions = built?.Positions.ToArray() ?? new Vector3[0],
                Indices = built?.Indices.ToArray() ?? new int[0],
                DataOffset = compound.DataOffset,
            };
            if (mesh.Positions.Length != 0)
            {
                Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
                foreach (Vector3 p in mesh.Positions) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
                mesh.Min = min;
                mesh.Max = max;
            }
            lock (_lock) _meshes[compound] = mesh;
            return mesh;
        }

        private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }

        /// <summary>Each submesh's model name (built once per level). Any thread once built; build on the UI thread.</summary>
        public static Dictionary<Models.CS2.Component.LOD.Submesh, string> ModelNames(Level level)
        {
            lock (_lock)
            {
                if (_modelNames != null && _modelNamesFor == level)
                    return _modelNames;
            }
            Dictionary<Models.CS2.Component.LOD.Submesh, string> names = new Dictionary<Models.CS2.Component.LOD.Submesh, string>(ReferenceComparer<Models.CS2.Component.LOD.Submesh>.Instance);
            foreach (Models.CS2 model in level.Models?.Entries ?? new List<Models.CS2>())
                foreach (Models.CS2.Component component in model.Components)
                    foreach (Models.CS2.Component.LOD lod in component.LODs)
                        foreach (Models.CS2.Component.LOD.Submesh submesh in lod.Submeshes)
                            if (submesh != null && !names.ContainsKey(submesh)) names[submesh] = model.Name;
            lock (_lock)
            {
                _modelNames = names;
                _modelNamesFor = level;
            }
            return names;
        }
        #endregion

        #region What collides
        /// <summary>The collision filters a query takes.</summary>
        public static readonly string[] Filters = { "solid", "walkable", "ballistic", "camera", "all" };

        public const string FilterHelp = "Which collision: 'solid' (default: what blocks movement and sight - world and ballistic colliders, barriers; not sound-only, ghosted or nav-only), " +
            "'walkable' (world colliders characters stand on), 'ballistic' (what bullets hit), 'camera' (solid plus camera-only barriers), 'all' (also ghosted, template, sound and nav barrier collision).";

        public static string ReadFilter(McpCall call, string name = "collision")
        {
            string filter = (call.Str(name) ?? "solid").Trim().ToLowerInvariant();
            if (!Filters.Contains(filter))
                throw new McpError("'" + name + "' is one of " + string.Join(", ", Filters) + ".");
            return filter;
        }

        /// <summary>One placed collider: an entity's proxy mesh or barrier box at a placement.</summary>
        public sealed class Record
        {
            /// <summary>Entities from the level root to the entity.</summary>
            public List<Entity> Chain;
            public Composite Composite;
            public Entity Entity;
            /// <summary>model (a ModelReference's proxy), barrier (CollisionBarrier), nav_barrier, sound_barrier.</summary>
            public string Kind;
            public bool World, Ballistic, Sound, SeeThrough, Ghosted, CameraOnly, PathClosed, Unhosted;
            public string Model;
            public string Material;
            public int Proxy = -1;
            public cTransform Transform;
            public int FirstTriangle, TriangleCount;

            public string Classes()
            {
                List<string> c = new List<string>();
                if (World) c.Add("world");
                if (Ballistic) c.Add("ballistic");
                if (Sound) c.Add("sound");
                if (SeeThrough) c.Add("see_through");
                if (Ghosted) c.Add("ghosted");
                if (CameraOnly) c.Add("camera_only");
                if (PathClosed) c.Add("nav_only");
                if (Unhosted) c.Add("unhosted");
                return string.Join(",", c);
            }

            public bool Passes(string filter)
            {
                switch (filter)
                {
                    case "all": return true;
                    case "walkable": return World && !Sound && !Ghosted && !PathClosed && !CameraOnly && !Unhosted;
                    case "ballistic": return Ballistic && !Ghosted;
                    case "camera": return (World || Ballistic) && !Sound && !Ghosted && !PathClosed && !Unhosted;
                    default: return (World || Ballistic) && !Sound && !Ghosted && !PathClosed && !CameraOnly && !Unhosted;
                }
            }
        }

        private static readonly ShortGuid HalfDimensions = ShortGuidUtils.Generate("half_dimensions");
        private static readonly ShortGuid EnableOnReset = ShortGuidUtils.Generate("enable_on_reset");
        private static readonly ShortGuid CollisionTypeParam = ShortGuidUtils.Generate("collision_type");

        /// <summary>The half extents a box entity's half_dimensions gives (the save pass's fallback when it has none).</summary>
        public static Vector3 HalfExtentsOf(Entity entity)
        {
            Vector3 half = entity.GetParameter(HalfDimensions)?.content is cVector3 v ? (Vector3)v.value : new Vector3(0.5f, 1f, 0.5f);
            return new Vector3(Math.Max(1e-4f, Math.Abs(half.X)), Math.Max(1e-4f, Math.Abs(half.Y)), Math.Max(1e-4f, Math.Abs(half.Z)));
        }

        /// <summary>
        /// A box entity's oriented box: it stands on its position (the save pass and the viewer both centre it at position +
        /// rotation * (0, half.y, 0)), spanning +-half.x and +-half.z, and 0 to 2 * half.y up, in its own rotation.
        /// </summary>
        public static Vector3 BoxCentre(cTransform world, Vector3 half) => world.position + InstanceTransform.DirectionToWorld(new cTransform(Vector3.Zero, world.rotation), new Vector3(0, half.Y, 0));

        /// <summary>Whether a function type is one of the box volumes a half_dimensions parameter sizes.</summary>
        public static bool IsBoxType(FunctionType type)
        {
            switch (type)
            {
                case FunctionType.CollisionBarrier:
                case FunctionType.NavMeshBarrier:
                case FunctionType.SoundBarrier:
                    return true;
            }
            return false;
        }
        #endregion

        #region Gathering from the script
        /// <summary>One collider to put in a soup: a proxy mesh (or a box) with its world matrix.</summary>
        public struct Placed
        {
            public ProxyMesh Mesh;
            public bool IsBox;
            public Vector3 HalfExtents;
            public Matrix4x4 Matrix;
            public Record Record;
        }

        /// <summary>A model's render bounds at a placement.</summary>
        public struct RenderBox
        {
            public Vector3 Min, Max;     // model space
            public cTransform World;
            public List<Entity> Chain;
        }

        /// <summary>A volume entity (PlayerTriggerBox, barrier, anything sized by half_dimensions) at a placement.</summary>
        public sealed class Volume
        {
            public List<Entity> Chain;
            public Composite Composite;
            public Entity Entity;
            public string Type;
            public cTransform World;
            public Vector3 HalfExtents;
        }

        /// <summary>What a walk of a region found.</summary>
        public sealed class Gathered
        {
            public List<Placed> Colliders = new List<Placed>();
            public List<RenderBox> Render = new List<RenderBox>();
            public List<Volume> Volumes = new List<Volume>();
            public int Visited, Placements, Skipped, NotReal;
            public bool Truncated;
            public List<string> Warnings = new List<string>();
        }

        /// <summary>
        /// Walk a region and collect its colliders (passing <paramref name="filter"/>, or none when null), render boxes and
        /// volumes. With <paramref name="clip"/>, only what touches the region's clip box (and the walk skips composites
        /// whose content cannot reach it). UI thread.
        /// </summary>
        public static Gathered Gather(McpCall call, Commands commands, Level level, McpRegion region, string filter, bool render, bool volumes, bool clip = true)
        {
            Hook();
            Gathered gathered = new Gathered();
            Dictionary<Models.CS2.Component.LOD.Submesh, string> modelNames = filter != null ? ModelNames(level) : null;
            bool clipping = clip && region.HasClip;

            bool Visit(McpPlacements.Step step)
            {
                if (!(step.Entity is FunctionEntity function) || !function.function.IsFunctionType)
                    return true;
                if (!step.Real && (filter != "all" || step.Deleted || step.SharedRepeat))
                {
                    gathered.NotReal++;
                    return true;
                }
                FunctionType type = function.function.AsFunctionType;
                cTransform world = step.World;
                if (world == null) return true;

                if (type == FunctionType.ModelReference)
                {
                    if (filter != null)
                        AddModelCollider(level, step, function, world, filter, region, clipping, modelNames, gathered);
                    if (render)
                    {
                        List<RenderableElements.Element> elements = function.GetResource(ResourceType.RENDERABLE_INSTANCE, true)?.RenderableInstance;
                        if (elements != null && elements.Count != 0)
                        {
                            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
                            foreach (RenderableElements.Element element in elements)
                            {
                                if (element?.Model == null) continue;
                                min = Vector3.Min(min, element.Model.MinBounds);
                                max = Vector3.Max(max, element.Model.MaxBounds);
                            }
                            if (min.X <= max.X && (!clipping || TouchesClip(region, min, max, world)))
                                gathered.Render.Add(new RenderBox() { Min = min, Max = max, World = world, Chain = new List<Entity>(step.Chain) });
                        }
                    }
                }
                else if (filter != null && IsBoxType(type))
                    AddBoxCollider(step, function, type, world, filter, region, clipping, gathered);

                if (volumes && (IsBoxType(type) || function.GetParameter(HalfDimensions) != null))
                {
                    Vector3 half = HalfExtentsOf(function);
                    Volume volume = new Volume() { Chain = new List<Entity>(step.Chain), Composite = step.Composite, Entity = function, Type = type.ToString(), World = world, HalfExtents = half };
                    if (!clipping || region.InClip(BoxCentre(world, half)))
                        gathered.Volumes.Add(volume);
                }
                return true;
            }

            WalkRegion(call, commands, level, region, clip, Visit, out gathered.Visited, out gathered.Truncated);
            gathered.Placements = gathered.Colliders.Count;
            if (gathered.Truncated)
                gathered.Warnings.Add("The walk stopped after " + gathered.Visited + " entities: some of the region was not read. Use a smaller region.");
            return gathered;
        }

        /// <summary>
        /// Walk a region's placements (its roots, or the whole level for a box or sphere), visiting every entity with its world
        /// transform. With <paramref name="clip"/> and a clip box, instances nothing of which can reach the box (models, collision,
        /// volumes or any entity's position) are not entered (their composite's extent is memoised; one with aliases moving things
        /// inside it is always entered). UI thread.
        /// </summary>
        public static void WalkRegion(McpCall call, Commands commands, Level level, McpRegion region, bool clip, Func<McpPlacements.Step, bool> visit, out int visited, out bool truncated)
        {
            Hook();
            McpPlacements walker = McpRegion.Walker(commands);
            Composite root = commands.EntryPoints[0];
            CancellationToken cancel = call?.Cancel ?? default(CancellationToken);
            bool clipping = clip && region.HasClip;
            Dictionary<Composite, LocalBounds> memo = clipping ? BoundsMemo(commands, level) : null;
            McpPlacements.Step current = null;

            bool Visit(McpPlacements.Step step)
            {
                current = step;
                return visit(step);
            }

            bool Descend(Composite child)
            {
                if (!clipping || current == null || current.OverridesBelow) return true;
                if (!memo.TryGetValue(child, out LocalBounds bounds)) bounds = Bounds(commands, level, child, memo, new HashSet<Composite>());
                if (bounds.Unbounded) return true;
                //Pruned on where anything inside can be, not just its models and collision: visitors also look for triggers and logic
                if (bounds.ReachEmpty) return false;
                return current.World == null || TouchesClip(region, bounds.ReachMin, bounds.ReachMax, current.World);
            }

            visited = 0;
            truncated = false;
            if (region.Roots.Count == 0)
            {
                walker.Walk(root, Visit, Descend, cancel);
                visited = walker.Visited;
                truncated = walker.Truncated;
                return;
            }
            foreach (List<Entity> chain in region.Roots)
            {
                walker.WalkUnder(root, chain, Visit, Descend, cancel);
                visited += walker.Visited;
                truncated |= walker.Truncated;
            }
        }

        private static bool TouchesClip(McpRegion region, Vector3 localMin, Vector3 localMax, cTransform world)
        {
            WorldBox(localMin, localMax, world, out Vector3 min, out Vector3 max);
            return region.TouchesClip(min, max);
        }

        /// <summary>A local box's corners placed by <paramref name="world"/>, as a world AABB.</summary>
        public static void WorldBox(Vector3 localMin, Vector3 localMax, cTransform world, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue);
            max = new Vector3(float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3((i & 1) == 0 ? localMin.X : localMax.X, (i & 2) == 0 ? localMin.Y : localMax.Y, (i & 4) == 0 ? localMin.Z : localMax.Z);
                Vector3 p = InstanceTransform.PointToWorld(world, corner);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }

        private static void AddModelCollider(Level level, McpPlacements.Step step, FunctionEntity function, cTransform world, string filter, McpRegion region, bool clipping, Dictionary<Models.CS2.Component.LOD.Submesh, string> modelNames, Gathered gathered)
        {
            CollisionMaps.COLLISION_MAPPING template = function.GetResource(ResourceType.COLLISION_MAPPING, true)?.CollisionMapping;
            HavokPackfile.StaticCompoundShape proxy = template?.CollisionProxy;
            if (proxy == null || level.Collision == null) return;

            //As Instancing.ProcessEntity decides the row: ghosted for templates and Required_Assets, gone or shelled when both delete flags are set
            bool ghosted = step.Template || step.Flag(EnableOnReset) == false || (step.Composite?.name ?? "").Replace('/', '\\').StartsWith("Required_Assets\\", StringComparison.OrdinalIgnoreCase);
            bool deleteStandard = step.DeleteStandardCollision, deleteBallistic = step.DeleteBallisticCollision;
            if (deleteStandard && deleteBallistic)
            {
                if (!ghosted) { gathered.Skipped++; return; }
                deleteStandard = deleteBallistic = false;
            }
            string material = template.Material?.Name;
            CollisionMaps.CollisionFlags storage = template.Flags & CollisionMaps.CollisionFlags.STORAGE_TYPE_MASK;
            bool worldStorage, ballisticStorage;
            bool sound = material == "AudioCollision->AudioCollision";
            bool seeThrough = material == "WindowCollision->WindowCollision" || (CollisionMaps.CollisionType)((uint)template.Flags & (uint)CollisionMaps.CollisionFlags.COLLISION_TYPE_MASK) == CollisionMaps.CollisionType.TRANSPARENT;
            if (storage != 0)
            {
                worldStorage = (storage & CollisionMaps.CollisionFlags.WORLD) != 0;
                ballisticStorage = (storage & CollisionMaps.CollisionFlags.BALLISTIC) != 0;
            }
            else if (material == "Collision->Collision" || material == "WindowCollision->WindowCollision" || material == "COLLISION_ONLY")
            {
                worldStorage = true;
                ballisticStorage = true;
            }
            else if (sound)
            {
                worldStorage = true;
                ballisticStorage = false;
            }
            else
            {
                worldStorage = false;
                ballisticStorage = !string.IsNullOrEmpty(material);
            }
            if (deleteStandard && worldStorage) { gathered.Skipped++; return; }
            if (deleteBallistic) ballisticStorage = false;

            Record record = new Record()
            {
                Kind = "model",
                World = worldStorage,
                Ballistic = ballisticStorage,
                Sound = sound,
                SeeThrough = seeThrough,
                Ghosted = ghosted,
                Unhosted = !worldStorage && !ballisticStorage,
                Material = material,
                Proxy = proxy.ProxyIndex,
            };
            if (!record.Passes(filter)) return;
            ProxyMesh mesh = Mesh(level, proxy);
            if (mesh.TriangleCount == 0) return;
            if (clipping && !TouchesClip(region, mesh.Min, mesh.Max, world)) return;
            record.Chain = new List<Entity>(step.Chain);
            record.Composite = step.Composite;
            record.Entity = function;
            record.Transform = world;
            List<RenderableElements.Element> elements = function.GetResource(ResourceType.RENDERABLE_INSTANCE, true)?.RenderableInstance;
            Models.CS2.Component.LOD.Submesh first = elements?.FirstOrDefault(o => o?.Model != null)?.Model;
            if (first != null && modelNames.TryGetValue(first, out string model)) record.Model = model;
            gathered.Colliders.Add(new Placed() { Mesh = mesh, Matrix = InstanceTransform.ToMatrix(world), Record = record });
        }

        private static void AddBoxCollider(McpPlacements.Step step, FunctionEntity function, FunctionType type, cTransform world, string filter, McpRegion region, bool clipping, Gathered gathered)
        {
            Record record = new Record() { Kind = type == FunctionType.CollisionBarrier ? "barrier" : type == FunctionType.NavMeshBarrier ? "nav_barrier" : "sound_barrier", World = true };
            record.Ghosted = step.Template || step.Flag(EnableOnReset) == false;
            if (type == FunctionType.NavMeshBarrier) record.PathClosed = true;
            else if (type == FunctionType.SoundBarrier) record.Sound = true;
            else
            {
                int collisionType = (function.GetParameter(CollisionTypeParam)?.content as cEnum)?.enumIndex ?? (int)COLLISION_TYPE.STANDARD_COL;
                //Player-only and physics-only walls (PLAYER_ONLY, AGAINST_DYNAMIC_SIMULATED) stop no bullet
                bool stopsShots = true;
                switch ((COLLISION_TYPE)collisionType)
                {
                    case COLLISION_TYPE.CAMERA_COL: record.CameraOnly = true; break;
                    case COLLISION_TYPE.LINE_OF_SIGHT_COL: record.PathClosed = true; break;
                    case COLLISION_TYPE.UI: record.Unhosted = true; break;
                    case COLLISION_TYPE.TRANSPARENT_COL: record.SeeThrough = true; break;
                    case COLLISION_TYPE.PLAYER_COL:
                    case COLLISION_TYPE.PHYSICS_COL: stopsShots = false; break;
                }
                record.Ballistic = stopsShots && !record.CameraOnly && !record.PathClosed;
            }
            if (!record.Passes(filter)) return;
            Vector3 half = HalfExtentsOf(function);
            //The box stands on its position: centred half.y above it, in its own rotation
            cTransform centred = new cTransform(BoxCentre(world, half), world.rotation);
            if (clipping && !TouchesClip(region, -half, half, centred)) return;
            record.Chain = new List<Entity>(step.Chain);
            record.Composite = step.Composite;
            record.Entity = function;
            record.Transform = world;
            gathered.Colliders.Add(new Placed() { IsBox = true, HalfExtents = half, Matrix = InstanceTransform.ToMatrix(centred), Record = record });
        }
        #endregion

        #region Composite bounds (for pruning walks)
        /// <summary>The extent of everything a composite places (collision and models, nested instances included), in its own space.</summary>
        public sealed class LocalBounds
        {
            public Vector3 Min = new Vector3(float.MaxValue), Max = new Vector3(float.MinValue);
            /// <summary>
            /// Where anything it places can be, for pruning walks: the content extent, and also every entity's position and
            /// volume box (triggers, lights and logic in a composite with no models still count).
            /// </summary>
            public Vector3 ReachMin = new Vector3(float.MaxValue), ReachMax = new Vector3(float.MinValue);
            /// <summary>An alias in it moves something below: the extent cannot be trusted, so walks always go in.</summary>
            public bool Unbounded;
            public bool Empty => Min.X > Max.X;
            public bool ReachEmpty => ReachMin.X > ReachMax.X;

            public void Add(Vector3 p) { Min = Vector3.Min(Min, p); Max = Vector3.Max(Max, p); AddReach(p); }

            public void AddReach(Vector3 p) { ReachMin = Vector3.Min(ReachMin, p); ReachMax = Vector3.Max(ReachMax, p); }
        }

        private static readonly Dictionary<Composite, LocalBounds> _bounds = new Dictionary<Composite, LocalBounds>();
        private static int _boundsVersion = -1;
        private static Commands _boundsFor;

        private static Dictionary<Composite, LocalBounds> BoundsMemo(Commands commands, Level level)
        {
            //Listening, so a script change or the level closing drops the memo
            Hook();
            lock (_lock)
            {
                if (_boundsFor != commands || _boundsVersion != Version)
                {
                    _bounds.Clear();
                    _boundsFor = commands;
                    _boundsVersion = Version;
                }
                return _bounds;
            }
        }

        /// <summary>A composite's local content extent (memoised until the script changes). UI thread.</summary>
        public static LocalBounds Bounds(Commands commands, Level level, Composite composite, Dictionary<Composite, LocalBounds> memo = null, HashSet<Composite> stack = null)
        {
            if (memo == null) memo = BoundsMemo(commands, level);
            if (stack == null) stack = new HashSet<Composite>();
            if (memo.TryGetValue(composite, out LocalBounds known)) return known;
            LocalBounds bounds = new LocalBounds();
            if (!stack.Add(composite)) return bounds;
            try
            {
                if (composite.aliases.Any(o => InstanceTransform.TransformOf(o) != null)) bounds.Unbounded = true;
                foreach (FunctionEntity function in composite.functions)
                {
                    cTransform local = InstanceTransform.TransformOf(function) ?? new cTransform(Vector3.Zero, Vector3.Zero);
                    //Its own position (a function with none sits at the origin), so a walk looking for it goes in
                    bounds.AddReach(local.position);
                    if (!function.function.IsFunctionType)
                    {
                        Composite child = commands.GetComposite(function.function);
                        if (child == null) continue;
                        LocalBounds inner = Bounds(commands, level, child, memo, stack);
                        if (inner.Unbounded) bounds.Unbounded = true;
                        if (!inner.ReachEmpty)
                        {
                            WorldBox(inner.ReachMin, inner.ReachMax, local, out Vector3 ra, out Vector3 rb);
                            bounds.AddReach(ra); bounds.AddReach(rb);
                        }
                        if (inner.Empty) continue;
                        WorldBox(inner.Min, inner.Max, local, out Vector3 a, out Vector3 b);
                        bounds.Add(a); bounds.Add(b);
                        continue;
                    }
                    FunctionType type = function.function.AsFunctionType;
                    if (type == FunctionType.ModelReference)
                    {
                        HavokPackfile.StaticCompoundShape proxy = function.GetResource(ResourceType.COLLISION_MAPPING, true)?.CollisionMapping?.CollisionProxy;
                        if (proxy != null && level.Collision != null)
                        {
                            Vector3 pmin, pmax;
                            if (proxy.DomainMax.X > proxy.DomainMin.X && proxy.DomainMax.Y >= proxy.DomainMin.Y && proxy.DomainMax.Z > proxy.DomainMin.Z)
                            {
                                pmin = new Vector3(proxy.DomainMin.X, proxy.DomainMin.Y, proxy.DomainMin.Z);
                                pmax = new Vector3(proxy.DomainMax.X, proxy.DomainMax.Y, proxy.DomainMax.Z);
                            }
                            else
                            {
                                ProxyMesh mesh = Mesh(level, proxy);
                                pmin = mesh.Min; pmax = mesh.Max;
                            }
                            if (pmin.X <= pmax.X)
                            {
                                WorldBox(pmin, pmax, local, out Vector3 a, out Vector3 b);
                                bounds.Add(a); bounds.Add(b);
                            }
                        }
                        List<RenderableElements.Element> elements = function.GetResource(ResourceType.RENDERABLE_INSTANCE, true)?.RenderableInstance;
                        if (elements != null)
                        {
                            foreach (RenderableElements.Element element in elements)
                            {
                                if (element?.Model == null) continue;
                                WorldBox(element.Model.MinBounds, element.Model.MaxBounds, local, out Vector3 a, out Vector3 b);
                                bounds.Add(a); bounds.Add(b);
                            }
                        }
                    }
                    else if (IsBoxType(type))
                    {
                        Vector3 half = HalfExtentsOf(function);
                        WorldBox(-half, half, new cTransform(BoxCentre(local, half), local.rotation), out Vector3 a, out Vector3 b);
                        bounds.Add(a); bounds.Add(b);
                    }
                    else if (function.GetParameter(HalfDimensions) != null)
                    {
                        //A trigger volume: not content, but somewhere a walk collecting volumes has to reach
                        Vector3 half = HalfExtentsOf(function);
                        WorldBox(-half, half, new cTransform(BoxCentre(local, half), local.rotation), out Vector3 a, out Vector3 b);
                        bounds.AddReach(a); bounds.AddReach(b);
                    }
                }
            }
            finally
            {
                stack.Remove(composite);
            }
            memo[composite] = bounds;
            return bounds;
        }

        /// <summary>A world box around a region's own content: the union of what <see cref="Gather"/> finds (render and collision), exact for its placements. UI thread.</summary>
        public static bool RegionBox(McpCall call, Commands commands, Level level, McpRegion region, out Vector3 min, out Vector3 max, out Gathered gathered)
        {
            min = new Vector3(float.MaxValue);
            max = new Vector3(float.MinValue);
            if (region.Roots.Count == 0 && region.HasClip)
            {
                min = region.ClipMin.Value;
                max = region.ClipMax.Value;
                gathered = null;
                return true;
            }
            gathered = Gather(call, commands, level, region, "all", render: true, volumes: false, clip: false);
            foreach (Placed placed in gathered.Colliders)
            {
                if (placed.Record.Ghosted) continue;
                PlacedBox(placed, out Vector3 a, out Vector3 b);
                min = Vector3.Min(min, a); max = Vector3.Max(max, b);
            }
            foreach (RenderBox box in gathered.Render)
            {
                WorldBox(box.Min, box.Max, box.World, out Vector3 a, out Vector3 b);
                min = Vector3.Min(min, a); max = Vector3.Max(max, b);
            }
            if (min.X > max.X) return false;
            if (region.Margin > 0) { min -= new Vector3(region.Margin); max += new Vector3(region.Margin); }
            return true;
        }

        /// <summary>A placed collider's world AABB (from its local box's corners).</summary>
        public static void PlacedBox(Placed placed, out Vector3 min, out Vector3 max)
        {
            Vector3 lmin = placed.IsBox ? -placed.HalfExtents : placed.Mesh.Min, lmax = placed.IsBox ? placed.HalfExtents : placed.Mesh.Max;
            min = new Vector3(float.MaxValue);
            max = new Vector3(float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3((i & 1) == 0 ? lmin.X : lmax.X, (i & 2) == 0 ? lmin.Y : lmax.Y, (i & 4) == 0 ? lmin.Z : lmax.Z);
                Vector3 p = Vector3.Transform(corner, placed.Matrix);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
        }
        #endregion

        #region Soups
        /// <summary>A ray's hit.</summary>
        public struct Hit
        {
            public float Distance;
            public Vector3 Point;
            /// <summary>The surface normal, turned to face where the ray came from.</summary>
            public Vector3 Normal;
            public Record Record;
            public int Triangle;
        }

        /// <summary>A region's collision as world triangles in a BVH, with each triangle's collider.</summary>
        public sealed class Soup
        {
            public string Key;
            public string Filter;
            public string RegionLabel;
            public float[] Vertices;
            public int[] Faces;
            public BVHAccel Bvh;
            public List<Record> Records;
            private int[] _starts;
            public Vector3 Min, Max;
            public int Visited;
            public bool Truncated;
            public List<string> Warnings = new List<string>();
            public long BuildMilliseconds;
            public int TriangleCount => Faces.Length / 3;

            internal void Index() => _starts = Records.Select(o => o.FirstTriangle).ToArray();

            public Record RecordOf(int triangle)
            {
                int index = Array.BinarySearch(_starts, triangle);
                if (index < 0) index = ~index - 1;
                return index < 0 || index >= Records.Count ? null : Records[index];
            }

            public Vector3 Vertex(int index) => new Vector3(Vertices[index * 3], Vertices[index * 3 + 1], Vertices[index * 3 + 2]);

            /// <summary>The first hit along a ray (direction normalised here) within <paramref name="maxDistance"/>, skipping colliders <paramref name="skip"/> picks.</summary>
            public bool Cast(Vector3 origin, Vector3 direction, float maxDistance, out Hit hit, Func<Record, bool> skip = null, float minDistance = 0f)
            {
                hit = default(Hit);
                if (Bvh == null || TriangleCount == 0) return false;
                float length = direction.Length();
                if (length < 1e-9f) return false;
                Vector3 dir = direction / length;
                //Skipped colliders are left out of the traversal itself: stepping the ray on past a skipped hit loses a surface level with it (a prop's base on the floor)
                Func<int, bool> accept = null;
                if (skip != null)
                {
                    Record last = null;
                    bool lastSkipped = false;
                    accept = triangle =>
                    {
                        Record record = RecordOf(triangle);
                        if (record == null) return true;
                        if (record != last)
                        {
                            last = record;
                            lastSkipped = skip(record);
                        }
                        return !lastSkipped;
                    };
                }
                Ray ray = new Ray(origin, dir, minDistance, maxDistance);
                if (!Bvh.Traverse(ref ray, out NanoRT.Hit found, accept))
                    return false;
                Vector3 normal = Bvh.TriangleNormal(found.PrimId);
                if (Vector3.Dot(normal, dir) > 0) normal = -normal;
                hit = new Hit() { Distance = found.T, Point = origin + dir * found.T, Normal = normal, Record = RecordOf(found.PrimId), Triangle = found.PrimId };
                return true;
            }

            /// <summary>Every hit along a ray, nearest first, at most <paramref name="max"/>.</summary>
            public List<Hit> CastAll(Vector3 origin, Vector3 direction, float maxDistance, int max, Func<Record, bool> skip = null)
            {
                List<Hit> hits = new List<Hit>();
                float from = 0f;
                while (hits.Count < max && Cast(origin, direction, maxDistance, out Hit hit, skip, from))
                {
                    hits.Add(hit);
                    from = hit.Distance + 1e-3f;
                }
                return hits;
            }

            /// <summary>The surface below a point: a ray down from <paramref name="above"/> metres over it.</summary>
            public bool FloorUnder(Vector3 point, out Hit hit, float above = 0.5f, float maxDrop = 50f, Func<Record, bool> skip = null)
            {
                return Cast(point + new Vector3(0, above, 0), -Vector3.UnitY, maxDrop + above, out hit, skip);
            }

            /// <summary>The surface above a point.</summary>
            public bool CeilingOver(Vector3 point, out Hit hit, float maxRise = 50f, Func<Record, bool> skip = null)
            {
                return Cast(point, Vector3.UnitY, maxRise, out hit, skip);
            }

            /// <summary>Whether the straight line between two points is clear (see-through colliders let it pass when <paramref name="throughSeeThrough"/>).</summary>
            public bool Clear(Vector3 from, Vector3 to, out Hit blocker, bool throughSeeThrough = true, Func<Record, bool> skip = null)
            {
                Vector3 d = to - from;
                float length = d.Length();
                blocker = default(Hit);
                if (length < 1e-5f) return true;
                Func<Record, bool> skipping = throughSeeThrough ? (Func<Record, bool>)(r => r.SeeThrough || (skip != null && skip(r))) : skip;
                return !Cast(from, d, length - 1e-3f, out blocker, skipping, 1e-3f);
            }
        }

        /// <summary>
        /// The soup for a region (cached). The walk is on the UI thread; the triangles and BVH are built on the calling
        /// thread. <paramref name="region"/> must already be resolved.
        /// </summary>
        public static Soup SoupFor(McpCall call, McpRegion region, string filter)
        {
            Commands commands = null;
            Level level = null;
            string key = null;
            Gathered gathered = null;
            Soup cached = McpEditor.UI(() =>
            {
                Hook();
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                commands = content.Level.Commands;
                level = content.Level;
                key = level.Name + "|" + RuntimeHelpers.GetHashCode(level) + "|" + Version + "|" + filter + "|" + region.Key;
                lock (_lock)
                {
                    Soup hit = _soups.FirstOrDefault(o => o.Key == key);
                    if (hit != null)
                    {
                        _soups.Remove(hit);
                        _soups.Insert(0, hit);
                        return hit;
                    }
                }
                using (McpEditorTools.Heartbeat(call, "Reading the collision of " + region.Label))
                    gathered = Gather(call, commands, level, region, filter, render: false, volumes: false);
                return null;
            });
            if (cached != null) return cached;

            Stopwatch timer = Stopwatch.StartNew();
            Soup soup = Build(gathered, key, filter, region.Label);
            soup.BuildMilliseconds = timer.ElapsedMilliseconds;
            lock (_lock)
            {
                _soups.Insert(0, soup);
                while (_soups.Count > SoupCache) _soups.RemoveAt(_soups.Count - 1);
            }
            return soup;
        }

        private static readonly int[] BoxFaces =
        {
            0, 2, 1, 1, 2, 3, //-z
            4, 5, 6, 5, 7, 6, //+z
            0, 1, 4, 1, 5, 4, //-y
            2, 6, 3, 3, 6, 7, //+y
            0, 4, 2, 2, 4, 6, //-x
            1, 3, 5, 3, 7, 5, //+x
        };

        /// <summary>World triangles and a BVH from gathered colliders.</summary>
        public static Soup Build(Gathered gathered, string key, string filter, string label, bool bvh = true)
        {
            List<Placed> colliders = gathered.Colliders;
            int[] vertexStart = new int[colliders.Count + 1], triangleStart = new int[colliders.Count + 1];
            for (int i = 0; i < colliders.Count; i++)
            {
                vertexStart[i + 1] = vertexStart[i] + (colliders[i].IsBox ? 8 : colliders[i].Mesh.Positions.Length);
                triangleStart[i + 1] = triangleStart[i] + (colliders[i].IsBox ? 12 : colliders[i].Mesh.TriangleCount);
            }
            float[] vertices = new float[vertexStart[colliders.Count] * 3];
            int[] faces = new int[triangleStart[colliders.Count] * 3];
            Parallel.For(0, colliders.Count, i =>
            {
                Placed placed = colliders[i];
                int v0 = vertexStart[i], f0 = triangleStart[i] * 3;
                if (placed.IsBox)
                {
                    Vector3 h = placed.HalfExtents;
                    for (int c = 0; c < 8; c++)
                    {
                        Vector3 p = Vector3.Transform(new Vector3((c & 1) == 0 ? -h.X : h.X, (c & 2) == 0 ? -h.Y : h.Y, (c & 4) == 0 ? -h.Z : h.Z), placed.Matrix);
                        vertices[(v0 + c) * 3] = p.X; vertices[(v0 + c) * 3 + 1] = p.Y; vertices[(v0 + c) * 3 + 2] = p.Z;
                    }
                    for (int f = 0; f < BoxFaces.Length; f++) faces[f0 + f] = v0 + BoxFaces[f];
                }
                else
                {
                    Vector3[] positions = placed.Mesh.Positions;
                    for (int c = 0; c < positions.Length; c++)
                    {
                        Vector3 p = Vector3.Transform(positions[c], placed.Matrix);
                        vertices[(v0 + c) * 3] = p.X; vertices[(v0 + c) * 3 + 1] = p.Y; vertices[(v0 + c) * 3 + 2] = p.Z;
                    }
                    int[] indices = placed.Mesh.Indices;
                    for (int f = 0; f < indices.Length; f++) faces[f0 + f] = v0 + indices[f];
                }
                placed.Record.FirstTriangle = triangleStart[i];
                placed.Record.TriangleCount = triangleStart[i + 1] - triangleStart[i];
            });

            Soup soup = new Soup()
            {
                Key = key,
                Filter = filter,
                RegionLabel = label,
                Vertices = vertices,
                Faces = faces,
                Records = colliders.Select(o => o.Record).ToList(),
                Visited = gathered.Visited,
                Truncated = gathered.Truncated,
                Warnings = new List<string>(gathered.Warnings),
            };
            soup.Index();
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            for (int i = 0; i + 2 < vertices.Length; i += 3)
            {
                Vector3 p = new Vector3(vertices[i], vertices[i + 1], vertices[i + 2]);
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            soup.Min = min;
            soup.Max = max;
            if (faces.Length != 0 && bvh)
            {
                soup.Bvh = new BVHAccel();
                soup.Bvh.Build(vertices, faces);
            }
            return soup;
        }
        #endregion

        #region Describing
        /// <summary>A collider for a result: what entity it is and where it is from.</summary>
        public static JToken Describe(Commands commands, Record record, bool brief = false)
        {
            if (record == null) return null;
            //Brief: one line, "Name in Composite (model) [classes]"
            if (brief)
            {
                string classes = record.Classes();
                return McpScript.EntityName(commands, record.Composite, record.Entity) + " in " + McpScript.CompositeLeaf(record.Composite) +
                    (record.Model != null ? " (" + record.Model.Replace('\\', '/').Split('/').Last() + ")" : "") + (classes.Length != 0 ? " [" + classes + "]" : "");
            }
            JObject described = new JObject()
            {
                ["entity"] = McpScript.EntityName(commands, record.Composite, record.Entity),
                ["id"] = McpScript.Id(record.Entity.shortGUID),
                ["composite"] = record.Composite?.name,
            };
            JObject chain = McpRegion.ChainJson(commands, record.Chain);
            described["path"] = chain["path"];
            described["ids"] = chain["ids"];
            described["kind"] = record.Kind;
            if (record.Model != null) described["model"] = record.Model;
            string kinds = record.Classes();
            if (kinds.Length != 0) described["collision"] = kinds;
            return described;
        }

        /// <summary>'floor', 'wall' or 'ceiling' from a hit normal (facing the ray).</summary>
        public static string Facing(Vector3 normal) => normal.Y > 0.7f ? "floor" : normal.Y < -0.7f ? "ceiling" : normal.Y > 0.35f ? "slope" : "wall";

        /// <summary>Slope of a surface in degrees from its normal.</summary>
        public static double SlopeDegrees(Vector3 normal) => Math.Round(Math.Acos(Math.Min(1.0, Math.Abs(normal.Y))) * 180.0 / Math.PI, 1);

        public static double R(double v, int digits = 3) => Math.Round(v, digits);

        public static JArray V(Vector3 v) => McpValues.Vector(new Vector3((float)Math.Round(v.X, 3), (float)Math.Round(v.Y, 3), (float)Math.Round(v.Z, 3)));

        /// <summary>An AABB for a result: min, max, centre and size.</summary>
        public static JObject Aabb(Vector3 min, Vector3 max) => new JObject()
        {
            ["min"] = V(min),
            ["max"] = V(max),
            ["centre"] = V((min + max) * 0.5f),
            ["size"] = V(max - min),
        };

        /// <summary>
        /// The dominant floor and ceiling heights of a set of triangles inside an XZ box: the 10 cm height band with the most
        /// up-facing area, and the band with the most down-facing area above it (at least <paramref name="minHeadroom"/> up).
        /// </summary>
        public static void FloorAndCeiling(Soup soup, Vector3 min, Vector3 max, out float? floor, out float? ceiling, float minHeadroom = 1.8f, IEnumerable<int> triangles = null)
        {
            //Per 10 cm band: the flat area in it, and that area's height summed (for the band's real height, not its middle)
            Dictionary<int, double> up = new Dictionary<int, double>(), heights = new Dictionary<int, double>();
            IEnumerable<int> range = triangles ?? Enumerable.Range(0, soup.TriangleCount);
            //A floor is usually the very bottom of the box: a hair of rounding must not drop it
            min -= new Vector3(0.05f, 0.3f, 0.05f);
            max += new Vector3(0.05f, 0.3f, 0.05f);
            foreach (int t in range)
            {
                Vector3 a = soup.Vertex(soup.Faces[t * 3]), b = soup.Vertex(soup.Faces[t * 3 + 1]), c = soup.Vertex(soup.Faces[t * 3 + 2]);
                Vector3 centre = (a + b + c) / 3f;
                if (centre.X < min.X || centre.X > max.X || centre.Z < min.Z || centre.Z > max.Z || centre.Y < min.Y || centre.Y > max.Y) continue;
                Vector3 n = Vector3.Cross(b - a, c - a);
                double area = n.Length() * 0.5;
                if (area < 1e-6) continue;
                n = Vector3.Normalize(n);
                //Winding is not reliable across proxies: a flat triangle counts as both until the heights sort it out
                if (Math.Abs(n.Y) < 0.8f) continue;
                int band = (int)Math.Floor(centre.Y * 10);
                up.TryGetValue(band, out double u); up[band] = u + area;
                heights.TryGetValue(band, out double h); heights[band] = h + area * centre.Y;
            }
            floor = null;
            ceiling = null;
            if (up.Count == 0) return;
            float HeightOf(int band) => (float)(heights[band] / up[band]);
            //The floor: the band with the most horizontal area in the lower half of the box (rooms have more floor than ceiling detail)
            double mid = (min.Y + max.Y) * 0.5;
            KeyValuePair<int, double> best = up.Where(o => o.Key / 10.0 <= mid || up.Count == 1).DefaultIfEmpty(up.OrderByDescending(o => o.Value).First()).OrderByDescending(o => o.Value).First();
            floor = HeightOf(best.Key);
            float f = floor.Value;
            List<KeyValuePair<int, double>> above = up.Where(o => HeightOf(o.Key) >= f + minHeadroom).OrderByDescending(o => o.Value).ToList();
            if (above.Count != 0) ceiling = HeightOf(above[0].Key);
        }
        #endregion
    }
}
