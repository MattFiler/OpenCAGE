using AlienPAK;
using Assimp;
using CATHODE;
using CathodeLib;
using OpenCAGE.Brushwork;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using Submesh = CATHODE.Models.CS2.Component.LOD.Submesh;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace OpenCAGE.Breakables
{
    /// <summary>What a ModelReference draws, read for breaking: the source triangles and, per group, the submesh and material it draws with.</summary>
    public sealed class FractureInput
    {
        public FractureSource Source = new FractureSource();
        public List<Submesh> Submeshes = new List<Submesh>();
        public List<Materials.Material> GroupMaterials = new List<Materials.Material>();
        /// <summary>The CATHODE texture coordinate indices each group's corners carry (in their order in <see cref="FractureVertex.UV"/>).</summary>
        public List<int[]> UVIndices = new List<int[]>();
        public List<bool> HasColour = new List<bool>();
        public Models.CS2 Model;
        public Vector3 Min, Max;
        public int Triangles;
        public List<string> Notes = new List<string>();
        public Vector3 Size => Max - Min;
    }

    /// <summary>
    /// The model side of breaking: reads what a ModelReference draws (its first LOD) as a <see cref="FractureSource"/>, and builds each
    /// piece as a model through <see cref="ModelIO.BuildCS2"/>, every submesh described by the one it was cut from. The material is set on
    /// the model, as the ModelReference 'material' parameter is not applied in game.
    /// </summary>
    public static class FractureModels
    {
        public const string Folder = "DEBRIS";

        /// <summary>Read what a run draws. Throws with the reason when it cannot be broken.</summary>
        public static FractureInput Read(Level level, List<RenderableElements.Element> run)
        {
            if (run == null || run.Count == 0 || !run.Any(o => o?.Model != null))
                throw new InvalidOperationException("It draws nothing to break.");
            FractureInput input = new FractureInput();

            //Where each submesh lives: its model, component and LOD; only each component's first LOD the run draws is broken
            Dictionary<Submesh, (Models.CS2 model, int component, int lod)> where = new Dictionary<Submesh, (Models.CS2, int, int)>();
            HashSet<Submesh> wanted = new HashSet<Submesh>(run.Where(o => o?.Model != null).Select(o => o.Model));
            foreach (Models.CS2 model in level.Models.Entries)
            {
                if (model == null) continue;
                for (int c = 0; c < model.Components.Count; c++)
                    for (int l = 0; l < model.Components[c].LODs.Count; l++)
                        foreach (Submesh s in model.Components[c].LODs[l].Submeshes)
                            if (s != null && wanted.Contains(s) && !where.ContainsKey(s)) where[s] = (model, c, l);
            }
            Dictionary<(Models.CS2, int), int> firstLod = new Dictionary<(Models.CS2, int), int>();
            foreach (var w in where.Values)
                firstLod[(w.model, w.component)] = firstLod.TryGetValue((w.model, w.component), out int l) ? Math.Min(l, w.lod) : w.lod;
            input.Model = where.Values.Select(o => o.model).FirstOrDefault();

            Dictionary<Materials.Material, Func<Vector2, float>> luminance = new Dictionary<Materials.Material, Func<Vector2, float>>();
            Vector3 mn = new Vector3(float.MaxValue), mx = new Vector3(float.MinValue);
            HashSet<Submesh> seen = new HashSet<Submesh>();
            int lodsLeftOut = 0;
            foreach (RenderableElements.Element element in run)
            {
                Submesh submesh = element?.Model;
                if (submesh == null || !seen.Add(submesh)) continue;
                if (where.TryGetValue(submesh, out var at) && at.lod != firstLod[(at.model, at.component)]) { lodsLeftOut++; continue; }
                if (submesh.Bones != null && submesh.Bones.Count != 0)
                    throw new InvalidOperationException("It is a skinned model (a character's): only rigid models can be broken.");
                cMesh mesh = submesh.ToMesh();
                if (mesh?.Vertices == null || mesh.Indices == null || mesh.Indices.Count < 3) continue;

                Materials.Material material = element.Material ?? submesh.Material;
                int[] uvIndices = ReferenceEquals(submesh.VertexFormatFull, null) ? new int[0] : submesh.VertexFormatFull.Attributes
                    .SelectMany(stream => stream).Where(a => a.Usage == Models.VertexFormat.Usage.TexCoord).Select(a => a.Index).Distinct().OrderBy(i => i).ToArray();
                bool colour = mesh.Colours != null && mesh.Colours.Count == mesh.Vertices.Count;
                int group = input.Source.Groups.Count;
                Func<Vector2, float> lum = null;
                if (material != null && !luminance.TryGetValue(material, out lum))
                    luminance[material] = lum = Luminance(material);
                input.Source.Groups.Add(new FractureGroup()
                {
                    Name = (material != null ? level.Materials.GetMaterialName(material) : null) ?? ("submesh " + group),
                    UVChannels = uvIndices.Length,
                    Overlay = CollisionMatch.IsSeeThrough(material),
                    Luminance = lum,
                });
                input.Submeshes.Add(submesh);
                input.GroupMaterials.Add(material);
                input.UVIndices.Add(uvIndices);
                input.HasColour.Add(colour);

                FractureVertex Corner(int i)
                {
                    FractureVertex v = new FractureVertex() { P = mesh.Vertices[i], Group = group, UV = new Vector2[uvIndices.Length], C = Vector4.One };
                    v.N = mesh.Normals != null && i < mesh.Normals.Count ? mesh.Normals[i] : Vector3.UnitY;
                    if (v.N.LengthSquared() > 1e-12f) v.N = Vector3.Normalize(v.N);
                    for (int u = 0; u < uvIndices.Length; u++)
                    {
                        int channel = uvIndices[u];
                        if (channel < mesh.UVs.Length && mesh.UVs[channel] != null && i < mesh.UVs[channel].Count) v.UV[u] = mesh.UVs[channel][i];
                    }
                    if (colour) { Color c = mesh.Colours[i]; v.C = new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f); }
                    return v;
                }
                for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
                {
                    int a = mesh.Indices[i], b = mesh.Indices[i + 1], c = mesh.Indices[i + 2];
                    if (a >= mesh.Vertices.Count || b >= mesh.Vertices.Count || c >= mesh.Vertices.Count || a == b || b == c || a == c) continue;
                    FractureTriangle t = new FractureTriangle() { A = Corner(a), B = Corner(b), C = Corner(c), Group = group, Kind = FractureTriangleKind.Body };
                    input.Source.Triangles.Add(t);
                    foreach (Vector3 p in new[] { t.A.P, t.B.P, t.C.P }) { mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }
                }
            }
            if (input.Source.Triangles.Count == 0)
                throw new InvalidOperationException("Its model holds no triangles.");
            if (input.Source.Groups.All(o => o.Overlay))
                throw new InvalidOperationException("It draws only see-through or effect surfaces (glass, decals): nothing solid to break.");
            if (lodsLeftOut != 0) input.Notes.Add(lodsLeftOut + " submesh(es) of its lower LODs left out: the pieces are cut from what it draws up close.");
            input.Min = mn;
            input.Max = mx;
            input.Triangles = input.Source.Triangles.Count;
            return input;
        }

        /// <summary>
        /// Luminance (0-255) of a material's diffuse texture at a texture coordinate, read from a copy at most 256 texels across; null
        /// when it has none that can be read (the interior is then projected instead of dark).
        /// </summary>
        private static Func<Vector2, float> Luminance(Materials.Material material)
        {
            try
            {
                Textures.TEX4 texture = MaterialApplier.GetDiffuseTexture(material);
                if (texture == null) return null;
                using (Bitmap full = texture.ToBitmap())
                {
                    if (full == null || full.Width == 0 || full.Height == 0) return null;
                    int w = Math.Min(256, full.Width), h = Math.Min(256, full.Height);
                    using (Bitmap small = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                    {
                        using (Graphics g = Graphics.FromImage(small))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                            g.DrawImage(full, new Rectangle(0, 0, w, h));
                        }
                        BitmapData data = small.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                        byte[] bytes = new byte[data.Stride * h];
                        Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                        int stride = data.Stride;
                        small.UnlockBits(data);
                        float[] lum = new float[w * h];
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++)
                            {
                                int o = y * stride + x * 4;
                                lum[y * w + x] = 0.0722f * bytes[o] + 0.7152f * bytes[o + 1] + 0.2126f * bytes[o + 2];
                            }
                        return uv =>
                        {
                            int x = Clamp((int)(Frac(uv.X) * w), w), y = Clamp((int)(Frac(uv.Y) * h), h);
                            return lum[y * w + x];
                        };
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private static float Frac(float x) => x - (float)Math.Floor(x);
        private static int Clamp(int x, int n) => Math.Max(0, Math.Min(n - 1, x));

        /// <summary>A name for a piece's model: DEBRIS\&lt;stem&gt;\&lt;stem&gt;_NN.CS2, unique in the level.</summary>
        public static string PieceModelName(Level level, string stem, int index, IEnumerable<string> alsoTaken = null)
        {
            string name = Folder + "\\" + stem + "\\" + stem + "_" + (index + 1).ToString("00") + ".CS2";
            return AssetName.MakeUnique(name, level.Models.Entries.Select(o => o?.Name).Where(o => o != null).Concat(alsoTaken ?? Enumerable.Empty<string>()));
        }

        /// <summary>
        /// A piece as a model (not yet in the level): one component, one LOD, one submesh per group it holds (cut faces and skin go in
        /// the group whose surface they close), each described by the submesh it was cut from. Throws with the reason when it cannot be built.
        /// </summary>
        public static Models.CS2 BuildPieceModel(FractureInput input, FracturePiece piece, string name, out List<string> warnings)
        {
            List<int> groups = piece.Triangles.Select(t => t.Group).Distinct().OrderBy(g => g).ToList();
            if (groups.Count > RenderableElements.MaxElementsPerInstance)
                throw new InvalidOperationException("A piece can draw with at most " + RenderableElements.MaxElementsPerInstance + " materials (it has " + groups.Count + ").");
            Scene scene = new Scene() { RootNode = new Node("DEBRIS") };
            List<ModelIO.SubmeshMetadata> described = new List<ModelIO.SubmeshMetadata>();
            for (int gi = 0; gi < groups.Count; gi++)
            {
                int g = groups[gi];
                int uvCount = input.UVIndices[g].Length;
                bool colour = input.HasColour[g];
                Mesh mesh = new Mesh("DEBRIS_" + gi, PrimitiveType.Triangle) { MaterialIndex = gi };
                Dictionary<VertexKey, int> welded = new Dictionary<VertexKey, int>();
                int Add(FractureVertex v)
                {
                    VertexKey key = new VertexKey(v, uvCount);
                    if (welded.TryGetValue(key, out int at)) return at;
                    at = mesh.Vertices.Count;
                    welded[key] = at;
                    mesh.Vertices.Add(new Vector3D(v.P.X, v.P.Y, v.P.Z));
                    mesh.Normals.Add(new Vector3D(v.N.X, v.N.Y, v.N.Z));
                    for (int u = 0; u < uvCount; u++)
                    {
                        Vector2 uv = v.UV != null && u < v.UV.Length ? v.UV[u] : Vector2.Zero;
                        mesh.TextureCoordinateChannels[u].Add(new Vector3D(uv.X, uv.Y, 0));
                    }
                    if (colour) mesh.VertexColorChannels[0].Add(new Color4D(v.C.X, v.C.Y, v.C.Z, v.C.W));
                    return at;
                }
                foreach (FractureTriangle t in piece.Triangles.Where(o => o.Group == g))
                {
                    int a = Add(t.A), b = Add(t.B), c = Add(t.C);
                    if (a == b || b == c || a == c) continue;
                    mesh.Faces.Add(new Face(new[] { a, b, c }));
                }
                for (int u = 0; u < uvCount; u++) mesh.UVComponentCount[u] = 2;
                scene.Materials.Add(new Assimp.Material() { Name = input.Source.Groups[g].Name ?? "DEBRIS" });
                scene.Meshes.Add(mesh);
                scene.RootNode.MeshIndices.Add(gi);

                //Described by the submesh it was cut from: its vertex format (what its material's shader reads), flags and LOD ranges
                ModelIO.SubmeshMetadata metadata = ModelIO.DescribeSubmesh(input.Submeshes[g]);
                if (metadata != null)
                {
                    metadata.CollisionProxyIndex = -1;
                    metadata.VertexScale = 0;
                    metadata.MinBounds = null;
                    metadata.MaxBounds = null;
                }
                described.Add(metadata);
            }

            //Already in the game's space and in metres: nothing for the import to turn or scale
            ModelIO.ImportPlan plan = ModelIO.CreateImportPlan(scene, null, "DEBRIS");
            plan.UnitScale = 1.0f;
            plan.Scale = 1.0f;
            foreach (ModelIO.PlannedSubmesh submesh in plan.AllSubmeshes())
            {
                int gi = scene.Meshes[submesh.MeshIndex].MaterialIndex;
                submesh.Material = input.GroupMaterials[groups[gi]] ?? Singleton.FallbackMaterial;
                submesh.Metadata = described[gi];
            }
            Models.CS2 model = ModelIO.BuildCS2(scene, plan, wanted => null, Singleton.FallbackMaterial, out warnings);
            if (model.Components.Count != 1 || model.Components[0].LODs.Count != 1 || model.Components[0].LODs[0].Submeshes.Count == 0)
                throw new InvalidOperationException("The piece could not be made into a model" + (warnings.Count != 0 ? ": " + string.Join(" ", warnings.Take(5)) : "."));
            model.Name = name;
            return model;
        }

        /// <summary>The submeshes a piece's model draws (its one component's LOD 0).</summary>
        public static List<Submesh> Submeshes(Models.CS2 model) => model?.Components.FirstOrDefault()?.LODs.FirstOrDefault()?.Submeshes ?? new List<Submesh>();

        /// <summary>The run a piece's ModelReference draws its model with: every submesh with its own material.</summary>
        public static List<RenderableElements.Element> Run(Models.CS2 model) =>
            Submeshes(model).Select(o => new RenderableElements.Element() { Model = o, Material = o.Material }).ToList();

        /// <summary>A piece's triangles as a mesh for a hull or a collision proxy (model space about its pivot).</summary>
        public static CollisionProxyImporter.MeshSource MeshOf(FracturePiece piece, string name)
        {
            CollisionProxyImporter.MeshSource mesh = new CollisionProxyImporter.MeshSource() { Name = name ?? "" };
            foreach (FractureTriangle t in piece.Triangles)
            {
                int start = mesh.Positions.Count;
                mesh.Positions.Add(t.A.P); mesh.Positions.Add(t.B.P); mesh.Positions.Add(t.C.P);
                mesh.Indices.Add(start); mesh.Indices.Add(start + 1); mesh.Indices.Add(start + 2);
            }
            return mesh;
        }

        /// <summary>Corners compared by value: two triangles' corners that came from the same source vertex or the same cut point weld.</summary>
        private struct VertexKey : IEquatable<VertexKey>
        {
            private readonly Vector3 _p, _n;
            private readonly Vector4 _c;
            private readonly Vector2 _u0, _u1, _u2, _u3;

            public VertexKey(FractureVertex v, int uvCount)
            {
                _p = v.P; _n = v.N; _c = v.C;
                _u0 = uvCount > 0 && v.UV != null && v.UV.Length > 0 ? v.UV[0] : Vector2.Zero;
                _u1 = uvCount > 1 && v.UV != null && v.UV.Length > 1 ? v.UV[1] : Vector2.Zero;
                _u2 = uvCount > 2 && v.UV != null && v.UV.Length > 2 ? v.UV[2] : Vector2.Zero;
                _u3 = uvCount > 3 && v.UV != null && v.UV.Length > 3 ? v.UV[3] : Vector2.Zero;
            }

            public bool Equals(VertexKey o) => _p == o._p && _n == o._n && _c == o._c && _u0 == o._u0 && _u1 == o._u1 && _u2 == o._u2 && _u3 == o._u3;
            public override bool Equals(object obj) => obj is VertexKey k && Equals(k);
            public override int GetHashCode()
            {
                unchecked
                {
                    int h = _p.GetHashCode();
                    h = h * 31 + _n.GetHashCode();
                    h = h * 31 + _u0.GetHashCode();
                    h = h * 31 + _u1.GetHashCode();
                    return h * 31 + _c.GetHashCode();
                }
            }
        }
    }
}
