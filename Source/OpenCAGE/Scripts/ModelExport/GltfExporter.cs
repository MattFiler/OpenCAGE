using Assimp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenCAGE.ModelExport
{
    /// <summary>
    /// Writes an assimp scene as glTF 2.0, either as .gltf with a sidecar buffer or packed into a
    /// single .glb.
    ///
    /// assimp's own glTF exporter can't be used for anything animated: it splits every channel into
    /// an animation of its own, loses the names, and takes the process down with it when a skinned
    /// mesh and an animation are in the same scene.
    /// </summary>
    public static class GltfExporter
    {
        private const int Byte = 5120, UnsignedByte = 5121, Short = 5122, UnsignedShort = 5123, UnsignedInt = 5125, Float = 5126;
        private const int ArrayBuffer = 34962, ElementArrayBuffer = 34963;

        public static void Export(Scene scene, string path, bool binary)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            new Document(scene).Write(path, binary);
        }

        private class Document
        {
            private readonly Scene _scene;
            private readonly MemoryStream _buffer = new MemoryStream();

            private readonly JArray _bufferViews = new JArray();
            private readonly JArray _accessors = new JArray();
            private readonly JArray _nodes = new JArray();
            private readonly JArray _meshes = new JArray();
            private readonly JArray _skins = new JArray();
            private readonly JArray _materials = new JArray();
            private readonly JArray _animations = new JArray();
            private readonly JArray _images = new JArray();
            private readonly JArray _textures = new JArray();

            //assimp node name to the index it was written at, so animations and skins can point at it
            private readonly Dictionary<string, int> _nodeIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _imageIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            public Document(Scene scene) { _scene = scene; }

            //nodes an animation drives, gathered before the tree is written because it changes how they are written
            private readonly HashSet<string> _animated = new HashSet<string>(StringComparer.Ordinal);

            public void Write(string path, bool binary)
            {
                _directory = Path.GetDirectoryName(Path.GetFullPath(path));

                foreach (Assimp.Animation animation in _scene.Animations)
                    foreach (NodeAnimationChannel channel in animation.NodeAnimationChannels)
                        _animated.Add(channel.NodeName);

                BuildMaterials();
                foreach (Mesh mesh in _scene.Meshes) BuildMesh(mesh);
                int root = BuildNode(_scene.RootNode);
                BuildSkins();
                BuildAnimations();

                JObject gltf = new JObject
                {
                    ["asset"] = new JObject { ["version"] = "2.0", ["generator"] = "OpenCAGE" },
                    ["scene"] = 0,
                    ["scenes"] = new JArray { new JObject { ["nodes"] = new JArray { root } } },
                    ["nodes"] = _nodes,
                    ["meshes"] = _meshes,
                    ["accessors"] = _accessors,
                    ["bufferViews"] = _bufferViews,
                };
                if (_materials.Count != 0) gltf["materials"] = _materials;
                if (_skins.Count != 0) gltf["skins"] = _skins;
                if (_animations.Count != 0) gltf["animations"] = _animations;
                if (_images.Count != 0)
                {
                    gltf["images"] = _images;
                    gltf["textures"] = _textures;
                    gltf["samplers"] = new JArray { new JObject { ["wrapS"] = 10497, ["wrapT"] = 10497 } };
                }

                byte[] data = _buffer.ToArray();
                if (binary) WriteBinary(path, gltf, data);
                else WriteText(path, gltf, data);
            }

            private void WriteText(string path, JObject gltf, byte[] data)
            {
                string bufferName = Path.GetFileNameWithoutExtension(path) + ".bin";
                gltf["buffers"] = new JArray { new JObject { ["byteLength"] = data.Length, ["uri"] = Uri.EscapeDataString(bufferName) } };

                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(path) ?? "", bufferName), data);
                File.WriteAllText(path, gltf.ToString(Formatting.None), new UTF8Encoding(false));
            }

            /* A .glb is the same JSON and the same buffer, wrapped in a twelve byte header and one
             * chunk header each. Both chunks are padded to four bytes - the JSON with spaces so it
             * stays parseable, the buffer with zeroes. */
            private void WriteBinary(string path, JObject gltf, byte[] data)
            {
                gltf["buffers"] = new JArray { new JObject { ["byteLength"] = data.Length } };

                byte[] json = new UTF8Encoding(false).GetBytes(gltf.ToString(Formatting.None));
                int jsonPadding = (4 - (json.Length % 4)) % 4;
                int dataPadding = (4 - (data.Length % 4)) % 4;

                using (FileStream file = File.Create(path))
                using (BinaryWriter writer = new BinaryWriter(file))
                {
                    int total = 12 + 8 + json.Length + jsonPadding + (data.Length == 0 ? 0 : 8 + data.Length + dataPadding);
                    writer.Write(0x46546C67);  //"glTF"
                    writer.Write(2);
                    writer.Write(total);

                    writer.Write(json.Length + jsonPadding);
                    writer.Write(0x4E4F534A);  //"JSON"
                    writer.Write(json);
                    for (int i = 0; i < jsonPadding; i++) writer.Write((byte)' ');

                    if (data.Length == 0) return;
                    writer.Write(data.Length + dataPadding);
                    writer.Write(0x004E4942);  //"BIN"
                    writer.Write(data);
                    for (int i = 0; i < dataPadding; i++) writer.Write((byte)0);
                }
            }

            #region BUFFER
            private int AddView(byte[] data, int? target)
            {
                //accessors read straight out of the buffer, so every view has to start 4 byte aligned
                while (_buffer.Length % 4 != 0) _buffer.WriteByte(0);

                int offset = (int)_buffer.Length;
                _buffer.Write(data, 0, data.Length);

                JObject view = new JObject { ["buffer"] = 0, ["byteOffset"] = offset, ["byteLength"] = data.Length };
                if (target.HasValue) view["target"] = target.Value;
                _bufferViews.Add(view);
                return _bufferViews.Count - 1;
            }

            private int AddAccessor(byte[] data, int componentType, int count, string type, int? target,
                                    IEnumerable<float> minimum = null, IEnumerable<float> maximum = null)
            {
                JObject accessor = new JObject
                {
                    ["bufferView"] = AddView(data, target),
                    ["componentType"] = componentType,
                    ["count"] = count,
                    ["type"] = type,
                };
                if (minimum != null) accessor["min"] = new JArray(minimum.Cast<object>().ToArray());
                if (maximum != null) accessor["max"] = new JArray(maximum.Cast<object>().ToArray());
                _accessors.Add(accessor);
                return _accessors.Count - 1;
            }

            private int AddFloats(IReadOnlyList<float> values, int components, string type, int? target, bool bounds = false)
            {
                byte[] data = new byte[values.Count * 4];
                Buffer.BlockCopy(values.ToArray(), 0, data, 0, data.Length);

                float[] minimum = null, maximum = null;
                if (bounds && values.Count != 0)
                {
                    minimum = new float[components];
                    maximum = new float[components];
                    for (int c = 0; c < components; c++) { minimum[c] = float.MaxValue; maximum[c] = float.MinValue; }
                    for (int i = 0; i < values.Count; i++)
                    {
                        int c = i % components;
                        if (values[i] < minimum[c]) minimum[c] = values[i];
                        if (values[i] > maximum[c]) maximum[c] = values[i];
                    }
                }
                return AddAccessor(data, Float, values.Count / components, type, target, minimum, maximum);
            }
            #endregion

            #region MESHES
            private void BuildMesh(Mesh mesh)
            {
                JObject attributes = new JObject();

                List<float> positions = new List<float>(mesh.VertexCount * 3);
                foreach (Vector3D v in mesh.Vertices) { positions.Add(v.X); positions.Add(v.Y); positions.Add(v.Z); }
                attributes["POSITION"] = AddFloats(positions, 3, "VEC3", ArrayBuffer, true);

                if (mesh.HasNormals)
                {
                    List<float> normals = new List<float>(mesh.VertexCount * 3);
                    foreach (Vector3D v in mesh.Normals) { normals.Add(v.X); normals.Add(v.Y); normals.Add(v.Z); }
                    attributes["NORMAL"] = AddFloats(normals, 3, "VEC3", ArrayBuffer);
                }

                /* glTF wants a four component tangent, the last one saying which way the bitangent
                 * points. Work it out from the pair the scene already carries. */
                if (mesh.HasTangentBasis)
                {
                    List<float> tangents = new List<float>(mesh.VertexCount * 4);
                    for (int i = 0; i < mesh.VertexCount; i++)
                    {
                        Vector3D n = mesh.HasNormals ? mesh.Normals[i] : new Vector3D(0, 1, 0);
                        Vector3D t = mesh.Tangents[i], b = mesh.BiTangents[i];
                        Vector3D cross = new Vector3D(
                            (n.Y * t.Z) - (n.Z * t.Y),
                            (n.Z * t.X) - (n.X * t.Z),
                            (n.X * t.Y) - (n.Y * t.X));
                        float handedness = ((cross.X * b.X) + (cross.Y * b.Y) + (cross.Z * b.Z)) < 0 ? -1.0f : 1.0f;
                        tangents.Add(t.X); tangents.Add(t.Y); tangents.Add(t.Z); tangents.Add(handedness);
                    }
                    attributes["TANGENT"] = AddFloats(tangents, 4, "VEC4", ArrayBuffer);
                }

                for (int channel = 0; channel < mesh.TextureCoordinateChannelCount; channel++)
                {
                    if (!mesh.HasTextureCoords(channel)) continue;
                    List<float> uvs = new List<float>(mesh.VertexCount * 2);
                    foreach (Vector3D uv in mesh.TextureCoordinateChannels[channel]) { uvs.Add(uv.X); uvs.Add(uv.Y); }
                    attributes["TEXCOORD_" + channel] = AddFloats(uvs, 2, "VEC2", ArrayBuffer);
                }

                if (mesh.HasVertexColors(0))
                {
                    List<float> colours = new List<float>(mesh.VertexCount * 4);
                    foreach (Color4D c in mesh.VertexColorChannels[0]) { colours.Add(c.R); colours.Add(c.G); colours.Add(c.B); colours.Add(c.A); }
                    attributes["COLOR_0"] = AddFloats(colours, 4, "VEC4", ArrayBuffer);
                }

                if (mesh.HasBones) BuildSkinAttributes(mesh, attributes);

                JObject primitive = new JObject
                {
                    ["attributes"] = attributes,
                    ["indices"] = BuildIndices(mesh),
                    ["mode"] = 4,
                };
                if (mesh.MaterialIndex >= 0 && mesh.MaterialIndex < _materials.Count) primitive["material"] = mesh.MaterialIndex;

                _meshes.Add(new JObject
                {
                    ["name"] = mesh.Name,
                    ["primitives"] = new JArray { primitive },
                });
            }

            private int BuildIndices(Mesh mesh)
            {
                List<int> indices = new List<int>(mesh.FaceCount * 3);
                foreach (Face face in mesh.Faces)
                    for (int i = 0; i < face.IndexCount; i++) indices.Add(face.Indices[i]);

                //a mesh that fits in 16 bits is half the size and every reader takes it
                if (mesh.VertexCount <= ushort.MaxValue)
                {
                    byte[] data = new byte[indices.Count * 2];
                    for (int i = 0; i < indices.Count; i++)
                    {
                        data[(i * 2) + 0] = (byte)(indices[i] & 0xFF);
                        data[(i * 2) + 1] = (byte)((indices[i] >> 8) & 0xFF);
                    }
                    return AddAccessor(data, UnsignedShort, indices.Count, "SCALAR", ElementArrayBuffer);
                }

                byte[] wide = new byte[indices.Count * 4];
                Buffer.BlockCopy(indices.ToArray(), 0, wide, 0, wide.Length);
                return AddAccessor(wide, UnsignedInt, indices.Count, "SCALAR", ElementArrayBuffer);
            }

            /* glTF holds four joints and four weights per vertex, with the joints numbered within the
             * mesh's own skin rather than the scene. Invert the scene's bone-to-vertices lists. */
            private readonly Dictionary<int, List<string>> _meshJoints = new Dictionary<int, List<string>>();

            private void BuildSkinAttributes(Mesh mesh, JObject attributes)
            {
                List<string> joints = mesh.Bones.Select(x => x.Name).ToList();
                _meshJoints[_meshes.Count] = joints;

                ushort[] indices = new ushort[mesh.VertexCount * 4];
                float[] weights = new float[mesh.VertexCount * 4];
                int[] slots = new int[mesh.VertexCount];

                for (int bone = 0; bone < mesh.Bones.Count; bone++)
                {
                    foreach (VertexWeight weight in mesh.Bones[bone].VertexWeights)
                    {
                        int vertex = weight.VertexID;
                        if (vertex < 0 || vertex >= mesh.VertexCount || slots[vertex] >= 4) continue;
                        int slot = (vertex * 4) + slots[vertex];
                        indices[slot] = (ushort)bone;
                        weights[slot] = weight.Weight;
                        slots[vertex]++;
                    }
                }

                byte[] jointData = new byte[indices.Length * 2];
                Buffer.BlockCopy(indices, 0, jointData, 0, jointData.Length);
                attributes["JOINTS_0"] = AddAccessor(jointData, UnsignedShort, mesh.VertexCount, "VEC4", ArrayBuffer);
                attributes["WEIGHTS_0"] = AddFloats(weights, 4, "VEC4", ArrayBuffer);
            }

            private void BuildSkins()
            {
                for (int meshIndex = 0; meshIndex < _scene.MeshCount; meshIndex++)
                {
                    Mesh mesh = _scene.Meshes[meshIndex];
                    if (!mesh.HasBones || !_meshJoints.TryGetValue(meshIndex, out List<string> joints)) continue;

                    List<float> inverseBind = new List<float>(joints.Count * 16);
                    JArray jointNodes = new JArray();
                    foreach (Bone bone in mesh.Bones)
                    {
                        jointNodes.Add(_nodeIndex.TryGetValue(bone.Name, out int node) ? node : 0);
                        AppendMatrix(inverseBind, bone.OffsetMatrix);
                    }

                    _skins.Add(new JObject
                    {
                        ["joints"] = jointNodes,
                        ["inverseBindMatrices"] = AddFloats(inverseBind, 16, "MAT4", null),
                    });

                    //the node that draws this mesh is the one that has to name the skin
                    if (_meshToNode.TryGetValue(meshIndex, out int owner))
                        ((JObject)_nodes[owner])["skin"] = _skins.Count - 1;
                }
            }

            //glTF matrices are column major, the transpose of how assimp holds them
            private static void AppendMatrix(List<float> into, Assimp.Matrix4x4 m)
            {
                into.Add(m.A1); into.Add(m.B1); into.Add(m.C1); into.Add(m.D1);
                into.Add(m.A2); into.Add(m.B2); into.Add(m.C2); into.Add(m.D2);
                into.Add(m.A3); into.Add(m.B3); into.Add(m.C3); into.Add(m.D3);
                into.Add(m.A4); into.Add(m.B4); into.Add(m.C4); into.Add(m.D4);
            }
            #endregion

            #region NODES
            private readonly Dictionary<int, int> _meshToNode = new Dictionary<int, int>();

            /* Assimp's own IsIdentity allows a tenth of a unit either way, which is nothing in
             * centimetres and several millimetres in metres - enough to lose the small offsets on a
             * character's eye bones. Ask the question exactly instead. */
            private static bool IsExactlyIdentity(Assimp.Matrix4x4 m)
            {
                return m.A1 == 1 && m.A2 == 0 && m.A3 == 0 && m.A4 == 0
                    && m.B1 == 0 && m.B2 == 1 && m.B3 == 0 && m.B4 == 0
                    && m.C1 == 0 && m.C2 == 0 && m.C3 == 1 && m.C4 == 0
                    && m.D1 == 0 && m.D2 == 0 && m.D3 == 0 && m.D4 == 1;
            }

            private int BuildNode(Node node)
            {
                JObject entry = new JObject { ["name"] = node.Name };
                _nodes.Add(entry);
                int index = _nodes.Count - 1;
                _nodeIndex[node.Name] = index;

                /* A node an animation drives has to say where it is as translation, rotation and
                 * scale - glTF forbids a matrix on one, because a channel replaces one of the three
                 * and there would be nothing to replace. Everything else keeps its matrix, which is
                 * exact even where the transform mirrors. */
                if (_animated.Contains(node.Name))
                {
                    node.Transform.Decompose(out Vector3D scale, out Assimp.Quaternion rotation, out Vector3D translation);
                    if (translation.X != 0 || translation.Y != 0 || translation.Z != 0)
                        entry["translation"] = new JArray(translation.X, translation.Y, translation.Z);
                    if (rotation.X != 0 || rotation.Y != 0 || rotation.Z != 0 || rotation.W != 1)
                        entry["rotation"] = new JArray(rotation.X, rotation.Y, rotation.Z, rotation.W);
                    if (scale.X != 1 || scale.Y != 1 || scale.Z != 1)
                        entry["scale"] = new JArray(scale.X, scale.Y, scale.Z);
                }
                else if (!IsExactlyIdentity(node.Transform))
                {
                    List<float> matrix = new List<float>(16);
                    AppendMatrix(matrix, node.Transform);
                    entry["matrix"] = new JArray(matrix.Cast<object>().ToArray());
                }

                if (node.MeshIndices.Count != 0)
                {
                    //glTF gives a node one mesh, so a node carrying several needs a child for each
                    if (node.MeshIndices.Count == 1)
                    {
                        entry["mesh"] = node.MeshIndices[0];
                        _meshToNode[node.MeshIndices[0]] = index;
                    }
                    else
                    {
                        JArray children = new JArray();
                        foreach (int mesh in node.MeshIndices)
                        {
                            JObject holder = new JObject { ["name"] = node.Name + "_" + mesh, ["mesh"] = mesh };
                            _nodes.Add(holder);
                            _meshToNode[mesh] = _nodes.Count - 1;
                            children.Add(_nodes.Count - 1);
                        }
                        entry["children"] = children;
                    }
                }

                if (node.ChildCount != 0)
                {
                    JArray children = entry["children"] as JArray ?? new JArray();
                    foreach (Node child in node.Children) children.Add(BuildNode(child));
                    entry["children"] = children;
                }
                return index;
            }
            #endregion

            #region MATERIALS
            private void BuildMaterials()
            {
                foreach (Material source in _scene.Materials)
                {
                    JObject pbr = new JObject
                    {
                        ["metallicFactor"] = 0.0,
                        ["roughnessFactor"] = 0.8,
                    };
                    if (source.HasColorDiffuse)
                        pbr["baseColorFactor"] = new JArray(source.ColorDiffuse.R, source.ColorDiffuse.G, source.ColorDiffuse.B, source.ColorDiffuse.A);

                    int diffuse = AddTexture(source, TextureType.Diffuse);
                    if (diffuse >= 0) pbr["baseColorTexture"] = new JObject { ["index"] = diffuse };

                    JObject material = new JObject
                    {
                        ["name"] = string.IsNullOrEmpty(source.Name) ? "material" : source.Name,
                        ["pbrMetallicRoughness"] = pbr,
                        ["doubleSided"] = true,
                    };

                    int normal = AddTexture(source, TextureType.Normals);
                    if (normal < 0) normal = AddTexture(source, TextureType.Height);
                    if (normal >= 0) material["normalTexture"] = new JObject { ["index"] = normal };

                    _materials.Add(material);
                }
            }

            private int AddTexture(Material material, TextureType type)
            {
                if (!material.GetMaterialTexture(type, 0, out TextureSlot slot) || string.IsNullOrEmpty(slot.FilePath)) return -1;

                if (!_imageIndex.TryGetValue(slot.FilePath, out int image))
                {
                    _images.Add(new JObject { ["uri"] = RelativeUri(slot.FilePath) });
                    image = _images.Count - 1;
                    _imageIndex[slot.FilePath] = image;

                    _textures.Add(new JObject { ["sampler"] = 0, ["source"] = image });
                }
                return _textures.Count - 1;
            }

            /* A glTF names its images relative to itself, and each path segment is escaped on its
             * own so the separators survive. Anything outside the model's own folder is left as an
             * absolute path, which readers accept even though it only works on this machine. */
            private string RelativeUri(string path)
            {
                string full = Path.GetFullPath(path).Replace('\\', '/');
                string root = (_directory ?? "").Replace('\\', '/').TrimEnd('/') + "/";

                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) full = full.Substring(root.Length);
                return string.Join("/", full.Split('/').Select(Uri.EscapeDataString));
            }

            private string _directory;
            #endregion

            #region ANIMATION
            private void BuildAnimations()
            {
                foreach (Assimp.Animation source in _scene.Animations)
                {
                    double ticksPerSecond = source.TicksPerSecond > 0 ? source.TicksPerSecond : 30.0;

                    JArray samplers = new JArray();
                    JArray channels = new JArray();

                    foreach (NodeAnimationChannel channel in source.NodeAnimationChannels)
                    {
                        if (!_nodeIndex.TryGetValue(channel.NodeName, out int node)) continue;

                        if (channel.HasPositionKeys)
                            AddChannel(samplers, channels, node, "translation",
                                channel.PositionKeys.Select(x => (float)(x.Time / ticksPerSecond)).ToList(),
                                channel.PositionKeys.SelectMany(x => new[] { x.Value.X, x.Value.Y, x.Value.Z }).ToList(), 3, "VEC3");

                        if (channel.HasRotationKeys)
                            AddChannel(samplers, channels, node, "rotation",
                                channel.RotationKeys.Select(x => (float)(x.Time / ticksPerSecond)).ToList(),
                                channel.RotationKeys.SelectMany(x => new[] { x.Value.X, x.Value.Y, x.Value.Z, x.Value.W }).ToList(), 4, "VEC4");

                        if (channel.HasScalingKeys)
                            AddChannel(samplers, channels, node, "scale",
                                channel.ScalingKeys.Select(x => (float)(x.Time / ticksPerSecond)).ToList(),
                                channel.ScalingKeys.SelectMany(x => new[] { x.Value.X, x.Value.Y, x.Value.Z }).ToList(), 3, "VEC3");
                    }

                    if (channels.Count == 0) continue;
                    _animations.Add(new JObject
                    {
                        ["name"] = source.Name,
                        ["samplers"] = samplers,
                        ["channels"] = channels,
                    });
                }
            }

            private void AddChannel(JArray samplers, JArray channels, int node, string path,
                                    List<float> times, List<float> values, int components, string type)
            {
                if (times.Count == 0) return;

                //the time accessor needs bounds; readers use them to work out how long the clip runs
                int input = AddFloats(times, 1, "SCALAR", null, true);
                int output = AddFloats(values, components, type, null);

                samplers.Add(new JObject { ["input"] = input, ["output"] = output, ["interpolation"] = "LINEAR" });
                channels.Add(new JObject
                {
                    ["sampler"] = samplers.Count - 1,
                    ["target"] = new JObject { ["node"] = node, ["path"] = path },
                });
            }
            #endregion
        }
    }
}
