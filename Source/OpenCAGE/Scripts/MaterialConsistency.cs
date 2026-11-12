using CATHODE;
using CATHODE.ShaderTypes;
using System.Collections.Generic;
using System.Linq;

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
        /// The lit opaque pass: world geometry, props and characters (most retail CA_ENVIRONMENT materials). In game a plain
        /// diffuse-plus-normal material renders untextured at 31, unlit at 39 and translucent at 52, so 70 is the pass that draws
        /// an opaque imported model the way its preview looks.
        /// </summary>
        public const int WorldPriority = 70;

        /// <summary>
        /// The see-through pass. Retail's glass (GLASS_Clean, GLASS_Dirty, KEY_CARD_GLASS, VISOR_GLASSCA, BROKEN_GLASS_SHARD) is
        /// CA_ENVIRONMENT FORCE_TO_ALPHA at 39 (the rest at 42 and 52, none at 70), as are particles. A FORCE_TO_ALPHA pane at
        /// 70 does not draw at all in game, and at 52 it draws flat pale white.
        /// </summary>
        public const int SeeThroughPriority = 39;

        /// <summary>
        /// The first-person pass. Retail's held items are NO_CLIP at 31: its NO_CLIP CA_ENVIRONMENT materials are at 31, or 52
        /// for throwables' and the tracker's decals, none above; CA_CHARACTER's (the first-person arms) are at 31 and 47, CA_SKIN's
        /// at 32. A NO_CLIP material above ~52 draws behind the arms.
        /// </summary>
        public const int FirstPersonPriority = 31;

        /// <summary>A draw pass a material can be put in by its priority, with an honest name for the editor's picker.</summary>
        public class DrawPass
        {
            public int Priority;
            public string Name;
            public string Description;
            public override string ToString() => Priority + " - " + Name;
        }

        /// <summary>
        /// The passes the Material Editor and MCP name, most used first. What each does is measured in game, except 42, which is
        /// named for what retail puts there.
        /// </summary>
        public static readonly DrawPass[] DrawPasses = new DrawPass[]
        {
            new DrawPass { Priority = WorldPriority, Name = "lit opaque", Description = "World geometry, props and characters: lit, opaque and textured. Most materials draw here." },
            new DrawPass { Priority = 52, Name = "mesh decal", Description = "Blended onto the surface behind it and lit as that surface (decals, terrain strips). Glass here draws flat pale white." },
            new DrawPass { Priority = 42, Name = "see-through (late)", Description = "Where retail puts its *_PREPASS window glass, fog planes and water. Not measured in game: prefer 39 for glass." },
            new DrawPass { Priority = SeeThroughPriority, Name = "see-through", Description = "Glass, visors and particles, blended over what is behind them: retail's glass is FORCE_TO_ALPHA here. An opaque material here draws unlit, near-black." },
            new DrawPass { Priority = FirstPersonPriority, Name = "first person", Description = "Held items drawn over the world and in front of the arms: retail's first-person weapons, with NO_CLIP. Without NO_CLIP a plain material here draws untextured over everything." },
        };

        /// <summary>The named pass at this priority, or null.</summary>
        public static DrawPass FindDrawPass(int priority) => DrawPasses.FirstOrDefault(o => o.Priority == priority);

        /// <summary>"39 - see-through", or "57 (no named pass)".</summary>
        public static string DescribeDrawPass(int priority) => FindDrawPass(priority)?.ToString() ?? priority + " (no named pass)";

        /// <summary>
        /// The pass retail puts a material of this family and permutation in, when its features decide it: first person for
        /// NO_CLIP (31; CA_SKIN 32), see-through for FORCE_TO_ALPHA (39), in families whose usual pass is the lit opaque one.
        /// Null when the features don't decide it (the family's usual pass applies).
        /// </summary>
        public static int? DrawPassForFeatures(SHADER_LIST family, long mask)
        {
            if (family == SHADER_LIST.CA_SKIN)
                return HasFeature(family, mask, "NO_CLIP") ? 32 : (int?)null;
            if (family != SHADER_LIST.CA_ENVIRONMENT && family != SHADER_LIST.CA_CHARACTER)
                return null;
            if (HasFeature(family, mask, "NO_CLIP")) return FirstPersonPriority;
            if (HasFeature(family, mask, "FORCE_TO_ALPHA")) return SeeThroughPriority;
            return null;
        }

        /// <summary>
        /// Why this material's priority will draw it wrongly, or null: a see-through (FORCE_TO_ALPHA) material in the lit opaque
        /// pass draws nothing, and a first-person (NO_CLIP) one above 52 draws behind the arms. Retail ships neither.
        /// </summary>
        public static string DrawPassProblem(Materials.Material material)
        {
            if (material?.Shader == null) return null;
            SHADER_LIST family = material.Shader.Ubershader;
            long mask = material.Shader.UbershaderFeatureFlags;
            if (family != SHADER_LIST.CA_ENVIRONMENT && family != SHADER_LIST.CA_CHARACTER) return null;
            if (HasFeature(family, mask, "NO_CLIP") && material.Priority > 52)
                return "NO_CLIP (first person) is on but the draw pass is " + material.Priority + ": above 52 a held item draws behind the arms. Retail's are at " + FirstPersonPriority + ".";
            if (HasFeature(family, mask, "FORCE_TO_ALPHA") && material.Priority >= 60)
                return "FORCE_TO_ALPHA (see-through) is on but the draw pass is " + material.Priority + ": a see-through material there does not draw at all in game. Retail's glass is at " + SeeThroughPriority + ".";
            return null;
        }

        private static bool HasFeature(SHADER_LIST family, long mask, string feature)
        {
            int? bit = ShaderUtility.GetShaderFunctionalityIndex(family, ShaderIndexType.FEATURES, feature);
            return bit != null && (mask & (1L << bit.Value)) != 0;
        }

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

            /* The draw pass is not a feature/texture disagreement, so it is not one of these problems: DrawPassProblem says when
             * a material's priority is wrong for its NO_CLIP / FORCE_TO_ALPHA features (the Material Editor's Draw pass row and
             * edit_material show it). */

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

        /// <summary>
        /// Put a material's environment map index in step with its ENVIRONMENT_MAPPING feature, as every shipped
        /// material has it: with the feature on, the index names the texture slot its ENVIRONMENT_MAP sampler reads;
        /// with it off, 255. Changing features or binding textures never touched the index, so a material given
        /// environment mapping that way kept 255 - and the game drew it with a black albedo, lit by its reflection
        /// and specular alone (9 Nov 2026: about 130 generated materials a level in the Hadley's Hope rebuild -
        /// terrain, walls - drew black in the albedo debug view). An index that already names a slot is kept: retail
        /// sometimes points it at a slot no sampler reads. Families that reflect a shared cube are left alone.
        /// True when the index changed.
        /// </summary>
        public static bool SyncEnvironmentMapIndex(Materials.Material material)
        {
            if (material?.Shader == null) return false;
            SHADER_LIST family = material.Shader.Ubershader;
            if (SkipEnvironmentMapRule.Contains(family)) return false;
            int? feature = ShaderUtility.GetShaderFunctionalityIndex(family, ShaderIndexType.FEATURES, "ENVIRONMENT_MAPPING");
            if (feature == null) return false;

            int before = material.EnvironmentMapIndex;
            bool on = (material.Shader.UbershaderFeatureFlags & (1L << feature.Value)) != 0;
            if (!on)
                material.EnvironmentMapIndex = 255;
            else if (before == 255 || before >= material.TextureReferences.Count)
            {
                int? sampler = ShaderUtility.GetShaderFunctionalityIndex(family, ShaderIndexType.SAMPLERS, "ENVIRONMENT_MAP");
                if (sampler != null && IsBound(material, family, "ENVIRONMENT_MAP"))
                    material.EnvironmentMapIndex = material.Shader.SamplerRemaps[sampler.Value];
            }
            return material.EnvironmentMapIndex != before;
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
