using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.Scripting.Refactor;
using CathodeLib;
using CathodeLib.ObjectExtensions;
using OpenCAGE.DockPanels;
using OpenCAGE.MCP;
using OpenCAGE.Undo;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using static CATHODE.CollisionMaps;
using static CathodeLib.CompositeFlowgraphTable;

namespace OpenCAGE
{
    /// <summary>
    /// Collision that is walked on AND shot at, made the way retail makes it: two entities with one collision row each.
    /// <list type="bullet">
    /// <item>The entity itself keeps a WORLD row (storage 0x40: the world host, collision layer 3) on a proxy whose own
    /// (template) instances carry collision type 3 - what characters stand on and the Alien paths over.</item>
    /// <item>A sibling ModelReference in the same composite, "&lt;name&gt;_Ballistic", at the same place, holds a BALLISTIC row
    /// (storage 0x80: the ballistic host, layer 9) on its own copy of the proxy whose template instances carry type 9 and the
    /// physics material's write index as userData - what bullets hit, and the only static layer the game's zone, footstep
    /// and camera-zone rays (layer 22) collide with.</item>
    /// </list>
    /// No retail row has both storage bits: the game picks a row's host from <c>flags &amp; 0xE0</c>, and 0xC0 picks none, so
    /// one row asked to be both was built as a layer-3 WORLD row with the ballistic half dropped - no zones, all footsteps
    /// the engine's fallback metal, and an instance the game cannot hide or move. Every editor path that sets "WORLD +
    /// BALLISTIC" collision (set_collision and the tools that call it, the Resources dialog's Collision Mapping) comes here.
    /// <para>The pair logic is written once against <see cref="IWriter"/>: <see cref="Undoable"/> records each change on
    /// the editor's undo history (the dialogs and tools), <see cref="Direct"/> makes it in place (harnesses, no editor).
    /// New proxies are never undoable, as elsewhere: a twin made once is reused for every entity sharing its source.</para>
    /// <para>The half follows its entity. Retail never scripts a <c>*_COL</c> half at all (19 PC levels, 4,728 pairs: no link
    /// into one, 5 aliases over one, no sequence entry) - its scripts reach the render half or the composite instance holding
    /// both - so there is no doubled-link pattern to copy. What retail does have for one ModelReference following another is
    /// a relay link (<c>A.hidden -&gt; B.hide</c>, <c>shown -&gt; show</c>, <c>enabled -&gt; enable</c>, <c>disabled -&gt;
    /// disable</c>, 100+ of them). So every save (<see cref="KeepHalvesInStep"/>, idempotent) gives the half its entity's
    /// parameters, those relay links from the entity (whatever hides or disables the entity - a link, an alias, a sequence,
    /// a proxy - then reaches the half), the entity's attachment, and a copy of each alias overriding the entity; it renames
    /// a half whose entity was renamed, and removes a half whose entity is gone. Deleting the entity (the editor, the tools,
    /// undo and redo) deletes the half with it (<see cref="DeleteWithHalf"/>).</para>
    /// </summary>
    public static class CollisionPairs
    {
        /// <summary>Added to the entity's name to name its ballistic half.</summary>
        public const string SiblingSuffix = "_Ballistic";
        /// <summary>The collision type a WORLD row's proxy carries on its own instances (STANDARD).</summary>
        public const uint StandardFilter = (uint)CollisionType.STANDARD;
        /// <summary>The collision type a BALLISTIC row's proxy carries on its own instances (BALLISTICS).</summary>
        public const uint BallisticsFilter = (uint)CollisionType.BALLISTICS;

        private const CollisionFlags WorldAndBallistic = CollisionFlags.WORLD | CollisionFlags.BALLISTIC;

        #region Flags
        /// <summary>Both storage bits: what retail makes as two rows on two entities.</summary>
        public static bool IsPair(CollisionFlags flags) => (flags & WorldAndBallistic) == WorldAndBallistic;

        public static CollisionFlags Storage(CollisionFlags flags) => flags & CollisionFlags.STORAGE_TYPE_MASK;
        public static bool IsWorldOnly(CollisionFlags flags) => Storage(flags) == CollisionFlags.WORLD;
        public static bool IsBallisticOnly(CollisionFlags flags) => Storage(flags) == CollisionFlags.BALLISTIC;

        /// <summary>The WORLD row of a pair: the ballistic storage bit off, and a BALLISTICS type (if one was given) made STANDARD.</summary>
        public static CollisionFlags WorldHalf(CollisionFlags flags)
        {
            flags &= ~CollisionFlags.BALLISTIC;
            if (TypeOf(flags) == CollisionType.BALLISTICS)
                flags = WithType(flags, CollisionType.STANDARD);
            return flags;
        }

        /// <summary>The BALLISTIC row of a pair: BALLISTIC storage alone, the other bits kept, any type given made BALLISTICS.</summary>
        public static CollisionFlags BallisticHalf(CollisionFlags flags)
        {
            flags = (flags & ~CollisionFlags.STORAGE_TYPE_MASK) | CollisionFlags.BALLISTIC;
            CollisionType type = TypeOf(flags);
            if (type != CollisionType.EVERYTHING && type != CollisionType.BALLISTICS)
                flags = WithType(flags, CollisionType.BALLISTICS);
            return flags;
        }

        private static CollisionType TypeOf(CollisionFlags flags) => (CollisionType)((uint)flags & (uint)CollisionFlags.COLLISION_TYPE_MASK);
        private static CollisionFlags WithType(CollisionFlags flags, CollisionType type) => (flags & ~CollisionFlags.COLLISION_TYPE_MASK) | (CollisionFlags)type;
        #endregion

        #region Proxies
        /// <summary>The collision type a new proxy for a row of these flags carries: STANDARD with the WORLD bit, BALLISTICS without (as retail).</summary>
        public static uint FilterFor(CollisionFlags flags) => (flags & CollisionFlags.WORLD) != 0 ? StandardFilter : BallisticsFilter;

        /// <summary>
        /// Whether a row of these flags can use the proxy as it is: retail's BALLISTIC rows name proxies whose own instances
        /// are all type 9, its WORLD rows proxies with none of type 9. (The type a proxy's own instances carry is the layer
        /// the game collides the placed copy on, whatever the row says.)
        /// </summary>
        public static bool Suits(HavokPackfile.StaticCompoundShape proxy, CollisionFlags flags)
        {
            if (proxy?.Instances == null || proxy.Instances.Count == 0)
                return true;
            if ((flags & CollisionFlags.WORLD) != 0)
                return !proxy.Instances.Any(o => o.FilterInfo == BallisticsFilter);
            if ((flags & CollisionFlags.BALLISTIC) != 0)
                return proxy.Instances.All(o => o.FilterInfo == BallisticsFilter);
            return true;
        }

        /// <summary>The collision type all of a proxy's own instances carry, or -1 when they differ (or it has none).</summary>
        public static int UniformFilter(HavokPackfile.StaticCompoundShape proxy)
        {
            if (proxy?.Instances == null || proxy.Instances.Count == 0)
                return -1;
            uint first = proxy.Instances[0].FilterInfo;
            foreach (HavokPackfile.CompoundInstance instance in proxy.Instances)
                if (instance.FilterInfo != first)
                    return -1;
            return (int)first;
        }

        /// <summary>The Havok userData of a proxy's first own instance (retail: the physics material's write index).</summary>
        public static uint UserDataOf(HavokPackfile.StaticCompoundShape proxy) => proxy?.Instances != null && proxy.Instances.Count != 0 ? (uint)proxy.Instances[0].UserData : 0u;

        /// <summary>What a new proxy for a row with this physics material stores as userData: the material's write index, else <paramref name="fallback"/>'s own.</summary>
        public static uint UserData(Level level, Materials.Material material, HavokPackfile.StaticCompoundShape fallback = null)
        {
            int index = material != null && level?.Materials != null ? level.Materials.GetWriteIndexOrPending(material) : -1;
            return index >= 0 ? (uint)index : UserDataOf(fallback);
        }

        /// <summary>Proxies made from the same triangles, by (collision type, userData): one family per source.</summary>
        private sealed class Family
        {
            public readonly Dictionary<(uint filter, uint userData), HavokPackfile.StaticCompoundShape> Members = new Dictionary<(uint, uint), HavokPackfile.StaticCompoundShape>();
        }
        //Keyed by the compound objects themselves, so a family goes with the level that holds them
        private static readonly ConditionalWeakTable<HavokPackfile.StaticCompoundShape, Family> _families = new ConditionalWeakTable<HavokPackfile.StaticCompoundShape, Family>();
        private static readonly object _familyLock = new object();

        private static Family FamilyOf(HavokPackfile.StaticCompoundShape proxy)
        {
            if (_families.TryGetValue(proxy, out Family family))
                return family;
            family = new Family();
            int filter = UniformFilter(proxy);
            if (filter >= 0)
                family.Members[((uint)filter, UserDataOf(proxy))] = proxy;
            _families.Add(proxy, family);
            return family;
        }

        /// <summary>
        /// Remember that <paramref name="twin"/> holds <paramref name="source"/>'s triangles (made from the same mesh, or copied
        /// from it), so the next row asking for its collision type and userData gets it rather than another copy.
        /// </summary>
        public static void RememberTwin(HavokPackfile.StaticCompoundShape source, HavokPackfile.StaticCompoundShape twin)
        {
            if (source == null || twin == null || ReferenceEquals(source, twin))
                return;
            lock (_familyLock)
            {
                Family family = FamilyOf(source);
                if (_families.TryGetValue(twin, out Family other) && !ReferenceEquals(other, family))
                {
                    //Two families turn out to be one: every member of the other joins this one
                    foreach (KeyValuePair<(uint, uint), HavokPackfile.StaticCompoundShape> member in other.Members.ToList())
                    {
                        if (!family.Members.ContainsKey(member.Key))
                            family.Members[member.Key] = member.Value;
                        _families.Remove(member.Value);
                        _families.Add(member.Value, family);
                    }
                    _families.Remove(twin);
                }
                if (!_families.TryGetValue(twin, out _))
                    _families.Add(twin, family);
                int filter = UniformFilter(twin);
                if (filter >= 0)
                    family.Members[((uint)filter, UserDataOf(twin))] = twin;
            }
        }

