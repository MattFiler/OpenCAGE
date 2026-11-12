using System;
using System.Collections.Generic;
using System.Numerics;

namespace OpenCAGE.Rigging
{
    /// <summary>
    /// The small pieces of geometry the rig tools share. Everything is System.Numerics' row-vector
    /// convention (v' = v * M), which is also what CathodeLib's skeletons use; Assimp's matrices are the
    /// transpose of that and go through <see cref="FromAssimp"/> / <see cref="ToAssimp"/>.
    /// </summary>
    public static class RigMath
    {
        public static Matrix4x4 FromAssimp(Assimp.Matrix4x4 m)
        {
            return new Matrix4x4(m.A1, m.B1, m.C1, m.D1, m.A2, m.B2, m.C2, m.D2,
                                 m.A3, m.B3, m.C3, m.D3, m.A4, m.B4, m.C4, m.D4);
        }

        public static Assimp.Matrix4x4 ToAssimp(Matrix4x4 m)
        {
            return new Assimp.Matrix4x4(m.M11, m.M21, m.M31, m.M41, m.M12, m.M22, m.M32, m.M42,
                                        m.M13, m.M23, m.M33, m.M43, m.M14, m.M24, m.M34, m.M44);
        }

        public static Vector3 V(Assimp.Vector3D v) { return new Vector3(v.X, v.Y, v.Z); }
        public static Assimp.Vector3D A(Vector3 v) { return new Assimp.Vector3D(v.X, v.Y, v.Z); }

        /// <summary>The shortest turn taking one direction onto another.</summary>
        public static Quaternion Arc(Vector3 from, Vector3 to)
        {
            from = Vector3.Normalize(from);
            to = Vector3.Normalize(to);
            float dot = Vector3.Dot(from, to);
            if (dot > 0.99999f) return Quaternion.Identity;
            Vector3 axis = Vector3.Cross(from, to);
            if (axis.LengthSquared() < 1e-10f) axis = Vector3.Cross(from, Math.Abs(from.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY);
            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), (float)Math.Acos(Math.Max(-1, Math.Min(1, dot))));
        }

        /// <summary>The rotation taking one frame (a direction and an across axis) onto another, as a row-vector matrix.</summary>
        public static Matrix4x4 Frame(Vector3 fromDirection, Vector3 fromAcross, Vector3 toDirection, Vector3 toAcross)
        {
            return Matrix4x4.Transpose(Basis(fromDirection, fromAcross)) * Basis(toDirection, toAcross);
        }

        private static Matrix4x4 Basis(Vector3 d, Vector3 a)
        {
            Vector3 x = Vector3.Normalize(d);
            Vector3 y = a - Vector3.Dot(a, x) * x;
            y = y.LengthSquared() < 1e-12f ? Vector3.Normalize(Vector3.Cross(x, Math.Abs(x.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY)) : Vector3.Normalize(y);
            Vector3 z = Vector3.Cross(x, y);
            return new Matrix4x4(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, 0, 0, 0, 1);
        }

        /// <summary>The outer product d * d^T, the projection onto a direction.</summary>
        public static Matrix4x4 Outer(Vector3 d)
        {
            return new Matrix4x4(d.X * d.X, d.X * d.Y, d.X * d.Z, 0, d.Y * d.X, d.Y * d.Y, d.Y * d.Z, 0, d.Z * d.X, d.Z * d.Y, d.Z * d.Z, 0, 0, 0, 0, 0);
        }

        /// <summary>The inverse transpose of a matrix's linear part, which is what carries a normal.</summary>
        public static Matrix4x4 NormalMatrix(Matrix4x4 m)
        {
            m.M41 = m.M42 = m.M43 = 0;
            if (!Matrix4x4.Invert(m, out Matrix4x4 inverse)) return Matrix4x4.Identity;
            return Matrix4x4.Transpose(inverse);
        }

        public static double Determinant3(Matrix4x4 m)
        {
            return m.M11 * (m.M22 * m.M33 - m.M23 * m.M32)
                 - m.M12 * (m.M21 * m.M33 - m.M23 * m.M31)
                 + m.M13 * (m.M21 * m.M32 - m.M22 * m.M31);
        }

        /* The 48 ways to send each axis to another axis with either sign - 24 rotations and the 24
         * reflections, as AnimationRetarget fits a foreign rig's frame. Row vectors. */
        public static IEnumerable<Matrix4x4> SignedPermutations()
        {
            int[][] orders = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 }, new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };
            foreach (int[] order in orders)
                for (int signs = 0; signs < 8; signs++)
                {
                    float[,] cells = new float[3, 3];
                    for (int column = 0; column < 3; column++)
                        cells[order[column], column] = (signs & (1 << column)) != 0 ? -1 : 1;
                    yield return new Matrix4x4(cells[0, 0], cells[0, 1], cells[0, 2], 0,
                                               cells[1, 0], cells[1, 1], cells[1, 2], 0,
                                               cells[2, 0], cells[2, 1], cells[2, 2], 0,
                                               0, 0, 0, 1);
                }
        }

        /// <summary>The direction a vector points, levelled onto the floor (Y up).</summary>
        public static Vector3 Flat(Vector3 v)
        {
            Vector3 flat = new Vector3(v.X, 0, v.Z);
            return flat.LengthSquared() < 1e-9f ? Vector3.UnitZ : Vector3.Normalize(flat);
        }

        /// <summary>Cumulative arc length along a polyline, from 0.</summary>
        public static float[] ArcLengths(IList<Vector3> points)
        {
            float[] at = new float[points.Count];
            for (int i = 1; i < points.Count; i++) at[i] = at[i - 1] + (points[i] - points[i - 1]).Length();
            return at;
        }

        /// <summary>The point a given distance along a polyline (clamped to its ends).</summary>
        public static Vector3 PointAt(IList<Vector3> points, float[] at, float distance)
        {
            if (points.Count == 1) return points[0];
            for (int i = 1; i < points.Count; i++)
                if (distance <= at[i] || i == points.Count - 1)
                {
                    float span = at[i] - at[i - 1];
                    float t = span <= 1e-9f ? 0 : (distance - at[i - 1]) / span;
                    return Vector3.Lerp(points[i - 1], points[i], Math.Max(0, Math.Min(1, t)));
                }
            return points[points.Count - 1];
        }

        /// <summary>The nearest point of a polyline: the segment it is on and how far along it (0..1).</summary>
        public static void Nearest(IList<Vector3> points, Vector3 p, out int segment, out float t)
        {
            segment = 0; t = 0;
            float best = float.MaxValue;
            for (int i = 0; i + 1 < points.Count; i++)
            {
                Vector3 d = points[i + 1] - points[i];
                float length = d.LengthSquared();
                float along = length <= 1e-12f ? 0 : Math.Max(0, Math.Min(1, Vector3.Dot(p - points[i], d) / length));
                float e = (points[i] + d * along - p).LengthSquared();
                if (e < best) { best = e; segment = i; t = along; }
            }
        }
    }
}
