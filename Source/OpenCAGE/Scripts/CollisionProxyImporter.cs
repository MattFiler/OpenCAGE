using Assimp;
using CATHODE;
using CathodeLib;
using CathodeLib.Havok;
using AlienPAK;
using OpenCAGE.ModelExport;
using System;
using System.Collections.Generic;
using System.IO;
using Matrix4x4 = System.Numerics.Matrix4x4;
using Vector3 = System.Numerics.Vector3;

namespace OpenCAGE
{
    /// <summary>
    /// New collision proxies from triangle meshes: the mesh comes from a model file or from a model the level
    /// already holds, and goes into both of the level's collision packfiles as a new hkpStaticCompoundShape
    /// (see <see cref="CollisionProxyWriter.AddMeshCollisionProxy"/>), at the same ordinal in each, or into neither.
    /// A mobile or Switch level has the one 64-bit tagfile, and that alone takes it.
    /// </summary>
    public static class CollisionProxyImporter
    {
        /// <summary>A triangle soup in collision space: metres, Y up, the game's handedness and winding.</summary>
        public sealed class MeshSource
        {
            public string Name = "";
            public List<Vector3> Positions = new List<Vector3>();
            public List<int> Indices = new List<int>();
            public int TriangleCount => Indices.Count / 3;

            public void Append(IList<Vector3> positions, IList<int> indices)
            {
                int start = Positions.Count;
                Positions.AddRange(positions);
                for (int i = 0; i < indices.Count; i++)
                    Indices.Add(indices[i] + start);
            }

            public HavokPackfile.PreviewMesh ToPreviewMesh()
            {
                HavokPackfile.PreviewMesh mesh = new HavokPackfile.PreviewMesh();
                mesh.Positions.AddRange(Positions);
                mesh.Indices.AddRange(Indices);
                mesh.ShapeCount = 1;
                return mesh;
            }
        }

        /// <summary>
        /// The triangles of a model file, through the same importer and post-processing as a model import, so
        /// they land in the game's space the same way a model would. Each mesh is placed by its node, a skinned
        /// one at the identity, and scaled from the format's unit to metres (times <paramref name="scale"/>).
        /// </summary>
        public static MeshSource FromModelFile(string path, float scale = 1f)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new FileNotFoundException("Model file not found.", path);

            Scene scene;
            using (AssimpContext importer = new AssimpContext())
                scene = importer.ImportFile(path, ModelIO.ImportPostProcessSteps);
            if (scene == null || scene.MeshCount == 0)
                throw new InvalidDataException("No mesh data found in the file. Ensure meshes are under the scene root.");

            float unitScale = ModelExporter.For(path).UnitScale;
            if (!(unitScale > 0f)) unitScale = 1f;
            if (!(scale > 0f)) scale = 1f;

            Dictionary<int, List<Matrix4x4>> placement = new Dictionary<int, List<Matrix4x4>>();
            CollectNodes(scene.RootNode, Matrix4x4.Identity, placement);

            MeshSource source = new MeshSource { Name = Path.GetFileNameWithoutExtension(path) };
            for (int m = 0; m < scene.MeshCount; m++)
            {
                Mesh mesh = scene.Meshes[m];
                if (mesh == null || mesh.VertexCount == 0 || mesh.FaceCount == 0)
                    continue;
                List<int> indices = new List<int>(mesh.FaceCount * 3);
                foreach (Face face in mesh.Faces)
                {
                    if (face.IndexCount != 3) continue;
                    indices.Add(face.Indices[0]);
                    indices.Add(face.Indices[1]);
                    indices.Add(face.Indices[2]);
                }
                if (indices.Count == 0)
                    continue;

                //A mesh goes in once per node that places it. A skinned mesh is placed by its bind matrices instead,
                //and its node often carries the file's unit conversion a second time
                List<Matrix4x4> transforms = mesh.HasBones || !placement.TryGetValue(m, out List<Matrix4x4> nodeTransforms) ? new List<Matrix4x4> { Matrix4x4.Identity } : nodeTransforms;
                foreach (Matrix4x4 transform in transforms)
                {
                    bool hasTransform = !transform.IsIdentity;
                    List<Vector3> positions = new List<Vector3>(mesh.VertexCount);
                    for (int i = 0; i < mesh.VertexCount; i++)
                    {
                        Vector3D v = mesh.Vertices[i];
                        Vector3 position = new Vector3(v.X, v.Y, v.Z);
                        if (hasTransform) position = Vector3.Transform(position, transform);
                        positions.Add(position * scale / unitScale);
                    }
                    source.Append(positions, indices);
                }
            }
            if (source.TriangleCount == 0)
                throw new InvalidDataException("The file holds no triangles.");
            return source;
        }

