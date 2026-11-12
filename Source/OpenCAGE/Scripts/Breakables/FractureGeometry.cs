using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.Breakables
{
    /// <summary>How the faces a cut opens are textured.</summary>
    public enum FractureInterior
    {
        /// <summary>One dark texel of the body texture (the darkest the cut's rim touches): a flat dark inside.</summary>
        Dark,
        /// <summary>The body texture projected flat across the cut at the body's own texel density: the material carries on inside (wood, concrete).</summary>
        Projected,
    }

    /// <summary>A corner of a triangle: position, normal, texture coordinates (one per channel the source has) and colour.</summary>
    public struct FractureVertex
    {
        public Vector3 P, N;
        public Vector2[] UV;
        public Vector4 C;
        /// <summary>The source group (submesh) the triangle this corner was cut from belongs to.</summary>
        public int Group;
    }

    public enum FractureTriangleKind { Body, Cap, Skin }

    public sealed class FractureTriangle
    {
        public FractureVertex A, B, C;
        public int Group;
        public FractureTriangleKind Kind;
        public FractureTriangle Copy() => new FractureTriangle() { A = A, B = B, C = C, Group = Group, Kind = Kind };
    }

    /// <summary>What a source group (one drawn submesh) is, as far as cutting it goes.</summary>
    public sealed class FractureGroup
    {
        public string Name;
        /// <summary>Texture coordinate channels its corners carry.</summary>
        public int UVChannels;
        /// <summary>See-through, decal or effect: its triangles are cut but never capped or skinned (they lie over the body).</summary>
        public bool Overlay;
        /// <summary>Luminance of its diffuse texture at a texture coordinate (channel 0), or null when it cannot be read.</summary>
        public Func<Vector2, float> Luminance;
    }

    /// <summary>The model to break: its triangles in model space (metres), grouped by drawn submesh.</summary>
    public sealed class FractureSource
    {
        public List<FractureGroup> Groups = new List<FractureGroup>();
        public List<FractureTriangle> Triangles = new List<FractureTriangle>();
    }

    public sealed class FractureOptions
    {
        public int Pieces = 6;
        public int Seed = 7;
        public FractureInterior Interior = FractureInterior.Dark;
        /// <summary>A reversed copy of the body this far inside it, drawn with the interior texture, so a piece never shows its hollow inside through a cut that could not be closed (0 = none). Metres.</summary>
        public double SkinDepth = 0.003;
    }

    /// <summary>One piece: its triangles about its own pivot (metres), where that pivot is in the model, and which way it flies.</summary>
    public sealed class FracturePiece
    {
        public int Index;
        public Vector3 Pivot, Size, Outward;
        public List<FractureTriangle> Triangles = new List<FractureTriangle>();
        public int BodyTriangles, CapTriangles, SkinTriangles;
        /// <summary>Closed cut loops capped, and open ones (a cut through a single-sided sheet) left open.</summary>
        public int CapsMade, OpenCuts;
        /// <summary>Its corners in model space (metres), for a hull.</summary>
        public IEnumerable<Vector3> PointsInModel => Triangles.SelectMany(t => new[] { t.A.P, t.B.P, t.C.P }).Select(p => p + Pivot);
    }

    /// <summary>
    /// Breaks a model into chunky pieces with closed cuts: K seeds by k-means++ and Lloyd on the area-weighted triangle centroids (fixed
    /// seed, so the same input breaks the same way), each piece the Voronoi cell of its seed. Cut loops are capped with one winding facing
    /// out of the piece (caps with both windings z-fight). The inner skin hides a piece's hollow inside where a model with its hidden faces
    /// deleted leaves cuts that cannot close. Works in centimetres internally.
    /// </summary>
    public static class FractureGeometry
    {
        public const int MinPieces = 2, MaxPieces = 32;
        /// <summary>A piece needs this much of the model's surface to be a body of its own.</summary>
        public const int MinBodyTriangles = 8;

        public static List<FracturePiece> Break(FractureSource source, FractureOptions options, List<string> notes = null)
        {
            if (source == null || source.Triangles.Count == 0) throw new InvalidOperationException("The model has no triangles to break.");
            int k = Math.Max(MinPieces, Math.Min(MaxPieces, options.Pieces));
            List<FractureTriangle> tris = source.Triangles.Select(t => Scale(t, 100f)).Where(t => Area(t) > 1e-8).ToList();
            if (tris.Count < k * 4) throw new InvalidOperationException("The model has too few triangles (" + tris.Count + ") to break into " + k + " pieces.");
            Dictionary<int, double> density = Densities(source, tris);

            Vector3 mn = new Vector3(float.MaxValue), mx = new Vector3(float.MinValue);
            foreach (FractureTriangle t in tris) foreach (Vector3 p in new[] { t.A.P, t.B.P, t.C.P }) { mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }
            Vector3 centre = (mn + mx) * 0.5f;

            List<Vector3> seeds = Seeds(tris, k, options.Seed);
            //Each seed's cell; a cell too small to be a body gives its seed up and the cells are cut again, so its triangles go to the neighbours
            List<(List<FractureTriangle> tris, int caps, int open)> cells;
            while (true)
            {
                cells = new List<(List<FractureTriangle>, int, int)>();
                for (int i = 0; i < seeds.Count; i++)
                {
                    List<FractureTriangle> piece = tris;
                    int caps = 0, open = 0;
                    for (int j = 0; j < seeds.Count; j++)
                    {
                        if (j == i) continue;
                        Vector3 n = Vector3.Normalize(seeds[j] - seeds[i]), m = (seeds[i] + seeds[j]) * 0.5f;
                        piece = Cut(piece, n, Vector3.Dot(n, m), source, density, options.Interior, ref caps, ref open);
                        if (piece.Count == 0) break;
                    }
                    cells.Add((piece, caps, open));
                }
                int smallest = -1, fewest = int.MaxValue;
                for (int i = 0; i < cells.Count; i++)
                {
                    int body = cells[i].tris.Count(t => t.Kind == FractureTriangleKind.Body);
                    if (body < MinBodyTriangles && body < fewest) { fewest = body; smallest = i; }
                }
                if (smallest < 0 || seeds.Count <= MinPieces) break;
                seeds.RemoveAt(smallest);
                notes?.Add("a cell holding " + fewest + " triangle(s) was merged into its neighbours");
            }
            List<FracturePiece> pieces = new List<FracturePiece>();
            foreach ((List<FractureTriangle> cell, int caps, int open) in cells)
            {
                List<FractureTriangle> piece = cell;
                if (!piece.Any(t => t.Kind == FractureTriangleKind.Body)) continue;
                if (options.SkinDepth > 0) piece.AddRange(Skin(piece, source, (float)(options.SkinDepth * 100.0)));

                //The pivot: the area-weighted centroid
                Vector3 pc = Vector3.Zero; double pa = 0;
                Vector3 pmn = new Vector3(float.MaxValue), pmx = new Vector3(float.MinValue);
                foreach (FractureTriangle t in piece)
                {
                    double ar = Area(t);
                    pc += (t.A.P + t.B.P + t.C.P) * (float)(ar / 3); pa += ar;
                    foreach (Vector3 p in new[] { t.A.P, t.B.P, t.C.P }) { pmn = Vector3.Min(pmn, p); pmx = Vector3.Max(pmx, p); }
                }
                pc = pa > 0 ? pc * (float)(1 / pa) : (pmn + pmx) * 0.5f;
                Vector3 outward = pc - centre;
                outward = outward.LengthSquared() > 1e-8f ? Vector3.Normalize(outward) : Vector3.UnitY;

                FracturePiece made = new FracturePiece()
                {
                    Index = pieces.Count,
                    Pivot = pc / 100f,
                    Size = (pmx - pmn) / 100f,
                    Outward = outward,
                    CapsMade = caps,
                    OpenCuts = open,
                };
                foreach (FractureTriangle t in piece)
                {
                    FractureTriangle o = Scale(t, 0.01f, pc);
                    made.Triangles.Add(o);
                    if (t.Kind == FractureTriangleKind.Body) made.BodyTriangles++;
                    else if (t.Kind == FractureTriangleKind.Cap) made.CapTriangles++;
                    else made.SkinTriangles++;
                }
                pieces.Add(made);
            }
            if (pieces.Count < 2) throw new InvalidOperationException("The model could not be broken into at least two pieces (it is too thin or too small for " + k + ").");
            return pieces;
        }

        #region Seeds
        private static List<Vector3> Seeds(List<FractureTriangle> tris, int k, int seed)
        {
            List<Vector3> cen = tris.Select(t => (t.A.P + t.B.P + t.C.P) * (1f / 3)).ToList();
            List<double> area = tris.Select(t => Area(t) + 1e-9).ToList();
            Random rnd = new Random(seed);
            List<Vector3> seeds = new List<Vector3>() { cen[rnd.Next(cen.Count)] };
            while (seeds.Count < k)
            {
                double[] d2 = new double[cen.Count];
                double tot = 0;
                for (int i = 0; i < cen.Count; i++) { d2[i] = seeds.Min(s => (double)Vector3.DistanceSquared(cen[i], s)) * area[i]; tot += d2[i]; }
                double r = rnd.NextDouble() * tot, acc = 0;
                int pick = 0;
                for (; pick < d2.Length - 1; pick++) { acc += d2[pick]; if (acc >= r) break; }
                seeds.Add(cen[pick]);
            }
            for (int it = 0; it < 30; it++)
            {
                Vector3[] sum = new Vector3[k];
                double[] w = new double[k];
                for (int i = 0; i < cen.Count; i++)
                {
                    int best = Nearest(seeds, cen[i]);
                    sum[best] += cen[i] * (float)area[i];
                    w[best] += area[i];
                }
                for (int s = 0; s < k; s++) if (w[s] > 0) seeds[s] = sum[s] * (float)(1 / w[s]);
            }
            //Two seeds that met would cut along a plane with no normal
            List<Vector3> distinct = new List<Vector3>();
            foreach (Vector3 s in seeds) if (!distinct.Any(o => Vector3.DistanceSquared(o, s) < 1e-4f)) distinct.Add(s);
            return distinct;
        }

        private static int Nearest(List<Vector3> s, Vector3 p)
        {
            int b = 0; float bd = float.MaxValue;
            for (int i = 0; i < s.Count; i++) { float d = Vector3.DistanceSquared(p, s[i]); if (d < bd) { bd = d; b = i; } }
            return b;
        }
        #endregion

        #region The cut
        private const double Eps = 1e-5;

        /// <summary>Keeps dot(n, p) &lt;= d; caps the cut's closed loops with one winding facing +n.</summary>
        private static List<FractureTriangle> Cut(List<FractureTriangle> tris, Vector3 n, double d, FractureSource source, Dictionary<int, double> density, FractureInterior interior, ref int caps, ref int open)
        {
            bool any = false;
            foreach (FractureTriangle t in tris)
                if (Vector3.Dot(n, t.A.P) - d > Eps || Vector3.Dot(n, t.B.P) - d > Eps || Vector3.Dot(n, t.C.P) - d > Eps) { any = true; break; }
            if (!any) return tris;

            List<FractureTriangle> o = new List<FractureTriangle>(tris.Count);
            List<(FractureVertex a, FractureVertex b, int group)> segs = new List<(FractureVertex, FractureVertex, int)>();
            //A surface modelled double-sided (two faces back to back: box flaps, panels) crosses the plane twice along the same line:
            //one segment, or the cut is capped twice on one plane and the two caps z-fight
            HashSet<string> segKeys = new HashSet<string>();
            foreach (FractureTriangle t in tris)
            {
                FractureVertex[] poly = { t.A, t.B, t.C };
                double[] dist = poly.Select(v => Vector3.Dot(n, v.P) - d).ToArray();
                if (dist.All(x => x <= Eps))
                {
                    o.Add(t);
                    //An edge lying in the plane, its triangle inside, is part of the cut's rim (a plane through the model's own vertices)
                    int on = dist.Count(x => Math.Abs(x) <= Eps);
                    if (on == 2 && t.Kind != FractureTriangleKind.Skin && !source.Groups[t.Group].Overlay)
                    {
                        int i0 = Array.FindIndex(dist, x => Math.Abs(x) <= Eps), i1 = Array.FindLastIndex(dist, x => Math.Abs(x) <= Eps);
                        string ka = Key(poly[i0].P), kb = Key(poly[i1].P);
                        if (ka != kb && segKeys.Add(string.CompareOrdinal(ka, kb) < 0 ? ka + "|" + kb : kb + "|" + ka))
                            segs.Add((poly[i0], poly[i1], t.Group));
                    }
                    continue;
                }
                if (dist.All(x => x >= -Eps)) continue;
                List<FractureVertex> outp = new List<FractureVertex>(4), cut = new List<FractureVertex>(2);
                for (int i = 0; i < 3; i++)
                {
                    FractureVertex a = poly[i], b = poly[(i + 1) % 3];
                    double da = dist[i], db = dist[(i + 1) % 3];
                    if (da <= Eps) outp.Add(a);
                    if ((da < -Eps && db > Eps) || (da > Eps && db < -Eps)) { FractureVertex x = Lerp(a, b, da, db); outp.Add(x); cut.Add(x); }
                    else if (Math.Abs(db) <= Eps && da > Eps) cut.Add(b);
                    else if (Math.Abs(da) <= Eps && db > Eps) cut.Add(a);
                }
                for (int i = 1; i + 1 < outp.Count; i++) o.Add(new FractureTriangle() { A = outp[0], B = outp[i], C = outp[i + 1], Group = t.Group, Kind = t.Kind });
                //Only the body closes a cut: overlays lie over it, and skin is added after the cuts
                if (t.Kind != FractureTriangleKind.Skin && !source.Groups[t.Group].Overlay && cut.Count == 2)
                {
                    string ka = Key(cut[0].P), kb = Key(cut[1].P);
                    if (ka != kb && segKeys.Add(string.CompareOrdinal(ka, kb) < 0 ? ka + "|" + kb : kb + "|" + ka))
                        segs.Add((cut[0], cut[1], t.Group));
                }
            }
            o.AddRange(Caps(segs, n, source, density, interior, ref caps, ref open));
            return o;
        }

        /// <summary>The point on a-b where the plane is, computed in a fixed endpoint order so both triangles of an edge get the same point.</summary>
        private static FractureVertex Lerp(FractureVertex a, FractureVertex b, double da, double db)
        {
            if (Less(b.P, a.P)) { FractureVertex tv = a; a = b; b = tv; double td = da; da = db; db = td; }
            float s = (float)(da / (da - db));
            FractureVertex r = new FractureVertex()
            {
                P = a.P + (b.P - a.P) * s,
                N = SafeNormal(a.N + (b.N - a.N) * s),
                C = a.C + (b.C - a.C) * s,
                Group = a.Group,
                UV = new Vector2[a.UV?.Length ?? 0],
            };
            for (int i = 0; i < r.UV.Length; i++)
                r.UV[i] = b.UV != null && i < b.UV.Length ? a.UV[i] + (b.UV[i] - a.UV[i]) * s : a.UV[i];
            return r;
        }

        private static bool Less(Vector3 a, Vector3 b) => a.X != b.X ? a.X < b.X : a.Y != b.Y ? a.Y < b.Y : a.Z < b.Z;
        private static string Key(Vector3 p) => Math.Round(p.X, 3).ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + Math.Round(p.Y, 3).ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + Math.Round(p.Z, 3).ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static List<FractureTriangle> Caps(List<(FractureVertex a, FractureVertex b, int group)> segs, Vector3 n, FractureSource source, Dictionary<int, double> density, FractureInterior interior, ref int caps, ref int openCount)
        {
            List<FractureTriangle> res = new List<FractureTriangle>();
            if (segs.Count < 3) { if (segs.Count != 0) openCount++; return res; }
            //Chain the segments (undirected) into loops
            Dictionary<string, List<int>> adj = new Dictionary<string, List<int>>();
            Dictionary<string, FractureVertex> pt = new Dictionary<string, FractureVertex>();
            for (int i = 0; i < segs.Count; i++)
                foreach (FractureVertex v in new[] { segs[i].a, segs[i].b })
                {
                    string k = Key(v.P);
                    if (!adj.ContainsKey(k)) { adj[k] = new List<int>(); pt[k] = v; }
                    adj[k].Add(i);
                }
            bool[] used = new bool[segs.Count];
            List<List<FractureVertex>> loops = new List<List<FractureVertex>>(), open = new List<List<FractureVertex>>();
            List<FractureVertex> Walk(string from)
            {
                List<FractureVertex> l = new List<FractureVertex>();
                string cur = from;
                for (int guard = 0; guard < segs.Count + 2; guard++)
                {
                    int next = -1;
                    foreach (int x in adj[cur]) if (!used[x]) { next = x; break; }
                    if (next < 0) break;
                    used[next] = true;
                    string a = Key(segs[next].a.P), b = Key(segs[next].b.P);
                    cur = a == cur ? b : a;
                    l.Add(pt[cur]);
                }
                return l;
            }
            for (int s0 = 0; s0 < segs.Count; s0++)
            {
                if (used[s0]) continue;
                used[s0] = true;
                string start = Key(segs[s0].a.P), end = Key(segs[s0].b.P);
                List<FractureVertex> chain = new List<FractureVertex>() { pt[start], pt[end] };
                chain.AddRange(Walk(end));
                if (Key(chain[chain.Count - 1].P) == start) { chain.RemoveAt(chain.Count - 1); if (chain.Count >= 3) loops.Add(chain); continue; }
                List<FractureVertex> back = Walk(start);
                back.Reverse();
                back.AddRange(chain);
                open.Add(back);
            }
            //A source that is not one closed shell leaves cut chains open; join their ends where the gap is small for the chains' size
            for (bool joined = true; joined && open.Count > 0;)
            {
                joined = false;
                for (int i = 0; i < open.Count && !joined; i++)
                {
                    List<FractureVertex> c = open[i];
                    double len = Length(c), best = Math.Max(2.0, 0.5 * len);
                    int bj = -1, mode = 0;
                    double self = (c[0].P - c[c.Count - 1].P).Length();
                    if (c.Count >= 3 && self < best) { best = self; bj = i; mode = 0; }
                    for (int j = 0; j < open.Count; j++)
                    {
                        if (j == i) continue;
                        List<FractureVertex> dd = open[j];
                        double lim = Math.Max(2.0, 0.5 * Math.Max(len, Length(dd)));
                        double g1 = (c[c.Count - 1].P - dd[0].P).Length(), g2 = (c[c.Count - 1].P - dd[dd.Count - 1].P).Length();
                        if (g1 < best && g1 < lim) { best = g1; bj = j; mode = 1; }
                        if (g2 < best && g2 < lim) { best = g2; bj = j; mode = 2; }
                    }
                    if (bj < 0) continue;
                    joined = true;
                    if (bj == i) { loops.Add(c); open.RemoveAt(i); }
                    else { List<FractureVertex> dd = open[bj]; if (mode == 2) dd.Reverse(); c.AddRange(dd); open.RemoveAt(bj); }
                }
            }
            openCount += open.Count;
            if (loops.Count == 0) return res;

            //The plane's 2D basis, (u, w, n) right-handed so CCW in 2D faces +n
            Vector3 u = Math.Abs(n.Y) < 0.9f ? Vector3.Normalize(Vector3.Cross(Vector3.UnitY, n)) : Vector3.Normalize(Vector3.Cross(Vector3.UnitX, n));
            Vector3 w = Vector3.Cross(n, u);
            List<List<double[]>> p2 = loops.Select(l => l.Select(v => new double[] { Vector3.Dot(v.P, u), Vector3.Dot(v.P, w) }).ToList()).ToList();
            List<double> area = p2.Select(Area2).ToList();
            int[] depth = new int[loops.Count];
            for (int i = 0; i < loops.Count; i++)
                for (int j = 0; j < loops.Count; j++)
                    if (i != j && Math.Abs(area[j]) > Math.Abs(area[i]) && Inside(p2[i][0], p2[j])) depth[i]++;
            for (int i = 0; i < loops.Count; i++)
            {
                if (depth[i] % 2 == 1 || Math.Abs(area[i]) < 1e-6) continue;
                (List<FractureVertex> v, List<double[]> p) outer = Orient(loops[i], p2[i], true);
                List<(List<FractureVertex> v, List<double[]> p)> holes = new List<(List<FractureVertex>, List<double[]>)>();
                for (int j = 0; j < loops.Count; j++)
                    if (depth[j] == depth[i] + 1 && Inside(p2[j][0], p2[i]) && Math.Abs(area[j]) < Math.Abs(area[i])) holes.Add(Orient(loops[j], p2[j], false));
                (List<FractureVertex> v, List<double[]> p) poly = Bridge(outer, holes);

                //Which material the cap takes: the group whose surface the rim runs along furthest
                Dictionary<int, double> along = new Dictionary<int, double>();
                for (int a = 0; a < poly.v.Count; a++)
                {
                    FractureVertex va = poly.v[a], vb = poly.v[(a + 1) % poly.v.Count];
                    along[va.Group] = (along.TryGetValue(va.Group, out double s) ? s : 0) + (vb.P - va.P).Length();
                }
                int group = along.OrderByDescending(o => o.Value).First().Key;
                List<FractureVertex> rim = poly.v.Where(o => o.Group == group).ToList();
                if (rim.Count == 0) rim = poly.v;
                Func<FractureVertex, FractureVertex> capVertex = InteriorTexturing(source.Groups[group], rim, n, u, w, density.TryGetValue(group, out double dn) ? dn : 0, interior);

                caps++;
                foreach ((int a, int b, int c) in EarClip(poly.p))
                {
                    FractureVertex A = capVertex(poly.v[a]), B = capVertex(poly.v[b]), C = capVertex(poly.v[c]);
                    if (Vector3.Dot(Vector3.Cross(B.P - A.P, C.P - A.P), n) < 0) { FractureVertex t = B; B = C; C = t; }
                    if (Vector3.Cross(B.P - A.P, C.P - A.P).Length() < 1e-9f) continue;
                    res.Add(new FractureTriangle() { A = A, B = B, C = C, Group = group, Kind = FractureTriangleKind.Cap });
                }
            }
            return res;
        }

        /// <summary>
        /// How a cap's corners are textured: Dark = every corner on the rim's darkest texel of the group's diffuse (one flat colour);
        /// Projected = the group's texture laid flat across the cap at its own texel density, continuing from a rim point.
        /// The other channels and the colour come from that rim point, the normal is the cut's.
        /// </summary>
        private static Func<FractureVertex, FractureVertex> InteriorTexturing(FractureGroup group, List<FractureVertex> rim, Vector3 n, Vector3 u, Vector3 w, double density, FractureInterior interior)
        {
            FractureVertex anchor = rim[0];
            bool dark = interior == FractureInterior.Dark && group.Luminance != null && rim[0].UV != null && rim[0].UV.Length > 0;
            if (dark)
            {
                float best = float.MaxValue;
                foreach (FractureVertex v in rim)
                {
                    float l = group.Luminance(v.UV[0]);
                    if (l < best) { best = l; anchor = v; }
                }
                return v => new FractureVertex() { P = v.P, N = n, UV = Clone(anchor.UV), C = anchor.C, Group = anchor.Group };
            }
            //Projected (also the fallback when the texture cannot be read): uv = anchor uv + (p - anchor) along u, w at the density
            float du = (float)density;
            return v =>
            {
                Vector2[] uv = Clone(anchor.UV);
                if (uv.Length > 0)
                {
                    Vector3 d = v.P - anchor.P;
                    uv[0] = anchor.UV[0] + new Vector2(Vector3.Dot(d, u) * du, Vector3.Dot(d, w) * du);
                }
                return new FractureVertex() { P = v.P, N = n, UV = uv, C = anchor.C, Group = anchor.Group };
            };
        }

        private static Vector2[] Clone(Vector2[] uv) => uv == null ? new Vector2[0] : (Vector2[])uv.Clone();

        /// <summary>Texture units per centimetre of each group's channel 0 (the square root of uv area over surface area).</summary>
        private static Dictionary<int, double> Densities(FractureSource source, List<FractureTriangle> tris)
        {
            Dictionary<int, double> uvArea = new Dictionary<int, double>(), area = new Dictionary<int, double>();
            foreach (FractureTriangle t in tris)
            {
                if (t.A.UV == null || t.A.UV.Length == 0) continue;
                Vector2 a = t.A.UV[0], b = t.B.UV[0], c = t.C.UV[0];
                double ua = Math.Abs((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y)) * 0.5;
                uvArea[t.Group] = (uvArea.TryGetValue(t.Group, out double x) ? x : 0) + ua;
                area[t.Group] = (area.TryGetValue(t.Group, out double y) ? y : 0) + Area(t);
            }
            Dictionary<int, double> density = new Dictionary<int, double>();
            foreach (int g in area.Keys) density[g] = area[g] > 0 ? Math.Sqrt(uvArea[g] / area[g]) : 0;
            return density;
        }

        /// <summary>The inner skin: each body triangle reversed, <paramref name="depth"/> cm in, unless the source has it back to back already.</summary>
        private static List<FractureTriangle> Skin(List<FractureTriangle> piece, FractureSource source, float depth)
        {
            Dictionary<string, int> faceKey = new Dictionary<string, int>();
            List<FractureTriangle> body = piece.Where(t => t.Kind == FractureTriangleKind.Body && !source.Groups[t.Group].Overlay).ToList();
            foreach (FractureTriangle t in body) { string k = TriKey(t); faceKey[k] = faceKey.TryGetValue(k, out int c) ? c + 1 : 1; }
            //The skin's texel per group: the darkest the piece's body touches (or the body's own uvs where the texture can't be read)
            Dictionary<int, FractureVertex?> darkest = new Dictionary<int, FractureVertex?>();
            foreach (IGrouping<int, FractureTriangle> g in body.GroupBy(t => t.Group))
            {
                FractureGroup group = source.Groups[g.Key];
                if (group.Luminance == null || g.First().A.UV == null || g.First().A.UV.Length == 0) { darkest[g.Key] = null; continue; }
                FractureVertex best = g.First().A; float bl = float.MaxValue;
                foreach (FractureTriangle t in g)
                    foreach (FractureVertex v in new[] { t.A, t.B, t.C })
                    {
                        float l = group.Luminance(v.UV[0]);
                        if (l < bl) { bl = l; best = v; }
                    }
                darkest[g.Key] = best;
            }
            List<FractureTriangle> skin = new List<FractureTriangle>();
            foreach (FractureTriangle t in body)
            {
                if (faceKey[TriKey(t)] != 1) continue;
                Vector3 fn = Vector3.Cross(t.B.P - t.A.P, t.C.P - t.A.P);
                if (fn.Length() < 1e-6f) continue;
                fn = Vector3.Normalize(fn);
                FractureVertex? dark = darkest.TryGetValue(t.Group, out FractureVertex? x) ? x : null;
                FractureVertex Inner(FractureVertex v) => new FractureVertex()
                {
                    P = v.P - fn * depth,
                    N = -fn,
                    UV = dark.HasValue ? Clone(dark.Value.UV) : Clone(v.UV),
                    C = dark.HasValue ? dark.Value.C : v.C,
                    Group = v.Group,
                };
                skin.Add(new FractureTriangle() { A = Inner(t.A), B = Inner(t.C), C = Inner(t.B), Group = t.Group, Kind = FractureTriangleKind.Skin });
            }
            return skin;
        }
        #endregion

        #region Polygons
        private static double Area2(List<double[]> p) { double s = 0; for (int i = 0; i < p.Count; i++) { double[] a = p[i], b = p[(i + 1) % p.Count]; s += a[0] * b[1] - b[0] * a[1]; } return s * 0.5; }

        private static bool Inside(double[] q, List<double[]> p)
        {
            bool c = false;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
                if ((p[i][1] > q[1]) != (p[j][1] > q[1]) && q[0] < (p[j][0] - p[i][0]) * (q[1] - p[i][1]) / (p[j][1] - p[i][1]) + p[i][0]) c = !c;
            return c;
        }

        private static (List<FractureVertex> v, List<double[]> p) Orient(List<FractureVertex> v, List<double[]> p, bool ccw)
        {
            if ((Area2(p) > 0) != ccw) { v = Enumerable.Reverse(v).ToList(); p = Enumerable.Reverse(p).ToList(); }
            return (v, p);
        }

        /// <summary>Holes joined to the outer loop by a two-way bridge from each hole's rightmost vertex to the nearest outer vertex that crosses nothing.</summary>
        private static (List<FractureVertex> v, List<double[]> p) Bridge((List<FractureVertex> v, List<double[]> p) outer, List<(List<FractureVertex> v, List<double[]> p)> holes)
        {
            List<FractureVertex> ov = new List<FractureVertex>(outer.v);
            List<double[]> op = new List<double[]>(outer.p);
            foreach ((List<FractureVertex> v, List<double[]> p) h in holes.OrderByDescending(h => h.p.Max(q => q[0])))
            {
                int hi = 0;
                for (int k = 1; k < h.p.Count; k++) if (h.p[k][0] > h.p[hi][0]) hi = k;
                double[] hp = h.p[hi];
                int best = -1; double bd = double.MaxValue;
                List<List<double[]>> others = holes.Where(x => x.p != h.p).Select(x => x.p).ToList();
                for (int k = 0; k < op.Count; k++)
                {
                    double dd = Sq2(op[k], hp);
                    if (dd >= bd) continue;
                    if (Crosses(op, hp, op[k], k) || CrossesLoop(h.p, hp, op[k]) || others.Any(x => CrossesLoop(x, hp, op[k]))) continue;
                    bd = dd; best = k;
                }
                if (best < 0) best = op.Select((q, k) => (Sq2(q, hp), k)).Min().Item2;
                List<FractureVertex> nv = new List<FractureVertex>();
                List<double[]> np = new List<double[]>();
                for (int k = 0; k <= best; k++) { nv.Add(ov[k]); np.Add(op[k]); }
                for (int k = 0; k <= h.p.Count; k++) { int m = (hi + k) % h.p.Count; nv.Add(h.v[m]); np.Add(h.p[m]); }
                nv.Add(ov[best]); np.Add(op[best]);
                for (int k = best + 1; k < op.Count; k++) { nv.Add(ov[k]); np.Add(op[k]); }
                ov = nv; op = np;
            }
            return (ov, op);
        }

        private static bool Crosses(List<double[]> loop, double[] a, double[] b, int skip)
        {
            for (int i = 0; i < loop.Count; i++) { int j = (i + 1) % loop.Count; if (i == skip || j == skip) continue; if (SegX(a, b, loop[i], loop[j])) return true; }
            return false;
        }

        private static bool CrossesLoop(List<double[]> loop, double[] a, double[] b)
        {
            for (int i = 0; i < loop.Count; i++)
            {
                int j = (i + 1) % loop.Count;
                if (Same(loop[i], a) || Same(loop[j], a) || Same(loop[i], b) || Same(loop[j], b)) continue;
                if (SegX(a, b, loop[i], loop[j])) return true;
            }
            return false;
        }

        private static bool Same(double[] a, double[] b) => Math.Abs(a[0] - b[0]) < 1e-9 && Math.Abs(a[1] - b[1]) < 1e-9;
        private static bool Between(double[] a, double[] b, double[] c) =>
            (b[0] - a[0]) * (c[0] - a[0]) + (b[1] - a[1]) * (c[1] - a[1]) > 0 && (b[0] - c[0]) * (a[0] - c[0]) + (b[1] - c[1]) * (a[1] - c[1]) > 0;
        private static double Cr(double[] o, double[] a, double[] b) => (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0]);
        private static bool SegX(double[] a, double[] b, double[] c, double[] d) => Cr(a, b, c) * Cr(a, b, d) < 0 && Cr(c, d, a) * Cr(c, d, b) < 0;

        /// <summary>Ear clipping of a CCW polygon (bridges make repeated points, so containment skips coincident ones); a stuck remainder is fanned.</summary>
        private static List<(int, int, int)> EarClip(List<double[]> p)
        {
            List<int> idx = Enumerable.Range(0, p.Count).ToList();
            List<(int, int, int)> o = new List<(int, int, int)>();
            int guard = 0;
            while (idx.Count > 3 && guard++ < 20000)
            {
                bool cut = false;
                for (int k = 0; k < idx.Count; k++)
                {
                    int ia = idx[(k + idx.Count - 1) % idx.Count], ib = idx[k], ic = idx[(k + 1) % idx.Count];
                    double cr = Cr(p[ia], p[ib], p[ic]);
                    if (cr <= 1e-12)
                    {
                        //A tip in line with its neighbours and between them: clipping it loses nothing, where leaving it in jams the
                        //clipping and the fanned rest can fold over itself
                        double ab = Math.Sqrt(Sq2(p[ia], p[ib])), bc = Math.Sqrt(Sq2(p[ib], p[ic]));
                        if (Math.Abs(cr) <= 1e-6 * ab * bc + 1e-12 && Between(p[ia], p[ib], p[ic])) { o.Add((ia, ib, ic)); idx.RemoveAt(k); cut = true; break; }
                        continue;
                    }
                    bool any = false;
                    foreach (int m in idx)
                    {
                        if (m == ia || m == ib || m == ic || Same(p[m], p[ia]) || Same(p[m], p[ib]) || Same(p[m], p[ic])) continue;
                        if (Cr(p[ia], p[ib], p[m]) >= 0 && Cr(p[ib], p[ic], p[m]) >= 0 && Cr(p[ic], p[ia], p[m]) >= 0) { any = true; break; }
                    }
                    if (any) continue;
                    o.Add((ia, ib, ic));
                    idx.RemoveAt(k);
                    cut = true;
                    break;
                }
                if (!cut) break;
            }
            if (idx.Count == 3) o.Add((idx[0], idx[1], idx[2]));
            else for (int k = 1; k + 1 < idx.Count; k++) o.Add((idx[0], idx[k], idx[k + 1]));
            return o;
        }
        #endregion

        #region Small helpers
        private static double Area(FractureTriangle t) => Vector3.Cross(t.B.P - t.A.P, t.C.P - t.A.P).Length() * 0.5;
        private static double Sq2(double[] a, double[] b) => (a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]);
        private static double Length(List<FractureVertex> c) { double s = 0; for (int i = 1; i < c.Count; i++) s += (c[i].P - c[i - 1].P).Length(); return s; }
        private static string TriKey(FractureTriangle t) => string.Join("|", new[] { Key(t.A.P), Key(t.B.P), Key(t.C.P) }.OrderBy(s => s, StringComparer.Ordinal));
        private static Vector3 SafeNormal(Vector3 v) => v.LengthSquared() > 1e-12f ? Vector3.Normalize(v) : Vector3.UnitY;

        private static FractureTriangle Scale(FractureTriangle t, float s, Vector3? minus = null)
        {
            Vector3 off = minus ?? Vector3.Zero;
            FractureVertex V(FractureVertex v) => new FractureVertex() { P = (v.P - off) * s, N = v.N, UV = v.UV, C = v.C, Group = v.Group };
            return new FractureTriangle() { A = V(t.A), B = V(t.B), C = V(t.C), Group = t.Group, Kind = t.Kind };
        }
        #endregion
    }
}
