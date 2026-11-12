using Assimp;
using AlienPAK;
using CATHODE;
using CATHODE.ShaderTypes;
using CathodeLib.Ubershaders;
using CathodeLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Quaternion = System.Numerics.Quaternion;
using Vector3 = System.Numerics.Vector3;

namespace OpenCAGE.SceneImport
{
    /// <summary>
    /// What importing a whole scene file would make, worked out without touching the level. Every node that carries meshes is a placement;
    /// its world matrix W splits into a rotation R and translation (the instance's transform) and B = W3 R^T (scale, mirror and shear, which
    /// a CATHODE entity cannot hold, so it goes into the model's vertices). Placements with the same meshes and B share one model, built by
    /// ModelIO.BuildCS2 as import_model builds it. Lights come from glTF's KHR_lights_punctual (read from the file itself, as Assimp drops
    /// the range) or Assimp's lights for other formats. <see cref="SceneImporter"/> writes it.
    /// </summary>
    internal sealed class SceneOptions
    {
        public string Path;
        /// <summary>The scene's name: its composites go in Scenes\&lt;Name&gt;\, its models and materials are named after it.</summary>
        public string Name;
        public float Scale = 1.0f;
        /// <summary>"top": a composite per node at the first level of the file's hierarchy; "none": one composite for everything.</summary>
        public string GroupBy = "top";
        /// <summary>Meshes with the same content (positions, triangles, UVs, material) count as one, for files that repeat a mesh rather than instance it.</summary>
        public bool MergeIdentical = true;
        /// <summary>solid (WORLD + BALLISTIC pair), world, ballistic or none.</summary>
        public string Collision = "solid";
        /// <summary>Node names (wildcards * ?) that become physics props.</summary>
        public List<string> Physics = new List<string>(DefaultPhysics);
        public static readonly string[] DefaultPhysics = { "*_dyn", "*_physics" };
        /// <summary>Node names (wildcards) left out, with everything under them.</summary>
        public List<string> Exclude = new List<string>();
        public bool Lights = true;
        /// <summary>Multiplies every light's intensity.</summary>
        public float LightScale = 1.0f;
        /// <summary>none, spots (default) or all: which imported lights cast shadows.</summary>
        public string Shadows = "spots";
        public bool CompactTextures;
        /// <summary>Where the scene's origin goes in the level (the group composites are placed there).</summary>
        public Vector3 Position = Vector3.Zero, Rotation = Vector3.Zero;
    }

    internal sealed class ScenePlacement
    {
        public Node Node;
        public string NodeName;
        public string Name;          //the instance's name in its group (unique there)
        public string Group;
        public Matrix4x4 World;      //file units, CATHODE axes, row vectors
        public Matrix4x4 Bake;       //B: the linear part that goes into the model
        public Vector3 Position;     //metres
        public Vector3 Rotation;     //degrees (pitch, yaw, roll) as cTransform holds them
        public bool Physics;
        public SceneModel Model;
    }

    internal sealed class SceneModel
    {
        public string Key;
        public string Name;          //leaf name (no folders)
        public List<int> Meshes = new List<int>();
        public Matrix4x4 Bake = Matrix4x4.Identity;
        public bool Physics;
        public List<ScenePlacement> Placements = new List<ScenePlacement>();
        public int Triangles;
        /// <summary>Submeshes after splitting meshes over 21,845 triangles (estimated as BuildCS2 cuts them).</summary>
        public int Submeshes;
        public List<string> SplitNotes = new List<string>();
        /// <summary>The meshes per component (one ModelReference draws one component: at most 255 elements each).</summary>
        public List<List<int>> Components = new List<List<int>>();
        public bool Mirrored => Bake.GetDeterminant() < 0;
        public bool Skinned;
        public string BakeText;

        //Filled by the importer
        public Models.CS2 Built;
        public string ModelAssetName;
        public string CompositeName;
        public CATHODE.Scripting.Composite Composite;
    }

    internal sealed class SceneLight
    {
        public string Name;
        public string Type;          //point, spot, directional
        public Vector3 Position;     //metres, CATHODE
        public Vector3 Direction;    //CATHODE, unit (spot / directional)
        public Vector3 Colour;       //sRGB 0-255, brightest lane 255
        public double Intensity;     //intensity_multiplier
        public double Range;         //end_attenuation, metres
        public double Inner, Outer;  //full cone angles, degrees
        public bool Shadow;
        public string From;          //what in the file it came from
        public double SourceIntensity;
        public bool SourceRange;
    }

    internal sealed class SceneMaterial
    {
        public int SourceIndex;
        public string SourceName;
        public MaterialGenerator.Plan Plan;
        public Materials.Material Reuses;
        public Materials.Material Made;
        public int Meshes;
        public string Error;
    }

