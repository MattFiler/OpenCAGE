using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCAGE.RuntimeUtilsConnection
{
    public static partial class LiveLink
    {
        /// <summary>
        /// Run one of the game's Debug Menu commands (as DebugMenu's RunCommand does), waiting at most timeoutSeconds for its
        /// answer: the script debugger's requests want a quick answer or none.
        /// </summary>
        public static Task<Reply> DebugMenuCommand(string name, string argument, double timeoutSeconds)
        {
            return Request(Command.DEBUG_MENU_CONTROL, w =>
            {
                w.Write((byte)DebugMenuOp.RunCommand);
                WriteString(w, name ?? "");
                WriteString(w, argument ?? "");
            }, timeoutSeconds);
        }

        /// <summary>
        /// The Debug Menu's report (DebugMenu's Report), waiting at most timeoutSeconds. The game answers it without putting
        /// anything on screen (a command shows its name in the corner for a few seconds), so it is what is polled.
        /// </summary>
        public static Task<Reply> DebugMenuReport(double timeoutSeconds)
        {
            return Request(Command.DEBUG_MENU_CONTROL, w => w.Write((byte)DebugMenuOp.Report), timeoutSeconds);
        }
    }

    /// <summary>
    /// A script breakpoint as the game holds it (see LiveLinkDebugger): an entity of a composite - in the one instance at a
    /// path of composite instance entity ids from the level's root, or in every running instance - and the methods whose
    /// call stops the scripts.
    /// </summary>
    public sealed class ScriptBreakpoint
    {
        /// <summary>The level it is for (its root composite, LiveLink.RootOf).</summary>
        public uint Root;
        public ShortGuid Composite;
        /// <summary>Instance entity ids from the root to the instance (empty: the root itself); null for every instance.</summary>
        public List<ShortGuid> Path;
        public ShortGuid Entity;
        public List<ShortGuid> Methods = new List<ShortGuid>();
        public string Label = "";
        /// <summary>The game's number for it; 0 while the game does not have it (not connected, or not sent yet).</summary>
        public int GameId;
        public bool Enabled = true;
        public uint Hits;
        /// <summary>Running instances the game found it in, and the method calls it stops on there.</summary>
        public int Instances;
        public int Records;
        /// <summary>Why the game has it in no running instance (its composite not in the level, another level running...).</summary>
        public string Problem;

        public string Key => KeyOf(Composite, Path, Entity);

        /// <summary>The game stops on it now.</summary>
        public bool Armed => GameId != 0 && Enabled && Records > 0;

        public static string KeyOf(ShortGuid composite, IList<ShortGuid> path, ShortGuid entity)
        {
            return composite.AsUInt32.ToString("X8") + "/" + (path == null ? "*" : string.Join(",", path.Select(o => o.AsUInt32.ToString("X8")))) + "/" + entity.AsUInt32.ToString("X8");
        }

        public bool SamePlace(ShortGuid composite, IList<ShortGuid> path, ShortGuid entity)
        {
            if (Composite != composite || Entity != entity || (Path == null) != (path == null))
                return false;
            return Path == null || Path.SequenceEqual(path);
        }
    }

    /// <summary>Where the scripts were stopped, as the game told it (debugger.state).</summary>
    public sealed class ScriptHit
    {
        /// <summary>The scripts are held by the debugger now (at a breakpoint, after a step, or paused).</summary>
        public bool Held;
        /// <summary>Held at the breakpoint itself (not after a step or a pause).</summary>
        public bool AtBreakpoint;
        /// <summary>Counts up with every hit (0: none yet).</summary>
        public uint Sequence;
        /// <summary>The game's number of the breakpoint hit.</summary>
        public int Breakpoint;
        /// <summary>The entity the held call goes to, the method, the entity's type, and where it is: the composite of the
        /// instance holding it, that instance's id as the game names it, and its path of instance entity ids from the root.</summary>
        public uint Entity, Method, Type, Composite, Instance;
        public string CompositeName = "";
        public List<uint> Path = new List<uint>();
        public double Time;
        public int Steps;
        /// <summary>The calls whose methods fired the outputs that led to it, latest first.</summary>
        public List<(uint entity, uint method, uint composite, List<uint> path)> Chain = new List<(uint, uint, uint, List<uint>)>();
    }

    /// <summary>
    /// OpenCAGE as a script debugger, over Live Link and the game's Debug Menu (its debugger.* commands, in the runtime
    /// utils' LIVE_DEBUGGER.cpp): breakpoints on flowgraph nodes stop the level's scripts - and the game - when a method of
    /// the node's entity is about to be called; OpenCAGE then brings that node to the front, and Continue or Step let the
    /// scripts go on.
    ///
    /// The breakpoints of the level open here are kept here, as the game last listed them, and are what the flowgraphs
    /// draw. While the game is connected (with its Debug Menu on) it is the one that holds them: a toggle is sent to it and
    /// the list read back. Ones set before (a game restarted since, say) are sent again when it connects.
    ///
    /// One pump on the thread pool polls the Debug Menu's report a few times a second while there are breakpoints or the
    /// scripts are held (the report carries the debugger's held flag, hit count and list version as values, and costs the
    /// game nothing on screen); when the hit count moves it reads where (debugger.state) and hands it to the UI thread, and
    /// when the list version moves it reads the list again. Nothing is sent from the UI thread but what the user asks.
    /// </summary>
    public static class LiveLinkDebugger
    {
        //How often the game is polled while anything needs it, and how long one answer may take
        private const int PollMs = 250;
        private const double PollTimeoutSeconds = 2.0;
        private const double CommandTimeoutSeconds = 5.0;

        private static readonly Dictionary<string, ScriptBreakpoint> _set = new Dictionary<string, ScriptBreakpoint>();
        private static ScriptHit _hit = new ScriptHit();
        private static volatile string _unavailable;
        private static volatile bool _held;
        private static volatile bool _panelOpen;
        private static volatile bool _showStates;

        //Bumped when the connection opens or closes; the pump's own copy of it, and of what it last saw from the game
        private static int _connection;
        private static int _pumping;
        private static readonly SemaphoreSlim _wake = new SemaphoreSlim(0, 1);
        private static int _pumpConnection = -1;
        private static bool _synced;
        private static string _lastHit;
        private static string _lastVersion;
        private static bool _lastHeld;
        //Where the scripts are held is to be read (again: the last read of it failed)
        private static bool _stateWanted;

        //Breakpoints changed here while the game could not take the change (UI thread), and a sync of them is due
        private static readonly HashSet<string> _dirty = new HashSet<string>();
        private static volatile bool _pendingSync;

        /// <summary>
        /// The game's breakpoints switch was turned off here as Live Link went off or OpenCAGE closed (DisarmOnLeave). Kept in
        /// the settings, so the next OpenCAGE puts right a game left running too: the switch is turned on again at the next
        /// connection that has the open level's breakpoints in the game (Rearm). Any thread.
        /// </summary>
        internal static bool DisarmedOnLeave
        {
            get => SettingsManager.GetBool(Settings.LiveLinkDebuggerDisarmed);
            private set
            {
                if (DisarmedOnLeave != value)
                    SettingsManager.SetBool(Settings.LiveLinkDebuggerDisarmed, value);
            }
        }

        //The game's breakpoints switch (debugger.breakpoints, the Debug Menu's "Stop at breakpoints") as the last report gave
        //it - 1 on, 0 off, -1 not given - and the connection that report came on (pump thread writes, DisarmOnLeave reads)
        private static volatile int _switchState = -1;
        private static volatile int _switchConnection = -1;
        //The connection DisarmOnLeave turned the switch off on: the pump never turns it on again there. Both take the lock to
        //send, so a switch on can't follow a switch off on its way out.
        private static readonly object _switchLock = new object();
        private static int _leftConnection = -1;
        //Pump thread: the switch is to be turned on again on this connection once the open level has breakpoints in the game
        private static bool _rearm;

        /// <summary>The breakpoints of the level open here (UI thread).</summary>
        public static IReadOnlyCollection<ScriptBreakpoint> Breakpoints => _set.Values;

        /// <summary>Where the scripts were last stopped (UI thread).</summary>
        public static ScriptHit LastHit => _hit;

        /// <summary>The scripts are held by the debugger now. Any thread.</summary>
        public static bool Held => _held;

        /// <summary>Why the game's script debugger can't be used now (null: it can): not connected, the Debug Menu off, a game from before it.</summary>
        public static string Unavailable => LiveLink.Connected ? _unavailable : "Live Link is not connected to the game";

        /// <summary>The breakpoints changed, or the scripts were held or let go (UI thread).</summary>
        public static event Action Changed;

        /// <summary>A breakpoint was hit (or the scripts were held after a step or a pause): where (UI thread).</summary>
        public static event Action<ScriptHit> HitArrived;

        /// <summary>The flowgraphs show the live state of their entities (and the values of their data pins), read from the game.</summary>
        public static bool ShowStates
        {
            get => _showStates;
            set
            {
                if (_showStates == value)
                    return;
                _showStates = value;
                RaiseChanged();
                Kick();
            }
        }

        /// <summary>Something shows the debugger (the composite display's Script Debugger menu open, an MCP request): poll even with no breakpoints.</summary>
        public static bool PanelOpen
        {
            get => _panelOpen;
            set
            {
                _panelOpen = value;
                Kick();
            }
        }

        static LiveLinkDebugger()
        {
            LiveLink.ConnectionChanged += () =>
            {
                Interlocked.Increment(ref _connection);
                RunOnUiThread(() =>
                {
                    //Not in a game that went: drawn as waiting until the next one has them
                    if (!LiveLink.Connected)
                    {
                        foreach (ScriptBreakpoint bp in _set.Values)
                        {
                            bp.GameId = 0;
                            bp.Records = 0;
                        }
                        _held = false;
                        _hit.Held = false;
                        RaiseChanged();
                    }
                    //A game connecting may hold breakpoints already (set before OpenCAGE was restarted), or be held at one
                    _lookOnce = LiveLink.Connected;
                    Kick();
                });
            };
            //Another level here: its breakpoints are its own (the game keeps those it has, for the level they were set for)
            Singleton.OnLevelLoaded += content => RunOnUiThread(() =>
            {
                _set.Clear();
                _synced = false;
                _dirty.Clear();
                _pendingSync = false;
                _lookOnce = true;
                RaiseChanged();
                Kick();
            });
        }

        //The game is to be looked at once, even with nothing set here (a connection, another level)
        private static volatile bool _lookOnce;

        #region SETTING
        /// <summary>The breakpoint on this entity in this place, or null (UI thread).</summary>
        public static ScriptBreakpoint Find(Composite composite, IList<ShortGuid> path, Entity entity)
        {
            if (composite == null || entity == null)
                return null;
            _set.TryGetValue(ScriptBreakpoint.KeyOf(composite.shortGUID, path, entity.shortGUID), out ScriptBreakpoint bp);
            return bp;
        }

        /// <summary>The methods a breakpoint on this entity stops on: its method pins (as Trigger in Game offers them).</summary>
        public static List<ShortGuid> BreakableMethods(Commands commands, Composite composite, Entity entity)
        {
            if (entity == null || entity is VariableEntity)
                return new List<ShortGuid>();
            return LiveLink.Methods(commands, entity, composite);
        }

        /// <summary>
        /// Set a breakpoint on an entity in the instance at this path (null: every instance), stopping on these methods (null:
        /// every method pin it has), or take it away when one is there already and no methods are given. Answers what it did
        /// (for the status bar). UI thread.
        /// </summary>
        public static async Task<string> Toggle(Commands commands, Composite composite, IList<ShortGuid> path, Entity entity, IList<ShortGuid> methods = null)
        {
            if (commands == null || composite == null || entity == null)
                return "Nothing to set a breakpoint on";
            ScriptBreakpoint existing = Find(composite, path, entity);
            if (existing != null && methods == null)
                return await Remove(existing);
            List<ShortGuid> stopOn = methods?.ToList() ?? BreakableMethods(commands, composite, entity);
            if (stopOn.Count == 0)
                return "Live Link: " + EntityText(commands, composite, entity) + " has no methods to stop on (a breakpoint stops when one of an entity's method pins is called)";
            //One method again on a breakpoint that has it: that method comes off
            if (existing != null && methods != null && stopOn.All(o => existing.Methods.Contains(o)))
            {
                if (existing.Methods.Count == stopOn.Count)
                    return await Remove(existing);
                return await RemoveMethods(existing, stopOn);
            }
            return await Add(commands, composite, path, entity, stopOn, existing);
        }

        /// <summary>
        /// Set a breakpoint on an entity in the instance at this path (null: every instance), stopping on these methods (null:
        /// every method pin it has) - added to one there already. Answers what it did. UI thread.
        /// </summary>
        public static async Task<string> Set(Commands commands, Composite composite, IList<ShortGuid> path, Entity entity, IList<ShortGuid> methods = null)
        {
            if (commands == null || composite == null || entity == null)
                return "Nothing to set a breakpoint on";
            List<ShortGuid> stopOn = methods?.ToList() ?? BreakableMethods(commands, composite, entity);
            if (stopOn.Count == 0)
                return "Live Link: " + EntityText(commands, composite, entity) + " has no methods to stop on (a breakpoint stops when one of an entity's method pins is called)";
            return await Add(commands, composite, path, entity, stopOn, Find(composite, path, entity));
        }

        /// <summary>Take away the breakpoint on an entity in this place (with methods: just those). Answers what it did. UI thread.</summary>
        public static async Task<string> Clear(Composite composite, IList<ShortGuid> path, Entity entity, IList<ShortGuid> methods = null)
        {
            ScriptBreakpoint existing = Find(composite, path, entity);
            if (existing == null)
                return "Live Link: no breakpoint there";
            if (methods == null || existing.Methods.All(o => methods.Contains(o)))
                return await Remove(existing);
            return await RemoveMethods(existing, methods);
        }

        private static async Task<string> Add(Commands commands, Composite composite, IList<ShortGuid> path, Entity entity, List<ShortGuid> stopOn, ScriptBreakpoint existing)
        {
            ScriptBreakpoint bp = new ScriptBreakpoint()
            {
                Root = LiveLink.RootOf(commands),
                Composite = composite.shortGUID,
                Path = path?.ToList(),
                Entity = entity.shortGUID,
                Methods = stopOn,
                Label = Label(commands, composite, entity),
            };
            if (existing != null)
                bp.Methods = existing.Methods.Union(stopOn).ToList();
            _set[bp.Key] = bp;
            RaiseChanged();
            string what = "Breakpoint on " + EntityText(commands, composite, entity) + (path == null ? " (every instance)" : "");
            if (Unavailable != null)
            {
                MarkDirty(bp.Key);
                return "Live Link: " + what + " - kept, and set in the game once it can take it (" + Unavailable + ")";
            }
            LiveLink.Reply reply = await LiveLink.DebugMenuCommand("debugger.add_breakpoint", AddArgument(bp), CommandTimeoutSeconds);
            if (!reply.Answered)
                MarkDirty(bp.Key);
            await RefreshList();
            Kick();
            if (!reply.Ok)
                return "Live Link: " + what + " - the game turned it away: " + DescribeRefusal(reply);
            string[] lines = (reply.Message ?? "").Split('\n');
            return "Live Link: " + what + (lines.Length > 1 ? " - " + lines[1].Trim() : "");
        }

        private static async Task<string> Remove(ScriptBreakpoint bp)
        {
            _set.Remove(bp.Key);
            RaiseChanged();
            if (Unavailable == null)
            {
                LiveLink.Reply reply = await LiveLink.DebugMenuCommand("debugger.remove_breakpoint", RemoveArgument(bp), CommandTimeoutSeconds);
                if (!reply.Answered)
                    MarkDirty(bp.Key);
                await RefreshList();
                if (!reply.Ok && reply.Answered && !reply.Message.StartsWith("no breakpoint"))
                    return "Live Link: the breakpoint could not be taken away in the game: " + DescribeRefusal(reply);
            }
            else
                MarkDirty(bp.Key);
            Kick();
            return "Live Link: breakpoint taken away";
        }

        private static async Task<string> RemoveMethods(ScriptBreakpoint bp, IList<ShortGuid> methods)
        {
            bp.Methods = bp.Methods.Where(o => !methods.Contains(o)).ToList();
            RaiseChanged();
            if (Unavailable == null)
            {
                LiveLink.Reply reply = await LiveLink.DebugMenuCommand("debugger.remove_breakpoint", RemoveArgument(bp) + " methods=" + string.Join(",", methods.Select(o => o.AsUInt32.ToString("X8"))), CommandTimeoutSeconds);
                if (!reply.Answered)
                    MarkDirty(bp.Key);
                await RefreshList();
                if (!reply.Ok && reply.Answered && !reply.Message.StartsWith("no breakpoint"))
                    return "Live Link: the breakpoint could not be changed in the game: " + DescribeRefusal(reply);
            }
            else
                MarkDirty(bp.Key);
            return "Live Link: the breakpoint no longer stops on " + string.Join(", ", methods.Select(o => o.ToString()));
        }

        /// <summary>Take every breakpoint of the level open here away (in the game too). UI thread.</summary>
        public static async Task<string> RemoveAll()
        {
            List<ScriptBreakpoint> all = _set.Values.ToList();
            _set.Clear();
            RaiseChanged();
            if (Unavailable == null)
            {
                foreach (ScriptBreakpoint bp in all)
                {
                    LiveLink.Reply reply = await LiveLink.DebugMenuCommand("debugger.remove_breakpoint", bp.GameId != 0 ? bp.GameId.ToString(CultureInfo.InvariantCulture) : RemoveArgument(bp), CommandTimeoutSeconds);
                    if (!reply.Answered)
                        MarkDirty(bp.Key);
                }
                await RefreshList();
            }
            else
            {
                foreach (ScriptBreakpoint bp in all)
                    MarkDirty(bp.Key);
            }
            return "Live Link: " + all.Count + " breakpoint(s) taken away";
        }

        //A change the game could not take (not connected, or no answer): put right in it by the next sync (SendKept). UI thread.
        private static void MarkDirty(string key)
        {
            _dirty.Add(key);
            _pendingSync = true;
            Kick();
        }

        private static string AddArgument(ScriptBreakpoint bp)
        {
            //The label last: it takes the rest of the line
            return "composite=" + bp.Composite.AsUInt32.ToString("X8") + " path=" + PathText(bp.Path) + " entity=" + bp.Entity.AsUInt32.ToString("X8")
                + " methods=" + string.Join(",", bp.Methods.Select(o => o.AsUInt32.ToString("X8"))) + " root=" + bp.Root.ToString("X8")
                + (string.IsNullOrEmpty(bp.Label) ? "" : " label=" + bp.Label.Replace('\n', ' '));
        }

        private static string RemoveArgument(ScriptBreakpoint bp)
        {
            return "composite=" + bp.Composite.AsUInt32.ToString("X8") + " path=" + PathText(bp.Path) + " entity=" + bp.Entity.AsUInt32.ToString("X8");
        }

        private static string PathText(IList<ShortGuid> path) => path == null ? "*" : string.Join(",", path.Select(o => o.AsUInt32.ToString("X8")));

        private static string Label(Commands commands, Composite composite, Entity entity)
        {
            try
            {
                return composite.name + " : " + EntityText(commands, composite, entity);
            }
            catch
            {
                return "";
            }
        }

        /// <summary>An entity as the status bar names it: its name and id.</summary>
        public static string EntityText(Commands commands, Composite composite, Entity entity)
        {
            string name = null;
            try
            {
                name = entity is VariableEntity variable ? ShortGuidUtils.FindString(variable.name) : commands?.Utils.GetEntityName(composite, entity);
            }
            catch
            {
                //Named as its id below
            }
            return (string.IsNullOrEmpty(name) ? "" : name + " ") + "[" + entity.shortGUID.ToByteString() + "]";
        }

        /// <summary>What a refusal from the game's Debug Menu means here.</summary>
        public static string DescribeRefusal(LiveLink.Reply reply)
        {
            if (reply == null)
                return "no answer";
            if (LiveLink.IsUnknownRequest(reply))
                return "the game's runtime utils are too old for the Debug Menu - launch the game again from this OpenCAGE";
            string message = reply.Message ?? "";
            if (message.Contains("debug menu is off"))
                return "the game's Debug Menu is off - launch the game from OpenCAGE with the Debug Menu on (Launch Game)";
            if (message.StartsWith("No debug menu command named debugger."))
                return "the game's runtime utils have no script debugger - launch the game again from this OpenCAGE";
            return message;
        }
        #endregion

        #region CONTROL
        /// <summary>Let the held scripts go on. Answers what happened (for the status bar).</summary>
        public static Task<string> Continue() => Control("debugger.continue", "");

        /// <summary>While held: make the held call and the rest of the calls due with it - one round (or, with frame, one whole frame of the game), then hold again.</summary>
        public static Task<string> Step(bool frame = false) => Control("debugger.step", frame ? "frame" : "cycle");

        /// <summary>Hold the level's scripts (and the game) now, as a breakpoint does.</summary>
        public static Task<string> Pause() => Control("debugger.pause", "");

        private static async Task<string> Control(string command, string argument)
        {
            return (await ControlChecked(command, argument)).Text;
        }

        /// <summary>Run one of the debugger's controls (debugger.continue, step, pause): whether the game did it, and what happened (for the status bar).</summary>
        public static async Task<(bool Ok, string Text)> ControlChecked(string command, string argument)
        {
            if (!LiveLink.Connected)
                return (false, "Live Link: not connected to the game");
            LiveLink.Reply reply = await LiveLink.DebugMenuCommand(command, argument, CommandTimeoutSeconds);
            //Look at the game at least once after any control (a pause with nothing set here would otherwise go unseen)
            _lookOnce = true;
            Kick();
            return (reply.Ok, "Live Link: " + (reply.Ok ? reply.Message : DescribeRefusal(reply)));
        }

        /// <summary>The game's report of where it is held (debugger.state), read now.</summary>
        public static async Task<ScriptHit> ReadState()
        {
            LiveLink.Reply reply = await LiveLink.DebugMenuCommand("debugger.state", "", CommandTimeoutSeconds);
            return reply.Ok ? ParseState(reply.Message) : null;
        }

        /// <summary>
        /// Live Link is being switched off, or OpenCAGE is closing: a game left with breakpoints set from here would stop at
        /// them with nothing to let it go on, so its breakpoints switch is turned off and the held scripts let go (the
        /// breakpoints themselves are kept, and the switch turned on again at the next connection that has them - this
        /// OpenCAGE's or the next one's - unless it was off already: then it was switched off in the game, and stays as the
        /// game has it). Sent now, the answers not waited for. UI thread.
        /// </summary>
        internal static void DisarmOnLeave()
        {
            if (!LiveLink.Connected || (_set.Count == 0 && !_held))
                return;
            int connection = Volatile.Read(ref _connection);
            bool offInGame = _switchConnection == connection && _switchState == 0;
            //The answers come back on the thread pool, not to this thread (it may be closing): each request is sent before
            //it first waits
            SynchronizationContext context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                lock (_switchLock)
                {
                    _leftConnection = connection;
                    //Switched off first, so the scripts let go are not stopped again at once
                    LiveLink.DebugMenu(LiveLink.DebugMenuOp.SetSwitch, "debugger.breakpoints", false);
                }
                LiveLink.DebugMenuCommand("debugger.continue", "", 1.0);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(context);
            }
            if (!offInGame)
                DisarmedOnLeave = true;
        }
        #endregion

        #region SELECT FROM GAME
        /// <summary>What the game's crosshair is on (the Debug Menu's World > Inspect: inspect.pick), as OpenCAGE resolves it.</summary>
        public sealed class Pick
        {
            public bool Ok;
            public string Message = "";
            public Composite Composite;
            /// <summary>Instance entity ids from the root to the instance holding the entity.</summary>
            public List<ShortGuid> Path;
            public Entity Entity;
            public string Description = "";
            public System.Numerics.Vector3? Position;
        }

        /// <summary>
        /// Ask the game what its crosshair is on (inspect.pick) and find that entity in the level open here, from its id and
        /// the id of the composite instance holding it (as the game names entities in its own records). UI thread.
        /// </summary>
        public static async Task<Pick> PickFromGame(Commands commands)
        {
            Pick pick = new Pick();
            if (!LiveLink.Connected)
            {
                pick.Message = "Live Link is not connected to the game";
                return pick;
            }
            LiveLink.Reply reply = await LiveLink.DebugMenuCommand("inspect.pick", "", CommandTimeoutSeconds);
            if (!reply.Ok)
            {
                string message = reply.Message ?? "";
                pick.Message = message.StartsWith("No debug menu command named inspect.pick")
                    ? "The game's Debug Menu has no crosshair inspector (World > Inspect) - launch the game again from this OpenCAGE"
                    : DescribeRefusal(reply);
                return pick;
            }
            Dictionary<string, string> fields = Fields(reply.Message);
            fields.TryGetValue("description", out pick.Description);
            pick.Description = pick.Description ?? "";
            if (fields.TryGetValue("position", out string position))
                pick.Position = Triple(position);
            if (!fields.TryGetValue("hit", out string hit) || hit != "1")
            {
                //No answer at all (answered=0: the game paused, no camera...) is not a miss: the game's reason is given
                fields.TryGetValue("answered", out string answered);
                if (answered == "0" || (pick.Description.Length != 0 && pick.Description != "nothing hit"))
                    pick.Message = "No answer from the game's crosshair: " + pick.Description;
                else
                    pick.Message = "Nothing under the game's crosshair - aim at something (the Debug Menu's World > Inspect shows what it is on)";
                return pick;
            }
            uint entityId = HexValue(fields, "entity");
            uint instanceId = HexValue(fields, "instance");
            if (entityId == 0)
            {
                pick.Message = "What the crosshair is on belongs to no script entity" + (pick.Description.Length != 0 ? " (" + pick.Description + ")" : "");
                return pick;
            }
            if (commands == null)
            {
                pick.Message = "No level is open here";
                return pick;
            }
            LevelContent content = Singleton.Editor?.CompositeBrowser?.Content;
            content?.EnsureEditorUtils();
            if (content?.EditorUtils == null)
            {
                pick.Message = "The level's instances are still being worked out - try again in a moment";
                return pick;
            }
            (Composite composite, EntityPath path) = content.EditorUtils.GetCompositeFromInstanceID(commands, new ShortGuid(instanceId));
            if (composite == null)
            {
                pick.Message = "The game's entity " + new ShortGuid(entityId).ToByteString() + " is in an instance (" + new ShortGuid(instanceId).ToByteString() + ") this level does not have - is the game running the level as saved?";
                return pick;
            }
            Entity entity = composite.GetEntityByID(new ShortGuid(entityId));
            if (entity == null)
            {
                pick.Message = "The game's entity " + new ShortGuid(entityId).ToByteString() + " is not in " + composite.name + " here - is the game running the level as saved?";
                return pick;
            }
            pick.Ok = true;
            pick.Composite = composite;
            pick.Entity = entity;
            pick.Path = path?.path.Where(o => o != ShortGuid.Invalid).ToList() ?? new List<ShortGuid>();
            pick.Message = "Live Link: the game's crosshair is on " + EntityText(commands, composite, entity) + " in " + composite.name;
            return pick;
        }
        #endregion

        #region NAVIGATION
        /// <summary>
        /// Show an entity in the composite display: the composite at this path of instance entity ids from the level's root
        /// (walked down from the root, as the search's Jump To does, so the breadcrumb and the viewport keep their place),
        /// with the entity selected and its node brought into view. False when the path does not lead there. UI thread.
        /// </summary>
        public static bool ShowEntity(Commands commands, IList<ShortGuid> path, ShortGuid entity)
        {
            Composite root = commands?.EntryPoints?[0];
            CommandsEditor editor = Singleton.Editor;
            if (root == null || editor == null || path == null)
                return false;
            //The path must still lead to a composite holding the entity
            Composite composite = root;
            foreach (ShortGuid step in path)
            {
                if (!(composite.GetEntityByID(step) is FunctionEntity instance) || instance.function.IsFunctionType)
                    return false;
                composite = commands.GetComposite(instance.function);
                if (composite == null)
                    return false;
            }
            if (composite.GetEntityByID(entity) == null)
                return false;

            CompositeDisplay display = editor.CompositeDisplay;
            if (display == null || display.IsDisposed || !display.Populated)
                display = editor.CompositeBrowser?.LoadComposite(root) ?? display;
            if (display == null || display.IsDisposed)
                return false;
            List<uint> guids = path.Select(o => o.AsUInt32).ToList();
            guids.Add(entity.AsUInt32);
            Func<Entity, Composite> child = e => e is FunctionEntity function && !function.function.IsFunctionType ? commands.GetComposite(function.function) : null;
            bool shown = display.NavigateAsOneStep(() =>
            {
                display.OpenAsSceneRoot(root);
                return display.ApplyViewerSelectionPath(root, guids, true, child);
            });
            return shown && display.Composite == composite;
        }

        /// <summary>
        /// Show where the scripts are held: the node the breakpoint hit is on, in the instance that hit it where that can be
        /// told (else the breakpoint's own place), and flash OpenCAGE's taskbar button if it is not in front. UI thread.
        /// </summary>
        public static void ShowHit(ScriptHit hit)
        {
            Commands commands = Singleton.Editor?.CompositeDisplay?.Content?.Level?.Commands ?? Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
            if (hit == null || commands == null)
                return;
            ScriptBreakpoint bp = _set.Values.FirstOrDefault(o => o.GameId == hit.Breakpoint && hit.Breakpoint != 0);
            List<ShortGuid> hitPath = hit.Path.Select(o => new ShortGuid(o)).ToList();
            bool shown = false;
            //The entity the held call goes to, in the instance it is in - unless the breakpoint is on something else (an alias
            //or a proxy of it), whose own node is the one to show
            if (bp == null || bp.Entity.AsUInt32 == hit.Entity)
                shown = ShowEntity(commands, hitPath, new ShortGuid(hit.Entity));
            if (!shown && bp != null && bp.Path != null)
                shown = ShowEntity(commands, bp.Path, bp.Entity);
            if (!shown && bp != null)
            {
                Composite composite = commands.GetComposite(bp.Composite);
                if (composite != null && composite.GetEntityByID(bp.Entity) != null)
                {
                    Singleton.Editor?.CompositeBrowser?.LoadCompositeAndEntity(composite, composite.GetEntityByID(bp.Entity));
                    shown = true;
                }
            }
            if (!shown)
            {
                Composite composite = commands.GetComposite(new ShortGuid(hit.Composite));
                Entity entity = composite?.GetEntityByID(new ShortGuid(hit.Entity));
                if (entity != null)
                    Singleton.Editor?.CompositeBrowser?.LoadCompositeAndEntity(composite, entity);
            }
            FlashEditor();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }
        [DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FLASHWINFO info);
        private const uint FLASHW_TRAY = 2, FLASHW_TIMERNOFG = 12;

        //The game has the screen when a breakpoint is hit: OpenCAGE's taskbar button flashes until it is brought to the front
        private static void FlashEditor()
        {
            CommandsEditor editor = Singleton.Editor;
            if (editor == null || editor.IsDisposed || !editor.IsHandleCreated || System.Windows.Forms.Form.ActiveForm != null)
                return;
            FLASHWINFO info = new FLASHWINFO() { hwnd = editor.Handle, dwFlags = FLASHW_TRAY | FLASHW_TIMERNOFG, uCount = 0, dwTimeout = 0 };
            info.cbSize = (uint)Marshal.SizeOf(info);
            FlashWindowEx(ref info);
        }
        #endregion

        #region PUMP
        /// <summary>Whether the game is to be polled: connected, and breakpoints set (or changes to them to send), the scripts held, live states shown or the menu open.</summary>
        private static bool Wanted => LiveLink.Connected && (Volatile.Read(ref _setCount) > 0 || _pendingSync || _held || _panelOpen || _showStates || _lookOnce);
        private static int _setCount;

        /// <summary>Look again soon (and start polling if anything needs it). Any thread.</summary>
        public static void Kick()
        {
            Volatile.Write(ref _setCount, _set.Count);
            if (_wake.CurrentCount == 0)
            {
                try
                {
                    _wake.Release();
                }
                catch (SemaphoreFullException)
                {
                    //Woken already
                }
            }
            if (!Wanted || Interlocked.CompareExchange(ref _pumping, 1, 0) != 0)
                return;
            Task.Run(() => Pump());
        }

        private static async Task Pump()
        {
            try
            {
                while (Wanted)
                {
                    await Poll();
                    await _wake.WaitAsync(PollMs);
                }
            }
            catch (Exception ex)
            {
                Debug.Log("Live Link", "The script debugger's poll failed: " + ex.Message);
            }
            finally
            {
                Volatile.Write(ref _pumping, 0);
            }
            //Wanted again just as it stood down
            if (Wanted)
                Kick();
        }

        /// <summary>One look at the game. Pump thread.</summary>
        private static async Task Poll()
        {
            int connection = Volatile.Read(ref _connection);
            if (connection != _pumpConnection)
            {
                _pumpConnection = connection;
                _synced = false;
                _lastHit = null;
                _lastVersion = null;
                _lastHeld = false;
                _stateWanted = false;
                _unavailable = null;
                _rearm = false;
            }
            LiveLink.Reply report = await LiveLink.DebugMenuReport(PollTimeoutSeconds);
            if (!report.Ok)
            {
                SetUnavailable(report.Answered ? DescribeRefusal(report) : "the game did not answer");
                //A game that answers it has no Debug Menu is not looked at again for nothing (what is set here keeps it polled)
                if (report.Answered)
                    _lookOnce = false;
                await Task.Delay(2000);
                return;
            }
            Dictionary<string, string> values = ReportValues(report.Message);
            if (!values.TryGetValue("debugger.version", out string version) || !values.TryGetValue("debugger.hit", out string hitCount))
            {
                SetUnavailable("the game's runtime utils have no script debugger - launch the game again from this OpenCAGE");
                _lookOnce = false;
                await Task.Delay(2000);
                return;
            }
            SetUnavailable(null);
            _lookOnce = false;
            values.TryGetValue("debugger.held", out string heldText);
            bool held = heldText == "1";
            int switchState = Fields(report.Message).TryGetValue("debugger.breakpoints", out string switchText) ? (switchText == "1" ? 1 : 0) : -1;
            _switchConnection = connection;
            _switchState = switchState;

            //The flag is cleared before the sync: a change made while it runs asks for another
            if (!_synced || _pendingSync)
            {
                bool firstSync = !_synced;
                _synced = true;
                _pendingSync = false;
                await RunOnUiThreadAsync(SendKept);
                _lastVersion = null;
                //The breakpoints switched off in the game when Live Link went off here or OpenCAGE closed (in this session or
                //an earlier one) stop the scripts again, once the open level's are read back from the game below
                if (firstSync)
                    _rearm = DisarmedOnLeave;
            }
            bool versionMoved = version != _lastVersion;
            if (versionMoved)
            {
                _lastVersion = version;
                await RefreshList();
            }
            if (_rearm)
                await Rearm(connection, switchState);
            //Held, a step (or a pause) moves the version without a hit: where it is held is read again too (and after a
            //read of it failed)
            if (_stateWanted || hitCount != _lastHit || held != _lastHeld || (held && versionMoved))
            {
                bool newHit = _lastHit != null && hitCount != _lastHit;
                bool firstLook = _lastHit == null;
                _held = held;
                _stateWanted = true;
                ScriptHit state = await ReadState();
                if (state == null)
                {
                    //Read again at the next poll; the toolbar follows the held flag meanwhile
                    RunOnUiThread(RaiseChanged);
                    return;
                }
                _lastHit = hitCount;
                _lastHeld = held;
                _stateWanted = false;
                RunOnUiThread(() =>
                {
                    _hit = state;
                    RaiseChanged();
                    //A new hit (or, when OpenCAGE connects to a game held already, the hit it is held at) is shown
                    if (state.Held && state.Sequence != 0 && (newHit || firstLook))
                    {
                        HitArrived?.Invoke(state);
                        Singleton.Editor?.ShowLiveLinkActivity("Live Link: scripts held at a breakpoint - " + HitText(state) + " (Continue: F8, Step: F10 on a flowgraph)");
                    }
                });
            }
        }

        /// <summary>
        /// The breakpoints switch DisarmOnLeave turned off (here or in an earlier OpenCAGE) turned on again, once the open
        /// level has breakpoints in the game; until then it waits (for one set from here on this connection, or the next
        /// connection). Never against the game's own switch: found on (a game restarted since, or switched on in it), it is
        /// left as it is and nothing is owed any more. Pump thread.
        /// </summary>
        private static async Task Rearm(int connection, int switchState)
        {
            if (switchState != 0)
            {
                _rearm = false;
                DisarmedOnLeave = false;
                return;
            }
            if (Volatile.Read(ref _setCount) == 0)
                return;
            Task<LiveLink.Reply> sent;
            lock (_switchLock)
            {
                //Live Link going off on this connection: the switch stays off
                if (_leftConnection == connection || Volatile.Read(ref _connection) != connection)
                    return;
                sent = LiveLink.DebugMenu(LiveLink.DebugMenuOp.SetSwitch, "debugger.breakpoints", true);
            }
            //Not answered: tried again at the next poll
            if ((await sent).Answered)
            {
                _rearm = false;
                DisarmedOnLeave = false;
            }
        }

        private static void SetUnavailable(string why)
        {
            if (_unavailable == why)
                return;
            _unavailable = why;
            RunOnUiThread(RaiseChanged);
        }

        //Breakpoints kept here that the game does not have (set before it connected, or before it was restarted) are sent to it,
        //and those changed here while it could not take the change are put right: taken out of it, and sent again as kept here.
        //UI thread.
        private static async Task SendKept()
        {
            if (_set.Count == 0 && _dirty.Count == 0)
                return;
            LiveLink.Reply list = await LiveLink.DebugMenuCommand("debugger.list", "", CommandTimeoutSeconds);
            if (!list.Ok)
            {
                _pendingSync = true;
                return;
            }
            Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
            uint root = LiveLink.RootOf(commands);
            List<string> dirty = _dirty.ToList();
            //Keys whose commands did not get through: left to the next sync
            HashSet<string> failed = new HashSet<string>();
            HashSet<string> inGame = new HashSet<string>();
            foreach (ScriptBreakpoint listed in ParseList(list.Message))
            {
                //Another level's (the game keeps them while it runs): not this one's
                if (listed.Root != 0 && root != 0 && listed.Root != root)
                    continue;
                if (!_dirty.Contains(listed.Key))
                {
                    inGame.Add(listed.Key);
                    continue;
                }
                LiveLink.Reply removed = await LiveLink.DebugMenuCommand("debugger.remove_breakpoint", listed.GameId.ToString(CultureInfo.InvariantCulture), CommandTimeoutSeconds);
                if (!removed.Ok && !(removed.Answered && (removed.Message ?? "").StartsWith("no breakpoint")))
                {
                    //Still in the game as it was: not sent again on top of it
                    failed.Add(listed.Key);
                    inGame.Add(listed.Key);
                }
            }
            foreach (ScriptBreakpoint bp in _set.Values.ToList())
            {
                if (inGame.Contains(bp.Key))
                    continue;
                LiveLink.Reply reply = await LiveLink.DebugMenuCommand("debugger.add_breakpoint", AddArgument(bp), CommandTimeoutSeconds);
                if (!reply.Ok)
                {
                    Debug.Log("Live Link", "A breakpoint could not be sent to the game: " + DescribeRefusal(reply));
                    //One the game turned away would be turned away again: only one it did not answer is tried again
                    if (!reply.Answered)
                        failed.Add(bp.Key);
                }
            }
            foreach (string key in dirty)
            {
                if (!failed.Contains(key))
                    _dirty.Remove(key);
            }
            if (failed.Count != 0)
                _pendingSync = true;
        }

        /// <summary>Read the game's list again and take it as the breakpoints of the level open here. Any thread.</summary>
        public static async Task RefreshList()
        {
            LiveLink.Reply reply = await LiveLink.DebugMenuCommand("debugger.list", "", CommandTimeoutSeconds);
            if (!reply.Ok)
                return;
            List<ScriptBreakpoint> listed = ParseList(reply.Message);
            await RunOnUiThreadAsync(() =>
            {
                Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
                uint root = LiveLink.RootOf(commands);
                Dictionary<string, ScriptBreakpoint> next = new Dictionary<string, ScriptBreakpoint>();
                foreach (ScriptBreakpoint bp in listed)
                {
                    //Another level's (the game keeps them while it runs): not this one's
                    if (bp.Root != 0 && root != 0 && bp.Root != root)
                        continue;
                    //Changed here while the game could not take it: kept as it is here until a sync puts it right in the game
                    if (_dirty.Contains(bp.Key))
                        continue;
                    if (_set.TryGetValue(bp.Key, out ScriptBreakpoint kept) && string.IsNullOrEmpty(bp.Label))
                        bp.Label = kept.Label;
                    next[bp.Key] = bp;
                }
                foreach (string key in _dirty)
                {
                    if (_set.TryGetValue(key, out ScriptBreakpoint kept))
                        next[key] = kept;
                }
                _set.Clear();
                foreach (KeyValuePair<string, ScriptBreakpoint> pair in next)
                    _set[pair.Key] = pair.Value;
                RaiseChanged();
                return Task.CompletedTask;
            });
        }
        #endregion

        #region PARSING
        /// <summary>"key=value" lines (a value may hold '=': each line is split at its first).</summary>
        public static Dictionary<string, string> Fields(string message)
        {
            Dictionary<string, string> fields = new Dictionary<string, string>();
            foreach (string line in (message ?? "").Split('\n'))
            {
                int equals = line.IndexOf('=');
                if (equals > 0)
                    fields[line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
            }
            return fields;
        }

        //The report's "values=name:1,name:2" line
        private static Dictionary<string, string> ReportValues(string message)
        {
            Dictionary<string, string> values = new Dictionary<string, string>();
            Fields(message).TryGetValue("values", out string line);
            foreach (string pair in (line ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = pair.LastIndexOf(':');
                if (colon > 0)
                    values[pair.Substring(0, colon)] = pair.Substring(colon + 1);
            }
            return values;
        }

        private static uint HexValue(Dictionary<string, string> fields, string key)
        {
            return fields.TryGetValue(key, out string text) && TryHex(text, out uint value) ? value : 0;
        }

        //8 hex digits, with or without 0x before them
        private static bool TryHex(string text, out uint value)
        {
            text = (text ?? "").Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                text = text.Substring(2);
            return uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }

        private static List<uint> HexList(string text)
        {
            List<uint> ids = new List<uint>();
            foreach (string part in (text ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                if (TryHex(part, out uint id))
                    ids.Add(id);
            return ids;
        }

        private static System.Numerics.Vector3? Triple(string value)
        {
            string[] parts = (value ?? "").Split(',');
            if (parts.Length != 3)
                return null;
            float[] v = new float[3];
            for (int i = 0; i < 3; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i]))
                    return null;
            return new System.Numerics.Vector3(v[0], v[1], v[2]);
        }

        /// <summary>debugger.state's answer.</summary>
        public static ScriptHit ParseState(string message)
        {
            Dictionary<string, string> fields = Fields(message);
            ScriptHit hit = new ScriptHit();
            hit.Held = fields.TryGetValue("held", out string held) && held == "1";
            hit.AtBreakpoint = fields.TryGetValue("at_breakpoint", out string at) && at == "1";
            if (fields.TryGetValue("hit", out string sequence))
                uint.TryParse(sequence, NumberStyles.Integer, CultureInfo.InvariantCulture, out hit.Sequence);
            if (fields.TryGetValue("breakpoint", out string breakpoint))
                int.TryParse(breakpoint, NumberStyles.Integer, CultureInfo.InvariantCulture, out hit.Breakpoint);
            hit.Entity = HexValue(fields, "entity");
            hit.Method = HexValue(fields, "method");
            hit.Type = HexValue(fields, "type");
            hit.Composite = HexValue(fields, "composite");
            hit.Instance = HexValue(fields, "instance");
            fields.TryGetValue("composite_name", out hit.CompositeName);
            hit.CompositeName = hit.CompositeName ?? "";
            fields.TryGetValue("path", out string path);
            hit.Path = HexList(path);
            if (fields.TryGetValue("time", out string time))
                double.TryParse(time, NumberStyles.Float, CultureInfo.InvariantCulture, out hit.Time);
            if (fields.TryGetValue("steps", out string steps))
                int.TryParse(steps, NumberStyles.Integer, CultureInfo.InvariantCulture, out hit.Steps);
            for (int i = 1; fields.TryGetValue("chain." + i.ToString(CultureInfo.InvariantCulture), out string link); i++)
            {
                uint entity = 0, method = 0, composite = 0;
                List<uint> linkPath = new List<uint>();
                foreach (string part in link.Split(' '))
                {
                    int colon = part.IndexOf(':');
                    if (colon <= 0)
                        continue;
                    string key = part.Substring(0, colon), value = part.Substring(colon + 1);
                    switch (key)
                    {
                        case "entity": uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out entity); break;
                        case "method": uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out method); break;
                        case "composite": uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out composite); break;
                        case "path": linkPath = HexList(value); break;
                    }
                }
                hit.Chain.Add((entity, method, composite, linkPath));
            }
            return hit;
        }

        //debugger.list's keys, in the order the game writes them (the label is last, and takes the rest of the line)
        private static readonly string[] ListKeys = { "id", "composite", "path", "entity", "methods", "root", "enabled", "hits", "instances", "records", "problem", "label" };

        /// <summary>debugger.list's answer: a line per breakpoint, "key=value" pairs (a problem's text has spaces in it).</summary>
        public static List<ScriptBreakpoint> ParseList(string message)
        {
            List<ScriptBreakpoint> list = new List<ScriptBreakpoint>();
            foreach (string raw in (message ?? "").Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (!line.StartsWith("id="))
                    continue;
                Dictionary<string, string> fields = new Dictionary<string, string>();
                //Each key's value runs to where the next key of the list begins
                List<(int at, string key)> starts = new List<(int, string)>();
                foreach (string key in ListKeys)
                {
                    int at = key == "id" ? (line.StartsWith("id=") ? 0 : -1) : line.IndexOf(" " + key + "=", StringComparison.Ordinal);
                    if (at >= 0)
                        starts.Add((key == "id" ? 0 : at + 1, key));
                }
                starts.Sort((a, b) => a.at.CompareTo(b.at));
                for (int i = 0; i < starts.Count; i++)
                {
                    int begin = starts[i].at + starts[i].key.Length + 1;
                    int end = starts[i].key == "label" || i + 1 == starts.Count ? line.Length : starts[i + 1].at - 1;
                    fields[starts[i].key] = end > begin ? line.Substring(begin, end - begin) : "";
                    if (starts[i].key == "label")
                        break;
                }
                ScriptBreakpoint bp = new ScriptBreakpoint();
                if (fields.TryGetValue("id", out string id))
                    int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out bp.GameId);
                bp.Composite = new ShortGuid(HexValue(fields, "composite"));
                bp.Entity = new ShortGuid(HexValue(fields, "entity"));
                bp.Root = HexValue(fields, "root");
                fields.TryGetValue("path", out string path);
                bp.Path = path == "*" ? null : HexList(path).Select(o => new ShortGuid(o)).ToList();
                fields.TryGetValue("methods", out string methods);
                bp.Methods = HexList(methods).Select(o => new ShortGuid(o)).ToList();
                bp.Enabled = !fields.TryGetValue("enabled", out string enabled) || enabled != "0";
                if (fields.TryGetValue("hits", out string hits))
                    uint.TryParse(hits, NumberStyles.Integer, CultureInfo.InvariantCulture, out bp.Hits);
                if (fields.TryGetValue("instances", out string instances))
                    int.TryParse(instances, NumberStyles.Integer, CultureInfo.InvariantCulture, out bp.Instances);
                if (fields.TryGetValue("records", out string records))
                    int.TryParse(records, NumberStyles.Integer, CultureInfo.InvariantCulture, out bp.Records);
                fields.TryGetValue("problem", out bp.Problem);
                fields.TryGetValue("label", out string label);
                bp.Label = label ?? "";
                list.Add(bp);
            }
            return list;
        }

        /// <summary>A hit as the status bar names it: the method and the entity, by name where the level here has them.</summary>
        public static string HitText(ScriptHit hit)
        {
            if (hit == null || hit.Sequence == 0)
                return "";
            string method = new ShortGuid(hit.Method).ToString();
            string entity = "[" + new ShortGuid(hit.Entity).ToByteString() + "]";
            string composite = hit.CompositeName;
            Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
            Composite holder = commands?.GetComposite(new ShortGuid(hit.Composite));
            Entity found = holder?.GetEntityByID(new ShortGuid(hit.Entity));
            if (found != null)
            {
                entity = EntityText(commands, holder, found);
                composite = holder.name;
            }
            return "'" + method + "' on " + entity + (string.IsNullOrEmpty(composite) ? "" : " in " + composite);
        }
        #endregion

        private static void RaiseChanged()
        {
            Volatile.Write(ref _setCount, _set.Count);
            Changed?.Invoke();
        }

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

        //Runs async work on the UI thread and waits for it to finish (it awaits there, posting back each time)
        private static Task RunOnUiThreadAsync(Func<Task> work)
        {
            TaskCompletionSource<bool> done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!RunOnUiThread(async () =>
            {
                try
                {
                    await work();
                }
                catch (Exception ex)
                {
                    Debug.Log("Live Link", "The script debugger failed: " + ex.Message);
                }
                done.TrySetResult(true);
            }))
                done.TrySetResult(false);
            return done.Task;
        }
    }
}
