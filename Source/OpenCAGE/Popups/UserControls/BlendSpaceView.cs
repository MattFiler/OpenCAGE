using CATHODE;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace OpenCAGE.Popups.UserControls
{
    /// <summary>
    /// A blend set drawn as what it is: clips scattered across one or two parameters, and the grid
    /// of samples the game reads at runtime.
    ///
    /// Hovering a grid point shows which clips the game would mix there and in what proportion,
    /// which is the only way to see what a blend set actually does without playing it.
    /// </summary>
    public class BlendSpaceView : Control
    {
        /// <summary>Raised when a different instance is clicked, with its index or -1.</summary>
        public event EventHandler<int> InstanceSelected;

        private GlobalAnimClipDB.BlendSet _set;
        private int _selected = -1;
        private int _hoveredVertex = -1;
        private RectangleF _plot;
        private float _minX, _maxX, _minY, _maxY;

        private const int Margin = 42;
        private const int InstanceRadius = 6;

        public BlendSpaceView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.FromArgb(30, 30, 34);
        }

        public GlobalAnimClipDB.BlendSet Set
        {
            get { return _set; }
            set { _set = value; _selected = -1; _hoveredVertex = -1; Measure(); Invalidate(); }
        }

        /// <summary>The instance the user last clicked, or -1.</summary>
        public int SelectedInstance
        {
            get { return _selected; }
            set { if (_selected == value) return; _selected = value; Invalidate(); }
        }

        /// <summary>Names to label the instances with, indexed by instance. Optional.</summary>
        public Func<int, string> LabelFor { get; set; }

        #region LAYOUT
        /* The instances rarely fill the sampled grid, and the grid rarely covers the instances, so
         * the view has to hold both or one of them ends up off the edge. */
        private void Measure()
        {
            _minX = _minY = float.MaxValue;
            _maxX = _maxY = float.MinValue;
            if (_set == null) return;

            for (int i = 0; i < InstanceCount; i++)
            {
                Include(PropertyOf(i, 0), PropertyOf(i, 1));
            }
            Include(_set.CellOrigin.X, _set.CellOrigin.Y);
            Include(_set.CellOrigin.X + (_set.XCellCount * _set.CellUnit.X),
                    _set.CellOrigin.Y + (_set.YCellCount * _set.CellUnit.Y));

            if (_minX > _maxX) { _minX = 0; _maxX = 1; }
            if (_minY > _maxY) { _minY = 0; _maxY = 1; }

            //a blend on one parameter has no height of its own, so give it some to draw into
            if (_maxX - _minX < 1e-6f) { _minX -= 0.5f; _maxX += 0.5f; }
            if (_maxY - _minY < 1e-6f) { _minY -= 0.5f; _maxY += 0.5f; }

            float padX = (_maxX - _minX) * 0.08f, padY = (_maxY - _minY) * 0.08f;
            _minX -= padX; _maxX += padX; _minY -= padY; _maxY += padY;
        }

        private void Include(float x, float y)
        {
            if (x < _minX) _minX = x;
            if (x > _maxX) _maxX = x;
            if (y < _minY) _minY = y;
            if (y > _maxY) _maxY = y;
        }

        private int InstanceCount { get { return _set?.PlaySpeeds?.Length ?? 0; } }

        private float PropertyOf(int instance, int axis)
        {
            if (_set == null || axis >= _set.Dimensions) return 0;
            int at = (instance * _set.Dimensions) + axis;
            return at >= 0 && at < _set.InstanceProperties.Length ? _set.InstanceProperties[at] : 0;
        }

        private PointF ToScreen(float x, float y)
        {
            return new PointF(
                _plot.Left + ((x - _minX) / (_maxX - _minX) * _plot.Width),
                _plot.Bottom - ((y - _minY) / (_maxY - _minY) * _plot.Height));
        }
        #endregion

        #region PAINT
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            _plot = new RectangleF(Margin, 12, Math.Max(10, Width - Margin - 16), Math.Max(10, Height - Margin - 12));

            if (_set == null)
            {
                TextRenderer.DrawText(e.Graphics, "Choose a blend set.", Font, ClientRectangle,
                    Color.FromArgb(140, 140, 150), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            if (IsDegenerate)
            {
                TextRenderer.DrawText(e.Graphics,
                    "Every blend point in '" + _set.Name + "' sits at the same spot and the grid has no size,\n"
                    + "so there is nothing to plot. The clips are still listed below.",
                    Font, ClientRectangle, Color.FromArgb(150, 150, 160),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            DrawGrid(e.Graphics);
            DrawAxes(e.Graphics);
            DrawInstances(e.Graphics);
            DrawHover(e.Graphics);
        }

        /* The sampled grid. Every point on it is a baked mix of clips, which is what the game reads -
         * the instances themselves are only ever consulted through it. */
        private void DrawGrid(Graphics g)
        {
            int xCells = Math.Max(0, _set.XCellCount), yCells = Math.Max(0, _set.YCellCount);
            if (xCells == 0 && yCells == 0) return;

            using (Pen pen = new Pen(Color.FromArgb(52, 52, 60)))
            {
                for (int x = 0; x <= xCells; x++)
                {
                    float at = _set.CellOrigin.X + (x * _set.CellUnit.X);
                    PointF top = ToScreen(at, _set.CellOrigin.Y + (yCells * _set.CellUnit.Y));
                    PointF bottom = ToScreen(at, _set.CellOrigin.Y);
                    g.DrawLine(pen, top, bottom);
                }
                for (int y = 0; y <= yCells; y++)
                {
                    float at = _set.CellOrigin.Y + (y * _set.CellUnit.Y);
                    PointF left = ToScreen(_set.CellOrigin.X, at);
                    PointF right = ToScreen(_set.CellOrigin.X + (xCells * _set.CellUnit.X), at);
                    g.DrawLine(pen, left, right);
                }
            }

            //each grid point tinted by how many clips meet there, so the blended regions stand out
            for (int v = 0; v < _set.Vertices.Count; v++)
            {
                PointF at = VertexPosition(v);
                int mixing = Mixing(v);
                Color colour = mixing >= 3 ? Color.FromArgb(200, 120, 200, 240)
                             : mixing == 2 ? Color.FromArgb(170, 90, 160, 200)
                             : Color.FromArgb(120, 70, 110, 140);
                using (SolidBrush brush = new SolidBrush(colour))
                    g.FillRectangle(brush, at.X - 1.5f, at.Y - 1.5f, 3, 3);
            }
        }

        private PointF VertexPosition(int vertex)
        {
            int xVerts = Math.Max(1, _set.XCellCount + 1);
            int x = vertex % xVerts;
            int y = _set.Dimensions > 1 ? (vertex / xVerts) % Math.Max(1, _set.YCellCount + 1) : 0;
            return ToScreen(_set.CellOrigin.X + (x * _set.CellUnit.X), _set.CellOrigin.Y + (y * _set.CellUnit.Y));
        }

        private int Mixing(int vertex)
        {
            GlobalAnimClipDB.BlendVertex baked = _set.Vertices[vertex];
            int mixing = 0;
            for (int i = 0; i < baked.Weights.Length; i++)
                if (baked.Weights[i] != 0 && baked.Instances[i] != 255) mixing++;
            return mixing;
        }

        private void DrawAxes(Graphics g)
        {
            using (Pen pen = new Pen(Color.FromArgb(90, 90, 100)))
            {
                g.DrawLine(pen, _plot.Left, _plot.Bottom, _plot.Right, _plot.Bottom);
                g.DrawLine(pen, _plot.Left, _plot.Top, _plot.Left, _plot.Bottom);
            }

            Color labelColour = Color.FromArgb(170, 170, 180);
            TextRenderer.DrawText(g, Short(_set.BlendPropertyX), Font,
                new Rectangle((int)_plot.Left, (int)_plot.Bottom + 14, (int)_plot.Width, 16), labelColour,
                TextFormatFlags.HorizontalCenter);

            if (_set.Dimensions > 1)
            {
                //the Y label reads up the side, which is the only way it fits
                using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center })
                {
                    GraphicsState state = g.Save();
                    g.TranslateTransform(14, _plot.Top + (_plot.Height / 2));
                    g.RotateTransform(-90);
                    using (SolidBrush brush = new SolidBrush(labelColour))
                        g.DrawString(Short(_set.BlendPropertyY), Font, brush, 0, 0, format);
                    g.Restore(state);
                }
            }

            using (SolidBrush brush = new SolidBrush(Color.FromArgb(120, 120, 130)))
            {
                g.DrawString(_minX.ToString("0.##"), Font, brush, _plot.Left, _plot.Bottom + 1);
                g.DrawString(_maxX.ToString("0.##"), Font, brush, _plot.Right - 28, _plot.Bottom + 1);
                if (_set.Dimensions > 1)
                {
                    g.DrawString(_maxY.ToString("0.##"), Font, brush, _plot.Left - 36, _plot.Top - 2);
                    g.DrawString(_minY.ToString("0.##"), Font, brush, _plot.Left - 36, _plot.Bottom - 14);
                }
            }
        }

        private static string Short(string property)
        {
            if (string.IsNullOrEmpty(property)) return "(none)";
            return property.Length <= 40 ? property : property.Substring(0, 38) + "..";
        }

        private void DrawInstances(Graphics g)
        {
            for (int i = 0; i < InstanceCount; i++)
            {
                PointF at = ToScreen(PropertyOf(i, 0), PropertyOf(i, 1));
                bool selected = i == _selected;

                using (SolidBrush brush = new SolidBrush(selected ? Color.FromArgb(255, 190, 90) : Color.FromArgb(120, 190, 255)))
                    g.FillEllipse(brush, at.X - InstanceRadius, at.Y - InstanceRadius, InstanceRadius * 2, InstanceRadius * 2);
                using (Pen pen = new Pen(selected ? Color.FromArgb(255, 230, 180) : Color.FromArgb(40, 60, 80), selected ? 2f : 1f))
                    g.DrawEllipse(pen, at.X - InstanceRadius, at.Y - InstanceRadius, InstanceRadius * 2, InstanceRadius * 2);
            }

            /* Labels last and only where they fit. A dense blend space has clip names several times
             * longer than the gap between its points, and drawing them all turns the picture into a
             * smear - the selected one always gets said, the rest take what room is left. */
            List<Rectangle> taken = new List<Rectangle>();
            foreach (int i in Order())
            {
                string label = LabelFor == null ? i.ToString() : LabelFor(i);
                if (string.IsNullOrEmpty(label)) continue;
                if (label.Length > 44) label = label.Substring(0, 42) + "..";

                PointF at = ToScreen(PropertyOf(i, 0), PropertyOf(i, 1));
                Size size = TextRenderer.MeasureText(g, label, Font);
                Rectangle box = new Rectangle((int)at.X + InstanceRadius + 2, (int)at.Y - 8, size.Width, size.Height);

                bool selected = i == _selected;
                if (!selected && taken.Any(x => x.IntersectsWith(box))) continue;
                taken.Add(box);

                TextRenderer.DrawText(g, label, Font, box.Location,
                    selected ? Color.FromArgb(255, 220, 160) : Color.FromArgb(190, 190, 200));
            }
        }

        //the selected instance is labelled first so it always wins the space it needs
        private IEnumerable<int> Order()
        {
            if (_selected >= 0 && _selected < InstanceCount) yield return _selected;
            for (int i = 0; i < InstanceCount; i++) if (i != _selected) yield return i;
        }

        /// <summary>
        /// Whether the blend space has any extent at all. A few of the shipped sets have every point
        /// on top of every other with a zero sized grid and no weights - unfinished data, and there
        /// is nothing to draw for one.
        /// </summary>
        private bool IsDegenerate
        {
            get
            {
                if (_set == null) return true;
                if (_set.CellUnit.X != 0 || _set.CellUnit.Y != 0) return false;

                for (int i = 1; i < InstanceCount; i++)
                    if (PropertyOf(i, 0) != PropertyOf(0, 0) || PropertyOf(i, 1) != PropertyOf(0, 1)) return false;
                return true;
            }
        }

        private void DrawHover(Graphics g)
        {
            if (_hoveredVertex < 0 || _hoveredVertex >= _set.Vertices.Count) return;

            PointF at = VertexPosition(_hoveredVertex);
            using (Pen pen = new Pen(Color.FromArgb(255, 190, 90), 1.5f))
                g.DrawEllipse(pen, at.X - 4, at.Y - 4, 8, 8);

            List<string> lines = new List<string>();
            GlobalAnimClipDB.BlendVertex baked = _set.Vertices[_hoveredVertex];
            int total = baked.Weights.Sum(x => (int)x);
            for (int i = 0; i < baked.Instances.Length; i++)
            {
                if (baked.Instances[i] == 255 || baked.Weights[i] == 0) continue;
                string name = LabelFor == null ? "instance " + baked.Instances[i] : LabelFor(baked.Instances[i]);
                lines.Add((total == 0 ? 0 : baked.Weights[i] * 100 / total) + "%  " + name);
            }
            if (lines.Count == 0) lines.Add("nothing plays here");

            Size size = TextRenderer.MeasureText(string.Join("\n", lines), Font);
            RectangleF box = new RectangleF(at.X + 10, at.Y + 10, size.Width + 12, size.Height + 8);
            if (box.Right > Width) box.X = at.X - box.Width - 10;
            if (box.Bottom > Height) box.Y = at.Y - box.Height - 10;

            using (SolidBrush brush = new SolidBrush(Color.FromArgb(235, 20, 20, 24)))
                g.FillRectangle(brush, box);
            using (Pen pen = new Pen(Color.FromArgb(90, 90, 100)))
                g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);

            TextRenderer.DrawText(g, string.Join("\n", lines), Font,
                new Rectangle((int)box.X + 6, (int)box.Y + 4, (int)box.Width, (int)box.Height),
                Color.FromArgb(220, 220, 230));
        }
        #endregion

        #region INPUT
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_set == null) return;

            int nearest = NearestVertex(e.Location);
            if (nearest == _hoveredVertex) return;
            _hoveredVertex = nearest;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredVertex < 0) return;
            _hoveredVertex = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_set == null) return;

            int hit = -1;
            for (int i = 0; i < InstanceCount; i++)
            {
                PointF at = ToScreen(PropertyOf(i, 0), PropertyOf(i, 1));
                float dx = at.X - e.X, dy = at.Y - e.Y;
                if (((dx * dx) + (dy * dy)) <= (InstanceRadius + 3) * (InstanceRadius + 3)) { hit = i; break; }
            }
            if (hit == _selected) return;

            _selected = hit;
            Invalidate();
            InstanceSelected?.Invoke(this, hit);
        }

        private int NearestVertex(Point mouse)
        {
            int nearest = -1;
            float best = 14 * 14;
            for (int v = 0; v < _set.Vertices.Count; v++)
            {
                PointF at = VertexPosition(v);
                float dx = at.X - mouse.X, dy = at.Y - mouse.Y;
                float distance = (dx * dx) + (dy * dy);
                if (distance >= best) continue;
                best = distance;
                nearest = v;
            }
            return nearest;
        }
        #endregion
    }
}
