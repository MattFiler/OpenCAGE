using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.Weapons
{
    /// <summary>
    /// The geometry the weapon-model wizard needs (<see cref="WeaponModelReplace"/>): how one of a weapon's composites sits against
    /// its first-person composite (a pickup prop draws the same gun in its own space), and how far a part pokes out of a model
    /// (the clearance check). Row-vector matrices throughout, as System.Numerics and the rest of OpenCAGE use them.
    /// </summary>
    internal static class WeaponGeometry
    {
        #region Which part is the body
        /// <summary>Upper case, letters and digits only, a Blender-style ".001" suffix dropped: how part and mesh names are compared.</summary>
        public static string Norm(string name)
        {
            string n = (name ?? "").Trim();
            int dot = n.LastIndexOf('.');
            if (dot > 0 && n.Substring(dot + 1).All(char.IsDigit)) n = n.Substring(0, dot);
            return new string(n.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        }

        /// <summary>
        /// The weapon's body among a composite's drawn parts (the part a model's unnamed meshes go on), by what doesn't change once parts
        /// have been replaced (a triangle count does): the part named after the weapon, else the one on the rig's first bone (-1 = none),
        /// else the largest (box diagonal), else the most triangles. Returns its index, or -1 for no parts.
        /// </summary>
        public static int MainPart(string weaponKey, IList<string> names, IList<int> bones, IList<float> sizes, IList<int> triangles)
        {
            if (names == null || names.Count == 0) return -1;
            string key = Norm(weaponKey);
            for (int i = 0; i < names.Count; i++)
                if (key.Length != 0 && Norm(names[i]) == key) return i;
            int bone = -1;
            for (int i = 0; i < names.Count; i++)
                if (bones[i] >= 0 && (bone < 0 || bones[i] < bones[bone])) bone = i;
            if (bone >= 0) return bone;
            int best = 0;
            for (int i = 1; i < names.Count; i++)
                if (sizes[i] > sizes[best] || (sizes[i] == sizes[best] && triangles[i] > triangles[best])) best = i;
            return best;
        }
        #endregion
        #region Fitting one composite's geometry onto another's
        /// <summary>How a target composite's drawn geometry lines up with the weapon's first-person geometry.</summary>
        internal sealed class FrameFit
        {
            /// <summary>Takes a point of the target composite's space into weapon space.</summary>
            public Matrix4x4 TargetToWeapon = Matrix4x4.Identity;
            /// <summary>Takes a point of weapon space into the target composite's space (the inverse).</summary>
            public Matrix4x4 WeaponToTarget = Matrix4x4.Identity;
            /// <summary>Mean distance, metres, between the two surfaces once lined up (both ways round).</summary>
            public float Residual;
            /// <summary>The weapon geometry's box diagonal, metres: what the residual is judged against.</summary>
            public float Size;
            /// <summary>The target's box extents over the weapon's, smallest over largest of the three sorted pairs (1 = the same size).</summary>
            public float ExtentRatio;
            /// <summary>No turn was needed (the two share a frame, as a weapon's first- and third-person composites do).</summary>
            public bool SameFrame;
            /// <summary>Whether the fit is good enough to put the new model there without being asked.</summary>
            public bool Accepted => Size > 0 && Residual <= Math.Max(0.01f, 0.05f * Size) && ExtentRatio >= 0.7f;
        }

        /// <summary>
        /// Find the rigid transform taking <paramref name="target"/>'s points (a composite's drawn geometry, in its own space) onto
        /// <paramref name="weapon"/>'s (the first-person geometry, in weapon space): each of the 24 quarter-turn orientations with the
        /// boxes centred, the best refined by a few rounds of closest-point alignment. The orientation that needs no turn wins any near tie,
        /// so two composites in one frame (the shotgun's Base and Base_TP) come out exactly the same frame.
        /// </summary>
        public static FrameFit Fit(List<Vector3> weapon, List<Vector3> target)
        {
            FrameFit fit = new FrameFit();
            if (weapon == null || target == null || weapon.Count < 4 || target.Count < 4)
                return fit;
            List<Vector3> a = Thin(weapon, 2500);
            List<Vector3> b = Thin(target, 2500);
            Bounds(a, out Vector3 aMin, out Vector3 aMax);
            fit.Size = (aMax - aMin).Length();
            Vector3 aCentre = (aMin + aMax) * 0.5f;

            Matrix4x4 best = Matrix4x4.Identity;
            float bestScore = float.MaxValue, identityScore = float.MaxValue;
            Matrix4x4 identityPlaced = Matrix4x4.Identity;
            foreach (Matrix4x4 turn in AxisTurns())
            {
                List<Vector3> turned = b.Select(o => Vector3.Transform(o, turn)).ToList();
                Bounds(turned, out Vector3 bMin, out Vector3 bMax);
                Matrix4x4 placed = turn * Matrix4x4.CreateTranslation(aCentre - (bMin + bMax) * 0.5f);
                float score = Residual(a, b, placed, 300);
                if (IsIdentity(turn)) { identityScore = score; identityPlaced = placed; }
                if (score < bestScore) { bestScore = score; best = placed; }
            }
            //A near tie goes to no turn at all: a symmetric shape (a flare's tube) fits several ways
            bool same = identityScore <= bestScore * 1.25f + 0.0005f;
            Matrix4x4 chosen = same ? identityPlaced : best;
            chosen = Refine(a, b, chosen, 12);
            fit.TargetToWeapon = chosen;
            Matrix4x4.Invert(chosen, out fit.WeaponToTarget);
            fit.Residual = Residual(a, b, chosen, 600);
            fit.SameFrame = same && TurnAngle(chosen) < 0.02f;

            List<Vector3> moved = b.Select(o => Vector3.Transform(o, chosen)).ToList();
            Bounds(moved, out Vector3 mMin, out Vector3 mMax);
            float[] ea = Sorted(aMax - aMin), eb = Sorted(mMax - mMin);
            fit.ExtentRatio = Enumerable.Range(0, 3).Select(i => Math.Min(ea[i], eb[i]) / Math.Max(1e-5f, Math.Max(ea[i], eb[i]))).Min();
            return fit;
        }

        private static float[] Sorted(Vector3 v) { float[] r = { v.X, v.Y, v.Z }; Array.Sort(r); return r; }

        /// <summary>The 24 rotations that take each axis onto an axis (signed permutations with determinant +1).</summary>
        public static IEnumerable<Matrix4x4> AxisTurns()
        {
            int[][] permutations = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };
            foreach (int[] p in permutations)
                for (int signs = 0; signs < 8; signs++)
                {
                    float[,] m = new float[3, 3];
                    for (int r = 0; r < 3; r++) m[r, p[r]] = ((signs >> r) & 1) == 0 ? 1 : -1;
                    float det = m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1]) - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0]) + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);
                    if (det < 0) continue;
                    yield return new Matrix4x4(m[0, 0], m[0, 1], m[0, 2], 0, m[1, 0], m[1, 1], m[1, 2], 0, m[2, 0], m[2, 1], m[2, 2], 0, 0, 0, 0, 1);
                }
        }

        private static bool IsIdentity(Matrix4x4 m) => m.M11 == 1 && m.M22 == 1 && m.M33 == 1;

        /// <summary>How far a matrix turns, radians.</summary>
        public static float TurnAngle(Matrix4x4 m)
        {
            float trace = m.M11 + m.M22 + m.M33;
            return (float)Math.Acos(Math.Max(-1.0, Math.Min(1.0, (trace - 1.0) / 2.0)));
        }

        /// <summary>Closest-point alignment (Horn's quaternion method each round), starting from <paramref name="start"/>.</summary>
        private static Matrix4x4 Refine(List<Vector3> a, List<Vector3> b, Matrix4x4 start, int rounds)
        {
            Matrix4x4 current = start;
            List<Vector3> sample = Thin(b, 400);
            for (int round = 0; round < rounds; round++)
            {
                List<Vector3> from = new List<Vector3>(), to = new List<Vector3>();
                List<float> distances = new List<float>();
                foreach (Vector3 point in sample)
                {
                    Vector3 p = Vector3.Transform(point, current);
                    Vector3 q = Nearest(a, p, out float d);
                    from.Add(p); to.Add(q); distances.Add(d);
                }
                //Leave out the worst fifth (bits one model has and the other doesn't)
                float cut = distances.OrderBy(o => o).ElementAt((int)(distances.Count * 0.8f));
                List<int> kept = Enumerable.Range(0, from.Count).Where(i => distances[i] <= cut).ToList();
                if (kept.Count < 4) break;
                Matrix4x4 step = Horn(kept.Select(i => from[i]).ToList(), kept.Select(i => to[i]).ToList());
                current = current * step;
                if (TurnAngle(step) < 1e-5f && step.Translation.Length() < 1e-6f) break;
            }
            return current;
        }

        /// <summary>
        /// The rotation and translation (row-vector matrix) best taking <paramref name="from"/> onto <paramref name="to"/>, pair by pair:
        /// Horn's closed form, the quaternion being the top eigenvector of the 4x4 matrix built from the cross-covariance.
        /// </summary>
        public static Matrix4x4 Horn(List<Vector3> from, List<Vector3> to)
        {
            Vector3 pc = Vector3.Zero, qc = Vector3.Zero;
            for (int i = 0; i < from.Count; i++) { pc += from[i]; qc += to[i]; }
            pc /= from.Count; qc /= to.Count;
            double sxx = 0, sxy = 0, sxz = 0, syx = 0, syy = 0, syz = 0, szx = 0, szy = 0, szz = 0;
            for (int i = 0; i < from.Count; i++)
            {
                Vector3 p = from[i] - pc, q = to[i] - qc;
                sxx += p.X * q.X; sxy += p.X * q.Y; sxz += p.X * q.Z;
                syx += p.Y * q.X; syy += p.Y * q.Y; syz += p.Y * q.Z;
                szx += p.Z * q.X; szy += p.Z * q.Y; szz += p.Z * q.Z;
            }
            double[,] n =
            {
                { sxx + syy + szz, syz - szy, szx - sxz, sxy - syx },
                { syz - szy, sxx - syy - szz, sxy + syx, szx + sxz },
                { szx - sxz, sxy + syx, -sxx + syy - szz, syz + szy },
                { sxy - syx, szx + sxz, syz + szy, -sxx - syy + szz },
            };
            double[] q4 = TopEigenvector(n);
            Quaternion rotation = Quaternion.Normalize(new Quaternion((float)q4[1], (float)q4[2], (float)q4[3], (float)q4[0]));
            Matrix4x4 result = Matrix4x4.CreateFromQuaternion(rotation);
            result.Translation = qc - Vector3.Transform(pc, rotation);
            return result;
        }

        /// <summary>The eigenvector of a symmetric 4x4 matrix with the largest eigenvalue (cyclic Jacobi).</summary>
        private static double[] TopEigenvector(double[,] input)
        {
            const int n = 4;
            double[,] a = (double[,])input.Clone();
            double[,] v = new double[n, n];
            for (int i = 0; i < n; i++) v[i, i] = 1;
            for (int sweep = 0; sweep < 60; sweep++)
            {
                double off = 0;
                for (int p = 0; p < n; p++) for (int q = p + 1; q < n; q++) off += a[p, q] * a[p, q];
                if (off < 1e-22) break;
                for (int p = 0; p < n; p++)
                    for (int q = p + 1; q < n; q++)
                    {
                        if (Math.Abs(a[p, q]) < 1e-30) continue;
                        double theta = (a[q, q] - a[p, p]) / (2 * a[p, q]);
                        double t = Math.Sign(theta == 0 ? 1 : theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                        double c = 1 / Math.Sqrt(t * t + 1), s = t * c;
                        for (int k = 0; k < n; k++)
                        {
                            double akp = a[k, p], akq = a[k, q];
                            a[k, p] = c * akp - s * akq;
                            a[k, q] = s * akp + c * akq;
                        }
                        for (int k = 0; k < n; k++)
                        {
                            double apk = a[p, k], aqk = a[q, k];
                            a[p, k] = c * apk - s * aqk;
                            a[q, k] = s * apk + c * aqk;
                        }
                        for (int k = 0; k < n; k++)
                        {
                            double vkp = v[k, p], vkq = v[k, q];
                            v[k, p] = c * vkp - s * vkq;
                            v[k, q] = s * vkp + c * vkq;
                        }
                    }
            }
            int top = 0;
            for (int i = 1; i < n; i++) if (a[i, i] > a[top, top]) top = i;
            return new[] { v[0, top], v[1, top], v[2, top], v[3, top] };
        }

        /// <summary>Mean closest-point distance both ways between <paramref name="a"/> and <paramref name="b"/> placed by <paramref name="place"/>.</summary>
        private static float Residual(List<Vector3> a, List<Vector3> b, Matrix4x4 place, int samples)
        {
            List<Vector3> moved = b.Select(o => Vector3.Transform(o, place)).ToList();
            double total = 0;
            int count = 0;
            foreach (Vector3 p in Thin(moved, samples)) { Nearest(a, p, out float d); total += d; count++; }
            foreach (Vector3 p in Thin(a, samples)) { Nearest(moved, p, out float d); total += d; count++; }
            return count == 0 ? float.MaxValue : (float)(total / count);
        }

        private static Vector3 Nearest(List<Vector3> points, Vector3 p, out float distance)
        {
            float best = float.MaxValue;
            Vector3 found = p;
            foreach (Vector3 q in points)
            {
                float d = Vector3.DistanceSquared(p, q);
                if (d < best) { best = d; found = q; }
            }
            distance = (float)Math.Sqrt(best);
            return found;
        }

        /// <summary>At most <paramref name="max"/> of the points, evenly spread through the list.</summary>
        public static List<Vector3> Thin(List<Vector3> points, int max)
        {
            if (points.Count <= max) return points;
            List<Vector3> thinned = new List<Vector3>(max);
            double step = points.Count / (double)max;
            for (int i = 0; i < max; i++) thinned.Add(points[(int)(i * step)]);
            return thinned;
        }

        public static void Bounds(IEnumerable<Vector3> points, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue);
            max = new Vector3(float.MinValue);
            foreach (Vector3 p in points) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            if (min.X > max.X) { min = Vector3.Zero; max = Vector3.Zero; }
        }
        #endregion

        #region Clearance: lines through a model along an axis
        /// <summary>
        /// Triangles indexed for "where does the line through this point, along this axis, enter and leave the model": a grid over
        /// the other two axes, each cell listing the triangles whose shadow covers it.
        /// </summary>
        internal sealed class ColumnIndex
        {
            private readonly List<Vector3> _corners;
            private readonly int _axis, _u, _v, _cells;
            private readonly float _uMin, _vMin, _uSize, _vSize;
            private readonly List<int>[] _grid;

            /// <param name="corners">Three corners per triangle.</param>
            /// <param name="axis">0 = X, 1 = Y, 2 = Z.</param>
            public ColumnIndex(List<Vector3> corners, int axis, int cells = 64)
            {
                _corners = corners;
                _axis = axis;
                _u = axis == 0 ? 1 : 0;
                _v = axis == 2 ? 1 : 2;
                _cells = cells;
                _grid = new List<int>[cells * cells];
                float uMax = float.MinValue, vMax = float.MinValue;
                _uMin = float.MaxValue; _vMin = float.MaxValue;
                foreach (Vector3 c in corners)
                {
                    _uMin = Math.Min(_uMin, Get(c, _u)); uMax = Math.Max(uMax, Get(c, _u));
                    _vMin = Math.Min(_vMin, Get(c, _v)); vMax = Math.Max(vMax, Get(c, _v));
                }
                _uSize = Math.Max(1e-6f, (uMax - _uMin) / cells);
                _vSize = Math.Max(1e-6f, (vMax - _vMin) / cells);
                for (int t = 0; t + 2 < corners.Count; t += 3)
                {
                    float u0 = Math.Min(Get(corners[t], _u), Math.Min(Get(corners[t + 1], _u), Get(corners[t + 2], _u)));
                    float u1 = Math.Max(Get(corners[t], _u), Math.Max(Get(corners[t + 1], _u), Get(corners[t + 2], _u)));
                    float v0 = Math.Min(Get(corners[t], _v), Math.Min(Get(corners[t + 1], _v), Get(corners[t + 2], _v)));
                    float v1 = Math.Max(Get(corners[t], _v), Math.Max(Get(corners[t + 1], _v), Get(corners[t + 2], _v)));
                    for (int i = Cell(u0, _uMin, _uSize); i <= Cell(u1, _uMin, _uSize); i++)
                        for (int j = Cell(v0, _vMin, _vSize); j <= Cell(v1, _vMin, _vSize); j++)
                        {
                            int k = i * _cells + j;
                            if (_grid[k] == null) _grid[k] = new List<int>();
                            _grid[k].Add(t);
                        }
                }
            }

            private int Cell(float x, float min, float size) => Math.Max(0, Math.Min(_cells - 1, (int)((x - min) / size)));
            private static float Get(Vector3 p, int axis) => axis == 0 ? p.X : axis == 1 ? p.Y : p.Z;

            /// <summary>Where the line through <paramref name="point"/> along the axis meets the model: lowest and highest crossing. False if it misses.</summary>
            public bool Query(Vector3 point, out float low, out float high)
            {
                low = float.MaxValue; high = float.MinValue;
                float pu = Get(point, _u), pv = Get(point, _v);
                int i = (int)Math.Floor((pu - _uMin) / _uSize), j = (int)Math.Floor((pv - _vMin) / _vSize);
                if (i < 0 || j < 0 || i >= _cells || j >= _cells) return false;
                List<int> cell = _grid[i * _cells + j];
                if (cell == null) return false;
                bool hit = false;
                foreach (int t in cell)
                {
                    Vector3 a = _corners[t], b = _corners[t + 1], c = _corners[t + 2];
                    float au = Get(a, _u), av = Get(a, _v), bu = Get(b, _u), bv = Get(b, _v), cu = Get(c, _u), cv = Get(c, _v);
                    float d = (bv - cv) * (au - cu) + (cu - bu) * (av - cv);
                    if (Math.Abs(d) < 1e-12f) continue;
                    float w0 = ((bv - cv) * (pu - cu) + (cu - bu) * (pv - cv)) / d;
                    float w1 = ((cv - av) * (pu - cu) + (au - cu) * (pv - cv)) / d;
                    float w2 = 1 - w0 - w1;
                    if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f) continue;
                    float x = w0 * Get(a, _axis) + w1 * Get(b, _axis) + w2 * Get(c, _axis);
                    low = Math.Min(low, x); high = Math.Max(high, x);
                    hit = true;
                }
                return hit;
            }
        }
        #endregion
    }
}
