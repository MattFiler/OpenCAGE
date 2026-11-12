using AlienPAK;
using Assimp;
using CATHODE;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Linq;
using Submesh = CATHODE.Models.CS2.Component.LOD.Submesh;
using static CATHODE.Models;
using static CATHODE.Models.CS2.Component.LOD;

namespace OpenCAGE.Brushwork
{
    /// <summary>
    /// A brush's model: its triangles grouped by material, one submesh each, in one component and one LOD - built by handing an
    /// in-memory scene to the model import (ModelIO.BuildCS2), so it is quantised, laid out and capped exactly as an imported
    /// model is. Rebuilt in place on an edit: the same model, the same submesh objects with their contents replaced, so every
    /// REDS run and resource holding them stays valid.
    /// </summary>
    public static class BrushModelBuilder
    {
        public const string Folder = "BRUSHES";

        /// <summary>A model name for a brush drawn by this entity: BRUSHES\BRUSH_&lt;id&gt;.CS2, made unique in the level.</summary>
        public static string NewModelName(Level level, CATHODE.Scripting.ShortGuid entity)
        {
            string name = Folder + "\\BRUSH_" + entity.AsUInt32.ToString("X8") + ".CS2";
            return AssetName.MakeUnique(name, level.Models.Entries.Select(o => o?.Name).Where(o => o != null));
        }

