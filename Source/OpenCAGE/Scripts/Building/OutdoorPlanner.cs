using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace OpenCAGE.Building
{
    /// <summary>
    /// World triangles (CATHODE metres) in 2 m buckets on X/Z, for the outdoor lighting planner's rays: a 2D DDA over the buckets with
    /// Moller-Trumbore, each triangle tested once a ray. Triangles wider than 400 m (a huge ground plane) are kept aside and tested by every
    /// ray. Thread safe.
    /// </summary>
    public sealed class TriangleScene
    {
        private readonly float[] _t;
        private readonly bool[] _seeThrough;
        private readonly Dictionary<long, int[]> _grid;
        private readonly int[] _big;
        private readonly float _b;
        private readonly ThreadLocal<int[]> _stamp;
        private readonly ThreadLocal<int[]> _rayId;
        public readonly int Count;
        public readonly Vector3 Min, Max;

        /// <param name="vertices">x, y, z per vertex</param>
        /// <param name="faces">three vertex indices per triangle</param>
        /// <param name="seeThrough">per triangle: a window or grate the light passes (optional)</param>
        public TriangleScene(float[] vertices, int[] faces, bool[] seeThrough = null, float bucket = 2f)
        {
            Count = faces.Length / 3;
            _t = new float[Count * 9];
            _seeThrough = seeThrough;
            _b = bucket;
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            for (int t = 0; t < Count; t++)
                for (int k = 0; k < 3; k++)
                {
                    int v = faces[t * 3 + k];
                    for (int c = 0; c < 3; c++) _t[t * 9 + k * 3 + c] = vertices[v * 3 + c];
                    Vector3 p = new Vector3(vertices[v * 3], vertices[v * 3 + 1], vertices[v * 3 + 2]);
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            Min = min;
            Max = max;
            Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
            List<int> big = new List<int>();
            for (int t = 0; t < Count; t++)
            {
                int o = t * 9;
                float x0 = Math.Min(_t[o], Math.Min(_t[o + 3], _t[o + 6])), x1 = Math.Max(_t[o], Math.Max(_t[o + 3], _t[o + 6]));
                float z0 = Math.Min(_t[o + 2], Math.Min(_t[o + 5], _t[o + 8])), z1 = Math.Max(_t[o + 2], Math.Max(_t[o + 5], _t[o + 8]));
                if (x1 - x0 > 400 || z1 - z0 > 400) { big.Add(t); continue; }
                for (int gx = (int)Math.Floor(x0 / _b); gx <= (int)Math.Floor(x1 / _b); gx++)
                    for (int gz = (int)Math.Floor(z0 / _b); gz <= (int)Math.Floor(z1 / _b); gz++)
                    {
                        long key = Key(gx, gz);
                        if (!grid.TryGetValue(key, out List<int> list)) grid[key] = list = new List<int>();
                        list.Add(t);
                    }
            }
            _grid = grid.ToDictionary(o => o.Key, o => o.Value.ToArray());
            _big = big.ToArray();
            int count = Count;
            _stamp = new ThreadLocal<int[]>(() => new int[Math.Max(1, count)]);
            _rayId = new ThreadLocal<int[]>(() => new int[1]);
        }

        private static long Key(int gx, int gz) => ((long)gx << 32) ^ (uint)gz;

        public bool IsSeeThrough(int triangle) => _seeThrough != null && triangle >= 0 && triangle < _seeThrough.Length && _seeThrough[triangle];

        private bool Hit(int t, float ox, float oy, float oz, float dx, float dy, float dz, out float tt)
        {
            tt = 0;
            int o = t * 9;
            float e1x = _t[o + 3] - _t[o], e1y = _t[o + 4] - _t[o + 1], e1z = _t[o + 5] - _t[o + 2];
            float e2x = _t[o + 6] - _t[o], e2y = _t[o + 7] - _t[o + 1], e2z = _t[o + 8] - _t[o + 2];
            float px = dy * e2z - dz * e2y, py = dz * e2x - dx * e2z, pz = dx * e2y - dy * e2x;
            float det = e1x * px + e1y * py + e1z * pz;
            if (Math.Abs(det) < 1e-12f) return false;
            float inv = 1f / det, sx = ox - _t[o], sy = oy - _t[o + 1], sz = oz - _t[o + 2];
            float u = (sx * px + sy * py + sz * pz) * inv;
            if (u < 0 || u > 1) return false;
            float qx = sy * e1z - sz * e1y, qy = sz * e1x - sx * e1z, qz = sx * e1y - sy * e1x;
            float v = (dx * qx + dy * qy + dz * qz) * inv;
            if (v < 0 || u + v > 1) return false;
            tt = (e2x * qx + e2y * qy + e2z * qz) * inv;
            return true;
        }

        /// <summary>The first hit along a ray (direction normalised here) between 2 cm and <paramref name="tmax"/>; see-through triangles pass the ray when <paramref name="passSeeThrough"/>.</summary>
        public bool Trace(Vector3 origin, Vector3 direction, float tmax, out float thit, bool passSeeThrough = true) => Trace(origin, direction, tmax, out thit, out int _, passSeeThrough);

        public bool Trace(Vector3 origin, Vector3 direction, float tmax, out float thit, out int triangle, bool passSeeThrough = true)
        {
            thit = float.MaxValue;
            triangle = -1;
            float len = direction.Length();
            if (len < 1e-9f || Count == 0) return false;
            float dx = direction.X / len, dy = direction.Y / len, dz = direction.Z / len, ox = origin.X, oy = origin.Y, oz = origin.Z;
            int[] stamp = _stamp.Value;
            int[] counter = _rayId.Value;
            int id = ++counter[0];
            if (id == int.MaxValue) { Array.Clear(stamp, 0, stamp.Length); counter[0] = id = 1; }

            foreach (int t in _big)
                if (Hit(t, ox, oy, oz, dx, dy, dz, out float tt) && tt > 0.02f && tt < tmax && tt < thit && !(passSeeThrough && IsSeeThrough(t))) { thit = tt; triangle = t; }

            int gx = (int)Math.Floor(ox / _b), gz = (int)Math.Floor(oz / _b);
            int sx = dx > 0 ? 1 : -1, sz = dz > 0 ? 1 : -1;
            float tdx = Math.Abs(dx) > 1e-7f ? _b / Math.Abs(dx) : float.MaxValue, tdz = Math.Abs(dz) > 1e-7f ? _b / Math.Abs(dz) : float.MaxValue;
            float tmx = Math.Abs(dx) > 1e-7f ? ((gx + (dx > 0 ? 1 : 0)) * _b - ox) / dx : float.MaxValue;
            float tmz = Math.Abs(dz) > 1e-7f ? ((gz + (dz > 0 ? 1 : 0)) * _b - oz) / dz : float.MaxValue;
            while (true)
            {
                if (_grid.TryGetValue(Key(gx, gz), out int[] list))
                    foreach (int t in list)
                    {
                        if (stamp[t] == id) continue;
                        stamp[t] = id;
                        if (Hit(t, ox, oy, oz, dx, dy, dz, out float tt) && tt > 0.02f && tt < tmax && tt < thit && !(passSeeThrough && IsSeeThrough(t))) { thit = tt; triangle = t; }
                    }
                float tnext = Math.Min(tmx, tmz);
                if (thit <= tnext) return true;
                if (tnext > tmax || tnext == float.MaxValue) return thit < tmax;
                if (tmx < tmz) { tmx += tdx; gx += sx; } else { tmz += tdz; gz += sz; }
            }
        }

        /// <summary>Every surface a vertical line at (x, z) crosses: height and |normal.y|, highest first, surfaces within 5 cm merged.</summary>
        public List<(float h, float ny)> VerticalHits(float x, float z)
        {
            List<(float, float)> r = new List<(float, float)>();
            void Test(int t)
            {
                int o = t * 9;
                float ax = _t[o], az = _t[o + 2], bx = _t[o + 3], bz = _t[o + 5], cx = _t[o + 6], cz = _t[o + 8];
                float d = (bz - cz) * (ax - cx) + (cx - bx) * (az - cz);
                if (Math.Abs(d) < 1e-9f) return;
                float u = ((bz - cz) * (x - cx) + (cx - bx) * (z - cz)) / d, v = ((cz - az) * (x - cx) + (ax - cx) * (z - cz)) / d, w = 1 - u - v;
                if (u < 0 || v < 0 || w < 0) return;
                float h = u * _t[o + 1] + v * _t[o + 4] + w * _t[o + 7];
                float e1x = bx - ax, e1y = _t[o + 4] - _t[o + 1], e1z = bz - az, e2x = cx - ax, e2y = _t[o + 7] - _t[o + 1], e2z = cz - az;
                float nx = e1y * e2z - e1z * e2y, ny = e1z * e2x - e1x * e2z, nz = e1x * e2y - e1y * e2x, nl = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                r.Add((h, nl > 0 ? Math.Abs(ny) / nl : 0));
            }
            if (_grid.TryGetValue(Key((int)Math.Floor(x / _b), (int)Math.Floor(z / _b)), out int[] list))
                foreach (int t in list) Test(t);
            foreach (int t in _big) Test(t);
            r.Sort((p, q) => q.Item1.CompareTo(p.Item1));
            List<(float h, float ny)> m = new List<(float, float)>();
            foreach ((float h, float ny) in r)
            {
                if (m.Count > 0 && m[m.Count - 1].h - h < 0.05f)
                {
                    if (ny > m[m.Count - 1].ny) m[m.Count - 1] = (m[m.Count - 1].h, ny);
                    continue;
                }
                m.Add((h, ny));
            }
            return m;
        }
    }

    /// <summary>What to plan for an outdoor area. Colours are sRGB 0-255 as a light takes them.</summary>
    public sealed class OutdoorSettings
    {
        /// <summary>The area to light (world AABB): floors inside it get light.</summary>
        public Vector3 Min, Max;
        /// <summary>From the scene TOWARDS the sun (normalised here).</summary>
        public Vector3 SunDirection = Panorama.DirectionOfAngles(150, 40);
        public Vector3 SunColour = new Vector3(255, 244, 222);
        public float SunIntensity = 6f;
        /// <summary>auto (spot, or patches when rooms sit under a shadow map coarser than AutoPatchTexel), spot: one shadowed spot far out along the sun; patches: shadowless square spots on the sunlit floors; none.</summary>
        public string SunMode = "auto";
        /// <summary>auto picks patches when there are rooms and one sun shadow texel would be wider than this (m): sun creeps in along their walls' tops.</summary>
        public float AutoPatchTexel = 0.18f;
        /// <summary>Metres from the area's centre to the sun spot (0 = auto: 3 x the area's radius, 150-600).</summary>
        public float SunDistance = 0;
        public int SunShadowPriority = 100;
        /// <summary>The sun's shadow depth_bias / slope_scale_depth_bias (null = the LightReference's defaults, 0.05 / 1).</summary>
        public float? SunDepthBias;
        public int? SunSlopeBias;

        public Vector3 SkyColour = new Vector3(170, 196, 255);
        /// <summary>The sky fill's strength on open ground (each light's intensity is set so the median open floor gets this).</summary>
        public float SkyIntensity = 1.2f;
        /// <summary>One fill light per cell this wide (m) at its most open floor point, plus covered height bands.</summary>
        public float FillSpacing = 12f;
        /// <summary>Floor sample grid (m).</summary>
        public float FloorStep = 3f;
        /// <summary>At most this share of a fill light's peak may reach a point in a room it cannot see.</summary>
        public float Leak = 0.1f;
        public float MaxReach = 30f;
        public float FillHeight = 6f;
        /// <summary>start_attenuation as a share of end_attenuation for the fills (full strength out to it).</summary>
        public float FillStartFraction = 0.35f;
        /// <summary>Fills cut short of a room are raised towards the target on their own patch of ground by at most this factor (1 = off).</summary>
        public float BalanceCap = 2.5f;

        /// <summary>Sun light thrown back off sunlit ground into the fills: 0 = none.</summary>
        public float BounceStrength = 0.5f;
        /// <summary>The ground's hue (sRGB 0-255) and how much it reflects (0-1).</summary>
        public Vector3 GroundColour = new Vector3(255, 236, 214);
        public float GroundAlbedo = 0.2f;

        /// <summary>Sun patches: floor cell size (0 = auto 1-2 m), the largest square under cover / in the open (cells a side).</summary>
        public float PatchStep = 0;
        public int PatchMax = 3, PatchOpen = 6;

        /// <summary>A sky dome to keep the lights off (null = none).</summary>
        public Vector3? DomeCentre;
        public float DomeRadius;

        public bool Reflections = true;
        /// <summary>Lights already in the level near the area (for the light-budget warning), and the shadow casters among them.</summary>
        public int ExistingLights, ExistingShadowCasters;
        public string Prefix = "Outdoor";
    }

    public sealed class PlannedLight
    {
        public string Name;
        /// <summary>sun, fill or patch.</summary>
        public string Role;
        /// <summary>OMNI or SPOT.</summary>
        public string Type;
        public Vector3 Position;
        /// <summary>The way a spot shines (normalised).</summary>
        public Vector3 Direction;
        public Vector3 Colour;
        public float Intensity, Start, End, Inner, Outer;
        public bool Shadow, Square;
        /// <summary>A square patch's half width on the floor (m).</summary>
        public float Half;
        public float NearDist = -1;
        public int ShadowPriority;
        public float? DepthBias;
        public int? SlopeBias;
    }

    public sealed class FloorPoint
    {
        public float X, Y, Z, Head;
        public float View, ViewRaw, Side;
        public bool Room, Sunlit;
    }

    public sealed class OutdoorPlan
    {
        public List<PlannedLight> Lights = new List<PlannedLight>();
        public List<FloorPoint> Floors = new List<FloorPoint>();
        public List<string> Warnings = new List<string>();
        public List<string> Notes = new List<string>();
        public float FloorStep, PatchStep;
        public int OpenPoints, EnclosedPoints, RoomPoints, Rooms, SunlitPoints, PatchCells, PatchCellsLit;
        /// <summary>Sky fill on open ground (median, = the asked strength) and in rooms (median and worst), as shares of the open-ground median.</summary>
        public double RoomFillMedian, RoomFillMax;
        /// <summary>The sky fill on open ground within 8 m of a room (median), as a share of open ground's.</summary>
        public double RingFillMedian = 1;
        /// <summary>The fills that light rooms most: position, kind, reach, and their summed share over the room points.</summary>
        public List<string> RoomLeakers = new List<string>();
        public int FillsCut, FillsDropped, FillsAimed, FillsBoosted;
        /// <summary>auto chose sun patches over the spot (rooms under a coarse shadow map).</summary>
        public bool AutoPatches;
        public double SunShadowTexel;
        public Vector3 SunPosition;
        public double Seconds;
    }

    /// <summary>
    /// Plans outdoor light for an area. The SUN: CATHODE has no directional light (LIGHTS.BIN's sun record is off in every retail level),
    /// so one shadowed spot far out along the sun stands in for it, flat over the area (start_attenuation past its far side, end 50 m beyond;
    /// the game clamps intensity x ((end - start) / 5)^2 x colour at 10000) and kept off the sky dome; 'patches' instead tiles the sunlit
    /// floors with shadowless square spots pointing down. The SKY: unshadowed omnis over the most open floor of each cell, each one's reach
    /// cut so at most Leak of it reaches a room point it cannot see, intensities set so open ground gets SkyIntensity.
    /// </summary>
    public static class OutdoorPlanner
    {
        /// <summary>The share of a non-physical CATHODE light's strength at distance d: full out to start, then defocus / (x^2 + defocus) x (1 - smoothstep).</summary>
        public static double Falloff(double d, double start, double end)
        {
            if (d <= start) return 1;
            if (d >= end) return 0;
            double range = Math.Max(1e-3, end - start), x = d - start, defocus = (range / 5) * (range / 5);
            double t = x / range, smooth = t * t * (3 - 2 * t);
            return defocus / (x * x + defocus) * (1 - smooth);
        }

        private static readonly Vector3 Up = new Vector3(0, 1, 0);

        public static OutdoorPlan Plan(TriangleScene scene, OutdoorSettings s, Action<string> progress = null)
        {
            System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
            OutdoorPlan plan = new OutdoorPlan();
            Vector3 sun = s.SunDirection.LengthSquared() > 1e-9f ? Vector3.Normalize(s.SunDirection) : Up;
            if (sun.Y <= 0.02f) plan.Warnings.Add("The sun is at or below the horizon (elevation " + Math.Round(Math.Asin(Math.Max(-1, Math.Min(1, sun.Y))) * 180 / Math.PI, 1) + " degrees): it lights no floor. Raise it.");
            double sinEl = Math.Max(0, sun.Y);

            //---- Floors: every upward surface on a grid with 2 m of room above it
            float areaXZ = Math.Max(1, (s.Max.X - s.Min.X) * (s.Max.Z - s.Min.Z));
            float step = Math.Max(s.FloorStep, (float)Math.Sqrt(areaXZ / 150000.0));
            plan.FloorStep = step;
            progress?.Invoke("Finding the floors");
            List<FloorPoint> floors = new List<FloorPoint>();
            for (float x = s.Min.X + step / 2; x < s.Max.X; x += step)
                for (float z = s.Min.Z + step / 2; z < s.Max.Z; z += step)
                {
                    List<(float h, float ny)> hits = scene.VerticalHits(x, z);
                    for (int k = 0; k < hits.Count; k++)
                    {
                        if (hits[k].ny < 0.6f || hits[k].h < s.Min.Y - 0.5f || hits[k].h > s.Max.Y + 0.5f) continue;
                        float head = k == 0 ? 99 : hits[k - 1].h - hits[k].h;
                        if (head < 2f) continue;
                        floors.Add(new FloorPoint() { X = x, Y = hits[k].h, Z = z, Head = head });
                    }
                }
            plan.Floors = floors;
            if (floors.Count == 0)
            {
                plan.Warnings.Add("No floor (an upward surface with 2 m of room over it) in the area: nothing to light. Is the area right, and does its ground have collision?");
                plan.Seconds = timer.Elapsed.TotalSeconds;
                return plan;
            }

            //---- Sky view and side openness
            const float vint = 0.12f, vlight = 0.3f, sint = 0.15f;
            progress?.Invoke("Measuring how much sky each of " + floors.Count + " floor points sees");
            List<Vector3> dirs = new List<Vector3>();
            int nd = 48;
            double golden = Math.PI * (3 - Math.Sqrt(5));
            for (int i = 0; i < nd; i++)
            {
                double r = Math.Sqrt((i + 0.5) / nd), th = i * golden;
                double dy = Math.Max(0.05, Math.Sqrt(Math.Max(0, 1 - r * r)));
                dirs.Add(Vector3.Normalize(new Vector3((float)(r * Math.Cos(th)), (float)dy, (float)(r * Math.Sin(th)))));
            }
            List<Vector3> sdirs = new List<Vector3>();
            foreach (double el in new[] { 5.0, 20.0 })
                for (int k = 0; k < 16; k++)
                {
                    double az = k * Math.PI / 8, ce = Math.Cos(el * Math.PI / 180);
                    sdirs.Add(new Vector3((float)(ce * Math.Cos(az)), (float)Math.Sin(el * Math.PI / 180), (float)(ce * Math.Sin(az))));
                }
            Parallel.For(0, floors.Count, i =>
            {
                FloorPoint f = floors[i];
                Vector3 o = new Vector3(f.X, f.Y + Math.Min(1f, f.Head * 0.5f), f.Z);
                int open = 0;
                foreach (Vector3 d in dirs) if (!scene.Trace(o, d, 50f, out float _)) open++;
                f.ViewRaw = open / (float)nd;
                if (f.ViewRaw < vint)
                {
                    Vector3 os = new Vector3(f.X, f.Y + Math.Min(1.2f, f.Head * 0.5f), f.Z);
                    int sopen = 0;
                    foreach (Vector3 d in sdirs) if (!scene.Trace(os, d, 25f, out float _)) sopen++;
                    f.Side = sopen / (float)sdirs.Count;
                }
                else f.Side = 1;
                f.View = f.ViewRaw < vint && f.Side >= sint ? vint : f.ViewRaw;
                f.Sunlit = sun.Y > 0.02f && !scene.Trace(new Vector3(f.X, f.Y + 0.15f, f.Z), sun, 250f, out float _);
            });
            plan.OpenPoints = floors.Count(o => o.ViewRaw >= vlight);
            plan.SunlitPoints = floors.Count(o => o.Sunlit);

            //---- Rooms: enclosed points joined to their neighbours (one step, within 1.5 m of height), groups of 24 m2 or more
            List<int> enclosed = Enumerable.Range(0, floors.Count).Where(i => floors[i].View < vint).ToList();
            plan.EnclosedPoints = enclosed.Count;
            Dictionary<(int, int), List<int>> at = new Dictionary<(int, int), List<int>>();
            (int, int) KeyOf(FloorPoint f) => ((int)Math.Floor((f.X - s.Min.X) / step), (int)Math.Floor((f.Z - s.Min.Z) / step));
            foreach (int i in enclosed) { var k = KeyOf(floors[i]); if (!at.TryGetValue(k, out List<int> l)) at[k] = l = new List<int>(); l.Add(i); }
            Dictionary<int, int> comp = new Dictionary<int, int>();
            List<int> compSize = new List<int>();
            foreach (int seed in enclosed)
            {
                if (comp.ContainsKey(seed)) continue;
                int id = compSize.Count;
                compSize.Add(0);
                Stack<int> stack = new Stack<int>();
                stack.Push(seed);
                comp[seed] = id;
                while (stack.Count > 0)
                {
                    int c = stack.Pop();
                    compSize[id]++;
                    var kc = KeyOf(floors[c]);
                    for (int ox = -1; ox <= 1; ox++)
                        for (int oz = -1; oz <= 1; oz++)
                            if (at.TryGetValue((kc.Item1 + ox, kc.Item2 + oz), out List<int> l))
                                foreach (int n in l)
                                    if (!comp.ContainsKey(n) && Math.Abs(floors[n].Y - floors[c].Y) < 1.5f) { comp[n] = id; stack.Push(n); }
                }
            }
            int roomPoints = Math.Max(2, (int)Math.Ceiling(24.0 / (step * step)));
            List<int> interior = enclosed.Where(i => compSize[comp[i]] >= roomPoints).ToList();
            foreach (int i in interior) floors[i].Room = true;
            plan.RoomPoints = interior.Count;
            plan.Rooms = Enumerable.Range(0, compSize.Count).Count(c => compSize[c] >= roomPoints);

            //---- The sun
            Vector3 centre = (s.Min + s.Max) * 0.5f;
            float radius = Math.Max(5f, (s.Max - s.Min).Length() * 0.5f);
            string sunMode = s.SunMode ?? "auto";
            if (sunMode == "auto" && sun.Y > 0.02f)
            {
                //One shadow map over the whole area: with rooms in it and texels wider than AutoPatchTexel, the sun creeps in along their
                //walls' tops and floor edges, so use patches
                OutdoorPlan trial = new OutdoorPlan();
                PlanSunSpot(trial, s, sun, centre, radius);
                if (plan.Rooms > 0 && trial.SunShadowTexel > s.AutoPatchTexel)
                {
                    sunMode = "patches";
                    plan.AutoPatches = true;
                    plan.Notes.Add("Sun 'auto' chose patches: the area holds " + plan.Rooms + " room" + (plan.Rooms == 1 ? "" : "s") + " and one shadow map over it would be " + Math.Round(trial.SunShadowTexel * 100) + " cm a texel - sun would creep into them along their walls. A smaller area gets the shadowed spot (walls and props lit by the sun too).");
                }
                else sunMode = "spot";
            }
            if (sunMode == "spot" && sun.Y > 0.02f)
            {
                PlanSunSpot(plan, s, sun, centre, radius);
                if (plan.Rooms > 0 && plan.SunShadowTexel > 0.08)
                    plan.Notes.Add("With " + Math.Round(plan.SunShadowTexel * 100) + " cm shadow texels, thin lines of sun can show inside rooms along their walls' tops and floor edges; a smaller area gives finer shadows.");
            }
            else if (sunMode == "patches" && sun.Y > 0.02f)
            {
                progress?.Invoke("Tiling the sunlit floors with sun patches");
                PlanPatches(plan, scene, s, sun, sinEl);
            }

            //---- Sky fill
            if (s.SkyIntensity > 0)
            {
                progress?.Invoke("Placing the sky fill");
                PlanFills(plan, scene, s, sun, sinEl, floors, interior, step);
            }

            //---- Budgets
            int added = plan.Lights.Count, total = added + s.ExistingLights;
            if (total > 1500)
                plan.Warnings.Add("LIGHT BUDGET: " + added + " new lights + " + s.ExistingLights + " already in and around the area = " + total + ". The game CRASHES on the 2049th unshadowed light drawn in one view, and a view over this area can hold most of them. " +
                    (plan.Lights.Any(o => o.Role == "patch") ? "Use bigger patches (patch step), fewer fills (fill spacing), zones, or occluders." : "Use fewer fills (fill spacing), zones, or occluders."));
            else if (total > 1000)
                plan.Notes.Add("Light budget: " + total + " lights in and around the area (the game crashes past 2048 unshadowed lights in one view).");
            if (plan.Lights.Any(o => o.Role == "sun") && s.ExistingShadowCasters > 0)
                plan.Warnings.Add("SHADOW BUDGET: " + s.ExistingShadowCasters + " shadowed light" + (s.ExistingShadowCasters == 1 ? " is" : "s are") + " already in and around the area. The game gives 4 shadow maps a frame; a shadowed light that misses out is drawn WITHOUT its shadow, and a sun without its shadow lights every room under it. " +
                    "The sun has shadow priority " + s.SunShadowPriority + "; check light.shadow_budget in game, or use sun 'patches' (no shadow maps).");
            plan.Seconds = timer.Elapsed.TotalSeconds;
            return plan;
        }

        private static void PlanSunSpot(OutdoorPlan plan, OutdoorSettings s, Vector3 sun, Vector3 centre, float radius)
        {
            float D = s.SunDistance > 0 ? s.SunDistance : Math.Max(150f, Math.Min(600f, radius * 3f));
            if (D < radius + 20f) D = radius + 20f;
            Vector3[] corners = new Vector3[8];
            for (int i = 0; i < 8; i++) corners[i] = new Vector3((i & 1) == 0 ? s.Min.X : s.Max.X, (i & 2) == 0 ? s.Min.Y : s.Max.Y, (i & 4) == 0 ? s.Min.Z : s.Max.Z);
            Vector3 forward = -sun;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector3 p = centre + sun * D;
                double half = 0, minAlong = double.MaxValue, maxAlong = 0;
                foreach (Vector3 q in corners)
                {
                    Vector3 v = q - p;
                    double along = Vector3.Dot(v, forward);
                    double perp = (v - forward * (float)along).Length();
                    half = Math.Max(half, Math.Atan2(perp, Math.Max(1, along)) * 180 / Math.PI);
                    minAlong = Math.Min(minAlong, along);
                    maxAlong = Math.Max(maxAlong, along);
                }
                double inner = Math.Min(170, 2 * (half + 1)), outer = Math.Min(176, inner + 6);
                float start = (float)(maxAlong + 5), end = start + 50f;
                //Keep it off the dome: no point of the dome inside the cone may be nearer the light than end
                if (s.DomeCentre != null && s.DomeRadius > 0)
                {
                    double nearest = DomeDistanceInCone(p, forward, outer / 2, s.DomeCentre.Value, s.DomeRadius);
                    if (nearest < end + 10 && attempt < 11 && D > radius + 30)
                    {
                        D = Math.Max(radius + 25f, D * 0.85f);
                        continue;
                    }
                    if (nearest < end + 10)
                        plan.Warnings.Add("The sun spot reaches the sky dome (" + Math.Round(nearest) + " m from it inside its cone, its light ends at " + Math.Round(end) + " m): the dome there will look lit. Make the dome bigger or the area smaller.");
                }
                plan.SunPosition = p;
                double footprint = 2 * D * Math.Tan(outer / 2 * Math.PI / 180);
                plan.SunShadowTexel = footprint / 2048.0;
                plan.Lights.Add(new PlannedLight()
                {
                    Name = s.Prefix + "_Sun",
                    Role = "sun",
                    Type = "SPOT",
                    Position = p,
                    Direction = forward,
                    Colour = s.SunColour,
                    Intensity = s.SunIntensity,
                    Start = start,
                    End = end,
                    Inner = (float)inner,
                    Outer = (float)outer,
                    Shadow = true,
                    NearDist = (float)Math.Max(1, minAlong - 100),
                    ShadowPriority = s.SunShadowPriority,
                    DepthBias = s.SunDepthBias,
                    SlopeBias = s.SunSlopeBias,
                });
                if (plan.SunShadowTexel > 0.25)
                    plan.Warnings.Add("The area is big for one sun shadow map: about " + Math.Round(plan.SunShadowTexel * 100) + " cm a shadow texel (at 2048), so shadows will be soft and blocky. Light a smaller area, or use sun 'patches' for the floors.");
                plan.Notes.Add("Sun: a shadowed spot " + Math.Round(D) + " m out, cone " + Math.Round(outer, 1) + " degrees, full strength out to " + Math.Round(start) + " m.");
                return;
            }
        }

        private static double DomeDistanceInCone(Vector3 p, Vector3 axis, double halfDegrees, Vector3 domeCentre, float domeRadius)
        {
            Vector3 a = Vector3.Normalize(axis);
            Vector3 side = Vector3.Normalize(Math.Abs(a.Y) < 0.9f ? Vector3.Cross(a, Up) : Vector3.Cross(a, new Vector3(1, 0, 0)));
            Vector3 side2 = Vector3.Cross(a, side);
            double nearest = double.MaxValue;
            for (int ring = 0; ring <= 4; ring++)
            {
                double ang = halfDegrees * ring / 4 * Math.PI / 180;
                int n = ring == 0 ? 1 : 24;
                for (int k = 0; k < n; k++)
                {
                    double phi = k * 2 * Math.PI / n;
                    Vector3 w = Vector3.Normalize(a * (float)Math.Cos(ang) + (side * (float)Math.Cos(phi) + side2 * (float)Math.Sin(phi)) * (float)Math.Sin(ang));
                    //|p + t w - c| = R, p inside the sphere: the positive root
                    Vector3 m = p - domeCentre;
                    double b = Vector3.Dot(m, w), c = m.LengthSquared() - (double)domeRadius * domeRadius;
                    double disc = b * b - c;
                    if (disc < 0) continue;
                    double t = -b + Math.Sqrt(disc);
                    if (t > 0) nearest = Math.Min(nearest, t);
                }
            }
            return nearest;
        }

        private static void PlanPatches(OutdoorPlan plan, TriangleScene scene, OutdoorSettings s, Vector3 sun, double sinEl)
        {
            float areaXZ = Math.Max(1, (s.Max.X - s.Min.X) * (s.Max.Z - s.Min.Z));
            float step = s.PatchStep > 0 ? s.PatchStep : (areaXZ <= 40000 ? 1f : 2f);
            step = Math.Max(step, (float)Math.Sqrt(areaXZ / 400000.0));
            plan.PatchStep = step;
            List<(float x, float h, float z, float head, float head2)> cells = new List<(float, float, float, float, float)>();
            for (float x = s.Min.X + step / 2; x < s.Max.X; x += step)
                for (float z = s.Min.Z + step / 2; z < s.Max.Z; z += step)
                {
                    List<(float h, float ny)> hits = scene.VerticalHits(x, z);
                    for (int k = 0; k < hits.Count; k++)
                    {
                        if (hits[k].ny < 0.7f || hits[k].h < s.Min.Y - 0.5f || hits[k].h > s.Max.Y + 0.5f) continue;
                        float head = k == 0 ? 999 : hits[k - 1].h - hits[k].h;
                        if (head < 1.2f) continue;
                        float head2 = k < 2 ? 999 : hits[k - 2].h - hits[k].h;
                        cells.Add((x, hits[k].h, z, head, head2));
                    }
                }
            plan.PatchCells = cells.Count;
            ConcurrentBag<(float x, float h, float z, float head, float head2)> lit = new ConcurrentBag<(float, float, float, float, float)>();
            Parallel.ForEach(cells, c =>
            {
                if (!scene.Trace(new Vector3(c.x, c.h + 0.15f, c.z), sun, 250f, out float _)) lit.Add(c);
            });
            plan.PatchCellsLit = lit.Count;
            float intensity = (float)(s.SunIntensity * Math.Max(0.05, sinEl));
            int n = 0;
            foreach (var g in lit.GroupBy(c => (int)Math.Floor((c.h + 0.75) / 2.0)))
            {
                Dictionary<(int, int), (float x, float h, float z, float head, float head2)> map = new Dictionary<(int, int), (float, float, float, float, float)>();
                foreach (var c in g)
                {
                    var kc = ((int)Math.Floor(c.x / step), (int)Math.Floor(c.z / step));
                    if (!map.TryGetValue(kc, out var c0) || c.h > c0.h) map[kc] = c;
                }
                HashSet<(int, int)> used = new HashSet<(int, int)>();
                foreach (var k0 in map.Keys.OrderBy(k => k.Item2).ThenBy(k => k.Item1).ToList())
                {
                    if (used.Contains(k0)) continue;
                    int maxS = map[k0].head > 10 ? s.PatchOpen : s.PatchMax, size = 1;
                    while (size < maxS)
                    {
                        int s2 = size + 1, nLit = 0, ringLit = 0;
                        bool clash = false;
                        for (int i = 0; i < s2 && !clash; i++)
                            for (int j = 0; j < s2; j++)
                            {
                                var kk = (k0.Item1 + i, k0.Item2 + j);
                                if (used.Contains(kk)) { clash = true; break; }
                                if (map.ContainsKey(kk)) { nLit++; if (i == size || j == size) ringLit++; }
                            }
                        if (clash || nLit < 0.75 * s2 * s2 || ringLit < 0.5 * (2 * size + 1)) break;
                        size = s2;
                    }
                    if (size == 1 && !map.ContainsKey((k0.Item1 + 1, k0.Item2)) && !map.ContainsKey((k0.Item1 - 1, k0.Item2)) && !map.ContainsKey((k0.Item1, k0.Item2 + 1)) && !map.ContainsKey((k0.Item1, k0.Item2 - 1)))
                    {
                        used.Add(k0);
                        continue;
                    }
                    List<(float x, float h, float z, float head, float head2)> members = new List<(float, float, float, float, float)>();
                    for (int i = 0; i < size; i++)
                        for (int j = 0; j < size; j++)
                        {
                            var kk = (k0.Item1 + i, k0.Item2 + j);
                            used.Add(kk);
                            if (map.TryGetValue(kk, out var cm)) members.Add(cm);
                        }
                    double cx = (k0.Item1 + size / 2.0) * step, cz = (k0.Item2 + size / 2.0) * step, top = members.Max(c => c.h), fh = members.Min(c => c.h);
                    double half = size * step / 2.0; //exactly the cells: square spots have hard edges, and any overlap draws bright seams
                    //High enough that the light under the floor's middle (reach - H) stays within ~0.3 m: a room under a lit roof stays dark.
                    //Under cover, the spot stays under the cover (inside the ceiling void when there is one)
                    double wanted = Math.Max(4, Math.Min(40, half * half / 0.3));
                    double room = Math.Min(members.Min(c => c.head) - 0.3, members.Min(c => c.head2) - 0.05);
                    double H = (top - fh) + Math.Max(1.0, Math.Min(wanted, room));
                    double outer = 2 * Math.Atan(half / H) * 180 / Math.PI, reach = Math.Sqrt(H * H + 2 * half * half);
                    plan.Lights.Add(new PlannedLight()
                    {
                        Name = s.Prefix + "_SunPatch_" + n++,
                        Role = "patch",
                        Type = "SPOT",
                        Position = new Vector3((float)cx, (float)(fh + H), (float)cz),
                        Direction = new Vector3(0, -1, 0),
                        Colour = s.SunColour,
                        Intensity = intensity,
                        Start = (float)(reach + 0.05),
                        End = (float)(reach + 0.55),
                        Inner = (float)Math.Max(1, outer - 1),
                        Outer = (float)outer,
                        Shadow = false,
                        Square = true,
                        Half = (float)half,
                    });
                }
            }
            plan.Notes.Add("Sun patches: " + n + " square spots over " + plan.PatchCellsLit + " sunlit of " + plan.PatchCells + " floor cells (" + step + " m). They light floors only: walls and props get no direct sun.");
        }

        private static void PlanFills(OutdoorPlan plan, TriangleScene scene, OutdoorSettings s, Vector3 sun, double sinEl, List<FloorPoint> floors, List<int> interior, float step)
        {
            const float vint = 0.12f, vlight = 0.3f;
            float cell = Math.Max(step * 2, s.FillSpacing);
            float Scale(FloorPoint f) => Math.Max(0.25f, Math.Min(1f, f.ViewRaw >= vint ? f.ViewRaw / 0.5f : f.Side));
            //Picks: the most open point of each cell, and one for each covered height band with no open point of its own
            List<int> picks = new List<int>();
            foreach (var grp in Enumerable.Range(0, floors.Count).Where(i => floors[i].View >= vint).GroupBy(i => ((int)Math.Floor(floors[i].X / cell), (int)Math.Floor(floors[i].Z / cell))))
            {
                float Centre(int i) => Math.Abs(floors[i].X - (grp.Key.Item1 + 0.5f) * cell) + Math.Abs(floors[i].Z - (grp.Key.Item2 + 0.5f) * cell);
                List<int> open = grp.Where(i => floors[i].ViewRaw >= vlight).ToList();
                if (open.Count > 0) picks.Add(open.OrderByDescending(i => floors[i].ViewRaw).ThenBy(Centre).First());
                List<int> sorted = grp.OrderBy(i => floors[i].Y).ToList();
                List<int> band = new List<int> { sorted[0] };
                for (int k = 1; k <= sorted.Count; k++)
                {
                    if (k == sorted.Count || floors[sorted[k]].Y - floors[sorted[k - 1]].Y > 3)
                    {
                        if (band.All(i => floors[i].ViewRaw < vlight)) picks.Add(band.OrderByDescending(i => Scale(floors[i])).ThenBy(Centre).First());
                        band = new List<int>();
                    }
                    if (k < sorted.Count) band.Add(sorted[k]);
                }
            }
            //Rooms in 10 m buckets
            Dictionary<(int, int), List<int>> ib = new Dictionary<(int, int), List<int>>();
            const float ibs = 10;
            foreach (int i in interior) { var k = ((int)Math.Floor(floors[i].X / ibs), (int)Math.Floor(floors[i].Z / ibs)); if (!ib.TryGetValue(k, out List<int> l)) ib[k] = l = new List<int>(); l.Add(i); }
            //Ring picks: the open ground within 6 m of a room gets no light from the cells' picks (theirs are cut short of the room or
            //aimed away), so it goes dark. One more pick per half-cell there, 3 m up, which only ever becomes a spot aimed away from the
            //room (never a cut omni): it washes the ground from the wall outwards
            HashSet<int> ringPicks = new HashSet<int>();
            if (interior.Count != 0)
            {
                float half = Math.Max(step, cell / 2);
                foreach (var grp in Enumerable.Range(0, floors.Count).Where(i => floors[i].ViewRaw >= vlight && floors[i].Head > 6 && !floors[i].Room && NearRoom(floors[i], 6f)).GroupBy(i => ((int)Math.Floor(floors[i].X / half), (int)Math.Floor(floors[i].Z / half), (int)Math.Floor(floors[i].Y / 3))))
                {
                    int best = grp.OrderByDescending(i => floors[i].ViewRaw).First();
                    if (picks.Any(p => Math.Abs(floors[p].X - floors[best].X) < half * 0.6f && Math.Abs(floors[p].Z - floors[best].Z) < half * 0.6f && Math.Abs(floors[p].Y - floors[best].Y) < 2)) continue;
                    picks.Add(best);
                    ringPicks.Add(best);
                }
            }
            bool NearRoom(FloorPoint p, float r)
            {
                var kk = ((int)Math.Floor(p.X / ibs), (int)Math.Floor(p.Z / ibs));
                for (int ox = -1; ox <= 1; ox++)
                    for (int oz = -1; oz <= 1; oz++)
                        if (ib.TryGetValue((kk.Item1 + ox, kk.Item2 + oz), out List<int> l))
                            foreach (int i in l) if (Math.Abs(floors[i].Y - p.Y) < 2 && (floors[i].X - p.X) * (floors[i].X - p.X) + (floors[i].Z - p.Z) * (floors[i].Z - p.Z) < r * r) return true;
                return false;
            }
            //The bounce: sunlit share of each cell's open floor
            Dictionary<(int, int), (int lit, int all)> share = new Dictionary<(int, int), (int, int)>();
            foreach (FloorPoint f in floors)
            {
                if (f.View < vint) continue;
                var k = ((int)Math.Floor(f.X / cell), (int)Math.Floor(f.Z / cell));
                share.TryGetValue(k, out var c);
                share[k] = (c.lit + (f.Sunlit ? 1 : 0), c.all + 1);
            }
            float sf = Math.Max(0.05f, Math.Min(0.9f, s.FillStartFraction));
            ConcurrentBag<Fill> made = new ConcurrentBag<Fill>();
            int dropped = 0, aimed = 0;
            Parallel.ForEach(picks, pi =>
            {
                FloorPoint f = floors[pi];
                bool ring = ringPicks.Contains(pi);
                Vector3 l = new Vector3(f.X, f.Y + Math.Min(ring ? 3f : s.FillHeight, 0.6f * f.Head), f.Z);
                //Every room point within reach that the light cannot see (a wall or a roof between)
                int r = (int)Math.Ceiling(s.MaxReach / ibs);
                List<(double d, int i)> cand = new List<(double, int)>();
                for (int gx = (int)Math.Floor(l.X / ibs) - r; gx <= (int)Math.Floor(l.X / ibs) + r; gx++)
                    for (int gz = (int)Math.Floor(l.Z / ibs) - r; gz <= (int)Math.Floor(l.Z / ibs) + r; gz++)
                        if (ib.TryGetValue((gx, gz), out List<int> list))
                            foreach (int i in list)
                            {
                                Vector3 q = new Vector3(floors[i].X, floors[i].Y + 1f, floors[i].Z);
                                double d = Vector3.Distance(q, l);
                                if (d < s.MaxReach) cand.Add((d, i));
                            }
                List<(double d, Vector3 u)> leaks = new List<(double, Vector3)>();
                foreach (var c in cand.OrderBy(o => o.d))
                {
                    if (c.d < 0.05) continue;
                    Vector3 q = new Vector3(floors[c.i].X, floors[c.i].Y + 1f, floors[c.i].Z);
                    if (scene.Trace(l, q - l, (float)c.d - 0.3f, out float _, false)) leaks.Add((c.d, Vector3.Normalize(q - l)));
                }
                if (leaks.Count == 0) { made.Add(new Fill() { Pick = pi, Pos = l, Start = sf * s.MaxReach, End = s.MaxReach, Scale = Scale(f) }); return; }
                //Rooms off to one side: a wide spot aimed away from them, 35 degrees down so the ground under it is lit, its cone
                //clear of every room point it cannot see by 8 degrees - full reach. Rooms below or all round: the reach is cut instead
                float wx = 0, wz = 0;
                foreach (var lk in leaks) { float w = 1 - (float)(lk.d / s.MaxReach); w *= w; wx += lk.u.X * w; wz += lk.u.Z * w; }
                float hl = (float)Math.Sqrt(wx * wx + wz * wz);
                if (hl > 1e-3f)
                {
                    float c35 = (float)Math.Cos(35 * Math.PI / 180), s35 = (float)Math.Sin(35 * Math.PI / 180);
                    Vector3 dir = new Vector3(-wx / hl * c35, -s35, -wz / hl * c35);
                    double minAng = 180;
                    foreach (var lk in leaks) minAng = Math.Min(minAng, Math.Acos(Math.Max(-1, Math.Min(1, Vector3.Dot(lk.u, dir)))) * 180 / Math.PI);
                    float half = (float)Math.Min(80, minAng - 8);
                    if (half >= (ring ? 40 : 50))
                    {
                        Interlocked.Increment(ref aimed);
                        made.Add(new Fill() { Pick = pi, Pos = l, Start = sf * s.MaxReach, End = s.MaxReach, Scale = Scale(f), Dir = dir, Half = half });
                        return;
                    }
                }
                if (ring) { Interlocked.Increment(ref dropped); return; }
                double nearest = leaks[0].d;
                float end = s.MaxReach, cut = 0;
                //the largest end_attenuation (start = sf x end) that lets at most Leak reach that point
                if (Falloff(nearest, sf * end, end) > s.Leak)
                {
                    float lo = 0.5f, hi = end;
                    for (int it = 0; it < 30; it++)
                    {
                        float mid = (lo + hi) / 2;
                        if (Falloff(nearest, sf * mid, mid) > s.Leak) hi = mid; else lo = mid;
                    }
                    end = lo;
                    cut = (float)nearest;
                }
                if (end < 4) { Interlocked.Increment(ref dropped); return; }
                made.Add(new Fill() { Pick = pi, Pos = l, Start = sf * end, End = end, Scale = Scale(f), Cut = cut });
            });
            List<Fill> fills = made.OrderBy(o => o.Pos.X).ThenBy(o => o.Pos.Z).ThenBy(o => o.Pos.Y).ToList();
            plan.FillsAimed = aimed;
            plan.FillsDropped = dropped;
            plan.FillsCut = fills.Count(o => o.Cut > 0);

            //Normalise: open ground gets SkyIntensity (the median over open points of the fills' sum - unshadowed, so every light in
            //range counts, seen or not, as in game)
            Dictionary<(int, int), List<int>> fb = new Dictionary<(int, int), List<int>>();
            float fbs = Math.Max(10, s.MaxReach);
            for (int i = 0; i < fills.Count; i++) { var k = ((int)Math.Floor(fills[i].Pos.X / fbs), (int)Math.Floor(fills[i].Pos.Z / fbs)); if (!fb.TryGetValue(k, out List<int> l)) fb[k] = l = new List<int>(); l.Add(i); }
            double Sum(FloorPoint f)
            {
                Vector3 p = new Vector3(f.X, f.Y + 0.5f, f.Z);
                double e = 0;
                int kx = (int)Math.Floor(p.X / fbs), kz = (int)Math.Floor(p.Z / fbs);
                for (int ox = -1; ox <= 1; ox++)
                    for (int oz = -1; oz <= 1; oz++)
                        if (fb.TryGetValue((kx + ox, kz + oz), out List<int> l))
                            foreach (int i in l) e += fills[i].Scale * fills[i].Reach(p);
                return e;
            }
            List<double> openSums = floors.Where(o => o.ViewRaw >= vlight).Select(Sum).Where(o => o > 0).OrderBy(o => o).ToList();
            if (openSums.Count == 0) openSums = floors.Where(o => o.View >= vint).Select(Sum).Where(o => o > 0).OrderBy(o => o).ToList();
            double median = openSums.Count == 0 ? 1 : openSums[openSums.Count / 2];
            double k0 = median > 1e-6 ? 1 / median : 1;

            //Local balance: a fill cut short of a room (or aimed away from it) leaves the ground round that room darker than open ground.
            //Each fill is raised (or lowered) towards the target on the open floor in its own full-strength zone (three rounds, at most
            //x BalanceCap); its leak into a room grows by the same factor at most, and the rooms' share is measured after
            if (s.BalanceCap > 1)
            {
                List<FloorPoint> open = floors.Where(o => o.View >= vint && !o.Room).ToList();
                Dictionary<int, List<FloorPoint>> own = new Dictionary<int, List<FloorPoint>>();
                for (int i = 0; i < fills.Count; i++) own[i] = new List<FloorPoint>();
                foreach (FloorPoint p in open)
                {
                    Vector3 q = new Vector3(p.X, p.Y + 0.5f, p.Z);
                    int best = -1;
                    double bestE = 0;
                    int kx = (int)Math.Floor(q.X / fbs), kz = (int)Math.Floor(q.Z / fbs);
                    for (int ox = -1; ox <= 1; ox++)
                        for (int oz = -1; oz <= 1; oz++)
                            if (fb.TryGetValue((kx + ox, kz + oz), out List<int> l))
                                foreach (int i in l)
                                {
                                    double e = fills[i].Scale * fills[i].Reach(q);
                                    if (e > bestE) { bestE = e; best = i; }
                                }
                    if (best >= 0) own[best].Add(p);
                }
                double[] boost = Enumerable.Repeat(1.0, fills.Count).ToArray();
                double[] baseScale = fills.Select(o => (double)o.Scale).ToArray();
                for (int round = 0; round < 3; round++)
                {
                    double[] ratio = new double[fills.Count];
                    for (int i = 0; i < fills.Count; i++)
                    {
                        List<double> r = own[i].Select(p => Sum(p) * k0).Where(e => e > 1e-6).Select(e => 1 / e).OrderBy(o => o).ToList();
                        ratio[i] = r.Count == 0 ? 1 : r[r.Count / 2];
                    }
                    for (int i = 0; i < fills.Count; i++)
                    {
                        boost[i] = Math.Max(1 / s.BalanceCap, Math.Min(s.BalanceCap, boost[i] * Math.Pow(ratio[i], 0.6)));
                        fills[i].Scale = (float)(baseScale[i] * boost[i]);
                    }
                }
                plan.FillsBoosted = boost.Count(o => o > 1.15);
            }
            List<double> roomSums = interior.Select(i => Sum(floors[i]) * k0).OrderBy(o => o).ToList();
            //Which fills reach the rooms most (for the report: a room still lit names its lights)
            if (interior.Count != 0)
            {
                double[] onRooms = new double[fills.Count];
                foreach (int ri in interior)
                {
                    Vector3 q = new Vector3(floors[ri].X, floors[ri].Y + 0.5f, floors[ri].Z);
                    for (int i = 0; i < fills.Count; i++) onRooms[i] += fills[i].Scale * fills[i].Reach(q) * k0 / interior.Count;
                }
                foreach (int i in Enumerable.Range(0, fills.Count).Where(i => onRooms[i] > 0.005).OrderByDescending(i => onRooms[i]).Take(6))
                {
                    Fill fl = fills[i];
                    plan.RoomLeakers.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} at ({1:0.0}, {2:0.0}, {3:0.0}) reach {4:0.0}-{5:0.0} m x{6:0.00}{7}: {8:0}% of open ground's fill on the room floors",
                        fl.Half > 0 ? "aimed spot" : ringPicks.Contains(fl.Pick) ? "ring omni" : fl.Cut > 0 ? "cut omni" : "omni", fl.Pos.X, fl.Pos.Y, fl.Pos.Z, fl.Start, fl.End, fl.Scale, fl.Cut > 0 ? " (cut at a room " + fl.Cut.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m off)" : "", onRooms[i] * 100));
                }
            }
            //Open ground within 8 m of a room (the ring a cut fill leaves darker)
            List<double> ringSums = new List<double>();
            foreach (FloorPoint p in floors)
            {
                if (p.View < vint || p.Room) continue;
                var kk = ((int)Math.Floor(p.X / ibs), (int)Math.Floor(p.Z / ibs));
                bool near = false;
                for (int ox = -1; ox <= 1 && !near; ox++)
                    for (int oz = -1; oz <= 1 && !near; oz++)
                        if (ib.TryGetValue((kk.Item1 + ox, kk.Item2 + oz), out List<int> l))
                            foreach (int i in l) if (Math.Abs(floors[i].Y - p.Y) < 2 && (floors[i].X - p.X) * (floors[i].X - p.X) + (floors[i].Z - p.Z) * (floors[i].Z - p.Z) < 64) { near = true; break; }
                if (near) ringSums.Add(Sum(p) * k0);
            }
            ringSums.Sort();
            plan.RingFillMedian = ringSums.Count == 0 ? 1 : ringSums[ringSums.Count / 2];
            plan.RoomFillMedian = roomSums.Count == 0 ? 0 : roomSums[roomSums.Count / 2];
            plan.RoomFillMax = roomSums.Count == 0 ? 0 : roomSums[roomSums.Count - 1];

            //Each fill's colour: sky, plus sun bounce off its cell's sunlit ground; into a light's sRGB colour and intensity (the
            //game's colour is (c / 255)^2.2 x intensity)
            Vector3 skyLin = Lin(s.SkyColour);
            Vector3 groundLin = Lin(s.GroundColour);
            Vector3 sunLin = Lin(s.SunColour);
            Vector3 bounceLin = groundLin * sunLin * (float)(s.GroundAlbedo * s.SunIntensity * sinEl * s.BounceStrength);
            int n = 0;
            foreach (var fl in fills)
            {
                FloorPoint f = floors[fl.Pick];
                var kc = ((int)Math.Floor(f.X / cell), (int)Math.Floor(f.Z / cell));
                share.TryGetValue(kc, out var sh);
                float lit = sh.all > 0 ? sh.lit / (float)sh.all : 0;
                Vector3 lin = (skyLin * s.SkyIntensity + bounceLin * lit) * (float)(k0 * fl.Scale);
                float max = Math.Max(lin.X, Math.Max(lin.Y, lin.Z));
                if (max <= 1e-6f) continue;
                Vector3 colour = new Vector3((float)Math.Round(255 * Math.Pow(lin.X / max, 1 / 2.2)), (float)Math.Round(255 * Math.Pow(lin.Y / max, 1 / 2.2)), (float)Math.Round(255 * Math.Pow(lin.Z / max, 1 / 2.2)));
                plan.Lights.Add(new PlannedLight()
                {
                    Name = s.Prefix + "_Sky_" + n++,
                    Role = "fill",
                    Type = fl.Half > 0 ? "SPOT" : "OMNI",
                    Position = fl.Pos,
                    Direction = fl.Half > 0 ? fl.Dir : new Vector3(0, -1, 0),
                    Colour = colour,
                    Intensity = max,
                    Start = fl.Start,
                    End = fl.End,
                    Inner = fl.Half > 0 ? Math.Max(1, 2 * (fl.Half - 20)) : 0,
                    Outer = fl.Half > 0 ? 2 * fl.Half : 0,
                });
            }
            plan.Notes.Add("Sky fill: " + n + " lights (" + plan.FillsAimed + " of them spots aimed away from a room beside them, " + plan.FillsCut + " with their reach cut short of a room, " + dropped + " left out as too close to one, " + plan.FillsBoosted + " raised to light their own patch of ground). Rooms get " + Math.Round(plan.RoomFillMedian * 100) + "% of open ground's sky fill (median; worst " + Math.Round(plan.RoomFillMax * 100) + "%; open ground within 8 m of a room " + Math.Round(plan.RingFillMedian * 100) + "%), as " + plan.RoomPoints + " points in " + plan.Rooms + " rooms.");
            if (plan.RoomFillMedian > Math.Max(0.25, s.Leak * 3))
                plan.Warnings.Add("Rooms still get " + Math.Round(plan.RoomFillMedian * 100) + "% of the outdoor sky fill (lights have no shadows, so fills near a room light it through its walls). Lower 'leak', or light a smaller area.");
        }

        /// <summary>A planned sky fill: an omni, or (Half > 0) a wide spot aimed away from the rooms beside it.</summary>
        private sealed class Fill
        {
            public int Pick;
            public Vector3 Pos, Dir;
            public float Start, End, Scale, Cut, Half;

            /// <summary>Its share at a point: the falloff, and for a spot nothing outside its cone.</summary>
            public double Reach(Vector3 p)
            {
                double d = Vector3.Distance(p, Pos);
                if (Half > 0 && d > 1e-3 && Vector3.Dot((p - Pos) / (float)d, Dir) < Math.Cos(Half * Math.PI / 180)) return 0;
                return Falloff(d, Start, End);
            }
        }

        private static Vector3 Lin(Vector3 srgb) => new Vector3((float)Panorama.SrgbToLinear(srgb.X / 255.0), (float)Panorama.SrgbToLinear(srgb.Y / 255.0), (float)Panorama.SrgbToLinear(srgb.Z / 255.0));

        /// <summary>A top-down map of a plan, north (+Z) up: rooms red, enclosed nooks orange, covered yellow, part covered teal, open green (brighter = more sky), sunlit floor lighter; fills white with their reach, patches yellow squares, the sun spot and the way the sun comes from.</summary>
        public static Bitmap DrawMap(OutdoorPlan plan, OutdoorSettings s, int maxPixels = 900)
        {
            float wx = Math.Max(1, s.Max.X - s.Min.X), wz = Math.Max(1, s.Max.Z - s.Min.Z);
            float ppm = Math.Max(0.5f, Math.Min(8f, maxPixels / Math.Max(wx, wz)));
            int W = Math.Max(64, (int)Math.Ceiling(wx * ppm)), H = Math.Max(64, (int)Math.Ceiling(wz * ppm));
            Bitmap bitmap = new Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            PointF P(float x, float z) => new PointF((x - s.Min.X) * ppm, (s.Max.Z - z) * ppm);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.FromArgb(24, 24, 28));
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float st = plan.FloorStep * ppm;
                //the lowest floor of each column
                Dictionary<(int, int), FloorPoint> low = new Dictionary<(int, int), FloorPoint>();
                foreach (FloorPoint f in plan.Floors)
                {
                    var k = ((int)Math.Floor((f.X - s.Min.X) / plan.FloorStep), (int)Math.Floor((f.Z - s.Min.Z) / plan.FloorStep));
                    if (!low.TryGetValue(k, out FloorPoint o) || f.Y < o.Y) low[k] = f;
                }
                foreach (FloorPoint f in low.Values)
                {
                    Color c;
                    if (f.Room) c = Color.FromArgb(200, 40, 40);
                    else if (f.View < 0.12f) c = Color.FromArgb(230, 140, 30);
                    else if (f.ViewRaw < 0.12f) c = Color.FromArgb(220, 210, 60);
                    else if (f.ViewRaw < 0.3f) c = Color.FromArgb(40, 170, 170);
                    else c = Color.FromArgb(20, (int)(90 + 160 * Math.Min(1, f.ViewRaw)), 30);
                    if (f.Sunlit && !f.Room) c = Color.FromArgb((c.R + 255) / 2, (c.G + 240) / 2, (c.B + 160) / 2);
                    PointF p = P(f.X, f.Z);
                    using (SolidBrush b = new SolidBrush(c)) g.FillRectangle(b, p.X - st / 2, p.Y - st / 2, st, st);
                }
                using (Pen reach = new Pen(Color.FromArgb(70, 255, 255, 255), 1))
                using (Pen patch = new Pen(Color.FromArgb(255, 230, 60), 1))
                    foreach (PlannedLight l in plan.Lights)
                    {
                        PointF p = P(l.Position.X, l.Position.Z);
                        if (l.Role == "fill")
                        {
                            float rr = l.End * ppm;
                            g.DrawEllipse(reach, p.X - rr, p.Y - rr, rr * 2, rr * 2);
                            g.FillEllipse(Brushes.White, p.X - 3, p.Y - 3, 6, 6);
                        }
                        else if (l.Role == "patch")
                        {
                            float hp = l.Half * ppm;
                            g.DrawRectangle(patch, p.X - hp, p.Y - hp, hp * 2, hp * 2);
                        }
                    }
                //the way the sun comes from
                Vector3 sun = Vector3.Normalize(s.SunDirection);
                Vector2 flat = new Vector2(sun.X, sun.Z);
                if (flat.LengthSquared() > 1e-6f)
                {
                    flat = Vector2.Normalize(flat);
                    PointF c = new PointF(W / 2f, H / 2f);
                    float len = Math.Min(W, H) * 0.42f;
                    PointF from = new PointF(c.X + flat.X * len, c.Y - flat.Y * len);
                    using (Pen arrow = new Pen(Color.FromArgb(255, 220, 80), 3) { CustomEndCap = new AdjustableArrowCap(5, 6) })
                        g.DrawLine(arrow, from, new PointF(c.X + flat.X * len * 0.55f, c.Y - flat.Y * len * 0.55f));
                    g.FillEllipse(Brushes.Gold, from.X - 8, from.Y - 8, 16, 16);
                }
                using (Font font = new Font(FontFamily.GenericSansSerif, 9f))
                    g.DrawString("N (+Z) up, " + Math.Round(1 / ppm, 2) + " m/px", font, Brushes.White, 4, 4);
            }
            return bitmap;
        }
    }
}
