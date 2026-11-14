using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.DockPanels;
using OpenCAGE.MCP;
using OpenCAGE.Popups.Base;
using OpenCAGE.Surfaces;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    /// <summary>
    /// "Find Z-Fighting...": surfaces drawn over each other at the same depth, which flicker in game, between placed models (View menu)
    /// or inside one model (Model Editor). Fix pushes one surface out until they are the clearance apart, as one undo step
    /// (find_z_fighting / fix_z_fighting do the same).
    /// </summary>
    public class ZFightWindow : BaseWindow
    {
        private readonly Models.CS2 _model;
        private readonly Action _modelChanged;

        private readonly ComboBox _scope = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        private readonly NumericUpDown _radius = new NumericUpDown() { Minimum = 1, Maximum = 500, Value = 15, Width = 55 };
        private readonly NumericUpDown _gap = new NumericUpDown() { Minimum = 0.05m, Maximum = 200, DecimalPlaces = 2, Increment = 0.5m, Width = 60 };
        private readonly NumericUpDown _angle = new NumericUpDown() { Minimum = 0, Maximum = 30, DecimalPlaces = 1, Increment = 0.5m, Width = 50 };
        private readonly NumericUpDown _minArea = new NumericUpDown() { Minimum = 0, Maximum = 10000, DecimalPlaces = 1, Width = 60 };
        private readonly CheckBox _inside = new CheckBox() { Text = "Inside one model too", AutoSize = true, Checked = true };
        private readonly CheckBox _hidden = new CheckBox() { Text = "Models hidden at the start", AutoSize = true };
        private readonly CheckBox _decals = new CheckBox() { Text = "Mesh decals on what they decorate", AutoSize = true };
        private readonly Button _find = new Button() { Text = "Find", AutoSize = true };
        private readonly ListView _list = new ListView() { View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = true, Width = 760, Height = 190 };
        private readonly PictureBox _preview = new PictureBox() { Width = 760, Height = 380, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(32, 34, 38) };
        private readonly ComboBox _action = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
        private readonly NumericUpDown _clearance = new NumericUpDown() { Minimum = 0.1m, Maximum = 500, DecimalPlaces = 1, Increment = 1, Width = 60 };
        private readonly Button _fixSelected = new Button() { Text = "Fix Selected", AutoSize = true, Enabled = false };
        private readonly Button _fixAll = new Button() { Text = "Fix All Listed", AutoSize = true, Enabled = false };
        private readonly Button _close = new Button() { Text = "Close", AutoSize = true };
        private readonly Label _status = new Label() { AutoSize = true, MaximumSize = new Size(760, 0) };
        private readonly Label _title = new Label() { AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), MaximumSize = new Size(760, 0) };

        private ZScan _scan;
        private ZFixPlan _plan;
        private bool _busy;
        private CancellationTokenSource _cancel;
        private readonly System.Windows.Forms.Timer _replan = new System.Windows.Forms.Timer() { Interval = 250 };

        /// <summary>The window opened last (for UI automation).</summary>
        public static ZFightWindow Latest { get; private set; }

        /// <summary>The last search and fix.</summary>
        public ZScan Scan => _scan;
        public JObject LastResult { get; private set; }

        private ZFightWindow(Models.CS2 model, Action modelChanged) : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            _model = model;
            _modelChanged = modelChanged;
            Latest = this;
            Text = model == null ? "Find Z-Fighting" : "Find Z-Fighting - " + System.IO.Path.GetFileName(model.Name);
            Icon = SharedFormIcon.Icon;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            StayAboveEditor = true;
            EnableDocumentation("https://opencage.co.uk/docs/z-fighting/");

            _gap.Value = (decimal)(McpSurfaceTools.Defaults.Gap * 1000);
            _angle.Value = (decimal)McpSurfaceTools.Defaults.MaxAngle;
            _minArea.Value = (decimal)(McpSurfaceTools.Defaults.MinArea * 10000);
            _clearance.Value = (decimal)(McpSurfaceTools.Defaults.Clearance * 1000);
            _scope.Items.AddRange(new object[] { "The whole level", "Round the selected entity", "The composite on screen" });
            _scope.SelectedIndex = 0;
            _action.Items.AddRange(new object[] { "Push the one on top out", "Sink the one underneath back" });
            _action.SelectedIndex = 0;
            _list.Columns.Add("#", 40);
            _list.Columns.Add("Overlap", 70);
            _list.Columns.Add("Apart", 95);
            _list.Columns.Add("On top", 240);
            _list.Columns.Add("Underneath", 210);
            _list.Columns.Add("Kind", 95);

            FlowLayoutPanel root = new FlowLayoutPanel() { FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(10), WrapContents = false };
            _title.Text = model == null ? "Surfaces of placed models drawn over each other at the same depth" : "Meshes of " + model.Name + " drawn over each other at the same depth";
            root.Controls.Add(_title);
            root.Controls.Add(new Label()
            {
                AutoSize = true,
                MaximumSize = new Size(760, 0),
                Text = "Two surfaces that face the same way and lie within the gap of each other flicker in game as the camera moves: the graphics card picks either from pixel to pixel. " +
                    "Fix pushes the one on top out (or sinks the one underneath) until they are the clearance apart" + (model == null ? ": a placed model moves, or a model's own vertices for a fight inside one model." : ": the model's vertices move (every placement of the model changes)."),
            });
            if (model == null)
                root.Controls.Add(Row(Lbl("Search:"), _scope, Lbl("  within"), _radius, Lbl("m of it")));
            root.Controls.Add(Row(Lbl("Closer than"), _gap, Lbl("mm, within"), _angle, Lbl("degrees of parallel, at least"), _minArea, Lbl("cm²"), _find));
            if (model == null) root.Controls.Add(Row(_inside, _hidden, _decals));
            else root.Controls.Add(Row(_decals));
            root.Controls.Add(_list);
            root.Controls.Add(_preview);
            root.Controls.Add(Row(Lbl("Fix:"), _action, Lbl("until"), _clearance, Lbl("mm apart"), _fixSelected, _fixAll, _close));
            root.Controls.Add(_status);
            Controls.Add(root);

            //Named for UI automation
            _find.Name = "zfFind"; _list.Name = "zfList"; _fixSelected.Name = "zfFixSelected"; _fixAll.Name = "zfFixAll"; _scope.Name = "zfScope"; _gap.Name = "zfGap"; _clearance.Name = "zfClearance"; _action.Name = "zfAction";
            _scope.SelectedIndexChanged += (s, e) => _radius.Enabled = _scope.SelectedIndex == 1;
            _radius.Enabled = false;
            _find.Click += (s, e) => Find();
            _list.SelectedIndexChanged += (s, e) => QueueReplan();
            _action.SelectedIndexChanged += (s, e) => QueueReplan();
            _clearance.ValueChanged += (s, e) => QueueReplan();
            _replan.Tick += (s, e) => { _replan.Stop(); Replan(); };
            _fixSelected.Click += (s, e) => Fix(Selected());
            _fixAll.Click += (s, e) => Fix(_scan?.Fights);
            _close.Click += (s, e) => Close();
            FormClosed += (s, e) => { _cancel?.Cancel(); _replan.Stop(); _replan.Dispose(); _preview.Image?.Dispose(); };
            Say(model == null ? "Pick where to search and press Find." : "Press Find.", false);
        }

        private static Label Lbl(string text) => new Label() { Text = text, AutoSize = true, Padding = new Padding(0, 5, 0, 0) };

        private static FlowLayoutPanel Row(params Control[] controls)
        {
            FlowLayoutPanel row = new FlowLayoutPanel() { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
            row.Controls.AddRange(controls);
            return row;
        }

        /// <summary>The window for the open level (View > Find Z-Fighting...).</summary>
        public static ZFightWindow ForLevel() => new ZFightWindow(null, null);

        /// <summary>The window for one model's own meshes (the Model Editor). <paramref name="changed"/> runs after a fix, an undo or a redo.</summary>
        public static ZFightWindow ForModel(Models.CS2 model, Action changed) => new ZFightWindow(model, changed);

        private static Level OpenLevel => Singleton.Editor?.CompositeBrowser?.Content?.Level;

        private void Say(string text, bool problem)
        {
            _status.Text = text;
            _status.ForeColor = problem ? Color.Firebrick : SystemColors.ControlText;
        }

        public ZFightOptions Options() => new ZFightOptions()
        {
            Gap = (float)_gap.Value / 1000f,
            MaxAngle = (float)_angle.Value,
            MinArea = (double)_minArea.Value / 10000.0,
            InsideGroups = _model != null || _inside.Checked,
            IncludeHidden = _hidden.Checked,
        };

        public ZFixOptions FixOptions() => new ZFixOptions()
        {
            Action = _action.SelectedIndex == 1 ? ZFixAction.Sink : ZFixAction.Push,
            Clearance = (float)_clearance.Value / 1000f,
        };

        #region Finding
        /// <summary>Search with the options shown (gathering on the editor's thread, the search on a worker). Returns when done.</summary>
        public Task Find()
        {
            if (_busy) return Task.CompletedTask;
            Level level = OpenLevel;
            if (level == null) { Say("No level is open.", true); return Task.CompletedTask; }
            ZScan scan;
            try
            {
                Cursor = Cursors.WaitCursor;
                if (_model != null)
                {
                    if (!level.Models.Entries.Contains(_model)) { Say("That model is no longer in the level.", true); return Task.CompletedTask; }
                    scan = ZFightScene.FromModel(_model, Options());
                    scan.Level = level;
                }
                else
                    scan = ZFightScene.FromRegion(null, level, Region(level), Options());
            }
            catch (Exception e)
            {
                Say("Can't search: " + e.Message, true);
                return Task.CompletedTask;
            }
            finally { Cursor = Cursors.Default; }

            _busy = true;
            SetEnabled(false);
            _cancel = new CancellationTokenSource();
            CancellationToken token = _cancel.Token;
            Say("Searching " + scan.Surfaces.Count.ToString("N0") + " surfaces...", false);
            bool decals = _decals.Checked;
            return Task.Run(() =>
            {
                ZFightScene.Run(scan, token, p => BeginInvoke(new Action(() => { if (!IsDisposed && _busy) Say("Searching " + scan.Surfaces.Count.ToString("N0") + " surfaces... " + (p * 100).ToString("0") + "%", false); })));
                if (!decals) McpSurfaceTools.Filter(scan);
            }).ContinueWith(t =>
            {
                if (IsDisposed) return;
                _busy = false;
                SetEnabled(true);
                if (t.Exception != null)
                {
                    Say("The search failed: " + t.Exception.GetBaseException().Message, true);
                    return;
                }
                Show(scan);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>Where to search, as the scope box says. Editor thread.</summary>
        private McpRegion Region(Level level)
        {
            Commands commands = level.Commands;
            CompositeDisplay display = Singleton.Editor?.CompositeDisplay;
            switch (_scope.SelectedIndex)
            {
                case 1:
                    {
                        Entity entity = display?.EntityDisplay?.Entity;
                        Composite composite = display?.Composite;
                        if (entity == null || composite == null) throw new InvalidOperationException("select an entity first (the search goes round it)");
                        McpPlacements walker = McpRegion.Walker(commands);
                        List<McpPlacements.Placement> found = new List<McpPlacements.Placement>();
                        cTransform world;
                        if (composite == commands.EntryPoints[0]) world = walker.Evaluate(composite, new List<Entity>() { entity }).World;
                        else
                        {
                            walker.PlacementsOf(commands.EntryPoints[0], composite, entity, found, 1, CancellationToken.None, realOnly: true);
                            world = found.FirstOrDefault()?.World;
                        }
                        if (world == null) throw new InvalidOperationException("the selected entity has no place in the level");
                        return McpRegion.Resolve(null, commands, level, new JObject() { ["near"] = McpValues.Vector(world.position), ["radius"] = (double)_radius.Value });
                    }
                case 2:
                    {
                        Composite composite = display?.Composite;
                        if (composite == null || composite == commands.EntryPoints[0]) return null;
                        return McpRegion.Resolve(null, commands, level, new JObject() { ["composite"] = composite.name });
                    }
                default:
                    return null;
            }
        }

        private void SetEnabled(bool on)
        {
            foreach (Control c in new Control[] { _find, _scope, _gap, _angle, _minArea, _inside, _hidden, _decals }) c.Enabled = on;
            _radius.Enabled = on && _scope.SelectedIndex == 1;
            _fixSelected.Enabled = on && _scan != null && _list.SelectedItems.Count != 0;
            _fixAll.Enabled = on && _scan != null && _scan.Fights.Count != 0;
        }

        private void Show(ZScan scan)
        {
            _scan = scan;
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (ZFight f in scan.Fights.Take(2000))
            {
                ListViewItem item = new ListViewItem(new[] { f.Id.ToString(), ZFightPreview.Area(f.Area), Apart(f), f.A.Label, f.B.Label, McpSurfaceTools.Category(f) }) { Tag = f };
                item.ToolTipText = (ZFightScene.SourceOf(f.A)?.Placement?.Path ?? "") + "\n" + (ZFightScene.SourceOf(f.B)?.Placement?.Path ?? "");
                _list.Items.Add(item);
            }
            _list.EndUpdate();
            if (_list.Items.Count != 0) _list.Items[0].Selected = true;
            else { _plan = null; DrawPreview(null); }
            string what = scan.Fights.Count == 0
                ? "Nothing fights within " + _gap.Value + " mm"
                : scan.Fights.Count + (scan.Fights.Count == 1 ? " overlap" : " overlaps") + ", " + ZFightPreview.Area(scan.FightArea) + " in all" + (scan.Fights.Count > 2000 ? " (the largest 2000 listed)" : "");
            Say(what + " - " + scan.Surfaces.Count.ToString("N0") + " surfaces of " + scan.Scope + " searched in " + (scan.Stats?.Seconds ?? 0).ToString("0.0") + " s." + (scan.Notes.Count != 0 ? " " + string.Join(" ", scan.Notes) : ""), false);
            SetEnabled(true);
            LastResult = McpSurfaceTools.Describe(scan, 40);
        }

        private static string Apart(ZFight f) => f.Kind == "crossing" ? "crosses (" + ZFightRepair.Mm(f.DeepestMin).Replace(" mm", "") + " to " + ZFightRepair.Mm(f.DeepestMax) + ")" : Math.Abs(f.OffsetMax - f.OffsetMin) < 0.0002f ? ZFightRepair.Mm(f.OffsetMean) : ZFightRepair.Mm(f.OffsetMin).Replace(" mm", "") + " to " + ZFightRepair.Mm(f.OffsetMax);

        /// <summary>Select one fight in the list by its number (shows its picture and planned fix).</summary>
        public void SelectFight(int id)
        {
            _list.SelectedItems.Clear();
            ListViewItem item = _list.Items.Cast<ListViewItem>().FirstOrDefault(o => (o.Tag as ZFight)?.Id == id);
            if (item == null) return;
            item.Selected = true;
            item.EnsureVisible();
            _replan.Stop();
            Replan();
        }

        private List<ZFight> Selected() => _list.SelectedItems.Cast<ListViewItem>().Select(o => o.Tag as ZFight).Where(o => o != null).ToList();
        #endregion

        #region Preview and fixing
        private void QueueReplan()
        {
            _fixSelected.Enabled = !_busy && _scan != null && _list.SelectedItems.Count != 0;
            _replan.Stop();
            _replan.Start();
        }

        /// <summary>Plan the selected fights' fix (nothing changes) and show the first.</summary>
        public void Replan()
        {
            if (_busy || IsDisposed || _scan == null) return;
            List<ZFight> fights = Selected();
            if (fights.Count == 0) { _plan = null; DrawPreview(null); return; }
            try
            {
                Cursor = Cursors.WaitCursor;
                _plan = ZFightRepair.Plan(_scan, fights, FixOptions());
                DrawPreview(fights[0]);
                string moves = string.Join("; ", _plan.Moves.Take(3).Select(o => o.Label)) + (_plan.Moves.Count > 3 ? "; and " + (_plan.Moves.Count - 3) + " more" : "");
                string check = _plan.Verified ? " Searched again with the moves made: " + (_plan.AreaLeft < 1e-6 ? "nothing left" : ZFightPreview.Area(_plan.AreaLeft) + " left") + (_plan.Made.Count != 0 ? ", " + _plan.Made.Count + " new overlap(s) the moves would make" : ", nothing new") + "." : "";
                Say("Fix: " + (moves.Length != 0 ? moves : "nothing to move") + "." + check + (_plan.Notes.Count != 0 ? " " + string.Join(" ", _plan.Notes) : ""), _plan.Made.Count != 0 || _plan.AreaLeft > 1e-6);
            }
            catch (Exception e) { Say("Can't plan the fix: " + e.Message, true); }
            finally { Cursor = Cursors.Default; }
        }

        private void DrawPreview(ZFight fight)
        {
            Image old = _preview.Image;
            _preview.Image = ZFightPreview.Draw(_scan, fight, _plan, _preview.Width, _preview.Height);
            old?.Dispose();
        }

        /// <summary>Fix these fights (one undo step) and search again.</summary>
        public void Fix(List<ZFight> fights)
        {
            if (_busy || _scan == null || fights == null || fights.Count == 0) return;
            Level level = OpenLevel;
            if (level == null || (_scan.Level != null && _scan.Level != level)) { Say("The level has changed: press Find again.", true); return; }
            if (_scan.LevelMode && MCP.McpCollision.Version != _scan.Version) { Say("The level has changed since the search: press Find again, so the fix starts from where things are now.", true); return; }
            try
            {
                Cursor = Cursors.WaitCursor;
                ZFixPlan plan = ZFightRepair.Plan(_scan, fights, FixOptions());
                if (plan.Moves.Count == 0) { Say("Nothing to move: " + string.Join(" ", plan.Notes), true); return; }
                if (plan.Made.Count != 0 && fights.Count > 1 &&
                    MessageBox.Show(this, "The moves would make " + plan.Made.Count + " new overlap(s) (a moved surface onto something else). Fix anyway?", "Fix z-fighting", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                    return;
                McpEditor.BeginCall(null);
                ZFightRepair.Outcome outcome = ZFightRepair.Apply(plan, "", _modelChanged);
                LastResult = McpSurfaceTools.DescribePlan(plan);
                LastResult["undo"] = outcome.Label;
                Say("Fixed: " + string.Join("; ", plan.Moves.Take(3).Select(o => o.Label)) + (plan.Moves.Count > 3 ? "; and " + (plan.Moves.Count - 3) + " more" : "") + ". Edit > Undo takes it back ('" + outcome.Label + "'). Save with a build to put it in the game.", false);
            }
            catch (Exception e)
            {
                Say("Not fixed: " + e.Message, true);
                return;
            }
            finally { Cursor = Cursors.Default; }
            string said = _status.Text;
            Find().ContinueWith(t => { if (!IsDisposed) Say(said + " Searched again: " + (_scan.Fights.Count == 0 ? "nothing left." : _scan.Fights.Count + " overlap(s) listed."), false); }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        #endregion

        #region Menus
        private static ZFightWindow _levelWindow;

        /// <summary>Add "Find Z-Fighting..." under a menu item (View, beside the Model Editor): the window for the open level.</summary>
        public static void AttachMenu(ToolStripMenuItem after)
        {
            if (after?.Owner == null) return;
            ToolStripMenuItem item = new ToolStripMenuItem("Find Z-Fighting...") { ToolTipText = "Find placed models whose surfaces lie over each other at the same depth (they flicker in game), and fix them" };
            ToolStrip owner = after.Owner;
            owner.Items.Insert(owner.Items.IndexOf(after) + 1, item);
            item.Click += (s, e) => OpenForLevel();
        }

        /// <summary>The level's window (one at a time), shown.</summary>
        public static ZFightWindow OpenForLevel()
        {
            if (OpenLevel == null) return null;
            if (_levelWindow == null || _levelWindow.IsDisposed)
            {
                _levelWindow = ForLevel();
                _levelWindow.Show();
            }
            else _levelWindow.Activate();
            return _levelWindow;
        }

        /// <summary>The window for one of the open level's models, by name, shown (nothing to redraw after a fix).</summary>
        public static ZFightWindow OpenForModel(string name)
        {
            Level level = OpenLevel;
            Models.CS2 model = level?.Models?.Entries?.FirstOrDefault(o => o != null && (string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(System.IO.Path.GetFileNameWithoutExtension(o.Name), name, StringComparison.OrdinalIgnoreCase)));
            if (model == null) return null;
            ZFightWindow window = ForModel(model, null);
            window.Show();
            return window;
        }

        /// <summary>Add "Find Z-Fighting..." to the Model Editor's tree: the model being edited. <paramref name="changed"/> redraws the editor after a fix.</summary>
        public static void AttachToModelEditor(TreeView tree, Func<Models.CS2> model, Action changed)
        {
            if (tree == null) return;
            ContextMenuStrip menu = tree.ContextMenuStrip ?? new ContextMenuStrip();
            ToolStripMenuItem item = new ToolStripMenuItem("Find Z-Fighting...") { ToolTipText = "Find this model's meshes that lie over each other at the same depth (they flicker in game), and fix them" };
            menu.Items.Add(item);
            menu.Opening += (s, e) => item.Visible = model?.Invoke() != null && OpenLevel != null;
            item.Click += (s, e) =>
            {
                Models.CS2 m = model?.Invoke();
                if (m == null) return;
                ZFightWindow window = ForModel(m, changed);
                window.Show();
            };
            tree.ContextMenuStrip = menu;
        }
        #endregion
    }
}
