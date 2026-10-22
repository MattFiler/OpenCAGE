using ST.Library.UI.NodeEditor;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OpenCAGE
{
    /// <summary>A node's breakpoint, as the flowgraph draws it.</summary>
    public enum BreakpointMark : byte
    {
        None,
        /// <summary>Set, and the game stops on it (a red dot).</summary>
        Armed,
        /// <summary>Set here, but not (yet) stopping the game: not connected, or in no running instance (a red ring).</summary>
        Waiting,
        /// <summary>Set, and switched off in the game (a grey dot).</summary>
        Off,
    }

    /// <summary>What the script debugger draws on one node (see DebuggerNodeEditor).</summary>
    public struct NodeDebugMark
    {
        public static readonly NodeDebugMark None = new NodeDebugMark();

        public BreakpointMark Breakpoint;
        /// <summary>The scripts are held at this node (the call to it is the one held): it is outlined, with an arrow.</summary>
        public bool Held;
        /// <summary>Lines drawn in a strip above the node: its live state, then values (null or empty: none).</summary>
        public string[] States;

        public bool IsEmpty => Breakpoint == BreakpointMark.None && !Held && (States == null || States.Length == 0);
    }

    /// <summary>
    /// The flowgraph's node editor, with the script debugger's marks drawn over the nodes once the editor has drawn them: a
    /// red dot on the top-left corner of a node with a breakpoint, an outline and arrow on the node the scripts are held
    /// at, and a strip of the node's live state above it. The page says what each node shows (DebugMarkProvider); with no
    /// provider, or none to show, it draws as the plain editor.
    /// </summary>
    public class DebuggerNodeEditor : STNodeEditor
    {
        /// <summary>What a node shows (asked for every node on every paint while set: a lookup, nothing more).</summary>
        public Func<STNode, NodeDebugMark> DebugMarkProvider { get; set; }

        private static readonly Color BreakpointColour = Color.FromArgb(220, 45, 45);
        private static readonly Color BreakpointOffColour = Color.FromArgb(140, 140, 140);
        private static readonly Color HeldColour = Color.FromArgb(255, 200, 40);
        private static readonly Color StateBackColour = Color.FromArgb(215, 24, 28, 34);
        private static readonly Color StateTextColour = Color.FromArgb(230, 230, 230);

        //The state strip's font, made again when the zoom changes its size
        private Font _stateFont;
        private float _stateFontSize;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Func<STNode, NodeDebugMark> provider = DebugMarkProvider;
            if (provider == null || Nodes.Count == 0)
                return;

            Graphics g = e.Graphics;
            g.ResetTransform();
            SmoothingMode smoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.HighQuality;
            float scale = CanvasScale;
            Rectangle clip = e.ClipRectangle;
            clip.Inflate(40, 40);
            try
            {
                foreach (STNode node in Nodes)
                {
                    NodeDebugMark mark = provider(node);
                    if (mark.IsEmpty)
                        continue;
                    Rectangle area = CanvasToControl(node.Rectangle);
                    Rectangle reach = area;
                    reach.Inflate(24, 24);
                    if (mark.States != null && mark.States.Length != 0)
                        reach = Rectangle.Union(reach, new Rectangle(area.Left, area.Top - 200, Math.Max(area.Width, 300), 200));
                    if (!reach.IntersectsWith(clip))
                        continue;
                    if (mark.Held)
                        DrawHeld(g, area, scale);
                    if (mark.States != null && mark.States.Length != 0 && scale >= 0.45f)
                        DrawStates(g, area, scale, mark.States);
                    if (mark.Breakpoint != BreakpointMark.None)
                        DrawBreakpoint(g, area, scale, mark.Breakpoint);
                }
            }
            finally
            {
                g.SmoothingMode = smoothing;
            }
        }

        private static void DrawBreakpoint(Graphics g, Rectangle area, float scale, BreakpointMark mark)
        {
            float size = Math.Max(8f, Math.Min(20f, 14f * scale));
            RectangleF dot = new RectangleF(area.Left - size * 0.35f, area.Top - size * 0.35f, size, size);
            using (Pen outline = new Pen(Color.FromArgb(230, 20, 20, 20), Math.Max(1f, 1.5f * scale)))
            {
                if (mark == BreakpointMark.Waiting)
                {
                    using (Pen ring = new Pen(BreakpointColour, Math.Max(2f, 3f * scale)))
                    {
                        RectangleF inner = dot;
                        inner.Inflate(-ring.Width * 0.5f, -ring.Width * 0.5f);
                        g.DrawEllipse(outline, dot);
                        g.DrawEllipse(ring, inner);
                    }
                    return;
                }
                using (SolidBrush fill = new SolidBrush(mark == BreakpointMark.Off ? BreakpointOffColour : BreakpointColour))
                    g.FillEllipse(fill, dot);
                g.DrawEllipse(outline, dot);
            }
        }

        private static void DrawHeld(Graphics g, Rectangle area, float scale)
        {
            float pad = Math.Max(3f, 5f * scale);
            RectangleF outline = new RectangleF(area.Left - pad, area.Top - pad, area.Width + pad * 2f, area.Height + pad * 2f);
            using (Pen pen = new Pen(HeldColour, Math.Max(2f, 3f * scale)))
                g.DrawRectangle(pen, outline.X, outline.Y, outline.Width, outline.Height);
            //An arrow pointing at the node's title from its left, as a debugger marks the line it is stopped on
            float arrow = Math.Max(10f, 18f * scale);
            float y = area.Top + Math.Max(8f, 10f * scale);
            float tip = outline.Left - 2f;
            PointF[] points =
            {
                new PointF(tip, y),
                new PointF(tip - arrow, y - arrow * 0.55f),
                new PointF(tip - arrow, y + arrow * 0.55f),
            };
            using (SolidBrush fill = new SolidBrush(HeldColour))
                g.FillPolygon(fill, points);
            using (Pen edge = new Pen(Color.FromArgb(230, 20, 20, 20), 1f))
                g.DrawPolygon(edge, points);
        }

        private void DrawStates(Graphics g, Rectangle area, float scale, string[] lines)
        {
            float size = Math.Max(6.5f, Math.Min(11f, 8.25f * scale));
            if (_stateFont == null || Math.Abs(_stateFontSize - size) > 0.1f)
            {
                _stateFont?.Dispose();
                _stateFont = new Font("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Point);
                _stateFontSize = size;
            }
            //Only as many lines as the zoom leaves readable room for
            int count = scale < 0.7f ? 1 : Math.Min(lines.Length, 6);
            string text = string.Join("\n", lines, 0, count);
            SizeF measured = g.MeasureString(text, _stateFont, Math.Max(80, area.Width * 2));
            float pad = 3f;
            RectangleF strip = new RectangleF(area.Left, area.Top - measured.Height - pad * 2f - Math.Max(2f, 4f * scale), measured.Width + pad * 2f, measured.Height + pad * 2f);
            using (SolidBrush back = new SolidBrush(StateBackColour))
                g.FillRectangle(back, strip);
            using (SolidBrush fore = new SolidBrush(StateTextColour))
                g.DrawString(text, _stateFont, fore, new RectangleF(strip.X + pad, strip.Y + pad, measured.Width + 1f, measured.Height + 1f));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stateFont?.Dispose();
                _stateFont = null;
            }
            base.Dispose(disposing);
        }
    }
}
