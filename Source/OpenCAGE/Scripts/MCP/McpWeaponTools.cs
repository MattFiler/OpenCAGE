using CATHODE.Scripting;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.Weapons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenCAGE.MCP
{
    /// <summary>The replace-weapon-model wizard's tools (Scripts\Weapons\WeaponModelReplace.cs): describe a weapon, export it to model against, replace its model.</summary>
    internal static class McpWeaponTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "describe_weapon",
                Title = "Describe weapon",
                Description = "A weapon or held item of the open level as replace_weapon_model sees it (no edits): its first-person composite (Required_Assets\\Weapons\\<Weapon>\\Base) with each drawn part, the rig bone that carries it and which clips move it (a shotgun's PUMP racks with 'recoil'), parts a script shows or hides (the SHELL a reload loads), its markers (MUZZLE_POS_MARKER, IK points); its third-person composite (Base_TP: NPCs' hands, and for items the pickup and thrown one); props drawing the same weapon (inventory pickup, the physics prop an NPC's death drops) with how well each lines up; and the NPC types whose weapon draws Base_TP. " +
                    "With no weapon, every weapon and held item of the level.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("weapon", "The weapon: its key as the list gives it (\"Shotgun\", \"Flare\", \"Motion_Tracker\\\\Tracker_1979\") or its first-person composite's path. Leave out to list them.")),
                ReadOnly = true,
                Idempotent = true,
                Run = DescribeWeapon,
            };

            yield return new McpTool()
            {
                Name = "export_weapon_reference",
                Title = "Export weapon reference",
                Description = "Write a weapon's retail model as a file to build a replacement against, in WEAPON SPACE - the space replace_weapon_model reads a model in (the first-person composite's own space: for the guns metres, X along the barrel, Z up): one mesh per first-person part named REF_<PART> (REF_TP_<PART> for third person) and an empty node per marker (REF_MARKER_MUZZLE_POS_MARKER...). " +
                    "Model or line up the new weapon over it, name meshes after the parts they should ride on (PUMP, SHELL...), and export from the same file: replace_weapon_model leaves REF_* meshes out. Format by the extension (.gltf/.glb metres, .fbx/.obj/.dae centimetres). Written straight away.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("weapon", "The weapon (describe_weapon lists them).", required: true),
                    McpSchema.String("path", "The file to write (full path; .gltf, .glb, .fbx, .obj or .dae).", required: true),
                    McpSchema.Boolean("third_person", "Also the third-person model, where the fit puts it in weapon space (default true)."),
                    McpSchema.Boolean("markers", "Also an empty node per marker (default true).")),
                Idempotent = true,
                Run = ExportReference,
            };

            yield return new McpTool()
            {
                Name = "replace_weapon_model",
                Title = "Replace weapon model",
                Description = "Put a model of your own on a weapon or held item, first person, third person and pickup / dropped props in one go, keeping its retail animation: each part keeps its entity and resource id (set_renderable), so the bone the level's animation entry hangs on it still carries the new geometry (a pump racks, a needle stabs). " +
                    "The model file is read in WEAPON SPACE (export_weapon_reference writes the retail weapon there to model against; offset / rotation adjust it). Meshes named after a part go on it (or say so in 'parts'); the rest go on the main part; an unmapped part the rig moves or a script shows (the loaded SHELL) is kept, other retail parts are hidden. " +
                    "First person is imported with its OWN NO_CLIP material copies at draw pass 31 (<material>_FP; an entity-level override draws a held model invisible), third person and props with the plain ones; each other composite's model is placed by fitting its retail geometry to the first person's. MUZZLE_POS_MARKER and the effects and markers near it move to the new barrel's end. A prop with its own physics body whose shape changes a lot gets a hull from the new model. " +
                    "npc_types keeps the third-person change to those NPC types (copies of their weapon archetype and of Base_TP, as duplicate_composite use_for). A clearance check says whether a kept animated part pokes out of the new model during the weapon's clips, against how retail does. " +
                    "dry_run lists the whole plan - parts, bones, actions, imports, markers, clearance, every step - without changing anything. Not one undo step: imports cannot be undone, the edits each are. Written by save_level (build=true for the game).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("weapon", "The weapon or held item (describe_weapon lists them): \"Shotgun\", \"Pistol\", \"Flare\", ...", required: true),
                    McpSchema.String("path", "The model file (glTF/GLB, FBX, OBJ, DAE), authored in weapon space. Leave out to only show or hide parts (parts {\"TRIGGER\": \"show\"}: a retail part an earlier replace hid comes back, nothing is imported)."),
                    McpSchema.Number("scale", "Resize the file on the way in (default 1). OBJ, FBX and DAE are read as centimetres, glTF as metres, as import_model reads them."),
                    McpSchema.Vector("offset", "Move the model in weapon space, metres (after the file's own placement): e.g. [0, 0, 0.015] raises a gun 1.5 cm."),
                    McpSchema.Rotation("rotation", "Turn the model in weapon space (about its origin, before the offset)."),
                    McpSchema.Map("parts", "First-person part -> what to do: \"keep\" (the retail part stays as it is), \"hide\", \"show\" (the retail part is shown again), \"auto\" (default), or the mesh names (or file material names, or indexes) of the file to draw with it: {\"PUMP\": [\"Forend\"], \"SHELL\": \"keep\", \"KNOBS\": \"hide\"}. A mesh named after a part goes on it without being asked."),
                    McpSchema.Strings("targets", "What to change (default: first and third person, and every prop that lines up): first_person, third_person, pickup, dropped, props, or composite paths."),
                    McpSchema.Any("muzzle", "\"auto\" (default: the new barrel's end along MUZZLE_POS_MARKER's line), \"keep\", or [x, y, z] in weapon space."),
                    McpSchema.Number("muzzle_radius", "Markers, lights and effects within this many metres of the old muzzle move with it (default 0.12)."),
                    McpSchema.Map("move", "Other markers to move, by name, by an offset in weapon space: {\"Left_IK_Male\": [0.02, 0, 0]} (in first and third person)."),
                    McpSchema.Strings("npc_types", "Only these NPC types get the new third-person model (describe_weapon lists them): the rest keep retail's."),
                    McpSchema.String("npc_suffix", "With npc_types: the copies' name suffix (default CUSTOM)."),
                    McpSchema.String("physics", "Props with their own physics body: auto (default: a hull of the new model when the shape differs by over 25%), hull, keep.", options: new[] { "auto", "hull", "keep" }),
                    McpSchema.String("prefix", "Folder for the models and materials it makes (default WEAPONS\\<WEAPON>)."),
                    McpSchema.Boolean("clearance", "Run the clearance check (default true)."),
                    McpSchema.Boolean("dry_run", "Only report the plan.")),
                Destructive = true,
                Run = Replace,
            };
        }

        private static object DescribeWeapon(McpCall call)
        {
            string weapon = call.Str("weapon");
            using (McpEditorTools.Heartbeat(call, "Reading the weapon"))
                return McpEditor.UI<object>(() =>
                {
                    Level level = McpEditor.RequireLevel(forEditing: false).Level;
                    if (weapon == null)
                    {
                        JArray rows = new JArray();
                        foreach (Tuple<string, Composite, Composite> found in WeaponModelReplace.List(level.Commands))
                        {
                            WeaponInfo info = WeaponModelReplace.Analyse(level, found.Item1, readClips: false, quick: true);
                            rows.Add(new JObject()
                            {
                                ["weapon"] = found.Item1,
                                ["first_person"] = found.Item2.name,
                                ["third_person"] = found.Item3?.name,
                                ["rig"] = info.FirstPerson.RigName,
                                ["parts"] = new JArray(info.FirstPerson.Parts.Select(o => o.Name)),
                                ["props"] = new JArray(info.Props.Select(o => o.Path)),
                            });
                        }
                        call.Note("describe_weapon with a weapon gives its parts, the bones that move them and its markers; replace_weapon_model puts a model of your own on it.");
                        return new JObject() { ["weapons"] = rows };
                    }
                    WeaponInfo described = WeaponModelReplace.Analyse(level, weapon);
                    return WeaponModelReplace.Describe(described, level.Commands);
                });
        }

        private static object ExportReference(McpCall call)
        {
            string path = McpAssets.AbsolutePath(call.Str("path", required: true), "path");
            string extension = (System.IO.Path.GetExtension(path) ?? "").ToLowerInvariant();
            if (!ModelExport.ModelExporter.Formats.Any(o => o.Extension == extension))
                throw McpError.Invalid("'path' has to end in " + string.Join(", ", ModelExport.ModelExporter.Formats.Select(o => o.Extension)) + ".");
            return McpEditor.UI<object>(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: false).Level;
                WeaponInfo info = WeaponModelReplace.Analyse(level, call.Str("weapon", required: true), readClips: false);
                return WeaponModelReplace.ExportReference(level, info, path, call.Bool("third_person", true), call.Bool("markers", true));
            });
        }

        /// <summary>The wizard's options from a call's arguments (the window builds the same).</summary>
        internal static WeaponOptions Options(McpCall call)
        {
            WeaponOptions options = new WeaponOptions()
            {
                Weapon = call.Str("weapon", required: true),
                ModelPath = call.Has("path") ? McpAssets.AbsolutePath(call.Str("path"), "path") : null,
                Scale = (float)call.Num("scale", 1),
                MuzzleRadius = (float)call.Num("muzzle_radius", 0.12),
                NpcSuffix = call.Str("npc_suffix") ?? "CUSTOM",
                Physics = call.Str("physics") ?? "auto",
                Prefix = call.Str("prefix"),
                Clearance = call.Bool("clearance", true),
            };
            if (call.Has("offset")) options.Offset = McpValues.ReadVector(call.Token("offset"), "offset", null);
            if (call.Has("rotation")) options.Rotation = McpValues.ReadVector(call.Token("rotation"), "rotation", null);
            if (options.MuzzleRadius < 0 || options.MuzzleRadius > 2) throw McpError.Invalid("'muzzle_radius' runs 0 to 2 metres.");
            JObject parts = call.Object("parts");
            if (parts != null)
                foreach (JProperty part in parts.Properties())
                {
                    if (part.Value is JArray list) { options.PartMeshes[part.Name] = list.Select(o => McpValues.ReadString(o)).ToList(); continue; }
                    string text = McpValues.ReadString(part.Value).Trim();
                    string lower = text.ToLowerInvariant();
                    if (lower == "keep" || lower == "hide" || lower == "show") options.PartActions[part.Name] = lower;
                    else if (lower == "auto" || lower.Length == 0) continue;
                    else options.PartMeshes[part.Name] = text.Split(',').Select(o => o.Trim()).Where(o => o.Length != 0).ToList();
                }
            if (call.Has("targets")) options.Targets = call.StrList("targets");
            if (call.Has("muzzle"))
            {
                JToken muzzle = call.Token("muzzle");
                if (muzzle.Type == JTokenType.Array) { options.Muzzle = "point"; options.MuzzlePoint = McpValues.ReadVector(muzzle, "muzzle", null); }
                else
                {
                    string text = McpValues.ReadString(muzzle).Trim().ToLowerInvariant();
                    if (text != "auto" && text != "keep") throw McpError.Invalid("'muzzle' takes \"auto\", \"keep\" or [x, y, z].");
                    options.Muzzle = text;
                }
            }
            JObject move = call.Object("move");
            if (move != null)
                foreach (JProperty entry in move.Properties())
                    options.Move[entry.Name] = McpValues.ReadVector(entry.Value, "move." + entry.Name, null);
            if (call.Has("npc_types")) options.NpcTypes = call.StrList("npc_types");
            string physics = options.Physics.Trim().ToLowerInvariant();
            if (physics != "auto" && physics != "hull" && physics != "keep") throw McpError.Invalid("'physics' takes auto, hull or keep.");
            return options;
        }

        private static object Replace(McpCall call)
        {
            WeaponOptions options = Options(call);
            bool dry = call.Bool("dry_run");
            Tuple<WeaponPlan, JObject> planned;
            using (McpEditorTools.Heartbeat(call, "Planning the replace"))
                planned = McpEditor.UI(() =>
                {
                    Level level = McpEditor.RequireLevel(forEditing: !dry).Level;
                    if (!dry) McpEditor.RequireUndoIdle();
                    WeaponPlan made = WeaponModelReplace.Plan(level, options);
                    return Tuple.Create(made, WeaponModelReplace.Describe(made, level.Commands));
                });
            if (dry)
            {
                planned.Item2["dry_run"] = true;
                call.Note("Nothing was changed. Run again without dry_run to make these changes; save_level with build=true then takes them to the game.");
                return planned.Item2;
            }
            JObject ran;
            using (McpEditorTools.Heartbeat(call, "Replacing the model of " + planned.Item1.Info.Key))
                ran = WeaponModelReplace.Execute(call, planned.Item1);
            JObject result = McpEditor.UI(() => WeaponModelReplace.Describe(planned.Item1, McpEditor.RequireLevel(forEditing: false).Level.Commands));
            result.Remove("steps");
            result["done"] = ran["steps"];
            result["check"] = ran["check"];
            call.Note("Made in the open level. save_level with build=true takes it to the game; the first-person model shows once the weapon is drawn.");
            return result;
        }
    }
}
