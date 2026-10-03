using Assimp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE.ModelExport
{
    /// <summary>
    /// The formats a model or animation can be written to, what each one can carry, and the one
    /// place that decides which writer handles it.
    ///
    /// FBX and glTF are written here rather than by assimp. assimp's FBX exporter resamples
    /// animation down to a handful of keys, and its glTF exporter mangles anything animated badly
    /// enough that it wasn't worth offering. The two formats everyone actually asks for are the two
    /// assimp is worst at, so they are ours; the rest still go through it.
    /// </summary>
    public static class ModelExporter
    {
        public class Format
        {
            /// <summary>Lower case, with the dot - ".fbx".</summary>
            public string Extension;
            public string Description;

            /// <summary>Whether an animation written to this format survives it.</summary>
            public bool Animation;

            /// <summary>Whether skin weights survive.</summary>
            public bool Skinning;

            /// <summary>How many UV sets it carries, for the formats that only take one.</summary>
            public int UVSets;

            /// <summary>What one unit means, in CATHODE metres.</summary>
            public float UnitScale;

            /// <summary>Whether the format's UV origin is the opposite corner to CATHODE's.</summary>
            public bool FlipUVs;

            /// <summary>Written here rather than handed to assimp.</summary>
            public bool Native;

            /// <summary>
            /// Whether textures have to go out as PNG. glTF allows PNG and JPEG only, so a .dds
            /// beside one is simply not loaded; every other format here reads DDS and keeps the
            /// compression, which is worth having on a character's worth of 2K maps.
            /// </summary>
            public bool PrefersPng;

            public override string ToString() { return Description; }
        }

        /* Centimetres for the DCC formats, which is what a unit has meant in FBX since the start and
         * what OBJ and COLLADA get exported as in practice. glTF is defined in metres. */
        private const float Centimetres = 100.0f;
        private const float Metres = 1.0f;

        public static readonly IReadOnlyList<Format> Formats = new List<Format>
        {
            new Format { Extension = ".fbx",  Description = "FBX",                Animation = true,  Skinning = true,  UVSets = 8, UnitScale = Centimetres, FlipUVs = true,  Native = true },
            new Format { Extension = ".glb",  Description = "glTF Binary",        Animation = true,  Skinning = true,  UVSets = 8, UnitScale = Metres,      FlipUVs = false, Native = true, PrefersPng = true },
            new Format { Extension = ".gltf", Description = "glTF",               Animation = true,  Skinning = true,  UVSets = 8, UnitScale = Metres,      FlipUVs = false, Native = true, PrefersPng = true },
            new Format { Extension = ".dae",  Description = "COLLADA",            Animation = true,  Skinning = true,  UVSets = 8, UnitScale = Centimetres, FlipUVs = true,  Native = false },
            new Format { Extension = ".obj",  Description = "Wavefront OBJ",      Animation = false, Skinning = false, UVSets = 1, UnitScale = Centimetres, FlipUVs = true,  Native = false },
        };

        /// <summary>The format a filename names, or FBX if it names nothing we know.</summary>
        public static Format For(string filename)
        {
            string extension = (Path.GetExtension(filename) ?? "").ToLowerInvariant();
            return Formats.FirstOrDefault(x => x.Extension == extension) ?? Formats[0];
        }

        /// <summary>
        /// A filter for a save dialog. Pass true to leave out the formats that can't carry an
        /// animation, so an animation export never offers one that would drop it.
        /// </summary>
        public static string Filter(bool animated)
        {
            IEnumerable<Format> offered = animated ? Formats.Where(x => x.Animation) : Formats;
            return string.Join("|", offered.Select(x => x.Description + " (*" + x.Extension + ")|*" + x.Extension));
        }

        /// <summary>
        /// A filter for an OPEN dialog, which is a different job to <see cref="Filter"/>: exporting
        /// has to settle on one format, but importing does not care which of them a file is, so the
        /// first entry takes them all and nobody has to know that their model is COLLADA.
        /// </summary>
        public static string ImportFilter(bool animated)
        {
            IEnumerable<Format> offered = animated ? Formats.Where(x => x.Animation) : Formats;
            List<Format> list = offered.ToList();

            string all = string.Join(";", list.Select(x => "*" + x.Extension));
            string entries = string.Join("|", list.Select(x => x.Description + " (*" + x.Extension + ")|*" + x.Extension));
            return "All supported models|" + all + "|" + entries + "|All files (*.*)|*.*";
        }

        /// <summary>Where a format sits in <see cref="Filter"/>, which dialogs count from one.</summary>
        public static int FilterIndex(string extension, bool animated)
        {
            List<Format> offered = (animated ? Formats.Where(x => x.Animation) : Formats).ToList();
            int index = offered.FindIndex(x => x.Extension == (extension ?? "").ToLowerInvariant());
            return index < 0 ? 1 : index + 1;
        }

        /// <summary>Write a scene, picking the writer from the file name.</summary>
        public static void Write(Scene scene, string filename)
        {
            Format format = For(filename);
            switch (format.Extension)
            {
                case ".fbx": FbxExporter.Export(scene, filename); return;
                case ".glb": GltfExporter.Export(scene, filename, true); return;
                case ".gltf": GltfExporter.Export(scene, filename, false); return;
                default:
                    using (AssimpContext exporter = new AssimpContext())
                        exporter.ExportFile(WithSkinnedMeshesBaked(scene), filename, AssimpFormatId(format.Extension));
                    return;
            }
        }

        /// <summary>
        /// The scene with every skinned mesh's node transform moved into the mesh itself, for the
        /// formats that can't say a skinned mesh sits anywhere but where its vertices are: glTF ignores
        /// a skinned mesh's node outright, and COLLADA readers disagree about it. ModelIO hangs an
        /// animated prop's part off a node that places it (see BuildScene), which FBX keeps as it is;
        /// these carry the same placement in the vertices instead, and the sidecar's record of it
        /// takes it back off on import. The scene passed in is left as it was.
        /// </summary>
        public static Scene WithSkinnedMeshesBaked(Scene scene)
        {
            Dictionary<int, Assimp.Matrix4x4> bake = new Dictionary<int, Assimp.Matrix4x4>();
            Dictionary<Node, Assimp.Matrix4x4> parentWorld = new Dictionary<Node, Assimp.Matrix4x4>();
            FindSkinnedNodes(scene, scene.RootNode, Assimp.Matrix4x4.Identity, bake, parentWorld);
            if (bake.Count == 0) return scene;

            Scene baked = new Scene();
            baked.RootNode = CopyNode(scene.RootNode, bake, parentWorld);
            for (int i = 0; i < scene.MeshCount; i++)
                baked.Meshes.Add(bake.TryGetValue(i, out Assimp.Matrix4x4 world) ? Bake(scene.Meshes[i], world) : scene.Meshes[i]);
            baked.Materials.AddRange(scene.Materials);
            baked.Animations.AddRange(scene.Animations);
            baked.Textures.AddRange(scene.Textures);
            baked.Lights.AddRange(scene.Lights);
            baked.Cameras.AddRange(scene.Cameras);
            return baked;
        }

        /* A node is baked when every mesh it draws is skinned and it sits anywhere but the origin */
        private static void FindSkinnedNodes(Scene scene, Node node, Assimp.Matrix4x4 parent, Dictionary<int, Assimp.Matrix4x4> bake, Dictionary<Node, Assimp.Matrix4x4> parentWorld)
        {
            //AssimpNet's "a * b" applies a first, so a node's world transform is its own times its parent's
            Assimp.Matrix4x4 world = node.Transform * parent;
            if (node.MeshIndices.Count != 0 && !IsExactlyIdentity(world)
                && node.MeshIndices.All(x => x >= 0 && x < scene.MeshCount && scene.Meshes[x].HasBones))
            {
                foreach (int mesh in node.MeshIndices) bake[mesh] = world;
                parentWorld[node] = parent;
            }
            foreach (Node child in node.Children) FindSkinnedNodes(scene, child, world, bake, parentWorld);
        }

        private static Node CopyNode(Node source, Dictionary<int, Assimp.Matrix4x4> bake, Dictionary<Node, Assimp.Matrix4x4> parentWorld)
        {
            Node node = new Node(source.Name) { Transform = source.Transform };
            node.MeshIndices.AddRange(source.MeshIndices);

            //the placement is in the vertices now, so the node itself goes back to the origin - and its name
            //says so, which is all a reader without the sidecar has to go by (see ModelIO.PlacementWarning)
            if (parentWorld.TryGetValue(source, out Assimp.Matrix4x4 parent))
            {
                parent.Inverse();
                node.Transform = parent;
                node.Name += AlienPAK.ModelIO.PlacedInVerticesSuffix;
            }
            foreach (Node child in source.Children) node.Children.Add(CopyNode(child, bake, parentWorld));
            return node;
        }

        /* A copy of a mesh moved by its node's transform. Its bones still have to reach the same
         * bone space from the moved vertices, so each offset matrix first takes the move back off. */
        private static Mesh Bake(Mesh source, Assimp.Matrix4x4 world)
        {
            System.Numerics.Matrix4x4 move = AlienPAK.ModelIO.ToNumerics(world);
            System.Numerics.Matrix4x4.Invert(move, out System.Numerics.Matrix4x4 unmove);
            System.Numerics.Matrix4x4 normals = System.Numerics.Matrix4x4.Transpose(unmove);

            //a move that mirrors turns every triangle inside out unless they are wound the other way too
            bool mirrors = move.GetDeterminant() < 0;

            Mesh mesh = new Mesh(source.Name, source.PrimitiveType) { MaterialIndex = source.MaterialIndex };
            foreach (Vector3D v in source.Vertices) mesh.Vertices.Add(Transform(v, move, false));
            foreach (Vector3D n in source.Normals) mesh.Normals.Add(Transform(n, normals, true));
            foreach (Vector3D t in source.Tangents) mesh.Tangents.Add(Transform(t, move, true));
            foreach (Vector3D b in source.BiTangents) mesh.BiTangents.Add(Transform(b, move, true));
            for (int channel = 0; channel < source.TextureCoordinateChannelCount; channel++)
            {
                mesh.TextureCoordinateChannels[channel].AddRange(source.TextureCoordinateChannels[channel]);
                mesh.UVComponentCount[channel] = source.UVComponentCount[channel];
            }
            for (int channel = 0; channel < source.VertexColorChannelCount; channel++)
                mesh.VertexColorChannels[channel].AddRange(source.VertexColorChannels[channel]);
            foreach (Face face in source.Faces) mesh.Faces.Add(new Face(face.Indices.ToArray()));
            if (mirrors) AlienPAK.ModelIO.ReverseWinding(mesh);

            Assimp.Matrix4x4 unbake = world;
            unbake.Inverse();
            foreach (Bone bone in source.Bones)
            {
                Bone copy = new Bone() { Name = bone.Name, OffsetMatrix = unbake * bone.OffsetMatrix };
                copy.VertexWeights.AddRange(bone.VertexWeights);
                mesh.Bones.Add(copy);
            }
            return mesh;
        }

        /// <summary>
        /// Where each bone in the scene binds, as a world transform, for the bones no skin binds -
        /// <paramref name="bound"/> is where the skins bind the rest, by bone name.
        ///
        /// A DCC tool's bone has no scale at rest, so it reads a scaled bind pose by dropping the scale,
        /// then poses the bone against what is left: Blender's FBX importer moved the strongbox's lid
        /// 5.8 m and the cutting vent's door 2.4 m that way. So ModelIO binds a rig's bones without the
        /// scale they rest at (see BuildScene), and a bone no skin binds goes the same way - measured
        /// against the node the rig hangs off, so that node's unit scale and turn stay. The rest
        /// transforms and the clip's keys keep the scale, and say exactly where each bone goes.
        ///
        /// A rig whose skins bind a bone at its scale (one with a skin spread over scaled bones, which
        /// can't be put right any other way) keeps the scale for every bone, so the rig binds one way
        /// throughout. A bone at a scale of 1 binds exactly where it rests either way.
        /// </summary>
        public static Dictionary<string, Assimp.Matrix4x4> BindPoses(Node root, IDictionary<string, Assimp.Matrix4x4> bound)
        {
            Dictionary<string, Assimp.Matrix4x4> rest = new Dictionary<string, Assimp.Matrix4x4>(StringComparer.Ordinal);
            Dictionary<string, Assimp.Matrix4x4> rigid = new Dictionary<string, Assimp.Matrix4x4>(StringComparer.Ordinal);
            GatherBinds(root, Assimp.Matrix4x4.Identity, Assimp.Matrix4x4.Identity, false, rest, rigid);

            foreach (KeyValuePair<string, Assimp.Matrix4x4> bind in bound)
                if (rigid.TryGetValue(bind.Key, out Assimp.Matrix4x4 without) && !SameScale(bind.Value, without))
                    return rest;
            return rigid;
        }

        private static void GatherBinds(Node node, Assimp.Matrix4x4 parent, Assimp.Matrix4x4 armature, bool parentIsBone,
                                        Dictionary<string, Assimp.Matrix4x4> rest, Dictionary<string, Assimp.Matrix4x4> rigid)
        {
            Assimp.Matrix4x4 world = node.Transform * parent;
            bool isBone = AlienPAK.ModelIO.TryParseBoneName(node.Name, out _);
            if (isBone)
            {
                if (!parentIsBone) armature = parent;
                rest[node.Name] = world;
                rigid[node.Name] = WithoutScale(world, armature);
            }
            foreach (Node child in node.Children) GatherBinds(child, world, armature, isBone, rest, rigid);
        }

        /* Whether two transforms stretch each axis by the same amount, give or take float error */
        private static bool SameScale(Assimp.Matrix4x4 a, Assimp.Matrix4x4 b)
        {
            System.Numerics.Matrix4x4 x = AlienPAK.ModelIO.ToNumerics(a), y = AlienPAK.ModelIO.ToNumerics(b);
            float[] first = { new System.Numerics.Vector3(x.M11, x.M12, x.M13).Length(), new System.Numerics.Vector3(x.M21, x.M22, x.M23).Length(), new System.Numerics.Vector3(x.M31, x.M32, x.M33).Length() };
            float[] second = { new System.Numerics.Vector3(y.M11, y.M12, y.M13).Length(), new System.Numerics.Vector3(y.M21, y.M22, y.M23).Length(), new System.Numerics.Vector3(y.M31, y.M32, y.M33).Length() };
            for (int i = 0; i < 3; i++)
                if (Math.Abs(first[i] - second[i]) > 1e-3f * Math.Max(1e-6f, second[i])) return false;
            return true;
        }

        private static Assimp.Matrix4x4 WithoutScale(Assimp.Matrix4x4 world, Assimp.Matrix4x4 armature)
        {
            Assimp.Matrix4x4 unarmature = armature;
            unarmature.Inverse();
            System.Numerics.Matrix4x4 local = AlienPAK.ModelIO.ToNumerics(world * unarmature);
            System.Numerics.Matrix4x4 rigid = AlienPAK.ModelIO.WithoutScale(local);
            return rigid == local ? world : AlienPAK.ModelIO.ToAssimp(rigid) * armature;
        }

        /// <summary>
        /// Whether a transform is exactly identity. Assimp's own IsIdentity allows a tenth of a unit
        /// either way, which is nothing in centimetres and several millimetres in metres - enough to
        /// lose the small offsets on a character's eye bones.
        /// </summary>
        public static bool IsExactlyIdentity(Assimp.Matrix4x4 m)
        {
            return m.A1 == 1 && m.A2 == 0 && m.A3 == 0 && m.A4 == 0
                && m.B1 == 0 && m.B2 == 1 && m.B3 == 0 && m.B4 == 0
                && m.C1 == 0 && m.C2 == 0 && m.C3 == 1 && m.C4 == 0
                && m.D1 == 0 && m.D2 == 0 && m.D3 == 0 && m.D4 == 1;
        }

        private static Vector3D Transform(Vector3D value, System.Numerics.Matrix4x4 matrix, bool direction)
        {
            System.Numerics.Vector3 v = new System.Numerics.Vector3(value.X, value.Y, value.Z);
            v = direction ? AlienPAK.ModelIO.Direction(v, matrix) : System.Numerics.Vector3.Transform(v, matrix);
            return new Vector3D(v.X, v.Y, v.Z);
        }

        /* A couple of assimp's exporters are registered under an id that isn't the extension */
        private static string AssimpFormatId(string extension)
        {
            switch (extension)
            {
                case ".dae": return "collada";
                case ".gltf": return "gltf2";
                case ".glb": return "glb2";
                default: return extension.TrimStart('.');
            }
        }
    }
}
