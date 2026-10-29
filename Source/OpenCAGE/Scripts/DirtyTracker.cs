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

        /// <summary>
        /// How many times level data has been marked as changed since the editor started, saves and loads included. Equal
        /// at two points means nothing was edited between them, for what is only rebuilt when something could differ.
        /// </summary>
        public static int Modifications => _modifications;
        private static int _modifications = 0;

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
        /// a spurious "unsaved changes" prompt is far better than silently losing an edit). True if it was marked.
        /// </summary>
        public static bool MarkIfChanged(string snapshot, object data)
        {
            if (snapshot == null)
            {
                MarkDirty();
                return true;
            }

            string current = Snapshot(data);
            if (current != null && current == snapshot)
                return false;
            MarkDirty();
            return true;
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
            System.Threading.Interlocked.Increment(ref _modifications);
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

        private static Commands _commands;
        //What the build read from each composite at the last one (see BuildFingerprint)
        private static Dictionary<Composite, Dictionary<ShortGuid, BuildFingerprint.Item>> _baseline = new Dictionary<Composite, Dictionary<ShortGuid, BuildFingerprint.Item>>();
        //What's been worked out about the open level's composites
        private static readonly BuildFingerprint.Cache _known = new BuildFingerprint.Cache();
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
                _baseline = new Dictionary<Composite, Dictionary<ShortGuid, BuildFingerprint.Item>>();
                _known.Clear();
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
            Dictionary<Composite, Dictionary<ShortGuid, BuildFingerprint.Item>> baseline = Fingerprint(commands);
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
            Dictionary<Composite, Dictionary<ShortGuid, BuildFingerprint.Item>> baseline = built ? Fingerprint(commands) : null;
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

        private static Dictionary<Composite, Dictionary<ShortGuid, BuildFingerprint.Item>> Fingerprint(Commands commands)
        {
            Dictionary<Composite, Dictionary<ShortGuid, BuildFingerprint.Item>> all = new Dictionary<Composite, Dictionary<ShortGuid, BuildFingerprint.Item>>();
            if (commands?.Entries == null) return all;
            lock (_lock)
                _known.Clear();
            try
            {
                foreach (Composite composite in commands.Entries.ToList())
                    if (composite != null && !all.ContainsKey(composite))
                        all[composite] = BuildFingerprint.Of(commands, composite, _known);
            }
            catch (InvalidOperationException) { } //edited while it was read: what was read is kept
            return all;
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
            Dictionary<ShortGuid, BuildFingerprint.Item> before;
            lock (_lock)
            {
                commands = _commands;
                if (commands == null || composite == null) return;
                //Deleted since: Deleted() has said so
                if (!commands.Entries.Contains(composite)) return;
                _baseline.TryGetValue(composite, out before);
            }
            Dictionary<ShortGuid, BuildFingerprint.Item> now;
            try { now = BuildFingerprint.Of(commands, composite, _known); }
            catch (InvalidOperationException) { return; }
            lock (_lock)
            {
                if (before == null)
                {
                    //Made since the build: unplaced it changes nothing in game, and placing it changes the composite that places it
                    _baseline[composite] = now;
                    return;
                }
                string reason = BuildFingerprint.Describe(composite, before, now);
                if (reason == null) _changed.Remove(composite);
                else _changed[composite] = reason;
            }
        }

        private static void Deleted(Composite composite)
        {
            lock (_lock)
            {
                if (_commands == null || composite == null) return;
                _pending.Remove(composite);
                if (_baseline.TryGetValue(composite, out Dictionary<ShortGuid, BuildFingerprint.Item> before) && before.Count != 0)
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
