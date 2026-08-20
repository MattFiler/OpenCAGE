using Assimp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenCAGE.ModelExport
{
    /// <summary>
    /// Writes an assimp scene out as binary FBX 7.4, complete with skinning, a bind pose and every
    /// keyframe of every animation.
    ///
    /// It exists because assimp's own FBX exporter resamples animation: a 227 frame clip comes back
    /// out as eight poses. Everything else about assimp's scene handling is fine, so the scene it
    /// builds is still the input here - only the writing is ours.
    /// </summary>
    public static class FbxExporter
    {
        /// <summary>FBX counts time in these units per second, and has done since 6.x.</summary>
        private const long TimeUnitsPerSecond = 46186158000L;

        public static void Export(Scene scene, string path)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            new Document(scene, Path.GetFileNameWithoutExtension(path)).Write(path);
        }

        /* One export in progress. Objects need ids before they can be connected, and connections
         * outnumber objects, so both are collected as we walk and written at the end. */
        private class Document
        {
            private readonly Scene _scene;
            private readonly string _name;

            private long _nextId = 1000000;
            private readonly List<FbxNode> _objects = new List<FbxNode>();
            private readonly List<FbxNode> _connections = new List<FbxNode>();
            private readonly Dictionary<string, int> _objectCounts = new Dictionary<string, int>();

            //every node in the assimp tree, by name, and the FBX Model id standing in for it
            private readonly Dictionary<string, long> _models = new Dictionary<string, long>(StringComparer.Ordinal);
            private readonly List<KeyValuePair<Node, long>> _bones = new List<KeyValuePair<Node, long>>();
            private readonly Dictionary<int, long> _materials = new Dictionary<int, long>();

            public Document(Scene scene, string name)
            {
                _scene = scene;
                _name = string.IsNullOrEmpty(name) ? "scene" : name;
            }

            private long NextId() { return _nextId++; }

            private FbxNode Object(string type, long id, string name, string subType)
            {
                /* An object's name and its class travel in one string. The binary form stores the two
                 * halves back to front with a 0x00 0x01 pair between them, which is what readers
                 * look for when they split it again. */
                FbxNode node = new FbxNode(type, id, name + "\0\u0001" + type, subType);
                node.ForceScope = true;
                _objects.Add(node);
                _objectCounts[type] = (_objectCounts.TryGetValue(type, out int count) ? count : 0) + 1;
                return node;
            }

            private void Connect(long child, long parent)
            {
                _connections.Add(new FbxNode("C", "OO", child, parent));
            }

            private void Connect(long child, long parent, string property)
            {
                _connections.Add(new FbxNode("C", "OP", child, parent, property));
            }

            public void Write(string path)
            {
                BuildMaterials();
                long root = BuildNode(_scene.RootNode, 0);
                BuildSkins();
                BuildBindPose();
                BuildAnimations();

                List<FbxNode> document = new List<FbxNode>();
                document.Add(Header());
                document.Add(GlobalSettings());
                document.Add(Documents());
                document.Add(new FbxNode("References"));
                document.Add(Definitions());

                FbxNode objects = new FbxNode("Objects");
                foreach (FbxNode entry in _objects) objects.Add(entry);
                document.Add(objects);

                FbxNode connections = new FbxNode("Connections");
                foreach (FbxNode entry in _connections) connections.Add(entry);
                document.Add(connections);

                /* Takes has to carry "Current" as a child rather than as its own properties. Beyond
                 * being the correct shape, it means the file ends on a closing bracket - a reader
                 * that runs out of tokens straight after a value has no way to know the element
                 * finished, and errors instead. */
                FbxNode takes = new FbxNode("Takes");
                takes.Add("Current", "");
                document.Add(takes);

                FbxBinary.Write(path, document);
                GC.KeepAlive(root);
            }

            #region STRUCTURE
            private FbxNode Header()
            {
                FbxNode header = new FbxNode("FBXHeaderExtension");
                header.Add("FBXHeaderVersion", 1003);
                header.Add("FBXVersion", 7400);
                header.Add("EncryptionType", 0);

                DateTime now = DateTime.Now;
                FbxNode stamp = header.Add("CreationTimeStamp");
                stamp.Add("Version", 1000);
                stamp.Add("Year", now.Year);
                stamp.Add("Month", now.Month);
                stamp.Add("Day", now.Day);
                stamp.Add("Hour", now.Hour);
                stamp.Add("Minute", now.Minute);
                stamp.Add("Second", now.Second);
                stamp.Add("Millisecond", now.Millisecond);

                header.Add("Creator", "OpenCAGE");
                return header;
            }

            private FbxNode GlobalSettings()
            {
                FbxNode settings = new FbxNode("GlobalSettings");
                settings.Add("Version", 1000);
                FbxNode properties = settings.Add("Properties70");

                /* Y up, Z forward, X right - the axes the scene is already built in. FBX spells an
                 * axis as an index into (X, Y, Z) plus a sign. */
                properties.Property("UpAxis", "int", "Integer", "", 1);
                properties.Property("UpAxisSign", "int", "Integer", "", 1);
                properties.Property("FrontAxis", "int", "Integer", "", 2);
                properties.Property("FrontAxisSign", "int", "Integer", "", 1);
                properties.Property("CoordAxis", "int", "Integer", "", 0);
                properties.Property("CoordAxisSign", "int", "Integer", "", 1);
                properties.Property("OriginalUpAxis", "int", "Integer", "", 1);
                properties.Property("OriginalUpAxisSign", "int", "Integer", "", 1);

                //the scene is written in centimetres, which is what a unit means to every FBX reader
                properties.Property("UnitScaleFactor", "double", "Number", "", 1.0);
                properties.Property("OriginalUnitScaleFactor", "double", "Number", "", 1.0);
                properties.Property("AmbientColor", "ColorRGB", "Color", "", 0.0, 0.0, 0.0);
                properties.Property("DefaultCamera", "KString", "", "", "Producer Perspective");

                /* Time mode 11 is "custom", which lets the frame rate be whatever the clip was
                 * authored at rather than rounding to one of FBX's fixed rates. */
                properties.Property("TimeMode", "enum", "", "", 11);
                properties.Property("TimeSpanStart", "KTime", "Time", "", 0L);
                properties.Property("TimeSpanStop", "KTime", "Time", "", TimeUnitsPerSecond);
                properties.Property("CustomFrameRate", "double", "Number", "", FrameRate());
                return settings;
            }

            private double FrameRate()
            {
                Assimp.Animation first = _scene.Animations?.FirstOrDefault();
                double rate = first == null ? 30.0 : first.TicksPerSecond;
                return rate > 0 ? rate : 30.0;
            }

            private FbxNode Documents()
            {
                FbxNode documents = new FbxNode("Documents");
                documents.Add("Count", 1);

                FbxNode document = documents.Add("Document", NextId(), "", "Scene");
                FbxNode properties = document.Add("Properties70");
                properties.Property("SourceObject", "object", "", "");
                properties.Property("ActiveAnimStackName", "KString", "", "", "");
                document.Add("RootNode", 0L);
                return documents;
            }

            private FbxNode Definitions()
            {
                FbxNode definitions = new FbxNode("Definitions");
                definitions.Add("Version", 100);
                definitions.Add("Count", _objects.Count);

                definitions.Add("ObjectType", "GlobalSettings").Add("Count", 1);
                foreach (KeyValuePair<string, int> type in _objectCounts)
                    definitions.Add("ObjectType", type.Key).Add("Count", type.Value);
                return definitions;
            }
            #endregion

            #region NODES
            /* Mirror the assimp node tree as FBX Models. A node carrying a mesh gets a Geometry of
             * its own; a node under the skeleton becomes a LimbNode so tools read it as a bone. */
            private long BuildNode(Node node, long parent)
            {
                bool isBone = ModelIOBoneName(node) >= 0;
                long id = NextId();
                _models[node.Name] = id;

                FbxNode model = Object("Model", id, node.Name, isBone ? "LimbNode" : node.MeshIndices.Count != 0 ? "Mesh" : "Null");
                model.Add("Version", 232);
                WriteTransform(model.Add("Properties70"), node.Transform);
                model.Add("Shading", true);
                model.Add("Culling", "CullingOff");
                Connect(id, parent);

                if (isBone)
                {
                    long attribute = NextId();
                    FbxNode limb = Object("NodeAttribute", attribute, node.Name, "LimbNode");
                    limb.Add("Properties70").Property("Size", "double", "Number", "", 1.0);
                    limb.Add("TypeFlags", "Skeleton");
                    Connect(attribute, id);
                    _bones.Add(new KeyValuePair<Node, long>(node, id));
                }
                else if (node.MeshIndices.Count != 0)
                {
                    //assimp splits a mesh per material, so a node can legitimately carry several
                    foreach (int meshIndex in node.MeshIndices)
                        BuildGeometry(_scene.Meshes[meshIndex], meshIndex, id);
                }

                foreach (Node child in node.Children) BuildNode(child, id);
                return id;
            }

            private static int ModelIOBoneName(Node node)
            {
                return AlienPAK.ModelIO.TryParseBoneName(node.Name, out int bone) ? bone : -1;
            }

            /* FBX describes a node's transform as translation, Euler rotation in degrees and scale,
             * so anything the scene holds as a matrix has to come apart into those three. */
            private void WriteTransform(FbxNode properties, Assimp.Matrix4x4 transform)
            {
                Decompose(transform, out Vector3D translation, out Vector3D rotation, out Vector3D scale);

                properties.Property("InheritType", "enum", "", "", 1);
                properties.Property("DefaultAttributeIndex", "int", "Integer", "", 0);
                if (translation.X != 0 || translation.Y != 0 || translation.Z != 0)
                    properties.Property("Lcl Translation", "Lcl Translation", "", "A", (double)translation.X, (double)translation.Y, (double)translation.Z);
                if (rotation.X != 0 || rotation.Y != 0 || rotation.Z != 0)
                    properties.Property("Lcl Rotation", "Lcl Rotation", "", "A", (double)rotation.X, (double)rotation.Y, (double)rotation.Z);
                if (scale.X != 1 || scale.Y != 1 || scale.Z != 1)
                    properties.Property("Lcl Scaling", "Lcl Scaling", "", "A", (double)scale.X, (double)scale.Y, (double)scale.Z);
            }
            #endregion

            #region GEOMETRY
            private void BuildGeometry(Mesh mesh, int meshIndex, long modelId)
            {
                long id = NextId();
                FbxNode geometry = Object("Geometry", id, mesh.Name, "Mesh");
                _geometryOf[meshIndex] = id;
                Connect(id, modelId);

                geometry.Add("GeometryVersion", 124);

                double[] positions = new double[mesh.VertexCount * 3];
                for (int i = 0; i < mesh.VertexCount; i++)
                {
                    positions[(i * 3) + 0] = mesh.Vertices[i].X;
                    positions[(i * 3) + 1] = mesh.Vertices[i].Y;
                    positions[(i * 3) + 2] = mesh.Vertices[i].Z;
                }
                geometry.Add("Vertices", positions);

                /* FBX has no separate triangle count - a polygon ends where an index goes negative,
                 * encoded as the bitwise complement so the value is still recoverable. */
                List<int> polygons = new List<int>(mesh.FaceCount * 3);
                foreach (Face face in mesh.Faces)
                {
                    for (int i = 0; i < face.IndexCount; i++)
                    {
                        int index = face.Indices[i];
                        polygons.Add(i == face.IndexCount - 1 ? ~index : index);
                    }
                }
                geometry.Add("PolygonVertexIndex", polygons.ToArray());

                List<string> layers = new List<string>();
                if (mesh.HasNormals) { WriteVectorLayer(geometry, "LayerElementNormal", "Normals", mesh.Normals); layers.Add("LayerElementNormal"); }
                if (mesh.HasTangentBasis)
                {
                    WriteVectorLayer(geometry, "LayerElementTangent", "Tangents", mesh.Tangents);
                    WriteVectorLayer(geometry, "LayerElementBinormal", "Binormals", mesh.BiTangents);
                    layers.Add("LayerElementTangent");
                    layers.Add("LayerElementBinormal");
                }

                int uvLayers = 0;
                for (int channel = 0; channel < mesh.TextureCoordinateChannelCount; channel++)
                {
                    if (!mesh.HasTextureCoords(channel)) continue;
                    WriteUVLayer(geometry, mesh, channel, uvLayers, polygons);
                    uvLayers++;
                }
                if (uvLayers != 0) layers.Add("LayerElementUV");

                if (mesh.HasVertexColors(0)) { WriteColourLayer(geometry, mesh); layers.Add("LayerElementColor"); }

                FbxNode materialLayer = geometry.Add("LayerElementMaterial", 0);
                materialLayer.Add("Version", 101);
                materialLayer.Add("Name", "");
                materialLayer.Add("MappingInformationType", "AllSame");
                materialLayer.Add("ReferenceInformationType", "IndexToDirect");
                materialLayer.Add("Materials", new int[] { 0 });
                layers.Add("LayerElementMaterial");

                FbxNode layer = geometry.Add("Layer", 0);
                layer.Add("Version", 100);
                foreach (string type in layers)
                {
                    FbxNode element = layer.Add("LayerElement");
                    element.Add("Type", type);
                    element.Add("TypedIndex", 0);
                }

                //extra UV sets live on their own layers, one per set past the first
                for (int extra = 1; extra < uvLayers; extra++)
                {
                    FbxNode more = geometry.Add("Layer", extra);
                    more.Add("Version", 100);
                    FbxNode element = more.Add("LayerElement");
                    element.Add("Type", "LayerElementUV");
                    element.Add("TypedIndex", extra);
                }

                if (mesh.MaterialIndex >= 0 && _materials.TryGetValue(mesh.MaterialIndex, out long material))
                    Connect(material, modelId);
            }

            private readonly Dictionary<int, long> _geometryOf = new Dictionary<int, long>();

            /* One value per control point, which is how the scene holds them - no need to expand out
             * to polygon vertices when nothing in the mesh is split along an edge. */
            private static void WriteVectorLayer(FbxNode geometry, string layerName, string valueName, List<Vector3D> values)
            {
                FbxNode layer = geometry.Add(layerName, 0);
                layer.Add("Version", 101);
                layer.Add("Name", "");
                layer.Add("MappingInformationType", "ByVertice");
                layer.Add("ReferenceInformationType", "Direct");

                double[] data = new double[values.Count * 3];
                for (int i = 0; i < values.Count; i++)
                {
                    data[(i * 3) + 0] = values[i].X;
                    data[(i * 3) + 1] = values[i].Y;
                    data[(i * 3) + 2] = values[i].Z;
                }
                layer.Add(valueName, data);
            }

            private static void WriteUVLayer(FbxNode geometry, Mesh mesh, int channel, int layerIndex, List<int> polygons)
            {
                FbxNode layer = geometry.Add("LayerElementUV", layerIndex);
                layer.Add("Version", 101);
                layer.Add("Name", "UVChannel_" + (channel + 1));
                layer.Add("MappingInformationType", "ByPolygonVertex");
                layer.Add("ReferenceInformationType", "IndexToDirect");

                List<Vector3D> uvs = mesh.TextureCoordinateChannels[channel];
                double[] data = new double[uvs.Count * 2];
                for (int i = 0; i < uvs.Count; i++)
                {
                    data[(i * 2) + 0] = uvs[i].X;
                    data[(i * 2) + 1] = uvs[i].Y;
                }
                layer.Add("UV", data);

                int[] indices = new int[polygons.Count];
                for (int i = 0; i < polygons.Count; i++)
                    indices[i] = polygons[i] < 0 ? ~polygons[i] : polygons[i];
                layer.Add("UVIndex", indices);
            }

            private static void WriteColourLayer(FbxNode geometry, Mesh mesh)
            {
                FbxNode layer = geometry.Add("LayerElementColor", 0);
                layer.Add("Version", 101);
                layer.Add("Name", "");
                layer.Add("MappingInformationType", "ByVertice");
                layer.Add("ReferenceInformationType", "Direct");

                List<Color4D> colours = mesh.VertexColorChannels[0];
                double[] data = new double[colours.Count * 4];
                for (int i = 0; i < colours.Count; i++)
                {
                    data[(i * 4) + 0] = colours[i].R;
                    data[(i * 4) + 1] = colours[i].G;
                    data[(i * 4) + 2] = colours[i].B;
                    data[(i * 4) + 3] = colours[i].A;
                }
                layer.Add("Colors", data);
            }
            #endregion

            #region MATERIALS
            private void BuildMaterials()
            {
                for (int i = 0; i < _scene.MaterialCount; i++)
                {
                    Material source = _scene.Materials[i];
                    long id = NextId();
                    _materials[i] = id;

                    FbxNode material = Object("Material", id, string.IsNullOrEmpty(source.Name) ? "material_" + i : source.Name, "");
                    material.Add("Version", 102);
                    material.Add("ShadingModel", "phong");
                    material.Add("MultiLayer", 0);

                    FbxNode properties = material.Add("Properties70");
                    Color4D diffuse = source.HasColorDiffuse ? source.ColorDiffuse : new Color4D(0.8f, 0.8f, 0.8f, 1.0f);
                    properties.Property("DiffuseColor", "Color", "", "A", (double)diffuse.R, (double)diffuse.G, (double)diffuse.B);
                    properties.Property("SpecularColor", "Color", "", "A", 0.2, 0.2, 0.2);
                    properties.Property("ShininessExponent", "Number", "", "A", 20.0);

                    AddTexture(source, TextureType.Diffuse, id, "DiffuseColor");
                    AddTexture(source, TextureType.Normals, id, "NormalMap");
                    AddTexture(source, TextureType.Height, id, "NormalMap");
                    AddTexture(source, TextureType.Specular, id, "SpecularColor");
                }
            }

            private void AddTexture(Material material, TextureType type, long materialId, string property)
            {
                if (!material.GetMaterialTexture(type, 0, out TextureSlot slot) || string.IsNullOrEmpty(slot.FilePath)) return;

                long videoId = NextId();
                FbxNode video = Object("Video", videoId, Path.GetFileNameWithoutExtension(slot.FilePath), "Clip");
                video.Add("Type", "Clip");
                video.Add("Properties70").Property("Path", "KString", "XRefUrl", "", slot.FilePath);
                video.Add("UseMipMap", 0);
                video.Add("Filename", slot.FilePath);
                video.Add("RelativeFilename", slot.FilePath);

                long textureId = NextId();
                FbxNode texture = Object("Texture", textureId, Path.GetFileNameWithoutExtension(slot.FilePath), "");
                texture.Add("Type", "TextureVideoClip");
                texture.Add("Version", 202);
                texture.Add("TextureName", "Texture::" + Path.GetFileNameWithoutExtension(slot.FilePath));
                texture.Add("Properties70").Property("UVSet", "KString", "", "", "UVChannel_1");
                texture.Add("Media", "Video::" + Path.GetFileNameWithoutExtension(slot.FilePath));
                texture.Add("FileName", slot.FilePath);
                texture.Add("RelativeFilename", slot.FilePath);

                Connect(videoId, textureId);
                Connect(textureId, materialId, property);
            }
            #endregion

            #region SKINNING
            /* A skin hangs off the geometry; under it one cluster per bone holds the vertices that
             * bone moves and the two matrices FBX uses to work out the bind pose. */
            private void BuildSkins()
            {
                for (int meshIndex = 0; meshIndex < _scene.MeshCount; meshIndex++)
                {
                    Mesh mesh = _scene.Meshes[meshIndex];
                    if (!mesh.HasBones || !_geometryOf.TryGetValue(meshIndex, out long geometryId)) continue;

                    long skinId = NextId();
                    FbxNode skin = Object("Deformer", skinId, mesh.Name + "_skin", "Skin");
                    skin.Add("Version", 101);
                    skin.Add("Link_DeformAcuracy", 50.0);
                    Connect(skinId, geometryId);

                    foreach (Bone bone in mesh.Bones)
                    {
                        if (!_models.TryGetValue(bone.Name, out long boneModel)) continue;

                        long clusterId = NextId();
                        FbxNode cluster = Object("Deformer", clusterId, bone.Name + "_cluster", "Cluster");
                        cluster.Add("Version", 100);
                        cluster.Add("UserData", "", "");

                        int[] indices = new int[bone.VertexWeightCount];
                        double[] weights = new double[bone.VertexWeightCount];
                        for (int i = 0; i < bone.VertexWeightCount; i++)
                        {
                            indices[i] = bone.VertexWeights[i].VertexID;
                            weights[i] = bone.VertexWeights[i].Weight;
                        }
                        cluster.Add("Indexes", indices);
                        cluster.Add("Weights", weights);

                        /* FBX asks for where the mesh and the bone each sat when the skin was bound.
                         * The mesh nodes we write are at the origin, so its half is the identity and
                         * the bone's half is the inverse of the offset matrix the scene carries. */
                        Assimp.Matrix4x4 link = bone.OffsetMatrix;
                        link.Inverse();
                        cluster.Add("Transform", ToArray(Assimp.Matrix4x4.Identity));
                        cluster.Add("TransformLink", ToArray(link));

                        Connect(clusterId, skinId);
                        Connect(boneModel, clusterId);
                    }
                }
            }

            /* Without a bind pose some tools re-derive one from the current node transforms, which is
             * only right if nothing has moved. Writing it removes the guesswork. */
            private void BuildBindPose()
            {
                if (_bones.Count == 0) return;

                long id = NextId();
                FbxNode pose = Object("Pose", id, "BIND_POSES", "BindPose");
                pose.Add("Type", "BindPose");
                pose.Add("Version", 100);

                Dictionary<string, Assimp.Matrix4x4> world = new Dictionary<string, Assimp.Matrix4x4>(StringComparer.Ordinal);
                Gather(_scene.RootNode, Assimp.Matrix4x4.Identity, world);

                int written = 0;
                List<FbxNode> entries = new List<FbxNode>();
                foreach (KeyValuePair<string, long> model in _models)
                {
                    if (!world.TryGetValue(model.Key, out Assimp.Matrix4x4 transform)) continue;
                    FbxNode entry = new FbxNode("PoseNode");
                    entry.Add("Node", model.Value);
                    entry.Add("Matrix", ToArray(transform));
                    entries.Add(entry);
                    written++;
                }

                pose.Add("NbPoseNodes", written);
                foreach (FbxNode entry in entries) pose.Add(entry);
            }

            private static void Gather(Node node, Assimp.Matrix4x4 parent, Dictionary<string, Assimp.Matrix4x4> into)
            {
                Assimp.Matrix4x4 world = parent * node.Transform;
                into[node.Name] = world;
                foreach (Node child in node.Children) Gather(child, world, into);
            }
            #endregion

            #region ANIMATION
            private void BuildAnimations()
            {
                if (_scene.AnimationCount == 0) return;

                foreach (Assimp.Animation animation in _scene.Animations)
                {
                    double ticksPerSecond = animation.TicksPerSecond > 0 ? animation.TicksPerSecond : 30.0;

                    long stackId = NextId();
                    FbxNode stack = Object("AnimationStack", stackId, animation.Name, "");
                    long stop = (long)Math.Round(animation.DurationInTicks / ticksPerSecond * TimeUnitsPerSecond);
                    FbxNode stackProperties = stack.Add("Properties70");
                    stackProperties.Property("LocalStart", "KTime", "Time", "", 0L);
                    stackProperties.Property("LocalStop", "KTime", "Time", "", stop);
                    stackProperties.Property("ReferenceStart", "KTime", "Time", "", 0L);
                    stackProperties.Property("ReferenceStop", "KTime", "Time", "", stop);

                    long layerId = NextId();
                    Object("AnimationLayer", layerId, animation.Name, "");
                    Connect(layerId, stackId);

                    foreach (NodeAnimationChannel channel in animation.NodeAnimationChannels)
                    {
                        if (!_models.TryGetValue(channel.NodeName, out long model)) continue;
                        BuildChannel(channel, model, layerId, ticksPerSecond);
                    }
                }
            }

            private void BuildChannel(NodeAnimationChannel channel, long model, long layer, double ticksPerSecond)
            {
                if (channel.HasPositionKeys)
                    BuildCurves(layer, model, "Lcl Translation",
                        channel.PositionKeys.Select(x => x.Time).ToList(),
                        channel.PositionKeys.Select(x => new Vector3D(x.Value.X, x.Value.Y, x.Value.Z)).ToList(),
                        ticksPerSecond);

                if (channel.HasRotationKeys)
                    BuildCurves(layer, model, "Lcl Rotation",
                        channel.RotationKeys.Select(x => x.Time).ToList(),
                        EulerTrack(channel.RotationKeys),
                        ticksPerSecond);

                if (channel.HasScalingKeys)
                    BuildCurves(layer, model, "Lcl Scaling",
                        channel.ScalingKeys.Select(x => x.Time).ToList(),
                        channel.ScalingKeys.Select(x => new Vector3D(x.Value.X, x.Value.Y, x.Value.Z)).ToList(),
                        ticksPerSecond);
            }

            /* One curve node per property, holding three curves. FBX addresses the components by the
             * property names "d|X", "d|Y" and "d|Z" on the curve node. */
            private void BuildCurves(long layer, long model, string property, List<double> times, List<Vector3D> values, double ticksPerSecond)
            {
                if (times.Count == 0) return;

                long curveNodeId = NextId();
                FbxNode curveNode = Object("AnimationCurveNode", curveNodeId, property.Replace("Lcl ", ""), "");
                FbxNode properties = curveNode.Add("Properties70");
                properties.Property("d|X", "Number", "", "A", (double)values[0].X);
                properties.Property("d|Y", "Number", "", "A", (double)values[0].Y);
                properties.Property("d|Z", "Number", "", "A", (double)values[0].Z);

                Connect(curveNodeId, layer);
                Connect(curveNodeId, model, property);

                long[] keyTimes = new long[times.Count];
                for (int i = 0; i < times.Count; i++)
                    keyTimes[i] = (long)Math.Round(times[i] / ticksPerSecond * TimeUnitsPerSecond);

                BuildCurve(curveNodeId, "d|X", keyTimes, values.Select(x => x.X).ToArray());
                BuildCurve(curveNodeId, "d|Y", keyTimes, values.Select(x => x.Y).ToArray());
                BuildCurve(curveNodeId, "d|Z", keyTimes, values.Select(x => x.Z).ToArray());
            }

            private void BuildCurve(long curveNode, string property, long[] times, float[] values)
            {
                long id = NextId();
                FbxNode curve = Object("AnimationCurve", id, "", "");
                curve.Add("Default", (double)(values.Length == 0 ? 0 : values[0]));
                curve.Add("KeyVer", 4008);
                curve.Add("KeyTime", times);
                curve.Add("KeyValueFloat", values);

                /* 0x00000002 is the flag for a linear key with no tangent data, which is what a
                 * sampled-every-frame curve wants - anything else invents motion between frames. */
                int[] flags = new int[values.Length];
                for (int i = 0; i < flags.Length; i++) flags[i] = 0x00000002;
                curve.Add("KeyAttrFlags", flags);
                curve.Add("KeyAttrDataFloat", new float[] { 0, 0, 0, 0 });
                curve.Add("KeyAttrRefCount", new int[] { values.Length });

                Connect(id, curveNode, property);
            }

            /* FBX has no quaternion curves, so a rotation track becomes three Euler curves. Each key
             * is picked to be the one nearest the key before it: the same orientation can be spelled
             * many ways, and jumping between spellings shows up as a spin. */
            private static List<Vector3D> EulerTrack(List<QuaternionKey> keys)
            {
                List<Vector3D> track = new List<Vector3D>(keys.Count);
                Vector3D previous = new Vector3D(0, 0, 0);

                for (int i = 0; i < keys.Count; i++)
                {
                    Assimp.Matrix4x4 rotation = new Assimp.Matrix4x4(keys[i].Value.GetMatrix());
                    Vector3D euler = EulerFromMatrix(rotation);
                    if (i != 0) euler = Nearest(euler, previous, rotation);
                    track.Add(euler);
                    previous = euler;
                }
                return track;
            }

            /* The other Euler triple for the same orientation, plus whole turns on any axis, all
             * describe the same rotation. Take whichever lands closest to the last key. */
            private static Vector3D Nearest(Vector3D euler, Vector3D previous, Assimp.Matrix4x4 rotation)
            {
                Vector3D best = Unwrap(euler, previous);
                float bestDistance = Distance(best, previous);

                Vector3D alternate = new Vector3D(euler.X + 180.0f, 180.0f - euler.Y, euler.Z + 180.0f);
                alternate = Unwrap(alternate, previous);
                if (Distance(alternate, previous) < bestDistance) best = alternate;
                return best;
            }

            private static Vector3D Unwrap(Vector3D euler, Vector3D previous)
            {
                return new Vector3D(
                    Unwrap(euler.X, previous.X),
                    Unwrap(euler.Y, previous.Y),
                    Unwrap(euler.Z, previous.Z));
            }

            private static float Unwrap(float value, float previous)
            {
                while (value - previous > 180.0f) value -= 360.0f;
                while (previous - value > 180.0f) value += 360.0f;
                return value;
            }

            private static float Distance(Vector3D a, Vector3D b)
            {
                return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z);
            }
            #endregion

            #region MATHS
            /// <summary>
            /// Split a transform into the translation, Euler rotation in degrees and scale that FBX
            /// stores. Handles a mirroring transform by folding the flip into the scale.
            /// </summary>
            public static void Decompose(Assimp.Matrix4x4 matrix, out Vector3D translation, out Vector3D rotation, out Vector3D scale)
            {
                translation = new Vector3D(matrix.A4, matrix.B4, matrix.C4);

                Vector3D x = new Vector3D(matrix.A1, matrix.B1, matrix.C1);
                Vector3D y = new Vector3D(matrix.A2, matrix.B2, matrix.C2);
                Vector3D z = new Vector3D(matrix.A3, matrix.B3, matrix.C3);

                float sx = x.Length(), sy = y.Length(), sz = z.Length();

                /* A negative determinant means the transform mirrors. FBX can say that with a
                 * negative scale, so put the flip on Z and leave the rotation a true rotation. */
                float determinant = matrix.A1 * (matrix.B2 * matrix.C3 - matrix.B3 * matrix.C2)
                                  - matrix.A2 * (matrix.B1 * matrix.C3 - matrix.B3 * matrix.C1)
                                  + matrix.A3 * (matrix.B1 * matrix.C2 - matrix.B2 * matrix.C1);
                if (determinant < 0) sz = -sz;

                scale = new Vector3D(sx, sy, sz);

                Assimp.Matrix4x4 pure = Assimp.Matrix4x4.Identity;
                if (Math.Abs(sx) > 1e-9f) { pure.A1 = x.X / sx; pure.B1 = x.Y / sx; pure.C1 = x.Z / sx; }
                if (Math.Abs(sy) > 1e-9f) { pure.A2 = y.X / sy; pure.B2 = y.Y / sy; pure.C2 = y.Z / sy; }
                if (Math.Abs(sz) > 1e-9f) { pure.A3 = z.X / sz; pure.B3 = z.Y / sz; pure.C3 = z.Z / sz; }

                rotation = EulerFromMatrix(pure);
            }

            /// <summary>
            /// Euler angles in degrees for FBX's default XYZ order, where the composed rotation is
            /// Rz * Ry * Rx applied to a column vector.
            /// </summary>
            public static Vector3D EulerFromMatrix(Assimp.Matrix4x4 m)
            {
                float y = (float)Math.Asin(Clamp(-m.C1));
                float x, z;

                //at a quarter turn on Y the X and Z axes line up and only their sum is defined
                if (Math.Abs(m.C1) < 0.9999995f)
                {
                    x = (float)Math.Atan2(m.C2, m.C3);
                    z = (float)Math.Atan2(m.B1, m.A1);
                }
                else
                {
                    x = (float)Math.Atan2(-m.B3, m.B2);
                    z = 0;
                }

                const float ToDegrees = (float)(180.0 / Math.PI);
                return new Vector3D(x * ToDegrees, y * ToDegrees, z * ToDegrees);
            }

            private static double Clamp(float value) { return value < -1 ? -1 : value > 1 ? 1 : value; }

            /* FBX matrices are written column by column, the transpose of how assimp holds them */
            private static double[] ToArray(Assimp.Matrix4x4 m)
            {
                return new double[]
                {
                    m.A1, m.B1, m.C1, m.D1,
                    m.A2, m.B2, m.C2, m.D2,
                    m.A3, m.B3, m.C3, m.D3,
                    m.A4, m.B4, m.C4, m.D4,
                };
            }
            #endregion
        }
    }
}
