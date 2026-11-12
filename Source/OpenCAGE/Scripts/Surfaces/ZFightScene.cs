using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.ShaderTypes;
using CathodeLib;
using OpenCAGE.MCP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using Submesh = CATHODE.Models.CS2.Component.LOD.Submesh;

namespace OpenCAGE.Surfaces
{
    /// <summary>A placed ModelReference as the z-fighting search found it: where it sits and how its position is written.</summary>
    public sealed class ZPlacement
    {
        public int Index;
        /// <summary>From the level's root composite to the entity.</summary>
        public List<Entity> Chain;
        /// <summary>The composite holding the entity.</summary>
        public Composite Composite;
        public FunctionEntity Entity;
        public cTransform World;
        /// <summary>An alias already overriding this placement's position (it is what moves), and the composite holding it.</summary>
        public AliasEntity Override;
        public Composite OverrideOwner;
        /// <summary>"Room_A > Floor_1".</summary>
        public string Path;
        public string Name;
        /// <summary>show_on_reset is off: drawn only once a script shows it.</summary>
        public bool HiddenAtStart;
        public override string ToString() => Path;
    }

    /// <summary>Where a surface of the search came from: its model and submesh, and the placement drawing it (null for one model alone).</summary>
    public sealed class ZSurfaceSource
    {
        public ZPlacement Placement;
        public Models.CS2 Model;
        public int Component, Lod, SubmeshIndex, Element;
        public Submesh Submesh;
        public Materials.Material Material;
    }

    /// <summary>One search: what was searched, the surfaces, the fights found.</summary>
    public sealed class ZScan
    {
        /// <summary>"the whole level", "within 10 m of ...", "model X".</summary>
        public string Scope;
        /// <summary>A level search (placements in world space); otherwise one model in its own space.</summary>
        public bool LevelMode;
        public Models.CS2 Model;
        public Level Level;
        public List<ZSurface> Surfaces = new List<ZSurface>();
        public List<ZPlacement> Placements = new List<ZPlacement>();
        public ZFightOptions Options;
        public List<ZFight> Fights = new List<ZFight>();
        public ZFightFinder.Stats Stats;
        /// <summary>McpCollision.Version when it was made: the level has changed since when that moves.</summary>
        public int Version;
        public Vector3? ClipMin, ClipMax;
        public int NotPlaced, Skinned, NotDrawn, Visited, Hidden;
        public bool Truncated;
        public List<string> Notes = new List<string>();
        public DateTime Made = DateTime.Now;

        public double FightArea => Fights.Sum(o => o.Area);
        public ZFight Fight(int id) => Fights.FirstOrDefault(o => o.Id == id);
    }

    /// <summary>
    /// The surfaces a z-fighting search works on: one model's submeshes in its own space (the Model Editor), or every placed
    /// ModelReference's drawn submeshes (LOD 0, its renderable's own materials) at its world transform as the editor has it now,
    /// unsaved edits included (the level search). Skinned meshes and materials that draw over everything, or nothing solid (overlay,
    /// particles, fog, lights, sky), are left out.
    /// </summary>
    public static class ZFightScene
    {
        #region Materials
        /// <summary>The pass a material draws its surface in for this search, or null when it never fights (drawn over everything, or nothing solid).</summary>
        public static ZPass? PassOf(Materials.Material material, out bool doubleSided)
        {
            doubleSided = false;
            if (material == null) return ZPass.Opaque;
            if (material.OfflineLightFeatures != null) return null;
            SHADER_LIST family = material.Shader?.Ubershader ?? SHADER_LIST.CA_ENVIRONMENT;
            long mask = material.Shader?.UbershaderFeatureFlags ?? 0;
            switch (family)
            {
                case SHADER_LIST.CA_ENVIRONMENT:
                case SHADER_LIST.CA_CHARACTER:
                case SHADER_LIST.CA_SKIN:
                case SHADER_LIST.CA_HAIR:
                case SHADER_LIST.CA_EYE:
                case SHADER_LIST.CA_LOW_LOD_CHARACTER:
                case SHADER_LIST.CA_LIGHTMAP_ENVIRONMENT:
                case SHADER_LIST.CA_TERRAIN:
                case SHADER_LIST.CA_DECAL_ENVIRONMENT:
                case SHADER_LIST.CA_DECAL:
                case SHADER_LIST.CA_LIGHT_DECAL:
                case SHADER_LIST.CA_EFFECT_OVERLAY:
                case SHADER_LIST.CA_EFFECT:
                case SHADER_LIST.CA_SURFACE_EFFECTS:
                case SHADER_LIST.CA_LIQUID_ENVIRONMENT:
                case SHADER_LIST.CA_REFRACTION:
                case SHADER_LIST.CA_SIMPLE_REFRACTION:
                case SHADER_LIST.CA_SIMPLEWATER:
                case SHADER_LIST.CA_NONINTERACTIVE_WATER:
                case SHADER_LIST.CA_SPACESUIT_VISOR:
                case SHADER_LIST.BESPOKE_SHADER:
                    break;
                default:
                    return null;
            }
            doubleSided = HasFeature(family, mask, "DOUBLE_SIDED");
            int priority = material.Priority;
            //The overlay passes draw over everything: nothing fights them
            if (priority <= 32) return null;
            //Retail's mesh decals are CA_ENVIRONMENT with DECAL on, at priority 52-55
            if (priority == 52 || HasFeature(family, mask, "DECAL") || family == SHADER_LIST.CA_DECAL || family == SHADER_LIST.CA_DECAL_ENVIRONMENT || family == SHADER_LIST.CA_LIGHT_DECAL || family == SHADER_LIST.CA_EFFECT_OVERLAY)
                return ZPass.Decal;
            if (priority < 52 || HasFeature(family, mask, "FORCE_TO_ALPHA"))
                return ZPass.SeeThrough;
            return ZPass.Opaque;
        }