        private static void CollectNodes(Node node, Matrix4x4 parentTransform, Dictionary<int, List<Matrix4x4>> placement)
        {
            if (node == null) return;
            //Assimp matrices are row-vector-on-the-right; System.Numerics is the transpose of that (as ModelIO does it)
            Assimp.Matrix4x4 a = node.Transform;
            Matrix4x4 local = new Matrix4x4(
                a.A1, a.B1, a.C1, a.D1,
                a.A2, a.B2, a.C2, a.D2,
                a.A3, a.B3, a.C3, a.D3,
                a.A4, a.B4, a.C4, a.D4);
            Matrix4x4 transform = local * parentTransform;
            foreach (int meshIndex in node.MeshIndices)
            {
                if (!placement.TryGetValue(meshIndex, out List<Matrix4x4> list))
                    placement[meshIndex] = list = new List<Matrix4x4>();
                list.Add(transform);
            }
            foreach (Node child in node.Children)
                CollectNodes(child, transform, placement);
        }

        /// <summary>The triangles of one LOD of a model the level holds: every submesh, in the model's own space.</summary>
        public static MeshSource FromLod(Models.CS2.Component.LOD lod, string name = null)
        {
            if (lod == null)
                throw new ArgumentNullException(nameof(lod));
            MeshSource source = new MeshSource { Name = name ?? lod.Name ?? "" };
            foreach (Models.CS2.Component.LOD.Submesh submesh in lod.Submeshes)
                AppendSubmesh(source, submesh);
            if (source.TriangleCount == 0)
                throw new InvalidDataException("The model's LOD holds no triangles.");
            return source;
        }