        /// <summary>
        /// The proxy for <paramref name="proxy"/>'s triangles with own instances of collision type <paramref name="filterInfo"/> and
        /// userData <paramref name="userData"/>: the proxy itself when it is one; a twin made before (this session, or one a pair in
        /// the level already uses); else a new one, copied from its triangles into the level's collision files (both on PC). A new
        /// proxy cannot be undone or deleted. Throws when there is nothing to copy.
        /// </summary>
        public static HavokPackfile.StaticCompoundShape ProxyFor(Level level, HavokPackfile.StaticCompoundShape proxy, uint filterInfo, uint userData, IWriter writer, out bool created)
        {
            created = false;
            if (proxy == null)
                return null;
            if (UniformFilter(proxy) == filterInfo && UserDataOf(proxy) == userData)
                return proxy;
            HavokPackfile hkx = level?.Collision;
            if (hkx == null || !hkx.Loaded)
                throw new InvalidOperationException("This level has no COLLISION.HKX loaded.");

            HavokPackfile.StaticCompoundShape known;
            lock (_familyLock)
                FamilyOf(proxy).Members.TryGetValue((filterInfo, userData), out known);
            if (known != null && ReferenceEquals(hkx.GetCompound(known.ProxyIndex), known))
                return known;
            HavokPackfile.StaticCompoundShape paired = FindPairedTwin(level, proxy, filterInfo, userData);
            if (paired != null)
            {
                RememberTwin(proxy, paired);
                return paired;
            }

            HavokPackfile.PreviewMesh mesh = hkx.BuildBakeMesh(proxy);
            if (mesh.TriangleCount == 0)
                throw new InvalidOperationException("Collision proxy " + proxy.ProxyIndex + " has no triangles to copy.");
            CollisionProxyImporter.MeshSource source = new CollisionProxyImporter.MeshSource() { Name = "proxy " + proxy.ProxyIndex };
            source.Append(mesh.Positions, mesh.Indices);
            HavokPackfile.StaticCompoundShape twin = CollisionProxyImporter.Import(level, source, userData, filterInfo);
            RememberTwin(proxy, twin);
            writer?.ProxyCreated(level, twin);
            created = true;
            return twin;
        }

        /// <summary>
        /// A twin an existing pair in the level already uses (from an earlier session): an entity whose row names
        /// <paramref name="source"/>, whose other half names a proxy of the type and userData asked for with the same triangles.
        /// </summary>
        private static HavokPackfile.StaticCompoundShape FindPairedTwin(Level level, HavokPackfile.StaticCompoundShape source, uint filterInfo, uint userData)
        {
            Commands commands = level?.Commands;
            if (commands == null)
                return null;
            foreach (Composite composite in commands.Entries)
            {
                if (composite == null) continue;
                Names names = null;
                foreach (FunctionEntity entity in composite.functions)
                {
                    if (!ReferenceEquals(RowOf(entity)?.CollisionProxy, source) || !IsModelReference(entity))
                        continue;
                    names = names ?? new Names(commands, composite);
                    FunctionEntity other = FindSibling(commands, composite, entity, names) ?? FindOriginal(commands, composite, entity, names);
                    HavokPackfile.StaticCompoundShape candidate = RowOf(other)?.CollisionProxy;
                    if (candidate == null || ReferenceEquals(candidate, source) || UniformFilter(candidate) != filterInfo || UserDataOf(candidate) != userData)
                        continue;
                    if (SameShape(level.Collision, source, candidate))
                        return candidate;
                }
            }
            return null;
        }

        private static bool SameShape(HavokPackfile hkx, HavokPackfile.StaticCompoundShape a, HavokPackfile.StaticCompoundShape b)
        {
            const float tolerance = 0.05f;
            if (Math.Abs(a.DomainMin.X - b.DomainMin.X) > tolerance || Math.Abs(a.DomainMin.Y - b.DomainMin.Y) > tolerance || Math.Abs(a.DomainMin.Z - b.DomainMin.Z) > tolerance ||
                Math.Abs(a.DomainMax.X - b.DomainMax.X) > tolerance || Math.Abs(a.DomainMax.Y - b.DomainMax.Y) > tolerance || Math.Abs(a.DomainMax.Z - b.DomainMax.Z) > tolerance)
                return false;
            //The encoder drops slivers that re-quantise to nothing, so a copy can hold a triangle or two fewer than its source
            int ta = hkx.BuildBakeMesh(a).TriangleCount, tb = hkx.BuildBakeMesh(b).TriangleCount;
            return Math.Abs(ta - tb) <= Math.Max(2, Math.Max(ta, tb) / 100);
        }
        #endregion

        #region Siblings
        public static bool IsModelReference(Entity entity) => entity is FunctionEntity function && function.function == FunctionType.ModelReference;

        /// <summary>The collision row (template) an entity names, from its own list or its 'resource' parameter.</summary>
        public static COLLISION_MAPPING RowOf(FunctionEntity entity) => entity?.GetResource(ResourceType.COLLISION_MAPPING, true)?.CollisionMapping;

        /// <summary>
        /// Whether a ModelReference is shaped as a pair's ballistic half: named "&lt;name&gt;_Ballistic", drawing nothing, its one
        /// collision row of BALLISTIC storage. (Retail has none: its few entities named "Ballistic" carry no such row.)
        /// </summary>
        public static bool IsHalfShaped(FunctionEntity entity, string name)
        {
            if (!IsModelReference(entity) || name == null || name.Length <= SiblingSuffix.Length || !name.EndsWith(SiblingSuffix, StringComparison.Ordinal))
                return false;
            COLLISION_MAPPING row = RowOf(entity);
            return row != null && IsBallisticOnly(row.Flags) && entity.GetResource(ResourceType.RENDERABLE_INSTANCE, true) == null;
        }

        /// <summary>The id an entity's half carries in its name when the entity shares its name: its bytes, as hex.</summary>
        private static string IdTag(Entity entity) => entity.shortGUID.ToByteString().Replace("-", "");

        /// <summary>Whether two proxies cover the same box (a twin copied from the other does, within the encoder's quantisation).</summary>
        private static bool SameBounds(HavokPackfile.StaticCompoundShape a, HavokPackfile.StaticCompoundShape b)
        {
            const float tolerance = 0.05f;
            if (a == null || b == null)
                return false;
            return Math.Abs(a.DomainMin.X - b.DomainMin.X) <= tolerance && Math.Abs(a.DomainMin.Y - b.DomainMin.Y) <= tolerance && Math.Abs(a.DomainMin.Z - b.DomainMin.Z) <= tolerance &&
                   Math.Abs(a.DomainMax.X - b.DomainMax.X) <= tolerance && Math.Abs(a.DomainMax.Y - b.DomainMax.Y) <= tolerance && Math.Abs(a.DomainMax.Z - b.DomainMax.Z) <= tolerance;
        }

        /// <summary>Whether two entities are placed the same: equal 'position' parameters, or neither has one.</summary>
        private static bool SamePlace(Entity a, Entity b)
        {
            ParameterData pa = a?.GetParameter(ShortGuids.position)?.content, pb = b?.GetParameter(ShortGuids.position)?.content;
            return (pa == null && pb == null) || (pa != null && pb != null && pa == pb);
        }

        /// <summary>
        /// A composite's ModelReferences by name, and its collision pairs: which ballistic half is whose. A half
        /// (<see cref="IsHalfShaped"/>) named "&lt;name&gt;_&lt;id&gt;_Ballistic" is the entity's with that id; one named
        /// "&lt;name&gt;_Ballistic" is the ModelReference of that name with a collision row (the one at its place when several
        /// are). A half no name places - its entity renamed since - is the one WORLD-row ModelReference at its place that no
        /// other half has; a half with none is an orphan (its entity is gone). An entity has at most one half.
        /// </summary>
        public sealed class Names
        {
            private readonly Commands _commands;
            private readonly Composite _composite;
            private readonly Dictionary<string, List<FunctionEntity>> _byName = new Dictionary<string, List<FunctionEntity>>(StringComparer.Ordinal);
            private readonly Dictionary<FunctionEntity, string> _names = new Dictionary<FunctionEntity, string>();
            private Dictionary<FunctionEntity, FunctionEntity> _halfOf, _entityOf;
            private List<FunctionEntity> _orphans;

            public Names(Commands commands, Composite composite)
            {
                _commands = commands;
                _composite = composite;
                foreach (FunctionEntity entity in composite.functions)
                {
                    if (!IsModelReference(entity)) continue;
                    string name = _commands.Utils.GetEntityName(_composite, entity) ?? "";
                    _names[entity] = name;
                    if (!_byName.TryGetValue(name, out List<FunctionEntity> list))
                        _byName[name] = list = new List<FunctionEntity>();
                    list.Add(entity);
                }
            }

            public string NameOf(Entity entity) => entity is FunctionEntity function && _names.TryGetValue(function, out string known) ? known : _commands.Utils.GetEntityName(_composite, entity) ?? "";
            /// <summary>Whether another ModelReference that is not a ballistic half has this name.</summary>
            public bool Shared(string name) => _byName.TryGetValue(name, out List<FunctionEntity> list) && list.Count(o => !IsHalf(o)) > 1;
            public FunctionEntity Find(string name, Entity except) => _byName.TryGetValue(name, out List<FunctionEntity> list) ? list.FirstOrDefault(o => !ReferenceEquals(o, except)) : null;
            public IEnumerable<FunctionEntity> Named(string name) => _byName.TryGetValue(name, out List<FunctionEntity> list) ? list : Enumerable.Empty<FunctionEntity>();

            private bool IsHalf(FunctionEntity entity) => IsHalfShaped(entity, NameOf(entity));

            /// <summary>The ballistic half of <paramref name="entity"/>'s pair, or null.</summary>
            public FunctionEntity HalfOf(FunctionEntity entity) { Resolve(); return entity != null && _halfOf.TryGetValue(entity, out FunctionEntity half) ? half : null; }
            /// <summary>When <paramref name="half"/> is a pair's ballistic half, its entity; otherwise null.</summary>
            public FunctionEntity EntityOf(FunctionEntity half) { Resolve(); return half != null && _entityOf.TryGetValue(half, out FunctionEntity entity) ? entity : null; }
            /// <summary>Every pair in the composite: entity, half.</summary>
            public List<(FunctionEntity entity, FunctionEntity half)> Pairs { get { Resolve(); return _halfOf.Select(o => (o.Key, o.Value)).ToList(); } }
            /// <summary>Halves whose entity is gone.</summary>
            public IReadOnlyList<FunctionEntity> Orphans { get { Resolve(); return _orphans; } }

