using CATHODE;
using CATHODE.Scripting;
using CathodeLib;
using OpenCAGE.MCP;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    /// <summary>
    /// Utilities > Calculate Alien Vents: finds spots on the navmesh for retail's vent composites (<see cref="AlienVents.Propose"/>), places the
    /// ticked ones as one undo step and runs Save &amp; Build to bake the backstage. Importing the vent composites is not undoable.
    /// </summary>
    internal partial class AlienVentsWindow : BaseWindow
    {
        private static AlienVentsWindow _open;

        private Composite _into;
        private VentProposal _proposal;

        private sealed class KindChoice
        {
            public VentKind Kind;
            public override string ToString() => Kind == null ? "Auto (by the ceiling over each spot)" : Kind.Label;
        }

        public AlienVentsWindow() : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            InitializeComponent();
            Icon = SharedFormIcon.Icon;
            StayAboveEditor = true;
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/alien-vents/");

            _kind.Items.Add(new KindChoice());
            foreach (VentKind kind in AlienVents.Kinds) _kind.Items.Add(new KindChoice() { Kind = kind });
            _kind.SelectedIndex = 0;

            Theming.ThemeManager.ApplyCurrent(this);
            SetInto(null);
            Summarise();
        }

        private void _useShown_Click(object sender, EventArgs e)
        {
            Composite shown = Singleton.Editor?.CompositeDisplay?.Composite;
            if (shown != null) SetInto(shown);
        }

        private void _useRoot_Click(object sender, EventArgs e) => SetInto(null);
        private void _find_Click(object sender, EventArgs e) => FindSpots();
        private void _place_Click(object sender, EventArgs e) => PlaceTicked();
        private void _build_Click(object sender, EventArgs e) => SaveAndBuild();
        private void _spots_ItemChecked(object sender, ItemCheckedEventArgs e) => UpdateButtons();

        private void _check_Click(object sender, EventArgs e)
        {
            Summarise();
            _status.Text = "Checked the level's vents (above).";
        }

        private void _bottomPanel_Resize(object sender, EventArgs e)
        {
            int w = _bottomPanel.ClientSize.Width, y = _bottomPanel.ClientSize.Height - 34;
            _status.Width = w - 16;
            _find.Location = new Point(8, y);
            _check.Location = new Point(_find.Right + 8, y);
            _build.Location = new Point(w - _build.Width - 8, y);
            _place.Location = new Point(_build.Left - _place.Width - 8, y);
        }

        #region Opening it
        /// <summary>The window (one at a time), shown beside the editor.</summary>
        public static AlienVentsWindow Open()
        {
            if (Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands == null)
                return null;
            if (_open != null && !_open.IsDisposed)
            {
                _open.Summarise();
                _open.Activate();
                return _open;
            }
            _open = new AlienVentsWindow();
            _open.FormClosed += (s, e) => { if (_open == s) _open = null; };
            if (Singleton.Editor != null) _open.Show(Singleton.Editor); else _open.Show();
            return _open;
        }

        /// <summary>The window set to add <paramref name="count"/> vents (0: as many as the area calls for) to a network (the Level Check's fix).</summary>
        public static AlienVentsWindow Open(int network, int count)
        {
            AlienVentsWindow window = Open();
            if (window == null) return null;
            window._network.Value = Math.Max(window._network.Minimum, Math.Min(window._network.Maximum, network));
            window._count.Value = Math.Max(window._count.Minimum, Math.Min(window._count.Maximum, count));
            return window;
        }

        /// <summary>Utilities > Calculate Alien Vents, after the Check Level item (or after <paramref name="after"/>).</summary>
        public static void AttachMenu(ToolStripMenuItem after)
        {
            ToolStrip owner = after?.Owner;
            if (owner == null) return;
            ToolStripDropDownItem menu = after.OwnerItem as ToolStripDropDownItem;
            ToolStripMenuItem item = new ToolStripMenuItem("Calculate Alien Vents") { Name = "alienVentsToolStripMenuItem", ToolTipText = "Give the alien vents to go backstage through: find spots under ceilings, place retail's vent composites, bake the backstage (an alien with no vents crashes the game when it retreats)" };
            item.Click += (s, e) => Open();
            int at = owner.Items.IndexOf(after) + 1;
            ToolStripItem check = owner.Items.Cast<ToolStripItem>().FirstOrDefault(o => o.Name == "levelCheckToolStripMenuItem");
            if (check != null) at = owner.Items.IndexOf(check) + 1;
            owner.Items.Insert(at, item);
            if (menu != null)
                menu.DropDownOpening += (s, e) => item.Enabled = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands != null;
        }
        #endregion

        private LevelContent Content => Singleton.Editor?.CompositeBrowser?.Content;

        private void SetInto(Composite composite)
        {
            Commands commands = Content?.Level?.Commands;
            _into = composite == null || commands == null || composite == commands.EntryPoints[0] ? null : composite;
            _intoLabel.Text = _into == null ? "the root composite (" + (commands?.EntryPoints[0]?.name ?? "?") + ")" : _into.name;
        }

        private VentOptions Options() => new VentOptions()
        {
            Count = (int)_count.Value,
            Spacing = (float)_spacing.Value,
            Kind = (_kind.SelectedItem as KindChoice)?.Kind,
            Killtraps = _killtraps.Checked,
            Network = (int)_network.Value,
            Into = _into,
        };

        /// <summary>What the level has: an alien that needs vents, its vents per network, what is baked, what is wrong.</summary>
        public void Summarise()
        {
            Level level = Content?.Level;
            if (level?.Commands == null) { _summary.Text = "No level is open."; return; }
            List<string> lines = new List<string>();
            try
            {
                AlienBackstageCheck.Result aliens = AlienBackstageCheck.Check(level.Commands);
                VentReport report = AlienVents.Check(level);
                if (aliens.Crashes)
                    lines.Add("THE GAME WILL CRASH: the alien can spawn here (" + string.Join(", ", aliens.Aliens.Take(3).Select(o => o.name)) + ") and the level has no vent. Add at least 3.");
                else if (report.Vents.Count == 0)
                    lines.Add("The level places no vents" + (aliens.PlacedNodes == 0 ? "" : "") + ".");
                if (report.Vents.Count != 0)
                {
                    lines.Add(report.Vents.Count + " vent(s) (" + report.Vents.Count(o => o.User) + " yours), kinds: " +
                        string.Join(", ", report.Vents.GroupBy(o => o.Kind?.Key ?? "other").Select(o => o.Count() + " x " + o.Key)) + ".");
                    foreach (AlienVents.Sheet sheet in report.Sheets)
                        lines.Add("  Network " + sheet.Network + ": " + sheet.Vents + " vent(s), spread " + Math.Round(sheet.Spread, 1) + " m, backstage sheet " +
                            (sheet.Builds ? sheet.Triangles + " triangles" : "NONE") + (sheet.Warning != null ? " (" + sheet.Warning + ")" : "") + ".");
                    if (report.Baked != null)
                        lines.Add(report.Baked.HasNavmesh ? "Saved navmesh: " + report.Baked.LinkBottoms.Count + " Backstage link(s), " + report.Baked.SheetPolygons + " backstage polygon(s)." : "The level has no navmesh yet.");
                }
                foreach (VentIssue issue in report.Issues)
                    lines.Add((issue.Severity == LevelCheckSeverity.Broken ? "BROKEN: " : "WARNING: ") + issue.Title + ". " + AlienVents.List(issue.Vents, 3));
                if (BuildTracker.NeedsBuild)
                    lines.Add("The level has changes a Save & Build has not taken yet: the navmesh above is as last built.");
            }
            catch (Exception e)
            {
                lines.Add("Could not read the level's vents: " + e.Message);
            }
            _summary.Text = string.Join(Environment.NewLine, lines);
        }

        private void FindSpots()
        {
            LevelContent content = Content;
            if (content?.Level == null) return;
            Cursor = Cursors.WaitCursor;
            _status.Text = "Looking for spots (reading the collision)...";
            _status.Refresh();
            try
            {
                _proposal = AlienVents.Propose(AlienVents.EditorCall(), content, Options());
                _spots.BeginUpdate();
                _spots.Items.Clear();
                int n = 1;
                foreach (VentSpot spot in _proposal.Spots)
                {
                    ListViewItem item = new ListViewItem(new[]
                    {
                        (n++).ToString(), McpRegion.Format(spot.Floor), spot.Ceiling.ToString("0.0", CultureInfo.InvariantCulture) + " m", spot.Kind.Label,
                        spot.Yaw.ToString("0", CultureInfo.InvariantCulture) + "°", spot.OpenFor.ToString("0.0", CultureInfo.InvariantCulture) + " m",
                    }) { Tag = spot, Checked = true };
                    _spots.Items.Add(item);
                }
                _spots.EndUpdate();
                string sheet = _proposal.Sheet == null ? "" : " With them network " + _proposal.Sheet.Network + " has " + _proposal.Sheet.Vents + " vents: " +
                    (_proposal.Sheet.Builds ? "a backstage sheet of " + _proposal.Sheet.Triangles + " triangles." : "NO backstage sheet (" + _proposal.Sheet.Warning + ").");
                _status.Text = _proposal.Spots.Count + " spot(s) of " + _proposal.Wanted + " wanted (" + _proposal.Candidates + " of " + _proposal.Samples + " samples fit; the rest: " +
                    AlienVents.Describe(_proposal.Rejected) + ")." + sheet + (_proposal.Notes.Count != 0 ? " " + string.Join(" ", _proposal.Notes) : "");
            }
            catch (Exception e)
            {
                _status.Text = "Could not find spots: " + e.Message;
            }
            finally
            {
                Cursor = Cursors.Default;
                UpdateButtons();
            }
        }

        private void UpdateButtons()
        {
            _place.Enabled = _spots.CheckedItems.Count != 0;
        }

        private void PlaceTicked()
        {
            LevelContent content = Content;
            if (content?.Level == null) return;
            List<VentSpot> spots = _spots.CheckedItems.Cast<ListViewItem>().Select(o => o.Tag as VentSpot).Where(o => o != null).ToList();
            if (spots.Count == 0) return;
            List<VentKind> missing = AlienVents.Missing(content.Level.Commands, spots.Select(o => o.Kind));
            if (missing.Count != 0 && MessageBox.Show(this, "The level does not have " + string.Join(", ", missing.Select(o => o.Path)) + " yet. It is brought in from " +
                string.Join(", ", missing.Select(o => AlienVents.SourceFor(o) ?? "(no level of this install)").Distinct()) + " first, as Import Composites From Level does: that cannot be undone (the vents themselves can).\n\nGo ahead?",
                "Alien Vents", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            Cursor = Cursors.WaitCursor;
            try
            {
                VentsPlaced placed = AlienVents.Place(content, spots, Options(), "", show: true);
                foreach (ListViewItem item in _spots.CheckedItems.Cast<ListViewItem>().ToList()) _spots.Items.Remove(item);
                _status.Text = "Placed " + placed.Names.Count + " vent(s) in " + EditorUtils.GetCompositeName(placed.Into) + " (" + string.Join(", ", placed.Names) + "; Undo takes them out). " +
                    string.Join(" ", placed.Notes) + " Save && Build bakes it now.";
                Summarise();
            }
            catch (Exception e)
            {
                _status.Text = "Not placed: " + e.Message;
            }
            finally
            {
                Cursor = Cursors.Default;
                UpdateButtons();
            }
        }

        private void SaveAndBuild()
        {
            if (Singleton.Editor == null || Content?.Level == null) return;
            _status.Text = "Saving and building (the navmesh baker bakes the backstage)...";
            _status.Refresh();
            try
            {
                Singleton.Editor.SaveLevel(true);
            }
            catch (Exception e)
            {
                _status.Text = "The save failed: " + e.Message;
                return;
            }
            if (IsDisposed) return;
            Summarise();
            _status.Text = "Saved and built: the vents above are as the game will use them.";
        }
    }
}
