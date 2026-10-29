using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.RuntimeUtilsConnection
{
    /// <summary>
    /// A link as CathodeLib keeps it, and as a flowgraph page draws it (see Flowgraph.SaveAndCompile): on the owner entity -
    /// the one with the output, or the top pin, it leaves from - from its pin, to the linked entity's pin.
    /// </summary>
    public struct LinkKey : IEquatable<LinkKey>
    {
        public readonly uint Owner;
        public readonly uint OwnerPin;
        public readonly uint Linked;
        public readonly uint LinkedPin;

        public LinkKey(uint owner, uint ownerPin, uint linked, uint linkedPin)
        {
            Owner = owner;
            OwnerPin = ownerPin;
            Linked = linked;
            LinkedPin = linkedPin;
        }

        public bool Equals(LinkKey other) => Owner == other.Owner && OwnerPin == other.OwnerPin && Linked == other.Linked && LinkedPin == other.LinkedPin;
        public override bool Equals(object obj) => obj is LinkKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Owner;
                hash = hash * 397 ^ (int)OwnerPin;
                hash = hash * 397 ^ (int)Linked;
                hash = hash * 397 ^ (int)LinkedPin;
                return hash;
            }
        }
    }

    /// <summary>How one link has been used since its display last cleared.</summary>
    public sealed class LinkActivity
    {
        /// <summary>When the game last used it, on LiveLinkTrace.NowMs's clock.</summary>
        public double LastMs;
        /// <summary>How many times.</summary>
        public long Count;
        /// <summary>A data link (its owner read or sent a value through it), rather than a logic link (its owner fired the output).</summary>
        public bool Data;
        /// <summary>
        /// A data link whose last use sent the value OUT of its owner, to the linked end (its owner wrote it), rather than
        /// into its owner from the linked end (its owner read it).
        /// </summary>
        public bool Write;
    }

    /// <summary>How an entity's pin was used: an output that fired (drawn on the node's right), or a method called (on its left).</summary>
    public enum PinUse : byte
    {
        Fired,
        Called,
    }

    /// <summary>An entity's pin, and how it was used (see PinUse).</summary>
    public struct PinKey : IEquatable<PinKey>
    {
        public readonly uint Entity;
        public readonly uint Pin;
        public readonly PinUse Use;

        public PinKey(uint entity, uint pin, PinUse use)
        {
            Entity = entity;
            Pin = pin;
            Use = use;
        }

        public bool Equals(PinKey other) => Entity == other.Entity && Pin == other.Pin && Use == other.Use;
        public override bool Equals(object obj) => obj is PinKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Entity;
                hash = hash * 397 ^ (int)Pin;
                hash = hash * 397 ^ (int)Use;
                return hash;
            }
        }
    }

    /// <summary>How often one entity's pin has been used since its display last cleared, and when last.</summary>
    public sealed class PinActivity
    {
        /// <summary>When the game last used it, on LiveLinkTrace.NowMs's clock.</summary>
        public double LastMs;
        /// <summary>How many times: outputs fired, or calls (from every caller).</summary>
        public long Count;
    }

    /// <summary>
    /// The script activity one composite display shows (see LiveLinkTrace): which of its composite's links the running
    /// game used in the instance on show, how often, and when last - shared by all the display's flowgraph pages, which
    /// draw it. Kept per link, as CathodeLib keeps links (LinkKey), and forgotten when the display moves to another
    /// composite or instance. UI thread only.
    ///
    /// What the game reports is an entity firing an output (every link out of that pin was followed), an entity reading a
    /// parameter through a data link or sending a value out through one (that one link), and a method called through a
    /// link. An entity is the display's own when it is in the instance on show; a composite instance the display shows
    /// the inside of reports its own pins, which are the variables of that name in here; and an entity further down, in an
    /// instance an alias here reaches into, is that alias. Besides the links, it counts how often each entity's outputs
    /// fired and its methods were called (the pages show them beside the pins).
    /// </summary>
    public sealed class LiveLinkActivity
    {
        public LiveLinkActivity(CompositeDisplay display)
        {
            Display = display;
        }

        public CompositeDisplay Display { get; }

        /// <summary>The composite shown, its level's commands, the level's root composite (LiveLink.RootOf), and the instance:
        /// composite instance entity ids from the root, or null for every instance (the display was not reached from the root).</summary>
        public Composite Composite { get; private set; }
        public Commands Commands { get; private set; }
        public uint Root { get; private set; }
        public List<ShortGuid> Path { get; private set; }
        public bool HasPlace => Composite != null;

        /// <summary>The links used since the last clear.</summary>
        public IReadOnlyDictionary<LinkKey, LinkActivity> Links => _links;
        public int Count => _links.Count;

        /// <summary>The entities' pins used since the last clear: outputs fired and methods called, with how often.</summary>
        public IReadOnlyDictionary<PinKey, PinActivity> Pins => _pins;
        public int PinCount => _pins.Count;

        /// <summary>Nothing has been used since the last clear: no link lit, no pin counted.</summary>
        public bool IsEmpty => _links.Count == 0 && _pins.Count == 0;

        /// <summary>When a link or pin here was last used (LiveLinkTrace.NowMs); negative infinity when none has been.</summary>
        public double LastMs { get; private set; } = double.NegativeInfinity;

        /// <summary>What a batch changed (LiveLinkActivity.Changed).</summary>
        public sealed class Change
        {
            /// <summary>Links whose drawing changed in a way the pages' glow repaint does not cover: newly lit, or glowing again after fading.</summary>
            public readonly HashSet<LinkKey> Repaint = new HashSet<LinkKey>();
            /// <summary>Every link the batch used that glows now.</summary>
            public readonly HashSet<LinkKey> Glowing = new HashSet<LinkKey>();
            /// <summary>Some pin's count went up.</summary>
            public bool Pins;
            /// <summary>Some pin was counted for the first time since the last clear.</summary>
            public bool NewPins;

            public bool Any => Repaint.Count != 0 || Glowing.Count != 0 || Pins;
        }

        /// <summary>A batch lit or counted something here. UI thread.</summary>
        public event Action<Change> Changed;

        /// <summary>Everything was forgotten (Clear, Activity switched off, or the display moved). UI thread.</summary>
        public event Action Cleared;

        private readonly Dictionary<LinkKey, LinkActivity> _links = new Dictionary<LinkKey, LinkActivity>();
        private readonly Dictionary<PinKey, PinActivity> _pins = new Dictionary<PinKey, PinActivity>();
        //When this display last cleared (LiveLinkTrace.NowMs): what the game last did before then is not shown again
        private double _clearedAtMs = double.NegativeInfinity;
        private string _placeKey = "";
        private uint[] _pathIds;

        /// <summary>An alias here, and where its entity is: the instance ids from here down to it, and its id there.</summary>
        private sealed class AliasRoute
        {
            public uint Alias;
            public uint[] Prefix;
            public uint Target;
            /// <summary>The composite holding the target (resolved down the prefix); 0 when the alias leads nowhere.</summary>
            public uint TargetComposite;
        }
        //By target entity id
        private readonly Dictionary<uint, List<AliasRoute>> _aliases = new Dictionary<uint, List<AliasRoute>>();
        private readonly List<AliasRoute> _routes = new List<AliasRoute>();
        //Variable entities by name: the composite's own pins
        private readonly Dictionary<uint, List<uint>> _variables = new Dictionary<uint, List<uint>>();
        //Whether a composite's pin of a name is a method pin (IsMethodPin), as looked up since the last index
        private readonly Dictionary<(uint composite, uint pin), bool> _methodPins = new Dictionary<(uint, uint), bool>();
        private double _indexedAt = double.NegativeInfinity;
        //What instance id paths from the root lead to (for a display of every instance: whether an alias's instance is in one)
        private readonly Dictionary<string, Composite> _resolved = new Dictionary<string, Composite>();

        //Aliases can be added, removed or re-pointed without the display moving: looked at again this often while records come
        private const double ReindexMs = 1000;

        /// <summary>The glow a link has now: 1 just used, fading to 0 over LiveLinkTrace.GlowMilliseconds.</summary>
        public static float GlowOf(LinkActivity link, double nowMs)
        {
            if (link == null)
                return 0f;
            double glow = 1.0 - (nowMs - link.LastMs) / LiveLinkTrace.GlowMilliseconds;
            return glow <= 0.0 ? 0f : glow >= 1.0 ? 1f : (float)glow;
        }

        /// <summary>Whether a pin was used a moment ago (within LiveLinkTrace.GlowMilliseconds): its count shows bright.</summary>
        public static bool IsGlowing(PinActivity pin, double nowMs)
        {
            return pin != null && nowMs - pin.LastMs < LiveLinkTrace.GlowMilliseconds;
        }

        public bool TryGet(LinkKey key, out LinkActivity link)
        {
            return _links.TryGetValue(key, out link);
        }

        public bool TryGet(PinKey key, out PinActivity pin)
        {
            return _pins.TryGetValue(key, out pin);
        }

        /// <summary>A count as a page shows it beside a pin: "[n]" up to 9999, then "[10k]", "[1.2M]" and so on (never more than it is).</summary>
        public static string CountText(long count)
        {
            System.Globalization.CultureInfo invariant = System.Globalization.CultureInfo.InvariantCulture;
            if (count <= 9999)
                return "[" + count.ToString(invariant) + "]";
            if (count < 1000000)
                return "[" + (count / 1000).ToString(invariant) + "k]";
            string[] units = { "M", "B", "T" };
            double scaled = count / 1000000.0;
            int unit = 0;
            while (scaled >= 1000 && unit < units.Length - 1)
            {
                scaled /= 1000;
                unit++;
            }
            //One decimal below 10, rounded down so it never says more than happened
            string text = scaled < 10 ? (Math.Floor(scaled * 10) / 10).ToString("0.#", invariant) : Math.Floor(scaled).ToString("0", invariant);
            return "[" + text + units[unit] + "]";
        }

        /// <summary>
        /// Forget every link used and pin counted (the toolbar's Clear Activity). They light up again as the game uses them
        /// from now on: what it hands over later but last did before the clear is left out.
        /// </summary>
        public void Clear()
        {
            _clearedAtMs = LiveLinkTrace.NowMs;
            LastMs = double.NegativeInfinity;
            if (IsEmpty)
                return;
            _links.Clear();
            _pins.Clear();
            Cleared?.Invoke();
        }

        #region WHERE
        /// <summary>
        /// Read what the display shows now. Another composite or instance (or level) than before forgets everything lit.
        /// UI thread (LiveLinkTrace.Refresh).
        /// </summary>
        internal void UpdatePlace()
        {
            Composite composite = null;
            Commands commands = null;
            List<ShortGuid> path = null;
            if (Display != null && !Display.IsDisposed && Display.Populated)
            {
                commands = Display.Content?.Level?.Commands;
                if (commands != null && commands.Loaded)
                {
                    composite = Display.Composite;
                    path = LiveLink.InstancePath(Display, commands);
                }
            }
            uint root = composite == null ? 0 : LiveLink.RootOf(commands);
            string key = composite == null ? "" : root.ToString("X8") + "/" + composite.shortGUID.AsUInt32.ToString("X8") + "/" + (path == null ? "*" : string.Join(",", path.Select(o => o.AsUInt32.ToString("X8"))));

            if (key == _placeKey && composite == Composite && commands == Commands)
            {
                if (composite != null)
                    Index();
                return;
            }
            _placeKey = key;
            Composite = composite;
            Commands = commands;
            Root = root;
            Path = path;
            _pathIds = path?.Select(o => o.AsUInt32).ToArray();
            _resolved.Clear();
            Clear();
            Index();
        }

        /// <summary>The instances this display needs the game to watch: the one it shows, and those its aliases reach into.</summary>
        internal void AddWatches(List<LiveLink.TraceWatch> shown, List<LiveLink.TraceWatch> reached)
        {
            if (Composite == null)
                return;
            shown.Add(new LiveLink.TraceWatch() { Composite = Composite.shortGUID, Path = Path == null ? null : new List<ShortGuid>(Path) });
            foreach (AliasRoute route in _routes)
            {
                if (route.TargetComposite == 0 || (Path != null && Path.Count + route.Prefix.Length > LiveLink.MaxTracePath))
                    continue;
                List<ShortGuid> path = null;
                if (Path != null)
                {
                    path = new List<ShortGuid>(Path);
                    path.AddRange(route.Prefix.Select(o => new ShortGuid(o)));
                }
                reached.Add(new LiveLink.TraceWatch() { Composite = new ShortGuid(route.TargetComposite), Path = path });
            }
        }

        /// <summary>Look the composite's aliases and variables up afresh.</summary>
        private void Index()
        {
            _aliases.Clear();
            _routes.Clear();
            _variables.Clear();
            _methodPins.Clear();
            _indexedAt = LiveLinkTrace.NowMs;
            if (Composite == null)
                return;

            foreach (VariableEntity variable in Composite.variables_dictionary.Values)
            {
                uint name = variable.name.AsUInt32;
                if (!_variables.TryGetValue(name, out List<uint> ids))
                    _variables[name] = ids = new List<uint>();
                ids.Add(variable.shortGUID.AsUInt32);
            }

            foreach (AliasEntity alias in Composite.aliases_dictionary.Values)
            {
                //A path of instance ids down to the entity, then the entity, ending with an empty id
                ShortGuid[] steps = alias.alias?.path;
                if (steps == null)
                    continue;
                int length = steps.Length;
                while (length > 0 && steps[length - 1].IsInvalid)
                    length--;
                if (length == 0)
                    continue;
                AliasRoute route = new AliasRoute()
                {
                    Alias = alias.shortGUID.AsUInt32,
                    Prefix = steps.Take(length - 1).Select(o => o.AsUInt32).ToArray(),
                    Target = steps[length - 1].AsUInt32,
                };
                Composite holder = ResolveDown(Composite, route.Prefix, 0, route.Prefix.Length);
                route.TargetComposite = holder?.shortGUID.AsUInt32 ?? 0;
                if (route.TargetComposite == 0)
                    continue; //leads nowhere: the game has nothing there to report
                if (!_aliases.TryGetValue(route.Target, out List<AliasRoute> routes))
                    _aliases[route.Target] = routes = new List<AliasRoute>();
                routes.Add(route);
                if (route.Prefix.Length != 0)
                    _routes.Add(route);
            }
        }

        /// <summary>The composite reached from <paramref name="from"/> down these instance ids; null where one is not a composite instance.</summary>
        private Composite ResolveDown(Composite from, uint[] ids, int start, int end)
        {
            Composite composite = from;
            for (int i = start; i < end && composite != null; i++)
            {
                if (!(composite.GetEntityByID(new ShortGuid(ids[i])) is FunctionEntity instance) || instance.function.IsFunctionType)
                    return null;
                composite = Commands.GetComposite(instance.function);
            }
            return composite;
        }

        /// <summary>
        /// Whether a composite's own pin of this name is a method pin (its variable of that name is one): a link into an
        /// instance of it calls it, and the instance is drawn with it on its left. Remembered until the next index.
        /// </summary>
        private bool IsMethodPin(uint composite, uint pin)
        {
            if (_methodPins.TryGetValue((composite, pin), out bool method))
                return method;
            Composite instanced = Commands?.GetComposite(new ShortGuid(composite));
            VariableEntity variable = instanced?.variables_dictionary.Values.FirstOrDefault(o => o.name.AsUInt32 == pin);
            method = variable != null && Commands.Utils.GetPinInfo(instanced, variable)?.PinTypeGUID.AsCompositePinType == CompositePinType.CompositeMethodPin;
            _methodPins[(composite, pin)] = method;
            return method;
        }

        /// <summary>The composite the first <paramref name="count"/> ids of a path from the level's root lead to (remembered).</summary>
        private Composite ResolveFromRoot(uint[] path, int count)
        {
            Composite root = Commands?.EntryPoints?[0];
            if (root == null)
                return null;
            if (count == 0)
                return root;
            string key = string.Join(",", path.Take(count));
            if (_resolved.TryGetValue(key, out Composite composite))
                return composite;
            composite = ResolveDown(root, path, 0, count);
            if (_resolved.Count > 4096)
                _resolved.Clear();
            _resolved[key] = composite;
            return composite;
        }
        #endregion

        #region MAPPING
        /// <summary>Light the links the game used, from batches it handed over. UI thread (LiveLinkTrace).</summary>
        internal void Apply(List<(LiveLink.TraceBatch batch, double receivedMs)> batches)
        {
            //Moved without a refresh yet (a navigation path put back): forget what was lit and watch the new place
            if (Display != null && Composite != null && (Display.Composite != Composite || !SamePath(LiveLink.InstancePath(Display, Commands))))
                LiveLinkTrace.Refresh();
            if (Composite == null)
                return;
            double now = LiveLinkTrace.NowMs;
            if (now - _indexedAt > ReindexMs)
                Index();

            Change change = new Change();
            List<(uint id, uint pin)> ends = new List<(uint, uint)>();
            List<(uint id, uint pin)> sources = new List<(uint, uint)>();
            foreach ((LiveLink.TraceBatch batch, double receivedMs) in batches)
            {
                foreach (LiveLink.TraceRecord record in batch.Records)
                {
                    double last = receivedMs - record.AgeMs;
                    //All of it happened before this display last cleared (the game gathers between takes)
                    if (last <= _clearedAtMs)
                        continue;
                    ends.Clear();
                    MapEnd(record.Composite, record.Entity, record.Pin, record.Self, record.Path, ends);
                    if (ends.Count == 0)
                        continue;
                    switch (record.Kind)
                    {
                        case LiveLink.TraceKind.Fired:
                            {
                                //Every link out of the pin that fired was followed. A composite instance fires its method pin when
                                //a link into it is followed: that is a call into the instance (counted as called, from the
                                //caller's link), so it is counted as fired only on the variable of that name inside it
                                bool calledIn = record.Self != 0 && IsMethodPin(record.Self, record.Pin);
                                foreach ((uint id, uint pin) in ends)
                                {
                                    Entity entity = Composite.GetEntityByID(new ShortGuid(id));
                                    if (entity == null)
                                        continue;
                                    if (!calledIn || entity is VariableEntity)
                                        Tally(new PinKey(id, pin, PinUse.Fired), record.Count, last, change);
                                    foreach (EntityConnector link in entity.childLinks)
                                        if (link.thisParamID.AsUInt32 == pin)
                                            Mark(new LinkKey(id, pin, link.linkedEntityID.AsUInt32, link.linkedParamID.AsUInt32), false, false, record.Count, last, now, change);
                                }
                                break;
                            }

                        case LiveLink.TraceKind.Called:
                            //The method called, however it was (each caller's link reports its own calls: they add up). The
                            //link itself lit up as its caller fired.
                            foreach ((uint id, uint pin) in ends)
                                if (Composite.GetEntityByID(new ShortGuid(id)) != null)
                                    Tally(new PinKey(id, pin, PinUse.Called), record.Count, last, change);
                            break;

                        case LiveLink.TraceKind.Read:
                        case LiveLink.TraceKind.Wrote:
                            {
                                //The one data link read or written through, kept on its owner (the reader, or the writer): from the
                                //owner's parameter to the other end's
                                bool write = record.Kind == LiveLink.TraceKind.Wrote;
                                sources.Clear();
                                MapEnd(record.SourceComposite, record.SourceEntity, record.SourcePin, record.SourceSelf, record.SourcePath, sources);
                                if (sources.Count == 0)
                                    break;
                                foreach ((uint id, uint pin) in ends)
                                {
                                    Entity entity = Composite.GetEntityByID(new ShortGuid(id));
                                    if (entity == null)
                                        continue;
                                    foreach ((uint sourceId, uint sourcePin) in sources)
                                    {
                                        foreach (EntityConnector link in entity.childLinks)
                                        {
                                            if (link.thisParamID.AsUInt32 == pin && link.linkedEntityID.AsUInt32 == sourceId && link.linkedParamID.AsUInt32 == sourcePin)
                                            {
                                                Mark(new LinkKey(id, pin, sourceId, sourcePin), true, write, record.Count, last, now, change);
                                                break;
                                            }
                                        }
                                    }
                                }
                                break;
                            }
                    }
                }
            }
            if (change.Any)
                Changed?.Invoke(change);
        }

        /// <summary>
        /// Where an entity the game named is in this display, with the pin as it is here: itself (in the instance shown), the
        /// variables of that name (it is the instance shown, reporting its own pin), or the aliases that reach it.
        /// </summary>
        private void MapEnd(uint composite, uint entity, uint pin, uint self, uint[] path, List<(uint id, uint pin)> into)
        {
            uint shown = Composite.shortGUID.AsUInt32;
            if (path == null)
                path = new uint[0];

            if (composite == shown && InShownInstance(path))
                into.Add((entity, pin));

            if (self == shown && IsShownInstance(path, entity) && _variables.TryGetValue(pin, out List<uint> variables))
                foreach (uint variable in variables)
                    into.Add((variable, pin));

            if (_aliases.TryGetValue(entity, out List<AliasRoute> routes))
                foreach (AliasRoute route in routes)
                    if (route.TargetComposite == composite && ReachedThrough(path, route.Prefix))
                        into.Add((route.Alias, pin));
        }

        //The entity's owner instance is the one shown
        private bool InShownInstance(uint[] path)
        {
            return _pathIds == null || SameIds(path, 0, _pathIds, 0, _pathIds.Length, path.Length);
        }

        //The entity (in its owner instance at path) is the instance shown
        private bool IsShownInstance(uint[] path, uint entity)
        {
            if (_pathIds == null)
                return true;
            return path.Length + 1 == _pathIds.Length && _pathIds[path.Length] == entity && SameIds(path, 0, _pathIds, 0, path.Length, path.Length);
        }

        //The entity's owner instance is reached from the one shown down these instance ids
        private bool ReachedThrough(uint[] path, uint[] prefix)
        {
            if (_pathIds != null)
                return path.Length == _pathIds.Length + prefix.Length
                    && SameIds(path, 0, _pathIds, 0, _pathIds.Length, _pathIds.Length)
                    && SameIds(path, _pathIds.Length, prefix, 0, prefix.Length, prefix.Length);
            //Every instance shown: the path ends with the alias's steps, from an instance of this composite
            int lead = path.Length - prefix.Length;
            return lead >= 0 && SameIds(path, lead, prefix, 0, prefix.Length, prefix.Length) && ResolveFromRoot(path, lead) == Composite;
        }

        //a[aStart..] and b[bStart..] hold the same count ids, and the whole of a from aStart is aLength long
        private static bool SameIds(uint[] a, int aStart, uint[] b, int bStart, int count, int aLength)
        {
            if (aLength != count || aStart + count > a.Length || bStart + count > b.Length)
                return false;
            for (int i = 0; i < count; i++)
                if (a[aStart + i] != b[bStart + i])
                    return false;
            return true;
        }

        private bool SamePath(List<ShortGuid> path)
        {
            if (path == null || _pathIds == null)
                return path == null && _pathIds == null;
            if (path.Count != _pathIds.Length)
                return false;
            for (int i = 0; i < path.Count; i++)
                if (path[i].AsUInt32 != _pathIds[i])
                    return false;
            return true;
        }

        private void Mark(LinkKey key, bool data, bool write, uint count, double lastMs, double nowMs, Change change)
        {
            if (!_links.TryGetValue(key, out LinkActivity link))
            {
                link = new LinkActivity() { Data = data, Write = write, LastMs = lastMs };
                _links.Add(key, link);
                change.Repaint.Add(key);
            }
            else
            {
                bool glowed = nowMs - link.LastMs < LiveLinkTrace.GlowMilliseconds;
                //The value's way through a data link is the way its latest use took (a link can be read and written)
                if (lastMs >= link.LastMs)
                {
                    link.LastMs = lastMs;
                    if (data)
                        link.Write = write;
                }
                if (!glowed && nowMs - link.LastMs < LiveLinkTrace.GlowMilliseconds)
                    change.Repaint.Add(key);
            }
            link.Count = link.Count > long.MaxValue - count ? long.MaxValue : link.Count + count;
            if (nowMs - link.LastMs < LiveLinkTrace.GlowMilliseconds)
                change.Glowing.Add(key);
            if (link.LastMs > LastMs)
                LastMs = link.LastMs;
        }

        private void Tally(PinKey key, uint count, double lastMs, Change change)
        {
            if (!_pins.TryGetValue(key, out PinActivity pin))
            {
                pin = new PinActivity() { LastMs = lastMs };
                _pins.Add(key, pin);
                change.NewPins = true;
            }
            else if (lastMs > pin.LastMs)
                pin.LastMs = lastMs;
            pin.Count = pin.Count > long.MaxValue - count ? long.MaxValue : pin.Count + count;
            change.Pins = true;
            if (pin.LastMs > LastMs)
                LastMs = pin.LastMs;
        }
        #endregion
    }
}
