using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using OpenCAGE.MCP;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Vector3 = System.Numerics.Vector3;

namespace OpenCAGE
{
    /// <summary>
    /// "Rotate Continuously...": turns a prop, light or composite instance about one of its own axes at a set speed for as long as
    /// the level runs, with no keyframes. Apply is one undo step (Spinners: a looping RotateAtSpeed wired as retail's).
    /// </summary>
    public class SpinDialog : BaseWindow
    {
        private readonly ShortGuid _compositeId, _entityId;
        private readonly RadioButton _x = new RadioButton() { Text = "X", AutoSize = true };
        private readonly RadioButton _y = new RadioButton() { Text = "Y", AutoSize = true };
        private readonly RadioButton _z = new RadioButton() { Text = "Z", AutoSize = true };
        private readonly NumericUpDown _speed = new NumericUpDown() { Minimum = -3600, Maximum = 3600, DecimalPlaces = 1, Increment = 10, Width = 90, Value = 90 };
        private readonly Label _title = new Label() { AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
        private readonly Label _suggest = new Label() { AutoSize = true, MaximumSize = new Size(420, 0) };
        private readonly Label _period = new Label() { AutoSize = true };
        private readonly Label _status = new Label() { AutoSize = true, MaximumSize = new Size(420, 0), ForeColor = Color.Firebrick };
        private readonly Panel _preview = new DoubleBufferedPanel() { Width = 420, Height = 220, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(32, 34, 38) };
        private readonly Button _apply = new Button() { Text = "Apply", AutoSize = true };
        private readonly Button _remove = new Button() { Text = "Stop Turning", AutoSize = true };
        private readonly Button _close = new Button() { Text = "Close", AutoSize = true };
        private (Vector3 Min, Vector3 Max)? _bounds;
        private bool _light;

        private sealed class DoubleBufferedPanel : Panel
        {
            public DoubleBufferedPanel() { DoubleBuffered = true; ResizeRedraw = true; }
        }

        public SpinDialog(Composite composite, Entity entity) : base(WindowClosesOn.COMMANDS_RELOAD | WindowClosesOn.NEW_COMPOSITE_SELECTION)
        {
            _compositeId = composite.shortGUID;
            _entityId = entity.shortGUID;
            Text = "Rotate Continuously";
            Icon = SharedFormIcon.Icon;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.CenterParent;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            StayAboveEditor = true;
            EnableDocumentation("https://opencage.co.uk/docs/rotate-continuously/");

            FlowLayoutPanel root = new FlowLayoutPanel() { FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(10), WrapContents = false };
            root.Controls.Add(_title);
            root.Controls.Add(new Label() { AutoSize = true, MaximumSize = new Size(420, 0), Text = "It turns about one of its OWN axes for as long as the level runs (its rotation in the level sets where those point). Negative speeds turn it the other way." });
            FlowLayoutPanel axisRow = new FlowLayoutPanel() { AutoSize = true, WrapContents = false };
            axisRow.Controls.Add(new Label() { Text = "Axis:", AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
            axisRow.Controls.AddRange(new Control[] { _x, _y, _z });
            axisRow.Controls.Add(new Label() { Text = "  Degrees per second:", AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
            axisRow.Controls.Add(_speed);
            axisRow.Controls.Add(_period);
            root.Controls.Add(axisRow);
            root.Controls.Add(_suggest);
            root.Controls.Add(_preview);
            root.Controls.Add(_status);
            FlowLayoutPanel buttons = new FlowLayoutPanel() { AutoSize = true, WrapContents = false };
            buttons.Controls.AddRange(new Control[] { _apply, _remove, _close });
            root.Controls.Add(buttons);
            Controls.Add(root);

            foreach (RadioButton radio in new[] { _x, _y, _z }) radio.CheckedChanged += (s, e) => { UpdatePeriod(); _preview.Invalidate(); };
            _speed.ValueChanged += (s, e) => { UpdatePeriod(); _preview.Invalidate(); };
            _preview.Paint += PaintPreview;
            _apply.Click += (s, e) => ApplySpin();
            _remove.Click += (s, e) => RemoveSpin();
            _close.Click += (s, e) => Close();
            Load(composite, entity);
        }

        private Composite Composite => Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands?.GetComposite(_compositeId);
        private Entity Entity => Composite?.GetEntityByID(_entityId);
        private Spinners.Axis ChosenAxis => _x.Checked ? Spinners.Axis.X : _z.Checked ? Spinners.Axis.Z : Spinners.Axis.Y;

        private void Load(Composite composite, Entity entity)
        {
            Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
            string name = commands?.Utils.GetEntityName(composite, entity) ?? entity.shortGUID.ToByteString();
            _title.Text = "Turn '" + name + "' continuously";
            _light = Spinners.IsLight(entity);
            _bounds = Spinners.ModelBounds(entity);
            Spinners.Axis suggested = Spinners.SuggestAxis(entity, out string why);
            _suggest.Text = "Suggested axis: " + suggested + " - " + why + ".";
            Spinners.Spinner existing = Spinners.Find(composite, entity);
            Spinners.Axis axis = existing?.Axis ?? suggested;
            _x.Checked = axis == Spinners.Axis.X;
            _y.Checked = axis == Spinners.Axis.Y;
            _z.Checked = axis == Spinners.Axis.Z;
            if (existing != null) _speed.Value = (decimal)Math.Max(-3600f, Math.Min(3600f, existing.DegreesPerSecond));
            _remove.Enabled = existing != null;
            _apply.Text = existing != null ? "Apply Change" : "Apply";
            string frozen = Spinners.WhyFrozen(commands, composite);
            bool can = Spinners.CanSpin(entity, out string cant);
            string now = existing == null ? "" : "It turns now: " + existing.DegreesPerSecond.ToString("0.#") + " deg/s about " + existing.Axis + " ('" + commands?.Utils.GetEntityName(composite, existing.RotateAtSpeed) + "').";
            _status.Text = !can ? cant : frozen ?? now;
            _status.ForeColor = !can || frozen != null ? Color.Firebrick : SystemColors.ControlText;
            _apply.Enabled = can && frozen == null;
            UpdatePeriod();
        }

        private void UpdatePeriod() => _period.Text = Spinners.Period((float)_speed.Value);

        private void ApplySpin()
        {
            Composite composite = Composite;
            Entity entity = Entity;
            if (composite == null || entity == null) { Close(); return; }
            try
            {
                string name = Singleton.Editor.CompositeBrowser.Content.Level.Commands.Utils.GetEntityName(composite, entity);
                McpScriptEdit.Run("Rotate " + name + " continuously", composite, edit => Spinners.Apply(edit, composite, entity, ChosenAxis, (float)_speed.Value),
                    new McpScriptEdit.RunOptions() { Show = false, LabelPrefix = "" });
                Load(composite, entity);
            }
            catch (Exception ex)
            {
                _status.ForeColor = Color.Firebrick;
                _status.Text = "Not applied: " + ex.Message;
            }
        }

        private void RemoveSpin()
        {
            Composite composite = Composite;
            Entity entity = Entity;
            if (composite == null || entity == null) { Close(); return; }
            try
            {
                string name = Singleton.Editor.CompositeBrowser.Content.Level.Commands.Utils.GetEntityName(composite, entity);
                McpScriptEdit.Run("Stop " + name + " turning", composite, edit => Spinners.Remove(edit, composite, entity),
                    new McpScriptEdit.RunOptions() { Show = false, LabelPrefix = "" });
                Load(composite, entity);
            }
            catch (Exception ex)
            {
                _status.ForeColor = Color.Firebrick;
                _status.Text = "Not removed: " + ex.Message;
            }
        }

        #region Preview
        //An oblique view of the entity's own frame: +X right, +Y up, +Z towards the viewer and down-left
        private static PointF Project(Vector3 p, PointF centre, float scale) =>
            new PointF(centre.X + (p.X - p.Z * 0.5f) * scale, centre.Y - (p.Y - p.Z * 0.35f) * scale);

        private void PaintPreview(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            PointF centre = new PointF(_preview.Width / 2f, _preview.Height / 2f + 10);
            Vector3 min, max;
            if (_bounds != null) { min = _bounds.Value.Min; max = _bounds.Value.Max; }
            else { min = new Vector3(-0.5f); max = new Vector3(0.5f); }
            Vector3 size = max - min;
            float extent = Math.Max(0.01f, Math.Max(size.X, Math.Max(size.Y, size.Z)));
            float scale = 120f / extent;
            Vector3 mid = (min + max) * 0.5f;
            Func<Vector3, PointF> at = p => Project(p - mid, centre, scale);

            using (Pen frame = new Pen(Color.FromArgb(70, 160, 160, 170), 1f))
            using (Pen box = new Pen(Color.FromArgb(200, 210, 210, 215), 1.5f))
            {
                //Its own axes, from its origin
                DrawAxis(g, at, Vector3.Zero, new Vector3(extent * 0.75f, 0, 0), Color.FromArgb(200, 220, 80, 80), "X");
                DrawAxis(g, at, Vector3.Zero, new Vector3(0, extent * 0.75f, 0), Color.FromArgb(200, 90, 200, 90), "Y");
                DrawAxis(g, at, Vector3.Zero, new Vector3(0, 0, extent * 0.75f), Color.FromArgb(200, 90, 140, 230), "Z");
                if (_light)
                {
                    //A spot's beam along its own +Z
                    Vector3 tip = new Vector3(0, 0, extent * 0.9f);
                    for (int i = 0; i < 4; i++)
                    {
                        double a = i * Math.PI / 2;
                        Vector3 rim = new Vector3((float)Math.Cos(a) * extent * 0.35f, (float)Math.Sin(a) * extent * 0.35f, extent * 0.9f);
                        g.DrawLine(frame, at(Vector3.Zero), at(rim));
                    }
                    g.DrawString("beam (+Z)", SystemFonts.DefaultFont, Brushes.Gainsboro, at(tip));
                }
                else
                {
                    Vector3[] c = new Vector3[8];
                    for (int i = 0; i < 8; i++) c[i] = new Vector3((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z);
                    int[,] edges = { { 0, 1 }, { 2, 3 }, { 4, 5 }, { 6, 7 }, { 0, 2 }, { 1, 3 }, { 4, 6 }, { 5, 7 }, { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 } };
                    for (int i = 0; i < 12; i++) g.DrawLine(box, at(c[edges[i, 0]]), at(c[edges[i, 1]]));
                    if (_bounds == null) g.DrawString("(no model: its own frame)", SystemFonts.DefaultFont, Brushes.Gainsboro, 6, 6);
                }
            }

            //The turn: the chosen axis through the middle of what it draws, and a ring round it with arrows the way a positive speed turns it
            Spinners.Axis axis = ChosenAxis;
            Vector3 dir = axis == Spinners.Axis.X ? Vector3.UnitX : axis == Spinners.Axis.Y ? Vector3.UnitY : Vector3.UnitZ;
            Vector3 u = axis == Spinners.Axis.X ? Vector3.UnitY : Vector3.UnitX;
            Vector3 v = Vector3.Cross(dir, u);
            Vector3 pivot = _light ? Vector3.Zero : new Vector3(mid.X * (axis == Spinners.Axis.X ? 1 : 0), mid.Y * (axis == Spinners.Axis.Y ? 1 : 0), mid.Z * (axis == Spinners.Axis.Z ? 1 : 0));
            using (Pen axisPen = new Pen(Color.Gold, 2.5f) { EndCap = LineCap.ArrowAnchor })
                g.DrawLine(axisPen, at(pivot - dir * extent * 0.7f), at(pivot + dir * extent * 0.85f));
            float radius = extent * 0.55f;
            float sign = Math.Sign((float)_speed.Value);
            if (sign == 0) return;
            List<PointF> ring = new List<PointF>();
            for (int i = 0; i <= 40; i++)
            {
                double a = sign * i * Math.PI * 1.7 / 40;
                ring.Add(at(pivot + (u * (float)Math.Cos(a) + v * (float)Math.Sin(a)) * radius));
            }
            using (Pen ringPen = new Pen(Color.Gold, 2f) { EndCap = LineCap.ArrowAnchor })
                g.DrawLines(ringPen, ring.ToArray());
            g.DrawString(Spinners.Period((float)_speed.Value), SystemFonts.DefaultFont, Brushes.Gold, 6, _preview.Height - 20);
        }

        private static void DrawAxis(Graphics g, Func<Vector3, PointF> at, Vector3 from, Vector3 to, Color colour, string label)
        {
            using (Pen pen = new Pen(colour, 1f) { DashStyle = DashStyle.Dash })
                g.DrawLine(pen, at(from), at(to));
            using (Brush brush = new SolidBrush(colour))
                g.DrawString(label, SystemFonts.DefaultFont, brush, at(to));
        }
        #endregion

        #region Menu
        /// <summary>
        /// Add "Rotate Continuously..." to an entity context menu: shown for one selected prop, light or composite instance, opening
        /// the dialog for it. <paramref name="composite"/> and <paramref name="selected"/> are read as the menu opens.
        /// </summary>
        public static void AttachMenu(ContextMenuStrip menu, Func<Composite> composite, Func<List<Entity>> selected)
        {
            if (menu == null) return;
            ToolStripMenuItem item = new ToolStripMenuItem("Rotate Continuously...") { ToolTipText = "Turn this prop, light or instance about one of its own axes for as long as the level runs (fans, rotors, beacons, gobo lights)" };
            menu.Items.Add(item);
            menu.Opening += (s, e) =>
            {
                List<Entity> entities = selected?.Invoke() ?? new List<Entity>();
                item.Visible = entities.Count == 1 && composite?.Invoke() != null && Spinners.CanSpin(entities[0], out _);
            };
            item.Click += (s, e) =>
            {
                List<Entity> entities = selected?.Invoke() ?? new List<Entity>();
                Composite c = composite?.Invoke();
                if (entities.Count != 1 || c == null) return;
                SpinDialog dialog = new SpinDialog(c, entities[0]);
                dialog.Show();
            };
        }
        #endregion
    }
}