        /// <summary>Whether a model is one brushes make.</summary>
        public static bool IsBrushModel(string name) => name != null && name.StartsWith(Folder + "\\", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The brush built as a model (not yet in the level). <paramref name="resolve"/> gives the level material for a face's
        /// material name (null name = the brush default). Throws with the reason when it cannot be built.
        /// </summary>
        public static Models.CS2 Build(BrushDefinition brush, string name, Func<string, Materials.Material> resolve, out List<string> warnings)
        {
            BrushGeometry geometry = BrushGeometry.Build(brush);
            if (geometry.Polygons.Count == 0)
                throw new InvalidOperationException("The brush has no faces to draw.");

            //One mesh per material, faces in order
            List<(Materials.Material material, List<BrushGeometry.Polygon> polygons)> groups = new List<(Materials.Material, List<BrushGeometry.Polygon>)>();
            foreach (BrushGeometry.Polygon polygon in geometry.Polygons)
            {
                Materials.Material material = resolve(polygon.Texture?.Material);
                int at = groups.FindIndex(o => ReferenceEquals(o.material, material));
                if (at < 0)
                {
                    groups.Add((material, new List<BrushGeometry.Polygon>()));
                    at = groups.Count - 1;
                }
                groups[at].polygons.Add(polygon);
            }
            if (groups.Count > RenderableElements.MaxElementsPerInstance)
                throw new InvalidOperationException("A brush can draw with at most " + RenderableElements.MaxElementsPerInstance + " materials (it has " + groups.Count + ").");

            Scene scene = new Scene() { RootNode = new Node("BRUSH") };
            for (int g = 0; g < groups.Count; g++)
            {
                List<BrushGeometry.Vertex> vertices = new List<BrushGeometry.Vertex>();
                List<int> indices = new List<int>();
                foreach (BrushGeometry.Polygon polygon in groups[g].polygons)
                    geometry.Triangulate(polygon, brush.UvOrigin, vertices, indices);

                Mesh mesh = new Mesh("BRUSH_" + g, PrimitiveType.Triangle) { MaterialIndex = g };
                foreach (BrushGeometry.Vertex v in vertices)
                {
                    mesh.Vertices.Add(new Vector3D(v.Position.X, v.Position.Y, v.Position.Z));
                    mesh.Normals.Add(new Vector3D(v.Normal.X, v.Normal.Y, v.Normal.Z));
                    mesh.Tangents.Add(new Vector3D(v.Tangent.X, v.Tangent.Y, v.Tangent.Z));
                    mesh.BiTangents.Add(new Vector3D(v.Bitangent.X, v.Bitangent.Y, v.Bitangent.Z));
                    mesh.TextureCoordinateChannels[0].Add(new Vector3D(v.UV.X, v.UV.Y, 0));
                }
                mesh.UVComponentCount[0] = 2;
                for (int i = 0; i + 2 < indices.Count; i += 3)
                    mesh.Faces.Add(new Face(new[] { indices[i], indices[i + 1], indices[i + 2] }));
                scene.Materials.Add(new Assimp.Material() { Name = groups[g].material?.Name ?? "BRUSH" });
                scene.Meshes.Add(mesh);
                scene.RootNode.MeshIndices.Add(g);
            }

            //Already in the game's space and in metres: nothing for the import to turn or scale
            ModelIO.ImportPlan plan = ModelIO.CreateImportPlan(scene, null, "BRUSH");
            plan.UnitScale = 1.0f;
            plan.Scale = 1.0f;
            List<ModelIO.PlannedSubmesh> planned = plan.AllSubmeshes().ToList();
            foreach (ModelIO.PlannedSubmesh submesh in planned)
                submesh.Material = groups[scene.Meshes[submesh.MeshIndex].MaterialIndex].material;

            Models.CS2 model = ModelIO.BuildCS2(scene, plan, wanted => null, Singleton.FallbackMaterial, out warnings);
            if (model.Components.Count != 1 || model.Components[0].LODs.Count != 1)
                throw new InvalidOperationException("The brush could not be made into a model" + (warnings.Count != 0 ? ": " + string.Join(" ", warnings.Take(5)) : "."));
            model.Name = name;
            return model;
        }

        /// <summary>The submeshes the model draws (its one component's LOD 0).</summary>
        public static List<Submesh> Submeshes(Models.CS2 model) => model?.Components.FirstOrDefault()?.LODs.FirstOrDefault()?.Submeshes;

        /// <summary>
        /// Put a freshly built model's contents into the one the level holds: submesh for submesh, field by field, so the objects
        /// every run and resource holds stay the ones the level saves. Extra new submeshes are appended; old ones past the new
        /// count are left as they were (nothing draws them now, but REDS may still name them).
        /// </summary>
        public static void CopyInto(Models.CS2 built, Models.CS2 target)
        {
            List<Submesh> from = Submeshes(built);
            if (target.Components.Count == 0) target.Components.Add(new Models.CS2.Component());
            Models.CS2.Component component = target.Components[0];
            if (component.LODs.Count == 0) component.LODs.Add(new Models.CS2.Component.LOD(built.Components[0].LODs[0].Name ?? ""));
            List<Submesh> into = component.LODs[0].Submeshes;
            for (int i = 0; i < from.Count; i++)
            {
                if (i < into.Count) SubmeshState.Of(from[i]).ApplyTo(into[i]);
                else into.Add(from[i]);
            }
        }

        /// <summary>The run a ModelReference draws the brush's model with: its submeshes in order, each with its own material (the first <paramref name="count"/>).</summary>
        public static List<RenderableElements.Element> Run(Models.CS2 model, int count)
        {
            return Submeshes(model).Take(count).Select(o => new RenderableElements.Element() { Model = o, Material = o.Material }).ToList();
        }

        /// <summary>Everything about a submesh a rebuild changes, to put it back.</summary>
        public sealed class SubmeshState
        {
            public System.Numerics.Vector3 MinBounds, MaxBounds;
            public float MinLODRange, MaxLODRange;
            public RenderingFlag RenderFlags;
            public Materials.Material Material;
            public int CollisionProxyIndex;
            public VertexFormat VertexFormatFull, VertexFormatPartial;
            public int VertexScale, VertexCount, IndexCount;
            public List<int> Bones;
            public byte[] Data;

            public static SubmeshState Of(Submesh s) => new SubmeshState()
            {
                MinBounds = s.MinBounds, MaxBounds = s.MaxBounds, MinLODRange = s.MinLODRange, MaxLODRange = s.MaxLODRange,
                RenderFlags = s.RenderFlags, Material = s.Material, CollisionProxyIndex = s.CollisionProxyIndex,
                VertexFormatFull = s.VertexFormatFull, VertexFormatPartial = s.VertexFormatPartial,
                VertexScale = s.VertexScale, VertexCount = s.VertexCount, IndexCount = s.IndexCount,
                Bones = new List<int>(s.Bones ?? new List<int>()), Data = s.Data,
            };

            public void ApplyTo(Submesh s)
            {
                s.MinBounds = MinBounds; s.MaxBounds = MaxBounds; s.MinLODRange = MinLODRange; s.MaxLODRange = MaxLODRange;
                s.RenderFlags = RenderFlags; s.Material = Material; s.CollisionProxyIndex = CollisionProxyIndex;
                s.VertexFormatFull = VertexFormatFull; s.VertexFormatPartial = VertexFormatPartial;
                s.VertexScale = VertexScale; s.VertexCount = VertexCount; s.IndexCount = IndexCount;
                s.Bones = new List<int>(Bones);
                s.Data = Data;
            }
        }

        /// <summary>A model's drawn contents as they stand: the submesh objects of its LOD and each one's state.</summary>
        public sealed class ModelState
        {
            public List<Submesh> Submeshes;
            public List<SubmeshState> States;

            public static ModelState Of(Models.CS2 model)
            {
                List<Submesh> submeshes = BrushModelBuilder.Submeshes(model) ?? new List<Submesh>();
                return new ModelState() { Submeshes = new List<Submesh>(submeshes), States = submeshes.Select(SubmeshState.Of).ToList() };
            }

            public void ApplyTo(Models.CS2 model)
            {
                List<Submesh> live = BrushModelBuilder.Submeshes(model);
                if (live == null) return;
                live.Clear();
                live.AddRange(Submeshes);
                for (int i = 0; i < Submeshes.Count; i++)
                    States[i].ApplyTo(Submeshes[i]);
            }
        }
    }
}
