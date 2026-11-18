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
    }
}