    internal sealed class ScenePlan
    {
        public SceneOptions Options;
        public Scene Scene;
        public float UnitScale = 1.0f;
        public string UnitText;
        public List<ScenePlacement> Placements = new List<ScenePlacement>();
        public List<SceneModel> Models = new List<SceneModel>();
        public List<SceneLight> Lights = new List<SceneLight>();
        public List<string> Groups = new List<string>();
        public Dictionary<int, SceneMaterial> Materials = new Dictionary<int, SceneMaterial>();
        public List<string> Warnings = new List<string>();
        public List<string> Notes = new List<string>();
        public int NodesVisited, MeshesMerged, Excluded;
        public double WorstPlacementMillimetres;
        public string WorstPlacement;
        public JArray LightFrameCheck;
        /// <summary>The scene's mesh index -> the mesh it is the same as (merged identical meshes).</summary>
        public int[] Canonical;

        public const string CompositeFolder = "Scenes";
        public string Folder => CompositeFolder + "\\" + Options.Name;
        public string ModelsFolder => Folder + "\\Models";
        public string LightsComposite => Folder + "\\Lights";
        public string GroupComposite(string group) => Folder + "\\" + group;

        #region Reading
        /// <summary>Read the file and plan models, placements, groups and lights (any thread; the level is not touched).</summary>
        public static ScenePlan Read(SceneOptions options)
        {
            if (options == null || string.IsNullOrEmpty(options.Path) || !File.Exists(options.Path))
                throw new SceneImportException("There is no file at " + options?.Path + ".");
            ScenePlan plan = new ScenePlan() { Options = options };
            try
            {
                using (AssimpContext importer = new AssimpContext())
                    plan.Scene = importer.ImportFile(options.Path, ModelIO.ImportPostProcessSteps);
            }
            catch (Exception e)
            {
                throw new SceneImportException(System.IO.Path.GetFileName(options.Path) + " could not be read: " + e.Message);
            }
            if (plan.Scene == null || plan.Scene.RootNode == null)
                throw new SceneImportException("Nothing could be read from " + System.IO.Path.GetFileName(options.Path) + ".");
            plan.UnitScale = ModelIO.FormatUnitScale(options.Path);
            if (!(plan.UnitScale > 0)) plan.UnitScale = 1;
            plan.UnitText = Math.Abs(plan.UnitScale - 1) < 1e-6 ? "metres" : Math.Abs(plan.UnitScale - 100) < 1e-6 ? "centimetres" : (1 / plan.UnitScale) + " m per file unit";
            plan.MergeMeshes();
            plan.WalkNodes();
            plan.BuildModels();
            plan.CheckPlacements();
            if (options.Lights) plan.ReadLights();
            if (plan.Scene.HasAnimations)
                plan.Notes.Add("The file carries " + plan.Scene.AnimationCount + " animation(s): a scene import places everything where the file shows it, unanimated (import_animation brings clips in for a rig).");
            if (plan.Scene.HasCameras)
                plan.Notes.Add("The file's " + plan.Scene.CameraCount + " camera(s) are not imported.");
            return plan;
        }

        /// <summary>Meshes with identical content are made one (the first of them), so they share a model.</summary>
        private void MergeMeshes()
        {
            Canonical = Enumerable.Range(0, Scene.MeshCount).ToArray();
            if (!Options.MergeIdentical) return;
            Dictionary<ulong, List<int>> byHash = new Dictionary<ulong, List<int>>();
            for (int i = 0; i < Scene.MeshCount; i++)
            {
                ulong hash = MeshHash(Scene.Meshes[i]);
                if (!byHash.TryGetValue(hash, out List<int> same)) byHash[hash] = same = new List<int>();
                int match = same.FindIndex(o => SameContent(Scene.Meshes[o], Scene.Meshes[i]));
                if (match >= 0)
                {
                    Canonical[i] = same[match];
                    MeshesMerged++;
                    continue;
                }
                same.Add(i);
            }
        }

        private static ulong MeshHash(Mesh mesh)
        {
            ulong h = 14695981039346656037UL;
            void Add(long v) { unchecked { h ^= (ulong)v; h *= 1099511628211UL; } }
            Add(mesh.VertexCount); Add(mesh.FaceCount); Add(mesh.MaterialIndex); Add(mesh.HasBones ? 1 : 0);
            foreach (Vector3D v in mesh.Vertices) { Add((long)Math.Round(v.X * 1e5)); Add((long)Math.Round(v.Y * 1e5)); Add((long)Math.Round(v.Z * 1e5)); }
            foreach (Face f in mesh.Faces) foreach (int i in f.Indices) Add(i);
            if (mesh.TextureCoordinateChannelCount > 0)
                foreach (Vector3D t in mesh.TextureCoordinateChannels[0]) { Add((long)Math.Round(t.X * 1e5)); Add((long)Math.Round(t.Y * 1e5)); }
            return h;
        }

