using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

namespace OpenCAGE.MapAssets
{
    /// <summary>A walkable polygon of a level's navmesh: its outline seen from above (x, z) and its height.</summary>
    public class MapPolygon
    {
        public PointF[] Outline;
        public float Height;
        public float CentreX, CentreZ;

        public MapPolygon(PointF[] outline, float height)
        {
            Outline = outline;
            Height = height;
            CentreX = outline.Average(o => o.X);
            CentreZ = outline.Average(o => o.Y);
        }
    }

    /// <summary>How one layer of a floor plan is traced and drawn.</summary>
    public class MapLayerStyle
    {
        public string Name = "rooms";
        /// <summary>Gaps narrower than twice this (metres) are closed before tracing; walls are wider than the gaps it closes.</summary>
        public float Close = 0.35f;
        /// <summary>How far the walkable area is grown out (metres): about the walk mesh's distance from the walls.</summary>
        public float Grow = 0.3f;
        /// <summary>Outline simplification tolerance (metres).</summary>
        public float Simplify = 0.3f;
        /// <summary>Outlines smaller than this (square metres) are dropped.</summary>
        public float MinArea = 3f;
        /// <summary>Enclosed holes up to this size (square metres: furniture, pillars) are filled.</summary>
        public float FillHoles = 30f;
        /// <summary>Edges near the world axes made exactly axis-aligned.</summary>
        public bool Square = true;
        public int FillAlpha = 25, LineAlpha = 255;
    }

    /// <summary>A traced outline (world x, z). Holes run the other way round.</summary>
    public class MapOutline
    {
        public List<PointF> Points;
        public bool Hole;
        public MapLayerStyle Style;
    }

    /// <summary>
    /// Floor plans from a navmesh: the chosen polygons are rasterised, small gaps closed, enclosed holes filled, the area grown to the
    /// walls, then traced (marching squares), simplified and squared to the axes.
    /// </summary>
    public static class MapFloorPlan
    {
        /// <summary>Metres per raster cell.</summary>
        public const float Resolution = 0.2f;

        public static List<MapOutline> Trace(IList<MapPolygon> polygons, MapLayerStyle style)
        {
            List<MapOutline> outlines = new List<MapOutline>();
            if (polygons.Count == 0)
                return outlines;
            Grid g = Rasterise(polygons, style.Close + style.Grow + 1f);
            if (style.Close > 0) { Dilate(g, style.Close); Erode(g, style.Close); }
            FillHoles(g, style.FillHoles);
            if (style.Grow > 0) Dilate(g, style.Grow);
            foreach (List<PointF> loop in Contours(g))
            {
                float area = Area(loop);
                if (Math.Abs(area) < style.MinArea) continue;
                List<PointF> simple = Simplify(loop, style.Simplify);
                if (style.Square) simple = Square(simple, 14f);
                if (simple.Count < 3) continue;
                outlines.Add(new MapOutline() { Points = simple, Hole = area > 0, Style = style });
            }
            return outlines;
        }

        #region Raster
        private class Grid
        {
            public int W, H;
            public float X0, Z0;
            public bool[] V;
            public bool At(int i, int j) { return i >= 0 && j >= 0 && i < W && j < H && V[j * W + i]; }
        }