        private static bool HasFeature(SHADER_LIST family, long mask, string feature)
        {
            if (family == SHADER_LIST.BESPOKE_SHADER) return false;
            try
            {
                int? bit = ShaderUtility.GetShaderFunctionalityIndex(family, ShaderIndexType.FEATURES, feature);
                return bit != null && (mask & (1L << bit.Value)) != 0;
            }
            catch { return false; }
        }

        public static string PassName(ZPass pass) => pass == ZPass.Decal ? "mesh decal (52)" : pass == ZPass.SeeThrough ? "see-through" : "opaque";
        #endregion

        #region Meshes
        private sealed class RefEq<T> : IEqualityComparer<T> where T : class
        {
            public static readonly RefEq<T> Instance = new RefEq<T>();
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }

        /// <summary>A submesh's positions (model space, metres) and triangles, decoded once per search.</summary>
        public sealed class MeshCache
        {
            private readonly Dictionary<Submesh, (Vector3[] positions, int[] triangles)> _meshes = new Dictionary<Submesh, (Vector3[], int[])>(RefEq<Submesh>.Instance);

            public (Vector3[] positions, int[] triangles) Get(Submesh submesh)
            {
                if (_meshes.TryGetValue(submesh, out (Vector3[], int[]) had)) return had;
                cMesh mesh = submesh.ToMesh();
                Vector3[] positions = mesh.Vertices.ToArray();
                List<int> triangles = new List<int>(mesh.Indices.Count);
                for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
                {
                    int a = mesh.Indices[i], b = mesh.Indices[i + 1], c = mesh.Indices[i + 2];
                    if (a >= positions.Length || b >= positions.Length || c >= positions.Length) continue;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                }
                (Vector3[], int[]) made = (positions, triangles.ToArray());
                _meshes[submesh] = made;
                return made;
            }
        }

        /// <summary>Every submesh of the level's models: its model and place in it.</summary>
        public static Dictionary<Submesh, (Models.CS2 model, int component, int lod, int index)> SubmeshOwners(Level level)
        {
            Dictionary<Submesh, (Models.CS2, int, int, int)> owners = new Dictionary<Submesh, (Models.CS2, int, int, int)>(RefEq<Submesh>.Instance);
            foreach (Models.CS2 model in level?.Models?.Entries ?? new List<Models.CS2>())
            {
                if (model == null) continue;
                for (int c = 0; c < model.Components.Count; c++)
                    for (int l = 0; l < model.Components[c].LODs.Count; l++)
                        for (int s = 0; s < model.Components[c].LODs[l].Submeshes.Count; s++)
                        {
                            Submesh submesh = model.Components[c].LODs[l].Submeshes[s];
                            if (submesh != null && !owners.ContainsKey(submesh)) owners[submesh] = (model, c, l, s);
                        }
            }
            return owners;
        }
        #endregion

