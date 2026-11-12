using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenCAGE
{
    /// <summary>
    /// What a glTF material says that AssimpNet loses: materials[].doubleSided. AssimpNet reads the glTF importer's one-byte two-sided
    /// flag wrongly, so Material.IsTwoSided always comes back false. The flag is read here from the file itself (the .gltf's JSON, or a
    /// .glb's JSON chunk) and matched to Assimp's material by index (Assimp keeps the glTF order), its name checked.
    /// </summary>
    public static class GltfMaterialFlags
    {
        public sealed class Info
        {
            public int Index;
            public string Name;
            public bool DoubleSided;
            public string AlphaMode;
        }

        private static readonly Dictionary<string, (DateTime written, long length, List<Info> materials)> _cache = new Dictionary<string, (DateTime, long, List<Info>)>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new object();

        public static bool IsGltf(string path)
        {
            string extension = Path.GetExtension(path ?? "").ToLowerInvariant();
            return extension == ".gltf" || extension == ".glb";
        }

        /// <summary>The file's materials as its JSON lists them; null when it is not a glTF or can't be read.</summary>
        public static List<Info> Read(string path)
        {
            if (!IsGltf(path) || !File.Exists(path))
                return null;
            FileInfo file = new FileInfo(path);
            lock (_lock)
            {
                if (_cache.TryGetValue(path, out var cached) && cached.written == file.LastWriteTimeUtc && cached.length == file.Length)
                    return cached.materials;
            }
            List<Info> materials = null;
            try
            {
                string json = Path.GetExtension(path).Equals(".glb", StringComparison.OrdinalIgnoreCase) ? GlbJson(path) : File.ReadAllText(path);
                if (json != null)
                {
                    JObject root = JObject.Parse(json);
                    materials = new List<Info>();
                    if (root["materials"] is JArray array)
                        for (int i = 0; i < array.Count; i++)
                        {
                            JObject material = array[i] as JObject;
                            materials.Add(new Info()
                            {
                                Index = i,
                                Name = (string)material?["name"],
                                DoubleSided = material?["doubleSided"] != null && material["doubleSided"].Type == JTokenType.Boolean && (bool)material["doubleSided"],
                                AlphaMode = (string)material?["alphaMode"] ?? "OPAQUE",
                            });
                        }
                }
            }
            catch (Exception e)
            {
                Debug.Log("GltfMaterialFlags", "Could not read the materials of " + path + ": " + e.Message);
                materials = null;
            }
            lock (_lock)
                _cache[path] = (file.LastWriteTimeUtc, file.Length, materials);
            return materials;
        }

        /// <summary>A .glb's JSON chunk: 12-byte header ('glTF', version, length), then chunk 0 = length, 'JSON', the text.</summary>
        private static string GlbJson(string path)
        {
            using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
            {
                if (reader.BaseStream.Length < 20 || reader.ReadUInt32() != 0x46546C67) return null;
                reader.ReadUInt32();
                reader.ReadUInt32();
                int length = reader.ReadInt32();
                if (reader.ReadUInt32() != 0x4E4F534A || length <= 0 || length > reader.BaseStream.Length - 20) return null;
                return Encoding.UTF8.GetString(reader.ReadBytes(length));
            }
        }

        /// <summary>
        /// The glTF material Assimp's <paramref name="material"/> came from: the same index when that one has the same name (Assimp keeps
        /// glTF's order and may add a default material at the end), else the only one with that name. Null when it can't be told.
        /// </summary>
        public static Info Find(string path, Assimp.Scene scene, Assimp.Material material)
        {
            List<Info> materials = Read(path);
            if (materials == null || material == null) return null;
            int index = scene?.Materials == null ? -1 : scene.Materials.IndexOf(material);
            string name = material.HasName ? material.Name : null;
            if (index >= 0 && index < materials.Count && (name == null || string.Equals(materials[index].Name ?? "", name, StringComparison.Ordinal) || materials[index].Name == null))
                return materials[index];
            List<Info> named = materials.Where(o => name != null && string.Equals(o.Name, name, StringComparison.Ordinal)).ToList();
            return named.Count == 1 ? named[0] : null;
        }

        /// <summary>
        /// Share of the edges of the meshes drawn with <paramref name="material"/> that only one triangle has (corners welded at 0.1 mm):
        /// 0 for a closed solid, high for a sheet (a leaf card, a net, a single pane) - a sheet seen from behind shows nothing one-sided.
        /// </summary>
        public static double OpenEdgeShare(Assimp.Scene scene, Assimp.Material material, Assimp.Mesh fallback)
        {
            int materialIndex = scene?.Materials == null || material == null ? -1 : scene.Materials.IndexOf(material);
            IEnumerable<Assimp.Mesh> meshes = materialIndex >= 0 ? scene.Meshes.Where(o => o.MaterialIndex == materialIndex) : fallback != null ? new[] { fallback } : Enumerable.Empty<Assimp.Mesh>();
            int open = 0, total = 0;
            foreach (Assimp.Mesh mesh in meshes)
            {
                Dictionary<(long, long, long), int> welded = new Dictionary<(long, long, long), int>();
                int[] corner = new int[mesh.VertexCount];
                for (int v = 0; v < mesh.VertexCount; v++)
                {
                    Assimp.Vector3D p = mesh.Vertices[v];
                    (long, long, long) key = ((long)Math.Round(p.X * 10000.0), (long)Math.Round(p.Y * 10000.0), (long)Math.Round(p.Z * 10000.0));
                    if (!welded.TryGetValue(key, out int id)) { id = welded.Count; welded[key] = id; }
                    corner[v] = id;
                }
                Dictionary<(int, int), int> edges = new Dictionary<(int, int), int>();
                foreach (Assimp.Face face in mesh.Faces)
                {
                    if (face.IndexCount != 3) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        int a = corner[face.Indices[k]], b = corner[face.Indices[(k + 1) % 3]];
                        if (a == b) continue;
                        (int, int) key = a < b ? (a, b) : (b, a);
                        edges.TryGetValue(key, out int count);
                        edges[key] = count + 1;
                    }
                }
                total += edges.Count;
                open += edges.Values.Count(o => o == 1);
            }
            return total == 0 ? 0 : (double)open / total;
        }

        /// <summary>Above this share of open edges a mesh is a sheet, whose back must draw.</summary>
        public const double SheetShare = 0.02;

        /// <summary>
        /// Carry a glTF material's doubleSided into a material plan (MaterialGenerator.Describe): an open sheet asking for it is made
        /// double-sided (<see cref="MaterialGenerator.Plan.NeedsBothSides"/>); a closed solid stays one-sided, its back faces hidden anyway.
        /// Assimp's own two-sided flag can't be set either (its getter reads it the same wrong way). Returns true when the file asked for it.
        /// </summary>
        public static bool ApplyDoubleSided(MaterialGenerator.Plan plan, Assimp.Scene scene, Assimp.Material source, Assimp.Mesh mesh, string modelPath)
        {
            if (plan == null || source == null) return false;
            Info info = Find(modelPath, scene, source);
            if (info == null || !info.DoubleSided) return false;
            double open = OpenEdgeShare(scene, source, mesh);
            if (open >= SheetShare)
            {
                if (!plan.NeedsBothSides)
                    plan.Notes.Add("The model file asks for this material to be drawn from both sides (glTF doubleSided), and its mesh is an open sheet (" + (open * 100).ToString("0.#") + "% of its edges have one face), so it is made double-sided: from behind it would show nothing.");
                plan.NeedsBothSides = true;
            }
            else
                plan.Notes.Add("The model file asks for this material to be drawn from both sides (glTF doubleSided). Its mesh is closed, so its back faces are hidden anyway: it is drawn one-sided, which costs the game half the faces.");
            return true;
        }
    }
}