            private void Resolve()
            {
                if (_halfOf != null)
                    return;
                _halfOf = new Dictionary<FunctionEntity, FunctionEntity>();
                _entityOf = new Dictionary<FunctionEntity, FunctionEntity>();
                _orphans = new List<FunctionEntity>();
                List<FunctionEntity> halves = _names.Where(o => IsHalfShaped(o.Key, o.Value)).Select(o => o.Key).ToList();
                if (halves.Count == 0)
                    return;
                HashSet<FunctionEntity> halfSet = new HashSet<FunctionEntity>(halves);
                bool CanOwn(FunctionEntity entity) => entity != null && !halfSet.Contains(entity) && !_halfOf.ContainsKey(entity) && RowOf(entity) != null;
                void Claim(FunctionEntity entity, FunctionEntity half) { _halfOf[entity] = half; _entityOf[half] = entity; }
                string Stem(FunctionEntity half) => NameOf(half).Substring(0, NameOf(half).Length - SiblingSuffix.Length);

                //"<name>_<id>_Ballistic": the entity with that id, of that name
                Dictionary<string, FunctionEntity> byTag = null;
                foreach (FunctionEntity half in halves)
                {
                    string stem = Stem(half);
                    int cut = stem.LastIndexOf('_');
                    if (cut <= 0 || stem.Length - cut - 1 != 8)
                        continue;
                    byTag = byTag ?? _names.Keys.ToDictionary(o => IdTag(o), o => o, StringComparer.OrdinalIgnoreCase);
                    if (byTag.TryGetValue(stem.Substring(cut + 1), out FunctionEntity tagged) && CanOwn(tagged) && NameOf(tagged) == stem.Substring(0, cut))
                        Claim(tagged, half);
                }
                //"<name>_Ballistic": the entity of that name - at the half's place, when several are
                foreach (FunctionEntity half in halves)
                {
                    if (_entityOf.ContainsKey(half)) continue;
                    List<FunctionEntity> named = Named(Stem(half)).Where(CanOwn).ToList();
                    if (named.Count != 0)
                        Claim(named.FirstOrDefault(o => SamePlace(o, half)) ?? named[0], half);
                }
                //No entity has its name: the one WORLD-row ModelReference at its place no half has (renamed since the pair was made)
                foreach (FunctionEntity half in halves)
                {
                    if (_entityOf.ContainsKey(half)) continue;
                    List<FunctionEntity> placed = _names.Keys.Where(o => CanOwn(o) && IsWorldOnly(RowOf(o).Flags) && SamePlace(o, half)).ToList();
                    //Several there (a cell's collision all sits at its origin): the one whose proxy has the half's bounds
                    if (placed.Count > 1)
                        placed = placed.Where(o => SameBounds(RowOf(o).CollisionProxy, RowOf(half).CollisionProxy)).ToList();
                    if (placed.Count == 1)
                        Claim(placed[0], half);
                    else
                        _orphans.Add(half);
                }
            }
        }

        /// <summary>
        /// The name an entity's ballistic half has: "&lt;name&gt;_Ballistic", with the entity's id in it when another
        /// ModelReference of the composite has the same name (so each keeps its own).
        /// </summary>
        public static string SiblingName(Commands commands, Composite composite, FunctionEntity original, Names names = null)
        {
            names = names ?? new Names(commands, composite);
            string name = names.NameOf(original);
            if (names.Shared(name))
                name += "_" + IdTag(original);
            return name + SiblingSuffix;
        }

        /// <summary>The ballistic half of an entity's collision pair, or null.</summary>
        public static FunctionEntity FindSibling(Commands commands, Composite composite, FunctionEntity original, Names names = null)
        {
            if (commands == null || composite == null || !IsModelReference(original))
                return null;
            names = names ?? new Names(commands, composite);
            return names.HalfOf(original);
        }

        /// <summary>When <paramref name="sibling"/> is the ballistic half of another entity's pair, that entity; otherwise null.</summary>
        public static FunctionEntity FindOriginal(Commands commands, Composite composite, FunctionEntity sibling, Names names = null)
        {
            if (commands == null || composite == null || !IsModelReference(sibling))
                return null;
            names = names ?? new Names(commands, composite);
            return names.EntityOf(sibling);
        }

        /// <summary>Parameters the ballistic half copies from its entity: everything (placement, the collision switches) but its name and resources.</summary>
        private static bool Copied(ShortGuid parameter) => parameter != ShortGuids.name && parameter != ShortGuids.resource;
        #endregion

        #region Making the pair
        /// <summary>What a pair change did.</summary>
        public sealed class Outcome
        {
            /// <summary>The ballistic half (after a removal: the entity that was removed).</summary>
            public FunctionEntity Sibling;
            public bool Created, Updated, Removed;
            /// <summary>The ballistic half's row (its collision template).</summary>
            public COLLISION_MAPPING Row;
            /// <summary>The proxy the ballistic row uses, and whether it was made for it now.</summary>
            public HavokPackfile.StaticCompoundShape Twin;
            public bool TwinCreated;
            public List<string> Notes = new List<string>();
            /// <summary>The halves of other entities sharing the row that was changed (<see cref="Normalise"/>): each has its own.</summary>
            public List<Outcome> AlsoPaired = new List<Outcome>();
        }

        /// <summary>
        /// Give <paramref name="original"/>'s collision its ballistic half, or bring the half it has in step: a sibling ModelReference
        /// (made when there is none) with the entity's parameters bar its name and resources, holding a BALLISTIC row with the same
        /// physics material and mapping, on the BALLISTICS twin of <paramref name="worldRow"/>'s proxy. <paramref name="worldRow"/>
        /// is the entity's own (WORLD) row as it now is. Nothing changes when the half is already as it should be.
        /// </summary>
        public static Outcome SyncSibling(Level level, Composite composite, FunctionEntity original, COLLISION_MAPPING worldRow, IWriter writer)
        {
            Commands commands = level.Commands;
            Names names = new Names(commands, composite);
            string name = SiblingName(commands, composite, original, names);
            FunctionEntity sibling = names.HalfOf(original);
            Outcome outcome = new Outcome() { Sibling = sibling };

            HavokPackfile.StaticCompoundShape world = worldRow.CollisionProxy;
            HavokPackfile.StaticCompoundShape twin = world == null ? null : ProxyFor(level, world, BallisticsFilter, UserData(level, worldRow.Material, world), writer, out outcome.TwinCreated);
            outcome.Twin = twin;

            COLLISION_MAPPING current = RowOf(sibling);
            COLLISION_MAPPING row = new COLLISION_MAPPING()
            {
                Flags = BallisticHalf(worldRow.Flags),
                CollisionProxy = twin,
                Material = worldRow.Material,
                MaterialMapping = worldRow.MaterialMapping,
                ResourceGUID = current?.ResourceGUID ?? ShortGuid.Invalid,
                Entity = new EntityHandle(),
                ZoneID = ShortGuid.Invalid,
            };

            if (sibling == null)
            {
                sibling = writer.BeginAdd(composite);
                foreach (Parameter parameter in original.parameters)
                    if (Copied(parameter.name) && parameter.content != null)
                        sibling.parameters.Add(new Parameter(parameter.name, (ParameterData)parameter.content.Clone(), parameter.variant));
                commands.Utils.SetEntityName(sibling, name);
                cResource resource = new cResource(sibling.shortGUID);
                resource.value.Add(new ResourceReference(ResourceType.COLLISION_MAPPING) { resource_id = sibling.shortGUID, CollisionMapping = row });
                sibling.AddParameter(ShortGuids.resource, resource, ParameterVariant.INTERNAL);
                writer.EndAdd(composite, sibling, row);
                outcome.Sibling = sibling;
                outcome.Created = true;
                outcome.Row = row;
                return outcome;
            }

            //The half there already: its placement and switches follow the entity's, and its row the entity's row
            outcome.Updated = SyncParameters(composite, original, sibling, writer);
            if (current == null || !SameRow(current, row))
            {
                writer.SetCollision(composite, sibling, row);
                outcome.Updated = true;
                outcome.Row = row;
            }
            else
                outcome.Row = current;
            return outcome;
        }

        /// <summary>The half's parameters made the entity's again (bar its name and resources). True when any changed.</summary>
        private static bool SyncParameters(Composite composite, FunctionEntity original, FunctionEntity sibling, IWriter writer)
        {
            bool changed = false;
            foreach (Parameter parameter in original.parameters)
            {
                if (!Copied(parameter.name) || parameter.content == null)
                    continue;
                Parameter mine = sibling.GetParameter(parameter.name);
                if (mine == null || mine.content == null || mine.content != parameter.content)
                {
                    writer.SetParameter(composite, sibling, parameter.name, (ParameterData)parameter.content.Clone(), parameter.variant);
                    changed = true;
                }
            }
            foreach (Parameter mine in sibling.parameters.ToList())
            {
                if (Copied(mine.name) && original.GetParameter(mine.name) == null)
                {
                    writer.RemoveParameter(composite, sibling, mine.name);
                    changed = true;
                }
            }
            return changed;
        }

        #region Keeping halves in step (each save)
        /// <summary>The methods a half follows its entity in: the entity's relay of each is linked to the half's method.</summary>
        private static readonly string[] FollowedMethods = { "hide", "show", "enable", "disable", "attach", "detach" };
        private static readonly ShortGuid Attachment = ShortGuidUtils.Generate("attachment");

        /// <summary>What <see cref="KeepHalvesInStep"/> did.</summary>
        public sealed class StepReport
        {
            public int Pairs, Parameters, LinksAdded, LinksRemoved, AliasesAdded, AliasesChanged, AliasesRemoved, AnimationBindings, Renamed, OrphansRemoved, PagesRedrawn;
            /// <summary>Composites whose script it changed.</summary>
            public HashSet<Composite> Changed = new HashSet<Composite>();
            public List<string> Notes = new List<string>();
            public bool Any => Parameters + LinksAdded + LinksRemoved + AliasesAdded + AliasesChanged + AliasesRemoved + AnimationBindings + Renamed + OrphansRemoved != 0;

