using AlienPAK;
using Assimp;
using CATHODE;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace OpenCAGE.Cinematics
{
    /// <summary>
    /// Bakes a character's pose at one frame of a clip into a static model (skinned vertices run through their bones, weights dropped)
    /// for the ordinary import. A frozen pose rides a moving vehicle exactly; an animated body trails a keyed vehicle by about 2 frames.
    /// Space "marker" is where the game draws a character playing the clip at a marker (each bone's pose times the inverse of
    /// REFERENCE_ROOT at that frame, untracked channels at engine defaults); "preview" is what the Animation Preview window shows.
    /// </summary>
    public static class PoseBake
    {
        public sealed class Options
        {
            public Models.CS2 Model;
            public Skeleton Skeleton;
            public CathodeLib.Animation.ClipReference Clip;
            public int Frame;
            /// <summary>marker (default) or preview.</summary>
            public string Space = "marker";
            /// <summary>preview space: whether the clip carries the body away from where it started (Follow) or holds it on the spot (Ignore).</summary>
            public CathodeLib.Animation.RootMotion PreviewRoot = CathodeLib.Animation.RootMotion.Ignore;
            public Retargeter Retarget;
            /// <summary>The LOD to bake (0 = the finest).</summary>
            public int Lod;
            /// <summary>Keep hit-collision parts (COL_ names): off, as they are not drawn.</summary>
            public bool KeepCollision;
            /// <summary>Submeshes to leave out, by "Cc_Ss" tag or material name.</summary>
            public HashSet<string> Exclude;
            /// <summary>The written materials' names start with this (they become the generated materials' names).</summary>
            public string MaterialPrefix = "POSED";
        }

        public sealed class Result
        {
            public Scene Scene;
            public int Meshes, Vertices, Triangles;
            public readonly List<string> Skipped = new List<string>();
            public readonly List<string> Notes = new List<string>();
            /// <summary>Mean distance from each well-weighted bone's joint to its vertices' weighted centroid, at rest and posed (posed should be about the same as rest).</summary>
            public float RestFit, PosedFit;
            public int FitBones;
            public Vector3 Min, Max;
            public readonly List<string> Materials = new List<string>();
            public readonly List<string> Textures = new List<string>();
            public string File;
        }

        /// <summary>The frame's bone transforms in the output space (mesh space for preview, marker space for marker).</summary>
        public static List<Matrix4x4> Pose(Options o)
        {
            if (o.Skeleton == null) throw new ArgumentException("No rig to pose on.");
            if (o.Clip?.Animation == null) throw new ArgumentException("No clip to take the pose from.");
            int frames = o.Clip.Animation.FrameCount;
            if (o.Frame < 0 || o.Frame >= Math.Max(1, frames)) throw new ArgumentException("Frame " + o.Frame + " is outside the clip (0-" + (frames - 1) + ").");
            string space = (o.Space ?? "marker").ToLowerInvariant();
            if (space == "preview")
            {
                List<Matrix4x4> pose = CathodeLib.Animation.SampleModelPose(o.Clip, o.Skeleton, o.Frame, o.PreviewRoot, o.Retarget);
                if (pose == null) throw new ArgumentException("The clip does not fit the rig '" + o.Skeleton.Name + "' bone for bone.");
                return pose;
            }
            if (space != "marker") throw new ArgumentException("space is 'marker' or 'preview'.");
            int reference = CathodeLib.Animation.ReferenceBone(o.Skeleton);
            if (reference < 0) throw new ArgumentException("The rig '" + o.Skeleton.Name + "' has no REFERENCE_ROOT bone, so there is no marker to place the pose against: use space 'preview'.");
            List<HavokPackfile.SampledTransform> bones = CathodeLib.Animation.SampleBones(o.Clip, o.Skeleton, o.Frame, CathodeLib.Animation.RootMotion.Authored, o.Retarget, CathodeLib.Animation.UntrackedChannels.EngineDefaults);
            if (bones == null || bones.Count != o.Skeleton.Bones.Count) throw new ArgumentException("The clip does not fit the rig '" + o.Skeleton.Name + "' bone for bone.");
            Matrix4x4[] m = new Matrix4x4[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                Matrix4x4 local = Matrix4x4.CreateScale(bones[i].Scale) * Matrix4x4.CreateFromQuaternion(System.Numerics.Quaternion.Normalize(bones[i].Rotation)) * Matrix4x4.CreateTranslation(bones[i].Translation);
                int p = o.Skeleton.Bones[i].ParentIndex;
                m[i] = p >= 0 && p < i ? local * m[p] : local * Skeleton.ToMeshSpace;
            }
            if (!Matrix4x4.Invert(m[reference], out Matrix4x4 inverse)) throw new ArgumentException("REFERENCE_ROOT cannot be inverted at that frame.");
            return m.Select(x => x * inverse).ToList();
        }

        /// <summary>Skinning matrices: inverse(bind) x pose, per bone.</summary>
        public static Matrix4x4[] Skinning(Options o, out List<Matrix4x4> pose)
        {
            pose = Pose(o);
            List<Matrix4x4> bind = o.Skeleton.GetBindPose();
            Matrix4x4[] skin = new Matrix4x4[pose.Count];
            for (int i = 0; i < pose.Count; i++)
                skin[i] = i < bind.Count && Matrix4x4.Invert(bind[i], out Matrix4x4 inverse) ? inverse * pose[i] : Matrix4x4.Identity;
            return skin;
        }

        private static bool LooksLikeCollision(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string last = name.Substring(name.LastIndexOfAny(new[] { '\\', '/' }) + 1);
            return last.StartsWith("COL_", StringComparison.OrdinalIgnoreCase) || last.IndexOf("_COL_", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// The posed model as an Assimp scene for <paramref name="outFile"/>'s format (units and UV flip from the writer's table), its
        /// materials naming textures written beside it in "&lt;stem&gt; Textures". Nothing is written but the textures.
        /// </summary>
        public static Result Build(Options o, string outFile)
        {
            if (o.Model == null) throw new ArgumentException("No model to pose.");
            Matrix4x4[] skin = Skinning(o, out List<Matrix4x4> pose);
            List<Matrix4x4> bind = o.Skeleton.GetBindPose();
            int n = pose.Count;
            Result result = new Result() { File = outFile };
            if (o.Clip.Additive)
                result.Notes.Add("'" + o.Clip.Name + "' is an additive clip: in game it lays on top of another animation, so on its own it poses the model over the bind pose (much like the bind pose itself). Pick a full-body clip for a pose.");
            string dir = Path.GetDirectoryName(outFile), stem = Path.GetFileNameWithoutExtension(outFile);
            float unitScale = ModelIO.FormatUnitScale(outFile);
            bool flipUVs = ModelIO.FormatFlipsUVs(outFile);
            Scene scene = new Scene { RootNode = new Node(stem) };
            Dictionary<Materials.Material, int> materialIndex = new Dictionary<Materials.Material, int>();
            Vector3[] restSum = new Vector3[n], poseSum = new Vector3[n];
            float[] weightSum = new float[n];
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);

            for (int c = 0; c < o.Model.Components.Count; c++)
            {
                Models.CS2.Component component = o.Model.Components[c];
                if (component.LODs.Count == 0) continue;
                int lodIndex = Math.Min(o.Lod, component.LODs.Count - 1);
                Models.CS2.Component.LOD lod = component.LODs[lodIndex];
                for (int s = 0; s < lod.Submeshes.Count; s++)
                {
                    Models.CS2.Component.LOD.Submesh sub = lod.Submeshes[s];
                    string tag = ModelIO.SubmeshTag(c, lodIndex, s);
                    string what = tag + " (" + (sub.Material?.Name ?? "no material") + ")";
                    if (!o.KeepCollision && (LooksLikeCollision(lod.Name) || LooksLikeCollision(sub.Material?.Name)))
                    {
                        result.Skipped.Add(what + ": hit collision, not drawn");
                        continue;
                    }
                    if (o.Exclude != null && (o.Exclude.Contains(tag) || (sub.Material?.Name != null && o.Exclude.Contains(sub.Material.Name))))
                    {
                        result.Skipped.Add(what + ": excluded");
                        continue;
                    }
                    if (sub.Data == null || sub.Data.Length == 0) { result.Skipped.Add(what + ": no geometry"); continue; }
                    cMesh mesh = ModelUtility.ToMesh(sub);
                    if (mesh.Vertices.Count == 0) { result.Skipped.Add(what + ": no vertices"); continue; }
                    bool skinned = mesh.BoneIndexes.Count == mesh.Vertices.Count && mesh.BoneWeights.Count == mesh.Vertices.Count && sub.Bones.Count != 0;
                    if (!skinned)
                    {
                        result.Skipped.Add(what + ": not skinned (no weights), so the pose cannot move it");
                        continue;
                    }
                    for (int v = 0; v < mesh.Vertices.Count; v++)
                    {
                        Vector4 bi = mesh.BoneIndexes[v], bw = mesh.BoneWeights[v];
                        float[] idx = { bi.X, bi.Y, bi.Z, bi.W }, w = { bw.X, bw.Y, bw.Z, bw.W };
                        float total = 0f;
                        for (int k = 0; k < 4; k++)
                        {
                            int local = (int)Math.Round(idx[k]);
                            if (w[k] > 0 && local >= 0 && local < sub.Bones.Count && sub.Bones[local] >= 0 && sub.Bones[local] < n) total += w[k];
                        }
                        Vector3 p = Vector3.Zero, nrm = Vector3.Zero, tan = Vector3.Zero;
                        if (total <= 0f) p = mesh.Vertices[v];
                        for (int k = 0; k < 4 && total > 0f; k++)
                        {
                            int local = (int)Math.Round(idx[k]);
                            if (w[k] <= 0 || local < 0 || local >= sub.Bones.Count) continue;
                            int b = sub.Bones[local];
                            if (b < 0 || b >= n) continue;
                            float wk = w[k] / total;
                            p += wk * Vector3.Transform(mesh.Vertices[v], skin[b]);
                            if (mesh.Normals.Count == mesh.Vertices.Count) nrm += wk * Vector3.TransformNormal(mesh.Normals[v], skin[b]);
                            if (mesh.Tangents.Count == mesh.Vertices.Count) tan += wk * Vector3.TransformNormal(new Vector3(mesh.Tangents[v].X, mesh.Tangents[v].Y, mesh.Tangents[v].Z), skin[b]);
                            restSum[b] += wk * mesh.Vertices[v];
                            weightSum[b] += wk;
                        }
                        for (int k = 0; k < 4 && total > 0f; k++)
                        {
                            int local = (int)Math.Round(idx[k]);
                            if (w[k] <= 0 || local < 0 || local >= sub.Bones.Count) continue;
                            int b = sub.Bones[local];
                            if (b < 0 || b >= n) continue;
                            poseSum[b] += (w[k] / total) * p;
                        }
                        mesh.Vertices[v] = p;
                        min = Vector3.Min(min, p);
                        max = Vector3.Max(max, p);
                        if (mesh.Normals.Count == mesh.Vertices.Count && nrm.LengthSquared() > 0) mesh.Normals[v] = Vector3.Normalize(nrm);
                        if (mesh.Tangents.Count == mesh.Vertices.Count && tan.LengthSquared() > 0)
                        {
                            Vector3 t3 = Vector3.Normalize(tan);
                            mesh.Tangents[v] = new Vector4(t3.X, t3.Y, t3.Z, mesh.Tangents[v].W);
                        }
                    }
                    mesh.BoneIndexes.Clear();
                    mesh.BoneWeights.Clear();
                    if (sub.Material != null && !materialIndex.ContainsKey(sub.Material)) materialIndex[sub.Material] = materialIndex.Count;
                    Mesh assimp = ModelIO.ToAssimpMesh(mesh, tag, sub.Material != null ? materialIndex[sub.Material] : 0, flipUVs, out int[] _, unitScale);
                    scene.Meshes.Add(assimp);
                    Node node = new Node(tag);
                    node.MeshIndices.Add(scene.Meshes.Count - 1);
                    scene.RootNode.Children.Add(node);
                    result.Meshes++;
                    result.Vertices += mesh.Vertices.Count;
                    result.Triangles += mesh.Indices.Count / 3;
                }
            }
            if (result.Meshes == 0)
                throw new ArgumentException(o.Model.Name + " has nothing skinned to pose" + (result.Skipped.Count != 0 ? ": " + string.Join("; ", result.Skipped.Take(6)) : "") + ".");

            //How far each well-weighted bone's vertices sit from its joint, at rest and posed: a bad pose or the wrong rig shows here
            float rest = 0f, posed = 0f;
            for (int b = 0; b < n; b++)
            {
                if (weightSum[b] < 50f || b >= bind.Count) continue;
                rest += Vector3.Distance(restSum[b] / weightSum[b], bind[b].Translation);
                posed += Vector3.Distance(poseSum[b] / weightSum[b], pose[b].Translation);
                result.FitBones++;
            }
            if (result.FitBones != 0) { result.RestFit = rest / result.FitBones; result.PosedFit = posed / result.FitBones; }
            if (result.FitBones != 0 && result.PosedFit > result.RestFit * 1.5f + 0.02f)
                result.Notes.Add("The posed mesh sits " + Math.Round(result.PosedFit * 100, 1) + " cm from its bones on average against " + Math.Round(result.RestFit * 100, 1) + " cm at rest: the model may not be skinned to '" + o.Skeleton.Name + "'.");
            result.Min = min;
            result.Max = max;

            //Materials, with their maps beside the file (DDS, as the Model Editor's export writes them)
            foreach (KeyValuePair<Materials.Material, int> kv in materialIndex.OrderBy(x => x.Value))
            {
                string Texture(Textures.TEX4 tex)
                {
                    if (tex == null) return null;
                    //Named after the character's own texture (not this bake), so every pose baked from one model shares one imported copy:
                    //the importer reuses a level texture of the same name and size
                    string leaf = Path.GetFileNameWithoutExtension(Path.GetFileName((tex.Name ?? "TEX").Replace('\\', '/')));
                    string file = Path.Combine(dir, stem + " Textures", "POSED_" + leaf + ".dds");
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    byte[] dds = tex.ToDDS();
                    if (dds == null || dds.Length == 0) return null;
                    File.WriteAllBytes(file, dds);
                    result.Textures.Add(file);
                    return file;
                }
                Assimp.Material material = kv.Key.ToAssimpMaterial(kv.Value, Texture(MaterialApplier.GetDiffuseTexture(kv.Key)), Texture(MaterialApplier.GetNormalMapTexture(kv.Key)), Texture(MaterialApplier.GetSpecularMapTexture(kv.Key)));
                material.Name = o.MaterialPrefix + "_" + Path.GetFileName((kv.Key.Name ?? "MAT").Replace('\\', '/'));
                scene.Materials.Add(material);
                result.Materials.Add(material.Name);
            }
            if (scene.Materials.Count == 0) scene.Materials.Add(new Assimp.Material() { Name = o.MaterialPrefix + "_DEFAULT" });
            result.Scene = scene;
            return result;
        }

        /// <summary><see cref="Build"/>, then the file written.</summary>
        public static Result Write(Options o, string outFile)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outFile));
            Result result = Build(o, outFile);
            OpenCAGE.ModelExport.ModelExporter.Write(result.Scene, outFile);
            return result;
        }
    }
}