        private static Grid Rasterise(IList<MapPolygon> polygons, float margin)
        {
            float minX = polygons.Min(p => p.Outline.Min(q => q.X)) - margin, maxX = polygons.Max(p => p.Outline.Max(q => q.X)) + margin;
            float minZ = polygons.Min(p => p.Outline.Min(q => q.Y)) - margin, maxZ = polygons.Max(p => p.Outline.Max(q => q.Y)) + margin;
            Grid g = new Grid() { X0 = minX, Z0 = minZ, W = (int)Math.Ceiling((maxX - minX) / Resolution) + 1, H = (int)Math.Ceiling((maxZ - minZ) / Resolution) + 1 };
            g.V = new bool[g.W * g.H];
            using (Bitmap bmp = new Bitmap(g.W, g.H, PixelFormat.Format32bppArgb))
            {
                using (Graphics gr = Graphics.FromImage(bmp))
                {
                    gr.Clear(Color.Black);
                    gr.SmoothingMode = SmoothingMode.None;
                    foreach (MapPolygon p in polygons)
                    {
                        PointF[] pts = p.Outline.Select(q => new PointF((q.X - minX) / Resolution, (q.Y - minZ) / Resolution)).ToArray();
                        gr.FillPolygon(Brushes.White, pts);
                        gr.DrawPolygon(Pens.White, pts);
                    }
                }
                BitmapData data = bmp.LockBits(new Rectangle(0, 0, g.W, g.H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                byte[] px = new byte[data.Stride * g.H];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, px, 0, px.Length);
                bmp.UnlockBits(data);
                for (int j = 0; j < g.H; j++)
                    for (int i = 0; i < g.W; i++)
                        g.V[j * g.W + i] = px[j * data.Stride + i * 4 + 1] > 127;
            }
            return g;
        }

        /* Squared distance (in cells) to the nearest cell holding 'to': two 1-D lower-envelope passes (exact euclidean) */
        private static float[] Distance(Grid g, bool to)
        {
            const float INF = 1e12f;
            float[] d = new float[g.W * g.H];
            for (int k = 0; k < d.Length; k++) d[k] = g.V[k] == to ? 0 : INF;
            int m = Math.Max(g.W, g.H);
            float[] f = new float[m], o = new float[m], zz = new float[m + 1];
            int[] v = new int[m];
            Action<int> pass = n =>
            {
                int k = 0; v[0] = 0; zz[0] = -INF; zz[1] = INF;
                for (int q = 1; q < n; q++)
                {
                    float s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2 * q - 2 * v[k]);
                    while (s <= zz[k]) { k--; s = ((f[q] + q * q) - (f[v[k]] + v[k] * v[k])) / (2 * q - 2 * v[k]); }
                    k++; v[k] = q; zz[k] = s; zz[k + 1] = INF;
                }
                k = 0;
                for (int q = 0; q < n; q++) { while (zz[k + 1] < q) k++; o[q] = (q - v[k]) * (q - v[k]) + f[v[k]]; }
            };
            for (int i = 0; i < g.W; i++) { for (int j = 0; j < g.H; j++) f[j] = d[j * g.W + i]; pass(g.H); for (int j = 0; j < g.H; j++) d[j * g.W + i] = o[j]; }
            for (int j = 0; j < g.H; j++) { for (int i = 0; i < g.W; i++) f[i] = d[j * g.W + i]; pass(g.W); for (int i = 0; i < g.W; i++) d[j * g.W + i] = o[i]; }
            return d;
        }
        private static void Dilate(Grid g, float r) { float[] d = Distance(g, true); float rr = (r / Resolution) * (r / Resolution); for (int k = 0; k < d.Length; k++) g.V[k] = d[k] <= rr; }
        private static void Erode(Grid g, float r) { float[] d = Distance(g, false); float rr = (r / Resolution) * (r / Resolution); for (int k = 0; k < d.Length; k++) g.V[k] = d[k] > rr; }