            public override string ToString() => Pairs + " pair(s): parameters of " + Parameters + " half(s) set again, links +" + LinksAdded + " -" + LinksRemoved + ", aliases +" + AliasesAdded + " ~" + AliasesChanged + " -" + AliasesRemoved + ", animation bindings " + AnimationBindings +
                ", " + Renamed + " renamed, " + OrphansRemoved + " orphan(s) removed, " + PagesRedrawn + " composite(s) redrawn";
        }

        /// <summary>
        /// Before a save: every pair's ballistic half made to follow its entity again (see the class summary), so whatever was
        /// done to the entity since - moved, switched off, hidden or attached by script, overridden by an alias, renamed,
        /// deleted - reaches the half in game. Idempotent: a second run changes nothing, and nothing is ever added twice. Made in
        /// place with no undo record, as the save's other fix-ups are: what the half holds is its entity's, not edited on its
        /// own. Where a composite's links are kept on flowgraph pages, the pages draw the links it adds (the open composite is
        /// built again from them once the save is done).
        /// </summary>
        public static StepReport KeepHalvesInStep(Level level)
        {
            StepReport report = new StepReport();
            Commands commands = level?.Commands;
            if (commands == null)
                return report;
            IWriter writer = Direct(level);

            //A half is named "..._Ballistic" on its own 'name' parameter: only composites holding one are looked at closely
            List<Names> holding = new List<Names>();
            List<Composite> composites = new List<Composite>();
            foreach (Composite composite in commands.Entries)
            {
                if (composite == null) continue;
                if (!composite.functions.Any(o => IsModelReference(o) && (CommandsUtils.GetEntityNameParameter(o)?.EndsWith(SiblingSuffix, StringComparison.Ordinal) ?? false)))
                    continue;
                Names names = new Names(commands, composite);
                if (names.Pairs.Count == 0 && names.Orphans.Count == 0)
                    continue;
                holding.Add(names);
                composites.Add(composite);
            }
            if (holding.Count == 0)
                return report;

            //The flowgraph pages are the editor's: kept in step only in the editor, with this level's pages loaded
            bool pages = Singleton.Editor != null && ReferenceEquals(FlowgraphLayoutManager.LinkedCommands, commands);
            Dictionary<Composite, PageMode> modes = new Dictionary<Composite, PageMode>();
            HashSet<Composite> relinked = new HashSet<Composite>();
            void BeforeLinks(Composite composite)
            {
                if (pages && !modes.ContainsKey(composite))
                    modes[composite] = PageModeOf(commands, composite);
                relinked.Add(composite);
                report.Changed.Add(composite);
            }

            HashSet<ShortGuid> pairIds = new HashSet<ShortGuid>(holding.SelectMany(o => o.Pairs.SelectMany(p => new[] { p.entity.shortGUID, p.half.shortGUID }).Concat(o.Orphans.Select(h => h.shortGUID))));
            AliasIndex aliases = AliasIndex.Build(commands, pairIds);
            AnimationIndex animations = AnimationIndex.Build(commands, pairIds);
            List<(ShortGuid relay, ShortGuid method)> followed = FollowedMethods.Select(o => (commands.Utils.GetRelay(ShortGuidUtils.Generate(o)), ShortGuidUtils.Generate(o))).Where(o => o.Item1 != ShortGuid.Invalid).ToList();

            for (int i = 0; i < holding.Count; i++)
            {
                Names names = holding[i];
                Composite composite = composites[i];
                foreach (FunctionEntity orphan in names.Orphans)
                {
                    BeforeLinks(composite);
                    report.Notes.Add(composite.name + " :: " + names.NameOf(orphan) + " removed: the entity it was the ballistic half of is gone");
                    RemoveDirect(composite, orphan, aliases, animations);
                    report.OrphansRemoved++;
                }
                foreach ((FunctionEntity original, FunctionEntity half) in names.Pairs)
                {
                    report.Pairs++;
                    string wanted = SiblingName(commands, composite, original, names);
                    if (names.NameOf(half) != wanted)
                    {
                        report.Notes.Add(composite.name + " :: " + names.NameOf(half) + " renamed " + wanted + ", after its entity");
                        commands.Utils.SetEntityName(half, wanted);
                        report.Renamed++;
                        report.Changed.Add(composite);
                    }
                    if (SyncParameters(composite, original, half, writer))
                    {
                        report.Parameters++;
                        report.Changed.Add(composite);
                    }
                    KeepLinks(composite, original, half, followed, report, () => BeforeLinks(composite));
                    KeepMovers(composite, original, half, report, () => BeforeLinks(composite));
                    KeepAliases(composite, original, half, aliases, report);
                    KeepAnimations(commands, composite, original, half, animations, report);
                }
            }

            if (pages)
                RedrawPages(commands, modes, relinked, report);
            //The composite on screen holds its links in its page windows, built before this ran: built again after the save
            CompositeDisplay display = pages ? Singleton.Editor?.CompositeDisplay : null;
            if (display != null && display.Composite != null && report.Changed.Contains(display.Composite))
                ReloadAfterSave(display.Composite);
            if (report.Any)
                Debug.Log("Collision pairs", "Kept the ballistic halves in step before saving: " + report + (report.Notes.Count != 0 ? "\n  " + string.Join("\n  ", report.Notes.Take(20)) : ""));
            return report;
        }

        /// <summary>
        /// The entity's relay links to its half (one of each - added when missing, extras removed) and the half's attachment
        /// links as the entity's are. Links of the composite are replaced as lists, never changed in place (undo records hold them).
        /// </summary>
        private static void KeepLinks(Composite composite, FunctionEntity original, FunctionEntity half, List<(ShortGuid relay, ShortGuid method)> followed, StepReport report, Action changing)
        {
            List<EntityConnector> links = original.childLinks;
            bool changed = false;
            foreach ((ShortGuid relay, ShortGuid method) in followed)
            {
                List<int> at = new List<int>();
                for (int i = 0; i < links.Count; i++)
                    if (links[i].linkedEntityID == half.shortGUID && links[i].thisParamID == relay && links[i].linkedParamID == method)
                        at.Add(i);
                if (at.Count == 1)
                    continue;
                if (!changed) { links = new List<EntityConnector>(links); changed = true; }
                if (at.Count == 0)
                {
                    links.Add(new EntityConnector(half.shortGUID, relay, method));
                    report.LinksAdded++;
                }
                else
                {
                    //A copy of a link counts once: the extras go
                    EntityConnector keep = links[at[0]];
                    int removed = links.RemoveAll(o => o.linkedEntityID == half.shortGUID && o.thisParamID == relay && o.linkedParamID == method);
                    links.Insert(Math.Min(at[0], links.Count), keep);
                    report.LinksRemoved += removed - 1;
                }
            }
            if (changed)
            {
                changing();
                original.childLinks = links;
            }

            //Attached as the entity is (its 'attachment' pin links to what it hangs from): the half hangs from the same
            List<(ShortGuid entity, ShortGuid parameter)> wanted = original.childLinks.Where(o => o.thisParamID == Attachment).Select(o => (o.linkedEntityID, o.linkedParamID)).Distinct().ToList();
            List<(ShortGuid entity, ShortGuid parameter)> have = half.childLinks.Where(o => o.thisParamID == Attachment).Select(o => (o.linkedEntityID, o.linkedParamID)).ToList();
            if (wanted.Count == have.Count && have.Distinct().Count() == have.Count && wanted.All(o => have.Contains(o)))
                return;
            changing();
            //One of each it hangs from: copies and ones the entity no longer has go, missing ones come
            List<EntityConnector> halfLinks = new List<EntityConnector>();
            HashSet<(ShortGuid, ShortGuid)> seen = new HashSet<(ShortGuid, ShortGuid)>();
            foreach (EntityConnector link in half.childLinks)
                if (link.thisParamID != Attachment || (wanted.Contains((link.linkedEntityID, link.linkedParamID)) && seen.Add((link.linkedEntityID, link.linkedParamID))))
                    halfLinks.Add(link);
            report.LinksRemoved += half.childLinks.Count - halfLinks.Count;
            foreach ((ShortGuid entity, ShortGuid parameter) in wanted.Where(o => !seen.Contains(o)))
            {
                halfLinks.Add(new EntityConnector(entity, Attachment, parameter));
                report.LinksAdded++;
            }
            half.childLinks = halfLinks;
        }

        /// <summary>
        /// Aliases overriding the entity, anywhere in the level, each with a copy over the half (the same path to it, the same
        /// parameters but its name and resources); copies whose alias is gone, or overrides nothing now, are removed.
        /// </summary>
        private static void KeepAliases(Composite composite, FunctionEntity original, FunctionEntity half, AliasIndex aliases, StepReport report)
        {
            List<(Composite holder, AliasEntity alias)> overHalf = aliases.Over(composite, half);
            HashSet<AliasEntity> kept = new HashSet<AliasEntity>();
            foreach ((Composite holder, AliasEntity alias) in aliases.Over(composite, original))
            {
                List<Parameter> copied = alias.parameters.Where(o => Copied(o.name) && o.content != null).ToList();
                ShortGuid[] path = alias.alias?.path;
                int last = path == null ? -1 : (path.Length != 0 && path[path.Length - 1] == ShortGuid.Invalid ? path.Length - 2 : path.Length - 1);
                if (copied.Count == 0 || last < 0 || path[last] != original.shortGUID)
                    continue;
                ShortGuid[] halfPath = (ShortGuid[])path.Clone();
                halfPath[last] = half.shortGUID;
                AliasEntity mirror = overHalf.FirstOrDefault(o => o.holder == holder && !kept.Contains(o.alias) && o.alias.alias?.path != null && o.alias.alias.path.SequenceEqual(halfPath)).alias;
                bool changed = false;
                if (mirror == null)
                {
                    mirror = holder.AddAlias(new AliasEntity(halfPath));
                    string name = CommandsUtils.GetEntityNameParameter(alias);
                    if (name != null)
                        mirror.AddParameter(ShortGuids.name, new cString(name + SiblingSuffix), ParameterVariant.PARAMETER);
                    report.AliasesAdded++;
                    report.Changed.Add(holder);
                }
                kept.Add(mirror);
                foreach (Parameter parameter in copied)
                {
                    Parameter mine = mirror.GetParameter(parameter.name);
                    if (mine != null && mine.content != null && mine.content == parameter.content)
                        continue;
                    mirror.AddParameter(parameter.name, (ParameterData)parameter.content.Clone(), parameter.variant);
                    changed = true;
                }
                foreach (Parameter mine in mirror.parameters.ToList())
                {
                    if (Copied(mine.name) && alias.GetParameter(mine.name) == null)
                    {
                        mirror.RemoveParameter(mine.name);
                        changed = true;
                    }
                }
                if (changed && overHalf.Any(o => o.alias == mirror))
                {
                    report.AliasesChanged++;
                    report.Changed.Add(holder);
                }
            }
            //A copy over the half whose alias is gone: removed, unless something links to it (that is someone's own)
            foreach ((Composite holder, AliasEntity alias) in overHalf)
            {
                if (kept.Contains(alias) || holder.GetEntities().Any(o => o.childLinks.Any(l => l.linkedEntityID == alias.shortGUID)) || alias.childLinks.Count != 0)
                    continue;
                holder.RemoveAlias(alias.shortGUID);
                report.AliasesRemoved++;
                report.Changed.Add(holder);
            }
        }

