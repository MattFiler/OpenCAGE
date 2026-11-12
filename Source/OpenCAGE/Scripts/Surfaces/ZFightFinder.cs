using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCAGE.Surfaces
{
    /// <summary>The draw pass a surface is in, as far as two surfaces fighting over the same depth goes.</summary>
    public enum ZPass
    {
        /// <summary>The lit opaque pass (priority 60 and up): world geometry, props.</summary>
        Opaque,
        /// <summary>The mesh-decal pass (priority 52): blended onto the surface behind it.</summary>
        Decal,
        /// <summary>The see-through passes (39 / 42): glass, water, blended over what is behind them.</summary>
        SeeThrough,
    }

    /// <summary>
    /// One drawn surface for the z-fighting search: a submesh as it is placed (its triangles in one space shared by every surface of
    /// the search - world space for a level, the model's own space for one model). <see cref="Group"/> is what was placed together
    /// (a placement in a level, a component of one model): the search can take or leave pairs inside one group.
    /// </summary>
    public sealed class ZSurface
    {
        public int Group;
        /// <summary>What it is ("Floor_1 / mesh 2 (FLOOR_TILE)").</summary>
        public string Label;
        /// <summary>The group it belongs to ("root/Room_A/Floor_1", "component 0").</summary>
        public string GroupLabel;
        public string Material;
        public ZPass Pass;
        /// <summary>Drawn from both sides: a back-to-back pair can fight too.</summary>
        public bool DoubleSided;
        public Vector3[] Positions;
        /// <summary>Index triples into <see cref="Positions"/>.</summary>
        public int[] Triangles;
        /// <summary>Whatever the caller needs to repair it (placement, model, submesh).</summary>
        public object Tag;

        /// <summary>Its place in the list the search was given.</summary>
        public int Index { get; internal set; }
        public Vector3 Min { get; private set; }
        public Vector3 Max { get; private set; }
        /// <summary>Its triangles' area, m2.</summary>
        public double Area { get; private set; }
        public int TriangleCount => Triangles == null ? 0 : Triangles.Length / 3;

        public void Prepare()
        {
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            double area = 0;
            if (Positions != null && Triangles != null)
            {
                for (int t = 0; t + 2 < Triangles.Length; t += 3)
                {
                    Vector3 a = Positions[Triangles[t]], b = Positions[Triangles[t + 1]], c = Positions[Triangles[t + 2]];
                    min = Vector3.Min(min, Vector3.Min(a, Vector3.Min(b, c)));
                    max = Vector3.Max(max, Vector3.Max(a, Vector3.Max(b, c)));
                    area += Vector3.Cross(b - a, c - a).Length() * 0.5;
                }
            }
            Min = min;
            Max = max;
            Area = area;
        }

        public override string ToString() => Label;
    }

    /// <summary>What the search counts as two surfaces fighting.</summary>
    public sealed class ZFightOptions
    {
        /// <summary>Surfaces this close (m, along their normal) draw at the same depth somewhere on screen: 4 mm by default.</summary>
        public float Gap = 0.004f;
        /// <summary>Surfaces this near parallel (degrees) or nearer: further apart in angle they cross in a line, not a patch.</summary>
        public float MaxAngle = 3f;
        /// <summary>A pair overlapping less than this (m2) is left out: 4 cm2 by default.</summary>
        public double MinArea = 0.0004;
        /// <summary>Pairs between surfaces of different groups (placements).</summary>
        public bool AcrossGroups = true;
        /// <summary>Pairs between surfaces of one group (a model's own submeshes).</summary>
        public bool InsideGroups = true;
        /// <summary>A surface overlapping itself (duplicated faces in one mesh).</summary>
        public bool SameSurface = false;
        /// <summary>Back-to-back pairs where neither is double-sided never show each other: left out unless this is set.</summary>
        public bool BackToBack = false;
        /// <summary>A level search also takes models hidden at the start (show_on_reset off), which a script may show later.</summary>
        public bool IncludeHidden = false;
        /// <summary>Grid cell (m) the triangles are sorted into.</summary>
        public float Cell = 1f;
        /// <summary>Tiles (m, across X and Z) the search works through one at a time.</summary>
        public float Tile = 64f;
        /// <summary>Overlap outlines kept per fight, for pictures.</summary>
        public int MaxPatches = 400;

        public ZFightOptions Clone() => (ZFightOptions)MemberwiseClone();
    }

    /// <summary>
    /// Two surfaces drawn over each other at (nearly) the same depth: the GPU picks either from pixel to pixel and frame to frame, so
    /// the overlap flickers. <see cref="A"/> lies on <see cref="B"/> (the decal, the panel, the smaller or more covered of the two):
    /// pushing A out along <see cref="Normal"/>, or sinking B back, separates them.
    /// </summary>
    public sealed class ZFight
    {
        public int Id;
        public ZSurface A, B;
        /// <summary>Overlap within the gap, m2.</summary>
        public double Area;
        /// <summary>A's offset in front of B along <see cref="Normal"/> over the overlap (m): negative is behind.</summary>
        public float OffsetMin, OffsetMax, OffsetMean;
        /// <summary>A's offset in front of B over the whole of where they overlap, not just where they are within the gap: for a crossing,
        /// how far A goes behind B (a rigid move has to clear that). The same as the offsets for two parallel surfaces.</summary>
        public float DeepestMin, DeepestMax;
        /// <summary>B's facing (unit): the way A is pushed out.</summary>
        public Vector3 Normal;
        public Vector3 Centre, Min, Max;
        /// <summary>The two face opposite ways (one is double-sided).</summary>
        public bool BackToBack;
        /// <summary>The two surfaces' overlap over every way they face (a fight is one way).</summary>
        public double PairArea;
        /// <summary>Each lies wholly on the other: one thing twice in one place (no move separates two copies; delete one).</summary>
        public bool Copies;
        /// <summary>A's and B's triangles (indices into each one's triangle list) that take part.</summary>
        public HashSet<int> TrianglesA = new HashSet<int>(), TrianglesB = new HashSet<int>();
        /// <summary>The overlap's outlines (on A), for pictures.</summary>
        public List<Vector3[]> Patches = new List<Vector3[]>();

        /// <summary>How much of each one's surface is in the overlap (0-1).</summary>
        public double CoverA => A.Area > 0 ? Math.Min(1, Area / A.Area) : 0;
        public double CoverB => B.Area > 0 ? Math.Min(1, Area / B.Area) : 0;
        public bool SameGroup => A.Group == B.Group;
        public bool SameSurface => A == B;

        /// <summary>coplanar (within half a millimetre of each other all over: flickers close up), crossing (one passes through the other at a
        /// shallow angle inside the overlap: a flickering band along where they cross), near (apart, within the gap: flickers further away).</summary>
        public string Kind => DeepestMin < -CoplanarTolerance && DeepestMax > CoplanarTolerance ? "crossing" : Math.Max(Math.Abs(DeepestMin), Math.Abs(DeepestMax)) <= CoplanarTolerance ? "coplanar" : "near";

        /// <summary>Surfaces within this of each other (m) count as lying in one plane.</summary>
        public const float CoplanarTolerance = 0.0005f;

        public override string ToString() => "#" + Id + " " + A.Label + " on " + B.Label + " " + Area.ToString("0.###") + " m2";
    }

    /// <summary>
    /// Finds z-fighting: pairs of surfaces that lie in (nearly) one plane, face the same way and overlap, closer than the gap. Each close,
    /// near-parallel pair of triangles is intersected exactly (one laid on the other's plane, cut by its edges and by the gap band), so areas
    /// and offsets are measured, not sampled. Works tile by tile through a grid of cells; each pair is measured once. Pure geometry.
    /// </summary>
    public static class ZFightFinder
    {
        public sealed class Stats
        {
            public int Surfaces, Triangles, Tiles, Pairs, Fights;
            public double Seconds;
            public override string ToString() => Surfaces + " surfaces, " + Triangles.ToString("N0") + " triangles, " + Tiles + " tiles, " + Pairs.ToString("N0") + " pairs measured, " + Fights + " fights, " + Seconds.ToString("0.00") + " s";
        }

        private const float NormalBin = 0.1f;

        private sealed class Acc
        {
            public int S1, S2;
            public double Area, SumH, BackArea;
            public float HMin = float.MaxValue, HMax = float.MinValue, FMin = float.MaxValue, FMax = float.MinValue;
            public Vector3 N1, N2, Centre;
            public Vector3 Min = new Vector3(float.MaxValue), Max = new Vector3(float.MinValue);
            public HashSet<int> T1 = new HashSet<int>(), T2 = new HashSet<int>();
            public List<Vector3[]> Patches = new List<Vector3[]>();

            public void Merge(Acc o, int maxPatches)
            {
                Area += o.Area; SumH += o.SumH; BackArea += o.BackArea;
                HMin = Math.Min(HMin, o.HMin); HMax = Math.Max(HMax, o.HMax); FMin = Math.Min(FMin, o.FMin); FMax = Math.Max(FMax, o.FMax);
                N1 += o.N1; N2 += o.N2; Centre += o.Centre;
                Min = Vector3.Min(Min, o.Min); Max = Vector3.Max(Max, o.Max);
                T1.UnionWith(o.T1); T2.UnionWith(o.T2);
                foreach (Vector3[] p in o.Patches) { if (Patches.Count >= maxPatches) break; Patches.Add(p); }
            }
        }

        /// <summary>Every fight among the surfaces, largest first, numbered from 1.</summary>
        public static List<ZFight> Find(IList<ZSurface> surfaces, ZFightOptions options, out Stats stats, CancellationToken cancel = default(CancellationToken), Action<double> progress = null)
        {
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            options = options ?? new ZFightOptions();
            stats = new Stats() { Surfaces = surfaces.Count };
            List<ZSurface> list = surfaces.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                list[i].Index = i;
                list[i].Prepare();
                stats.Triangles += list[i].TriangleCount;
            }
            List<ZFight> fights = new List<ZFight>();
            if (list.Count == 0) { stats.Seconds = watch.Elapsed.TotalSeconds; return fights; }

            float gap = Math.Max(0, options.Gap);
            float cell = Math.Max(0.05f, options.Cell);
            //Tiles are whole cells, so a cell belongs to one tile
            int cellsPerTile = Math.Max(1, (int)Math.Round(Math.Max(cell, options.Tile) / cell));
            float tile = cellsPerTile * cell;
            bool anyDoubleSided = options.BackToBack || list.Any(o => o.DoubleSided);

            //Surfaces by the tiles they reach
            Dictionary<(int, int), List<int>> tiles = new Dictionary<(int, int), List<int>>();
            for (int s = 0; s < list.Count; s++)
            {
                ZSurface surface = list[s];
                if (surface.TriangleCount == 0 || surface.Min.X > surface.Max.X) continue;
                int x0 = (int)Math.Floor((surface.Min.X - gap) / tile), x1 = (int)Math.Floor((surface.Max.X + gap) / tile);
                int z0 = (int)Math.Floor((surface.Min.Z - gap) / tile), z1 = (int)Math.Floor((surface.Max.Z + gap) / tile);
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        if (!tiles.TryGetValue((x, z), out List<int> l)) tiles[(x, z)] = l = new List<int>();
                        l.Add(s);
                    }
            }

            //A tile with one surface can only hold a fight of that surface with itself
            List<KeyValuePair<(int, int), List<int>>> work = tiles.Where(o => o.Value.Count > 1 || options.SameSurface).ToList();
            stats.Tiles = work.Count;
            Dictionary<(long, int), Acc> all = new Dictionary<(long, int), Acc>();
            object gate = new object();
            int done = 0;
            long pairs = 0;
            ParallelOptions parallel = new ParallelOptions() { CancellationToken = cancel, MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
            Parallel.ForEach(work, parallel, entry =>
            {
                Dictionary<(long, int), Acc> local = new Dictionary<(long, int), Acc>();
                long measured = SearchTile(list, entry.Value, entry.Key.Item1, entry.Key.Item2, cellsPerTile, cell, gap, options, anyDoubleSided, local, cancel);
                lock (gate)
                {
                    pairs += measured;
                    foreach (KeyValuePair<(long, int), Acc> kv in local)
                    {
                        if (all.TryGetValue(kv.Key, out Acc had)) had.Merge(kv.Value, options.MaxPatches);
                        else all[kv.Key] = kv.Value;
                    }
                    done++;
                    progress?.Invoke(done / (double)work.Count);
                }
            });
            stats.Pairs = (int)Math.Min(int.MaxValue, pairs);

            //One fight per pair of surfaces and way they face (a decal wrapped round a corner is two, each pushed its own way); which lies
            //on which is decided for the pair as a whole
            foreach (IGrouping<long, KeyValuePair<(long, int), Acc>> pair in all.GroupBy(o => o.Key.Item1))
            {
                Acc first = pair.First().Value;
                ZSurface s1 = list[first.S1], s2 = list[first.S2];
                double total = pair.Sum(o => o.Value.Area);
                bool aIs1 = FirstLiesOnSecond(s1, s2, total);
                bool copies = s1.Area > 0 && s2.Area > 0 && total >= 0.95 * s1.Area && total >= 0.95 * s2.Area;
                foreach (KeyValuePair<(long, int), Acc> kv in pair)
                {
                    if (kv.Value.Area < options.MinArea) continue;
                    ZFight fight = ToFight(s1, s2, kv.Value, aIs1);
                    fight.PairArea = total;
                    fight.Copies = copies;
                    fights.Add(fight);
                }
            }
            fights.Sort((x, y) => y.Area.CompareTo(x.Area));
            for (int i = 0; i < fights.Count; i++) fights[i].Id = i + 1;
            stats.Fights = fights.Count;
            stats.Seconds = watch.Elapsed.TotalSeconds;
            return fights;
        }

        /// <summary>Which of the two lies on the other: the decal over the surface, else the one more covered by the overlap, else the smaller.</summary>
        private static bool FirstLiesOnSecond(ZSurface s1, ZSurface s2, double area)
        {
            double c1 = s1.Area > 0 ? area / s1.Area : 0, c2 = s2.Area > 0 ? area / s2.Area : 0;
            if (s1.Pass == ZPass.Decal && s2.Pass != ZPass.Decal) return true;
            if (s2.Pass == ZPass.Decal && s1.Pass != ZPass.Decal) return false;
            if (Math.Abs(c1 - c2) > 0.1 * Math.Max(c1, c2)) return c1 > c2;
            return s1.Area <= s2.Area;
        }

        private static ZFight ToFight(ZSurface s1, ZSurface s2, Acc acc, bool aIs1)
        {
            float mean = acc.Area > 0 ? (float)(acc.SumH / acc.Area) : 0;
            ZFight fight = new ZFight()
            {
                A = aIs1 ? s1 : s2,
                B = aIs1 ? s2 : s1,
                Area = acc.Area,
                //The offsets were measured as s1 in front of s2
                OffsetMin = aIs1 ? acc.HMin : -acc.HMax,
                OffsetMax = aIs1 ? acc.HMax : -acc.HMin,
                OffsetMean = aIs1 ? mean : -mean,
                DeepestMin = aIs1 ? acc.FMin : -acc.FMax,
                DeepestMax = aIs1 ? acc.FMax : -acc.FMin,
                Centre = acc.Area > 0 ? acc.Centre / (float)acc.Area : (acc.Min + acc.Max) * 0.5f,
                Min = acc.Min,
                Max = acc.Max,
                BackToBack = acc.BackArea > acc.Area * 0.5,
                TrianglesA = aIs1 ? acc.T1 : acc.T2,
                TrianglesB = aIs1 ? acc.T2 : acc.T1,
                Patches = acc.Patches,
            };
            Vector3 n = aIs1 ? acc.N2 : acc.N1;
            fight.Normal = n.LengthSquared() > 1e-20f ? Vector3.Normalize(n) : Vector3.UnitY;
            //Back to back the offsets have no front: their size is what counts
            if (fight.BackToBack)
            {
                float lo = Math.Min(Math.Abs(fight.OffsetMin), Math.Abs(fight.OffsetMax)), hi = Math.Max(Math.Abs(fight.OffsetMin), Math.Abs(fight.OffsetMax));
                if (fight.OffsetMin <= 0 && fight.OffsetMax >= 0) lo = 0;
                fight.OffsetMin = lo; fight.OffsetMax = hi; fight.OffsetMean = Math.Abs(fight.OffsetMean);
            }
            return fight;
        }

        #region One tile
        private sealed class TileTris
        {
            public List<int> Surf = new List<int>(), Tri = new List<int>();
            public List<Vector3> A = new List<Vector3>(), B = new List<Vector3>(), C = new List<Vector3>(), N = new List<Vector3>(), Min = new List<Vector3>(), Max = new List<Vector3>();
            public List<int> Bin = new List<int>();
            public int Count => Surf.Count;
        }

        private static long SearchTile(List<ZSurface> surfaces, List<int> members, int tileX, int tileZ, int cellsPerTile, float cell, float gap, ZFightOptions options, bool anyDoubleSided, Dictionary<(long, int), Acc> accs, CancellationToken cancel)
        {
            float tile = cellsPerTile * cell;
            float tx0 = tileX * tile, tz0 = tileZ * tile, tx1 = tx0 + tile, tz1 = tz0 + tile;
            int cx0 = tileX * cellsPerTile, cz0 = tileZ * cellsPerTile, cx1 = cx0 + cellsPerTile - 1, cz1 = cz0 + cellsPerTile - 1;
            Vector3 grow = new Vector3(gap);

            //This tile's triangles
            TileTris t = new TileTris();
            foreach (int s in members)
            {
                ZSurface surface = surfaces[s];
                Vector3[] p = surface.Positions;
                int[] idx = surface.Triangles;
                for (int k = 0; k + 2 < idx.Length; k += 3)
                {
                    Vector3 a = p[idx[k]], b = p[idx[k + 1]], c = p[idx[k + 2]];
                    Vector3 min = Vector3.Min(a, Vector3.Min(b, c)) - grow, max = Vector3.Max(a, Vector3.Max(b, c)) + grow;
                    if (max.X < tx0 || min.X > tx1 || max.Z < tz0 || min.Z > tz1) continue;
                    Vector3 n = Vector3.Cross(b - a, c - a);
                    float len = n.Length();
                    if (!(len > 1e-10f)) continue;
                    n /= len;
                    t.Surf.Add(s); t.Tri.Add(k / 3);
                    t.A.Add(a); t.B.Add(b); t.C.Add(c); t.N.Add(n); t.Min.Add(min); t.Max.Add(max);
                    t.Bin.Add(BinOf(n));
                }
            }
            if (t.Count < 2) return 0;

            //Into cells (this tile's columns only)
            Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();
            for (int i = 0; i < t.Count; i++)
            {
                Vector3 min = t.Min[i], max = t.Max[i];
                int x0 = Math.Max(cx0, Floor(min.X, cell)), x1 = Math.Min(cx1, Floor(max.X, cell));
                int z0 = Math.Max(cz0, Floor(min.Z, cell)), z1 = Math.Min(cz1, Floor(max.Z, cell));
                int y0 = Floor(min.Y, cell), y1 = Floor(max.Y, cell);
                for (int x = x0; x <= x1; x++)
                    for (int y = y0; y <= y1; y++)
                        for (int z = z0; z <= z1; z++)
                        {
                            long key = Key(x, y, z);
                            if (!cells.TryGetValue(key, out List<int> l)) cells[key] = l = new List<int>(4);
                            l.Add(i);
                        }
            }

            float cosMax = (float)Math.Cos(Math.Max(0, Math.Min(45, options.MaxAngle)) * Math.PI / 180.0);
            int reach = Math.Max(1, (int)Math.Ceiling(2 * Math.Sin(Math.Max(0, Math.Min(45, options.MaxAngle)) * Math.PI / 360.0) / NormalBin + 1e-6));
            long measured = 0;
            int visited = 0;
            foreach (KeyValuePair<long, List<int>> entry in cells)
            {
                List<int> here = entry.Value;
                if (here.Count < 2) continue;
                if ((++visited & 1023) == 0) cancel.ThrowIfCancellationRequested();
                (int ox, int oy, int oz) = Unkey(entry.Key);
                if (here.Count <= 24)
                {
                    for (int a = 0; a < here.Count; a++)
                        for (int b = a + 1; b < here.Count; b++)
                            measured += TryPair(surfaces, t, here[a], here[b], cell, gap, cosMax, options, ox, oy, oz, accs);
                    continue;
                }
                //Many triangles here: only those whose normals are near each other (or opposite, where back to back counts) meet
                Dictionary<int, List<int>> bins = new Dictionary<int, List<int>>();
                foreach (int i in here)
                {
                    if (!bins.TryGetValue(t.Bin[i], out List<int> l)) bins[t.Bin[i]] = l = new List<int>();
                    l.Add(i);
                }
                foreach (int i in here)
                {
                    (int bx, int by, int bz) = UnBin(t.Bin[i]);
                    for (int side = 0; side < (anyDoubleSided ? 2 : 1); side++)
                    {
                        int sx = side == 0 ? bx : -bx, sy = side == 0 ? by : -by, sz = side == 0 ? bz : -bz;
                        for (int dx = -reach; dx <= reach; dx++)
                            for (int dy = -reach; dy <= reach; dy++)
                                for (int dz = -reach; dz <= reach; dz++)
                                {
                                    if (!bins.TryGetValue(PackBin(sx + dx, sy + dy, sz + dz), out List<int> l)) continue;
                                    foreach (int j in l)
                                        if (j > i) measured += TryPair(surfaces, t, i, j, cell, gap, cosMax, options, ox, oy, oz, accs);
                                }
                    }
                }
            }
            return measured;
        }

        /// <summary>Measure one pair of triangles, if it is one to measure here. Returns 1 when measured.</summary>
        private static int TryPair(List<ZSurface> surfaces, TileTris t, int i, int j, float cell, float gap, float cosMax, ZFightOptions options, int ox, int oy, int oz, Dictionary<(long, int), Acc> accs)
        {
            int si = t.Surf[i], sj = t.Surf[j];
            ZSurface a = surfaces[si], b = surfaces[sj];
            if (si == sj)
            {
                //Neighbours in one mesh meet at an edge, which measures as no area: only a face folded or copied over another counts
                if (!options.SameSurface) return 0;
            }
            else if (a.Group == b.Group ? !options.InsideGroups : !options.AcrossGroups)
                return 0;

            float cos = Vector3.Dot(t.N[i], t.N[j]);
            bool back;
            if (cos >= cosMax) back = false;
            else if (cos <= -cosMax && (options.BackToBack || a.DoubleSided || b.DoubleSided)) back = true;
            else return 0;

            Vector3 minI = t.Min[i], maxI = t.Max[i], minJ = t.Min[j], maxJ = t.Max[j];
            if (maxI.X < minJ.X || maxJ.X < minI.X || maxI.Y < minJ.Y || maxJ.Y < minI.Y || maxI.Z < minJ.Z || maxJ.Z < minI.Z) return 0;
            //Measured once: in the cell where the two boxes start to overlap
            Vector3 start = Vector3.Max(minI, minJ);
            if (Floor(start.X, cell) != ox || Floor(start.Y, cell) != oy || Floor(start.Z, cell) != oz) return 0;

            //Within the gap anywhere? (the distance to j's plane is linear across i)
            Vector3 nj = t.N[j];
            float da = Vector3.Dot(t.A[i] - t.A[j], nj), db = Vector3.Dot(t.B[i] - t.A[j], nj), dc = Vector3.Dot(t.C[i] - t.A[j], nj);
            if ((da > gap && db > gap && dc > gap) || (da < -gap && db < -gap && dc < -gap)) return 1;

            //i's plane, from its first corner
            Vector3 origin = t.A[i], ni = t.N[i];
            Vector3 u = t.B[i] - origin;
            float ul = u.Length();
            if (!(ul > 1e-8f)) return 1;
            u /= ul;
            Vector3 v = Vector3.Cross(ni, u);
            List<Vector2> poly = new List<Vector2>(8) { Vector2.Zero, Flat(t.B[i] - origin, u, v), Flat(t.C[i] - origin, u, v) };
            if (SignedArea(poly) < 0) poly.Reverse();
            Vector2 q0 = Flat(t.A[j] - origin, u, v), q1 = Flat(t.B[j] - origin, u, v), q2 = Flat(t.C[j] - origin, u, v);
            float jArea = Cross(q1 - q0, q2 - q0);
            if (Math.Abs(jArea) < 1e-12f) return 1;
            if (jArea < 0) { Vector2 swap = q1; q1 = q2; q2 = swap; }
            poly = ClipEdge(poly, q0, q1);
            if (poly.Count >= 3) poly = ClipEdge(poly, q1, q2);
            if (poly.Count >= 3) poly = ClipEdge(poly, q2, q0);
            if (poly.Count < 3) return 1;
            //...and to the band within the gap of j's plane: h = i's offset in front of j along j's normal, linear on i's plane. The whole
            //overlap's offsets are kept too: how deep a crossing goes, which is how far a rigid move has to go to clear it
            float h0 = da, hx = Vector3.Dot(u, nj), hy = Vector3.Dot(v, nj);
            float fullMin = float.MaxValue, fullMax = float.MinValue;
            foreach (Vector2 q in poly) { float h = h0 + hx * q.X + hy * q.Y; fullMin = Math.Min(fullMin, h); fullMax = Math.Max(fullMax, h); }
            poly = ClipLinear(poly, gap - h0, -hx, -hy);
            if (poly.Count >= 3) poly = ClipLinear(poly, gap + h0, hx, hy);
            if (poly.Count < 3) return 1;
            float area = SignedArea(poly);
            if (!(area > 1e-10f)) return 1;

            //Accumulated as the lower-numbered surface in front of the other
            bool flip = si > sj;
            int s1 = flip ? sj : si, s2 = flip ? si : sj;
            //...and by the way the second faces (coarsely): each way is a fight of its own
            Vector3 way = flip ? ni : nj;
            (long, int) key = (((long)s1 << 32) | (uint)s2, PackBin((int)Math.Round(way.X * 2), (int)Math.Round(way.Y * 2), (int)Math.Round(way.Z * 2)));
            if (!accs.TryGetValue(key, out Acc acc)) accs[key] = acc = new Acc() { S1 = s1, S2 = s2 };
            Vector2 centroid = Centroid(poly, area);
            float hc = h0 + hx * centroid.X + hy * centroid.Y;
            float scale = back ? 1f : 1f / Math.Max(cosMax, Math.Abs(cos));
            acc.Area += area;
            acc.SumH += (flip ? -hc * scale : hc) * area;
            acc.FMin = Math.Min(acc.FMin, flip ? -fullMax * scale : fullMin);
            acc.FMax = Math.Max(acc.FMax, flip ? -fullMin * scale : fullMax);
            if (back) acc.BackArea += area;
            Vector3[] patch = new Vector3[poly.Count];
            for (int p = 0; p < poly.Count; p++)
            {
                float h = h0 + hx * poly[p].X + hy * poly[p].Y;
                float hs = flip ? -h * scale : h;
                acc.HMin = Math.Min(acc.HMin, hs);
                acc.HMax = Math.Max(acc.HMax, hs);
                patch[p] = origin + u * poly[p].X + v * poly[p].Y;
                acc.Min = Vector3.Min(acc.Min, patch[p]);
                acc.Max = Vector3.Max(acc.Max, patch[p]);
            }
            Vector3 c3 = origin + u * centroid.X + v * centroid.Y;
            acc.Centre += c3 * area;
            acc.N1 += (flip ? nj : ni) * area;
            acc.N2 += (flip ? ni : nj) * area;
            (flip ? acc.T2 : acc.T1).Add(t.Tri[i]);
            (flip ? acc.T1 : acc.T2).Add(t.Tri[j]);
            if (acc.Patches.Count < options.MaxPatches) acc.Patches.Add(patch);
            return 1;
        }
        #endregion

        #region Geometry
        private static int Floor(float value, float cell) => (int)Math.Floor(value / cell);

        private static long Key(int x, int y, int z) => ((long)(x + (1 << 20)) & 0x1FFFFF) | (((long)(y + (1 << 20)) & 0x1FFFFF) << 21) | (((long)(z + (1 << 20)) & 0x1FFFFF) << 42);

        private static (int, int, int) Unkey(long key) => ((int)(key & 0x1FFFFF) - (1 << 20), (int)((key >> 21) & 0x1FFFFF) - (1 << 20), (int)((key >> 42) & 0x1FFFFF) - (1 << 20));

        private static int BinOf(Vector3 n) => PackBin((int)Math.Round(n.X / NormalBin), (int)Math.Round(n.Y / NormalBin), (int)Math.Round(n.Z / NormalBin));

        private static int PackBin(int x, int y, int z) => ((x + 32) & 0xFF) | (((y + 32) & 0xFF) << 8) | (((z + 32) & 0xFF) << 16);

        private static (int, int, int) UnBin(int bin) => ((bin & 0xFF) - 32, ((bin >> 8) & 0xFF) - 32, ((bin >> 16) & 0xFF) - 32);

        private static Vector2 Flat(Vector3 d, Vector3 u, Vector3 v) => new Vector2(Vector3.Dot(d, u), Vector3.Dot(d, v));

        private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

        public static float SignedArea(IList<Vector2> poly)
        {
            double sum = 0;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                sum += (double)a.X * b.Y - (double)b.X * a.Y;
            }
            return (float)(sum * 0.5);
        }

        private static Vector2 Centroid(List<Vector2> poly, float area)
        {
            double cx = 0, cy = 0;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                double f = (double)a.X * b.Y - (double)b.X * a.Y;
                cx += (a.X + b.X) * f;
                cy += (a.Y + b.Y) * f;
            }
            return area > 0 ? new Vector2((float)(cx / (6 * area)), (float)(cy / (6 * area))) : poly[0];
        }

        /// <summary>Keep the part of a convex polygon left of the edge p0 -> p1.</summary>
        private static List<Vector2> ClipEdge(List<Vector2> poly, Vector2 p0, Vector2 p1)
        {
            Vector2 e = p1 - p0;
            //f(x) = cross(e, x - p0) = c0 + cx * x + cy * y
            return ClipLinear(poly, -Cross(e, p0), -e.Y, e.X);
        }

        /// <summary>Keep the part of a convex polygon where c0 + cx * x + cy * y >= 0.</summary>
        private static List<Vector2> ClipLinear(List<Vector2> poly, float c0, float cx, float cy)
        {
            List<Vector2> result = new List<Vector2>(poly.Count + 2);
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                float fa = c0 + cx * a.X + cy * a.Y, fb = c0 + cx * b.X + cy * b.Y;
                if (fa >= 0) result.Add(a);
                if ((fa >= 0) != (fb >= 0))
                {
                    float k = fa / (fa - fb);
                    result.Add(a + (b - a) * k);
                }
            }
            return result;
        }
        #endregion
    }
}
