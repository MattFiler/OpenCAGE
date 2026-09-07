using System;
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

        public static void Initialise()
        {
            if (_initialised)
                return;
            _initialised = true;

            /* Anything that can move an entity in or out of a zone. There is no event for a link being
               made or broken, so the ones raised either side of that stand in for it: the inspector
               reloads both ends of an edited link, and the flowgraph reloads the composite it rewired. */
            Singleton.OnLevelLoaded += content => MarkDirty();
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
        public static void SendNow()
        {
            if (!Send.Connected || !SettingsManager.GetBool(Settings.ShowZones))
                return;

            LevelContent content = Singleton.Editor?.CompositeBrowser?.Content;
            if (content == null || !content.IsLevelDataLoaded)
                return;

            try
            {
                Send.SendZonesPacket(ZoneMembership.Calculate(content.Level));
            }
            catch (Exception ex)
            {
                Debug.Log("Websocket", "Failed to calculate zones for the viewport: " + ex.Message);
            }
        }
    }
}