        /* What moves an entity at runtime, by the pin of the mover that names the moved thing (its 'reference'). Retail moves
           collision this way only on BALLISTIC-row ModelReferences and composite instances - never a WORLD row (8 levels: 0 of
           the mover and CAGEAnimation position bindings land on one) - so a walkable surface that moves belongs in a composite
           whose instance is moved, where both halves go with it. A mover on the pair's entity is mirrored onto the half, so the
           ballistic half (bullets, footsteps, zones) at least goes where the entity is driven. */
        private static readonly (string function, string pin)[] MoverPins =
        {
            ("RotateAtSpeed", "Result"), ("RotateInTime", "Result"), ("MoveInTime", "result"), ("SmoothMove", "result"), ("PlayEnvironmentAnimation", "geometry"),
        };
        private static readonly ShortGuid ReferencePin = ShortGuidUtils.Generate("reference");

        private static bool IsMoverPin(Entity owner, ShortGuid pin)
        {
            if (!(owner is FunctionEntity function) || !function.function.IsFunctionType)
                return false;
            string type = function.function.AsFunctionType.ToString();
            foreach ((string name, string p) in MoverPins)
                if (name == type && ShortGuidUtils.Generate(p) == pin)
                    return true;
            return false;
        }

        /// <summary>Each mover link into the entity's reference has one twin into the half's; mover links into the half with none on the entity go.</summary>
        private static void KeepMovers(Composite composite, FunctionEntity original, FunctionEntity half, StepReport report, Action changing)
        {
            foreach (Entity owner in composite.GetEntities())
            {
                if (owner.childLinks.Count == 0 || !(owner is FunctionEntity))
                    continue;
                List<ShortGuid> wanted = owner.childLinks.Where(o => o.linkedEntityID == original.shortGUID && o.linkedParamID == ReferencePin && IsMoverPin(owner, o.thisParamID)).Select(o => o.thisParamID).Distinct().ToList();
                List<EntityConnector> have = owner.childLinks.Where(o => o.linkedEntityID == half.shortGUID && o.linkedParamID == ReferencePin && IsMoverPin(owner, o.thisParamID)).ToList();
                if (wanted.Count == have.Count && have.Select(o => o.thisParamID).Distinct().Count() == have.Count && have.All(o => wanted.Contains(o.thisParamID)))
                    continue;
                changing();
                HashSet<ShortGuid> seen = new HashSet<ShortGuid>();
                List<EntityConnector> links = new List<EntityConnector>();
                foreach (EntityConnector link in owner.childLinks)
                {
                    bool mine = link.linkedEntityID == half.shortGUID && link.linkedParamID == ReferencePin && IsMoverPin(owner, link.thisParamID);
                    if (!mine || (wanted.Contains(link.thisParamID) && seen.Add(link.thisParamID)))
                        links.Add(link);
                }
                report.LinksRemoved += owner.childLinks.Count - links.Count;
                foreach (ShortGuid pin in wanted.Where(o => !seen.Contains(o)))
                {
                    links.Add(new EntityConnector(half.shortGUID, pin, ReferencePin));
                    report.LinksAdded++;
                }
                owner.childLinks = links;
            }
        }

        /// <summary>CAGEAnimation bindings anywhere in the level, by what they resolve to (only those ending at one of a set of entities).</summary>
        private sealed class AnimationIndex
        {
            private readonly Dictionary<(Composite, Entity), List<(Composite holder, CAGEAnimation animation, CAGEAnimation.Connection connection)>> _bound = new Dictionary<(Composite, Entity), List<(Composite, CAGEAnimation, CAGEAnimation.Connection)>>();

            public static AnimationIndex Build(Commands commands, HashSet<ShortGuid> targets)
            {
                AnimationIndex index = new AnimationIndex();
                foreach (Composite holder in commands.Entries)
                {
                    if (holder == null) continue;
                    foreach (FunctionEntity function in holder.functions)
                    {
                        if (!(function is CAGEAnimation animation) || animation.connections == null) continue;
                        foreach (CAGEAnimation.Connection connection in animation.connections)
                        {
                            ShortGuid[] path = connection?.connectedEntity?.path;
                            if (path == null || path.Length == 0) continue;
                            int last = path[path.Length - 1] == ShortGuid.Invalid ? path.Length - 2 : path.Length - 1;
                            if (last < 0 || !targets.Contains(path[last])) continue;
                            List<Tuple<Composite, Entity>> resolved = commands.Utils.ResolveEntityPath(connection.connectedEntity, holder);
                            if (resolved == null || resolved.Count == 0) continue;
                            (Composite, Entity) key = (resolved[resolved.Count - 1].Item1, resolved[resolved.Count - 1].Item2);
                            if (!index._bound.TryGetValue(key, out var list))
                                index._bound[key] = list = new List<(Composite, CAGEAnimation, CAGEAnimation.Connection)>();
                            list.Add((holder, animation, connection));
                        }
                    }
                }
                return index;
            }

            public List<(Composite holder, CAGEAnimation animation, CAGEAnimation.Connection connection)> On(Composite composite, Entity entity) => _bound.TryGetValue((composite, entity), out var list) ? list.ToList() : new List<(Composite, CAGEAnimation, CAGEAnimation.Connection)>();
        }

        private static readonly ShortGuid PositionParameter = ShortGuidUtils.Generate("position");

        /// <summary>
        /// A CAGEAnimation track moving the entity (its position) moves the half too: the same track bound to the half by a second
        /// connection (retail binds one track to two entities 59 times on two levels). Bindings of the half with none on the entity go.
        /// </summary>
        private static void KeepAnimations(Commands commands, Composite composite, FunctionEntity original, FunctionEntity half, AnimationIndex animations, StepReport report)
        {
            List<(Composite holder, CAGEAnimation animation, CAGEAnimation.Connection connection)> onHalf = animations.On(composite, half).Where(o => o.connection.target_param == PositionParameter).ToList();
            HashSet<CAGEAnimation.Connection> kept = new HashSet<CAGEAnimation.Connection>();
            foreach ((Composite holder, CAGEAnimation animation, CAGEAnimation.Connection connection) in animations.On(composite, original))
            {
                if (connection.target_param != PositionParameter)
                    continue;
                ShortGuid[] path = connection.connectedEntity.path;
                int last = path[path.Length - 1] == ShortGuid.Invalid ? path.Length - 2 : path.Length - 1;
                if (last < 0 || path[last] != original.shortGUID)
                    continue;
                ShortGuid[] halfPath = (ShortGuid[])path.Clone();
                halfPath[last] = half.shortGUID;
                (Composite, CAGEAnimation, CAGEAnimation.Connection) twin = onHalf.FirstOrDefault(o => o.animation == animation && !kept.Contains(o.connection) && o.connection.target_track == connection.target_track
                    && o.connection.target_sub_param == connection.target_sub_param && o.connection.connectedEntity.path.SequenceEqual(halfPath));
                if (twin.Item3 != null)
                {
                    kept.Add(twin.Item3);
                    continue;
                }
                CAGEAnimation.Connection copy = new CAGEAnimation.Connection()
                {
                    binding_guid = ShortGuidUtils.GenerateRandom(),
                    target_track = connection.target_track,
                    binding_type = connection.binding_type,
                    target_param = connection.target_param,
                    target_param_type = connection.target_param_type,
                    target_sub_param = connection.target_sub_param,
                    connectedEntity = new EntityPath(halfPath),
                };
                //A new list: the one the animation holds may be an undo step's own record of it
                animation.connections = new List<CAGEAnimation.Connection>(animation.connections) { copy };
                kept.Add(copy);
                report.AnimationBindings++;
                report.Changed.Add(holder);
            }
            foreach ((Composite holder, CAGEAnimation animation, CAGEAnimation.Connection connection) in onHalf)
            {
                if (kept.Contains(connection))
                    continue;
                animation.connections = animation.connections.Where(o => !ReferenceEquals(o, connection)).ToList();
                report.AnimationBindings++;
                report.Changed.Add(holder);
            }
        }

        /// <summary>A half whose entity is gone taken out, with the links into it and the aliases over it (and the links into those).</summary>
        private static void RemoveDirect(Composite composite, FunctionEntity entity, AliasIndex aliases, AnimationIndex animations)
        {
            composite.RemoveFunction(entity.shortGUID);
            RemoveLinksInto(composite, entity.shortGUID);
            foreach ((Composite holder, CAGEAnimation animation, CAGEAnimation.Connection connection) in animations.On(composite, entity))
                animation.connections = animation.connections.Where(o => !ReferenceEquals(o, connection)).ToList();
            foreach ((Composite holder, AliasEntity alias) in aliases.Over(composite, entity))
            {
                holder.RemoveAlias(alias.shortGUID);
                RemoveLinksInto(holder, alias.shortGUID);
            }
        }

