using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.MCP
{
    internal static partial class McpZonePlanner
    {
        /// <summary>
        /// A composite's content prepared for zoning: each door's frame (its passage axis and width, whether it is a dead end
        /// or see-through), the wall-like kit pieces, and the placement units and points the partition works on. Built once
        /// from the scene; partition, neighbours and bridge all read it.
        /// </summary>
        /// <remarks>
        /// A line-by-line port of the measured research algorithm (algo_gen.py): constrained agglomerative clustering of
        /// placement units on a point graph, with doorway, corridor bulkhead and one-sided wall barriers, cannot-link door
        /// tokens, a cluster width cap, tiny-cluster grouping and attachment, and a door guarantee. Kit (walls, windows, vent
        /// ducts, bulkheads) is typed at every depth when the nested instances are given, so a level that keeps its geometry
        /// inside a few big level composites is read as well as one that places it at the top. Everything is computed in
        /// double from the float inputs, string orders are ordinal, and every order that can change a result follows the
        /// reference exactly (sorted keys, heap order on full tuples, Python's pow for squares), so the output is identical to
        /// the reference given the same inputs.
        /// </remarks>
        public sealed class Geometry
        {
            #region Constants
            // units
            private const double MaxExt = 9.0;        // descend into an instance whose leaves span more than this in X or Z (m); also the "placed in level" span
            private const double StraddleD = 6.0;     // ... or that has leaves on both sides of a live door within this depth (m)
            private const double StraddleW = 3.0;     // ... and within the door half-width + this along the door (m)
            // points / links
            private const double Vox = 0.25;          // per-unit point de-duplication voxel (m)
            private const double RLink = 2.5;         // link radius in XZ (m)
            private const double Dy = 2.0;            // max |dy| of a link (m)
            private const double RAtom = 1.0;         // phase A single-linkage radius (m)
            // walls
            private const double WallTol = 0.25;      // one-sided rule: the other end may be at most this far on the wall's +Z side (m)
            private const double WallHl = 1.5;        // wall barrier half-length along local X (m)
            private const double WallYlo = -1.0, WallYhi = 4.5;
            // doors
            private const double DoorHwPad = 0.5;     // doorway barrier half-width = door half-width + this (m)
            private const double DoorYlo = -1.0, DoorYhi = 4.5;
            private const double VoidD = 4.0, VoidW = 1.0, VoidYhi = 3.5;
            private const int VoidK = 1;
            private const double TagD = 5.0;          // token box depth either side of the door plane (m)
            private const double TagMin = 0.4;        // ... ignoring points closer than this to the plane (m)
            private const double TagW = 3.0;          // ... half-width = door half-width + this (m)
            // clustering
            private const double Lambda = 7e-5;       // constant-Potts resolution at R_LINK = LAMBDA_R
            private const double LambdaR = 2.5;
            private const double MaxSpan = 35.0;      // phase B never makes a cluster wider than this (8 directions, XZ, m) ...
            private const double MaxSpanBulk = 45.0;  // ... or than this when the level has bulkhead planes (corridor runs are cut at them)
            private const int SMin = 25;              // clusters with fewer points are tiny
            private const double AttachR = 4.0;       // tiny attach: prefer the nearest unblocked candidate within this distance (m); also the tiny group radius
            private const bool TinyGroup = true;      // isolated tiny clusters first group with each other
            private const double VertW = 3.0;         // tiny attach: vertical distances count this many times
            private const bool Guarantee = true;
            // bulkheads (corridor narrowings)
            private const double BulkHw = 3.5;        // bulkhead plane half-width along its local X (m)
            private const double BulkMod = 4.0;       // a bulkhead parallel to a door and within this distance of it is that door's module rib (m)
            private const double RibDy = 2.0;         // ... at most this far from the door's pivot height (m)
            private const double RibCos = 0.9;        // ... and parallel: |cos| between the door's passage axis and the bulkhead's local +Z above this
            // neighbours
            private const double ROpen = 4.5;
            private const int OpenK = 6;
            private const int OpenKDoor = 30;
            private const double DoorExcl = 2.5;
            private const double WinHw = 1.0;
            // bridge
            private const double BrD = 4.0, BrDFar = 8.0, BrW = 0.5, BrMin = 0.3;

            private const double GridNear = 4.0;      // cell of the nearest-point ring searches (m)
            private const double DegToRad = Math.PI / 180.0;
            private const double CandidateMargin = 1.0; // Bridge(): leaves this close to the vote box are read (> a voxel's diagonal)
            #endregion

            #region Prepared data
            private sealed class Frame
            {
                public Door Door;           // null for a bulkhead plane (a doorway with no door and no tokens)
                public double Cx, Cy, Cz;   // pivot
                public double Nx, Nz;       // passage axis (across the doorway)
                public double Ux, Uz;       // width axis (along the doorway)
                public double Hw;           // half-width
                public bool Void, Cross, Window;
            }

            private sealed class Wall
            {
                public string Path;
                public double Ox, Oy, Oz;   // pivot
                public double Zx, Zz;       // local +Z (the room is on -Z)
                public double Xx, Xz;       // local +X (the barrier runs along it)
                public bool Window;
            }

            private sealed class Unit
            {
                public string Path;
                public List<int> Leaves;    // content leaf indices, input order
                public int Wall;            // wall index, or -1
                public int Layer;           // 1 = vent kit, 0 = everything else
            }

            private struct MemberPoint
            {
                public double X, Y, Z;
                public int Unit;
                public string Zone;
            }

            private struct Candidate
            {
                public int Unit;
                public int Leaf;            // content leaf index
            }

            // content leaves (door leaves excluded)
            private readonly Leaf[] _leaf;
            private readonly double[] _lx, _ly, _lz;
            private readonly int[] _vx, _vy, _vz;      // voxel of each content leaf
            // doors, walls, units, points
            private readonly List<Frame> _frames = new List<Frame>();
            private readonly Dictionary<string, int> _frameByPath = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly List<Door> _doors;
            private readonly List<Frame> _bulks = new List<Frame>();   // bulkhead planes: only the partition's link filter and span cap read them
            private readonly List<Wall> _walls = new List<Wall>();
            private readonly List<Unit> _units = new List<Unit>();
            private readonly double[] _px, _py, _pz;
            private readonly int[] _pu;
            private readonly int[] _uwall, _ulayer;
            private readonly HashSet<string> _dset;
            private readonly List<string> _itemPath = new List<string>();
            private readonly List<double> _ix = new List<double>(), _iy = new List<double>(), _iz = new List<double>();
            // lazily computed
            private Dictionary<string, int> _partition;
            private List<Candidate>[] _candidates;
            #endregion

            /// <param name="items">The composite's top-level entities, with the composite path each places as Type, in placement order.</param>
            /// <param name="leaves">Every renderable or collision leaf under the composite, including those inside doors, in placement order.</param>
            /// <param name="doors">The door variant instances (composites with a zone link pin), in placement order.</param>
            /// <param name="instances">Optional: every composite instance placed at any depth under the composite (doors and what lies
            /// under them included; the planner skips those itself), each with the composite path it places as Type and its world
            /// transform, in placement order. Kit nested inside level composites (walls, windows, vent ducts, corridor bulkheads) is
            /// only seen through these: without them only the top-level items are typed.</param>
            public Geometry(IList<Item> items, IList<Leaf> leaves, IList<Door> doors, IList<Item> instances = null)
            {
                if (items == null) throw new ArgumentNullException(nameof(items));
                if (leaves == null) throw new ArgumentNullException(nameof(leaves));
                if (doors == null) throw new ArgumentNullException(nameof(doors));
                _doors = new List<Door>(doors);
                _dset = new HashSet<string>(StringComparer.Ordinal);
                foreach (Door d in doors) _dset.Add(d.Path);

                // ---- P0 kit typing: the top-level items, then every nested instance that is not an item (input order)
                HashSet<string> itemPaths = new HashSet<string>(StringComparer.Ordinal);
                foreach (Item it in items) itemPaths.Add(it.Path);
                List<Item> typed = new List<Item>(items);
                if (instances != null)
                    foreach (Item it in instances)
                        if (!itemPaths.Contains(it.Path)) typed.Add(it);
                HashSet<string> ventInst = new HashSet<string>(StringComparer.Ordinal);
                foreach (Item it in typed) if (IsVent(it.Type)) ventInst.Add(it.Path);

                // ---- P1 leaf tree (door content excluded), with each content leaf's kit layer
                Dictionary<string, List<int>> nodeLeaves = new Dictionary<string, List<int>>(StringComparer.Ordinal);
                Dictionary<string, int> nodeVent = new Dictionary<string, int>(StringComparer.Ordinal);   // node -> vent-layer leaves under it
                Dictionary<string, HashSet<string>> children = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
                Dictionary<string, List<(double x, double z)>> doorLeaves = new Dictionary<string, List<(double, double)>>(StringComparer.Ordinal);
                List<Leaf> content = new List<Leaf>();
                List<int> leafVent = new List<int>();          // content leaf -> 1 vent layer / 0 other
                foreach (Leaf l in leaves)
                {
                    string dp = DoorOf(l.Path);
                    if (dp != null)
                    {
                        GetOrAdd(doorLeaves, dp).Add((l.Position.X, l.Position.Z));
                        continue;
                    }
                    int li = content.Count;
                    content.Add(l);
                    int lv = VentPath(l.Path) ? 1 : 0;
                    leafVent.Add(lv);
                    string path = l.Path;
                    string prev = null;
                    int start = 0;
                    while (true)
                    {
                        int slash = path.IndexOf('/', start);
                        string p = slash < 0 ? path : path.Substring(0, slash);
                        GetOrAdd(nodeLeaves, p).Add(li);
                        nodeVent.TryGetValue(p, out int nv);
                        nodeVent[p] = nv + lv;
                        if (prev != null)
                        {
                            if (!children.TryGetValue(prev, out HashSet<string> set)) children[prev] = set = new HashSet<string>(StringComparer.Ordinal);
                            set.Add(p);
                        }
                        prev = p;
                        if (slash < 0) break;
                        start = slash + 1;
                    }
                }
                int nl = content.Count;
                _leaf = content.ToArray();
                _lx = new double[nl]; _ly = new double[nl]; _lz = new double[nl];
                _vx = new int[nl]; _vy = new int[nl]; _vz = new int[nl];
                string[] leafTop = new string[nl];
                for (int i = 0; i < nl; i++)
                {
                    Vector3 p = _leaf[i].Position;
                    _lx[i] = p.X; _ly[i] = p.Y; _lz[i] = p.Z;
                    _vx[i] = Fl(_lx[i], Vox); _vy[i] = Fl(_ly[i], Vox); _vz[i] = Fl(_lz[i], Vox);
                    string path = _leaf[i].Path;
                    int slash = path.IndexOf('/');
                    leafTop[i] = slash < 0 ? path : path.Substring(0, slash);
                }

                // ---- S0 door frames
                foreach (Door d in doors)
                {
                    double a = (double)d.Rotation.Y * DegToRad;
                    double axx = Math.Cos(a), axz = -Math.Sin(a), azx = Math.Sin(a), azz = Math.Cos(a);
                    double nx = azx, nz = azz, ux = axx, uz = axz, hw = 1.25;
                    if (doorLeaves.TryGetValue(d.Path, out List<(double x, double z)> own) && own.Count > 0)
                    {
                        double exMin = double.PositiveInfinity, exMax = double.NegativeInfinity, ezMin = double.PositiveInfinity, ezMax = double.NegativeInfinity;
                        foreach ((double x, double z) in own)
                        {
                            double ex = x * axx + z * axz;
                            double ez = x * azx + z * azz;
                            if (ex < exMin) exMin = ex;
                            if (ex > exMax) exMax = ex;
                            if (ez < ezMin) ezMin = ez;
                            if (ez > ezMax) ezMax = ez;
                        }
                        double wx = exMax - exMin, wz = ezMax - ezMin;
                        if (wx < wz) { nx = axx; nz = axz; ux = azx; uz = azz; }
                        hw = PyMax(0.5, PyMax(wx, wz) / 2);
                    }
                    _frameByPath[d.Path] = _frames.Count;
                    _frames.Add(new Frame()
                    {
                        Door = d,
                        Cx = d.Position.X, Cy = d.Position.Y, Cz = d.Position.Z,
                        Nx = nx, Nz = nz, Ux = ux, Uz = uz, Hw = hw,
                        Window = IsWindow(d.Type),
                    });
                }

                // dead-end ("void") doors: fewer than VOID_K content leaves in the side box on either side
                Dictionary<long, List<int>> lg = new Dictionary<long, List<int>>();
                for (int i = 0; i < nl; i++) GetOrAdd(lg, Key(Fl(_lx[i], GridNear), Fl(_lz[i], GridNear))).Add(i);
                foreach (Frame f in _frames)
                {
                    int cntP = 0, cntM = 0, layP0 = 0, layP1 = 0, layM0 = 0, layM1 = 0;
                    double r = VoidD + f.Hw + VoidW;
                    int gx0 = Fl(f.Cx - r, GridNear), gx1 = Fl(f.Cx + r, GridNear);
                    int gz0 = Fl(f.Cz - r, GridNear), gz1 = Fl(f.Cz + r, GridNear);
                    for (int gx = gx0; gx <= gx1; gx++)
                        for (int gz = gz0; gz <= gz1; gz++)
                        {
                            if (!lg.TryGetValue(Key(gx, gz), out List<int> cell)) continue;
                            foreach (int li in cell)
                            {
                                double dy = _ly[li] - f.Cy;
                                if (dy < DoorYlo || dy > VoidYhi) continue;
                                Local(f, _lx[li], _lz[li], out double s, out double t);
                                if (Math.Abs(s) < TagMin || Math.Abs(s) > VoidD || Math.Abs(t) > f.Hw + VoidW) continue;
                                bool vent = leafVent[li] == 1;
                                if (s > 0) { cntP++; if (vent) layP1++; else layP0++; }
                                else { cntM++; if (vent) layM1++; else layM0++; }
                            }
                        }
                    f.Void = Math.Min(cntP, cntM) < VoidK;
                    // a door whose two sides are in different kit layers (a vent iris) is already a layer boundary
                    f.Cross = (layP1 > layP0) != (layM1 > layM0);
                }
                List<Frame> live = _frames.Where(o => !o.Void).ToList();

                // ---- P4 walls: wall-kit entities at any depth (top-level items only without instances) that are placed directly in
                //      the level; doors and everything under a door excluded. Every Item carries a rotation, so none is skipped for
                //      lacking one.
                Dictionary<string, bool> wide = new Dictionary<string, bool>(StringComparer.Ordinal);   // node -> its content leaves span more than MAX_EXT
                // kit is placed directly in the level: a top-level item, or a nested instance whose parent's content leaves span
                // more than MAX_EXT (a level / section composite); parts of props, FX and other small composites are not kit
                bool PlacedInLevel(Item it)
                {
                    if (itemPaths.Contains(it.Path)) return true;
                    int cut = it.Path.LastIndexOf('/');
                    string par = cut < 0 ? it.Path : it.Path.Substring(0, cut);
                    if (!wide.TryGetValue(par, out bool w))
                    {
                        w = nodeLeaves.TryGetValue(par, out List<int> pl) && pl.Count > 0 && Ext(pl) > MaxExt;
                        wide[par] = w;
                    }
                    return w;
                }
                Dictionary<string, int> wallOf = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (Item it in typed)
                {
                    if (DoorOf(it.Path) != null || !IsWall(it.Type)) continue;
                    if (!PlacedInLevel(it)) continue;
                    double a = (double)it.Rotation.Y * DegToRad;
                    wallOf[it.Path] = _walls.Count;
                    _walls.Add(new Wall()
                    {
                        Path = it.Path,
                        Ox = it.Position.X, Oy = it.Position.Y, Oz = it.Position.Z,
                        Zx = Math.Sin(a), Zz = Math.Cos(a),
                        Xx = Math.Cos(a), Xz = -Math.Sin(a),
                        Window = IsWindow(it.Type),
                    });
                }

                // ---- P4b bulkhead planes: a corridor bulkhead (ring frame across a corridor; its local +Z is the corridor axis)
                //      narrows the corridor; partition links may not cross the plane through its pivot with normal local +Z within
                //      BULK_HW along local X (vertically DOOR_YLO..DOOR_YHI, as a doorway with no door and no tokens). Skipped:
                //      vent-layer bulkheads (ducts), and a bulkhead parallel to a door within BULK_MOD of it (the rib of that door's
                //      module: the door already splits)
                foreach (Item it in typed)
                {
                    if (DoorOf(it.Path) != null || !IsBulkhead(it.Type)) continue;
                    if (IsVent(it.Type) || VentPath(it.Path) || !PlacedInLevel(it)) continue;
                    double a = (double)it.Rotation.Y * DegToRad;
                    double axx = Math.Cos(a), axz = -Math.Sin(a), azx = Math.Sin(a), azz = Math.Cos(a);
                    double ox = it.Position.X, oy = it.Position.Y, oz = it.Position.Z;
                    bool rib = false;
                    foreach (Frame f in _frames)
                    {
                        if (Math.Abs(f.Cy - oy) >= RibDy) continue;
                        if (Sq(f.Cx - ox) + Sq(f.Cz - oz) > Sq(BulkMod)) continue;
                        if (Math.Abs(f.Nx * azx + f.Nz * azz) > RibCos) { rib = true; break; }
                    }
                    if (rib) continue;
                    _bulks.Add(new Frame()
                    {
                        Door = null,
                        Cx = ox, Cy = oy, Cz = oz,
                        Nx = azx, Nz = azz, Ux = axx, Uz = axz, Hw = BulkHw - DoorHwPad,
                    });
                }

                // ---- P5 units
                double Ext(List<int> ls)
                {
                    double xmin = double.PositiveInfinity, xmax = double.NegativeInfinity, zmin = double.PositiveInfinity, zmax = double.NegativeInfinity;
                    foreach (int li in ls)
                    {
                        double x = _lx[li], z = _lz[li];
                        if (x < xmin) xmin = x;
                        if (x > xmax) xmax = x;
                        if (z < zmin) zmin = z;
                        if (z > zmax) zmax = z;
                    }
                    return PyMax(xmax - xmin, zmax - zmin);
                }
                bool Straddles(List<int> ls)
                {
                    foreach (Frame f in live)
                    {
                        bool pos = false, neg = false;
                        foreach (int li in ls)
                        {
                            double dy = _ly[li] - f.Cy;
                            if (dy < DoorYlo || dy > DoorYhi) continue;
                            Local(f, _lx[li], _lz[li], out double s, out double t);
                            if (Math.Abs(s) < TagMin || Math.Abs(s) > StraddleD || Math.Abs(t) > f.Hw + StraddleW) continue;
                            if (s > 0) pos = true;
                            else neg = true;
                            if (pos && neg) return true;
                        }
                    }
                    return false;
                }
                // the subtree holds both vent-layer and other content
                bool Mixed(string p)
                {
                    int v = nodeVent.TryGetValue(p, out int c) ? c : 0;
                    return 0 < v && v < nodeLeaves[p].Count;
                }
                void Rec(string p)
                {
                    List<int> ls = nodeLeaves[p];
                    if (children.TryGetValue(p, out HashSet<string> kids) && kids.Count > 0 && (Ext(ls) > MaxExt || Mixed(p) || (!wallOf.ContainsKey(p) && Straddles(ls))))
                    {
                        List<string> sorted = new List<string>(kids);
                        sorted.Sort(StringComparer.Ordinal);
                        foreach (string c in sorted) Rec(c);
                    }
                    else
                    {
                        _units.Add(new Unit() { Path = p, Leaves = ls, Wall = wallOf.TryGetValue(p, out int w) ? w : -1, Layer = VentPath(p) ? 1 : 0 });
                    }
                }
                foreach (Item it in items)
                {
                    if (_dset.Contains(it.Path) || !nodeLeaves.ContainsKey(it.Path)) continue;
                    Rec(it.Path);
                }
                // leaves under no item (not expected): their own top-level ids
                HashSet<string> seenTop = new HashSet<string>(items.Select(o => o.Path), StringComparer.Ordinal);
                HashSet<string> orphanTops = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < nl; i++) if (!seenTop.Contains(leafTop[i])) orphanTops.Add(leafTop[i]);
                List<string> orphans = orphanTops.ToList();
                orphans.Sort(StringComparer.Ordinal);
                foreach (string top in orphans) Rec(top);

                // ---- P6 points
                List<double> px = new List<double>(), py = new List<double>(), pz = new List<double>();
                List<int> pu = new List<int>();
                HashSet<(int, int, int)> seen = new HashSet<(int, int, int)>();
                for (int ui = 0; ui < _units.Count; ui++)
                {
                    seen.Clear();
                    foreach (int li in _units[ui].Leaves)
                    {
                        if (!seen.Add((_vx[li], _vy[li], _vz[li]))) continue;
                        px.Add(_lx[li]); py.Add(_ly[li]); pz.Add(_lz[li]); pu.Add(ui);
                    }
                }
                _px = px.ToArray(); _py = py.ToArray(); _pz = pz.ToArray(); _pu = pu.ToArray();
                _uwall = _units.Select(o => o.Wall).ToArray();
                _ulayer = _units.Select(o => o.Layer).ToArray();

                foreach (Item it in items)
                {
                    _itemPath.Add(it.Path);
                    _ix.Add(it.Position.X); _iy.Add(it.Position.Y); _iz.Add(it.Position.Z);
                }

                // the door instance a path lies under (shortest such prefix), or null
                string DoorOf(string path)
                {
                    int from = 0;
                    while (true)
                    {
                        int slash = path.IndexOf('/', from);
                        string p = slash < 0 ? path : path.Substring(0, slash);
                        if (_dset.Contains(p)) return p;
                        if (slash < 0) return null;
                        from = slash + 1;
                    }
                }
                // vent layer: the path itself or any ancestor on it is an instance of a vent type
                bool VentPath(string path)
                {
                    if (ventInst.Count == 0) return false;
                    int from = 0;
                    while (true)
                    {
                        int slash = path.IndexOf('/', from);
                        string p = slash < 0 ? path : path.Substring(0, slash);
                        if (ventInst.Contains(p)) return true;
                        if (slash < 0) return false;
                        from = slash + 1;
                    }
                }
            }

            #region API
            /// <summary>Split the content into zones: entry path -> zone number. A leaf takes the number of its longest listed prefix; doors are never listed.</summary>
            public Dictionary<string, int> Partition()
            {
                if (_partition == null) _partition = ComputePartition();
                return new Dictionary<string, int>(_partition, StringComparer.Ordinal);
            }

            /// <summary>The zone pairs that need joining, given each leaf's zone (null for none or several): 'door' pairs with their door, and 'open' pairs with a reason.</summary>
            public List<Pair> Neighbours(Func<Leaf, string> zoneOf)
            {
                if (zoneOf == null) throw new ArgumentNullException(nameof(zoneOf));
                List<MemberPoint> mp = MemberPoints(zoneOf);
                Dictionary<(string, string, string), Pair> found = new Dictionary<(string, string, string), Pair>();
                Dictionary<(string, string), Door> doorPairs = new Dictionary<(string, string), Door>();
                void Add(string a, string b, string kind, Door door, string reason)
                {
                    if (string.CompareOrdinal(a, b) > 0) { string x = a; a = b; b = x; }
                    if (found.ContainsKey((a, b, kind))) return;
                    found[(a, b, kind)] = new Pair() { A = a, B = b, Kind = kind, Door = door, Reason = reason };
                }

                // door links (and open links for see-through doors)
                foreach (Door d in _doors)
                {
                    if (!_frameByPath.TryGetValue(d.Path, out int fi)) continue;
                    Frame f = _frames[fi];
                    if (f.Void) continue;
                    (string a, string b)? r = BridgeVotes(mp, f);
                    if (r == null || r.Value.a == r.Value.b) continue;
                    (string lo, string hi) = Ordered(r.Value.a, r.Value.b);
                    Add(lo, hi, "door", d, null);
                    if (!doorPairs.ContainsKey((lo, hi))) doorPairs[(lo, hi)] = d;
                    if (f.Window) Add(lo, hi, "open", d, "window_door");
                }
                // see-through walls: the zones on their two faces
                foreach (Wall w in _walls)
                {
                    if (!w.Window) continue;
                    SideVotes(mp, w.Ox, w.Oy, w.Oz, w.Zx, w.Zz, w.Xx, w.Xz, WinHw, BrD, BrW, out Dictionary<string, double> vp, out Dictionary<string, double> vm);
                    if (vp.Count == 0 || vm.Count == 0) continue;
                    string a = Ranked(vp)[0].Key, b = Ranked(vm)[0].Key;
                    if (a != b) Add(a, b, "open", null, "window_wall");
                }
                // contacts: point pairs of different units and zones, same layer, not crossing a doorway or a solid wall
                Filter flt = new Filter(this, ROpen, true);
                Dictionary<(string, string), int> contacts = new Dictionary<(string, string), int>();
                Dictionary<(string, string), int> away = new Dictionary<(string, string), int>();
                const double E2 = DoorExcl * DoorExcl;
                int nm = mp.Count;
                double[] mx_ = new double[nm], my_ = new double[nm], mz_ = new double[nm];
                for (int i = 0; i < nm; i++) { mx_[i] = mp[i].X; my_[i] = mp[i].Y; mz_[i] = mp[i].Z; }
                Pairs(mx_, my_, mz_, nm, ROpen, Dy, (i, j, dist) =>
                {
                    MemberPoint a = mp[i], b = mp[j];
                    if (a.Zone == b.Zone || a.Unit == b.Unit || _ulayer[a.Unit] != _ulayer[b.Unit]) return;
                    if (!flt.Ok(a.X, a.Y, a.Z, b.X, b.Y, b.Z, _uwall[a.Unit], _uwall[b.Unit], true, -1)) return;
                    (string, string) k = string.CompareOrdinal(a.Zone, b.Zone) < 0 ? (a.Zone, b.Zone) : (b.Zone, a.Zone);
                    contacts.TryGetValue(k, out int c);
                    contacts[k] = c + 1;
                    double mx = 0.5 * (a.X + b.X), mz = 0.5 * (a.Z + b.Z);
                    bool farFromAll = true;
                    foreach (Frame f in _frames)
                    {
                        double ddx = mx - f.Cx, ddz = mz - f.Cz;
                        if (Math.Abs(ddx) > DoorExcl + 1e-6 || Math.Abs(ddz) > DoorExcl + 1e-6) continue;   // certainly beyond DOOR_EXCL
                        if (!(Sq(ddx) + Sq(ddz) > E2)) { farFromAll = false; break; }
                    }
                    if (farFromAll)
                    {
                        away.TryGetValue(k, out int c2);
                        away[k] = c2 + 1;
                    }
                });
                List<(string, string)> keys = contacts.Keys.ToList();
                keys.Sort((p, q) => { int c = string.CompareOrdinal(p.Item1, q.Item1); return c != 0 ? c : string.CompareOrdinal(p.Item2, q.Item2); });
                foreach ((string, string) k in keys)
                {
                    if (!doorPairs.TryGetValue(k, out Door door))
                    {
                        if (contacts[k] >= OpenK) Add(k.Item1, k.Item2, "open", null, "contact");
                    }
                    else if (away.TryGetValue(k, out int n) && n >= OpenKDoor)
                    {
                        Add(k.Item1, k.Item2, "open", door, "beside_door");
                    }
                }

                List<Pair> pairs = found.Values.ToList();
                foreach (Pair p in pairs) p.Contacts = contacts.TryGetValue((p.A, p.B), out int c) ? c : 0;
                pairs.Sort((p, q) =>
                {
                    int c = string.CompareOrdinal(p.A, q.A);
                    if (c == 0) c = string.CompareOrdinal(p.B, q.B);
                    if (c == 0) c = string.CompareOrdinal(p.Kind, q.Kind);
                    return c;
                });
                return pairs;
            }

            /// <summary>The two zones a door joins, from the zoned content either side of its doorway; null for a dead end or when one side has no zone.</summary>
            public (string a, string b)? Bridge(Door door, Func<Leaf, string> zoneOf) => Bridge(door, zoneOf, out _);

            /// <summary>As <see cref="Bridge(Door, Func{Leaf, string})"/>; <paramref name="both"/> is the zone that wins both sides when that is why it is null (zones merged across the door), else null.</summary>
            public (string a, string b)? Bridge(Door door, Func<Leaf, string> zoneOf, out string both)
            {
                both = null;
                if (door == null) throw new ArgumentNullException(nameof(door));
                if (zoneOf == null) throw new ArgumentNullException(nameof(zoneOf));
                if (!_frameByPath.TryGetValue(door.Path, out int fi)) return null;
                Frame f = _frames[fi];
                if (f.Void) return null;
                // only the leaves around the doorway are read; a unit's (zone, voxel) de-duplication sees every leaf that could
                // share a voxel with one in the vote box, so the kept points in the box are the ones the full member set has
                List<Candidate> cands = Candidates(fi);
                List<MemberPoint> mp = new List<MemberPoint>();
                HashSet<(string, int, int, int)> seen = new HashSet<(string, int, int, int)>();
                int unit = -1;
                foreach (Candidate c in cands)
                {
                    if (c.Unit != unit) { unit = c.Unit; seen.Clear(); }
                    string zone = zoneOf(_leaf[c.Leaf]);
                    if (zone == null) continue;
                    if (!seen.Add((zone, _vx[c.Leaf], _vy[c.Leaf], _vz[c.Leaf]))) continue;
                    mp.Add(new MemberPoint() { X = _lx[c.Leaf], Y = _ly[c.Leaf], Z = _lz[c.Leaf], Unit = c.Unit, Zone = zone });
                }
                return BridgeVotes(mp, f, out both);
            }

            /// <summary>The content with the most weight on each side of a door's doorway, as <see cref="Bridge(Door, Func{Leaf, string})"/> weighs it, with content no
            /// one zone claims counted as <paramref name="none"/>: null for a side with no content (or a dead end).</summary>
            public void SideWinners(Door door, Func<Leaf, string> zoneOf, string none, out string plus, out string minus)
            {
                plus = minus = null;
                if (door == null || zoneOf == null || !_frameByPath.TryGetValue(door.Path, out int fi)) return;
                Frame f = _frames[fi];
                if (f.Void) return;
                List<MemberPoint> mp = new List<MemberPoint>();
                HashSet<(string, int, int, int)> seen = new HashSet<(string, int, int, int)>();
                int unit = -1;
                foreach (Candidate c in Candidates(fi))
                {
                    if (c.Unit != unit) { unit = c.Unit; seen.Clear(); }
                    string zone = zoneOf(_leaf[c.Leaf]) ?? none;
                    if (!seen.Add((zone, _vx[c.Leaf], _vy[c.Leaf], _vz[c.Leaf]))) continue;
                    mp.Add(new MemberPoint() { X = _lx[c.Leaf], Y = _ly[c.Leaf], Z = _lz[c.Leaf], Unit = c.Unit, Zone = zone });
                }
                foreach (double depth in new[] { BrD, BrDFar })
                {
                    SideVotes(mp, f.Cx, f.Cy, f.Cz, f.Nx, f.Nz, f.Ux, f.Uz, f.Hw, depth, BrW, out Dictionary<string, double> vp, out Dictionary<string, double> vm);
                    if (vp.Count == 0 || vm.Count == 0) continue;
                    plus = Ranked(vp)[0].Key;
                    minus = Ranked(vm)[0].Key;
                    return;
                }
            }

            /// <summary>Whether nothing stands on one side of the door: it leads nowhere and joins nothing.</summary>
            public bool IsDeadEnd(Door door)
            {
                return door != null && _frameByPath.TryGetValue(door.Path, out int fi) && _frames[fi].Void;
            }

            /// <summary>Whether the door can be seen through (a window): the rooms either side see each other with it shut.</summary>
            public bool IsSeeThrough(Door door)
            {
                return door != null && _frameByPath.TryGetValue(door.Path, out int fi) && _frames[fi].Window;
            }
            #endregion

            #region Partition
            private Dictionary<string, int> ComputePartition()
            {
                int n = _units.Count;
                int np = _pu.Length;
                // ---- S4 links (doorways, bulkhead planes and walls block)
                Filter flt = new Filter(this, RLink, false, true);
                Dictionary<long, double> dmin = new Dictionary<long, double>();
                Dictionary<long, double> cnt = new Dictionary<long, double>();
                Pairs(_px, _py, _pz, np, RLink, Dy, (i, j, d) =>
                {
                    int ua = _pu[i], ub = _pu[j];
                    if (ua == ub || _ulayer[ua] != _ulayer[ub]) return;
                    if (!flt.Ok(_px[i], _py[i], _pz[i], _px[j], _py[j], _pz[j], _uwall[ua], _uwall[ub], true, -1)) return;
                    long k = ua < ub ? PairKey(ua, ub) : PairKey(ub, ua);
                    if (!dmin.TryGetValue(k, out double best) || d < best) dmin[k] = d;
                    cnt.TryGetValue(k, out double c);
                    cnt[k] = c + 1.0;
                });
                List<long> cntKeys = cnt.Keys.ToList();
                cntKeys.Sort();

                // ---- S5 tokens
                HashSet<int>[] tags = Tokens();
                int[] size = new int[n];
                for (int i = 0; i < np; i++) size[_pu[i]]++;
                State st = new State(n, size, tags);

                // ---- S6 phase A: constrained single linkage up to R_ATOM
                List<long> dkeys = dmin.Keys.ToList();
                dkeys.Sort((p, q) =>
                {
                    double a = dmin[p], b = dmin[q];
                    if (a < b) return -1;
                    if (a > b) return 1;
                    return p.CompareTo(q);
                });
                foreach (long k in dkeys)
                {
                    if (dmin[k] > RAtom) break;
                    int a = st.Find(PairA(k)), b = st.Find(PairB(k));
                    if (a == b || Conflict(st.Tags[a], st.Tags[b])) continue;
                    st.Union(a, b);
                }

                // ---- S7 phase B: constant-Potts agglomeration
                Potts(st, cnt, cntKeys, n);
                // ---- S8 tiny clusters
                AbsorbTiny(st, cnt, cntKeys);
                // ---- S9 door guarantee: no bucket may hold the content just in front of both faces of a live door
                if (Guarantee) DoorGuarantee(st, dmin, tags);

                Dictionary<string, int> output = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int ui = 0; ui < n; ui++) output[_units[ui].Path] = st.Find(ui);
                // ---- S10 items without leaves: bucket of the nearest point
                HashSet<string> ancestors = new HashSet<string>(StringComparer.Ordinal);
                foreach (Unit u in _units)
                {
                    int from = 0;
                    while (true)
                    {
                        int slash = u.Path.IndexOf('/', from);
                        if (slash < 0) break;
                        ancestors.Add(u.Path.Substring(0, slash));
                        from = slash + 1;
                    }
                }
                if (np > 0)
                {
                    Nearest near = new Nearest(_px, _py, _pz);
                    for (int k = 0; k < _itemPath.Count; k++)
                    {
                        string path = _itemPath[k];
                        if (output.ContainsKey(path) || _dset.Contains(path) || ancestors.Contains(path)) continue;
                        int i = near.Query(_ix[k], _iy[k], _iz[k], null).i;
                        if (i >= 0) output[path] = st.Find(_pu[i]);
                    }
                }
                return output;
            }

            /// <summary>unit -> set of door tokens (door index, side): units with points in the token box of a live same-layer
            /// door that see the doorway get the token of their majority side.</summary>
            private HashSet<int>[] Tokens()
            {
                HashSet<int>[] tags = new HashSet<int>[_units.Count];
                List<int> live = new List<int>();
                for (int di = 0; di < _frames.Count; di++) if (!_frames[di].Void && !_frames[di].Cross) live.Add(di);
                if (live.Count == 0) return tags;
                double maxHw = double.NegativeInfinity;
                foreach (int di in live) maxHw = PyMax(maxHw, _frames[di].Hw);
                double vcell = Math.Sqrt(Sq(TagD) + Sq(maxHw + TagW)) + 0.5;
                Filter vis = new Filter(this, vcell, false);
                foreach (int di in live)
                {
                    Frame f = _frames[di];
                    Dictionary<int, int[]> votes = new Dictionary<int, int[]>();
                    for (int i = 0; i < _pu.Length; i++)
                    {
                        double x = _px[i], y = _py[i], z = _pz[i];
                        double dy = y - f.Cy;
                        if (dy < DoorYlo || dy > DoorYhi) continue;
                        Local(f, x, z, out double s, out double t);
                        if (Math.Abs(s) < TagMin || Math.Abs(s) > TagD) continue;
                        if (Math.Abs(t) > f.Hw + TagW) continue;
                        double sg = s > 0 ? 0.3 : -0.3;
                        double qx = f.Cx + f.Nx * sg, qz = f.Cz + f.Nz * sg;   // the doorway mouth on the point's side
                        int ui = _pu[i];
                        if (!vis.Ok(x, y, z, qx, y, qz, _uwall[ui], -1, false, di)) continue;
                        if (!votes.TryGetValue(ui, out int[] v)) votes[ui] = v = new int[2];
                        v[s > 0 ? 0 : 1]++;
                    }
                    List<int> voters = votes.Keys.ToList();
                    voters.Sort();
                    foreach (int ui in voters)
                    {
                        int[] v = votes[ui];
                        if (tags[ui] == null) tags[ui] = new HashSet<int>();
                        tags[ui].Add(Token(di, v[0] >= v[1] ? 1 : -1));
                    }
                }
                return tags;
            }

            private sealed class State
            {
                public readonly int[] Parent;
                public readonly int[] Size;
                public readonly HashSet<int>[] Tags;

                public State(int n, int[] size, HashSet<int>[] tags)
                {
                    Parent = new int[n];
                    for (int i = 0; i < n; i++) Parent[i] = i;
                    Size = (int[])size.Clone();
                    Tags = new HashSet<int>[n];
                    for (int i = 0; i < n; i++) Tags[i] = tags[i] == null ? new HashSet<int>() : new HashSet<int>(tags[i]);
                }

                public int Find(int x)
                {
                    int[] p = Parent;
                    while (p[x] != x)
                    {
                        p[x] = p[p[x]];
                        x = p[x];
                    }
                    return x;
                }

                /// <summary>merge root b into root a</summary>
                public void Union(int a, int b)
                {
                    Parent[b] = a;
                    Size[a] += Size[b];
                    Tags[a].UnionWith(Tags[b]);
                }
            }

            private struct PottsEntry
            {
                public double NegGain;
                public int X, Y, VX, VY;
            }

            private static int ComparePotts(PottsEntry p, PottsEntry q)
            {
                if (p.NegGain < q.NegGain) return -1;
                if (p.NegGain > q.NegGain) return 1;
                if (p.X != q.X) return p.X < q.X ? -1 : 1;
                if (p.Y != q.Y) return p.Y < q.Y ? -1 : 1;
                if (p.VX != q.VX) return p.VX < q.VX ? -1 : 1;
                if (p.VY != q.VY) return p.VY < q.VY ? -1 : 1;
                return 0;
            }

            private static readonly double[] KCos = Enumerable.Range(0, 8).Select(k => Math.Cos(Math.PI * k / 8.0)).ToArray();
            private static readonly double[] KSin = Enumerable.Range(0, 8).Select(k => Math.Sin(Math.PI * k / 8.0)).ToArray();

            private void Potts(State st, Dictionary<long, double> cnt, List<long> cntKeys, int n)
            {
                Dictionary<int, Dictionary<int, double>> adj = new Dictionary<int, Dictionary<int, double>>();
                Dictionary<int, double> Adj(int r)
                {
                    if (!adj.TryGetValue(r, out Dictionary<int, double> d)) adj[r] = d = new Dictionary<int, double>();
                    return d;
                }
                foreach (long k in cntKeys)
                {
                    double w = cnt[k];
                    int ra = st.Find(PairA(k)), rb = st.Find(PairB(k));
                    if (ra == rb) continue;
                    Dictionary<int, double> da = Adj(ra), db = Adj(rb);
                    da.TryGetValue(rb, out double x1);
                    da[rb] = x1 + w;
                    db.TryGetValue(ra, out double x2);
                    db[ra] = x2 + w;
                }
                double[] mass = new double[n];
                for (int i = 0; i < n; i++) if (st.Find(i) == i) mass[i] = st.Size[i];
                double lam = Lambda * Sq(RLink / LambdaR);
                // 8-direction extents (k-DOP) of every current cluster: lo[k], hi[k] of x*cos + z*sin
                double[][] lo = new double[n][], hi = new double[n][];
                for (int i = 0; i < _pu.Length; i++)
                {
                    int r = st.Find(_pu[i]);
                    double x = _px[i], z = _pz[i];
                    if (lo[r] == null)
                    {
                        lo[r] = new double[8]; hi[r] = new double[8];
                        for (int k = 0; k < 8; k++) lo[r][k] = hi[r][k] = x * KCos[k] + z * KSin[k];
                    }
                    else
                    {
                        for (int k = 0; k < 8; k++)
                        {
                            double v = x * KCos[k] + z * KSin[k];
                            if (v < lo[r][k]) lo[r][k] = v;
                            if (v > hi[r][k]) hi[r][k] = v;
                        }
                    }
                }
                double cap = _bulks.Count > 0 ? MaxSpanBulk : MaxSpan;
                bool TooWide(int x, int y)
                {
                    double m = double.NegativeInfinity;
                    for (int k = 0; k < 8; k++) m = PyMax(m, PyMax(hi[x][k], hi[y][k]) - PyMin(lo[x][k], lo[y][k]));
                    return m > cap;
                }
                double Gain(int x, int y) { return adj[x][y] - lam * mass[x] * mass[y]; }

                int[] ver = new int[n];
                MinHeap<PottsEntry> heap = new MinHeap<PottsEntry>(ComparePotts);
                List<int> roots = adj.Keys.ToList();
                roots.Sort();
                foreach (int x in roots)
                {
                    List<int> ys = adj[x].Keys.ToList();
                    ys.Sort();
                    foreach (int y in ys)
                        if (x < y) heap.Push(new PottsEntry() { NegGain = -Gain(x, y), X = x, Y = y, VX = 0, VY = 0 });
                }
                while (heap.Count > 0)
                {
                    PottsEntry e = heap.Pop();
                    int x = e.X, y = e.Y;
                    if (st.Parent[x] != x || st.Parent[y] != y || ver[x] != e.VX || ver[y] != e.VY) continue;
                    if (-e.NegGain <= 0) break;                          // best valid gain not positive: stop
                    if (Conflict(st.Tags[x], st.Tags[y])) continue;      // token sets only grow: blocked for good
                    if (TooWide(x, y)) continue;                         // extents only grow: blocked for good
                    if (Adj(x).Count < Adj(y).Count) { int t = x; x = y; y = t; }   // survivor = more graph neighbours (tie: smaller id)
                    st.Union(x, y);
                    mass[x] += mass[y];
                    for (int k = 0; k < 8; k++)
                    {
                        lo[x][k] = PyMin(lo[x][k], lo[y][k]);
                        hi[x][k] = PyMax(hi[x][k], hi[y][k]);
                    }
                    Dictionary<int, double> ax = Adj(x), ay = Adj(y);
                    List<int> zs = ay.Keys.ToList();
                    zs.Sort();
                    foreach (int z in zs)
                    {
                        double w = ay[z];
                        if (z == x) continue;
                        ax.TryGetValue(z, out double cur);
                        ax[z] = cur + w;
                        Dictionary<int, double> az = Adj(z);
                        az[x] = ax[z];
                        az.Remove(y);
                    }
                    ax.Remove(y);
                    adj[y] = new Dictionary<int, double>();
                    ver[x]++;
                    zs = ax.Keys.ToList();
                    zs.Sort();
                    foreach (int z in zs)
                    {
                        int a = x < z ? x : z, b = x < z ? z : x;
                        heap.Push(new PottsEntry() { NegGain = -Gain(a, b), X = a, Y = b, VX = ver[a], VY = ver[b] });
                    }
                }
            }

            private struct AttachKey
            {
                public int A0, A1, A2;
                public double D2;
                public int I;

                public bool Less(AttachKey o)
                {
                    if (A0 != o.A0) return A0 < o.A0;
                    if (A1 != o.A1) return A1 < o.A1;
                    if (A2 != o.A2) return A2 < o.A2;
                    if (D2 < o.D2) return true;
                    if (D2 > o.D2) return false;
                    return I < o.I;
                }
            }

            private void AbsorbTiny(State st, Dictionary<long, double> cnt, List<long> cntKeys)
            {
                int n = _units.Count;
                int[] size = st.Size;
                // 1. strongest-contact non-conflicting neighbour, smallest clusters first, until stable
                while (true)
                {
                    Dictionary<long, double> W = new Dictionary<long, double>();
                    foreach (long k in cntKeys)
                    {
                        double c = cnt[k];
                        int ra = st.Find(PairA(k)), rb = st.Find(PairB(k));
                        if (ra == rb) continue;
                        if (size[ra] < SMin) { long key = PairKey(ra, rb); W.TryGetValue(key, out double v); W[key] = v + c; }
                        if (size[rb] < SMin) { long key = PairKey(rb, ra); W.TryGetValue(key, out double v); W[key] = v + c; }
                    }
                    List<long> wkeys = W.Keys.ToList();
                    wkeys.Sort();
                    Dictionary<int, (double w, int size, int negRb, int rb)> best = new Dictionary<int, (double, int, int, int)>();
                    foreach (long k in wkeys)
                    {
                        int ra = PairA(k), rb = PairB(k);
                        if (Conflict(st.Tags[ra], st.Tags[rb])) continue;
                        (double w, int size, int negRb, int rb) cand = (W[k], size[rb], -rb, rb);
                        if (!best.TryGetValue(ra, out (double w, int size, int negRb, int rb) cur) || Greater(cand, cur)) best[ra] = cand;
                    }
                    bool moved = false;
                    List<(int size, int r)> order = best.Keys.Select(r => (size[r], r)).ToList();
                    order.Sort((p, q) => p.size != q.size ? p.size.CompareTo(q.size) : p.r.CompareTo(q.r));
                    foreach ((int size, int r) entry in order)
                    {
                        int ra = entry.r;
                        int a = st.Find(ra), b = st.Find(best[ra].rb);
                        if (a == b || size[a] >= SMin || Conflict(st.Tags[a], st.Tags[b])) continue;
                        st.Union(b, a);
                        moved = true;
                    }
                    if (!moved) break;
                }
                // 1b. tiny groups: isolated tiny clusters group with each other first, so a sparse space (kit pieces more than R_LINK
                //     apart) becomes its own cluster instead of being spread over its neighbours. Point pairs of two different tiny
                //     clusters within ATTACH_R (XZ) and DY, same layer, not blocked (doorways and one-sided walls; bulkheads do not
                //     block here), in ascending (distance, i, j) order: union unless the token sets conflict (Kruskal). A group
                //     reaching S_MIN points is no longer tiny; smaller groups attach in step 2 as one cluster
                if (TinyGroup)
                {
                    SortedSet<int> groupRoots = new SortedSet<int>();
                    for (int i = 0; i < n; i++) groupRoots.Add(st.Find(i));
                    HashSet<int> gset = new HashSet<int>();
                    foreach (int r in groupRoots) if (size[r] < SMin) gset.Add(r);
                    if (gset.Count > 0 && gset.Count < groupRoots.Count)
                    {
                        List<int> tp = new List<int>();             // tiny points, point order (fixed before any union)
                        for (int i = 0; i < _pu.Length; i++) if (gset.Contains(st.Find(_pu[i]))) tp.Add(i);
                        int nt = tp.Count;
                        double[] tx = new double[nt], ty = new double[nt], tz = new double[nt];
                        for (int i = 0; i < nt; i++) { tx[i] = _px[tp[i]]; ty[i] = _py[tp[i]]; tz[i] = _pz[tp[i]]; }
                        Filter gflt = new Filter(this, AttachR, false);
                        List<(double d, int i, int j)> prs = new List<(double, int, int)>();
                        Pairs(tx, ty, tz, nt, AttachR, Dy, (i, j, d) =>
                        {
                            if (_ulayer[_pu[tp[i]]] == _ulayer[_pu[tp[j]]]) prs.Add((d, i, j));
                        });
                        prs.Sort((p, q) =>
                        {
                            int c = p.d.CompareTo(q.d);
                            if (c == 0) c = p.i.CompareTo(q.i);
                            if (c == 0) c = p.j.CompareTo(q.j);
                            return c;
                        });
                        foreach ((double d, int i, int j) e in prs)
                        {
                            int pa = tp[e.i], pb = tp[e.j];
                            int ua = _pu[pa], ub = _pu[pb];
                            int ra = st.Find(ua), rb = st.Find(ub);
                            if (ra == rb || Conflict(st.Tags[ra], st.Tags[rb])) continue;
                            if (!gflt.Ok(_px[pa], _py[pa], _pz[pa], _px[pb], _py[pb], _pz[pb], _uwall[ua], _uwall[ub], true, -1)) continue;
                            if (ra > rb) { int t = ra; ra = rb; rb = t; }
                            st.Union(ra, rb);
                        }
                    }
                }
                // 2. isolated tiny clusters, in ascending root id, each join one non-tiny cluster (fixed before any attach)
                SortedSet<int> rootSet = new SortedSet<int>();
                for (int i = 0; i < n; i++) rootSet.Add(st.Find(i));
                List<int> roots = rootSet.ToList();
                List<int> tiny = roots.Where(r => size[r] < SMin).ToList();
                if (tiny.Count == 0 || tiny.Count == roots.Count) return;
                HashSet<int> tset = new HashSet<int>(tiny);
                List<int> big = new List<int>();
                for (int i = 0; i < _pu.Length; i++) if (!tset.Contains(st.Find(_pu[i]))) big.Add(i);
                int nb = big.Count;
                double[] bx = new double[nb], by = new double[nb], bz = new double[nb];
                int[] bunit = new int[nb], broot = new int[nb];
                for (int i = 0; i < nb; i++)
                {
                    int p = big[i];
                    bx[i] = _px[p]; by[i] = _py[p]; bz[i] = _pz[p]; bunit[i] = _pu[p]; broot[i] = st.Find(_pu[p]);
                }
                Nearest near = new Nearest(bx, by, bz);
                Filter aflt = new Filter(this, AttachR, false);
                Dictionary<int, List<int>> members = new Dictionary<int, List<int>>();
                for (int i = 0; i < _pu.Length; i++) GetOrAdd(members, st.Find(_pu[i])).Add(i);
                double R2 = Sq(AttachR);
                int rings = (int)Math.Ceiling(AttachR / GridNear);
                foreach (int r in tiny)
                {
                    bool has = false;
                    AttachKey bestk = default(AttachKey);
                    foreach (int qi in members[r])
                    {
                        double qx = _px[qi], qy = _py[qi], qz = _pz[qi];
                        int lq = _ulayer[_pu[qi]], wq = _uwall[_pu[qi]];
                        int gx = Fl(qx, GridNear), gz = Fl(qz, GridNear);
                        for (int dx = -rings; dx <= rings; dx++)          // unblocked candidates within ATTACH_R
                            for (int dz = -rings; dz <= rings; dz++)
                            {
                                List<int> cell = near.Cell(gx + dx, gz + dz);
                                if (cell == null) continue;
                                foreach (int i in cell)
                                {
                                    double ox = bx[i], oy = by[i], oz = bz[i];
                                    if (Sq(ox - qx) + Sq(oz - qz) > R2 || Math.Abs(oy - qy) > Dy) continue;
                                    if (!aflt.Ok(qx, qy, qz, ox, oy, oz, wq, _uwall[bunit[i]], true, -1)) continue;
                                    double d2 = Sq(ox - qx) + Sq(VertW * (oy - qy)) + Sq(oz - qz);
                                    AttachKey k = new AttachKey()
                                    {
                                        A0 = 0,
                                        A1 = _ulayer[bunit[i]] != lq ? 1 : 0,
                                        A2 = Conflict(st.Tags[r], st.Tags[broot[i]]) ? 1 : 0,
                                        D2 = d2,
                                        I = i,
                                    };
                                    if (!has || k.Less(bestk)) { bestk = k; has = true; }
                                }
                            }
                        if (has && bestk.A0 == 0) continue;
                        for (int rank = 0; rank < 4; rank++)            // blocked fallback: nearest (dy weighted VERT_W) by (other layer, conflict)
                        {
                            int rk = rank, rr = r, lqq = lq;
                            Func<int, bool> acc = i =>
                            {
                                if (rk < 2 && _ulayer[bunit[i]] != lqq) return false;
                                if (rk % 2 == 0 && Conflict(st.Tags[rr], st.Tags[broot[i]])) return false;
                                return true;
                            };
                            (double d2, int found) = near.Query(qx, qy, qz, acc, 8, true, VertW);
                            if (found >= 0)
                            {
                                AttachKey k = new AttachKey() { A0 = 1, A1 = rank / 2, A2 = rank % 2, D2 = d2, I = found };
                                if (!has || k.Less(bestk)) { bestk = k; has = true; }
                                break;
                            }
                        }
                    }
                    if (has)
                    {
                        int tgt = st.Find(broot[bestk.I]), src = st.Find(r);
                        if (tgt != src) st.Union(tgt, src);
                    }
                }
            }

            private static bool Greater((double w, int size, int negRb, int rb) p, (double w, int size, int negRb, int rb) q)
            {
                if (p.w != q.w) return p.w > q.w;
                if (p.size != q.size) return p.size > q.size;
                return p.negRb > q.negRb;
            }

            private struct DijkstraEntry
            {
                public double D;
                public int Label, Unit;
            }

            private static int CompareDijkstra(DijkstraEntry p, DijkstraEntry q)
            {
                if (p.D < q.D) return -1;
                if (p.D > q.D) return 1;
                if (p.Label != q.Label) return p.Label < q.Label ? -1 : 1;
                if (p.Unit != q.Unit) return p.Unit < q.Unit ? -1 : 1;
                return 0;
            }

            /// <summary>For each live same-layer door (input order): if the cluster holding most points of the + side box also
            /// holds most points of the - side box, split it by a geodesic race over its unit link graph from its units in the
            /// two boxes.</summary>
            private void DoorGuarantee(State st, Dictionary<long, double> dmin, HashSet<int>[] tags)
            {
                int n = _units.Count;
                List<(int v, double w)>[] adj = new List<(int, double)>[n];
                for (int i = 0; i < n; i++) adj[i] = new List<(int, double)>();
                List<long> dkeys = dmin.Keys.ToList();
                dkeys.Sort();
                foreach (long k in dkeys)
                {
                    int a = PairA(k), b = PairB(k);
                    double w = dmin[k];
                    adj[a].Add((b, w));
                    adj[b].Add((a, w));
                }
                int[] usize = new int[n];
                for (int i = 0; i < _pu.Length; i++) usize[_pu[i]]++;
                for (int di = 0; di < _frames.Count; di++)
                {
                    Frame f = _frames[di];
                    if (f.Void || f.Cross) continue;
                    Dictionary<int, int> rootP = new Dictionary<int, int>(), rootM = new Dictionary<int, int>();
                    Dictionary<int, int> unitP = new Dictionary<int, int>(), unitM = new Dictionary<int, int>();
                    for (int i = 0; i < _pu.Length; i++)
                    {
                        double dy = _py[i] - f.Cy;
                        if (dy < DoorYlo || dy > DoorYhi) continue;
                        Local(f, _px[i], _pz[i], out double s, out double t);
                        if (Math.Abs(s) < TagMin || Math.Abs(s) > VoidD || Math.Abs(t) > f.Hw + VoidW) continue;
                        int ui = _pu[i];
                        int r = st.Find(ui);
                        if (s > 0) { Increment(rootP, r); Increment(unitP, ui); }
                        else { Increment(rootM, r); Increment(unitM, ui); }
                    }
                    if (rootP.Count == 0 || rootM.Count == 0) continue;
                    int rp = ArgMax(rootP), rm = ArgMax(rootM);
                    if (rp != rm) continue;
                    int R = rp;
                    SortedSet<int> seedUnits = new SortedSet<int>(unitP.Keys);
                    seedUnits.UnionWith(unitM.Keys);
                    List<DijkstraEntry> seeds = new List<DijkstraEntry>();
                    foreach (int u in seedUnits)
                    {
                        if (st.Find(u) != R) continue;
                        unitP.TryGetValue(u, out int cp);
                        unitM.TryGetValue(u, out int cm);
                        seeds.Add(new DijkstraEntry() { D = 0.0, Label = cp >= cm ? 0 : 1, Unit = u });   // 0 = + side, 1 = - side
                    }
                    if (seeds.Select(o => o.Label).Distinct().Count() < 2) continue;
                    MinHeap<DijkstraEntry> heap = new MinHeap<DijkstraEntry>(CompareDijkstra);
                    foreach (DijkstraEntry e in seeds) heap.Push(e);
                    Dictionary<int, int> label = new Dictionary<int, int>();
                    while (heap.Count > 0)
                    {
                        DijkstraEntry e = heap.Pop();
                        if (label.ContainsKey(e.Unit)) continue;
                        label[e.Unit] = e.Label;
                        foreach ((int v, double w) in adj[e.Unit])
                            if (!label.ContainsKey(v) && st.Find(v) == R) heap.Push(new DijkstraEntry() { D = e.D + w, Label = e.Label, Unit = v });
                    }
                    List<int> plus = new List<int>(), minus = new List<int>();
                    for (int u = 0; u < n; u++)
                    {
                        if (st.Find(u) != R) continue;
                        int lb = label.TryGetValue(u, out int x) ? x : 0;
                        if (lb == 0) plus.Add(u);
                        else if (lb == 1) minus.Add(u);
                    }
                    if (plus.Count == 0 || minus.Count == 0) continue;
                    foreach (List<int> grp in new[] { plus, minus })
                    {
                        int root = grp.Min();
                        foreach (int u in grp) st.Parent[u] = root;
                        int sum = 0;
                        foreach (int u in grp) sum += usize[u];
                        st.Size[root] = sum;
                        HashSet<int> tg = new HashSet<int>();
                        foreach (int u in grp) if (tags[u] != null) tg.UnionWith(tags[u]);
                        st.Tags[root] = tg;
                    }
                }
            }

            private static void Increment(Dictionary<int, int> counter, int key)
            {
                counter.TryGetValue(key, out int c);
                counter[key] = c + 1;
            }

            /// <summary>The key with the highest count (ties: the smaller key).</summary>
            private static int ArgMax(Dictionary<int, int> counter)
            {
                int best = 0, bestCount = -1;
                foreach (KeyValuePair<int, int> kv in counter)
                    if (kv.Value > bestCount || (kv.Value == bestCount && kv.Key < best)) { best = kv.Key; bestCount = kv.Value; }
                return best;
            }
            #endregion

            #region Bridge and neighbours
            /// <summary>points of leaves with a zone: de-duplicated per (unit, zone, voxel), in unit then leaf order</summary>
            private List<MemberPoint> MemberPoints(Func<Leaf, string> zoneOf)
            {
                List<MemberPoint> output = new List<MemberPoint>();
                HashSet<(string, int, int, int)> seen = new HashSet<(string, int, int, int)>();
                for (int ui = 0; ui < _units.Count; ui++)
                {
                    seen.Clear();
                    foreach (int li in _units[ui].Leaves)
                    {
                        string zone = zoneOf(_leaf[li]);
                        if (zone == null) continue;
                        if (!seen.Add((zone, _vx[li], _vy[li], _vz[li]))) continue;
                        output.Add(new MemberPoint() { X = _lx[li], Y = _ly[li], Z = _lz[li], Unit = ui, Zone = zone });
                    }
                }
                return output;
            }

            /// <summary>The unit leaves (unit then leaf order) near a door's bridge vote box, widened by a margin.</summary>
            private List<Candidate> Candidates(int fi)
            {
                if (_candidates == null) _candidates = new List<Candidate>[_frames.Count];
                if (_candidates[fi] != null) return _candidates[fi];
                Frame f = _frames[fi];
                List<Candidate> list = new List<Candidate>();
                double depth = Math.Max(BrD, BrDFar) + CandidateMargin, width = f.Hw + BrW + CandidateMargin;
                for (int ui = 0; ui < _units.Count; ui++)
                    foreach (int li in _units[ui].Leaves)
                    {
                        double dy = _ly[li] - f.Cy;
                        if (dy < DoorYlo - CandidateMargin || dy > DoorYhi + CandidateMargin) continue;
                        Local(f, _lx[li], _lz[li], out double s, out double t);
                        if (Math.Abs(s) > depth || Math.Abs(t) > width) continue;
                        list.Add(new Candidate() { Unit = ui, Leaf = li });
                    }
                _candidates[fi] = list;
                return list;
            }

            private static void SideVotes(List<MemberPoint> mp, double cx, double cy, double cz, double nx, double nz, double ux, double uz, double hw, double depth, double wmargin,
                out Dictionary<string, double> plus, out Dictionary<string, double> minus)
            {
                plus = new Dictionary<string, double>(StringComparer.Ordinal);
                minus = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (MemberPoint p in mp)
                {
                    double dy = p.Y - cy;
                    if (dy < DoorYlo || dy > DoorYhi) continue;
                    double dx = p.X - cx, dz = p.Z - cz;
                    double s = dx * nx + dz * nz, t = dx * ux + dz * uz;
                    if (Math.Abs(s) < BrMin || Math.Abs(s) > depth || Math.Abs(t) > hw + wmargin) continue;
                    Dictionary<string, double> side = s > 0 ? plus : minus;
                    side.TryGetValue(p.Zone, out double v);
                    side[p.Zone] = v + 1.0 / (0.5 + Math.Abs(s));
                }
            }

            /// <summary>votes ranked by (-vote, zone)</summary>
            private static List<KeyValuePair<string, double>> Ranked(Dictionary<string, double> votes)
            {
                List<KeyValuePair<string, double>> list = votes.ToList();
                list.Sort((p, q) =>
                {
                    if (p.Value > q.Value) return -1;
                    if (p.Value < q.Value) return 1;
                    return string.CompareOrdinal(p.Key, q.Key);
                });
                return list;
            }

            /// <summary>distance-weighted zone vote either side of the doorway along the passage axis</summary>
            private static (string a, string b)? BridgeVotes(List<MemberPoint> mp, Frame f) => BridgeVotes(mp, f, out _);

            private static (string a, string b)? BridgeVotes(List<MemberPoint> mp, Frame f, out string both)
            {
                both = null;
                foreach (double depth in new[] { BrD, BrDFar })
                {
                    SideVotes(mp, f.Cx, f.Cy, f.Cz, f.Nx, f.Nz, f.Ux, f.Uz, f.Hw, depth, BrW, out Dictionary<string, double> vp, out Dictionary<string, double> vm);
                    if (vp.Count == 0 || vm.Count == 0) continue;
                    List<KeyValuePair<string, double>> a = Ranked(vp), b = Ranked(vm);
                    if (a[0].Key != b[0].Key) return (a[0].Key, b[0].Key);
                    // same winner both sides: keep it where it dominates more, take the other side's runner-up
                    bool hasRa = a.Count > 1, hasRb = b.Count > 1;
                    if (!hasRa && !hasRb) { both = a[0].Key; return null; }
                    if (!hasRb || (hasRa && a[0].Value / a[1].Value < b[0].Value / b[1].Value)) return (a[1].Key, b[0].Key);
                    return (a[0].Key, b[1].Key);
                }
                return null;
            }
            #endregion

            #region Primitives
            /// <summary>Barrier index for link tests. Each doorway and wall segment is registered in every grid cell within its
            /// half-length + cell of its centre, so a query segment no longer than the cell sees every barrier it could cross by
            /// looking up its start's cell only. seeThrough: see-through doors and walls do not block. bulkheads: the bulkhead
            /// planes block too, as doorways (they follow the door frames, so a door's frame index is unchanged).</summary>
            private sealed class Filter
            {
                private readonly double _cell;
                private readonly List<Frame> _frames;
                private readonly List<Wall> _walls;
                private readonly Dictionary<long, List<int>> _fg = new Dictionary<long, List<int>>();
                private readonly Dictionary<long, List<int>> _wg = new Dictionary<long, List<int>>();

                public Filter(Geometry g, double cell, bool seeThrough, bool bulkheads = false)
                {
                    _cell = cell;
                    _frames = g._frames;
                    if (bulkheads && g._bulks.Count > 0)
                    {
                        _frames = new List<Frame>(g._frames);
                        _frames.AddRange(g._bulks);
                    }
                    _walls = g._walls;
                    for (int fi = 0; fi < _frames.Count; fi++)
                    {
                        Frame f = _frames[fi];
                        if (seeThrough && f.Window) continue;
                        double rr = f.Hw + DoorHwPad + cell;
                        for (int gx = Fl(f.Cx - rr, cell), gx1 = Fl(f.Cx + rr, cell); gx <= gx1; gx++)
                            for (int gz = Fl(f.Cz - rr, cell), gz1 = Fl(f.Cz + rr, cell); gz <= gz1; gz++)
                                GetOrAdd(_fg, Key(gx, gz)).Add(fi);
                    }
                    for (int wi = 0; wi < _walls.Count; wi++)
                    {
                        Wall w = _walls[wi];
                        if (seeThrough && w.Window) continue;
                        double rr = WallHl + cell;
                        for (int gx = Fl(w.Ox - rr, cell), gx1 = Fl(w.Ox + rr, cell); gx <= gx1; gx++)
                            for (int gz = Fl(w.Oz - rr, cell), gz1 = Fl(w.Oz + rr, cell); gz <= gz1; gz++)
                                GetOrAdd(_wg, Key(gx, gz)).Add(wi);
                    }
                }

                /// <summary>may a link a -> b exist? wa / wb: wall index of the unit owning a / b, -1 if that unit is not a wall</summary>
                public bool Ok(double ax, double ay, double az, double bx, double by, double bz, int wa, int wb, bool onesided, int skipDoor)
                {
                    List<Wall> W = _walls;
                    if (onesided)
                    {
                        if (wa >= 0)
                        {
                            Wall w = W[wa];
                            if ((bx - w.Ox) * w.Zx + (bz - w.Oz) * w.Zz > WallTol) return false;
                        }
                        if (wb >= 0)
                        {
                            Wall w = W[wb];
                            if ((ax - w.Ox) * w.Zx + (az - w.Oz) * w.Zz > WallTol) return false;
                        }
                    }
                    long key = Key(Fl(ax, _cell), Fl(az, _cell));
                    if (_fg.TryGetValue(key, out List<int> fs))
                        foreach (int fi in fs)
                        {
                            if (fi == skipDoor) continue;
                            Frame f = _frames[fi];
                            double cx = f.Cx, cy = f.Cy, cz = f.Cz;
                            double sa = (ax - cx) * f.Nx + (az - cz) * f.Nz;
                            double sb = (bx - cx) * f.Nx + (bz - cz) * f.Nz;
                            if ((sa > 0) == (sb > 0)) continue;
                            double tt = sa / (sa - sb);
                            double t = (ax + (bx - ax) * tt - cx) * f.Ux + (az + (bz - az) * tt - cz) * f.Uz;
                            double y = ay + (by - ay) * tt - cy;
                            if (Math.Abs(t) <= f.Hw + DoorHwPad && DoorYlo <= y && y <= DoorYhi) return false;
                        }
                    if (_wg.TryGetValue(key, out List<int> ws))
                        foreach (int wi in ws)
                        {
                            if (wi == wa || wi == wb) continue;
                            Wall w = W[wi];
                            double sa = (ax - w.Ox) * w.Zx + (az - w.Oz) * w.Zz;
                            double sb = (bx - w.Ox) * w.Zx + (bz - w.Oz) * w.Zz;
                            if ((sa > 0) == (sb > 0)) continue;
                            double tt = sa / (sa - sb);
                            double t = (ax + (bx - ax) * tt - w.Ox) * w.Xx + (az + (bz - az) * tt - w.Oz) * w.Xz;
                            if (Math.Abs(t) > WallHl) continue;
                            double y = ay + (by - ay) * tt - w.Oy;
                            if (WallYlo <= y && y <= WallYhi) return false;
                        }
                    return true;
                }
            }

            /// <summary>exact nearest (3D) among indexed points by ring search on an XZ grid; candidates may be filtered</summary>
            private sealed class Nearest
            {
                private readonly double[] _x, _y, _z;
                private readonly double _cell;
                private readonly Dictionary<long, List<int>> _g = new Dictionary<long, List<int>>();

                public Nearest(double[] x, double[] y, double[] z, double cell = GridNear)
                {
                    _x = x; _y = y; _z = z; _cell = cell;
                    for (int i = 0; i < x.Length; i++) GetOrAdd(_g, Key(Fl(x[i], cell), Fl(z[i], cell))).Add(i);
                }

                public List<int> Cell(int gx, int gz)
                {
                    return _g.TryGetValue(Key(gx, gz), out List<int> list) ? list : null;
                }

                /// <summary>(d2, index) of the nearest accepted point (ties: smaller index), or (inf, -1). Rings 0..maxRings of the
                /// XZ grid are searched, stopping after ring k once the best d2 &lt;= (k * cell)^2; if that never happens the
                /// remaining points are brute-forced when 'brute' is set.</summary>
                public (double d2, int i) Query(double x, double y, double z, Func<int, bool> accept, int maxRings = 8, bool brute = true, double vw = 1.0)
                {
                    int gx = Fl(x, _cell), gz = Fl(z, _cell);
                    double bd = double.PositiveInfinity;
                    int bi = -1;
                    for (int rad = 0; rad <= maxRings; rad++)
                    {
                        for (int dx = -rad; dx <= rad; dx++)
                            for (int dz = -rad; dz <= rad; dz++)
                            {
                                if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != rad) continue;
                                if (!_g.TryGetValue(Key(gx + dx, gz + dz), out List<int> list)) continue;
                                foreach (int i in list)
                                {
                                    double d = Sq(_x[i] - x) + Sq(vw * (_y[i] - y)) + Sq(_z[i] - z);
                                    if ((d < bd || (d == bd && i < bi)) && (accept == null || accept(i))) { bd = d; bi = i; }
                                }
                            }
                        if (bi >= 0 && bd <= Sq(rad * _cell)) return (bd, bi);
                    }
                    if (!brute) return (bd, bi);
                    for (int i = 0; i < _x.Length; i++)        // far away: brute force
                    {
                        double d = Sq(_x[i] - x) + Sq(vw * (_y[i] - y)) + Sq(_z[i] - z);
                        if ((d < bd || (d == bd && i < bi)) && (accept == null || accept(i))) { bd = d; bi = i; }
                    }
                    return (bd, bi);
                }
            }

            /// <summary>A binary min-heap on a total order (equal entries are identical, so the pop sequence is fixed).</summary>
            private sealed class MinHeap<T>
            {
                private readonly List<T> _a = new List<T>();
                private readonly Comparison<T> _cmp;

                public MinHeap(Comparison<T> cmp) { _cmp = cmp; }

                public int Count { get { return _a.Count; } }

                public void Push(T value)
                {
                    _a.Add(value);
                    int i = _a.Count - 1;
                    while (i > 0)
                    {
                        int p = (i - 1) >> 1;
                        if (_cmp(_a[i], _a[p]) >= 0) break;
                        T t = _a[i]; _a[i] = _a[p]; _a[p] = t;
                        i = p;
                    }
                }

                public T Pop()
                {
                    T top = _a[0];
                    int last = _a.Count - 1;
                    _a[0] = _a[last];
                    _a.RemoveAt(last);
                    int n = _a.Count, i = 0;
                    while (true)
                    {
                        int l = 2 * i + 1, r = l + 1, m = i;
                        if (l < n && _cmp(_a[l], _a[m]) < 0) m = l;
                        if (r < n && _cmp(_a[r], _a[m]) < 0) m = r;
                        if (m == i) break;
                        T t = _a[i]; _a[i] = _a[m]; _a[m] = t;
                        i = m;
                    }
                    return top;
                }
            }

            /// <summary>(i, j, d) for i &lt; j with XZ distance &lt;= R and |dy| &lt;= dyMax, on a grid of cell R visited in ascending
            /// cell order, then i ascending, then j over the 3x3 neighbourhood.</summary>
            private static void Pairs(double[] x, double[] y, double[] z, int count, double R, double dyMax, Action<int, int, double> emit)
            {
                Dictionary<long, List<int>> g = new Dictionary<long, List<int>>();
                for (int i = 0; i < count; i++) GetOrAdd(g, Key(Fl(x[i], R), Fl(z[i], R))).Add(i);
                double R2 = R * R;
                List<long> keys = g.Keys.ToList();
                keys.Sort();
                List<int> near = new List<int>();
                foreach (long key in keys)
                {
                    int gx = KeyX(key), gz = KeyZ(key);
                    near.Clear();
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                            if (g.TryGetValue(Key(gx + dx, gz + dz), out List<int> list)) near.AddRange(list);
                    foreach (int i in g[key])
                    {
                        double ax = x[i], ay = y[i], az = z[i];
                        foreach (int j in near)
                        {
                            if (j <= i) continue;
                            double ddx = ax - x[j], ddz = az - z[j];
                            double d2 = ddx * ddx + ddz * ddz;
                            if (d2 > R2 || Math.Abs(ay - y[j]) > dyMax) continue;
                            emit(i, j, Math.Sqrt(d2));
                        }
                    }
                }
            }

            /// <summary>(s, t): across the doorway, along it</summary>
            private static void Local(Frame f, double x, double z, out double s, out double t)
            {
                double dx = x - f.Cx, dz = z - f.Cz;
                s = dx * f.Nx + dz * f.Nz;
                t = dx * f.Ux + dz * f.Uz;
            }

            private static bool Conflict(HashSet<int> ta, HashSet<int> tb)
            {
                if (ta == null || tb == null || ta.Count == 0 || tb.Count == 0) return false;
                if (ta.Count > tb.Count) { HashSet<int> t = ta; ta = tb; tb = t; }
                foreach (int token in ta) if (tb.Contains(token ^ 1)) return true;
                return false;
            }

            /// <summary>door token (door index, side) as an int whose opposite side is token ^ 1</summary>
            private static int Token(int door, int side) { return door * 2 + (side > 0 ? 0 : 1); }

            /// <summary>Python's x ** 2 (the platform pow, not x * x: they differ in the last bit now and then)</summary>
            private static double Sq(double v) { return Math.Pow(Math.Abs(v), 2.0); }

            /// <summary>Python's max(a, b) / min(a, b): the first argument unless the second is strictly beyond it</summary>
            private static double PyMax(double a, double b) { return b > a ? b : a; }
            private static double PyMin(double a, double b) { return b < a ? b : a; }

            private static int Fl(double v, double c) { return (int)Math.Floor(v / c); }

            /// <summary>a grid cell key whose signed order is the (gx, gz) tuple order</summary>
            private static long Key(int gx, int gz) { return ((long)gx << 32) | (uint)(gz ^ int.MinValue); }
            private static int KeyX(long key) { return (int)(key >> 32); }
            private static int KeyZ(long key) { return (int)(key & 0xFFFFFFFFL) ^ int.MinValue; }

            /// <summary>an ordered unit pair (a, b) of non-negative ids as a key whose order is the tuple order</summary>
            private static long PairKey(int a, int b) { return ((long)a << 32) | (uint)b; }
            private static int PairA(long key) { return (int)(key >> 32); }
            private static int PairB(long key) { return (int)(key & 0xFFFFFFFFL); }

            private static (string, string) Ordered(string a, string b) { return string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a); }

            private static List<TV> GetOrAdd<TK, TV>(Dictionary<TK, List<TV>> d, TK key)
            {
                if (!d.TryGetValue(key, out List<TV> list)) d[key] = list = new List<TV>();
                return list;
            }

            private static string TypeLeaf(string t)
            {
                if (string.IsNullOrEmpty(t)) return "";
                string s = t.Replace('/', '\\');
                return s.Substring(s.LastIndexOf('\\') + 1).ToLowerInvariant();
            }

            /// <summary>wall kit: the last path segment contains none of arch / floor / ceiling, and split at '_' it has a token wall /
            /// walls / filler / door and no token volume (whole tokens, so 'Papers_Wallet' and 'Filler_Light_Volume' are not walls)</summary>
            private static bool IsWall(string t)
            {
                string n = TypeLeaf(t);
                if (n.Contains("arch") || n.Contains("floor") || n.Contains("ceiling")) return false;
                string[] toks = n.Split('_');          // empty tokens are kept and match nothing
                foreach (string tk in toks) if (tk == "volume") return false;
                foreach (string tk in toks) if (tk == "wall" || tk == "walls" || tk == "filler" || tk == "door") return true;
                return false;
            }

            /// <summary>corridor bulkhead kit: the last path segment contains 'bulkhead' and none of filler / door</summary>
            private static bool IsBulkhead(string t)
            {
                string n = TypeLeaf(t);
                return n.Contains("bulkhead") && !n.Contains("filler") && !n.Contains("door");
            }

            /// <summary>Whether a kit type (a door variant's composite path, say) is see-through: the rule <see cref="IsSeeThrough"/> applies to doors.</summary>
            internal static bool IsWindowType(string t) => IsWindow(t);

            //"no window(s)" names (Door_Lift_No_Windows) are not see-through
            private static bool IsWindow(string t) { return TypeLeaf(t).Replace("no_window", "").Replace("nowindow", "").Replace("without_window", "").Contains("window"); }

            private static bool IsVent(string t) { return (t ?? "").Replace('/', '\\').ToLowerInvariant().Contains("\\vents\\"); }
            #endregion
        }
    }
}
