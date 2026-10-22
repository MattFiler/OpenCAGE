using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.DockPanels;
using OpenCAGE.RuntimeUtilsConnection;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace OpenCAGE
{
    /* Live Link script debugger on a flowgraph page (see LiveLinkDebugger): a node's menu (or F9 over it) sets or takes away
       a breakpoint on its entity - in the instance the composite was reached through from the level's root, or in every
       instance when it was opened from the browser - and a method pin's menu does it for that one method. The page draws
       each breakpoint as a red dot on its node (a ring while the game does not stop on it), outlines the node the scripts
       are held at, and - while Show Live States Above Nodes is on - shows above each node its state in the running game (started,
       enabled...) and the values its data pins read, polled from the game twice a second while the page is on screen. F8
       continues and F10 steps while the scripts are held. */
    public partial class Flowgraph
    {
        private CompositeDisplay _debuggerDisplay;
        private ToolStripSeparator _breakpointSeparator;
        private ToolStripMenuItem _breakpointItem;
        private ToolStripMenuItem _breakpointMethodItem;
        //What the menu acts on, settled as it opened
        private Entity _breakpointEntity;
        private ShortGuid _breakpointMethod;

        //What each node shows, worked out when the breakpoints, the hit or the states change (the editor asks per node per paint)
        private readonly Dictionary<uint, BreakpointMark> _breakpointMarks = new Dictionary<uint, BreakpointMark>();
        private readonly Dictionary<uint, string[]> _stateLines = new Dictionary<uint, string[]>();
        private uint _heldEntity;
        private bool _heldHere;
        private string _debugPlaceKey = "";
        private int _debugPlaceCheckedAt;

        //Live states: polled while the page is on screen and Show Live States Above Nodes is on
        private Timer _statesTimer;
        private bool _statesBusy;
        private string _statesProblem;
        //Data pins' types, by entity, as CathodeLib gives them (forgotten every so often: entities are edited)
        private readonly Dictionary<uint, Dictionary<uint, DataType>> _pinTypes = new Dictionary<uint, Dictionary<uint, DataType>>();
        private int _pinTypesAt;
        private const int StatesPollMs = 500;
        private const int MaxStateEntities = 1024;
        private const int MaxStateValues = 200;

        /// <summary>Show the script debugger on this page (its menu items, its marks, live states). UI thread.</summary>
        internal void AttachLiveLinkDebugger(CompositeDisplay display)
        {
            _debuggerDisplay = display;
            if (_breakpointItem != null)
            {
                RefreshDebugMarks();
                return;
            }
            _breakpointSeparator = new ToolStripSeparator();
            _breakpointItem = new ToolStripMenuItem("Toggle Breakpoint") { ShortcutKeyDisplayString = "F9" };
            _breakpointItem.ToolTipText = "Stop the level's scripts (and the game) when any of this entity's methods is about to be called in the running game (Live Link, with the Debug Menu on).";
            _breakpointItem.Click += async (s, e) => await ToggleBreakpoint(_breakpointEntity, null);
            _breakpointMethodItem = new ToolStripMenuItem("Break on Method");
            _breakpointMethodItem.Click += async (s, e) => await ToggleBreakpoint(_breakpointEntity, new List<ShortGuid>() { _breakpointMethod });
            nodeContextMenu.Items.Add(_breakpointSeparator);
            nodeContextMenu.Items.Add(_breakpointItem);
            nodeContextMenu.Items.Add(_breakpointMethodItem);
            //After the page's own handler (added in the designer), which sets the other items for the same node and pin
            nodeContextMenu.Opening += ConfigureBreakpointItems;

            if (stNodeEditor1 is DebuggerNodeEditor editor)
                editor.DebugMarkProvider = DebugMarkOf;
            LiveLinkDebugger.Changed += OnDebuggerChanged;
            LiveLinkDebugger.HitArrived += OnDebuggerHit;
            VisibleChanged += OnDebuggerVisibleChanged;
            FormClosed += OnDebuggerClosed;
            Disposed += OnDebuggerClosed;

            _statesTimer = new Timer() { Interval = StatesPollMs };
            _statesTimer.Tick += async (s, e) => await PollStates();
            _statesTimer.Start();
            RefreshDebugMarks();
        }

        private void OnDebuggerClosed(object sender, EventArgs e)
        {
            LiveLinkDebugger.Changed -= OnDebuggerChanged;
            LiveLinkDebugger.HitArrived -= OnDebuggerHit;
            VisibleChanged -= OnDebuggerVisibleChanged;
            FormClosed -= OnDebuggerClosed;
            Disposed -= OnDebuggerClosed;
            _statesTimer?.Stop();
            _statesTimer?.Dispose();
            _statesTimer = null;
            if (stNodeEditor1 is DebuggerNodeEditor editor && !editor.IsDisposed)
                editor.DebugMarkProvider = null;
        }

        private void OnDebuggerChanged()
        {
            if (IsDisposed)
                return;
            if (!LiveLinkDebugger.ShowStates && _stateLines.Count != 0)
                _stateLines.Clear();
            RefreshDebugMarks();
        }

        private void OnDebuggerHit(ScriptHit hit)
        {
            if (IsDisposed)
                return;
            RefreshDebugMarks();
            //The states as they are where the scripts stopped
            if (Visible && LiveLinkDebugger.ShowStates)
                BeginInvoke(new Action(async () => await PollStates()));
        }

        private void OnDebuggerVisibleChanged(object sender, EventArgs e)
        {
            if (Visible)
                RefreshDebugMarks();
        }

        /// <summary>The instance this page shows (null: every instance of its composite), as the display was reached.</summary>
        private List<ShortGuid> DebugPath()
        {
            CompositeDisplay display = _debuggerDisplay;
            if (display == null || display.IsDisposed || display.Composite != _composite)
                return null;
            return LiveLink.InstancePath(display, _commands);
        }

        private string DebugPlaceKey(List<ShortGuid> path)
        {
            return (_composite?.shortGUID.AsUInt32 ?? 0).ToString("X8") + "/" + (path == null ? "*" : string.Join(",", path.Select(o => o.AsUInt32.ToString("X8"))));
        }

        /// <summary>The breakpoint a node of this page shows: one for this instance, else one for every instance, else one for another instance.</summary>
        private ScriptBreakpoint DrawnBreakpoint(Entity entity, List<ShortGuid> path)
        {
            if (_composite == null || entity == null)
                return null;
            ScriptBreakpoint exact = LiveLinkDebugger.Find(_composite, path, entity);
            if (exact != null)
                return exact;
            ScriptBreakpoint any = LiveLinkDebugger.Find(_composite, null, entity);
            if (any != null)
                return any;
            return LiveLinkDebugger.Breakpoints.FirstOrDefault(o => o.Composite == _composite.shortGUID && o.Entity == entity.shortGUID);
        }

        /// <summary>Work out again what each node shows, and repaint if it changed. UI thread.</summary>
        private void RefreshDebugMarks()
        {
            if (IsDisposed || _composite == null)
                return;
            List<ShortGuid> path = DebugPath();
            _debugPlaceKey = DebugPlaceKey(path);
            _debugPlaceCheckedAt = Environment.TickCount;
            _breakpointMarks.Clear();
            foreach (ScriptBreakpoint bp in LiveLinkDebugger.Breakpoints)
            {
                if (bp.Composite != _composite.shortGUID)
                    continue;
                ScriptBreakpoint drawn = DrawnBreakpoint(_composite.GetEntityByID(bp.Entity), path);
                if (drawn != bp)
                    continue;
                _breakpointMarks[bp.Entity.AsUInt32] = bp.Armed ? BreakpointMark.Armed : !bp.Enabled ? BreakpointMark.Off : BreakpointMark.Waiting;
            }

            //The node the scripts are held at: the entity the held call goes to, when it is in the instance shown here (any
            //instance of this composite, for a page opened from the browser) - or the node whose breakpoint was hit
            _heldHere = false;
            ScriptHit hit = LiveLinkDebugger.LastHit;
            if (LiveLinkDebugger.Held && hit != null && hit.Sequence != 0)
            {
                bool samePlace = hit.Composite == _composite.shortGUID.AsUInt32 && (path == null || path.Select(o => o.AsUInt32).SequenceEqual(hit.Path));
                if (samePlace && _composite.GetEntityByID(new ShortGuid(hit.Entity)) != null)
                {
                    _heldHere = true;
                    _heldEntity = hit.Entity;
                }
                else
                {
                    ScriptBreakpoint bp = LiveLinkDebugger.Breakpoints.FirstOrDefault(o => o.GameId == hit.Breakpoint && hit.Breakpoint != 0);
                    if (bp != null && bp.Composite == _composite.shortGUID && _breakpointMarks.ContainsKey(bp.Entity.AsUInt32))
                    {
                        _heldHere = true;
                        _heldEntity = bp.Entity.AsUInt32;
                    }
                }
            }
            if (Visible && stNodeEditor1 != null && !stNodeEditor1.IsDisposed)
                stNodeEditor1.Invalidate();
        }

        private NodeDebugMark DebugMarkOf(STNode node)
        {
            if (node?.Entity == null)
                return NodeDebugMark.None;
            //The place can change without an event (a page shown again after navigating): looked at again now and then
            if (unchecked(Environment.TickCount - _debugPlaceCheckedAt) > 500)
            {
                _debugPlaceCheckedAt = Environment.TickCount;
                if (DebugPlaceKey(DebugPath()) != _debugPlaceKey)
                {
                    _stateLines.Clear();
                    BeginInvoke(new Action(RefreshDebugMarks));
                }
            }
            uint id = node.Entity.shortGUID.AsUInt32;
            if (_breakpointMarks.Count == 0 && !_heldHere && _stateLines.Count == 0)
                return NodeDebugMark.None;
            NodeDebugMark mark = new NodeDebugMark();
            if (_breakpointMarks.TryGetValue(id, out BreakpointMark breakpoint))
                mark.Breakpoint = breakpoint;
            mark.Held = _heldHere && _heldEntity == id;
            if (_stateLines.TryGetValue(id, out string[] lines))
                mark.States = lines;
            return mark;
        }

        #region SETTING
        private void ConfigureBreakpointItems(object sender, System.ComponentModel.CancelEventArgs e)
        {
            STNode node = stNodeEditor1.GetHoveredNode();
            STNodeOption pin = stNodeEditor1.GetHoveredPin();
            //Top and bottom pins show the node's menu (as ContextMenu_Opening has it)
            if (pin != null && (pin.Location == PinLocation.Top || pin.Location == PinLocation.Bottom))
                pin = null;
            Entity entity = node?.Entity ?? pin?.Owner?.Entity;
            _breakpointEntity = null;
            bool breakable = entity != null && _composite != null && !(entity is VariableEntity);
            bool onMethod = false;
            if (breakable)
            {
                List<ShortGuid> methods = LiveLinkDebugger.BreakableMethods(_commands, _composite, entity);
                breakable = methods.Count != 0;
                onMethod = pin != null && pin.Location == PinLocation.Left && methods.Contains(pin.ShortGUID);
            }
            ScriptBreakpoint bp = breakable ? DrawnBreakpoint(entity, DebugPath()) : null;
            _breakpointSeparator.Visible = breakable && (pin == null || onMethod);
            _breakpointItem.Visible = breakable && pin == null;
            _breakpointItem.Checked = bp != null;
            _breakpointItem.Text = bp != null ? "Remove Breakpoint" : "Set Breakpoint";
            _breakpointMethodItem.Visible = breakable && onMethod;
            if (!breakable)
                return;
            _breakpointEntity = entity;
            if (onMethod)
            {
                _breakpointMethod = pin.ShortGUID;
                _breakpointMethodItem.Checked = bp != null && bp.Methods.Contains(pin.ShortGUID);
                _breakpointMethodItem.Text = "Break on '" + pin.ShortGUID.ToString() + "'";
            }
            string unavailable = LiveLinkDebugger.Unavailable;
            _breakpointItem.ToolTipText = unavailable == null
                ? "Stop the level's scripts (and the game) when any of this entity's methods is about to be called in the running game."
                : "Kept here, and set in the game once it can take it: " + unavailable + ".";
        }

        /// <summary>Set or take away a breakpoint on an entity of this page (in the instance it shows); with methods, just those. UI thread.</summary>
        private async System.Threading.Tasks.Task ToggleBreakpoint(Entity entity, List<ShortGuid> methods)
        {
            if (entity == null || _composite == null)
                return;
            List<ShortGuid> path = DebugPath();
            //The breakpoint the node shows is the one taken away (it may be for every instance, or another one)
            ScriptBreakpoint drawn = DrawnBreakpoint(entity, path);
            if (drawn != null && methods == null)
                path = drawn.Path;
            else if (drawn != null && methods != null && drawn.Methods.Contains(methods[0]))
                path = drawn.Path;
            string result = await LiveLinkDebugger.Toggle(_commands, _composite, path, entity, methods);
            Singleton.Editor?.ShowLiveLinkActivity(result);
            RefreshDebugMarks();
        }

        /// <summary>F9 sets or takes away a breakpoint on the node under the mouse (else on the selected nodes); F8 continues and F10 steps while the scripts are held.</summary>
        internal bool TryHandleDebuggerKey(Keys keyData)
        {
            if (_breakpointItem == null || (keyData & Keys.Modifiers) != Keys.None)
                return false;
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.F9)
            {
                List<Entity> entities = new List<Entity>();
                STNode hovered = stNodeEditor1.GetHoveredNode();
                if (hovered?.Entity != null)
                    entities.Add(hovered.Entity);
                else
                    entities.AddRange(stNodeEditor1.GetSelectedNode().Where(o => o?.Entity != null).Select(o => o.Entity).Distinct());
                entities = entities.Where(o => !(o is VariableEntity)).ToList();
                if (entities.Count == 0)
                    return false;
                BeginInvoke(new Action(async () =>
                {
                    foreach (Entity entity in entities.Take(16))
                        await ToggleBreakpoint(entity, null);
                }));
                return true;
            }
            if ((key == Keys.F8 || key == Keys.F10) && LiveLinkDebugger.Held)
            {
                bool step = key == Keys.F10;
                BeginInvoke(new Action(async () =>
                {
                    string result = step ? await LiveLinkDebugger.Step() : await LiveLinkDebugger.Continue();
                    Singleton.Editor?.ShowLiveLinkActivity(result);
                }));
                return true;
            }
            return false;
        }
        #endregion

        #region LIVE STATES
        //The state flags shown, in this order (others are left out: they say little on a flowgraph)
        private static readonly (int bit, string name)[] StateFlags =
        {
            (2, "Started"), (3, "Paused"), (7, "Enabled"), (9, "Locked"), (5, "Opened"), (6, "Closed"), (14, "On"), (18, "Updating"),
            (19, "Suspended"), (21, "Invisible"), (22, "Frozen"),
        };
        private const int InitialisedBit = 28;

        /// <summary>A state as the strip above a node shows it.</summary>
        public static string StateText(uint state)
        {
            List<string> names = StateFlags.Where(o => (state & (1u << o.bit)) != 0).Select(o => o.name).ToList();
            if ((state & (1u << InitialisedBit)) == 0)
                names.Insert(0, "Not initialised");
            return names.Count == 0 ? "(idle)" : string.Join(" · ", names);
        }

        private static string KindOf(DataType type)
        {
            switch (type)
            {
                case DataType.BOOL: return "bool";
                case DataType.INTEGER: return "int";
                case DataType.FLOAT: return "float";
                case DataType.STRING: return "string";
                case DataType.VECTOR: return "vector";
                case DataType.COLOUR: return "vector";
                case DataType.ENUM: return "enum";
                case DataType.RESOURCE: return "guid";
                default: return null;
            }
        }

        private Dictionary<uint, DataType> PinTypes(Entity entity)
        {
            if (unchecked(Environment.TickCount - _pinTypesAt) > 10000)
            {
                _pinTypes.Clear();
                _pinTypesAt = Environment.TickCount;
            }
            uint id = entity.shortGUID.AsUInt32;
            if (_pinTypes.TryGetValue(id, out Dictionary<uint, DataType> types))
                return types;
            types = new Dictionary<uint, DataType>();
            try
            {
                foreach ((ShortGuid pin, ParameterVariant variant, DataType type) in _commands.Utils.GetAllParameters(entity, _composite))
                    if (!types.ContainsKey(pin.AsUInt32) && variant != ParameterVariant.METHOD_PIN && variant != ParameterVariant.METHOD_FUNCTION)
                        types[pin.AsUInt32] = type;
            }
            catch
            {
                //No types for it: no values shown
            }
            _pinTypes[id] = types;
            return types;
        }

        /// <summary>Read the states (and data pin values) of this page's nodes from the game, and show them. UI thread (the page's timer).</summary>
        private async System.Threading.Tasks.Task PollStates()
        {
            if (_statesBusy || IsDisposed || !Visible || _composite == null)
                return;
            if (!LiveLinkDebugger.ShowStates || !LiveLink.Connected || LiveLinkDebugger.Unavailable != null)
            {
                if (_stateLines.Count != 0)
                {
                    _stateLines.Clear();
                    stNodeEditor1.Invalidate();
                }
                return;
            }
            _statesBusy = true;
            try
            {
                List<ShortGuid> path = DebugPath();
                string place = "composite=" + _composite.shortGUID.AsUInt32.ToString("X8") + " path=" + (path == null ? "*" : string.Join(",", path.Select(o => o.AsUInt32.ToString("X8"))));
                List<STNode> nodes = stNodeEditor1.Nodes.Cast<STNode>().Where(o => o?.Entity != null && !(o.Entity is VariableEntity)).ToList();
                List<uint> entities = nodes.Select(o => o.Entity.shortGUID.AsUInt32).Distinct().Take(MaxStateEntities).ToList();
                if (entities.Count == 0)
                    return;
                LiveLink.Reply states = await LiveLink.DebugMenuCommand("debugger.entities", place + " entities=" + string.Join(",", entities.Select(o => o.ToString("X8"))), 3.0);
                if (IsDisposed)
                    return;
                Dictionary<uint, List<string>> lines = new Dictionary<uint, List<string>>();
                string problem = null;
                if (!states.Ok)
                    problem = LiveLinkDebugger.DescribeRefusal(states);
                else
                {
                    Dictionary<string, string> fields = LiveLinkDebugger.Fields(states.Message);
                    fields.TryGetValue("problem", out problem);
                    foreach (KeyValuePair<string, string> pair in fields)
                    {
                        if (pair.Key.Length == 8 && uint.TryParse(pair.Key, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint entity)
                            && uint.TryParse(pair.Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint state))
                            lines[entity] = new List<string>() { StateText(state) };
                    }
                }

                //The data pins drawn on the nodes the game has: what each reads now
                if (problem == null && lines.Count != 0)
                {
                    List<string> asks = new List<string>();
                    Dictionary<string, (uint entity, string pin)> named = new Dictionary<string, (uint, string)>();
                    foreach (STNode node in nodes)
                    {
                        uint id = node.Entity.shortGUID.AsUInt32;
                        if (!lines.ContainsKey(id))
                            continue;
                        Dictionary<uint, DataType> types = PinTypes(node.Entity);
                        foreach (STNodeOption[] options in new[] { node.GetTopOptions(), node.GetBottomOptions() })
                        {
                            if (options == null)
                                continue;
                            foreach (STNodeOption option in options)
                            {
                                if (option == null || option == STNodeOption.Empty || asks.Count >= MaxStateValues)
                                    continue;
                                uint pin = option.ShortGUID.AsUInt32;
                                string key = id.ToString("X8") + "." + pin.ToString("X8");
                                if (named.ContainsKey(key) || !types.TryGetValue(pin, out DataType type))
                                    continue;
                                string kind = KindOf(type);
                                if (kind == null)
                                    continue;
                                asks.Add(id.ToString("X8") + ":" + pin.ToString("X8") + ":" + kind);
                                named[key] = (id, option.ShortGUID.ToString());
                            }
                        }
                    }
                    if (asks.Count != 0)
                    {
                        LiveLink.Reply values = await LiveLink.DebugMenuCommand("debugger.values", place + " values=" + string.Join(",", asks), 3.0);
                        if (IsDisposed)
                            return;
                        if (values.Ok)
                        {
                            foreach (string raw in (values.Message ?? "").Split('\n'))
                            {
                                string line = raw.TrimEnd('\r');
                                if (line.Length < 17 || line[8] != '.')
                                    continue;
                                string key = line.Substring(0, 17);
                                if (!named.TryGetValue(key, out (uint entity, string pin) at) || !lines.TryGetValue(at.entity, out List<string> entityLines))
                                    continue;
                                if (line.Length > 17 && line[17] == '=')
                                    entityLines.Add(at.pin + " = " + line.Substring(18));
                            }
                        }
                    }
                }

                _stateLines.Clear();
                foreach (KeyValuePair<uint, List<string>> pair in lines)
                    _stateLines[pair.Key] = pair.Value.ToArray();
                if (problem != _statesProblem)
                {
                    _statesProblem = problem;
                    if (problem != null)
                        Singleton.Editor?.ShowLiveLinkActivity("Live Link: live states can't be shown on " + _composite.name + " - " + problem);
                }
                stNodeEditor1.Invalidate();
            }
            catch (Exception ex)
            {
                Debug.Log("Live Link", "Could not show live states: " + ex.Message);
            }
            finally
            {
                _statesBusy = false;
            }
        }
        #endregion
    }
}
