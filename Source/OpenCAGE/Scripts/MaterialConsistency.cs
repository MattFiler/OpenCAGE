using CATHODE;
using CATHODE.ShaderTypes;
using System.Collections.Generic;

namespace OpenCAGE
{
    /// <summary>
    /// The rules every shipped material obeys, and which the engine relies on. Changing a material's
    /// feature combination binds a new shader but leaves its textures alone, so it is easy to end up
    /// with a material that declares a feature it cannot feed - which renders wrongly rather than
    /// failing, so nothing says what went wrong.
    ///
    /// Measured over 15,625 CA_ENVIRONMENT materials in five pristine levels (Solace, Tech_Hub,
    /// Sci_Hub, Tech_RnD_HzdLab, Frontend), with **zero exceptions in either direction**:
    ///
    /// - a sampler-gated feature is set in exactly the materials that bind its sampler (0 materials
    ///   declare a feature with an empty sampler, 0 bind a sampler with the feature off);
    /// - ENVIRONMENT_MAPPING is on in exactly the materials whose EnvironmentMapIndex is not 255
    ///   (8,699 on, none at 255; 6,926 off, none with an index).
    /// </summary>
    public static class MaterialConsistency
    {
        public enum Kind
        {
            /// <summary>The mask declares a feature, but the texture it samples is not bound.</summary>
            FeatureWithoutTexture,

            /// <summary>A texture is bound to a sampler the mask does not turn on, so it is never read.</summary>
            TextureWithoutFeature,

            /// <summary>ENVIRONMENT_MAPPING and the material's environment map disagree.</summary>
            EnvironmentMap,
        }

        /// <summary>
        /// What retail sets a hand-held weapon's materials to. The pistol, boltgun and welding torch
        /// in BSP_TORRENS are all 70, and so is every other world material; the motion tracker is the
        /// one weapon that draws in the first-person overlay pass at 31, and its materials carry a far
        /// richer feature mask than a generated one has. Measured in game, a plain diffuse-plus-normal
        /// material renders untextured at 31, unlit at 39 and translucent at 52, so 70 is also the
        /// only pass that draws an imported model the way its preview looks.
        /// </summary>
        public const int WorldPriority = 70;

        public class Problem
        {
            public Kind Type;
            public string Text;
        }

        /* Both rules were then re-measured PER FAMILY over ~48,000 materials in six pristine levels,
         * because they are not universal and a check that cries wolf on shipped data is worse than no
         * check. 29 families come back with zero violations, including the two that matter most here
         * (CA_ENVIRONMENT 16,904 and CA_CHARACTER 3,579). The exclusions below are the families that
         * genuinely break one of the rules in retail data, so nothing is said about them. */
        /* Per (family, sampler) rather than per family: the exceptions are individual samplers that do
         * not track their feature, not whole families, and excluding the family would stop the check
         * saying anything useful about the rest of it. Measured counts are in the comments. */
        private static readonly HashSet<string> SkipSampler = new HashSet<string>
        {
            "CA_DECAL.ALPHATHRESHOLD_MAP",              //69 of 288 declare ALPHATHRESHOLD with no map
            "CA_DECAL_ENVIRONMENT.ALPHATHRESHOLD_MAP",  //6 of 54
            "CA_SIMPLEWATER.ENVIRONMENT_MAP",           //6 of 6 bind one with the feature off
        };

        /* Families whose ENVIRONMENT_MAPPING does not come from the material's own EnvironmentMapIndex
         * - characters and decals reflect a shared or irradiance cube instead, so the index stays 255
         * (CA_SKIN 684 of 1,240, CA_EYE 138 of 138, CA_DECAL 187 of 288), and CA_SIMPLEWATER carries an
         * index with the feature off (6 of 6). */
        private static readonly HashSet<SHADER_LIST> SkipEnvironmentMapRule = new HashSet<SHADER_LIST>
        {
            SHADER_LIST.CA_SKIN,
            SHADER_LIST.CA_EYE,
            SHADER_LIST.CA_DECAL,
            SHADER_LIST.CA_SPACESUIT_VISOR,
            SHADER_LIST.CA_SURFACE_EFFECTS,
            SHADER_LIST.CA_SIMPLEWATER,
        };