        /* Empty regions not reaching the grid's edge, up to maxArea square metres, become walkable */
        private static void FillHoles(Grid g, float maxArea)
        {
            if (maxArea <= 0) return;
            int[] comp = new int[g.W * g.H];
            int next = 0, maxCells = (int)(maxArea / (Resolution * Resolution));
            Queue<int> q = new Queue<int>();
            for (int s = 0; s < comp.Length; s++)
            {
                if (g.V[s] || comp[s] != 0) continue;
                next++;
                List<int> cells = new List<int>();
                bool edge = false;
                comp[s] = next; q.Enqueue(s);
                while (q.Count > 0)
                {
                    int c = q.Dequeue(); cells.Add(c);
                    int i = c % g.W, j = c / g.W;
                    if (i == 0 || j == 0 || i == g.W - 1 || j == g.H - 1) edge = true;
                    if (i > 0 && !g.V[c - 1] && comp[c - 1] == 0) { comp[c - 1] = next; q.Enqueue(c - 1); }
                    if (i < g.W - 1 && !g.V[c + 1] && comp[c + 1] == 0) { comp[c + 1] = next; q.Enqueue(c + 1); }
                    if (j > 0 && !g.V[c - g.W] && comp[c - g.W] == 0) { comp[c - g.W] = next; q.Enqueue(c - g.W); }
                    if (j < g.H - 1 && !g.V[c + g.W] && comp[c + g.W] == 0) { comp[c + g.W] = next; q.Enqueue(c + g.W); }
                }
                if (!edge && cells.Count <= maxCells) foreach (int c in cells) g.V[c] = true;
            }
        }
        #endregion

        #region Tracing
        /* Marching squares through the cell centres; every loop has the walkable side on its right (x right, z up) */
        private static List<List<PointF>> Contours(Grid g)
        {
            Dictionary<long, long> segs = new Dictionary<long, long>();
            Func<int, int, long> key = (a, b) => ((long)(a + 4) << 32) | (uint)(b + 4);
            for (int j = -1; j < g.H; j++)
                for (int i = -1; i < g.W; i++)
                {
                    int c = (g.At(i, j) ? 1 : 0) | (g.At(i + 1, j) ? 2 : 0) | (g.At(i + 1, j + 1) ? 4 : 0) | (g.At(i, j + 1) ? 8 : 0);
                    long B = key(2 * i + 1, 2 * j), R = key(2 * i + 2, 2 * j + 1), T = key(2 * i + 1, 2 * j + 2), L = key(2 * i, 2 * j + 1);
                    switch (c)
                    {
                        case 1: segs[L] = B; break;
                        case 2: segs[B] = R; break;
                        case 3: segs[L] = R; break;
                        case 4: segs[R] = T; break;
                        case 5: segs[L] = T; segs[R] = B; break;
                        case 6: segs[B] = T; break;
                        case 7: segs[L] = T; break;
                        case 8: segs[T] = L; break;
                        case 9: segs[T] = B; break;
                        case 10: segs[B] = L; segs[T] = R; break;
                        case 11: segs[T] = R; break;
                        case 12: segs[R] = L; break;
                        case 13: segs[R] = B; break;
                        case 14: segs[B] = L; break;
                    }
                }
            List<List<PointF>> loops = new List<List<PointF>>();
            HashSet<long> used = new HashSet<long>();
            foreach (long start in segs.Keys.ToList())
            {
                if (used.Contains(start)) continue;
                List<PointF> loop = new List<PointF>();
                long cur = start;
                while (!used.Contains(cur) && segs.ContainsKey(cur))
                {
                    used.Add(cur);
                    int a = (int)(cur >> 32) - 4, b = (int)(uint)cur - 4;
                    loop.Add(new PointF(g.X0 + (a / 2f) * Resolution, g.Z0 + (b / 2f) * Resolution));
                    cur = segs[cur];
                }
                if (loop.Count >= 3) loops.Add(loop);
            }
            return loops;
        }

        /// <summary>Signed area (x, z): positive when the loop runs anticlockwise.</summary>
        public static float Area(List<PointF> l)
        {
            double a = 0;
            for (int i = 0; i < l.Count; i++) { PointF p = l[i], q = l[(i + 1) % l.Count]; a += (double)p.X * q.Y - (double)q.X * p.Y; }
            return (float)(a / 2);
        }

        private static float Sq(PointF p, PointF q) { return (p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y); }

