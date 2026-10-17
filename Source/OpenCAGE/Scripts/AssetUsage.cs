using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// What in a level still points at a material or a texture, so a delete can take along only the
    /// assets nothing else needs - and refuse to take a material something still draws with.
    ///
    /// A save writes whatever is left pointing at a removed asset as index -1, so "nothing else
    /// uses it" has to cover every way a level refers to one: by object (model submeshes, render
    /// element rows, collision mappings, other materials' texture slots, movers' cubemaps) and by
    /// name (material mapping targets, a ModelReference's "material" override, any script string
    /// naming a texture - an EnvironmentMap's cubemap, a light's gobo). Names resolve the way
    /// instancing resolves them, so a name that would land on a material counts as using it.
    /// </summary>
    public static class AssetUsage
    {
        private static readonly ShortGuid MaterialParameter = ShortGuidUtils.Generate("material");

        /// <summary>Everything that points at materials, taken once and asked about several.</summary>
        public class MaterialReferences
        {
            internal readonly Dictionary<Materials.Material, HashSet<Models.CS2>> Models =
                new Dictionary<Materials.Material, HashSet<Models.CS2>>(BulkExport.ByReference<Materials.Material>.Instance);
            internal readonly Dictionary<Materials.Material, int> Elements =
                new Dictionary<Materials.Material, int>(BulkExport.ByReference<Materials.Material>.Instance);
            internal readonly HashSet<Materials.Material> Collisions =
                new HashSet<Materials.Material>(BulkExport.ByReference<Materials.Material>.Instance);
            internal readonly HashSet<Materials.Material> Named =
                new HashSet<Materials.Material>(BulkExport.ByReference<Materials.Material>.Instance);

            public bool InUse(Materials.Material material)
            {
                return Models.ContainsKey(material) || Elements.ContainsKey(material) || Collisions.Contains(material) || Named.Contains(material);
            }

            /// <summary>What still uses the material, as lines to show; empty when nothing does.</summary>
            public List<string> Describe(Materials.Material material)
            {
                List<string> uses = new List<string>();
                if (Models.TryGetValue(material, out HashSet<Models.CS2> models))
                {
                    List<string> names = models.Select(o => o.Name ?? "(unnamed)").OrderBy(o => o, StringComparer.OrdinalIgnoreCase).ToList();
                    uses.Add((names.Count == 1 ? "the model " : names.Count + " models: ") + string.Join(", ", names.Take(5)) + (names.Count > 5 ? ", ..." : ""));
                }
                if (Elements.TryGetValue(material, out int rows))
                    uses.Add(rows + (rows == 1 ? " placed renderable" : " placed renderables"));
                if (Collisions.Contains(material))
                    uses.Add("a collision mapping");
                if (Named.Contains(material))
                    uses.Add("a material mapping or a ModelReference's \"material\" parameter, by name");
                return uses;
            }
        }

        /// <summary>
        /// Every reference to a material in the level, leaving out the given model - whose submeshes,
        /// and the render rows drawing them, stop counting once it is deleted.
        /// </summary>
        public static MaterialReferences FindMaterialReferences(Level level, Models.CS2 ignoring = null)
        {
            MaterialReferences references = new MaterialReferences();
            if (level == null) return references;

            HashSet<Models.CS2.Component.LOD.Submesh> liveSubmeshes =
                new HashSet<Models.CS2.Component.LOD.Submesh>(BulkExport.ByReference<Models.CS2.Component.LOD.Submesh>.Instance);
            foreach (Models.CS2 model in level.Models?.Entries ?? new List<Models.CS2>())
            {
                if (model == null || ReferenceEquals(model, ignoring)) continue;
                foreach (Models.CS2.Component.LOD.Submesh submesh in Submeshes(model))
                {
                    liveSubmeshes.Add(submesh);
                    if (submesh.Material == null) continue;
                    if (!references.Models.TryGetValue(submesh.Material, out HashSet<Models.CS2> models))
                        references.Models[submesh.Material] = models = new HashSet<Models.CS2>(BulkExport.ByReference<Models.CS2>.Instance);
                    models.Add(model);
                }
            }

            //A row drawing a submesh of a model that is gone is dead - it is written as -1 whatever its material
            foreach (RenderableElements.Element element in level.RenderableElements?.Entries ?? new List<RenderableElements.Element>())
            {
                if (element?.Material == null || element.Model == null || !liveSubmeshes.Contains(element.Model)) continue;
                references.Elements[element.Material] = references.Elements.TryGetValue(element.Material, out int count) ? count + 1 : 1;
            }

            foreach (CollisionMaps.COLLISION_MAPPING mapping in level.CollisionMaps?.Entries ?? new List<CollisionMaps.COLLISION_MAPPING>())
                if (mapping?.Material != null)
                    references.Collisions.Add(mapping.Material);

            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (MaterialMappings.MaterialMapping mapping in level.MaterialMappings?.Entries ?? new List<MaterialMappings.MaterialMapping>())
                foreach (MaterialMappings.MaterialMapping.Mapping entry in mapping?.Mappings ?? new List<MaterialMappings.MaterialMapping.Mapping>())
                    if (!string.IsNullOrEmpty(entry?.to))
                        names.Add(entry.to);
            foreach (Entity entity in ScriptEntities(level))
            {
                Parameter parameter = entity.parameters.FirstOrDefault(o => o.name == MaterialParameter);
                string name = (parameter?.content as cString)?.value;
                if (!string.IsNullOrEmpty(name))
                    names.Add(name);
            }
            /* The three ways MaterialRemappingUtils.MaterialsAnsweringTo lets a name land on a material - as
               written, normalised, and normalised with a 3ds Max authoring slot dropped from both sides -
               in one pass over the table, rather than one per name: a big level names hundreds. */
            HashSet<string> normalised = new HashSet<string>(names.Select(MaterialRemappingUtils.NormalizeMaterialNameForLookup), StringComparer.Ordinal);
            HashSet<string> slotless = new HashSet<string>(names.Select(o => MaterialRemappingUtils.NormalizeMaterialNameForLookup(StripAuthoringSlot(o))), StringComparer.Ordinal);
            foreach (Materials.Material material in level.Materials?.Entries ?? new List<Materials.Material>())
            {
                if (string.IsNullOrEmpty(material?.Name)) continue;
                if (names.Contains(material.Name)
                    || normalised.Contains(MaterialRemappingUtils.NormalizeMaterialNameForLookup(material.Name))
                    || slotless.Contains(MaterialRemappingUtils.NormalizeMaterialNameForLookup(StripAuthoringSlot(material.Name))))
                    references.Named.Add(material);
            }

            return references;
        }

        /* "MULTIMATERIAL->X->Y" as "X->Y", as MaterialRemappingUtils.StripAuthoringSlot does; two-part names come back as they were */
        private static string StripAuthoringSlot(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int last = name.LastIndexOf("->", StringComparison.Ordinal);
            if (last <= 0) return name;
            int previous = name.LastIndexOf("->", last - 1, StringComparison.Ordinal);
            return previous < 0 ? name : name.Substring(previous + 2);
        }

        /// <summary>
        /// The materials on this model that would be left unused once it is deleted, in the order the
        /// model first uses them. Materials that aren't the level's own (a global one) are never offered.
        /// </summary>
        public static List<Materials.Material> MaterialsOnlyUsedBy(Level level, Models.CS2 model, out int sharedCount)
        {
            sharedCount = 0;
            List<Materials.Material> only = new List<Materials.Material>();
            if (level?.Materials == null || model == null) return only;

            HashSet<Materials.Material> levelMaterials = new HashSet<Materials.Material>(level.Materials.Entries, BulkExport.ByReference<Materials.Material>.Instance);
            HashSet<Materials.Material> seen = new HashSet<Materials.Material>(BulkExport.ByReference<Materials.Material>.Instance);
            MaterialReferences references = FindMaterialReferences(level, model);
            foreach (Models.CS2.Component.LOD.Submesh submesh in Submeshes(model))
            {
                Materials.Material material = submesh.Material;
                if (material == null || !levelMaterials.Contains(material) || !seen.Add(material)) continue;
                if (references.InUse(material)) sharedCount++;
                else only.Add(material);
            }
            return only;
        }

        /// <summary>
        /// The level textures these materials bind that nothing else would be left using once they are
        /// deleted: no other material's slot, no mover's cubemap, no script string naming it.
        /// </summary>
        public static List<Textures.TEX4> TexturesOnlyUsedBy(Level level, ICollection<Materials.Material> materials, out int sharedCount)
        {
            sharedCount = 0;
            List<Textures.TEX4> only = new List<Textures.TEX4>();
            if (level?.Textures == null || materials == null || materials.Count == 0) return only;

            HashSet<Materials.Material> leaving = new HashSet<Materials.Material>(materials, BulkExport.ByReference<Materials.Material>.Instance);
            HashSet<Textures.TEX4> levelTextures = new HashSet<Textures.TEX4>(level.Textures.Entries, BulkExport.ByReference<Textures.TEX4>.Instance);

            List<Textures.TEX4> candidates = new List<Textures.TEX4>();
            HashSet<Textures.TEX4> seen = new HashSet<Textures.TEX4>(BulkExport.ByReference<Textures.TEX4>.Instance);
            foreach (Materials.Material material in materials)
                foreach (TexturePtr slot in material?.TextureReferences ?? new List<TexturePtr>())
                    if (slot?.Texture != null && slot.Location == TexturePtr.Source.LEVEL && levelTextures.Contains(slot.Texture) && seen.Add(slot.Texture))
                        candidates.Add(slot.Texture);
            if (candidates.Count == 0) return only;

            HashSet<Textures.TEX4> used = new HashSet<Textures.TEX4>(BulkExport.ByReference<Textures.TEX4>.Instance);
            foreach (Materials.Material material in level.Materials?.Entries ?? new List<Materials.Material>())
            {
                if (material == null || leaving.Contains(material)) continue;
                foreach (TexturePtr slot in material.TextureReferences)
                    if (slot?.Texture != null) used.Add(slot.Texture);
            }
            foreach (Movers.MOVER_DESCRIPTOR mover in level.Movers?.Entries ?? new List<Movers.MOVER_DESCRIPTOR>())
                if (mover?.EnvironmentMap != null) used.Add(mover.EnvironmentMap);

            //Scripts name textures by path - an EnvironmentMap's cubemap, a light's gobo - with or without the build prefix
            Dictionary<string, List<Textures.TEX4>> byName = new Dictionary<string, List<Textures.TEX4>>(StringComparer.Ordinal);
            foreach (Textures.TEX4 texture in candidates)
            {
                foreach (string key in new[] { TextureKey(texture.Name), TextureKey(StripExtension(texture.Name)) }.Distinct())
                {
                    if (!byName.TryGetValue(key, out List<Textures.TEX4> list)) byName[key] = list = new List<Textures.TEX4>();
                    list.Add(texture);
                }
            }
            foreach (Entity entity in ScriptEntities(level))
            {
                foreach (Parameter parameter in entity.parameters)
                {
                    string value = (parameter?.content as cString)?.value;
                    if (string.IsNullOrEmpty(value)) continue;
                    string key = TextureKey(value);
                    if (byName.TryGetValue(key, out List<Textures.TEX4> named) || byName.TryGetValue(TextureKey(StripExtension(value)), out named))
                        foreach (Textures.TEX4 texture in named) used.Add(texture);
                }
            }

            foreach (Textures.TEX4 texture in candidates)
            {
                if (used.Contains(texture)) sharedCount++;
                else only.Add(texture);
            }
            return only;
        }

        /// <summary>Removes exactly these objects - Material and TEX4 compare by value, so List.Remove could take a twin instead.</summary>
        public static int RemoveByReference<T>(List<T> entries, IEnumerable<T> remove) where T : class
        {
            HashSet<T> gone = new HashSet<T>(remove, BulkExport.ByReference<T>.Instance);
            return entries.RemoveAll(o => gone.Contains(o));
        }

        /// <summary>"a, b, c and 4 more" - for naming what a question is about without filling the screen.</summary>
        public static string ListNames(IEnumerable<string> names, int max = 8)
        {
            List<string> all = names.ToList();
            if (all.Count <= max) return string.Join("\n", all.Select(o => "    " + o));
            return string.Join("\n", all.Take(max).Select(o => "    " + o)) + "\n    ... and " + (all.Count - max) + " more";
        }

        private static IEnumerable<Models.CS2.Component.LOD.Submesh> Submeshes(Models.CS2 model)
        {
            foreach (Models.CS2.Component component in model.Components)
                foreach (Models.CS2.Component.LOD lod in component.LODs)
                    foreach (Models.CS2.Component.LOD.Submesh submesh in lod.Submeshes)
                        if (submesh != null)
                            yield return submesh;
        }

        private static IEnumerable<Entity> ScriptEntities(Level level)
        {
            foreach (Composite composite in level.Commands?.Entries ?? new List<Composite>())
                if (composite != null)
                    foreach (Entity entity in composite.GetEntities())
                        if (entity?.parameters != null)
                            yield return entity;
        }

        private const string BuildTextures = "CONTENT\\BUILD\\TEXTURES\\";

        private static string TextureKey(string name)
        {
            string key = (name ?? "").ToUpperInvariant().Replace('/', '\\').Trim();
            int prefix = key.IndexOf(BuildTextures, StringComparison.Ordinal);
            return prefix < 0 ? key : key.Substring(prefix + BuildTextures.Length);
        }

        private static string StripExtension(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int dot = name.LastIndexOf('.');
            int separator = Math.Max(name.LastIndexOf('\\'), name.LastIndexOf('/'));
            return dot > separator ? name.Substring(0, dot) : name;
        }
    }
}