        #region One model
        /// <summary>
        /// One model's own surfaces, in its own space: LOD 0 of each component, every submesh a surface, each component a group (a
        /// model's components are not always drawn together, so only pairs inside one component count).
        /// </summary>
        public static ZScan FromModel(Models.CS2 model, ZFightOptions options)
        {
            ZScan scan = new ZScan() { Scope = "model " + model.Name, LevelMode = false, Model = model, Options = options.Clone() };
            scan.Options.AcrossGroups = false;
            scan.Options.InsideGroups = true;
            MeshCache cache = new MeshCache();
            for (int c = 0; c < model.Components.Count; c++)
            {
                if (model.Components[c].LODs.Count == 0) continue;
                Models.CS2.Component.LOD lod = model.Components[c].LODs[0];
                for (int s = 0; s < lod.Submeshes.Count; s++)
                {
                    Submesh submesh = lod.Submeshes[s];
                    if (submesh == null) continue;
                    if (submesh.Bones.Count != 0) { scan.Skinned++; continue; }
                    ZPass? pass = PassOf(submesh.Material, out bool doubleSided);
                    if (pass == null) { scan.NotDrawn++; continue; }
                    (Vector3[] positions, int[] triangles) = cache.Get(submesh);
                    if (triangles.Length == 0) continue;
                    scan.Surfaces.Add(new ZSurface()
                    {
                        Group = c,
                        GroupLabel = model.Components.Count > 1 ? "component " + c : System.IO.Path.GetFileName(model.Name),
                        Label = (model.Components.Count > 1 ? "component " + c + " " : "") + "mesh " + s + " (" + (submesh.Material?.Name ?? "no material") + ")",
                        Material = submesh.Material?.Name,
                        Pass = pass.Value,
                        DoubleSided = doubleSided,
                        Positions = positions,
                        Triangles = triangles,
                        Tag = new ZSurfaceSource() { Model = model, Component = c, Lod = 0, SubmeshIndex = s, Submesh = submesh, Material = submesh.Material, Element = -1 },
                    });
                }
            }
            if (scan.Skinned != 0) scan.Notes.Add(scan.Skinned + " skinned mesh(es) left out (they move with a skeleton).");
            if (scan.NotDrawn != 0) scan.Notes.Add(scan.NotDrawn + " mesh(es) left out: their material draws over everything or nothing solid (overlay, particles, light, sky).");
            return scan;
        }
        #endregion

