using Assimp;
using System;
using System.Collections.Generic;
using System.Linq;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;

namespace OpenCAGE.Rigging
{
    /// <summary>
    /// The skeleton a model file is skinned to, plus each mesh's vertices and weights in one shared space (metres).
    /// Joints rest where the file's inverse bind matrices put them (the only reading guaranteed to agree with the vertices);
    /// unweighted joints have none and are placed from the node tree relative to their nearest weighted ancestor, which holds
    /// even when the node tree is not at the bind pose.
    /// </summary>
    public class SourceRig
    {
        public class Joint
        {
            public string Name;
            public int Parent = -1;
            public List<int> Children = new List<int>();

            /// <summary>Where the joint rests, in metres, in the shared mesh space (Bind's translation).</summary>
            public Vector3 Rest;
            public Matrix4x4 Bind;

            /// <summary>Whether any vertex is weighted to it (its bind came from the file's own bind matrix).</summary>
            public bool Weighted;

            public override string ToString() => Name;
        }

        /// <summary>One mesh of the file, as the rebind reads it.</summary>
        public class MeshSkin
        {
            public int MeshIndex;
            public string Name;
            public Vector3[] Positions;          //metres, shared space
            public Vector3[] Normals, Tangents, Bitangents;
            public List<(int joint, float weight)>[] Weights;
            public bool Skinned;                 //false: a static mesh carried rigidly by the joint its node hangs off
            public int RigidJoint = -1;
            public Matrix4x4 ToShared;           //file units of this mesh -> metres, shared space
        }

        public List<Joint> Joints = new List<Joint>();
        public Dictionary<string, int> Index = new Dictionary<string, int>(StringComparer.Ordinal);
        public List<MeshSkin> Meshes = new List<MeshSkin>();
        public float UnitScale = 1;
        public List<string> Notes = new List<string>();

        public int Find(string name) => name != null && Index.TryGetValue(name, out int i) ? i : -1;

        public bool IsAncestor(int ancestor, int joint)
        {
            for (int a = joint >= 0 ? Joints[joint].Parent : -1; a >= 0; a = Joints[a].Parent)
                if (a == ancestor) return true;
            return false;
        }

        public IEnumerable<int> Descendants(int joint)
        {
            Stack<int> open = new Stack<int>(Joints[joint].Children);
            while (open.Count != 0)
            {
                int j = open.Pop();
                yield return j;
                foreach (int c in Joints[j].Children) open.Push(c);
            }
        }

        public int Depth(int joint) { int d = 0; for (int a = Joints[joint].Parent; a >= 0; a = Joints[a].Parent) d++; return d; }

        /// <summary>The joints from <paramref name="from"/> down to <paramref name="to"/>, both included; null if to is not under from.</summary>
        public List<int> Path(int from, int to)
        {
            List<int> path = new List<int>();
            for (int j = to; j >= 0; j = Joints[j].Parent)
            {
                path.Add(j);
                if (j == from) { path.Reverse(); return path; }
            }
            return null;
        }

