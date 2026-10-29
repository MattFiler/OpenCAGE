using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using OpenCAGE.RuntimeUtilsConnection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.DockPanels
{
    /* Live Link script debugger (see LiveLinkDebugger): the composite display's Script Debugger menu - Go to Breakpoint (each
       breakpoint set goes to its node), Remove All Breakpoints, Show Live States Above Nodes, Select Entity Under Game
       Crosshair (from the Debug Menu's World > Inspect), Pause the Game's Scripts, Jump to Node When a Breakpoint Hits - and,
       while the scripts are held, Continue, Step and where they are held. A breakpoint hit brings its node to the front
       (unless Jump to Node is off). The menu is there while the game is connected, or while breakpoints are kept for it. */
    public partial class CompositeDisplay
    {
        private ToolStripSeparator _debuggerSeparator;
        private ToolStripDropDownButton _debuggerMenu;
        private ToolStripMenuItem _debuggerBreakpoints;
        private ToolStripMenuItem _debuggerRemoveAll;
        private ToolStripMenuItem _debuggerShowStates;
        private ToolStripMenuItem _debuggerSelectFromGame;
        private ToolStripMenuItem _debuggerPause;
        private ToolStripMenuItem _debuggerFollowHits;
        private ToolStripMenuItem _debuggerErrorLog;
        private ToolStripButton _debuggerContinue;
        private ToolStripButton _debuggerStep;
        private ToolStripLabel _debuggerStatus;

        /// <summary>A breakpoint hit brings its node to the front (the Script Debugger menu's Jump to Node When a Breakpoint Hits). Session only.</summary>
        public static bool DebuggerFollowsHits = true;

        //The Script Debugger menu's tooltip while the game's script debugger can be used
        private const string DebuggerMenuTip = "Script breakpoints and live states in the running game (Live Link, with the game's Debug Menu on).\n"
            + "Right-click a node and Set Breakpoint (or press F9 over it): the level's scripts - and the game - stop when one of its methods is about to be called, "
            + "and the node is brought to the front here. F8 continues, F10 steps.";

        private void SetupLiveLinkDebugger()
        {
            _debuggerSeparator = new ToolStripSeparator();
            _debuggerMenu = new ToolStripDropDownButton("Script Debugger")
            {
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                ToolTipText = DebuggerMenuTip,
            };
            EditorIcons.Bind(_debuggerMenu, EditorIcon.DebugScripts);
            _debuggerBreakpoints = new ToolStripMenuItem("Go to Breakpoint")
            {
                ToolTipText = "The breakpoints set: click one to bring its node to the front.",
            };
            EditorIcons.Bind(_debuggerBreakpoints, EditorIcon.Breakpoint);
            _debuggerRemoveAll = new ToolStripMenuItem("Remove All Breakpoints");
            EditorIcons.Bind(_debuggerRemoveAll, EditorIcon.RemoveBreakpoints);
            _debuggerRemoveAll.Click += async (s, e) => Singleton.Editor?.ShowLiveLinkActivity(await LiveLinkDebugger.RemoveAll());
            _debuggerShowStates = new ToolStripMenuItem("Show Live States Above Nodes")
            {
                CheckOnClick = true,
                ToolTipText = "Above each node of the flowgraph on screen: its entity's state in the running game (started, enabled, locked...) and what its data pins read now, twice a second.",
            };
            EditorIcons.Bind(_debuggerShowStates, EditorIcon.LiveStates);
            _debuggerShowStates.CheckedChanged += (s, e) => LiveLinkDebugger.ShowStates = _debuggerShowStates.Checked;
            _debuggerSelectFromGame = new ToolStripMenuItem("Select Entity Under Game Crosshair")
            {
                ToolTipText = "Select the entity the game's crosshair is on (turn the crosshair on in the Debug Menu's World > Inspect).",
            };
            EditorIcons.Bind(_debuggerSelectFromGame, EditorIcon.SelectFromGame);
            _debuggerSelectFromGame.Click += async (s, e) => await SelectFromGame();
            _debuggerPause = new ToolStripMenuItem("Pause the Game's Scripts")
            {
                ToolTipText = "Hold the level's scripts (and the game) now, as a breakpoint does, until Continue.",
            };
            EditorIcons.Bind(_debuggerPause, EditorIcon.PauseScripts);
            _debuggerPause.Click += async (s, e) => Singleton.Editor?.ShowLiveLinkActivity(await LiveLinkDebugger.Pause());
            _debuggerFollowHits = new ToolStripMenuItem("Jump to Node When a Breakpoint Hits")
            {
                CheckOnClick = true,
                Checked = DebuggerFollowsHits,
                ToolTipText = "When a breakpoint is hit, bring its node to the front (and flash OpenCAGE's taskbar button).",
            };
            EditorIcons.Bind(_debuggerFollowHits, EditorIcon.JumpToBreakpointHit);
            _debuggerFollowHits.CheckedChanged += (s, e) => DebuggerFollowsHits = _debuggerFollowHits.Checked;
            _debuggerErrorLog = new ToolStripMenuItem("Runtime Error Log", ScriptErrorLog.SignImage(false, 16))
            {
                ToolTipText = "The level's script errors as the running game finds them: double-click one to go to its entity (Launch Game's Error log opens it as the game starts).",
            };
            _debuggerErrorLog.Click += (s, e) => ErrorLog.Open();
            _debuggerMenu.DropDownItems.AddRange(new ToolStripItem[] { _debuggerBreakpoints, _debuggerRemoveAll, new ToolStripSeparator(), _debuggerShowStates,
                _debuggerSelectFromGame, _debuggerPause, new ToolStripSeparator(), _debuggerFollowHits, new ToolStripSeparator(), _debuggerErrorLog });
            _debuggerMenu.DropDownOpening += (s, e) =>
            {
                LiveLinkDebugger.PanelOpen = true;
                FillDebuggerMenu();
            };
            _debuggerMenu.DropDownClosed += (s, e) => LiveLinkDebugger.PanelOpen = false;

            _debuggerContinue = new ToolStripButton("Continue")
            {
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                ToolTipText = "Let the held scripts go on (F8 on a flowgraph).",
            };
            EditorIcons.Bind(_debuggerContinue, EditorIcon.ContinueScripts);
            _debuggerContinue.Click += async (s, e) => Singleton.Editor?.ShowLiveLinkActivity(await LiveLinkDebugger.Continue());
            _debuggerStep = new ToolStripButton("Step")
            {
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                ToolTipText = "Make the held call and the rest of the calls due at the same moment (one round), then hold again (F10 on a flowgraph).",
            };
            EditorIcons.Bind(_debuggerStep, EditorIcon.StepScripts);
            _debuggerStep.Click += async (s, e) => Singleton.Editor?.ShowLiveLinkActivity(await LiveLinkDebugger.Step());
            _debuggerStatus = new ToolStripLabel("") { ForeColor = System.Drawing.Color.FromArgb(230, 150, 40) };
            _debuggerStatus.Click += (s, e) => LiveLinkDebugger.ShowHit(LiveLinkDebugger.LastHit);

            toolStrip1.Items.Add(_debuggerSeparator);
            toolStrip1.Items.Add(_debuggerMenu);
            toolStrip1.Items.Add(_debuggerContinue);
            toolStrip1.Items.Add(_debuggerStep);
            toolStrip1.Items.Add(_debuggerStatus);
            UpdateDebuggerButtons();

            LiveLink.ConnectionChanged += OnDebuggerConnectionChanged;
            LiveLinkDebugger.Changed += UpdateDebuggerButtons;
            LiveLinkDebugger.HitArrived += OnDebuggerHitArrived;
            //A connection made before there was a handle to hear of it on
            HandleCreated += (s, e) => UpdateDebuggerButtons();
            Disposed += (s, e) =>
            {
                LiveLink.ConnectionChanged -= OnDebuggerConnectionChanged;
                LiveLinkDebugger.Changed -= UpdateDebuggerButtons;
                LiveLinkDebugger.HitArrived -= OnDebuggerHitArrived;
            };
        }

        /// <summary>A new flowgraph page shows the debugger's marks and menu items. UI thread.</summary>
        private void AttachLiveLinkDebugger(Flowgraph page)
        {
            page?.AttachLiveLinkDebugger(this);
        }

        /// <summary>The menu while the game is connected or breakpoints are kept; Continue, Step and where while the scripts are held.</summary>
        private void UpdateDebuggerButtons()
        {
            if (IsDisposed || _debuggerMenu == null)
                return;
            bool there = LiveLink.Connected || LiveLinkDebugger.Breakpoints.Count != 0;
            bool held = LiveLink.Connected && LiveLinkDebugger.Held;
            _debuggerSeparator.Visible = there;
            _debuggerMenu.Visible = there;
            int count = LiveLinkDebugger.Breakpoints.Count;
            _debuggerMenu.Text = count == 0 ? "Script Debugger" : "Script Debugger (" + count + ")";
            _debuggerContinue.Visible = held;
            _debuggerStep.Visible = held;
            _debuggerStatus.Visible = held;
            if (held)
            {
                ScriptHit hit = LiveLinkDebugger.LastHit;
                string where = LiveLinkDebugger.HitText(hit);
                _debuggerStatus.Text = hit != null && hit.AtBreakpoint ? "Held: " + where : hit != null && hit.Steps > 0 ? "Held after a step" : "Scripts paused";
                _debuggerStatus.ToolTipText = DescribeHeld(hit) + "\nClick to go to it.";
            }
        }

        private static string DescribeHeld(ScriptHit hit)
        {
            if (hit == null || hit.Sequence == 0)
                return "The level's scripts (and the game) are held by the debugger.";
            Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
            List<string> lines = new List<string>() { "Held at breakpoint " + hit.Breakpoint + ": " + LiveLinkDebugger.HitText(hit) };
            if (hit.Chain.Count != 0)
            {
                lines.Add("Called because of (latest first):");
                foreach ((uint entity, uint method, uint composite, List<uint> path) in hit.Chain.Take(8))
                {
                    Composite holder = commands?.GetComposite(new ShortGuid(composite));
                    Entity found = holder?.GetEntityByID(new ShortGuid(entity));
                    string name = found != null ? LiveLinkDebugger.EntityText(commands, holder, found) + " in " + holder.name : "[" + new ShortGuid(entity).ToByteString() + "]";
                    lines.Add("  '" + new ShortGuid(method).ToString() + "' on " + name);
                }
            }
            return string.Join("\n", lines);
        }

        private void FillDebuggerMenu()
        {
            string unavailable = LiveLinkDebugger.Unavailable;
            _debuggerBreakpoints.DropDownItems.Clear();
            Commands commands = Content?.Level?.Commands;
            foreach (ScriptBreakpoint bp in LiveLinkDebugger.Breakpoints.OrderBy(o => o.Label))
            {
                string text = string.IsNullOrEmpty(bp.Label) ? bp.Composite.ToByteString() + " : " + bp.Entity.ToByteString() : bp.Label;
                if (bp.Path == null)
                    text += " (every instance)";
                if (bp.Hits != 0)
                    text += " - hit " + bp.Hits + "x";
                ToolStripMenuItem item = new ToolStripMenuItem(text) { Checked = bp.Armed };
                item.ToolTipText = !bp.Enabled ? "Switched off in the game" : bp.Armed ? "Stops on " + string.Join(", ", bp.Methods.Select(o => o.ToString())) + " in " + bp.Instances + " running instance(s)"
                    : "Not stopping the game: " + (bp.GameId == 0 ? (unavailable ?? "not in the game yet") : (string.IsNullOrEmpty(bp.Problem) ? "in no running instance" : bp.Problem));
                ScriptBreakpoint target = bp;
                item.Click += (s, e) => GoToBreakpoint(commands, target);
                _debuggerBreakpoints.DropDownItems.Add(item);
            }
            _debuggerBreakpoints.Enabled = _debuggerBreakpoints.DropDownItems.Count != 0;
            _debuggerRemoveAll.Enabled = LiveLinkDebugger.Breakpoints.Count != 0;
            bool connected = LiveLink.Connected;
            _debuggerSelectFromGame.Enabled = connected;
            _debuggerPause.Enabled = connected && !LiveLinkDebugger.Held;
            _debuggerShowStates.Checked = LiveLinkDebugger.ShowStates;
            _debuggerMenu.ToolTipText = unavailable == null ? DebuggerMenuTip : "Script debugger: " + unavailable;
        }

        private static void GoToBreakpoint(Commands commands, ScriptBreakpoint bp)
        {
            if (commands == null || bp == null)
                return;
            if (bp.Path != null && LiveLinkDebugger.ShowEntity(commands, bp.Path, bp.Entity))
                return;
            Composite composite = commands.GetComposite(bp.Composite);
            Entity entity = composite?.GetEntityByID(bp.Entity);
            if (entity != null)
                Singleton.Editor?.CompositeBrowser?.LoadCompositeAndEntity(composite, entity);
        }

        private void OnDebuggerHitArrived(ScriptHit hit)
        {
            if (IsDisposed || Singleton.Editor?.CompositeDisplay != this)
                return;
            UpdateDebuggerButtons();
            if (DebuggerFollowsHits && hit != null && hit.AtBreakpoint)
                LiveLinkDebugger.ShowHit(hit);
        }

        private void OnDebuggerConnectionChanged()
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
                return;
            try
            {
                BeginInvoke(new Action(UpdateDebuggerButtons));
            }
            catch
            {
                //Closing
            }
        }

        /// <summary>Select the entity the game's crosshair is on (the Debug Menu's World > Inspect). UI thread.</summary>
        private async System.Threading.Tasks.Task SelectFromGame()
        {
            Commands commands = Content?.Level?.Commands;
            LiveLinkDebugger.Pick pick = await LiveLinkDebugger.PickFromGame(commands);
            if (!pick.Ok)
            {
                Singleton.Editor?.ShowLiveLinkActivity("Live Link: " + pick.Message);
                MessageBox.Show(pick.Message, "Select Entity Under Game Crosshair", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!LiveLinkDebugger.ShowEntity(commands, pick.Path, pick.Entity.shortGUID))
                Singleton.Editor?.CompositeBrowser?.LoadCompositeAndEntity(pick.Composite, pick.Entity);
            Singleton.Editor?.ShowLiveLinkActivity(pick.Message);
        }
    }
}
