using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE.MapAssets
{
    /// <summary>A floor of the map: the heights it takes (from, to], its name, the map keyframe it borrows.</summary>
    public class MapFloor
    {
        public string Name;
        public string Keyframe;
        /// <summary>Walkable heights (metres) this floor takes: above From, up to and including To.</summary>
        public float From, To;
        /// <summary>The height most of its floor area is at.</summary>
        public float Level;
        public float Area;

        public bool Takes(float height) { return height > From && height <= To; }
        public override string ToString() { return Name + " (" + Level.ToString("0.0") + " m, " + From.ToString("0.0") + " .. " + To.ToString("0.0") + ")"; }
    }

    /// <summary>Finding a level's floors from its walk mesh.</summary>
    public static class MapFloors
    {
        /// <summary>
        /// The polygons a character can walk to from a start polygon, following the walk mesh's links (off-mesh connections such as
        /// ladders and drops included). What is left out (roofs, the tops of furniture, sealed-off islands) is never drawn.
        /// </summary>
        public static bool[] Reachable(int count, Func<int, IEnumerable<int>> links, IEnumerable<int> starts)
        {
            bool[] seen = new bool[count];
            Queue<int> q = new Queue<int>();
            foreach (int s in starts) if (s >= 0 && s < count && !seen[s]) { seen[s] = true; q.Enqueue(s); }
            while (q.Count > 0)
            {
                int p = q.Dequeue();
                foreach (int n in links(p))
                    if (n >= 0 && n < count && !seen[n]) { seen[n] = true; q.Enqueue(n); }
            }
            return seen;
        }

        /// <summary>The polygon whose centre is nearest a point (height weighted up, so the floor under the point wins).</summary>
        public static int Nearest(IList<MapPolygon> polygons, float x, float y, float z)
        {
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < polygons.Count; i++)
            {
                MapPolygon p = polygons[i];
                float d = (p.CentreX - x) * (p.CentreX - x) + (p.CentreZ - z) * (p.CentreZ - z) + 4 * (p.Height - y) * (p.Height - y);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        public static float AreaOf(MapPolygon p)
        {
            double a = 0;
            for (int i = 0; i < p.Outline.Length; i++) { var u = p.Outline[i]; var v = p.Outline[(i + 1) % p.Outline.Length]; a += (double)u.X * v.Y - (double)v.X * u.Y; }
            return (float)Math.Abs(a / 2);
        }

        /// <summary>
        /// The floors: walkable area by height (0.25 m steps, smoothed), its peaks at least minGap apart and with at least minShare of
        /// the area, each floor taking the heights up to halfway to the next. Lowest first.
        /// </summary>
        public static List<MapFloor> Find(IList<MapPolygon> polygons, float minGap = 2.4f, float minShare = 0.05f)
        {
            List<MapFloor> floors = new List<MapFloor>();
            if (polygons.Count == 0) return floors;
            const float step = 0.25f;
            float lo = polygons.Min(p => p.Height) - 1, hi = polygons.Max(p => p.Height) + 1;
            int n = (int)Math.Ceiling((hi - lo) / step) + 1;
            double[] h = new double[n];
            double total = 0;
            foreach (MapPolygon p in polygons) { float a = AreaOf(p); h[(int)((p.Height - lo) / step)] += a; total += a; }
            // smoothed over about +-0.5 m
            double[] s = new double[n];
            for (int i = 0; i < n; i++) for (int k = -2; k <= 2; k++) if (i + k >= 0 && i + k < n) s[i] += h[i + k] * (3 - Math.Abs(k));
            // peaks, biggest first, kept when far enough from those kept
            List<int> order = Enumerable.Range(0, n).Where(i => (i == 0 || s[i] >= s[i - 1]) && (i == n - 1 || s[i] > s[i + 1])).OrderByDescending(i => s[i]).ToList();
            List<int> peaks = new List<int>();
            foreach (int i in order)
            {
                if (peaks.Any(p => Math.Abs(p - i) * step < minGap)) continue;
                double area = 0; for (int k = -4; k <= 4; k++) if (i + k >= 0 && i + k < n) area += h[i + k];
                if (area < total * minShare) continue;
                peaks.Add(i);
            }
            peaks.Sort();
            for (int k = 0; k < peaks.Count; k++)
            {
                float level = lo + (peaks[k] + 0.5f) * step;
                MapFloor f = new MapFloor() { Level = level, Name = "LEVEL " + (k + 1) };
                f.From = k == 0 ? float.NegativeInfinity : (lo + (peaks[k - 1] + peaks[k] + 1) * step / 2);
                f.To = k == peaks.Count - 1 ? float.PositiveInfinity : (lo + (peaks[k] + peaks[k + 1] + 1) * step / 2);
                f.Area = (float)polygons.Where(p => f.Takes(p.Height)).Sum(p => AreaOf(p));
                floors.Add(f);
            }
            return floors;
        }

        /// <summary>A walkable surface this far (metres) or more above another one lies a storey above it.</summary>
        public const float Storey = 2f;
        /// <summary>The steepest a deck's surface is (degrees): a region is a deck when most of it is this level.</summary>
        public const float MaxDeckSlope = 2.5f;
        /// <summary>A deck smaller than this (square metres) that lies over nothing is a step or a crate top: it joins the floor beside it.</summary>
        public const float MinDeckArea = 50f;

        /// <summary>
        /// The floor each polygon is drawn on (an index into floors; a floor may end up with none). The floors' heights split the walkable
        /// area into regions, each connected within one floor's heights. A deck (a mostly level region) goes on its heights' floor, or one
        /// floor above the highest region it lies a storey or more over (where at least minOver square metres and minOverShare of it do);
        /// a slope (hills, ramps, uneven ground) goes on the lowest floor of the regions beside it, never above its heights' floor. So the
        /// ground stays on the ground floor at any height, and a deck or mezzanine goes above what it covers. links: each polygon's walk
        /// links (indices into polygons).
        /// </summary>
        public static int[] Assign(IList<MapPolygon> polygons, IList<List<int>> links, IList<MapFloor> floors, float minOver = 25, float minOverShare = 0.05f)
        {
            int n = polygons.Count;
            int[] band = polygons.Select(p => MapPlanner.FloorOf(floors, p.Height)).ToArray();
            int[] region = Enumerable.Repeat(-1, n).ToArray();
            int regions = 0;
            for (int i = 0; i < n; i++)
            {
                if (region[i] >= 0) continue;
                Queue<int> q = new Queue<int>();
                q.Enqueue(i); region[i] = regions;
                while (q.Count > 0)
                    foreach (int k in links[q.Dequeue()])
                        if (k >= 0 && k < n && region[k] < 0 && band[k] == band[i]) { region[k] = regions; q.Enqueue(k); }
                regions++;
            }
            double[] area = new double[regions], height = new double[regions], level = new double[regions];
            int[] regionBand = new int[regions];
            for (int i = 0; i < n; i++)
            {
                float a = AreaOf(polygons[i]);
                area[region[i]] += a; height[region[i]] += a * polygons[i].Height; regionBand[region[i]] = band[i];
                if (polygons[i].IsLevel(MaxDeckSlope)) level[region[i]] += a;
            }

            //Which regions lie over which, and over how much: the surfaces over each 1 m square's centre
            Dictionary<long, List<KeyValuePair<int, float>>> cells = new Dictionary<long, List<KeyValuePair<int, float>>>();
            for (int i = 0; i < n; i++)
            {
                MapPolygon p = polygons[i];
                int x0 = (int)Math.Floor(p.Outline.Min(o => o.X)), x1 = (int)Math.Floor(p.Outline.Max(o => o.X));
                int z0 = (int)Math.Floor(p.Outline.Min(o => o.Y)), z1 = (int)Math.Floor(p.Outline.Max(o => o.Y));
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        if (!p.Contains(x + 0.5f, z + 0.5f)) continue;
                        long key = ((long)x << 32) ^ (uint)z;
                        if (!cells.TryGetValue(key, out List<KeyValuePair<int, float>> l)) cells[key] = l = new List<KeyValuePair<int, float>>();
                        l.Add(new KeyValuePair<int, float>(region[i], p.HeightAt(x + 0.5f, z + 0.5f)));
                    }
            }
            Dictionary<long, double> over = new Dictionary<long, double>();
            HashSet<long> pairs = new HashSet<long>();
            foreach (List<KeyValuePair<int, float>> l in cells.Values)
            {
                if (l.Count < 2) continue;
                pairs.Clear();
                foreach (KeyValuePair<int, float> a in l)
                    foreach (KeyValuePair<int, float> b in l)
                        if (a.Key != b.Key && a.Value - b.Value >= Storey) pairs.Add((long)a.Key * regions + b.Key);
                foreach (long pair in pairs) { over.TryGetValue(pair, out double v); over[pair] = v + 1; }
            }
            bool[] deck = Enumerable.Range(0, regions).Select(r => level[r] >= area[r] / 2).ToArray();
            List<int>[] under = Enumerable.Range(0, regions).Select(o => new List<int>()).ToArray();
            foreach (KeyValuePair<long, double> o in over)
            {
                int upper = (int)(o.Key / regions), lower = (int)(o.Key % regions);
                if (deck[upper] && o.Value >= minOver && o.Value >= minOverShare * area[upper]) under[upper].Add(lower);
            }
            HashSet<int>[] next = Enumerable.Range(0, regions).Select(o => new HashSet<int>()).ToArray();
            for (int i = 0; i < n; i++)
                foreach (int k in links[i])
                    if (k >= 0 && k < n && region[k] != region[i]) next[region[i]].Add(region[k]);

            //A deck goes on its heights' floor, or above the highest region it lies over; a slope goes no higher than its heights' floor
            //and down to the lowest floor beside it (so ground at any height joins the ground floor)
            int[] floorOf = (int[])regionBand.Clone();
            int[] order = Enumerable.Range(0, regions).OrderBy(r => regionBand[r]).ThenBy(r => height[r] / Math.Max(area[r], 1e-6)).ToArray();
            for (int pass = 0; pass < 16; pass++)
            {
                bool changed = false;
                foreach (int r in order)
                {
                    int f = regionBand[r];
                    if (deck[r] && (under[r].Count != 0 || area[r] >= MinDeckArea)) foreach (int lower in under[r]) f = Math.Max(f, floorOf[lower] + 1);
                    else foreach (int s in next[r]) f = Math.Min(f, floorOf[s]);
                    f = Math.Max(0, Math.Min(f, floors.Count - 1));
                    if (f != floorOf[r]) { floorOf[r] = f; changed = true; }
                }
                if (!changed) break;
            }
            return Enumerable.Range(0, n).Select(i => floorOf[region[i]]).ToArray();
        }

        /// <summary>
        /// The polygon a point (a map item, a label) stands on: the highest surface under it no more than 'above' metres over it, or
        /// else the nearest polygon (sideways, then up or down). -1 when there are none.
        /// </summary>
        public static int StandsOn(IList<MapPolygon> polygons, float x, float y, float z, float above = 0.75f)
        {
            int best = -1; float bestH = float.MinValue;
            int near = -1; float nearD = float.MaxValue;
            for (int i = 0; i < polygons.Count; i++)
            {
                MapPolygon p = polygons[i];
                float minX = p.Outline.Min(o => o.X), maxX = p.Outline.Max(o => o.X), minZ = p.Outline.Min(o => o.Y), maxZ = p.Outline.Max(o => o.Y);
                float dx = Math.Max(0, Math.Max(minX - x, x - maxX)), dz = Math.Max(0, Math.Max(minZ - z, z - maxZ));
                if (dx > 6 || dz > 6) continue;
                if (dx == 0 && dz == 0 && p.Contains(x, z))
                {
                    float h = p.HeightAt(x, z);
                    if (h <= y + above && h > bestH) { bestH = h; best = i; }
                }
                float dy = Math.Abs(p.HeightAt(Math.Max(minX, Math.Min(maxX, x)), Math.Max(minZ, Math.Min(maxZ, z))) - y);
                float d = (float)Math.Sqrt(dx * dx + dz * dz) + Math.Max(0, dy - above) * 2;
                if (d < nearD) { nearD = d; near = i; }
            }
            return best >= 0 ? best : near;
        }
    }
}
