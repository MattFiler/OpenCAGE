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
        /// <summary>Each corner's height, when known (else the whole polygon is at Height).</summary>
        public float[] Heights;
        public float CentreX, CentreZ;

        public MapPolygon(PointF[] outline, float height, float[] heights = null)
        {
            Outline = outline;
            Height = height;
            Heights = heights != null && heights.Length == outline.Length ? heights : null;
            CentreX = outline.Average(o => o.X);
            CentreZ = outline.Average(o => o.Y);
        }

        public float Lowest => Heights != null ? Heights.Min() : Height;
        public float Highest => Heights != null ? Heights.Max() : Height;

        /// <summary>
        /// The surface's height at (x, z): within the fan of triangles from its centre (at Height) to each pair of corners, so every
        /// corner's height holds even where the polygon isn't flat.
        /// </summary>
        public float HeightAt(float x, float z)
        {
            if (Heights == null || Outline.Length < 3) return Height;
            float bestMin = float.MinValue, y = Height;
            for (int i = 0; i < Outline.Length; i++)
            {
                int j = (i + 1) % Outline.Length;
                float ax = Outline[i].X - CentreX, az = Outline[i].Y - CentreZ, bx = Outline[j].X - CentreX, bz = Outline[j].Y - CentreZ;
                float den = ax * bz - az * bx;
                if (Math.Abs(den) < 1e-9f) continue;
                float px = x - CentreX, pz = z - CentreZ;
                float u = (px * bz - pz * bx) / den, v = (ax * pz - az * px) / den, w = 1 - u - v;
                float worst = Math.Min(w, Math.Min(u, v));
                if (worst <= bestMin) continue;
                bestMin = worst;
                u = Math.Max(0, u); v = Math.Max(0, v); w = Math.Max(0, w);
                float sum = u + v + w;
                y = (w * Height + u * Heights[i] + v * Heights[j]) / sum;
                if (worst >= 0) break;
            }
            return Math.Max(Lowest, Math.Min(Highest, y));
        }

        /// <summary>Whether it is level: its corners within the rise maxDegrees makes across it (or 0.25 m on a small one).</summary>
        public bool IsLevel(float maxDegrees)
        {
            if (Heights == null) return true;
            float w = Outline.Max(o => o.X) - Outline.Min(o => o.X), d = Outline.Max(o => o.Y) - Outline.Min(o => o.Y);
            return Highest - Lowest <= Math.Max(0.25, Math.Tan(maxDegrees * Math.PI / 180) * Math.Sqrt(w * w + d * d));
        }

        public bool Contains(float x, float z)
        {
            bool c = false;
            for (int i = 0, j = Outline.Length - 1; i < Outline.Length; j = i++)
                if ((Outline[i].Y > z) != (Outline[j].Y > z) && x < (Outline[j].X - Outline[i].X) * (z - Outline[i].Y) / (Outline[j].Y - Outline[i].Y) + Outline[i].X)
                    c = !c;
            return c;
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
        public float Simplify = 0.4f;
        /// <summary>Outlines smaller than this (square metres) are dropped.</summary>
        public float MinArea = 3f;
        /// <summary>Enclosed holes up to this size (square metres: furniture, pillars) are filled.</summary>
        public float FillHoles = 30f;
        /// <summary>Outlines squared to their walls' axes: edges near them made exactly parallel, small steps and cut corners removed.</summary>
        public bool Square = true;
        /// <summary>Edges within this many degrees of the axes are made parallel to them.</summary>
        public float SquareDegrees = 14f;
        /// <summary>Steps between parallel edges up to this deep (metres) are straightened into one edge.</summary>
        public float Jog = 0.6f;
        /// <summary>Slanted edges up to this long (metres) between edges at right angles are a cut corner, made square.</summary>
        public float Corner = 2f;
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
    /// walls, then traced (marching squares), simplified and squared to the walls' axes (the plan's main axes, or an outline's own).
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
            List<KeyValuePair<List<PointF>, float>> loops = new List<KeyValuePair<List<PointF>, float>>();
            foreach (List<PointF> loop in Contours(g))
            {
                float area = Area(loop);
                if (Math.Abs(area) < style.MinArea) continue;
                loops.Add(new KeyValuePair<List<PointF>, float>(Simplify(loop, style.Simplify), area));
            }
            float main = style.Square ? Axis(loops.SelectMany(o => Edges(o.Key)), 0, 0) : 0;
            foreach (KeyValuePair<List<PointF>, float> loop in loops)
            {
                List<PointF> simple = loop.Key;
                if (style.Square) simple = Rectify(simple, Axis(Edges(simple), main, 0.5f), style);
                if (simple.Count < 3 || Math.Abs(Area(simple)) < style.MinArea / 2) continue;
                outlines.Add(new MapOutline() { Points = simple, Hole = loop.Value > 0, Style = style });
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

        private static IEnumerable<KeyValuePair<PointF, PointF>> Edges(List<PointF> loop)
        {
            for (int i = 0; i < loop.Count; i++) yield return new KeyValuePair<PointF, PointF>(loop[i], loop[(i + 1) % loop.Count]);
        }

        /* The axes most of the edges' length runs along or across (radians, 0 up to 90 degrees), from 1 degree bins smoothed over 5.
           'fallback' when those edges hold less than 'share' of the length, or lie within 2 degrees of it */
        private static float Axis(IEnumerable<KeyValuePair<PointF, PointF>> edges, float fallback, float share)
        {
            double[] bins = new double[90];
            double total = 0;
            foreach (KeyValuePair<PointF, PointF> e in edges)
            {
                double dx = e.Value.X - e.Key.X, dz = e.Value.Y - e.Key.Y, len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 0.5) continue;
                double deg = Math.Atan2(dz, dx) * 180 / Math.PI;
                deg = ((deg % 90) + 90) % 90;
                bins[(int)deg % 90] += len;
                total += len;
            }
            if (total <= 0) return fallback;
            int best = 0; double bestSum = -1;
            for (int b = 0; b < 90; b++)
            {
                double s = 0;
                for (int k = -2; k <= 2; k++) s += bins[(b + k + 90) % 90];
                if (s > bestSum) { bestSum = s; best = b; }
            }
            if (bestSum < total * share) return fallback;
            double wx = 0, ww = 0;
            for (int k = -2; k <= 2; k++) { wx += bins[(best + k + 90) % 90] * (best + k + 0.5); ww += bins[(best + k + 90) % 90]; }
            double axis = ((wx / ww) % 90 + 90) % 90;
            double fb = fallback * 180 / Math.PI, d = Math.Abs(axis - fb) % 90;
            if (Math.Min(d, 90 - d) <= 2) return fallback;
            return (float)(axis * Math.PI / 180);
        }

        private enum RunAxis { Across, Along, Slanted }

        /* A stretch of outline: along one axis at an offset (x for Along, z for Across), or slanted from A to B */
        private class Run
        {
            public RunAxis Axis;
            public float Offset, Length;
            public PointF A, B;
        }

        /*
         * An outline squared to its axes: in their frame, each edge within SquareDegrees of an axis becomes a run exactly along it (at
         * its mean offset); neighbouring runs on one axis a small step apart join (length-weighted offset); a short slanted edge between
         * runs at right angles (a rounded or cut corner) goes, as does a short notch between two runs on one axis; a short slanted step
         * between them is made square. The corners are then where neighbouring runs meet, so every edge is a straight line.
         */
        private static List<PointF> Rectify(List<PointF> loop, float axis, MapLayerStyle style)
        {
            if (loop.Count < 4) return loop;
            float c = (float)Math.Cos(axis), s = (float)Math.Sin(axis);
            List<PointF> p = loop.Select(q => new PointF(q.X * c + q.Y * s, -q.X * s + q.Y * c)).ToList();
            double tan = Math.Tan(style.SquareDegrees * Math.PI / 180);
            List<Run> runs = new List<Run>();
            for (int i = 0; i < p.Count; i++)
            {
                PointF a = p[i], b = p[(i + 1) % p.Count];
                float dx = b.X - a.X, dz = b.Y - a.Y, len = (float)Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-4f) continue;
                Run r = new Run() { A = a, B = b, Length = len, Axis = RunAxis.Slanted };
                if (Math.Abs(dz) <= Math.Abs(dx) * tan) { r.Axis = RunAxis.Across; r.Offset = (a.Y + b.Y) / 2; }
                else if (Math.Abs(dx) <= Math.Abs(dz) * tan) { r.Axis = RunAxis.Along; r.Offset = (a.X + b.X) / 2; }
                else if (Math.Abs(Math.Abs(dx) - Math.Abs(dz)) <= (Math.Abs(dx) + Math.Abs(dz)) * tan / 2)
                {
                    //Near the diagonal: exactly 45 degrees, through its middle
                    float mx = (a.X + b.X) / 2, mz = (a.Y + b.Y) / 2, h = (Math.Abs(dx) + Math.Abs(dz)) / 4;
                    r.A = new PointF(mx - Math.Sign(dx) * h, mz - Math.Sign(dz) * h);
                    r.B = new PointF(mx + Math.Sign(dx) * h, mz + Math.Sign(dz) * h);
                }
                runs.Add(r);
            }

            for (int guard = 0; guard < 4 * p.Count && runs.Count > 4; guard++)
            {
                bool changed = false;
                for (int i = 0; i < runs.Count && !changed; i++)
                {
                    int ni = (i + 1) % runs.Count, mi = (i + 2) % runs.Count;
                    Run r = runs[i], n = runs[ni], m = runs[mi];
                    if (r.Axis == RunAxis.Slanted) continue;
                    if (n.Axis == r.Axis && Math.Abs(r.Offset - n.Offset) <= style.Jog)
                    {
                        Join(r, n);
                        runs.RemoveAt(ni);
                        changed = true;
                        continue;
                    }
                    //Up to three short runs between r and a run on r's axis (a notch or bump): gone when the two are a small step apart
                    //(one run), else a lone slanted one is made a square step; between r and a run across it, slanted ones are a cut corner
                    float between = 0;
                    bool slanted = true;
                    for (int k = 2; k <= 4 && k < runs.Count - 1 && !changed; k++)
                    {
                        Run b = runs[(i + k - 1) % runs.Count];
                        between += b.Length;
                        slanted &= b.Axis == RunAxis.Slanted;
                        if (b.Length > style.Corner || between > 2 * style.Corner) break;
                        Run e = runs[(i + k) % runs.Count];
                        if (e.Axis == r.Axis && Math.Abs(r.Offset - e.Offset) <= style.Jog)
                        {
                            Join(r, e);
                            Remove(runs, i, k);
                            changed = true;
                        }
                        else if (e.Axis == r.Axis && k == 2 && slanted)
                        {
                            b.Axis = r.Axis == RunAxis.Across ? RunAxis.Along : RunAxis.Across;
                            b.Offset = b.Axis == RunAxis.Along ? (b.A.X + b.B.X) / 2 : (b.A.Y + b.B.Y) / 2;
                            changed = true;
                        }
                        else if (e.Axis != r.Axis && e.Axis != RunAxis.Slanted && slanted && between <= style.Corner * 1.5f)
                        {
                            Remove(runs, i, k - 1);
                            changed = true;
                        }
                    }
                }
                if (!changed) break;
            }
            if (runs.Count < 3) return loop;

            List<PointF> o = new List<PointF>();
            for (int i = 0; i < runs.Count; i++)
                Meet(runs[i], runs[(i + 1) % runs.Count], o, style.Corner);
            List<PointF> r2 = new List<PointF>();
            foreach (PointF q in o) if (r2.Count == 0 || Sq(r2[r2.Count - 1], q) > 1e-4f) r2.Add(q);
            if (r2.Count > 1 && Sq(r2[0], r2[r2.Count - 1]) < 1e-4f) r2.RemoveAt(r2.Count - 1);
            for (int i = 0; i < r2.Count && r2.Count > 3;)
            {
                PointF a = r2[(i + r2.Count - 1) % r2.Count], b = r2[i], d = r2[(i + 1) % r2.Count];
                float cross = (b.X - a.X) * (d.Y - b.Y) - (b.Y - a.Y) * (d.X - b.X);
                if (Math.Abs(cross) < 1e-3f) r2.RemoveAt(i); else i++;
            }
            if (r2.Count < 3) return loop;
            return r2.Select(q => new PointF(q.X * c - q.Y * s, q.X * s + q.Y * c)).ToList();
        }

        /* The 'count' runs after run i dropped */
        private static void Remove(List<Run> runs, int i, int count)
        {
            foreach (int k in Enumerable.Range(1, count).Select(j => (i + j) % runs.Count).OrderByDescending(o => o).ToList())
                runs.RemoveAt(k);
        }

        /* n added onto the end of r (one run along r's axis, at the length-weighted offset) */
        private static void Join(Run r, Run n)
        {
            if (n.Axis == r.Axis) r.Offset = (r.Offset * r.Length + n.Offset * n.Length) / (r.Length + n.Length);
            r.Length += n.Length;
            r.B = n.B;
        }

        /* Where run r meets the next run n: the corners added to o */
        private static void Meet(Run r, Run n, List<PointF> o, float reach)
        {
            PointF at = n.A;
            if (r.Axis == RunAxis.Across && n.Axis == RunAxis.Along) { o.Add(new PointF(n.Offset, r.Offset)); return; }
            if (r.Axis == RunAxis.Along && n.Axis == RunAxis.Across) { o.Add(new PointF(r.Offset, n.Offset)); return; }
            if (r.Axis == RunAxis.Across && n.Axis == RunAxis.Across) { o.Add(new PointF(at.X, r.Offset)); o.Add(new PointF(at.X, n.Offset)); return; }
            if (r.Axis == RunAxis.Along && n.Axis == RunAxis.Along) { o.Add(new PointF(r.Offset, at.Y)); o.Add(new PointF(n.Offset, at.Y)); return; }
            PointF hit;
            if (Cross(Line(r), Line(n), out hit) && Sq(hit, at) <= reach * reach * 4) { o.Add(hit); return; }
            if (r.Axis != RunAxis.Slanted) o.Add(OnRun(r, at));
            o.Add(at);
            if (n.Axis != RunAxis.Slanted) o.Add(OnRun(n, at));
        }

        private static PointF OnRun(Run r, PointF p) => r.Axis == RunAxis.Across ? new PointF(p.X, r.Offset) : new PointF(r.Offset, p.Y);

        /* A run as a point and a unit direction */
        private static KeyValuePair<PointF, PointF> Line(Run r)
        {
            if (r.Axis == RunAxis.Across) return new KeyValuePair<PointF, PointF>(new PointF(r.A.X, r.Offset), new PointF(1, 0));
            if (r.Axis == RunAxis.Along) return new KeyValuePair<PointF, PointF>(new PointF(r.Offset, r.A.Y), new PointF(0, 1));
            float dx = r.B.X - r.A.X, dz = r.B.Y - r.A.Y, len = (float)Math.Sqrt(dx * dx + dz * dz);
            return new KeyValuePair<PointF, PointF>(r.A, new PointF(dx / len, dz / len));
        }

        /* Two lines' crossing, unless they run within 10 degrees of each other */
        private static bool Cross(KeyValuePair<PointF, PointF> a, KeyValuePair<PointF, PointF> b, out PointF hit)
        {
            hit = PointF.Empty;
            float den = a.Value.X * b.Value.Y - a.Value.Y * b.Value.X;
            if (Math.Abs(den) < 0.17f) return false;
            float t = ((b.Key.X - a.Key.X) * b.Value.Y - (b.Key.Y - a.Key.Y) * b.Value.X) / den;
            hit = new PointF(a.Key.X + a.Value.X * t, a.Key.Y + a.Value.Y * t);
            return true;
        }
        #endregion
    }
}
