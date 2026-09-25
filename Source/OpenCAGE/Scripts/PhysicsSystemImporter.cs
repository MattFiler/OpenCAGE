using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using CathodeLib.Havok;
using System;
using System.Linq;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

namespace OpenCAGE
{
    /// <summary>
    /// New physics systems from meshes: the mesh comes from a model file or a model the level already holds (the
    /// readers <see cref="CollisionProxyImporter"/> uses), becomes a convex hull, and goes into both of the level's
    /// physics packfiles as a new one-body system (see <see cref="PhysicsSystemWriter.AddConvexPhysicsSystem"/>), at the
    /// same index in each, or into neither.
    /// </summary>
    public static class PhysicsSystemImporter
    {
        /// <summary>What the composite a PhysicsSystem entity sits in suggests for a new system.</summary>
        public sealed class HostDefaults
        {
            /// <summary>Retail's convention: the composite's path, then its last segment again, in capitals.</summary>
            public string SystemName = "OPENCAGE\\IMPORTED";
            /// <summary>Retail names a body after the ModelReference entity it drives.</summary>
            public string BodyName = "Body";
            /// <summary>The ModelReference's placement relative to the PhysicsSystem entity: where retail puts the body.</summary>
            public Vector3 Position = Vector3.Zero;
            public Quaternion Rotation = Quaternion.Identity;
            /// <summary>Which entity the placement came from, for the dialog; null when there is none.</summary>
            public string PlacementSource;
            /// <summary>The model that ModelReference draws, to open the model picker on.</summary>
            public Models.CS2.Component.LOD.Submesh Model;
        }

        /// <summary>
        /// Defaults from the composite a PhysicsSystem entity sits in: the first ModelReference in it that draws a
        /// model. Retail places each body where its ModelReference sits (94% of bodies on the levels checked, rotated
        /// ones included) and names it after that entity (241 of 258 on Torrens).
        /// </summary>
        public static HostDefaults DefaultsFor(Composite composite, FunctionEntity physicsEntity)
        {
            HostDefaults defaults = new HostDefaults();
            if (composite == null)
                return defaults;

            string path = (composite.name ?? "").Replace('/', '\\').Trim('\\');
            if (path.Length > 0)
            {
                string leaf = path.Substring(path.LastIndexOf('\\') + 1);
                defaults.SystemName = (path + "\\" + leaf).ToUpperInvariant();
            }

            foreach (FunctionEntity function in composite.functions)
            {
                if (function.function != FunctionType.ModelReference)
                    continue;
                ResourceReference renderable = function.GetResource(ResourceType.RENDERABLE_INSTANCE, true);
                RenderableElements.Element drawn = renderable?.RenderableInstance?.FirstOrDefault(e => e?.Model != null);
                if (drawn == null)
                    continue;

                string name = CommandsUtils.GetEntityNameParameter(function);
                if (!string.IsNullOrEmpty(name))
                    defaults.BodyName = name;
                defaults.Model = drawn.Model;
                defaults.PlacementSource = string.IsNullOrEmpty(name) ? "the composite's model" : name;

                (Vector3 position, Quaternion rotation) = LocalTransform(function);
                //Bodies are held relative to the system, which the game places where the PhysicsSystem entity is
                (Vector3 systemPosition, Quaternion systemRotation) = LocalTransform(physicsEntity);
                Quaternion inverse = Quaternion.Inverse(systemRotation);
                defaults.Position = Vector3.Transform(position - systemPosition, inverse);
                defaults.Rotation = Quaternion.Normalize(inverse * rotation);
                break;
            }
            return defaults;
        }

        /// <summary>An entity's own position parameter, turned the way the instancer turns it (yaw Y, pitch X, roll Z, degrees).</summary>
        static (Vector3, Quaternion) LocalTransform(Entity entity)
        {
            if (!(entity?.GetParameter("position")?.content is cTransform transform))
                return (Vector3.Zero, Quaternion.Identity);
            const float toRadians = (float)Math.PI / 180f;
            return (new Vector3(transform.position.X, transform.position.Y, transform.position.Z),
                Quaternion.CreateFromYawPitchRoll(transform.rotation.Y * toRadians, transform.rotation.X * toRadians, transform.rotation.Z * toRadians));
        }