        private static bool SameContent(Mesh a, Mesh b)
        {
            if (a.VertexCount != b.VertexCount || a.FaceCount != b.FaceCount || a.MaterialIndex != b.MaterialIndex || a.HasBones || b.HasBones) return false;
            for (int i = 0; i < a.VertexCount; i++)
            {
                Vector3D p = a.Vertices[i], q = b.Vertices[i];
                if (Math.Abs(p.X - q.X) > 1e-5f || Math.Abs(p.Y - q.Y) > 1e-5f || Math.Abs(p.Z - q.Z) > 1e-5f) return false;
            }
            for (int f = 0; f < a.FaceCount; f++)
            {
                List<int> x = a.Faces[f].Indices, y = b.Faces[f].Indices;
                if (x.Count != y.Count) return false;
                for (int k = 0; k < x.Count; k++) if (x[k] != y[k]) return false;
            }
            int channels = Math.Min(a.TextureCoordinateChannelCount, b.TextureCoordinateChannelCount);
            if (a.TextureCoordinateChannelCount != b.TextureCoordinateChannelCount) return false;
            for (int c = 0; c < channels; c++)
                for (int i = 0; i < a.VertexCount && i < a.TextureCoordinateChannels[c].Count && i < b.TextureCoordinateChannels[c].Count; i++)
                {
                    Vector3D p = a.TextureCoordinateChannels[c][i], q = b.TextureCoordinateChannels[c][i];
                    if (Math.Abs(p.X - q.X) > 1e-5f || Math.Abs(p.Y - q.Y) > 1e-5f) return false;
                }
            return true;
        }

