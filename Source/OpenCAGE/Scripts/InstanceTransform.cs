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
    /// </remarks>
    internal static class InstanceTransform
    {
        private const float Deg2Rad = (float)(Math.PI / 180.0);
        private const float Rad2Deg = (float)(180.0 / Math.PI);

        public static Quaternion ToQuaternion(Vector3 eulerDegrees)
        {
            return Quaternion.CreateFromYawPitchRoll(
                eulerDegrees.Y * Deg2Rad,
                eulerDegrees.X * Deg2Rad,
                eulerDegrees.Z * Deg2Rad);
        }

        public static Vector3 ToEulerDegrees(Quaternion rotation)
        {
            /* Matrix4x4 here is row-vector (v * M), so it holds the transpose of the column-vector
               R = Ry(yaw) * Rx(pitch) * Rz(roll) that CreateFromYawPitchRoll describes. Hence M32 for
               -sin(pitch), and the pairs below for the other two. */
            Matrix4x4 m = Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(rotation));

            float sinPitch = Math.Min(1f, Math.Max(-1f, -m.M32));
            float pitch = (float)Math.Asin(sinPitch);
            float yaw, roll;

            /* Pitched fully over, yaw and roll turn about the same axis: give the whole turn to yaw. Only
               where it is genuinely degenerate, though - atan2 copes with a small cosine perfectly well,
               and taking this branch across a wider band cost most of a degree either side of straight up. */
            if (Math.Sqrt(Math.Max(0.0, 1.0 - (double)sinPitch * sinPitch)) < 1e-6)
            {
                yaw = (float)Math.Atan2(-m.M13, m.M11);
                roll = 0f;
            }
            else
            {
                yaw = (float)Math.Atan2(m.M31, m.M33);
                roll = (float)Math.Atan2(m.M12, m.M22);
            }

            return new Vector3(pitch * Rad2Deg, yaw * Rad2Deg, roll * Rad2Deg);
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

            Quaternion parentRotation = ToQuaternion(parent.rotation);
            return new cTransform(
                parent.position + Vector3.Transform(child.position, parentRotation),
                ToEulerDegrees(parentRotation * ToQuaternion(child.rotation)));
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
