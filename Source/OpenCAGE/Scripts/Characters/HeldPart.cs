using AlienPAK;
using Assimp;
using CATHODE;
using OpenCAGE.Rigging;
using System;
using System.Collections.Generic;
using System.Linq;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;

namespace OpenCAGE.Characters
{
    /// <summary>
    /// A part a character carries in its own display model - a held gun, a holstered pistol, a helmet lamp - skinned 100% to one bone of
    /// its rig, so it moves with that bone in every animation. The part is modelled about the bone (a weapon about the weapon bone, X down
    /// the barrel, as the game's own third-person weapons are), placed by an offset and moved into the rig's bind pose. The scene is
    /// rewritten between import_model reading the file and planning it, so the rest is the ordinary import of a mesh on the rig.
    /// </summary>
    internal static class HeldPart
    {
        public class Placement
        {
            public string Bone = "RIGHTWEAPONBONE";
            /// <summary>Metres, in the bone's frame.</summary>
            public Vector3 Position = Vector3.Zero;
            /// <summary>Degrees about X, Y, Z (applied Y, X, Z as the game's own transforms), in the bone's frame.</summary>
            public Vector3 Rotation = Vector3.Zero;
            public float Scale = 1f;

            public Matrix4x4 Local => Matrix4x4.CreateScale(Scale)
                * Matrix4x4.CreateFromYawPitchRoll(Rotation.Y * (float)Math.PI / 180f, Rotation.X * (float)Math.PI / 180f, Rotation.Z * (float)Math.PI / 180f)
                * Matrix4x4.CreateTranslation(Position);
        }

        /// <summary>Bones a held part is usually put on, by what they carry.</summary>
        public static readonly string[] CommonBones = { "RIGHTWEAPONBONE", "LEFTWEAPONBONE", "RIGHTHAND", "LEFTHAND", "HEAD", "SPINE3", "HIPS", "LEFTUPLEG", "RIGHTUPLEG" };

        /// <summary>
        /// Rewrite every mesh of <paramref name="scene"/> (as import_model read it: the file's units, the game's axes) as a part on one
        /// bone of <paramref name="rig"/>. Returns a line for the report. Throws an McpError when the bone is not on the rig.
        /// </summary>
        public static string Apply(Scene scene, ModelIO.ImportPlan plan, Skeleton rig, Placement placement, ICollection<string> leftOut = null)
        {
            int bone = FirstPersonCut.BoneIndex(rig, placement.Bone);
            if (bone < 0)
                throw new MCP.McpError(rig.Name + " has no bone " + placement.Bone + ". Its bones: " + string.Join(", ", rig.Bones.Select((b, i) => FirstPersonCut.ShortName(rig, i)).Where(n => n.IndexOf("ROLL", StringComparison.Ordinal) < 0).Take(80)) + ".");
            float unit = plan.UnitScale > 0 ? plan.UnitScale : ModelIO.UnitScale;
            Matrix4x4 bind = rig.GetBindPose()[bone];
            Matrix4x4 place = placement.Local * bind;
            Matrix4x4 turn = RigMath.NormalMatrix(place);

            //where each mesh's node puts it in the file: a skinned mesh's node is ignored on import, so the placement goes into the vertices
            Dictionary<int, Matrix4x4> nodeOf = new Dictionary<int, Matrix4x4>();
            void Walk(Node node, Matrix4x4 parent)
            {
                Matrix4x4 global = RigMath.FromAssimp(node.Transform) * parent;
                if (node.HasMeshes) foreach (int m in node.MeshIndices) if (!nodeOf.ContainsKey(m)) nodeOf[m] = global;
                foreach (Node child in node.Children) Walk(child, global);
            }
            if (scene.RootNode != null) Walk(scene.RootNode, Matrix4x4.Identity);

            Matrix4x4 bindFile = bind;
            bindFile.Translation *= unit;
            Matrix4x4.Invert(bindFile, out Matrix4x4 offset);
            string boneName = ModelIO.BoneName(bone, FirstPersonCut.ShortName(rig, bone));
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            int vertices = 0, counted = 0;
            for (int m = 0; m < scene.MeshCount; m++)
            {
                Mesh mesh = scene.Meshes[m];
                Matrix4x4 node = nodeOf.TryGetValue(m, out Matrix4x4 n) ? n : Matrix4x4.Identity;
                bool counts = leftOut == null || !(leftOut.Contains(mesh.Name ?? "") || leftOut.Contains(m.ToString()));
                Matrix4x4 nodeTurn = RigMath.NormalMatrix(node);
                for (int i = 0; i < mesh.VertexCount; i++)
                {
                    Vector3 file = Vector3.Transform(RigMath.V(mesh.Vertices[i]), node);
                    Vector3 model = Vector3.Transform(file / unit, place);
                    if (counts) { min = Vector3.Min(min, model); max = Vector3.Max(max, model); }
                    mesh.Vertices[i] = RigMath.A(model * unit);
                    if (mesh.Normals.Count == mesh.VertexCount)
                    {
                        Vector3 normal = Vector3.TransformNormal(Vector3.TransformNormal(RigMath.V(mesh.Normals[i]), nodeTurn), turn);
                        if (normal.LengthSquared() > 1e-12f) normal = Vector3.Normalize(normal);
                        mesh.Normals[i] = RigMath.A(normal);
                    }
                }
                if (counts) { vertices += mesh.VertexCount; counted++; }
                //the importer rebuilds a tangent basis where the shader wants one
                mesh.Tangents.Clear();
                mesh.BiTangents.Clear();
                mesh.Bones.Clear();
                Bone weights = new Bone() { Name = boneName, OffsetMatrix = RigMath.ToAssimp(offset) };
                for (int i = 0; i < mesh.VertexCount; i++) weights.VertexWeights.Add(new VertexWeight(i, 1f));
                mesh.Bones.Add(weights);
            }
            plan.Scale = 1;
            Vector3 at = bind.Translation;
            return counted + " mesh(es), " + vertices + " vertices on " + rig.Name + ":" + FirstPersonCut.ShortName(rig, bone) + " (bone " + bone + ", at " + V(at) + "); the part spans " + V(min) + " to " + V(max) + " at the rig's rest pose";
        }

        private static string V(Vector3 v) => "(" + v.X.ToString("0.000") + ", " + v.Y.ToString("0.000") + ", " + v.Z.ToString("0.000") + ")";
    }
}