        /* A closed loop simplified: split at two far-apart points, each half by Douglas-Peucker */
        private static List<PointF> Simplify(List<PointF> pts, float tol)
        {
            if (pts.Count < 4) return pts;
            int a = 0, b = 0; float best = -1;
            int step = Math.Max(1, pts.Count / 64);
            for (int i = 0; i < pts.Count; i += step) for (int j = 0; j < pts.Count; j++) { float d = Sq(pts[i], pts[j]); if (d > best) { best = d; a = i; b = j; } }
            if (a > b) { int t = a; a = b; b = t; }
            if (a == b) return pts;
            List<PointF> h1 = pts.GetRange(a, b - a + 1), h2 = pts.GetRange(b, pts.Count - b).Concat(pts.GetRange(0, a + 1)).ToList();
            List<PointF> r1 = DouglasPeucker(h1, tol), r2 = DouglasPeucker(h2, tol);
            return r1.Take(r1.Count - 1).Concat(r2.Take(r2.Count - 1)).ToList();
        }

        private static List<PointF> DouglasPeucker(List<PointF> pts, float tol)
        {
            if (pts.Count < 3) return pts;
            bool[] keep = new bool[pts.Count]; keep[0] = keep[pts.Count - 1] = true;
            Stack<KeyValuePair<int, int>> stack = new Stack<KeyValuePair<int, int>>();
            stack.Push(new KeyValuePair<int, int>(0, pts.Count - 1));
            while (stack.Count > 0)
            {
                KeyValuePair<int, int> se = stack.Pop(); int s = se.Key, e = se.Value;
                float md = 0; int mi = -1;
                PointF A = pts[s], B = pts[e]; float dx = B.X - A.X, dy = B.Y - A.Y, len = (float)Math.Sqrt(dx * dx + dy * dy);
                for (int i = s + 1; i < e; i++)
                {
                    float d = len < 1e-6 ? (float)Math.Sqrt(Sq(pts[i], A)) : Math.Abs(dy * pts[i].X - dx * pts[i].Y + B.X * A.Y - B.Y * A.X) / len;
                    if (d > md) { md = d; mi = i; }
                }
                if (md > tol && mi > 0) { keep[mi] = true; stack.Push(new KeyValuePair<int, int>(s, mi)); stack.Push(new KeyValuePair<int, int>(mi, e)); }
            }
            return pts.Where((p, i) => keep[i]).ToList();
        }

        /* Edges within maxDeg of the world axes made exactly axis-aligned (both ends to their mean), then repeated and collinear
           points dropped: rooms are built square to the axes, the tracer's steps are not */
        private static List<PointF> Square(List<PointF> pts, float maxDeg)
        {
            List<PointF> p = pts.ToList();
            double tan = Math.Tan(maxDeg * Math.PI / 180);
            for (int pass = 0; pass < 3; pass++)
                for (int i = 0; i < p.Count; i++)
                {
                    int j = (i + 1) % p.Count;
                    float dx = p[j].X - p[i].X, dz = p[j].Y - p[i].Y;
                    if (Math.Abs(dx) < 1e-4 && Math.Abs(dz) < 1e-4) continue;
                    if (Math.Abs(dz) <= Math.Abs(dx) * tan) { float m = (p[i].Y + p[j].Y) / 2; p[i] = new PointF(p[i].X, m); p[j] = new PointF(p[j].X, m); }
                    else if (Math.Abs(dx) <= Math.Abs(dz) * tan) { float m = (p[i].X + p[j].X) / 2; p[i] = new PointF(m, p[i].Y); p[j] = new PointF(m, p[j].Y); }
                }
            List<PointF> r = new List<PointF>();
            foreach (PointF q in p) if (r.Count == 0 || Sq(r[r.Count - 1], q) > 1e-4f) r.Add(q);
            if (r.Count > 1 && Sq(r[0], r[r.Count - 1]) < 1e-4f) r.RemoveAt(r.Count - 1);
            for (int i = 0; i < r.Count && r.Count > 3;)
            {
                PointF a = r[(i + r.Count - 1) % r.Count], b = r[i], c = r[(i + 1) % r.Count];
                float cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                if (Math.Abs(cross) < 1e-3f) r.RemoveAt(i); else i++;
            }
            return r;
        }
        #endregion
    }
}
