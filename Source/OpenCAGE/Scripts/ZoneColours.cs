using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OpenCAGE
{
    /// <summary>
    /// Which zone each entity in the open composite goes with, in the colour the viewport draws that
    /// zone in - so a flowgraph node or the inspector can be matched against the level at a glance.
    /// </summary>
    public sealed class ZoneColours
    {
        /// <summary>How an entity goes with the zone it is shown in.</summary>
        public enum Relation
        {
            /// <summary>It is the Zone entity: its own colour.</summary>
            IsZone,
            /// <summary>A TriggerSequence the zone takes its contents from.</summary>
            FillsZone,
            /// <summary>The zone claims it - the innermost one, as the viewport draws it.</summary>
            InZone,
        }

        public sealed class Match
        {
            /// <summary>The zone the viewport draws it in - the innermost, and of those the first.</summary>
            public SyncedZone Zone;
            public Color Colour;
            public Relation Relation;

            /// <summary>
            /// Every zone it goes with at that same depth, <see cref="Zone"/> first. Zones overlap - a room's zone usually takes
            /// in the corridors either side - and the viewport can draw a mesh in one colour only, so the flowgraph stripes in
            /// all of them: each Zone node's colour is then on every entity it lists.
            /// </summary>
            public List<SyncedZone> Zones = new List<SyncedZone>();
            public List<Color> Colours = new List<Color>();

            public Match(SyncedZone zone, Relation relation)
            {
                Zone = zone;
                Colour = ColourOf(zone);
                Relation = relation;
                Zones.Add(zone);
                Colours.Add(Colour);
            }

            public void Add(SyncedZone zone)
            {
                if (Zones.Contains(zone))
                    return;
                Zones.Add(zone);
                Colours.Add(ColourOf(zone));
            }

            /// <summary>What the colour is saying, for a tooltip.</summary>
            public string Describe()
            {
                string name = string.Join(", ", Zones.ConvertAll(o => string.IsNullOrWhiteSpace(o?.name) ? "a zone" : o.name));
                string plural = Zones.Count == 1 ? "zone " : "zones ";
                switch (Relation)
                {
                    case Relation.IsZone:
                        return "Zone " + name;
                    case Relation.FillsZone:
                        return "Fills " + plural + name;
                    default:
                        return "In " + plural + name;
                }
            }
        }

        //The zones placed in this composite at this drill path, and the sequences they take their
        //contents from - these are shown in the zone they ARE, rather than the zone they sit in
        private readonly Dictionary<uint, Match> _own = new Dictionary<uint, Match>();

        //Roots naming an entity of this composite directly, and the innermost root the composite
        //itself sits under (a zone claims everything inside what it names)
        private readonly Dictionary<uint, Match> _named = new Dictionary<uint, Match>();
        private readonly Match _inherited;

        //For an alias or proxy, which is shown in the zone of what it points at - wherever in the level that is
        private readonly Commands _commands;
        private readonly Composite _composite;
        private readonly List<uint> _drillPath;
        private readonly List<SyncedZone> _table;
        private Dictionary<string, Match> _rootsByPath;
        private Dictionary<string, Match> _zonesByPath;

        private ZoneColours(Commands commands, Composite composite, List<uint> drillPath, List<SyncedZone> table)
        {
            _commands = commands;
            _composite = composite;
            _drillPath = drillPath;
            _table = table;

            uint compositeId = composite.shortGUID.AsUInt32;
            int depth = drillPath.Count;
            int inheritedLength = 0;

            /* Where roots meet, the viewport keeps the innermost, and a root two zones both name goes to
               the first of them - so a longer match replaces a shorter one, and an equal one never does
               (it is added to the match's other zones, which the stripes show too). */
            foreach (SyncedZone zone in table)
            {
                if (zone?.roots == null)
                    continue;

                if (zone.zone_composite == compositeId && EntityInstancePath.Equal(zone.zone_path, drillPath) && !_own.ContainsKey(zone.zone_entity))
                    _own[zone.zone_entity] = new Match(zone, Relation.IsZone);

                foreach (List<uint> root in zone.roots)
                {
                    if (root == null || root.Count == 0)
                        continue;

                    if (root.Count <= depth)
                    {
                        if (!StartsWith(drillPath, root))
                            continue;
                        if (root.Count > inheritedLength)
                        {
                            _inherited = new Match(zone, Relation.InZone);
                            inheritedLength = root.Count;
                        }
                        else if (root.Count == inheritedLength)
                        {
                            _inherited.Add(zone);
                        }
                    }
                    else if (root.Count == depth + 1 && StartsWith(root, drillPath))
                    {
                        if (_named.TryGetValue(root[depth], out Match named))
                            named.Add(zone);
                        else
                            _named[root[depth]] = new Match(zone, Relation.InZone);
                    }
                }
            }

            //The sequences each zone here is filled from, which are how the zone is edited - and the pins a zone in a reusable
            //composite takes its contents through, which are filled from outside it
            foreach (Match zone in new List<Match>(_own.Values))
            {
                Entity zoneEntity = composite.GetEntityByID(new ShortGuid(zone.Zone.zone_entity));
                if (zoneEntity != null)
                    MarkFillers(zoneEntity, zone.Zone);
            }

            //An alias or proxy of a zone can carry 'composites' links of its own, which fill the zone it points at
            //(ZoneMembership.AddCarriedRoots) - wherever in the level that zone is
            List<Entity> pointers = new List<Entity>(composite.aliases);
            pointers.AddRange(composite.proxies);
            foreach (Entity pointer in pointers)
            {
                if (!pointer.childLinks.Exists(link => link.thisParamID == ShortGuids.composites))
                    continue;
                if (TryFindPointedAt(pointer, out Match pointed) && pointed?.Relation == Relation.IsZone)
                    MarkFillers(pointer, pointed.Zone);
            }
        }

        //The sequences and pins on 'holder's 'composites' pin, shown as filling 'zone'
        private void MarkFillers(Entity holder, SyncedZone zone)
        {
            foreach (EntityConnector link in holder.childLinks)
            {
                if (link.thisParamID != ShortGuids.composites)
                    continue;
                Entity filler = _composite.GetEntityByID(link.linkedEntityID);
                if (!(filler is TriggerSequence || filler is VariableEntity))
                    continue;

                uint id = link.linkedEntityID.AsUInt32;
                if (!_own.TryGetValue(id, out Match fills))
                    _own[id] = new Match(zone, Relation.FillsZone);
                else if (fills.Relation == Relation.FillsZone)
                    fills.Add(zone); //one sequence filling several zones here
            }
        }

        /// <summary>
        /// The zones for the entities of the composite open at the end of <paramref name="path"/>, or null
        /// when there is nothing to show: zones are not being highlighted, or the path does not start at
        /// the level root.
        /// </summary>
        public static ZoneColours For(Composite composite, CompositePath path)
        {
            if (composite == null || !ViewerZoneSync.Showing)
                return null;

            List<SyncedZone> table = ViewerZoneSync.Current;
            Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
            if (table == null || commands?.EntryPoints == null || commands.EntryPoints.Length == 0)
                return null;

            Composite start = path != null && path.AllComposites.Count != 0 ? path.AllComposites[0] : composite;
            if (start != commands.EntryPoints[0])
                return null;

            List<uint> drillPath = new List<uint>();
            if (path != null)
            {
                foreach (Entity step in path.AllEntities)
                    drillPath.Add(step.shortGUID.AsUInt32);
            }

            return new ZoneColours(commands, composite, drillPath, table);
        }

        /// <summary>The zone an entity of this composite is shown in, or null for none.</summary>
        public Match Find(Entity entity)
        {
            if (entity == null)
                return null;

            uint id = entity.shortGUID.AsUInt32;
            if (_own.TryGetValue(id, out Match own))
                return own;
            if (_named.TryGetValue(id, out Match named))
                return named;
            //One whose target is in no zone is shown in none, as the target is drawn; one pointing nowhere falls back on
            //where it sits
            if ((entity is AliasEntity || entity is ProxyEntity) && TryFindPointedAt(entity, out Match pointed))
                return pointed;
            return _inherited;
        }

        /* An alias or proxy draws nothing of its own: what the viewport colours is the entity it points at, which is
           usually deeper than this composite (an alias reaches into a placement) and for a proxy anywhere in the level. No
           zone names the alias itself - a zone's roots name what an alias stands for - so it is matched by where it points:
           the innermost root holding the target, as the viewport colours it, or the zone itself for an alias of a Zone. */
        private bool TryFindPointedAt(Entity pointer, out Match match)
        {
            match = null;
            if (!EntityInstancePath.TryResolvePointer(_commands, _composite, _drillPath, pointer, out List<uint> target, out bool _))
                return false;

            if (_rootsByPath == null)
                IndexTable();

            List<string> prefixes = new List<string>(target.Count);
            System.Text.StringBuilder key = new System.Text.StringBuilder();
            for (int i = 0; i < target.Count; i++)
            {
                if (i != 0)
                    key.Append(',');
                key.Append(target[i]);
                prefixes.Add(key.ToString());
            }

            if (prefixes.Count != 0 && _zonesByPath.TryGetValue(prefixes[prefixes.Count - 1], out Match zone))
            {
                match = zone;
                return true;
            }
            for (int i = prefixes.Count - 1; i >= 0; i--)
            {
                if (_rootsByPath.TryGetValue(prefixes[i], out Match root))
                {
                    match = root;
                    return true;
                }
            }
            return true;
        }

        //Every root by its path - the first zone to name one is the one the viewport draws, the rest go with it - and every
        //zone by its own (its placement's path with the Zone on the end)
        private void IndexTable()
        {
            _rootsByPath = new Dictionary<string, Match>();
            _zonesByPath = new Dictionary<string, Match>();
            foreach (SyncedZone zone in _table)
            {
                if (zone == null)
                    continue;

                List<uint> own = new List<uint>(zone.zone_path ?? new List<uint>()) { zone.zone_entity };
                string ownKey = string.Join(",", own);
                if (!_zonesByPath.ContainsKey(ownKey))
                    _zonesByPath[ownKey] = new Match(zone, Relation.IsZone);

                if (zone.roots == null)
                    continue;
                foreach (List<uint> root in zone.roots)
                {
                    if (root == null || root.Count == 0)
                        continue;
                    string rootKey = string.Join(",", root);
                    if (_rootsByPath.TryGetValue(rootKey, out Match named))
                        named.Add(zone);
                    else
                        _rootsByPath[rootKey] = new Match(zone, Relation.InZone);
                }
            }
        }

        /// <summary>The colour the viewport draws a zone in.</summary>
        public static Color ColourOf(SyncedZone zone)
        {
            return Color.FromArgb(ToByte(zone.colour_r), ToByte(zone.colour_g), ToByte(zone.colour_b));
        }

        private static int ToByte(float channel)
        {
            return Math.Max(0, Math.Min(255, (int)Math.Round(channel * 255f)));
        }

        private static bool StartsWith(List<uint> path, List<uint> prefix)
        {
            if (prefix.Count > path.Count)
                return false;
            for (int i = 0; i < prefix.Count; i++)
            {
                if (path[i] != prefix[i])
                    return false;
            }
            return true;
        }
    }
}
