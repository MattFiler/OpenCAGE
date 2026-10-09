using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenCAGE
{
    public static class DirtyTracker
    {
        private static bool _isDirty = false;
        public static bool IsDirty => _isDirty;

        public static Action OnDirty;
        public static Action OnClean;
        public static Action<bool> OnChanged;

        static DirtyTracker()
        {
            BuildTracker.Hook();

            Singleton.OnLevelLoaded += MarkClean;
            Singleton.OnSaved += MarkClean;

            Singleton.OnCompositeAdded += MarkDirty;
            Singleton.OnCompositeDeleted += MarkDirty;
            Singleton.OnCompositeRenamed += MarkDirty;

            Singleton.OnEntityAdded += MarkDirty;
            Singleton.OnEntityDeleted += MarkDirty;
            Singleton.OnEntityRenamed += MarkDirty;
            Singleton.OnEntityMoved += MarkDirty;

            Singleton.OnResourceModified += MarkDirty;
            Singleton.OnParameterModified += MarkDirty;
            Singleton.OnEntityParameterModified += (entity, parameter, removed) => MarkDirty();
        }

        /// <summary>
        /// Flag that level data has been changed. Most changes come through the Singleton events above, but
        /// call this directly from anywhere that edits level data without raising one of them (flowgraph
        /// links/layouts, the function editors, etc).
        /// </summary>
        public static void MarkLevelDataModified() => MarkDirty();

        /// <summary>
        /// Capture the state of some level data, for editors that mutate it in many places. Pair with
        /// MarkIfChanged when the editor closes: that way a new edit path added to the editor later is
        /// covered automatically, rather than needing its own MarkLevelDataModified call.
        /// </summary>
        public static string Snapshot(object data)
        {
            try
            {
                return JsonConvert.SerializeObject(data);
            }
            catch
            {
                return null; //couldn't snapshot - MarkIfChanged will assume the worst
            }
        }

        /// <summary>
        /// Mark the level as modified if the data differs from the snapshot (or if it couldn't be captured -
        /// a spurious "unsaved changes" prompt is far better than silently losing an edit).
        /// </summary>
        public static void MarkIfChanged(string snapshot, object data)
        {
            if (snapshot == null)
            {
                MarkDirty();
                return;
            }

            string current = Snapshot(data);
            if (current == null || current != snapshot)
                MarkDirty();
        }

        private static void MarkClean(object a) => MarkClean();
        private static void MarkClean()
        {
            bool changed = _isDirty;
            _isDirty = false;
            OnClean?.Invoke();
            if (changed) OnChanged?.Invoke(false);
        }

        private static void MarkDirty(object a, object b) => MarkDirty();
        private static void MarkDirty(object a) => MarkDirty();
        private static void MarkDirty()
        {
            bool changed = !_isDirty;
            _isDirty = true;
            OnDirty?.Invoke();
            if (changed) OnChanged?.Invoke(true);
        }
    }

    /// <summary>
    /// Whether the open level has changes the game only takes after a Save & Build. A plain save writes the script and
    /// the assets, which is all a logic change needs; what the game derives from the script at a build - the movers that
    /// draw models and lights, collision and physics, zone membership, the navmesh, cover, job positions and sound
    /// networks - is regenerated only by Save & Build. So the placed content each composite holds (the entities of the
    /// types the build reads, the instances of composites carrying any, aliases, and a zone composite's sequences) is
    /// fingerprinted when the level opens and after each build, and compared whenever a composite or entity changes:
    /// undoing an edit takes its reason back again.
    /// </summary>
    public static class BuildTracker
    {
        private static readonly object _lock = new object();
        private static bool _hooked;

        //The types the build (instancing and its bakers) reads from the script
        private static readonly HashSet<ShortGuid> _builtTypes = BuiltTypes();
        private static readonly ShortGuid _zone = new ShortGuid((uint)FunctionType.Zone);
        private static readonly ShortGuid _triggerSequence = new ShortGuid((uint)FunctionType.TriggerSequence);

        private sealed class Item
        {
            public long Hash;
            public string Type;
        }

        private static Commands _commands;
        private static Dictionary<Composite, Dictionary<ShortGuid, Item>> _baseline = new Dictionary<Composite, Dictionary<ShortGuid, Item>>();
        private static readonly Dictionary<Composite, bool> _carries = new Dictionary<Composite, bool>();
        //Why a build is needed: per composite (taken back when it matches the baseline again), and others that stay
        private static readonly Dictionary<Composite, string> _changed = new Dictionary<Composite, string>();
        private static readonly Dictionary<string, string> _other = new Dictionary<string, string>();
        private static bool _savedWithoutBuild;

        /// <summary>When this session last saved the level (UTC), and last built it; null if it has not.</summary>
        public static DateTime? LastSavedUtc { get; private set; }
        public static DateTime? LastBuiltUtc { get; private set; }
        /// <summary>The bakers the last build in this session skipped (their data was kept as it was on disk).</summary>
        public static List<string> LastBuildSkipped { get; private set; } = new List<string>();
        /// <summary>The level was opened as it was last saved without a build after its last Save & Build (so the game's derived data may be older than its script).</summary>
        public static bool SavedWithoutBuildOnDisk { get { lock (_lock) return _savedWithoutBuild; } }

        /// <summary>Whether the game would run data older than the level's script: a Save & Build is needed before playing. UI thread.</summary>
        public static bool NeedsBuild
        {
            get
            {
                Settle();
                lock (_lock) return _savedWithoutBuild || _changed.Count != 0 || _other.Count != 0;
            }
        }

        /// <summary>Why a build is needed, at most <paramref name="max"/> of them. UI thread.</summary>
        public static List<string> Reasons(int max, out int total)
        {
            Settle();
            lock (_lock)
            {
                List<string> all = new List<string>();
                if (_savedWithoutBuild)
                    all.Add("the level's script on disk is newer than its last Save & Build (saved without a build, or restored from a backup, before it was opened): what changed is unknown");
                all.AddRange(_other.Values);
                all.AddRange(_changed.Values.OrderBy(o => o, StringComparer.OrdinalIgnoreCase));
                total = all.Count;
                return all.Take(max).ToList();
            }
        }

        public static void Hook()
        {
            lock (_lock)
            {
                if (_hooked) return;
                _hooked = true;
            }
            Singleton.OnCompositesModified += composites => { foreach (Composite composite in composites) Recheck(composite); };
            Singleton.OnCompositeDeleted += Deleted;
            Singleton.OnEntityMoved += (transform, entity) => EntityChanged(entity);
            Singleton.OnEntityParameterModified += (entity, parameter, removed) => EntityChanged(entity);
            Singleton.OnEntityAdded += EntityChanged;
            //A deletion names its composite before the entity goes (OnEntityDeleted comes after, when nothing holds it)
            Singleton.OnEntityDeletePending += (entity, composite) => Recheck(composite);
            Singleton.OnEntityDeleted += EntityChanged;
            Singleton.OnResourceModified += () =>
            {
                lock (_lock)
                    if (_commands != null)
                        _other["resources"] = "the level's resources changed (models, materials, collision or physics: a build makes the movers and collision that use them)";
            };
            //The closing level lets go here: held until the next one is loaded, its script and everything it reaches stayed alive through that load
            Singleton.OnLevelClosing += LevelClosing;
        }

        private static void LevelClosing(LevelContent content)
        {
            lock (_lock)
            {
                Commands closing = content?.Level?.Commands;
                if (_commands == null || (closing != null && closing != _commands))
                    return;
                _commands = null;
                _baseline = new Dictionary<Composite, Dictionary<ShortGuid, Item>>();
                _carries.Clear();
                _builtPins.Clear();
                _changed.Clear();
                _other.Clear();
                _pending.Clear();
                _savedWithoutBuild = false;
                LastSavedUtc = null;
                LastBuiltUtc = null;
                LastBuildSkipped = new List<string>();
            }
        }

        /// <summary>A level has been loaded (any thread): what it holds now is what the game runs, unless it was saved without a build.</summary>
        public static void LevelLoaded(LevelContent content)
        {
            Hook();
            Commands commands = content?.Level?.Commands;
            bool savedWithoutBuild = false;
            try
            {
                //Retail ships COMMANDS.PAK alone; an OpenCAGE save writes the BIN beside it. Only a build writes the radiosity
                //collision mapping, so a script written well after it was saved without a build.
                string script = content.Level.CommandsFilepath;
                string world = Path.GetDirectoryName(script);
                string radiosity = Path.Combine(world, "RADIOSITY_COLLISION_MAPPING.BIN");
                if (File.Exists(script) && File.Exists(Path.Combine(world, "COMMANDS.BIN")) && File.Exists(radiosity))
                    savedWithoutBuild = File.GetLastWriteTimeUtc(script) > File.GetLastWriteTimeUtc(radiosity).AddSeconds(60);
            }
            catch { }
            Dictionary<Composite, Dictionary<ShortGuid, Item>> baseline = Fingerprint(commands);
            lock (_lock)
            {
                _commands = commands;
                _baseline = baseline;
                _changed.Clear();
                _other.Clear();
                _pending.Clear();
                _savedWithoutBuild = savedWithoutBuild;
                LastSavedUtc = null;
                LastBuiltUtc = null;
                LastBuildSkipped = new List<string>();
            }
        }

        /// <summary>The open level has never been built (a level just made from scratch): it needs a Save & Build before it is played.</summary>
        public static void MarkUnbuilt(string reason)
        {
            lock (_lock)
                if (_commands != null)
                    _other["unbuilt"] = reason;
        }

        /// <summary>The open level was saved (built: a Save & Build, skipping these bakers).</summary>
        public static void Saved(LevelContent content, bool built, List<string> skipped)
        {
            Commands commands = content?.Level?.Commands;
            Dictionary<Composite, Dictionary<ShortGuid, Item>> baseline = built ? Fingerprint(commands) : null;
            lock (_lock)
            {
                LastSavedUtc = DateTime.UtcNow;
                if (!built || commands == null || commands != _commands) return;
                LastBuiltUtc = LastSavedUtc;
                LastBuildSkipped = skipped ?? new List<string>();
                _baseline = baseline;
                _changed.Clear();
                _other.Clear();
                _pending.Clear();
                _savedWithoutBuild = false;
            }
        }

        private static HashSet<ShortGuid> BuiltTypes()
        {
            string[] names =
            {
                "ModelReference", "EnvironmentModelReference", "LightReference", "ParticleEmitterReference", "RibbonEmitterReference", "ProjectiveDecal",
                "FogBox", "FogPlane", "FogSphere", "SimpleWater", "SimpleRefraction", "SurfaceEffectBox", "SurfaceEffectSphere", "ExclusiveMaster",
                "CollisionBarrier", "PhysicsSystem", "RadiosityProxy", "RadiosityIsland", "Zone",
                "NavMeshArea", "NavMeshBarrier", "NavMeshExclusionArea", "NavMeshReachabilitySeedPoint", "NavMeshWalkablePlatform",
                "PathfindingAlienBackstageNode", "PathfindingManualNode", "PathfindingTeleportNode", "PathfindingWaitNode",
                "CoverExclusionArea", "CoverLine", "SpottingExclusionArea", "JOB_Assault", "JOB_SpottingPosition",
                "SoundBarrier", "SoundEnvironmentMarker", "SoundNetworkNode", "SoundLevelInitialiser",
            };
            HashSet<ShortGuid> types = new HashSet<ShortGuid>();
            foreach (string name in names)
                if (Enum.TryParse(name, out FunctionType type))
                    types.Add(new ShortGuid((uint)type));
            //Traversals become navmesh links
            foreach (FunctionType type in Enum.GetValues(typeof(FunctionType)))
                if (type.ToString().StartsWith("TRAV_", StringComparison.Ordinal))
                    types.Add(new ShortGuid((uint)type));
            return types;
        }

        private static Dictionary<Composite, Dictionary<ShortGuid, Item>> Fingerprint(Commands commands)
        {
            Dictionary<Composite, Dictionary<ShortGuid, Item>> all = new Dictionary<Composite, Dictionary<ShortGuid, Item>>();
            if (commands?.Entries == null) return all;
            lock (_lock)
            {
                _carries.Clear();
                _builtPins.Clear();
            }
            try
            {
                foreach (Composite composite in commands.Entries.ToList())
                    if (composite != null && !all.ContainsKey(composite))
                        all[composite] = Fingerprint(commands, composite);
            }
            catch (InvalidOperationException) { } //edited while it was read: what was read is kept
            return all;
        }

        /// <summary>The build-relevant entities of one composite: id -> a hash of what the build reads from it.</summary>
        private static Dictionary<ShortGuid, Item> Fingerprint(Commands commands, Composite composite)
        {
            Dictionary<ShortGuid, Item> items = new Dictionary<ShortGuid, Item>();
            bool zones = composite.functions.Any(o => o.function == _zone);
            foreach (FunctionEntity function in composite.functions.ToList())
            {
                string type;
                if (function.function.IsFunctionType)
                {
                    if (!_builtTypes.Contains(function.function) && !(zones && function.function == _triggerSequence)) continue;
                    type = function.function.AsFunctionType.ToString();
                }
                else
                {
                    Composite instanced = commands.GetComposite(function.function);
                    if (instanced == null || !Carries(commands, instanced)) continue;
                    type = "instance of " + instanced.name;
                }
                long hash = function.function.GetHashCode();
                //An instance's own parameters are mostly its pins - logic - unless a pin feeds what the build reads
                hash = Mix(hash, function.function.IsFunctionType ? ParametersHash(function) : ParametersHash(function, BuiltPins(commands, commands.GetComposite(function.function))));
                foreach (ResourceReference resource in function.resources)
                    hash = Mix(hash, resource?.GetHashCode() ?? 0);
                if (function.function == _zone || function.function == _triggerSequence)
                    foreach (EntityConnector link in function.childLinks)
                        hash = Mix(Mix(Mix(hash, link.thisParamID.GetHashCode()), link.linkedEntityID.GetHashCode()), link.linkedParamID.GetHashCode());
                items[function.shortGUID] = new Item() { Hash = hash, Type = type };
            }
            //Aliases override what instances place (positions, models, flags)
            foreach (AliasEntity alias in composite.aliases.ToList())
            {
                long hash = 31;
                if (alias.alias?.path != null)
                    foreach (ShortGuid step in alias.alias.path)
                        hash = Mix(hash, step.GetHashCode());
                hash = Mix(hash, ParametersHash(alias));
                items[alias.shortGUID] = new Item() { Hash = hash, Type = "alias" };
            }
            return items;
        }

        private static long ParametersHash(Entity entity, HashSet<ShortGuid> only = null)
        {
            long hash = 17;
            foreach (Parameter parameter in entity.parameters.ToList())
            {
                if (parameter == null || (only != null && !only.Contains(parameter.name))) continue;
                hash = Mix(Mix(hash, parameter.name.GetHashCode()), parameter.content?.GetHashCode() ?? 0);
            }
            return hash;
        }

        //What an instance's placement is made of: where it is and the flags that keep its contents out of the game
        private static readonly string[] _placementParameters = { "position", "deleted", "delete_me", "is_template", "is_shared", "delete_standard_collision", "delete_ballistic_collision" };
        private static readonly Dictionary<Composite, HashSet<ShortGuid>> _builtPins = new Dictionary<Composite, HashSet<ShortGuid>>();

        /// <summary>
        /// The parameters of an instance of <paramref name="composite"/> the build reads: its placement, and the pins (variables)
        /// the composite links straight into an entity of a built type, or into such a pin of an instance inside it.
        /// </summary>
        private static HashSet<ShortGuid> BuiltPins(Commands commands, Composite composite)
        {
            HashSet<ShortGuid> pins = new HashSet<ShortGuid>(_placementParameters.Select(o => ShortGuidUtils.Generate(o)));
            if (composite == null) return pins;
            lock (_lock)
                if (_builtPins.TryGetValue(composite, out HashSet<ShortGuid> known))
                    return known;
            lock (_lock) _builtPins[composite] = pins; //placement only while it is worked out (a composite inside itself)
            foreach (VariableEntity variable in composite.variables.ToList())
            {
                foreach (EntityConnector link in variable.childLinks)
                {
                    if (!(composite.GetEntityByID(link.linkedEntityID) is FunctionEntity target)) continue;
                    bool feeds = target.function.IsFunctionType ? _builtTypes.Contains(target.function)
                        : BuiltPins(commands, commands.GetComposite(target.function)).Contains(link.linkedParamID);
                    if (feeds) { pins.Add(variable.name); break; }
                }
            }
            return pins;
        }

        private static long Mix(long hash, long value) => unchecked(hash * 31 + value);

        /// <summary>Whether a composite, or anything it places, holds content the build reads.</summary>
        private static bool Carries(Commands commands, Composite composite)
        {
            lock (_lock)
                if (_carries.TryGetValue(composite, out bool known))
                    return known;
            HashSet<Composite> seen = new HashSet<Composite>();
            bool carries = Carries(commands, composite, seen);
            lock (_lock)
                _carries[composite] = carries;
            return carries;
        }

        private static bool Carries(Commands commands, Composite composite, HashSet<Composite> seen)
        {
            if (!seen.Add(composite)) return false;
            lock (_lock)
                if (_carries.TryGetValue(composite, out bool known))
                    return known;
            foreach (FunctionEntity function in composite.functions)
            {
                if (function.function.IsFunctionType)
                {
                    if (_builtTypes.Contains(function.function)) return true;
                    continue;
                }
                Composite instanced = commands.GetComposite(function.function);
                if (instanced != null && Carries(commands, instanced, seen)) return true;
            }
            return false;
        }

        //Composites changed since they were last compared: compared when asked (an edit can fire many events, a drag one a frame)
        private static readonly HashSet<Composite> _pending = new HashSet<Composite>();

        private static void Recheck(Composite composite)
        {
            if (composite == null) return;
            lock (_lock)
                if (_commands != null)
                    _pending.Add(composite);
        }

        /// <summary>Compare what changed since the last look with the fingerprints from the last build. UI thread (it reads the script).</summary>
        private static void Settle()
        {
            List<Composite> pending;
            lock (_lock)
            {
                if (_pending.Count == 0) return;
                pending = _pending.ToList();
                _pending.Clear();
            }
            foreach (Composite composite in pending)
                Compare(composite);
        }

        /// <summary>Compare a composite with its fingerprint from the last build, and keep (or drop) its reason.</summary>
        private static void Compare(Composite composite)
        {
            Commands commands;
            Dictionary<ShortGuid, Item> before;
            lock (_lock)
            {
                commands = _commands;
                if (commands == null || composite == null) return;
                //Deleted since: Deleted() has said so
                if (!commands.Entries.Contains(composite)) return;
                _baseline.TryGetValue(composite, out before);
            }
            Dictionary<ShortGuid, Item> now;
            try { now = Fingerprint(commands, composite); }
            catch (InvalidOperationException) { return; }
            lock (_lock)
            {
                if (before == null)
                {
                    //Made since the build: unplaced it changes nothing in game, and placing it changes the composite that places it
                    _baseline[composite] = now;
                    return;
                }
                string reason = Describe(composite, before, now);
                if (reason == null) _changed.Remove(composite);
                else _changed[composite] = reason;
            }
        }

        private static string Describe(Composite composite, Dictionary<ShortGuid, Item> before, Dictionary<ShortGuid, Item> now)
        {
            List<Item> added = now.Where(o => !before.ContainsKey(o.Key)).Select(o => o.Value).ToList();
            List<Item> removed = before.Where(o => !now.ContainsKey(o.Key)).Select(o => o.Value).ToList();
            List<Item> changed = now.Where(o => before.TryGetValue(o.Key, out Item old) && old.Hash != o.Value.Hash).Select(o => o.Value).ToList();
            if (added.Count + removed.Count + changed.Count == 0)
                return null;
            List<string> parts = new List<string>();
            if (added.Count != 0) parts.Add(added.Count + " added");
            if (changed.Count != 0) parts.Add(changed.Count + " changed");
            if (removed.Count != 0) parts.Add(removed.Count + " removed");
            List<string> types = added.Concat(changed).Concat(removed).Select(o => o.Type).Distinct().Take(4).ToList();
            return composite.name + ": " + string.Join(", ", parts) + " (" + string.Join(", ", types) + ")";
        }

        private static void Deleted(Composite composite)
        {
            lock (_lock)
            {
                if (_commands == null || composite == null) return;
                _pending.Remove(composite);
                if (_baseline.TryGetValue(composite, out Dictionary<ShortGuid, Item> before) && before.Count != 0)
                    _changed[composite] = composite.name + ": deleted (it held placed content)";
            }
        }

        /// <summary>
        /// An edit the editor reports by entity (the inspector, the viewport): recheck the composite that holds it, so the
        /// comparison with the baseline gives the reason and an undo takes it back. One that no composite holds has been
        /// deleted, and its deletion's pending event named the composite.
        /// </summary>
        private static void EntityChanged(Entity entity)
        {
            if (entity == null) return;
            Commands commands;
            lock (_lock) commands = _commands;
            if (commands == null) return;
            Composite shown = Singleton.Editor?.CompositeDisplay?.Composite;
            if (shown != null && shown.GetEntityByID(entity.shortGUID) == entity)
            {
                Recheck(shown);
                return;
            }
            //Not the composite on screen: whichever holds it
            Composite holder = null;
            try { holder = commands.Entries.FirstOrDefault(o => o != null && o.GetEntityByID(entity.shortGUID) == entity); }
            catch (InvalidOperationException) { } //edited while it was read
            Recheck(holder);
        }
    }
}
