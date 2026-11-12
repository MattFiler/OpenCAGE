using AlienPAK;
using Assimp;
using CATHODE;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.Rigging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// The rig tools: rebinding a model onto one of the game's skeletons, scaled display rigs, environment (prop)
    /// animations and animation contexts. Offered from McpAnimationAssetTools.Tools(), each with its own toolset.
    /// </summary>
    internal static class McpRigTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            yield return new McpTool()
            {
                Name = "import_skinned_onto_rig",
                Title = "Import a skinned model onto a game skeleton",
                Toolset = "characters",
                Description = "Import a character skinned to a skeleton the game doesn't have (FBX/glTF from Blender, Mixamo, Unreal, 3ds Max Biped...) REBOUND onto one of the game's character rigs (MALE, FEMALE, FEMALENPC, ALIEN...), " +
                    "so it plays that rig's animations: the mesh is moved into the rig's bind pose bone by bone (each bone placed, turned and stretched onto the rig's; the head keeps its shape; spine and tail laid along the rig's) and weighted to its bones, " +
                    "then imported as import_model does into a DisplayModel:<name> composite a Character wears (display_model). Body parts are matched to the file's joints by name and hierarchy; bone_map fixes any (dry_run lists the mapping). " +
                    "hit_collision copies a hit-collision part of this level skinned to the same rig into the DisplayModel (without one, bullets pass through the character). dry_run reports the mapping and fit changing nothing; picture adds a front/side image at rest and walking. " +
                    "The other arguments are import_model's (materials, generate, material, name, composite_name, exclude). Not undoable (as import_model).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("path", "The model file's full path.", required: true),
                    McpSchema.String("rig", "The game rig to rebind onto (default MALE; any character rig with hips, neck, arms and feet: MALE, FEMALE, FEMALENPC, ALIEN, FEMALEFP...)."),
                    McpSchema.Map("bone_map", "Body part -> the file's joint, over the automatic mapping (dry_run lists both): {\"LEFTFOREARM\": \"Bip01-L-Forearm\", \"TAIL\": \"tail_01\"}; \"\" leaves a part out. Parts: HIPS, NECK, HEAD, TAIL, and LEFT/RIGHT + SHOULDER, ARM, ARMROLL, FOREARM, FOREARMROLL, HAND, HAND<THUMB|INDEX|MIDDLE|RING|PINKY><1-3>, UPLEG, LEG, LEG2 (a digitigrade leg's second knee), FOOT, TOEBASE."),
                    McpSchema.Boolean("fit_size", "Size the model so its hips-to-neck matches the rig's (default true); false keeps the file's own units."),
                    McpSchema.Number("scale", "An extra size factor on top (default 1)."),
                    McpSchema.Boolean("keep_head_shape", "Move the head without turning or stretching it (default true)."),
                    McpSchema.Boolean("head_own_height", "Keep the head's own height above the shoulders rather than pinning it to the rig's HEAD joint (default true)."),
                    McpSchema.Boolean("stretch_tail", "Stretch a tail along the rig's whole tail (default false: it keeps its own length)."),
                    McpSchema.String("hit_collision", "'auto' (default): copy a hit-collision part of this level skinned to the rig (a retail character's COL_ part) into the DisplayModel; 'none'; or 'composite|entity' naming the part to copy."),
                    McpSchema.Boolean("dry_run", "Work the rebind out and report it (mapping, fit, warnings) without importing anything."),
                    McpSchema.Boolean("picture", "With dry_run: return a picture of the result on the rig, front and side, at rest and posed by one of its walk clips."),
                    McpSchema.String("name", "The model's name in the level (default the file's name)."),
                    McpSchema.String("composite_name", "The DisplayModel composite's name (default DisplayModel:<model name>)."),
                    McpSchema.Strings("exclude", "Meshes of the file to leave out (names or indexes)."),
                    McpSchema.Map("materials", "Mesh name or the file's material name -> a level material to draw it with."),
                    McpSchema.String("material", "Use this level material for every mesh not named in 'materials', instead of generating."),
                    McpSchema.Any("generate", "As import_model: the file's material name -> {family, name, always_new}.")),
                Destructive = true,
                Run = ImportSkinnedOntoRig,
            };

            yield return new McpTool()
            {
                Name = "create_display_rig",
                Title = "Create a display rig",
                Toolset = "characters",
                Description = "Make a character smaller, bigger or differently proportioned while it still plays a gameplay rig's animations (e.g. MALE at x0.5). Writes a DISPLAY RIG into ANIMATION.PAK - a copy of the gameplay rig (MALE, FEMALE, FEMALENPC, ALIEN...) at 'scale', with optional per-bone scale/offset, " +
                    "joined to it by a skeleton mapping of that scale and named in a skeleton definition that Character.reference_skeleton loads - then, in the open level, optionally makes a DisplayModel for it from one already on the gameplay rig (fit_display_model: models and hit collision resized, animated model moved onto the rig) " +
                    "and sets reference_skeleton (and display_model) on the given NPC archetypes AND on every placement of them that passes its own value (a placement's value wins). " +
                    "LIMITS (engine): weapons, CharacterAttachmentNode attachments and the muzzle follow the GAMEPLAY skeleton, so a held gun floats where a gameplay-size hand would be; feet slide (the character moves at the gameplay rig's speed); the rig keeps the gameplay rig's ragdoll, so a corpse is posed on gameplay-size joints (spindly); " +
                    "AI, capsule and navigation stay gameplay size. ANIMATION.PAK is shared by every level and written at once (not undoable; dry_run first); the level changes are written by save_level. Re-running with the same name replaces a rig made by this tool.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("name", "The new rig's name (upper case letters, digits, underscores; e.g. GRUNT_SMALL).", required: true),
                    McpSchema.Number("scale", "Its size against the gameplay rig (e.g. 0.5 half height, 1.15 bigger).", required: true),
                    McpSchema.String("gameplay", "The rig whose animations it plays (default MALE)."),
                    McpSchema.Map("bone_scale", "Bone (without the rig prefix) -> a factor on that bone's distance from its parent, on top of scale: {\"HEAD\": 1.3, \"LEFTLEG\": 1.2, \"RIGHTLEG\": 1.2}."),
                    McpSchema.Any("bone_offset", "Bone -> [x, y, z] metres added to its offset from its parent (in the parent's frame), at rest and in every pose."),
                    McpSchema.String("fit_display_model", "A DisplayModel: composite of the open level on the gameplay rig: make a copy of it for the new rig."),
                    McpSchema.String("display_model_name", "That copy's name (default DisplayModel:<its name>_<rig name>)."),
                    McpSchema.Strings("archetypes", "NPC archetype composites (e.g. Archetypes\\NPCs\\Human\\Security\\Grunt_NPC_Pistol) to put on the rig: reference_skeleton (and display_model + custom_character_type NONE when a display model was made), on the archetype and every placement passing its own."),
                    McpSchema.Boolean("dry_run", "Build and check the rig in memory and report, writing nothing (the PAK in memory is left untouched)."),
                    McpSchema.Boolean("close_game", "Close a running game first (it holds ANIMATION.PAK open).")),
                Destructive = true,
                Run = CreateDisplayRig,
            };

            yield return new McpTool()
            {
                Name = "import_environment_animation",
                Title = "Import an animated prop",
                Toolset = "animation",
                Description = "Import a prop that moves - a hatch, a fan, a machine arm - from an FBX/glTF whose parts are animated as objects (each part its own node, keyed) or skinned to an armature, as the game's own animated props work: " +
                    "a PROP RIG of its own (its parts' nodes or the armature's joints as bones, in the prop's space) and an animation set named after it holding the file's clips are written into ANIMATION.PAK; " +
                    "the parts come into the open level as one model skinned to that rig, in a composite with an EnvironmentModelReference (its ENVIRONMENT_ANIMATION.DAT entry) and a PlayEnvironmentAnimation playing a clip from the start of the level (play; loop repeats it). " +
                    "More PlayEnvironmentAnimations can play the other clips (AnimationSet = name, Animation = the clip, geometry -> the EnvironmentModelReference's reference; describe_animated_prop). place_in places an instance. " +
                    "ANIMATION.PAK is shared by every level and written at once (not undoable; re-running with the same name replaces a prop rig made here); the level's part is written by save_level (build=true). dry_run reports the rig, parts and clips first.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("path", "The model file's full path.", required: true),
                    McpSchema.String("name", "The prop's rig and animation set name (upper case letters, digits and underscores, e.g. PROP_HATCH).", required: true),
                    McpSchema.String("composite_name", "The composite placing it (default PROPS\\<name>)."),
                    McpSchema.String("model_name", "The model's name in the level (default PROPS\\<name>)."),
                    McpSchema.Number("frame_rate", "Frames per second to sample the file's animation at (default 30)."),
                    McpSchema.Strings("clip_names", "Names for the file's animations in order (default their own names, lower case)."),
                    McpSchema.String("play", "The clip the composite plays from the start of the level (default the first; 'none' for no player)."),
                    McpSchema.Boolean("loop", "Play it round and round (default true)."),
                    McpSchema.String("place_in", "Also place an instance of the composite in this composite ('root' for the level)."),
                    McpSchema.Position("position", "With place_in: where, in place_in's space."),
                    McpSchema.Rotation("rotation", "With place_in: its rotation."),
                    McpSchema.Map("materials", "Mesh name or the file's material name -> a level material to draw it with."),
                    McpSchema.String("material", "Use this level material for every mesh not named in 'materials', instead of generating."),
                    McpSchema.Boolean("max_worldspace", "End the rig with a MAX_WORLDSPACE bone on the root, as the game's own prop rigs do (default true)."),
                    McpSchema.Boolean("dry_run", "Read the file and report the rig, parts and clips, writing nothing."),
                    McpSchema.Boolean("close_game", "Close a running game first (it holds ANIMATION.PAK open).")),
                Destructive = true,
                Run = ImportEnvironmentAnimation,
            };

            yield return new McpTool()
            {
                Name = "list_animation_contexts",
                Title = "List animation contexts",
                Toolset = "animation",
                Description = "An animation set's contexts - the named groups of clip lines the game swaps in while a character is in that state (MALE's WEAPON_HANDGUN, CROUCHED, WEAPON_TRACKER...) - each with its clip and blend set lines, " +
                    "and the set's resident animation memory: a character only spawns once every resident clip section of its set (and its parents) fits the game's resident animation heap (about 21.5 MB, shared by every set in use).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("set", "The animation set (list_animation_sets).", required: true),
                    McpSchema.String("context", "One context in full: every clip line (name -> clip path)."),
                    McpSchema.Strings("alongside", "Sets usually in play with it, for the resident memory check (default MALE, FEMALE, ALIEN, 1ST_PERSON_RIPLEY).")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListAnimationContexts,
            };

            yield return new McpTool()
            {
                Name = "create_animation_context",
                Title = "Create an animation context",
                Toolset = "animation",
                Description = "Add a named context to an animation set (e.g. MALE's WEAPON_FLARE for a new held item): empty, or a copy of an existing context's clip and blend set lines (template) so it behaves as that one until its lines are pointed at other clips. " +
                    "'lines' points lines - the slots the animation trees ask for by name (held_weapon_layer_idle, aim_centre...) - at clips the set already holds (import new ones with import_animation into the set's own lines first: those are also all a script command reaches; retail points 1,147 CMD_PlayAnimation / CHR_PlaySecondaryAnimation at own lines and none at a context-only one, and the game rejects one that does - 'Attempt to load bad animation data'). The trees don't stream clips in, so a line they play wants a resident clip. existing points lines of a context the set already has. The game picks a character's context from what it holds: the weapon's or item's character_animation_context (an AIMED_WEAPON, AIMED_ITEM or MELEE_WEAPON parameter; the pistol's PISTOL_LOGIC in Pistol_SharedScript names WEAPON_HANDGUN) - set_parameters with a path to one NPC's weapon puts just that character in it. " +
                    "Lines copied from another context reuse its clips (nothing new to load); clips imported here are streamed one-clip sections. The result reports the set's resident animation memory against the heap. " +
                    "ANIMATION.PAK is shared by every level and written at once (not undoable); dry_run reports first.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("set", "The animation set (list_animation_sets).", required: true),
                    McpSchema.String("name", "The new context's name (upper case letters, digits, underscores; e.g. WEAPON_FLARE).", required: true),
                    McpSchema.String("template", "Copy this context's lines ('' for the set's own unnamed lines). Default: start empty."),
                    McpSchema.Map("lines", "Line name -> a clip of the set to point it at (added when the context has no such line): {\"aim_idle\": \"my_imported_hold\"}."),
                    McpSchema.Boolean("blend_sets", "With template: copy its blend set lines too (default true)."),
                    McpSchema.Boolean("existing", "Point 'lines' in a context the set already has (named 'name'; one made here or a shipped one) instead of making a new one."),
                    McpSchema.Boolean("dry_run", "Check and report without writing."),
                    McpSchema.Boolean("close_game", "Close a running game first (it holds ANIMATION.PAK open).")),
                Destructive = true,
                Run = CreateAnimationContext,
            };
        }

        #region CONTEXTS
        private static readonly string[] DefaultAlongside = { "MALE", "FEMALE", "ALIEN", "1ST_PERSON_RIPLEY" };

        private static CathodeLib.Animation.AnimationSet RequireSet(CathodeLib.Animation animations, string name)
        {
            CathodeLib.Animation.AnimationSet set = animations.GetSet(name);
            if (set == null) throw McpError.NotFound("animation set", name, animations.Sets.Select(o => o.Name), "list_animation_sets lists them.");
            return set;
        }

        private static JObject HeapReport(CathodeLib.Animation animations, string set, IEnumerable<string> alongside)
        {
            AnimationHeapBudget.Estimate alone = AnimationHeapBudget.ForSets(animations, new[] { set });
            AnimationHeapBudget.Estimate together = AnimationHeapBudget.ForSets(animations, new[] { set }.Concat(alongside).Distinct(StringComparer.OrdinalIgnoreCase));
            JObject o = new JObject()
            {
                ["set_resident"] = AnimationHeapBudget.Megabytes(alone.Bytes),
                ["with_alongside"] = AnimationHeapBudget.Megabytes(together.Bytes),
                ["alongside"] = new JArray(alongside),
                ["heap"] = AnimationHeapBudget.Megabytes(AnimationHeapBudget.Budget(false)),
                ["fits"] = together.Fits,
                //Launch Game's larger animation memory (launch_options larger_animation_memory) doubles the heap
                ["fits_larger_memory"] = together.FitsLarger,
                ["larger_memory_on"] = AnimationHeapBudget.Enabled,
            };
            string warning = AnimationHeapBudget.Warning(animations, set, alongside);
            if (warning != null) o["warning"] = warning;
            return o;
        }

        private static object ListAnimationContexts(McpCall call)
        {
            CathodeLib.Animation animations = Singleton.Animations;
            if (animations == null || !animations.Loaded) throw new McpError("The game's ANIMATION.PAK is not loaded.");
            List<string> alongside = call.StrList("alongside").ToList();
            if (alongside.Count == 0) alongside = DefaultAlongside.ToList();
            return McpEditor.UI(() =>
            {
                CathodeLib.Animation.AnimationSet set = RequireSet(animations, call.Str("set", required: true));
                string one = call.Str("context");
                JObject result = new JObject() { ["set"] = set.Name, ["kind"] = set.Kind.ToString(), ["skeleton"] = set.Skeleton };
                JArray contexts = new JArray();
                for (int i = 0; i < set.Database.Contexts.Count; i++)
                {
                    AnimClipDB.Context c = set.Database.Contexts[i];
                    if (one != null && !string.Equals(c.Name, one, StringComparison.OrdinalIgnoreCase)) continue;
                    JObject row = new JObject() { ["name"] = c.Name.Length == 0 ? "(unnamed slot " + i + ")" : c.Name, ["clip_lines"] = c.Animations.Count, ["blend_set_lines"] = c.BlendSets.Count };
                    if (one != null)
                    {
                        row["lines"] = new JObject(c.Animations.Select(a => new JProperty(a.Name.Length == 0 ? "(unnamed)" : a.Name, a.Path)).GroupBy(p => p.Name).Select(g => g.First()));
                        row["blend_sets"] = new JArray(c.BlendSets.Select(b => b.Name + " -> " + b.Filename));
                    }
                    contexts.Add(row);
                }
                if (one != null && contexts.Count == 0) throw McpError.NotFound("context", one, set.Database.Contexts.Select(o => o.Name).Where(o => o.Length != 0), null);
                result["own_lines"] = set.Database.Animations.Count;
                result["contexts"] = contexts;
                result["resident_memory"] = HeapReport(animations, set.Name, alongside.Where(o => !string.Equals(o, set.Name, StringComparison.OrdinalIgnoreCase)));
                return result;
            });
        }

        private static object CreateAnimationContext(McpCall call)
        {
            CathodeLib.Animation animations = Singleton.Animations;
            if (animations == null || !animations.Loaded) throw new McpError("The game's ANIMATION.PAK is not loaded.");
            string name = (call.Str("name", required: true) ?? "").Trim().ToUpperInvariant();
            string template = call.Has("template") ? (call.Str("template") ?? "") : null;
            JObject lines = call.Object("lines");
            bool dryRun = call.Bool("dry_run");
            CathodeLib.Animation.AnimationSet set = McpEditor.UI(() => RequireSet(animations, call.Str("set", required: true)));
            bool existing = call.Bool("existing");
            if (existing)
            {
                //only the lines change: the context has to be there already, and something has to be pointed
                AnimClipDB.Context had = set.Database?.Contexts.FirstOrDefault(c => c.Name.Length != 0 && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
                if (had == null) throw McpError.NotFound("context", name, set.Database?.Contexts.Select(o => o.Name).Where(o => o.Length != 0) ?? Enumerable.Empty<string>(), "Leave out existing to make it.");
                name = had.Name;
                if (lines == null || lines.Count == 0) throw McpError.Invalid("With existing, give the lines to point.");
                if (template != null) throw McpError.Invalid("template only applies to a new context.");
            }
            string problem = existing ? null : AnimationContexts.NameProblem(set, name);
            if (problem != null) throw McpError.Invalid(problem);
            if (template != null && template.Length != 0 && !set.Database.Contexts.Any(c => string.Equals(c.Name, template, StringComparison.OrdinalIgnoreCase)))
                throw McpError.NotFound("context", template, set.Database.Contexts.Select(o => o.Name).Where(o => o.Length != 0), "'' copies the set's own lines.");
            if (lines != null)
                foreach (JProperty p in lines.Properties())
                    if (!set.Contexts.SelectMany(c => c.Clips).Any(c => string.Equals(c.Name, McpValues.ReadString(p.Value), StringComparison.OrdinalIgnoreCase)))
                        throw McpError.Invalid("lines." + p.Name + ": " + set.Name + " has no clip '" + McpValues.ReadString(p.Value) + "' (import it with import_animation first).");
            List<string> alongside = DefaultAlongside.Where(o => !string.Equals(o, set.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (dryRun)
                return new JObject()
                {
                    ["set"] = set.Name, ["context"] = name, ["existing"] = existing, ["template"] = template,
                    ["copies"] = template == null ? 0 : (template.Length == 0 ? set.Database.Animations.Count : set.Database.Contexts.First(c => string.Equals(c.Name, template, StringComparison.OrdinalIgnoreCase)).Animations.Count),
                    ["lines"] = lines?.Count ?? 0,
                    ["resident_memory"] = McpEditor.UI(() => HeapReport(animations, set.Name, alongside)),
                };

            McpAnimationAssetTools.RefuseIfAnimationPakChanged();
            McpAnimationAssetTools.CloseOrRefuse(call, "ANIMATION.PAK can't be written while it holds it open");
            AnimationContexts.Result made = existing ? new AnimationContexts.Result() : McpEditor.UI(() => AnimationContexts.Create(animations, set, name, template, call.Bool("blend_sets", true)));
            if (!existing && !made.Ok) throw new McpError(made.Problem);
            List<string> pointed = new List<string>();
            if (lines != null)
                foreach (JProperty p in lines.Properties())
                {
                    string err = McpEditor.UI(() => AnimationContexts.SetLine(set, name, p.Name, McpValues.ReadString(p.Value)));
                    if (err != null) call.Note(err); else pointed.Add(p.Name + " -> " + McpValues.ReadString(p.Value));
                }
            McpEditor.UI(() => McpAnimationAssetTools.WriteAnimationPak(call, animations, "the context " + set.Name + "\\" + name));
            AnimationHeapBudget.Invalidate();
            McpEditor.UI(() => Singleton.OnAnimationsModified?.Invoke());
            return new JObject()
            {
                ["set"] = set.Name,
                ["context"] = name,
                ["report"] = new JArray(made.Report),
                ["lines_pointed"] = new JArray(pointed),
                ["resident_memory"] = McpEditor.UI(() => HeapReport(animations, set.Name, alongside)),
            };
        }
        #endregion

        #region PROPS
        private static object ImportEnvironmentAnimation(McpCall call)
        {
            string path = McpAssets.AbsolutePath(call.Str("path", required: true), "path");
            if (!File.Exists(path)) throw new McpError("There is no file at " + path + ".");
            CathodeLib.Animation animations = Singleton.Animations;
            if (animations == null || !animations.Loaded) throw new McpError("The game's ANIMATION.PAK is not loaded.");
            string name = (call.Str("name", required: true) ?? "").Trim().ToUpperInvariant();
            string problem = PropRigBuilder.NameProblem(animations, name, out bool replaces);
            if (problem != null) throw McpError.Invalid(problem);
            float rate = (float)call.Num("frame_rate", 30);
            if (!(rate >= 1 && rate <= 240)) throw McpError.Invalid("'frame_rate' has to be between 1 and 240.");
            List<string> clipNames = call.StrList("clip_names").ToList();
            string compositeName = (call.Str("composite_name") ?? ("PROPS\\" + name)).Trim();
            string modelName = (call.Str("model_name") ?? ("PROPS\\" + name)).Trim();
            bool loop = call.Bool("loop", true);
            bool dryRun = call.Bool("dry_run");

            Scene scene;
            using (McpEditorTools.Heartbeat(call, "Reading " + Path.GetFileName(path)))
            using (AssimpContext importer = new AssimpContext())
                scene = importer.ImportFile(path, ModelIO.ImportPostProcessSteps);
            float unit = ModelIO.FormatUnitScale(path);
            PropRig rig = PropRig.Read(scene, path, unit, name, rate, out string why);
            if (rig == null) throw new McpError("The prop could not be read: " + why);
            List<string> finalClips = rig.Clips.Select((c, i) => PropRigBuilder.AnimationName(i < clipNames.Count && !string.IsNullOrWhiteSpace(clipNames[i]) ? clipNames[i] : c.Name)).ToList();
            string play = call.Str("play");
            if (play == null) play = finalClips.FirstOrDefault();
            else if (play.Equals("none", StringComparison.OrdinalIgnoreCase)) play = null;
            else if (!finalClips.Contains(play, StringComparer.OrdinalIgnoreCase)) throw McpError.Invalid("'play': the file's clips come in as " + string.Join(", ", finalClips) + ".");
            McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands(forEditing: !dryRun);
                string clash = ModelCompositeBuilder.Problem(compositeName, false, commands);
                if (clash != null) throw McpError.Invalid("composite_name: " + clash);
                if (call.Has("place_in")) McpScript.FindComposite(commands, call.Str("place_in"));
            });

            JObject plan = new JObject()
            {
                ["rig"] = name,
                ["replaces_own_rig"] = replaces,
                ["skinned_file"] = rig.Skinned,
                ["bones"] = new JArray(rig.Joints.Select(j => j.Name + (j.Parent >= 0 ? " (under " + rig.Joints[j.Parent].Name + ")" : " (root)"))),
                ["parts"] = new JArray(rig.Parts.Select(p => scene.Meshes[p.MeshIndex].Name + (p.Weights == null ? " on " + rig.Joints[p.Joint].Name : " (skinned)") + ", " + p.Positions.Length + " vertices")),
                ["clips"] = new JArray(rig.Clips.Select((c, i) => finalClips[i] + ": " + c.Frames.Count + " frames, " + ((c.Frames.Count - 1) * c.FrameDuration).ToString("0.##") + " s")),
                ["composite"] = compositeName,
                ["plays"] = play == null ? null : play + (loop ? " (loop)" : ""),
            };
            if (rig.Notes.Count != 0) plan["notes"] = new JArray(rig.Notes);
            if (dryRun) return plan;
            if (rig.Clips.Count == 0) call.Note("The file has no animation: the prop is made, but nothing moves it until a clip is added (import_animation into set " + name + ").");

            //---- ANIMATION.PAK: the rig, the set, the clips
            McpAnimationAssetTools.RefuseIfAnimationPakChanged();
            McpAnimationAssetTools.CloseOrRefuse(call, "ANIMATION.PAK can't be written while it holds it open");
            bool maxWorldspace = call.Bool("max_worldspace", true);
            PropRigBuilder.Result built = McpEditor.UI(() => PropRigBuilder.Create(animations, name, rig, finalClips, maxWorldspace));
            if (!built.Ok) throw new McpError(built.Problem);
            McpEditor.UI(() => McpAnimationAssetTools.WriteAnimationPak(call, animations, "the prop rig " + name));
            foreach (string clip in built.Clips) Singleton.RegisterAnimation(name, clip);
            McpEditor.UI(() => Singleton.OnAnimationsModified?.Invoke());
            JObject result = new JObject() { ["rig"] = name, ["template_layout"] = built.Template, ["clips"] = new JArray(built.Clips), ["written"] = new JArray(built.Written), ["report"] = new JArray(built.Report) };

            //---- the model: import_model, its scene rewritten as the parts about their bones (rigid: one component per bone, ordinary
            //materials) or skinned to the rig
            bool rigidParts = rig.Parts.All(o => o.Weights == null);
            JObject args = new JObject() { ["path"] = path, ["name"] = modelName, ["skeleton"] = rigidParts ? "none" : name, ["create_composite"] = false };
            foreach (string pass in new[] { "materials", "material" }) if (call.Args[pass] != null) args[pass] = call.Args[pass].DeepClone();
            McpCall inner = new McpCall(call.Tool, args, call.Cancel, (text, done, total) => call.Progress(text, done, total));
            JObject imported = McpImportTools.ImportModel(inner, (s, p) =>
            {
                PropRig again = PropRig.Read(s, path, p.UnitScale, name, rate, out string whyNot);
                if (again == null) throw new McpError("The prop could not be read again for its model: " + whyNot);
                PropImport.ApplyToScene(again, s, p);
                p.Scale = 1;
            }) as JObject ?? new JObject();
            //the model import's own notes, less those this tool has already answered (the rig, the composite, the clips it brought in)
            foreach (string note in inner.Notes.Where(n => !n.Contains("does not sit on") && !n.Contains("No DisplayModel composite") && !n.Contains("which are not part of the model"))) call.Note(note);
            result["model"] = imported["model"];
            result["materials_generated"] = imported["materials_generated"];

            //---- the composite and its player
            string madeModel = (string)imported["model"];
            JObject composite = McpEditor.UI(() =>
            {
                Level level = McpEditor.RequireLevel(forEditing: true).Level;
                Models.CS2 model = level.Models.Entries.FirstOrDefault(o => o != null && string.Equals(o.Name, madeModel, StringComparison.OrdinalIgnoreCase));
                if (model == null) throw new McpError("The model " + madeModel + " is not in the level after its import.");
                Singleton.OnCompositeAddPending?.Invoke();
                PropImport.Placed placed = PropImport.MakeComposite(level, model, compositeName, built.Skeleton, rig, name, play, loop);
                Singleton.OnCompositeAdded?.Invoke(placed.Composite);
                EditHistory.CompositesChanged(level.Commands, new[] { placed.Composite });
                Singleton.Editor.CompositeBrowser?.RefreshList();
                Singleton.OnResourceModified?.Invoke();
                return new JObject()
                {
                    ["composite"] = placed.Composite.name,
                    ["environment_model_reference"] = placed.Reference == null ? null : McpScript.Id(placed.Reference.shortGUID),
                    ["player"] = placed.Player == null ? null : McpScript.Id(placed.Player.shortGUID),
                    ["report"] = new JArray(placed.Report),
                };
            });
            result["composite"] = composite;

            //---- placed, if asked
            if (call.Has("place_in"))
            {
                Composite into = null, made = null;
                cTransform local = null;
                McpEditor.UI(() =>
                {
                    Commands commands = McpEditor.RequireCommands();
                    into = McpScript.FindComposite(commands, call.Str("place_in"));
                    made = McpScript.FindComposite(commands, (string)composite["composite"]);
                    local = new cTransform(call.Has("position") ? McpValues.ReadVector(call.Token("position"), "position", null) : System.Numerics.Vector3.Zero,
                                           call.Has("rotation") ? McpValues.ReadVector(call.Token("rotation"), "rotation", null) : System.Numerics.Vector3.Zero);
                });
                FunctionEntity instance = null;
                McpScriptEdit.Run("Place " + made.name, into, edit =>
                {
                    instance = edit.AddInstance(into, made, null);
                    edit.SetParameter(into, instance, "position", new JObject() { ["position"] = McpValues.Vector(local.position), ["rotation"] = McpValues.Vector(local.rotation) });
                });
                result["placed"] = new JObject() { ["into"] = into.name, ["instance"] = McpScript.Id(instance.shortGUID) };
            }
            call.Note("save_level with build=true puts the prop in the game's level data; the clips are in ANIMATION.PAK already. Play another clip with a PlayEnvironmentAnimation {AnimationSet: '" + name + "', Animation: <clip>} whose geometry links to the composite's EnvironmentModelReference (or, from outside, to the instance's reference).");
            return result;
        }
        #endregion

        #region DISPLAY RIG
        private static object CreateDisplayRig(McpCall call)
        {
            CathodeLib.Animation animations = Singleton.Animations;
            if (animations == null || !animations.Loaded) throw new McpError("The game's ANIMATION.PAK is not loaded.");
            DisplayRigBuilder.Options options = new DisplayRigBuilder.Options()
            {
                Name = (call.Str("name", required: true) ?? "").Trim().ToUpperInvariant(),
                Scale = (float)call.Num("scale", 0),
                Gameplay = (call.Str("gameplay") ?? "MALE").Trim().ToUpperInvariant(),
            };
            JObject boneScale = call.Object("bone_scale");
            if (boneScale != null) foreach (JProperty p in boneScale.Properties()) options.BoneScale[p.Name.Trim().ToUpperInvariant()] = (float)McpValues.ReadDouble(p.Value, "bone_scale." + p.Name);
            if (call.Token("bone_offset") is JObject boneOffset)
                foreach (JProperty p in boneOffset.Properties()) options.BoneOffset[p.Name.Trim().ToUpperInvariant()] = McpValues.ReadVector(p.Value, "bone_offset." + p.Name, null);
            bool dryRun = call.Bool("dry_run");
            string fit = call.Str("fit_display_model");
            List<string> archetypes = call.StrList("archetypes").ToList();

            //everything checked before ANIMATION.PAK changes in memory
            string problem = DisplayRigBuilder.NameProblem(animations, options.Name, out bool replaces);
            if (problem != null) throw McpError.Invalid(problem);
            if (animations.GetSkeleton(options.Gameplay)?.Skeleton == null)
                throw McpError.NotFound("rig", options.Gameplay, RebindImport.CharacterRigs(animations).Select(o => o.Name), "The gameplay rig plays the animations: MALE for human NPCs, FEMALENPC / FEMALE for women, ALIEN for the alien.");
            if (!(options.Scale > 0.05f && options.Scale < 20f)) throw McpError.Invalid("'scale' has to be between 0.05 and 20.");
            string newDisplay = null;
            if (fit != null || archetypes.Count != 0)
                McpEditor.UI(() =>
                {
                    Commands commands = McpEditor.RequireCommands(forEditing: !dryRun);
                    if (fit != null)
                    {
                        Composite source = McpScript.FindComposite(commands, fit);
                        EnvironmentAnimations.EnvironmentAnimation entry = DisplayRigFit.AnimatedModelOf(source, out _);
                        if (entry == null) throw McpError.Invalid(source.name + " has no animated model: it isn't a DisplayModel.");
                        if (!string.Equals(entry.SkeletonName, options.Gameplay, StringComparison.OrdinalIgnoreCase))
                            throw McpError.Invalid(source.name + " is on " + entry.SkeletonName + ", not on " + options.Gameplay + ": fit a display model that is on the gameplay rig.");
                        string leaf = source.name.StartsWith(ModelCompositeBuilder.DisplayModelPrefix) ? source.name.Substring(ModelCompositeBuilder.DisplayModelPrefix.Length) : source.name;
                        newDisplay = ModelCompositeBuilder.Normalise(call.Str("display_model_name") ?? ModelCompositeBuilder.DisplayModelPrefix + leaf + "_" + options.Name);
                        if (!newDisplay.StartsWith(ModelCompositeBuilder.DisplayModelPrefix)) newDisplay = ModelCompositeBuilder.DisplayModelPrefix + newDisplay;
                        string clash = ModelCompositeBuilder.Problem(newDisplay, true, commands);
                        if (clash != null) throw McpError.Invalid("display_model_name: " + clash);
                    }
                    foreach (string archetype in archetypes) McpScript.FindComposite(commands, archetype);
                });

            if (dryRun)
            {
                //nothing is built: the builder writes into the PAK in memory, so the plan is worked out from the gameplay rig alone
                JObject plan = new JObject()
                {
                    ["rig"] = options.Name,
                    ["gameplay"] = options.Gameplay,
                    ["scale"] = options.Scale,
                    ["replaces_own_rig"] = replaces,
                    ["bone_changes"] = options.BoneScale.Count + options.BoneOffset.Count,
                    ["display_model"] = newDisplay,
                    ["archetypes"] = new JArray(archetypes),
                };
                Skeleton gameplay = animations.GetSkeleton(options.Gameplay).Skeleton;
                float[] scales = DisplayRigBuilder.BoneScales(gameplay, options);
                int head = RigRoles.Bone(gameplay, "HEAD");
                if (head >= 0)
                {
                    //where HEAD would rest: its chain from the root, each offset times its scale
                    System.Numerics.Vector3 at = System.Numerics.Vector3.Zero;
                    List<System.Numerics.Matrix4x4> local = new List<System.Numerics.Matrix4x4>();
                    for (int i = 0; i < gameplay.Bones.Count; i++)
                    {
                        Skeleton.Bone b = gameplay.Bones[i];
                        System.Numerics.Vector3 t = b.Position * scales[i] + (options.BoneOffset.TryGetValue(RigRoles.Short(b.Name), out System.Numerics.Vector3 o) ? o : System.Numerics.Vector3.Zero);
                        System.Numerics.Matrix4x4 m = System.Numerics.Matrix4x4.CreateScale(b.ScaleXYZ) * System.Numerics.Matrix4x4.CreateFromQuaternion(b.Rotation) * System.Numerics.Matrix4x4.CreateTranslation(t);
                        local.Add(b.ParentIndex >= 0 ? m * local[b.ParentIndex] : m);
                    }
                    plan["head_height_m"] = Math.Round((local[head] * Skeleton.ToMeshSpace).Translation.Y, 3);
                    plan["gameplay_head_height_m"] = Math.Round(gameplay.GetBindPose()[head].Translation.Y, 3);
                }
                plan["limits"] = "Weapons and attachments follow the gameplay rig (a held gun floats), feet slide, corpses are posed spindly on gameplay-size joints.";
                return plan;
            }

            //---- ANIMATION.PAK (global), on the UI thread as the animation browser writes it
            McpAnimationAssetTools.RefuseIfAnimationPakChanged();
            McpAnimationAssetTools.CloseOrRefuse(call, "ANIMATION.PAK can't be written while it holds it open");
            DisplayRigBuilder.Result built = McpEditor.UI(() => DisplayRigBuilder.Create(animations, options));
            if (!built.Ok) throw new McpError(built.Problem);
            McpEditor.UI(() => McpAnimationAssetTools.WriteAnimationPak(call, animations, "the display rig " + options.Name));
            JObject result = new JObject()
            {
                ["rig"] = options.Name,
                ["gameplay"] = options.Gameplay,
                ["replaced"] = built.Replaced,
                ["mapping"] = built.MappingFile,
                ["written"] = new JArray(built.Written),
                ["report"] = new JArray(built.Report),
            };

            //---- the level: a display model for it, and who wears it
            if (newDisplay != null)
            {
                JObject fitted = McpEditor.UI(() =>
                {
                    Level level = McpEditor.RequireLevel(forEditing: true).Level;
                    Composite source = McpScript.FindComposite(level.Commands, fit);
                    Singleton.OnCompositeAddPending?.Invoke();
                    DisplayRigFit.Result made = DisplayRigFit.Fit(level, source, animations.GetSkeleton(options.Gameplay).Skeleton, built.Skeleton, newDisplay, options.Scale);
                    if (made.Composite != null)
                    {
                        Singleton.OnCompositeAdded?.Invoke(made.Composite);
                        EditHistory.CompositesChanged(level.Commands, new[] { made.Composite });
                        Singleton.Editor.CompositeBrowser?.RefreshList();
                    }
                    if (made.Model != null) EditHistory.AssetChanged(level, CathodeLib.AssetModificationInfoTable.AssetKind.MODEL, made.Model.Name);
                    Singleton.OnResourceModified?.Invoke();
                    if (!made.Ok) throw new McpError("The rig was written, but the display model was not made: " + made.Problem);
                    return new JObject() { ["composite"] = made.Composite.name, ["model"] = made.Model.Name, ["report"] = new JArray(made.Report) };
                });
                result["display_model"] = fitted;
            }
            if (archetypes.Count != 0)
            {
                JArray changed = new JArray();
                foreach (string archetype in archetypes)
                    changed.Add(PutOnRig(call, archetype, options.Name, newDisplay));
                result["archetypes"] = changed;
            }
            call.Note("A Character uses it by reference_skeleton '" + options.Name + "' (anim_set stays " + options.Gameplay + "'s): set it on every placement that passes its own value. Check in game with the Debug Menu's anim.report <character>: skeleton=" + options.Name + ".");
            return result;
        }

        /* reference_skeleton (and the display model) on an archetype's own pins and on every placement of it that passes its own value */
        private static JObject PutOnRig(McpCall call, string archetype, string rig, string displayModel)
        {
            List<(string composite, string entity, string was)> placements = McpEditor.UI(() =>
            {
                Commands commands = McpEditor.RequireCommands();
                Composite composite = McpScript.FindComposite(commands, archetype);
                return DisplayRigFit.PlacementsPassing(commands, composite, "reference_skeleton").Select(o => (o.composite.name, McpScript.Id(o.entity.shortGUID), o.was)).ToList();
            });
            JObject values = new JObject() { ["reference_skeleton"] = rig };
            if (displayModel != null)
            {
                values["display_model"] = displayModel.Substring(ModelCompositeBuilder.DisplayModelPrefix.Length);
                values["custom_character_type"] = "NONE";
            }
            JArray changes = new JArray();
            foreach (JProperty v in values.Properties()) changes.Add(new JObject() { ["entity"] = v.Name, ["parameters"] = new JObject() { [v.Name] = v.Value } });
            JObject own = null;
            try { own = McpEditorTools.Nested(call, "set_parameters", new JObject() { ["composite"] = archetype, ["changes"] = changes }); }
            catch (McpError e) { call.Note(archetype + ": its own pins could not all be set (" + e.Message + "); its placements are still set below."); }
            JArray done = new JArray();
            foreach (var p in placements.Where(o => !string.Equals(o.was, rig, StringComparison.OrdinalIgnoreCase)))
            {
                JObject parameters = new JObject() { ["reference_skeleton"] = rig };
                McpEditorTools.Nested(call, "set_parameters", new JObject() { ["composite"] = p.composite, ["changes"] = new JArray(new JObject() { ["entity"] = p.entity, ["parameters"] = parameters }) });
                done.Add(p.composite + " / " + p.entity + ": " + p.was + " -> " + rig);
            }
            return new JObject() { ["archetype"] = archetype, ["pins_set"] = own != null, ["placements_changed"] = done, ["placements_already"] = placements.Count(o => string.Equals(o.was, rig, StringComparison.OrdinalIgnoreCase)) };
        }
        #endregion

        #region REBIND
        private static object ImportSkinnedOntoRig(McpCall call)
        {
            string path = McpAssets.AbsolutePath(call.Str("path", required: true), "path");
            if (!File.Exists(path)) throw new McpError("There is no file at " + path + ".");
            if (!Singleton.AnimationsLoaded || Singleton.Animations == null) throw new McpError("The game's animation data is not loaded yet, so there is no rig to rebind onto. Try again in a moment.");
            string rigName = (call.Str("rig") ?? "MALE").Trim();
            Skeleton rig = RebindImport.CharacterRigs(Singleton.Animations).FirstOrDefault(o => string.Equals(o.Name, rigName, StringComparison.OrdinalIgnoreCase));
            if (rig == null)
                throw McpError.NotFound("character rig", rigName, RebindImport.CharacterRigs(Singleton.Animations).Select(o => o.Name), "A rig needs hips, a neck, both arms and both feet to be rebound onto.");

            SkinRebind.Options options = new SkinRebind.Options()
            {
                FitSize = call.Bool("fit_size", true),
                Scale = (float)call.Num("scale", 1.0),
                KeepHeadShape = call.Bool("keep_head_shape", true),
                HeadAtOwnHeight = call.Bool("head_own_height", true),
                StretchTail = call.Bool("stretch_tail", false),
            };
            if (options.Scale <= 0) throw McpError.Invalid("'scale' has to be above 0.");
            JObject map = call.Object("bone_map");
            if (map != null)
                foreach (JProperty p in map.Properties())
                {
                    string role = p.Name.Trim().ToUpperInvariant();
                    if (!RigRoles.AllRoles().Contains(role)) throw McpError.Invalid("bone_map: '" + p.Name + "' is not a body part. Parts: " + string.Join(", ", RigRoles.AllRoles().Take(12)) + ", ... (see the tool's description).");
                    options.Map[role] = McpValues.ReadString(p.Value) ?? "";
                }

            bool dryRun = call.Bool("dry_run");
            string hit = (call.Str("hit_collision") ?? "auto").Trim();

            if (dryRun)
            {
                Scene scene;
                using (McpEditorTools.Heartbeat(call, "Reading " + Path.GetFileName(path)))
                using (AssimpContext importer = new AssimpContext())
                    scene = importer.ImportFile(path, ModelIO.ImportPostProcessSteps);
                ModelIO.ImportPlan plan = ModelIO.CreateImportPlan(scene, ModelIO.TryLoadSidecar(path), Path.GetFileNameWithoutExtension(path));
                if (!plan.HasMetadata) plan.UnitScale = ModelIO.FormatUnitScale(path);
                SkinRebind.Result result = RebindImport.Plan(scene, plan.UnitScale, rig, options);
                JObject report = Describe(result);
                if (hit != "none")
                    report["hit_collision"] = McpEditor.UI(() =>
                    {
                        Level level = McpEditor.RequireLevel(forEditing: false).Level;
                        List<RebindImport.CollisionSource> parts = RebindImport.HitCollisionParts(level, rig);
                        return parts.Count == 0 ? (JToken)("none in this level skinned to " + rig.Name + ": port a character's display model or its NPC collision composite first, or the character can't be shot")
                                                : new JArray(parts.Take(8).Select(o => o.Description + " [" + o.Composite.name + "|" + McpScript.Id(o.Part.shortGUID) + "]"));
                    });
                if (call.Bool("picture") && result.Ok)
                {
                    using (Bitmap bitmap = RebindImport.Picture(result, scene, RebindImport.ShowcaseClip(Singleton.Animations, rig)))
                    using (MemoryStream png = new MemoryStream())
                    {
                        bitmap.Save(png, ImageFormat.Png);
                        return new McpImage() { Data = png.ToArray(), MimeType = "image/png", Caption = report.ToString(Newtonsoft.Json.Formatting.None) };
                    }
                }
                return report;
            }

            //the import itself: import_model, with the scene rebound between reading it and planning it
            SkinRebind.Result applied = null;
            JObject args = (JObject)call.Args.DeepClone();
            foreach (string own in new[] { "rig", "bone_map", "fit_size", "keep_head_shape", "head_own_height", "stretch_tail", "hit_collision", "dry_run", "picture", "scale" })
                args.Remove(own);
            args["skeleton"] = rig.Name;
            args["create_composite"] = true;
            McpCall inner = new McpCall(call.Tool, args, call.Cancel, (text, done, total) => call.Progress(text, done, total));
            JObject imported = McpImportTools.ImportModel(inner, (scene, plan) =>
            {
                applied = RebindImport.Plan(scene, plan.UnitScale, rig, options);
                if (!applied.Ok) throw new McpError("Can't rebind onto " + rig.Name + ": " + applied.Problem);
                RebindImport.Apply(applied, scene);
                plan.Scale = 1;
            }) as JObject ?? new JObject();
            foreach (string note in inner.Notes) call.Note(note);
            imported["rebind"] = Describe(applied, brief: true);

            //the hit collision, copied into the display model and listed among its animated model's meshes
            string composite = (string)imported["composite"];
            if (composite != null && hit != "none")
            {
                try { imported["hit_collision"] = AddHitCollision(call, composite, rig, hit); }
                catch (McpError e) { call.Note("The model was imported, but no hit collision was added: " + e.Message); }
            }
            call.Note("A Character wears it with display_model '" + (composite ?? "DisplayModel:...").Replace(ModelCompositeBuilder.DisplayModelPrefix, "") + "' and reference_skeleton '" + rig.Name + "' (an NPC archetype: also custom_character_type NONE). Check the materials: a character wants CA_CHARACTER.");
            return imported;
        }

        private static JToken AddHitCollision(McpCall call, string displayModel, Skeleton rig, string hit)
        {
            string fromComposite, fromEntity, description;
            if (hit.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                RebindImport.CollisionSource source = McpEditor.UI(() => RebindImport.HitCollisionParts(McpEditor.RequireLevel(forEditing: false).Level, rig).FirstOrDefault());
                if (source == null) throw new McpError("this level has no hit-collision part skinned to " + rig.Name + " to copy (port a character's DisplayModel or its NPC collision composite, then run copy_entities into " + displayModel + " and list it with set_animated_model, or re-import).");
                fromComposite = McpEditor.UI(() => source.Composite.name);
                fromEntity = McpScript.Id(source.Part.shortGUID);
                description = source.Description;
            }
            else
            {
                string[] parts = hit.Split('|');
                if (parts.Length != 2) throw McpError.Invalid("hit_collision takes 'auto', 'none' or 'composite|entity'.");
                fromComposite = parts[0]; fromEntity = parts[1]; description = hit;
            }
            JObject copied = McpEditorTools.Nested(call, "copy_entities", new JObject() { ["composite"] = fromComposite, ["entities"] = new JArray(fromEntity), ["into"] = displayModel, ["links"] = "none" });
            int listed = McpEditor.UI(() =>
            {
                Composite target = McpScript.FindComposite(McpEditor.RequireCommands(), displayModel);
                int n = RebindImport.ListPartsInAnimatedModel(target);
                Singleton.OnResourceModified?.Invoke();
                return n;
            });
            return new JObject() { ["copied_from"] = description, ["listed_in_animated_model"] = listed, ["new_entities"] = copied["new_ids"] ?? copied["copies"] ?? copied };
        }

        private static JObject Describe(SkinRebind.Result r, bool brief = false)
        {
            JObject o = new JObject() { ["rig"] = r.Target?.Name };
            if (!r.Ok) { o["problem"] = r.Problem; o["warnings"] = new JArray(r.Warnings); return o; }
            o["mirrored"] = r.Mirrored;
            o["size_factor"] = Math.Round(r.Scale, 4);
            o["fit_cm"] = Math.Round(r.Fit * 100, 1);
            if (!brief)
            {
                JObject mapping = new JObject();
                foreach (string role in RigRoles.AllRoles())
                {
                    if (!r.Roles.Has(role)) continue;
                    int bone = RigRoles.TargetBone(r.Target, role);
                    mapping[role] = r.Source.Joints[r.Roles[role]].Name + (bone < 0 ? " (rig has no " + role + ")" : "") + (r.Roles.How.TryGetValue(role, out string how) && how != "name" ? " [" + how + "]" : "");
                }
                o["mapping"] = mapping;
                o["unmapped_parts"] = new JArray(RigRoles.AllRoles().Where(x => !r.Roles.Has(x) && RigRoles.TargetBone(r.Target, x) >= 0 && !x.Contains("ROLL") && !x.EndsWith("3")));
                o["report"] = new JArray(r.Report);
            }
            o["warnings"] = new JArray(r.Warnings);
            return o;
        }
        #endregion
    }
}