        #region The level
        /// <summary>
        /// The placed models of a region of the level (null: the whole level), at their world transforms as the editor has them now.
        /// UI thread (it walks the level's script).
        /// </summary>
        internal static ZScan FromRegion(McpCall call, Level level, McpRegion region, ZFightOptions options)
        {
            Commands commands = level.Commands;
            region = region ?? new McpRegion() { Kind = "level", Label = "the whole level" };
            ZScan scan = new ZScan() { Scope = region.Label, LevelMode = true, Level = level, Options = options.Clone(), Version = McpCollision.Version };
            if (region.HasClip) { scan.ClipMin = region.ClipMin; scan.ClipMax = region.ClipMax; }
            MeshCache cache = new MeshCache();
            Dictionary<Submesh, (Models.CS2 model, int component, int lod, int index)> owners = SubmeshOwners(level);
            Composite root = commands.EntryPoints[0];

            bool Visit(McpPlacements.Step step)
            {
                if (!(step.Entity is FunctionEntity function) || !function.function.IsFunctionType || function.function.AsFunctionType != FunctionType.ModelReference)
                    return true;
                if (!step.Real) { scan.NotPlaced++; return true; }
                cTransform world = step.World;
                if (world == null) return true;
                List<RenderableElements.Element> elements = function.GetResource(ResourceType.RENDERABLE_INSTANCE, true)?.RenderableInstance;
                if (elements == null || elements.Count == 0) return true;

                //Its box first: a region only takes what reaches into it
                Vector3 lmin = new Vector3(float.MaxValue), lmax = new Vector3(float.MinValue);
                foreach (RenderableElements.Element element in elements)
                {
                    if (element?.Model == null) continue;
                    lmin = Vector3.Min(lmin, element.Model.MinBounds);
                    lmax = Vector3.Max(lmax, element.Model.MaxBounds);
                }
                if (lmin.X > lmax.X) return true;
                if (region.HasClip)
                {
                    McpCollision.WorldBox(lmin, lmax, world, out Vector3 wmin, out Vector3 wmax);
                    if (!region.TouchesClip(wmin - new Vector3(scan.Options.Gap), wmax + new Vector3(scan.Options.Gap))) return true;
                }

                ZPlacement placement = new ZPlacement()
                {
                    Index = scan.Placements.Count,
                    Chain = new List<Entity>(step.Chain),
                    Composite = step.Composite,
                    Entity = function,
                    World = new cTransform(world.position, world.rotation),
                    Override = step.OverriddenBy,
                    OverrideOwner = step.OverrideOwner,
                    Name = commands.Utils.GetEntityName(step.Composite, function),
                    HiddenAtStart = step.Flag(ShortGuids.show_on_reset) == false,
                };
                if (placement.HiddenAtStart && !scan.Options.IncludeHidden) { scan.Hidden++; return true; }
                placement.Path = McpRegion.DescribeChain(commands, placement.Chain);
                Matrix4x4 matrix = InstanceTransform.ToMatrix(placement.World);
                bool any = false;
                for (int e = 0; e < elements.Count; e++)
                {
                    RenderableElements.Element element = elements[e];
                    Submesh submesh = element?.Model;
                    if (submesh == null) continue;
                    if (submesh.Bones.Count != 0) { scan.Skinned++; continue; }
                    Materials.Material material = element.Material ?? submesh.Material;
                    ZPass? pass = PassOf(material, out bool doubleSided);
                    if (pass == null) { scan.NotDrawn++; continue; }
                    (Vector3[] local, int[] triangles) = cache.Get(submesh);
                    if (triangles.Length == 0) continue;
                    Vector3[] positions = new Vector3[local.Length];
                    for (int v = 0; v < local.Length; v++) positions[v] = Vector3.Transform(local[v], matrix);
                    owners.TryGetValue(submesh, out (Models.CS2 model, int component, int lod, int index) owner);
                    string modelName = owner.model != null ? System.IO.Path.GetFileNameWithoutExtension(owner.model.Name) : "?";
                    scan.Surfaces.Add(new ZSurface()
                    {
                        Group = placement.Index,
                        GroupLabel = placement.Path,
                        Label = placement.Name + " / " + modelName + " mesh " + (owner.model != null ? owner.index : e) + " (" + (material?.Name ?? "no material") + ")",
                        Material = material?.Name,
                        Pass = pass.Value,
                        DoubleSided = doubleSided,
                        Positions = positions,
                        Triangles = triangles,
                        Tag = new ZSurfaceSource() { Placement = placement, Model = owner.model, Component = owner.component, Lod = owner.lod, SubmeshIndex = owner.index, Submesh = submesh, Material = material, Element = e },
                    });
                    any = true;
                }
                if (any) scan.Placements.Add(placement);
                return true;
            }

            McpCollision.WalkRegion(call, commands, level, region, true, Visit, out scan.Visited, out scan.Truncated);
            if (scan.Hidden != 0) scan.Notes.Add(scan.Hidden + " model(s) hidden at the start (show_on_reset off) left out: a script shows them later.");
            if (scan.Truncated) scan.Notes.Add("The walk stopped after " + scan.Visited + " entities: part of the level was not searched. Search a smaller region.");
            if (scan.Skinned != 0) scan.Notes.Add(scan.Skinned + " skinned mesh(es) left out.");
            if (scan.NotDrawn != 0) scan.Notes.Add(scan.NotDrawn + " mesh(es) left out: their material draws over everything or nothing solid (overlay, particles, light, sky).");
            return scan;
        }

        /// <summary>The whole level, or a world box of it (headless: no editor needed).</summary>
        public static ZScan FromLevel(Level level, Vector3? boxMin, Vector3? boxMax, ZFightOptions options)
        {
            McpRegion region = boxMin != null && boxMax != null ? McpRegion.Box(boxMin.Value, boxMax.Value) : null;
            return FromRegion(null, level, region, options);
        }
        #endregion

        #region Searching
        /// <summary>Search the scan's surfaces (any thread: pure geometry). A level search keeps only fights reaching into its region.</summary>
        public static void Run(ZScan scan, CancellationToken cancel = default(CancellationToken), Action<double> progress = null)
        {
            scan.Fights = ZFightFinder.Find(scan.Surfaces, scan.Options, out ZFightFinder.Stats stats, cancel, progress);
            scan.Stats = stats;
            if (scan.ClipMin != null && scan.ClipMax != null)
            {
                Vector3 a = scan.ClipMin.Value, b = scan.ClipMax.Value;
                scan.Fights = scan.Fights.Where(o => o.Max.X >= a.X && o.Min.X <= b.X && o.Max.Y >= a.Y && o.Min.Y <= b.Y && o.Max.Z >= a.Z && o.Min.Z <= b.Z).ToList();
                for (int i = 0; i < scan.Fights.Count; i++) scan.Fights[i].Id = i + 1;
                stats.Fights = scan.Fights.Count;
            }
        }

        public static ZSurfaceSource SourceOf(ZSurface surface) => surface?.Tag as ZSurfaceSource;
        #endregion
    }
}
