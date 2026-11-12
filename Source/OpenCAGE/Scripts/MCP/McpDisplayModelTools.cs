using AlienPAK;
using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CATHODE.ShaderTypes;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.Characters;
using OpenCAGE.Rigging;
using OpenCAGE.Undo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Vector3 = System.Numerics.Vector3;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// Character looks: display model variants that work in game (check_display_models, create_display_variant,
    /// add_held_part), a first-person body made from any body on a rig (make_first_person_body) and the player's model (set_player_model).
    /// Registered from McpPageTools.Tools(); toolset 'characters'. The Composite Browser's DisplayModel menu and the Model Editor's
    /// "Make First-Person Body..." run these same tools.
    /// </summary>
    internal static class McpDisplayModelTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "check_display_models",
                Title = "Check display models",
                Toolset = "characters",
                Description = "Check the level's DisplayModel: composites (or one) against the shape every shipped one has, which a character wearing it needs in game: the composite's id is the hash of its name (the game finds it by hashing display_model), " +
                    "its skeleton entity (EnvironmentModelReference) has the composite's own id and the ANIMATED_MODEL resource id 00-E4-14-34, its animated-model entry is its own (no two share one) and lists exactly its parts. " +
                    "A copy or rename made outside OpenCAGE's Duplicate / Rename (or by an older version) can break these. fix: true puts each one right, one undo step per display model.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "One DisplayModel: composite (default: every one in the level)."),
                    McpSchema.Boolean("fix", "Put what can be fixed right (default false: report only).")),
                Run = CheckDisplayModels,
            };

            yield return new McpTool()
            {
                Name = "create_display_variant",
                Title = "Create a display model variant",
                Toolset = "characters",
                Description = "A new look for a character that copies an existing one without breaking it: a copy of a DisplayModel: composite (the parts keep their meshes; the copy gets the id its name hashes to, its own skeleton id and its own animated-model entry, as every shipped display model has), " +
                    "optionally TINTED (each body part's materials copied as <variant>\\<material> with DIFFUSE_TINT multiplied by 'tint'; the hit collision is never tinted) and/or holding an extra part skinned to its rig (held_part: a gun in its hand - see add_held_part). " +
                    "Wear it by setting a Character's (or an NPC archetype's) display_model to the variant's name without the prefix (set_npc settings.display_model, or set_parameters) and custom_character_type NONE. Script changes are undo steps; copied materials and an imported part are level assets (written by save_level, not undoable, as create_material / import_model).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("source", "The DisplayModel: composite to copy (e.g. DisplayModel:ONEAL).", required: true),
                    McpSchema.String("name", "The variant's name (DisplayModel: is added if left off), e.g. DisplayModel:ONEAL_RED.", required: true),
                    McpSchema.Any("tint", "[r, g, b] (0-1, or 0-255) multiplied into DIFFUSE_TINT of every tinted part's materials, e.g. [1, 0.25, 0.2] for red. Leave out for an untinted copy."),
                    McpSchema.Strings("tint_parts", "Only these parts (entity names or ids of the variant's ModelReferences); default every part except hit collision."),
                    McpSchema.Strings("tint_materials", "Only these materials (as list_materials names them), e.g. the clothing but not the skin; default every material of the tinted parts."),
                    McpSchema.Any("held_part", "An extra part skinned to the rig: {path (FBX/glTF/OBJ, modelled about the bone: a weapon X down the barrel like the game's own), bone (default RIGHTWEAPONBONE), position [x,y,z] m, rotation [x,y,z] degrees, scale, part_name (default HELD), materials / material / generate (as import_model)}."),
                    McpSchema.Boolean("dry_run", "Report what would be made, changing nothing.")),
                Destructive = true,
                Run = CreateDisplayVariant,
            };

            yield return new McpTool()
            {
                Name = "add_held_part",
                Title = "Add a part skinned to a character's rig",
                Toolset = "characters",
                Description = "Add a part to a DisplayModel: that follows one bone of its rig in every animation - a gun in its hand, a holstered pistol, a lamp on its head: the file's meshes are modelled about the bone (a weapon about the weapon bone, X down the barrel, as the game's own third-person weapons are), placed by position/rotation/scale in the bone's frame, " +
                    "moved into the rig's bind pose and weighted 100% to that bone, imported as import_model does (materials generated for a skinned mesh, or 'materials'/'material'), then drawn by a new ModelReference in the display model and listed in its animated-model entry (the display model first gets an entry of its own if it shares one). " +
                    "NPC weapons still hang off the gameplay skeleton: a held part is for a look, not a working gun. The model import is not undoable; the script changes are.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("display_model", "The DisplayModel: composite to add to.", required: true),
                    McpSchema.String("path", "The part's model file (FBX, glTF, OBJ...).", required: true),
                    McpSchema.String("bone", "The bone it follows, without the rig prefix (default RIGHTWEAPONBONE; also LEFTWEAPONBONE, RIGHTHAND, HEAD, SPINE3, HIPS...)."),
                    McpSchema.Any("position", "[x, y, z] metres in the bone's frame (default 0)."),
                    McpSchema.Any("rotation", "[x, y, z] degrees in the bone's frame (default 0)."),
                    McpSchema.Number("scale", "Size factor on top of the file's units (default 1)."),
                    McpSchema.String("part_name", "The new ModelReference's name (default HELD)."),
                    McpSchema.String("model_name", "The imported model's name (default CHARACTERS\\<display model>_<part_name>)."),
                    McpSchema.Map("materials", "Mesh name or the file's material name -> a level material (it must be one that skins, e.g. a character material)."),
                    McpSchema.String("material", "One level material for every mesh."),
                    McpSchema.Any("generate", "As import_model."),
                    McpSchema.Strings("exclude", "Meshes of the file to leave out (names or indexes), as import_model."),
                    McpSchema.Boolean("dry_run", "Read the file and report where the part would sit (its meshes, the bone, its extent at the rig's rest pose), changing nothing.")),
                Destructive = true,
                Run = AddHeldPartTool,
            };

            yield return new McpTool()
            {
                Name = "make_first_person_body",
                Title = "Make a first-person body",
                Toolset = "characters",
                Description = "Make a body the player can wear in first person from any body skinned to a character rig: a copy of its model with what a camera in the head would see from the inside taken off, as the game's own first-person bodies (RIPLEY_FP) are cut - the head, neck and clavicles " +
                    "(vertices led by HEAD/NECK/LEFT|RIGHTSHOULDER, or carrying more than 25% of them) and the upper torso except a front panel (heights as fractions of the rig's hips-to-neck, measured on RIPLEY_FP); arms, hands, legs and the lower torso stay. Whole triangles go (any corner in the cut; lenient: two or more), " +
                    "kept vertices are copied byte for byte, the skinned bounds are set again. Then a DisplayModel: for it (the shipped shape) with the source's hit collision copied in, and optionally set as the player's model. dry_run (+ picture) shows the cut in red, front and side, changing nothing. " +
                    "The player's model is on FEMALEFP: a body for the player has to be on FEMALEFP (rebind one with import_skinned_onto_rig rig FEMALEFP).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("display_model", "The DisplayModel: composite whose body to cut (its hit collision is copied too)."),
                    McpSchema.String("model", "Or the model to cut (when no display_model; then rig is needed if the model doesn't say)."),
                    McpSchema.String("rig", "The rig the body is skinned to (default the display model's)."),
                    McpSchema.String("name", "The new DisplayModel's name (default DisplayModel:<source>_FP)."),
                    McpSchema.String("model_name", "The cut model's name (default <model>_FP)."),
                    McpSchema.Boolean("lenient", "A triangle goes only when two or more corners are in the cut (kinder to low-polygon bodies; default false)."),
                    McpSchema.Boolean("keep_torso", "Take only the head, neck and clavicles; keep the whole torso."),
                    McpSchema.Any("drop", "Bones whose vertices go besides the rule's (e.g. [\"LEFTSHOULDERARMOUR\"])."),
                    McpSchema.String("hit_collision", "'source' (default with display_model: copy the source's hit-collision parts), 'auto' (a hit-collision part of this level on the rig), or 'none'."),
                    McpSchema.Boolean("set_player_model", "Then make it the player's model (Global's FlightSuit, or 'suit')."),
                    McpSchema.String("suit", "Which of Global's player models: FlightSuit (default), Spacesuit or FlightSuit_1."),
                    McpSchema.Boolean("dry_run", "Report the cut, changing nothing."),
                    McpSchema.Boolean("picture", "With dry_run: return a picture of the cut (red) front and side.")),
                Destructive = true,
                Run = MakeFirstPersonBody,
            };

            yield return new McpTool()
            {
                Name = "set_player_model",
                Title = "Set the player's model",
                Toolset = "characters",
                Description = "Make the player wear a DisplayModel in first person: the player's Character wears PLAYER_FP, which is whichever model the level's Global composite registers (RegisterCharacterModel FlightSuit -> RIPLEY_FP, Spacesuit -> SPACESUIT_RIPLEY_FP, FlightSuit_1 -> RIPLEY_NAKED_FP); " +
                    "this sets that registration's display_model. The model has to be on the player's rig (FEMALEFP; SPACESUITFP for the spacesuit) and should be cut for first person (make_first_person_body). One undo step; in game after save_level build=true.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("display_model", "The DisplayModel: composite (or its name without the prefix); 'default' puts the shipped model back.", required: true),
                    McpSchema.String("suit", "FlightSuit (default), Spacesuit or FlightSuit_1."),
                    McpSchema.Boolean("dry_run", "Report what would change.")),
                Destructive = true,
                Run = SetPlayerModel,
            };
        }

        #region helpers
        //the shader families that skin a mesh to its rig (what a held part needs to follow its bone)
        private static readonly HashSet<SHADER_LIST> SkinningFamilies = new HashSet<SHADER_LIST>() { SHADER_LIST.CA_CHARACTER, SHADER_LIST.CA_SKIN, SHADER_LIST.CA_SKIN_OCCLUSION, SHADER_LIST.CA_HAIR, SHADER_LIST.CA_EYE, SHADER_LIST.CA_LOW_LOD_CHARACTER };

        private static Composite DisplayModel(Commands commands, string reference, string argument)
        {
            string name = (reference ?? "").Trim();
            if (name.Length == 0) throw McpError.Invalid("Say which display model ('" + argument + "').");
            Composite found = commands.Entries.FirstOrDefault(o => o != null && string.Equals(o.name, name, StringComparison.OrdinalIgnoreCase))
                ?? commands.Entries.FirstOrDefault(o => o != null && string.Equals(o.name, DisplayModelShape.Prefix + name, StringComparison.OrdinalIgnoreCase))
                ?? McpScript.FindComposite(commands, name);
            if (!DisplayModelShape.IsDisplayModel(found))
                throw McpError.Invalid(found.name + " is not a DisplayModel: composite. The level's: " + string.Join(", ", commands.Entries.Where(DisplayModelShape.IsDisplayModel).Select(o => o.name).Take(40)) + ".");
            return found;
        }

        private static string WithPrefix(string name)
        {
            name = (name ?? "").Trim();
            return name.StartsWith(DisplayModelShape.Prefix, StringComparison.OrdinalIgnoreCase) ? DisplayModelShape.Prefix + name.Substring(DisplayModelShape.Prefix.Length) : DisplayModelShape.Prefix + name;
        }

        private static Skeleton Rig(string name)
        {
            Skeleton rig = string.IsNullOrWhiteSpace(name) ? null : Singleton.Global?.GetSkeleton(name.Trim());
            if (rig == null)
                throw McpError.NotFound("rig", name, Singleton.Animations?.Skeletons?.Select(o => o.ToString()), Singleton.AnimationsLoaded ? "list_skeletons lists them." : "(The animation data is not loaded yet.)");
            return rig;
        }

        private static JObject Shape(Commands commands, Composite composite)
        {
            DisplayModelShape.Report report = DisplayModelShape.Inspect(commands, composite);
            JObject o = new JObject() { ["composite"] = composite.name, ["id"] = McpScript.Id(composite.shortGUID), ["wear_as"] = DisplayModelShape.CharacterName(composite), ["ok"] = report.Ok };
            if (report.Facts.Count != 0) o["facts"] = new JArray(report.Facts);
            if (report.Problems.Count != 0) o["problems"] = new JArray(report.Problems);
            o["parts"] = new JArray(DisplayModelShape.Parts(composite).Select(p => commands.Utils.GetEntityName(composite, p.Entity) + " (" + McpScript.Id(p.Renderable.resource_id) + (IsHitCollision(p.Renderable) ? ", hit collision" : "") + ")"));
            return o;
        }

        private static bool IsHitCollision(ResourceReference renderable) => renderable?.RenderableInstance?.Any(e => e?.Model?.WeightedCollision != null) == true;

        /// <summary>The fix step for one display model, applied (UI thread). Null when nothing needed doing.</summary>
        private static DisplayModelFixEdit Fix(Commands commands, Composite composite, string label)
        {
            DisplayModelShape.Report report = DisplayModelShape.Inspect(commands, composite);
            if (report.Ok || !report.Fixable) return null;
            DisplayModelFixEdit edit = new DisplayModelFixEdit(composite, label);
            UndoStack.Current.Apply(edit);
            return edit;
        }

        private static Vector3 ReadVector(McpCall call, JToken token, string name, Vector3 fallback)
        {
            if (token == null || token.Type == JTokenType.Null) return fallback;
            return McpValues.ReadVector(token, name, null);
        }

        private static string UniqueModelName(Level level, string wanted)
        {
            string name = wanted;
            for (int i = 2; level.Models.Entries.Any(o => o != null && string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase)); i++) name = wanted + "_" + i;
            return name;
        }

        private static string Leaf(string name)
        {
            string n = (name ?? "").Replace('/', '\\');
            if (n.EndsWith(".CS2", StringComparison.OrdinalIgnoreCase)) n = n.Substring(0, n.Length - 4);
            int at = n.LastIndexOf('\\');
            return at >= 0 ? n.Substring(at + 1) : n;
        }
        #endregion

        #region check_display_models
        private static object CheckDisplayModels(McpCall call)
        {
            bool fix = call.Bool("fix");
            return McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: fix);
                List<Composite> targets = call.Has("composite") ? new List<Composite>() { DisplayModel(commands, call.Str("composite"), "composite") } : commands.Entries.Where(DisplayModelShape.IsDisplayModel).OrderBy(o => o.name).ToList();
                JArray list = new JArray();
                int bad = 0, fixedCount = 0;
                foreach (Composite composite in targets)
                {
                    JObject shape = Shape(commands, composite);
                    if (!(bool)shape["ok"]) bad++;
                    if (fix && !(bool)shape["ok"])
                    {
                        DisplayModelFixEdit edit = Fix(commands, composite, "AI: Fix display model " + DisplayModelShape.CharacterName(composite));
                        if (edit != null)
                        {
                            fixedCount++;
                            shape["fixed"] = new JArray(edit.Changes);
                            shape["after"] = Shape(commands, composite);
                        }
                    }
                    list.Add(shape);
                }
                if (fixedCount != 0) Singleton.Editor?.CompositeBrowser?.RefreshList();
                return new JObject() { ["display_models"] = list, ["count"] = targets.Count, ["with_problems"] = bad, ["fixed"] = fixedCount };
            });
        }
        #endregion

        #region create_display_variant
        private static object CreateDisplayVariant(McpCall call)
        {
            bool dryRun = call.Bool("dry_run");
            string name = WithPrefix(call.Str("name", required: true));
            Vector3? tint = null;
            if (call.Has("tint"))
            {
                Vector3 t = McpValues.ReadVector(call.Token("tint"), "tint", null);
                if (t.X > 1.0001f || t.Y > 1.0001f || t.Z > 1.0001f) t /= 255f;
                if (t.X < 0 || t.Y < 0 || t.Z < 0 || t.X > 4 || t.Y > 4 || t.Z > 4) throw McpError.Invalid("'tint' is [r, g, b]: each 0-1 (or 0-255).");
                tint = t;
            }
            JObject held = call.Object("held_part");

            //---- checked before anything changes
            string sourceName = null, rigName = null;
            List<(string part, string material)> tinting = new List<(string, string)>();
            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: !dryRun);
                Level level = McpEditor.RequireLevel(forEditing: !dryRun).Level;
                Composite source = DisplayModel(commands, call.Str("source", required: true), "source");
                sourceName = source.name;
                string problem = ModelCompositeBuilder.Problem(name, true, commands);
                if (problem != null) throw McpError.Invalid("name: " + problem);
                rigName = DisplayModelShape.RigName(source);
                if (held != null)
                {
                    Skeleton rig = Rig(rigName);
                    string bone = (string)held["bone"] ?? "RIGHTWEAPONBONE";
                    if (FirstPersonCut.BoneIndex(rig, bone) < 0) throw McpError.Invalid("held_part.bone: " + rig.Name + " has no bone " + bone + ".");
                    if (held["path"] == null || !File.Exists(McpAssets.AbsolutePath((string)held["path"], "held_part.path"))) throw McpError.Invalid("held_part.path: there is no file there.");
                }
                if (tint != null)
                {
                    McpAssets.MaterialNames names = McpAssets.Names(level);
                    HashSet<string> only = new HashSet<string>(call.StrList("tint_parts"), StringComparer.OrdinalIgnoreCase);
                    HashSet<string> onlyMaterials = new HashSet<string>(call.StrList("tint_materials"), StringComparer.OrdinalIgnoreCase);
                    foreach ((FunctionEntity entity, ResourceReference renderable) in DisplayModelShape.Parts(source))
                    {
                        string partName = commands.Utils.GetEntityName(source, entity);
                        if (IsHitCollision(renderable)) continue;
                        if (only.Count != 0 && !only.Contains(partName) && !only.Contains(McpScript.Id(entity.shortGUID))) continue;
                        foreach (RenderableElements.Element element in renderable.RenderableInstance ?? new List<RenderableElements.Element>())
                            if (element?.Material != null && (onlyMaterials.Count == 0 || onlyMaterials.Contains(names.Ref(element.Material)) || onlyMaterials.Contains(element.Material.Name ?? ""))) tinting.Add((McpScript.Id(entity.shortGUID), names.Ref(element.Material)));
                    }
                    if (tinting.Count == 0) throw McpError.Invalid("Nothing to tint: " + (only.Count != 0 ? "no part of " + source.name + " is named " + string.Join(", ", only) : source.name + " has no parts with materials") + ".");
                }
            });
            string variant = name.Substring(DisplayModelShape.Prefix.Length);
            List<string> materials = tinting.Select(o => o.material).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (dryRun)
            {
                return new JObject()
                {
                    ["dry_run"] = true,
                    ["variant"] = name,
                    ["wear_as"] = variant,
                    ["copy_of"] = sourceName,
                    ["rig"] = rigName,
                    ["tint"] = tint == null ? null : new JArray(Math.Round(tint.Value.X, 3), Math.Round(tint.Value.Y, 3), Math.Round(tint.Value.Z, 3)),
                    ["materials_to_copy"] = new JArray(materials.Select(m => m + " -> " + variant + "\\" + Leaf(m.Split('#')[0]))),
                    ["held_part"] = held == null ? null : held.DeepClone(),
                };
            }

            //---- the copy: CathodeLib gives its skeleton entity its id, CompositeRefactoring.DuplicateEdit an animated-model entry of its own
            McpEditorTools.Nested(call, "duplicate_composite", new JObject() { ["composite"] = sourceName, ["path"] = name });
            JObject result = new JObject() { ["variant"] = name, ["wear_as"] = variant, ["copy_of"] = sourceName };

            //---- tint: a copy of each material with DIFFUSE_TINT multiplied, the variant's parts drawing the copies
            if (tint != null)
            {
                JObject map = new JObject();
                JArray made = new JArray();
                foreach (string material in materials)
                {
                    string copyName = variant + "\\" + Leaf(material.Split('#')[0]);
                    JObject created = McpEditorTools.Nested(call, "create_material", new JObject() { ["copy_from"] = material, ["name"] = copyName });
                    string createdName = (string)created["name"] ?? (string)created["material"]?["name"] ?? copyName;
                    JObject described = McpEditorTools.Nested(call, "describe_material", new JObject() { ["material"] = createdName });
                    JToken current = (described["parameters"] as JArray)?.FirstOrDefault(p => (string)p["name"] == "DIFFUSE_TINT")?["value"];
                    if (current == null)
                    {
                        call.Note(material + " has no DIFFUSE_TINT to tint (its shader family doesn't take one): its copy draws as the original.");
                        map[material] = createdName;
                        continue;
                    }
                    double[] was = current is JArray a ? a.Select(o => (double)o).ToArray() : new[] { (double)current, (double)current, (double)current, 1.0 };
                    if (was.Length < 4) was = was.Concat(Enumerable.Repeat(1.0, 4 - was.Length)).ToArray();
                    JArray now = new JArray(Math.Round(was[0] * tint.Value.X, 4), Math.Round(was[1] * tint.Value.Y, 4), Math.Round(was[2] * tint.Value.Z, 4), was[3]);
                    McpEditorTools.Nested(call, "edit_material", new JObject() { ["material"] = createdName, ["parameters"] = new JObject() { ["DIFFUSE_TINT"] = now } });
                    map[material] = createdName;
                    made.Add(new JObject() { ["from"] = material, ["copy"] = createdName, ["DIFFUSE_TINT"] = new JArray(was.Select(o => Math.Round(o, 4))) + " -> " + now.ToString(Newtonsoft.Json.Formatting.None) });
                }
                List<string> parts = tinting.Select(o => o.part).Distinct().ToList();
                McpEditorTools.Nested(call, "set_renderable", new JObject() { ["composite"] = name, ["entities"] = new JArray(parts), ["materials"] = map });
                result["tinted"] = made;
                result["tinted_parts"] = parts.Count;
            }

            //---- the held part
            if (held != null)
            {
                JObject args = (JObject)held.DeepClone();
                args["display_model"] = name;
                result["held_part"] = AddHeldPart(call, args);
            }

            //---- and the shape the game needs, checked (and put right: the mappings after the parts changed)
            result["display_model"] = McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                Composite made = DisplayModel(commands, name, "name");
                DisplayModelFixEdit edit = Fix(commands, made, "AI: Fix display model " + variant);
                if (edit != null) result["fixed"] = new JArray(edit.Changes);
                Singleton.Editor?.CompositeBrowser?.RefreshList();
                return Shape(commands, made);
            });
            call.Note("Wear it: display_model '" + variant + "' (and custom_character_type NONE) on a Character or NPC archetype - set_npc settings {display_model: '" + variant + "'} for a placed NPC. save_level with build=true puts it in game.");
            return result;
        }
        #endregion

        #region add_held_part
        private static object AddHeldPartTool(McpCall call) => call.Bool("dry_run") ? HeldPartPlan(call) : AddHeldPart(call, call.Args);

        /// <summary>add_held_part's dry run: the file read and placed on the bone in memory, nothing imported.</summary>
        private static JObject HeldPartPlan(McpCall call)
        {
            string path = McpAssets.AbsolutePath(call.Str("path", required: true), "path");
            if (!File.Exists(path)) throw new McpError("There is no file at " + path + ".");
            HeldPart.Placement placement = new HeldPart.Placement()
            {
                Bone = (call.Str("bone") ?? "RIGHTWEAPONBONE").Trim().ToUpperInvariant(),
                Position = ReadVector(call, call.Token("position"), "position", Vector3.Zero),
                Rotation = ReadVector(call, call.Token("rotation"), "rotation", Vector3.Zero),
                Scale = call.Has("scale") ? (float)call.Num("scale", 1) : 1f,
            };
            string display = null, rigName = null;
            McpEditor.UI(() =>
            {
                Composite composite = DisplayModel(McpEditor.RequireCommands(forEditing: false), call.Str("display_model", required: true), "display_model");
                display = composite.name;
                rigName = DisplayModelShape.RigName(composite);
            });
            Skeleton rig = Rig(rigName);
            Assimp.Scene scene;
            using (Assimp.AssimpContext importer = new Assimp.AssimpContext())
                scene = importer.ImportFile(path, ModelIO.ImportPostProcessSteps);
            ModelIO.ImportPlan plan = ModelIO.CreateImportPlan(scene, ModelIO.TryLoadSidecar(path), Path.GetFileNameWithoutExtension(path));
            if (!plan.HasMetadata) plan.UnitScale = ModelIO.FormatUnitScale(path);
            HashSet<string> excluded = new HashSet<string>(call.StrList("exclude"), StringComparer.OrdinalIgnoreCase);
            string placed = HeldPart.Apply(scene, plan, rig, placement, excluded);
            return new JObject()
            {
                ["dry_run"] = true,
                ["display_model"] = display,
                ["rig"] = rig.Name,
                ["bone"] = placement.Bone,
                ["meshes"] = new JArray(scene.Meshes.Select((m, i) => i + ": " + m.Name + " (" + m.VertexCount + " vertices)" + (excluded.Contains(m.Name) || excluded.Contains(i.ToString()) ? " - left out" : ""))),
                ["placed"] = placed,
            };
        }

        /// <summary>The held part from <paramref name="args"/> (add_held_part's arguments) into its display model. Any thread.</summary>
        private static JObject AddHeldPart(McpCall call, JObject args)
        {
            string display = (string)args["display_model"];
            string path = McpAssets.AbsolutePath((string)args["path"], "path");
            if (!File.Exists(path)) throw new McpError("There is no file at " + path + ".");
            HeldPart.Placement placement = new HeldPart.Placement()
            {
                Bone = ((string)args["bone"] ?? "RIGHTWEAPONBONE").Trim().ToUpperInvariant(),
                Position = ReadVector(call, args["position"], "position", Vector3.Zero),
                Rotation = ReadVector(call, args["rotation"], "rotation", Vector3.Zero),
                Scale = args["scale"] == null ? 1f : (float)McpValues.ReadDouble(args["scale"], "scale"),
            };
            if (placement.Scale <= 0) throw McpError.Invalid("'scale' has to be above 0.");
            string partName = ((string)args["part_name"] ?? "HELD").Trim();
            if (partName.Length == 0) partName = "HELD";

            string compositeName = null, rigName = null, modelName = null;
            Skeleton rig = null;
            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                Composite composite = DisplayModel(commands, display, "display_model");
                compositeName = composite.name;
                rigName = DisplayModelShape.RigName(composite);
                rig = Rig(rigName);
                if (FirstPersonCut.BoneIndex(rig, placement.Bone) < 0) throw McpError.Invalid(rig.Name + " has no bone " + placement.Bone + ".");
                if (composite.functions.Any(f => string.Equals(commands.Utils.GetEntityName(composite, f), partName, StringComparison.OrdinalIgnoreCase)))
                    throw McpError.Invalid(composite.name + " already has an entity called " + partName + ": give another part_name.");
                Level level = McpEditor.RequireLevel().Level;
                modelName = (string)args["model_name"] ?? "CHARACTERS\\" + DisplayModelShape.CharacterName(composite) + "_" + partName;
                modelName = UniqueModelName(level, modelName);
                //a shared entry gets the part too unless it is made the display model's own first
                Fix(commands, composite, "AI: Own animated model for " + DisplayModelShape.CharacterName(composite));
            });

            //---- the model: import_model, its scene rewritten as a rigid part on the bone, in the rig's bind pose
            string placed = null;
            JObject import = new JObject() { ["path"] = path, ["name"] = modelName, ["skeleton"] = rigName, ["create_composite"] = false };
            foreach (string pass in new[] { "materials", "material", "generate", "exclude" }) if (args[pass] != null) import[pass] = args[pass].DeepClone();
            McpCall inner = new McpCall(call.Tool, import, call.Cancel, (text, done, total) => call.Progress(text, done, total));
            HashSet<string> leftOut = new HashSet<string>((args["exclude"] as JArray ?? new JArray()).Select(o => (string)o), StringComparer.OrdinalIgnoreCase);
            JObject imported = McpImportTools.ImportModel(inner, (scene, plan) => { placed = HeldPart.Apply(scene, plan, rig, placement, leftOut); }) as JObject ?? new JObject();
            foreach (string note in inner.Notes.Where(n => n.IndexOf("No DisplayModel composite", StringComparison.Ordinal) < 0)) call.Note(note);
            string model = (string)imported["model"];
            if (model == null) throw new McpError("The part's model did not import.");
            int parts = McpEditor.UI(() => McpAssets.FindModel(McpEditor.RequireLevel().Level, model).Components.Count(c => c.LODs.Count != 0 && c.LODs[0].Submeshes.Count != 0));
            //A part only follows the bone when its materials skin (a character shader): an OpenCAGE export's sidecar brings back the level's own
            //materials, which for a prop or a weapon draw it at the rest pose
            List<string> stiff = McpEditor.UI(() => McpAssets.FindModel(McpEditor.RequireLevel().Level, model).Components.SelectMany(c => c.LODs.SelectMany(l => l.Submeshes))
                .Select(s => s.Material).Where(m => m != null && !SkinningFamilies.Contains(m.Shader?.Ubershader ?? SHADER_LIST.CA_ENVIRONMENT)).Select(m => m.Name + " (" + m.Shader?.Ubershader + ")").Distinct().ToList());
            if (stiff.Count != 0)
                call.Note("These materials don't skin, so what draws with them stays at the rig's rest pose instead of following " + placement.Bone + ": " + string.Join(", ", stiff) +
                    ". Give the part a character material ('materials' / 'material'), or move the file's .cs2meta.json aside so materials are generated for it.");

            //---- the parts that draw it: one ModelReference per part of the model, listed in the animated model
            JArray entities = new JArray();
            for (int i = 0; i < Math.Max(parts, 1); i++)
            {
                string entityName = parts <= 1 ? partName : partName + "_" + i;
                McpEditorTools.Nested(call, "create_entities", new JObject()
                {
                    ["composite"] = compositeName,
                    ["entities"] = new JArray(new JObject() { ["function"] = "ModelReference", ["name"] = entityName, ["position"] = new JArray(0, 0, 0), ["rotation"] = new JArray(0, 0, 0) }),
                });
                McpEditorTools.Nested(call, "set_renderable", new JObject() { ["composite"] = compositeName, ["entity"] = entityName, ["model"] = model, ["part"] = i });
                entities.Add(entityName);
            }
            JArray changes = McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                Composite composite = DisplayModel(commands, compositeName, "display_model");
                DisplayModelFixEdit edit = Fix(commands, composite, "AI: List " + partName + " in " + DisplayModelShape.CharacterName(composite) + "'s animated model");
                Singleton.OnResourceModified?.Invoke();
                return new JArray(edit?.Changes ?? new List<string>());
            });
            return new JObject()
            {
                ["display_model"] = compositeName,
                ["model"] = model,
                ["materials_generated"] = imported["materials_generated"],
                ["materials_picked"] = imported["materials_picked"],
                ["entities"] = entities,
                ["bone"] = placement.Bone,
                ["placed"] = placed,
                ["animated_model"] = changes,
            };
        }
        #endregion

        #region make_first_person_body
        private static object MakeFirstPersonBody(McpCall call)
        {
            bool dryRun = call.Bool("dry_run");
            FirstPersonRule rule = new FirstPersonRule() { Lenient = call.Bool("lenient"), KeepTorso = call.Bool("keep_torso") };
            foreach (string bone in call.StrList("drop")) { rule.DropDominant.Add(bone.Trim().ToUpperInvariant()); rule.DropAnyWeight.Add(bone.Trim().ToUpperInvariant()); }
            string hitMode = (call.Str("hit_collision") ?? "source").Trim().ToLowerInvariant();
            if (hitMode != "source" && hitMode != "auto" && hitMode != "none") throw McpError.Invalid("hit_collision takes source, auto or none.");

            //---- the body, its rig, the cut
            object outcome = McpEditor.UI<object>(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: !dryRun).Level;
                Commands commands = level.Commands;
                Composite source = call.Has("display_model") ? DisplayModel(commands, call.Str("display_model"), "display_model") : null;
                if (source == null && call.Has("model") && !call.Has("rig"))
                {
                    //a model a display model draws: that display model (its rig, its hit collision)
                    Models.CS2 wanted = McpAssets.FindModel(level, call.Str("model"));
                    source = commands.Entries.Where(DisplayModelShape.IsDisplayModel).FirstOrDefault(o => DisplayModelShape.Parts(o).Any(p => p.Renderable.RenderableInstance?.Any(e => e?.Model != null && wanted.Components.Any(c => c.LODs.Any(l => l.Submeshes.Contains(e.Model)))) == true));
                    if (source != null) call.Note(wanted.Name + " is drawn by " + source.name + ": its rig and hit collision are used.");
                }
                Models.CS2 model;
                List<(FunctionEntity entity, ResourceReference renderable)> hitParts = new List<(FunctionEntity, ResourceReference)>();
                if (source != null)
                {
                    List<Models.CS2> bodies = new List<Models.CS2>();
                    foreach ((FunctionEntity entity, ResourceReference renderable) in DisplayModelShape.Parts(source))
                    {
                        if (IsHitCollision(renderable)) { hitParts.Add((entity, renderable)); continue; }
                        Models.CS2 drawn = renderable.RenderableInstance?.Select(e => e?.Model == null ? null : level.Models.FindModel(e.Model)).FirstOrDefault(o => o != null);
                        if (drawn != null && !bodies.Contains(drawn)) bodies.Add(drawn);
                    }
                    if (call.Has("model")) model = McpAssets.FindModel(level, call.Str("model"));
                    else if (bodies.Count == 1) model = bodies[0];
                    else if (bodies.Count == 0) throw McpError.Invalid(source.name + " draws no body model.");
                    else throw McpError.Invalid(source.name + " draws " + bodies.Count + " models (" + string.Join(", ", bodies.Select(o => o.Name)) + "): pass 'model' to pick the body.");
                }
                else model = McpAssets.FindModel(level, call.Str("model") ?? throw McpError.Invalid("Give display_model (or model)."));
                string rigName = call.Str("rig") ?? (source != null ? DisplayModelShape.RigName(source) : null) ?? Singleton.Animations?.RigFor(model)?.Name;
                Skeleton rig = Rig(rigName ?? throw McpError.Invalid("Say which rig the model is skinned to ('rig')."));
                FirstPersonCutResult plan = FirstPersonCut.Plan(model, rig, rule, out string problem);
                if (plan == null) throw new McpError(problem);
                JObject report = new JObject()
                {
                    ["model"] = model.Name,
                    ["rig"] = rig.Name,
                    ["rule"] = rule.ToString(),
                    ["torso_kept_to_m"] = new JObject() { ["front"] = Math.Round(plan.FrontY, 3), ["mid"] = Math.Round(plan.MidY, 3), ["back"] = Math.Round(plan.BackY, 3), ["hips"] = Math.Round(plan.HipsY, 3), ["neck"] = Math.Round(plan.NeckY, 3) },
                    ["triangles"] = plan.TrianglesBefore + " -> " + plan.TrianglesAfter,
                    ["submeshes"] = new JArray(plan.Pieces.Select(o => o.ToString())),
                };
                if (plan.Notes.Count != 0) report["notes"] = new JArray(plan.Notes);
                if (!string.Equals(rig.Name, "FEMALEFP", StringComparison.OrdinalIgnoreCase) && !string.Equals(rig.Name, "SPACESUITFP", StringComparison.OrdinalIgnoreCase))
                    report["warning"] = "The player's character is on FEMALEFP (SPACESUITFP in the spacesuit): a body on " + rig.Name + " can be cut but not worn by the player. Rebind it onto FEMALEFP first (import_skinned_onto_rig rig FEMALEFP).";
                if (dryRun)
                {
                    report["dry_run"] = true;
                    if (call.Bool("picture"))
                    {
                        using (Bitmap bitmap = BodyPicture.Draw(model, plan, rig, 900, 620, model.Name + " on " + rig.Name + ": red is cut (" + (plan.TrianglesBefore - plan.TrianglesAfter) + " of " + plan.TrianglesBefore + " triangles), green the eye (HEAD)"))
                        using (MemoryStream png = new MemoryStream())
                        {
                            bitmap.Save(png, ImageFormat.Png);
                            return new McpImage() { Data = png.ToArray(), MimeType = "image/png", Caption = report.ToString(Newtonsoft.Json.Formatting.None) };
                        }
                    }
                    return report;
                }
                if (!plan.ChangesAnything) throw new McpError("The rule takes nothing from " + model.Name + ": it is cut already (or nothing of it is on the head, neck, clavicles or upper torso).");

                //---- the cut copy, the display model
                string modelName = UniqueModelName(level, (call.Str("model_name") ?? (model.Name.EndsWith(".CS2", StringComparison.OrdinalIgnoreCase) ? model.Name.Substring(0, model.Name.Length - 4) : model.Name) + "_FP").Trim());
                string name = call.Has("name") ? WithPrefix(call.Str("name")) : DisplayModelShape.Prefix + Leaf(source != null ? DisplayModelShape.CharacterName(source) : model.Name) + "_FP";
                string clash = ModelCompositeBuilder.Problem(name, true, commands);
                if (clash != null) throw McpError.Invalid("name: " + clash);
                Models.CS2 cut = FirstPersonCut.MakeCopy(model, modelName, plan, out List<Models.CS2.Component.LOD.Submesh> changed);
                ModelIO.SetSkinnedBounds(cut, rig);
                level.Models.Entries.Add(cut);
                EditHistory.AssetChanged(level, CathodeLib.AssetModificationInfoTable.AssetKind.MODEL, cut.Name);
                Singleton.OnCompositeAddPending?.Invoke();
                ModelCompositeBuilder.Result made = ModelCompositeBuilder.Create(level, cut, name, rig, rig.Name);
                Singleton.OnCompositeAdded?.Invoke(made.Composite);
                EditHistory.CompositesChanged(commands, new[] { made.Composite });
                report["cut_model"] = cut.Name;
                report["display_model"] = made.Composite.name;
                report["wear_as"] = DisplayModelShape.CharacterName(made.Composite);
                report["build_notes"] = new JArray(made.Notes);
                report["hit_collision_from"] = hitMode == "source" ? (JToken)new JArray(hitParts.Select(o => source.name + "|" + McpScript.Id(o.entity.shortGUID))) : hitMode;
                return report;
            });
            return !dryRun && outcome is JObject built ? FinishFirstPersonBody(call, built, hitMode) : outcome;
        }

        private static object FinishFirstPersonBody(McpCall call, JObject report, string hitMode)
        {
            if (report["display_model"] == null) return report;
            string display = (string)report["display_model"];
            //---- hit collision: the source's parts, or one of the level's on the rig
            JArray copied = new JArray();
            List<string> from = new List<string>();
            if (hitMode == "source" && report["hit_collision_from"] is JArray sources) from.AddRange(sources.Select(o => (string)o));
            if (hitMode == "auto" || (hitMode == "source" && from.Count == 0))
            {
                string auto = McpEditor.UI(() =>
                {
                    Level level = McpEditor.RequireLevel().Level;
                    RebindImport.CollisionSource best = RebindImport.HitCollisionParts(level, Rig((string)report["rig"])).FirstOrDefault();
                    return best == null ? null : best.Composite.name + "|" + McpScript.Id(best.Part.shortGUID);
                });
                if (auto != null) from.Add(auto);
                else call.Note("No hit-collision part on " + (string)report["rig"] + " in this level to copy: bullets will pass through this body.");
            }
            foreach (string spec in from)
            {
                string[] parts = spec.Split('|');
                JObject result = McpEditorTools.Nested(call, "copy_entities", new JObject() { ["composite"] = parts[0], ["entities"] = new JArray(parts[1]), ["into"] = display, ["links"] = "none" });
                copied.Add(spec);
            }
            report["hit_collision"] = copied;
            report["shape"] = McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                Composite composite = DisplayModel(commands, display, "name");
                DisplayModelFixEdit edit = Fix(commands, composite, "AI: List " + DisplayModelShape.CharacterName(composite) + "'s parts in its animated model");
                if (edit != null) report["fixed"] = new JArray(edit.Changes);
                Singleton.Editor?.CompositeBrowser?.RefreshList();
                Singleton.OnResourceModified?.Invoke();
                return Shape(commands, composite);
            });
            if (call.Bool("set_player_model"))
                report["player_model"] = McpEditorTools.Nested(call, "set_player_model", new JObject() { ["display_model"] = display, ["suit"] = call.Str("suit") ?? "FlightSuit" });
            else
                call.Note("Make it the player's: set_player_model display_model '" + display + "'. save_level with build=true puts it in game.");
            return report;
        }
        #endregion

        #region set_player_model
        private static readonly Dictionary<string, string> ShippedSuits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FlightSuit"] = "RIPLEY_FP", ["Spacesuit"] = "SPACESUIT_RIPLEY_FP", ["FlightSuit_1"] = "RIPLEY_NAKED_FP",
        };

        /// <summary>The Global composite's player model registrations: (entity, its name, the display_model it registers). UI thread.</summary>
        internal static List<(FunctionEntity Entity, string Name, string Model)> PlayerSuits(Commands commands, out Composite global)
        {
            global = commands.EntryPoints.Length > 1 ? commands.EntryPoints[1] : null;
            List<(FunctionEntity, string, string)> found = new List<(FunctionEntity, string, string)>();
            if (global == null) return found;
            foreach (FunctionEntity entity in global.functions.Where(f => f.function == FunctionType.RegisterCharacterModel))
                found.Add((entity, commands.Utils.GetEntityName(global, entity), (entity.GetParameter("display_model")?.content as cString)?.value));
            return found;
        }

        private static object SetPlayerModel(McpCall call)
        {
            bool dryRun = call.Bool("dry_run");
            string suitName = (call.Str("suit") ?? "FlightSuit").Trim();
            string wanted = (call.Str("display_model", required: true) ?? "").Trim();
            string globalName = null, entityId = null, before = null, after = null;
            List<string> warnings = new List<string>();
            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: !dryRun);
                List<(FunctionEntity Entity, string Name, string Model)> suits = PlayerSuits(commands, out Composite global);
                if (global == null || suits.Count == 0) throw new McpError("This level's Global composite registers no player models (RegisterCharacterModel).");
                var suit = suits.FirstOrDefault(o => string.Equals(o.Name, suitName, StringComparison.OrdinalIgnoreCase));
                if (suit.Entity == null) throw McpError.Invalid("Global has no player model '" + suitName + "'. It has: " + string.Join(", ", suits.Select(o => o.Name + " (" + o.Model + ")")) + ".");
                globalName = global.name; entityId = McpScript.Id(suit.Entity.shortGUID); before = suit.Model;
                if (wanted.Equals("default", StringComparison.OrdinalIgnoreCase))
                {
                    if (!ShippedSuits.TryGetValue(suit.Name, out after)) throw McpError.Invalid("No shipped model is known for " + suit.Name + ".");
                    return;
                }
                Composite composite = DisplayModel(commands, wanted, "display_model");
                after = DisplayModelShape.CharacterName(composite);
                string rig = DisplayModelShape.RigName(composite);
                Composite current = commands.Entries.FirstOrDefault(o => DisplayModelShape.IsDisplayModel(o) && string.Equals(DisplayModelShape.CharacterName(o), before, StringComparison.OrdinalIgnoreCase));
                string want = current != null ? DisplayModelShape.RigName(current) : (suit.Name.StartsWith("Space", StringComparison.OrdinalIgnoreCase) ? "SPACESUITFP" : "FEMALEFP");
                if (!string.Equals(rig, want, StringComparison.OrdinalIgnoreCase))
                    warnings.Add(composite.name + " is on " + rig + ", but the player's " + suit.Name + " model is on " + want + ": it won't play the player's animations (rebind it onto " + want + ").");
                DisplayModelShape.Report shape = DisplayModelShape.Inspect(commands, composite);
                if (!shape.Ok) warnings.Add(composite.name + " is not in the shape the game needs: " + string.Join(" ", shape.Problems) + " (check_display_models fix: true).");
            });
            JObject result = new JObject() { ["global"] = globalName, ["suit"] = suitName, ["entity"] = entityId, ["before"] = before, ["after"] = after };
            if (warnings.Count != 0) result["warnings"] = new JArray(warnings);
            if (dryRun) { result["dry_run"] = true; return result; }
            if (!string.Equals(before, after, StringComparison.OrdinalIgnoreCase))
                McpEditorTools.Nested(call, "set_parameters", new JObject()
                {
                    ["composite"] = globalName,
                    ["changes"] = new JArray(new JObject() { ["entity"] = entityId, ["parameters"] = new JObject() { ["display_model"] = after } }),
                });
            call.Note("The player's Character wears PLAYER_FP, which is now " + after + " in the " + suitName + ". save_level with build=true puts it in game.");
            return result;
        }
        #endregion
    }
}