        public static List<Problem> Check(Materials.Material material)
        {
            List<Problem> problems = new List<Problem>();
            if (material?.Shader == null) return problems;

            SHADER_LIST family = material.Shader.Ubershader;
            long mask = material.Shader.UbershaderFeatureFlags;
            bool checkEnvironment = !SkipEnvironmentMapRule.Contains(family);

            foreach (string sampler in ShaderUtility.GetSamplers(family) ?? new List<string>())
            {
                if (SkipSampler.Contains(family + "." + sampler)) continue;

                string feature = MaterialGenerator.FeatureForSampler(family, sampler);
                if (feature == null) continue;                       //ungated, always sampled

                int? bit = ShaderUtility.GetShaderFunctionalityIndex(family, ShaderIndexType.FEATURES, feature);
                if (bit == null) continue;

                bool on = (mask & (1L << bit.Value)) != 0;
                bool bound = IsBound(material, family, sampler);

                if (on && !bound)
                    problems.Add(new Problem
                    {
                        Type = Kind.FeatureWithoutTexture,
                        Text = feature + " is on but no " + Pretty(sampler) + " is set."
                    });
                else if (bound && !on)
                    problems.Add(new Problem
                    {
                        Type = Kind.TextureWithoutFeature,
                        Text = "A " + Pretty(sampler) + " is set but " + feature + " is off, so it is never read."
                    });
            }

            int? environment = checkEnvironment
                ? ShaderUtility.GetShaderFunctionalityIndex(family, ShaderIndexType.FEATURES, "ENVIRONMENT_MAPPING")
                : null;
            if (environment != null)
            {
                bool on = (mask & (1L << environment.Value)) != 0;
                bool hasIndex = material.EnvironmentMapIndex != 255;
                if (on && !hasIndex)
                    problems.Add(new Problem { Type = Kind.EnvironmentMap, Text = "ENVIRONMENT_MAPPING is on but this material has no environment map." });
                else if (hasIndex && !on)
                    problems.Add(new Problem { Type = Kind.EnvironmentMap, Text = "This material has an environment map but ENVIRONMENT_MAPPING is off." });
            }

            /* There was a rule here saying a NO_CLIP material above priority 52 would draw behind the
             * player's hands, and it was wrong in both directions: re-measured with the family's own
             * feature ordering, the NO_CLIP materials retail ships sit at 39 (300), 70 (41), 10, 52,
             * 59 and 56 - not one of them at 31. Following it is what put an imported model in the
             * overlay pass, where it renders untextured. NO_CLIP and priority are independent. */

            return problems;
        }

        private static bool IsBound(Materials.Material material, SHADER_LIST family, string sampler)
        {
            int? index = ShaderUtility.GetShaderFunctionalityIndex(family, ShaderIndexType.SAMPLERS, sampler);
            if (index == null || index.Value >= material.Shader.SamplerRemaps.Count) return false;

            int reference = material.Shader.SamplerRemaps[index.Value];
            if (reference == 255 || reference >= material.TextureReferences.Count) return false;
            return material.TextureReferences[reference]?.Texture != null;
        }

        /// <summary>"SECONDARY_NORMAL_MAP" as "secondary normal map".</summary>
        private static string Pretty(string sampler)
        {
            return sampler.Replace('_', ' ').ToLowerInvariant();
        }

        /// <summary>
        /// How many textures a material would still need to satisfy a mask it is not on yet - for
        /// telling someone what a permutation will cost them before they pick it.
        /// </summary>
        public static int TexturesNeededFor(Materials.Material material, long mask)
        {
            if (material?.Shader == null) return 0;

            SHADER_LIST family = material.Shader.Ubershader;
            int needed = 0;
            foreach (string sampler in ShaderUtility.GetSamplers(family) ?? new List<string>())
            {
                string feature = MaterialGenerator.FeatureForSampler(family, sampler);
                if (feature == null) continue;

                int? bit = ShaderUtility.GetShaderFunctionalityIndex(family, ShaderIndexType.FEATURES, feature);
                if (bit == null || (mask & (1L << bit.Value)) == 0) continue;
                if (!IsBound(material, family, sampler)) needed++;
            }
            return needed;
        }
    }
}
