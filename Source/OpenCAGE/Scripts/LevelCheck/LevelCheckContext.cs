using CATHODE;
using CathodeLib;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// What the Level Check's checks share for one run: the level, the composites it places, and one walk of its placements
    /// (every entry point down, through instances only into composites holding a type some check reads), with each placement's world
    /// position and whether the game places it at all (<see cref="McpPlacements.Placement.Real"/>).
    /// </summary>
    internal sealed class LevelCheckContext
    {
        public readonly Level Level;
        public readonly Commands Commands;
        public readonly LevelCheckReport Report;
        public bool Truncated { get; private set; }

        private readonly Dictionary<FunctionType, List<McpPlacements.Placement>> _placements = new Dictionary<FunctionType, List<McpPlacements.Placement>>();
        private HashSet<Composite> _placed;

        public LevelCheckContext(Level level, LevelCheckReport report)
        {
            Level = level;
            Commands = level.Commands;
            Report = report;
        }

        private HashSet<ShortGuid> _users;

        /// <summary>
        /// A composite the user made, brought in or changed with OpenCAGE (its modification info, which the level keeps): the script checks
        /// that find authoring mistakes look only at these, since retail's own composites play as shipped. <see cref="LevelCheck.LookAtRetail"/>
        /// makes every composite count (to measure retail).
        /// </summary>
        public bool IsUsers(Composite composite)
        {
            if (LevelCheck.LookAtRetail) return true;
            if (composite == null) return false;
            if (_users == null)
            {
                _users = new HashSet<ShortGuid>();
                try
                {
                    foreach (CompositeModificationInfoTable.ModificationInfo info in Commands.Utils?.AllModificationInfo ?? new List<CompositeModificationInfoTable.ModificationInfo>())
                        if (info != null) _users.Add(info.composite_id);
                }
                catch { }
            }
            return _users.Contains(composite.shortGUID);
        }

        /// <summary>The entities of a type in the user's own composites the level places (see <see cref="IsUsers"/>).</summary>
        public IEnumerable<(Composite Composite, FunctionEntity Entity)> InUsers(FunctionType type) => InPlaced(type).Where(o => IsUsers(o.Composite));

        /// <summary>The level's root composite (null for a level without entry points).</summary>
        public Composite Root => Commands.EntryPoints != null && Commands.EntryPoints.Length != 0 ? Commands.EntryPoints[0] : null;

        public static bool Is(Entity entity, FunctionType type) =>
            entity is FunctionEntity function && function.function.IsFunctionType && function.function.AsFunctionType == type;

        public static bool IsAny(Entity entity, HashSet<FunctionType> types) =>
            entity is FunctionEntity function && function.function.IsFunctionType && types.Contains(function.function.AsFunctionType);

        /// <summary>The composites the level places, from its entry points down (all of them, for a level without entry points).</summary>
        public HashSet<Composite> Placed
        {
            get
            {
                if (_placed != null) return _placed;
                _placed = new HashSet<Composite>();
                Composite[] entries = Commands.EntryPoints;
                if (entries == null || entries.Length == 0 || entries[0] == null)
                {
                    foreach (Composite composite in Commands.Entries)
                        if (composite != null) _placed.Add(composite);
                    return _placed;
                }
                Stack<Composite> todo = new Stack<Composite>();
                foreach (Composite entry in entries)
                    if (entry != null && _placed.Add(entry)) todo.Push(entry);
                while (todo.Count != 0)
                {
                    foreach (FunctionEntity function in todo.Pop().functions)
                    {
                        if (function.function.IsFunctionType) continue;
                        Composite child = Commands.GetComposite(function.function);
                        if (child != null && _placed.Add(child)) todo.Push(child);
                    }
                }
                return _placed;
            }
        }

        /// <summary>The entities of a type in composites the level places, with the composite holding each (placed or not, once each).</summary>
        public IEnumerable<(Composite Composite, FunctionEntity Entity)> InPlaced(FunctionType type)
        {
            foreach (Composite composite in Commands.Entries)
            {
                if (composite == null || !Placed.Contains(composite)) continue;
                foreach (FunctionEntity function in composite.functions)
                    if (function.function.IsFunctionType && function.function.AsFunctionType == type)
                        yield return (composite, function);
            }
        }

        /// <summary>Every placement of the type the walk found (real or not: filter on <see cref="McpPlacements.Placement.Real"/>).</summary>
        public List<McpPlacements.Placement> Placements(FunctionType type) =>
            _placements.TryGetValue(type, out List<McpPlacements.Placement> list) ? list : new List<McpPlacements.Placement>();

        private readonly Dictionary<string, List<McpPlacements.Placement>> _instances = new Dictionary<string, List<McpPlacements.Placement>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Every placement of an instance of the composite (by path) the walk found - one a rule's <see cref="LevelCheckRule.WalksInstancesOf"/> asked for.</summary>
        public List<McpPlacements.Placement> InstancePlacements(string composite) =>
            _instances.TryGetValue(composite, out List<McpPlacements.Placement> list) ? list : new List<McpPlacements.Placement>();

        /// <summary>The placements the game makes of the type.</summary>
        public IEnumerable<McpPlacements.Placement> RealPlacements(FunctionType type) => Placements(type).Where(o => o.Real);

        /// <summary>One walk from every entry point, keeping the placements of these types.</summary>
        public void Walk(IEnumerable<FunctionType> typesWanted, IEnumerable<string> instancesWanted = null)
        {
            HashSet<FunctionType> types = new HashSet<FunctionType>(typesWanted);
            //Composites whose placements (instances) a check reads, by path
            HashSet<Composite> instanced = new HashSet<Composite>((instancesWanted ?? Enumerable.Empty<string>())
                .Select(o => Commands.Entries.FirstOrDefault(c => c != null && string.Equals(c.name, o, StringComparison.OrdinalIgnoreCase))).Where(o => o != null));
            if ((types.Count == 0 && instanced.Count == 0) || Root == null) return;
            foreach (FunctionType type in types) _placements[type] = new List<McpPlacements.Placement>();
            foreach (Composite composite in instanced) _instances[composite.name] = new List<McpPlacements.Placement>();
            bool IsWantedInstance(FunctionEntity o) => !o.function.IsFunctionType && instanced.Count != 0 && instanced.Contains(Commands.GetComposite(o.function));

            //Composites that hold one of the types (or an instance wanted), themselves or anywhere below them
            Dictionary<Composite, bool> holds = new Dictionary<Composite, bool>();
            bool Holds(Composite composite)
            {
                if (composite == null) return false;
                if (holds.TryGetValue(composite, out bool known)) return known;
                holds[composite] = false;
                bool result = composite.functions.Any(o => IsAny(o, types) || IsWantedInstance(o));
                if (!result)
                    foreach (FunctionEntity function in composite.functions)
                        if (!function.function.IsFunctionType && Holds(Commands.GetComposite(function.function))) { result = true; break; }
                holds[composite] = result;
                return result;
            }

            McpPlacements walker = McpRegion.Walker(Commands);
            HashSet<Composite> started = new HashSet<Composite>();
            foreach (Composite entry in Commands.EntryPoints)
            {
                if (entry == null || !started.Add(entry)) continue;
                walker.Walk(entry, step =>
                {
                    if (step.Entity is FunctionEntity function)
                    {
                        if (function.function.IsFunctionType && types.Contains(function.function.AsFunctionType))
                            _placements[function.function.AsFunctionType].Add(McpPlacements.Placement.Of(step));
                        else if (IsWantedInstance(function))
                            _instances[Commands.GetComposite(function.function).name].Add(McpPlacements.Placement.Of(step));
                    }
                    return true;
                }, Holds);
                Truncated |= walker.Truncated;
            }
        }

        #region Naming
        public string EntityName(Composite composite, Entity entity)
        {
            try { return McpScript.EntityName(Commands, composite, entity); }
            catch { return entity?.shortGUID.ToByteString() ?? "?"; }
        }

        /// <summary>"composite / entity" (the composite by its last path part).</summary>
        public string Where(Composite composite, Entity entity)
        {
            return (composite == null ? "?" : EditorUtils.GetCompositeName(composite)) + " / " + EntityName(composite, entity);
        }

        /// <summary>A placement as a path of names from its entry point.</summary>
        public string PathText(McpPlacements.Placement placement)
        {
            if (placement?.Chain == null || placement.Chain.Count == 0) return "?";
            List<Composite> along = McpScript.CompositesAlong(Commands, placement.Composite == null ? Root : FirstComposite(placement), placement.Chain);
            List<string> names = new List<string>();
            for (int i = 0; i < placement.Chain.Count; i++)
                names.Add(along[i] == null ? placement.Chain[i].shortGUID.ToByteString() : EntityName(along[i], placement.Chain[i]));
            return string.Join(" > ", names);
        }

        //The entry point a placement's chain starts in: the one holding its first step
        private Composite FirstComposite(McpPlacements.Placement placement)
        {
            Entity first = placement.Chain[0];
            foreach (Composite entry in Commands.EntryPoints)
                if (entry != null && entry.GetEntityByID(first.shortGUID) == first) return entry;
            return Root;
        }

        /// <summary>The composite holding the last step of a placement's chain.</summary>
        public Composite HolderOf(McpPlacements.Placement placement)
        {
            List<Composite> along = McpScript.CompositesAlong(Commands, FirstComposite(placement), placement.Chain);
            return along.Count >= placement.Chain.Count ? along[placement.Chain.Count - 1] : null;
        }

        public LevelCheckSource Source(Composite composite, Entity entity, string detail = null)
        {
            return new LevelCheckSource() { Composite = composite, Entity = entity, Text = Where(composite, entity) + (detail == null ? "" : " (" + detail + ")") };
        }

        public LevelCheckSource Source(McpPlacements.Placement placement, string detail = null)
        {
            Composite holder = HolderOf(placement);
            Entity entity = placement.Chain[placement.Chain.Count - 1];
            return new LevelCheckSource() { Composite = holder, Entity = entity, Chain = placement.Chain, Text = PathText(placement) + (detail == null ? "" : " (" + detail + ")") };
        }
        #endregion

        #region Links
        /// <summary>Whether an entity's pin is linked: a link it holds on the pin, or one from another entity of its composite into the pin.</summary>
        public static bool PinLinked(Composite composite, Entity entity, ShortGuid pin)
        {
            if (entity.childLinks.Any(o => o.thisParamID == pin)) return true;
            foreach (Entity other in composite.GetEntities())
                foreach (EntityConnector link in other.childLinks)
                    if (link.linkedEntityID == entity.shortGUID && link.linkedParamID == pin) return true;
            return false;
        }

        /// <summary>Whether an alias anywhere in the level points at this entity (by its id at the end of its path) and links the pin.</summary>
        public bool PinLinkedByAlias(Entity entity, ShortGuid pin)
        {
            foreach (Composite composite in Commands.Entries)
            {
                if (composite == null) continue;
                foreach (AliasEntity alias in composite.aliases)
                {
                    ShortGuid[] path = alias.alias?.path;
                    if (path == null || path.Length == 0) continue;
                    int last = path.Length - 1;
                    if (path[last] == ShortGuid.Invalid) last--;
                    if (last < 0 || path[last] != entity.shortGUID) continue;
                    if (alias.childLinks.Any(o => o.thisParamID == pin)) return true;
                    foreach (Entity other in composite.GetEntities())
                        foreach (EntityConnector link in other.childLinks)
                            if (link.linkedEntityID == alias.shortGUID && link.linkedParamID == pin) return true;
                }
            }
            return false;
        }

        public static bool? Bool(Entity entity, string parameter) => (entity?.GetParameter(parameter)?.content as cBool)?.value;

        public static string Text(Entity entity, string parameter) => (entity?.GetParameter(parameter)?.content as cString)?.value;
        #endregion
    }
}
