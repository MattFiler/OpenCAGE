using Assimp;
using CATHODE;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace OpenCAGE.Rigging
{
    /// <summary>
    /// An animated prop (hatch, fan, machine arm) read from a model file as an environment rig: a skeleton in the prop's space with
    /// the root at the identity, bones carrying the parts, and clips sampled per frame from the file's node animation. Reads parts
    /// animated as objects (each holding or keyed node becomes a bone) or a mesh skinned to an armature (its joints become the bones).
    /// </summary>
    public class PropRig
    {
        public class Joint
        {
            public string Name;              //the bone's name in the rig (upper case, unique)
            public string Node;              //the file's node it comes from ("" for the root)
            public int Parent = -1;
            public Matrix4x4 Rest;           //model space (the prop's), metres, rigid
        }

        public class Part
        {
            public int MeshIndex;
            public int Joint;                //for a rigid part, the bone carrying it
            public Vector3[] Positions;      //prop space, metres
            public Vector3[] Normals;
            public List<(int joint, float weight)>[] Weights;   //for a skinned part

            /// <summary>A rigid part about its own bone, as the game's props model each part: where its bone is, it is at the origin.</summary>
            public Vector3[] LocalPositions, LocalNormals;
        }

        public class Clip
        {
            public string Name;
            public float FrameDuration;
            public List<List<HavokPackfile.SampledTransform>> Frames = new List<List<HavokPackfile.SampledTransform>>();
        }

        public List<Joint> Joints = new List<Joint>();
        public List<Part> Parts = new List<Part>();
        public List<Clip> Clips = new List<Clip>();
        public List<string> Notes = new List<string>();
        public float UnitScale = 1;
        public bool Skinned;

        /// <summary>Read a prop from a scene read as the model importer reads it. Null with a problem when it can't be.</summary>
        public static PropRig Read(Scene scene, string path, float unitScale, string rigName, float frameRate, out string problem)
        {
            problem = null;
            PropRig rig = new PropRig() { UnitScale = unitScale > 0 ? unitScale : 1 };
            if (scene?.RootNode == null || scene.MeshCount == 0) { problem = "The file holds no meshes."; return null; }
            float unit = rig.UnitScale;
            Matrix4x4 S = Matrix4x4.CreateScale(1f / unit), SInv = Matrix4x4.CreateScale(unit);
            Matrix4x4 Metres(Matrix4x4 fileTransform) => SInv * fileTransform * S;     //a node's transform in metres: rigid, translation / unit

            Dictionary<Node, Matrix4x4> rest = new Dictionary<Node, Matrix4x4>();
            Dictionary<string, Node> byName = new Dictionary<string, Node>(StringComparer.Ordinal);
            void Walk(Node n, Matrix4x4 parent) { Matrix4x4 g = RigMath.FromAssimp(n.Transform) * parent; rest[n] = g; if (n.Name != null && !byName.ContainsKey(n.Name)) byName[n.Name] = n; foreach (Node c in n.Children) Walk(c, g); }
            Walk(scene.RootNode, Matrix4x4.Identity);
            HashSet<string> keyed = new HashSet<string>(scene.Animations.SelectMany(a => a.NodeAnimationChannels).Select(c => c.NodeName ?? ""), StringComparer.Ordinal);

            //---- which nodes are bones, and where the file's frame of reference meets the rig's (calibration, in file units)
            HashSet<Node> boneNodes = new HashSet<Node>();
            Matrix4x4 calibration = Matrix4x4.Identity;
            SourceRig skin = scene.Meshes.Any(m => m.HasBones) ? SourceRig.Read(scene, unit) : null;
            rig.Skinned = skin != null;
            if (skin != null)
            {
                foreach (SourceRig.Joint j in skin.Joints) if (byName.TryGetValue(j.Name, out Node n)) boneNodes.Add(n);
                //the joint nearest the root: its bind in the shared (vertex) space against its node in the tree
                SourceRig.Joint top = skin.Joints.OrderBy(j => skin.Depth(skin.Joints.IndexOf(j))).First(j => byName.ContainsKey(j.Name));
                Matrix4x4 bindFile = top.Bind * SInv;         //Joint.Bind = bind(file) * S
                Matrix4x4.Invert(rest[byName[top.Name]], out Matrix4x4 inverseTop);
                calibration = inverseTop * bindFile;
            }
            for (int m = 0; m < scene.MeshCount; m++)
            {
                Node holder = FindHolder(scene.RootNode, m);
                if (holder != null && !scene.Meshes[m].HasBones) boneNodes.Add(holder);
            }
            foreach (Node n in byName.Values) if (keyed.Contains(n.Name)) boneNodes.Add(n);
            //and everything between them and the scene root, so each keeps its place in the hierarchy
            foreach (Node n in boneNodes.ToList()) for (Node p = n.Parent; p != null && p != scene.RootNode; p = p.Parent) boneNodes.Add(p);
            if (boneNodes.Count + 1 > 255) { problem = "The file has " + boneNodes.Count + " parts and joints; a rig holds at most 254 here."; return null; }

            //---- the bones, the root first (at the identity, as every shipped prop rig's is), then the file's in tree order
            Matrix4x4 Model(Node n) => Metres(rest[n] * calibration);
            rig.Joints.Add(new Joint() { Name = rigName, Node = "", Parent = -1, Rest = Matrix4x4.Identity });
            Dictionary<Node, int> jointOf = new Dictionary<Node, int>();
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rigName };
            void Order(Node n)
            {
                if (boneNodes.Contains(n))
                {
                    int parent = 0;
                    for (Node p = n.Parent; p != null; p = p.Parent) if (jointOf.TryGetValue(p, out int pj)) { parent = pj; break; }
                    string name = BoneName(n.Name, rig.Joints.Count, names);
                    jointOf[n] = rig.Joints.Count;
                    rig.Joints.Add(new Joint() { Name = name, Node = n.Name ?? "", Parent = parent, Rest = Model(n) });
                }
                foreach (Node c in n.Children) Order(c);
            }
            Order(scene.RootNode);

            //---- the parts, in the prop's space
            for (int m = 0; m < scene.MeshCount; m++)
            {
                Mesh mesh = scene.Meshes[m];
                if (mesh.VertexCount == 0) continue;
                Part part = new Part() { MeshIndex = m };
                if (mesh.HasBones && skin != null)
                {
                    SourceRig.MeshSkin s = skin.Meshes.First(o => o.MeshIndex == m);
                    part.Positions = s.Positions;
                    part.Normals = s.Normals;
                    part.Weights = s.Weights.Select(w => w.Select(x => (byName.TryGetValue(skin.Joints[x.joint].Name, out Node n) && jointOf.TryGetValue(n, out int j) ? j : 0, x.weight)).ToList()).ToArray();
                }
                else
                {
                    Node holder = FindHolder(scene.RootNode, m);
                    Matrix4x4 place = holder != null ? rest[holder] * calibration * S : S;
                    part.Joint = holder != null && jointOf.TryGetValue(holder, out int j) ? j : 0;
                    part.Positions = mesh.Vertices.Select(v => Vector3.Transform(RigMath.V(v), place)).ToArray();
                    if (mesh.Normals.Count == mesh.VertexCount) part.Normals = mesh.Normals.Select(v => SourceRig.SafeNormalize(Vector3.TransformNormal(RigMath.V(v), place))).ToArray();
                    Matrix4x4 toBone = Inverse(rig.Joints[part.Joint].Rest);
                    part.LocalPositions = part.Positions.Select(p => Vector3.Transform(p, toBone)).ToArray();
                    if (part.Normals != null) part.LocalNormals = part.Normals.Select(n => SourceRig.SafeNormalize(Vector3.TransformNormal(n, toBone))).ToArray();
                }
                rig.Parts.Add(part);
            }

            //---- the clips: every node composed per frame from the scene root, the keyed ones from their keys
            bool milliseconds = Path.GetExtension(path ?? "").StartsWith(".gl", StringComparison.OrdinalIgnoreCase);
            foreach (Assimp.Animation animation in scene.Animations)
            {
                Dictionary<string, NodeAnimationChannel> channels = new Dictionary<string, NodeAnimationChannel>(StringComparer.Ordinal);
                foreach (NodeAnimationChannel c in animation.NodeAnimationChannels) if (c.NodeName != null) channels[c.NodeName] = c;
                if (channels.Count == 0) continue;
                double first = double.MaxValue, last = double.MinValue;
                foreach (NodeAnimationChannel c in channels.Values)
                    foreach (double t in c.PositionKeys.Select(k => k.Time).Concat(c.RotationKeys.Select(k => k.Time)).Concat(c.ScalingKeys.Select(k => k.Time)))
                    { first = Math.Min(first, t); last = Math.Max(last, t); }
                if (first > last) continue;
                double ticksPerSecond = milliseconds ? 1000.0 : (animation.TicksPerSecond > 0 ? animation.TicksPerSecond : 25.0);
                double seconds = (last - first) / ticksPerSecond;
                float rate = frameRate > 0 ? frameRate : 30f;
                int frames = Math.Max(2, (int)Math.Round(seconds * rate) + 1);
                if (frames > 30 * 600) { rig.Notes.Add("'" + animation.Name + "' is over ten minutes long; left out."); continue; }
                Clip clip = new Clip() { Name = string.IsNullOrEmpty(animation.Name) ? "clip" + rig.Clips.Count : animation.Name, FrameDuration = 1f / rate };
                for (int f = 0; f < frames; f++)
                {
                    double at = first + (frames > 1 ? (last - first) * f / (frames - 1) : 0);
                    Dictionary<Node, Matrix4x4> world = new Dictionary<Node, Matrix4x4>();
                    void Pose(Node n, Matrix4x4 parent)
                    {
                        Matrix4x4 local = channels.TryGetValue(n.Name ?? "", out NodeAnimationChannel c) ? Sample(c, at, RigMath.FromAssimp(n.Transform)) : RigMath.FromAssimp(n.Transform);
                        Matrix4x4 g = local * parent;
                        world[n] = g;
                        foreach (Node child in n.Children) Pose(child, g);
                    }
                    Pose(scene.RootNode, Matrix4x4.Identity);
                    Matrix4x4[] model = new Matrix4x4[rig.Joints.Count];
                    List<HavokPackfile.SampledTransform> pose = new List<HavokPackfile.SampledTransform>(rig.Joints.Count);
                    for (int j = 0; j < rig.Joints.Count; j++)
                    {
                        model[j] = j == 0 ? Matrix4x4.Identity : Metres(world[byName[rig.Joints[j].Node]] * calibration);
                        Matrix4x4 local = j == 0 ? Matrix4x4.Identity : model[j] * Inverse(model[rig.Joints[j].Parent]);
                        pose.Add(ToSampled(local));
                    }
                    clip.Frames.Add(pose);
                }
                rig.Clips.Add(clip);
            }
            if (rig.Clips.Count == 0) rig.Notes.Add("The file has no animation of its parts: the rig and the prop are made, but no clip.");
            return rig;
        }

        /// <summary>The bones' rest transforms relative to their parents, as a skeleton stores them.</summary>
        public HavokPackfile.SampledTransform RestLocal(int j)
        {
            return ToSampled(j == 0 ? Matrix4x4.Identity : Joints[j].Rest * Inverse(Joints[Joints[j].Parent].Rest));
        }

        public static HavokPackfile.SampledTransform ToSampled(Matrix4x4 m)
        {
            Matrix4x4.Decompose(m, out Vector3 scale, out Quaternion rotation, out Vector3 translation);
            return new HavokPackfile.SampledTransform() { Translation = translation, Rotation = Quaternion.Normalize(rotation), Scale = scale, HasTranslation = true, HasRotation = true, HasScale = true };
        }

        private static Matrix4x4 Inverse(Matrix4x4 m) { Matrix4x4.Invert(m, out Matrix4x4 i); return i; }

        private static Matrix4x4 Sample(NodeAnimationChannel c, double at, Matrix4x4 restLocal)
        {
            Matrix4x4.Decompose(restLocal, out Vector3 scale, out Quaternion rotation, out Vector3 translation);
            if (c.PositionKeyCount != 0) translation = Lerp(c.PositionKeys, at);
            if (c.ScalingKeyCount != 0) scale = Lerp(c.ScalingKeys, at);
            if (c.RotationKeyCount != 0) rotation = Slerp(c.RotationKeys, at);
            return Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation);
        }

        private static Vector3 Lerp(List<VectorKey> keys, double at)
        {
            if (keys.Count == 1 || at <= keys[0].Time) return RigMath.V(keys[0].Value);
            if (at >= keys[keys.Count - 1].Time) return RigMath.V(keys[keys.Count - 1].Value);
            int i = 0; while (i + 1 < keys.Count && keys[i + 1].Time <= at) i++;
            double span = keys[i + 1].Time - keys[i].Time;
            return Vector3.Lerp(RigMath.V(keys[i].Value), RigMath.V(keys[i + 1].Value), span <= 0 ? 0 : (float)((at - keys[i].Time) / span));
        }

        private static Quaternion Slerp(List<QuaternionKey> keys, double at)
        {
            Quaternion Q(Assimp.Quaternion q) => new Quaternion(q.X, q.Y, q.Z, q.W);
            if (keys.Count == 1 || at <= keys[0].Time) return Q(keys[0].Value);
            if (at >= keys[keys.Count - 1].Time) return Q(keys[keys.Count - 1].Value);
            int i = 0; while (i + 1 < keys.Count && keys[i + 1].Time <= at) i++;
            double span = keys[i + 1].Time - keys[i].Time;
            return Quaternion.Slerp(Q(keys[i].Value), Q(keys[i + 1].Value), span <= 0 ? 0 : (float)((at - keys[i].Time) / span));
        }

        private static Node FindHolder(Node node, int mesh)
        {
            if (node.MeshIndices.Contains(mesh)) return node;
            foreach (Node child in node.Children) { Node found = FindHolder(child, mesh); if (found != null) return found; }
            return null;
        }

        /* A bone's name: the node's, upper case, letters digits and underscores, unique within the rig */
        private static string BoneName(string node, int index, HashSet<string> taken)
        {
            string name = new string((node ?? "").ToUpperInvariant().Select(c => char.IsLetterOrDigit(c) && c < 128 ? c : '_').ToArray()).Trim('_');
            if (name.Length == 0) name = "PART";
            if (name.Length > 40) name = name.Substring(0, 40);
            string unique = name;
            for (int i = 2; taken.Contains(unique); i++) unique = name + "_" + i;
            taken.Add(unique);
            return unique;
        }
    }
}
