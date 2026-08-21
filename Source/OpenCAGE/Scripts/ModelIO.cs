using Assimp;
using CATHODE;
using CathodeLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using static CATHODE.Models;
using static CATHODE.Models.CS2.Component.LOD;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace AlienPAK
{
    /* Converts CS2 models to and from Assimp scenes such that an exported model can be re-imported unmodified.
     * The component/LOD/submesh structure is encoded into object names, and everything the mesh formats can't
     * carry (render flags, vertex formats, skinning data, ...) is written to a sidecar next to the model. */
    public static class ModelIO
    {
        public const string SidecarExtension = ".cs2meta.json";
        private const int SidecarVersion = 1;

        //ToMesh multiplies UVs by this, so we divide by it when going back
        private const float UVScale = 16.0f;

        /* CATHODE works in metres; FBX, COLLADA and OBJ all treat a unit as a centimetre, so a model
         * written at its real size lands in Blender at a hundredth of it. glTF is defined in metres and
         * needs no such factor - see ModelExporter.Formats, which is where each format states its own.
         * This is the default for callers that do not name a format. */
        public const float UnitScale = 100.0f;

        private static readonly Regex _tagRegex = new Regex(@"^CS2_C(\d+)(?:_L(\d+)(?:_S(\d+))?)?", RegexOptions.CultureInvariant);

        //Bones are matched by name, and DCC tools like to decorate names, so this is deliberately searched for anywhere
        private static readonly Regex _boneRegex = new Regex(@"CS2_BONE_(\d+)", RegexOptions.CultureInvariant);

        public const string SkeletonNodeName = "CS2_SKELETON";

        /* The index is what actually matters on the way back in, so it always leads. The game's own name
         * is appended when we know it, purely so the rig reads sensibly in a DCC tool. */
        public static string BoneName(int bone, string skeletonBoneName = null)
        {
            string name = "CS2_BONE_" + bone.ToString("000");
            string suffix = Sanitise(skeletonBoneName);
            return suffix.Length == 0 ? name : name + "_" + suffix;
        }

        #region TAGGING

        public static string ComponentTag(int component) => "CS2_C" + component.ToString("00");
        public static string LODTag(int component, int lod) => ComponentTag(component) + "_L" + lod.ToString("00");
        public static string SubmeshTag(int component, int lod, int submesh) => LODTag(component, lod) + "_S" + submesh.ToString("000");

        /* Tags are unique within the scene, which stops exporters mangling them to avoid name clashes */
        public static bool TryParseTag(string name, out int component, out int lod, out int submesh)
        {
            component = lod = submesh = -1;
            if (string.IsNullOrEmpty(name)) return false;

            Match match = _tagRegex.Match(name);
            if (!match.Success) return false;

            component = int.Parse(match.Groups[1].Value);
            if (match.Groups[2].Success) lod = int.Parse(match.Groups[2].Value);
            if (match.Groups[3].Success) submesh = int.Parse(match.Groups[3].Value);
            return true;
        }

        /// <summary>The skeleton bone index a node or bone name carries, if it carries one.</summary>
        public static bool TryParseBoneName(string name, out int bone)
        {
            bone = -1;
            if (string.IsNullOrEmpty(name)) return false;

            Match match = _boneRegex.Match(name);
            return match.Success && int.TryParse(match.Groups[1].Value, out bone);
        }

        /* Keep names to characters every exporter round trips without escaping */
        public static string Sanitise(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            char[] content = name.ToCharArray();
            for (int i = 0; i < content.Length; i++)
                if (!char.IsLetterOrDigit(content[i]) && content[i] != '_' && content[i] != '-')
                    content[i] = '_';
            return new string(content);
        }

        #endregion

        #region SIDECAR

        public class ModelMetadata
        {
            public int Version = SidecarVersion;
            public string Name;

            //Units the positions in the model file were written in, relative to CATHODE's metres
            public float UnitScale = ModelIO.UnitScale;

            //Skeleton the rig in the model file came from, if one was picked on export
            public string Skeleton;
            public List<ComponentMetadata> Components = new List<ComponentMetadata>();

            [JsonIgnore] private Dictionary<string, SubmeshMetadata> _lookup = null;

            public SubmeshMetadata FindSubmesh(string tag)
            {
                if (tag == null) return null;
                if (_lookup == null)
                {
                    _lookup = new Dictionary<string, SubmeshMetadata>();
                    foreach (ComponentMetadata component in Components ?? new List<ComponentMetadata>())
                        foreach (LODMetadata lod in component.LODs ?? new List<LODMetadata>())
                            foreach (SubmeshMetadata submesh in lod.Submeshes ?? new List<SubmeshMetadata>())
                                if (submesh.Tag != null && !_lookup.ContainsKey(submesh.Tag))
                                    _lookup[submesh.Tag] = submesh;
                }
                _lookup.TryGetValue(tag, out SubmeshMetadata found);
                return found;
            }

            public string FindLODName(int component, int lod)
            {
                if (Components == null || component < 0 || component >= Components.Count) return null;
                List<LODMetadata> lods = Components[component].LODs;
                if (lods == null || lod < 0 || lod >= lods.Count) return null;
                return lods[lod].Name;
            }
        }

        public class ComponentMetadata
        {
            public List<LODMetadata> LODs = new List<LODMetadata>();
        }

        public class LODMetadata
        {
            public string Name;
            public List<SubmeshMetadata> Submeshes = new List<SubmeshMetadata>();
        }

        public class SubmeshMetadata
        {
            public string Tag;
            public string Material;
            public uint RenderFlags;
            public float MinLODRange;
            public float MaxLODRange;
            public float[] MinBounds;
            public float[] MaxBounds;
            public int VertexScale = 1;
            public int VertexCount;
            public int CollisionProxyIndex = -1;
            public List<List<AttributeMetadata>> VertexFormatFull;
            public List<List<AttributeMetadata>> VertexFormatPartial;

            //Assimp UV channel index -> CATHODE TexCoord index
            public int[] UVChannels;

            //Lets us rebuild the original vertex numbering after the importer has welded the mesh back together
            public string Indices;

            //Per-vertex data that the mesh formats have nowhere to put. Only re-applied if the vertex numbering survives.
            public string PositionW;
            public string BlendIndices;
            public string BlendWeights;
            public List<int> Bones;
        }

        public class AttributeMetadata
        {
            public string Type;
            public string Usage;
            public int Index;

            public AttributeMetadata() { }
            public AttributeMetadata(VertexFormat.Attribute attribute)
            {
                Type = attribute.Type.ToString();
                Usage = attribute.Usage.ToString();
                Index = attribute.Index;
            }

            public VertexFormat.Attribute ToAttribute()
            {
                VertexFormat.Type type = (VertexFormat.Type)Enum.Parse(typeof(VertexFormat.Type), Type);
                VertexFormat.Usage usage = (VertexFormat.Usage)Enum.Parse(typeof(VertexFormat.Usage), Usage);
                return new VertexFormat.Attribute(type, usage, Index);
            }
        }

        public static string GetSidecarPath(string modelPath)
        {
            return modelPath + SidecarExtension;
        }

        /* Find the sidecar for a model the user has picked, allowing for the model having been re-exported under a different extension */
        public static ModelMetadata TryLoadSidecar(string modelPath)
        {
            if (string.IsNullOrEmpty(modelPath)) return null;

            List<string> candidates = new List<string>() { GetSidecarPath(modelPath) };
            string withoutExtension = Path.Combine(Path.GetDirectoryName(modelPath) ?? "", Path.GetFileNameWithoutExtension(modelPath));
            foreach (string extension in new string[] { ".fbx", ".gltf", ".glb", ".obj" })
                candidates.Add(GetSidecarPath(withoutExtension + extension));

            foreach (string candidate in candidates)
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    ModelMetadata metadata = JsonConvert.DeserializeObject<ModelMetadata>(File.ReadAllText(candidate));
                    if (metadata != null && metadata.Components != null) return metadata;
                }
                catch { }
            }
            return null;
        }

        public static void SaveSidecar(ModelMetadata metadata, string modelPath)
        {
            File.WriteAllText(GetSidecarPath(modelPath), JsonConvert.SerializeObject(metadata, Formatting.Indented,
                new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore }));
        }

        #endregion

        #region EXPORT

        /* Kept for callers that only have a file name. The formats table is the real answer. */
        public static bool FormatFlipsUVs(string filename)
        {
            return OpenCAGE.ModelExport.ModelExporter.For(filename).FlipUVs;
        }

        /// <summary>What one unit means to the format a file name picks, in CATHODE metres.</summary>
        public static float FormatUnitScale(string filename)
        {
            return OpenCAGE.ModelExport.ModelExporter.For(filename).UnitScale;
        }

        /* Build an Assimp scene mirroring the CS2 hierarchy, plus the sidecar describing everything the scene can't hold.
         * Pass a skeleton to write a real rig with the game's bone names and bind pose.
         *
         * rigidBinding is for the environment animation system: a static mesh has no weights of its
         * own, so to make an exported clip move it each part is bound whole to the bone that carries
         * it. Only worth doing when a clip is going out with the mesh - a plain model export should
         * stay a plain static model, so that it comes back in as one.
         *
         * A prop's parts are each modelled about their own origin, so an animated export also has
         * to assemble them. Pass the level's record of the prop and they go out where they belong;
         * without one the parts can only be matched to bones by name, and stay where they are. */
        public static Scene BuildScene(Models.CS2 cs2, Func<Models.CS2.Component.LOD.Submesh, int> materialIndex, bool flipUVs, out ModelMetadata metadata, Skeleton skeleton = null, bool rigidBinding = false, float unitScale = UnitScale, EnvironmentRigs.Prop prop = null)
        {
            Scene scene = new Scene();
            scene.RootNode = new Node(Sanitise(Path.GetFileNameWithoutExtension(cs2.Name ?? "model")));

            metadata = new ModelMetadata() { Name = cs2.Name };
            metadata.Skeleton = skeleton?.Name;
            metadata.UnitScale = unitScale;
            SortedSet<int> usedBones = new SortedSet<int>();
            List<Matrix4x4> bindPose = skeleton?.GetBindPose();
            bool rigid = rigidBinding && skeleton != null && Skeleton.RequiredBoneCount(cs2) == 0;
            int[][] rigidBones = rigid && prop == null ? EnvironmentRigs.Bind(cs2, skeleton) : null;

            /* Which bone carries which part, as the level records it. Matching them up by name is
             * only a guess - plenty of props name a bone and the geometry it moves differently, and
             * some name their parts not at all.
             *
             * The level's record also says where each part goes, which an exported file needs as
             * much as the preview does: a prop's parts are each modelled about their own origin, so
             * binding them without it writes a file whose pieces are all piled up at the origin.
             * That placement is folded into the bind matrix rather than baked into the geometry, so
             * the vertices going out are still the vertices that came in. */
            Dictionary<Models.CS2.Component.LOD.Submesh, EnvironmentRigs.Part> placement = null;
            Matrix4x4[] propOffsets = null;
            List<Matrix4x4> propBindPose = null;
            if (prop != null && skeleton != null)
            {
                placement = new Dictionary<Models.CS2.Component.LOD.Submesh, EnvironmentRigs.Part>(SameSubmesh.Instance);
                foreach (EnvironmentRigs.Part part in prop.Parts) placement[part.Submesh] = part;
                propOffsets = EnvironmentRigs.Offsets(prop, skeleton);

                /* An environment rig sits in the same space as the prop it drives, so its bones go
                 * out without the rotation into mesh space a character's need - see the skeleton's
                 * root node, which leaves it off to match. */
                propBindPose = skeleton.GetModelSpacePose();
            }

            for (int i = 0; i < cs2.Components.Count; i++)
            {
                Node componentNode = new Node(ComponentTag(i));
                scene.RootNode.Children.Add(componentNode);

                ComponentMetadata componentMetadata = new ComponentMetadata();
                metadata.Components.Add(componentMetadata);

                for (int x = 0; x < cs2.Components[i].LODs.Count; x++)
                {
                    Models.CS2.Component.LOD lod = cs2.Components[i].LODs[x];

                    string lodSuffix = Sanitise(lod.Name);
                    Node lodNode = new Node(LODTag(i, x) + (lodSuffix.Length == 0 ? "" : "_" + lodSuffix));
                    componentNode.Children.Add(lodNode);

                    LODMetadata lodMetadata = new LODMetadata() { Name = lod.Name };
                    componentMetadata.LODs.Add(lodMetadata);

                    for (int y = 0; y < lod.Submeshes.Count; y++)
                    {
                        Models.CS2.Component.LOD.Submesh submesh = lod.Submeshes[y];
                        string tag = SubmeshTag(i, x, y);

                        EnvironmentRigs.Part known = null;
                        placement?.TryGetValue(submesh, out known);

                        /* A part with vertex weights deforms and goes out weighted, whatever the
                         * level's record says about which bone it hangs off. */
                        int rigidBone = -1;
                        if (known != null) rigidBone = known.Skinned ? -1 : known.Bone;
                        else if (rigid) rigidBone = rigidBones != null ? rigidBones[i][x] : -1;

                        cMesh cathodeMesh = ModelUtility.ToMesh(submesh);
                        Mesh mesh = ToAssimpMesh(cathodeMesh, tag, materialIndex == null ? 0 : materialIndex(submesh), flipUVs, out int[] uvChannels, unitScale);
                        /* Weights on a prop bind against the rig's own model space, because its
                         * skeleton goes out without the rotation into mesh space a character's
                         * carries. That holds for any weighted mesh in a prop's file, listed in the
                         * level's record or not. */
                        if (rigidBone >= 0) AddRigidBone(mesh, rigidBone, usedBones, skeleton, bindPose, unitScale, propOffsets);
                        else if (!rigid || (known != null && known.Skinned))
                            AddBones(mesh, cathodeMesh, submesh.Bones, usedBones, skeleton, propBindPose ?? bindPose, unitScale);
                        scene.Meshes.Add(mesh);

                        Node submeshNode = new Node(tag);
                        submeshNode.MeshIndices.Add(scene.Meshes.Count - 1);
                        lodNode.Children.Add(submeshNode);

                        lodMetadata.Submeshes.Add(BuildSubmeshMetadata(submesh, cathodeMesh, tag, uvChannels));
                    }
                }
            }

            //Exporters only write a skin for bones that exist as nodes
            if (usedBones.Count != 0 || skeleton != null)
                scene.RootNode.Children.Add(BuildSkeletonNodes(skeleton, usedBones, unitScale, prop != null));
            return scene;
        }

        /* Just the rig, for exporting an animation with nothing bound to it */
        public static Scene BuildSkeletonScene(Skeleton skeleton, float unitScale = UnitScale)
        {
            Scene scene = new Scene();
            scene.RootNode = new Node(Sanitise(skeleton?.Name ?? "skeleton"));
            scene.RootNode.Children.Add(BuildSkeletonNodes(skeleton, new SortedSet<int>(), unitScale));
            scene.Materials.Add(new Assimp.Material());
            return scene;
        }

        /// <summary>
        /// Turn a clip into an animation keyed against the bone nodes <see cref="BuildScene"/> writes,
        /// so the two can be exported into the same file and line up.
        ///
        /// Keys are in the bones' own space, exactly as their rest transforms are written - the
        /// conversion into export space rides on the skeleton's parent node and applies to both.
        /// </summary>
        public static Assimp.Animation BuildAnimation(CathodeLib.Animation.ClipReference clip, Skeleton skeleton, string name = null,
                                                     CathodeLib.Animation.RootMotion rootMotion = CathodeLib.Animation.RootMotion.Ignore,
                                                     Retargeter retarget = null)
        {
            if (clip?.Animation == null || skeleton == null) return null;

            int frames = clip.Animation.FrameCount;
            float frameDuration = clip.Animation.FrameDuration > 0
                ? clip.Animation.FrameDuration
                : (frames > 1 ? clip.Animation.Duration / (frames - 1) : 1 / 30.0f);

            Assimp.Animation animation = new Assimp.Animation
            {
                Name = Sanitise(name ?? (clip.Name.Length != 0 ? clip.Name : Path.GetFileName(clip.Path))),
                TicksPerSecond = frameDuration > 0 ? 1.0 / frameDuration : 30.0,
                DurationInTicks = Math.Max(0, frames - 1),
            };

            /* Only the bones the clip actually drives get a channel - anything else is left at its
             * rest transform, which is what the node hierarchy already says. */
            SortedSet<int> driven = new SortedSet<int>();
            if (retarget == null)
            {
                foreach (int bone in clip.Animation.TrackToBone)
                    if (bone >= 0 && bone < skeleton.Bones.Count) driven.Add(bone);
            }
            else
            {
                /* Retargeted, the clip drives whichever of this rig.s bones the mapping covers -
                 * which has nothing to do with the bones the clip.s own tracks name. */
                foreach (HavokPackfile.BoneMapping pair in retarget.TargetPairs)
                    if (pair.BoneB >= 0 && pair.BoneB < skeleton.Bones.Count) driven.Add(pair.BoneB);
            }
            if (driven.Count == 0) return null;

            Dictionary<int, NodeAnimationChannel> channels = new Dictionary<int, NodeAnimationChannel>();
            foreach (int bone in driven)
                channels[bone] = new NodeAnimationChannel { NodeName = BoneName(bone, skeleton.Bones[bone].Name) };

            for (int frame = 0; frame < frames; frame++)
            {
                List<HavokPackfile.SampledTransform> pose = CathodeLib.Animation.SampleBones(clip, skeleton, frame, rootMotion, retarget);
                if (pose == null) break;

                foreach (int bone in driven)
                {
                    HavokPackfile.SampledTransform transform = pose[bone];
                    NodeAnimationChannel channel = channels[bone];
                    channel.PositionKeys.Add(new VectorKey(frame, ToAssimp(transform.Translation)));
                    channel.RotationKeys.Add(new QuaternionKey(frame, ToAssimp(transform.Rotation)));
                    channel.ScalingKeys.Add(new VectorKey(frame, ToAssimp(transform.Scale)));
                }
            }

            foreach (int bone in driven) animation.NodeAnimationChannels.Add(channels[bone]);
            return animation;
        }

        /* The rig the mesh binds to. With a skeleton we can write the real hierarchy, names and bind
         * pose; without one all we can offer is a flat set of nodes to hang the weights off. */
        private static Node BuildSkeletonNodes(Skeleton skeleton, SortedSet<int> usedBones, float unitScale, bool environment = false)
        {
            Node root = new Node(SkeletonNodeName);
            if (skeleton == null)
            {
                foreach (int bone in usedBones)
                    root.Children.Add(new Node(BoneName(bone)));
                return root;
            }

            /* Bone transforms stay in the skeleton's own space and the conversion to export space
             * rides on this one node, so each bone's local transform is just what Havok stored.
             *
             * A character's mesh is authored in mesh space, so its rig is rotated into it here. An
             * environment rig already lives in the same space as the prop it drives - that is the
             * difference SampleRigPose exists for - so rotating it would take the prop away from its
             * own geometry. */
            root.Transform = ToAssimp((environment ? Matrix4x4.Identity : Skeleton.ToMeshSpace)
                                      * Matrix4x4.CreateScale(unitScale, unitScale, -unitScale));

            Node[] nodes = new Node[skeleton.Bones.Count];
            for (int i = 0; i < skeleton.Bones.Count; i++)
            {
                nodes[i] = new Node(BoneName(i, skeleton.Bones[i].Name));
                nodes[i].Transform = ToAssimp(skeleton.Bones[i].LocalTransform);
            }
            for (int i = 0; i < nodes.Length; i++)
            {
                int parent = skeleton.Bones[i].ParentIndex;
                (parent >= 0 && parent < nodes.Length && parent != i ? nodes[parent] : root).Children.Add(nodes[i]);
            }
            return root;
        }

        /* CATHODE stores four bone slots per vertex, indexing a per-submesh palette of skeleton bone indices.
         * We write them out as real weighted bones so they can be seen and edited, named after the skeleton index. */
        private static void AddBones(Mesh mesh, cMesh cathodeMesh, List<int> palette, SortedSet<int> usedBones, Skeleton skeleton, List<Matrix4x4> bindPose, float unitScale)
        {
            if (cathodeMesh.BoneWeights.Count != cathodeMesh.Vertices.Count || cathodeMesh.BoneIndexes.Count != cathodeMesh.Vertices.Count)
                return;

            Dictionary<int, Bone> bones = new Dictionary<int, Bone>();
            for (int vertex = 0; vertex < cathodeMesh.Vertices.Count; vertex++)
            {
                Vector4 indexes = cathodeMesh.BoneIndexes[vertex];
                Vector4 weights = cathodeMesh.BoneWeights[vertex];

                for (int slot = 0; slot < 4; slot++)
                {
                    float weight = Component(weights, slot);
                    if (weight <= 0.0f) continue;

                    int local = (int)Math.Round(Component(indexes, slot));
                    int bone = (palette != null && local >= 0 && local < palette.Count) ? palette[local] : local;

                    if (!bones.TryGetValue(bone, out Bone assimpBone))
                    {
                        assimpBone = new Bone()
                        {
                            Name = BoneName(bone, skeleton != null && bone < skeleton.Bones.Count ? skeleton.Bones[bone].Name : null),
                            OffsetMatrix = InverseBindPose(bindPose, bone, unitScale),
                        };
                        bones[bone] = assimpBone;
                        usedBones?.Add(bone);
                    }
                    assimpBone.VertexWeights.Add(new VertexWeight(vertex, weight));
                }
            }

            foreach (KeyValuePair<int, Bone> bone in bones.OrderBy(x => x.Key))
                mesh.Bones.Add(bone.Value);
        }

        /* Bind a whole submesh to one bone. That's how the game moves a part of a prop - the part sits
         * where it belongs already and the bone carries a delta - and it's the only way to say the
         * same thing in a model format, which only knows about weights. */
        private static void AddRigidBone(Mesh mesh, int bone, SortedSet<int> usedBones, Skeleton skeleton, List<Matrix4x4> bindPose, float unitScale,
                                         Matrix4x4[] propOffsets = null)
        {
            Bone assimpBone = new Bone()
            {
                Name = BoneName(bone, skeleton != null && bone < skeleton.Bones.Count ? skeleton.Bones[bone].Name : null),
                OffsetMatrix = propOffsets != null
                    ? PropBind(propOffsets, bone, unitScale)
                    : InverseBindPose(bindPose, bone, unitScale),
            };
            for (int vertex = 0; vertex < mesh.VertexCount; vertex++)
                assimpBone.VertexWeights.Add(new VertexWeight(vertex, 1.0f));

            mesh.Bones.Add(assimpBone);
            usedBones?.Add(bone);
        }

        /* A skin binds through the inverse of where the bone sat when the mesh was authored. Without a
         * skeleton we have nothing to invert, and identity at least keeps the weights readable. */

        /// <summary>Identity, not equality - two submeshes with the same contents are still two submeshes.</summary>
        private class SameSubmesh : IEqualityComparer<Models.CS2.Component.LOD.Submesh>
        {
            public static readonly SameSubmesh Instance = new SameSubmesh();

            public bool Equals(Models.CS2.Component.LOD.Submesh x, Models.CS2.Component.LOD.Submesh y) { return ReferenceEquals(x, y); }
            public int GetHashCode(Models.CS2.Component.LOD.Submesh obj) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj); }
        }

        private static Assimp.Matrix4x4 InverseBindPose(List<Matrix4x4> bindPose, int bone, float unitScale)
        {
            if (bindPose == null || bone < 0 || bone >= bindPose.Count)
                return Assimp.Matrix4x4.Identity;

            Matrix4x4 pose = bindPose[bone] * Matrix4x4.CreateScale(unitScale, unitScale, -unitScale);
            return Matrix4x4.Invert(pose, out Matrix4x4 inverse) ? ToAssimp(inverse) : Assimp.Matrix4x4.Identity;
        }

        /* What a rigid part of a prop binds through.
         *
         * A character's mesh is authored around the rig's bind pose, so its bind matrix is the
         * inverse of where the bone sat and a vertex comes back to itself at rest. A prop's parts
         * are the other way up: each is modelled about its own origin and the level says where it
         * goes, so the bind matrix has to carry it there instead. That placement is exactly what
         * EnvironmentRigs.Offsets holds - the same thing the preview poses with, so a file and the
         * preview cannot drift apart.
         *
         * (A part that deforms needs no special case: its vertices are already in the prop's space,
         * so it takes the ordinary inverse bind, just against the rig's own model space rather than
         * mesh space.)
         *
         * The unit scale is divided back out because the vertices carry it already and so does the
         * skeleton's root node - it must not be applied twice. */
        private static Assimp.Matrix4x4 PropBind(Matrix4x4[] propOffsets, int bone, float unitScale)
        {
            if (propOffsets == null || bone < 0 || bone >= propOffsets.Length) return Assimp.Matrix4x4.Identity;

            Matrix4x4 scale = Matrix4x4.CreateScale(unitScale, unitScale, -unitScale);
            return Matrix4x4.Invert(scale, out Matrix4x4 unscale)
                ? ToAssimp(unscale * propOffsets[bone]) : Assimp.Matrix4x4.Identity;
        }

        private static float Component(Vector4 value, int index)
        {
            switch (index)
            {
                case 0: return value.X;
                case 1: return value.Y;
                case 2: return value.Z;
                default: return value.W;
            }
        }

        /* Describe an existing submesh so its properties can be applied to a mesh that's replacing it. Per-vertex data
         * is deliberately left out: the replacement is different geometry, so none of it would line up. */
        public static SubmeshMetadata DescribeSubmesh(Models.CS2.Component.LOD.Submesh submesh)
        {
            if (submesh == null) return null;

            int[] uvChannels = ReferenceEquals(submesh.VertexFormatFull, null) ? new int[0] : submesh.VertexFormatFull.Attributes
                .SelectMany(stream => stream)
                .Where(attribute => attribute.Usage == VertexFormat.Usage.TexCoord)
                .Select(attribute => attribute.Index).Distinct().OrderBy(index => index).ToArray();

            SubmeshMetadata metadata = BuildSubmeshMetadata(submesh, new cMesh(), "", uvChannels);
            metadata.Indices = null;
            metadata.PositionW = null;
            metadata.BlendIndices = null;
            metadata.BlendWeights = null;
            metadata.Bones = null;
            metadata.VertexCount = 0;
            return metadata;
        }

        private static SubmeshMetadata BuildSubmeshMetadata(Models.CS2.Component.LOD.Submesh submesh, cMesh mesh, string tag, int[] uvChannels)
        {
            SubmeshMetadata metadata = new SubmeshMetadata()
            {
                Tag = tag,
                Material = submesh.Material?.Name,
                RenderFlags = (uint)submesh.RenderFlags,
                MinLODRange = submesh.MinLODRange,
                MaxLODRange = submesh.MaxLODRange,
                MinBounds = new float[] { submesh.MinBounds.X, submesh.MinBounds.Y, submesh.MinBounds.Z },
                MaxBounds = new float[] { submesh.MaxBounds.X, submesh.MaxBounds.Y, submesh.MaxBounds.Z },
                VertexScale = submesh.VertexScale,
                VertexCount = submesh.VertexCount,
                CollisionProxyIndex = submesh.CollisionProxyIndex,
                VertexFormatFull = ToMetadata(submesh.VertexFormatFull),
                VertexFormatPartial = ToMetadata(submesh.VertexFormatPartial),
                UVChannels = uvChannels,
                Bones = submesh.Bones.Count == 0 ? null : new List<int>(submesh.Bones),
            };

            if (mesh.Indices.Count == submesh.IndexCount)
                metadata.Indices = Convert.ToBase64String(ToBytes(mesh.Indices.Select(x => (short)x).ToArray()));

            short[] positionW = ReadPositionW(submesh);
            if (positionW != null && positionW.Any(x => x != -short.MaxValue))
                metadata.PositionW = Convert.ToBase64String(ToBytes(positionW));

            if (mesh.BoneIndexes.Count == submesh.VertexCount && mesh.BoneWeights.Count == submesh.VertexCount)
            {
                metadata.BlendIndices = Convert.ToBase64String(ToBytes(mesh.BoneIndexes, 1.0f));
                metadata.BlendWeights = Convert.ToBase64String(ToBytes(mesh.BoneWeights, 255.0f));
            }
            return metadata;
        }

        private static List<List<AttributeMetadata>> ToMetadata(VertexFormat format)
        {
            if (ReferenceEquals(format, null)) return null;
            return format.Attributes.Select(stream => stream.Select(attribute => new AttributeMetadata(attribute)).ToList()).ToList();
        }

        private static VertexFormat FromMetadata(List<List<AttributeMetadata>> metadata)
        {
            if (metadata == null) return null;
            VertexFormat format = new VertexFormat();
            foreach (List<AttributeMetadata> stream in metadata)
                format.Attributes.Add(stream.Select(attribute => attribute.ToAttribute()).ToList());
            return format;
        }

        public static Mesh ToAssimpMesh(Models.CS2.Component.LOD.Submesh submesh, string name, int materialIndex, bool flipUVs, out int[] uvChannels, float unitScale = UnitScale)
        {
            return ToAssimpMesh(ModelUtility.ToMesh(submesh), name, materialIndex, flipUVs, out uvChannels, unitScale);
        }

        public static Mesh ToAssimpMesh(cMesh cathodeMesh, string name, int materialIndex, bool flipUVs, out int[] uvChannels, float unitScale = UnitScale)
        {
            Mesh mesh = new Mesh();
            mesh.Name = name;
            mesh.MaterialIndex = materialIndex;
            uvChannels = new int[0];

            //CATHODE is Z-flipped relative to the scenes we write, and the winding flips with it. Reverse each
            //triangle rather than swapping two corners, so the importer's FlipWindingOrder gives us back the original.
            int[] indices = cathodeMesh.Indices.Select(x => (int)x).ToArray();
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                int a = indices[i];
                indices[i] = indices[i + 2];
                indices[i + 2] = a;
            }
            if (cathodeMesh.Vertices.Count == 0 || !mesh.SetIndices(indices, 3))
                return mesh;

            for (int i = 0; i < cathodeMesh.Vertices.Count; i++)
                mesh.Vertices.Add(new Assimp.Vector3D(cathodeMesh.Vertices[i].X * unitScale, cathodeMesh.Vertices[i].Y * unitScale, -cathodeMesh.Vertices[i].Z * unitScale));

            if (cathodeMesh.Normals.Count == cathodeMesh.Vertices.Count)
                for (int i = 0; i < cathodeMesh.Normals.Count; i++)
                    mesh.Normals.Add(new Assimp.Vector3D(cathodeMesh.Normals[i].X, cathodeMesh.Normals[i].Y, -cathodeMesh.Normals[i].Z));

            //Assimp only considers a tangent basis valid if both halves are present
            if (cathodeMesh.Tangents.Count == cathodeMesh.Vertices.Count && cathodeMesh.BiNormals.Count == cathodeMesh.Vertices.Count)
            {
                for (int i = 0; i < cathodeMesh.Tangents.Count; i++)
                {
                    mesh.Tangents.Add(new Assimp.Vector3D(cathodeMesh.Tangents[i].X, cathodeMesh.Tangents[i].Y, -cathodeMesh.Tangents[i].Z));
                    mesh.BiTangents.Add(new Assimp.Vector3D(cathodeMesh.BiNormals[i].X, cathodeMesh.BiNormals[i].Y, -cathodeMesh.BiNormals[i].Z));
                }
            }

            if (cathodeMesh.Colours.Count == cathodeMesh.Vertices.Count)
                for (int i = 0; i < cathodeMesh.Colours.Count; i++)
                    mesh.VertexColorChannels[0].Add(new Color4D(cathodeMesh.Colours[i].R / 255.0f, cathodeMesh.Colours[i].G / 255.0f, cathodeMesh.Colours[i].B / 255.0f, cathodeMesh.Colours[i].A / 255.0f));

            List<int> exportedUVs = new List<int>();
            for (int i = 0; i < cathodeMesh.UVs.Length && exportedUVs.Count < mesh.TextureCoordinateChannels.Length; i++)
            {
                if (cathodeMesh.UVs[i] == null || cathodeMesh.UVs[i].Count != cathodeMesh.Vertices.Count) continue;

                int channel = exportedUVs.Count;
                for (int x = 0; x < cathodeMesh.UVs[i].Count; x++)
                    mesh.TextureCoordinateChannels[channel].Add(new Assimp.Vector3D(cathodeMesh.UVs[i][x].X, flipUVs ? 1.0f - cathodeMesh.UVs[i][x].Y : cathodeMesh.UVs[i][x].Y, 0));
                mesh.UVComponentCount[channel] = 2;
                exportedUVs.Add(i);
            }
            uvChannels = exportedUVs.ToArray();

            return mesh;
        }

        /* The W of a packed position isn't part of cMesh, but it isn't always the same value, so pull it out separately */
        private static short[] ReadPositionW(Models.CS2.Component.LOD.Submesh submesh)
        {
            if (submesh.Data.Length == 0 || ReferenceEquals(submesh.VertexFormatFull, null) || submesh.VertexFormatFull.Attributes.Count < 2)
                return null;

            List<VertexFormat.Attribute> stream = submesh.VertexFormatFull.Attributes[0];
            int offset = 0;
            bool found = false;
            foreach (VertexFormat.Attribute attribute in stream)
            {
                if (attribute.Usage == VertexFormat.Usage.Position)
                {
                    if (attribute.Type != VertexFormat.Type.S16_4N) return null;
                    found = true;
                    break;
                }
                offset += SizeOf(attribute.Type);
            }
            if (!found) return null;

            int stride = stream.Sum(x => SizeOf(x.Type));
            if (stride == 0 || (long)stride * submesh.VertexCount > submesh.Data.Length) return null;

            short[] w = new short[submesh.VertexCount];
            using (BinaryReader reader = new BinaryReader(new MemoryStream(submesh.Data)))
            {
                for (int i = 0; i < submesh.VertexCount; i++)
                {
                    reader.BaseStream.Position = (i * stride) + offset + 6;
                    w[i] = reader.ReadInt16();
                }
            }
            return w;
        }

        #endregion

        #region IMPORT

        /* JoinIdenticalVertices matters as much as the rest: importers hand back a vertex per index, which would blow
         * straight through the 16 bit index limit on anything but the smallest meshes. */
        public const PostProcessSteps ImportPostProcessSteps =
            PostProcessSteps.Triangulate | PostProcessSteps.LimitBoneWeights |
            PostProcessSteps.GenerateBoundingBoxes | PostProcessSteps.FlipUVs | PostProcessSteps.FlipWindingOrder |
            PostProcessSteps.MakeLeftHanded | PostProcessSteps.GenerateSmoothNormals | PostProcessSteps.JoinIdenticalVertices;

        /* The structure we'll create from a scene, so it can be shown to the user before anything is built */
        public class ImportPlan
        {
            public string Name;
            public bool HasMetadata;
            public float UnitScale = ModelIO.UnitScale;
            public List<PlannedComponent> Components = new List<PlannedComponent>();

            public IEnumerable<PlannedSubmesh> AllSubmeshes()
            {
                foreach (PlannedComponent component in Components)
                    foreach (PlannedLOD lod in component.LODs)
                        foreach (PlannedSubmesh submesh in lod.Submeshes)
                            yield return submesh;
            }
        }

        public class PlannedComponent
        {
            public List<PlannedLOD> LODs = new List<PlannedLOD>();
        }

        public class PlannedLOD
        {
            public string Name = "";
            public List<PlannedSubmesh> Submeshes = new List<PlannedSubmesh>();
        }

        public class PlannedSubmesh
        {
            public int MeshIndex;
            public string MeshName;
            public string Tag;
            public SubmeshMetadata Metadata;
            public Matrix4x4 Transform = Matrix4x4.Identity;
            public bool Include = true;

            //Set by the caller to override whatever the metadata asks for
            public Materials.Material Material;
        }

        /* Work out what structure a scene describes: tagged objects rebuild their original layout, anything
         * untagged falls back to a single component/LOD holding every mesh in the order we find them. */
        public static ImportPlan CreateImportPlan(Scene scene, ModelMetadata metadata, string fallbackName)
        {
            ImportPlan plan = new ImportPlan()
            {
                Name = metadata?.Name ?? (fallbackName + ".CS2"),
                HasMetadata = metadata != null,
                UnitScale = (metadata != null && metadata.UnitScale > 0) ? metadata.UnitScale : UnitScale,
            };
            if (scene == null) return plan;

            Dictionary<int, Node> meshNodes = new Dictionary<int, Node>();
            Dictionary<Node, Matrix4x4> transforms = new Dictionary<Node, Matrix4x4>();
            CollectNodes(scene.RootNode, Matrix4x4.Identity, meshNodes, transforms);

            //(component, lod) -> submeshes, keeping the order tags ask for and the scene order for anything untagged
            SortedDictionary<int, SortedDictionary<int, List<Tuple<int, PlannedSubmesh>>>> tagged = new SortedDictionary<int, SortedDictionary<int, List<Tuple<int, PlannedSubmesh>>>>();
            List<PlannedSubmesh> untagged = new List<PlannedSubmesh>();
            Dictionary<string, string> lodNames = new Dictionary<string, string>();

            for (int i = 0; i < scene.MeshCount; i++)
            {
                meshNodes.TryGetValue(i, out Node node);
                PlannedSubmesh submesh = new PlannedSubmesh()
                {
                    MeshIndex = i,
                    MeshName = string.IsNullOrEmpty(scene.Meshes[i].Name) ? (node?.Name ?? "Mesh " + i) : scene.Meshes[i].Name,
                    Transform = node != null && transforms.ContainsKey(node) ? transforms[node] : Matrix4x4.Identity,
                };

                if (!TryResolveTag(scene.Meshes[i].Name, node, out int component, out int lod, out int index))
                {
                    untagged.Add(submesh);
                    continue;
                }

                submesh.Tag = SubmeshTag(component, lod, index);
                submesh.Metadata = metadata?.FindSubmesh(submesh.Tag);

                if (!tagged.ContainsKey(component)) tagged[component] = new SortedDictionary<int, List<Tuple<int, PlannedSubmesh>>>();
                if (!tagged[component].ContainsKey(lod)) tagged[component][lod] = new List<Tuple<int, PlannedSubmesh>>();
                tagged[component][lod].Add(new Tuple<int, PlannedSubmesh>(index, submesh));

                string authoredName = FindAuthoredLODName(node);
                if (authoredName != null && !lodNames.ContainsKey(LODTag(component, lod)))
                    lodNames[LODTag(component, lod)] = authoredName;
            }

            foreach (KeyValuePair<int, SortedDictionary<int, List<Tuple<int, PlannedSubmesh>>>> component in tagged)
            {
                PlannedComponent plannedComponent = new PlannedComponent();
                plan.Components.Add(plannedComponent);

                foreach (KeyValuePair<int, List<Tuple<int, PlannedSubmesh>>> lod in component.Value)
                {
                    lodNames.TryGetValue(LODTag(component.Key, lod.Key), out string authoredName);
                    PlannedLOD plannedLOD = new PlannedLOD() { Name = metadata?.FindLODName(component.Key, lod.Key) ?? authoredName ?? "" };
                    plannedLOD.Submeshes.AddRange(lod.Value.OrderBy(x => x.Item1).Select(x => x.Item2));
                    plannedComponent.LODs.Add(plannedLOD);
                }
            }

            if (untagged.Count != 0)
            {
                PlannedComponent plannedComponent = new PlannedComponent();
                PlannedLOD plannedLOD = new PlannedLOD() { Name = plan.Components.Count == 0 ? (metadata?.FindLODName(0, 0) ?? fallbackName ?? "") : "" };
                plannedLOD.Submeshes.AddRange(untagged);
                plannedComponent.LODs.Add(plannedLOD);
                plan.Components.Add(plannedComponent);
            }

            return plan;
        }

        private static void CollectNodes(Node node, Matrix4x4 parentTransform, Dictionary<int, Node> meshNodes, Dictionary<Node, Matrix4x4> transforms)
        {
            if (node == null) return;

            Matrix4x4 transform = ToNumerics(node.Transform) * parentTransform;
            transforms[node] = transform;
            foreach (int meshIndex in node.MeshIndices)
                if (!meshNodes.ContainsKey(meshIndex))
                    meshNodes[meshIndex] = node;

            foreach (Node child in node.Children)
                CollectNodes(child, transform, meshNodes, transforms);
        }

        /* We write LOD names as a suffix on the LOD's node, so pick them back up when there's no sidecar to read them from */
        private static string FindAuthoredLODName(Node node)
        {
            Node current = node;
            while (current != null)
            {
                if (TryParseTag(current.Name, out int component, out int lod, out int submesh) && lod != -1 && submesh == -1)
                {
                    string suffix = current.Name.Substring(LODTag(component, lod).Length);
                    return suffix.StartsWith("_") ? suffix.Substring(1) : null;
                }
                current = current.Parent;
            }
            return null;
        }

        /* Prefer the mesh name (which is what survives an FBX round trip), then the node it hangs off, then its parents */
        private static bool TryResolveTag(string meshName, Node node, out int component, out int lod, out int submesh)
        {
            if (TryParseTag(meshName, out component, out lod, out submesh) && submesh != -1) return true;

            Node current = node;
            while (current != null)
            {
                if (TryParseTag(current.Name, out component, out lod, out submesh) && submesh != -1) return true;
                current = current.Parent;
            }

            component = lod = submesh = -1;
            return false;
        }

        /* Turn a plan into a CS2. Submeshes with metadata keep their original vertex format and properties. */
        public static Models.CS2 BuildCS2(Scene scene, ImportPlan plan, Func<string, Materials.Material> findMaterial, Materials.Material fallbackMaterial, out List<string> warnings)
        {
            warnings = new List<string>();

            Models.CS2 cs2 = new Models.CS2() { Name = plan.Name };
            foreach (PlannedComponent plannedComponent in plan.Components)
            {
                Models.CS2.Component component = new Models.CS2.Component();
                foreach (PlannedLOD plannedLOD in plannedComponent.LODs)
                {
                    List<PlannedSubmesh> included = plannedLOD.Submeshes.Where(x => x.Include).ToList();
                    if (included.Count == 0) continue;

                    Models.CS2.Component.LOD lod = new Models.CS2.Component.LOD(plannedLOD.Name ?? "");
                    foreach (PlannedSubmesh plannedSubmesh in included)
                    {
                        Models.CS2.Component.LOD.Submesh submesh = ToSubmesh(scene.Meshes[plannedSubmesh.MeshIndex], plannedSubmesh.Transform, plannedSubmesh.Metadata, out List<string> submeshWarnings, plan.UnitScale);
                        if (submesh == null)
                        {
                            warnings.Add(plannedSubmesh.MeshName + ": could not be converted (it needs at least 3 vertices, triangular faces, and no more than " + short.MaxValue + " vertices).");
                            continue;
                        }
                        foreach (string warning in submeshWarnings)
                            warnings.Add(plannedSubmesh.MeshName + ": " + warning);

                        Materials.Material material = plannedSubmesh.Material;
                        if (material == null && plannedSubmesh.Metadata?.Material != null)
                            material = findMaterial?.Invoke(plannedSubmesh.Metadata.Material);
                        submesh.Material = material ?? fallbackMaterial;

                        lod.Submeshes.Add(submesh);
                    }
                    if (lod.Submeshes.Count != 0)
                        component.LODs.Add(lod);
                }
                if (component.LODs.Count != 0)
                    cs2.Components.Add(component);
            }
            return cs2;
        }

        /* Convert a single Assimp mesh into a submesh, re-using the original vertex format where we have one */
        public static Models.CS2.Component.LOD.Submesh ToSubmesh(Mesh mesh, Matrix4x4 transform, SubmeshMetadata metadata, out List<string> warnings, float unitScale = UnitScale)
        {
            warnings = new List<string>();

            if (mesh == null || mesh.VertexCount < 3) return null;

            //A triangulated mesh can still hold the odd line or point where the source geometry was degenerate
            List<int> triangles = new List<int>(mesh.FaceCount * 3);
            foreach (Face face in mesh.Faces)
            {
                if (face.IndexCount != 3) continue;
                triangles.AddRange(face.Indices);
            }
            if (triangles.Count == 0) return null;

            int[] indices = triangles.ToArray();
            if (indices.Length > ushort.MaxValue) return null;

            //Importers weld and renumber vertices, so put the original numbering back where we still recognise the mesh
            int[] vertexMap = ResolveVertexNumbering(mesh, ref indices, metadata, out bool numberingRestored);

            //Vertices are addressed by 16 bit indices, so that's the ceiling
            if (vertexMap.Length > ushort.MaxValue) return null;

            Models.CS2.Component.LOD.Submesh submesh = new Models.CS2.Component.LOD.Submesh();
            submesh.VertexCount = vertexMap.Length;
            submesh.IndexCount = indices.Length;

            //Positions are the only thing we have to move into CATHODE's space; the importer has already un-flipped Z for us
            bool hasTransform = !transform.IsIdentity;
            if (unitScale <= 0) unitScale = UnitScale;
            List<Vector3> positions = new List<Vector3>(vertexMap.Length);
            for (int i = 0; i < vertexMap.Length; i++)
            {
                Assimp.Vector3D vertex = mesh.Vertices[vertexMap[i]];
                Vector3 position = new Vector3(vertex.X, vertex.Y, vertex.Z);
                if (hasTransform) position = Vector3.Transform(position, transform);
                positions.Add(position / unitScale);
            }

            Vector3 min = positions[0], max = positions[0];
            foreach (Vector3 position in positions)
            {
                min = Vector3.Min(min, position);
                max = Vector3.Max(max, position);
            }
            submesh.MinBounds = min;
            submesh.MaxBounds = max;

            //Positions are stored normalised against the scale, so it has to cover the mesh's extent
            int requiredScale = CalculateScaleFactor(min, max);
            submesh.VertexScale = (metadata != null && metadata.VertexScale >= requiredScale) ? metadata.VertexScale : requiredScale;

            submesh.RenderFlags = metadata != null ? (RenderingFlag)metadata.RenderFlags : DefaultRenderFlags;
            submesh.MinLODRange = metadata?.MinLODRange ?? 0;
            submesh.MaxLODRange = metadata?.MaxLODRange ?? 10000;
            submesh.CollisionProxyIndex = metadata?.CollisionProxyIndex ?? -1;

            //Exporters don't write a tangent basis, so rebuild one if the format we're targeting expects it
            bool needsTangents = metadata?.VertexFormatFull != null && metadata.VertexFormatFull.Any(stream =>
                stream.Any(x => x.Usage == VertexFormat.Usage.Tangent.ToString() || x.Usage == VertexFormat.Usage.Binormal.ToString()));

            ResolveSkinning(mesh, vertexMap, numberingRestored ? metadata : null, metadata, out byte[] blendIndices, out byte[] blendWeights, out List<int> bonePalette, warnings);
            if (bonePalette != null) submesh.Bones.AddRange(bonePalette);

            VertexSource source = new VertexSource(mesh, vertexMap, positions, submesh.VertexScale, numberingRestored ? metadata : null, metadata, needsTangents, indices, blendIndices, blendWeights);

            VertexFormat full = FromMetadata(metadata?.VertexFormatFull);
            VertexFormat partial = FromMetadata(metadata?.VertexFormatPartial);
            if (ReferenceEquals(full, null))
            {
                full = BuildVertexFormat(source, true);
                partial = BuildVertexFormat(source, false);
            }
            else
            {
                //The mesh may no longer have everything the original format asked for
                full = PruneVertexFormat(full, source, warnings);
                partial = ReferenceEquals(partial, null) ? BuildVertexFormat(source, false) : PruneVertexFormat(partial, source, null);
            }
            submesh.VertexFormatFull = full;
            submesh.VertexFormatPartial = partial;

            //A palette is meaningless if the format we ended up with has nowhere to put the blend indices
            if (!submesh.VertexFormatFull.Attributes.Any(stream => stream.Any(x => x.Usage == VertexFormat.Usage.BlendIndices)))
                submesh.Bones.Clear();

            //A submesh with nothing encoded in it is worse than no submesh at all
            submesh.Data = Encode(submesh.VertexFormatFull, source, indices);
            if (submesh.Data == null || submesh.Data.Length == 0) return null;
            return submesh;
        }

        /* The sidecar's copy of the blend data is exact, so prefer it whenever the vertex numbering lined up.
         * Otherwise fall back to the skin in the file, which is the only thing that survives an edited mesh. */
        private static void ResolveSkinning(Mesh mesh, int[] vertexMap, SubmeshMetadata perVertex, SubmeshMetadata metadata, out byte[] blendIndices, out byte[] blendWeights, out List<int> palette, List<string> warnings)
        {
            blendIndices = null;
            blendWeights = null;
            palette = null;

            if (perVertex?.BlendIndices != null && perVertex.BlendWeights != null)
            {
                blendIndices = Convert.FromBase64String(perVertex.BlendIndices);
                blendWeights = Convert.FromBase64String(perVertex.BlendWeights);
                if (blendIndices.Length == vertexMap.Length * 4 && blendWeights.Length == vertexMap.Length * 4)
                {
                    palette = perVertex.Bones == null ? null : new List<int>(perVertex.Bones);
                    return;
                }
                blendIndices = null;
                blendWeights = null;
            }

            if (TryReadBones(mesh, vertexMap, metadata?.Bones, out blendIndices, out blendWeights, out palette, out string warning))
                return;

            if (warning != null)
                warnings.Add(warning);
            else if (metadata?.BlendIndices != null)
                warnings.Add("this submesh was skinned, but the mesh being imported has no bone weights - it will import unskinned.");
        }

        /* Turn a skinned mesh back into CATHODE's four-slots-per-vertex form: a palette of skeleton bone indices for the
         * submesh, and per-vertex indices into it. Unlike the sidecar's copy, this survives the mesh being edited. */
        private static bool TryReadBones(Mesh mesh, int[] vertexMap, List<int> preferredPalette, out byte[] blendIndices, out byte[] blendWeights, out List<int> palette, out string warning)
        {
            blendIndices = null;
            blendWeights = null;
            palette = null;
            warning = null;
            if (mesh.BoneCount == 0) return false;

            //Bone names are how we know which skeleton bone is which - anything else can't be mapped onto the rig
            List<KeyValuePair<int, Bone>> bones = new List<KeyValuePair<int, Bone>>();
            foreach (Bone bone in mesh.Bones)
            {
                Match match = _boneRegex.Match(bone.Name ?? "");
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out int index) || index > byte.MaxValue)
                {
                    warning = "the mesh is skinned to bones this tool doesn't recognise ('" + bone.Name + "'), so the skinning has been dropped. Bones must keep their exported '" + BoneName(0) + "' style names.";
                    return false;
                }
                bones.Add(new KeyValuePair<int, Bone>(index, bone));
            }

            //Gather the weights per vertex, keeping only the four heaviest as that's all a vertex has room for
            List<List<KeyValuePair<int, float>>> perVertex = new List<List<KeyValuePair<int, float>>>(mesh.VertexCount);
            for (int i = 0; i < mesh.VertexCount; i++) perVertex.Add(new List<KeyValuePair<int, float>>());
            foreach (KeyValuePair<int, Bone> bone in bones)
                foreach (VertexWeight weight in bone.Value.VertexWeights)
                    if (weight.Weight > 0.0f && weight.VertexID >= 0 && weight.VertexID < perVertex.Count)
                        perVertex[weight.VertexID].Add(new KeyValuePair<int, float>(bone.Key, weight.Weight));

            SortedSet<int> used = new SortedSet<int>();
            for (int i = 0; i < perVertex.Count; i++)
            {
                if (perVertex[i].Count > 4)
                    perVertex[i] = perVertex[i].OrderByDescending(x => x.Value).Take(4).ToList();
                foreach (KeyValuePair<int, float> entry in perVertex[i])
                    used.Add(entry.Key);
            }
            if (used.Count == 0) return false;

            //Reuse the palette the submesh already had if it still covers everything, so unedited meshes keep their layout
            palette = (preferredPalette != null && used.All(preferredPalette.Contains)) ? new List<int>(preferredPalette) : used.ToList();
            if (palette.Count > byte.MaxValue + 1)
            {
                warning = "the mesh is skinned to " + palette.Count + " bones, but a submesh can only reference " + (byte.MaxValue + 1) + " - the skinning has been dropped.";
                palette = null;
                return false;
            }

            blendIndices = new byte[vertexMap.Length * 4];
            blendWeights = new byte[vertexMap.Length * 4];
            for (int i = 0; i < vertexMap.Length; i++)
            {
                List<KeyValuePair<int, float>> entries = perVertex[vertexMap[i]];
                float total = entries.Sum(x => x.Value);
                if (total <= 0.0f) continue;

                //Weights are stored as bytes summing to 255, so hand the rounding error to the heaviest bone
                int assigned = 0, heaviest = 0;
                for (int slot = 0; slot < entries.Count; slot++)
                {
                    byte weight = (byte)Math.Max(0, Math.Min(255, Math.Round(entries[slot].Value / total * 255.0f)));
                    blendIndices[(i * 4) + slot] = (byte)palette.IndexOf(entries[slot].Key);
                    blendWeights[(i * 4) + slot] = weight;
                    assigned += weight;
                    if (entries[slot].Value > entries[heaviest].Value) heaviest = slot;
                }
                blendWeights[(i * 4) + heaviest] = (byte)Math.Max(0, Math.Min(255, blendWeights[(i * 4) + heaviest] + (255 - assigned)));
            }
            return true;
        }

        /* Importers weld duplicated vertices and renumber what's left, which loses the per-vertex data the mesh formats
         * can't carry. As long as the triangles still line up with the ones we exported, we can undo that: corner N of
         * the imported mesh is corner N of the original, so the original index buffer tells us where each vertex went.
         * Returns a map of submesh vertex -> Assimp vertex, and rewrites the indices to match. */
        private static int[] ResolveVertexNumbering(Mesh mesh, ref int[] indices, SubmeshMetadata metadata, out bool restored)
        {
            restored = false;

            short[] original = metadata?.Indices == null ? null : FromBytes(Convert.FromBase64String(metadata.Indices));
            if (original != null && original.Length == indices.Length && metadata.VertexCount > 0)
            {
                int[] map = new int[metadata.VertexCount];
                for (int i = 0; i < map.Length; i++) map[i] = -1;

                bool usable = true;
                for (int i = 0; i < original.Length; i++)
                {
                    int vertex = (ushort)original[i];
                    if (vertex >= map.Length) { usable = false; break; }
                    if (map[vertex] == -1) map[vertex] = indices[i];

                    //Two corners that shared a vertex must still share one, or this isn't the mesh we exported
                    if (map[vertex] != indices[i]) { usable = false; break; }
                }
                if (usable && !map.Contains(-1))
                {
                    int[] rewritten = new int[original.Length];
                    for (int i = 0; i < original.Length; i++) rewritten[i] = (ushort)original[i];
                    indices = rewritten;
                    restored = true;
                    return map;
                }
            }

            int[] identity = new int[mesh.VertexCount];
            for (int i = 0; i < identity.Length; i++) identity[i] = i;
            return identity;
        }

        public const RenderingFlag DefaultRenderFlags =
            RenderingFlag.IS_FIRST_PERSON_LOD | RenderingFlag.HAS_FIRST_PERSON_LOD |
            RenderingFlag.IS_THIRD_PERSON_LOD | RenderingFlag.HAS_THIRD_PERSON_LOD |
            RenderingFlag.IS_SHADOW_CASTING | RenderingFlag.HAS_SHADOW_CASTING |
            RenderingFlag.IS_LEVEL_PACK;

        /* Scale factors in the game's own models are powers of two */
        public static int CalculateScaleFactor(Vector3 min, Vector3 max)
        {
            float extent = Math.Max(Math.Max(Math.Abs(min.X), Math.Abs(max.X)), Math.Max(Math.Max(Math.Abs(min.Y), Math.Abs(max.Y)), Math.Max(Math.Abs(min.Z), Math.Abs(max.Z))));
            int scale = 1;
            while (scale < ushort.MaxValue && extent > scale)
                scale *= 2;
            return scale;
        }

        #endregion

        #region VERTEX DATA

        /* Everything we can hand to the encoder, pulled out of the Assimp mesh (and the sidecar, for the bits it can't hold) */
        private class VertexSource
        {
            public readonly int VertexCount;
            public readonly int VertexScale;
            public readonly List<Vector3> Positions;
            public readonly float[] PositionW;
            public readonly Mesh Mesh;
            public readonly int[] VertexMap; //submesh vertex -> Assimp vertex
            public readonly Dictionary<int, int> UVChannels = new Dictionary<int, int>(); //CATHODE TexCoord index -> Assimp channel
            public readonly byte[] BlendIndices;
            public readonly byte[] BlendWeights;
            public readonly Vector3[] Tangents;
            public readonly Vector3[] Binormals;

            /* <paramref name="perVertex"/> is only set when the original vertex numbering was recovered, so anything
             * indexed by vertex can be trusted; <paramref name="metadata"/> is always the submesh's own entry. */
            public VertexSource(Mesh mesh, int[] vertexMap, List<Vector3> positions, int vertexScale, SubmeshMetadata perVertex, SubmeshMetadata metadata, bool generateTangents, int[] indices, byte[] blendIndices, byte[] blendWeights)
            {
                Mesh = mesh;
                VertexMap = vertexMap;
                Positions = positions;
                VertexCount = positions.Count;
                VertexScale = vertexScale;
                BlendIndices = blendIndices;
                BlendWeights = blendWeights;

                for (int i = 0; i < mesh.TextureCoordinateChannelCount; i++)
                {
                    if (mesh.TextureCoordinateChannels[i].Count != mesh.VertexCount) continue;
                    int index = (metadata?.UVChannels != null && i < metadata.UVChannels.Length) ? metadata.UVChannels[i] : i;
                    if (!UVChannels.ContainsKey(index)) UVChannels[index] = i;
                }

                if (perVertex?.PositionW != null)
                {
                    short[] packed = FromBytes(Convert.FromBase64String(perVertex.PositionW));
                    if (packed.Length == VertexCount)
                    {
                        PositionW = new float[VertexCount];
                        for (int i = 0; i < VertexCount; i++)
                            PositionW[i] = packed[i] / (float)short.MaxValue;
                    }
                }
                if (generateTangents && mesh.Normals.Count == mesh.VertexCount && UVChannels.ContainsKey(0))
                    GenerateTangents(indices, out Tangents, out Binormals);
            }

            /* Rebuild a tangent basis from the UVs, in the same handedness the game stores */
            private void GenerateTangents(int[] indices, out Vector3[] tangents, out Vector3[] binormals)
            {
                tangents = new Vector3[VertexCount];
                binormals = new Vector3[VertexCount];

                int channel = UVChannels[0];
                Vector3[] accumulatedTangent = new Vector3[VertexCount];
                Vector3[] accumulatedBinormal = new Vector3[VertexCount];

                for (int i = 0; i + 2 < indices.Length; i += 3)
                {
                    int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                    if (a >= VertexCount || b >= VertexCount || c >= VertexCount) continue;

                    Vector3 edge1 = Positions[b] - Positions[a];
                    Vector3 edge2 = Positions[c] - Positions[a];

                    Assimp.Vector3D uvA = Mesh.TextureCoordinateChannels[channel][VertexMap[a]];
                    Assimp.Vector3D uvB = Mesh.TextureCoordinateChannels[channel][VertexMap[b]];
                    Assimp.Vector3D uvC = Mesh.TextureCoordinateChannels[channel][VertexMap[c]];
                    float du1 = uvB.X - uvA.X, dv1 = uvB.Y - uvA.Y;
                    float du2 = uvC.X - uvA.X, dv2 = uvC.Y - uvA.Y;

                    float determinant = (du1 * dv2) - (du2 * dv1);
                    if (Math.Abs(determinant) < 1e-12f) continue;
                    float r = 1.0f / determinant;

                    Vector3 tangent = ((edge1 * dv2) - (edge2 * dv1)) * r;
                    Vector3 binormal = ((edge2 * du1) - (edge1 * du2)) * r;

                    foreach (int vertex in new int[] { a, b, c })
                    {
                        accumulatedTangent[vertex] += tangent;
                        accumulatedBinormal[vertex] += binormal;
                    }
                }

                for (int i = 0; i < VertexCount; i++)
                {
                    Assimp.Vector3D source = Mesh.Normals[VertexMap[i]];
                    Vector3 normal = new Vector3(source.X, source.Y, source.Z);
                    Vector3 tangent = accumulatedTangent[i] - (normal * Vector3.Dot(normal, accumulatedTangent[i]));
                    tangent = tangent.LengthSquared() > 1e-12f ? Vector3.Normalize(tangent) : AnyPerpendicular(normal);
                    tangents[i] = tangent;

                    Vector3 binormal = Vector3.Cross(normal, tangent);
                    if (Vector3.Dot(binormal, accumulatedBinormal[i]) < 0) binormal = -binormal;
                    binormals[i] = binormal;
                }
            }

            private static Vector3 AnyPerpendicular(Vector3 normal)
            {
                Vector3 candidate = Math.Abs(normal.X) < 0.9f ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0);
                Vector3 result = Vector3.Cross(normal, candidate);
                return result.LengthSquared() > 1e-12f ? Vector3.Normalize(result) : new Vector3(1, 0, 0);
            }

            public Vector4 Get(VertexFormat.Attribute attribute, int vertex)
            {
                switch (attribute.Usage)
                {
                    case VertexFormat.Usage.Position:
                        Vector3 position = Positions[vertex];
                        return new Vector4(position.X / VertexScale, position.Y / VertexScale, position.Z / VertexScale, PositionW == null ? -1.0f : PositionW[vertex]);
                    case VertexFormat.Usage.Normal:
                        if (VertexMap[vertex] < Mesh.Normals.Count)
                        {
                            Assimp.Vector3D normal = Mesh.Normals[VertexMap[vertex]];
                            return new Vector4(normal.X, normal.Y, normal.Z, 0);
                        }
                        return Vector4.Zero;
                    case VertexFormat.Usage.Tangent:
                        if (Tangents != null) return new Vector4(Tangents[vertex], 0);
                        if (VertexMap[vertex] < Mesh.Tangents.Count)
                        {
                            Assimp.Vector3D tangent = Mesh.Tangents[VertexMap[vertex]];
                            return new Vector4(tangent.X, tangent.Y, tangent.Z, 0);
                        }
                        return Vector4.Zero;
                    case VertexFormat.Usage.Binormal:
                        if (Binormals != null) return new Vector4(Binormals[vertex], 0);
                        if (VertexMap[vertex] < Mesh.BiTangents.Count)
                        {
                            Assimp.Vector3D binormal = Mesh.BiTangents[VertexMap[vertex]];
                            return new Vector4(binormal.X, binormal.Y, binormal.Z, 0);
                        }
                        return Vector4.Zero;
                    case VertexFormat.Usage.TexCoord:
                        if (UVChannels.TryGetValue(attribute.Index, out int channel))
                        {
                            Assimp.Vector3D uv = Mesh.TextureCoordinateChannels[channel][VertexMap[vertex]];
                            return new Vector4(uv.X / UVScale, uv.Y / UVScale, 0, 0);
                        }
                        return Vector4.Zero;
                    case VertexFormat.Usage.Color:
                        if (Mesh.VertexColorChannelCount > 0 && VertexMap[vertex] < Mesh.VertexColorChannels[0].Count)
                        {
                            Color4D colour = Mesh.VertexColorChannels[0][VertexMap[vertex]];
                            return new Vector4(colour.R, colour.G, colour.B, colour.A);
                        }
                        return Vector4.One;
                    case VertexFormat.Usage.BlendIndices:
                        if (BlendIndices != null) return new Vector4(BlendIndices[vertex * 4], BlendIndices[(vertex * 4) + 1], BlendIndices[(vertex * 4) + 2], BlendIndices[(vertex * 4) + 3]);
                        return Vector4.Zero;
                    case VertexFormat.Usage.BlendWeight:
                        if (BlendWeights != null) return new Vector4(BlendWeights[vertex * 4] / 255.0f, BlendWeights[(vertex * 4) + 1] / 255.0f, BlendWeights[(vertex * 4) + 2] / 255.0f, BlendWeights[(vertex * 4) + 3] / 255.0f);
                        return Vector4.Zero;
                }
                return Vector4.Zero;
            }

            public bool Has(VertexFormat.Attribute attribute)
            {
                switch (attribute.Usage)
                {
                    case VertexFormat.Usage.Position: return true;
                    case VertexFormat.Usage.Normal: return Mesh.Normals.Count == Mesh.VertexCount;
                    case VertexFormat.Usage.Tangent: return Tangents != null || Mesh.Tangents.Count == Mesh.VertexCount;
                    case VertexFormat.Usage.Binormal: return Binormals != null || Mesh.BiTangents.Count == Mesh.VertexCount;
                    case VertexFormat.Usage.TexCoord: return UVChannels.ContainsKey(attribute.Index);
                    case VertexFormat.Usage.Color: return Mesh.VertexColorChannelCount > 0 && Mesh.VertexColorChannels[0].Count == Mesh.VertexCount;
                    case VertexFormat.Usage.BlendIndices:
                    case VertexFormat.Usage.BlendWeight: return BlendIndices != null && BlendWeights != null;
                }
                return false;
            }
        }

        /* The layout the game uses for static meshes: positions and the first UV in one stream, shading data in the next */
        private static VertexFormat BuildVertexFormat(VertexSource source, bool full)
        {
            VertexFormat format = new VertexFormat();

            List<VertexFormat.Attribute> stream = new List<VertexFormat.Attribute>();
            stream.Add(new VertexFormat.Attribute(VertexFormat.Type.S16_4N, VertexFormat.Usage.Position));
            if (source.UVChannels.ContainsKey(0))
                stream.Add(new VertexFormat.Attribute(VertexFormat.Type.S16_2N, VertexFormat.Usage.TexCoord, 0));
            if (source.BlendIndices != null && source.BlendWeights != null)
            {
                stream.Add(new VertexFormat.Attribute(VertexFormat.Type.U8_4, VertexFormat.Usage.BlendIndices));
                stream.Add(new VertexFormat.Attribute(VertexFormat.Type.U8_4N, VertexFormat.Usage.BlendWeight));
            }
            format.Attributes.Add(stream);

            if (full)
            {
                List<VertexFormat.Attribute> shading = new List<VertexFormat.Attribute>();
                if (source.Mesh.Normals.Count == source.VertexCount)
                    shading.Add(new VertexFormat.Attribute(VertexFormat.Type.FP32_3, VertexFormat.Usage.Normal));
                if (source.Mesh.VertexColorChannelCount > 0 && source.Mesh.VertexColorChannels[0].Count == source.VertexCount)
                    shading.Add(new VertexFormat.Attribute(VertexFormat.Type.Color, VertexFormat.Usage.Color));
                if (source.Mesh.Tangents.Count == source.VertexCount)
                    shading.Add(new VertexFormat.Attribute(VertexFormat.Type.FP32_3, VertexFormat.Usage.Tangent));
                foreach (int index in source.UVChannels.Keys.Where(x => x != 0).OrderBy(x => x))
                    shading.Add(new VertexFormat.Attribute(VertexFormat.Type.S16_2N, VertexFormat.Usage.TexCoord, index));
                if (shading.Count != 0)
                    format.Attributes.Add(shading);
            }

            format.Attributes.Add(new List<VertexFormat.Attribute>() { new VertexFormat.Attribute(VertexFormat.Type.Unused) });
            return format;
        }

        /* Drop anything the mesh can no longer supply, so we never write a stream of zeroes the game will try to shade with */
        private static VertexFormat PruneVertexFormat(VertexFormat format, VertexSource source, List<string> warnings)
        {
            VertexFormat pruned = new VertexFormat();
            for (int i = 0; i < format.Attributes.Count; i++)
            {
                if (i == format.Attributes.Count - 1)
                {
                    pruned.Attributes.Add(format.Attributes[i]);
                    continue;
                }

                List<VertexFormat.Attribute> stream = new List<VertexFormat.Attribute>();
                foreach (VertexFormat.Attribute attribute in format.Attributes[i])
                {
                    if (source.Has(attribute))
                        stream.Add(attribute);
                    else
                        warnings?.Add("the mesh no longer provides " + attribute.Usage + (attribute.Index == 0 ? "" : " " + attribute.Index) + ", so it has been dropped from the vertex format.");
                }
                if (stream.Count != 0)
                    pruned.Attributes.Add(stream);
            }
            return pruned;
        }

        private static byte[] Encode(VertexFormat format, VertexSource source, int[] indices)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                for (int i = 0; i < format.Attributes.Count; i++)
                {
                    if (i == format.Attributes.Count - 1)
                    {
                        for (int x = 0; x < indices.Length; x++)
                            writer.Write((ushort)indices[x]);
                        Utilities.Align(writer, 16);
                        continue;
                    }

                    for (int x = 0; x < source.VertexCount; x++)
                        for (int y = 0; y < format.Attributes[i].Count; y++)
                            Write(writer, source.Get(format.Attributes[i][y], x), format.Attributes[i][y].Type);
                    Utilities.Align(writer, 16);
                }
                return stream.ToArray();
            }
        }

        private static void Write(BinaryWriter writer, Vector4 value, VertexFormat.Type type)
        {
            switch (type)
            {
                case VertexFormat.Type.FP32_1:
                    writer.Write(value.X);
                    break;
                case VertexFormat.Type.FP32_2:
                    writer.Write(value.X); writer.Write(value.Y);
                    break;
                case VertexFormat.Type.FP32_3:
                    writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Z);
                    break;
                case VertexFormat.Type.FP32_4:
                    writer.Write(value.X); writer.Write(value.Y); writer.Write(value.Z); writer.Write(value.W);
                    break;
                case VertexFormat.Type.Color:
                    writer.Write(((uint)ToByte(value.X, 255.0f) << 24) | ((uint)ToByte(value.Y, 255.0f) << 16) | ((uint)ToByte(value.Z, 255.0f) << 8) | ToByte(value.W, 255.0f));
                    break;
                case VertexFormat.Type.U8_4:
                    writer.Write(ToByte(value.X, 1.0f)); writer.Write(ToByte(value.Y, 1.0f)); writer.Write(ToByte(value.Z, 1.0f)); writer.Write(ToByte(value.W, 1.0f));
                    break;
                case VertexFormat.Type.U8_4N:
                    writer.Write(ToByte(value.X, 255.0f)); writer.Write(ToByte(value.Y, 255.0f)); writer.Write(ToByte(value.Z, 255.0f)); writer.Write(ToByte(value.W, 255.0f));
                    break;
                case VertexFormat.Type.S16_2:
                    writer.Write(ToShort(value.X, 1.0f)); writer.Write(ToShort(value.Y, 1.0f));
                    break;
                case VertexFormat.Type.S16_4:
                    writer.Write(ToShort(value.X, 1.0f)); writer.Write(ToShort(value.Y, 1.0f)); writer.Write(ToShort(value.Z, 1.0f)); writer.Write(ToShort(value.W, 1.0f));
                    break;
                case VertexFormat.Type.S16_2N:
                    writer.Write(ToShort(value.X, short.MaxValue)); writer.Write(ToShort(value.Y, short.MaxValue));
                    break;
                case VertexFormat.Type.S16_4N:
                    writer.Write(ToShort(value.X, short.MaxValue)); writer.Write(ToShort(value.Y, short.MaxValue)); writer.Write(ToShort(value.Z, short.MaxValue)); writer.Write(ToShort(value.W, short.MaxValue));
                    break;
                case VertexFormat.Type.U16_2N:
                    writer.Write(ToUShort(value.X)); writer.Write(ToUShort(value.Y));
                    break;
                case VertexFormat.Type.U16_4N:
                    writer.Write(ToUShort(value.X)); writer.Write(ToUShort(value.Y)); writer.Write(ToUShort(value.Z)); writer.Write(ToUShort(value.W));
                    break;
                case VertexFormat.Type.Dec3N:
                    //Declared as DEC3N, but really three bytes biased around 128 with a fourth that's always zero
                    writer.Write(ToBiasedByte(value.X));
                    writer.Write(ToBiasedByte(value.Y));
                    writer.Write(ToBiasedByte(value.Z));
                    writer.Write((byte)0);
                    break;
                default:
                    throw new Exception("Unsupported VertexFormatType: " + type);
            }
        }

        private static byte ToByte(float value, float scale) => (byte)Math.Max(0, Math.Min(255, Math.Round(value * scale)));
        private static short ToShort(float value, float scale) => (short)Math.Max(-short.MaxValue, Math.Min(short.MaxValue, Math.Round(value * scale)));
        private static ushort ToUShort(float value) => (ushort)Math.Max(0, Math.Min(ushort.MaxValue, Math.Round(value * ushort.MaxValue)));
        private static byte ToBiasedByte(float value) => (byte)Math.Max(0, Math.Min(255, Math.Round(value * 127.0f) + 128));

        public static int SizeOf(VertexFormat.Type type)
        {
            switch (type)
            {
                case VertexFormat.Type.FP32_1:
                case VertexFormat.Type.Color:
                case VertexFormat.Type.U8_4:
                case VertexFormat.Type.S16_2:
                case VertexFormat.Type.U8_4N:
                case VertexFormat.Type.S16_2N:
                case VertexFormat.Type.U16_2N:
                case VertexFormat.Type.UDec3:
                case VertexFormat.Type.Dec3N:
                case VertexFormat.Type.FP16_2:
                    return 4;
                case VertexFormat.Type.FP32_2:
                case VertexFormat.Type.S16_4:
                case VertexFormat.Type.S16_4N:
                case VertexFormat.Type.U16_4N:
                case VertexFormat.Type.FP16_4:
                    return 8;
                case VertexFormat.Type.FP32_3:
                    return 12;
                case VertexFormat.Type.FP32_4:
                    return 16;
            }
            return 0;
        }

        #endregion

        #region HELPERS

        private static Matrix4x4 ToNumerics(Assimp.Matrix4x4 matrix)
        {
            //Assimp matrices are row-vector-on-the-right, System.Numerics are the transpose of that
            return new Matrix4x4(
                matrix.A1, matrix.B1, matrix.C1, matrix.D1,
                matrix.A2, matrix.B2, matrix.C2, matrix.D2,
                matrix.A3, matrix.B3, matrix.C3, matrix.D3,
                matrix.A4, matrix.B4, matrix.C4, matrix.D4);
        }

        private static Assimp.Vector3D ToAssimp(Vector3 value)
        {
            return new Assimp.Vector3D(value.X, value.Y, value.Z);
        }

        private static Assimp.Quaternion ToAssimp(System.Numerics.Quaternion value)
        {
            return new Assimp.Quaternion(value.W, value.X, value.Y, value.Z);
        }

        private static Assimp.Matrix4x4 ToAssimp(Matrix4x4 matrix)
        {
            return new Assimp.Matrix4x4(
                matrix.M11, matrix.M21, matrix.M31, matrix.M41,
                matrix.M12, matrix.M22, matrix.M32, matrix.M42,
                matrix.M13, matrix.M23, matrix.M33, matrix.M43,
                matrix.M14, matrix.M24, matrix.M34, matrix.M44);
        }

        private static byte[] ToBytes(short[] values)
        {
            byte[] bytes = new byte[values.Length * 2];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        private static short[] FromBytes(byte[] bytes)
        {
            short[] values = new short[bytes.Length / 2];
            Buffer.BlockCopy(bytes, 0, values, 0, values.Length * 2);
            return values;
        }

        private static byte[] ToBytes(List<Vector4> values, float scale)
        {
            byte[] bytes = new byte[values.Count * 4];
            for (int i = 0; i < values.Count; i++)
            {
                bytes[i * 4] = ToByte(values[i].X, scale);
                bytes[(i * 4) + 1] = ToByte(values[i].Y, scale);
                bytes[(i * 4) + 2] = ToByte(values[i].Z, scale);
                bytes[(i * 4) + 3] = ToByte(values[i].W, scale);
            }
            return bytes;
        }

        #endregion
    }
}
