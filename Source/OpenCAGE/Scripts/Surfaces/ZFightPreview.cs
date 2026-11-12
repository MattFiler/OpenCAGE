using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace OpenCAGE.Surfaces
{
    /// <summary>
    /// A picture of one fight and its fix (the Find Z-Fighting window's preview and the tools' pictures): above,
    /// the two surfaces seen straight on (B grey, A blue, where they fight red, a scale bar); below, a cut through them before and
    /// after the fix, the separation drawn larger than life with its real size written beside it.
    /// </summary>
    public static class ZFightPreview
    {
        private static readonly Color Back = Color.FromArgb(32, 34, 38);
        private static readonly Color BColour = Color.FromArgb(150, 152, 158);
        private static readonly Color AColour = Color.FromArgb(70, 140, 235);
        private static readonly Color Fight = Color.FromArgb(235, 60, 50);
        private static readonly Color Text = Color.FromArgb(225, 228, 232);
        private static readonly Color Dim = Color.FromArgb(150, 155, 162);
        private static readonly Color Good = Color.FromArgb(90, 200, 110);

        public static Bitmap Draw(ZScan scan, ZFight fight, ZFixPlan plan, int width, int height)
        {
            Bitmap bitmap = new Bitmap(Math.Max(64, width), Math.Max(64, height), PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bitmap))
            using (Font font = new Font("Segoe UI", 8.5f))
            using (Font bold = new Font("Segoe UI", 9f, FontStyle.Bold))
            {
                g.Clear(Back);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                if (fight == null)
                {
                    DrawCentred(g, font, Dim, "No overlap selected.", new RectangleF(0, 0, bitmap.Width, bitmap.Height));
                    return bitmap;
                }
                int planH = (int)(bitmap.Height * 0.68f);
                DrawPlan(g, font, bold, fight, new Rectangle(0, 0, bitmap.Width, planH));
                DrawSection(g, font, fight, plan, new Rectangle(0, planH, bitmap.Width, bitmap.Height - planH));
            }
            return bitmap;
        }

        #region Straight on
        private static void DrawPlan(Graphics g, Font font, Font bold, ZFight fight, Rectangle area)
        {
            Vector3 n = fight.Normal;
            Vector3 u = Math.Abs(n.Y) > 0.9f ? Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, n)) : Vector3.Normalize(Vector3.Cross(Vector3.UnitY, n));
            //Seen from the front: right = u, up = v
            Vector3 v = Vector3.Cross(n, u);
            Vector2 Flat(Vector3 p) => new Vector2(Vector3.Dot(p - fight.Centre, u), Vector3.Dot(p - fight.Centre, v));

            //The window: the overlap, grown
            Vector2 lo = new Vector2(float.MaxValue), hi = new Vector2(float.MinValue);
            foreach (Vector3[] patch in fight.Patches) foreach (Vector3 p in patch) { Vector2 f = Flat(p); lo = Vector2.Min(lo, f); hi = Vector2.Max(hi, f); }
            if (lo.X > hi.X) { lo = new Vector2(-0.5f); hi = new Vector2(0.5f); }
            Vector2 size = Vector2.Max(hi - lo, new Vector2(0.05f));
            float pad = Math.Max(0.15f, Math.Max(size.X, size.Y) * 0.35f);
            lo -= new Vector2(pad); hi += new Vector2(pad);
            Rectangle inner = new Rectangle(area.X + 10, area.Y + 34, area.Width - 20, area.Height - 44);
            float scale = Math.Min(inner.Width / (hi.X - lo.X), inner.Height / (hi.Y - lo.Y));
            PointF centre = new PointF(inner.X + inner.Width / 2f, inner.Y + inner.Height / 2f);
            Vector2 mid = (lo + hi) * 0.5f;
            PointF Screen(Vector3 p) { Vector2 f = Flat(p); return new PointF(centre.X + (f.X - mid.X) * scale, centre.Y - (f.Y - mid.Y) * scale); }
            bool InView(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector2 fa = Flat(a), fb = Flat(b), fc = Flat(c);
                Vector2 mn = Vector2.Min(fa, Vector2.Min(fb, fc)), mx = Vector2.Max(fa, Vector2.Max(fb, fc));
                return mx.X >= lo.X && mn.X <= hi.X && mx.Y >= lo.Y && mn.Y <= hi.Y && Math.Abs(Vector3.Dot(a - fight.Centre, n)) < 1.0f;
            }

            g.SetClip(inner);
            DrawSurface(g, fight.B, Color.FromArgb(150, BColour), Color.FromArgb(200, 90, 92, 98), Screen, InView);
            DrawSurface(g, fight.A, Color.FromArgb(120, AColour), Color.FromArgb(220, 120, 175, 255), Screen, InView);
            using (SolidBrush red = new SolidBrush(Color.FromArgb(170, Fight)))
                foreach (Vector3[] patch in fight.Patches)
                    if (patch.Length >= 3) g.FillPolygon(red, patch.Select(Screen).ToArray());
            g.ResetClip();

            //Scale bar
            float bar = NiceLength((hi.X - lo.X) * 0.25f);
            float barPx = bar * scale;
            using (Pen pen = new Pen(Text, 2))
            {
                float y = area.Bottom - 12, x = area.Right - 16 - barPx;
                g.DrawLine(pen, x, y, x + barPx, y);
                g.DrawLine(pen, x, y - 4, x, y + 4);
                g.DrawLine(pen, x + barPx, y - 4, x + barPx, y + 4);
                string label = bar >= 1 ? bar.ToString("0.#") + " m" : (bar * 100).ToString("0.#") + " cm";
                SizeF s = g.MeasureString(label, font);
                using (SolidBrush b = new SolidBrush(Text)) g.DrawString(label, font, b, x + barPx / 2 - s.Width / 2, y - 18);
            }
            using (SolidBrush t = new SolidBrush(Text))
            using (SolidBrush d = new SolidBrush(Dim))
            {
                g.DrawString("#" + fight.Id + "  " + Area(fight.Area) + " overlap, " + Range(fight) + (fight.BackToBack ? " (back to back)" : ""), bold, t, area.X + 8, area.Y + 4);
                g.DrawString("A (blue): " + Short(fight.A.Label, 70) + "   on   B (grey): " + Short(fight.B.Label, 70), font, d, area.X + 8, area.Y + 19);
                using (SolidBrush red = new SolidBrush(Fight)) g.DrawString("red: where they fight", font, red, area.X + 10, area.Bottom - 20);
            }
        }

        private static void DrawSurface(Graphics g, ZSurface s, Color fill, Color edge, Func<Vector3, PointF> screen, Func<Vector3, Vector3, Vector3, bool> inView)
        {
            using (SolidBrush brush = new SolidBrush(fill))
            using (Pen pen = new Pen(edge, 1))
            {
                int drawn = 0;
                for (int t = 0; t + 2 < s.Triangles.Length && drawn < 30000; t += 3)
                {
                    Vector3 a = s.Positions[s.Triangles[t]], b = s.Positions[s.Triangles[t + 1]], c = s.Positions[s.Triangles[t + 2]];
                    if (!inView(a, b, c)) continue;
                    PointF[] pts = { screen(a), screen(b), screen(c) };
                    g.FillPolygon(brush, pts);
                    g.DrawPolygon(pen, pts);
                    drawn++;
                }
            }
        }
        #endregion

        #region The cut
        private static void DrawSection(Graphics g, Font font, ZFight fight, ZFixPlan plan, Rectangle area)
        {
            using (Pen sep = new Pen(Color.FromArgb(60, 64, 70)))
                g.DrawLine(sep, area.X + 6, area.Y, area.Right - 6, area.Y);
            int half = area.Width / 2;
            bool planned = plan != null && plan.How.ContainsKey(fight.Id);
            (bool sink, float distance) how = planned ? plan.How[fight.Id] : (false, 0f);
            float before = fight.OffsetMean;
            float after = planned ? (how.sink ? before + how.distance : before + how.distance) : before;
            DrawCut(g, font, new Rectangle(area.X, area.Y + 4, half, area.Height - 8), "Now", before, fight.OffsetMin, fight.OffsetMax, false, false, fight, null);
            string fix;
            if (!planned) fix = plan == null ? "No fix planned." : "Nothing to move.";
            else
            {
                string what = how.sink ? "B sinks back " : "A is pushed out ";
                ZMove move = plan.Moves.FirstOrDefault(o => o.Fights.Contains(fight.Id));
                float real = move != null ? move.Distance : how.distance;
                fix = what + ZFightRepair.Mm(real) + (move != null && !move.IsPlacement ? " (its vertices)" : move != null ? " (its placement)" : "");
                after = fight.OffsetMin + real;
            }
            string check = null;
            if (planned && plan.Verified)
            {
                double left = plan.AreaAfter.TryGetValue(fight.Id, out double l) ? l : 0;
                check = left < 1e-6 ? "searched again: gone" : "searched again: " + Area(left) + " left";
                if (plan.Made.Count != 0) check += "; " + plan.Made.Count + " new overlap(s) the move makes";
            }
            DrawCut(g, font, new Rectangle(area.X + half, area.Y + 4, area.Width - half, area.Height - 8), planned ? "After" : "After (nothing planned)", after, after, after, planned, how.sink, fight, fix + (check != null ? "\n" + check : ""));
        }

        private static void DrawCut(Graphics g, Font font, Rectangle r, string title, float offset, float min, float max, bool fixedState, bool sink, ZFight fight, string note)
        {
            using (SolidBrush t = new SolidBrush(Text))
            using (SolidBrush d = new SolidBrush(Dim))
            {
                g.DrawString(title, font, t, r.X + 10, r.Y + 2);
                float midY = r.Y + r.Height * 0.55f;
                float x0 = r.X + 24, x1 = r.Right - 24;
                //Larger than life: 1 mm = 3 px, at least 3 px apart when apart at all
                float px = offset * 1000 * 3;
                if (Math.Abs(offset) > 1e-5f && Math.Abs(px) < 3) px = Math.Sign(px) * 3;
                px = Math.Max(-r.Height * 0.35f, Math.Min(r.Height * 0.35f, px));
                bool flickers = !fixedState && Math.Abs(min) <= 0.004f + 1e-6f;
                using (Pen b = new Pen(BColour, 5))
                using (Pen a = new Pen(AColour, 3))
                {
                    g.DrawLine(b, x0, midY + 3, x1, midY + 3);
                    float ay = midY - px;
                    g.DrawLine(a, x0 + (x1 - x0) * 0.2f, ay, x1 - (x1 - x0) * 0.2f, ay);
                    if (flickers)
                        using (Pen red = new Pen(Fight, 2) { DashStyle = DashStyle.Dot })
                            g.DrawLine(red, x0 + (x1 - x0) * 0.2f, midY, x1 - (x1 - x0) * 0.2f, midY);
                    string label = (offset >= 0 ? "A " + ZFightRepair.Mm(offset) + " in front of B" : "A " + ZFightRepair.Mm(-offset) + " behind B") + (fixedState ? "" : Math.Abs(max - min) > 0.0002f ? " (" + ZFightRepair.Mm(min) + " to " + ZFightRepair.Mm(max) + ")" : "");
                    using (SolidBrush lb = new SolidBrush(flickers ? Fight : fixedState ? Good : Text))
                        g.DrawString(label + (flickers ? ": flickers" : fixedState ? ": clear" : ""), font, lb, r.X + 10, r.Y + 18);
                }
                if (note != null) g.DrawString(note, font, d, r.X + 10, r.Bottom - 32);
            }
        }
        #endregion

        private static float NiceLength(float around)
        {
            float[] steps = { 0.01f, 0.02f, 0.05f, 0.1f, 0.2f, 0.25f, 0.5f, 1, 2, 5, 10, 20, 50, 100 };
            foreach (float s in steps) if (s >= around) return s;
            return steps[steps.Length - 1];
        }

        public static string Area(double m2) => m2 >= 1 ? m2.ToString("0.0") + " m²" : (m2 * 10000).ToString("0") + " cm²";

        public static string Range(ZFight f)
        {
            if (Math.Abs(f.OffsetMax - f.OffsetMin) < 0.0002f) return ZFightRepair.Mm(f.OffsetMean) + " apart";
            return ZFightRepair.Mm(f.OffsetMin) + " to " + ZFightRepair.Mm(f.OffsetMax) + " apart";
        }

        private static string Short(string s, int max) => s == null ? "" : s.Length <= max ? s : s.Substring(0, max - 1) + "…";

        private static void DrawCentred(Graphics g, Font font, Color colour, string text, RectangleF r)
        {
            using (SolidBrush b = new SolidBrush(colour))
            using (StringFormat f = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(text, font, b, r, f);
        }
    }
}
