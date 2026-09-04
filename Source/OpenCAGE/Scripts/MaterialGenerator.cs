using Assimp;
using CATHODE;
using CATHODE.ShaderTypes;
using AlienPAK;
using CathodeLib;
using CathodeLib.ObjectExtensions;
using CathodeLib.Ubershaders;
using OpenCAGE.TextureTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE
{
    /// <summary>
    /// Builds a CATHODE material out of what an imported model already carries - its texture slots,
    /// its colours, and whether the mesh is skinned - so a model can arrive with working materials
    /// instead of having to be pointed at ones the level happens to already have.
    ///
    /// Three measured facts shape all of this, taken from 58,155 materials across seven shipped
    /// levels (BSP_Torrens, Solace, Tech_Hub, Sci_Hub, ChallengeMap1, Tech_RnD_HzdLab, Frontend):
    ///
    /// 1. **A feature bit and its sampler are the same fact.** For every family and every sampler
    ///    that names a feature, the feature is set in exactly the materials that bind the sampler -
    ///    NORMAL_MAPPING 6,047 of 6,047, SPECULAR_MAPPING 13,147 of 13,147, and so on, with not one
    ///    exception either way. So the features this class sets are entirely decided by which
    ///    textures it found, and a feature whose map is missing is always cleared.
    /// 2. **DIFFUSE_MAP is ungated and universal.** No feature bit tracks it, and all 21,211
    ///    CA_ENVIRONMENT materials bind one. A generated material always needs a diffuse map.
    /// 3. **Format and colour space follow the role, not the image.** Diffuse, AO and dirt maps are
    ///    sRGB; normal maps are DXN and never sRGB; specular maps are linear (only 17% carry the
    ///    sRGB flag). See <see cref="Role"/>.
    ///
    /// Everything a model cannot tell us - radiosity, UV multipliers, roughness, priority - is taken
    /// from a donor material of the same family already in the level, so a generated material starts
    /// from values that are known to work there rather than from zero.
    /// </summary>
    public static class MaterialGenerator
    {
        /// <summary>
        /// One sampler we know how to fill, where its image comes from in an imported model, and how
        /// it has to be encoded. Ordered as they should be presented.
        /// </summary>
        public class Role
        {
            public string Sampler;                       //CATHODE sampler name, e.g. "NORMAL_MAP"
            public TextureType[] Sources;                //assimp slots to try, in order
            public int SourceIndex;                      //which index within the slot
            public Textures.TextureFormat Format;
            public bool SRGB;
            public bool RequiresMetadata;                //only read when OpenCAGE's own sidecar is beside the model

            /// <summary>
            /// Refuse the slot when it names the same file as TextureType.Unknown, which for a glTF is
            /// the metallic-roughness map. assimp files that under Lightmap as well as Unknown, and a
            /// metal-rough map bound as ambient occlusion is worse than no ambient occlusion at all.
            /// A real glTF occlusion texture is under Lightmap and NOT under Unknown, so it survives.
            /// </summary>
            public bool NotTheUnknownSlot;
            public string Description;
        }

        /* Index 0 of a standard slot is read from any model. Index 1 and TextureType.Unknown are only
         * read back when OpenCAGE's own sidecar sits beside the file, because those are where OUR
         * exporter parks the secondary and dirt maps - in a model from anywhere else, Unknown is
         * usually a glTF metallic-roughness map, which is not a dirt map and must not be bound as one. */
        private static readonly Role[] Roles = new Role[]
        {
            new Role { Sampler = "DIFFUSE_MAP",            Sources = new[] { TextureType.Diffuse },                        SourceIndex = 0, Format = Textures.TextureFormat.BC7, SRGB = true,  Description = "Base colour" },
            new Role { Sampler = "NORMAL_MAP",             Sources = new[] { TextureType.Normals, TextureType.Height },    SourceIndex = 0, Format = Textures.TextureFormat.DXN, SRGB = false, Description = "Normal map" },
            new Role { Sampler = "SPECULAR_MAP",           Sources = new[] { TextureType.Specular },                       SourceIndex = 0, Format = Textures.TextureFormat.BC7, SRGB = false, Description = "Specular" },
            new Role { Sampler = "AMBIENT_OCCLUSION_MAP",  Sources = new[] { TextureType.AmbientOcclusion, TextureType.Lightmap }, SourceIndex = 0, Format = Textures.TextureFormat.BC7, SRGB = true, NotTheUnknownSlot = true, Description = "Ambient occlusion" },
            new Role { Sampler = "SEPARATE_ALPHA_MAP",     Sources = new[] { TextureType.Opacity },                        SourceIndex = 0, Format = Textures.TextureFormat.BC7, SRGB = true,  Description = "Opacity" },
            new Role { Sampler = "SECONDARY_DIFFUSE_MAP",  Sources = new[] { TextureType.Diffuse },                        SourceIndex = 1, Format = Textures.TextureFormat.BC7, SRGB = true,  RequiresMetadata = true, Description = "Second base colour" },
            new Role { Sampler = "SECONDARY_NORMAL_MAP",   Sources = new[] { TextureType.Normals },                        SourceIndex = 1, Format = Textures.TextureFormat.DXN, SRGB = false, RequiresMetadata = true, Description = "Second normal map" },
            new Role { Sampler = "SECONDARY_SPECULAR_MAP", Sources = new[] { TextureType.Specular },                       SourceIndex = 1, Format = Textures.TextureFormat.BC7, SRGB = false, RequiresMetadata = true, Description = "Second specular" },
            new Role { Sampler = "DIRT_MAP",               Sources = new[] { TextureType.Unknown },                        SourceIndex = 0, Format = Textures.TextureFormat.BC7, SRGB = true,  RequiresMetadata = true, Description = "Dirt" },
        };

        /// <summary>One texture the generated material will bind, and where its image comes from.</summary>
        public class PlannedTexture
        {
            public Role Role;
            public int SamplerIndex;
            public string SourcePath;              //file beside the model, or null when embedded
            public EmbeddedTexture Embedded;       //GLB-style embedded image, or null
            public string SourceLabel;             //what to show the user
            public string TextureName;             //name it will be given in the level
            public Textures.TEX4 Existing;         //a level texture of that name to reuse instead of importing
            public string Problem;                 //why this one cannot be used, or null
            public bool Usable => Problem == null;
        }

        /// <summary>What generating would do, so it can be shown before anything is written.</summary>
        public class Plan
        {
            public SHADER_LIST Family;
            public string Name;
            public long Mask;
            public bool ExactMask;                 //false when we had to settle for the nearest shipped permutation
            public PermutationSource Source;
            public List<PlannedTexture> Textures = new List<PlannedTexture>();
            public List<string> Features = new List<string>();      //feature names the mask turns on
            public List<string> Notes = new List<string>();
            public string Error;                   //set when the plan cannot be carried out at all

            /// <summary>
            /// Build a material of this plan's own even when the level already holds one that matches
            /// exactly. Off by default, so importing the same model twice lands back on the same
            /// material instead of leaving copies behind; on when someone wants two models that start
            /// the same to be tuned apart afterwards.
            /// </summary>
            public bool AlwaysCreateNew;

            public bool CanGenerate => Error == null;
            public IEnumerable<PlannedTexture> UsableTextures => Textures.Where(o => o.Usable);
        }

        #region FEATURE / SAMPLER PAIRING

        /// <summary>
        /// The feature that gates a sampler, or null when the sampler is always on (DIFFUSE_MAP).
        ///
        /// The pairing is by name, and the census is what says the name rule is the right one: where
        /// several bits correlate perfectly with a sampler - IRRADIANCE_CUBE_MAP also matches
        /// GPU_SKINNING and BLUR_MASKING on characters, because every character has all three - the
        /// name-matching bit is the one that is actually about the texture.
        /// </summary>
        public static string FeatureForSampler(SHADER_LIST family, string sampler)
        {
            List<string> features = ShaderUtility.GetFeatures(family);
            if (features == null || features.Count == 0) return null;

            //NORMAL_MAP -> NORMAL_MAPPING, then SEPARATE_ALPHA_MAP -> SEPARATE_ALPHA, then an exact name
            if (sampler.EndsWith("_MAP", StringComparison.Ordinal))
            {
                string mapping = sampler.Substring(0, sampler.Length - 4) + "_MAPPING";
                if (features.Contains(mapping)) return mapping;

                string bare = sampler.Substring(0, sampler.Length - 4);
                if (features.Contains(bare)) return bare;
            }
            return features.Contains(sampler) ? sampler : null;
        }

        private static int FeatureBit(SHADER_LIST family, string feature)
        {
            if (feature == null) return -1;
            int? index = ShaderUtility.GetShaderFunctionalityIndex(family, ShaderIndexType.FEATURES, feature);
            return index ?? -1;
        }

        private static int SamplerIndex(SHADER_LIST family, string sampler)
        {
            int? index = ShaderUtility.GetShaderFunctionalityIndex(family, ShaderIndexType.SAMPLERS, sampler);
            return index ?? -1;
        }

        #endregion

        #region PLANNING

        /// <summary>
        /// The family a mesh should be built on: skinned meshes are characters, everything else is
        /// environment. Falls back to whatever the level can actually supply a shader for.
        /// </summary>
        public static SHADER_LIST SuggestFamily(Mesh mesh, IEnumerable<SHADER_LIST> creatable)
        {
            SHADER_LIST wanted = Skinned(mesh) ? SHADER_LIST.CA_CHARACTER : SHADER_LIST.CA_ENVIRONMENT;
            if (creatable == null) return wanted;

            List<SHADER_LIST> available = creatable.ToList();
            if (available.Contains(wanted)) return wanted;
            if (available.Contains(SHADER_LIST.CA_ENVIRONMENT)) return SHADER_LIST.CA_ENVIRONMENT;
            return available.Count == 0 ? wanted : available[0];
        }

        /* Whether the mesh arrives skinned, which is not the same as whether the file skins it. A bone
         * the importer cannot read costs the submesh all of its skinning, not just that bone, so a
         * mesh on a skeleton the game doesn't have comes in static however many bones it was built on
         * - and a static mesh wants the unskinned shader. */
        private static bool Skinned(Mesh mesh)
        {
            return mesh != null && mesh.HasBones
                && mesh.Bones.All(x => ModelIO.TryParseBoneName(x.Name, out int _));
        }

        /// <summary>
        /// Work out what a material generated from this model material would look like, without
        /// changing anything.
        /// </summary>
        public static Plan Describe(Scene scene, Assimp.Material source, Mesh mesh, string modelPath, bool hasMetadata,
                                    Level level, SHADER_LIST family, string name, string gameRoot)
        {
            Plan plan = new Plan { Family = family, Name = name };

            if (level?.Materials == null || level.Shaders == null || level.Textures == null)
            {
                plan.Error = "This level's materials, shaders or textures are not loaded.";
                return plan;
            }

            string modelDirectory = string.IsNullOrEmpty(modelPath) ? null : Path.GetDirectoryName(modelPath);
            List<string> samplers = ShaderUtility.GetSamplers(family) ?? new List<string>();

            foreach (Role role in Roles)
            {
                if (!samplers.Contains(role.Sampler)) continue;
                if (role.RequiresMetadata && !hasMetadata) continue;

                TextureSlot slot;
                if (!TryFindSlot(source, role, out slot)) continue;

                PlannedTexture planned = ResolveTexture(scene, slot, role, modelDirectory, level, plan.Name);
                planned.SamplerIndex = SamplerIndex(family, role.Sampler);
                if (planned.SamplerIndex == -1) continue;
                plan.Textures.Add(planned);
            }

            if (!plan.Textures.Any(o => o.Usable && o.Role.Sampler == "DIFFUSE_MAP"))
                plan.Notes.Add("No base colour texture was found on this model material. Every shipped material of this kind has one, so the result will render untextured until you set one.");

            //Everything a model can't tell us comes from the level's own most typical material of this family
            long baseMask;
            if (!TryMostUsedMask(level.Materials, level.Shaders, family, gameRoot, out baseMask))
            {
                plan.Error = "No " + family + " shader could be found to start from, in this level or the shader database.";
                return plan;
            }

            long wanted = ApplyFeatures(baseMask, family, plan, source, mesh);
            plan.Mask = wanted;

            //Can we have exactly that combination, or do we have to settle for something shipped?
            PermutationSource probeSource;
            if (CanResolve(level.Materials, level.Shaders, family, wanted, gameRoot, out probeSource))
            {
                plan.ExactMask = true;
                plan.Source = probeSource;
            }
            else
            {
                long nearest;
                if (!TryNearestPermutation(level.Materials, level.Shaders, family, gameRoot, wanted, plan, out nearest))
                {
                    plan.Error = "No usable " + family + " shader permutation could be found for this material.";
                    return plan;
                }

                plan.Mask = nearest;
                plan.ExactMask = false;
                plan.Source = PermutationSource.LevelPool;
                DropTexturesTheMaskCannotUse(plan, family, nearest);
                plan.Notes.Add("The exact feature combination this model needs isn't available here, so the nearest shipped one is used instead. "
                             + "Build the shader database, or install a HLSL compiler, to widen what can be built.");
            }

            plan.Features = DescribeFeatures(family, plan.Mask);
            return plan;
        }

        private static bool TryFindSlot(Assimp.Material source, Role role, out TextureSlot slot)
        {
            slot = default(TextureSlot);
            if (source == null) return false;

            string unknown = null;
            if (role.NotTheUnknownSlot)
            {
                TextureSlot other;
                if (source.GetMaterialTexture(TextureType.Unknown, 0, out other))
                    unknown = other.FilePath;
            }

            foreach (TextureType type in role.Sources)
            {
                TextureSlot candidate;
                if (!source.GetMaterialTexture(type, role.SourceIndex, out candidate)) continue;
                if (string.IsNullOrEmpty(candidate.FilePath)) continue;
                if (unknown != null && string.Equals(candidate.FilePath, unknown, StringComparison.OrdinalIgnoreCase)) continue;
                slot = candidate;
                return true;
            }
            return false;
        }

        /* Where the image actually is: embedded in the file (glTF binary and FBX both do this), or a
         * path that is nearly always relative to the model. Neither is guaranteed to lead anywhere,
         * so an unusable one is kept in the plan with its reason rather than quietly dropped. */
        private static PlannedTexture ResolveTexture(Scene scene, TextureSlot slot, Role role, string modelDirectory, Level level, string materialName)
        {
            PlannedTexture planned = new PlannedTexture { Role = role, SourceLabel = slot.FilePath };

            EmbeddedTexture embedded = scene?.GetEmbeddedTexture(slot.FilePath);
            if (embedded != null)
            {
                planned.Embedded = embedded;
                planned.SourceLabel = "(embedded in the model)";
                if (!embedded.HasCompressedData && !embedded.HasNonCompressedData)
                    planned.Problem = "the embedded image is empty";
            }
            else
            {
                string path = ResolveOnDisk(slot.FilePath, modelDirectory);
                if (path == null)
                    planned.Problem = "no file of that name was found next to the model";
                planned.SourcePath = path;
            }

            planned.TextureName = TextureNameFor(slot.FilePath, role, materialName);

            /* A texture already in the level under that name is reused rather than imported twice, but
             * only when it really is the same image. Exporters name textures after the scene and the
             * slot, not after the model, so two unrelated models routinely both arrive carrying a
             * "Scene_-_Root_baseColor" - and matching on the name alone silently gave the second model
             * the first one's pixels. Dimensions are the cheap half of "same image": they can be read
             * from the source header without running a conversion, and a mismatch is proof. Two
             * different images of identical size still share, which is the price of not converting
             * every candidate just to compare it. */
            Textures.TEX4 sameName = level.Textures.Entries.FirstOrDefault(o => string.Equals(o.Name, planned.TextureName, StringComparison.OrdinalIgnoreCase));
            if (sameName != null && SameSize(planned, sameName))
            {
                planned.Existing = sameName;
                planned.Problem = null;
            }
            else if (sameName != null)
            {
                planned.TextureName = AssetName.MakeUnique(planned.TextureName, level.Textures.Entries.Select(o => o.Name));
            }

            return planned;
        }

        /* Does the planned image have the same pixel dimensions as a texture already in the level? The
         * streamed part is the full-size one, so that is what a source image has to match. Anything we
         * cannot read - an unreadable file, an empty embedded image - answers no, so the import path
         * runs and reports its own error rather than this quietly reusing someone else's texture. */
        private static bool SameSize(PlannedTexture planned, Textures.TEX4 existing)
        {
            Textures.TEX4.Texture part = existing?.TextureStreamed;
            if (part == null || part.Width <= 0) part = existing?.TexturePersistent;
            if (part == null || part.Width <= 0) return false;

            try
            {
                if (planned.Embedded != null)
                {
                    if (planned.Embedded.HasNonCompressedData)
                        return planned.Embedded.Width == part.Width && planned.Embedded.Height == part.Height;
                    if (!planned.Embedded.HasCompressedData) return false;
                    using (MemoryStream stream = new MemoryStream(planned.Embedded.CompressedData))
                    using (System.Drawing.Image image = System.Drawing.Image.FromStream(stream, false, false))
                        return image.Width == part.Width && image.Height == part.Height;
                }

                if (planned.SourcePath == null || !File.Exists(planned.SourcePath)) return false;
                using (FileStream stream = File.OpenRead(planned.SourcePath))
                using (System.Drawing.Image image = System.Drawing.Image.FromStream(stream, false, false))
                    return image.Width == part.Width && image.Height == part.Height;
            }
            catch
            {
                return false;
            }
        }

        /* Model files name their textures every way there is - absolute paths from the authoring
         * machine, "../textures/x.png", or a bare file name - so try the path as given, then the file
         * name beside the model, then the usual texture subfolders. */
        private static string ResolveOnDisk(string filePath, string modelDirectory)
        {
            if (string.IsNullOrEmpty(filePath)) return null;

            string cleaned = filePath.Replace('/', Path.DirectorySeparatorChar).Trim();
            try
            {
                if (File.Exists(cleaned)) return Path.GetFullPath(cleaned);
                if (string.IsNullOrEmpty(modelDirectory)) return null;

                string relative = Path.Combine(modelDirectory, cleaned);
                if (File.Exists(relative)) return Path.GetFullPath(relative);

                string leaf = Path.GetFileName(cleaned);
                if (string.IsNullOrEmpty(leaf)) return null;

                foreach (string folder in new[] { "", "textures", "Textures", "tex", "maps", "images" })
                {
                    string candidate = Path.Combine(Path.Combine(modelDirectory, folder), leaf);
                    if (File.Exists(candidate)) return Path.GetFullPath(candidate);
                }
            }
            catch
            {
                //A path from another machine can be malformed enough to throw - that just means "not found"
            }
            return null;
        }

        private static string TextureNameFor(string filePath, Role role, string materialName)
        {
            string leaf = null;
            try { leaf = Path.GetFileNameWithoutExtension((filePath ?? "").Replace('/', Path.DirectorySeparatorChar)); }
            catch { }

            //Embedded images are named "*0", which is no use as an asset name
            if (string.IsNullOrWhiteSpace(leaf) || leaf.StartsWith("*", StringComparison.Ordinal))
                leaf = AssetName.Normalise(materialName).Replace(AssetName.Separator, '_') + "_" + role.Sampler;

            return AssetName.Normalise(leaf);
        }

        /* The mask retail would most likely have used here: the one the level's own materials of this
         * family sit on most often. Counted over MATERIALS, not shader entries - every shipped entry
         * carries its own mask, so entry counts are all 1 and "most common" would degenerate to the
         * lowest mask, which is a bare shader with nothing turned on. */
        private static bool TryMostUsedMask(Materials materials, Shaders shaders, SHADER_LIST family, string gameRoot, out long mask)
        {
            mask = 0;
            Dictionary<long, int> uses = new Dictionary<long, int>();
            foreach (Materials.Material material in materials.Entries)
            {
                if (material?.Shader == null || material.Shader.Ubershader != family) continue;
                long m = material.Shader.UbershaderFeatureFlags;
                uses[m] = uses.ContainsKey(m) ? uses[m] + 1 : 1;
            }

            if (uses.Count != 0)
            {
                mask = uses.OrderByDescending(o => o.Value).ThenBy(o => o.Key).First().Key;
                return true;
            }

            //Nothing of this family in the level - fall back to whatever can be resolved at all
            List<ShaderPermutationService.Permutation> available =
                ShaderPermutationService.AvailablePermutations(materials, shaders, family, gameRoot);
            if (available.Count == 0) return false;
            mask = available[0].Mask;
            return true;
        }

        /* Set every sampler-gated feature from what we actually found, and the handful of features a
         * model file can speak to directly. Everything else stays as the donor mask had it. */
        private static long ApplyFeatures(long baseMask, SHADER_LIST family, Plan plan, Assimp.Material source, Mesh mesh)
        {
            long mask = baseMask;

            foreach (string sampler in ShaderUtility.GetSamplers(family) ?? new List<string>())
            {
                int bit = FeatureBit(family, FeatureForSampler(family, sampler));
                if (bit == -1) continue;

                bool bound = plan.Textures.Any(o => o.Usable && o.Role.Sampler == sampler);
                if (bound) mask |= 1L << bit;
                else mask &= ~(1L << bit);
            }

            SetIf(ref mask, family, "DOUBLE_SIDED", source != null && source.HasTwoSided && source.IsTwoSided);
            SetIf(ref mask, family, "VERTEX_COLOUR", mesh != null && mesh.VertexColorChannelCount > 0);

            /* Emissive is deliberately left alone. CA_ENVIRONMENT has an EMISSIVE feature but no
             * emissive sampler, so a model's emissive MAP cannot be carried at all - and a glTF that
             * has one sets emissiveFactor to white, which turned into a material glowing at full
             * brightness over its whole surface. A constant glow is not what the model says, so it is
             * better to leave the feature off and let it be ticked in the material editor. */
            if (source != null && source.HasColorEmissive && Luminance(source.ColorEmissive) > 0.001f)
                plan.Notes.Add("This model material is emissive. There is no emissive map on this shader type, so it has been left off - turn EMISSIVE on in the material editor if you want it.");

            return mask;
        }

        private static void SetIf(ref long mask, SHADER_LIST family, string feature, bool on)
        {
            int bit = FeatureBit(family, feature);
            if (bit == -1) return;
            if (on) mask |= 1L << bit;
            else mask &= ~(1L << bit);
        }

        private static float Luminance(Color4D colour)
        {
            return 0.2126f * colour.R + 0.7152f * colour.G + 0.0722f * colour.B;
        }

        /* Read-only: describing a plan must not add anything to the level's shader pool, because the
         * user can change the family in the dropdown, or close the window, and an entry left behind
         * by a plan nobody carried out is saved into the level forever. */
        private static bool CanResolve(Materials materials, Shaders shaders, SHADER_LIST family, long mask, string gameRoot, out PermutationSource source)
        {
            foreach (ShaderPermutationService.Permutation permutation in
                     ShaderPermutationService.AvailablePermutations(materials, shaders, family, gameRoot))
            {
                if (permutation.Mask != mask) continue;
                source = permutation.Source;
                return true;
            }

            if (ShaderPermutationService.CanBuildArbitraryPermutations(family))
            {
                source = PermutationSource.Recompiled;
                return true;
            }

            source = PermutationSource.None;
            return false;
        }

        /* When the exact combination can't be had, take the shipped one that costs least to live
         * with. Asking for a feature we have no texture for is much worse than going without one we
         * could have fed: the first renders a material sampling a texture that isn't there, the
         * second just loses a map. */
        private static bool TryNearestPermutation(Materials materials, Shaders shaders, SHADER_LIST family, string gameRoot,
                                                  long wanted, Plan plan, out long nearest)
        {
            nearest = 0;
            List<ShaderPermutationService.Permutation> available =
                ShaderPermutationService.AvailablePermutations(materials, shaders, family, gameRoot);
            if (available.Count == 0) return false;

            long samplerBits = 0;
            foreach (string sampler in ShaderUtility.GetSamplers(family) ?? new List<string>())
            {
                int bit = FeatureBit(family, FeatureForSampler(family, sampler));
                if (bit != -1) samplerBits |= 1L << bit;
            }

            long best = 0;
            int bestCost = int.MaxValue;
            foreach (ShaderPermutationService.Permutation permutation in available)
            {
                int cost = 0;
                long diff = permutation.Mask ^ wanted;
                for (int b = 0; b < 64; b++)
                {
                    long bit = 1L << b;
                    if ((diff & bit) == 0) continue;
                    if ((samplerBits & bit) == 0) cost += 1;                       //not about a texture at all
                    else if ((permutation.Mask & bit) != 0) cost += 100;           //wants a map we do not have
                    else cost += 8;                                               //drops a map we do have
                }
                if (cost < bestCost) { bestCost = cost; best = permutation.Mask; }
            }

            nearest = best;
            return true;
        }

        /* A permutation that doesn't declare a feature can't sample its map, so a texture whose
         * feature the chosen mask leaves off has nowhere to go. */
        private static void DropTexturesTheMaskCannotUse(Plan plan, SHADER_LIST family, long mask)
        {
            foreach (PlannedTexture texture in plan.Textures)
            {
                if (!texture.Usable) continue;
                int bit = FeatureBit(family, FeatureForSampler(family, texture.Role.Sampler));
                if (bit == -1) continue;                                  //ungated, always fine
                if ((mask & (1L << bit)) != 0) continue;
                texture.Problem = "the chosen shader permutation has no " + texture.Role.Sampler.ToLower().Replace('_', ' ');
            }
        }

        private static List<string> DescribeFeatures(SHADER_LIST family, long mask)
        {
            List<string> on = new List<string>();
            foreach (string feature in ShaderUtility.GetFeatures(family) ?? new List<string>())
            {
                int bit = FeatureBit(family, feature);
                if (bit != -1 && (mask & (1L << bit)) != 0) on.Add(feature);
            }
            return on;
        }

        #endregion

        #region GENERATION

        /// <summary>
        /// Carry out a plan: import whatever textures it needs into the level, create the material,
        /// and bind them. Returns null with a reason on failure, having changed nothing that matters.
        /// </summary>
        public static Materials.Material Generate(Plan plan, Assimp.Material source, Level level, string gameRoot, out string error)
        {
            error = null;
            if (plan == null || !plan.CanGenerate)
            {
                error = plan?.Error ?? "There is nothing to generate.";
                return null;
            }

            /* Importing the same model twice - or re-importing one after deleting it, which leaves its
             * materials behind because nothing else knows they were only for it - must land back on
             * the material that is already there. Minting "name_1", "name_2" would fill the level with
             * copies nobody asked for, and every one of them carries its own shader entry. */
            Materials.Material existing = plan.AlwaysCreateNew ? null : FindEquivalent(plan, level);
            if (existing != null)
                return existing;

            //Textures first: a half-made material with no maps is worse than no material at all
            Dictionary<PlannedTexture, Textures.TEX4> imported = new Dictionary<PlannedTexture, Textures.TEX4>();
            foreach (PlannedTexture planned in plan.UsableTextures)
            {
                Textures.TEX4 texture = planned.Existing ?? ImportTexture(planned, level, out error);
                if (texture == null)
                {
                    error = "Could not import " + planned.Role.Description.ToLower() + ": " + (error ?? "unknown reason");
                    return null;
                }
                imported[planned] = texture;
            }

            /* The permutation goes in through CreateMaterial rather than being rebound afterwards: it
             * clones the shader entry before handing it back, and a rebind can legitimately land on a
             * pool entry another material is already using - whose sampler remaps we would then
             * overwrite with this material's textures. */
            string name = AssetName.MakeUnique(AssetName.Normalise(plan.Name), level.Materials.Entries.Select(o => o.Name));
            Materials.Material material = ShaderPermutationService.CreateMaterial(
                level.Materials, level.Shaders, plan.Family, name, gameRoot, out error, plan.Mask);
            if (material == null)
                return null;

            SeedConstantsFromDonor(material, level.Materials, plan.Family);
            ApplySourceMaterialValues(material, source, plan.Family);

            foreach (KeyValuePair<PlannedTexture, Textures.TEX4> entry in imported)
                BindTexture(material, entry.Key.SamplerIndex, entry.Value);

            return material;
        }

        /// <summary>
        /// A material already in the level that this plan would only recreate: same name, same shader
        /// family and permutation, and the same texture in every sampler the plan fills. Anything less
        /// than all of that and it is a different material that happens to share a name, so a new one
        /// is built beside it.
        /// </summary>
        /// <summary>Whether <see cref="Generate"/> would reuse a material rather than build one.</summary>
        public static Materials.Material WouldReuse(Plan plan, Level level)
        {
            if (plan == null || plan.AlwaysCreateNew || level?.Materials == null) return null;
            return FindEquivalent(plan, level);
        }

        private static Materials.Material FindEquivalent(Plan plan, Level level)
        {
            string wanted = AssetName.Normalise(plan.Name);
            foreach (Materials.Material candidate in level.Materials.Entries)
            {
                if (candidate?.Shader == null) continue;
                if (!SameName(candidate.Name, wanted)) continue;
                if (candidate.Shader.Ubershader != plan.Family) continue;
                if (candidate.Shader.UbershaderFeatureFlags != plan.Mask) continue;
                if (SameTextures(plan, candidate)) return candidate;
            }
            return null;
        }

        /* The name this plan wants, or that name with the number a previous import had to add to it.
         * Without the second case a model imported three times leaves "x", "x_1" and "x_2" behind: the
         * first import renames itself out of the way of something else, and every import after that
         * then fails to recognise its own work. */
        private static bool SameName(string candidate, string wanted)
        {
            string tidy = AssetName.Normalise(candidate);
            if (string.Equals(tidy, wanted, StringComparison.OrdinalIgnoreCase)) return true;
            if (tidy.Length <= wanted.Length + 1) return false;
            if (!tidy.StartsWith(wanted + "_", StringComparison.OrdinalIgnoreCase)) return false;

            for (int i = wanted.Length + 1; i < tidy.Length; i++)
                if (tidy[i] < '0' || tidy[i] > '9') return false;
            return true;
        }

        private static bool SameTextures(Plan plan, Materials.Material candidate)
        {
            foreach (PlannedTexture planned in plan.UsableTextures)
            {
                //Only a texture already in the level can match - a plan that still has to import one cannot
                if (planned.Existing == null) return false;
                if (planned.SamplerIndex < 0 || planned.SamplerIndex >= candidate.Shader.SamplerRemaps.Count) return false;

                int reference = candidate.Shader.SamplerRemaps[planned.SamplerIndex];
                if (reference == 255 || reference >= candidate.TextureReferences.Count) return false;
                if (!ReferenceEquals(candidate.TextureReferences[reference]?.Texture, planned.Existing)) return false;
            }
            return true;
        }

        /// <summary>
        /// Longest edge of the always-resident copy of an imported texture. Retail's own choice: of
        /// the level textures that carry both parts, 1,457 have a 128px resident edge and 683 have
        /// 32px, so 128 sits at the generous end of what the game itself ships.
        /// </summary>
        private const int ResidentEdge = 128;

        private static Textures.TEX4 ImportTexture(PlannedTexture planned, Level level, out string error)
        {
            error = null;
            string sourceFile = planned.SourcePath;
            string scratch = null;
            try
            {
                if (sourceFile == null)
                {
                    sourceFile = scratch = WriteEmbeddedToDisk(planned.Embedded, out error);
                    if (sourceFile == null) return null;
                }

                byte[] dds = TextureConverter.Convert(sourceFile, planned.Role.Format, 0, out error);
                if (dds == null) return null;

                Textures.TEX4 texture = new Textures.TEX4
                {
                    Name = AssetName.MakeUnique(planned.TextureName, level.Textures.Entries.Select(o => o.Name))
                };

                Textures.TextureFormat format;
                Textures.TextureStateFlag state;
                Textures.TextureUsageFlag usage;
                Textures.TEX4.Texture part = dds.ToTEX4Part(out format, out state, out usage);
                if (part == null)
                {
                    error = "the converted image could not be read back";
                    return null;
                }

                texture.Format = planned.Role.Format;
                texture.StateFlags = state;
                texture.UsageFlags = usage;

                /* Colour space is a property of what the texture is FOR, not of the file it came from:
                 * every shipped diffuse, AO and dirt map is sRGB and no normal map ever is. */
                if (planned.Role.SRGB) texture.StateFlags |= Textures.TextureStateFlag.ALLOW_SRGB;
                else texture.StateFlags &= ~Textures.TextureStateFlag.ALLOW_SRGB;

                /* A texture needs a RESIDENT copy as well as a streamed one. The streamed part is
                 * paged in on demand; the persistent part is what the surface samples until it
                 * arrives, and a texture with an empty one renders as flat untextured colour - which
                 * is what an imported model looked like in game while everything about its material
                 * was correct. Retail ships both parts on 2,977 of 3,087 level textures, and sizes
                 * the resident one to a small edge: 128px on 1,457 and 32px on 683, which is 90% of
                 * them between the two. */
                texture.TextureStreamed = part.Copy();

                int drop = TextureConverter.DropForEdge(part.Width, part.Height, ResidentEdge, part.MipLevels);
                if (drop <= 0)
                {
                    //Already small enough to just be resident, which is what retail does with 590 of them
                    texture.TextureStreamed = new Textures.TEX4.Texture();
                    texture.TexturePersistent = part.Copy();
                }
                else
                {
                    Textures.TEX4.Texture resident = TextureConverter.Slice(part, texture.Format, drop);
                    texture.TexturePersistent = resident ?? part.Copy();
                }

                level.Textures.Entries.Add(texture);
                return texture;
            }
            finally
            {
                if (scratch != null)
                    try { File.Delete(scratch); } catch { }
            }
        }

        /* texconv reads files, so an image that only exists inside the model has to land on disk
         * first. Compressed data is a whole PNG/JPEG; uncompressed is raw BGRA texels. */
        private static string WriteEmbeddedToDisk(EmbeddedTexture embedded, out string error)
        {
            error = null;
            if (embedded == null) { error = "there is no image data"; return null; }

            try
            {
                if (embedded.HasCompressedData)
                {
                    string hint = string.IsNullOrEmpty(embedded.CompressedFormatHint) ? "png" : embedded.CompressedFormatHint.Trim('.', ' ');
                    string path = Path.Combine(Path.GetTempPath(), "opencage_embedded_" + Guid.NewGuid().ToString("N") + "." + hint);
                    File.WriteAllBytes(path, embedded.CompressedData);
                    return path;
                }

                if (embedded.HasNonCompressedData)
                {
                    string path = Path.Combine(Path.GetTempPath(), "opencage_embedded_" + Guid.NewGuid().ToString("N") + ".png");
                    using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(embedded.Width, embedded.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                    {
                        Texel[] texels = embedded.NonCompressedData;
                        for (int y = 0; y < embedded.Height; y++)
                            for (int x = 0; x < embedded.Width; x++)
                            {
                                Texel texel = texels[(y * embedded.Width) + x];
                                bitmap.SetPixel(x, y, System.Drawing.Color.FromArgb(texel.A, texel.R, texel.G, texel.B));
                            }
                        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    return path;
                }
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }

            error = "the embedded image is empty";
            return null;
        }

        /* A brand new material starts with every shader constant at zero, which for most parameters
         * means invisible - a zero UV multiplier samples one texel, a zero tint is black. Seed them
         * from the level's closest working material of the same family instead, then let the model's
         * own values override the few it can actually speak to. */
        private static void SeedConstantsFromDonor(Materials.Material material, Materials materials, SHADER_LIST family)
        {
            //Sensible values first, so parameters the donor doesn't carry - and a level with no
            //materials at all to donate from - still come out with something workable
            ApplyFallbackDefaults(material, family);
            material.Priority = DefaultPriority(family);

            Materials.Material donor = FindDonor(materials, material, family);
            if (donor?.Shader == null) return;

            CopyConstants(donor.Shader.PixelShaderParameterRemaps, donor.PixelShaderConstants,
                          material.Shader.PixelShaderParameterRemaps, material.PixelShaderConstants, family);
            CopyConstants(donor.Shader.VertexShaderParameterRemaps, donor.VertexShaderConstants,
                          material.Shader.VertexShaderParameterRemaps, material.VertexShaderConstants, family);

            material.PhysicalMaterialIndex = donor.PhysicalMaterialIndex;
        }

        /* Render priority selects the pass, and the wrong pass is not a subtle difference: measured in
         * game on an imported model, priority 31 draws it untextured over everything, 39 draws it
         * unlit, 52 draws it translucent, and only 70 draws it lit, opaque and textured. The donor is
         * picked for its constants and can sit in any of those passes, so taking its priority as well
         * made a generated material render correctly only by luck.
         *
         * The pass a material belongs in is a property of its shader family, so that is what this
         * table is: the modal priority of each family over ~48,000 materials in six pristine levels.
         * CA_ENVIRONMENT 14,028 of 17,493 at 70, CA_SKIN 1,622 of 1,694 at 59, CA_PARTICLE 17,786 of
         * 20,330 at 39, and so on down. Anything not listed falls back to the world pass. */
        private static int DefaultPriority(SHADER_LIST family)
        {
            switch (family)
            {
                case SHADER_LIST.CA_OCCLUSION_CULLING: return 82;
                case SHADER_LIST.CA_LIGHTMAP_ENVIRONMENT: return 69;
                case SHADER_LIST.CA_SKIN:
                case SHADER_LIST.CA_EYE: return 59;
                case SHADER_LIST.CA_HAIR: return 58;
                case SHADER_LIST.CA_DECAL:
                case SHADER_LIST.CA_EFFECT_OVERLAY:
                case SHADER_LIST.CA_WATER_CAUSTICS_OVERLAY: return 52;
                case SHADER_LIST.CA_SKIN_OCCLUSION: return 48;
                case SHADER_LIST.CA_LIGHT_DECAL: return 46;
                case SHADER_LIST.CA_DEFERRED: return 45;
                case SHADER_LIST.CA_PLANET: return 44;
                case SHADER_LIST.CA_NONINTERACTIVE_WATER:
                case SHADER_LIST.CA_SIMPLEWATER: return 42;
                case SHADER_LIST.CA_PARTICLE:
                case SHADER_LIST.CA_RIBBON:
                case SHADER_LIST.CA_FOGPLANE:
                case SHADER_LIST.CA_FOGSPHERE:
                case SHADER_LIST.CA_VOLUME_LIGHT:
                case SHADER_LIST.CA_DECAL_ENVIRONMENT:
                case SHADER_LIST.CA_LIQUID_ENVIRONMENT: return 39;
                case SHADER_LIST.CA_SPACESUIT_VISOR: return 29;
                case SHADER_LIST.CA_REFRACTION:
                case SHADER_LIST.CA_SIMPLE_REFRACTION: return 10;
                default: return 70;
            }
        }

        /* Zero is the wrong default for most shader parameters: a zero UV multiplier samples a single
         * texel and a zero tint is black, so a material built from zeroes renders as a flat dark
         * smear. These are the values retail actually ships, read off the parameter census - every one
         * of the 528 materials on CA_ENVIRONMENT's most-used permutation carries FRESNEL_INTENSITY 1,
         * DIFFUSE_UV_MULT 1, DIFFUSE_TINT 1 and EMISSIVE_MULT 0.1, and CA_CHARACTER agrees on the
         * multipliers, SPECULAR_POWER 0.85 and DIFFUSE_ROUGHNESS_FACTOR 0.5. */
        private static void ApplyFallbackDefaults(Materials.Material material, SHADER_LIST family)
        {
            foreach (string parameter in ShaderUtility.GetParameters(family) ?? new List<string>())
            {
                float value;
                if (parameter.EndsWith("_UV_MULT", StringComparison.Ordinal)
                    || parameter.EndsWith("_UV_SCALE", StringComparison.Ordinal)
                    || parameter.EndsWith("_MAP_MULT", StringComparison.Ordinal)
                    || parameter.EndsWith("_MAP_STRENGTH", StringComparison.Ordinal)
                    || parameter.EndsWith("_TINT", StringComparison.Ordinal)
                    || parameter == "FRESNEL_INTENSITY")
                    value = 1.0f;
                else if (parameter.EndsWith("SPECULAR_POWER", StringComparison.Ordinal))
                    value = 0.85f;
                else if (parameter == "DIFFUSE_ROUGHNESS_FACTOR")
                    value = 0.5f;
                else if (parameter == "EMISSIVE_MULT")
                    value = 0.1f;
                else
                    continue;                                   //zero is right, or we have nothing better

                SetParameter(material, family, parameter, value, value, value, value);
            }
        }

        private static Materials.Material FindDonor(Materials materials, Materials.Material exclude, SHADER_LIST family)
        {
            Materials.Material best = null;
            int bestDistance = int.MaxValue;
            int bestUses = -1;

            //How many materials sit on each mask, so a tie goes to the one the level leans on most
            Dictionary<long, int> uses = new Dictionary<long, int>();
            foreach (Materials.Material candidate in materials.Entries)
            {
                if (candidate?.Shader == null || candidate.Shader.Ubershader != family) continue;
                long m = candidate.Shader.UbershaderFeatureFlags;
                uses[m] = uses.ContainsKey(m) ? uses[m] + 1 : 1;
            }

            foreach (Materials.Material candidate in materials.Entries)
            {
                if (candidate == exclude || candidate?.Shader == null || candidate.Shader.Ubershader != family) continue;

                int distance = PopCount(candidate.Shader.UbershaderFeatureFlags ^ exclude.Shader.UbershaderFeatureFlags);
                int candidateUses = uses[candidate.Shader.UbershaderFeatureFlags];
                if (distance > bestDistance) continue;
                if (distance == bestDistance && candidateUses <= bestUses) continue;

                best = candidate;
                bestDistance = distance;
                bestUses = candidateUses;
            }
            return best;
        }

        private static void CopyConstants(List<int> fromRemaps, List<float> from, List<int> toRemaps, List<float> to, SHADER_LIST family)
        {
            if (fromRemaps == null || toRemaps == null) return;

            for (int id = 0; id < toRemaps.Count && id < fromRemaps.Count; id++)
            {
                int target = toRemaps[id];
                int origin = fromRemaps[id];
                if (target == 255 || origin == 255) continue;

                int width = UberShaderRecompiler.ParamWidth(family, id);
                for (int component = 0; component < width; component++)
                {
                    if (origin + component >= from.Count || target + component >= to.Count) break;
                    to[target + component] = from[origin + component];
                }
            }
        }

        private static int PopCount(long value)
        {
            int count = 0;
            ulong v = (ulong)value;
            while (v != 0) { count += (int)(v & 1); v >>= 1; }
            return count;
        }

        /* The values a model file genuinely carries. Tints only: a DCC's shininess and reflectivity
         * are on scales that have no agreed meaning, and guessing at them would quietly overwrite
         * donor values that are known to work. */
        private static void ApplySourceMaterialValues(Materials.Material material, Assimp.Material source, SHADER_LIST family)
        {
            /* UV multipliers are the one donor value that is always wrong here. They tile a texture
             * across geometry the donor was authored for - a donor with NORMAL_UV_MULT 7 tiles its
             * normal map seven times - whereas an imported mesh arrives with UVs that already lay its
             * own textures out 1:1. Nothing about the donor transfers; the answer is always 1. */
            foreach (string parameter in ShaderUtility.GetParameters(family) ?? new List<string>())
                if (parameter.EndsWith("_UV_MULT", StringComparison.Ordinal) || parameter.EndsWith("_UV_SCALE", StringComparison.Ordinal))
                    SetParameter(material, family, parameter, 1.0f, 1.0f, 1.0f, 1.0f);

            if (source == null) return;

            if (source.HasColorDiffuse)
                SetParameter(material, family, "DIFFUSE_TINT", source.ColorDiffuse.R, source.ColorDiffuse.G, source.ColorDiffuse.B, 1.0f);

            if (source.HasColorSpecular)
                SetParameter(material, family, "SPECULAR_TINT", Luminance(source.ColorSpecular), Luminance(source.ColorSpecular), Luminance(source.ColorSpecular), 1.0f);

            /* No emissive here either - see ApplyFeatures. Writing the tint without the feature would
             * be harmless, but writing the multiplier would make the material glow the moment anyone
             * ticked EMISSIVE, which is not a surprise worth leaving behind. */
        }

        private static void SetParameter(Materials.Material material, SHADER_LIST family, string parameter, float x, float y, float z, float w)
        {
            int? id = ShaderUtility.GetShaderFunctionalityIndex(family, ShaderIndexType.PARAMETERS, parameter);
            if (id == null) return;

            UberShaderParameterType? type = ShaderUtility.GetParameterType(family, parameter);
            if (type == null) return;

            int width = ComponentCount(type.Value);
            float[] values = new float[] { x, y, z, w };

            WriteParameter(material.Shader.PixelShaderParameterRemaps, material.PixelShaderConstants, id.Value, values, width);
            WriteParameter(material.Shader.VertexShaderParameterRemaps, material.VertexShaderConstants, id.Value, values, width);
        }

        private static void WriteParameter(List<int> remaps, List<float> constants, int id, float[] values, int width)
        {
            if (remaps == null || id >= remaps.Count) return;
            int slot = remaps[id];
            if (slot == 255) return;

            for (int component = 0; component < width && component < values.Length; component++)
            {
                if (slot + component >= constants.Count) break;
                constants[slot + component] = values[component];
            }
        }

        private static int ComponentCount(UberShaderParameterType type)
        {
            switch (type)
            {
                case UberShaderParameterType.Float2:
                case UberShaderParameterType.Half2: return 2;
                case UberShaderParameterType.Float3:
                case UberShaderParameterType.Half3: return 3;
                case UberShaderParameterType.Float4:
                case UberShaderParameterType.Half4: return 4;
                default: return 1;
            }
        }

        private static void BindTexture(Materials.Material material, int samplerIndex, Textures.TEX4 texture)
        {
            if (samplerIndex < 0 || texture == null) return;

            while (material.Shader.SamplerRemaps.Count <= samplerIndex)
                material.Shader.SamplerRemaps.Add(255);

            int reference = material.TextureReferences.Count;
            material.TextureReferences.Add(new TexturePtr { Texture = texture, Location = TexturePtr.Source.LEVEL });
            material.Shader.SamplerRemaps[samplerIndex] = reference;
        }

        #endregion
    }
}
