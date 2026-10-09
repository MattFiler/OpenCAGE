using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.ShaderTypes;
using CathodeLib;
using CathodeLib.ObjectExtensions;
using CathodeLib.Ubershaders;
using Newtonsoft.Json.Linq;
using OpenCAGE.Modding;
using OpenCAGE.UnityConnection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenCAGE.MCP
{
    /// <summary>The open level's materials and material mapping sets, as the Material Editor and Material Mapping Editor show and change them.</summary>
    internal static class McpMaterialTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            #region Materials
            yield return new McpTool()
            {
                Name = "list_materials",
                Title = "List materials",
                Description = "The materials the open level holds, with their shader family and render priority. When names repeat (anywhere in the level, not just in this list) a material is named name#index: pass it back as that. Filter by words, family, features that must be on (e.g. EMISSIVE, NORMAL_MAPPING), a texture it samples, or priority. describe_material shows one in full; find_asset_users shows where one is drawn.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Words the material name must contain."),
                    McpSchema.String("family", "Only this shader family, e.g. CA_ENVIRONMENT."),
                    McpSchema.Strings("require_features", "Only materials with all of these features on, e.g. ['EMISSIVE']."),
                    McpSchema.String("texture", "Only materials sampling this texture (level or GLOBAL)."),
                    McpSchema.Integer("priority", "Only this render priority (70 world, 52 translucent, 39 unlit, 31 overlay)."),
                    McpSchema.Limit(200, "materials"),
                    McpSchema.Offset("materials")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListMaterials,
            };

            yield return new McpTool()
            {
                Name = "describe_material",
                Title = "Describe material",
                Description = "One material in full, as the Material Editor shows it: shader family and permutation mask, each feature (on/off, and whether it can be toggled), each sampler's texture, each parameter's value, render priority, feature/texture consistency problems, and the models drawing with it.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("material", "The material's name (list_materials), or name#index.", required: true)),
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() =>
                {
                    Level level = McpEditor.RequireLevel(forEditing: false).Level;
                    return DescribeMaterial(level, McpAssets.FindMaterial(level, call.Str("material", required: true)), true);
                }),
            };

            yield return new McpTool()
            {
                Name = "create_material",
                Title = "Create material",
                Description = "Make a new material in the open level: on a shader family (optionally a given permutation mask), or as a copy of an existing one (copy_from) with its own shader entry so later sampler edits do not leak into the original. Starts with the family's usual values. One undo step; written by save_level.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("name", "The new material's name (backslash folders allowed). Default for a copy: '<source> Clone'."),
                    McpSchema.String("family", "Shader family for a new material (default CA_ENVIRONMENT; list_material_permutations with no arguments lists them)."),
                    McpSchema.String("mask", "Permutation mask to start on, e.g. '0x1A0' (list_material_permutations); default the family's most-used one."),
                    McpSchema.String("copy_from", "Duplicate this material instead (its textures, parameters and priority)."),
                    McpSchema.Boolean("seed_parameters", "New material: copy parameters from the level's closest material of the family (default true); false leaves them at zero.")),
                Run = CreateMaterial,
            };

            yield return new McpTool()
            {
                Name = "edit_material",
                Title = "Edit material",
                Description = "Change a material as the Material Editor does: bind or clear sampler textures (keeping the sampler's feature in step; GLOBAL textures can be bound too), turn features on/off or pick a whole permutation mask (rebinding the shader, compiling one if needed), set parameters, set render priority. Everything is checked before anything changes. An open Material Editor is closed first (it would write its old values back). " +
                    "One undo step (undo puts the material back exactly; the previous values are returned too). Every model drawing the material changes; written by save_level (no build needed).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("material", "The material's name, or name#index.", required: true),
                    McpSchema.Map("textures", "Sampler name to texture name (the level's, else GLOBAL's), or null to clear: {\"DIFFUSE_MAP\": \"MYMOD\\\\WALL_D\", \"NORMAL_MAP\": null}."),
                    McpSchema.Strings("enable", "Features to turn on, e.g. NORMAL_MAPPING."),
                    McpSchema.Strings("disable", "Features to turn off."),
                    McpSchema.String("mask", "A whole permutation mask instead (e.g. '0x1A0', from list_material_permutations); enable/disable apply on top."),
                    McpSchema.Boolean("sync_features", "Turn a sampler's gating feature on/off with its texture (default true), as every shipped material has it."),
                    McpSchema.Map("parameters", "Parameter name to a number or [x, y, z(, w)]: {\"DIFFUSE_TINT\": [1, 0.5, 0.5, 1], \"SPECULAR_POWER\": 0.8}."),
                    McpSchema.Integer("priority", "Render priority 0-255: 70 world (lit, opaque), 52 translucent, 39 unlit, 31 untextured overlay.")),
                Run = EditMaterialTool,
            };

            yield return new McpTool()
            {
                Name = "list_material_permutations",
                Title = "List material permutations",
                Description = "The shader permutations (feature masks) a family can be given without compiling: the level's own and the shipped-shader database's, most used first, with their features. For a material, also how many textures each would still need. With no arguments, the families a material can be created on.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("material", "List for this material's family, marking its current mask."),
                    McpSchema.String("family", "Or for this shader family."),
                    McpSchema.Strings("require", "Only masks with all of these features on."),
                    McpSchema.Strings("exclude", "Only masks with none of these features on."),
                    McpSchema.Integer("limit", "At most this many (default 40).")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListPermutations,
            };

            yield return new McpTool()
            {
                Name = "retry_shader_harvest",
                Title = "Retry shader harvest",
                Description = "Restart OpenCAGE's background harvest of the permutations shipped in the game's levels (the database that widens what list_material_permutations and edit_material can reach), as the Material Editor's 'Try again' does after it failed. Returns straight away; writes only OpenCAGE's cache under DATA/MODTOOLS/SHADERS.",
                InputSchema = McpSchema.Object(),
                Idempotent = true,
                Run = call =>
                {
                    ShaderDatabase.AutoBuildState before = ShaderDatabase.AutoBuild;
                    bool started = false;
                    if (before == ShaderDatabase.AutoBuildState.Failed || before == ShaderDatabase.AutoBuildState.NotStarted)
                    {
                        ShaderDatabase.ResetAutoBuild();
                        ShaderDatabase.EnsureBuiltInBackground(Singleton.PathToAI);
                        started = true;
                    }
                    JObject result = HarvestState();
                    result["restarted"] = started;
                    if (!started) call.Note(before == ShaderDatabase.AutoBuildState.Running ? "It is already running." : "It has already finished; there is nothing to retry.");
                    return result;
                },
            };
            #endregion

            #region Material mappings
            yield return new McpTool()
            {
                Name = "list_material_mappings",
                Title = "List material mappings",
                Description = "The open level's material mapping sets (each swaps materials: from -> to pairs, by the materials' stored names), with their ids and the entities whose 'mapping' parameter uses them. " +
                    "How a set is used: the 'mapping' parameter of a composite INSTANCE swaps the materials of the ModelReferences directly inside the composite it places (not deeper), so it changes one placement when set on that instance or on an alias of it (set_parameters / create_entities alias). A ModelReference's own 'material' parameter instead replaces a one-submesh model's material within its slot. Both reach the game at save_level build=true; get_entity_resources with 'path' shows what a placement really draws.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Words the set's name must contain."),
                    McpSchema.Boolean("users", "List the entities using each set (default true)."),
                    McpSchema.Limit(50, "sets"),
                    McpSchema.Offset("sets")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListMappings,
            };

            JObject pair = new JObject()
            {
                ["type"] = "object",
                ["properties"] = new JObject()
                {
                    ["from"] = new JObject() { ["type"] = "string", ["description"] = "The material that is swapped out." },
                    ["to"] = new JObject() { ["type"] = "string", ["description"] = "The material drawn instead." },
                },
                ["required"] = new JArray("from", "to"),
            };
            yield return new McpTool()
            {
                Name = "edit_material_mapping",
                Title = "Edit material mapping",
                Description = "Create a material mapping set, or add, change and remove its from -> to pairs, as the Material Mapping Editor does. Materials are named as list_materials names them (name#index when repeated) and checked against the level; a set maps each 'from' once. " +
                    "To swap a material on ONE placement of a prop: make a set mapping its material to the new one, then set 'mapping' to the set's name on the instance that directly places the prop's ModelReferences (or an alias of that instance, for one placement deeper down). An open Material Mapping Editor is closed first. One undo step (the previous pairs are returned too); reaches the game at save_level build=true.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("mapping", "The set's name (list_material_mappings).", required: true),
                    McpSchema.Boolean("create", "Make a new set with this name (default false)."),
                    McpSchema.Array("add", "Pairs to add.", pair),
                    McpSchema.Array("set", "Pairs whose 'from' is already in the set: change what it maps to.", pair),
                    McpSchema.Strings("remove", "'from' materials whose pairs to remove.")),
                Destructive = true,
                Run = EditMapping,
            };
            #endregion
        }

        #region Describing
        private static object ListMaterials(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: false).Level;
                McpAssets.MaterialNames names = McpAssets.Names(level);
                string[] words = (call.Str("filter") ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                SHADER_LIST? family = call.Has("family") ? ParseFamily(call.Str("family")) : (SHADER_LIST?)null;
                List<string> required = call.StrList("require_features").Select(o => o.Trim()).Where(o => o.Length != 0).ToList();
                if (family != null)
                    foreach (string feature in required)
                        if (!FeatureBits(family.Value).Any(o => string.Equals(o.name, feature, StringComparison.OrdinalIgnoreCase)))
                            throw McpError.NotFound("feature of " + family, feature, FeatureBits(family.Value).Select(o => o.name), "It has: " + string.Join(", ", FeatureBits(family.Value).Select(o => o.name)) + ".");
                Textures.TEX4 texture = call.Has("texture") ? McpAssets.FindTexture(level, call.Str("texture"), true, out bool _) : null;
                int? priority = call.Has("priority") ? call.Int("priority") : (int?)null;
                Dictionary<SHADER_LIST, List<(string name, int bit)>> bits = new Dictionary<SHADER_LIST, List<(string, int)>>();
                bool HasFeatures(Materials.Material material)
                {
                    if (required.Count == 0) return true;
                    if (material.Shader == null) return false;
                    if (!bits.TryGetValue(material.Shader.Ubershader, out List<(string name, int bit)> features))
                        bits[material.Shader.Ubershader] = features = FeatureBits(material.Shader.Ubershader);
                    long mask = material.Shader.UbershaderFeatureFlags;
                    return required.All(r => features.Any(f => string.Equals(f.name, r, StringComparison.OrdinalIgnoreCase) && (mask & (1L << f.bit)) != 0));
                }
                List<(int index, Materials.Material material, string name)> materials = level.Materials.Entries
                    .Select((o, i) => (i, o, o == null ? null : names.Display(o)))
                    .Where(o => o.Item2 != null && (family == null || o.Item2.Shader?.Ubershader == family) && (priority == null || o.Item2.Priority == priority)
                        && words.All(w => (o.Item3 ?? "").IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0 || (o.Item2.Name ?? "").IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)
                        && (texture == null || (o.Item2.TextureReferences ?? new List<TexturePtr>()).Any(t => t != null && ReferenceEquals(t.Texture, texture)))
                        && HasFeatures(o.Item2))
                    .OrderBy(o => o.Item3, StringComparer.OrdinalIgnoreCase).ThenBy(o => o.Item1).ToList();
                JObject result = new JObject();
                McpPaging.Page(call, materials, result, "materials", o =>
                {
                    JObject item = new JObject() { ["name"] = names.Ref(o.material), ["shader"] = o.material.Shader?.Ubershader.ToString(), ["priority"] = o.material.Priority };
                    if (!string.Equals(o.name, o.material.Name, StringComparison.Ordinal)) item["stored_name"] = o.material.Name;
                    return item;
                }, 200);
                if (materials.Count == 0 && required.Count != 0 && family == null)
                    call.Note("No material has all of " + string.Join(", ", required) + " on. Feature names belong to a family: list_material_permutations family:X lists a family's.");
                return result;
            });
        }

        internal static JObject DescribeMaterial(Level level, Materials.Material material, bool full)
        {
            McpAssets.MaterialNames names = McpAssets.Names(level);
            JObject result = new JObject()
            {
                ["name"] = names.Ref(material),
                //By reference: IndexOf compares materials by value, and finds the first of identical duplicates
                ["index"] = level.Materials.Entries.FindIndex(o => ReferenceEquals(o, material)),
            };
            if (!string.Equals(material.Name, names.Display(material), StringComparison.Ordinal)) result["stored_name"] = material.Name;
            Shaders.Shader shader = material.Shader;
            if (shader == null)
            {
                result["shader"] = null;
                return result;
            }
            SHADER_LIST family = shader.Ubershader;
            long mask = shader.UbershaderFeatureFlags;
            bool canCompile = ShaderPermutationService.CanBuildArbitraryPermutations(family);
            result["shader"] = family.ToString();
            result["mask"] = Hex(mask);
            result["priority"] = material.Priority;
            if (material.EnvironmentMapIndex != 255) result["environment_map_index"] = material.EnvironmentMapIndex;

            //Features: on or off, and what toggling one would need (as the editor's checkboxes offer them)
            List<(string name, int bit)> features = FeatureBits(family);
            Dictionary<int, PermutationSource> availability = null;
            if (canCompile && full)
            {
                try { availability = ShaderPermutationService.AvailabilityForToggles(level.Shaders, family, mask, Singleton.PathToAI, features.Select(o => o.bit)); }
                catch { availability = null; }
            }
            JArray featureList = new JArray();
            foreach ((string name, int bit) in features)
            {
                JObject item = new JObject() { ["name"] = name, ["on"] = (mask & (1L << bit)) != 0 };
                if (availability != null && availability.TryGetValue(bit, out PermutationSource source))
                    item["toggle"] = source == PermutationSource.None || source == PermutationSource.Unimplemented ? "unavailable" : Source(source);
                featureList.Add(item);
            }
            result["features"] = featureList;
            result["features_toggle"] = canCompile
                ? "free: enable/disable any feature marked with a toggle source"
                : EditMaterialReason(family) + " only shipped combinations can be used: pick one with list_material_permutations and edit_material(mask)";

            JArray samplers = new JArray();
            foreach (string sampler in ShaderUtility.GetSamplers(family) ?? new List<string>())
            {
                int? index = ShaderUtility.GetShaderFunctionalityIndex(family, ShaderIndexType.SAMPLERS, sampler);
                if (index == null) continue;
                TexturePtr reference = TextureAt(material, index.Value);
                JObject item = new JObject() { ["name"] = sampler, ["texture"] = reference?.Texture?.Name };
                if (reference?.Texture != null && reference.Location == TexturePtr.Source.GLOBAL) item["global"] = true;
                string gate = MaterialGenerator.FeatureForSampler(family, sampler);
                if (gate != null) item["feature"] = gate;
                samplers.Add(item);
            }
            result["samplers"] = samplers;

            JArray parameters = new JArray();
            foreach (string parameter in ShaderUtility.GetParameters(family) ?? new List<string>())
            {
                JToken value = ReadParameter(material, family, parameter, out string stage);
                if (value == null) continue;
                parameters.Add(new JObject() { ["name"] = parameter, ["type"] = ShaderUtility.GetParameterType(family, parameter)?.ToString(), ["value"] = value, ["stage"] = stage });
            }
            result["parameters"] = parameters;

            List<MaterialConsistency.Problem> problems = MaterialConsistency.Check(material);
            if (problems.Count != 0) result["problems"] = new JArray(problems.Select(o => o.Text));
            int sharing = level.Materials.Entries.Count(o => o != null && !ReferenceEquals(o, material) && ReferenceEquals(o.Shader, shader));
            if (sharing != 0) result["shares_shader_entry_with"] = sharing;

            if (full)
            {
                List<string> models = level.Models.Entries.Where(o => o != null && o.Components.Any(c => c.LODs.Any(l => l.Submeshes.Any(s => ReferenceEquals(s.Material, material))))).Select(o => o.Name).ToList();
                result["default_of_models_count"] = models.Count;
                result["default_of_models"] = new JArray(models.Take(20));
                result["users"] = "find_asset_users {material} lists what really draws it: placed entities (with placement counts and positions), mapping sets and overrides.";
                if (!ShaderDatabase.IsBuilt(Singleton.PathToAI)) result["shader_database"] = HarvestState();
            }
            return result;
        }

        private static string EditMaterialReason(SHADER_LIST family)
        {
            try { return OpenCAGE.EditMaterial.FixedFeatureReason(family); }
            catch { return "This family cannot be recompiled,"; }
        }

        private static JObject HarvestState()
        {
            JObject state = new JObject() { ["state"] = ShaderDatabase.AutoBuild.ToString(), ["built"] = ShaderDatabase.IsBuilt(Singleton.PathToAI) };
            if (!string.IsNullOrEmpty(ShaderDatabase.AutoBuildProgress)) state["progress"] = ShaderDatabase.AutoBuildProgress;
            if (!string.IsNullOrEmpty(ShaderDatabase.AutoBuildError)) state["error"] = ShaderDatabase.AutoBuildError;
            return state;
        }

        private static string Source(PermutationSource source)
        {
            switch (source)
            {
                case PermutationSource.LevelPool: return "level";
                case PermutationSource.Database: return "database";
                case PermutationSource.Recompiled: return "compiled";
                case PermutationSource.Relabelled: return "relabelled";
                default: return source.ToString().ToLowerInvariant();
            }
        }

        private static string Hex(long mask) => "0x" + mask.ToString("X");

        private static List<(string name, int bit)> FeatureBits(SHADER_LIST family)
        {
            List<(string, int)> bits = new List<(string, int)>();
            foreach (string feature in ShaderUtility.GetFeatures(family) ?? new List<string>())
            {
                int? bit = ShaderUtility.GetShaderFunctionalityIndex(family, ShaderIndexType.FEATURES, feature);
                if (bit != null) bits.Add((feature, bit.Value));
            }
            return bits;
        }

        private static List<string> FeaturesOf(SHADER_LIST family, long mask) =>
            FeatureBits(family).Where(o => (mask & (1L << o.bit)) != 0).Select(o => o.name).ToList();

        private static TexturePtr TextureAt(Materials.Material material, int samplerIndex)
        {
            if (samplerIndex >= material.Shader.SamplerRemaps.Count) return null;
            int reference = material.Shader.SamplerRemaps[samplerIndex];
            if (reference == 255 || reference < 0 || reference >= material.TextureReferences.Count) return null;
            return material.TextureReferences[reference];
        }

        private static int Width(UberShaderParameterType type)
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

        /// <summary>A parameter's value from whichever stage carries it (pixel first, as the editor shows them); null if this permutation has none.</summary>
        private static JToken ReadParameter(Materials.Material material, SHADER_LIST family, string parameter, out string stage)
        {
            stage = null;
            int? id = ShaderUtility.GetShaderFunctionalityIndex(family, ShaderIndexType.PARAMETERS, parameter);
            UberShaderParameterType? type = ShaderUtility.GetParameterType(family, parameter);
            if (id == null || type == null) return null;
            int width = Width(type.Value);
            List<float> values = null;
            int slot = Slot(material.Shader.PixelShaderParameterRemaps, id.Value);
            if (slot >= 0 && slot < material.PixelShaderConstants.Count) { values = material.PixelShaderConstants; stage = "pixel"; }
            else
            {
                slot = Slot(material.Shader.VertexShaderParameterRemaps, id.Value);
                if (slot >= 0 && slot < material.VertexShaderConstants.Count) { values = material.VertexShaderConstants; stage = "vertex"; }
            }
            if (values == null) return null;
            bool integer = type.Value == UberShaderParameterType.Int;
            JToken Component(int k) => slot + k < values.Count ? (integer ? (JToken)(int)values[slot + k] : Math.Round((double)values[slot + k], 6)) : JValue.CreateNull();
            if (width == 1) return Component(0);
            JArray array = new JArray();
            for (int k = 0; k < width; k++) array.Add(Component(k));
            return array;
        }

        private static int Slot(List<int> remaps, int id)
        {
            if (remaps == null || id < 0 || id >= remaps.Count) return -1;
            return remaps[id] == 255 ? -1 : remaps[id];
        }
        #endregion

        #region Creating
        private static SHADER_LIST ParseFamily(string text)
        {
            string name = (text ?? "").Trim().ToUpperInvariant();
            if (!name.StartsWith("CA_")) name = "CA_" + name;
            if (!Enum.TryParse(name, out SHADER_LIST family) || !Enum.IsDefined(typeof(SHADER_LIST), family))
                throw McpError.NotFound("shader family", text, Enum.GetNames(typeof(SHADER_LIST)), "Families are e.g. CA_ENVIRONMENT, CA_CHARACTER; list_material_permutations with no arguments lists them.");
            return family;
        }

        private static long ParseMask(string text)
        {
            string value = (text ?? "").Trim();
            long mask;
            bool ok = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? long.TryParse(value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out mask)
                : long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out mask);
            if (!ok || mask < 0)
                throw new McpError("'mask' is a permutation mask such as '0x1A0' (list_material_permutations shows them).");
            return mask;
        }

        private static object CreateMaterial(McpCall call)
        {
            using (McpEditorTools.Heartbeat(call, "Creating material"))
            {
                return McpEditor.UI(() =>
                {
                    Level level = McpEditor.RequireLevel().Level;
                    McpEditor.RequireUndoIdle();
                    List<Shaders.Shader> poolBefore = new List<Shaders.Shader>(level.Shaders.Entries);
                    int materialCount = level.Materials.Entries.Count;
                    Materials.Material material;
                    if (call.Has("copy_from"))
                    {
                        if (call.Has("family") || call.Has("mask"))
                            throw new McpError("A copy keeps its source's shader: leave out family and mask (change them afterwards with edit_material).");
                        Materials.Material source = McpAssets.FindMaterial(level, call.Str("copy_from"), "copy_from");
                        string name = NewMaterialName(level, call.Str("name") ?? level.Materials.GetMaterialName(source) + " Clone");
                        //The editor's Duplicate shares the shader entry; the copy gets its own here, since sampler remaps live on it
                        material = source.Copy();
                        material.Name = name;
                        if (source.Shader != null) material.Shader = PrivateShader(level, source.Shader);
                        level.Materials.Entries.Add(material);
                    }
                    else
                    {
                        string name = NewMaterialName(level, call.Str("name", required: true));
                        SHADER_LIST family = call.Has("family") ? ParseFamily(call.Str("family")) : SHADER_LIST.CA_ENVIRONMENT;
                        List<ShaderPermutationService.Creatable> creatable = ShaderPermutationService.CreatableFamilies(level.Shaders, Singleton.PathToAI);
                        if (!creatable.Any(o => o.Family == family))
                            throw new McpError(family + " has no shader this level or the shader database can supply. Families that can be used: " + string.Join(", ", creatable.Select(o => o.Family)) + ".");
                        long? mask = call.Has("mask") ? ParseMask(call.Str("mask")) : (long?)null;
                        material = ShaderPermutationService.CreateMaterial(level.Materials, level.Shaders, family, name, Singleton.PathToAI, out string error, mask);
                        if (material == null)
                            throw new McpError(error ?? "The material could not be created.");
                        //A material built from zeroes renders as a flat dark smear: start it where the material generator starts its own
                        if (call.Bool("seed_parameters", true))
                            MaterialGenerator.SeedConstantsFromDonor(material, level.Materials, family);
                    }
                    Singleton.OnResourceModified?.Invoke();

                    //Undo takes the material (and the shader entries made for it) back out
                    HashSet<Shaders.Shader> known = new HashSet<Shaders.Shader>(poolBefore, McpAssets.ByReference<Shaders.Shader>.Instance);
                    List<Shaders.Shader> added = level.Shaders.Entries.Where(o => o != null && !known.Contains(o)).ToList();
                    Materials.Material made = material;
                    (Action apply, Action revert) entry = McpAssetEdit.ListChange(level.Materials.Entries, made, materialCount, true);
                    McpAssetEdit.Record("Create material " + made.Name, () =>
                    {
                        foreach (Shaders.Shader shader in added)
                            if (!level.Shaders.Entries.Any(o => ReferenceEquals(o, shader))) level.Shaders.Entries.Add(shader);
                        entry.apply();
                    }, () =>
                    {
                        //Something pointed at it since, by a change that kept no step, would be left pointing at nothing
                        McpAssets.RefuseRemovalWhileUsed("Material " + made.Name, McpAssets.UsersOf(level, made));
                        entry.revert();
                        foreach (Shaders.Shader shader in added)
                            if (!level.Materials.Entries.Any(o => o != null && ReferenceEquals(o.Shader, shader)))
                                level.Shaders.Entries.RemoveAll(o => ReferenceEquals(o, shader));
                    });

                    JObject result = DescribeMaterial(level, material, false);
                    call.Note("Bind textures and set features/parameters with edit_material; draw a model with it via edit_model(action: set_material).");
                    if (System.Windows.Forms.Application.OpenForms.OfType<OpenCAGE.EditMaterial>().Any(o => !o.IsDisposed))
                        call.Note("The open Material Editor lists the materials as they were when it opened: reopen it to see this one.");
                    return result;
                });
            }
        }

        private static string NewMaterialName(Level level, string requested)
        {
            string name = AssetName.Normalise((requested ?? "").Trim());
            string problem = AssetName.Problem(name);
            if (problem != null)
                throw new McpError("'" + requested + "' cannot be a material name: " + problem);
            if (level.Materials.Entries.Any(o => o != null && (string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(level.Materials.GetMaterialName(o), name, StringComparison.OrdinalIgnoreCase))))
                throw new McpError("The level already has a material called '" + name + "'. Pick another name.");
            return name;
        }

        /// <summary>A copy of a shader entry for one material alone, sharing the bytecode, added to the level's pool.</summary>
        private static Shaders.Shader PrivateShader(Level level, Shaders.Shader original)
        {
            Shaders.Shader copy = original.Copy();
            copy.VertexShader = original.VertexShader;
            copy.PixelShader = original.PixelShader;
            copy.HullShader = original.HullShader;
            copy.DomainShader = original.DomainShader;
            copy.GeometryShader = original.GeometryShader;
            copy.ComputeShader = original.ComputeShader;
            level.Shaders.Entries.Add(copy);
            return copy;
        }
        #endregion

        #region Editing
        private static object EditMaterialTool(McpCall call)
        {
            JObject textures = call.Object("textures");
            JObject parameters = call.Object("parameters");
            bool anything = (textures != null && textures.Count != 0) || (parameters != null && parameters.Count != 0) || call.Has("enable") || call.Has("disable") || call.Has("mask") || call.Has("priority");
            if (!anything)
                throw new McpError("Give textures, enable/disable, mask, parameters and/or priority (describe_material shows what the material has).");
            int? priority = call.Has("priority") ? call.Int("priority") : (int?)null;
            if (priority != null && (priority < 0 || priority > 255))
                throw new McpError("'priority' is 0-255 (70 is the world pass).");

            using (McpEditorTools.Heartbeat(call, "Editing material"))
            {
                return McpEditor.UI(() =>
                {
                    Level level = McpEditor.RequireLevel().Level;
                    McpEditor.RequireUndoIdle();
                    Materials.Material material = McpAssets.FindMaterial(level, call.Str("material", required: true));
                    if (material.Shader == null)
                        throw new McpError(level.Materials.GetMaterialName(material) + " has no shader to edit.");
                    McpMaterialState snapshot = McpMaterialState.Of(material);
                    List<Shaders.Shader> poolBefore = new List<Shaders.Shader>(level.Shaders.Entries);
                    SHADER_LIST family = material.Shader.Ubershader;
                    Shaders.Shader oldShader = material.Shader;
                    long oldMask = oldShader.UbershaderFeatureFlags;
                    List<(string name, int bit)> features = FeatureBits(family);
                    List<string> samplerNames = ShaderUtility.GetSamplers(family) ?? new List<string>();
                    List<string> parameterNames = ShaderUtility.GetParameters(family) ?? new List<string>();

                    //Everything is checked before anything changes
                    int FeatureBit(string raw, string argument)
                    {
                        //Not upper-cased: a few feature names are mixed case (CA_FILTERS' BLOOM_DOWNSAMPLE_2x2)
                        string name = (raw ?? "").Trim();
                        (string name, int bit) match = features.FirstOrDefault(o => string.Equals(o.name, name, StringComparison.OrdinalIgnoreCase));
                        if (match.name == null)
                            throw new McpError("'" + raw + "' in " + argument + " is not a feature of " + family + ". It has: " + string.Join(", ", features.Select(o => o.name)) + ".");
                        return match.bit;
                    }
                    long mask = call.Has("mask") ? ParseMask(call.Str("mask")) : oldMask;
                    HashSet<int> explicitBits = new HashSet<int>();
                    foreach (string feature in call.StrList("enable")) { int bit = FeatureBit(feature, "enable"); mask |= 1L << bit; explicitBits.Add(bit); }
                    foreach (string feature in call.StrList("disable"))
                    {
                        int bit = FeatureBit(feature, "disable");
                        if (explicitBits.Contains(bit)) throw new McpError("'" + feature + "' is in both enable and disable.");
                        mask &= ~(1L << bit);
                        explicitBits.Add(bit);
                    }

                    List<(string sampler, int index, Textures.TEX4 texture, bool global)> binds = new List<(string, int, Textures.TEX4, bool)>();
                    if (textures != null)
                    {
                        foreach (JProperty property in textures.Properties())
                        {
                            string sampler = property.Name.Trim().ToUpperInvariant();
                            if (!samplerNames.Contains(sampler))
                                throw McpError.NotFound("sampler of " + family, property.Name, samplerNames, "It has: " + string.Join(", ", samplerNames) + ".");
                            int? index = ShaderUtility.GetShaderFunctionalityIndex(family, ShaderIndexType.SAMPLERS, sampler);
                            if (index == null) throw new McpError("'" + sampler + "' has no sampler slot.");
                            bool global = false;
                            Textures.TEX4 texture = property.Value.Type == JTokenType.Null || (property.Value.Type == JTokenType.String && ((string)property.Value).Trim().Length == 0)
                                ? null : McpAssets.FindTexture(level, McpValues.ReadString(property.Value), true, out global);
                            binds.Add((sampler, index.Value, texture, global));
                            //Every shipped material keeps a sampler and its gating feature in step; do the same unless told otherwise
                            string gate = MaterialGenerator.FeatureForSampler(family, sampler);
                            if (gate != null && call.Bool("sync_features", true))
                            {
                                int bit = FeatureBit(gate, "textures");
                                if (!explicitBits.Contains(bit))
                                    mask = texture != null ? mask | (1L << bit) : mask & ~(1L << bit);
                            }
                        }
                    }

                    List<(string name, int id, int width, float[] values)> writes = new List<(string, int, int, float[])>();
                    if (parameters != null)
                    {
                        foreach (JProperty property in parameters.Properties())
                        {
                            string name = property.Name.Trim().ToUpperInvariant();
                            if (!parameterNames.Contains(name))
                                throw new McpError("'" + property.Name + "' is not a parameter of " + family + "." + Suggest(parameterNames, name));
                            UberShaderParameterType type = ShaderUtility.GetParameterType(family, name) ?? UberShaderParameterType.Float;
                            int id = ShaderUtility.GetShaderFunctionalityIndex(family, ShaderIndexType.PARAMETERS, name) ?? -1;
                            int width = Width(type);
                            List<double> numbers = new List<double>();
                            if (property.Value is JArray array) numbers.AddRange(array.Select(o => McpValues.ReadDouble(o, name)));
                            else numbers.Add(McpValues.ReadDouble(property.Value, name));
                            if (numbers.Count != width)
                                throw new McpError(name + " is a " + type + ": give " + (width == 1 ? "one number" : width + " numbers [" + string.Join(", ", "xyzw".Take(width)) + "]") + ".");
                            if (numbers.Any(o => double.IsNaN(o) || double.IsInfinity(o)))
                                throw new McpError(name + " needs finite numbers.");
                            if (type == UberShaderParameterType.Int && numbers.Any(o => Math.Abs(o - Math.Round(o)) > 1e-9))
                                throw new McpError(name + " is an Int: give whole numbers.");
                            writes.Add((name, id, width, numbers.Select(o => (float)o).ToArray()));
                        }
                    }

                    JObject previous = new JObject();
                    JObject result = new JObject() { ["material"] = level.Materials.GetMaterialName(material) };

                    //1. The permutation: rebinding to another entry of the pool, the database's, or a compiled one
                    Shaders.Shader newShader = oldShader;
                    int poolCount = level.Shaders.Entries.Count;
                    try
                    {
                        if (mask != oldMask)
                        {
                            newShader = ShaderPermutationService.Resolve(level.Shaders, family, mask, oldShader, Singleton.PathToAI, out PermutationSource source, out string error);
                            if (newShader == null)
                                throw new McpError((error ?? "That feature combination is not available.") + (ShaderPermutationService.CanBuildArbitraryPermutations(family) ? "" : " list_material_permutations shows the ones that are."));
                            result["shader_source"] = Source(source);
                        }
                        foreach ((string name, int id, int width, float[] values) in writes)
                            if (Slot(newShader.PixelShaderParameterRemaps, id) < 0 && Slot(newShader.VertexShaderParameterRemaps, id) < 0)
                                throw new McpError(name + " is not used by " + (mask != oldMask ? "the new" : "this material's") + " permutation (" + Hex(mask) + "), so it has no value to set. Turn on the feature that uses it.");

                        //A material holds 12 textures: bind on copies of its slots and the new entry's remaps first, so running out refuses the call here
                        if (binds.Count != 0)
                        {
                            List<int> remaps = new List<int>(newShader.SamplerRemaps);
                            List<TexturePtr> slots = material.TextureReferences.Select(o => o == null ? null : new TexturePtr() { Texture = o.Texture, Location = o.Location }).ToList();
                            foreach ((string sampler, int index, Textures.TEX4 texture, bool global) in binds)
                                Bind(remaps, slots, material.EnvironmentMapIndex, index, texture, global);
                        }
                    }
                    catch
                    {
                        //Resolve adds the entry it found or built to the level's pool: a refused call leaves none behind
                        level.Shaders.Entries.RemoveRange(poolCount, level.Shaders.Entries.Count - poolCount);
                        throw;
                    }

                    //The Material Editor keeps each box's constant slot and each sampler's texture slot, and writes a box back
                    //into its slot when it loses focus: close it before anything changes (it applies its edits as they are made)
                    CloseEditors<OpenCAGE.EditMaterial>(call, "Material Editor", "it would have shown, and written back, this material's values from before the change");

                    if (mask != oldMask)
                    {
                        previous["mask"] = Hex(oldMask);
                        if (!ReferenceEquals(newShader, oldShader))
                        {
                            ShaderPermutationService.MigrateConstants(material, oldShader, newShader);
                            material.Shader = newShader;
                        }
                        result["mask"] = Hex(mask);
                        result["features_on"] = new JArray(FeaturesOf(family, mask).Except(FeaturesOf(family, oldMask)));
                        result["features_off"] = new JArray(FeaturesOf(family, oldMask).Except(FeaturesOf(family, mask)));
                    }

                    //2. Textures: sampler remaps live on the shader entry, so it has to be this material's alone first
                    if (binds.Count != 0)
                    {
                        if (level.Materials.Entries.Any(o => o != null && !ReferenceEquals(o, material) && ReferenceEquals(o.Shader, material.Shader)))
                            material.Shader = PrivateShader(level, material.Shader);
                        JObject before = new JObject();
                        foreach ((string sampler, int index, Textures.TEX4 texture, bool global) in binds)
                        {
                            before[sampler] = TextureAt(material, index)?.Texture?.Name;
                            Bind(material.Shader.SamplerRemaps, material.TextureReferences, material.EnvironmentMapIndex, index, texture, global);
                        }
                        previous["textures"] = before;
                        result["textures"] = new JObject(binds.Select(o => new JProperty(o.sampler, o.texture == null ? null : o.global ? "GLOBAL: " + o.texture.Name : o.texture.Name)));
                    }

                    //3. Parameters, into whichever stage carries each one (as generated materials are written)
                    if (writes.Count != 0)
                    {
                        JObject before = new JObject();
                        foreach ((string name, int id, int width, float[] values) in writes)
                        {
                            before[name] = ReadParameter(material, family, name, out string _);
                            Write(material.Shader.PixelShaderParameterRemaps, material.PixelShaderConstants, id, values, width);
                            Write(material.Shader.VertexShaderParameterRemaps, material.VertexShaderConstants, id, values, width);
                        }
                        previous["parameters"] = before;
                        result["parameters"] = new JObject(writes.Select(o => new JProperty(o.name, ReadParameter(material, family, o.name, out string _))));
                    }

                    //4. Render priority, which decides the pass it draws in
                    if (priority != null && priority != material.Priority)
                    {
                        previous["priority"] = material.Priority;
                        material.Priority = priority.Value;
                        result["priority"] = material.Priority;
                    }

                    Singleton.OnResourceModified?.Invoke();
                    McpMaterialState.Record(level, "Edit material " + level.Materials.GetMaterialName(material), new List<McpMaterialState>() { snapshot }, poolBefore);
                    List<MaterialConsistency.Problem> problems = MaterialConsistency.Check(material);
                    if (problems.Count != 0)
                    {
                        result["problems"] = new JArray(problems.Select(o => o.Text));
                        call.Note("The features and textures disagree, which no shipped material does and the engine will not draw correctly: set the missing textures, or change the features.");
                    }
                    result["previous"] = previous;
                    result["undo"] = "One undo step: 'AI: Edit material " + level.Materials.GetMaterialName(material) + "'.";
                    return result;
                });
            }
        }

        private static string Suggest(List<string> names, string wanted)
        {
            List<string> near = names.Where(o => o.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0 || wanted.IndexOf(o, StringComparison.OrdinalIgnoreCase) >= 0).Take(8).ToList();
            if (near.Count != 0) return " Did you mean: " + string.Join(", ", near) + "?";
            return " It has: " + string.Join(", ", names.Take(60)) + (names.Count > 60 ? ", ..." : "") + ".";
        }

        /// <summary>
        /// Bind (or clear, with null) a sampler's texture, as the Material Editor's Pick/Clear Texture do: the sampler's own
        /// texture slot is reused when nothing else reads it, otherwise a free slot, otherwise a new one (a material holds 12).
        /// Works on the lists it is given (the shader entry's sampler remaps and the material's texture slots), so
        /// it can be tried on copies first.
        /// </summary>
        private static void Bind(List<int> remaps, List<TexturePtr> references, int environmentMapIndex, int samplerIndex, Textures.TEX4 texture, bool global = false)
        {
            TexturePtr.Source location = global ? TexturePtr.Source.GLOBAL : TexturePtr.Source.LEVEL;
            while (remaps.Count <= samplerIndex) remaps.Add(255);
            int current = remaps[samplerIndex];
            bool SharedSlot(int slot) => slot == environmentMapIndex || remaps.Where((r, i) => i != samplerIndex && r == slot).Any();

            if (texture == null)
            {
                if (current != 255 && current < references.Count && references[current] != null && !SharedSlot(current))
                    references[current].Texture = null;
                remaps[samplerIndex] = 255;
                return;
            }

            if (current != 255 && current < references.Count && references[current] != null && !SharedSlot(current))
            {
                references[current].Texture = texture;
                references[current].Location = location;
                return;
            }

            int free = -1;
            for (int slot = 0; slot < references.Count; slot++)
            {
                if (slot == environmentMapIndex || remaps.Contains(slot)) continue;
                if (references[slot] == null || references[slot].Texture == null) { free = slot; break; }
            }
            if (free < 0)
            {
                if (references.Count >= 12)
                    throw new McpError("The material already holds 12 textures, the most a material can; clear a sampler first. Nothing was changed.");
                free = references.Count;
                references.Add(null);
            }
            references[free] = new TexturePtr() { Texture = texture, Location = location };
            remaps[samplerIndex] = free;
        }

        /// <summary>
        /// Close OpenCAGE's open editors of this kind before a tool changes what they show: they keep slots and objects
        /// from when they were filled and write through them later. They apply their own edits as they are made, so
        /// closing one loses nothing of the user's.
        /// </summary>
        private static void CloseEditors<T>(McpCall call, string what, string why) where T : System.Windows.Forms.Form
        {
            List<T> open = System.Windows.Forms.Application.OpenForms.OfType<T>().Where(o => !o.IsDisposed).ToList();
            if (open.Count == 0) return;
            foreach (T form in open) form.Close();
            call.Note("Closed OpenCAGE's " + what + (open.Count > 1 ? " windows" : "") + ": " + why + ". Reopen it to see the change.");
        }

        private static void Write(List<int> remaps, List<float> constants, int id, float[] values, int width)
        {
            int slot = Slot(remaps, id);
            if (slot < 0) return;
            for (int k = 0; k < width && k < values.Length; k++)
            {
                if (slot + k >= constants.Count) break;
                constants[slot + k] = values[k];
            }
        }
        #endregion

        #region Permutations
        private static object ListPermutations(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: false).Level;
                int limit = Math.Max(1, call.Int("limit", 40));
                Materials.Material material = call.Has("material") ? McpAssets.FindMaterial(level, call.Str("material")) : null;
                if (material != null && material.Shader == null)
                    throw new McpError(level.Materials.GetMaterialName(material) + " has no shader.");
                SHADER_LIST? family = material != null ? material.Shader.Ubershader : call.Has("family") ? ParseFamily(call.Str("family")) : (SHADER_LIST?)null;
                if (material != null && call.Has("family") && ParseFamily(call.Str("family")) != family)
                    throw new McpError(level.Materials.GetMaterialName(material) + " is a " + family + "; leave out 'family'.");

                if (family == null)
                {
                    List<ShaderPermutationService.Creatable> creatable = ShaderPermutationService.CreatableFamilies(level.Shaders, Singleton.PathToAI);
                    return new JObject()
                    {
                        ["families"] = new JArray(creatable.Select(o => new JObject()
                        {
                            ["family"] = o.Family.ToString(),
                            ["permutations"] = o.Permutations,
                            ["in_level"] = o.InLevel,
                            ["any_combination"] = ShaderPermutationService.CanBuildArbitraryPermutations(o.Family),
                        })),
                        ["shader_database"] = HarvestState(),
                    };
                }

                List<(string name, int bit)> features = FeatureBits(family.Value);
                long Bits(IEnumerable<string> names, string argument)
                {
                    long bits = 0;
                    foreach (string raw in names)
                    {
                        string name = (raw ?? "").Trim();
                        (string name, int bit) match = features.FirstOrDefault(o => string.Equals(o.name, name, StringComparison.OrdinalIgnoreCase));
                        if (match.name == null)
                            throw new McpError("'" + raw + "' in " + argument + " is not a feature of " + family + ". It has: " + string.Join(", ", features.Select(o => o.name)) + ".");
                        bits |= 1L << match.bit;
                    }
                    return bits;
                }
                long require = Bits(call.StrList("require"), "require");
                long exclude = Bits(call.StrList("exclude"), "exclude");

                List<ShaderPermutationService.Permutation> permutations = ShaderPermutationService.AvailablePermutations(level.Materials, level.Shaders, family.Value, Singleton.PathToAI)
                    .Where(o => (o.Mask & require) == require && (o.Mask & exclude) == 0).ToList();
                JObject result = new JObject()
                {
                    ["family"] = family.ToString(),
                    ["any_combination"] = ShaderPermutationService.CanBuildArbitraryPermutations(family.Value),
                    ["count"] = permutations.Count,
                    ["permutations"] = new JArray(permutations.Take(limit).Select(o =>
                    {
                        JObject item = new JObject()
                        {
                            ["mask"] = Hex(o.Mask),
                            ["source"] = Source(o.Source),
                            ["materials_using"] = o.MaterialUses,
                            ["features"] = new JArray(FeaturesOf(family.Value, o.Mask)),
                        };
                        if (material != null)
                        {
                            if (o.Mask == material.Shader.UbershaderFeatureFlags) item["current"] = true;
                            item["textures_needed"] = MaterialConsistency.TexturesNeededFor(material, o.Mask);
                        }
                        return item;
                    })),
                };
                if (material != null) result["current_mask"] = Hex(material.Shader.UbershaderFeatureFlags);
                if (!ShaderDatabase.IsBuilt(Singleton.PathToAI)) result["shader_database"] = HarvestState();
                return result;
            });
        }
        #endregion

        #region Material mappings
        private static object ListMappings(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: false).Level;
                McpAssets.MaterialNames names = McpAssets.Names(level);
                string[] words = (call.Str("filter") ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int offset = McpPaging.Offset(call), limit = McpPaging.Limit(call, 50);
                bool users = call.Bool("users", true);
                List<MaterialMappings.MaterialMapping> sets = (level.MaterialMappings?.Entries ?? new List<MaterialMappings.MaterialMapping>())
                    .Where(o => o != null && words.All(w => (o.Name ?? "").IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)).OrderBy(o => o.Name).ToList();

                Dictionary<ShortGuid, List<JObject>> usedBy = new Dictionary<ShortGuid, List<JObject>>();
                if (users)
                {
                    HashSet<ShortGuid> wanted = new HashSet<ShortGuid>(sets.Skip(offset).Take(limit).Select(o => o.ID));
                    foreach (Composite composite in level.Commands.Entries)
                    {
                        if (composite == null) continue;
                        foreach (Entity entity in composite.GetEntities())
                        {
                            if (!(entity.GetParameter(ShortGuids.mapping)?.content is cResource resource) || !wanted.Contains(resource.shortGUID)) continue;
                            if (!usedBy.TryGetValue(resource.shortGUID, out List<JObject> list)) usedBy[resource.shortGUID] = list = new List<JObject>();
                            list.Add(new JObject() { ["composite"] = composite.name, ["id"] = McpScript.Id(entity.shortGUID), ["name"] = McpScript.EntityName(level.Commands, composite, entity) });
                        }
                    }
                }

                JObject result = new JObject();
                McpPaging.Page(call, sets, result, "mappings", o =>
                {
                    JObject item = new JObject()
                    {
                        ["name"] = o.Name,
                        ["id"] = McpScript.Id(o.ID),
                        ["pairs"] = new JArray(o.Mappings.Take(100).Select(p => Pair(names, p.from, p.to))),
                    };
                    if (o.Mappings.Count > 100) item["pair_count"] = o.Mappings.Count;
                    if (users)
                    {
                        List<JObject> list = usedBy.TryGetValue(o.ID, out List<JObject> found) ? found : new List<JObject>();
                        item["used_by_count"] = list.Count;
                        if (list.Count != 0) item["used_by"] = new JArray(list.Take(10));
                    }
                    return item;
                }, 50);
                return result;
            });
        }

        /// <summary>A pair as the set stores it (by stored name), with the names material tools take where those differ.</summary>
        private static JObject Pair(McpAssets.MaterialNames names, string from, string to) => new JObject() { ["from"] = names.StoredValue(from), ["to"] = names.StoredValue(to) };

        private static object EditMapping(McpCall call)
        {
            string setName = call.Str("mapping", required: true).Trim();
            bool create = call.Bool("create");
            JArray add = call.Array("add");
            JArray set = call.Array("set");
            List<string> remove = call.StrList("remove");
            if (!create && add.Count == 0 && set.Count == 0 && remove.Count == 0)
                throw new McpError("Give add, set and/or remove (or create: true to make a new set).");

            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                if (level.MaterialMappings == null)
                    throw new McpError("This level has no material mapping table.");
                List<MaterialMappings.MaterialMapping> entries = level.MaterialMappings.Entries;
                MaterialMappings.MaterialMapping mapping = entries.FirstOrDefault(o => o != null && string.Equals(o.Name, setName, StringComparison.OrdinalIgnoreCase));
                if (create && mapping != null)
                    throw new McpError("A material mapping set called '" + mapping.Name + "' already exists.");
                if (!create && mapping == null)
                    throw McpError.NotFound("material mapping set", setName, entries.Where(o => o != null).Select(o => o.Name), "list_material_mappings lists them; pass create: true to make a new one.");

                //A pair names materials the way the editor's pickers store them: by the material's own name
                string MaterialName(JToken token, string where)
                {
                    string text = token == null || token.Type == JTokenType.Null ? "" : McpValues.ReadString(token).Trim();
                    if (text.Length == 0) throw new McpError(where + " is empty.");
                    return McpAssets.FindMaterial(level, text, where).Name;
                }
                List<(string from, string to)> ReadPairs(JArray pairs, string argument)
                {
                    List<(string, string)> read = new List<(string, string)>();
                    for (int i = 0; i < pairs.Count; i++)
                    {
                        if (!(pairs[i] is JObject item)) throw new McpError(argument + "[" + i + "] has to be {\"from\": ..., \"to\": ...}.");
                        read.Add((MaterialName(item["from"], argument + "[" + i + "].from"), MaterialName(item["to"], argument + "[" + i + "].to")));
                    }
                    return read;
                }
                List<(string from, string to)> adds = ReadPairs(add, "add");
                List<(string from, string to)> sets = ReadPairs(set, "set");

                JArray before = mapping == null ? null : new JArray(mapping.Mappings.Select(p => new JObject() { ["from"] = p.from, ["to"] = p.to }));
                List<MaterialMappings.MaterialMapping.Mapping> pairsNow = mapping == null ? new List<MaterialMappings.MaterialMapping.Mapping>() : mapping.Mappings;
                //A 'from' to remove as the set stores it: its exact text, or the material a name picks out (as add and set read them)
                string Stored(string text)
                {
                    text = (text ?? "").Trim();
                    MaterialMappings.MaterialMapping.Mapping exact = pairsNow.FirstOrDefault(p => string.Equals(p.from, text, StringComparison.OrdinalIgnoreCase));
                    if (exact != null) return exact.from;
                    string named = null;
                    try { named = McpAssets.FindMaterial(level, text, "remove").Name; } catch (McpError) { }
                    if (named != null && pairsNow.Any(p => string.Equals(p.from, named, StringComparison.OrdinalIgnoreCase)))
                        return named;
                    throw new McpError("The set has no pair from '" + text + "'. It maps: " + string.Join(", ", pairsNow.Take(30).Select(p => p.from)) + ".");
                }
                remove = remove.Select(Stored).ToList();
                //A set maps each material once, so a call names each 'from' once too
                HashSet<string> changing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach ((string from, string to) in sets)
                {
                    if (!pairsNow.Any(p => string.Equals(p.from, from, StringComparison.OrdinalIgnoreCase)))
                        throw new McpError("The set has no pair from '" + from + "' to change; use 'add' for a new one.");
                    if (remove.Any(r => string.Equals(r.Trim(), from, StringComparison.OrdinalIgnoreCase)))
                        throw new McpError("'" + from + "' is in both set and remove: give it in one of them.");
                    if (!changing.Add(from))
                        throw new McpError("'" + from + "' is in set more than once: a set maps each material to one other.");
                }
                HashSet<string> adding = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach ((string from, string to) in adds)
                {
                    if (pairsNow.Any(p => string.Equals(p.from, from, StringComparison.OrdinalIgnoreCase)) && !remove.Any(r => string.Equals(r.Trim(), from, StringComparison.OrdinalIgnoreCase)))
                        throw new McpError("The set already maps '" + from + "'; use 'set' to change what it maps to.");
                    if (!adding.Add(from))
                        throw new McpError("'" + from + "' is in add more than once: a set maps each material to one other.");
                }

                //The Material Mapping Editor keeps the pair objects it listed and edits through them: close it before anything changes
                CloseEditors<OpenCAGE.EditMaterialMapping>(call, "Material Mapping Editor", "it would have kept listing, and editing, the pairs from before the change");

                bool created = mapping == null;
                List<(string from, string to)> pairsBefore = created ? null : mapping.Mappings.Select(p => (p.from, p.to)).ToList();
                if (mapping == null)
                {
                    mapping = new MaterialMappings.MaterialMapping()
                    {
                        Name = setName,
                        Mappings = new List<MaterialMappings.MaterialMapping.Mapping>(),
                        ID = MaterialMappings.GenerateMappingID(setName, true),
                    };
                    entries.Add(mapping);
                }
                mapping.Mappings.RemoveAll(p => remove.Any(r => string.Equals(r.Trim(), p.from, StringComparison.OrdinalIgnoreCase)));
                foreach ((string from, string to) in sets)
                    foreach (MaterialMappings.MaterialMapping.Mapping pair in mapping.Mappings.Where(p => string.Equals(p.from, from, StringComparison.OrdinalIgnoreCase)))
                        pair.to = to;
                foreach ((string from, string to) in adds)
                    mapping.Mappings.Add(new MaterialMappings.MaterialMapping.Mapping() { from = from, to = to });

                //What the Material Mapping Editor does after each change: mark the level changed and tell the viewer
                Singleton.OnResourceModified?.Invoke();
                Send.NotifyMaterialMappingModified(mapping);

                //One undo step: the pairs put back (new pair objects, as nothing else holds them), or the new set taken out
                MaterialMappings.MaterialMapping edited = mapping;
                List<(string from, string to)> pairsAfter = edited.Mappings.Select(p => (p.from, p.to)).ToList();
                void Pairs(List<(string from, string to)> pairs)
                {
                    edited.Mappings.Clear();
                    edited.Mappings.AddRange(pairs.Select(p => new MaterialMappings.MaterialMapping.Mapping() { from = p.from, to = p.to }));
                    Send.NotifyMaterialMappingModified(edited);
                }
                int at = entries.FindIndex(o => ReferenceEquals(o, edited));
                (Action apply, Action revert) listed = McpAssetEdit.ListChange(entries, edited, at, true);
                string label = (created ? "Create material mapping " : "Edit material mapping ") + edited.Name;
                if (created) McpAssetEdit.Record(label, () => { listed.apply(); Pairs(pairsAfter); }, () => listed.revert());
                else McpAssetEdit.Record(label, () => Pairs(pairsAfter), () => Pairs(pairsBefore));

                McpAssets.MaterialNames names = McpAssets.Names(level);
                JObject result = new JObject()
                {
                    ["mapping"] = mapping.Name,
                    ["id"] = McpScript.Id(mapping.ID),
                    ["pairs"] = new JArray(mapping.Mappings.Select(p => Pair(names, p.from, p.to))),
                };
                if (before == null) result["created"] = true;
                else result["previous_pairs"] = before;
                result["undo"] = "One undo step: 'AI: " + label + "'.";
                result["needs_build"] = true;
                int usedBy = level.Commands.Entries.Where(o => o != null).Sum(c => c.GetEntities().Count(e => e.GetParameter(ShortGuids.mapping)?.content is cResource r && r.shortGUID == mapping.ID));
                if (usedBy == 0)
                    call.Note("Nothing uses this set yet: set 'mapping' to '" + mapping.Name + "' on the composite instance that directly places the ModelReferences to swap (set_parameters, or an alias of that instance for one placement deeper down).");
                else
                    call.Note(usedBy + " entities use this set; it reaches the game at save_level build=true.");
                return result;
            });
        }
        #endregion
    }
}
