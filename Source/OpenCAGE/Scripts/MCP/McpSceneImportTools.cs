using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.SceneImport;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Bulk scene import: import_scene brings a whole scene file - many meshes, many placements, lights - into the open level in one step.
    /// The planning is Scripts\SceneImport\ScenePlan, the writing SceneImporter; the File > Import Scene window runs this same tool.
    /// Registered from McpZoneTools.Tools() (toolset "assets").
    /// </summary>
    internal static class McpSceneImportTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "import_scene",
                Title = "Import a whole scene (many models, placements, lights)",
                Description = "Import a whole scene file (glTF / GLB, FBX, COLLADA, OBJ) into the open level in one step: every node that carries meshes becomes an instance, and nodes drawing the same meshes at the same scale share ONE model (a crate placed fifty times is one model and fifty instances; a file that repeats a mesh rather than instancing it is merged too). " +
                    "A CATHODE entity has no scale, so each placement's scale / mirror / shear goes into its model (a crate placed at 1.5x is a second model) and its rotation and position into the instance. " +
                    "Each model gets materials generated from the file's (once per file material, shared: glass that is see-through in the file draws in the see-through pass, doubleSided sheets draw from both sides), a composite (Scenes\\<name>\\Models\\<model>) and collision made to match it ('collision': solid = retail's WORLD + BALLISTIC pair, walked on and shot at; see-through parts left out). " +
                    "Nodes whose names match 'physics' (default *_dyn and *_physics) become physics props instead (make_physics_prop: spawned at load, knocked about). " +
                    "Placements are grouped by the file's hierarchy (a composite Scenes\\<name>\\<group> per node at its first level, placed in the root at 'position' / 'rotation'); lights (glTF KHR_lights_punctual, or the file's own) go in Scenes\\<name>\\Lights; a directional light (a sun - the game has none) is reported with the add_outdoor_lighting call that builds one, or run with directional 'outdoor_lighting'. " +
                    "dry_run lists everything and the budgets it runs into - the texture heap after the import, 255 submeshes per drawn part, 21,845 triangles per submesh (bigger meshes are cut), the lights the level would hold against the 2048 / 64 in-view crash limits - and checks every placement against the file (worst error in mm). " +
                    "One undo step takes the whole import out (the models, materials and textures it built stay in the level unused, as after import_model). Reaches the game after save_level build=true.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("path", "The scene file's full path.", required: true),
                    McpSchema.String("name", "The scene's name (default the file's): composites go in Scenes\\<name>\\, models and materials are named after it. One folder level, no backslashes."),
                    McpSchema.Boolean("dry_run", "Only plan and report: nothing changes."),
                    McpSchema.Number("scale", "Resize the whole scene (default 1). glTF is read in metres; FBX, COLLADA and OBJ in centimetres, as import_model reads them."),
                    McpSchema.Vector("position", "Where the scene's origin goes in the level (default [0, 0, 0])."),
                    McpSchema.Vector("rotation", "The scene's rotation in the level, degrees [pitch, yaw, roll] (default none)."),
                    McpSchema.String("group_by", "top (default): a composite per node at the file's first level; none: one composite for everything.", options: new[] { "top", "none" }),
                    McpSchema.Boolean("merge_identical", "Meshes with the same content count as one (default true)."),
                    McpSchema.String("collision", "solid (default: WORLD + BALLISTIC pair), world (blocks movement), ballistic (bullets, footsteps, zones) or none.", options: new[] { "solid", "world", "ballistic", "none" }),
                    McpSchema.Strings("physics", "Node names that become physics props (wildcards * and ?; default [\"*_dyn\", \"*_physics\"]; [] for none)."),
                    McpSchema.Strings("exclude", "Node names (wildcards) left out, with everything under them."),
                    McpSchema.Boolean("lights", "Import the file's lights (default true)."),
                    McpSchema.Number("light_scale", "Multiply every light's intensity (default 1; a light of 50 candela comes in at intensity 1)."),
                    McpSchema.String("shadows", "Which imported lights cast shadows: spots (default), all, none. The game draws at most 64 shadowed lights in a view.", options: new[] { "spots", "all", "none" }),
                    McpSchema.String("directional", "A directional light (a sun): note (default: reported with the add_outdoor_lighting call that builds one) or outdoor_lighting (run it after the import).", options: new[] { "note", "outdoor_lighting" }),
                    McpSchema.Boolean("compact_textures", "Encode colour maps as DXT1 / DXT5 instead of BC7: half the texture memory, for a big scene that would overflow the texture heap."),
                    McpSchema.Integer("limit", "At most this many models / lights listed in the result (default 200).")),
                Destructive = true,
                Toolset = "assets",
                Run = ImportScene,
            };
        }

        /// <summary>The options a call (or the window, through the same arguments) gives.</summary>
        internal static SceneOptions ReadOptions(McpCall call)
        {
            string path = McpAssets.AbsolutePath(call.Str("path", required: true), "path");
            if (!File.Exists(path)) throw new McpError("There is no file at " + path + ".");
            SceneOptions options = new SceneOptions() { Path = path };
            string name = call.Str("name");
            if (string.IsNullOrWhiteSpace(name)) name = Path.GetFileNameWithoutExtension(path);
            if (name.IndexOf('\\') >= 0 || name.IndexOf('/') >= 0) throw McpError.Invalid("'name' is one folder level: no slashes.");
            options.Name = ScenePlan.EntityName(name, "Scene");
            options.Scale = (float)call.Num("scale", 1.0);
            if (!(options.Scale > 0) || float.IsInfinity(options.Scale)) throw McpError.Invalid("'scale' has to be above 0.");
            if (call.Has("position")) options.Position = McpValues.ReadVector(call.Token("position"), "position", null);
            if (call.Has("rotation")) options.Rotation = McpValues.ReadVector(call.Token("rotation"), "rotation", null);
            options.GroupBy = (call.Str("group_by") ?? "top").Trim().ToLowerInvariant();
            if (options.GroupBy != "top" && options.GroupBy != "none") throw McpError.Invalid("'group_by' is top or none.");
            options.MergeIdentical = call.Bool("merge_identical", true);
            options.Collision = (call.Str("collision") ?? "solid").Trim().ToLowerInvariant();
            if (options.Collision != "solid" && options.Collision != "world" && options.Collision != "ballistic" && options.Collision != "none")
                throw McpError.Invalid("'collision' is solid, world, ballistic or none.");
            if (call.Has("physics")) options.Physics = call.StrList("physics");
            if (call.Has("exclude")) options.Exclude = call.StrList("exclude");
            options.Lights = call.Bool("lights", true);
            options.LightScale = (float)call.Num("light_scale", 1.0);
            if (options.LightScale < 0 || options.LightScale > 1000) throw McpError.Invalid("'light_scale' takes 0 to 1000.");
            options.Shadows = (call.Str("shadows") ?? "spots").Trim().ToLowerInvariant();
            if (options.Shadows != "spots" && options.Shadows != "all" && options.Shadows != "none") throw McpError.Invalid("'shadows' is spots, all or none.");
            options.CompactTextures = call.Bool("compact_textures");
            return options;
        }

        private static object ImportScene(McpCall call)
        {
            SceneOptions options = ReadOptions(call);
            bool dryRun = call.Bool("dry_run");
            string directional = (call.Str("directional") ?? "note").Trim().ToLowerInvariant();
            if (directional != "note" && directional != "outdoor_lighting") throw McpError.Invalid("'directional' is note or outdoor_lighting.");
            int limit = call.Has("limit") ? Math.Max(1, call.Int("limit")) : 200;

            ScenePlan plan;
            try
            {
                using (McpEditorTools.Heartbeat(call, "Reading " + Path.GetFileName(options.Path)))
                    plan = ScenePlan.Read(options);
            }
            catch (SceneImportException e) { throw new McpError(e.Message); }

            JObject report = null;
            using (McpEditorTools.Heartbeat(call, "Planning materials and budgets"))
                McpEditor.UI(() =>
                {
                    Level level = McpEditor.RequireLevel(forEditing: !dryRun).Level;
                    plan.PlanMaterials(level);
                    report = SceneImporter.Describe(plan, level);
                });
            Trim(report, limit);
            if (dryRun)
            {
                report["dry_run"] = true;
                return report;
            }
            if (report["warnings"] is JArray warnings && warnings.Any(o => ((string)o ?? "").StartsWith("NAME:")))
                throw new McpError(McpErrorCodes.Refused, (string)warnings.First(o => ((string)o ?? "").StartsWith("NAME:")));

            JObject result = SceneImporter.Import(call, plan, directional);
            //What was planned, then what was made
            JObject combined = new JObject() { ["summary"] = report["summary"], ["budgets"] = report["budgets"], ["placement_check"] = report["placement_check"] };
            foreach (JProperty property in result.Properties()) combined[property.Name] = property.Value;
            if (report["lights"] is JArray lights && lights.Count != 0) combined["lights"] = lights;
            if (report["warnings"] != null) combined["warnings"] = report["warnings"];
            if (report["notes"] is JArray notes) foreach (JToken note in notes) call.Note((string)note);
            return combined;
        }

        private static void Trim(JObject report, int limit)
        {
            foreach (string list in new[] { "models", "lights", "materials" })
                if (report[list] is JArray array && array.Count > limit)
                {
                    int total = array.Count;
                    while (array.Count > limit) array.RemoveAt(array.Count - 1);
                    report[list + "_listed"] = limit + " of " + total;
                }
        }
    }
}
