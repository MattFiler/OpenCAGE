using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Keeps the Level Viewer's copy of the zone table up to date. What a zone covers is worked out by
    /// <see cref="ZoneMembership"/>; this decides when that is worth doing and sends the answer.
    /// </summary>
    /// <remarks>
    /// It runs here rather than in the viewer because zone membership is made entirely of links, and
    /// links are not part of the entity sync: the viewer's copy of the level would be right at load and
    /// wrong from the first rewiring onwards.
    /// </remarks>
    public static class ViewerZoneSync
    {
        //Edits arrive in bursts (a paste, an undo of a group), so a recalculation waits for the burst to end
        private const int CoalesceMilliseconds = 300;

        private static Timer _coalesce;
        private static bool _initialised;

        //A table written from wherever the selection's hierarchy starts, for marking a selected Zone's
        //contents - kept until an edit could have changed it (see ResolveZoneMembers)
        private static readonly object _selectionTableLock = new object();
        private static Composite _selectionTableFrom;
        private static List<SyncedZone> _selectionTable;
        private static int _generation;

        /// <summary>
        /// Whether the viewport is highlighting zones: Highlight Zones is on, and there is a viewport to
        /// draw them. The editor shows zone colours of its own (flowgraph nodes, the inspector) only then,
        /// since they are there to be matched against the level on screen.
        /// </summary>
        public static bool Showing => Singleton.ViewportEnabled && SettingsManager.GetBool(Settings.ShowZones);

        /// <summary>
        /// The table last worked out for the viewport, written from the level root - exactly what it was
        /// sent, colours and all. Null while zones are not <see cref="Showing"/>.
        /// </summary>
        public static List<SyncedZone> Current { get; private set; }

        /// <summary><see cref="Current"/> was replaced. Always raised on the UI thread.</summary>
        public static event Action CurrentChanged;

        public static void Initialise()
        {
            if (_initialised)
                return;
            _initialised = true;

            /* Anything that can move an entity in or out of a zone. There is no event for a link being
               made or broken, so the ones raised either side of that stand in for it: the inspector
               reloads both ends of an edited link, and the flowgraph reloads the composite it rewired. */
            Singleton.OnLevelLoaded += content =>
            {
                //The old level's table names nothing in the new one - drop it rather than colour by it
                //until the recalculation lands
                SetCurrent(null);
                MarkDirty();
            };
            Singleton.OnEntityAdded += entity => MarkDirty();
            Singleton.OnEntityDeleted += entity => MarkDirty();
            Singleton.OnEntityReloaded += entity => MarkDirty();
            Singleton.OnCompositeReloaded += composite => MarkDirty();
            Singleton.OnCompositeDeleted += composite => MarkDirty();
            Singleton.OnEntityRenamed += (entity, name) => MarkDirty();
            Singleton.OnParameterModified += MarkDirty;
        }

        /// <summary>
        /// The zone table has (or may have) changed - recalculate and send it once things settle.
        /// </summary>
        /// <remarks>
        /// Nothing is calculated while the overlay is off: a level's worth of link chasing is not free,
        /// and every edit would pay for it to produce something nobody is looking at. Turning the
        /// overlay on sends the table itself, so switching it on is never stale.
        /// </remarks>
        public static void MarkDirty()
        {
            //A selected Zone's contents are worked out whether or not the overlay is on
            DropSelectionTable();

            if (!SettingsManager.GetBool(Settings.ShowZones))
                return;

            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return;

            if (editor.InvokeRequired)
            {
                try
                {
                    editor.BeginInvoke(new Action(MarkDirty));
                }
                catch (Exception)
                {
                }
                return;
            }

            if (_coalesce == null)
            {
                _coalesce = new Timer() { Interval = CoalesceMilliseconds };
                _coalesce.Tick += (sender, args) =>
                {
                    _coalesce.Stop();
                    SendNow();
                };
            }

            //Restarting it is what coalesces the burst: only the last edit of a run actually fires
            _coalesce.Stop();
            _coalesce.Start();
        }

        /// <summary>Recalculate and send the zone table straight away.</summary>
        /// <remarks>
        /// Also what the editor's own zone colours are refreshed by, so the table is worked out whenever
        /// zones are <see cref="Showing"/> - including while the viewer is still starting and has not
        /// connected - and dropped the moment they are not.
        /// </remarks>
        public static void SendNow()
        {
            if (!Showing)
            {
                SetCurrent(null);
                return;
            }

            LevelContent content = Singleton.Editor?.CompositeBrowser?.Content;
            if (content == null || !content.IsLevelDataLoaded)
                return;

            List<SyncedZone> zones;
            try
            {
                zones = ZoneMembership.Calculate(content.Level);
            }
            catch (Exception ex)
            {
                Debug.Log("Websocket", "Failed to calculate zones for the viewport: " + ex.Message);
                return;
            }

            SetCurrent(zones);
            if (Send.Connected)
                Send.SendZonesPacket(zones);
        }

        private static void SetCurrent(List<SyncedZone> zones)
        {
            if (Current == null && zones == null)
                return;
            Current = zones;

            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return;

            if (editor.InvokeRequired)
            {
                try
                {
                    editor.BeginInvoke(new Action(() => CurrentChanged?.Invoke()));
                }
                catch (Exception)
                {
                }
                return;
            }

            CurrentChanged?.Invoke();
        }

        /// <summary>
        /// Where a Zone entity's contents are, for marking them when it is selected - the same roots the
        /// overlay colours from, so selecting a zone marks exactly what it is drawn over.
        /// </summary>
        /// <param name="from">
        /// The composite the hierarchy starts at, which is what the viewer's scene is built from: the
        /// paths returned are written from there.
        /// </param>
        /// <param name="drillPath">The entity ids stepped through from <paramref name="from"/> to the zone's composite.</param>
        /// <remarks>
        /// A zone is per placement, so the one wanted is the entry for this entity reached down this drill
        /// path, not any entry for it. The table is kept until the next edit that could have changed it:
        /// every packet carries the selection, and working out a level's zones costs around 200 ms on the
        /// larger levels - once per packet would be felt on every click.
        /// </remarks>
        internal static List<List<uint>> ResolveZoneMembers(
            Level level,
            Composite from,
            IReadOnlyList<uint> drillPath,
            Composite composite,
            FunctionEntity zone)
        {
            if (level?.Commands == null || from == null || composite == null || zone == null)
                return null;

            List<SyncedZone> table = SelectionTableFrom(level, from);
            if (table == null)
                return null;

            uint zoneId = zone.shortGUID.AsUInt32;
            uint compositeId = composite.shortGUID.AsUInt32;
            foreach (SyncedZone candidate in table)
            {
                if (candidate.zone_entity != zoneId || candidate.zone_composite != compositeId)
                    continue;
                if (!EntityInstancePath.Equal(candidate.zone_path, drillPath))
                    continue;

                //Two sequences can name the same entity; it is one thing to mark
                List<List<uint>> roots = new List<List<uint>>(candidate.roots.Count);
                foreach (List<uint> root in candidate.roots)
                {
                    bool seen = false;
                    foreach (List<uint> kept in roots)
                    {
                        if (EntityInstancePath.Equal(kept, root))
                        {
                            seen = true;
                            break;
                        }
                    }
                    if (!seen)
                        roots.Add(root);
                }
                return roots.Count == 0 ? null : roots;
            }

            return null;
        }

        private static List<SyncedZone> SelectionTableFrom(Level level, Composite from)
        {
            int generation;
            lock (_selectionTableLock)
            {
                if (_selectionTable != null && _selectionTableFrom == from)
                    return _selectionTable;
                generation = _generation;
            }

            List<SyncedZone> table;
            try
            {
                table = ZoneMembership.CalculateFrom(level, from);
            }
            catch (Exception ex)
            {
                Debug.Log("Websocket", "Failed to calculate zones for the selection: " + ex.Message);
                return null;
            }

            lock (_selectionTableLock)
            {
                //An edit landed while this was being worked out: use it this once, but don't keep it
                if (generation == _generation)
                {
                    _selectionTableFrom = from;
                    _selectionTable = table;
                }
            }
            return table;
        }

        private static void DropSelectionTable()
        {
            lock (_selectionTableLock)
            {
                _generation++;
                _selectionTable = null;
                _selectionTableFrom = null;
            }
        }
    }
}