        /// <summary>The triangles of a model component at its first LOD, which is what the game draws up close.</summary>
        public static MeshSource FromComponent(Models.CS2.Component component, string name = null)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));
            if (component.LODs == null || component.LODs.Count == 0)
                throw new InvalidDataException("The model component has no LODs.");
            return FromLod(component.LODs[0], name);
        }

        /// <summary>The triangles a renderable run draws (an entity's RENDERABLE_INSTANCE), in the model's space.</summary>
        public static MeshSource FromRenderableRun(IList<RenderableElements.Element> run, string name = null)
        {
            if (run == null)
                throw new ArgumentNullException(nameof(run));
            MeshSource source = new MeshSource { Name = name ?? "" };
            foreach (RenderableElements.Element element in run)
                if (element?.Model != null)
                    AppendSubmesh(source, element.Model);
            if (source.TriangleCount == 0)
                throw new InvalidDataException("The entity's renderable holds no triangles.");
            return source;
        }

        private static void AppendSubmesh(MeshSource source, Models.CS2.Component.LOD.Submesh submesh)
        {
            cMesh mesh = submesh?.ToMesh();
            if (mesh == null || mesh.Vertices == null || mesh.Indices == null || mesh.Indices.Count < 3)
                return;
            List<int> indices = new List<int>(mesh.Indices.Count);
            for (int i = 0; i < mesh.Indices.Count; i++)
                indices.Add(mesh.Indices[i]);
            source.Append(mesh.Vertices, indices);
        }

        /// <summary>
        /// Write the mesh into the level's collision files as a new proxy. A PC level has two (COLLISION.HKX and
        /// COLLISION.HKX64): both get it at the same ordinal, or neither does, each being put back to how it was if
        /// the other refuses. The mobile and Switch builds ship only the 64-bit file, a Havok 2018 tagfile, which
        /// takes it alone.
        /// </summary>
        /// <param name="userData">Retail stores the write index of the row's physics material here.</param>
        /// <param name="filterInfo">The template instance's collision type: 3 (STANDARD) for world collision, 9 (BALLISTICS) otherwise.</param>
        /// <returns>The new compound as the level's leading packfile holds it - the one the picker lists.</returns>
        public static HavokPackfile.StaticCompoundShape Import(Level level, MeshSource mesh, uint userData, uint filterInfo)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            HavokPackfile hk32 = level.CollisionHKX != null && level.CollisionHKX.Loaded ? level.CollisionHKX : null;
            HavokPackfile hk64 = level.CollisionHKX64 != null && level.CollisionHKX64.Loaded ? level.CollisionHKX64 : null;
            if (hk32 == null && hk64 == null)
                throw new InvalidOperationException("This level has no collision packfile loaded.");
            if (hk32 != null && hk64 != null && hk32.StaticCompoundShapes.Count != hk64.StaticCompoundShapes.Count)
                throw new InvalidOperationException("COLLISION.HKX and COLLISION.HKX64 hold different numbers of compounds (" + hk32.StaticCompoundShapes.Count + " / " + hk64.StaticCompoundShapes.Count + "), so a new proxy could not be given the same ordinal in both.");

            List<(HavokPackfile.StaticCompoundShape, int)> ordinals32 = hk32 != null && hk32.IsTagfile ? Ordinals(hk32) : null;
            List<(HavokPackfile.StaticCompoundShape, int)> ordinals64 = hk64 != null && hk64.IsTagfile ? Ordinals(hk64) : null;
            AppendCheckpoint cp32 = hk32?.CreateCheckpoint();
            AppendCheckpoint cp64 = hk64?.CreateCheckpoint();
            try
            {
                HavokPackfile.StaticCompoundShape p32 = hk32?.AddMeshCollisionProxy(mesh.Positions, mesh.Indices, userData, filterInfo);
                HavokPackfile.StaticCompoundShape p64 = hk64?.AddMeshCollisionProxy(mesh.Positions, mesh.Indices, userData, filterInfo);
                if (p32 != null && p64 != null && p32.ProxyIndex != p64.ProxyIndex)
                    throw new InvalidOperationException("The new proxy landed at ordinal " + p32.ProxyIndex + " in COLLISION.HKX but " + p64.ProxyIndex + " in COLLISION.HKX64.");
                if (ordinals32 != null) CheckOrdinals(hk32, ordinals32, p32, "COLLISION.HKX");
                if (ordinals64 != null) CheckOrdinals(hk64, ordinals64, p64, "COLLISION.HKX64");
                return level.Collision == hk32 ? p32 : p64 ?? p32;
            }
            catch
            {
                if (cp32 != null) hk32.RestoreCheckpoint(cp32);
                if (cp64 != null) hk64.RestoreCheckpoint(cp64);
                throw;
            }
        }

        /// <summary>
        /// The collision files an import writes, for saying so: both widths on PC, the 64-bit file alone on the
        /// mobile and Switch builds, which ship no 32-bit Havok data. Null when the level has neither loaded.
        /// </summary>
        public static string FilesWritten(Level level)
        {
            bool has32 = level?.CollisionHKX != null && level.CollisionHKX.Loaded;
            bool has64 = level?.CollisionHKX64 != null && level.CollisionHKX64.Loaded;
            if (has32 && has64) return "COLLISION.HKX and COLLISION.HKX64";
            if (has32) return "COLLISION.HKX";
            return has64 ? "COLLISION.HKX64" : null;
        }

        private static List<(HavokPackfile.StaticCompoundShape, int)> Ordinals(HavokPackfile hkx)
        {
            List<(HavokPackfile.StaticCompoundShape, int)> ordinals = new List<(HavokPackfile.StaticCompoundShape, int)>(hkx.StaticCompoundShapes.Count);
            foreach (HavokPackfile.StaticCompoundShape compound in hkx.StaticCompoundShapes)
                ordinals.Add((compound, compound.ProxyIndex));
            return ordinals;
        }

        /* A tagfile level ships one width of collision, so there is no second file for a new proxy's ordinal to agree
           with. What that agreement stands for is checked against the file itself instead, the way a COLLISION.MAP row
           finds its proxy on load (the first compound carrying its ordinal): every proxy already there still answers to
           its ordinal, so the rows and pickers holding one keep naming the same geometry, and the new one's ordinal is
           its own, so the row that takes it finds it again after a save. */
        private static void CheckOrdinals(HavokPackfile hkx, List<(HavokPackfile.StaticCompoundShape Compound, int Ordinal)> before, HavokPackfile.StaticCompoundShape created, string file)
        {
            if (created == null)
                throw new InvalidOperationException("No new proxy came back from " + file + ".");
            Dictionary<int, HavokPackfile.StaticCompoundShape> resolves = new Dictionary<int, HavokPackfile.StaticCompoundShape>();
            foreach (HavokPackfile.StaticCompoundShape compound in hkx.StaticCompoundShapes)
                if (!resolves.ContainsKey(compound.ProxyIndex))
                    resolves.Add(compound.ProxyIndex, compound);

            foreach ((HavokPackfile.StaticCompoundShape compound, int ordinal) in before)
            {
                if (compound.ProxyIndex != ordinal || !resolves.TryGetValue(ordinal, out HavokPackfile.StaticCompoundShape now) || !ReferenceEquals(now, compound))
                    throw new InvalidOperationException("Proxy " + ordinal + " in " + file + " no longer answers to its ordinal after the new one went in, so the collision rows already using it would name other geometry.");
                if (ordinal == created.ProxyIndex)
                    throw new InvalidOperationException("The new proxy was given ordinal " + ordinal + " in " + file + ", which proxy " + ordinal + " already has.");
            }
            if (!resolves.TryGetValue(created.ProxyIndex, out HavokPackfile.StaticCompoundShape found) || !ReferenceEquals(found, created))
                throw new InvalidOperationException("The new proxy's ordinal " + created.ProxyIndex + " does not lead back to it in " + file + ", so a collision row could not name it.");
        }
    }
}
