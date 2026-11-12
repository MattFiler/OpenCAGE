using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.MCP;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenCAGE.Breakables
{
    /// <summary>
    /// "Make Breakable...": a placed model that breaks into physics debris when shot, hit or its Break pin is triggered. From the Model
    /// Editor, "Fracture into Debris..." makes only the pieces, as spawnable physics assets. Make runs <see cref="BreakableBuilder"/>.
    /// </summary>
    public class FractureWindow : BaseWindow
    {
        private readonly ShortGuid _compositeId, _entityId;
        private readonly Models.CS2 _model;
        private readonly bool _debrisOnly;

        private readonly NumericUpDown _pieces = new NumericUpDown() { Minimum = FractureGeometry.MinPieces, Maximum = 16, Value = 6, Width = 50 };
        private readonly NumericUpDown _seed = new NumericUpDown() { Minimum = 0, Maximum = 99999, Value = 7, Width = 65 };
        private readonly Button _reseed = new Button() { Text = "Another Way", AutoSize = true };
        private readonly ComboBox _interior = new ComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 95 };
        private readonly CheckBox _skin = new CheckBox() { Text = "Inner skin", AutoSize = true, Checked = true };
        private readonly CheckBox _autoMass = new CheckBox() { Text = "Mass from size", AutoSize = true, Checked = true };
        private readonly NumericUpDown _mass = new NumericUpDown() { Minimum = 0.05m, Maximum = 10000, DecimalPlaces = 2, Value = 10, Width = 75, Enabled = false };
        private readonly NumericUpDown _kick = new NumericUpDown() { Minimum = 0, Maximum = 30, DecimalPlaces = 1, Increment = 0.5m, Value = 2.5m, Width = 55 };
        private readonly CheckBox _damage = new CheckBox() { Text = "Breaks when shot or hit, damage at least", AutoSize = true, Checked = true };
        private readonly NumericUpDown _threshold = new NumericUpDown() { Minimum = 0, Maximum = 10000, Value = 20, Width = 60 };
        private readonly CheckBox _monitor = new CheckBox() { Text = "Also pistol / shotgun hits anywhere round it", AutoSize = true, Checked = true };
        private readonly NumericUpDown _despawn = new NumericUpDown() { Minimum = 0, Maximum = 3600, Value = 0, Width = 60 };
        private readonly TrackBar _explode = new TrackBar() { Minimum = 0, Maximum = 100, Value = 35, TickStyle = TickStyle.None, Width = 160 };
        private readonly PictureBox _preview = new PictureBox() { Width = 560, Height = 320, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(32, 34, 38), SizeMode = PictureBoxSizeMode.Normal };
        private readonly Label _title = new Label() { AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), MaximumSize = new Size(560, 0) };
        private readonly Label _stats = new Label() { AutoSize = true, MaximumSize = new Size(560, 0) };
        private readonly Label _status = new Label() { AutoSize = true, MaximumSize = new Size(560, 0) };
        private readonly Button _make = new Button() { AutoSize = true };
        private readonly Button _close = new Button() { Text = "Close", AutoSize = true };
        private BreakablePlan _plan;
        private bool _busy;
        private readonly Timer _recut = new Timer() { Interval = 350 };

        /// <summary>The last result: what Make did, or the error.</summary>
        public JObject LastResult { get; private set; }

        private FractureWindow(Composite composite, Entity entity, Models.CS2 model) : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            _debrisOnly = model != null;
            _model = model;
            if (composite != null) _compositeId = composite.shortGUID;
            if (entity != null) _entityId = entity.shortGUID;
            Text = _debrisOnly ? "Fracture into Debris" : "Make Breakable";
            Icon = SharedFormIcon.Icon;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            StayAboveEditor = true;
            EnableDocumentation("https://opencage.co.uk/docs/breakables/");

            _interior.Items.AddRange(new object[] { "Dark inside", "Texture inside" });
            _interior.SelectedIndex = 0;
            _make.Text = _debrisOnly ? "Make Debris Pieces" : "Make Breakable";

            FlowLayoutPanel root = new FlowLayoutPanel() { FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(10), WrapContents = false };
            root.Controls.Add(_title);
            root.Controls.Add(new Label()
            {
                AutoSize = true,
                MaximumSize = new Size(560, 0),
                Text = _debrisOnly
                    ? "Each piece becomes a physics asset of its own (its model, collision and a one-body physics system, kicked outwards as it spawns). Spawn one with an AssetSpawner (allow_physics) whose asset is an is_template instance of it, placed at the piece's place relative to the whole model (a spawned asset appears where its template instance is)."
                    : "The model is cut into pieces, each a physics asset of its own, spawned where it was when the model breaks: when it is shot or hit, or when a script calls its Break pin (Broken fires after). It goes into a composite of its own, placed where it was.",
            });
            root.Controls.Add(Row(new Label() { Text = "Pieces:", AutoSize = true, Padding = new Padding(0, 4, 0, 0) }, _pieces,
                new Label() { Text = "  Cut:", AutoSize = true, Padding = new Padding(0, 4, 0, 0) }, _seed, _reseed,
                new Label() { Text = "  Cut faces:", AutoSize = true, Padding = new Padding(0, 4, 0, 0) }, _interior, _skin));
            root.Controls.Add(Row(_autoMass, _mass, new Label() { Text = "kg in all    Thrown out at", AutoSize = true, Padding = new Padding(0, 4, 0, 0) }, _kick, new Label() { Text = "m/s", AutoSize = true, Padding = new Padding(0, 4, 0, 0) }));
            if (!_debrisOnly)
            {
                root.Controls.Add(Row(_damage, _threshold));
                root.Controls.Add(Row(_monitor, new Label() { Text = "   Pieces go after", AutoSize = true, Padding = new Padding(0, 4, 0, 0) }, _despawn, new Label() { Text = "s (0 = they stay)", AutoSize = true, Padding = new Padding(0, 4, 0, 0) }));
            }
            root.Controls.Add(_preview);
            root.Controls.Add(Row(new Label() { Text = "Pushed apart:", AutoSize = true, Padding = new Padding(0, 4, 0, 0) }, _explode));
            root.Controls.Add(_stats);
            root.Controls.Add(_status);
            root.Controls.Add(Row(_make, _close));
            Controls.Add(root);

            foreach (Control c in new Control[] { _pieces, _seed, _skin, _interior })
            {
                if (c is NumericUpDown n) n.ValueChanged += (s, e) => QueueRecut();
                else if (c is CheckBox b) b.CheckedChanged += (s, e) => QueueRecut();
                else if (c is ComboBox x) x.SelectedIndexChanged += (s, e) => QueueRecut();
            }
            _mass.ValueChanged += (s, e) => QueueRecut();
            _kick.ValueChanged += (s, e) => QueueRecut();
            _autoMass.CheckedChanged += (s, e) => { _mass.Enabled = !_autoMass.Checked; QueueRecut(); };
            _damage.CheckedChanged += (s, e) => _threshold.Enabled = _damage.Checked;
            _reseed.Click += (s, e) => _seed.Value = (_seed.Value * 7919 + 101) % 99999;
            _explode.ValueChanged += (s, e) => DrawPreview();
            _recut.Tick += (s, e) => { _recut.Stop(); Recut(); };
            _make.Click += (s, e) => Make();
            _close.Click += (s, e) => Close();
            Shown += (s, e) => Recut();
            FormClosed += (s, e) => { _recut.Stop(); _recut.Dispose(); _preview.Image?.Dispose(); };
        }

        private static FlowLayoutPanel Row(params Control[] controls)
        {
            FlowLayoutPanel row = new FlowLayoutPanel() { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
            row.Controls.AddRange(controls);
            return row;
        }

        /// <summary>The window for a placed model (the entity list's Make Breakable...).</summary>
        public static FractureWindow ForEntity(Composite composite, Entity entity) => new FractureWindow(composite, entity, null);

        /// <summary>The window for a model alone (the Model Editor's Fracture into Debris...).</summary>
        public static FractureWindow ForModel(Models.CS2 model) => new FractureWindow(null, null, model);

        private static Level OpenLevel => Singleton.Editor?.CompositeBrowser?.Content?.Level;

        private (Composite composite, FunctionEntity entity) Target()
        {
            Level level = OpenLevel;
            Composite composite = level?.Commands?.GetComposite(_compositeId);
            return (composite, composite?.GetEntityByID(_entityId) as FunctionEntity);
        }

        /// <summary>The options as the window shows them.</summary>
        public BreakableOptions Options() => new BreakableOptions()
        {
            Pieces = (int)_pieces.Value,
            Seed = (int)_seed.Value,
            Interior = _interior.SelectedIndex == 1 ? FractureInterior.Projected : FractureInterior.Dark,
            InnerSkin = _skin.Checked,
            Mass = _autoMass.Checked ? (double?)null : (double)_mass.Value,
            KickSpeed = (double)_kick.Value,
            BreakOnDamage = _damage.Checked,
            DamageThreshold = (int)_threshold.Value,
            HitMonitor = _monitor.Checked,
            DespawnAfter = (double)_despawn.Value,
        };

        private void QueueRecut()
        {
            _recut.Stop();
            _recut.Start();
        }

        /// <summary>Cut it again with the options shown (nothing in the level changes) and show the pieces.</summary>
        public void Recut()
        {
            if (_busy || IsDisposed) return;
            Level level = OpenLevel;
            try
            {
                Cursor = Cursors.WaitCursor;
                if (_debrisOnly)
                {
                    _title.Text = "Fracture " + _model.Name;
                    _plan = BreakableBuilder.PlanCore(level, BreakableBuilder.RunOf(_model), null, _model.Name, Options());
                }
                else
                {
                    (Composite composite, FunctionEntity entity) = Target();
                    if (entity == null) { Say("That model is no longer in the level.", true); _make.Enabled = false; return; }
                    _title.Text = "Make " + level.Commands.Utils.GetEntityName(composite, entity) + " breakable (in " + composite.name + ")";
                    string why = BreakableBuilder.WhyNot(level, composite, entity);
                    if (why != null) { Say(why, true); _make.Enabled = false; return; }
                    _plan = BreakableBuilder.PlanCore(level, Brushwork.CollisionMatch.RunOf(entity), entity.GetResource(ResourceType.COLLISION_MAPPING, true)?.CollisionMapping,
                        level.Commands.Utils.GetEntityName(composite, entity), Options());
                }
                List<PiecePlan> p = _plan.Plans;
                _stats.Text = p.Count + " pieces from " + _plan.Input.Triangles.ToString("N0") + " triangles (" + p.Sum(o => o.Triangles).ToString("N0") + " with the cut faces), " +
                    p.Min(o => o.Mass).ToString("0.##") + " to " + p.Max(o => o.Mass).ToString("0.##") + " kg each; physics hulls of " + p.Min(o => o.SurfaceCorners) + " to " + p.Max(o => o.SurfaceCorners) + " corners. " +
                    (_debrisOnly ? "Pieces go in " + _plan.DebrisFolder + "." : "It becomes " + _plan.BreakablePath + ", its pieces " + _plan.DebrisFolder + ".");
                Say(_plan.Notes.Count != 0 ? string.Join(" ", _plan.Notes.Distinct()) : "Cut in " + _plan.Seconds.ToString("0.0") + " s. Nothing in the level changes until " + _make.Text + ".", false);
                _make.Enabled = true;
                DrawPreview();
            }
            catch (Exception e)
            {
                _plan = null;
                Say("It cannot be broken: " + e.Message, true);
                _make.Enabled = false;
                DrawPreview();
            }
            finally { Cursor = Cursors.Default; }
        }

        private void DrawPreview()
        {
            Image old = _preview.Image;
            _preview.Image = FracturePreview.Draw(_plan?.Pieces, _preview.Width, _preview.Height, _explode.Value / 100.0);
            old?.Dispose();
        }

        private void Say(string text, bool problem)
        {
            _status.Text = text;
            _status.ForeColor = problem ? Color.Firebrick : SystemColors.ControlText;
        }

        /// <summary>Make it (on a worker: each step runs on the editor's thread as the tools' do). Returns when done.</summary>
        public Task Make()
        {
            if (_busy || _plan == null) return Task.CompletedTask;
            _busy = true;
            foreach (Control c in new Control[] { _make, _pieces, _seed, _reseed, _interior, _skin }) c.Enabled = false;
            Say("Making it...", false);
            BreakableOptions options = Options();
            (Composite composite, FunctionEntity entity) = _debrisOnly ? (null, null) : Target();
            string compositeName = composite?.name, entityId = entity == null ? null : McpScript.Id(entity.shortGUID);
            Models.CS2 model = _model;
            BreakableHost host = new BreakableHost() { Call = null, LabelPrefix = "" };
            host.Progress = s => BeginInvoke(new Action(() => { if (!IsDisposed) Say("Making it: " + s + "...", false); }));
            return Task.Run(() =>
            {
                McpEditor.BeginCall(null);
                return _debrisOnly ? BreakableBuilder.MakeDebris(host, model, options, false) : BreakableBuilder.MakeBreakable(host, compositeName, entityId, options, false);
            }).ContinueWith(t =>
            {
                if (IsDisposed) return;
                _busy = false;
                if (t.Exception != null)
                {
                    Exception e = t.Exception.GetBaseException();
                    LastResult = new JObject() { ["error"] = e.Message };
                    Say("Not made: " + e.Message, true);
                    foreach (Control c in new Control[] { _make, _pieces, _seed, _reseed, _interior, _skin }) c.Enabled = true;
                    return;
                }
                LastResult = t.Result;
                int made = (t.Result["made"] as JArray)?.Count ?? 0;
                Say(_debrisOnly
                    ? "Made " + made + " debris pieces in " + _plan.DebrisFolder + ". Save with a build to put them in the game."
                    : "Made: " + _plan.BreakablePath + " with " + made + " pieces. Save with a build to put it in the game; Edit > Undo takes the script back (the models and physics stay, unused).", false);
                _make.Text = "Made";
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        #region Menus
        /// <summary>Add "Make Breakable..." to an entity list's context menu: shown for one ModelReference that can be broken.</summary>
        public static void AttachMenu(ContextMenuStrip menu, Func<Composite> composite, Func<List<Entity>> selected)
        {
            if (menu == null) return;
            ToolStripMenuItem item = new ToolStripMenuItem("Make Breakable...") { ToolTipText = "Break this model into physics debris when it is shot, hit or told to (Break pin)" };
            menu.Items.Add(item);
            menu.Opening += (s, e) =>
            {
                List<Entity> entities = selected?.Invoke() ?? new List<Entity>();
                Composite c = composite?.Invoke();
                Level level = OpenLevel;
                item.Visible = entities.Count == 1 && c != null && level != null && entities[0] is FunctionEntity f && f.function == FunctionType.ModelReference;
                item.Enabled = item.Visible && BreakableBuilder.WhyNot(level, c, entities[0]) == null;
            };
            item.Click += (s, e) =>
            {
                List<Entity> entities = selected?.Invoke() ?? new List<Entity>();
                Composite c = composite?.Invoke();
                if (entities.Count != 1 || c == null) return;
                ForEntity(c, entities[0]).Show();
            };
        }

        /// <summary>Add "Fracture into Debris..." to the Model Editor's tree: the whole model being edited.</summary>
        public static void AttachToModelEditor(TreeView tree, Func<Models.CS2> model)
        {
            if (tree == null) return;
            ContextMenuStrip menu = tree.ContextMenuStrip ?? new ContextMenuStrip();
            ToolStripMenuItem item = new ToolStripMenuItem("Fracture into Debris...") { ToolTipText = "Cut this model into physics debris pieces (spawnable assets)" };
            menu.Items.Add(item);
            menu.Opening += (s, e) => item.Visible = model?.Invoke() != null && OpenLevel != null;
            item.Click += (s, e) =>
            {
                Models.CS2 m = model?.Invoke();
                if (m == null) return;
                ForModel(m).Show();
            };
            tree.ContextMenuStrip = menu;
        }
        #endregion
    }
}
