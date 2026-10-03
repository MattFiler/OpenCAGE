using AlienPAK;
using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.TextureTools;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OpenCAGE.MCP
{
    /// <summary>The open level's models, textures and sky: looking at them, placing a model, and changing them as the Model, Texture and Galaxy editors do.</summary>
    internal static class McpAssetTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            #region Models
            yield return new McpTool()
            {
                Name = "list_models",
                Title = "List models",
                Description = "The models (CS2 meshes) the open level holds, by name, with their parts and materials. describe_model shows one in full; place_model puts one in a composite. To use a model from another level, port a composite that places it (find one with search_level).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Words the model name must contain."),
                    McpSchema.Integer("limit", "At most this many (default 100).")),
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() =>
                {
                    Level level = McpEditor.RequireLevel(forEditing: false).Level;
                    string[] words = (call.Str("filter") ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    int limit = Math.Max(1, call.Int("limit", 100));
                    List<Models.CS2> models = level.Models.Entries.Where(o => o != null && words.All(w => (o.Name ?? "").IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)).OrderBy(o => o.Name).ToList();
                    return new JObject()
                    {
                        ["count"] = models.Count,
                        ["models"] = new JArray(models.Take(limit).Select(o => new JObject()
                        {
                            ["name"] = o.Name,
                            ["parts"] = o.Components.Count,
                            ["submeshes"] = o.Components.Sum(c => c.LODs.FirstOrDefault()?.Submeshes.Count ?? 0),
                            ["lods"] = o.Components.Count == 0 ? 0 : o.Components.Max(c => c.LODs.Count),
                            ["materials"] = new JArray(o.Components.SelectMany(c => c.LODs.Take(1)).SelectMany(l => l.Submeshes).Where(s => s.Material != null).Select(s => level.Materials.GetMaterialName(s.Material)).Distinct().Take(12)),
                        })),
                    };
                }),
            };

            yield return new McpTool()
            {
                Name = "describe_model",
                Title = "Describe model",
                Description = "One model in full: components > LODs > submeshes with vertex and triangle counts, vertex attributes, material, render flags and bounds; whether every level must carry it; and which entities place it. The component/lod/submesh indexes are what edit_model takes.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("model", "The model's name (from list_models).", required: true),
                    McpSchema.Boolean("lower_lods", "Include the LODs after the first (default true)."),
                    McpSchema.Boolean("placements", "List the entities that place it (default true; slower on big levels)."),
                    McpSchema.Integer("limit", "At most this many submeshes listed (default 200).")),
                ReadOnly = true,
                Idempotent = true,
                Run = DescribeModel,
            };

            yield return new McpTool()
            {
                Name = "export_model",
                Title = "Export model",
                Description = "Write a model to a file as the Model Editor's Export does: FBX, GLB/glTF, DAE or OBJ by the path's extension, plus a .cs2meta.json sidecar (so it re-imports exactly) and a '<name> Textures' folder. A skinned model is bound to a skeleton. Written straight away; nothing in the level changes.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("model", "The model's name.", required: true),
                    McpSchema.String("path", "Full path of the file to write; the extension picks the format.", required: true),
                    McpSchema.String("skeleton", "'auto' (default: the best-fitting game skeleton for a skinned model, none for a static one), 'none', or a skeleton name."),
                    McpSchema.Boolean("overwrite", "Replace a file already at the path (default false).")),
                Idempotent = true,
                Run = ExportModel,
            };

            yield return new McpTool()
            {
                Name = "edit_model",
                Title = "Edit model",
                Description = "Change a model as the Model Editor does: its submeshes' default material, render flags, size, geometry (from a model file), add a LOD/component/submesh from a file, remove a part, or delete the model. Changes the level's model data: not undoable (except update_placed's step), written by save_level. A required model (describe_model's required_model) can only have its material and render flags changed: it cannot be resized, have its geometry or parts changed, or be deleted.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("model", "The model's name.", required: true),
                    McpSchema.String("action", "What to do.", required: true, options: new[] { "set_material", "set_render_flags", "rescale", "replace_geometry", "add_lod", "add_component", "add_submesh", "remove_part", "delete" }),
                    McpSchema.Integer("component", "Component index (describe_model). Leave out to mean every component where that makes sense."),
                    McpSchema.Integer("lod", "LOD index within the component. Leave out for every LOD."),
                    McpSchema.Integer("submesh", "Submesh index within the LOD. Leave out for every submesh."),
                    McpSchema.String("material", "set_material: the level material to use. add_*: material for the new submeshes (default the component's own)."),
                    McpSchema.Boolean("update_placed", "set_material: also re-material entities already placing these submeshes (one undo step). Default false."),
                    McpSchema.Strings("flags_on", "set_render_flags: flags to turn on, e.g. IS_SHADOW_CASTING, DYNAMIC_GEOM."),
                    McpSchema.Strings("flags_off", "set_render_flags: flags to turn off."),
                    McpSchema.Number("factor", "rescale: size multiplier (e.g. 0.5 halves it)."),
                    McpSchema.String("path", "replace_geometry / add_*: full path of the model file to read."),
                    McpSchema.String("mesh", "replace_geometry / add_*: which mesh of the file (name or index). Default: the first for replace/add_submesh, every mesh for add_lod/add_component."),
                    McpSchema.String("name", "add_lod / add_component: the new LOD's name (default the file's name)."),
                    McpSchema.Number("scale", "replace_geometry / add_*: resize the file's geometry on the way in (default 1)."),
                    McpSchema.Boolean("force", "remove_part / delete: go ahead even though entities place it (they will draw nothing).")),
                Destructive = true,
                Run = EditModelTool,
            };

            yield return new McpTool()
            {
                Name = "place_model",
                Title = "Place model",
                Description = "Put a model the open level holds into a composite, as ModelReference entities (one per part of the model) at a position. One undo step. It renders, but has no collision: for walkable geometry, port and place a composite that already places the model with its collision.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "Where to put it (path or id; 'root' for the level's root).", required: true),
                    McpSchema.String("model", "The model's name, from list_models.", required: true),
                    McpSchema.Vector("position", "[x, y, z] metres."),
                    McpSchema.Vector("rotation", "[x, y, z] degrees."),
                    McpSchema.String("name", "A name for the entity (numbered per part).")),
                Run = PlaceModel,
            };
            #endregion

            #region Textures
            yield return new McpTool()
            {
                Name = "list_textures",
                Title = "List textures",
                Description = "The textures the open level holds, with format and size. environment_maps_only lists the cubemaps an EnvironmentMap entity's Texture parameter can use, with the path to set it to.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Words the texture name must contain."),
                    McpSchema.Boolean("environment_maps_only", "Only cubemaps (environment maps), each with its parameter_path."),
                    McpSchema.Integer("limit", "At most this many (default 200).")),
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() =>
                {
                    Level level = McpEditor.RequireLevel(forEditing: false).Level;
                    string[] words = (call.Str("filter") ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    int limit = Math.Max(1, call.Int("limit", 200));
                    bool cubes = call.Bool("environment_maps_only");
                    IEnumerable<Textures.TEX4> source = cubes ? level.Textures.GetEnvironmentMaps() : level.Textures.Entries;
                    List<Textures.TEX4> textures = source.Where(o => o != null && words.All(w => (o.Name ?? "").IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)).OrderBy(o => o.Name).ToList();
                    return new JObject()
                    {
                        ["count"] = textures.Count,
                        ["textures"] = new JArray(textures.Take(limit).Select(o =>
                        {
                            Textures.TEX4.Texture part = McpAssets.HasContent(o.TextureStreamed) ? o.TextureStreamed : o.TexturePersistent;
                            JObject item = new JObject() { ["name"] = o.Name, ["format"] = o.Format.ToString() };
                            if (McpAssets.HasContent(part)) item["size"] = part.Width + "x" + part.Height;
                            if (o.StateFlags.HasFlag(Textures.TextureStateFlag.CUBE))
                            {
                                item["cubemap"] = true;
                                item["parameter_path"] = McpAssets.EnvironmentMapPath(o);
                            }
                            return item;
                        })),
                    };
                }),
            };

            yield return new McpTool()
            {
                Name = "describe_texture",
                Title = "Describe texture",
                Description = "One texture: format, state and usage flags, the streamed and persistent copies (size, mips, bytes) and the materials using it. preview=true returns the image itself (PNG, scaled down), one mip of either copy; a cubemap shows its six faces in a strip.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("texture", "The texture's name (from list_textures).", required: true),
                    McpSchema.Boolean("preview", "Return the image (default false)."),
                    McpSchema.String("part", "Which copy to preview (default: streamed if it has one).", options: new[] { "streamed", "persistent" }),
                    McpSchema.Integer("mip", "Which mip level to preview (default 0, the largest)."),
                    McpSchema.Integer("max_size", "Longest edge of the preview in pixels (default 512, at most 2048).")),
                ReadOnly = true,
                Idempotent = true,
                Run = DescribeTexture,
            };

            yield return new McpTool()
            {
                Name = "import_texture",
                Title = "Import texture",
                Description = "Import an image (DDS, PNG, JPG, TGA, BMP, TIFF, HDR) into the open level as a new texture, or into an existing one with 'replace' (keeping its usage flags and streamed/persistent split), as the Texture Editor does. Converted with texconv. Not undoable; written by save_level.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("path", "The image file's full path.", required: true),
                    McpSchema.String("name", "The new texture's name (backslash folders allowed); default from the file name."),
                    McpSchema.String("replace", "Put the image into this existing texture instead of making a new one."),
                    McpSchema.String("format", "BC7 (default for a new one; a replaced one keeps its own), DXN (normal maps), DXT1, DXT5, BC6H, A8R8G8B8, ..."),
                    McpSchema.Integer("mips", "Mip levels to build: 0 = full chain (default), 1 = no mips."),
                    McpSchema.Integer("persistent_drop", "Levels down for the smaller always-resident copy (default 1; a replaced one keeps its own); 0 = streamed copy only."),
                    McpSchema.Boolean("persistent_only", "Keep only the resident copy (how volume textures are stored)."),
                    McpSchema.Boolean("srgb", "Set (true) or clear (false) the ALLOW_SRGB flag: colour maps are sRGB, normal maps are not. Default: as converted.")),
                Run = ImportTexture,
            };

            yield return new McpTool()
            {
                Name = "export_textures",
                Title = "Export textures",
                Description = "Write textures of the open level to disk as DDS, PNG or JPG: one texture to a file ('texture' + 'path', format from the extension), or every texture matching 'filter' into a folder keeping their folder names ('filter' + 'folder' + 'format'). Written straight away.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("texture", "One texture to export (with 'path')."),
                    McpSchema.String("path", "Full path of the file to write (.dds, .png or .jpg)."),
                    McpSchema.String("filter", "Export every texture whose name contains this ('*' for all), into 'folder'."),
                    McpSchema.String("folder", "Full path of the folder to export into."),
                    McpSchema.String("format", "Batch format (default dds).", options: new[] { "dds", "png", "jpg" }),
                    McpSchema.Boolean("overwrite", "Replace files already there (default false: they are skipped).")),
                Idempotent = true,
                Run = ExportTextures,
            };

            yield return new McpTool()
            {
                Name = "edit_texture",
                Title = "Edit texture",
                Description = "Change a texture's state flags (ALLOW_SRGB, CUBE, VOLUME, ...) or usage flags (POST_PROCESSING, LENS_FLARE, GALAXY, ...), or delete it, as the Texture Editor does. Delete refuses while materials use it unless force (their samplers are then emptied and unbound, as the Material Editor's Clear Texture does). Not undoable; written by save_level.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("texture", "The texture's name.", required: true),
                    McpSchema.String("action", "What to do.", required: true, options: new[] { "set_flags", "delete" }),
                    McpSchema.Strings("state_on", "State flags to turn on."),
                    McpSchema.Strings("state_off", "State flags to turn off."),
                    McpSchema.Strings("usage_on", "Usage flags to turn on."),
                    McpSchema.Strings("usage_off", "Usage flags to turn off."),
                    McpSchema.Boolean("force", "delete: delete even though materials use it.")),
                Destructive = true,
                Run = EditTextureTool,
            };
            #endregion

            #region Galaxy
            yield return new McpTool()
            {
                Name = "get_galaxy",
                Title = "Get galaxy",
                Description = "The open level's galaxy (the star field drawn as its sky): its name, star count, and the star templates stars are drawn from (frequency, size range in radians, intensity range, colour).",
                InputSchema = McpSchema.Object(),
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() => DescribeGalaxy(RequireGalaxy(McpEditor.RequireLevel(forEditing: false).Level))),
            };

            JObject starTemplate = new JObject()
            {
                ["type"] = "object",
                ["properties"] = new JObject()
                {
                    ["frequency"] = new JObject() { ["type"] = "number", ["description"] = "How often, relative to the others (0-10000)." },
                    ["min_size"] = new JObject() { ["type"] = "number", ["description"] = "Radians, 0-10." },
                    ["max_size"] = new JObject() { ["type"] = "number", ["description"] = "Radians, 0-10." },
                    ["min_intensity"] = new JObject() { ["type"] = "number", ["description"] = "0-1." },
                    ["max_intensity"] = new JObject() { ["type"] = "number", ["description"] = "0-1." },
                    ["colour"] = new JObject() { ["type"] = "array", ["items"] = new JObject() { ["type"] = "number" }, ["description"] = "[r, g, b], each 0-1." },
                },
            };
            yield return new McpTool()
            {
                Name = "set_galaxy",
                Title = "Set galaxy",
                Description = "Change the open level's galaxy (name, star count, star templates) and regenerate its stars, as the Galaxy Editor's Generate does; the viewport's sky updates. Not undoable (the previous definition is returned); written by save_level.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("name", "The galaxy's name."),
                    McpSchema.Integer("star_count", "How many stars (0-16384)."),
                    McpSchema.Array("entries", "Replace every star template with these (leave out to keep them).", starTemplate)),
                Run = SetGalaxy,
            };
            #endregion
        }

        #region Models
        private static object DescribeModel(McpCall call)
        {
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: false).Level;
                Models.CS2 model = McpAssets.FindModel(level, call.Str("model", required: true));
                bool lowerLods = call.Bool("lower_lods", true);
                int limit = Math.Max(1, call.Int("limit", 200));
                int listed = 0, total = 0;

                JArray components = new JArray();
                for (int c = 0; c < model.Components.Count; c++)
                {
                    Models.CS2.Component component = model.Components[c];
                    JArray lods = new JArray();
                    for (int l = 0; l < component.LODs.Count; l++)
                    {
                        Models.CS2.Component.LOD lod = component.LODs[l];
                        total += lod.Submeshes.Count;
                        if (l != 0 && !lowerLods) continue;
                        JArray submeshes = new JArray();
                        for (int s = 0; s < lod.Submeshes.Count; s++)
                        {
                            if (listed >= limit) break;
                            listed++;
                            submeshes.Add(DescribeSubmesh(level, lod.Submeshes[s], s));
                        }
                        lods.Add(new JObject() { ["index"] = l, ["name"] = lod.Name ?? "", ["submesh_count"] = lod.Submeshes.Count, ["submeshes"] = submeshes });
                    }
                    components.Add(new JObject() { ["index"] = c, ["lod_count"] = component.LODs.Count, ["lods"] = lods });
                }

                JObject result = new JObject()
                {
                    ["model"] = model.Name,
                    ["required_model"] = RequiredModels.IsRequiredEntry(level.Models, model),
                    ["components"] = components,
                    ["submesh_count"] = total,
                };
                int bones = Skeleton.RequiredBoneCount(model);
                if (bones != 0) result["skinned_bones"] = bones;
                if (listed < total && (lowerLods || listed >= limit)) result["submeshes_listed"] = listed;

                if (call.Bool("placements", true))
                {
                    List<McpAssets.Placement> placed = McpAssets.PlacedBy(level, McpAssets.SubmeshesOf(model));
                    result["placed_by_count"] = placed.Count;
                    result["placed_by"] = new JArray(placed.Take(25).Select(o => McpAssets.Describe(level, o)));
                }
                return result;
            });
        }

        private static JObject DescribeSubmesh(Level level, Models.CS2.Component.LOD.Submesh submesh, int index)
        {
            JObject item = new JObject()
            {
                ["index"] = index,
                ["vertices"] = submesh.VertexCount,
                ["triangles"] = submesh.IndexCount / 3,
                ["material"] = submesh.Material == null ? null : level.Materials.GetMaterialName(submesh.Material),
                ["render_flags"] = new JArray(McpAssets.FlagNames(submesh.RenderFlags)),
                ["attributes"] = new JArray(VertexUsages(submesh.VertexFormatFull)),
                ["bounds"] = new JObject() { ["min"] = McpValues.Vector(submesh.MinBounds), ["max"] = McpValues.Vector(submesh.MaxBounds) },
            };
            if (submesh.VertexScale != 1) item["vertex_scale"] = submesh.VertexScale;
            if (submesh.Bones.Count != 0) item["bones"] = submesh.Bones.Count;
            if (submesh.CollisionProxyIndex >= 0) item["collision_proxy"] = submesh.CollisionProxyIndex;
            return item;
        }

        /// <summary>What a submesh's vertices carry, as the submesh info window lists it (every stream but the index one).</summary>
        private static IEnumerable<string> VertexUsages(Models.VertexFormat format)
        {
            if (ReferenceEquals(format, null) || format.Attributes == null || format.Attributes.Count == 0)
                return Enumerable.Empty<string>();
            HashSet<string> usages = new HashSet<string>();
            for (int stream = 0; stream < format.Attributes.Count - 1; stream++)
            {
                if (format.Attributes[stream] == null) continue;
                foreach (Models.VertexFormat.Attribute attribute in format.Attributes[stream])
                    if (!ReferenceEquals(attribute, null) && attribute.Type != Models.VertexFormat.Type.Unused)
                        usages.Add(attribute.Usage.ToString() + (attribute.Index != 0 ? attribute.Index.ToString() : ""));
            }
            return usages.OrderBy(o => o);
        }

        private static object ExportModel(McpCall call)
        {
            string path = McpAssets.AbsolutePath(call.Str("path", required: true), "path");
            string extension = (Path.GetExtension(path) ?? "").ToLowerInvariant();
            if (!OpenCAGE.ModelExport.ModelExporter.Formats.Any(o => o.Extension == extension))
                throw new McpError("'" + extension + "' is not a format OpenCAGE writes. Use one of: " + string.Join(", ", OpenCAGE.ModelExport.ModelExporter.Formats.Select(o => o.Extension)) + ".");
            if (File.Exists(path) && !call.Bool("overwrite"))
                throw new McpError("There is already a file at " + path + ". Pass overwrite: true to replace it, or pick another path.");
            string skeletonArg = (call.Str("skeleton") ?? "auto").Trim();

            using (McpEditorTools.Heartbeat(call, "Exporting model"))
            {
                return McpEditor.UI(() =>
                {
                    Level level = McpEditor.RequireLevel(forEditing: false).Level;
                    Models.CS2 model = McpAssets.FindModel(level, call.Str("model", required: true));
                    int required = Skeleton.RequiredBoneCount(model);
                    Skeleton skeleton = null;
                    string fit = null;

                    if (string.Equals(skeletonArg, "none", StringComparison.OrdinalIgnoreCase))
                    {
                        if (required != 0) call.Note(model.Name + " is skinned (" + required + " bones) but was written without a skeleton, so it comes out unskinned.");
                    }
                    else if (string.Equals(skeletonArg, "auto", StringComparison.OrdinalIgnoreCase))
                    {
                        if (required != 0)
                        {
                            skeleton = BestSkeleton(model, required, out fit);
                            if (skeleton == null)
                                throw new McpError(model.Name + " is skinned (" + required + " bones) and no game skeleton could be scored against it" + (Singleton.Global?.Skeletons == null ? " (the animation data is not loaded)" : "") + ". Name one with 'skeleton', or pass 'none'.");
                        }
                    }
                    else
                    {
                        skeleton = Singleton.Global?.GetSkeleton(skeletonArg);
                        if (skeleton == null)
                            throw new McpError("There is no skeleton '" + skeletonArg + "'" + (Singleton.Global?.Skeletons == null ? " (the animation data is not loaded)." : ".") + " Use 'auto' to pick the best fit.");
                        if (skeleton.Bones.Count < required)
                            call.Note(skeleton.Name + " has " + skeleton.Bones.Count + " bones but " + model.Name + " is skinned to " + required + "; some of the mesh has no bone to follow.");
                    }

                    OpenCAGE.ModelExport.ModelExporter.Format format = OpenCAGE.ModelExport.ModelExporter.For(path);
                    if (required != 0 && skeleton != null && !format.Skinning)
                        call.Note(format.Description + " cannot carry skinning: the mesh is written without it. Use .fbx or .glb to keep it.");

                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    model.ExportMesh(path, skeleton);

                    JArray written = new JArray(path);
                    string sidecar = ModelIO.GetSidecarPath(path);
                    if (File.Exists(sidecar)) written.Add(sidecar);
                    string textures = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + " Textures");
                    JObject result = new JObject() { ["model"] = model.Name, ["format"] = format.Description, ["written"] = written };
                    if (Directory.Exists(textures)) result["textures_folder"] = textures;
                    if (skeleton != null) result["skeleton"] = skeleton.Name;
                    if (fit != null) result["skeleton_fit"] = fit;
                    call.Note("Keep the sidecar beside the file (and the object names intact): import_model then brings it back exactly.");
                    return result;
                });
            }
        }

        /// <summary>The game skeleton a skinned model fits best, ranked as the Export window's skeleton picker ranks them.</summary>
        private static Skeleton BestSkeleton(Models.CS2 model, int required, out string fit)
        {
            fit = null;
            SkeletonDB db = Singleton.Global?.Skeletons;
            if (db == null) return null;
            List<Skeleton> skeletons = new List<Skeleton>();
            foreach (SkeletonDB.SkeletonEntry entry in db.Skeletons)
            {
                Skeleton skeleton = Singleton.Global.GetSkeleton(entry);
                if (skeleton == null || skeleton.Bones.Count < required) continue;
                skeletons.Add(skeleton);
            }

            Skeleton best = null;
            float bestFit = float.MaxValue;
            List<float> scores = ModelIO.ScoreFits(model, skeletons);
            for (int i = 0; i < skeletons.Count; i++)
            {
                if (scores[i] < 0 || scores[i] >= bestFit) continue;
                bestFit = scores[i];
                best = skeletons[i];
            }
            if (best != null) fit = bestFit.ToString("0.000") + " m average bone-to-weights distance";
            return best;
        }

        private static object EditModelTool(McpCall call)
        {
            string action = call.Str("action", required: true).Trim().ToLowerInvariant();
            //Model files are read here, off the UI thread: assimp can take a while on a big one
            Assimp.Scene scene = null;
            ModelIO.ImportPlan plan = null;
            string sourcePath = null;
            if (action == "replace_geometry" || action == "add_lod" || action == "add_component" || action == "add_submesh")
            {
                sourcePath = McpAssets.AbsolutePath(call.Str("path", required: true), "path");
                if (!File.Exists(sourcePath))
                    throw new McpError("There is no file at " + sourcePath + ".");
                try
                {
                    using (McpEditorTools.Heartbeat(call, "Reading " + Path.GetFileName(sourcePath)))
                    using (Assimp.AssimpContext importer = new Assimp.AssimpContext())
                        scene = importer.ImportFile(sourcePath, ModelIO.ImportPostProcessSteps);
                }
                catch (Exception e)
                {
                    throw new McpError(Path.GetFileName(sourcePath) + " could not be read: " + e.Message);
                }
                if (scene == null || scene.MeshCount == 0)
                    throw new McpError("No meshes were found in " + Path.GetFileName(sourcePath) + " (they need to be under the scene root).");
                plan = ModelIO.CreateImportPlan(scene, ModelIO.TryLoadSidecar(sourcePath), Path.GetFileNameWithoutExtension(sourcePath));
                if (!plan.HasMetadata)
                    plan.UnitScale = ModelIO.FormatUnitScale(sourcePath);
            }

            switch (action)
            {
                case "set_material": return SetModelMaterial(call);
                case "set_render_flags":
                case "rescale":
                case "remove_part":
                case "delete":
                    return McpEditor.UI(() => EditModelInPlace(call, action));
                case "replace_geometry":
                case "add_lod":
                case "add_component":
                case "add_submesh":
                    return McpEditor.UI(() => EditModelGeometry(call, action, scene, plan, sourcePath));
            }
            throw new McpError("Unknown action '" + action + "'.");
        }

        /// <summary>The submeshes an edit_model call is aimed at: component/lod/submesh narrow it, each left out meaning all.</summary>
        private static List<(int c, int l, int s, Models.CS2.Component.LOD.Submesh submesh)> Scope(McpCall call, Models.CS2 model)
        {
            int? component = call.Has("component") ? call.Int("component") : (int?)null;
            int? lod = call.Has("lod") ? call.Int("lod") : (int?)null;
            int? submesh = call.Has("submesh") ? call.Int("submesh") : (int?)null;
            if (component != null && (component < 0 || component >= model.Components.Count))
                throw new McpError(model.Name + " has components 0-" + (model.Components.Count - 1) + " (describe_model shows them).");
            if ((lod != null || submesh != null) && component == null)
                throw new McpError("Give 'component' too when naming a lod or submesh.");
            if (submesh != null && lod == null)
                throw new McpError("Give 'lod' too when naming a submesh.");

            List<(int, int, int, Models.CS2.Component.LOD.Submesh)> found = new List<(int, int, int, Models.CS2.Component.LOD.Submesh)>();
            for (int c = 0; c < model.Components.Count; c++)
            {
                if (component != null && c != component) continue;
                Models.CS2.Component comp = model.Components[c];
                if (lod != null && (lod < 0 || lod >= comp.LODs.Count))
                    throw new McpError("Component " + c + " of " + model.Name + " has LODs 0-" + (comp.LODs.Count - 1) + ".");
                for (int l = 0; l < comp.LODs.Count; l++)
                {
                    if (lod != null && l != lod) continue;
                    Models.CS2.Component.LOD level = comp.LODs[l];
                    if (submesh != null && (submesh < 0 || submesh >= level.Submeshes.Count))
                        throw new McpError("LOD " + l + " of component " + c + " has submeshes 0-" + (level.Submeshes.Count - 1) + ".");
                    for (int s = 0; s < level.Submeshes.Count; s++)
                        if (submesh == null || s == submesh)
                            found.Add((c, l, s, level.Submeshes[s]));
                }
            }
            if (found.Count == 0)
                throw new McpError("That selects no submeshes of " + model.Name + ".");
            return found;
        }

        private static string Address(int c, int l, int s) => "C" + c + "/L" + l + "/S" + s;

        private static object SetModelMaterial(McpCall call)
        {
            bool updatePlaced = call.Bool("update_placed");
            //Everything decided first, on the UI thread; the placed entities then change as one undo step
            List<(Composite composite, FunctionEntity entity, cResource after)> rewrites = new List<(Composite, FunctionEntity, cResource)>();
            JObject result = McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                Models.CS2 model = McpAssets.FindModel(level, call.Str("model", required: true));
                Materials.Material material = McpAssets.FindMaterial(level, call.Str("material", required: true));
                if (updatePlaced) McpEditor.RequireUndoIdle();
                var scope = Scope(call, model);

                Dictionary<Models.CS2.Component.LOD.Submesh, Materials.Material> previous = new Dictionary<Models.CS2.Component.LOD.Submesh, Materials.Material>(McpAssets.ByReference<Models.CS2.Component.LOD.Submesh>.Instance);
                JArray changed = new JArray();
                foreach (var target in scope)
                {
                    if (ReferenceEquals(target.submesh.Material, material)) continue;
                    previous[target.submesh] = target.submesh.Material;
                    changed.Add(new JObject() { ["submesh"] = Address(target.c, target.l, target.s), ["was"] = target.submesh.Material == null ? null : level.Materials.GetMaterialName(target.submesh.Material) });
                    target.submesh.Material = material;
                }
                if (previous.Count != 0)
                    Singleton.OnResourceModified?.Invoke();

                JObject summary = new JObject() { ["model"] = model.Name, ["material"] = level.Materials.GetMaterialName(material), ["changed"] = changed };
                List<McpAssets.Placement> placed = McpAssets.PlacedBy(level, new HashSet<Models.CS2.Component.LOD.Submesh>(scope.Select(o => o.submesh), McpAssets.ByReference<Models.CS2.Component.LOD.Submesh>.Instance));
                if (!updatePlaced)
                {
                    if (placed.Count != 0)
                        call.Note(placed.Count + " entities already place these submeshes and keep drawing with their own materials; pass update_placed: true to change them too.");
                    return summary;
                }

                //Each placed run is rebuilt with the new material wherever it drew the submesh with its old default (per-instance overrides are left alone)
                int skippedOnEntity = 0;
                foreach (McpAssets.Placement placement in placed)
                {
                    if (!placement.InParameter) { skippedOnEntity++; continue; }
                    cResource before = placement.Entity.GetParameter(ShortGuids.resource)?.content as cResource;
                    if (before == null) continue;
                    bool any = false;
                    cResource after = new cResource(before.shortGUID) { value = new List<ResourceReference>() };
                    foreach (ResourceReference reference in before.value)
                    {
                        if (reference == null || reference.resource_type != ResourceType.RENDERABLE_INSTANCE || reference.RenderableInstance == null)
                        {
                            after.value.Add(reference);
                            continue;
                        }
                        ResourceReference copy = (ResourceReference)reference.Clone();
                        copy.RenderableInstance = reference.RenderableInstance.Select(o => Rematerial(o, previous, material, ref any)).ToList();
                        after.value.Add(copy);
                    }
                    if (!any) continue;
                    foreach (ResourceReference reference in after.value)
                        if (reference?.resource_type == ResourceType.RENDERABLE_INSTANCE && reference.RenderableInstance != null)
                            reference.RenderableInstance = level.RenderableElements.EnsureRegistered(reference.RenderableInstance);
                    rewrites.Add((placement.Composite, placement.Entity, after));
                }
                if (skippedOnEntity != 0)
                    call.Note(skippedOnEntity + " placements hold their renderables on the entity itself rather than its 'resource' parameter and were not changed.");
                return summary;
            });

            if (rewrites.Count != 0)
            {
                string label = "AI: Re-material " + McpAssets.Leaf(call.Str("model"));
                //The editor's own record of a parameter value change, grouped: the whole re-material is one undo step
                JArray updated = McpEditor.UI(() =>
                {
                    Commands commands = McpEditor.RequireCommands();
                    McpEditor.RequireUndoIdle();
                    JArray entities = new JArray();
                    using (UndoStack.Current.BeginGroup(label))
                    {
                        foreach ((Composite composite, FunctionEntity entity, cResource after) in rewrites)
                        {
                            Parameter parameter = entity.GetParameter(ShortGuids.resource);
                            if (parameter == null) continue;
                            bool modified = ParameterModificationTracker.IsParameterModified(composite.shortGUID, entity.shortGUID, ShortGuids.resource);
                            UndoStack.Current.Apply(new ParameterValueEdit(composite, entity, ShortGuids.resource, ParameterValues.Clone(parameter.content), after, modified, modified, label));
                            if (entities.Count < 20)
                                entities.Add(new JObject() { ["composite"] = composite.name, ["id"] = McpScript.Id(entity.shortGUID), ["name"] = McpScript.EntityName(commands, composite, entity) });
                        }
                    }
                    return entities;
                });
                result["placed_updated"] = rewrites.Count;
                result["placed_updated_entities"] = updated;
                call.Note("The placed entities changed as one undo step (undo puts their old material back); the model's own default stays changed.");
            }
            return result;
        }

        /// <summary>A copy of a renderable element (and its LOD chain) drawing the given submeshes with the new material where it drew them with the old default.</summary>
        private static RenderableElements.Element Rematerial(RenderableElements.Element element, Dictionary<Models.CS2.Component.LOD.Submesh, Materials.Material> previous, Materials.Material material, ref bool any)
        {
            if (element == null) return null;
            RenderableElements.Element copy = element.Copy();
            if (copy.Model != null && previous.TryGetValue(copy.Model, out Materials.Material old) && (ReferenceEquals(copy.Material, old) || copy.Material == null))
            {
                copy.Material = material;
                any = true;
            }
            if (copy.LODs != null)
            {
                List<RenderableElements.Element> lods = new List<RenderableElements.Element>(copy.LODs.Count);
                foreach (RenderableElements.Element lod in copy.LODs)
                    lods.Add(Rematerial(lod, previous, material, ref any));
                copy.LODs = lods;
            }
            return copy;
        }

        private static object EditModelInPlace(McpCall call, string action)
        {
            Level level = McpEditor.RequireLevel().Level;
            Models.CS2 model = McpAssets.FindModel(level, call.Str("model", required: true));
            bool required = RequiredModels.IsRequiredEntry(level.Models, model);
            JObject result = new JObject() { ["model"] = model.Name, ["action"] = action };

            switch (action)
            {
                case "set_render_flags":
                    {
                        Models.CS2.Component.LOD.RenderingFlag on = McpAssets.ParseFlags<Models.CS2.Component.LOD.RenderingFlag>(call.StrList("flags_on"), "flags_on");
                        Models.CS2.Component.LOD.RenderingFlag off = McpAssets.ParseFlags<Models.CS2.Component.LOD.RenderingFlag>(call.StrList("flags_off"), "flags_off");
                        if (on == 0 && off == 0)
                            throw new McpError("Give flags_on and/or flags_off: " + string.Join(", ", McpAssets.SingleFlags<Models.CS2.Component.LOD.RenderingFlag>()) + ".");
                        if ((on & off) != 0)
                            throw new McpError("A flag cannot be in both flags_on and flags_off.");
                        var scope = Scope(call, model);
                        int changed = 0;
                        foreach (var target in scope)
                        {
                            Models.CS2.Component.LOD.RenderingFlag before = target.submesh.RenderFlags;
                            target.submesh.RenderFlags = (before | on) & ~off;
                            if (target.submesh.RenderFlags != before) changed++;
                        }
                        if (changed != 0) Singleton.OnResourceModified?.Invoke();
                        result["submeshes"] = scope.Count;
                        result["changed"] = changed;
                        result["flags_now"] = new JArray(McpAssets.FlagNames(scope[0].submesh.RenderFlags));
                        return result;
                    }
                case "rescale":
                    {
                        double factor = call.Num("factor", 0);
                        if (!call.Has("factor") || factor <= 0 || double.IsNaN(factor) || double.IsInfinity(factor))
                            throw new McpError("'factor' has to be a number above 0 (e.g. 0.5 halves the size).");
                        if (required)
                            throw new McpError(model.Name + " is one of the models every level is required to carry; it cannot be resized.");
                        var scope = Scope(call, model);
                        int resized = 0;
                        List<string> skipped = new List<string>();
                        foreach (var target in scope)
                        {
                            if (ModelIO.RescaleSubmesh(target.submesh, (float)factor)) resized++;
                            else skipped.Add(Address(target.c, target.l, target.s));
                        }
                        if (resized != 0) Singleton.OnResourceModified?.Invoke();
                        result["resized"] = resized;
                        if (skipped.Count != 0) result["skipped_no_positions"] = new JArray(skipped);
                        call.Note("Collision is not resized with it; placed entities show the new size once the viewport resyncs.");
                        return result;
                    }
                case "remove_part":
                    {
                        if (required)
                            throw new McpError(model.Name + " is one of the models every level is required to carry (fog, light proxies, particles and decals draw with it); its parts cannot be removed.");
                        if (!call.Has("component"))
                            throw new McpError("Give 'component' (and optionally 'lod', then 'submesh') to say which part to remove; to remove the whole model use action 'delete'.");
                        var scope = Scope(call, model);
                        int c = call.Int("component");
                        bool wholeComponent = !call.Has("lod");
                        bool wholeLod = call.Has("lod") && !call.Has("submesh");
                        Models.CS2.Component component = model.Components[c];
                        if (wholeComponent && model.Components.Count == 1)
                            throw new McpError("That is the model's only component; use action 'delete' to remove the whole model.");
                        GuardPlaced(call, level, scope.Select(o => o.submesh), "that part");

                        if (wholeComponent)
                            model.Components.RemoveAt(c);
                        else if (wholeLod)
                        {
                            if (component.LODs.Count == 1)
                                throw new McpError("That is the component's only LOD; remove the component instead (lod left out).");
                            component.LODs.RemoveAt(call.Int("lod"));
                        }
                        else
                        {
                            Models.CS2.Component.LOD lod = component.LODs[call.Int("lod")];
                            if (lod.Submeshes.Count == 1)
                                throw new McpError("That is the LOD's only submesh; remove the LOD instead (submesh left out).");
                            lod.Submeshes.RemoveAt(call.Int("submesh"));
                        }
                        Singleton.OnResourceModified?.Invoke();
                        result["removed_submeshes"] = scope.Count;
                        result["components_now"] = model.Components.Count;
                        return result;
                    }
                case "delete":
                    {
                        //The engine indexes the required models by position at the head of the pak, and fog volumes, light proxies,
                        //particles and decals are drawn with them: the Model Editor refuses these too
                        if (required)
                            throw new McpError("'" + model.Name + "' is one of the models every level is required to carry. Fog volumes, light proxies, particles and decals are all drawn with it.");
                        GuardPlaced(call, level, McpAssets.SubmeshesOf(model), model.Name);
                        level.Models.Entries.Remove(model);
                        Singleton.OnResourceModified?.Invoke();
                        result["deleted"] = true;
                        return result;
                    }
            }
            throw new McpError("Unknown action '" + action + "'.");
        }

        /// <summary>Refuse to take geometry out from under placed entities unless told to (their renderables would point at nothing).</summary>
        private static void GuardPlaced(McpCall call, Level level, IEnumerable<Models.CS2.Component.LOD.Submesh> submeshes, string what)
        {
            List<McpAssets.Placement> placed = McpAssets.PlacedBy(level, new HashSet<Models.CS2.Component.LOD.Submesh>(submeshes, McpAssets.ByReference<Models.CS2.Component.LOD.Submesh>.Instance));
            if (placed.Count == 0) return;
            string list = string.Join("; ", placed.Take(10).Select(o => o.Composite.name + " > " + McpScript.EntityName(level.Commands, o.Composite, o.Entity) + " (" + McpScript.Id(o.Entity.shortGUID) + ")"));
            if (!call.Bool("force"))
                throw new McpError(placed.Count + " entities place " + what + ": " + list + (placed.Count > 10 ? " ..." : "") + ". Delete or re-point them first, or pass force: true (they will then draw nothing).");
            call.Note(placed.Count + " entities still place " + what + " and will draw nothing until deleted or re-pointed: " + list + (placed.Count > 10 ? " ..." : ""));
        }

        private static object EditModelGeometry(McpCall call, string action, Assimp.Scene scene, ModelIO.ImportPlan plan, string sourcePath)
        {
            Level level = McpEditor.RequireLevel().Level;
            Models.CS2 model = McpAssets.FindModel(level, call.Str("model", required: true));
            if (RequiredModels.IsRequiredEntry(level.Models, model))
                throw new McpError(model.Name + " is one of the models every level is required to carry; its geometry cannot be changed.");
            float scale = (float)call.Num("scale", 1.0);
            if (scale <= 0 || float.IsNaN(scale) || float.IsInfinity(scale))
                throw new McpError("'scale' has to be above 0.");
            Materials.Material material = call.Has("material") ? McpAssets.FindMaterial(level, call.Str("material")) : null;

            //Which meshes of the file: one named, else the first (replace / add_submesh) or all of them (a new LOD or component)
            List<ModelIO.PlannedSubmesh> planned = plan.AllSubmeshes().ToList();
            List<ModelIO.PlannedSubmesh> meshes;
            if (call.Has("mesh"))
                meshes = new List<ModelIO.PlannedSubmesh>() { FindPlannedMesh(scene, planned, call.Str("mesh")) };
            else if (action == "replace_geometry" || action == "add_submesh")
            {
                meshes = new List<ModelIO.PlannedSubmesh>() { planned.OrderBy(o => o.MeshIndex).First() };
                if (planned.Count > 1)
                    call.Note(Path.GetFileName(sourcePath) + " has " + planned.Count + " meshes; the first ('" + meshes[0].MeshName + "') was used. Name another with 'mesh'.");
            }
            else
                meshes = planned;

            List<string> warnings = new List<string>();
            JObject result = new JObject() { ["model"] = model.Name, ["action"] = action };

            if (action == "replace_geometry")
            {
                if (!call.Has("component") || !call.Has("lod") || !call.Has("submesh"))
                    throw new McpError("replace_geometry needs 'component', 'lod' and 'submesh' (describe_model shows them).");
                var target = Scope(call, model)[0];
                //Keep the properties of the submesh being stood in for (vertex format, material, flags), so it still renders the same way
                Models.CS2.Component.LOD.Submesh made = ModelIO.ToSubmesh(scene.Meshes[meshes[0].MeshIndex], meshes[0].Transform, ModelIO.DescribeSubmesh(target.submesh), out List<string> meshWarnings, plan.UnitScale, scale, plan.BoneNames);
                if (made == null)
                    throw new McpError("'" + meshes[0].MeshName + "' could not be converted (it needs at least 3 vertices, triangular faces, and no more than " + short.MaxValue + " vertices).");
                warnings.AddRange(meshWarnings);
                Models.CS2.Component.LOD.Submesh submesh = target.submesh;
                submesh.Data = made.Data;
                submesh.IndexCount = made.IndexCount;
                submesh.VertexCount = made.VertexCount;
                submesh.VertexFormatFull = made.VertexFormatFull;
                submesh.VertexFormatPartial = made.VertexFormatPartial;
                submesh.VertexScale = made.VertexScale;
                submesh.MaxBounds = made.MaxBounds;
                submesh.MinBounds = made.MinBounds;
                submesh.Bones.Clear();
                submesh.Bones.AddRange(made.Bones);
                if (material != null) submesh.Material = material;
                result["submesh"] = Address(target.c, target.l, target.s);
                result["vertices"] = submesh.VertexCount;
                result["triangles"] = submesh.IndexCount / 3;
            }
            else
            {
                int c = -1, l = -1;
                if (action == "add_lod" || action == "add_submesh")
                {
                    if (!call.Has("component")) throw new McpError(action + " needs 'component'.");
                    c = call.Int("component");
                    if (c < 0 || c >= model.Components.Count) throw new McpError(model.Name + " has components 0-" + (model.Components.Count - 1) + ".");
                }
                if (action == "add_submesh")
                {
                    if (!call.Has("lod")) throw new McpError("add_submesh needs 'lod'.");
                    l = call.Int("lod");
                    if (l < 0 || l >= model.Components[c].LODs.Count) throw new McpError("Component " + c + " has LODs 0-" + (model.Components[c].LODs.Count - 1) + ".");
                }

                //New submeshes draw with the material asked for, else the one the component already draws with
                Materials.Material fallback = material
                    ?? (c >= 0 ? model.Components[c].LODs.SelectMany(o => o.Submeshes).FirstOrDefault(o => o.Material != null)?.Material : null)
                    ?? model.Components.SelectMany(o => o.LODs).SelectMany(o => o.Submeshes).FirstOrDefault(o => o.Material != null)?.Material
                    ?? Singleton.FallbackMaterial;

                List<Models.CS2.Component.LOD.Submesh> built = new List<Models.CS2.Component.LOD.Submesh>();
                foreach (ModelIO.PlannedSubmesh mesh in meshes)
                {
                    Models.CS2.Component.LOD.Submesh made = ModelIO.ToSubmesh(scene.Meshes[mesh.MeshIndex], mesh.Transform, mesh.Metadata, out List<string> meshWarnings, plan.UnitScale, scale, plan.BoneNames);
                    if (made == null)
                    {
                        warnings.Add(mesh.MeshName + ": could not be converted (it needs at least 3 vertices, triangular faces, and no more than " + short.MaxValue + " vertices).");
                        continue;
                    }
                    warnings.AddRange(meshWarnings.Select(o => mesh.MeshName + ": " + o));
                    if (made.Material == null) made.Material = fallback;
                    built.Add(made);
                }
                if (built.Count == 0)
                    throw new McpError("None of the meshes could be converted. " + string.Join(" ", warnings.Take(10)));

                string lodName = call.Str("name") ?? Path.GetFileNameWithoutExtension(sourcePath);
                switch (action)
                {
                    case "add_component":
                        {
                            Models.CS2.Component component = new Models.CS2.Component();
                            Models.CS2.Component.LOD lod = new Models.CS2.Component.LOD(lodName);
                            lod.Submeshes.AddRange(built);
                            component.LODs.Add(lod);
                            model.Components.Add(component);
                            result["component"] = model.Components.Count - 1;
                            break;
                        }
                    case "add_lod":
                        {
                            Models.CS2.Component.LOD lod = new Models.CS2.Component.LOD(lodName);
                            lod.Submeshes.AddRange(built);
                            model.Components[c].LODs.Add(lod);
                            result["component"] = c;
                            result["lod"] = model.Components[c].LODs.Count - 1;
                            call.Note("Entities placed before this keep the LODs they were placed with; placements made from now on (place_model) carry the new one.");
                            break;
                        }
                    case "add_submesh":
                        {
                            model.Components[c].LODs[l].Submeshes.AddRange(built);
                            result["component"] = c;
                            result["lod"] = l;
                            result["first_submesh"] = model.Components[c].LODs[l].Submeshes.Count - built.Count;
                            break;
                        }
                }
                result["submeshes_added"] = built.Count;
                result["material"] = level.Materials.GetMaterialName(fallback);
            }

            Singleton.OnResourceModified?.Invoke();
            foreach (string warning in warnings.Take(15)) call.Note(warning);
            return result;
        }

        private static ModelIO.PlannedSubmesh FindPlannedMesh(Assimp.Scene scene, List<ModelIO.PlannedSubmesh> planned, string wanted)
        {
            string name = (wanted ?? "").Trim();
            ModelIO.PlannedSubmesh match = planned.FirstOrDefault(o => string.Equals(o.MeshName, name, StringComparison.OrdinalIgnoreCase));
            if (match == null && int.TryParse(name, out int index))
                match = planned.FirstOrDefault(o => o.MeshIndex == index);
            if (match == null)
                throw new McpError("The file has no mesh '" + name + "'. It has: " + string.Join(", ", planned.OrderBy(o => o.MeshIndex).Take(30).Select(o => o.MeshIndex + ": " + o.MeshName)) + ".");
            return match;
        }

        private static object PlaceModel(McpCall call)
        {
            Composite composite = null;
            Models.CS2 model = null;
            Level level = null;
            //Checked before anything changes, so a bad transform cannot leave half an edit behind
            JObject transform = new JObject();
            if (call.Has("position")) transform["position"] = call.Token("position");
            if (call.Has("rotation")) transform["rotation"] = call.Token("rotation");
            if (transform.Count != 0)
                McpValues.ReadTransform(transform, "position", null);

            McpEditor.UI(() =>
            {
                level = McpEditor.RequireLevel().Level;
                composite = McpScript.FindComposite(level.Commands, call.Str("composite", required: true));
                model = McpAssets.FindModel(level, call.Str("model", required: true));
                McpEditor.RequireUndoIdle();
            });

            JArray made = new JArray();
            McpScriptEdit.Run("Place " + McpAssets.Leaf(model.Name), composite, edit =>
            {
                string stem = call.Str("name") ?? McpAssets.Leaf(model.Name);
                List<Models.CS2.Component> parts = model.Components.Where(o => o.LODs.Count != 0 && o.LODs[0].Submeshes.Count != 0).ToList();
                if (parts.Count == 0)
                    throw new McpError(model.Name + " has no geometry to place.");
                List<ResourceReference> renderables = new List<ResourceReference>();
                for (int i = 0; i < parts.Count; i++)
                {
                    Models.CS2.Component component = parts[i];
                    FunctionEntity entity = edit.AddFunction(composite, FunctionType.ModelReference, parts.Count == 1 ? stem : stem + "_" + i);

                    //As the model importer builds them: LOD0's submeshes, lower LODs hung off the first
                    cResource resource = new cResource(entity.shortGUID);
                    ResourceReference renderable = resource.AddResource(ResourceType.RENDERABLE_INSTANCE);
                    foreach (Models.CS2.Component.LOD.Submesh submesh in component.LODs[0].Submeshes)
                        renderable.RenderableInstance.Add(new RenderableElements.Element { Model = submesh, Material = submesh.Material });
                    for (int l = 1; l < component.LODs.Count; l++)
                        foreach (Models.CS2.Component.LOD.Submesh submesh in component.LODs[l].Submeshes)
                            renderable.RenderableInstance[0].LODs.Add(new RenderableElements.Element { Model = submesh, Material = submesh.Material });
                    entity.AddParameter("resource", resource);
                    renderables.Add(renderable);

                    if (transform.Count != 0)
                        edit.SetParameter(composite, entity, "position", transform, allowCustom: true);
                    made.Add(McpScript.Brief(edit.Commands, composite, entity));
                }

                //Registered in REDS last, once nothing else in the edit can refuse, so a refusal leaves no orphan run behind.
                //The runs stay registered after an undo: a redo brings back these same objects, and EnsureRegistered
                //reuses an identical run rather than appending another, so placing the model again adds nothing.
                foreach (ResourceReference renderable in renderables)
                    renderable.RenderableInstance = level.RenderableElements.EnsureRegistered(renderable.RenderableInstance);
            });
            call.Note("A placed model has no collision and is not lit until the level is saved with build=true.");
            return new JObject() { ["model"] = model.Name, ["created"] = made };
        }
        #endregion

        #region Textures
        private static object DescribeTexture(McpCall call)
        {
            return McpEditor.UI<object>(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: false).Level;
                Textures.TEX4 texture = McpAssets.FindTexture(level, call.Str("texture", required: true));
                JObject info = new JObject()
                {
                    ["name"] = texture.Name,
                    ["format"] = texture.Format.ToString(),
                    ["state_flags"] = new JArray(McpAssets.FlagNames(texture.StateFlags)),
                    ["usage_flags"] = new JArray(McpAssets.FlagNames(texture.UsageFlags)),
                    ["streamed"] = DescribePart(texture.TextureStreamed),
                    ["persistent"] = DescribePart(texture.TexturePersistent),
                };
                bool cube = texture.StateFlags.HasFlag(Textures.TextureStateFlag.CUBE);
                if (cube) info["parameter_path"] = McpAssets.EnvironmentMapPath(texture);
                List<Materials.Material> users = McpAssets.MaterialsUsing(level, texture);
                info["used_by_materials_count"] = users.Count;
                info["used_by_materials"] = new JArray(users.Take(20).Select(o => level.Materials.GetMaterialName(o)));

                if (!call.Bool("preview"))
                    return info;

                string partName = call.Str("part");
                Textures.TEX4.Texture part = partName == "persistent" ? texture.TexturePersistent
                    : partName == "streamed" ? texture.TextureStreamed
                    : McpAssets.HasContent(texture.TextureStreamed) ? texture.TextureStreamed : texture.TexturePersistent;
                if (!McpAssets.HasContent(part))
                    throw new McpError(texture.Name + " has no " + (partName ?? "image") + " copy to preview.");
                int mip = Math.Max(0, call.Int("mip", 0));
                int maxSize = Math.Max(16, Math.Min(2048, call.Int("max_size", 512)));
                int levels = Math.Max(1, (int)part.MipLevels);
                if (mip >= levels)
                    throw new McpError("That copy has mips 0-" + (levels - 1) + ".");

                Bitmap bitmap = null;
                try
                {
                    //A cubemap is six chains end to end, so only its top level can be shown, as the six-face strip
                    if (mip == 0 || cube)
                        bitmap = texture.ToBitmap(part);
                    else
                    {
                        Textures.TEX4.Texture one = TextureConverter.Level(part, texture.Format, mip);
                        if (one == null) throw new McpError("Mip " + mip + " could not be read.");
                        bitmap = texture.ToBitmap(one);
                    }
                    if (bitmap == null)
                        throw new McpError(texture.Name + " could not be decoded (" + texture.Format + ").");
                    if (cube && mip != 0) call.Note("A cubemap previews its top level only.");
                    info["preview_of"] = (ReferenceEquals(part, texture.TextureStreamed) ? "streamed" : "persistent") + " mip " + (cube ? 0 : mip) + ", " + bitmap.Width + "x" + bitmap.Height;
                    return new McpImage() { Data = McpAssets.Png(bitmap, maxSize), MimeType = "image/png", Caption = info.ToString(Newtonsoft.Json.Formatting.None) };
                }
                finally
                {
                    bitmap?.Dispose();
                }
            });
        }

        private static JObject DescribePart(Textures.TEX4.Texture part)
        {
            if (!McpAssets.HasContent(part)) return null;
            JObject item = new JObject() { ["width"] = part.Width, ["height"] = part.Height, ["mips"] = part.MipLevels, ["bytes"] = part.Content.Length };
            if (part.Depth > 1) item["depth"] = part.Depth;
            return item;
        }

        private static object ImportTexture(McpCall call)
        {
            string path = McpAssets.AbsolutePath(call.Str("path", required: true), "path");
            if (!File.Exists(path))
                throw new McpError("There is no file at " + path + ".");
            string[] readable = { ".dds", ".png", ".jpg", ".jpeg", ".tga", ".bmp", ".tif", ".tiff", ".hdr", ".astc" };
            if (!readable.Contains((Path.GetExtension(path) ?? "").ToLowerInvariant()))
                throw new McpError("OpenCAGE imports " + string.Join(", ", readable) + " images.");
            int mips = call.Int("mips", 0);
            if (mips < 0) throw new McpError("'mips' is 0 (full chain) or more.");

            //Everything about the destination is settled first, so a bad name fails before texconv runs
            string replaceName = call.Str("replace");
            Textures.TextureFormat format = Textures.TextureFormat.BC7;
            int drop = 1;
            bool persistentOnly = call.Bool("persistent_only");
            string name = null;
            McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                bool sourceIsCube = DdsIsCube(path);
                if (replaceName != null)
                {
                    Textures.TEX4 slot = McpAssets.FindTexture(level, replaceName);
                    name = slot.Name;
                    if (slot.Format != Textures.TextureFormat.AUTO) format = slot.Format;
                    //Replacing follows the slot: whatever split the game shipped is the one to keep
                    bool hasStreamed = McpAssets.HasContent(slot.TextureStreamed), hasPersistent = McpAssets.HasContent(slot.TexturePersistent);
                    bool volume = slot.StateFlags.HasFlag(Textures.TextureStateFlag.VOLUME) || (slot.TexturePersistent?.Depth ?? 1) > 1 || (slot.TextureStreamed?.Depth ?? 1) > 1;
                    if (!call.Has("persistent_only")) persistentOnly = volume || !hasStreamed;
                    drop = hasStreamed && hasPersistent ? Math.Max(1, slot.TextureStreamed.MipLevels - slot.TexturePersistent.MipLevels) : 0;
                    if (slot.StateFlags.HasFlag(Textures.TextureStateFlag.CUBE)) drop = 0;
                }
                else
                {
                    name = call.Str("name") ?? AssetName.FromFile(path);
                    name = AssetName.Normalise(name);
                    string problem = AssetName.Problem(name);
                    if (problem != null) throw new McpError("'" + name + "' cannot be a texture name: " + problem);
                    if (AssetName.Exists(name, level.Textures.Entries.Select(o => o.Name)))
                        throw new McpError("The level already has a texture called '" + name + "'. Pick another name, or pass replace: '" + name + "' to put the image into it.");
                    //Every shipped cubemap is streamed only
                    if (sourceIsCube) drop = 0;
                }
            });
            if (call.Has("format"))
                format = McpAssets.ParseTextureFormat(call.Str("format"));
            if (call.Has("persistent_drop"))
            {
                drop = call.Int("persistent_drop");
                if (drop < 0) throw new McpError("'persistent_drop' is 0 (no resident copy) or more.");
            }

            byte[] converted;
            string conversionProblem;
            using (McpEditorTools.Heartbeat(call, "Converting " + Path.GetFileName(path)))
                converted = TextureConverter.Convert(path, format, mips, out conversionProblem);
            if (converted == null)
                throw new McpError(conversionProblem ?? "The image could not be converted.");

            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                Textures.TEX4 texture;
                bool replacing = replaceName != null;
                Textures.TextureUsageFlag keptUsage = 0;
                if (replacing)
                {
                    texture = McpAssets.FindTexture(level, replaceName);
                    keptUsage = texture.UsageFlags;
                }
                else
                {
                    if (AssetName.Exists(name, level.Textures.Entries.Select(o => o.Name)))
                        throw new McpError("The level already has a texture called '" + name + "'.");
                    texture = new Textures.TEX4 { Name = name };
                }

                Textures.TEX4.Texture part = converted.ToTEX4Part(out Textures.TextureFormat readFormat, out Textures.TextureStateFlag state, out Textures.TextureUsageFlag usage);
                if (part == null)
                    throw new McpError("The converted image could not be read back as a texture.");
                texture.StateFlags = state;
                //A replace keeps the slot's usage flags: they say which pack it belongs to and what the engine does with it
                texture.UsageFlags = replacing ? keptUsage : usage;
                //A8 and L8 share a DDS format, so the choice is the only thing that separates them
                texture.Format = format;
                if (call.Has("srgb"))
                {
                    if (call.Bool("srgb")) texture.StateFlags |= Textures.TextureStateFlag.ALLOW_SRGB;
                    else texture.StateFlags &= ~Textures.TextureStateFlag.ALLOW_SRGB;
                }
                OpenCAGE.EditTexture.ApplyParts(texture, part, drop, persistentOnly);
                if (!replacing) level.Textures.Entries.Add(texture);
                Singleton.OnResourceModified?.Invoke();

                JObject result = new JObject()
                {
                    ["texture"] = texture.Name,
                    [replacing ? "replaced" : "imported"] = true,
                    ["format"] = texture.Format.ToString(),
                    ["state_flags"] = new JArray(McpAssets.FlagNames(texture.StateFlags)),
                    ["streamed"] = DescribePart(texture.TextureStreamed),
                    ["persistent"] = DescribePart(texture.TexturePersistent),
                };
                if (replacing)
                {
                    List<Materials.Material> users = McpAssets.MaterialsUsing(level, texture);
                    result["used_by_materials_count"] = users.Count;
                }
                else
                    call.Note("Bind it to a material with edit_material (textures: {SAMPLER: '" + texture.Name + "'}).");
                return result;
            });
        }

        /// <summary>Whether a DDS file is a cubemap, from its header (anything else is a flat image).</summary>
        private static bool DdsIsCube(string path)
        {
            try
            {
                byte[] head;
                using (FileStream stream = File.OpenRead(path))
                {
                    head = new byte[Math.Min(256, (int)Math.Min(int.MaxValue, stream.Length))];
                    stream.Read(head, 0, head.Length);
                }
                return DdsFile.Describe(head, out DirectXTex.DirectXTexUtility.DXGI_FORMAT _, out int _, out int _, out int _, out bool cube) && cube;
            }
            catch
            {
                return false;
            }
        }

        private static object ExportTextures(McpCall call)
        {
            bool overwrite = call.Bool("overwrite");
            if (call.Has("texture"))
            {
                if (call.Has("filter") || call.Has("folder"))
                    throw new McpError("Give either 'texture' + 'path' (one texture), or 'filter' + 'folder' (several).");
                string path = McpAssets.AbsolutePath(call.Str("path", required: true), "path");
                string extension = (Path.GetExtension(path) ?? "").ToLowerInvariant();
                if (extension != ".dds" && extension != ".png" && extension != ".jpg")
                    throw new McpError("The file has to end in .dds, .png or .jpg (that picks the format).");
                if (File.Exists(path) && !overwrite)
                    throw new McpError("There is already a file at " + path + ". Pass overwrite: true to replace it.");
                return McpEditor.UI(() =>
                {
                    Level level = McpEditor.RequireLevel(forEditing: false).Level;
                    Textures.TEX4 texture = McpAssets.FindTexture(level, call.Str("texture"));
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    WriteTexture(texture, path);
                    return new JObject() { ["texture"] = texture.Name, ["written"] = path };
                });
            }

            if (!call.Has("filter"))
                throw new McpError("Give 'texture' + 'path' to export one texture, or 'filter' ('*' for all) + 'folder' to export several.");
            string folder = McpAssets.AbsolutePath(call.Str("folder", required: true), "folder");
            string ext = "." + (call.Str("format") ?? "dds").Trim().TrimStart('.').ToLowerInvariant();
            if (ext != ".dds" && ext != ".png" && ext != ".jpg")
                throw new McpError("'format' is dds, png or jpg.");
            string filter = call.Str("filter").Trim();
            List<Textures.TEX4> textures = McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: false).Level;
                return level.Textures.Entries.Where(o => o != null && (filter == "*" || filter.Length == 0 || (o.Name ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            });
            if (textures.Count == 0)
                throw new McpError("No texture's name contains '" + filter + "' (list_textures shows them).");

            int written = 0, skipped = 0;
            List<string> failures = new List<string>();
            using (McpEditorTools.Heartbeat(call, "Exporting " + textures.Count + " textures"))
            {
                //In batches on the UI thread, so the editor stays responsive between them
                for (int start = 0; start < textures.Count; start += 25)
                {
                    call.ThrowIfCancelled();
                    call.Progress("Exporting textures", start, textures.Count);
                    List<Textures.TEX4> batch = textures.Skip(start).Take(25).ToList();
                    McpEditor.UI(() =>
                    {
                        foreach (Textures.TEX4 texture in batch)
                        {
                            string relative = (texture.Name ?? "").Replace('/', '\\').TrimStart('\\');
                            string directory = Path.GetDirectoryName(relative);
                            string stem = Path.GetFileNameWithoutExtension(Path.GetFileName(relative));
                            if (string.IsNullOrEmpty(stem)) stem = Path.GetFileName(relative);
                            string target = Path.Combine(string.IsNullOrEmpty(directory) ? folder : Path.Combine(folder, directory), stem + ext);
                            if (!Path.GetFullPath(target).StartsWith(Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase))
                            {
                                failures.Add(texture.Name + ": its name leaves the folder");
                                continue;
                            }
                            if (File.Exists(target) && !overwrite) { skipped++; continue; }
                            try
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(target));
                                WriteTexture(texture, target);
                                written++;
                            }
                            catch (Exception e)
                            {
                                failures.Add(texture.Name + ": " + e.Message);
                            }
                        }
                    });
                }
            }
            JObject result = new JObject() { ["folder"] = folder, ["matched"] = textures.Count, ["written"] = written };
            if (skipped != 0) result["skipped_existing"] = skipped;
            if (failures.Count != 0)
            {
                result["failed_count"] = failures.Count;
                result["failed"] = new JArray(failures.Take(20));
            }
            return result;
        }

        /// <summary>The Texture Editor's export: DDS straight from the texture, anything else through a decoded bitmap.</summary>
        private static void WriteTexture(Textures.TEX4 texture, string path)
        {
            if (string.Equals(Path.GetExtension(path), ".dds", StringComparison.OrdinalIgnoreCase))
            {
                byte[] dds = texture.ToDDS();
                if (dds == null)
                    throw new McpError("'" + texture.Format + "' has no DDS equivalent, so " + texture.Name + " cannot be written as one; export it as .png.");
                File.WriteAllBytes(path, dds);
                return;
            }
            using (Bitmap bitmap = texture.ToBitmap())
            {
                if (bitmap == null)
                    throw new McpError("Could not decode " + texture.Name + " for export.");
                bitmap.Save(path, string.Equals(Path.GetExtension(path), ".jpg", StringComparison.OrdinalIgnoreCase) ? ImageFormat.Jpeg : ImageFormat.Png);
            }
        }

        private static object EditTextureTool(McpCall call)
        {
            string action = call.Str("action", required: true).Trim().ToLowerInvariant();
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                Textures.TEX4 texture = McpAssets.FindTexture(level, call.Str("texture", required: true));
                JObject result = new JObject() { ["texture"] = texture.Name };
                if (action == "set_flags")
                {
                    Textures.TextureStateFlag stateOn = McpAssets.ParseFlags<Textures.TextureStateFlag>(call.StrList("state_on"), "state_on");
                    Textures.TextureStateFlag stateOff = McpAssets.ParseFlags<Textures.TextureStateFlag>(call.StrList("state_off"), "state_off");
                    Textures.TextureUsageFlag usageOn = McpAssets.ParseFlags<Textures.TextureUsageFlag>(call.StrList("usage_on"), "usage_on");
                    Textures.TextureUsageFlag usageOff = McpAssets.ParseFlags<Textures.TextureUsageFlag>(call.StrList("usage_off"), "usage_off");
                    if (stateOn == 0 && stateOff == 0 && usageOn == 0 && usageOff == 0)
                        throw new McpError("Give state_on/state_off (" + string.Join(", ", McpAssets.SingleFlags<Textures.TextureStateFlag>()) + ") and/or usage_on/usage_off (" + string.Join(", ", McpAssets.SingleFlags<Textures.TextureUsageFlag>()) + ").");
                    if ((stateOn & stateOff) != 0 || (usageOn & usageOff) != 0)
                        throw new McpError("A flag cannot be turned both on and off.");
                    result["state_flags_before"] = new JArray(McpAssets.FlagNames(texture.StateFlags));
                    result["usage_flags_before"] = new JArray(McpAssets.FlagNames(texture.UsageFlags));
                    texture.StateFlags = (texture.StateFlags | stateOn) & ~stateOff;
                    texture.UsageFlags = (texture.UsageFlags | usageOn) & ~usageOff;
                    Singleton.OnResourceModified?.Invoke();
                    result["state_flags"] = new JArray(McpAssets.FlagNames(texture.StateFlags));
                    result["usage_flags"] = new JArray(McpAssets.FlagNames(texture.UsageFlags));
                    return result;
                }
                if (action == "delete")
                {
                    List<Materials.Material> users = McpAssets.MaterialsUsing(level, texture);
                    if (users.Count != 0 && !call.Bool("force"))
                        throw new McpError(users.Count + " materials use " + texture.Name + ": " + string.Join(", ", users.Take(10).Select(o => level.Materials.GetMaterialName(o))) + (users.Count > 10 ? " ..." : "") + ". Rebind them (edit_material) first, or pass force: true to empty those samplers.");
                    //Their slots are emptied rather than left pointing at a texture the level no longer writes...
                    List<(Materials.Material material, List<int> slots)> emptied = users.Select(o => (o, Enumerable.Range(0, o.TextureReferences.Count).Where(i => ReferenceEquals(o.TextureReferences[i]?.Texture, texture)).ToList())).ToList();
                    foreach ((Materials.Material material, List<int> slots) in emptied)
                        foreach (int slot in slots)
                            material.TextureReferences[slot].Texture = null;
                    //...and the samplers reading them unbound (255), as the Material Editor's Clear Texture does: a sampler left on an emptied
                    //slot is written as level texture -1 and still sampled. The remaps live on the shader entry, which other materials can
                    //share: when one of them still draws from those slots, this material gets an entry of its own, as edit_material does
                    int privatised = 0;
                    foreach ((Materials.Material material, List<int> slots) in emptied)
                    {
                        if (material.Shader?.SamplerRemaps == null || !material.Shader.SamplerRemaps.Any(slots.Contains)) continue;
                        if (level.Materials.Entries.Any(o => o != null && !ReferenceEquals(o, material) && ReferenceEquals(o.Shader, material.Shader)
                            && slots.Any(s => o.TextureReferences != null && s < o.TextureReferences.Count && o.TextureReferences[s]?.Texture != null)))
                        {
                            material.Shader = PrivateShader(level, material.Shader);
                            privatised++;
                        }
                        List<int> remaps = material.Shader.SamplerRemaps;
                        for (int i = 0; i < remaps.Count; i++)
                            if (slots.Contains(remaps[i])) remaps[i] = 255;
                    }
                    level.Textures.Entries.RemoveAll(o => ReferenceEquals(o, texture));
                    Singleton.OnResourceModified?.Invoke();
                    result["deleted"] = true;
                    if (users.Count != 0)
                        result["materials_emptied"] = new JArray(users.Take(50).Select(o => level.Materials.GetMaterialName(o)));
                    if (privatised != 0)
                        call.Note(privatised + " of those materials shared a shader entry with materials still drawing from the same samplers, so each was given an entry of its own.");
                    if (texture.StateFlags.HasFlag(Textures.TextureStateFlag.CUBE))
                        call.Note("It was a cubemap: EnvironmentMap entities whose Texture was " + McpAssets.EnvironmentMapPath(texture) + " now point at nothing.");
                    return result;
                }
                throw new McpError("Unknown action '" + action + "'.");
            });
        }

        /// <summary>A copy of a shared shader entry for one material to change alone (its bytecode shared), added to the level's pool, as edit_material makes one.</summary>
        private static Shaders.Shader PrivateShader(Level level, Shaders.Shader original)
        {
            Shaders.Shader copy = global::CathodeLib.ObjectExtensions.ObjectExtensions.Copy(original);
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

        #region Galaxy
        private static GalaxyDefinition RequireGalaxy(Level level)
        {
            if (level.GalaxyDefinition == null || level.GalaxyItems == null)
                throw new McpError("This level has no galaxy definition.");
            return level.GalaxyDefinition;
        }

        private static JObject DescribeGalaxy(GalaxyDefinition galaxy)
        {
            return new JObject()
            {
                ["name"] = galaxy.Name ?? "",
                ["star_count"] = galaxy.StarCount,
                ["entries"] = new JArray((galaxy.Entries ?? new List<GalaxyDefinition.StarTemplate>()).Select(o => new JObject()
                {
                    ["frequency"] = Math.Round(o.Frequency, 6),
                    ["min_size"] = Math.Round(o.MinSize, 6),
                    ["max_size"] = Math.Round(o.MaxSize, 6),
                    ["min_intensity"] = Math.Round(o.MinIntensity, 6),
                    ["max_intensity"] = Math.Round(o.MaxIntensity, 6),
                    ["colour"] = McpValues.Vector(o.Colour),
                })),
            };
        }

        private static object SetGalaxy(McpCall call)
        {
            if (!call.Has("name") && !call.Has("star_count") && !call.Has("entries"))
                throw new McpError("Give name, star_count and/or entries (get_galaxy shows the current ones).");
            int? starCount = call.Has("star_count") ? call.Int("star_count") : (int?)null;
            if (starCount != null && (starCount < 0 || starCount > 16384))
                throw new McpError("'star_count' is 0-16384.");

            List<GalaxyDefinition.StarTemplate> entries = null;
            if (call.Has("entries"))
            {
                entries = new List<GalaxyDefinition.StarTemplate>();
                int i = 0;
                foreach (JToken token in call.Array("entries"))
                {
                    if (!(token is JObject entry))
                        throw new McpError("entries[" + i + "] has to be an object.");
                    string at = "entries[" + i + "].";
                    foreach (JProperty property in entry.Properties())
                        if (!new[] { "frequency", "min_size", "max_size", "min_intensity", "max_intensity", "colour" }.Contains(property.Name))
                            throw new McpError(at + property.Name + " is not a star template field (frequency, min_size, max_size, min_intensity, max_intensity, colour).");
                    GalaxyDefinition.StarTemplate template = new GalaxyDefinition.StarTemplate()
                    {
                        Frequency = Field(entry, "frequency", 1f, 0f, 10000f, at),
                        MinSize = Field(entry, "min_size", 0f, 0f, 10f, at),
                        MaxSize = Field(entry, "max_size", 0.001f, 0f, 10f, at),
                        MinIntensity = Field(entry, "min_intensity", 0.5f, 0f, 1f, at),
                        MaxIntensity = Field(entry, "max_intensity", 1f, 0f, 1f, at),
                        Colour = new System.Numerics.Vector3(1f, 1f, 1f),
                    };
                    if (entry["colour"] != null)
                    {
                        System.Numerics.Vector3 colour = McpValues.ReadVector(entry["colour"], at + "colour", null);
                        if (colour.X < 0 || colour.X > 1 || colour.Y < 0 || colour.Y > 1 || colour.Z < 0 || colour.Z > 1)
                            throw new McpError(at + "colour components have to be between 0 and 1.");
                        template.Colour = colour;
                    }
                    if (template.MinSize > template.MaxSize || template.MinIntensity > template.MaxIntensity)
                        throw new McpError(at + " has a minimum above its maximum.");
                    entries.Add(template);
                    i++;
                }
            }

            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                GalaxyDefinition galaxy = RequireGalaxy(level);
                JObject before = DescribeGalaxy(galaxy);
                if (call.Has("name")) galaxy.Name = call.Str("name") ?? "";
                if (starCount != null) galaxy.StarCount = starCount.Value;
                if (entries != null) galaxy.Entries = entries;
                if (!level.GalaxyItems.Generate(galaxy))
                    throw new McpError("The galaxy could not be generated from that definition.");
                //Marks the level changed and sends the viewport its new sky, as the Galaxy Editor's Generate does
                Singleton.OnResourceModified?.Invoke();
                UnityConnection.ViewerResourceSync.ScheduleSync();
                return new JObject() { ["galaxy"] = DescribeGalaxy(galaxy), ["stars_generated"] = level.GalaxyItems.Entries.Count, ["previous"] = before };
            });
        }

        private static float Field(JObject entry, string name, float fallback, float min, float max, string at)
        {
            JToken token = entry[name];
            if (token == null || token.Type == JTokenType.Null) return fallback;
            double value = McpValues.ReadDouble(token, at + name);
            if (double.IsNaN(value) || double.IsInfinity(value) || value < min || value > max)
                throw new McpError(at + name + " has to be between " + min + " and " + max + ".");
            return (float)value;
        }
        #endregion
    }

    /// <summary>Finding and describing the open level's models, materials and textures.</summary>
    internal static class McpAssets
    {
        public const string EnvironmentMapPrefix = "n:\\content\\build\\textures\\";

        /// <summary>The model (and part) a submesh belongs to, in the open level, for describing resources.</summary>
        public static string SubmeshName(Models.CS2.Component.LOD.Submesh submesh, Models models = null)
        {
            try
            {
                models = models ?? Singleton.Editor?.CompositeBrowser?.Content?.Level?.Models;
                Models.CS2 model = models?.FindModel(submesh);
                return model?.Name ?? "?";
            }
            catch
            {
                return "?";
            }
        }

        public static string Leaf(string name)
        {
            string leaf = (name ?? "").Replace('/', '\\');
            if (leaf.EndsWith(".CS2", StringComparison.OrdinalIgnoreCase)) leaf = leaf.Substring(0, leaf.Length - 4);
            int at = leaf.LastIndexOf('\\');
            return at >= 0 ? leaf.Substring(at + 1) : leaf;
        }

        /// <summary>A path a tool is to read or write: it has to be absolute, since a relative one lands wherever OpenCAGE happens to be running.</summary>
        public static string AbsolutePath(string path, string argument)
        {
            string trimmed = (path ?? "").Trim().Trim('"');
            if (trimmed.Length == 0)
                throw new McpError("'" + argument + "' is required.");
            //A drive and a separator, or a UNC share: "C:file" and "\file" are relative to something the caller cannot see
            bool rooted = (trimmed.Length >= 3 && char.IsLetter(trimmed[0]) && trimmed[1] == ':' && (trimmed[2] == '\\' || trimmed[2] == '/'))
                || trimmed.StartsWith("\\\\") || trimmed.StartsWith("//");
            if (!rooted)
                throw new McpError("'" + argument + "' has to be a full path (e.g. C:\\Users\\me\\Mods\\file), not '" + trimmed + "'.");
            try { return Path.GetFullPath(trimmed); }
            catch (Exception e) { throw new McpError("'" + argument + "' is not a valid path: " + e.Message); }
        }

        public static Models.CS2 FindModel(Level level, string reference)
        {
            string name = (reference ?? "").Trim().Replace('/', '\\');
            if (name.Length == 0) throw new McpError("Say which model (list_models shows them).");
            List<Models.CS2> exact = level.Models.Entries.Where(o => o != null && string.Equals((o.Name ?? "").Replace('/', '\\'), name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count == 0 && !name.EndsWith(".CS2", StringComparison.OrdinalIgnoreCase))
                exact = level.Models.Entries.Where(o => o != null && string.Equals((o.Name ?? "").Replace('/', '\\'), name + ".CS2", StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count != 0) return exact[0];
            List<Models.CS2> named = level.Models.Entries.Where(o => o != null && string.Equals(Leaf(o.Name), Leaf(name), StringComparison.OrdinalIgnoreCase)).ToList();
            if (named.Count == 1) return named[0];
            if (named.Count > 1)
                throw new McpError("'" + reference + "' could be: " + string.Join("; ", named.Take(10).Select(o => o.Name)) + ". Give the full name.");
            List<Models.CS2> near = level.Models.Entries.Where(o => o != null && (o.Name ?? "").IndexOf(Leaf(name), StringComparison.OrdinalIgnoreCase) >= 0).Take(8).ToList();
            throw new McpError("The open level has no model '" + reference + "'." + (near.Count != 0 ? " Similar: " + string.Join("; ", near.Select(o => o.Name)) + "." : " list_models shows them."));
        }

        /// <summary>
        /// A material by its name (as stored, or as shown when the stored one is a hash), or "name#index" /
        /// "#index" for the index list_materials gives when names repeat.
        /// </summary>
        public static Materials.Material FindMaterial(Level level, string reference, string argument = "material")
        {
            string name = (reference ?? "").Trim();
            if (name.Length == 0) throw new McpError("'" + argument + "' is required (list_materials shows them).");
            List<Materials.Material> entries = level.Materials.Entries;
            int hash = name.LastIndexOf('#');
            if (hash >= 0 && int.TryParse(name.Substring(hash + 1), out int index))
            {
                if (index < 0 || index >= entries.Count || entries[index] == null)
                    throw new McpError("There is no material #" + index + " (the level has " + entries.Count + ").");
                string prefix = name.Substring(0, hash).Trim();
                if (prefix.Length != 0 && !string.Equals(prefix, entries[index].Name, StringComparison.OrdinalIgnoreCase) && !string.Equals(prefix, level.Materials.GetMaterialName(entries[index]), StringComparison.OrdinalIgnoreCase))
                    throw new McpError("Material #" + index + " is '" + level.Materials.GetMaterialName(entries[index]) + "', not '" + prefix + "'.");
                return entries[index];
            }
            string wanted = name.Replace('/', '\\');
            List<int> matches = new List<int>();
            for (int i = 0; i < entries.Count; i++)
            {
                Materials.Material material = entries[i];
                if (material == null) continue;
                if (string.Equals((material.Name ?? "").Replace('/', '\\'), wanted, StringComparison.OrdinalIgnoreCase) || string.Equals((level.Materials.GetMaterialName(material) ?? "").Replace('/', '\\'), wanted, StringComparison.OrdinalIgnoreCase))
                    matches.Add(i);
            }
            if (matches.Count == 1) return entries[matches[0]];
            if (matches.Count > 1)
                throw new McpError(matches.Count + " materials are called '" + name + "': give one as name#index (" + string.Join(", ", matches.Take(10).Select(o => name + "#" + o + " " + entries[o].Shader?.Ubershader)) + ").");
            List<Materials.Material> near = entries.Where(o => o != null && (level.Materials.GetMaterialName(o) ?? "").IndexOf(Leaf(wanted), StringComparison.OrdinalIgnoreCase) >= 0).Take(8).ToList();
            throw new McpError("The open level has no material '" + name + "'." + (near.Count != 0 ? " Similar: " + string.Join("; ", near.Select(o => level.Materials.GetMaterialName(o))) + "." : " list_materials shows them."));
        }

        public static Textures.TEX4 FindTexture(Level level, string reference)
        {
            string name = (reference ?? "").Trim().Replace('/', '\\');
            if (name.Length == 0) throw new McpError("Say which texture (list_textures shows them).");
            int build = name.IndexOf("content\\build\\textures\\", StringComparison.OrdinalIgnoreCase);
            if (build >= 0) name = name.Substring(build + "content\\build\\textures\\".Length);
            Textures.TEX4 match = level.Textures.Entries.FirstOrDefault(o => o != null && string.Equals((o.Name ?? "").Replace('/', '\\'), name, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
            List<Textures.TEX4> named = level.Textures.Entries.Where(o => o != null && string.Equals(Path.GetFileName((o.Name ?? "").Replace('/', '\\')), name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (named.Count == 1) return named[0];
            if (named.Count > 1)
                throw new McpError("'" + reference + "' could be: " + string.Join("; ", named.Take(10).Select(o => o.Name)) + ". Give the full name.");
            List<Textures.TEX4> near = level.Textures.Entries.Where(o => o != null && (o.Name ?? "").IndexOf(Path.GetFileName(name), StringComparison.OrdinalIgnoreCase) >= 0).Take(8).ToList();
            throw new McpError("The open level has no texture '" + reference + "'." + (near.Count != 0 ? " Similar: " + string.Join("; ", near.Select(o => o.Name)) + "." : " list_textures shows them."));
        }

        /// <summary>The value an EnvironmentMap entity's Texture parameter holds to use this cubemap.</summary>
        public static string EnvironmentMapPath(Textures.TEX4 texture) => EnvironmentMapPrefix + texture.Name;

        public static bool HasContent(Textures.TEX4.Texture part) => part?.Content != null && part.Content.Length != 0;

        public static List<Materials.Material> MaterialsUsing(Level level, Textures.TEX4 texture)
        {
            return level.Materials.Entries.Where(o => o?.TextureReferences != null && o.TextureReferences.Any(r => r != null && ReferenceEquals(r.Texture, texture))).ToList();
        }

        public static HashSet<Models.CS2.Component.LOD.Submesh> SubmeshesOf(Models.CS2 model)
        {
            return new HashSet<Models.CS2.Component.LOD.Submesh>(model.Components.SelectMany(c => c.LODs).SelectMany(l => l.Submeshes).Where(s => s != null), ByReference<Models.CS2.Component.LOD.Submesh>.Instance);
        }

        /// <summary>An entity drawing some of a model's submeshes.</summary>
        public sealed class Placement
        {
            public Composite Composite;
            public FunctionEntity Entity;
            /// <summary>Its renderables are in its 'resource' parameter (as placed and ported ones are), which an edit can rewrite.</summary>
            public bool InParameter;
        }

        /// <summary>Every entity of the open level whose renderables draw any of these submeshes (in their run or its LOD chain).</summary>
        public static List<Placement> PlacedBy(Level level, HashSet<Models.CS2.Component.LOD.Submesh> submeshes)
        {
            List<Placement> found = new List<Placement>();
            if (submeshes == null || submeshes.Count == 0 || level?.Commands?.Entries == null)
                return found;
            foreach (Composite composite in level.Commands.Entries)
            {
                if (composite?.functions == null) continue;
                foreach (FunctionEntity entity in composite.functions)
                {
                    cResource resource = entity.GetParameter(ShortGuids.resource)?.content as cResource;
                    bool inParameter = resource?.value != null && resource.value.Any(o => o != null && o.resource_type == ResourceType.RENDERABLE_INSTANCE && Draws(o.RenderableInstance, submeshes, 0));
                    bool onEntity = !inParameter && entity.resources != null && entity.resources.Any(o => o != null && o.resource_type == ResourceType.RENDERABLE_INSTANCE && Draws(o.RenderableInstance, submeshes, 0));
                    if (inParameter || onEntity)
                        found.Add(new Placement() { Composite = composite, Entity = entity, InParameter = inParameter });
                }
            }
            return found;
        }

        private static bool Draws(List<RenderableElements.Element> elements, HashSet<Models.CS2.Component.LOD.Submesh> submeshes, int depth)
        {
            if (elements == null || depth > 4) return false;
            foreach (RenderableElements.Element element in elements)
            {
                if (element == null) continue;
                if (element.Model != null && submeshes.Contains(element.Model)) return true;
                if (Draws(element.LODs, submeshes, depth + 1)) return true;
            }
            return false;
        }

        public static JObject Describe(Level level, Placement placement)
        {
            return new JObject()
            {
                ["composite"] = placement.Composite.name,
                ["id"] = McpScript.Id(placement.Entity.shortGUID),
                ["name"] = McpScript.EntityName(level.Commands, placement.Composite, placement.Entity),
            };
        }

        /// <summary>The single-bit flags set in a flags enum value, by name.</summary>
        public static IEnumerable<string> FlagNames<T>(T value) where T : struct
        {
            ulong bits = Convert.ToUInt64(value);
            foreach (T flag in Enum.GetValues(typeof(T)))
            {
                ulong bit = Convert.ToUInt64(flag);
                if (bit != 0 && (bit & (bit - 1)) == 0 && (bits & bit) == bit)
                    yield return flag.ToString();
            }
        }

        public static IEnumerable<string> SingleFlags<T>() where T : struct
        {
            foreach (T flag in Enum.GetValues(typeof(T)))
            {
                ulong bit = Convert.ToUInt64(flag);
                if (bit != 0 && (bit & (bit - 1)) == 0)
                    yield return flag.ToString();
            }
        }

        public static T ParseFlags<T>(IEnumerable<string> names, string argument) where T : struct
        {
            ulong bits = 0;
            foreach (string raw in names ?? Enumerable.Empty<string>())
            {
                string name = (raw ?? "").Trim().Replace(' ', '_');
                if (name.Length == 0) continue;
                if (!Enum.TryParse(name, true, out T flag) || !Enum.IsDefined(typeof(T), flag))
                    throw new McpError("'" + raw + "' in " + argument + " is not a flag. They are: " + string.Join(", ", SingleFlags<T>()) + ".");
                bits |= Convert.ToUInt64(flag);
            }
            return (T)Enum.ToObject(typeof(T), bits);
        }

        public static Textures.TextureFormat ParseTextureFormat(string text)
        {
            string name = (text ?? "").Trim().ToUpperInvariant();
            if (name == "BC5") name = "DXN";
            if (name == "BC1") name = "DXT1";
            if (name == "BC3") name = "DXT5";
            if (name == "BC2") name = "DXT3";
            List<Textures.TextureFormat> offered = TextureConverter.ImportFormats(Singleton.Platform).ToList();
            if (!Enum.TryParse(name, true, out Textures.TextureFormat format) || !offered.Contains(format))
                throw new McpError("'" + text + "' is not a format this build of the game can be given. Use one of: " + string.Join(", ", offered) + ".");
            return format;
        }

        /// <summary>A bitmap as PNG, scaled down so its longest edge is at most maxSize.</summary>
        public static byte[] Png(Bitmap bitmap, int maxSize)
        {
            int longest = Math.Max(bitmap.Width, bitmap.Height);
            Bitmap scaled = bitmap;
            try
            {
                if (longest > maxSize)
                {
                    double ratio = (double)maxSize / longest;
                    scaled = new Bitmap(bitmap, Math.Max(1, (int)(bitmap.Width * ratio)), Math.Max(1, (int)(bitmap.Height * ratio)));
                }
                using (MemoryStream stream = new MemoryStream())
                {
                    scaled.Save(stream, ImageFormat.Png);
                    return stream.ToArray();
                }
            }
            finally
            {
                if (!ReferenceEquals(scaled, bitmap)) scaled.Dispose();
            }
        }

        /// <summary>Compares by identity: models, materials and textures overload == as value equality, which is slow and not what "this one" means.</summary>
        public sealed class ByReference<T> : IEqualityComparer<T> where T : class
        {
            public static readonly ByReference<T> Instance = new ByReference<T>();
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
        }

        /// <summary>Refuse while the game is running, for tools that write the game's shared files (it holds them open).</summary>
        public static bool GameIsRunning()
        {
            try { return System.Diagnostics.Process.GetProcessesByName("AI").Length != 0; }
            catch { return false; }
        }
    }
}
