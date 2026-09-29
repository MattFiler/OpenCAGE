using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using System;
using System.Numerics;

namespace OpenCAGE
{
    /// <summary>
    /// Composing entity transforms down a chain of composite instances.
    /// </summary>
    /// <remarks>
    /// An entity's <c>position</c> is relative to the composite that owns it, so an entity several
    /// instances down needs every instance on the way applied to it to say where it actually is. The
    /// viewer does this by parenting nodes; this is the same thing done by hand, for the times the
    /// answer is needed on this side (lifting a deep-selected entity out into the composite on screen).
    ///
    /// <para><see cref="CommandsUtils.CalculateInstancedPosition"/> answers a similar question by adding
    /// the transforms up, which loses the parent's rotation of the child's offset - a child one metre
    /// out from a parent turned 90 degrees belongs a metre round, not a metre along. That matters here,
    /// where the result has to land on top of what the user is looking at, so this composes properly.</para>
    ///
    /// <para>The euler convention is the one the rest of the codebase states: Y is yaw, X pitch, Z roll,
    /// read through <see cref="Quaternion.CreateFromYawPitchRoll"/> (as CalculateInstancedPosition does
    /// when it hands a rotation back). <see cref="ToEulerDegrees"/> is the exact inverse of
    /// <see cref="ToQuaternion"/> - which is what makes a composed rotation survive being stored as
    /// euler angles and read back.</para>
    ///
    /// <para>The maths is done in double precision, following System.Numerics' own formulas exactly (as
    /// CathodeLib's RefactorCommon.Compose does). A rotation pitched straight up or down leaves yaw and
    /// roll to be recovered from matrix entries that are all but zero; in single precision sin(pitch)
    /// rounds to a hair under one there, the degenerate case goes unnoticed, and yaw and roll come back
    /// as rounding noise - a sign flip on the composed rotation, not a small error.</para>
    /// </remarks>
    internal static class InstanceTransform
    {
        private const float Deg2Rad = (float)(Math.PI / 180.0);

        public static Quaternion ToQuaternion(Vector3 eulerDegrees)
        {
            return Quaternion.CreateFromYawPitchRoll(
                eulerDegrees.Y * Deg2Rad,
                eulerDegrees.X * Deg2Rad,
                eulerDegrees.Z * Deg2Rad);
        }

        public static Vector3 ToEulerDegrees(Quaternion rotation)
        {
            return ToEulerDegrees(new double[] { rotation.X, rotation.Y, rotation.Z, rotation.W });
        }

        /// <summary>
        /// <paramref name="child"/> placed inside <paramref name="parent"/>: the child's offset turned
        /// by the parent's rotation, then moved out to it.
        /// </summary>
        public static cTransform Compose(cTransform parent, cTransform child)
        {
            if (parent == null)
                return child == null ? null : new cTransform(child.position, child.rotation);
            if (child == null)
                return new cTransform(parent.position, parent.rotation);

            /* Nothing turned: add the offsets and keep the child's stored angles exactly, rather than
               recomputing them - which would at best hand back a different spelling of the same rotation. */
            if (parent.rotation == Vector3.Zero)
                return new cTransform(parent.position + child.position, child.rotation);

            double[] p = ToQuaternionDouble(parent.rotation);
            double[] c = ToQuaternionDouble(child.rotation);
            double[] offset = Rotate(p, child.position.X, child.position.Y, child.position.Z);
            return new cTransform(
                new Vector3((float)(parent.position.X + offset[0]), (float)(parent.position.Y + offset[1]), (float)(parent.position.Z + offset[2])),
                ToEulerDegrees(Multiply(p, c)));
        }

        /* Quaternions as {x, y, z, w}, following System.Numerics exactly: CreateFromYawPitchRoll,
           operator *, Vector3.Transform and Matrix4x4.CreateFromQuaternion. */
        private static double[] ToQuaternionDouble(Vector3 eulerDegrees)
        {
            const double d = Math.PI / 180.0;
            double yaw = eulerDegrees.Y * d, pitch = eulerDegrees.X * d, roll = eulerDegrees.Z * d;
            double sr = Math.Sin(roll * 0.5), cr = Math.Cos(roll * 0.5);
            double sp = Math.Sin(pitch * 0.5), cp = Math.Cos(pitch * 0.5);
            double sy = Math.Sin(yaw * 0.5), cy = Math.Cos(yaw * 0.5);
            return new[]
            {
                cy * sp * cr + sy * cp * sr,
                sy * cp * cr - cy * sp * sr,
                cy * cp * sr - sy * sp * cr,
                cy * cp * cr + sy * sp * sr,
            };
        }

