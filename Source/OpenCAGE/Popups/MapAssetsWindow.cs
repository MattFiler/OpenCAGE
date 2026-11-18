using OpenCAGE.MapAssets;
using OpenCAGE.Popups.Base;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.Popups
{
    /// <summary>
    /// File > Generate Map Assets: the level's Tab map from its walk mesh (<see cref="MapAssetGenerator"/>). Find Floors proposes the
    /// floors; the user names them and moves their heights; Preview draws each as the game will; Generate writes the map's movies into
    /// the level's UI folder and sets the anchor, floor boxes and map items' floors as one undo step.
    /// </summary>
    internal class MapAssetsWindow : BaseWindow
    {
        private static MapAssetsWindow _open;

        private readonly TextBox _summary;
        private readonly DataGridView _floors;
        private readonly NumericUpDown _scale;
        private readonly TextBox _mapName;
        private readonly ComboBox _previewFloor;
        private readonly PictureBox _preview;
        private readonly Label _status;
        private readonly Button _find, _add, _remove, _show, _generate;
        private MapAssetsPlan _plan;

        public MapAssetsWindow() : base(WindowClosesOn.COMMANDS_RELOAD)
        {
            Text = "Generate Map Assets";
            Icon = SharedFormIcon.Icon;
            Size = new Size(980, 720);
            MinimumSize = new Size(820, 560);
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            StayAboveEditor = true;
            BaseWindow.EnableDocumentation(this, "https://opencage.co.uk/docs/map-assets/");

            _summary = new TextBox()
            {
                Dock = DockStyle.Top, Height = 70, Multiline = true, ReadOnly = true, WordWrap = true, ScrollBars = ScrollBars.Vertical,
                Text = "Makes the level's Tab map from its walk mesh (as last built): each floor is drawn in the game's map style, the level's own hub and map page and a movie per floor go in the level's UI folder, " +
                       "and a MapAnchor, floor boxes per floor and every map item's floor are set in the root composite (one undo step). The game only opens maps for keyframes on its own list, so the level " +
                       "BORROWS the game's (RnD_HzdLab_1, RnD_HzdLab_2, And_Lab_0...): only this level shows its map under them. Launch the level through OpenCAGE to see it.",
            };

            Panel left = new Panel() { Dock = DockStyle.Left, Width = 430, Padding = new Padding(8) };
            _floors = new DataGridView()
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, MultiSelect = false,
            };
            _floors.Columns.Add(new DataGridViewTextBoxColumn() { Name = "name", HeaderText = "Floor name", FillWeight = 40 });
            _floors.Columns.Add(new DataGridViewTextBoxColumn() { Name = "from", HeaderText = "Above (m)", FillWeight = 20 });
            _floors.Columns.Add(new DataGridViewTextBoxColumn() { Name = "to", HeaderText = "Up to (m)", FillWeight = 20 });
            _floors.Columns.Add(new DataGridViewTextBoxColumn() { Name = "keyframe", HeaderText = "Borrows", FillWeight = 26, ReadOnly = true });
            _floors.CellEndEdit += (s, e) => { _plan = null; Renumber(); };
            Panel floorButtons = new Panel() { Dock = DockStyle.Bottom, Height = 36 };
            _find = new Button() { Text = "Find Floors", Width = 100, Height = 28, Location = new Point(0, 4) };
            _find.Click += (s, e) => FindFloors();
            _add = new Button() { Text = "Add Floor", Width = 90, Height = 28, Location = new Point(106, 4) };
            _add.Click += (s, e) => { if (_floors.Rows.Count < MapPlanner.MaxFloors) { _floors.Rows.Add("LEVEL " + (_floors.Rows.Count + 1), "", "", ""); _plan = null; Renumber(); } };
            _remove = new Button() { Text = "Remove Floor", Width = 100, Height = 28, Location = new Point(202, 4) };
            _remove.Click += (s, e) => { if (_floors.SelectedRows.Count != 0) { _floors.Rows.Remove(_floors.SelectedRows[0]); _plan = null; Renumber(); } };
            floorButtons.Controls.AddRange(new Control[] { _find, _add, _remove });
            Panel options = new Panel() { Dock = DockStyle.Bottom, Height = 66 };
            options.Controls.Add(new Label() { Text = "Map scale:", AutoSize = true, Location = new Point(0, 8) });
            _scale = new NumericUpDown() { Location = new Point(90, 4), Width = 70, Minimum = 0, Maximum = 10, DecimalPlaces = 2, Increment = 0.05m, Value = 0 };
            options.Controls.Add(_scale);
            options.Controls.Add(new Label() { Text = "(0: chosen to fit)", AutoSize = true, Location = new Point(166, 8) });
            options.Controls.Add(new Label() { Text = "Map name:", AutoSize = true, Location = new Point(0, 40) });
            _mapName = new TextBox() { Location = new Point(90, 36), Width = 300 };
            options.Controls.Add(_mapName);
            _scale.ValueChanged += (s, e) => _plan = null;
            _mapName.TextChanged += (s, e) => _plan = null;
            left.Controls.Add(_floors);
            left.Controls.Add(floorButtons);
            left.Controls.Add(options);

            Panel right = new Panel() { Dock = DockStyle.Fill, Padding = new Padding(8) };
            _preview = new PictureBox() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(4, 8, 9), SizeMode = PictureBoxSizeMode.Zoom };
            Panel previewBar = new Panel() { Dock = DockStyle.Top, Height = 34 };
            _show = new Button() { Text = "Preview", Width = 90, Height = 28, Location = new Point(0, 2) };
            _show.Click += (s, e) => Preview();
            _previewFloor = new ComboBox() { Location = new Point(98, 5), Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
            _previewFloor.SelectedIndexChanged += (s, e) => DrawPreview();
            previewBar.Controls.AddRange(new Control[] { _show, _previewFloor });
            right.Controls.Add(_preview);
            right.Controls.Add(previewBar);

            Panel bottom = new Panel() { Dock = DockStyle.Bottom, Height = 76, Padding = new Padding(8) };
            _status = new Label() { AutoSize = false, Location = new Point(8, 4), Height = 34, Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right };
            _generate = new Button() { Text = "Generate", Width = 120, Height = 28, Anchor = AnchorStyles.Right | AnchorStyles.Bottom };
            _generate.Click += (s, e) => Generate();
            bottom.Controls.AddRange(new Control[] { _status, _generate });
            bottom.Resize += (s, e) => { _status.Width = bottom.ClientSize.Width - 16; _generate.Location = new Point(bottom.ClientSize.Width - _generate.Width - 8, bottom.ClientSize.Height - 34); };

            Controls.Add(right);
            Controls.Add(left);
            Controls.Add(_summary);
            Controls.Add(bottom);
            Theming.ThemeManager.ApplyCurrent(this);
            _status.Text = "Find Floors reads the walk mesh; rename the floors and move their heights, Preview, then Generate.";
        }

        #region Opening it
        public static MapAssetsWindow Open()
        {
            if (Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands == null)
                return null;
            if (_open != null && !_open.IsDisposed) { _open.Activate(); return _open; }
            _open = new MapAssetsWindow();
            _open.FormClosed += (s, e) => { if (_open == s) _open = null; };
            if (Singleton.Editor != null) _open.Show(Singleton.Editor); else _open.Show();
            _open.FindFloors();
            return _open;
        }

        /// <summary>File > Generate Map Assets, after the Calculate Alien Vents item (or after <paramref name="after"/>).</summary>
        public static void AttachMenu(ToolStripMenuItem after)
        {
            ToolStrip owner = after?.Owner;
            if (owner == null) return;
            ToolStripDropDownItem menu = after.OwnerItem as ToolStripDropDownItem;
            ToolStripMenuItem item = new ToolStripMenuItem("Generate Map Assets") { Name = "mapAssetsToolStripMenuItem", ToolTipText = "Make the level's Tab map from its walk mesh: floor plans in the game's map style, the map's anchor and floors, every map item on its floor" };
            item.Click += (s, e) => Open();
            int at = owner.Items.IndexOf(after) + 1;
            ToolStripItem check = owner.Items.Cast<ToolStripItem>().FirstOrDefault(o => o.Name == "alienVentsToolStripMenuItem") ?? owner.Items.Cast<ToolStripItem>().FirstOrDefault(o => o.Name == "levelCheckToolStripMenuItem");
            if (check != null) at = owner.Items.IndexOf(check) + 1;
            owner.Items.Insert(at, item);
            if (menu != null)
                menu.DropDownOpening += (s, e) => item.Enabled = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands != null;
        }
        #endregion

        private LevelContent Content => Singleton.Editor?.CompositeBrowser?.Content;

        private static string M(float v) => float.IsInfinity(v) ? "" : v.ToString("0.##", CultureInfo.InvariantCulture);

        private void Renumber()
        {
            for (int i = 0; i < _floors.Rows.Count; i++)
                _floors.Rows[i].Cells["keyframe"].Value = i < MapPlanner.BorrowedKeyframes.Length ? MapPlanner.BorrowedKeyframes[i] : "(too many)";
        }

        private void FindFloors()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                MapAssetsPlan plan = MapAssetGenerator.Propose(Content, new MapAssetsOptions() { Scale = (float)_scale.Value, MapName = _mapName.Text });
                _floors.Rows.Clear();
                foreach (MapFloor f in plan.Floors)
                    _floors.Rows.Add(f.Name, M(f.From), M(f.To), f.Keyframe);
                Renumber();
                _plan = plan;
                _status.Text = plan.Floors.Count + " floor(s) found over " + plan.Walkable.Count + " walkable polygons. " + string.Join(" ", plan.Notes);
                FillPreviewList();
            }
            catch (Exception e) { _status.Text = "Could not read the level: " + e.Message; }
            finally { Cursor = Cursors.Default; }
        }

        /* The floors as the grid has them (heights left blank: the floor takes everything below / above) */
        private MapAssetsOptions Options()
        {
            MapAssetsOptions options = new MapAssetsOptions() { Scale = (float)_scale.Value, MapName = _mapName.Text, Floors = new List<MapFloor>() };
            foreach (DataGridViewRow row in _floors.Rows)
            {
                float from = Parse(row.Cells["from"].Value, float.NegativeInfinity), to = Parse(row.Cells["to"].Value, float.PositiveInfinity);
                options.Floors.Add(new MapFloor()
                {
                    Name = Convert.ToString(row.Cells["name"].Value),
                    From = from, To = to,
                    Level = float.IsInfinity(from) ? (float.IsInfinity(to) ? 0 : to - 1) : (float.IsInfinity(to) ? from + 1 : (from + to) / 2),
                });
            }
            return options;
        }

        private static float Parse(object v, float blank)
        {
            string s = Convert.ToString(v);
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) ? f : blank;
        }

        private bool MakePlan()
        {
            if (_plan != null) return true;
            try
            {
                Cursor = Cursors.WaitCursor;
                _plan = MapAssetGenerator.Propose(Content, Options());
                FillPreviewList();
                return true;
            }
            catch (Exception e) { _status.Text = "Could not work out the map: " + e.Message; return false; }
            finally { Cursor = Cursors.Default; }
        }

        private void FillPreviewList()
        {
            int keep = Math.Max(0, _previewFloor.SelectedIndex);
            _previewFloor.Items.Clear();
            if (_plan == null) return;
            foreach (MapFloor f in _plan.Floors) _previewFloor.Items.Add(f.Name + " (" + f.Keyframe + ")");
            if (_previewFloor.Items.Count != 0) _previewFloor.SelectedIndex = Math.Min(keep, _previewFloor.Items.Count - 1);
        }

        private void Preview()
        {
            if (MakePlan()) DrawPreview();
        }

        /* The floor as the map page shows it, unzoomed: the map screen's 960 x 540 frame and what lies round it */
        private void DrawPreview()
        {
            if (_plan == null || _previewFloor.SelectedIndex < 0 || _previewFloor.SelectedIndex >= _plan.Drawn.Count) return;
            MapPlanFloor floor = _plan.Drawn[_previewFloor.SelectedIndex];
            List<PointF> all = floor.Outlines.SelectMany(o => o.Points.Select(p => MapTransform.ToMovie(_plan.Anchor, p.X, p.Y))).ToList();
            if (all.Count == 0) { _preview.Image = null; return; }
            float minX = Math.Min(0, all.Min(p => p.X)) - 20, minY = Math.Min(0, all.Min(p => p.Y)) - 20, maxX = Math.Max(960, all.Max(p => p.X)) + 20, maxY = Math.Max(540, all.Max(p => p.Y)) + 20;
            float k = Math.Min(2400f / (maxX - minX), 2400f / (maxY - minY));
            Bitmap bmp = new Bitmap(Math.Max(1, (int)((maxX - minX) * k)), Math.Max(1, (int)((maxY - minY) * k)));
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(4, 8, 9));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Func<PointF, PointF> P = m => new PointF((m.X - minX) * k, (m.Y - minY) * k);
                using (Pen frame = new Pen(Color.FromArgb(70, 255, 255, 255)))
                    g.DrawRectangle(frame, -minX * k, -minY * k, 960 * k, 540 * k);
                foreach (IGrouping<MapLayerStyle, MapOutline> layer in floor.Outlines.GroupBy(o => o.Style))
                    using (GraphicsPath path = new GraphicsPath(FillMode.Alternate))
                    {
                        foreach (MapOutline o in layer) path.AddPolygon(o.Points.Select(p => P(MapTransform.ToMovie(_plan.Anchor, p.X, p.Y))).ToArray());
                        using (SolidBrush b = new SolidBrush(Color.FromArgb(layer.Key.FillAlpha, MapMovie.Tint))) g.FillPath(b, path);
                        using (Pen p = new Pen(Color.FromArgb(layer.Key.LineAlpha, MapMovie.Tint), 1f)) g.DrawPath(p, path);
                    }
            }
            Image old = _preview.Image;
            _preview.Image = bmp;
            old?.Dispose();
            _status.Text = floor.Floor.Name + ": " + floor.Outlines.Count + " outlines, " + Math.Round(floor.Floor.Area) + " m² walkable. The white frame is one map screen at map scale " + _plan.Anchor.Scale + ".";
        }

        private void Generate()
        {
            LevelContent content = Content;
            if (content?.Level == null || !MakePlan()) return;
            try
            {
                Cursor = Cursors.WaitCursor;
                List<string> done = MapAssetGenerator.Apply(content, _plan, "");
                _status.Text = string.Join(" ", done.Skip(Math.Max(0, done.Count - 3)));
                _plan = null;
            }
            catch (Exception e) { _status.Text = "Not generated: " + e.Message; }
            finally { Cursor = Cursors.Default; }
        }
    }
}