        /// <summary>The convex shape a mesh becomes: its hull, at most 64 corners, shrunk by a convex radius.</summary>
        public static ConvexBody BuildShape(CollisionProxyImporter.MeshSource mesh)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            return PhysicsSystemWriter.BuildConvexBody(mesh.Positions);
        }

        /// <summary>The hull's surface, for the preview: what the body will collide as.</summary>
        public static HavokPackfile.PreviewMesh ToPreviewMesh(ConvexBody shape)
        {
            HavokPackfile.PreviewMesh mesh = new HavokPackfile.PreviewMesh();
            mesh.Positions.AddRange(shape.SurfaceVertices);
            mesh.Indices.AddRange(shape.SurfaceTriangles);
            mesh.ShapeCount = 1;
            return mesh;
        }

        /// <summary>
        /// A starting mass for a hull: its solid volume at 300 kg/m3, which lands retail's pickups (a water bottle,
        /// a battery, a canister) within a factor of two of what they ship with, kept between 0.1 and 50 kg.
        /// </summary>
        public static float SuggestedMass(ConvexBody shape)
        {
            double mass = shape.Volume * 300.0;
            mass = Math.Max(0.1, Math.Min(50.0, mass));
            return (float)Math.Round(mass, mass < 1 ? 2 : 1);
        }

        /// <summary>
        /// Write the shape into the level's physics packfiles as a new one-body system. Both files get it at the same
        /// index, or neither does: each is put back to how it was if the other refuses.
        /// </summary>
        /// <returns>The new system as the level's leading packfile holds it - the one the picker lists and entities bind to.</returns>
        public static HavokPackfile.PhysicsSystem Import(Level level, string systemName, ConvexBody shape, PhysicsBodySettings body)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (shape == null) throw new ArgumentNullException(nameof(shape));
            if (body == null) throw new ArgumentNullException(nameof(body));
            HavokPackfile hk32 = level.PhysicsHKX != null && level.PhysicsHKX.Loaded ? level.PhysicsHKX : null;
            HavokPackfile hk64 = level.PhysicsHKX64 != null && level.PhysicsHKX64.Loaded ? level.PhysicsHKX64 : null;
            if (hk32 == null && hk64 == null)
                throw new InvalidOperationException("This level has no physics packfile loaded.");
            if ((hk32 != null && hk32.IsTagfile) || (hk64 != null && hk64.IsTagfile))
                throw new NotSupportedException("New physics systems can only be written to the PC packfiles.");
            if (hk32 != null && hk64 != null && hk32.PhysicsSystems.Count != hk64.PhysicsSystems.Count)
                throw new InvalidOperationException("PHYSICS.HKX and PHYSICS.HKX64 hold different numbers of systems (" + hk32.PhysicsSystems.Count + " / " + hk64.PhysicsSystems.Count + "), so a new one could not be given the same index in both.");

            AppendCheckpoint cp32 = hk32?.CreateCheckpoint();
            AppendCheckpoint cp64 = hk64?.CreateCheckpoint();
            try
            {
                HavokPackfile.PhysicsSystem s32 = hk32?.AddConvexPhysicsSystem(systemName, shape, body);
                HavokPackfile.PhysicsSystem s64 = hk64?.AddConvexPhysicsSystem(systemName, shape, body);
                if (s32 != null && s64 != null && s32.SystemIndex != s64.SystemIndex)
                    throw new InvalidOperationException("The new system landed at index " + s32.SystemIndex + " in PHYSICS.HKX but " + s64.SystemIndex + " in PHYSICS.HKX64.");
                return level.Physics == hk32 ? s32 : s64 ?? s32;
            }
            catch
            {
                if (cp32 != null) hk32.RestoreCheckpoint(cp32);
                if (cp64 != null) hk64.RestoreCheckpoint(cp64);
                throw;
            }
        }
    }
}