        private static double[] Multiply(double[] a, double[] b)
        {
            double cx = a[1] * b[2] - a[2] * b[1];
            double cy = a[2] * b[0] - a[0] * b[2];
            double cz = a[0] * b[1] - a[1] * b[0];
            double dot = a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
            return new[]
            {
                a[0] * b[3] + b[0] * a[3] + cx,
                a[1] * b[3] + b[1] * a[3] + cy,
                a[2] * b[3] + b[2] * a[3] + cz,
                a[3] * b[3] - dot,
            };
        }

        private static double[] Rotate(double[] q, double x, double y, double z)
        {
            double x2 = q[0] + q[0], y2 = q[1] + q[1], z2 = q[2] + q[2];
            double wx2 = q[3] * x2, wy2 = q[3] * y2, wz2 = q[3] * z2;
            double xx2 = q[0] * x2, xy2 = q[0] * y2, xz2 = q[0] * z2;
            double yy2 = q[1] * y2, yz2 = q[1] * z2, zz2 = q[2] * z2;
            return new[]
            {
                x * (1.0 - yy2 - zz2) + y * (xy2 - wz2) + z * (xz2 + wy2),
                x * (xy2 + wz2) + y * (1.0 - xx2 - zz2) + z * (yz2 - wx2),
                x * (xz2 - wy2) + y * (yz2 + wx2) + z * (1.0 - xx2 - yy2),
            };
        }

        private static Vector3 ToEulerDegrees(double[] q)
        {
            double n = Math.Sqrt(q[0] * q[0] + q[1] * q[1] + q[2] * q[2] + q[3] * q[3]);
            double x = q[0] / n, y = q[1] / n, z = q[2] / n, w = q[3] / n;

            /* The row-vector (v * M) matrix of the quaternion, as Matrix4x4.CreateFromQuaternion builds it:
               the transpose of the column-vector R = Ry(yaw) * Rx(pitch) * Rz(roll) that
               CreateFromYawPitchRoll describes. Hence m32 for -sin(pitch), and the pairs below for the
               other two. Only the entries the angles come from. */
            double m11 = 1.0 - 2.0 * (y * y + z * z), m12 = 2.0 * (x * y + z * w), m13 = 2.0 * (x * z - y * w);
            double m22 = 1.0 - 2.0 * (z * z + x * x);
            double m31 = 2.0 * (x * z + y * w), m32 = 2.0 * (y * z - x * w), m33 = 1.0 - 2.0 * (y * y + x * x);

            double sinPitch = Math.Min(1.0, Math.Max(-1.0, -m32));
            double pitch = Math.Asin(sinPitch);
            double yaw, roll;

            /* Pitched fully over, yaw and roll turn about the same axis: give the whole turn to yaw. Only
               where it is genuinely degenerate, though - within a millionth of a radian, where the entries
               yaw and roll come from are rounding noise. atan2 copes with a small cosine perfectly well,
               and taking this branch across a wider band cost most of a degree either side of straight up. */
            if (Math.Sqrt(Math.Max(0.0, 1.0 - sinPitch * sinPitch)) < 1e-6)
            {
                yaw = Math.Atan2(-m13, m11);
                roll = 0.0;
            }
            else
            {
                yaw = Math.Atan2(m31, m33);
                roll = Math.Atan2(m12, m22);
            }

            const double r = 180.0 / Math.PI;
            return new Vector3((float)(pitch * r), (float)(yaw * r), (float)(roll * r));
        }

        /// <summary>The entity's own <c>position</c>, or null when it has none.</summary>
        public static cTransform TransformOf(Entity entity)
        {
            Parameter parameter = entity?.GetParameter("position");
            if (parameter?.content == null || parameter.content.dataType != DataType.TRANSFORM)
                return null;

            cTransform transform = (cTransform)parameter.content;
            return new cTransform(transform.position, transform.rotation);
        }
    }
}
