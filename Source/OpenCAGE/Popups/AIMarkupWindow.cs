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
    /// File > Place AI Markup: finds spots on the navmesh for retail's search and area sweep jobs and melee high cover (<see cref="AIMarkup.Propose"/>),
    /// places the ticked ones as one undo step, and compares the tool with a level's own markup. Importing the composites is not undoable.
    /// </summary>
    internal partial class AIMarkupWindow : BaseWindow
    {
        private static AIMarkupWindow _open;

        private Composite _into;

        public AIMarkupWindow() : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            InitializeComponent();
            Icon = SharedFormIcon.Icon;
            StayAboveEditor = true;
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/ai-markup/");

            _searchSpacing.Value = (decimal)AIMarkup.SearchJob.Spacing;
            _sweepSpacing.Value = (decimal)AIMarkup.AreaSweepJob.Spacing;
            _highCoverSpacing.Value = (decimal)AIMarkup.HighCover.Spacing;

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
        private void _compare_Click(object sender, EventArgs e) => CompareWithLevel();
        private void _place_Click(object sender, EventArgs e) => PlaceTicked();
        private void _spots_ItemChecked(object sender, ItemCheckedEventArgs e) => UpdateButtons();

        private void _bottomPanel_Resize(object sender, EventArgs e)
        {
            int w = _bottomPanel.ClientSize.Width, y = _bottomPanel.ClientSize.Height - 34;
            _status.Width = w - 16;
            _find.Location = new Point(8, y);
            _compare.Location = new Point(_find.Right + 8, y);
            _place.Location = new Point(w - _place.Width - 8, y);
        }

        #region Opening it
        /// <summary>The window (one at a time), shown beside the editor.</summary>
        public static AIMarkupWindow Open()
        {
            if (Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands == null)
                return null;
            if (_open != null && !_open.IsDisposed)
            {
                _open.Summarise();
                _open.Activate();
                return _open;
            }
            _open = new AIMarkupWindow();
            _open.FormClosed += (s, e) => { if (_open == s) _open = null; };
            if (Singleton.Editor != null) _open.Show(Singleton.Editor); else _open.Show();
            return _open;
        }

        /// <summary>File > Place AI Markup, after the Generate Map Assets item (or the nearest level tool before it, or <paramref name="after"/>).</summary>
        public static void AttachMenu(ToolStripMenuItem after)
        {
            ToolStrip owner = after?.Owner;
            if (owner == null) return;
            ToolStripDropDownItem menu = after.OwnerItem as ToolStripDropDownItem;
            ToolStripMenuItem item = new ToolStripMenuItem("Place AI Markup") { Name = "aiMarkupToolStripMenuItem", ToolTipText = "Give NPCs spots to search at and sweep over, and androids walls to throw the player against: place retail's search jobs, area sweep jobs and melee high cover over the navmesh" };
            item.Click += (s, e) => Open();
            int at = owner.Items.IndexOf(after) + 1;
            ToolStripItem anchor = new[] { "mapAssetsToolStripMenuItem", "alienVentsToolStripMenuItem", "levelCheckToolStripMenuItem" }
                .Select(n => owner.Items.Cast<ToolStripItem>().FirstOrDefault(o => o.Name == n)).FirstOrDefault(o => o != null);
            if (anchor != null) at = owner.Items.IndexOf(anchor) + 1;
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

        private MarkupOptions Options()
        {
            MarkupOptions options = new MarkupOptions()
            {
                Search = _search.Checked,
                Sweep = _sweep.Checked,
                HighCover = _highCover.Checked,
                KeepClearOfExisting = _keepClear.Checked,
                IndoorsOnly = _indoors.Checked,
                Into = _into,
            };
            options.Spacing[AIMarkup.SearchJob.Key] = (float)_searchSpacing.Value;
            options.Spacing[AIMarkup.AreaSweepJob.Key] = (float)_sweepSpacing.Value;
            options.Spacing[AIMarkup.HighCover.Key] = (float)_highCoverSpacing.Value;
            return options;
        }

        /// <summary>What the level has of each kind.</summary>
        public void Summarise()
        {
            Level level = Content?.Level;
            if (level?.Commands == null) { _summary.Text = "No level is open."; return; }
            List<string> lines = new List<string>();
            try
            {
                List<PlacedMarkup> markup = AIMarkup.Survey(level.Commands);
                List<MarkupStanding> standing = AIMarkup.OnNavmesh(level, markup);
                foreach (MarkupKind kind in AIMarkup.Kinds)
                {
                    List<PlacedMarkup> mine = markup.Where(o => o.Kind == kind).ToList();
                    int off = standing.Count(o => o.Markup.Kind == kind && o.OnNavmesh == false);
                    lines.Add(kind.NamePrefix + ": " + (mine.Count == 0 ? "none" : mine.Count + " (" + mine.Count(o => o.User) + " yours)" +
                        (mine.Count > 1 ? ", median spacing " + Math.Round(AIMarkup.MedianSpacing(mine.Select(o => o.Position).ToList()), 1).ToString(CultureInfo.InvariantCulture) + " m" : "") +
                        (off != 0 ? ", " + off + " OFF the navmesh" : "")) + ".");
                }
                if (standing.Count == 0 && markup.Count != 0)
                    lines.Add("The level has no navmesh yet: Save & Build with the navmesh baker before finding spots.");
                if (BuildTracker.NeedsBuild)
                    lines.Add("The level has changes a Save & Build has not taken yet: the navmesh is as last built.");
            }
            catch (Exception e)
            {
                lines.Add("Could not read the level's markup: " + e.Message);
            }
            _summary.Text = string.Join(Environment.NewLine, lines);
        }

        private void FindSpots()
        {
            LevelContent content = Content;
            if (content?.Level == null) return;
            Cursor = Cursors.WaitCursor;
            _status.Text = "Looking for spots (reading the navmesh" + (_highCover.Checked ? " and the collision" : "") + ")...";
            _status.Refresh();
            try
            {
                MarkupProposal proposal = AIMarkup.Propose(AIMarkup.EditorCall(), content, Options());
                _spots.BeginUpdate();
                _spots.Items.Clear();
                int n = 1;
                foreach (MarkupSpot spot in proposal.Spots)
                {
                    bool high = spot.Kind == AIMarkup.HighCover;
                    ListViewItem item = new ListViewItem(new[]
                    {
                        (n++).ToString(), spot.Kind.NamePrefix, McpRegion.Format(spot.Position), spot.Clearance.ToString("0.00", CultureInfo.InvariantCulture) + " m",
                        high ? spot.Yaw.ToString("0", CultureInfo.InvariantCulture) + "°" : "", high ? spot.Wall.ToString("0.00", CultureInfo.InvariantCulture) + " m" : "",
                    }) { Tag = spot, Checked = true };
                    _spots.Items.Add(item);
                }
                _spots.EndUpdate();
                _status.Text = string.Join(", ", AIMarkup.Kinds.Where(o => proposal.Spots.Any(s => s.Kind == o)).Select(o => proposal.Spots.Count(s => s.Kind == o) + " " + o.NamePrefix)) +
                    (proposal.Spots.Count == 0 ? "No spots" : "") + " over " + Math.Round(proposal.Area) + " m² of floor." + (proposal.Notes.Count != 0 ? " " + string.Join(" ", proposal.Notes) : "");
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

        private void CompareWithLevel()
        {
            LevelContent content = Content;
            if (content?.Level == null) return;
            Cursor = Cursors.WaitCursor;
            _status.Text = "Finding spots round the level's own markup and comparing...";
            _status.Refresh();
            try
            {
                List<MarkupComparison> results = AIMarkup.Compare(AIMarkup.EditorCall(), content, Options());
                _summary.Text = results.Count == 0 ? "The level places none of the ticked kinds, so there is nothing to compare with." : string.Join(Environment.NewLine, results.Select(o => o.Summary()));
                _status.Text = "Compared (above): the level's own markup is left as it is.";
            }
            catch (Exception e)
            {
                _status.Text = "Could not compare: " + e.Message;
            }
            finally
            {
                Cursor = Cursors.Default;
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
            List<MarkupSpot> spots = _spots.CheckedItems.Cast<ListViewItem>().Select(o => o.Tag as MarkupSpot).Where(o => o != null).ToList();
            if (spots.Count == 0) return;
            List<MarkupKind> missing = AIMarkup.Missing(content.Level.Commands, spots.Select(o => o.Kind));
            if (missing.Count != 0 && MessageBox.Show(this, "The level does not have " + string.Join(", ", missing.Select(o => o.Path)) + " yet. It is brought in from " +
                (AIMarkup.SourceFor() ?? "(no level of this install)") + " first, as Import Composites From Level does: that cannot be undone (the markup itself can).\n\nGo ahead?",
                "AI Markup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            Cursor = Cursors.WaitCursor;
            try
            {
                MarkupPlaced placed = AIMarkup.Place(content, spots, Options(), "", show: true);
                foreach (ListViewItem item in _spots.CheckedItems.Cast<ListViewItem>().ToList()) _spots.Items.Remove(item);
                _status.Text = "Placed " + placed.Names.Count + " in " + EditorUtils.GetCompositeName(placed.Into) + " (Undo takes them out). " + string.Join(" ", placed.Notes);
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
    }
}