        /// <summary>
        /// Reads a scene imported as the model importer does (ModelIO.ImportPostProcessSteps), with
        /// <paramref name="unitScale"/> file units to the metre. Null if nothing in it is skinned.
        /// </summary>
        public static SourceRig Read(Scene scene, float unitScale)
        {
            if (scene?.RootNode == null) return null;
            if (unitScale <= 0) unitScale = 1;
            SourceRig rig = new SourceRig() { UnitScale = unitScale };

            //every node's world transform, from the scene root
            Dictionary<Node, Matrix4x4> global = new Dictionary<Node, Matrix4x4>();
            Dictionary<string, Node> byName = new Dictionary<string, Node>(StringComparer.Ordinal);
            void Walk(Node node, Matrix4x4 parent)
            {
                Matrix4x4 world = RigMath.FromAssimp(node.Transform) * parent;
                global[node] = world;
                if (node.Name != null && !byName.ContainsKey(node.Name)) byName[node.Name] = node;
                foreach (Node child in node.Children) Walk(child, world);
            }
            Walk(scene.RootNode, Matrix4x4.Identity);

            //which nodes are joints: every bone a mesh is weighted to, what holds them up, and the mesh-less nodes below them
            HashSet<string> boneNames = new HashSet<string>(scene.Meshes.SelectMany(m => m.Bones).Select(b => b.Name ?? ""), StringComparer.Ordinal);
            if (boneNames.Count == 0) return null;
            HashSet<Node> jointNodes = new HashSet<Node>();
            foreach (string bone in boneNames)
            {
                if (!byName.TryGetValue(bone, out Node node)) { rig.Notes.Add("The file weights vertices to '" + bone + "', which is not a node in it; those weights are ignored."); continue; }
                for (Node n = node; n != null && n != scene.RootNode; n = n.Parent)
                {
                    if (n.MeshCount != 0 && !boneNames.Contains(n.Name ?? "")) break;
                    jointNodes.Add(n);
                }
            }
            foreach (Node node in jointNodes.ToList())
            {
                Stack<Node> open = new Stack<Node>(node.Children);
                while (open.Count != 0)
                {
                    Node n = open.Pop();
                    if (n.MeshCount != 0) continue;
                    jointNodes.Add(n);
                    foreach (Node c in n.Children) open.Push(c);
                }
            }

            //in tree order, so a parent always comes before its children
            Dictionary<Node, int> jointOf = new Dictionary<Node, int>();
            void Order(Node node)
            {
                if (jointNodes.Contains(node) && !rig.Index.ContainsKey(node.Name ?? ""))
                {
                    int parent = -1;
                    for (Node p = node.Parent; p != null; p = p.Parent) if (jointOf.TryGetValue(p, out parent)) break; else parent = -1;
                    Joint joint = new Joint() { Name = node.Name ?? "", Parent = parent, Weighted = boneNames.Contains(node.Name ?? "") };
                    jointOf[node] = rig.Joints.Count;
                    rig.Index[joint.Name] = rig.Joints.Count;
                    if (parent >= 0) rig.Joints[parent].Children.Add(rig.Joints.Count);
                    rig.Joints.Add(joint);
                }
                foreach (Node child in node.Children) Order(child);
            }
            Order(scene.RootNode);

            /* One shared space: the vertex space of the biggest skinned mesh. Another mesh is brought into it through a bone
             * the two share (both bind matrices describe the same joint), or failing that through the node tree. */
            List<Mesh> skinned = scene.Meshes.Where(m => m.HasBones).ToList();
            Mesh canonical = skinned.OrderByDescending(m => m.VertexCount).First();
            Dictionary<string, Matrix4x4> canonicalOffset = canonical.Bones.GroupBy(b => b.Name).ToDictionary(g => g.Key, g => RigMath.FromAssimp(g.First().OffsetMatrix));

            //node space -> shared space, from the weighted joint nearest the root (the rest only report how much they disagree)
            Matrix4x4 calibration = Matrix4x4.Identity;
            int calibrated = -1;
            foreach (int j in Enumerable.Range(0, rig.Joints.Count).Where(o => canonicalOffset.ContainsKey(rig.Joints[o].Name)).OrderBy(rig.Depth))
            {
                Matrix4x4.Invert(canonicalOffset[rig.Joints[j].Name], out Matrix4x4 bind);
                Matrix4x4.Invert(global[byName[rig.Joints[j].Name]], out Matrix4x4 inverseGlobal);
                calibration = inverseGlobal * bind;
                calibrated = j;
                break;
            }

            Dictionary<Mesh, Matrix4x4> toCanonical = new Dictionary<Mesh, Matrix4x4>();
            foreach (Mesh mesh in skinned)
            {
                if (mesh == canonical) { toCanonical[mesh] = Matrix4x4.Identity; continue; }
                Bone shared = mesh.Bones.FirstOrDefault(b => canonicalOffset.ContainsKey(b.Name));
                if (shared != null)
                {
                    Matrix4x4.Invert(canonicalOffset[shared.Name], out Matrix4x4 bind);
                    toCanonical[mesh] = RigMath.FromAssimp(shared.OffsetMatrix) * bind;
                }
                else
                {
                    Bone any = mesh.Bones.First(b => byName.ContainsKey(b.Name ?? ""));
                    toCanonical[mesh] = RigMath.FromAssimp(any.OffsetMatrix) * global[byName[any.Name]] * calibration;
                }
            }

            //each weighted joint's bind, through whichever mesh weights it
            bool[] placed = new bool[rig.Joints.Count];
            foreach (Mesh mesh in skinned.OrderBy(m => m == canonical ? 0 : 1))
                foreach (Bone bone in mesh.Bones)
                {
                    int j = rig.Find(bone.Name);
                    if (j < 0 || placed[j]) continue;
                    Matrix4x4.Invert(RigMath.FromAssimp(bone.OffsetMatrix), out Matrix4x4 bind);
                    rig.Joints[j].Bind = bind * toCanonical[mesh];
                    placed[j] = true;
                }

            //everything else from the node tree, relative to the nearest joint that has a bind of its own
            double worst = 0;
            for (int j = 0; j < rig.Joints.Count; j++)
            {
                Node node = byName[rig.Joints[j].Name];
                if (placed[j])
                {
                    //how far the node tree's own reading of this joint is from its bind: a measure of whether the tree is at the bind pose
                    Vector3 fromTree = (global[node] * calibration).Translation;
                    worst = Math.Max(worst, (fromTree - rig.Joints[j].Bind.Translation).Length() / unitScale);
                    continue;
                }
                int a = rig.Joints[j].Parent;
                while (a >= 0 && !placed[a]) a = rig.Joints[a].Parent;
                if (a >= 0)
                {
                    Matrix4x4.Invert(global[byName[rig.Joints[a].Name]], out Matrix4x4 inverseAncestor);
                    rig.Joints[j].Bind = global[node] * inverseAncestor * rig.Joints[a].Bind;
                }
                else
                    rig.Joints[j].Bind = global[node] * calibration;
                placed[j] = true;
            }
            if (worst > 0.02)
                rig.Notes.Add("The file's node tree is not at its bind pose (up to " + (worst * 100).ToString("0") + " cm off); joints nothing is weighted to are placed relative to their weighted parents.");

            //metres, from here on
            Matrix4x4 toMetres = Matrix4x4.CreateScale(1f / unitScale);
            foreach (Joint joint in rig.Joints)
            {
                joint.Bind = joint.Bind * toMetres;
                joint.Rest = joint.Bind.Translation;
            }

            //the meshes, in the shared space and in metres
            for (int m = 0; m < scene.MeshCount; m++)
            {
                Mesh mesh = scene.Meshes[m];
                if (mesh.VertexCount == 0) continue;
                MeshSkin skin = new MeshSkin() { MeshIndex = m, Name = mesh.Name, Skinned = mesh.HasBones };
                if (mesh.HasBones)
                    skin.ToShared = toCanonical[mesh] * toMetres;
                else
                {
                    //a static mesh: placed by its node, and carried by the nearest joint above it
                    Node holder = FindHolder(scene.RootNode, m);
                    Matrix4x4 nodeGlobal = holder != null ? global[holder] : Matrix4x4.Identity;
                    skin.ToShared = nodeGlobal * calibration * toMetres;
                    for (Node p = holder; p != null; p = p.Parent)
                        if (jointOf.TryGetValue(p, out int joint)) { skin.RigidJoint = joint; break; }
                }

                int n = mesh.VertexCount;
                skin.Positions = new Vector3[n];
                for (int i = 0; i < n; i++) skin.Positions[i] = Vector3.Transform(RigMath.V(mesh.Vertices[i]), skin.ToShared);
                if (mesh.Normals.Count == n) skin.Normals = mesh.Normals.Select(o => SafeNormalize(Vector3.TransformNormal(RigMath.V(o), skin.ToShared))).ToArray();
                if (mesh.Tangents.Count == n) skin.Tangents = mesh.Tangents.Select(o => SafeNormalize(Vector3.TransformNormal(RigMath.V(o), skin.ToShared))).ToArray();
                if (mesh.BiTangents.Count == n) skin.Bitangents = mesh.BiTangents.Select(o => SafeNormalize(Vector3.TransformNormal(RigMath.V(o), skin.ToShared))).ToArray();

                skin.Weights = new List<(int, float)>[n];
                for (int i = 0; i < n; i++) skin.Weights[i] = new List<(int, float)>(4);
                foreach (Bone bone in mesh.Bones)
                {
                    int j = rig.Find(bone.Name);
                    if (j < 0) continue;
                    foreach (VertexWeight weight in bone.VertexWeights)
                        if (weight.Weight > 0 && weight.VertexID >= 0 && weight.VertexID < n)
                            skin.Weights[weight.VertexID].Add((j, weight.Weight));
                }
                rig.Meshes.Add(skin);
            }
            return rig;
        }

        private static Node FindHolder(Node node, int mesh)
        {
            if (node.MeshIndices.Contains(mesh)) return node;
            foreach (Node child in node.Children)
            {
                Node found = FindHolder(child, mesh);
                if (found != null) return found;
            }
            return null;
        }

        internal static Vector3 SafeNormalize(Vector3 v)
        {
            float l = v.Length();
            return l > 1e-12f ? v / l : Vector3.UnitY;
        }
    }
}
