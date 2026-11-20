using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.DockPanels;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    /// <summary>
    /// Utilities > Check Level: what the Level Check (Scripts\LevelCheck) finds in the open level, crashes first, with Fix where the fix is
    /// mechanical and Dismiss (kept per level on this PC). Modeless, because the script editor's tools refuse to run under a modal window.
    /// </summary>
    public partial class LevelCheckWindow : BaseWindow
    {
        private static LevelCheckWindow _open;

        private LevelCheckReport _report;

        public LevelCheckWindow() : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            InitializeComponent();
            Icon = SharedFormIcon.Icon;
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/level-check/");

            Theming.ThemeManager.ApplyCurrent(this);
            LayoutButtons(_buttonsPanel);
        }

        private void _run_Click(object sender, EventArgs e) => RunCheck();
        private void _showDismissed_CheckedChanged(object sender, EventArgs e) => Fill();
        private void _list_SelectedIndexChanged(object sender, EventArgs e) => ShowSelected();
        private void _list_DoubleClick(object sender, EventArgs e) => GoTo();
        private void _fix_Click(object sender, EventArgs e) => ApplyFix();
        private void _dismiss_Click(object sender, EventArgs e) => ToggleDismiss();
        private void _goTo_Click(object sender, EventArgs e) => GoTo();
        private void _buttonsPanel_Resize(object sender, EventArgs e) => LayoutButtons(_buttonsPanel);

        private void LayoutButtons(Panel panel)
        {
            _goTo.Left = panel.ClientSize.Width - _goTo.Width;
            _dismiss.Left = _goTo.Left - _dismiss.Width - 6;
        }

        #region Opening it
        /// <summary>The window, open or brought forward, showing <paramref name="report"/> (or running the check when null).</summary>
        public static LevelCheckWindow Open(LevelCheckReport report = null)
        {
            if (_open == null || _open.IsDisposed)
            {
                _open = new LevelCheckWindow();
                _open.FormClosed += (s, e) => _open = null;
                if (Singleton.Editor != null) _open.Show(Singleton.Editor); else _open.Show();
            }
            else
            {
                if (_open.WindowState == FormWindowState.Minimized) _open.WindowState = FormWindowState.Normal;
                _open.Activate();
            }
            if (report != null) _open.Show(report);
            else _open.RunCheck();
            return _open;
        }

        /// <summary>
        /// Utilities > Check Level, put in after <paramref name="after"/> (Recapture All Cubemaps) in its menu: enabled while a level is open.
        /// Called once from the editor's constructor.
        /// </summary>
        public static void AttachMenu(ToolStripMenuItem after)
        {
            ToolStripDropDownItem menu = after?.OwnerItem as ToolStripDropDownItem;
            ToolStrip owner = after?.Owner;
            if (owner == null) return;
            ToolStripMenuItem item = new ToolStripMenuItem("Check Level") { Name = "levelCheckToolStripMenuItem", ToolTipText = "What in the open level will crash the game or silently not work in it" };
            item.Click += (s, e) => Open();
            owner.Items.Insert(owner.Items.IndexOf(after) + 1, item);
            if (menu != null)
                menu.DropDownOpening += (s, e) => item.Enabled = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands != null;
        }

        /// <summary>
        /// Reports the level's undismissed findings after a Save &amp; Build (CommandsEditor.SaveLevelCore). When <paramref name="interactive"/>
        /// this window opens on them and a crash also gets a message box; otherwise (MCP saves) only the message box is shown.
        /// </summary>
        public static void AfterBuild(IWin32Window owner, LevelContent content, bool interactive)
        {
            IReadOnlyList<string> warnings = content?.LastLevelWarnings;
            if (warnings == null || warnings.Count == 0) return;
            LevelCheckReport report = LevelCheck.Last(content.Level?.Name);
            List<LevelCheckFinding> active = report?.Active.ToList() ?? new List<LevelCheckFinding>();
            List<LevelCheckFinding> crashes = active.Where(o => o.Severity == LevelCheckSeverity.Crash).ToList();
            if (!interactive || report == null)
            {
                bool crash = report == null || crashes.Count != 0;
                MessageBox.Show(owner, "The level saved, but the Level Check found " + (crash ? "what will crash the game" : "what won't work in game") + ":\n\n" + string.Join("\n\n", warnings),
                    crash ? "The game will crash in this level" : "Level Check", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Open(report);
            if (crashes.Count != 0)
                MessageBox.Show(owner, "The level saved, but the game will crash in it:\n\n" + string.Join("\n", crashes.Select(o => "- " + o.Title)) +
                    "\n\nThe Level Check window lists " + (active.Count == crashes.Count ? "them" : "them and " + (active.Count - crashes.Count) + " other finding" + (active.Count - crashes.Count == 1 ? "" : "s")) +
                    ", with where each comes from and what to do.", "The game will crash in this level", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        #endregion

        #region Showing a report
        private void RunCheck()
        {
            Level level = Singleton.Editor?.CompositeBrowser?.Content?.Level;
            if (level?.Commands == null)
            {
                _summary.Text = "No level is open.";
                return;
            }
            Cursor = Cursors.WaitCursor;
            _run.Enabled = false;
            _status.Text = "Checking " + level.Name + "...";
            Application.DoEvents();
            try
            {
                //The UI thread: the script can't change under the check
                LevelCheckReport report = LevelCheck.Run(level);
                Show(report);
                _status.Text = "Checked " + level.Name + " in " + report.Seconds.ToString("0.0") + " s. Fixes are ordinary edits (Undo puts them back); Save & Build takes them into the game.";
            }
            catch (Exception e)
            {
                _status.Text = "The check failed: " + e.Message;
                Debug.Log("LevelCheck", "The Level Check window's run failed: " + e);
            }
            finally
            {
                Cursor = Cursors.Default;
                _run.Enabled = true;
            }
        }

        private void Show(LevelCheckReport report)
        {
            _report = report;
            Fill();
        }

        private static string SeverityText(LevelCheckSeverity severity) =>
            severity == LevelCheckSeverity.Crash ? "CRASH" : severity == LevelCheckSeverity.Broken ? "Won't work" : "Check";

        private void Fill()
        {
            string selected = (_list.SelectedItems.Count != 0 ? _list.SelectedItems[0].Tag as LevelCheckFinding : null)?.Id;
            _list.BeginUpdate();
            _list.Items.Clear();
            if (_report != null)
            {
                foreach (LevelCheckFinding finding in _report.Findings.OrderBy(o => o.Severity))
                {
                    if (finding.Dismissed && !_showDismissed.Checked) continue;
                    ListViewItem item = new ListViewItem(new[] { SeverityText(finding.Severity), finding.Title, finding.Fix == null ? "" : finding.Fix.Global ? "Fix (global)" : "Fix" }) { Tag = finding };
                    if (finding.Dismissed) item.ForeColor = SystemColors.GrayText;
                    else if (finding.Severity == LevelCheckSeverity.Crash) item.ForeColor = Theming.ThemeManager.IsDark ? Color.FromArgb(255, 120, 110) : Color.Firebrick;
                    _list.Items.Add(item);
                    if (finding.Id == selected) item.Selected = true;
                }
            }
            _list.EndUpdate();
            if (_list.SelectedItems.Count == 0 && _list.Items.Count != 0) _list.Items[0].Selected = true;
            ShowSelected();
            _summary.Text = Summary();
        }

        private string Summary()
        {
            if (_report == null) return "";
            int active = _report.Active.Count(), dismissed = _report.Findings.Count - active;
            List<string> parts = new List<string>();
            parts.Add(_report.Level + ": " + (active == 0 ? "nothing found" : active + " finding" + (active == 1 ? "" : "s")) + (dismissed != 0 ? " (" + dismissed + " dismissed)" : ""));
            if (_report.Figures["characters"] is Newtonsoft.Json.Linq.JObject characters)
                parts.Add("characters it can have alive at once: " + characters["can_reach"] + " of " + characters["pool"]);
            if (_report.Figures["textures"] is Newtonsoft.Json.Linq.JObject textures)
                parts.Add("textures streamed at once: about " + textures["peak_mb"] + " of " + textures["heap_mb"] + " MB");
            if (_report.Failed.Count != 0)
                parts.Add(_report.Failed.Count + " check" + (_report.Failed.Count == 1 ? "" : "s") + " failed (" + string.Join(", ", _report.Failed.Keys) + ")");
            return string.Join("   |   ", parts);
        }

        private LevelCheckFinding Selected => _list.SelectedItems.Count != 0 ? _list.SelectedItems[0].Tag as LevelCheckFinding : null;

        private void ShowSelected()
        {
            LevelCheckFinding finding = Selected;
            _fix.Enabled = finding?.Fix != null;
            _fix.Text = finding?.Fix != null ? finding.Fix.Label : "Fix";
            _dismiss.Enabled = finding != null;
            _dismiss.Text = finding?.Dismissed == true ? "Bring Back" : "Dismiss";
            _goTo.Enabled = finding?.Sources.Any(o => o.Composite != null) == true;
            if (finding == null)
            {
                _details.Text = _report == null ? "" : _report.Active.Any() ? "" : "Nothing to report: none of the Level Check's checks found a problem" + (_report.Findings.Count != 0 ? " that hasn't been dismissed." : ".");
                return;
            }
            List<string> lines = new List<string>() { finding.Title, "", finding.Text };
            if (finding.Sources.Count != 0)
            {
                lines.Add("");
                lines.Add("Where:");
                lines.AddRange(finding.Sources.Select(o => "  " + o.Text));
            }
            if (finding.Fix != null)
            {
                lines.Add("");
                lines.Add("Fix: " + finding.Fix.Detail);
            }
            if (finding.Dismissed)
            {
                lines.Add("");
                lines.Add("Dismissed for this level: Save & Build no longer says it.");
            }
            _details.Text = string.Join(Environment.NewLine, lines);
        }
        #endregion

        #region Acting on a finding
        private void ApplyFix()
        {
            LevelCheckFinding finding = Selected;
            if (finding?.Fix == null) return;
            if (finding.Fix.Global && MessageBox.Show(this, finding.Fix.Detail + "\n\nChange it?", "A global change", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            Cursor = Cursors.WaitCursor;
            try
            {
                string done = finding.Fix.Apply(false);
                _status.Text = done;
                RunCheck();
                _status.Text = done;
            }
            catch (Exception e)
            {
                _status.Text = "Not fixed: " + e.Message;
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void ToggleDismiss()
        {
            LevelCheckFinding finding = Selected;
            if (finding == null || _report == null) return;
            //Set: the kept report's findings (this window's, after a build or a check) follow it
            bool dismiss = !finding.Dismissed;
            LevelCheck.Dismissals.Set(_report.Level, finding.Id, dismiss);
            finding.Dismissed = dismiss;
            _status.Text = finding.Dismissed ? "Dismissed for " + _report.Level + ": a Save & Build no longer says it. Show dismissed brings it back." : "Brought back: a Save & Build says it again.";
            Fill();
        }

        private void GoTo()
        {
            LevelCheckSource source = Selected?.Sources.FirstOrDefault(o => o.Composite != null);
            if (source == null) return;
            CommandsEditor editor = Singleton.Editor;
            Commands commands = editor?.CompositeBrowser?.Content?.Level?.Commands;
            if (commands == null || !commands.Entries.Contains(source.Composite))
            {
                _status.Text = "That composite is no longer in the level: Check Now again.";
                return;
            }
            CompositeDisplay display = editor.CompositeBrowser.LoadComposite(source.Composite);
            if (display == null || source.Entity == null || source.Composite.GetEntityByID(source.Entity.shortGUID) != source.Entity) return;
            display.BeginInvoke(new Action(() =>
            {
                try { if (!display.IsDisposed && display.Composite == source.Composite) display.LoadEntity(source.Entity, false); }
                catch (Exception e) { Debug.Log("LevelCheck", "Could not select the entity: " + e.Message); }
            }));
        }
        #endregion
    }
}