        private List<Regex> _physics, _exclude;
        private static readonly Regex GenericMeshName = new Regex(@"^(meshId\d+(_name)?|mesh[\s_.-]*\d*|object[\s_.-]*\d*|defaultobject|geometry[\s_.-]*\d*|polySurface\d*|pCube\d*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex CopyNumber = new Regex(@"([_.\s-]\d+)+$", RegexOptions.CultureInvariant);

        private static Regex Wildcard(string pattern)
        {
            string p = (pattern ?? "").Trim();
            return new Regex("^" + Regex.Escape(p).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static bool Matches(List<Regex> patterns, string name) => patterns != null && name != null && patterns.Any(o => o.IsMatch(name));

        /// <summary>Every node carrying meshes becomes a placement, in its group.</summary>
        private void WalkNodes()
        {
            _physics = Options.Physics?.Where(o => !string.IsNullOrWhiteSpace(o)).Select(Wildcard).ToList() ?? new List<Regex>();
            _exclude = Options.Exclude?.Where(o => !string.IsNullOrWhiteSpace(o)).Select(Wildcard).ToList() ?? new List<Regex>();

            //The content's root: wrapper nodes with one child and no meshes (an exporter's "Scene" / "RootNode" chain) are stepped through
            Node content = Scene.RootNode;
            while (content.ChildCount == 1 && !content.HasMeshes)
                content = content.Children[0];

            Dictionary<string, int> taken = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            void Visit(Node node, Matrix4x4 parent, string group)
            {
                NodesVisited++;
                Matrix4x4 world = ModelIO.ToNumerics(node.Transform) * parent;
                if (Matches(_exclude, node.Name))
                {
                    Excluded++;
                    return;
                }
                if (node.Parent == content && Options.GroupBy != "none")
                    group = GroupName(node, taken);
                if (node.HasMeshes)
                {
                    ScenePlacement placement = new ScenePlacement()
                    {
                        Node = node,
                        NodeName = node.Name ?? "",
                        World = world,
                        Group = group ?? "Main",
                        Physics = Matches(_physics, node.Name),
                    };
                    //A loose node at the first level is a group of one: those go together in Main
                    if (node.Parent == content && !node.HasChildren)
                        placement.Group = "Main";
                    Placements.Add(placement);
                }
                foreach (Node child in node.Children)
                    Visit(child, world, group);
            }
            Visit(content, content == Scene.RootNode ? Matrix4x4.Identity : WorldOf(content.Parent), null);

            //Groups in file order, each placement named after its node (unique in the group)
            foreach (ScenePlacement placement in Placements)
                if (!Groups.Contains(placement.Group)) Groups.Add(placement.Group);
            foreach (IGrouping<string, ScenePlacement> group in Placements.GroupBy(o => o.Group))
            {
                HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ScenePlacement placement in group)
                {
                    string name = EntityName(placement.NodeName, "Mesh");
                    string unique = name;
                    for (int n = 2; !names.Add(unique); n++) unique = name + "_" + n;
                    placement.Name = unique;
                }
            }
            if (Placements.Count == 0)
                throw new SceneImportException("The file has no meshes to place" + (Excluded != 0 ? " (" + Excluded + " node(s) were excluded)." : "."));
        }

        private string GroupName(Node node, Dictionary<string, int> taken)
        {
            string name = EntityName(node.Name, "Group");
            if (string.Equals(name, "Models", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Lights", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Main", StringComparison.OrdinalIgnoreCase))
                name += "_Group";
            if (!taken.TryGetValue(name, out int count)) { taken[name] = 1; return name; }
            taken[name] = count + 1;
            return name + "_" + (count + 1);
        }

        private static Matrix4x4 WorldOf(Node node)
        {
            //row vectors: a node's world is its own transform times its parents', innermost first
            Matrix4x4 composed = Matrix4x4.Identity;
            List<Node> chain = new List<Node>();
            for (Node at = node; at != null; at = at.Parent) chain.Add(at);
            foreach (Node at in chain) composed = composed * ModelIO.ToNumerics(at.Transform);
            return composed;
        }

        /// <summary>A name as an entity or composite can carry it: letters, digits, _ - . and spaces kept, the rest _.</summary>
        public static string EntityName(string name, string fallback)
        {
            string text = (name ?? "").Trim();
            if (text.Length == 0) return fallback;
            StringBuilder b = new StringBuilder(text.Length);
            foreach (char c in text)
                b.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' || c == ' ' ? c : '_');
            string clean = b.ToString().Trim('.', ' ');
            if (clean.Length > 60) clean = clean.Substring(0, 60);
            return clean.Length == 0 ? fallback : clean;
        }

        /// <summary>Split each placement's world matrix into the instance's transform and the model's bake, and share models.</summary>
        private void BuildModels()
        {
            Dictionary<string, SceneModel> byKey = new Dictionary<string, SceneModel>();
            float unit = UnitScale, scale = Options.Scale;
            foreach (ScenePlacement placement in Placements)
            {
                Split(placement.World, out Matrix4x4 bake, out Matrix4x4 rotation);
                placement.Bake = bake;
                Vector3 t = placement.World.Translation;
                placement.Position = t * scale / unit;
                placement.Rotation = InstanceTransform.ToEulerDegrees(Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(rotation)));

                List<int> meshes = placement.Node.MeshIndices.Select(o => Canonical[o]).OrderBy(o => o).ToList();
                string key = string.Join(",", meshes) + "|" + BakeKey(bake) + "|" + (placement.Physics ? "P" : "S");
                if (!byKey.TryGetValue(key, out SceneModel model))
                {
                    model = new SceneModel() { Key = key, Meshes = meshes, Bake = bake, Physics = placement.Physics };
                    model.Skinned = meshes.Any(o => Scene.Meshes[o].HasBones);
                    byKey[key] = model;
                    Models.Add(model);
                }
                model.Placements.Add(placement);
                placement.Model = model;
            }

            //Names: the first mesh's (or the node's) name; variants of the same meshes say how they differ
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (IGrouping<string, SceneModel> sameMeshes in Models.GroupBy(o => string.Join(",", o.Meshes)))
            {
                List<SceneModel> variants = sameMeshes.ToList();
                foreach (SceneModel model in variants)
                {
                    Mesh first = Scene.Meshes[model.Meshes[0]];
                    //The mesh's own name, unless it is an exporter's placeholder ("meshId3_name", "Mesh.001"): then the node's, less a copy number
                    string meshName = first.Name ?? "";
                    string baseName = EntityName(!string.IsNullOrWhiteSpace(meshName) && !GenericMeshName.IsMatch(meshName) ? meshName : CopyNumber.Replace(model.Placements[0].NodeName ?? "", ""), "Model");
                    model.BakeText = DescribeBake(model.Bake);
                    string name = baseName;
                    if (variants.Count > 1 && !IsIdentity(model.Bake) && baseName.IndexOf(model.BakeText.Split(' ')[0], StringComparison.OrdinalIgnoreCase) < 0)
                        name += "_" + model.BakeText.Replace(" ", "").Replace(",", "x").Replace("scaled", "x").Replace("mirrored", "Mirrored").Replace("sheared", "Sheared");
                    if (model.Physics && variants.Any(o => !o.Physics)) name += "_Physics";
                    name = EntityName(name, "Model");
                    string unique = name;
                    for (int n = 2; !names.Add(unique); n++) unique = name + "_" + n;
                    model.Name = unique;
                }
            }

            //Triangles, splits, components
            foreach (SceneModel model in Models)
            {
                List<List<int>> components = new List<List<int>>() { new List<int>() };
                int inComponent = 0;
                foreach (int index in model.Meshes)
                {
                    Mesh mesh = Scene.Meshes[index];
                    int triangles = mesh.Faces.Count(o => o.IndexCount == 3);
                    model.Triangles += triangles;
                    int pieces = triangles > ModelIO.MaxTrianglesPerSubmesh ? (int)Math.Ceiling(triangles / 20000.0) : 1;
                    if (pieces > 1) model.SplitNotes.Add((string.IsNullOrEmpty(mesh.Name) || GenericMeshName.IsMatch(mesh.Name) ? model.Name : mesh.Name) + ": " + triangles.ToString("N0") + " triangles -> " + pieces + " submeshes (one holds at most " + ModelIO.MaxTrianglesPerSubmesh.ToString("N0") + ")");
                    model.Submeshes += pieces;
                    if (inComponent + pieces > RenderableElements.MaxElementsPerInstance && components[components.Count - 1].Count != 0)
                    {
                        components.Add(new List<int>());
                        inComponent = 0;
                    }
                    components[components.Count - 1].Add(index);
                    inComponent += pieces;
                }
                model.Components = components;
                if (model.Skinned)
                    Warnings.Add(model.Name + " is skinned: a scene import brings it in static, at its bind pose (import_model with a game skeleton keeps skinning).");
            }
        }

        /// <summary>W3 = B * R: R by Gram-Schmidt of W3's rows (a rotation, determinant +1), B = W3 R^T (scale, mirror and shear).</summary>
        public static void Split(Matrix4x4 world, out Matrix4x4 bake, out Matrix4x4 rotation)
        {
            Vector3 r0 = new Vector3(world.M11, world.M12, world.M13), r1 = new Vector3(world.M21, world.M22, world.M23);
            Vector3 x = r0.Length() > 1e-12f ? Vector3.Normalize(r0) : Vector3.UnitX;
            Vector3 y = r1 - Vector3.Dot(r1, x) * x;
            if (y.Length() < 1e-9f)
            {
                //row 1 along row 0 (or nothing): any perpendicular
                y = Math.Abs(x.Y) < 0.9f ? Vector3.Cross(Vector3.UnitY, x) : Vector3.Cross(Vector3.UnitZ, x);
                y = Vector3.Cross(x, y);
            }
            y = Vector3.Normalize(y);
            Vector3 z = Vector3.Cross(x, y);
            rotation = new Matrix4x4(x.X, x.Y, x.Z, 0, y.X, y.Y, y.Z, 0, z.X, z.Y, z.Z, 0, 0, 0, 0, 1);
            Matrix4x4 w3 = world;
            w3.Translation = Vector3.Zero;
            bake = w3 * Matrix4x4.Transpose(rotation);
            bake.M14 = bake.M24 = bake.M34 = 0; bake.M41 = bake.M42 = bake.M43 = 0; bake.M44 = 1;
            //tidy rounding noise so an unscaled node bakes to exactly the identity
            float[] values = { bake.M11, bake.M12, bake.M13, bake.M21, bake.M22, bake.M23, bake.M31, bake.M32, bake.M33 };
            for (int i = 0; i < 9; i++)
            {
                float target = (i % 4 == 0) ? (float)Math.Round(values[i]) : 0;
                if (Math.Abs(values[i] - target) < 1e-6f) values[i] = target;
            }
            bake = new Matrix4x4(values[0], values[1], values[2], 0, values[3], values[4], values[5], 0, values[6], values[7], values[8], 0, 0, 0, 0, 1);
        }

        /// <summary>B to 0.1% of its largest element: placements this close share a model.</summary>
        private static string BakeKey(Matrix4x4 b)
        {
            float[] v = { b.M11, b.M12, b.M13, b.M21, b.M22, b.M23, b.M31, b.M32, b.M33 };
            float m = v.Max(o => Math.Abs(o));
            if (m < 1e-12f) return "0";
            long magnitude = (long)Math.Round(Math.Log(m) / Math.Log(1.001));
            return magnitude + ":" + string.Join(",", v.Select(o => (long)Math.Round(o / m * 1000)));
        }

        private static bool IsIdentity(Matrix4x4 b) => BakeKey(b) == BakeKey(Matrix4x4.Identity);

        private static string DescribeBake(Matrix4x4 b)
        {
            if (IsIdentity(b)) return "as authored";
            bool diagonal = Math.Abs(b.M12) + Math.Abs(b.M13) + Math.Abs(b.M21) + Math.Abs(b.M23) + Math.Abs(b.M31) + Math.Abs(b.M32) < 1e-4f;
            string Num(float f) => Math.Round(f, 3).ToString(System.Globalization.CultureInfo.InvariantCulture);
            List<string> parts = new List<string>();
            if (b.GetDeterminant() < 0) parts.Add("mirrored");
            if (diagonal)
            {
                float sx = Math.Abs(b.M11), sy = Math.Abs(b.M22), sz = Math.Abs(b.M33);
                if (Math.Abs(sx - sy) < 1e-3f && Math.Abs(sy - sz) < 1e-3f) { if (Math.Abs(sx - 1) > 1e-3f) parts.Add("scaled " + Num(sx)); }
                else parts.Add("scaled " + Num(sx) + "," + Num(sy) + "," + Num(sz));
            }
            else parts.Add("sheared");
            return parts.Count == 0 ? "as authored" : string.Join(" ", parts);
        }

        /// <summary>
        /// Every placement checked the way the game will place it: each corner of its meshes' box through the file's world matrix, against
        /// the same corner through the model's bake (the shared model's, after rounding) and the instance's cTransform. Worst in mm.
        /// </summary>
        private void CheckPlacements()
        {
            double worst = 0;
            float unit = UnitScale, scale = Options.Scale;
            foreach (ScenePlacement placement in Placements)
            {
                Matrix4x4 instance = InstanceTransform.ToMatrix(new CATHODE.Scripting.cTransform(placement.Position, placement.Rotation));
                foreach (int index in placement.Node.MeshIndices)
                {
                    Mesh mesh = Scene.Meshes[index];
                    if (mesh.VertexCount == 0) continue;
                    Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
                    foreach (Vector3D v in mesh.Vertices) { Vector3 p = new Vector3(v.X, v.Y, v.Z); min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
                    for (int c = 0; c < 8; c++)
                    {
                        Vector3 corner = new Vector3((c & 1) == 0 ? min.X : max.X, (c & 2) == 0 ? min.Y : max.Y, (c & 4) == 0 ? min.Z : max.Z);
                        Vector3 want = Vector3.Transform(corner, placement.World) * scale / unit;
                        Vector3 got = Vector3.Transform(Vector3.Transform(corner, placement.Model.Bake) * scale / unit, instance);
                        double off = Vector3.Distance(want, got) * 1000.0;
                        if (off > worst) { worst = off; WorstPlacement = placement.Group + " / " + placement.Name; }
                    }
                }
            }
            WorstPlacementMillimetres = worst;
            if (worst > 10)
                Warnings.Add("PLACEMENT: the worst placement (" + WorstPlacement + ") lands " + worst.ToString("0.#") + " mm from where the file puts it (a shared model's rounded scale). Pass merge_identical false or scale it in the file.");
        }
        #endregion

        #region Lights
        /// <summary>Lights: glTF KHR_lights_punctual from the file's JSON (range, intensity and colour kept apart); Assimp's for other formats.</summary>
        private void ReadLights()
        {
            bool gltf = GltfMaterialFlags.IsGltf(Options.Path);
            List<SceneLight> fromJson = gltf ? GltfLights() : null;
            List<SceneLight> fromAssimp = AssimpLights();
            if (fromJson != null)
            {
                Lights.AddRange(fromJson);
                //The two readings compared: the Assimp route (used for FBX / COLLADA) checked against the file's own numbers
                LightFrameCheck = new JArray();
                foreach (SceneLight light in fromJson)
                {
                    SceneLight other = fromAssimp.FirstOrDefault(o => o.From == light.From);
                    if (other == null) continue;
                    LightFrameCheck.Add(new JObject()
                    {
                        ["light"] = light.Name,
                        ["position_difference_m"] = Math.Round(Vector3.Distance(light.Position, other.Position), 4),
                        ["direction_difference_deg"] = light.Type == "point" ? 0 : Math.Round(Math.Acos(Math.Max(-1, Math.Min(1, Vector3.Dot(light.Direction, other.Direction)))) * 180 / Math.PI, 2),
                    });
                }
            }
            else
                Lights.AddRange(fromAssimp);

            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SceneLight light in Lights)
            {
                string name = EntityName(light.Name, "Light");
                string unique = name;
                for (int n = 2; !names.Add(unique); n++) unique = name + "_" + n;
                light.Name = unique;
                light.Shadow = light.Type == "spot" ? Options.Shadows != "none" : light.Type == "point" && Options.Shadows == "all";
            }
            int directional = Lights.Count(o => o.Type == "directional");
            if (directional != 0)
                Notes.Add("The file has " + directional + " directional light(s) (a sun): Alien has no sun light, so it is not placed as one - add_outdoor_lighting with the 'sun' direction listed below builds a sun, sky fill and bounce from the game's own lights (directional 'outdoor_lighting' runs it after the import).");
        }

        /// <summary>
        /// The file's KHR_lights_punctual lights, placed through the file's own node hierarchy (right-handed, metres) and turned into
        /// CATHODE axes (z negated, as the importer does with the meshes). Colour linear -> sRGB with the brightest lane 255 (the game reads a
        /// light's colour as sRGB); intensity candela (lux for a sun) -> intensity_multiplier: 50 cd = 1 (Blender's 1000 W point exports as 54
        /// cd), x light_scale; range kept, else where the light falls to a twentieth of a 1-intensity light at 1 m; cones half-angle -> full.
        /// </summary>
        private List<SceneLight> GltfLights()
        {
            JObject root;
            try { root = JObject.Parse(GltfJson(Options.Path)); }
            catch (Exception e)
            {
                Warnings.Add("The file's lights could not be read (" + e.Message + ").");
                return null;
            }
            JArray lights = root.SelectToken("extensions.KHR_lights_punctual.lights") as JArray;
            JArray nodes = root["nodes"] as JArray;
            List<SceneLight> found = new List<SceneLight>();
            if (lights == null || nodes == null) return found;

            //Each node's parent, then its world matrix (column vectors: M = T R S, world = parent * local)
            int[] parent = Enumerable.Repeat(-1, nodes.Count).ToArray();
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i]["children"] is JArray children)
                    foreach (JToken c in children) if ((int)c >= 0 && (int)c < nodes.Count) parent[(int)c] = i;
            double[,] Local(int i)
            {
                JObject node = (JObject)nodes[i];
                double[,] m = new double[4, 4];
                if (node["matrix"] is JArray matrix && matrix.Count == 16)
                {
                    for (int c = 0; c < 4; c++) for (int r = 0; r < 4; r++) m[r, c] = (double)matrix[c * 4 + r];
                    return m;
                }
                double[] t = node["translation"] is JArray ta ? ta.Select(o => (double)o).ToArray() : new double[3];
                double[] q = node["rotation"] is JArray ra ? ra.Select(o => (double)o).ToArray() : new double[] { 0, 0, 0, 1 };
                double[] s = node["scale"] is JArray sa ? sa.Select(o => (double)o).ToArray() : new double[] { 1, 1, 1 };
                double x = q[0], y = q[1], z = q[2], w = q[3];
                double[,] r3 = { { 1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w) }, { 2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w) }, { 2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y) } };
                for (int a = 0; a < 3; a++) { for (int b = 0; b < 3; b++) m[a, b] = r3[a, b] * s[b]; m[a, 3] = t[a]; }
                m[3, 3] = 1;
                return m;
            }
            double[,] Mul(double[,] a, double[,] b) { double[,] o = new double[4, 4]; for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) for (int k = 0; k < 4; k++) o[i, j] += a[i, k] * b[k, j]; return o; }
            double[,] World(int i) => parent[i] < 0 ? Local(i) : Mul(World(parent[i]), Local(i));

            float scale = Options.Scale;
            for (int i = 0; i < nodes.Count; i++)
            {
                JToken index = nodes[i].SelectToken("extensions.KHR_lights_punctual.light");
                if (index == null || (int)index < 0 || (int)index >= lights.Count) continue;
                string nodeName = (string)nodes[i]["name"];
                if (Matches(_exclude, nodeName)) continue;
                JObject light = (JObject)lights[(int)index];
                double[,] m = World(i);
                Vector3 position = new Vector3((float)m[0, 3], (float)m[1, 3], (float)-m[2, 3]) * scale;
                //A punctual light points down its node's -Z
                Vector3 direction = new Vector3((float)-m[0, 2], (float)-m[1, 2], (float)m[2, 2]);
                direction = direction.Length() > 1e-9f ? Vector3.Normalize(direction) : -Vector3.UnitY;
                double[] colour = light["color"] is JArray ca ? ca.Select(o => (double)o).ToArray() : new double[] { 1, 1, 1 };
                double intensity = light["intensity"] != null ? (double)light["intensity"] : 1.0;
                string type = ((string)light["type"] ?? "point").ToLowerInvariant();
                SceneLight made = MakeLight(nodeName ?? (string)light["name"], type, position, direction, colour, intensity, light["range"] != null ? (double?)((double)light["range"] * scale) : null,
                    (double?)light.SelectToken("spot.innerConeAngle") ?? 0.0, (double?)light.SelectToken("spot.outerConeAngle") ?? Math.PI / 4);
                made.From = nodeName ?? ("node " + i);
                found.Add(made);
            }
            return found;
        }

        /// <summary>Lights as Assimp reads them, placed by their node (already in CATHODE axes; a light's own local position and direction are flipped the same way).</summary>
        private List<SceneLight> AssimpLights()
        {
            List<SceneLight> found = new List<SceneLight>();
            if (!Scene.HasLights) return found;
            float unit = UnitScale, scale = Options.Scale;
            foreach (Light light in Scene.Lights)
            {
                Node node = Scene.RootNode.FindNode(light.Name);
                if (node == null || Matches(_exclude, light.Name)) continue;
                Matrix4x4 world = WorldOf(node);
                Vector3 local = new Vector3(light.Position.X, light.Position.Y, -light.Position.Z);
                Vector3 localDirection = new Vector3(light.Direction.X, light.Direction.Y, -light.Direction.Z);
                Vector3 position = Vector3.Transform(local, world) * scale / unit;
                Vector3 direction = Vector3.TransformNormal(localDirection, world);
                direction = direction.Length() > 1e-9f ? Vector3.Normalize(direction) : -Vector3.UnitY;
                string type = light.LightType == LightSourceType.Spot ? "spot" : light.LightType == LightSourceType.Directional ? "directional" : light.LightType == LightSourceType.Point ? "point" : null;
                if (type == null)
                {
                    Notes.Add("Light '" + light.Name + "' is an " + light.LightType + " light, which has no game equivalent: left out.");
                    continue;
                }
                //Assimp folds intensity into the colour
                Color3D c = light.ColorDiffuse;
                double peak = Math.Max(c.R, Math.Max(c.G, c.B));
                double[] colour = peak > 0 ? new double[] { c.R / peak, c.G / peak, c.B / peak } : new double[] { 1, 1, 1 };
                SceneLight made = MakeLight(light.Name, type, position, direction, colour, peak > 0 ? peak : 1, null, light.AngleInnerCone, light.AngleOuterCone);
                made.From = light.Name;
                found.Add(made);
            }
            return found;
        }

        public const double CandelaPerIntensity = 50.0;

        private SceneLight MakeLight(string name, string type, Vector3 position, Vector3 direction, double[] linear, double intensity, double? range, double innerHalf, double outerHalf)
        {
            double peak = Math.Max(linear[0], Math.Max(linear[1], linear[2]));
            if (!(peak > 0)) { linear = new double[] { 1, 1, 1 }; peak = 1; }
            //sRGB of the linear colour, brightest lane 255 (game colour = (c/255)^2.2 x intensity)
            Vector3 srgb = new Vector3((float)(255 * Math.Pow(linear[0] / peak, 1 / 2.2)), (float)(255 * Math.Pow(linear[1] / peak, 1 / 2.2)), (float)(255 * Math.Pow(linear[2] / peak, 1 / 2.2)));
            double candela = intensity * peak;
            double value = candela / CandelaPerIntensity * Options.LightScale;
            double reach = range ?? Math.Max(2, Math.Min(30, Math.Sqrt(Math.Max(value, 0.01) * 20)));
            double outer = Math.Max(1, Math.Min(179, outerHalf * 2 * 180 / Math.PI));
            double inner = Math.Max(0.5, Math.Min(outer, innerHalf * 2 * 180 / Math.PI));
            return new SceneLight()
            {
                Name = name,
                Type = type,
                Position = position,
                Direction = direction,
                Colour = new Vector3((float)Math.Round(srgb.X), (float)Math.Round(srgb.Y), (float)Math.Round(srgb.Z)),
                Intensity = Math.Round(value, 4),
                Range = Math.Round(reach, 3),
                Inner = Math.Round(inner, 2),
                Outer = Math.Round(outer, 2),
                SourceIntensity = intensity,
                SourceRange = range.HasValue,
            };
        }

        private static string GltfJson(string path)
        {
            if (!System.IO.Path.GetExtension(path).Equals(".glb", StringComparison.OrdinalIgnoreCase))
                return File.ReadAllText(path);
            using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
            {
                if (reader.BaseStream.Length < 20 || reader.ReadUInt32() != 0x46546C67) throw new InvalidDataException("not a GLB");
                reader.ReadUInt32();
                reader.ReadUInt32();
                int length = reader.ReadInt32();
                if (reader.ReadUInt32() != 0x4E4F534A || length <= 0 || length > reader.BaseStream.Length - 20) throw new InvalidDataException("no JSON chunk");
                return Encoding.UTF8.GetString(reader.ReadBytes(length));
            }
        }
        #endregion

        #region Materials (UI thread: reads the level)
        /// <summary>
        /// One material plan per source material for the whole scene (read-only: MaterialGenerator.Describe). Describe judges double-sided
        /// faces and the glTF doubleSided sheet test over every mesh of the material; the mesh it is given decides vertex colour, so the
        /// first one with colours is used.
        /// </summary>
        public void PlanMaterials(Level level)
        {
            Materials.Clear();
            HashSet<int> used = new HashSet<int>(Models.SelectMany(o => o.Meshes).Select(o => Scene.Meshes[o].MaterialIndex).Where(o => o >= 0 && o < Scene.MaterialCount));
            List<SHADER_LIST> creatable = null;
            foreach (int index in used.OrderBy(o => o))
            {
                Material source = Scene.Materials[index];
                List<Mesh> meshes = Models.SelectMany(o => o.Meshes).Distinct().Select(o => Scene.Meshes[o]).Where(o => o.MaterialIndex == index).ToList();
                Mesh representative = meshes.FirstOrDefault(o => o.VertexColorChannelCount > 0) ?? meshes.FirstOrDefault();
                creatable = creatable ?? ShaderPermutationService.CreatableFamilies(level.Shaders, Singleton.PathToAI).Select(o => o.Family).ToList();
                SHADER_LIST family = MaterialGenerator.SuggestFamily(representative, creatable);
                if (meshes.Any(o => o.HasBones) && creatable.Contains(SHADER_LIST.CA_ENVIRONMENT)) family = SHADER_LIST.CA_ENVIRONMENT;
                string sourceName = string.IsNullOrWhiteSpace(source.Name) || source.Name == "DefaultMaterial" ? "Default" : source.Name;
                string name = AssetName.Sanitise(Options.Name + "\\" + sourceName);
                SceneMaterial material = new SceneMaterial() { SourceIndex = index, SourceName = sourceName, Meshes = meshes.Count };
                try
                {
                    MaterialGenerator.Plan plan = MaterialGenerator.Describe(Scene, source, representative, Options.Path, false, level, family, name, Singleton.PathToAI);
                    plan.CompactTextures = Options.CompactTextures;
                    material.Plan = plan;
                    material.Error = plan.Error;
                    if (plan.CanGenerate) material.Reuses = MaterialGenerator.WouldReuse(plan, level);
                }
                catch (Exception e)
                {
                    material.Error = e.Message;
                }
                Materials[index] = material;
            }
        }
        #endregion
    }

    internal sealed class SceneImportException : Exception
    {
        public SceneImportException(string message) : base(message) { }
    }
}
