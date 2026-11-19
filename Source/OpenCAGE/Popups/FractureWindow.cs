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
    public partial class FractureWindow : BaseWindow
    {
        private readonly ShortGuid _compositeId, _entityId;
        private readonly Models.CS2 _model;
        private readonly bool _debrisOnly;

        private BreakablePlan _plan;
        private bool _busy;

        /// <summary>The last result: what Make did, or the error.</summary>
        public JObject LastResult { get; private set; }

        private FractureWindow(Composite composite, Entity entity, Models.CS2 model) : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            _debrisOnly = model != null;
            _model = model;
            if (composite != null) _compositeId = composite.shortGUID;
            if (entity != null) _entityId = entity.shortGUID;
            InitializeComponent();
            Icon = SharedFormIcon.Icon;
            StayAboveEditor = true;
            EnableDocumentation("https://opencage.co.uk/docs/breakables/");

            //Picked before it is wired, so it does not queue a recut
            _interior.SelectedIndex = 0;
            _interior.SelectedIndexChanged += _option_Changed;

            if (_debrisOnly)
            {
                Text = "Fracture into Debris";
                _make.Text = "Make Debris Pieces";
                _intro.Text = "Each piece becomes a physics asset of its own (its model, collision and a one-body physics system, kicked outwards as it spawns). Spawn one with an AssetSpawner (allow_physics) whose asset is an is_template instance of it, placed at the piece's place relative to the whole model (a spawned asset appears where its template instance is).";
                //Only a placed model breaks when hit. The root grew to fit those rows, so it sizes again to what is left
                _root.Controls.Remove(_damageRow);
                _root.Controls.Remove(_monitorRow);
                _root.Size = Size.Empty;
            }
        }

        private void _option_Changed(object sender, EventArgs e) => QueueRecut();
        private void _damage_CheckedChanged(object sender, EventArgs e) => _threshold.Enabled = _damage.Checked;
        private void _reseed_Click(object sender, EventArgs e) => _seed.Value = (_seed.Value * 7919 + 101) % 99999;
        private void _explode_ValueChanged(object sender, EventArgs e) => DrawPreview();
        private void _make_Click(object sender, EventArgs e) => Make();
        private void _close_Click(object sender, EventArgs e) => Close();
        private void FractureWindow_Shown(object sender, EventArgs e) => Recut();

        private void _autoMass_CheckedChanged(object sender, EventArgs e)
        {
            _mass.Enabled = !_autoMass.Checked;
            QueueRecut();
        }

        private void _recut_Tick(object sender, EventArgs e)
        {
            _recut.Stop();
            Recut();
        }

        private void FractureWindow_FormClosed(object sender, FormClosedEventArgs e)
        {
            _recut.Stop();
            _recut.Dispose();
            _preview.Image?.Dispose();
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
