using CATHODE;
using CATHODE.Scripting;
using OpenCAGE.Markup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.UnityConnection
{
    /// <summary>
    /// Keeps the viewport's composite markup (COMPOSITE_MARKUP) in step with the level: the built-in set for the composites the
    /// level has, plus the level's own pieces. The whole set goes each time - it is a few hundred kilobytes at most - and only
    /// when it changed or the viewer has read the level afresh since it was last sent.
    /// </summary>
    public static class ViewerMarkupSync
    {
        private const int CoalesceMilliseconds = 200;

        private static bool _initialised;
        private static Timer _coalesce;
        private static string _lastSent;
        private static bool _viewerReloaded = true;

        public static void Initialise()
        {
            if (_initialised)
                return;
            _initialised = true;

            CompositeMarkup.Changed += Schedule;
            //Built-in markup follows composites by path, so a composite arriving, going or renamed can change the set
            Singleton.OnCompositeAdded += composite => Schedule();
            Singleton.OnCompositeDeleted += composite => Schedule();
            Singleton.OnCompositeRenamed += (composite, name) => Schedule();
            Singleton.OnLevelLoaded += content => Schedule();
        }

        /// <summary>LEVEL_LOADED went to the viewer: whatever it had is gone or may be.</summary>
        internal static void NotifyViewerReloading() => _viewerReloaded = true;

        /// <summary>The viewer finished a populate: send the set if it may not have it.</summary>
        internal static void NotifyViewerPopulated() => SendNow(onlyIfNeeded: true);

        private static void Schedule()
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return;
            if (editor.InvokeRequired)
            {
                try { editor.BeginInvoke(new Action(Schedule)); } catch { }
                return;
            }

            if (_coalesce == null)
            {
                _coalesce = new Timer() { Interval = CoalesceMilliseconds };
                _coalesce.Tick += (sender, args) =>
                {
                    _coalesce.Stop();
                    SendNow(onlyIfNeeded: true);
                };
            }
            _coalesce.Stop();
            _coalesce.Start();
        }

        /// <summary>Work the set out and send it (UI thread). With <paramref name="onlyIfNeeded"/>, an unchanged set the viewer already has stays put.</summary>
        public static void SendNow(bool onlyIfNeeded = false)
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed)
                return;
            if (editor.InvokeRequired)
            {
                try { editor.BeginInvoke(new Action(() => SendNow(onlyIfNeeded))); } catch { }
                return;
            }
            if (!Send.Connected)
                return;

            Commands commands = editor.CompositeBrowser?.Content?.Level?.Commands;
            List<SyncedCompositeMarkup> set;
            try
            {
                set = CompositeMarkup.BuildSyncSet(commands);
            }
            catch (Exception e)
            {
                Debug.Log("Markup", "Could not build the composite markup: " + e.Message);
                return;
            }

            string signature = (editor.CompositeBrowser?.Content?.Level?.Name ?? "") + "|" + Signature(set);
            if (onlyIfNeeded && !_viewerReloaded && signature == _lastSent)
                return;
            _lastSent = signature;
            _viewerReloaded = false;
            Send.SendCompositeMarkupPacket(set);
        }

        private static string Signature(List<SyncedCompositeMarkup> set)
        {
            unchecked
            {
                long hash = 17;
                foreach (SyncedCompositeMarkup entry in set)
                {
                    hash = hash * 31 + entry.composite;
                    foreach (SyncedMarkupSurface surface in entry.surfaces)
                    {
                        hash = hash * 31 + surface.colour_r.GetHashCode() * 7 + surface.colour_g.GetHashCode() * 13 + surface.colour_b.GetHashCode();
                        foreach (float v in surface.vertices) hash = hash * 31 + v.GetHashCode();
                        foreach (int i in surface.indices) hash = hash * 31 + i;
                    }
                }
                return set.Count + ":" + hash;
            }
        }
    }
}
