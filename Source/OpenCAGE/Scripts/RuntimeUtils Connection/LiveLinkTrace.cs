using CATHODE.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCAGE.RuntimeUtilsConnection
{
    /// <summary>
    /// The composite display's "Show Activity" (live link): flowgraph links light up as the running game uses them - a logic
    /// link when its entity fires the output it leaves from, a data link when the entity at its top reads the parameter
    /// through it or sends its value out through it - and the pins show how often each output fired and each method was
    /// called.
    ///
    /// The game gathers what happens in the composite instances it is asked to watch (LiveLink TRACE) and hands it over,
    /// counted and aged, when asked (TRACE_GET). Each open composite display watches the instance it shows - the one the
    /// user navigated to from the level's root, or every instance of its composite when it was opened from the browser -
    /// and the instances below it that its aliases reach into (LiveLinkActivity works those out, and maps what comes back
    /// onto its links). One pump on the thread pool sends the game what to watch whenever that changes (and on a new
    /// connection, or another level), asks for what was gathered every PollInterval, and hands it to the displays on the
    /// UI thread - one batch at a time: while one waits there the game is not asked again, and goes on gathering, so
    /// nothing is lost to a busy UI thread. Nothing here is sent from the UI thread.
    /// </summary>
    public static class LiveLinkTrace
    {
        /// <summary>How long a link glows after the game last used it, fading as it goes.</summary>
        public const double GlowMilliseconds = 1500;

        //How often the game is asked for what it gathered while activity is shown
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(150);
        //After the game turns a request away (another level running, no answer), the next waits this long: the reason
        //does not change from one poll to the next
        private static readonly TimeSpan RefusalBackoff = TimeSpan.FromSeconds(1);
        //More instances of one composite than this to watch (aliases reaching into many copies of the same thing): the game
        //is asked to watch every instance of it instead. It looks through a composite's watched instances each time one of
        //its entities does something, and many of a busy composite cost it; the displays narrow it down again.
        private const int MaxInstancesPerComposite = 16;

        private static readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

        /// <summary>Milliseconds on a clock that only goes forward: the times activity is kept at. Any thread.</summary>
        public static double NowMs => _clock.Elapsed.TotalMilliseconds;

        /// <summary>Whether links light up as the game uses them (the toolbar's Activity, the LiveLinkShowActivity setting). Any thread.</summary>
        public static bool ShowActivity => _show;

        /// <summary>Activity was switched on or off (in any display, or by the MCP tools). Raised on the UI thread.</summary>
        public static event Action ShowActivityChanged;

        /// <summary>The game's runtime utils are from before TRACE: nothing is shown until it is launched from OpenCAGE again.</summary>
        public static bool Unsupported => _unsupported;

        /// <summary>The game has taken the watches the open displays want (and is gathering for them). Any thread.</summary>
        public static bool Tracing
        {
            get
            {
                string sent = Volatile.Read(ref _sentKey);
                return sent != null && sent == Volatile.Read(ref _wanted).Key;
            }
        }

        /// <summary>The instances the game is asked to watch: as many as it takes at once, and how many more were left out.</summary>
        public static int WatchCount => Volatile.Read(ref _wanted).Watches.Count;
        public static int WatchesLeftOut => Volatile.Read(ref _wanted).LeftOut;

        /// <summary>
        /// How many watches the game takes at once: LiveLink.MaxTraceWatches, or LiveLink.OldMaxTraceWatches once a game whose
        /// runtime utils predate the larger limit turned more away (until the next connection).
        /// </summary>
        public static int WatchLimit => _watchLimit;

        /// <summary>How many batches the game has handed over since OpenCAGE started, and NowMs when the last arrived (or NaN).</summary>
        public static long Batches => Interlocked.Read(ref _batches);
        public static double LastBatchMs => Interlocked.CompareExchange(ref _lastBatchMs, 0, 0);

        /// <summary>Distinct activities the game could not keep for OpenCAGE (its store was full between takes), since OpenCAGE started.</summary>
        public static long Dropped => Interlocked.Read(ref _dropped);

        /// <summary>The last line put in the status bar about activity; null before the first.</summary>
        public static string LastStatus => Volatile.Read(ref _lastStatus);

        /// <summary>The displays that show activity (UI thread).</summary>
        public static IReadOnlyList<LiveLinkActivity> Displays => _displays;

        private static volatile bool _show;
        private static readonly List<LiveLinkActivity> _displays = new List<LiveLinkActivity>();

        /// <summary>What the game should be watching, as last worked out on the UI thread. Swapped in whole, never changed once made.</summary>
        private sealed class Wanted
        {
            /// <summary>The root composite of the level open here (LiveLink.RootOf): the game traces only while running it.</summary>
            public uint Root;
            public List<LiveLink.TraceWatch> Watches = new List<LiveLink.TraceWatch>();
            /// <summary>Root and watches as text, to tell whether they changed.</summary>
            public string Key = "";
            /// <summary>Watches left out: more than the game takes at once.</summary>
            public int LeftOut;
        }
        private static Wanted _wanted = new Wanted();

        //Bumped by every change and connection change: a pump standing down looks again if it moved meanwhile
        private static int _changes;
        //Bumped whenever the connection opens or closes: the pump then starts over with the game
        private static int _connection;
        private static int _pumping;    //1 while a pump runs: there is only ever one
        //Released by every change, so a pump waiting for its next poll looks again at once
        private static readonly SemaphoreSlim _wake = new SemaphoreSlim(0, 1);

        //The pump's own: only the one pump running writes these
        private static int _pumpChanges = -1;
        private static int _pumpConnection = -1;
        private static string _sentKey;         //the watches (Wanted.Key) the game took on this connection; null: none
        private static uint _sentRoot;          //the root they were sent with, for the stop
        private static bool _tracing;           //the game may be tracing for this connection: a stop is owed when nothing needs it
        private static volatile bool _unsupported;
        private static volatile int _watchLimit = LiveLink.MaxTraceWatches;
        //NowMs when the game last turned a request away, and when it was last asked for what it gathered
        private static double _refusedAt = double.NegativeInfinity;
        private static double _polledAt = double.NegativeInfinity;
        private static string _reported;        //what the status bar was last told, so each change is said once
        private static bool _toldUnreadable;

        private static long _batches;
        private static double _lastBatchMs = double.NaN;
        private static long _dropped;
        private static string _lastStatus;

        //Batches waiting for the UI thread, and whether a hand-over is posted there (1) - only ever one
        private static readonly object _deliverLock = new object();
        private static List<(LiveLink.TraceBatch batch, double receivedMs)> _undelivered = new List<(LiveLink.TraceBatch, double)>();
        private static int _deliveryPosted;

        private static int _refreshPosted;

        static LiveLinkTrace()
        {
            _show = SettingsManager.GetBool(Settings.LiveLinkShowActivity);

            //A game that connects is told what to watch; one that disconnects has stopped by itself
            LiveLink.ConnectionChanged += () =>
            {
                Interlocked.Increment(ref _connection);
                RequestRefresh();
                Kick();
            };
            //What the displays show changes with the level, the composite or instance on show, and its aliases
            Singleton.OnLevelLoaded += content => RequestRefresh();
            Singleton.OnCompositeSelected += composite => RequestRefresh();
            Singleton.OnCompositeReloaded += composite => RequestRefresh();
            Singleton.OnEntityAdded += entity => RequestRefresh();
            Singleton.OnEntityDeleted += entity => RequestRefresh();
            //The settings file edited by hand (or by another OpenCAGE)
            SettingsManager.SettingsChanged += (sender, e) =>
            {
                if (SettingsChangedEventArgs.ContainsKey(e.ChangedKeys, Settings.LiveLinkShowActivity))
                    RunOnUiThread(() => ApplyShowActivity(SettingsManager.GetBool(Settings.LiveLinkShowActivity)));
            };
        }

        #region DISPLAYS
        /// <summary>A composite display that shows activity, from when it is made until it is disposed. UI thread.</summary>
        public static void Register(LiveLinkActivity display)
        {
            if (display == null || _displays.Contains(display))
                return;
            _displays.Add(display);
            RequestRefresh();
        }

        public static void Unregister(LiveLinkActivity display)
        {
            if (display == null || !_displays.Remove(display))
                return;
            RequestRefresh();
        }

        /// <summary>
        /// Switch activity on or off - remembered across sessions, and in every display. Switched off, every display's
        /// highlights go and the game stops tracing. UI thread.
        /// </summary>
        public static void SetShowActivity(bool on)
        {
            if (SettingsManager.GetBool(Settings.LiveLinkShowActivity) != on)
                SettingsManager.SetBool(Settings.LiveLinkShowActivity, on);
            ApplyShowActivity(on);
        }

        private static void ApplyShowActivity(bool on)
        {
            if (_show == on)
                return;
            _show = on;
            if (!on)
            {
                foreach (LiveLinkActivity display in _displays.ToArray())
                    display.Clear();
            }
            ShowActivityChanged?.Invoke();
            Refresh();
            Kick();
        }

        /// <summary>
        /// Work out again what the game should watch, from the displays as they are now: each one's composite and instance
        /// (a display that moved somewhere else forgets what it had lit), and the instances its aliases reach into. The
        /// pump sends the game the change. UI thread.
        /// </summary>
        public static void Refresh()
        {
            bool connected = LiveLink.Connected;
            uint root = 0;
            List<LiveLink.TraceWatch> shown = new List<LiveLink.TraceWatch>();
            List<LiveLink.TraceWatch> reached = new List<LiveLink.TraceWatch>();
            foreach (LiveLinkActivity display in _displays.ToArray())
            {
                display.UpdatePlace();
                if (!display.HasPlace)
                    continue;
                if (root == 0)
                    root = display.Root;
                if (_show && connected && display.Root == root)
                    display.AddWatches(shown, reached);
            }

            Wanted wanted = new Wanted() { Root = root };
            int limit = _watchLimit;
            List<LiveLink.TraceWatch> watches = Collapse(Distinct(shown.Concat(reached)));
            //More than the game takes at once (a composite with aliases into many instances): those reached through aliases
            //are watched in every instance of their composite instead, which the displays narrow down again
            if (watches.Count > limit)
                watches = Collapse(Distinct(shown.Concat(reached.Select(o => new LiveLink.TraceWatch() { Composite = o.Composite, Path = null }))));
            if (watches.Count > limit)
            {
                wanted.LeftOut = watches.Count - limit;
                watches = watches.Take(limit).ToList();
            }
            wanted.Watches = watches;
            wanted.Key = watches.Count == 0 ? "" : root.ToString("X8") + "|" + string.Join(";", watches.Select(o => o.Key));

            if (wanted.Key == Volatile.Read(ref _wanted).Key && wanted.LeftOut == Volatile.Read(ref _wanted).LeftOut)
                return;
            Volatile.Write(ref _wanted, wanted);
            Kick();
        }

        /// <summary>Watches without repeats; one of an instance whose composite is watched in every instance anyway is left out.</summary>
        private static List<LiveLink.TraceWatch> Distinct(IEnumerable<LiveLink.TraceWatch> watches)
        {
            List<LiveLink.TraceWatch> all = watches.ToList();
            HashSet<uint> everywhere = new HashSet<uint>(all.Where(o => o.Path == null).Select(o => o.Composite.AsUInt32));
            HashSet<string> seen = new HashSet<string>();
            List<LiveLink.TraceWatch> distinct = new List<LiveLink.TraceWatch>();
            foreach (LiveLink.TraceWatch watch in all)
            {
                if (watch.Path != null && everywhere.Contains(watch.Composite.AsUInt32))
                    continue;
                if (seen.Add(watch.Key))
                    distinct.Add(watch);
            }
            return distinct;
        }

        /// <summary>
        /// Watches (without repeats) with every composite watched in more than MaxInstancesPerComposite instances watched in
        /// every instance instead, where its first one was.
        /// </summary>
        private static List<LiveLink.TraceWatch> Collapse(List<LiveLink.TraceWatch> watches)
        {
            Dictionary<uint, int> instances = new Dictionary<uint, int>();
            bool many = false;
            foreach (LiveLink.TraceWatch watch in watches)
            {
                if (watch.Path == null)
                    continue;
                uint composite = watch.Composite.AsUInt32;
                instances.TryGetValue(composite, out int count);
                instances[composite] = ++count;
                many |= count > MaxInstancesPerComposite;
            }
            if (!many)
                return watches;
            List<LiveLink.TraceWatch> collapsed = new List<LiveLink.TraceWatch>();
            HashSet<uint> everywhere = new HashSet<uint>();
            foreach (LiveLink.TraceWatch watch in watches)
            {
                uint composite = watch.Composite.AsUInt32;
                if (watch.Path != null && instances[composite] > MaxInstancesPerComposite)
                {
                    if (everywhere.Add(composite))
                        collapsed.Add(new LiveLink.TraceWatch() { Composite = watch.Composite, Path = null });
                    continue;
                }
                collapsed.Add(watch);
            }
            return Distinct(collapsed);
        }

        /// <summary>Refresh soon, on the UI thread (once, however many ask before it runs). Any thread.</summary>
        public static void RequestRefresh()
        {
            if (Interlocked.Exchange(ref _refreshPosted, 1) != 0)
                return;
            if (!RunOnUiThread(() =>
            {
                Volatile.Write(ref _refreshPosted, 0);
                Refresh();
            }))
                Volatile.Write(ref _refreshPosted, 0);
        }

        //Posted, even from the UI thread: these come from inside edits and navigation, which are better left to finish first
        private static bool RunOnUiThread(Action action)
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed || !editor.IsHandleCreated)
                return false;
            try
            {
                editor.BeginInvoke(action);
                return true;
            }
            catch
            {
                //Closing
                return false;
            }
        }
        #endregion

        #region PUMP
        private static void Kick()
        {
            Interlocked.Increment(ref _changes);
            if (_wake.CurrentCount == 0)
            {
                try
                {
                    _wake.Release();
                }
                catch (SemaphoreFullException)
                {
                    //Another change woke it already
                }
            }
            if (Interlocked.CompareExchange(ref _pumping, 1, 0) != 0)
                return;
            Task.Run(() => Pump());
        }

        private static async Task Pump()
        {
            try
            {
                while (true)
                {
                    if (await Step())
                        continue;

                    //Nothing left to do: stand down, then look once more - a change that came in just before found the pump
                    //still running, so did not start another
                    Volatile.Write(ref _pumping, 0);
                    if (Volatile.Read(ref _changes) == _pumpChanges || Interlocked.CompareExchange(ref _pumping, 1, 0) != 0)
                        return;
                }
            }
            catch (Exception ex)
            {
                Debug.Log("Live Link", "Showing script activity failed: " + ex.Message);
                Volatile.Write(ref _pumping, 0);
            }
        }

        /// <summary>Send the game what to watch, or take what it gathered. True while there is more to do. Pump thread.</summary>
        private static async Task<bool> Step()
        {
            _pumpChanges = Volatile.Read(ref _changes);

            int connection = Volatile.Read(ref _connection);
            if (connection != _pumpConnection)
            {
                //Another connection, or none: the game traces nothing for it yet (and a game whose runtime utils were too old
                //may have been relaunched with newer ones)
                _pumpConnection = connection;
                Volatile.Write(ref _sentKey, null);
                _tracing = false;
                _unsupported = false;
                _refusedAt = double.NegativeInfinity;
                _reported = null;
                //...and a game that took fewer watches at once may have been updated: the displays' watches are worked out
                //again for the full number
                if (_watchLimit != LiveLink.MaxTraceWatches)
                {
                    _watchLimit = LiveLink.MaxTraceWatches;
                    RequestRefresh();
                }
            }

            if (!LiveLink.Connected)
            {
                //A game that disconnects stops tracing by itself, and there is nothing to ask until one connects (that kicks
                //the pump again)
                Volatile.Write(ref _sentKey, null);
                _tracing = false;
                return false;
            }

            Wanted wanted = Volatile.Read(ref _wanted);
            if (!_show || wanted.Watches.Count == 0)
            {
                if (!_tracing)
                    return false;
                //Nothing shows activity any more: the game stops gathering it
                _tracing = false;
                Volatile.Write(ref _sentKey, null);
                LiveLink.Reply stopped = await LiveLink.SetTrace(_sentRoot, null);
                if (!stopped.Ok && stopped.Answered && !LiveLink.IsUnknownRequest(stopped))
                    Debug.Log("Live Link", "The game did not stop tracing: " + stopped.Message);
                return true;
            }

            if (_unsupported)
                return false;

            if (wanted.Key != Volatile.Read(ref _sentKey))
            {
                //Worked out for more watches than the game takes: the displays' watches are being worked out again for fewer
                if (wanted.Watches.Count > _watchLimit)
                {
                    await _wake.WaitAsync(PollInterval);
                    return true;
                }
                if (await WaitOutRefusal())
                    return true;
                LiveLink.Reply reply = await LiveLink.SetTrace(wanted.Root, wanted.Watches);
                if (LiveLink.IsUnknownRequest(reply))
                {
                    _unsupported = true;
                    Report("Live Link: the game's OpenCAGE_Utils.asi is too old to show script activity - launch the game from OpenCAGE again to update it");
                    return true;
                }
                if (!reply.Ok && reply.Answered && reply.Message == "Malformed request" && wanted.Watches.Count > LiveLink.OldMaxTraceWatches && _watchLimit > LiveLink.OldMaxTraceWatches)
                {
                    //Runtime utils from before the larger limit take at most 32 at once: watch that many (those reached through
                    //aliases in every instance of their composite first, as ever), until a newer game connects
                    _watchLimit = LiveLink.OldMaxTraceWatches;
                    Debug.Log("Live Link", "The game's OpenCAGE_Utils.asi takes at most " + LiveLink.OldMaxTraceWatches + " composite instances to show script activity in at once (" + wanted.Watches.Count + " were asked for) - launch the game from OpenCAGE again to update it");
                    RequestRefresh();
                    return true;
                }
                //Not answered, it may have been taken all the same: told to stop either way once nothing needs it
                if (reply.Ok || !reply.Answered)
                {
                    _tracing = true;
                    _sentRoot = wanted.Root;
                }
                if (reply.Ok)
                {
                    Volatile.Write(ref _sentKey, wanted.Key);
                    _refusedAt = double.NegativeInfinity;
                    //Nothing is gathered yet: the first take is a poll from now
                    _polledAt = NowMs;
                    string leftOut = "";
                    if (wanted.LeftOut > 0)
                        leftOut = " (" + wanted.LeftOut + " instance(s) reached through aliases are left out - at most " + wanted.Watches.Count + " at once"
                            + (_watchLimit < LiveLink.MaxTraceWatches ? " with this game's OpenCAGE_Utils.asi: launch the game from OpenCAGE again to update it" : "") + ")";
                    else if (_watchLimit < LiveLink.MaxTraceWatches)
                        leftOut = " (this game's OpenCAGE_Utils.asi watches at most " + _watchLimit + " instances at once: launch the game from OpenCAGE again to update it)";
                    Report("Live Link: showing script activity" + leftOut);
                }
                else
                {
                    //Turned away for another level, the game stopped what it was tracing; not answered, what it traces is not
                    //known: either way the watches are sent again, even if they come back to the ones it took last
                    Volatile.Write(ref _sentKey, null);
                    _refusedAt = NowMs;
                    Report("Live Link: script activity can't be shown - " + reply.Message);
                }
                return true;
            }

            //One batch at a time waits for the UI thread: until it is taken there the game goes on gathering
            if (Volatile.Read(ref _deliveryPosted) != 0)
            {
                await _wake.WaitAsync(PollInterval);
                return true;
            }

            double waitMs = _polledAt + PollInterval.TotalMilliseconds - NowMs;
            if (waitMs > 0)
            {
                await _wake.WaitAsync(TimeSpan.FromMilliseconds(waitMs));
                return true;
            }
            _polledAt = NowMs;
            LiveLink.TraceBatch batch = await LiveLink.GetTrace(wanted.Root);
            double received = NowMs;
            if (batch.Unsupported)
            {
                _unsupported = true;
                Report("Live Link: the game's OpenCAGE_Utils.asi is too old to show script activity - launch the game from OpenCAGE again to update it");
                return true;
            }
            if (!batch.Reply.Ok)
            {
                //Another level running (or no answer): what to watch is sent again once that is over, as the game may have
                //dropped it meanwhile
                Volatile.Write(ref _sentKey, null);
                _refusedAt = NowMs;
                Report("Live Link: script activity can't be shown - " + batch.Reply.Message);
                return true;
            }
            if (!batch.Valid)
            {
                if (!_toldUnreadable)
                {
                    _toldUnreadable = true;
                    Debug.Log("Live Link", "Could not read the game's script activity (\"" + batch.Reply.Message + "\", " + (batch.Reply.Payload?.Length ?? 0) + " bytes)");
                }
                return true;
            }
            Interlocked.Increment(ref _batches);
            Interlocked.Exchange(ref _lastBatchMs, received);
            if (batch.Dropped != 0)
                Interlocked.Add(ref _dropped, batch.Dropped);
            if (batch.Records.Count != 0)
                Deliver(batch, received);
            return true;
        }

        /// <summary>Turned away (or not answered) a moment ago: wait out the rest of that. True when it waited (look again after). Pump thread.</summary>
        private static async Task<bool> WaitOutRefusal()
        {
            double waitMs = _refusedAt + RefusalBackoff.TotalMilliseconds - NowMs;
            if (waitMs <= 0)
                return false;
            await _wake.WaitAsync(TimeSpan.FromMilliseconds(waitMs));
            return true;
        }

        /// <summary>Hand a batch to the displays, on the UI thread. Pump thread.</summary>
        private static void Deliver(LiveLink.TraceBatch batch, double receivedMs)
        {
            lock (_deliverLock)
            {
                _undelivered.Add((batch, receivedMs));
                if (_deliveryPosted != 0)
                    return;
                _deliveryPosted = 1;
            }
            if (!RunOnUiThread(DeliverOnUiThread))
            {
                lock (_deliverLock)
                {
                    _undelivered.Clear();
                    _deliveryPosted = 0;
                }
            }
        }

        private static void DeliverOnUiThread()
        {
            List<(LiveLink.TraceBatch batch, double receivedMs)> batches;
            lock (_deliverLock)
            {
                batches = _undelivered;
                _undelivered = new List<(LiveLink.TraceBatch, double)>();
                _deliveryPosted = 0;
            }
            //The pump may be waiting on this to ask the game again
            Kick();
            if (!_show)
                return;
            foreach (LiveLinkActivity display in _displays.ToArray())
            {
                try
                {
                    display.Apply(batches);
                }
                catch (Exception ex)
                {
                    Debug.Log("Live Link", "Could not show script activity: " + ex.Message);
                }
            }
        }
        #endregion

        /// <summary>Tell the status bar, when it is something other than what it was last told. Pump thread.</summary>
        private static void Report(string text)
        {
            if (text == _reported)
                return;
            _reported = text;
            Volatile.Write(ref _lastStatus, text);
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed || !editor.IsHandleCreated)
                return;
            try
            {
                editor.ShowLiveLinkActivity(text); //moves itself to the UI thread
            }
            catch
            {
                //Closing
            }
        }
    }
}
