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
    /// <remarks>
    /// Read off the table the viewport was sent (<see cref="ViewerZoneSync.Current"/>) rather than worked
    /// out again, so the two cannot disagree - and there is only anything to show while the viewport is
    /// highlighting zones.
    ///
    /// That table is written from the level root, and a zone is per placement: an entity in a composite
    /// placed twice can be in a different zone in each. So the answer is for the drill path the user took
    /// to get here, and there is one only when that path starts at the level root. A composite opened on
    /// its own is what the viewport shows on its own too, and it draws no zones there either.
    /// </remarks>
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
            public SyncedZone Zone;
            public Color Colour;
            public Relation Relation;

            /// <summary>What the colour is saying, for a tooltip.</summary>
            public string Describe()
            {
                string name = string.IsNullOrWhiteSpace(Zone?.name) ? "a zone" : Zone.name;
                switch (Relation)
                {
                    case Relation.IsZone:
                        return "Zone " + name;
                    case Relation.FillsZone:
                        return "Fills zone " + name;
                    default:
                        return "In zone " + name;
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

        private ZoneColours(Composite composite, List<uint> drillPath, List<SyncedZone> table)
        {
            uint compositeId = composite.shortGUID.AsUInt32;
            int depth = drillPath.Count;
            int inheritedLength = 0;
            SyncedZone inherited = null;

            /* Where roots meet, the viewport keeps the innermost, and a root two zones both name goes to
               the first of them - so a longer match replaces a shorter one, and an equal one never does. */
            foreach (SyncedZone zone in table)
            {
                if (zone?.roots == null)
                    continue;

                if (zone.zone_composite == compositeId && EntityInstancePath.Equal(zone.zone_path, drillPath) && !_own.ContainsKey(zone.zone_entity))
                    _own[zone.zone_entity] = new Match() { Zone = zone, Colour = ColourOf(zone), Relation = Relation.IsZone };

                foreach (List<uint> root in zone.roots)
                {
                    if (root == null || root.Count == 0)
                        continue;

                    if (root.Count <= depth)
                    {
                        if (root.Count > inheritedLength && StartsWith(drillPath, root))
                        {
                            inherited = zone;
                            inheritedLength = root.Count;
                        }
                    }
                    else if (root.Count == depth + 1 && StartsWith(root, drillPath) && !_named.ContainsKey(root[depth]))
                    {
                        _named[root[depth]] = new Match() { Zone = zone, Colour = ColourOf(zone), Relation = Relation.InZone };
                    }
                }
            }

            if (inherited != null)
                _inherited = new Match() { Zone = inherited, Colour = ColourOf(inherited), Relation = Relation.InZone };

            //The sequences each zone here is filled from, which are how the zone is edited
            foreach (Match zone in new List<Match>(_own.Values))
            {
                Entity zoneEntity = composite.GetEntityByID(new ShortGuid(zone.Zone.zone_entity));
                if (zoneEntity == null)
                    continue;

                foreach (EntityConnector link in zoneEntity.childLinks)
                {
                    if (link.thisParamID != ShortGuids.composites)
                        continue;
                    if (!(composite.GetEntityByID(link.linkedEntityID) is TriggerSequence))
                        continue;

                    uint id = link.linkedEntityID.AsUInt32;
                    if (!_own.ContainsKey(id))
                        _own[id] = new Match() { Zone = zone.Zone, Colour = zone.Colour, Relation = Relation.FillsZone };
                }
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

            return new ZoneColours(composite, drillPath, table);
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
            return _inherited;
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
