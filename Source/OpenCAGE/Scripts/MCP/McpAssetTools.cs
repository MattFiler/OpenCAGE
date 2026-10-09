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
                Description = "The models (CS2 meshes) the open level holds, by name, with their parts (components), materials, size (a box round LOD0, metres, the model's own space) and whether their submeshes name a collision proxy. describe_model shows one in full; place_model puts one in a composite; find_asset_users shows where one is placed. To use a model from another level, port a composite that places it (find one with search_level).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Words the model name must contain."),
                    McpSchema.Limit(100, "models"),
                    McpSchema.Offset("models")),
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() =>
                {
                    Level level = McpEditor.RequireLevel(forEditing: false).Level;
                    McpAssets.MaterialNames names = McpAssets.Names(level);
                    string[] words = (call.Str("filter") ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    List<Models.CS2> models = level.Models.Entries.Where(o => o != null && words.All(w => (o.Name ?? "").IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)).OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase).ToList();
                    JObject result = new JObject();
                    McpPaging.Page(call, models, result, "models", o =>
                    {
                        JObject item = new JObject()
                        {
                            ["name"] = o.Name,
                            ["parts"] = o.Components.Count,
                            ["submeshes"] = o.Components.Sum(c => c.LODs.FirstOrDefault()?.Submeshes.Count ?? 0),
                            ["lods"] = o.Components.Count == 0 ? 0 : o.Components.Max(c => c.LODs.Count),
                            ["materials"] = new JArray(o.Components.SelectMany(c => c.LODs.Take(1)).SelectMany(l => l.Submeshes).Where(s => s.Material != null).Select(s => names.Ref(s.Material)).Distinct().Take(12)),
                        };
                        if (McpAssets.ModelBounds(o, out System.Numerics.Vector3 min, out System.Numerics.Vector3 max)) item["size"] = McpCollision.V(max - min);
                        if (o.Components.Any(c => McpAssets.CollisionProxyOf(c) >= 0)) item["has_collision_proxy"] = true;
                        return item;
                    }, 100);
                    if (models.Count == 0 && words.Length != 0)
                        call.Note("No model's name has all of: " + string.Join(" ", words) + "." + McpNames.DidYouMean(level.Models.Entries.Where(o => o != null).Select(o => McpAssets.Leaf(o.Name)), string.Join(" ", words)));
                    return result;
                }),
            };

            yield return new McpTool()
            {
                Name = "describe_model",
                Title = "Describe model",
                Description = "One model in full: components > LODs > submeshes with vertex and triangle counts, vertex attributes, material, render flags and bounds; its overall size (a box round LOD0, metres, the model's own space) and the collision proxy its submeshes name with that proxy's bounds; whether every level must carry it; and which entities place it, with how often each is placed in the level. The component/lod/submesh indexes are what edit_model takes. preview:true returns a picture of it (the first LOD, its own materials).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("model", "The model's name (from list_models).", required: true),
                    McpSchema.Boolean("lower_lods", "Include the LODs after the first (default true)."),
                    McpSchema.Boolean("placements", "List the entities that place it (default true; slower on big levels)."),
                    McpSchema.Integer("limit", "At most this many submeshes listed (default 200)."),
                    McpSchema.Boolean("preview", "Return a picture of the model (PNG) with the description as its caption."),
                    McpSchema.Integer("max_size", "With preview: width of the picture in pixels (default 512, 128-1024)."),
                    McpSchema.Vector("view", "With preview: the direction the camera looks along (default [-0.5, -0.5, -1], from above and in front).")),
                ReadOnly = true,
                Idempotent = true,
                Run = DescribeModel,
            };

            yield return new McpTool()
            {
                Name = "export_model",
                Title = "Export model",
                Description = "Write a model to a file as the Model Editor's Export does: FBX, GLB/glTF, DAE or OBJ by the path's extension, plus a .cs2meta.json sidecar (so it re-imports exactly) and a '<name> Textures' folder. A skinned model is bound to a skeleton. Or every model matching 'filter' into 'folder' in one 'format', as the Model Editor's Export All does. Written straight away; nothing in the level changes.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("model", "The model's name (with 'path')."),
                    McpSchema.String("path", "Full path of the file to write; the extension picks the format."),
                    McpSchema.String("filter", "Instead: every model whose name contains this ('*' for all), into 'folder'."),
                    McpSchema.String("folder", "Full path of the folder for a batch (each model keeps its folder names)."),
                    McpSchema.String("format", "Batch format (default fbx).", options: new[] { "fbx", "glb", "gltf", "dae", "obj" }),
                    McpSchema.String("skeleton", "'auto' (default: the best-fitting game skeleton for a skinned model, none for a static one), 'none', or a skeleton name."),
                    McpSchema.Boolean("overwrite", "Replace files already there (default false: a single export refuses, a batch skips them).")),
                Idempotent = true,
                Run = ExportModel,
            };

            yield return new McpTool()
            {
                Name = "edit_model",
                Title = "Edit model",
                Description = "Change a model as the Model Editor does: its submeshes' default material, render flags, size, geometry (from a model file), add a LOD/component/submesh from a file, remove a part, delete the model, or duplicate it under a new name (to resize or re-material one copy: set_renderable then points an entity at the copy). " +
                    "set_material, set_render_flags, remove_part, delete and duplicate are one undo step each; rescale, replace_geometry and add_* change the mesh data and cannot be undone. Written by save_level; placed entities show a change after save_level build=true. A component can draw at most 255 submeshes (the game draws no more for one entity). A required model (describe_model's required_model) can only have its material and render flags changed.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("model", "The model's name.", required: true),
                    McpSchema.String("action", "What to do.", required: true, options: new[] { "set_material", "set_render_flags", "rescale", "replace_geometry", "add_lod", "add_component", "add_submesh", "remove_part", "delete", "duplicate" }),
                    McpSchema.Integer("component", "Component index (describe_model; place_model and set_renderable call it 'part'). Leave out to mean every component where that makes sense."),
                    McpSchema.Deprecated(McpSchema.Integer("part", "The same as 'component'.")),
                    McpSchema.Boolean("rebuild_collision", "rescale: also make collision at the new size for the entities drawing it that already have collision (a new proxy from each one's renderable, set as set_collision from_model does; one undo step). Ones drawn without collision stay without (collision_skipped_none_before) unless collision_where_none is true."),
                    McpSchema.Boolean("collision_where_none", "rescale with rebuild_collision: also give collision to the entities drawing it that had none (decals, render-only copies beside their own collider; default false)."),
                    McpSchema.Integer("lod", "LOD index within the component. Leave out for every LOD."),
                    McpSchema.Integer("submesh", "Submesh index within the LOD. Leave out for every submesh."),
                    McpSchema.String("material", "set_material: the level material to use. add_*: material for the new submeshes (default the component's own)."),
                    McpSchema.Boolean("update_placed", "set_material: also re-material entities already placing these submeshes (one undo step). Default false."),
                    McpSchema.Strings("flags_on", "set_render_flags: flags to turn on, e.g. IS_SHADOW_CASTING, DYNAMIC_GEOM."),
                    McpSchema.Strings("flags_off", "set_render_flags: flags to turn off."),
                    McpSchema.Number("factor", "rescale: size multiplier (e.g. 0.5 halves it)."),
                    McpSchema.String("path", "replace_geometry / add_*: full path of the model file to read."),
                    McpSchema.String("mesh", "replace_geometry / add_*: which mesh of the file (name or index). Default: the first for replace/add_submesh, every mesh for add_lod/add_component."),
                    McpSchema.String("name", "add_lod / add_component: the new LOD's name (default the file's name). duplicate: the copy's name (default '<name>_COPY')."),
                    McpSchema.Number("scale", "replace_geometry / add_*: resize the file's geometry on the way in (default 1)."),
                    McpSchema.Boolean("force", "remove_part / delete: go ahead even though entities place it (they will draw nothing).")),
                Destructive = true,
                Run = EditModelTool,
            };

            yield return new McpTool()
            {
                Name = "place_model",
                Title = "Place model",
                Description = "Put a model the open level holds into a composite, as ModelReference entities (one per part of the model) at a position, with collision: by default the proxy its submeshes name, with the flags and physics material the level's own placements of it use (collision 'model'); or a proxy index, 'from_model' (made from its triangles) or 'none'. " +
                    "position/look_at are in the composite's space, or the world's with space 'world' (placement picks which placement of a composite placed more than once). One undo step (an imported proxy stays in the level). Shows in the viewport at once; lit and solid in game after save_level build=true. " +
                    "An entity placed already gets collision with set_collision (proxy 'from_model'); a mesh from a file is imported, placed and given collision in one call by import_model (place_in, collision).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "Where to put it (path or id). " + McpSchema.CompositeDefaultRoot),
                    McpSchema.String("model", "The model's name, from list_models.", required: true),
                    McpSchema.Position("position", "Where it goes"),
                    McpSchema.Rotation("rotation", "Its rotation"),
                    McpSchema.Position("look_at", "Instead of rotation: a point for its +Z to face (turned about Y only)"),
                    McpSchema.String("space", "'composite' (default): position and look_at are in the composite's space; 'world': in the level's (converted for the composite).", options: new[] { "composite", "world" }),
                    McpSchema.Integer("placement", "With space 'world' in a composite placed more than once: which placement (0-based, as get_placements lists them)."),
                    McpSchema.Any("collision", "'model' (default when the model's submeshes name a proxy), a proxy index (list_collision_proxies), 'from_model' (a new proxy from its triangles) or 'none' (default when the model names none)."),
                    McpSchema.Any("collision_flags", "Collision flags: names such as ['WORLD','BALLISTIC'] (default: as the level's own placements of the proxy, else WORLD+BALLISTIC), or a number."),
                    McpSchema.String("physics_material", "The physics material its collision makes footsteps and impacts with (list_materials' names); default as the level's own placements of the proxy."),
                    McpSchema.String("name", "A name for the entity (numbered per part).")),
                Run = PlaceModel,
            };
            #endregion

            #region Textures
            yield return new McpTool()
            {
                Name = "list_textures",
                Title = "List textures",
                Description = "The textures the open level holds, with format and size. environment_maps_only lists the cubemaps an EnvironmentMap entity's Texture parameter can use, with the path to set it to. include_global adds GLOBAL's textures (shared by every level: they can be described, exported and bound to a material, not replaced). find_asset_users shows the materials, models and entities using one.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Words the texture name must contain."),
                    McpSchema.Boolean("environment_maps_only", "Only cubemaps (environment maps), each with its parameter_path."),
                    McpSchema.Boolean("include_global", "Also list GLOBAL's textures (marked global: true)."),
                    McpSchema.Limit(200, "textures"),
                    McpSchema.Offset("textures")),
                ReadOnly = true,
                Idempotent = true,
                Run = call => McpEditor.UI(() =>
                {
                    Level level = McpEditor.RequireLevel(forEditing: false).Level;
                    string[] words = (call.Str("filter") ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    bool cubes = call.Bool("environment_maps_only");
                    IEnumerable<Textures.TEX4> source = cubes ? level.Textures.GetEnvironmentMaps() : level.Textures.Entries;
                    List<(Textures.TEX4 texture, bool global)> textures = source.Where(o => o != null).Select(o => (o, false)).ToList();
                    if (call.Bool("include_global") && level.Global?.Textures?.Entries != null)
                        textures.AddRange(level.Global.Textures.Entries.Where(o => o != null && (!cubes || o.StateFlags.HasFlag(Textures.TextureStateFlag.CUBE))).Select(o => (o, true)));
                    textures = textures.Where(o => words.All(w => (o.texture.Name ?? "").IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)).OrderBy(o => o.texture.Name, StringComparer.OrdinalIgnoreCase).ThenBy(o => o.global).ToList();
                    JObject result = new JObject();
                    McpPaging.Page(call, textures, result, "textures", o =>
                    {
                        Textures.TEX4.Texture part = McpAssets.HasContent(o.texture.TextureStreamed) ? o.texture.TextureStreamed : o.texture.TexturePersistent;
                        JObject item = new JObject() { ["name"] = o.texture.Name, ["format"] = o.texture.Format.ToString() };
                        if (McpAssets.HasContent(part)) item["size"] = part.Width + "x" + part.Height;
                        if (o.global) item["global"] = true;
                        if (o.texture.StateFlags.HasFlag(Textures.TextureStateFlag.CUBE))
                        {
                            item["cubemap"] = true;
                            item["parameter_path"] = McpAssets.EnvironmentMapPath(o.texture);
                        }
                        return item;
                    }, 200);
                    return result;
                }),
            };

            yield return new McpTool()
            {
                Name = "describe_texture",
                Title = "Describe texture",
                Description = "One texture (the open level's, or GLOBAL's): format, state and usage flags, the streamed and persistent copies (size, mips, bytes) and the materials using it. preview=true returns the image itself (PNG, scaled down), one mip of either copy; a cubemap shows its six faces in a strip. find_asset_users follows it on to models and placed entities.",
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
                Description = "Import an image (DDS, PNG, JPG, TGA, BMP, TIFF, HDR) into the open level as a new texture, or into an existing one with 'replace' (keeping its format, usage flags, state flags - sRGB, cubemap, volume, alpha - and streamed/persistent split), as the Texture Editor does. Converted with texconv. " +
                    "A cubemap can only replace a cubemap (and a flat image a flat one) unless force. Every material sampling a replaced texture shows it. One undo step (the old image comes back); written by save_level.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("path", "The image file's full path.", required: true),
                    McpSchema.String("name", "The new texture's name (backslash folders allowed); default from the file name."),
                    McpSchema.String("replace", "Put the image into this existing texture instead of making a new one."),
                    McpSchema.String("format", "BC7 (default for a new one; a replaced one keeps its own), DXN (normal maps), DXT1, DXT5, BC6H, A8R8G8B8, ..."),
                    McpSchema.Integer("mips", "Mip levels to build: 0 = full chain (default), 1 = no mips."),
                    McpSchema.Integer("persistent_drop", "Levels down for the smaller always-resident copy (default 1; a replaced one keeps its own); 0 = streamed copy only."),
                    McpSchema.Boolean("persistent_only", "Keep only the resident copy (how volume textures are stored)."),
                    McpSchema.Boolean("srgb", "Set (true) or clear (false) ALLOW_SRGB: colour maps are sRGB, normal maps (DXN), masks and single-channel maps are not. Default: a replaced texture keeps its own; a new one is sRGB except in DXN/BC5, A8 and L8."),
                    McpSchema.Boolean("force", "replace: put a cubemap into a flat texture or the reverse (it changes the slot's kind).")),
                Run = ImportTexture,
            };

            yield return new McpTool()
            {
                Name = "export_textures",
                Title = "Export textures",
                Description = "Write textures of the open level to disk as DDS, PNG or JPG: one texture (the level's or GLOBAL's) to a file ('texture' + 'path', format from the extension), or every texture of the level matching 'filter' into a folder keeping their folder names ('filter' + 'folder' + 'format'). Written straight away.",
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
                Description = "Change a texture's state flags (ALLOW_SRGB, CUBE, VOLUME, ...) or usage flags (POST_PROCESSING, LENS_FLARE, GALAXY, ...), or delete it, as the Texture Editor does. Delete refuses while materials use it unless force (their samplers are then emptied and unbound, as the Material Editor's Clear Texture does). One undo step (a delete comes back with the materials' samplers); written by save_level.",
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
                Destructive = true,
                Run = SetGalaxy,
            };
            #endregion

            #region Who uses what
            yield return new McpTool()
            {
                Name = "find_asset_users",
                Title = "Find asset users",
                Description = "Where an asset is used, followed all the way to the level: a texture -> the materials sampling it -> the models drawing those -> the entities placing them; a material -> models, entities (by what they really draw, set_renderable choices included), mapping sets and 'material' overrides naming it; a model -> its entities. " +
                    "Each entity comes with how often its composite is placed in the level and up to 'positions' world positions. within (a region) keeps only placements there. Answers 'which props use texture X and where are they'.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("texture", "A texture (the level's or GLOBAL's)."),
                    McpSchema.String("material", "Or a material (list_materials' names, name#index when repeated)."),
                    McpSchema.String("model", "Or a model."),
                    McpSchema.Nested("within", "Only placements in this place (positions are world space). " + McpRegion.Help, McpRegion.Schema),
                    McpSchema.Integer("positions", "World positions per entity (default 3, at most 20; 0 for none - faster)."),
                    McpSchema.Limit(50, "entities"),
                    McpSchema.Offset("entities")),
                ReadOnly = true,
                Idempotent = true,
                Run = FindAssetUsers,
            };
            #endregion
        }

        #region Models
        private static object DescribeModel(McpCall call)
        {
            bool preview = call.Bool("preview");
            int width = Math.Max(128, Math.Min(1024, call.Int("max_size", 512)));
            System.Numerics.Vector3 view = call.Has("view") ? McpValues.ReadVector(call.Token("view"), "view", null) : new System.Numerics.Vector3(-0.5f, -0.5f, -1f);
            if (view.LengthSquared() < 1e-6f) throw McpError.Invalid("'view' is a direction: it cannot be [0, 0, 0].");
            return McpEditor.UI<object>(() =>
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
                    JObject described = new JObject() { ["index"] = c, ["lod_count"] = component.LODs.Count };
                    if (McpAssets.ModelBounds(model, out System.Numerics.Vector3 cmin, out System.Numerics.Vector3 cmax, c)) described["bounds"] = McpAssets.BoxJson(cmin, cmax);
                    int proxy = McpAssets.CollisionProxyOf(component);
                    if (proxy >= 0)
                    {
                        described["collision_proxy"] = proxy;
                        if (McpAssets.ProxyBounds(level, proxy, out System.Numerics.Vector3 pmin, out System.Numerics.Vector3 pmax)) described["collision_bounds"] = McpAssets.BoxJson(pmin, pmax);
                    }
                    described["lods"] = lods;
                    components.Add(described);
                }

                JObject result = new JObject()
                {
                    ["model"] = model.Name,
                    ["required_model"] = RequiredModels.IsRequiredEntry(level.Models, model),
                };
                if (McpAssets.ModelBounds(model, out System.Numerics.Vector3 min, out System.Numerics.Vector3 max))
                {
                    result["bounds"] = McpAssets.BoxJson(min, max);
                    result["space"] = "the model's own (metres; Y up)";
                }
                result["components"] = components;
                result["submesh_count"] = total;
                int bones = Skeleton.RequiredBoneCount(model);
                if (bones != 0) result["skinned_bones"] = bones;
                if (listed < total && (lowerLods || listed >= limit)) result["submeshes_listed"] = listed;
                if (model.Components.Any(o => o.LODs.Count != 0 && o.LODs[0].Submeshes.Count > RenderableElements.MaxElementsPerInstance))
                    call.Note("A component has more than " + RenderableElements.MaxElementsPerInstance + " submeshes at LOD0: the game draws no more than that for one entity.");

                if (call.Bool("placements", true))
                {
                    List<McpAssets.Placement> placed = McpAssets.PlacedBy(level, McpAssets.SubmeshesOf(model));
                    result["placed_by_count"] = placed.Count;
                    result["placed_by"] = new JArray(placed.Take(25).Select(o =>
                    {
                        JObject item = McpAssets.Describe(level, o);
                        item["composite_placed"] = McpAssets.PlacementCount(level.Commands, o.Composite);
                        return item;
                    }));
                    if (placed.Count != 0) call.Note("get_composite_preview of a placed_by composite shows it as placed; find_asset_users {model} gives world positions.");
                }
                if (!preview)
                    return result;

                byte[] png = OpenCAGE.Popups.UserControls.GUI_ModelViewer.RenderPreview(model.Components.Where(o => o.LODs.Count != 0).SelectMany(o => o.LODs[0].Submeshes).Where(o => o != null).ToList(), width, view);
                if (png == null)
                    throw new McpError(McpErrorCodes.Failed, model.Name + " could not be drawn (it has no geometry the previewer can show).");
                return new McpImage() { Data = png, MimeType = "image/png", Caption = result.ToString(Newtonsoft.Json.Formatting.None) };
            });
        }

        private static JObject DescribeSubmesh(Level level, Models.CS2.Component.LOD.Submesh submesh, int index)
        {
            JObject item = new JObject()
            {
                ["index"] = index,
                ["vertices"] = submesh.VertexCount,
                ["triangles"] = submesh.IndexCount / 3,
                ["material"] = submesh.Material == null ? null : McpAssets.MaterialRef(level, submesh.Material),
                ["render_flags"] = new JArray(McpAssets.FlagNames(submesh.RenderFlags)),
                ["attributes"] = new JArray(VertexUsages(submesh.VertexFormatFull)),
                ["bounds"] = McpAssets.BoxJson(submesh.MinBounds, submesh.MaxBounds),
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
            if (call.Has("filter") || call.Has("folder"))
                return ExportModels(call);
            if (!call.Has("model") || !call.Has("path"))
                throw McpError.Invalid("Give 'model' + 'path' to export one model, or 'filter' ('*' for all) + 'folder' (+ 'format') to export several.");
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
                            throw Singleton.Global?.Skeletons == null
                                ? new McpError(McpErrorCodes.NotFound, "There is no skeleton '" + skeletonArg + "' (the animation data is not loaded). Use 'auto' to pick the best fit.")
                                : McpError.NotFound("skeleton", skeletonArg, Singleton.Global.Skeletons.Skeletons.Select(o => o.Name), "Use 'auto' to pick the best fit (list_skeletons lists them).");
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

        /// <summary>Export All: every model matching a filter into a folder, keeping their folder names, in batches on the UI thread.</summary>
        private static object ExportModels(McpCall call)
        {
            if (call.Has("model") || call.Has("path"))
                throw McpError.Invalid("Give either 'model' + 'path' (one model), or 'filter' + 'folder' (several).");
            string folder = McpAssets.AbsolutePath(call.Str("folder", required: true), "folder");
            string filter = (call.Str("filter", required: true) ?? "").Trim();
            string extension = "." + (call.Str("format") ?? "fbx").Trim().TrimStart('.').ToLowerInvariant();
            if (!OpenCAGE.ModelExport.ModelExporter.Formats.Any(o => o.Extension == extension))
                throw McpError.Invalid("'format' is one of: " + string.Join(", ", OpenCAGE.ModelExport.ModelExporter.Formats.Select(o => o.Extension.TrimStart('.'))) + ".");
            bool overwrite = call.Bool("overwrite");
            string skeletonArg = (call.Str("skeleton") ?? "auto").Trim();
            string[] words = filter == "*" ? new string[0] : filter.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            List<Models.CS2> models = McpEditor.UI(() => McpEditor.RequireLevel(forEditing: false).Level.Models.Entries
                .Where(o => o != null && words.All(w => (o.Name ?? "").IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)).OrderBy(o => o.Name).ToList());
            if (models.Count == 0)
                throw new McpError(McpErrorCodes.NotFound, "No model's name has all of '" + filter + "' (list_models shows them).");

            int written = 0, skipped = 0;
            List<string> failures = new List<string>();
            JArray files = new JArray();
            using (McpEditorTools.Heartbeat(call, "Exporting " + models.Count + " models"))
            {
                for (int i = 0; i < models.Count; i++)
                {
                    call.ThrowIfCancelled();
                    call.Progress("Exporting models", i, models.Count);
                    Models.CS2 model = models[i];
                    string relative = (model.Name ?? "model").Replace('/', '\\').TrimStart('\\');
                    if (relative.EndsWith(".CS2", StringComparison.OrdinalIgnoreCase)) relative = relative.Substring(0, relative.Length - 4);
                    string target = Path.GetFullPath(Path.Combine(folder, relative + extension));
                    if (!target.StartsWith(Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase)) { failures.Add(model.Name + ": its name leaves the folder"); continue; }
                    if (File.Exists(target) && !overwrite) { skipped++; continue; }
                    try
                    {
                        McpEditor.UI(() =>
                        {
                            Skeleton skeleton = null;
                            int required = Skeleton.RequiredBoneCount(model);
                            if (required != 0 && !string.Equals(skeletonArg, "none", StringComparison.OrdinalIgnoreCase))
                                skeleton = string.Equals(skeletonArg, "auto", StringComparison.OrdinalIgnoreCase) ? BestSkeleton(model, required, out string _) : Singleton.Global?.GetSkeleton(skeletonArg);
                            Directory.CreateDirectory(Path.GetDirectoryName(target));
                            model.ExportMesh(target, skeleton);
                        });
                        written++;
                        if (files.Count < 50) files.Add(target);
                    }
                    catch (Exception e)
                    {
                        failures.Add(model.Name + ": " + (e is McpError ? e.Message : e.GetType().Name + ": " + e.Message));
                    }
                }
            }
            JObject result = new JObject() { ["folder"] = folder, ["matched"] = models.Count, ["written"] = written, ["files"] = files };
            if (skipped != 0) result["skipped_existing"] = skipped;
            if (failures.Count != 0)
            {
                result["failed_count"] = failures.Count;
                result["failed"] = new JArray(failures.Take(20));
            }
            call.Note("Each file has its .cs2meta.json sidecar beside it: keep them together so import_model brings a model back exactly.");
            return result;
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
            //'part' (as place_model and set_renderable call a component) is taken as 'component'
            if (call.Has("part"))
            {
                if (call.Has("component")) throw McpError.Invalid("'part' and 'component' mean the same; give one.");
                call.Args["component"] = call.Args["part"];
                call.Args.Remove("part");
            }
            if (action == "duplicate")
                return McpEditor.UI(() => DuplicateModel(call));
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

        /// <summary>A copy of a model under a new name (its own submeshes, geometry copied; same materials and collision proxies), as one undo step.</summary>
        private static object DuplicateModel(McpCall call)
        {
            Level level = McpEditor.RequireLevel().Level;
            McpEditor.RequireUndoIdle();
            Models.CS2 model = McpAssets.FindModel(level, call.Str("model", required: true));
            string name = AssetName.Normalise((call.Str("name") ?? (model.Name.EndsWith(".CS2", StringComparison.OrdinalIgnoreCase) ? model.Name.Substring(0, model.Name.Length - 4) : model.Name) + "_COPY").Trim());
            string problem = AssetName.Problem(name);
            if (problem != null) throw McpError.Invalid("'" + name + "' cannot be a model name: " + problem);
            if (AssetName.Exists(name, level.Models.Entries.Where(o => o != null).Select(o => o.Name)) || AssetName.Exists(name + ".CS2", level.Models.Entries.Where(o => o != null).Select(o => o.Name)))
                throw new McpError(McpErrorCodes.Conflict, "The level already has a model called '" + name + "'. Give another 'name'.");
            if (model.Name.EndsWith(".CS2", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".CS2", StringComparison.OrdinalIgnoreCase)) name += ".CS2";

            Models.CS2 copy = new Models.CS2() { Name = name };
            foreach (Models.CS2.Component component in model.Components)
            {
                Models.CS2.Component made = new Models.CS2.Component();
                foreach (Models.CS2.Component.LOD lod in component.LODs)
                {
                    Models.CS2.Component.LOD copied = new Models.CS2.Component.LOD(lod.Name);
                    foreach (Models.CS2.Component.LOD.Submesh submesh in lod.Submeshes)
                        copied.Submeshes.Add(submesh == null ? null : new Models.CS2.Component.LOD.Submesh()
                        {
                            MinBounds = submesh.MinBounds,
                            MaxBounds = submesh.MaxBounds,
                            MinLODRange = submesh.MinLODRange,
                            MaxLODRange = submesh.MaxLODRange,
                            RenderFlags = submesh.RenderFlags,
                            Material = submesh.Material,
                            CollisionProxyIndex = submesh.CollisionProxyIndex,
                            WeightedCollision = submesh.WeightedCollision,
                            MorphAnimSet = submesh.MorphAnimSet,
                            VertexFormatFull = submesh.VertexFormatFull,
                            VertexFormatPartial = submesh.VertexFormatPartial,
                            VertexScale = submesh.VertexScale,
                            VertexCount = submesh.VertexCount,
                            IndexCount = submesh.IndexCount,
                            Bones = new List<int>(submesh.Bones),
                            Data = (byte[])submesh.Data.Clone(),
                        });
                    made.LODs.Add(copied);
                }
                copy.Components.Add(made);
            }
            (Action apply, Action revert) listed = McpAssetEdit.ListChange(level.Models.Entries, copy, level.Models.Entries.Count, true);
            listed.apply();
            Singleton.OnResourceModified?.Invoke();
            McpAssetEdit.Record("Duplicate model " + Leaf(model.Name), listed.apply, () =>
            {
                //An entity pointed at the copy since by a change that kept no step would draw nothing
                McpAssets.RefuseRemovalWhileUsed("Model " + copy.Name, McpAssets.UsersOf(level, copy));
                listed.revert();
            });
            call.Note("The copy shares the original's materials and collision proxies. set_renderable {model: '" + copy.Name + "'} points an entity at it; edit_model then changes the copy alone.");
            return new JObject() { ["model"] = copy.Name, ["copied_from"] = model.Name, ["components"] = copy.Components.Count, ["undo"] = "One undo step: 'AI: Duplicate model " + Leaf(model.Name) + "'." };
        }

        private static string Leaf(string name) => McpAssets.Leaf(name);

        /// <summary>Whether an entity has a collision mapping, in its own list or its 'resource' parameter (where set_collision would find one).</summary>
        private static bool HasCollision(FunctionEntity entity)
        {
            if (entity.resources != null && entity.resources.Any(o => o != null && o.resource_type == ResourceType.COLLISION_MAPPING))
                return true;
            return entity.GetParameter(ShortGuids.resource)?.content is cResource resource && resource.value != null && resource.value.Any(o => o != null && o.resource_type == ResourceType.COLLISION_MAPPING);
        }

        private static object SetModelMaterial(McpCall call)
        {
            bool updatePlaced = call.Bool("update_placed");
            //Everything decided first, on the UI thread; the placed entities then change as one undo step
            List<(Composite composite, FunctionEntity entity, cResource after)> rewrites = new List<(Composite, FunctionEntity, cResource)>();
            object mark = null;
            JObject result = McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                Models.CS2 model = McpAssets.FindModel(level, call.Str("model", required: true));
                Materials.Material material = McpAssets.FindMaterial(level, call.Str("material", required: true));
                McpEditor.RequireUndoIdle();
                mark = UndoStack.Current.Mark();
                var scope = Scope(call, model);

                Dictionary<Models.CS2.Component.LOD.Submesh, Materials.Material> previous = new Dictionary<Models.CS2.Component.LOD.Submesh, Materials.Material>(McpAssets.ByReference<Models.CS2.Component.LOD.Submesh>.Instance);
                JArray changed = new JArray();
                foreach (var target in scope)
                {
                    if (ReferenceEquals(target.submesh.Material, material)) continue;
                    previous[target.submesh] = target.submesh.Material;
                    changed.Add(new JObject() { ["submesh"] = Address(target.c, target.l, target.s), ["was"] = target.submesh.Material == null ? null : McpAssets.MaterialRef(level, target.submesh.Material) });
                    target.submesh.Material = material;
                }
                if (previous.Count != 0)
                {
                    Singleton.OnResourceModified?.Invoke();
                    List<KeyValuePair<Models.CS2.Component.LOD.Submesh, Materials.Material>> was = previous.ToList();
                    McpAssetEdit.Record("Re-material " + McpAssets.Leaf(model.Name), () => { foreach (var o in was) o.Key.Material = material; }, () => { foreach (var o in was) o.Key.Material = o.Value; });
                }

                JObject summary = new JObject() { ["model"] = model.Name, ["material"] = McpAssets.MaterialRef(level, material), ["changed"] = changed };
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
                result["placements_affected"] = McpEditor.UI(() => rewrites.Select(o => o.composite).Distinct().Sum(o => McpAssets.PlacementCount(McpEditor.RequireCommands(), o)));
                result["needs_build"] = true;
                call.Note("Placed entities show the new material after save_level build=true (the viewport shows it at once).");
            }
            //The model's default and the placed entities undo together
            string stepLabel = "AI: Re-material " + McpAssets.Leaf(call.Str("model"));
            McpEditor.UI(() =>
            {
                int steps = UndoStack.Current.StepsSince(mark);
                if (steps > 1) UndoStack.Current.Collapse(steps, stepLabel);
            });
            result["undo"] = "One undo step: '" + stepLabel + "'.";
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
                        List<(Models.CS2.Component.LOD.Submesh submesh, Models.CS2.Component.LOD.RenderingFlag before, Models.CS2.Component.LOD.RenderingFlag after)> flags = new List<(Models.CS2.Component.LOD.Submesh, Models.CS2.Component.LOD.RenderingFlag, Models.CS2.Component.LOD.RenderingFlag)>();
                        foreach (var target in scope)
                        {
                            Models.CS2.Component.LOD.RenderingFlag before = target.submesh.RenderFlags;
                            target.submesh.RenderFlags = (before | on) & ~off;
                            if (target.submesh.RenderFlags != before) changed++;
                            flags.Add((target.submesh, before, target.submesh.RenderFlags));
                        }
                        if (changed != 0)
                        {
                            Singleton.OnResourceModified?.Invoke();
                            McpAssetEdit.Record("Set render flags of " + McpAssets.Leaf(model.Name), () => { foreach (var o in flags) o.submesh.RenderFlags = o.after; }, () => { foreach (var o in flags) o.submesh.RenderFlags = o.before; });
                        }
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
                        List<McpAssets.Placement> drawing = McpAssets.PlacedBy(level, new HashSet<Models.CS2.Component.LOD.Submesh>(scope.Select(o => o.submesh), McpAssets.ByReference<Models.CS2.Component.LOD.Submesh>.Instance));
                        if (call.Bool("rebuild_collision") && resized != 0)
                        {
                            //Collision at the new size: a proxy made from each entity's renderable, assigned as set_collision from_model does.
                            //Only where there was collision, unless asked: one drawn without it (a decal, a render-only copy beside its own
                            //collider) is meant to be walked through
                            bool everywhere = call.Bool("collision_where_none");
                            McpEditor.RequireUndoIdle();
                            JArray rebuilt = new JArray();
                            List<string> failed = new List<string>();
                            List<string> none = new List<string>();
                            using (UndoStack.Current.BeginGroup("AI: Rebuild collision of " + McpAssets.Leaf(model.Name)))
                            {
                                foreach (McpAssets.Placement placement in drawing.Where(o => o.Entity.function == FunctionType.ModelReference))
                                {
                                    if (!everywhere && !HasCollision(placement.Entity))
                                    {
                                        none.Add(McpScript.EntityName(level.Commands, placement.Composite, placement.Entity) + " in " + placement.Composite.name);
                                        continue;
                                    }
                                    try
                                    {
                                        JObject made = McpResourceTools.GiveCollision(call, level, placement.Composite, placement.Entity, "from_model", null, null);
                                        if (made != null && rebuilt.Count < 20) rebuilt.Add(new JObject() { ["entity"] = McpAssets.Describe(level, placement), ["collision"] = made });
                                    }
                                    catch (McpError e) { failed.Add(McpScript.EntityName(level.Commands, placement.Composite, placement.Entity) + ": " + e.Message); }
                                }
                            }
                            result["collision_rebuilt"] = rebuilt;
                            if (failed.Count != 0) result["collision_failed"] = new JArray(failed.Take(10));
                            if (none.Count != 0)
                            {
                                result["collision_skipped_none_before"] = new JObject() { ["count"] = none.Count, ["entities"] = new JArray(none.Take(10)) };
                                call.Note(none.Count + " of the entities drawing it had no collision and were left without (collision_skipped_none_before); pass collision_where_none: true to give them collision too.");
                            }
                            call.Note("The rescale itself cannot be undone; the new collision assignments are one undo step (the proxies stay in the level).");
                        }
                        else
                            call.Note("The rescale cannot be undone. Collision is not resized with it" + (drawing.Count != 0 ? " (" + drawing.Count + " entities draw it): pass rebuild_collision: true, or set_collision proxy 'from_model' on each" : "") + ". Placed entities show the new size once the viewport resyncs, and in game after save_level build=true.");
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

                        (Action apply, Action revert) removal;
                        if (wholeComponent)
                            removal = McpAssetEdit.ListChange(model.Components, component, c, false);
                        else if (wholeLod)
                        {
                            if (component.LODs.Count == 1)
                                throw new McpError("That is the component's only LOD; remove the component instead (lod left out).");
                            removal = McpAssetEdit.ListChange(component.LODs, component.LODs[call.Int("lod")], call.Int("lod"), false);
                        }
                        else
                        {
                            Models.CS2.Component.LOD lod = component.LODs[call.Int("lod")];
                            if (lod.Submeshes.Count == 1)
                                throw new McpError("That is the LOD's only submesh; remove the LOD instead (submesh left out).");
                            removal = McpAssetEdit.ListChange(lod.Submeshes, lod.Submeshes[call.Int("submesh")], call.Int("submesh"), false);
                        }
                        removal.apply();
                        Singleton.OnResourceModified?.Invoke();
                        McpAssetEdit.Record("Remove part of " + McpAssets.Leaf(model.Name), removal.apply, removal.revert);
                        result["removed_submeshes"] = scope.Count;
                        result["components_now"] = model.Components.Count;
                        result["undo"] = "One undo step.";
                        return result;
                    }
                case "delete":
                    {
                        //The engine indexes the required models by position at the head of the pak, and fog volumes, light proxies,
                        //particles and decals are drawn with them: the Model Editor refuses these too
                        if (required)
                            throw new McpError("'" + model.Name + "' is one of the models every level is required to carry. Fog volumes, light proxies, particles and decals are all drawn with it.");
                        GuardPlaced(call, level, McpAssets.SubmeshesOf(model), model.Name);
                        (Action apply, Action revert) removal = McpAssetEdit.ListChange(level.Models.Entries, model, level.Models.Entries.FindIndex(o => ReferenceEquals(o, model)), false);
                        removal.apply();
                        Singleton.OnResourceModified?.Invoke();
                        McpAssetEdit.Record("Delete model " + McpAssets.Leaf(model.Name), removal.apply, removal.revert);
                        result["deleted"] = true;
                        result["undo"] = "One undo step.";
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
                //The game draws at most 255 elements of an entity's run: more would not show
                int inLod = action == "add_submesh" ? model.Components[c].LODs[l].Submeshes.Count + built.Count : built.Count;
                if (inLod > RenderableElements.MaxElementsPerInstance)
                    throw new McpError(McpErrorCodes.Refused, "That would make a LOD of " + inLod + " submeshes, but the game draws at most " + RenderableElements.MaxElementsPerInstance + " for one entity. Merge meshes in the file, or add them as another component (add_component) and place each component separately.");

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
                result["material"] = McpAssets.MaterialRef(level, fallback);
            }

            Singleton.OnResourceModified?.Invoke();
            foreach (string warning in warnings.Take(15)) call.Note(warning);
            call.Note("This changed the model's mesh data and cannot be undone (reload the level without saving to drop it). Placed entities show it in game after save_level build=true.");
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
            //Everything is checked before anything changes, so a bad argument cannot leave half an edit behind
            if (call.Has("rotation") && call.Has("look_at")) throw McpError.Invalid("Give rotation or look_at, not both.");
            string space = (call.Str("space") ?? "composite").Trim().ToLowerInvariant();
            if (space != "composite" && space != "world") throw McpError.Invalid("'space' is 'composite' or 'world'.");
            if (call.Has("placement") && space != "world") throw McpError.Invalid("'placement' goes with space 'world' (it picks the placement whose frame converts the position).");
            System.Numerics.Vector3 position = call.Has("position") ? McpValues.ReadVector(call.Token("position"), "position", null) : System.Numerics.Vector3.Zero;
            System.Numerics.Vector3 rotation = call.Has("rotation") ? McpValues.ReadVector(call.Token("rotation"), "rotation", null) : System.Numerics.Vector3.Zero;
            if (call.Has("look_at"))
            {
                System.Numerics.Vector3 target = McpValues.ReadVector(call.Token("look_at"), "look_at", null);
                System.Numerics.Vector3 facing = InstanceTransform.LookAt(position, new System.Numerics.Vector3(target.X, position.Y, target.Z));
                rotation = new System.Numerics.Vector3(0, facing.Y, 0);
            }
            McpResourceTools.CheckCollisionFlags(call.Token("collision_flags"));
            string collision = call.Has("collision") ? (call.Token("collision").Type == JTokenType.String ? ((string)call.Token("collision")).Trim().ToLowerInvariant() : call.Token("collision").ToString()) : null;
            if (collision != null && collision != "model" && collision != "from_model" && collision != "none" && !int.TryParse(collision, out int _))
                throw McpError.Invalid("'collision' is 'model', a proxy index, 'from_model' or 'none'.");

            List<Models.CS2.Component> parts = null;
            cTransform placed = new cTransform(position, rotation);
            JObject frameDescribed = null;
            bool inRoot = false;
            McpEditor.UI(() =>
            {
                level = McpEditor.RequireLevel().Level;
                composite = McpScript.FindComposite(level.Commands, call.Str("composite") ?? "root");
                inRoot = composite == level.Commands.EntryPoints[0];
                model = McpAssets.FindModel(level, call.Str("model", required: true));
                McpEditor.RequireUndoIdle();
                parts = model.Components.Where(o => o.LODs.Count != 0 && o.LODs[0].Submeshes.Count != 0).ToList();
                if (parts.Count == 0)
                    throw new McpError(McpErrorCodes.Refused, model.Name + " has no geometry to place.");
                Models.CS2.Component tooBig = parts.FirstOrDefault(o => o.LODs[0].Submeshes.Count > RenderableElements.MaxElementsPerInstance);
                if (tooBig != null)
                    throw new McpError(McpErrorCodes.Refused, model.Name + " has a part of " + tooBig.LODs[0].Submeshes.Count + " submeshes, but the game draws at most " + RenderableElements.MaxElementsPerInstance + " for one entity. Re-import it (import_model splits big meshes into parts of " + RenderableElements.MaxElementsPerInstance + " or fewer).");
                //Collision: what the model's submeshes name, unless told otherwise
                if (collision == null)
                    collision = parts.Any(o => McpAssets.CollisionProxyOf(o) >= 0) ? "model" : "none";
                else if (collision == "model" && !parts.Any(o => McpAssets.CollisionProxyOf(o) >= 0))
                    throw McpError.Invalid(model.Name + "'s submeshes name no collision proxy: use collision 'from_model' to make one from its triangles, a proxy index (list_collision_proxies), or 'none'.");
                else if (int.TryParse(collision, out int index) && (level.Collision == null || level.Collision.GetCompound(index) == null))
                    throw new McpError(McpErrorCodes.NotFound, "There is no collision proxy " + index + " (list_collision_proxies shows them).");
                if (call.Has("physics_material")) McpAssets.FindMaterial(level, call.Str("physics_material"), "physics_material");
                if (space == "world")
                    placed = InstanceTransform.ToLocal(McpSpatialTools.FrameOf(level.Commands, composite, call.Has("placement") ? call.Int("placement") : (int?)null, "'placement'", out frameDescribed), placed);
            });
            JObject transform = new JObject() { ["position"] = McpValues.Vector(placed.position), ["rotation"] = McpValues.Vector(placed.rotation) };
            bool positioned = call.Has("position") || call.Has("rotation") || call.Has("look_at");

            object mark = McpEditor.UI(() => UndoStack.Current.Mark());
            JArray made = new JArray();
            List<FunctionEntity> entities = new List<FunctionEntity>();
            McpScriptEdit.Run("Place " + McpAssets.Leaf(model.Name), composite, edit =>
            {
                string stem = call.Str("name") ?? McpAssets.Leaf(model.Name);
                List<ResourceReference> renderables = new List<ResourceReference>();
                for (int i = 0; i < parts.Count; i++)
                {
                    Models.CS2.Component component = parts[i];
                    FunctionEntity entity = edit.AddFunction(composite, FunctionType.ModelReference, parts.Count == 1 ? stem : stem + "_" + i);
                    entities.Add(entity);

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

                    if (positioned)
                        edit.SetParameter(composite, entity, "position", transform, allowCustom: true);
                    made.Add(McpScript.Brief(edit.Commands, composite, entity));
                }

                //Registered in REDS last, once nothing else in the edit can refuse, so a refusal leaves no orphan run behind.
                //The runs stay registered after an undo: a redo brings back these same objects, and EnsureRegistered
                //reuses an identical run rather than appending another, so placing the model again adds nothing.
                foreach (ResourceReference renderable in renderables)
                    renderable.RenderableInstance = level.RenderableElements.EnsureRegistered(renderable.RenderableInstance);
            });

            //Collision for each new ModelReference, then everything folded into the one step
            JArray collisions = new JArray();
            string label = "AI: Place " + McpAssets.Leaf(model.Name);
            McpEditor.UI(() =>
            {
                try
                {
                    if (collision != "none")
                    {
                        for (int i = 0; i < entities.Count; i++)
                        {
                            JToken proxy;
                            if (collision == "model")
                            {
                                if (McpAssets.CollisionProxyOf(parts[i]) < 0)
                                {
                                    call.Note("Part " + i + "'s submeshes name no collision proxy, so it has none (set_collision proxy 'from_model' makes one).");
                                    continue;
                                }
                                proxy = "model";
                            }
                            else proxy = int.TryParse(collision, out int index) ? (JToken)index : collision;
                            try
                            {
                                JObject given = McpResourceTools.GiveCollision(call, level, composite, entities[i], proxy, call.Str("physics_material"), call.Token("collision_flags"));
                                collisions.Add(given ?? (JToken)JValue.CreateNull());
                            }
                            catch (McpError e)
                            {
                                call.Note("Part " + i + " was placed without collision: " + e.Message);
                                collisions.Add(JValue.CreateNull());
                            }
                        }
                    }
                }
                finally
                {
                    int steps = UndoStack.Current.StepsSince(mark);
                    if (steps > 1) UndoStack.Current.Collapse(steps, label);
                }
            });
            JObject result = new JObject() { ["model"] = model.Name, ["composite"] = composite.name, ["created"] = made, ["space"] = inRoot ? "world" : "composite" };
            if (space == "world" && !inRoot)
            {
                result["world_position"] = McpValues.Vector(position);
                result["frame"] = frameDescribed;
            }
            if (collision == "none")
            {
                result["collision"] = "none";
                call.Note("It has no collision (walk-through): " + (call.Has("collision") ? "as asked." : "its submeshes name no proxy. place_model collision 'from_model', or set_collision on each created entity, gives it some."));
            }
            else
                result["collision"] = collisions;
            result["needs_build"] = true;
            result["undo"] = "One undo step: '" + label + "'" + (collision == "from_model" ? " (an imported proxy stays in the level)." : ".");
            call.Note("It shows in the viewport now; it is lit, and solid, in game after save_level build=true.");
            return result;
        }
        #endregion

        #region Textures
        private static object DescribeTexture(McpCall call)
        {
            return McpEditor.UI<object>(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: false).Level;
                Textures.TEX4 texture = McpAssets.FindTexture(level, call.Str("texture", required: true), true, out bool global);
                JObject info = new JObject()
                {
                    ["name"] = texture.Name,
                    ["format"] = texture.Format.ToString(),
                    ["state_flags"] = new JArray(McpAssets.FlagNames(texture.StateFlags)),
                    ["usage_flags"] = new JArray(McpAssets.FlagNames(texture.UsageFlags)),
                    ["streamed"] = DescribePart(texture.TextureStreamed),
                    ["persistent"] = DescribePart(texture.TexturePersistent),
                };
                if (global) info["global"] = "one of GLOBAL's textures, shared by every level: it can be bound to a material and exported, not replaced or edited";
                bool cube = texture.StateFlags.HasFlag(Textures.TextureStateFlag.CUBE);
                if (cube) info["parameter_path"] = McpAssets.EnvironmentMapPath(texture);
                List<Materials.Material> users = McpAssets.MaterialsUsing(level, texture);
                McpAssets.MaterialNames names = McpAssets.Names(level);
                info["used_by_materials_count"] = users.Count;
                info["used_by_materials"] = new JArray(users.Take(40).Select(o => names.Ref(o)));
                if (users.Count > 40) call.Note("find_asset_users {texture} lists every material using it, and the models and entities drawing them.");

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
            bool sourceIsCube = DdsIsCube(path);
            McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                if (replaceName != null)
                {
                    Textures.TEX4 slot = McpAssets.FindTexture(level, replaceName);
                    name = slot.Name;
                    //A cubemap slot fed a flat image (or the reverse) changes kind: what samples it (EnvironmentMaps, a material) breaks
                    bool slotIsCube = slot.StateFlags.HasFlag(Textures.TextureStateFlag.CUBE);
                    if (slotIsCube != sourceIsCube && !call.Bool("force"))
                        throw McpError.Invalid(slot.Name + " is " + (slotIsCube ? "a cubemap (an environment map)" : "a flat texture") + " but " + Path.GetFileName(path) + " is " + (sourceIsCube ? "a cubemap DDS" : "a flat image") + ": " + (slotIsCube ? "give a cubemap DDS (six faces)" : "give a flat image") + ", or pass force: true to change its kind.");
                    if (slot.StateFlags.HasFlag(Textures.TextureStateFlag.VOLUME) && !string.Equals(Path.GetExtension(path), ".dds", StringComparison.OrdinalIgnoreCase) && !call.Bool("force"))
                        throw McpError.Invalid(slot.Name + " is a volume (3D) texture; a " + Path.GetExtension(path) + " image is flat. Give a volume DDS, or pass force: true.");
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

            bool transparent = ImageHasTransparency(path);
            return McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel().Level;
                McpEditor.RequireUndoIdle();
                Textures.TEX4 texture;
                bool replacing = replaceName != null;
                (Textures.TextureStateFlag state, Textures.TextureUsageFlag usage, Textures.TextureFormat format, Textures.TEX4.Texture streamed, Textures.TEX4.Texture persistent) before = default((Textures.TextureStateFlag, Textures.TextureUsageFlag, Textures.TextureFormat, Textures.TEX4.Texture, Textures.TEX4.Texture));
                if (replacing)
                {
                    texture = McpAssets.FindTexture(level, replaceName);
                    before = (texture.StateFlags, texture.UsageFlags, texture.Format, texture.TextureStreamed, texture.TexturePersistent);
                }
                else
                {
                    if (AssetName.Exists(name, level.Textures.Entries.Select(o => o.Name)))
                        throw new McpError(McpErrorCodes.Conflict, "The level already has a texture called '" + name + "'.");
                    texture = new Textures.TEX4 { Name = name };
                }

                Textures.TEX4.Texture part = converted.ToTEX4Part(out Textures.TextureFormat readFormat, out Textures.TextureStateFlag state, out Textures.TextureUsageFlag usage);
                if (part == null)
                    throw new McpError(McpErrorCodes.Failed, "The converted image could not be read back as a texture.");
                const Textures.TextureStateFlag Kind = Textures.TextureStateFlag.CUBE | Textures.TextureStateFlag.VOLUME;
                if (replacing)
                {
                    //A replace keeps the slot's flags: its usage flags say which pack it belongs to and what the engine does with
                    //it, and its state flags how it is sampled (sRGB, cubemap, volume, alpha) - a forced change of kind takes the new one's
                    texture.StateFlags = call.Bool("force") ? (before.state & ~Kind) | (state & Kind) : before.state;
                    texture.UsageFlags = before.usage;
                }
                else
                {
                    texture.StateFlags = state;
                    texture.UsageFlags = usage;
                    //Colour maps are sRGB; normal maps (DXN/BC5) and single-channel masks are not, as the material generator sets them
                    if (format == Textures.TextureFormat.DXN || format.ToString() == "A8" || format.ToString() == "L8")
                        texture.StateFlags &= ~Textures.TextureStateFlag.ALLOW_SRGB;
                    if (transparent) texture.StateFlags |= Textures.TextureStateFlag.NON_SOLID;
                }
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

                //One undo step: the slot's old image and flags back, or the new texture out again
                Textures.TEX4 made = texture;
                if (replacing)
                {
                    var after = (made.StateFlags, made.UsageFlags, made.Format, made.TextureStreamed, made.TexturePersistent);
                    var was = before;
                    McpAssetEdit.Record("Replace texture " + made.Name,
                        () => { made.StateFlags = after.Item1; made.UsageFlags = after.Item2; made.Format = after.Item3; made.TextureStreamed = after.Item4; made.TexturePersistent = after.Item5; },
                        () => { made.StateFlags = was.state; made.UsageFlags = was.usage; made.Format = was.format; made.TextureStreamed = was.streamed; made.TexturePersistent = was.persistent; });
                }
                else
                {
                    (Action apply, Action revert) listed = McpAssetEdit.ListChange(level.Textures.Entries, made, level.Textures.Entries.Count - 1, true);
                    McpAssetEdit.Record("Import texture " + made.Name, listed.apply, () =>
                    {
                        //A material bound to it since (the Material Editor keeps no step) would sample nothing
                        McpAssets.RefuseRemovalWhileUsed("Texture " + made.Name, McpAssets.UsersOf(level, made));
                        listed.revert();
                    });
                }

                JObject result = new JObject()
                {
                    ["texture"] = texture.Name,
                    [replacing ? "replaced" : "imported"] = true,
                    ["format"] = texture.Format.ToString(),
                    ["state_flags"] = new JArray(McpAssets.FlagNames(texture.StateFlags)),
                    ["streamed"] = DescribePart(texture.TextureStreamed),
                    ["persistent"] = DescribePart(texture.TexturePersistent),
                    ["undo"] = "One undo step.",
                };
                if (replacing && before.state != texture.StateFlags)
                    result["state_flags_before"] = new JArray(McpAssets.FlagNames(before.state));
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

        /// <summary>Whether an image has see-through pixels (for a new texture's NON_SOLID flag); false when it cannot tell (DDS, TGA, HDR).</summary>
        private static bool ImageHasTransparency(string path)
        {
            string extension = (Path.GetExtension(path) ?? "").ToLowerInvariant();
            if (extension != ".png" && extension != ".bmp" && extension != ".tif" && extension != ".tiff") return false;
            try
            {
                using (Bitmap source = new Bitmap(path))
                {
                    if (!Image.IsAlphaPixelFormat(source.PixelFormat)) return false;
                    //Sampled on a grid: enough to tell a cut-out or a glass from an opaque image
                    int stepX = Math.Max(1, source.Width / 256), stepY = Math.Max(1, source.Height / 256);
                    for (int y = 0; y < source.Height; y += stepY)
                        for (int x = 0; x < source.Width; x += stepX)
                            if (source.GetPixel(x, y).A < 250) return true;
                    return false;
                }
            }
            catch
            {
                return false;
            }
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
                    Textures.TEX4 texture = McpAssets.FindTexture(level, call.Str("texture"), true, out bool global);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    WriteTexture(texture, path);
                    JObject one = new JObject() { ["texture"] = texture.Name, ["written"] = path };
                    if (global) one["global"] = true;
                    return one;
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
                    Textures.TextureStateFlag stateBefore = texture.StateFlags;
                    Textures.TextureUsageFlag usageBefore = texture.UsageFlags;
                    texture.StateFlags = (texture.StateFlags | stateOn) & ~stateOff;
                    texture.UsageFlags = (texture.UsageFlags | usageOn) & ~usageOff;
                    Textures.TextureStateFlag stateAfter = texture.StateFlags;
                    Textures.TextureUsageFlag usageAfter = texture.UsageFlags;
                    Singleton.OnResourceModified?.Invoke();
                    McpAssetEdit.Record("Set flags of texture " + texture.Name, () => { texture.StateFlags = stateAfter; texture.UsageFlags = usageAfter; }, () => { texture.StateFlags = stateBefore; texture.UsageFlags = usageBefore; });
                    result["state_flags"] = new JArray(McpAssets.FlagNames(texture.StateFlags));
                    result["usage_flags"] = new JArray(McpAssets.FlagNames(texture.UsageFlags));
                    return result;
                }
                if (action == "delete")
                {
                    List<Materials.Material> users = McpAssets.MaterialsUsing(level, texture);
                    if (users.Count != 0 && !call.Bool("force"))
                        throw new McpError(McpErrorCodes.Refused, users.Count + " materials use " + texture.Name + ": " + string.Join(", ", users.Take(10).Select(o => McpAssets.MaterialRef(level, o))) + (users.Count > 10 ? " ..." : "") + ". Rebind them (edit_material) first, or pass force: true to empty those samplers.");
                    //What undo puts back: the materials as they were (shader entries too), and the texture at its place
                    List<McpMaterialState> materialsBefore = users.Select(McpMaterialState.Of).ToList();
                    List<Shaders.Shader> poolBefore = new List<Shaders.Shader>(level.Shaders.Entries);
                    int textureIndex = level.Textures.Entries.FindIndex(o => ReferenceEquals(o, texture));
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
                    List<McpMaterialState> materialsAfter = users.Select(McpMaterialState.Of).ToList();
                    HashSet<Shaders.Shader> known = new HashSet<Shaders.Shader>(poolBefore, McpAssets.ByReference<Shaders.Shader>.Instance);
                    List<Shaders.Shader> added = level.Shaders.Entries.Where(o => o != null && !known.Contains(o)).ToList();
                    (Action apply, Action revert) listed = McpAssetEdit.ListChange(level.Textures.Entries, texture, textureIndex, false);
                    McpAssetEdit.Record("Delete texture " + texture.Name, () =>
                    {
                        foreach (Shaders.Shader shader in added)
                            if (!level.Shaders.Entries.Any(o => ReferenceEquals(o, shader))) level.Shaders.Entries.Add(shader);
                        foreach (McpMaterialState state in materialsAfter) state.Restore();
                        listed.apply();
                    }, () =>
                    {
                        listed.revert();
                        foreach (McpMaterialState state in materialsBefore) state.Restore();
                        foreach (Shaders.Shader shader in added)
                            if (!level.Materials.Entries.Any(o => o != null && ReferenceEquals(o.Shader, shader)))
                                level.Shaders.Entries.RemoveAll(o => ReferenceEquals(o, shader));
                    });
                    result["deleted"] = true;
                    result["undo"] = "One undo step (it comes back with the materials' samplers).";
                    if (users.Count != 0)
                        result["materials_emptied"] = new JArray(users.Take(50).Select(o => McpAssets.MaterialRef(level, o)));
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

        #region Who uses what
        private static object FindAssetUsers(McpCall call)
        {
            int asked = (call.Has("texture") ? 1 : 0) + (call.Has("material") ? 1 : 0) + (call.Has("model") ? 1 : 0);
            if (asked != 1) throw McpError.Invalid("Give one of 'texture', 'material' or 'model'.");
            int perEntity = call.Int("positions", 3);
            if (perEntity < 0 || perEntity > 20) throw McpError.Invalid("'positions' is 0-20.");
            using (McpEditorTools.Heartbeat(call, "Following the asset to its users"))
                return McpEditor.UI(() =>
                {
                    Level level = McpEditor.RequireLevel(forEditing: false).Level;
                    Commands commands = level.Commands;
                    McpAssets.MaterialNames names = McpAssets.Names(level);
                    JObject result = new JObject();
                    HashSet<Materials.Material> materials = new HashSet<Materials.Material>(McpAssets.ByReference<Materials.Material>.Instance);
                    HashSet<Models.CS2.Component.LOD.Submesh> submeshes = null;
                    if (call.Has("texture"))
                    {
                        Textures.TEX4 texture = McpAssets.FindTexture(level, call.Str("texture"), true, out bool global);
                        foreach (Materials.Material material in McpAssets.MaterialsUsing(level, texture)) materials.Add(material);
                        result["asset"] = new JObject() { ["texture"] = texture.Name, ["global"] = global };
                        result["materials_count"] = materials.Count;
                        result["materials"] = new JArray(materials.Take(60).Select(o => names.Ref(o)));
                    }
                    else if (call.Has("material"))
                    {
                        Materials.Material material = McpAssets.FindMaterial(level, call.Str("material"));
                        materials.Add(material);
                        result["asset"] = new JObject() { ["material"] = names.Ref(material) };
                    }
                    else
                    {
                        Models.CS2 model = McpAssets.FindModel(level, call.Str("model"));
                        submeshes = McpAssets.SubmeshesOf(model);
                        result["asset"] = new JObject() { ["model"] = model.Name };
                    }

                    //What draws it: renderable runs (the material each element is drawn with, not just the submesh's default)
                    Dictionary<(Composite, FunctionEntity), HashSet<string>> drawing = new Dictionary<(Composite, FunctionEntity), HashSet<string>>();
                    HashSet<string> models = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    Dictionary<Models.CS2.Component.LOD.Submesh, string> modelOf = new Dictionary<Models.CS2.Component.LOD.Submesh, string>(McpAssets.ByReference<Models.CS2.Component.LOD.Submesh>.Instance);
                    string ModelName(Models.CS2.Component.LOD.Submesh submesh)
                    {
                        if (submesh == null) return "?";
                        if (!modelOf.TryGetValue(submesh, out string name)) modelOf[submesh] = name = McpAssets.SubmeshName(submesh, level.Models);
                        return name;
                    }
                    foreach (Composite composite in commands.Entries)
                    {
                        if (composite?.functions == null) continue;
                        foreach (FunctionEntity entity in composite.functions)
                        {
                            IEnumerable<ResourceReference> references = (entity.GetParameter(ShortGuids.resource)?.content as cResource)?.value ?? Enumerable.Empty<ResourceReference>();
                            if (entity.resources != null) references = references.Concat(entity.resources);
                            foreach (ResourceReference reference in references)
                            {
                                if (reference?.resource_type != ResourceType.RENDERABLE_INSTANCE || reference.RenderableInstance == null) continue;
                                foreach (RenderableElements.Element element in Flatten(reference.RenderableInstance))
                                {
                                    bool hit = submeshes != null ? element.Model != null && submeshes.Contains(element.Model) : element.Material != null && materials.Contains(element.Material);
                                    if (!hit) continue;
                                    if (!drawing.TryGetValue((composite, entity), out HashSet<string> drawn)) drawing[(composite, entity)] = drawn = new HashSet<string>();
                                    string modelName = ModelName(element.Model);
                                    models.Add(modelName);
                                    drawn.Add(submeshes != null ? names.Ref(element.Material) : modelName + " [" + names.Ref(element.Material) + "]");
                                }
                            }
                        }
                    }
                    if (submeshes == null)
                    {
                        foreach (Models.CS2 model in level.Models.Entries.Where(o => o != null))
                            if (model.Components.Any(c => c.LODs.Any(l => l.Submeshes.Any(s => s?.Material != null && materials.Contains(s.Material)))))
                                models.Add(model.Name);
                        result["models_count"] = models.Count;
                        result["models"] = new JArray(models.OrderBy(o => o).Take(60));

                        //Swapped in or out by mapping sets and 'material' overrides, which name materials by their stored names
                        HashSet<string> stored = new HashSet<string>(materials.Select(o => o.Name ?? ""), StringComparer.OrdinalIgnoreCase);
                        JArray sets = new JArray();
                        foreach (MaterialMappings.MaterialMapping set in level.MaterialMappings?.Entries ?? new List<MaterialMappings.MaterialMapping>())
                        {
                            List<MaterialMappings.MaterialMapping.Mapping> pairs = set?.Mappings?.Where(p => stored.Contains(p.from ?? "") || stored.Contains(p.to ?? "")).ToList();
                            if (pairs == null || pairs.Count == 0) continue;
                            if (sets.Count < 40) sets.Add(new JObject() { ["set"] = set.Name, ["pairs"] = new JArray(pairs.Take(10).Select(p => p.from + " -> " + p.to)) });
                        }
                        if (sets.Count != 0) result["mapping_sets"] = sets;
                        JArray overrides = new JArray();
                        ShortGuid materialParameter = ShortGuidUtils.Generate("material");
                        foreach (Composite composite in commands.Entries)
                        {
                            if (composite == null) continue;
                            foreach (Entity entity in composite.GetEntities())
                            {
                                string value = (entity.GetParameter(materialParameter)?.content as cString)?.value;
                                if (string.IsNullOrEmpty(value) || !stored.Contains(value)) continue;
                                if (overrides.Count < 40) overrides.Add(new JObject() { ["composite"] = composite.name, ["id"] = McpScript.Id(entity.shortGUID), ["name"] = McpScript.EntityName(commands, composite, entity), ["kind"] = McpScript.Kind(entity) });
                            }
                        }
                        if (overrides.Count != 0) result["material_overrides"] = overrides;
                    }

                    //Where the entities are: one walk of the level (or of 'within') collecting their placements
                    Dictionary<(Composite, FunctionEntity), (int count, List<Vector3Json> positions)> placed = null;
                    McpRegion within = call.Has("within") ? McpRegion.Resolve(call, commands, level, call.Token("within"), "within") : null;
                    if (within != null || perEntity > 0)
                    {
                        placed = new Dictionary<(Composite, FunctionEntity), (int, List<Vector3Json>)>();
                        HashSet<Composite> holders = new HashSet<Composite>(drawing.Keys.Select(o => o.Item1));
                        McpPlacements walker = McpRegion.Walker(commands);
                        HashSet<Composite> reach = new HashSet<Composite>();
                        foreach (Composite holder in holders) reach.UnionWith(walker.Reaching(holder));
                        bool Visit(McpPlacements.Step step)
                        {
                            if (!(step.Entity is FunctionEntity function) || !holders.Contains(step.Composite) || !drawing.ContainsKey((step.Composite, function))) return true;
                            if (!step.Real || step.World == null) return true;
                            if (within != null && !within.InClip(step.World.position)) return true;
                            placed.TryGetValue((step.Composite, function), out (int count, List<Vector3Json> positions) known);
                            List<Vector3Json> positions = known.positions ?? new List<Vector3Json>();
                            if (positions.Count < perEntity) positions.Add(new Vector3Json(step.World.position));
                            placed[(step.Composite, function)] = (known.count + 1, positions);
                            return true;
                        }
                        if (within == null || within.Roots.Count == 0)
                            walker.Walk(commands.EntryPoints[0], Visit, o => reach.Contains(o), call.Cancel);
                        else
                            foreach (List<Entity> root in within.Roots)
                                walker.WalkUnder(commands.EntryPoints[0], root, Visit, o => reach.Contains(o), call.Cancel);
                        if (walker.Truncated) call.Note("The level is too big to walk whole: some placements were not counted (use 'within').");
                        foreach (string note in within?.Notes ?? new List<string>()) call.Note(note);
                    }

                    List<KeyValuePair<(Composite, FunctionEntity), HashSet<string>>> users = drawing
                        .Where(o => within == null || (placed.TryGetValue(o.Key, out var there) && there.count != 0))
                        .OrderBy(o => o.Key.Item1.name, StringComparer.OrdinalIgnoreCase).ThenBy(o => McpScript.Id(o.Key.Item2.shortGUID)).ToList();
                    if (within != null) result["within"] = within.Label;
                    result["space"] = "world";
                    McpPaging.Page(call, users, result, "entities", o =>
                    {
                        JObject item = new JObject()
                        {
                            ["composite"] = o.Key.Item1.name,
                            ["id"] = McpScript.Id(o.Key.Item2.shortGUID),
                            ["name"] = McpScript.EntityName(commands, o.Key.Item1, o.Key.Item2),
                            ["composite_placed"] = McpAssets.PlacementCount(commands, o.Key.Item1),
                            ["draws"] = new JArray(o.Value.Take(8)),
                        };
                        if (placed != null && placed.TryGetValue(o.Key, out var where))
                        {
                            item[within != null ? "placed_here" : "placed_in_level"] = where.count;
                            if (where.positions != null && where.positions.Count != 0) item["positions"] = new JArray(where.positions.Select(p => p.Json));
                        }
                        return item;
                    }, 50);
                    if (drawing.Count == 0)
                        call.Note("No entity draws it." + (submeshes == null && materials.Count == 0 ? " No material samples that texture." : ""));
                    return result;
                });
        }

        /// <summary>A position kept for a result.</summary>
        private struct Vector3Json
        {
            private readonly System.Numerics.Vector3 _value;
            public Vector3Json(System.Numerics.Vector3 value) { _value = value; }
            public JArray Json => McpCollision.V(_value);
        }

        /// <summary>A run's elements and the lower LODs hung off them.</summary>
        private static IEnumerable<RenderableElements.Element> Flatten(IEnumerable<RenderableElements.Element> run, int depth = 0)
        {
            if (run == null || depth > 4) yield break;
            foreach (RenderableElements.Element element in run)
            {
                if (element == null) continue;
                yield return element;
                foreach (RenderableElements.Element lower in Flatten(element.LODs, depth + 1))
                    yield return lower;
            }
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
                throw McpError.Ambiguous("models", reference, named.Select(o => new JObject() { ["name"] = o.Name }), "Give the full name.");
            throw McpError.NotFound("model", reference, level.Models.Entries.Where(o => o != null).Select(o => o.Name), "list_models (filter) searches them; find_asset_users shows where one is used.");
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
                throw McpError.Ambiguous("materials", name, matches.Select(o => new JObject() { ["id"] = (level.Materials.GetMaterialName(entries[o]) ?? name) + "#" + o, ["name"] = level.Materials.GetMaterialName(entries[o]), ["type"] = entries[o].Shader?.Ubershader.ToString() }), "Give one as name#index (describe_material shows each).");
            MaterialNames names = Names(level);
            throw McpError.NotFound("material", name, entries.Where(o => o != null).Select(o => names.Display(o)), "list_materials (filter) searches them.");
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
                throw McpError.Ambiguous("textures", reference, named.Select(o => new JObject() { ["name"] = o.Name }), "Give the full name.");
            bool global = level.Global?.Textures?.Entries?.Any(o => o != null && (string.Equals((o.Name ?? "").Replace('/', '\\'), name, StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetFileName((o.Name ?? "").Replace('/', '\\')), name, StringComparison.OrdinalIgnoreCase))) == true;
            throw McpError.NotFound("texture in the open level", reference, level.Textures.Entries.Where(o => o != null).Select(o => o.Name),
                global ? "It is one of GLOBAL's textures (shared by every level): describe_texture and export_textures read those, and edit_material can bind one, but it cannot be replaced or edited here." : "list_textures (filter) searches them.");
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

        /// <summary>
        /// Refuse while this install's game is running, for tools that write the game's shared files (it holds them open); another
        /// install's game has its own. Close it with EditorUtils.CloseAI(null, thisInstallOnly: true).
        /// </summary>
        public static bool GameIsRunning()
        {
            try { return EditorUtils.ThisInstallsGameRunning(); }
            catch { return false; }
        }

        #region Caches of the open level
        private static bool _hooked;

        /// <summary>Let go of the caches below when a level closes (hooked once). UI thread.</summary>
        private static void Hook()
        {
            McpCollision.Hook();
            if (_hooked) return;
            _hooked = true;
            Singleton.OnLevelClosing += _ => DropCaches();
            Singleton.OnLevelLoaded += _ => DropCaches();
        }

        /// <summary>Forget the cached material names and placement counts: each holds the whole level it was built for.</summary>
        public static void DropCaches()
        {
            _names = null;
            _placementCounts = null;
            _placementCountsFor = null;
            _placementCountsVersion = -1;
        }

        /// <summary>
        /// Whether this is the open level. Only its caches are kept: another level's (a tool reading a level that is not open)
        /// would hold it after it is released, since nothing closes it.
        /// </summary>
        private static bool IsOpen(Level level) => level != null && ReferenceEquals(Singleton.Editor?.CompositeBrowser?.Content?.Level, level);
        private static bool IsOpen(Commands commands) => commands != null && ReferenceEquals(Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands, commands);
        #endregion

        #region Naming materials so a result can be given back
        private static MaterialNames _names;

        /// <summary>
        /// How results name the open level's materials, so any name a tool gives is accepted back by <see cref="FindMaterial"/>.
        /// Built once and kept until a resource or the script changes, or the level closes. UI thread.
        /// </summary>
        public static MaterialNames Names(Level level)
        {
            Hook();
            MaterialNames names = _names;
            if (names != null && ReferenceEquals(names.Level, level) && names.Version == McpCollision.Version && names.Count == level.Materials.Entries.Count)
                return names;
            names = new MaterialNames(level);
            if (IsOpen(level)) _names = names;
            return names;
        }

        /// <summary>A material as results name it (see <see cref="MaterialNames.Ref"/>).</summary>
        public static string MaterialRef(Level level, Materials.Material material) => material == null ? null : Names(level).Ref(material);

        /// <summary>
        /// The names of a level's materials. A material is shown by its display name (the stored name, or the name a hashed
        /// stored name stands for); when another material shows the same name it is "name#index", the form every material
        /// argument takes. Repeats are counted over the whole level, never a filtered list.
        /// </summary>
        public sealed class MaterialNames
        {
            public readonly Level Level;
            public readonly int Version, Count;
            private readonly Dictionary<Materials.Material, int> _index = new Dictionary<Materials.Material, int>(ByReference<Materials.Material>.Instance);
            private readonly Dictionary<Materials.Material, string> _display = new Dictionary<Materials.Material, string>(ByReference<Materials.Material>.Instance);
            private readonly Dictionary<string, int> _shown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, List<Materials.Material>> _stored = new Dictionary<string, List<Materials.Material>>(StringComparer.OrdinalIgnoreCase);

            public MaterialNames(Level level)
            {
                Level = level;
                Version = McpCollision.Version;
                List<Materials.Material> entries = level.Materials.Entries;
                Count = entries.Count;
                for (int i = 0; i < entries.Count; i++)
                {
                    Materials.Material material = entries[i];
                    if (material == null || _index.ContainsKey(material)) continue;
                    _index[material] = i;
                    string display = level.Materials.GetMaterialName(material) ?? material.Name ?? "";
                    _display[material] = display;
                    _shown.TryGetValue(display, out int shown);
                    _shown[display] = shown + 1;
                    string stored = material.Name ?? "";
                    if (!_stored.TryGetValue(stored, out List<Materials.Material> list))
                        _stored[stored] = list = new List<Materials.Material>();
                    list.Add(material);
                }
            }

            /// <summary>Its index in the level's material list (-1 when it is not in it).</summary>
            public int IndexOf(Materials.Material material) => material != null && _index.TryGetValue(material, out int index) ? index : -1;

            /// <summary>The name the editor shows.</summary>
            public string Display(Materials.Material material) => material == null ? null : _display.TryGetValue(material, out string display) ? display : material.Name;

            /// <summary>Whether more than one material shows this name.</summary>
            public bool Repeated(string display) => display != null && _shown.TryGetValue(display, out int count) && count > 1;

            /// <summary>The name to give back: the display name, as "name#index" when another material shows the same name.</summary>
            public string Ref(Materials.Material material)
            {
                if (material == null) return null;
                string display = Display(material);
                int index = IndexOf(material);
                return Repeated(display) && index >= 0 ? display + "#" + index : display;
            }

            /// <summary>The materials stored under a name (several when it repeats).</summary>
            public List<Materials.Material> ByStoredName(string stored) => stored != null && _stored.TryGetValue(stored, out List<Materials.Material> list) ? list : new List<Materials.Material>();

            /// <summary>
            /// A stored name written as a value (a mapping pair, a ModelReference's 'material'): the value itself, plus the name
            /// to give a material tool when that differs (a hashed stored name, or one several materials share).
            /// </summary>
            public JToken StoredValue(string stored)
            {
                List<Materials.Material> matches = ByStoredName(stored);
                if (matches.Count == 0) return stored;
                string shown = Ref(matches[0]);
                if (matches.Count == 1 && string.Equals(shown, stored, StringComparison.OrdinalIgnoreCase)) return stored;
                JObject value = new JObject() { ["value"] = stored, ["material"] = shown };
                if (matches.Count > 1) value["materials_with_this_name"] = matches.Count;
                return value;
            }
        }
        #endregion

        #region How often composites are placed
        private static Dictionary<Composite, int> _placementCounts;
        private static Commands _placementCountsFor;
        private static int _placementCountsVersion = -1;

        /// <summary>
        /// How many times a composite is placed under the level's root: every path of instances down to it, multiplied out
        /// (templates and deleted placements included). The root is placed once. Cached for the open level until the script
        /// changes or the level closes; another level's are counted afresh each time. UI thread.
        /// </summary>
        public static int PlacementCount(Commands commands, Composite composite)
        {
            Hook();
            Dictionary<Composite, int> counts = _placementCounts;
            if (!ReferenceEquals(_placementCountsFor, commands) || _placementCountsVersion != McpCollision.Version || counts == null)
            {
                counts = BuildPlacementCounts(commands);
                if (IsOpen(commands))
                {
                    _placementCounts = counts;
                    _placementCountsFor = commands;
                    _placementCountsVersion = McpCollision.Version;
                }
            }
            return composite != null && counts.TryGetValue(composite, out int count) ? count : 0;
        }

        private static Dictionary<Composite, int> BuildPlacementCounts(Commands commands)
        {
            //Parents with how many instances of the child each holds, then counts multiplied down from the root
            Dictionary<ShortGuid, Dictionary<Composite, int>> parents = new Dictionary<ShortGuid, Dictionary<Composite, int>>();
            foreach (Composite composite in commands.Entries)
            {
                if (composite == null) continue;
                foreach (FunctionEntity function in composite.functions)
                {
                    if (function.function.IsFunctionType) continue;
                    if (!parents.TryGetValue(function.function, out Dictionary<Composite, int> holders))
                        parents[function.function] = holders = new Dictionary<Composite, int>();
                    holders.TryGetValue(composite, out int n);
                    holders[composite] = n + 1;
                }
            }
            Dictionary<Composite, int> counts = new Dictionary<Composite, int>();
            Composite root = commands.EntryPoints?.FirstOrDefault();
            HashSet<Composite> stack = new HashSet<Composite>();
            int Count(Composite composite)
            {
                if (counts.TryGetValue(composite, out int known)) return known;
                if (composite == root) { counts[composite] = 1; return 1; }
                if (!stack.Add(composite)) return 0;
                long total = 0;
                if (parents.TryGetValue(composite.shortGUID, out Dictionary<Composite, int> holders))
                    foreach (KeyValuePair<Composite, int> holder in holders)
                        total += (long)Count(holder.Key) * holder.Value;
                stack.Remove(composite);
                int clamped = (int)Math.Min(int.MaxValue, total);
                counts[composite] = clamped;
                return clamped;
            }
            foreach (Composite composite in commands.Entries)
                if (composite != null) Count(composite);
            return counts;
        }

        /// <summary>
        /// The note an edit inside a shared composite deserves: every placement of it changes. Null when it is placed once
        /// (or not at all). <paramref name="instead"/> says what to use for one placement.
        /// </summary>
        public static string SharedEditNote(Commands commands, Composite composite, string instead)
        {
            int placements = PlacementCount(commands, composite);
            if (placements <= 1) return null;
            return composite.name + " is placed " + placements + " times in the level, so this changed every one of them." + (instead == null ? "" : " " + instead);
        }
        #endregion

        #region What still uses an asset
        /// <summary>
        /// Undoing the step that added an asset takes it out of the level's list. Changes made since that keep no undo step of
        /// their own (the editor's asset editors, edit_model's geometry changes) can have pointed something at it, which would
        /// then point at nothing (saved as index -1). So the undo refuses while anything uses it: the history is cleared with
        /// a message (see UndoStack), and nothing has changed. UI thread.
        /// </summary>
        public static void RefuseRemovalWhileUsed(string asset, string users)
        {
            if (users != null)
                throw new InvalidOperationException(asset + " is not taken out, since " + users + " still use it.");
        }

        /// <summary>What uses a material (submeshes drawing with it, entities' renderables and collision rows), or null.</summary>
        public static string UsersOf(Level level, Materials.Material material)
        {
            if (material == null) return null;
            Users users = new Users();
            foreach (Models.CS2 model in level.Models?.Entries ?? new List<Models.CS2>())
            {
                if (model?.Components == null) continue;
                if (model.Components.Any(c => c?.LODs != null && c.LODs.Any(l => l?.Submeshes != null && l.Submeshes.Any(s => s != null && ReferenceEquals(s.Material, material)))))
                    users.Add("model " + Leaf(model.Name));
            }
            foreach ((Composite composite, FunctionEntity entity, ResourceReference reference) in ResourcesOf(level))
            {
                bool uses = reference.resource_type == ResourceType.RENDERABLE_INSTANCE ? AnyElement(reference.RenderableInstance, o => ReferenceEquals(o.Material, material), 0)
                    : reference.resource_type == ResourceType.COLLISION_MAPPING && ReferenceEquals(reference.CollisionMapping?.Material, material);
                if (uses) users.Add(McpScript.EntityName(level.Commands, composite, entity) + " in " + composite.name);
            }
            return users.Describe();
        }

        /// <summary>What uses a texture (materials sampling it), or null.</summary>
        public static string UsersOf(Level level, Textures.TEX4 texture)
        {
            if (texture == null) return null;
            Users users = new Users();
            foreach (Materials.Material material in MaterialsUsing(level, texture))
                users.Add("material " + (level.Materials.GetMaterialName(material) ?? material.Name));
            return users.Describe();
        }

        /// <summary>What draws any of a model's submeshes (entities' renderables), or null.</summary>
        public static string UsersOf(Level level, Models.CS2 model)
        {
            if (model == null) return null;
            Users users = new Users();
            foreach (Placement placement in PlacedBy(level, SubmeshesOf(model)))
                users.Add(McpScript.EntityName(level.Commands, placement.Composite, placement.Entity) + " in " + placement.Composite.name);
            return users.Describe();
        }

        /// <summary>Every resource reference the level's entities hold: their own lists and their 'resource' parameters.</summary>
        private static IEnumerable<(Composite, FunctionEntity, ResourceReference)> ResourcesOf(Level level)
        {
            if (level?.Commands?.Entries == null) yield break;
            foreach (Composite composite in level.Commands.Entries)
            {
                if (composite?.functions == null) continue;
                foreach (FunctionEntity entity in composite.functions)
                {
                    if (entity.resources != null)
                        foreach (ResourceReference reference in entity.resources)
                            if (reference != null) yield return (composite, entity, reference);
                    if (entity.GetParameter(ShortGuids.resource)?.content is cResource resource && resource.value != null)
                        foreach (ResourceReference reference in resource.value)
                            if (reference != null) yield return (composite, entity, reference);
                }
            }
        }

        private static bool AnyElement(List<RenderableElements.Element> elements, Func<RenderableElements.Element, bool> match, int depth)
        {
            if (elements == null || depth > 4) return false;
            foreach (RenderableElements.Element element in elements)
                if (element != null && (match(element) || AnyElement(element.LODs, match, depth + 1))) return true;
            return false;
        }

        /// <summary>The first few users by name, and how many there are.</summary>
        private sealed class Users
        {
            private readonly List<string> _named = new List<string>();
            private int _count;

            public void Add(string user)
            {
                _count++;
                if (_named.Count < 3 && !_named.Contains(user)) _named.Add(user);
            }

            public string Describe() => _count == 0 ? null : string.Join(", ", _named) + (_count > _named.Count ? " (" + _count + " in all)" : "");
        }
        #endregion

        #region Model extents
        /// <summary>The box round a model's first LOD (every component), in its own space; false when it has no geometry.</summary>
        public static bool ModelBounds(Models.CS2 model, out System.Numerics.Vector3 min, out System.Numerics.Vector3 max, int component = -1)
        {
            min = new System.Numerics.Vector3(float.MaxValue);
            max = new System.Numerics.Vector3(float.MinValue);
            for (int c = 0; c < model.Components.Count; c++)
            {
                if (component >= 0 && c != component) continue;
                Models.CS2.Component.LOD lod = model.Components[c].LODs.FirstOrDefault();
                if (lod == null) continue;
                foreach (Models.CS2.Component.LOD.Submesh submesh in lod.Submeshes)
                {
                    if (submesh == null) continue;
                    min = System.Numerics.Vector3.Min(min, submesh.MinBounds);
                    max = System.Numerics.Vector3.Max(max, submesh.MaxBounds);
                }
            }
            return min.X <= max.X;
        }

        /// <summary>{min, max, centre, size} in metres: the one box shape every spatial tool gives (<see cref="McpCollision.Aabb"/>).</summary>
        public static JObject BoxJson(System.Numerics.Vector3 min, System.Numerics.Vector3 max) => McpCollision.Aabb(min, max);

        /// <summary>
        /// The collision proxy a component's first-LOD submeshes name (retail keeps it equal to its ModelReferences' mapping's;
        /// often only some submeshes carry it), or -1 when they name none or disagree.
        /// </summary>
        public static int CollisionProxyOf(Models.CS2.Component component)
        {
            List<int> named = component?.LODs.FirstOrDefault()?.Submeshes.Where(o => o != null && o.CollisionProxyIndex >= 0).Select(o => o.CollisionProxyIndex).Distinct().ToList();
            return named != null && named.Count == 1 ? named[0] : -1;
        }

        /// <summary>A collision proxy's extent in its own space, from its stored domain or its triangles; false when it has neither.</summary>
        public static bool ProxyBounds(Level level, int proxyIndex, out System.Numerics.Vector3 min, out System.Numerics.Vector3 max)
        {
            min = max = System.Numerics.Vector3.Zero;
            HavokPackfile hkx = level.Collision;
            if (hkx == null || !hkx.Loaded) return false;
            HavokPackfile.StaticCompoundShape proxy = hkx.GetCompound(proxyIndex);
            if (proxy == null) return false;
            if (proxy.DomainMax.X > proxy.DomainMin.X && proxy.DomainMax.Z > proxy.DomainMin.Z && !float.IsInfinity(proxy.DomainMin.X) && !float.IsInfinity(proxy.DomainMax.X))
            {
                min = new System.Numerics.Vector3(proxy.DomainMin.X, proxy.DomainMin.Y, proxy.DomainMin.Z);
                max = new System.Numerics.Vector3(proxy.DomainMax.X, proxy.DomainMax.Y, proxy.DomainMax.Z);
                return true;
            }
            HavokPackfile.PreviewMesh mesh = hkx.BuildPreviewMesh(proxy);
            if (mesh == null || mesh.Positions.Count == 0) return false;
            min = new System.Numerics.Vector3(float.MaxValue);
            max = new System.Numerics.Vector3(float.MinValue);
            foreach (System.Numerics.Vector3 p in mesh.Positions)
            {
                min = System.Numerics.Vector3.Min(min, p);
                max = System.Numerics.Vector3.Max(max, p);
            }
            return true;
        }
        #endregion

        #region Global textures
        /// <summary>
        /// A texture of the open level, or (when <paramref name="allowGlobal"/>) of ENV/GLOBAL, which every level shares and
        /// tools can only read and bind. <paramref name="global"/> says which it was.
        /// </summary>
        public static Textures.TEX4 FindTexture(Level level, string reference, bool allowGlobal, out bool global)
        {
            global = false;
            try
            {
                return FindTexture(level, reference);
            }
            catch (McpError e) when (allowGlobal && e.Code == McpErrorCodes.NotFound && level.Global?.Textures?.Entries != null)
            {
                string name = (reference ?? "").Trim().Replace('/', '\\');
                int build = name.IndexOf("content\\build\\textures\\", StringComparison.OrdinalIgnoreCase);
                if (build >= 0) name = name.Substring(build + "content\\build\\textures\\".Length);
                List<Textures.TEX4> shared = level.Global.Textures.Entries;
                Textures.TEX4 match = shared.FirstOrDefault(o => o != null && string.Equals((o.Name ?? "").Replace('/', '\\'), name, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                {
                    List<Textures.TEX4> named = shared.Where(o => o != null && string.Equals(Path.GetFileName((o.Name ?? "").Replace('/', '\\')), name, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (named.Count == 1) match = named[0];
                }
                if (match == null)
                    throw new McpError(McpErrorCodes.NotFound, "Neither the open level nor GLOBAL has a texture '" + reference + "'." + McpNames.DidYouMean(level.Textures.Entries.Concat(shared).Where(o => o != null).Select(o => o.Name), name) + " list_textures (include_global) lists them.");
                global = true;
                return match;
            }
        }
        #endregion
    }

    /// <summary>
    /// A change to the open level's assets - a texture, a material, a mapping set, a model - as one step on the undo history:
    /// the actions that put it back and do it again. The editor's own asset editors keep no history; these steps let undo take
    /// back what a tool did to them. Assets live as long as the level, so they are held directly.
    /// </summary>
    internal sealed class McpAssetEdit : IEdit
    {
        private readonly Action _apply, _revert;

        public string Label { get; }
        public ShortGuid CompositeId => ShortGuid.Invalid;
        public ShortGuid EntityId => ShortGuid.Invalid;

        /// <param name="apply">Does the change again (the change itself is made before the step is recorded).</param>
        /// <param name="revert">Puts things back as they were.</param>
        public McpAssetEdit(string label, Action apply, Action revert)
        {
            Label = label.StartsWith("AI: ", StringComparison.Ordinal) ? label : "AI: " + label;
            _apply = apply;
            _revert = revert;
        }

        public void Apply(UndoContext context) { _apply(); Changed(); }
        public void Revert(UndoContext context) { _revert(); Changed(); }
        public bool TryMerge(IEdit next) => false;

        private static void Changed()
        {
            DirtyTracker.MarkLevelDataModified();
            Singleton.OnResourceModified?.Invoke();
        }

        /// <summary>Record a change already made (UI thread). Nothing is recorded while undo is applying or blocked.</summary>
        public static void Record(string label, Action apply, Action revert)
        {
            UndoStack.Current?.Record(new McpAssetEdit(label, apply, revert));
        }

        /// <summary>An object put into (or taken out of) a level list at an index, as an undo pair.</summary>
        public static (Action apply, Action revert) ListChange<T>(List<T> list, T item, int index, bool present) where T : class
        {
            void Set(bool there)
            {
                int at = list.FindIndex(o => ReferenceEquals(o, item));
                if (there && at < 0) list.Insert(Math.Min(Math.Max(index, 0), list.Count), item);
                else if (!there && at >= 0) list.RemoveAt(at);
            }
            return (() => Set(present), () => Set(!present));
        }
    }

    /// <summary>Everything about a material a tool can change, to put it back exactly (its shader entry's sampler remaps included).</summary>
    internal sealed class McpMaterialState
    {
        public Materials.Material Material;
        public Shaders.Shader Shader;
        public List<int> Remaps;
        public List<TexturePtr> Textures;
        public List<float> Pixel, Vertex;
        public int Priority;
        public string Name;

        public static McpMaterialState Of(Materials.Material material) => new McpMaterialState()
        {
            Material = material,
            Shader = material.Shader,
            Remaps = material.Shader?.SamplerRemaps == null ? null : new List<int>(material.Shader.SamplerRemaps),
            Textures = (material.TextureReferences ?? new List<TexturePtr>()).Select(Copy).ToList(),
            Pixel = new List<float>(material.PixelShaderConstants ?? new List<float>()),
            Vertex = new List<float>(material.VertexShaderConstants ?? new List<float>()),
            Priority = material.Priority,
            Name = material.Name,
        };

        private static TexturePtr Copy(TexturePtr o) => o == null ? null : new TexturePtr() { Texture = o.Texture, Location = o.Location };

        public void Restore()
        {
            Material.Shader = Shader;
            if (Shader?.SamplerRemaps != null && Remaps != null)
            {
                Shader.SamplerRemaps.Clear();
                Shader.SamplerRemaps.AddRange(Remaps);
            }
            Material.TextureReferences = Textures.Select(Copy).ToList();
            Material.PixelShaderConstants = new List<float>(Pixel);
            Material.VertexShaderConstants = new List<float>(Vertex);
            Material.Priority = Priority;
            Material.Name = Name;
        }

        /// <summary>
        /// Record what an edit did to materials as one undo step: their states before and after, and the shader entries it
        /// added to the level's pool (taken out again on undo when nothing else uses them). UI thread.
        /// </summary>
        public static void Record(Level level, string label, List<McpMaterialState> before, List<Shaders.Shader> poolBefore)
        {
            List<McpMaterialState> after = before.Select(o => Of(o.Material)).ToList();
            HashSet<Shaders.Shader> known = new HashSet<Shaders.Shader>(poolBefore, McpAssets.ByReference<Shaders.Shader>.Instance);
            List<Shaders.Shader> added = level.Shaders.Entries.Where(o => o != null && !known.Contains(o)).ToList();
            McpAssetEdit.Record(label, () =>
            {
                foreach (Shaders.Shader shader in added)
                    if (!level.Shaders.Entries.Any(o => ReferenceEquals(o, shader))) level.Shaders.Entries.Add(shader);
                foreach (McpMaterialState state in after) state.Restore();
            }, () =>
            {
                foreach (McpMaterialState state in before) state.Restore();
                foreach (Shaders.Shader shader in added)
                    if (!level.Materials.Entries.Any(o => o != null && ReferenceEquals(o.Shader, shader)))
                        level.Shaders.Entries.RemoveAll(o => ReferenceEquals(o, shader));
            });
        }
    }
}