        private static void RemoveLinksInto(Composite composite, ShortGuid id)
        {
            foreach (Entity other in composite.GetEntities())
                if (other.childLinks.Any(o => o.linkedEntityID == id))
                    other.childLinks = other.childLinks.Where(o => o.linkedEntityID != id).ToList();
        }

        /// <summary>The aliases anywhere in the level whose path ends at one of a set of entities, by what they resolve to.</summary>
        private sealed class AliasIndex
        {
            private readonly Dictionary<(Composite, Entity), List<(Composite, AliasEntity)>> _over = new Dictionary<(Composite, Entity), List<(Composite, AliasEntity)>>();

            public static AliasIndex Build(Commands commands, HashSet<ShortGuid> targets)
            {
                AliasIndex index = new AliasIndex();
                foreach (Composite holder in commands.Entries)
                {
                    if (holder == null) continue;
                    foreach (AliasEntity alias in holder.aliases)
                    {
                        ShortGuid[] path = alias?.alias?.path;
                        if (path == null || path.Length == 0) continue;
                        int last = path[path.Length - 1] == ShortGuid.Invalid ? path.Length - 2 : path.Length - 1;
                        if (last < 0 || !targets.Contains(path[last])) continue;
                        List<Tuple<Composite, Entity>> resolved = commands.Utils.ResolveAlias(alias, holder);
                        if (resolved == null || resolved.Count == 0) continue;
                        (Composite, Entity) key = (resolved[resolved.Count - 1].Item1, resolved[resolved.Count - 1].Item2);
                        if (!index._over.TryGetValue(key, out List<(Composite, AliasEntity)> list))
                            index._over[key] = list = new List<(Composite, AliasEntity)>();
                        list.Add((holder, alias));
                    }
                }
                return index;
            }

            public List<(Composite holder, AliasEntity alias)> Over(Composite composite, Entity entity) => _over.TryGetValue((composite, entity), out List<(Composite, AliasEntity)> list) ? list.ToList() : new List<(Composite, AliasEntity)>();
        }

        /// <summary>How a composite's links are held in the editor: drawn on its pages (Carry), data only (its pages do not match: shown as links), or no pages yet (Generate).</summary>
        private enum PageMode { Carry, DataOnly, Generate }

        /// <summary>As the editor will judge the composite, read before its links change (MCP's ModeOf, from the same tables).</summary>
        private static PageMode PageModeOf(Commands commands, Composite composite)
        {
            if (FlowgraphLayoutManager.HasCompatibilityInfo(composite))
                return FlowgraphLayoutManager.IsCompatible(composite) ? PageMode.Carry : PageMode.DataOnly;
            if (FlowgraphLayoutManager.HasLayout(composite))
            {
                List<FlowgraphMeta> judged = FlowgraphLayoutManager.GetLayouts(composite).Select(o => o.Copy()).ToList();
                FlowgraphLayoutManager.TrimToComposite(judged, composite, out int _, out int _);
                return RefactorPages.PagesMatchLinks(composite, judged) ? PageMode.Carry : PageMode.DataOnly;
            }
            //Never opened, no pages: with no links it would open supported on an empty page; with links of its own, as links
            return commands.Utils.CountLinks(composite) == 0 ? PageMode.Generate : PageMode.DataOnly;
        }

        /// <summary>The pages of each composite whose links changed drawn again, so they show (and the next page compile keeps) the links added.</summary>
        private static void RedrawPages(Commands commands, Dictionary<Composite, PageMode> modes, HashSet<Composite> relinked, StepReport report)
        {
            foreach (Composite composite in relinked)
            {
                if (!modes.TryGetValue(composite, out PageMode mode) || mode == PageMode.DataOnly)
                    continue;
                try
                {
                    List<FlowgraphMeta> current = mode == PageMode.Carry ? FlowgraphLayoutManager.GetLayouts(composite).Select(o => o.Copy()).ToList() : new List<FlowgraphMeta>();
                    FlowgraphLayoutManager.TrimToComposite(current, composite, out int _, out int _);
                    string pageName = current.Count != 0 ? (FlowgraphLayoutManager.GetSelectedPage(composite) is string selected && current.Any(o => o.Name == selected) ? selected : current[0].Name) : EditorUtils.GetCompositeName(composite);
                    List<FlowgraphMeta> drawn = RefactorPages.DrawLinks(composite, current, pageName, commands);
                    if (drawn.Count == 0)
                        drawn.Add(new FlowgraphMeta() { CompositeGUID = composite.shortGUID, Name = pageName, CanvasScale = 1f });
                    FlowgraphLayoutManager.RemoveAllLayouts(composite);
                    foreach (FlowgraphMeta page in drawn)
                        FlowgraphLayoutManager.AddLayout(page);
                    report.PagesRedrawn++;
                }
                catch (Exception e)
                {
                    //The links are in the script either way: a page that could not be drawn only leaves the composite shown as links
                    Debug.Log("Collision pairs", "Could not draw the links added to " + composite.name + " on its pages: " + e);
                }
            }
        }

        private static Composite _reloadAfterSave;
        private static bool _savedHooked;

        /// <summary>Build the composite on screen again from its pages once the save is done (<see cref="Singleton.OnSaved"/>, UI thread).</summary>
        private static void ReloadAfterSave(Composite composite)
        {
            _reloadAfterSave = composite;
            if (_savedHooked)
                return;
            _savedHooked = true;
            Singleton.OnSaved += () =>
            {
                Composite reload = _reloadAfterSave;
                _reloadAfterSave = null;
                CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
                if (reload == null || display == null || display.IsDisposed || !display.Populated || display.Composite != reload)
                    return;
                display.ClearEntitySelection();
                display.Reload(true);
            };
        }
        #endregion

        #region Pairing what was made before pairs
        /// <summary>
        /// ModelReferences whose one collision row still asks for WORLD and BALLISTIC storage with a proxy (made before collision was
        /// made as pairs): the build makes each a WORLD row only, with no ballistic half. In <paramref name="only"/>, or the whole level.
        /// </summary>
        public static List<(Composite composite, FunctionEntity entity, COLLISION_MAPPING row)> FindUnpaired(Commands commands, Composite only = null)
        {
            List<(Composite, FunctionEntity, COLLISION_MAPPING)> found = new List<(Composite, FunctionEntity, COLLISION_MAPPING)>();
            if (commands == null)
                return found;
            foreach (Composite composite in only != null ? new[] { only } : commands.Entries.ToArray())
            {
                if (composite == null) continue;
                Names names = null;
                foreach (FunctionEntity entity in composite.functions)
                {
                    if (!IsModelReference(entity)) continue;
                    COLLISION_MAPPING row = RowOf(entity);
                    if (row?.CollisionProxy == null || !IsPair(row.Flags)) continue;
                    names = names ?? new Names(commands, composite);
                    if (names.EntityOf(entity) != null) continue;
                    found.Add((composite, entity, row));
                }
            }
            return found;
        }

        /// <summary>What <see cref="PairAll"/> did.</summary>
        public sealed class PairAllResult
        {
            public int Entities, HalvesCreated, HalvesUpdated, TwinsCopied;
            public List<string> Failures = new List<string>();
        }

        /// <summary>Give each of <paramref name="unpaired"/> (from <see cref="FindUnpaired"/>) its ballistic half, as the Resources dialog does.</summary>
        public static PairAllResult PairAll(Level level, List<(Composite composite, FunctionEntity entity, COLLISION_MAPPING row)> unpaired, IWriter writer)
        {
            PairAllResult result = new PairAllResult();
            RowUsers users = RowUsers.Of(level.Commands);
            foreach ((Composite composite, FunctionEntity entity, COLLISION_MAPPING row) in unpaired)
            {
                //A row several entities share is paired for all of them by the first
                if (!IsPair(row.Flags) && FindSibling(level.Commands, composite, entity) != null)
                    continue;
                try
                {
                    Outcome outcome = Normalise(level, composite, entity, row, writer, false, users);
                    if (outcome == null) continue;
                    foreach (Outcome one in new[] { outcome }.Concat(outcome.AlsoPaired))
                    {
                        result.Entities++;
                        if (one.Created) result.HalvesCreated++;
                        else if (one.Updated) result.HalvesUpdated++;
                        if (one.TwinCreated) result.TwinsCopied++;
                    }
                }
                catch (Exception e)
                {
                    result.Failures.Add(composite.name + " :: " + level.Commands.Utils.GetEntityName(composite, entity) + ": " + e.Message);
                }
            }
            return result;
        }

        //Levels whose user said not now this session
        private static readonly HashSet<string> _declined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Before the user's Save &amp; Build (UI thread): when the level still has collision made before pairs, offer once per level
        /// and session to give it its ballistic halves now - one undo step - rather than build it WORLD-only again. No builds it as it
        /// is (the build's warning names them; pair_collision does it later).
        /// </summary>
        public static void OfferBeforeBuild(IWin32Window owner, LevelContent content)
        {
            Level level = content?.Level;
            if (level?.Commands == null || _declined.Contains(level.Name))
                return;
            List<(Composite composite, FunctionEntity entity, COLLISION_MAPPING row)> unpaired = FindUnpaired(level.Commands);
            if (unpaired.Count == 0)
                return;
            int composites = unpaired.Select(o => o.composite).Distinct().Count();
            int proxies = unpaired.Select(o => o.row.CollisionProxy).Distinct().Count();
            string sample = string.Join("\n  ", unpaired.Take(3).Select(o => o.composite.name + " :: " + level.Commands.Utils.GetEntityName(o.composite, o.entity))) + (unpaired.Count > 3 ? "\n  ..." : "");
            string text = unpaired.Count + " collision entit" + (unpaired.Count == 1 ? "y" : "ies") + " in " + composites + " composite" + (composites == 1 ? "" : "s") +
                " ask for WORLD and BALLISTIC collision on one row, as OpenCAGE made it before November 2026:\n  " + sample + "\n\n" +
                "The game cannot use such a row: built as they are, they collide as WORLD only - walked on, but no bullet hits, footstep sounds or zones from them.\n\n" +
                "Give each its ballistic half now, as retail has it? Each gets a collision-only '<name>_Ballistic' ModelReference at its place holding the BALLISTIC row, " +
                "on a copy of its proxy (up to " + proxies + " new proxies). One undo step; the copied proxies stay in the level's collision files.\n\n" +
                "No builds them as they are, and is not asked again for this level until OpenCAGE restarts.";
            if (MessageBox.Show(owner, text, "Collision made before pairs", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                _declined.Add(level.Name);
                return;
            }
            string label = "Pair collision of " + UndoLabels.Count(unpaired.Count, "entity", "entities");
            Cursor previous = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            PairAllResult result;
            try
            {
                using (UndoStack.Current.BeginGroup(label))
                    result = PairAll(level, unpaired, Undoable(level, label));
            }
            finally
            {
                Cursor.Current = previous;
            }
            if (result.Failures.Count != 0)
                MessageBox.Show(owner, (result.Entities) + " were paired; " + result.Failures.Count + " could not be (they build WORLD-only):\n  " + string.Join("\n  ", result.Failures.Take(10)), "Collision made before pairs", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        #endregion

        #region Deleting, moving
        /// <summary>
        /// <paramref name="entities"/> of <paramref name="composite"/> with the ballistic half of each pair's entity among them added, for
        /// what moves entities to another composite (a half left behind would be an orphan, and its entity would lose its BALLISTIC row).
        /// </summary>
        public static List<Entity> WithHalves(Commands commands, Composite composite, IEnumerable<Entity> entities)
        {
            List<Entity> list = entities?.ToList() ?? new List<Entity>();
            if (commands == null || composite == null)
                return list;
            Names names = new Names(commands, composite);
            foreach (Entity entity in list.ToList())
                if (entity is FunctionEntity function && names.HalfOf(function) is FunctionEntity half && !list.Contains(half))
                    list.Add(half);
            return list;
        }

        /// <summary>
        /// The ballistic half <paramref name="entity"/>'s collision left behind when that collision was removed: the half named for it
        /// ("&lt;name&gt;_Ballistic") that no entity now owns. Null while the entity has collision, or when there is none.
        /// </summary>
        public static FunctionEntity HalfLeftBehind(Commands commands, Composite composite, Entity entity)
        {
            if (commands == null || composite == null || !(entity is FunctionEntity function) || !IsModelReference(function) || RowOf(function) != null)
                return null;
            Names names = new Names(commands, composite);
            string name = SiblingName(commands, composite, function, names);
            return names.Orphans.FirstOrDefault(o => names.NameOf(o) == name);
        }

        /// <summary>The half that goes when <paramref name="entity"/> is deleted: its ballistic half, when it is a pair's entity.</summary>
        public static FunctionEntity HalfGoingWith(Commands commands, Composite composite, Entity entity) => entity is FunctionEntity function && IsModelReference(function) ? FindSibling(commands, composite, function) : null;

        /// <summary>
        /// Delete an entity as the editor's Delete does - and when it is a collision pair's entity, its ballistic half with it, in the
        /// same undo step (undo brings both back, with the links between them). UI thread.
        /// </summary>
        public static void DeleteWithHalf(Commands commands, Composite composite, Entity entity, string label)
        {
            FunctionEntity half = HalfGoingWith(commands, composite, entity);
            using (UndoStack.Current.BeginGroup(label))
            {
                UndoStack.Current.Apply(new EntityDeleteEdit(composite, entity, label));
                if (half != null && composite.GetEntityByID(half.shortGUID) == half)
                    UndoStack.Current.Apply(new EntityDeleteEdit(composite, half, "Delete " + UndoLabels.Entity(composite, half)));
            }
        }
        #endregion

        /// <summary>Remove the ballistic half of <paramref name="original"/>'s pair (the whole entity). Null when it has none.</summary>
        public static Outcome RemoveSibling(Level level, Composite composite, FunctionEntity original, IWriter writer)
        {
            return RemoveHalf(composite, FindSibling(level.Commands, composite, original), writer);
        }

        /// <summary>
        /// Remove a half found before its entity's collision went (once the entity has no row, nothing names it its half any
        /// more). Null when <paramref name="half"/> is null or already gone.
        /// </summary>
        public static Outcome RemoveHalf(Composite composite, FunctionEntity half, IWriter writer)
        {
            if (half == null || composite.GetEntityByID(half.shortGUID) != half)
                return null;
            Outcome outcome = new Outcome() { Sibling = half, Row = RowOf(half), Removed = true };
            writer.Delete(composite, half);
            return outcome;
        }

        /// <summary>
        /// Bring an entity's collision row - <paramref name="row"/>, the live object, changed in place - to retail's shape, as the
        /// Resources dialog's Collision Mapping does after each change: "WORLD + BALLISTIC" (or WORLD with a ballistic half already
        /// there) becomes the WORLD row plus the half (<see cref="SyncSibling"/>); a row of one storage keeps it, on a proxy of its
        /// collision type. A row asking for both with no proxy yet is left as it is: the pair is made once it has one. The ballistic
        /// half's own row stays a BALLISTIC row. Every other ModelReference naming the same row object (the row is shared, and has
        /// just changed for them all) gets its own half too (<see cref="Outcome.AlsoPaired"/>). With <paramref name="materialChanged"/>
        /// (the row's physics material was just set) the row's proxy carries that material as userData, as retail's do - a copy of
        /// it when it carries another. Returns what happened to the half, or null when it was not touched.
        /// </summary>
        /// <param name="users">Who names which row, when the caller has it (<see cref="RowUsers.Of"/>); found here otherwise.</param>
        public static Outcome Normalise(Level level, Composite composite, FunctionEntity entity, COLLISION_MAPPING row, IWriter writer, bool materialChanged = false, RowUsers users = null)
        {
            if (level?.Commands == null || composite == null || row == null || !IsModelReference(entity))
                return null;
            Commands commands = level.Commands;
            Names names = new Names(commands, composite);
            CollisionFlags flagsBefore = row.Flags;
            HavokPackfile.StaticCompoundShape proxyBefore = row.CollisionProxy;
            bool half = FindOriginal(commands, composite, entity, names) != null;
            FunctionEntity sibling = half ? null : FindSibling(commands, composite, entity, names);
            bool pair = !half && (IsPair(row.Flags) || (sibling != null && IsWorldOnly(row.Flags)));
            if (pair && row.CollisionProxy == null && sibling == null)
                return null;

            if (pair)
                row.Flags = WorldHalf(row.Flags);
            else if (half && IsPair(row.Flags))
                row.Flags = BallisticHalf(row.Flags);
            if (row.CollisionProxy != null)
            {
                bool suits = Suits(row.CollisionProxy, row.Flags);
                uint userData = UserData(level, row.Material, row.CollisionProxy);
                if (!suits || (materialChanged && userData != UserDataOf(row.CollisionProxy)))
                {
                    int own = UniformFilter(row.CollisionProxy);
                    row.CollisionProxy = ProxyFor(level, row.CollisionProxy, !suits || own < 0 ? FilterFor(row.Flags) : (uint)own, userData, writer, out _);
                }
            }
            if (row.Flags != flagsBefore || !ReferenceEquals(row.CollisionProxy, proxyBefore))
            {
                //A placed copy is built from the template at the next Save & Build: the row the editor holds names no instance
                row.CollisionInstance = null;
                writer.RowChanged(composite, entity, row, flagsBefore, proxyBefore);
            }
            if (!pair)
                return null;
            Outcome outcome = SyncSibling(level, composite, entity, row, writer);

            //The row is the object every entity naming it holds: changed for one, it changed for all, and each needs its own half
            foreach ((Composite holder, FunctionEntity other) in (users ?? RowUsers.Of(commands)).Of(row))
            {
                if (ReferenceEquals(other, entity) || holder.GetEntityByID(other.shortGUID) != other || FindOriginal(commands, holder, other) != null)
                    continue;
                outcome.AlsoPaired.Add(SyncSibling(level, holder, other, row, writer));
            }
            return outcome;
        }

        /// <summary>Which ModelReferences name which collision row (the objects themselves): a row can be shared.</summary>
        public sealed class RowUsers
        {
            private readonly Dictionary<COLLISION_MAPPING, List<(Composite, FunctionEntity)>> _users = new Dictionary<COLLISION_MAPPING, List<(Composite, FunctionEntity)>>(new ReferenceComparer<COLLISION_MAPPING>());

            public static RowUsers Of(Commands commands)
            {
                RowUsers users = new RowUsers();
                foreach (Composite composite in commands.Entries)
                {
                    if (composite == null) continue;
                    foreach (FunctionEntity entity in composite.functions)
                    {
                        if (!IsModelReference(entity)) continue;
                        COLLISION_MAPPING row = RowOf(entity);
                        if (row == null) continue;
                        if (!users._users.TryGetValue(row, out List<(Composite, FunctionEntity)> list))
                            users._users[row] = list = new List<(Composite, FunctionEntity)>();
                        list.Add((composite, entity));
                    }
                }
                return users;
            }

            public IReadOnlyList<(Composite composite, FunctionEntity entity)> Of(COLLISION_MAPPING row) => row != null && _users.TryGetValue(row, out List<(Composite, FunctionEntity)> list) ? list : new List<(Composite, FunctionEntity)>();
        }

        private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }

        /// <summary>The same row, field by field (the objects it names by identity).</summary>
        private static bool SameRow(COLLISION_MAPPING a, COLLISION_MAPPING b)
        {
            return a.Flags == b.Flags && ReferenceEquals(a.CollisionProxy, b.CollisionProxy) && ReferenceEquals(a.Material, b.Material) && ReferenceEquals(a.MaterialMapping, b.MaterialMapping);
        }
        #endregion

        #region Writers
        /// <summary>How a pair change reaches the level: recorded on the undo history, or made in place.</summary>
        public interface IWriter
        {
            /// <summary>A new ModelReference in the composite, to be filled in and then handed to <see cref="EndAdd"/>.</summary>
            FunctionEntity BeginAdd(Composite composite);
            /// <summary>The new entity is complete (parameters, name, its collision row).</summary>
            void EndAdd(Composite composite, FunctionEntity entity, COLLISION_MAPPING row);
            void SetParameter(Composite composite, FunctionEntity entity, ShortGuid parameter, ParameterData content, ParameterVariant variant);
            void RemoveParameter(Composite composite, FunctionEntity entity, ShortGuid parameter);
            /// <summary>The entity's collision reference names <paramref name="row"/> from now on (added to the level's rows).</summary>
            void SetCollision(Composite composite, FunctionEntity entity, COLLISION_MAPPING row);
            /// <summary>A row was changed in place (flags, proxy): record how it was.</summary>
            void RowChanged(Composite composite, FunctionEntity entity, COLLISION_MAPPING row, CollisionFlags flagsBefore, HavokPackfile.StaticCompoundShape proxyBefore);
            void Delete(Composite composite, FunctionEntity entity);
            void ProxyCreated(Level level, HavokPackfile.StaticCompoundShape proxy);
        }

        /// <summary>Changes made in place, with no undo record or editor notification: for harnesses and tools without the editor.</summary>
        public static IWriter Direct(Level level) => new DirectWriter(level);

        /// <summary>Changes recorded on the editor's undo history under <paramref name="label"/> (group them with the change that asked for them). UI thread.</summary>
        public static IWriter Undoable(Level level, string label) => new UndoWriter(level, label);

        private static void SetReference(FunctionEntity entity, COLLISION_MAPPING row)
        {
            ResourceReference reference = entity.resources?.FirstOrDefault(o => o != null && o.resource_type == ResourceType.COLLISION_MAPPING);
            if (reference == null && entity.GetParameter(ShortGuids.resource)?.content is cResource parameter)
                reference = parameter.value.FirstOrDefault(o => o != null && o.resource_type == ResourceType.COLLISION_MAPPING);
            if (reference == null)
            {
                if (!(entity.GetParameter(ShortGuids.resource)?.content is cResource resource))
                    resource = (cResource)entity.AddParameter(ShortGuids.resource, new cResource(entity.shortGUID), ParameterVariant.INTERNAL).content;
                reference = new ResourceReference(ResourceType.COLLISION_MAPPING) { resource_id = resource.shortGUID };
                resource.value.Add(reference);
            }
            reference.CollisionMapping = row;
        }

        private sealed class DirectWriter : IWriter
        {
            private readonly Level _level;
            public DirectWriter(Level level) { _level = level; }

            public FunctionEntity BeginAdd(Composite composite) => composite.AddFunction(FunctionType.ModelReference);
            public void EndAdd(Composite composite, FunctionEntity entity, COLLISION_MAPPING row) => _level.CollisionMaps?.Entries.Add(row);
            public void SetParameter(Composite composite, FunctionEntity entity, ShortGuid parameter, ParameterData content, ParameterVariant variant) => entity.AddParameter(parameter, content, variant);
            public void RemoveParameter(Composite composite, FunctionEntity entity, ShortGuid parameter) => entity.RemoveParameter(parameter);
            public void SetCollision(Composite composite, FunctionEntity entity, COLLISION_MAPPING row)
            {
                SetReference(entity, row);
                _level.CollisionMaps?.Entries.Add(row);
            }
            public void RowChanged(Composite composite, FunctionEntity entity, COLLISION_MAPPING row, CollisionFlags flagsBefore, HavokPackfile.StaticCompoundShape proxyBefore) { }
            public void Delete(Composite composite, FunctionEntity entity)
            {
                composite.RemoveFunction(entity.shortGUID);
                foreach (Entity other in composite.GetEntities())
                    other.childLinks.RemoveAll(o => o.linkedEntityID == entity.shortGUID);
            }
            public void ProxyCreated(Level level, HavokPackfile.StaticCompoundShape proxy) { }
        }

        private sealed class UndoWriter : IWriter
        {
            private readonly Level _level;
            private readonly string _label;
            public UndoWriter(Level level, string label) { _level = level; _label = label; }

            public FunctionEntity BeginAdd(Composite composite)
            {
                Singleton.OnEntityAddPending?.Invoke();
                return composite.AddFunction(FunctionType.ModelReference);
            }

            public void EndAdd(Composite composite, FunctionEntity entity, COLLISION_MAPPING row)
            {
                UndoStack.Current.Apply(new McpLevelListEdit<COLLISION_MAPPING>(l => l.CollisionMaps?.Entries, row, _level.CollisionMaps.Entries.Count, true, _label, composite, entity));
                UndoStack.Current.Record(new EntityAddEdit(composite, entity, _label));
                //Listed where the composite is shown, but not selected: the user stays on the entity they were changing (a
                //selection would also close the Resources dialog the change came from). The viewer is sent it directly.
                ViewerSelectionSync.SuppressSyncBroadcastDepth++;
                try { Singleton.OnEntityAdded?.Invoke(entity); }
                finally { ViewerSelectionSync.SuppressSyncBroadcastDepth--; }
                Send.SendCompositeContents(composite, new Entity[] { entity });
            }

            public void SetParameter(Composite composite, FunctionEntity entity, ShortGuid parameter, ParameterData content, ParameterVariant variant)
            {
                Parameter live = entity.GetParameter(parameter);
                bool modified = ParameterModificationTracker.IsParameterModified(composite.shortGUID, entity.shortGUID, parameter);
                if (live == null)
                    UndoStack.Current.Apply(new ParameterPresenceEdit(composite, entity, new Parameter(parameter, content, variant), entity.parameters.Count, true, false, _label));
                else
                    UndoStack.Current.Apply(new ParameterValueEdit(composite, entity, parameter, ParameterValues.Clone(live.content), content, modified, modified, _label));
            }

            public void RemoveParameter(Composite composite, FunctionEntity entity, ShortGuid parameter)
            {
                Parameter live = entity.GetParameter(parameter);
                if (live == null)
                    return;
                bool modified = ParameterModificationTracker.IsParameterModified(composite.shortGUID, entity.shortGUID, parameter);
                UndoStack.Current.Apply(new ParameterPresenceEdit(composite, entity, live, entity.parameters.IndexOf(live), false, modified, _label));
            }

            /// <summary>As set_collision commits a row: through the Resources dialog's own record, where the entity keeps its reference.</summary>
            public void SetCollision(Composite composite, FunctionEntity entity, COLLISION_MAPPING row)
            {
                IEdit edit;
                if (entity.resources != null && entity.resources.Any(o => o != null && o.resource_type == ResourceType.COLLISION_MAPPING))
                {
                    List<ResourceReference> before = ResourceSessionEdit.CloneReferences(entity.resources);
                    List<ResourceReference> after = ResourceSessionEdit.CloneReferences(entity.resources);
                    after.First(o => o.resource_type == ResourceType.COLLISION_MAPPING).CollisionMapping = row;
                    edit = new ResourceSessionEdit(composite, entity, before, after, _label);
                }
                else
                {
                    Parameter existing = entity.GetParameter(ShortGuids.resource);
                    bool had = existing != null;
                    int index = had ? entity.parameters.IndexOf(existing) : entity.parameters.Count;
                    ParameterData beforeContent = ParameterValues.Clone(existing?.content);
                    ParameterVariant beforeVariant = existing?.variant ?? ParameterVariant.INTERNAL;
                    cResource after = existing?.content is cResource current ? (cResource)ParameterValues.Clone(current) : new cResource(entity.shortGUID);
                    ResourceReference reference = after.value.FirstOrDefault(o => o != null && o.resource_type == ResourceType.COLLISION_MAPPING);
                    if (reference == null)
                    {
                        reference = new ResourceReference(ResourceType.COLLISION_MAPPING) { resource_id = after.shortGUID };
                        after.value.Add(reference);
                    }
                    reference.CollisionMapping = row;
                    Parameter parameter = existing ?? new Parameter(ShortGuids.resource, new cResource(entity.shortGUID), ParameterVariant.INTERNAL);
                    edit = new ResourceSessionEdit(composite, entity, parameter, had, index, beforeContent, beforeVariant, after, _label);
                }
                UndoStack.Current.Apply(new McpLevelListEdit<COLLISION_MAPPING>(l => l.CollisionMaps?.Entries, row, _level.CollisionMaps.Entries.Count, true, _label, composite, entity));
                UndoStack.Current.Apply(edit);
            }

            public void RowChanged(Composite composite, FunctionEntity entity, COLLISION_MAPPING row, CollisionFlags flagsBefore, HavokPackfile.StaticCompoundShape proxyBefore)
            {
                UndoStack.Current.Record(new RowFieldsEdit(composite, entity, row, flagsBefore, proxyBefore, _label));
            }

            public void Delete(Composite composite, FunctionEntity entity)
            {
                UndoStack.Current.Apply(new EntityDeleteEdit(composite, entity, _label));
            }

            public void ProxyCreated(Level level, HavokPackfile.StaticCompoundShape proxy)
            {
                EditHistory.AssetChanged(level, AssetModificationInfoTable.AssetKind.COLLISION, proxy.ProxyIndex.ToString());
                Singleton.OnResourceModified?.Invoke();
            }
        }

        /// <summary>
        /// A collision row's flags and proxy changed in place (the Resources dialog edits the row it shows, rather than a
        /// copy): put back on undo, so the entity asks for its pair again once the half it was given is gone.
        /// </summary>
        private sealed class RowFieldsEdit : IEdit
        {
            private readonly COLLISION_MAPPING _row;
            private readonly CollisionFlags _flagsBefore, _flagsAfter;
            private readonly HavokPackfile.StaticCompoundShape _proxyBefore, _proxyAfter;

            public string Label { get; }
            public ShortGuid CompositeId { get; }
            public ShortGuid EntityId { get; }

            public RowFieldsEdit(Composite composite, Entity entity, COLLISION_MAPPING row, CollisionFlags flagsBefore, HavokPackfile.StaticCompoundShape proxyBefore, string label)
            {
                _row = row;
                _flagsBefore = flagsBefore;
                _proxyBefore = proxyBefore;
                _flagsAfter = row.Flags;
                _proxyAfter = row.CollisionProxy;
                CompositeId = composite.shortGUID;
                EntityId = entity.shortGUID;
                Label = label;
            }

            public void Apply(UndoContext context) => Set(_flagsAfter, _proxyAfter);
            public void Revert(UndoContext context) => Set(_flagsBefore, _proxyBefore);

            private void Set(CollisionFlags flags, HavokPackfile.StaticCompoundShape proxy)
            {
                _row.Flags = flags;
                _row.CollisionProxy = proxy;
                _row.CollisionInstance = null;
                Singleton.OnResourceModified?.Invoke();
            }

            public bool TryMerge(IEdit next) => false;
        }
        #endregion
    }
}
