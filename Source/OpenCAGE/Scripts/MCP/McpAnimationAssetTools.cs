using AlienPAK;
using CATHODE;
using CATHODE.Animations;
using CATHODE.Scripting;
using CATHODE.Scripting.Internal;
using CathodeLib;
using Newtonsoft.Json.Linq;
using OpenCAGE.AnimTrees;
using OpenCAGE.Audio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;
using Anim = CathodeLib.Animation;
using STNode = ST.Library.UI.NodeEditor.STNode;
using STNodeOption = ST.Library.UI.NodeEditor.STNodeOption;
using STNodeEditor = ST.Library.UI.NodeEditor.STNodeEditor;
using TreeLayout = OpenCAGE.AnimTreeLayouts.TreeLayout;

namespace OpenCAGE.MCP
{
    /// <summary>
    /// The game's global animation and audio data: ANIMATION.PAK (sets, clips, rigs, blend sets and
    /// animation trees), the AI behaviour trees and the Wwise sounds. None of it belongs to a level, so
    /// reading it needs no level open, and every change is written to the game's files straight away (each
    /// writing tool has a dry_run that says what would change first). None of it is on the undo history.
    /// Also how the open level uses it: which set a placed character really plays from, what a prop can play,
    /// and whether the level's animation entities name clips that exist (check_animations).
    /// </summary>
    internal static class McpAnimationAssetTools
    {
        public static IEnumerable<McpTool> Tools()
        {
            #region Animations
            yield return new McpTool()
            {
                Name = "list_animation_sets",
                Title = "List animation sets",
                Description = "The animation sets in the game's ANIMATION.PAK (no level needed): name, character or environment (prop), the rig it plays on, its contexts and clip count. " +
                    "An ANIMATION_SET parameter (CMD_PlayAnimation, Character.anim_set) takes a set's name; list_animations lists its clips. A context (WEAPON_HANDGUN, CROUCHED...) holds clips the character " +
                    "plays only in that state; '(default)' always plays. Retail also ships ready-made animation composites (find_composites 'Single_Anims': seated, kneeling, leaning idles) to place instead of wiring a clip.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Words the set or rig name must all contain (falls back to the sets holding a clip or context that matches)."),
                    McpSchema.String("kind", "character, environment or all (default).", options: new[] { "character", "environment", "all" }),
                    McpSchema.Limit(100, "sets"),
                    McpSchema.Offset("sets")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListAnimationSets,
            };

            yield return new McpTool()
            {
                Name = "list_animations",
                Title = "List animations",
                Description = "Animation clips in ANIMATION.PAK: the name an ANIMATION parameter takes, its set and context, stored path, authoring rig, frames, length (s), additive and event count. " +
                    "CMD_PlayAnimation plays a clip by its set and name; a clip in a named context plays only while the character is in that state. Give set for one set's clips (context narrows it), " +
                    "or filter to search every set; plays_on keeps only clips that play on a rig. describe_animation shows one clip, preview_animation draws it.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("set", "An animation set's name (list_animation_sets)."),
                    McpSchema.String("context", "Only this context of the set ('default' for the unnamed one). Needs set."),
                    McpSchema.String("filter", "Words the clip's name or stored path must all contain."),
                    McpSchema.String("rig", "Only clips AUTHORED on this rig (list_skeletons) - most character clips are authored on a shared rig such as MALE."),
                    McpSchema.String("plays_on", "Only clips that play on this rig, or on this set's rig: authored on it or retargeted onto it by the game's data (rows say retargeted_onto)."),
                    McpSchema.Limit(100, "clips"),
                    McpSchema.Offset("clips")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListAnimations,
            };

            yield return new McpTool()
            {
                Name = "describe_animation",
                Title = "Describe animation",
                Description = "One clip in full: frames, fps, length, bones, the rig it was authored on and how it retargets onto the rig it plays on, limbs it leaves at rest, additive, " +
                    "and every event marker (sounds with their bone) and metadata setting it carries.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("set", "The animation set.", required: true),
                    McpSchema.String("animation", "The clip's name (or its stored path).", required: true),
                    McpSchema.String("context", "The context, when the name is in more than one."),
                    McpSchema.String("rig", "The rig to judge retargeting against (default: the set's own rig)."),
                    McpSchema.Integer("limit", "At most this many markers, and this many settings (default 200).")),
                ReadOnly = true,
                Idempotent = true,
                Run = DescribeAnimation,
            };

            yield return new McpTool()
            {
                Name = "list_skeletons",
                Title = "List skeletons",
                Description = "The rigs in ANIMATION.PAK: bone count, whether it is an environment (prop) rig, its reference rig and how many sets play on it. " +
                    "With set, ranks rigs by how many of the set's clips were authored on them (export_animations' default); with model, by fit to that open-level model's skin weights. " +
                    "With name, one rig in full: its bones (index, name, parent, rest position in metres and rotation as [x,y,z] degrees, parent-relative) - the names bone_name / bone_to_focus parameters take.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Words the rig name must all contain."),
                    McpSchema.String("name", "One rig, with its bones listed (filter then matches bone names)."),
                    McpSchema.String("set", "Rank rigs for exporting this animation set's clips."),
                    McpSchema.String("model", "Rank rigs by fit to this model of the open level (list_models)."),
                    McpSchema.Limit(100, "rigs, or bones with name", 5000),
                    McpSchema.Offset("rigs, or bones with name")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListSkeletons,
            };

            yield return new McpTool()
            {
                Name = "export_animations",
                Title = "Export animations",
                Description = "Write clips to an .fbx, .glb, .gltf or .dae file against a rig (default: the one most were authored on). mode hold/travel are for viewing, as_stored for editing " +
                    "and re-importing with import_animation. model adds an open-level mesh bound to the rig (hold or travel only); without rig, a skinned mesh's own rig is used and the clips are retargeted onto it. Unreadable clips are skipped. Writes that file, plus a <name>.bin " +
                    "beside a .gltf, and with model a .cs2meta.json sidecar and a '<name> Textures' folder; overwrite covers all of them.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("set", "The animation set.", required: true),
                    McpSchema.Strings("animations", "Clip names to export (default: every clip of context, or of the whole set)."),
                    McpSchema.String("context", "Export every clip in this context of the set."),
                    McpSchema.String("path", "Absolute path to write, ending .fbx, .glb, .gltf or .dae.", required: true),
                    McpSchema.String("rig", "The rig to write against (list_skeletons with set ranks them)."),
                    McpSchema.String("mode", "hold (default): kept on the spot; travel: carried by the root; as_stored: exactly what the clip holds.", options: new[] { "hold", "travel", "as_stored" }),
                    McpSchema.String("model", "A model of the open level (list_models) to export bound to the rig, with its textures. Not with mode as_stored."),
                    McpSchema.Boolean("overwrite", "Replace the file, and the .bin, sidecar or textures folder written beside it, if they exist."),
                    McpSchema.Boolean("allow_large", "Allow an export estimated at over 250 MB.")),
                Run = ExportAnimations,
            };

            yield return new McpTool()
            {
                Name = "import_animation",
                Title = "Import animation",
                Description = "Add an animation from an FBX/glTF/DAE file to an animation set in ANIMATION.PAK (or rebuild an existing clip with replace). A file on another skeleton - an Unreal mannequin or " +
                    "Mixamo/HumanIK rig - is retargeted (converted) onto the rig; one exported from OpenCAGE is matched bone for bone. Written to the game at once, for every level; not undoable " +
                    "(remove_animation takes out a clip imported here). dry_run first: frames, route, matched bones, warnings, whether it builds; with preview a picture of the poses to catch a turned or mirrored clip.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("path", "Absolute path of the model file holding the animation.", required: true),
                    McpSchema.String("set", "The animation set to add it to (list_animation_sets).", required: true),
                    McpSchema.String("name", "The name the set plays it by, which ANIMATION parameters take (default: the file name; for several clips each clip's own name). With replace, the clip to rebuild."),
                    McpSchema.String("stored_path", "Where it is stored (default ANIMATION\\OPENCAGE\\<SET>\\<FILE>, plus _<CLIP> for several clips). Must be unused."),
                    McpSchema.String("rig", "The rig to build it against (default: the rig most of the set's clips use; list_skeletons)."),
                    McpSchema.String("root", "auto (default): keep root motion only if the file moves the root; engine: leave the root to the game; authored: keep it.", options: new[] { "auto", "engine", "authored" }),
                    McpSchema.Number("frame_rate", "Frames per second (default: worked out from the file)."),
                    McpSchema.Boolean("additive", "Layer it over whatever else is playing instead of replacing it."),
                    McpSchema.Any("clip_index", "Which animation in the file, from 0 (default 0); a list of indexes, or 'all', imports several in one write."),
                    McpSchema.String("retarget", "auto (default): convert when the file is on another skeleton (its bone names don't prove it is this rig); always; never (match by name only).", options: new[] { "auto", "always", "never" }),
                    McpSchema.String("context", "File it in this context of the set (e.g. WEAPON_HANDGUN): it then plays only in that state. Default: the set's own unnamed context."),
                    McpSchema.Integer("start_frame", "Keep frames from this one (from 0; default 0)."),
                    McpSchema.Integer("end_frame", "Keep frames up to this one, inclusive (default: the last)."),
                    McpSchema.Boolean("replace", "Rebuild the existing clip 'name' in place: its name, path and every script or tree naming it stay; its events and settings are kept, including the movement measurements and blend anchors locomotion is chosen on (linear_speed, translation, yTotalRotation...: the result's kept_settings), which still describe the old animation. Only a clip with a section (and one metadata instance block) to itself."),
                    McpSchema.Boolean("dry_run", "Read the file and build the clip, reporting what would be imported, changing nothing."),
                    McpSchema.Boolean("preview", "With dry_run (one clip): return a picture of the built clip's poses (stick figure, front and side) with the report as its caption."),
                    McpSchema.Boolean("close_game", "Close a running game first (it holds ANIMATION.PAK open).")),
                Destructive = true,
                Run = ImportAnimation,
            };

            yield return new McpTool()
            {
                Name = "remove_animation",
                Title = "Remove animation",
                Description = "Take a clip that was imported with OpenCAGE (stored under ANIMATION\\OPENCAGE\\) out of its set and ANIMATION.PAK, for every level; not undoable. Refuses while an animation tree, " +
                    "a blend set or an entity of the open level still names it, listing them. dry_run reports without writing.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("set", "The animation set.", required: true),
                    McpSchema.String("animation", "The clip's name.", required: true),
                    McpSchema.String("context", "The context, when the name is in more than one."),
                    McpSchema.Boolean("dry_run", "Check and report, writing nothing."),
                    McpSchema.Boolean("close_game", "Close a running game first.")),
                Destructive = true,
                Run = RemoveAnimation,
            };

            yield return new McpTool()
            {
                Name = "preview_animation",
                Title = "Preview animation",
                Description = "A picture of a clip: stick-figure poses at evenly spaced moments (or given times), from the front and side, drawn on the rig it plays on. No level or viewport needed. " +
                    "The caption gives sanity numbers: height of the head over the feet, how far the root travels and which way the rig faces at the start and end (a 180 degree turn shows here). " +
                    "Characters face +Z in the side and top views' terms; an environment (prop) set draws its rig's bones.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("set", "The animation set.", required: true),
                    McpSchema.String("animation", "The clip's name.", required: true),
                    McpSchema.String("context", "The context, when the name is in more than one."),
                    McpSchema.String("rig", "The rig to draw it on (default: the set's own; list_skeletons)."),
                    McpSchema.Integer("frames", "How many evenly spaced poses (default 4, at most 12)."),
                    McpSchema.Array("times", "Poses at these times in seconds instead.", new JObject() { ["type"] = "number" }),
                    McpSchema.Strings("views", "Any of front, side, top (default front and side)."),
                    McpSchema.String("root_motion", "hold (default): kept on the spot; travel: carried by the root, as it moves in game.", options: new[] { "hold", "travel" }),
                    McpSchema.Integer("max_width", "Scale the picture down to at most this many pixels wide (default 1024).")),
                ReadOnly = true,
                Idempotent = true,
                Run = PreviewAnimation,
            };

            JObject eventAdd = new JObject()
            {
                ["type"] = "object",
                ["properties"] = new JObject()
                {
                    ["time"] = new JObject() { ["type"] = "number", ["description"] = "Seconds from the clip's start." },
                    ["property"] = new JObject() { ["type"] = "string", ["description"] = "The marker's name, e.g. footstep_l, or 'sound' for a plain sound (describe_animation shows retail ones)." },
                    ["sound_event"] = new JObject() { ["type"] = "string", ["description"] = "A SOUND_EVENT to fire at that moment." },
                    ["bone"] = new JObject() { ["type"] = "string", ["description"] = "The bone the sound comes from (list_skeletons name), e.g. LeftFoot." },
                },
                ["required"] = new JArray("time", "property"),
            };
            JObject eventRemove = new JObject()
            {
                ["type"] = "object",
                ["properties"] = new JObject()
                {
                    ["time"] = new JObject() { ["type"] = "number", ["description"] = "Only the marker at this time (within 1 ms)." },
                    ["property"] = new JObject() { ["type"] = "string", ["description"] = "Only markers of this name." },
                },
            };
            yield return new McpTool()
            {
                Name = "edit_animation_events",
                Title = "Edit animation events",
                Description = "Add or remove timed markers on a clip (a footstep, a sound fired from a bone), written to ANIMATION.PAK at once for every level; not undoable. " +
                    "Only clips with a section to themselves; a retail clip also needs allow_retail. describe_animation lists a clip's markers. dry_run reports without writing.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("set", "The animation set.", required: true),
                    McpSchema.String("animation", "The clip's name.", required: true),
                    McpSchema.String("context", "The context, when the name is in more than one."),
                    McpSchema.Array("add", "Markers to add: {time, property, sound_event?, bone?}.", eventAdd),
                    McpSchema.Array("remove", "Markers to remove: {time?, property?} (at least one of them).", eventRemove),
                    McpSchema.Boolean("allow_retail", "Allow editing a clip that ships with the game (it changes for every character playing it)."),
                    McpSchema.Boolean("dry_run", "Check and report, writing nothing."),
                    McpSchema.Boolean("close_game", "Close a running game first.")),
                Destructive = true,
                Run = EditAnimationEvents,
            };

            yield return new McpTool()
            {
                Name = "get_character_animation_profile",
                Title = "Get character animation profile",
                Description = "Which animation set, tree set, reference skeleton and display model a character really uses in the open level, per placement - resolved through the instance chain " +
                    "(placement parameter -> composite variable -> nested instance -> the Character's own value -> default), each with the route it came by - and which sets it can play: " +
                    "its anim_set (contexts and clip counts) and the sets whose clips retarget onto its rig. Give a Character, or an instance of an NPC archetype that holds one.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The composite holding the entity.", required: true),
                    McpSchema.String("entity", "A Character entity, or an instance of a composite that holds one (e.g. an Android_NPC placement).", required: true),
                    McpSchema.Integer("limit", "At most this many placements resolved (default 20).")),
                ReadOnly = true,
                Idempotent = true,
                Run = GetCharacterAnimationProfile,
            };

            yield return new McpTool()
            {
                Name = "describe_animated_prop",
                Title = "Describe animated prop",
                Description = "What a prop composite can play (no edits): its EnvironmentModelReference's animation entry (rig, bones, which ModelReference each bone moves), the environment sets " +
                    "on that rig with their clips, how many placements share it, the PlayEnvironmentAnimations that already play it, and the wiring recipe. list:true lists every animated prop of the open level. " +
                    "An unrigged prop (no entry) is animated with a CAGEAnimation instead (animate_parameters).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "The prop's composite (find_composites), or one holding an instance of it."),
                    McpSchema.Boolean("list", "Every animated prop of the open level instead (filter narrows it)."),
                    McpSchema.String("filter", "With list: words the composite path or rig must all contain."),
                    McpSchema.Integer("limit", "At most this many props, or clips per set (default 50).")),
                ReadOnly = true,
                Idempotent = true,
                Run = DescribeAnimatedProp,
            };

            yield return new McpTool()
            {
                Name = "check_animations",
                Title = "Check animations",
                Description = "Check every CMD_PlayAnimation, CHR_PlaySecondaryAnimation, PlayEnvironmentAnimation and Character (anim_set) in a composite or the whole open level: the set exists and is " +
                    "the right kind, the clip is in it (and in which context), and a prop's set suits its rig. Lists only problems, each with near names. Values fed by a link are not checked.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("composite", "Only this composite (default: every composite of the open level)."),
                    McpSchema.Integer("limit", "At most this many problems (default 100).")),
                ReadOnly = true,
                Idempotent = true,
                Run = CheckAnimations,
            };

            yield return new McpTool()
            {
                Name = "list_camera_clips",
                Title = "List camera clips",
                Description = "The baked cutscene camera clips the open level's CameraPlayAnimation entities play (their data_file), with shot numbers and where they are. These clips can't be listed " +
                    "from the game data or authored in OpenCAGE: a new camera move is a CameraResource whose position a CAGEAnimation keys - create_camera_animation builds one round a room (or through points) wired to play in game; animate_camera_path or animate_parameters key an existing one.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Words the clip path must all contain."),
                    McpSchema.Limit(100, "clips", 2000),
                    McpSchema.Offset("clips")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListCameraClips,
            };
            #endregion

            #region Blend sets
            yield return new McpTool()
            {
                Name = "list_blend_sets",
                Title = "List blend sets",
                Description = "The parametric blend sets in ANIMATION.PAK (what ANIM_2DParametric tree nodes name in BlendSet): key, dimensions, driving properties, clip and blend-point counts, users. " +
                    "Give name for one set in full: its clips (length, mirrored), blend points (position, clip, speed) and the characters and contexts that can use it.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Text in the blend set's name or key, or in one of its clips' names."),
                    McpSchema.String("name", "A blend set's key (e.g. HUMAN_WEAPON_HANDGUN\\aim) or its name if unique: returns that set in full."),
                    McpSchema.Limit(100, "blend sets"),
                    McpSchema.Offset("blend sets")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListBlendSets,
            };

            JObject blendClip = new JObject()
            {
                ["type"] = "object",
                ["properties"] = new JObject()
                {
                    ["index"] = new JObject() { ["type"] = "integer", ["description"] = "The clip's index (list_blend_sets with name)." },
                    ["name"] = new JObject() { ["type"] = "string", ["description"] = "The clip name, as the using character's set names it." },
                    ["duration"] = new JObject() { ["type"] = "number", ["description"] = "Length in seconds." },
                    ["mirrored"] = new JObject() { ["type"] = "boolean" },
                },
                ["required"] = new JArray("index"),
            };
            JObject blendPoint = new JObject()
            {
                ["type"] = "object",
                ["properties"] = new JObject()
                {
                    ["index"] = new JObject() { ["type"] = "integer", ["description"] = "The blend point's index." },
                    ["clip"] = new JObject() { ["description"] = "The clip it plays: its index or name." },
                    ["speed"] = new JObject() { ["type"] = "number", ["description"] = "Play speed." },
                },
                ["required"] = new JArray("index"),
            };
            JObject blendUser = new JObject()
            {
                ["type"] = "object",
                ["properties"] = new JObject()
                {
                    ["character"] = new JObject() { ["type"] = "string", ["description"] = "The character's animation set (e.g. ANDROID)." },
                    ["context"] = new JObject() { ["type"] = "string", ["description"] = "One of its contexts; leave out for the character itself." },
                },
                ["required"] = new JArray("character"),
            };
            yield return new McpTool()
            {
                Name = "edit_blend_set",
                Title = "Edit blend set",
                Description = "Change a blend set's authored half and write ANIMATION.PAK at once (every level, every character sharing the set; not undoable): a clip's name, length (s) or mirroring, " +
                    "which clip a blend point plays and its speed, and which characters or contexts can use the set. Blend-point positions are baked and cannot change. dry_run reports without writing. " +
                    "To change one NPC rather than every character, use script entities instead (e.g. NPC_SetLocomotionTargetSpeed, CHR_LocomotionModifier).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("name", "The blend set's key or unique name.", required: true),
                    McpSchema.Array("clips", "Clip edits: {index, name?, duration?, mirrored?}.", blendClip),
                    McpSchema.Array("points", "Blend point edits: {index, clip? (index or name), speed?}.", blendPoint),
                    McpSchema.Array("add_users", "Give the set to these: {character, context?}.", blendUser),
                    McpSchema.Array("remove_users", "Take the set away from these: {character, context?}.", blendUser),
                    McpSchema.Boolean("dry_run", "Check and report the changes without making them."),
                    McpSchema.Boolean("close_game", "Close a running game first (it holds ANIMATION.PAK open).")),
                Destructive = true,
                Run = EditBlendSet,
            };
            #endregion

            #region Animation trees
            yield return new McpTool()
            {
                Name = "list_anim_trees",
                Title = "List animation trees",
                Description = "The animation trees in ANIMATION.PAK, grouped by tree set (the value an ANIMATION_TREE_SET parameter takes). With no arguments, lists the sets; " +
                    "set lists that set's trees; filter searches tree names in every set. get_anim_tree reads one as a node graph.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("set", "A tree set, e.g. HUMANOID."),
                    McpSchema.String("filter", "Words the tree name must contain."),
                    McpSchema.Limit(200, "trees or tree sets"),
                    McpSchema.Offset("trees or tree sets")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListAnimTrees,
            };

            yield return new McpTool()
            {
                Name = "get_anim_tree",
                Title = "Get animation tree",
                Description = "One animation tree as a node graph: the tree's own settings, then each node's type, fields (with their current values) and links (field -> node), " +
                    "and any flow nodes that nothing parents, which are not saved. The field names and links are what edit_anim_tree takes. " +
                    "layout:true adds where the Animation Tree Editor draws each node (x, y) and any ghosts - extra copies of a node, each drawing some of its links - as edit_anim_tree_layout changes them.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("set", "The tree set (list_anim_trees).", required: true),
                    McpSchema.String("tree", "The tree's name.", required: true),
                    McpSchema.String("filter", "Only nodes whose name contains all these words."),
                    McpSchema.Boolean("fields", "Include each node's field values (default true)."),
                    McpSchema.Boolean("layout", "Include where each node is drawn: x, y, its ghosts and the links each copy draws (default false)."),
                    McpSchema.Limit(150, "nodes", 2000),
                    McpSchema.Offset("nodes")),
                ReadOnly = true,
                Idempotent = true,
                Run = GetAnimTree,
            };

            yield return new McpTool()
            {
                Name = "find_in_anim_trees",
                Title = "Find in animation trees",
                Description = "Search every animation tree's node fields for a value: which leaves play a clip (AnimationName, AnimationPool[i].AnimationName), which nodes use a blend set (BlendSet), " +
                    "a parameter or callback name. Returns tree set, tree, node, node type and field path - what edit_anim_tree takes to change it.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("value", "The value to find, e.g. a clip name (any case).", required: true),
                    McpSchema.String("field", "Only fields whose path holds this, e.g. AnimationName, BlendSet, Parameter (default: any field)."),
                    McpSchema.String("set", "Only this tree set (list_anim_trees)."),
                    McpSchema.Boolean("contains", "Match fields containing all of value's words instead of equal to it."),
                    McpSchema.Integer("limit", "At most this many hits (default 100).")),
                ReadOnly = true,
                Idempotent = true,
                Run = FindInAnimTrees,
            };

            JObject treeOp = new JObject()
            {
                ["type"] = "object",
                ["properties"] = new JObject()
                {
                    ["op"] = new JObject() { ["type"] = "string", ["enum"] = new JArray("set", "rename", "add_node", "remove_node", "link", "unlink") },
                    ["node"] = new JObject() { ["type"] = "string", ["description"] = "The node's name; '@tree' is the tree's own settings." },
                    ["node_type"] = new JObject() { ["type"] = "string", ["description"] = "The node's type, when two nodes share its name." },
                    ["field"] = new JObject() { ["type"] = "string", ["description"] = "set: a field path, e.g. Looping, States[2].Value, Value.ValueType. link/unlink: e.g. Callback, States[0].Node, Children." },
                    ["value"] = new JObject() { ["description"] = "set: the new value (enum by name; flags as a list of names; vectors as [x,y,z])." },
                    ["to"] = new JObject() { ["type"] = "string", ["description"] = "link/unlink: the node linked to." },
                    ["to_type"] = new JObject() { ["type"] = "string", ["description"] = "The linked node's type, when two nodes share its name." },
                    ["type"] = new JObject() { ["type"] = "string", ["description"] = "add_node: the node type.", ["enum"] = new JArray(Enum.GetNames(typeof(NodeType)).Where(o => o != NodeType.ANIM_Tree_Top_Level.ToString()).OrderBy(o => o)) },
                    ["name"] = new JObject() { ["type"] = "string", ["description"] = "add_node: the new node's name (default: made from its type). rename: the new name." },
                    ["x"] = new JObject() { ["type"] = "integer", ["description"] = "add_node: where the Animation Tree Editor draws it (with y; default: beside what it links to)." },
                    ["y"] = new JObject() { ["type"] = "integer", ["description"] = "add_node: where the Animation Tree Editor draws it (with x)." },
                },
                ["required"] = new JArray("op"),
            };
            yield return new McpTool()
            {
                Name = "edit_anim_tree",
                Title = "Edit animation tree",
                Description = "Edit an animation tree and write ANIMATION.PAK at once (every level; not undoable). ops run in order, all or none: set a field, rename, add_node, remove_node, " +
                    "link or unlink a node field (flow links also parent the node, so it is saved). Refuses while the game runs unless close_game. dry_run checks without writing. " +
                    "A tree with a stored layout keeps it (renamed nodes keep their place, removed ones and their ghosts go, added ones go at x,y or beside what they link to); the Animation Tree Editor redraws the tree if it shows it.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("set", "The tree set.", required: true),
                    McpSchema.String("tree", "The tree's name.", required: true),
                    McpSchema.Array("ops", "e.g. {op:'set',node:'Walk',field:'Looping',value:true}, {op:'link',node:'Sel',field:'States[0].Node',to:'Walk'}.", treeOp, required: true),
                    McpSchema.Boolean("dry_run", "Check the ops against a copy and report, writing nothing."),
                    McpSchema.Boolean("close_game", "Close a running game first, as the editor's own Save does.")),
                Destructive = true,
                Run = EditAnimTree,
            };

            JObject layoutNode = new JObject()
            {
                ["type"] = "object",
                ["properties"] = new JObject()
                {
                    ["node"] = new JObject() { ["type"] = "string", ["description"] = "The node's name; '@tree' is the tree's own node." },
                    ["node_type"] = new JObject() { ["type"] = "string", ["description"] = "The node's type, when two nodes share its name." },
                    ["ghost"] = new JObject() { ["type"] = "integer", ["description"] = "Which copy (0 = the first, default)." },
                    ["x"] = new JObject() { ["type"] = "integer", ["description"] = "Where to put it (with y)." },
                    ["y"] = new JObject() { ["type"] = "integer" },
                    ["dx"] = new JObject() { ["type"] = "integer", ["description"] = "Or move it by this much (with dy)." },
                    ["dy"] = new JObject() { ["type"] = "integer" },
                },
                ["required"] = new JArray("node"),
            };
            yield return new McpTool()
            {
                Name = "edit_anim_tree_layout",
                Title = "Lay out an animation tree",
                Description = "Change how the Animation Tree Editor draws a tree, kept inside ANIMATION.PAK with the trees (so a game file check or a mod's PAK takes the layouts with the trees) and written at once with it " +
                    "(not undoable; the game never reads the layouts - while it runs and holds the PAK open, the change waits for the PAK's next write unless close_game, " +
                    "and while the tree's set has changes not saved yet it waits to be written with them). " +
                    "arrange: lay the whole tree out afresh - flow left to right, each value above what reads it, and a value read far from where it sits given a ghost (another copy of it) beside each distant reader. " +
                    "reset: forget the stored layout, so the tree is laid out automatically whenever it opens. move: nodes [{node, ghost?, x, y} or {node, dx, dy}]. " +
                    "add_ghost: another copy of 'node' (at x,y, else beside what it draws), drawing its links with the nodes named in 'links'. move_links: those links onto copy 'ghost'. " +
                    "remove_ghost: copy 'ghost' goes, its links back on the first copy. The editor shows the change if the tree is open there. get_anim_tree with layout:true reads it back.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("set", "The tree set.", required: true),
                    McpSchema.String("tree", "The tree's name.", required: true),
                    McpSchema.String("action", "What to do.", required: true, options: _treeLayoutActions),
                    McpSchema.Array("nodes", "move: the nodes to move, e.g. {node:'Walk', x:400, y:120} or {node:'speed', ghost:1, dx:0, dy:-80}.", layoutNode),
                    McpSchema.String("node", "add_ghost / move_links / remove_ghost: the node."),
                    McpSchema.String("node_type", "Its type, when two nodes share its name."),
                    McpSchema.Integer("ghost", "remove_ghost: the copy to remove (1 = the first ghost). move_links: the copy the links move to."),
                    McpSchema.Integer("x", "add_ghost: where to draw the ghost (with y)."),
                    McpSchema.Integer("y", "add_ghost: where to draw the ghost (with x)."),
                    McpSchema.Strings("links", "add_ghost / move_links: the nodes at the other end of the links to draw from that copy (every link between the two moves); 'name:TYPE' when several nodes share a name."),
                    McpSchema.Boolean("dry_run", "Work it out and report, storing nothing."),
                    McpSchema.Boolean("close_game", "Close a running game first, so ANIMATION.PAK can be written now (else the layout waits for its next write).")),
                Destructive = true,
                Run = EditAnimTreeLayout,
            };

            yield return new McpTool()
            {
                Name = "open_anim_tree",
                Title = "Open animation tree",
                Description = "Show an animation tree in the Animation Tree Editor (opening the editor if it is closed), drawn as its stored layout has it or laid out automatically. capture_anim_tree pictures a tree.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("set", "The tree set (list_anim_trees).", required: true),
                    McpSchema.String("tree", "The tree's name.", required: true)),
                Idempotent = true,
                Run = OpenAnimTree,
            };

            yield return new McpTool()
            {
                Name = "capture_anim_tree",
                Title = "Picture an animation tree",
                Description = "A picture of an animation tree as the Animation Tree Editor draws it (its stored layout, or the automatic one), whether or not the editor is open. 'node' pictures the area around one node.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("set", "The tree set.", required: true),
                    McpSchema.String("tree", "The tree's name.", required: true),
                    McpSchema.String("node", "Picture the area around this node (and its ghosts) only."),
                    McpSchema.String("node_type", "Its type, when two nodes share its name."),
                    McpSchema.Integer("max_width", "Widest the picture may be, in pixels (default 1600, at most 4096).")),
                ReadOnly = true,
                Idempotent = true,
                Run = CaptureAnimTree,
            };
            #endregion

            #region Behaviour trees
            yield return new McpTool()
            {
                Name = "get_behaviour_tree",
                Title = "Get behaviour tree",
                Description = "The AI behaviour trees in DATA/BINARY_BEHAVIOR/_DIRECTORY_CONTENTS.BML (what a character class's Behavior_Tree attribute names). Without name, lists them; with name " +
                    "(or class, a character class whose tree to open), returns its XML (Brainiac Node/Connector elements) - or with outline an indented summary - the trees it references and those " +
                    "referencing it, the character classes that run it and whether the open level lists it. catalogue:true lists every node Class used across the trees with its attributes and sample values.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("name", "A behaviour tree, e.g. NPC_cover."),
                    McpSchema.String("class", "A character class (e.g. SECURITY_GUARD): open the root tree it runs."),
                    McpSchema.String("filter", "Without name: words the tree names contain; with catalogue: words the node Class contains."),
                    McpSchema.String("xpath", "With name: only these nodes, by an XPath relative to the tree (e.g. .//Node[@Class='LegendPlugin.Nodes.ActionSuccess'])."),
                    McpSchema.Boolean("outline", "With name: an indented outline (Class and attributes, connectors in brackets) instead of the XML."),
                    McpSchema.Integer("depth", "With outline: nodes this deep at most (default 12)."),
                    McpSchema.Boolean("catalogue", "Every node Class across the trees with counts, its attributes (with sample values) and connectors - the vocabulary edit_behaviour_tree takes."),
                    McpSchema.Integer("max_chars", "Cut the XML or outline at this many characters (default 60000)."),
                    McpSchema.Integer("limit", "At most this many trees, matches or classes (default 200).")),
                ReadOnly = true,
                Idempotent = true,
                Run = GetBehaviourTree,
            };

            JObject behaviourOp = new JObject()
            {
                ["type"] = "object",
                ["properties"] = new JObject()
                {
                    ["op"] = new JObject() { ["type"] = "string", ["enum"] = new JArray("set_attribute", "remove_attribute", "insert_xml", "remove") },
                    ["xpath"] = new JObject() { ["type"] = "string", ["description"] = "The node(s) to change, relative to the tree; for insert_xml, the one parent to insert into." },
                    ["attribute"] = new JObject() { ["type"] = "string" },
                    ["value"] = new JObject() { ["type"] = "string" },
                    ["xml"] = new JObject() { ["type"] = "string", ["description"] = "insert_xml: the element(s) to insert." },
                    ["index"] = new JObject() { ["type"] = "integer", ["description"] = "insert_xml: position among the parent's child elements (default: last)." },
                    ["expect"] = new JObject() { ["type"] = "integer", ["description"] = "Refuse unless the XPath selects exactly this many nodes." },
                },
                ["required"] = new JArray("op", "xpath"),
            };
            yield return new McpTool()
            {
                Name = "edit_behaviour_tree",
                Title = "Edit behaviour tree",
                Description = "Edit an AI behaviour tree in the game's _DIRECTORY_CONTENTS.BML, written at once (every level; not undoable; reset_configs restores vanilla). ops run in order, all or none. " +
                    "xml replaces the whole tree; create adds a new tree. Node classes or attributes no other tree uses are reported as unrecognised (get_behaviour_tree catalogue:true lists the known ones). " +
                    "A tree runs for every character class using it (get_behaviour_tree used_by_classes); to change one NPC, give it another attribute set (Character attribute_set) instead. " +
                    "Refuses while the game or the Behaviour Tree Editor runs unless close_game. dry_run checks without writing.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("name", "The behaviour tree.", required: true),
                    McpSchema.Array("ops", "{op:'set_attribute',xpath,attribute,value} | {op:'remove_attribute',xpath,attribute} | {op:'insert_xml',xpath,xml,index?} | {op:'remove',xpath}.", behaviourOp),
                    McpSchema.String("xml", "Replace the whole tree with this XML (its root is <Behavior>)."),
                    McpSchema.Boolean("create", "Add a new tree called name, from xml."),
                    McpSchema.Boolean("dry_run", "Check the change and report it, writing nothing."),
                    McpSchema.Boolean("close_game", "Close the game and the Behaviour Tree Editor first (that editor would overwrite this file when it saves).")),
                Destructive = true,
                Run = EditBehaviourTree,
            };
            #endregion

            #region Sounds
            yield return new McpTool()
            {
                Name = "list_sound_banks",
                Title = "List sound banks",
                Description = "The game's soundbanks, from the sound data every level carries (the same in all of them - it declares the whole game, not what this level loads), with how many sound events " +
                    "each holds, whether its .bnk ships and whether it is permanently loaded. Give bank to list its events. An event plays only while a bank declaring it is loaded: describe_sound_event " +
                    "says what loads one in the open level (permanent banks, SoundLoadBank entities).",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Words the bank name contains."),
                    McpSchema.String("bank", "One bank: list the sound events it declares."),
                    McpSchema.Limit(200, "banks, or events with bank", 5000),
                    McpSchema.Offset("banks, or events with bank")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListSoundBanks,
            };

            yield return new McpTool()
            {
                Name = "describe_sound_event",
                Title = "Describe sound event",
                Description = "What a sound event (a SOUND_EVENT value) plays: the outcome, or why it plays nothing; the banks declaring it and whether one is loaded in the open level (permanent, or by " +
                    "which SoundLoadBank entities, and the level's SOUNDLOADZONES list); its audible range (max_attenuation_m), the Stop_ event that ends it and whether it likely loops; the open " +
                    "level's entities playing it (examples to copy); and each variation (random or switch take) with its container path, bank, streaming, file and copies. decode adds lengths and formats. " +
                    "A dialogue event's lines: list_dialogue_lines.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("event", "The sound event's name.", required: true),
                    McpSchema.Boolean("decode", "Decode each variation for its length and format (slower)."),
                    McpSchema.Integer("limit", "At most this many variations, and entities playing it (default 50).")),
                ReadOnly = true,
                Idempotent = true,
                Run = DescribeSoundEvent,
            };

            yield return new McpTool()
            {
                Name = "export_sound",
                Title = "Export sound",
                Description = "Decode one variation of a sound event to a 16-bit PCM .wav (e.g. to edit before replace_sound). Give the event and its variation index, or a source_id from " +
                    "describe_sound_event. Sounds longer than 300 s are cut short. Writes only the .wav.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("event", "The sound event."),
                    McpSchema.Integer("variation", "The variation's index in describe_sound_event (default 0)."),
                    McpSchema.String("source_id", "The audio's source id, instead of a variation index."),
                    McpSchema.String("path", "Absolute path of the .wav to write.", required: true),
                    McpSchema.Boolean("overwrite", "Replace the file if it exists.")),
                Run = ExportSound,
            };

            yield return new McpTool()
            {
                Name = "replace_sound",
                Title = "Replace sound",
                Description = "Replace a sound event's audio with a mono or stereo .wav, encoded to the original's codec, in every bank and package that ships a copy. One variation by default; " +
                    "variations takes several or 'all' (a footstep's switch takes are per surface - check container_path in describe_sound_event). gain_db, normalize_db and trims shape the .wav first. " +
                    "Writes the game's sound files at once for every level that plays the event; not undoable (only the mod baseline or verifying the game restores them). dry_run reports without writing.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("event", "The sound event."),
                    McpSchema.Integer("variation", "The variation's index in describe_sound_event (default 0)."),
                    McpSchema.Any("variations", "Several variation indexes, e.g. [0, 2], or 'all'. Each distinct audio is replaced once."),
                    McpSchema.String("source_id", "The audio's source id, instead of a variation index."),
                    McpSchema.String("wav_path", "Absolute path of the .wav to use.", required: true),
                    McpSchema.Number("quality", "Vorbis quality from 0 to 1 (default 0.6; raised if the game's decoder needs it)."),
                    McpSchema.Number("gain_db", "Decibels added to the .wav before encoding (negative is quieter)."),
                    McpSchema.Number("normalize_db", "Scale the .wav so its peak sits at this many dBFS, e.g. -1 (replaces gain_db)."),
                    McpSchema.Number("trim_start", "Seconds cut from the start of the .wav."),
                    McpSchema.Number("trim_end", "Seconds cut from the end of the .wav."),
                    McpSchema.Boolean("dry_run", "Encode and report what would be written, writing nothing."),
                    McpSchema.Boolean("close_game", "Close a running game first (it holds the sound files open).")),
                Destructive = true,
                Run = ReplaceSound,
            };
            #endregion
        }

        #region Shared helpers
        private static Anim RequireAnimations()
        {
            Anim animations = Singleton.Animations;
            if (animations == null || !animations.Loaded)
                throw new McpError("The game's ANIMATION.PAK is not loaded (it is missing or unreadable in this install; verifying the game files restores it).");
            return animations;
        }

        private static int Limit(McpCall call, int fallback, int most = 1000)
        {
            int limit = call.Int("limit", fallback);
            if (limit < 1) throw new McpError("'limit' must be at least 1.");
            return Math.Min(limit, most);
        }

        private static string[] Words(string text) => (text ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        private static bool Holds(string value, string search) => value != null && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        private static bool AllWords(string[] words, params string[] texts) => words.All(w => texts.Any(t => Holds(t, w)));
        private static bool Same(string a, string b) => string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);
        private static double Round(double value, int digits) => double.IsNaN(value) || double.IsInfinity(value) ? 0 : Math.Round(value, digits);
        private static double FloatJson(float value) => double.Parse(value.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        /// <summary>A file path argument, which has to be absolute: a relative one would land wherever OpenCAGE happens to be running from.</summary>
        private static string AbsolutePath(McpCall call, string name, bool required = true)
        {
            string path = call.Str(name, required);
            if (path == null) return null;
            path = path.Trim().Trim('"');
            bool drive = path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/');
            bool share = path.StartsWith("\\\\") || path.StartsWith("//");
            if (!drive && !share)
                throw new McpError("'" + name + "' must be an absolute path, such as C:\\Mods\\file.ext (a relative path would resolve against OpenCAGE's own folder).");
            try { return Path.GetFullPath(path); }
            catch (Exception e) { throw new McpError("'" + name + "' is not a usable path: " + e.Message); }
        }

        internal static string Relative(string file)
        {
            if (string.IsNullOrEmpty(file)) return file;
            string root = (Singleton.PathToAI ?? "").Replace('/', '\\').TrimEnd('\\');
            string full = file.Replace('/', '\\');
            return root.Length != 0 && full.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length + 1) : full;
        }

        private static bool ProcessRunning(string name)
        {
            System.Diagnostics.Process[] found = System.Diagnostics.Process.GetProcessesByName(name);
            foreach (System.Diagnostics.Process process in found) process.Dispose();
            return found.Length != 0;
        }

        /// <summary>This install's game (an AI.exe in Singleton.PathToAI): another install's has its own copy of the global files.</summary>
        private static bool GameRunning() => EditorUtils.ThisInstallsGameRunning();

        /// <summary>Close this install's game (and <paramref name="extra"/> processes), saying what was closed.</summary>
        private static void CloseGame(McpCall call, List<string> extra = null)
        {
            List<string> closed = EditorUtils.CloseAI(extra, thisInstallOnly: true);
            call.Note(closed.Count == 0 ? "Nothing needed closing." : "Closed " + string.Join(", ", closed) + " so the file could be written.");
        }

        /* Changes made in memory whose write failed: the next whole-PAK write (Animation.Save) takes them out, but
         * edit_anim_tree and the tree editor write only their tree set, so every global write reports them. */
        private static readonly List<string> _pendingPak = new List<string>();

        /// <summary>
        /// Write ANIMATION.PAK as the animation browser does, capturing the mod baseline first. UI thread.
        /// It serialises everything parsed - every tree set too, so an Animation Tree Editor window's unsaved edits go out with it.
        /// </summary>
        private static void WriteAnimationPak(McpCall call, Anim animations, string inMemory)
        {
            if (Application.OpenForms.OfType<AnimTreeEditor>().Any())
                call.Note("The Animation Tree Editor is open: this write also saved any tree edits it held unsaved (ANIMATION.PAK is written whole).");
            //Every tree goes out, unsaved edits and all: they must load again, and their node layouts go in with them
            string refused = AnimationPakWrite.BeforeWholeSave(animations);
            if (refused != null)
            {
                //A broken tree can be put right and the next write takes this out; a PAK changed on disk can't be written until a restart
                if (AnimationPakWrite.ChangedOnDisk(animations.PAK.Filepath) == null)
                    _pendingPak.Add(inMemory);
                throw new McpError("ANIMATION.PAK was not written: " + refused + " " + inMemory + " kept in memory only.");
            }
            bool saved;
            string why = null;
            try { saved = animations.Save(); }
            catch (Exception e) { saved = false; why = e.Message; }
            finally { AnimationPakWrite.Written(animations); }
            if (!saved)
            {
                _pendingPak.Add(inMemory);
                throw new McpError("ANIMATION.PAK could not be written (" + (why ?? "is the game running, or the file read-only?") + "). " + inMemory + " kept in memory only: the next import_animation, remove_animation, "
                    + "edit_blend_set or edit_animation_events writes it (edit_anim_tree and the tree editor's Save write only their trees, not this). Close the game (close_game) and retry.");
            }
            if (_pendingPak.Count != 0)
            {
                call.Note("This write also saved changes an earlier failed write had left in memory: " + string.Join("; ", _pendingPak) + ".");
                _pendingPak.Clear();
            }
        }

        /// <summary>Refuse before anything changes in memory when ANIMATION.PAK has changed on disk since it was loaded: it couldn't be written back.</summary>
        private static void RefuseIfAnimationPakChanged()
        {
            //Not a step on the UI thread of its own: only the file on disk is looked at
            string changed = AnimationPakWrite.ChangedOnDisk(Singleton.Animations?.PAK?.Filepath);
            if (changed != null)
                throw new McpError(McpErrorCodes.Refused, changed + " Nothing was changed.");
        }

        /// <summary>Refuse, or close the game when asked, before a global file is written: the game holds them open.</summary>
        private static void CloseOrRefuse(McpCall call, string why)
        {
            if (!GameRunning()) return;
            if (!call.Bool("close_game"))
                throw new McpError(McpErrorCodes.Refused, "Alien: Isolation is running, and " + why + ". Pass close_game:true to close it first (as the editor's Save does), or close it with the close_game tool.");
            CloseGame(call);
        }

        private static void CheckKeys(JObject item, string what, params string[] allowed)
        {
            List<string> unknown = item.Properties().Select(o => o.Name).Where(o => !allowed.Contains(o)).ToList();
            if (unknown.Count != 0)
                throw new McpError(what + " has unknown key" + (unknown.Count > 1 ? "s " : " ") + string.Join(", ", unknown.Select(o => "'" + o + "'")) + "; it takes " + string.Join(", ", allowed) + ".");
        }

        private static string ItemStr(JObject item, string key)
        {
            JToken token = item[key];
            if (token == null || token.Type == JTokenType.Null) return null;
            return token.Type == JTokenType.String ? (string)token : token.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <summary>Anything a metadata argument or marker can hold, as JSON.</summary>
        private static JToken ValueJson(object value)
        {
            if (value == null) return JValue.CreateNull();
            if (value is float f) return new JValue(FloatJson(f));
            if (value is double d) return new JValue(d);
            if (value is System.Numerics.Vector3 v) return new JArray(FloatJson(v.X), FloatJson(v.Y), FloatJson(v.Z));
            if (value is Enum) return new JValue(value.ToString());
            if (value is string || value is bool || value is int || value is uint || value is long || value is ulong || value is short || value is ushort || value is byte || value is sbyte)
                return new JValue(value);
            return new JValue(value.ToString());
        }
        #endregion

        #region Animation sets and clips
        private static bool IsCharacter(Anim.AnimationSet set) => set.Kind == Anim.AnimationKind.Character;
        private static bool IsUnnamed(Anim.AnimationContext context) => (context.Name ?? "").Trim().Length == 0;
        private static string ContextName(Anim.AnimationContext context) => IsUnnamed(context) ? "(default)" : context.Name;
        private static string ClipName(Anim.ClipReference clip) => string.IsNullOrEmpty(clip.Name) ? Path.GetFileName(clip.Path ?? "") : clip.Name;

        private static Anim.AnimationSet FindSet(Anim animations, string name)
        {
            name = (name ?? "").Trim();
            Anim.AnimationSet set = animations.GetSet(name);
            if (set != null) return set;
            throw new McpError("There is no animation set '" + name + "'" + McpGlobalAssetChecks.DidYouMean(animations.Sets.Select(o => o.Name), name, " (list_animation_sets lists them)."));
        }

        private static Anim.AnimationContext FindContext(Anim.AnimationSet set, string name)
        {
            string wanted = (name ?? "").Trim();
            bool unnamed = wanted.Length == 0 || Same(wanted, "default") || Same(wanted, "(default)") || Same(wanted, "(always available)");
            Anim.AnimationContext context = set.Contexts.FirstOrDefault(o => !IsUnnamed(o) && Same(o.Name, wanted))
                ?? (unnamed ? set.Contexts.FirstOrDefault(IsUnnamed) : null);
            if (context == null)
                throw new McpError(set.Name + " has no context '" + name + "'. Its contexts are: " + string.Join(", ", set.Contexts.Where(o => o.Clips.Count != 0).Select(ContextName)) + ".");
            return context;
        }

        private static Anim.ClipReference FindClip(Anim.AnimationSet set, string name, string context)
        {
            name = (name ?? "").Trim();
            List<Anim.ClipReference> clips = (context == null ? set.Contexts : new List<Anim.AnimationContext>() { FindContext(set, context) }).SelectMany(o => o.Clips).ToList();
            Anim.ClipReference clip = clips.FirstOrDefault(o => Same(o.Name, name))
                ?? clips.FirstOrDefault(o => Same(o.Path, name))
                ?? clips.FirstOrDefault(o => string.IsNullOrEmpty(o.Name) && Same(Path.GetFileName(o.Path ?? ""), name));
            if (clip != null) return clip;
            string elsewhere = context == null ? null : set.Contexts.SelectMany(o => o.Clips).Where(o => Same(o.Name, name)).Select(o => ContextName(o.Context)).FirstOrDefault();
            if (elsewhere != null)
                throw new McpError(set.Name + "'s " + context + " context has no animation '" + name + "'; it is in the " + elsewhere + " context.");
            throw new McpError(set.Name + (context == null ? "" : " (" + context + ")") + " has no animation '" + name + "'"
                + McpGlobalAssetChecks.DidYouMean(clips.Select(ClipName), name, " (list_animations set " + set.Name + " lists them)."));
        }

        private static JObject SetRow(Anim.AnimationSet set)
        {
            return new JObject()
            {
                ["name"] = set.Name,
                ["kind"] = IsCharacter(set) ? "character" : "environment",
                ["rig"] = set.Skeleton,
                ["clips"] = set.ClipCount,
                ["contexts"] = new JArray(set.Contexts.Where(o => o.Clips.Count != 0).Select(ContextName)),
            };
        }

        private static JObject ClipRow(Anim.ClipReference clip)
        {
            JObject row = new JObject() { ["name"] = ClipName(clip) };
            if (clip.Context != null)
            {
                row["set"] = clip.Context.Set?.Name;
                row["context"] = ContextName(clip.Context);
            }
            row["path"] = clip.Path;
            HavokPackfile.AnimationClip animation = clip.Animation;
            if (animation == null || animation.FrameCount <= 0)
            {
                row["playable"] = false;
                row["problem"] = clip.Section == null ? "its section is missing from ANIMATION.PAK" : "it could not be read out of " + Path.GetFileName(clip.Section.Filepath);
                return row;
            }
            row["playable"] = true;
            row["authored_on"] = animation.SkeletonName;
            row["frames"] = animation.FrameCount;
            row["duration"] = Round(animation.Duration, 3);
            if (animation.Additive) row["additive"] = true;
            int events = clip.Markers.Count;
            if (events != 0) row["events"] = events;
            return row;
        }

        private static object ListAnimationSets(McpCall call)
        {
            string filter = (call.Str("filter") ?? "").Trim();
            string kind = (call.Str("kind") ?? "all").Trim().ToLowerInvariant();
            if (kind != "all" && kind != "character" && kind != "environment")
                throw new McpError("'kind' is character, environment or all.");
            using (McpEditorTools.Heartbeat(call, "Reading ANIMATION.PAK"))
                return McpEditor.UI(() =>
                {
                    Anim animations = RequireAnimations();
                    List<Anim.AnimationSet> candidates = animations.Sets
                        .Where(o => kind == "all" || (kind == "character") == IsCharacter(o))
                        .OrderBy(o => (o.Name ?? "").StartsWith("#") ? 1 : 0).ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    //As the browser searches: names and rigs first, and only the clips inside when nothing is called that
                    List<Anim.AnimationSet> listed = candidates;
                    bool widened = false;
                    string[] words = Words(filter);
                    if (filter.Length != 0)
                    {
                        listed = candidates.Where(o => AllWords(words, o.Name, o.Skeleton)).ToList();
                        if (listed.Count == 0)
                        {
                            listed = candidates.Where(o => o.Contexts.Any(c => c != null && (AllWords(words, c.Name)
                                || (c.Clips != null && c.Clips.Any(x => x != null && AllWords(words, x.Name, x.Path)))))).ToList();
                            widened = listed.Count != 0;
                        }
                        if (listed.Count == 0)
                            call.Note("Nothing matches '" + filter + "'" + McpGlobalAssetChecks.DidYouMean(candidates.Select(o => o.Name), filter, "."));
                    }
                    if (widened)
                        call.Note("No set or rig is called '" + filter + "', so these are the sets holding an animation that matches it.");
                    JObject result = new JObject();
                    McpPaging.Page(call, listed, result, "sets", SetRow, 100);
                    if (animations.Failures.Count != 0)
                        result["unreadable_files"] = animations.Failures.Count;
                    return result;
                });
        }

        private static object ListAnimations(McpCall call)
        {
            string setName = call.Str("set");
            string contextName = call.Str("context");
            string rig = call.Str("rig");
            string playsOn = call.Str("plays_on");
            string[] words = Words(call.Str("filter"));
            if (contextName != null && setName == null)
                throw new McpError("'context' needs 'set'.");

            using (McpEditorTools.Heartbeat(call, "Reading ANIMATION.PAK"))
                return McpEditor.UI(() =>
                {
                    Anim animations = RequireAnimations();

                    //plays_on: a rig, or a set/character whose rig it means; a clip plays there if authored on it or retargeted onto it
                    string target = null;
                    Dictionary<string, bool> retargets = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                    if (playsOn != null)
                    {
                        playsOn = playsOn.Trim();
                        Anim.AnimationSet named = animations.GetSet(playsOn);
                        if (RigSkeleton(animations, playsOn) != null) target = RigSkeleton(animations, playsOn).Name;
                        else if (named != null && !string.IsNullOrEmpty(named.Skeleton)) target = named.Skeleton;
                        else throw new McpError("'" + playsOn + "' is neither a rig nor an animation set" + McpGlobalAssetChecks.DidYouMean(animations.Sets.Select(o => o.Name).Concat(animations.Skeletons.Select(o => o.ToString())), playsOn, " (list_skeletons and list_animation_sets list them)."));
                        if (!Same(target, playsOn)) call.Note(playsOn + " is a set; its rig is " + target + ".");
                        if (RigSkeleton(animations, target) == null)
                            call.Note("The rig '" + target + "' is not in ANIMATION.PAK, so only clips authored on it are listed (the game's data can't say what retargets onto it). Its own set plays all of its clips: list_animations set=" + (named?.Name ?? target) + ".");
                    }
                    bool PlaysOn(Anim.ClipReference clip, out bool moved)
                    {
                        moved = false;
                        string authored = clip.Skeleton;
                        if (string.IsNullOrEmpty(authored)) return false;
                        if (Same(authored, target)) return true;
                        if (!retargets.TryGetValue(authored, out bool reaches))
                            retargets[authored] = reaches = RigSkeleton(animations, target) != null && Retargeter.Between(animations, authored, target) != null;
                        moved = reaches;
                        return reaches;
                    }

                    IEnumerable<Anim.AnimationContext> contexts;
                    if (setName != null)
                    {
                        Anim.AnimationSet set = FindSet(animations, setName);
                        contexts = contextName == null ? set.Contexts : new List<Anim.AnimationContext>() { FindContext(set, contextName) };
                    }
                    else
                        contexts = animations.Sets.SelectMany(o => o.Contexts);

                    List<Anim.ClipReference> clips = new List<Anim.ClipReference>();
                    HashSet<Anim.ClipReference> retargeted = new HashSet<Anim.ClipReference>();
                    int nameMatches = 0;
                    foreach (Anim.AnimationContext context in contexts)
                        foreach (Anim.ClipReference clip in context.Clips.Where(o => o != null).OrderBy(o => o.Name ?? "", StringComparer.OrdinalIgnoreCase))
                        {
                            if (clip == null || !AllWords(words, clip.Name, clip.Path)) continue;
                            nameMatches++;
                            //The section names the rigs it needs cheaply; only a clip that might be on this one is decoded to be sure
                            if (rig != null)
                            {
                                List<string> needs = clip.Section?.SkeletonDependencies;
                                if (needs != null && needs.Count != 0 && !needs.Any(o => Same(o, rig))) continue;
                                if (!Same(clip.Skeleton, rig)) continue;
                            }
                            if (target != null)
                            {
                                if (!PlaysOn(clip, out bool moved)) continue;
                                if (moved) retargeted.Add(clip);
                            }
                            clips.Add(clip);
                        }

                    if (nameMatches == 0 && words.Length != 0)
                        call.Note("No clip's name or path holds '" + string.Join(" ", words) + "'" + McpGlobalAssetChecks.DidYouMean(contexts.SelectMany(o => o.Clips).Where(o => o != null).Select(ClipName), string.Join(" ", words), "."));
                    else if (clips.Count == 0 && nameMatches != 0)
                        call.Note(nameMatches + " clips match the filter, but none " + (rig != null ? "is authored on " + rig : "plays on " + target) + ".");
                    JObject result = new JObject();
                    McpPaging.Page(call, clips, result, "animations", o =>
                    {
                        JObject row = ClipRow(o);
                        if (retargeted.Contains(o)) row["retargeted_onto"] = target;
                        return row;
                    }, 100);
                    if (target != null) result["plays_on"] = target;
                    return result;
                });
        }

        /// <summary>
        /// The rig a skinned mesh binds to: its own (<see cref="Anim.RigFor"/>), or the rig already in hand when the mesh
        /// fits that about as well (<see cref="Anim.KeepsRig"/>). Men's trousers skinned to MALE's numbering fit a hundred
        /// rigs, a female one closest, and that is no more theirs than MALE.
        /// </summary>
        private static Skeleton MeshRig(Anim animations, Models.CS2 model, Skeleton inHand, params string[] prefer)
        {
            Skeleton own = model == null ? null : animations.RigFor(model, prefer);
            if (own == null || inHand == null) return own;
            return Anim.KeepsRig(model, inHand, own) ? inHand : own;
        }

        /// <summary>A rig's bones by its name: through the skeleton index first, as the importer reads it, then any loaded rig of that name.</summary>
        private static Skeleton RigSkeleton(Anim animations, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Skeleton skeleton = animations.GetSkeleton(name)?.Skeleton;
            if (skeleton != null && skeleton.Loaded && skeleton.Bones.Count != 0) return skeleton;
            return animations.Skeletons.Select(o => o.Skeleton ?? o.Skeleton64).FirstOrDefault(o => o != null && Same(o.Name, name));
        }

        private static object DescribeAnimation(McpCall call)
        {
            string setName = call.Str("set", required: true);
            string clipName = call.Str("animation", required: true);
            string contextName = call.Str("context");
            string rigName = call.Str("rig");
            int limit = Limit(call, 200, 5000);

            using (McpEditorTools.Heartbeat(call, "Reading ANIMATION.PAK"))
                return McpEditor.UI(() =>
                {
                    Anim animations = RequireAnimations();
                    Anim.AnimationSet set = FindSet(animations, setName);
                    Anim.ClipReference clip = FindClip(set, clipName, contextName);
                    JObject result = ClipRow(clip);
                    result["contexts"] = new JArray(set.Contexts.Where(o => o.Clips.Any(c => Same(c.Name, clip.Name))).Select(ContextName));
                    string label = clip.Label;
                    if (!string.IsNullOrEmpty(label) && !Same(label, clip.Path)) result["label"] = label;

                    HavokPackfile.AnimationClip animation = clip.Animation;
                    List<string> warnings = new List<string>();
                    if (animation != null && animation.FrameCount > 0)
                    {
                        result["fps"] = animation.FrameDuration > 0 ? Round(1.0 / animation.FrameDuration, 2) : 0;
                        result["bones_animated"] = animation.TransformTrackCount;
                        if (animation.FloatTrackCount != 0) result["float_tracks"] = animation.FloatTrackCount;
                        List<Skeleton.Bone> authoredBones = animations.GetSkeleton(animation.SkeletonName)?.Bones;
                        result["authored_rig_bones"] = authoredBones == null ? (JToken)"not in this PAK" : authoredBones.Count;

                        //Nearly every clip is authored on a shared rig and moved onto the set's own as it plays
                        string playsOn = rigName ?? (string.IsNullOrEmpty(set.Skeleton) ? animation.SkeletonName : set.Skeleton);
                        Skeleton target = RigSkeleton(animations, playsOn);
                        JObject retarget = new JObject() { ["plays_on"] = playsOn };
                        if (target == null)
                        {
                            if (rigName != null) throw new McpError("There is no rig '" + rigName + "' in ANIMATION.PAK (list_skeletons lists them).");
                            retarget["note"] = "That rig is not in ANIMATION.PAK.";
                        }
                        else
                        {
                            Retargeter retargeter = null;
                            if (Same(animation.SkeletonName, target.Name))
                                retarget["needed"] = false;
                            else
                            {
                                retarget["needed"] = true;
                                retargeter = Retargeter.Between(animations, animation.SkeletonName, target.Name);
                                if (retargeter != null)
                                {
                                    retarget["route"] = retargeter.ToString();
                                    retarget["mapped_bones"] = retargeter.MappedBones;
                                }
                                else
                                    warnings.Add("Nothing in the game's data maps '" + animation.SkeletonName + "' onto '" + target.Name + "', so it plays bone for bone there, which looks wrong wherever the two rigs differ.");
                            }
                            if (IsCharacter(set))
                            {
                                List<string> limbs = Anim.LimbsLeftAtRest(clip, target, retargeter);
                                if (limbs.Count != 0)
                                {
                                    retarget["limbs_left_at_rest"] = new JArray(limbs);
                                    warnings.Add("It never moves the " + string.Join(", ", limbs) + ": in game another animation drives " + (limbs.Count == 1 ? "that limb" : "those limbs") + " at the same time.");
                                }
                            }
                        }
                        result["retarget"] = retarget;
                        if (animation.Additive)
                            warnings.Add("Additive: in game it lays its movement over whatever else is playing.");
                    }

                    //Everything tagged on its timeline: foot strikes, sounds (with the bone they come from) and so on
                    List<Anim.ClipMarker> markers = clip.Markers;
                    result["markers_total"] = markers.Count;
                    result["markers"] = new JArray(markers.Take(limit).Select(o =>
                    {
                        JObject marker = new JObject() { ["time"] = Round(o.Time, 4), ["property"] = o.Property };
                        if (o.Event != null) marker["event"] = o.Event;
                        if (o.Type != MetadataValueType.PROPERTY_REFERENCE) marker["event_type"] = o.Type.ToString();
                        if (o.Instance >= 0) marker["instance"] = o.Instance;
                        if (o.Audio != null)
                        {
                            marker["sound_event"] = o.Audio.Event;
                            if (!string.IsNullOrEmpty(o.Audio.Bone)) marker["sound_bone"] = o.Audio.Bone;
                        }
                        else if (o.Argument?.Value != null)
                            marker["value"] = ValueJson(o.Argument.Value);
                        return marker;
                    }));

                    List<JObject> settings = new List<JObject>();
                    AnimClipDBSec.MetadataSet metadata = clip.Metadata;
                    if (metadata != null)
                    {
                        AddSettings(settings, metadata.Common, -1);
                        for (int i = 0; i < metadata.Instances.Count; i++) AddSettings(settings, metadata.Instances[i], i);
                    }
                    result["settings_total"] = settings.Count;
                    result["settings"] = new JArray(settings.Take(limit));
                    if (warnings.Count != 0) result["warnings"] = new JArray(warnings);
                    return result;
                });
        }

        private static void AddSettings(List<JObject> into, AnimClipDBSec.MetadataBlock block, int instance)
        {
            if (block == null) return;
            foreach (AnimClipDBSec.MetadataArgument argument in block.Arguments)
            {
                JObject setting = new JObject() { ["name"] = argument.Name, ["type"] = argument.Type.ToString() };
                Anim.AudioEvent audio = argument.Type == MetadataValueType.AUDIO ? Anim.ParseAudioEvent(argument.Value as string) : null;
                if (audio != null)
                {
                    setting["sound_event"] = audio.Event;
                    if (!string.IsNullOrEmpty(audio.Bone)) setting["sound_bone"] = audio.Bone;
                }
                else
                    setting["value"] = ValueJson(argument.Value);
                if (instance >= 0) setting["instance"] = instance;
                into.Add(setting);
            }
        }
        #endregion

        #region Rigs
        private sealed class RigCandidate
        {
            public string Name;
            public Skeleton Skeleton;
            public int Authored;
            public bool BigEnough = true;
            public float Fit = -1;
            public bool FitsBoneCount = true;
        }

        /// <summary>The rigs an export offers for these clips, in the order the rig picker lists them: authored on first, then the set's own, then big enough, then by name.</summary>
        private static List<RigCandidate> RigsForClips(Anim animations, Anim.AnimationSet set, IEnumerable<Anim.ClipReference> clips)
        {
            Dictionary<string, int> authored = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int mostTracks = 0;
            foreach (Anim.ClipReference clip in clips)
            {
                HavokPackfile.AnimationClip animation = clip.Animation;
                if (animation == null) continue;
                authored.TryGetValue(animation.SkeletonName, out int seen);
                authored[animation.SkeletonName] = seen + 1;
                foreach (int bone in animation.TrackToBone)
                    if (bone + 1 > mostTracks) mostTracks = bone + 1;
            }

            List<RigCandidate> candidates = new List<RigCandidate>();
            foreach (Anim.SkeletonAsset asset in animations.Skeletons)
            {
                Skeleton skeleton = asset.Skeleton ?? asset.Skeleton64;
                if (skeleton == null) continue;
                authored.TryGetValue(skeleton.Name, out int count);
                candidates.Add(new RigCandidate() { Name = skeleton.Name, Skeleton = skeleton, Authored = count, BigEnough = skeleton.Bones.Count >= mostTracks });
            }
            candidates.Sort((a, b) =>
            {
                if (a.Authored != b.Authored) return b.Authored.CompareTo(a.Authored);
                bool setA = Same(a.Name, set?.Skeleton), setB = Same(b.Name, set?.Skeleton);
                if (setA != setB) return setA ? -1 : 1;
                if (a.BigEnough != b.BigEnough) return a.BigEnough ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return candidates;
        }

        /// <summary>A model of the open level by its full or last name, as every asset tool finds one (did-you-mean when there is none).</summary>
        private static Models.CS2 FindModel(Level level, string name) => McpAssets.FindModel(level, name);

        private static object ListSkeletons(McpCall call)
        {
            string filter = (call.Str("filter") ?? "").Trim();
            string setName = call.Str("set");
            string modelName = call.Str("model");
            string rigName = call.Str("name");
            int limit = Limit(call, 100, 5000);
            if (setName != null && modelName != null)
                throw new McpError("Give set or model, not both.");
            if (rigName != null && (setName != null || modelName != null))
                throw new McpError("'name' describes one rig: leave out set and model.");

            using (McpEditorTools.Heartbeat(call, "Reading ANIMATION.PAK"))
                return McpEditor.UI(() =>
                {
                    Anim animations = RequireAnimations();
                    if (rigName != null)
                        return DescribeRig(call, animations, rigName, Words(filter), limit);
                    Dictionary<string, int> setsUsing = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (Anim.AnimationSet set in animations.Sets)
                    {
                        if (string.IsNullOrEmpty(set.Skeleton)) continue;
                        setsUsing.TryGetValue(set.Skeleton, out int count);
                        setsUsing[set.Skeleton] = count + 1;
                    }

                    List<RigCandidate> rigs;
                    JObject result = new JObject();
                    if (setName != null)
                    {
                        Anim.AnimationSet set = FindSet(animations, setName);
                        rigs = RigsForClips(animations, set, set.Contexts.SelectMany(o => o.Clips));
                        result["set"] = set.Name;
                        result["set_rig"] = set.Skeleton;
                    }
                    else
                    {
                        rigs = new List<RigCandidate>();
                        foreach (SkeletonDB.SkeletonEntry entry in animations.SkeletonIndex?.Skeletons ?? new List<SkeletonDB.SkeletonEntry>())
                        {
                            //The mobile and Switch builds ship only the 64-bit rigs
                            Anim.SkeletonAsset asset = animations.GetSkeleton(entry);
                            Skeleton skeleton = asset?.Skeleton ?? asset?.Skeleton64;
                            if (skeleton == null || !skeleton.Loaded) continue;
                            rigs.Add(new RigCandidate() { Name = entry.Name, Skeleton = skeleton });
                        }
                        if (modelName != null)
                        {
                            //As the model exporter's picker ranks them: big enough first, then scored, then by fit
                            Models.CS2 model = FindModel(McpEditor.RequireLevel(forEditing: false).Level, modelName);
                            int required = Skeleton.RequiredBoneCount(model);
                            List<float> fits = ModelIO.ScoreFits(model, rigs.Select(x => x.Skeleton).ToList());
                            for (int i = 0; i < rigs.Count; i++)
                            {
                                rigs[i].FitsBoneCount = rigs[i].Skeleton.Bones.Count >= required;
                                rigs[i].Fit = fits[i];
                            }
                            rigs.Sort((a, b) =>
                            {
                                if (a.FitsBoneCount != b.FitsBoneCount) return a.FitsBoneCount ? -1 : 1;
                                bool scoredA = a.Fit >= 0, scoredB = b.Fit >= 0;
                                if (scoredA != scoredB) return scoredA ? -1 : 1;
                                if (scoredA && a.Fit != b.Fit) return a.Fit.CompareTo(b.Fit);
                                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                            });
                            result["model"] = model.Name;
                            result["bone_slots_needed"] = required;
                            if (required == 0)
                                call.Note("That model isn't skinned, so any rig will do: a rig only writes a skeleton beside the mesh.");
                        }
                        else
                            rigs.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                    }

                    string[] words = Words(filter);
                    List<RigCandidate> listed = rigs.Where(o => AllWords(words, o.Name)).ToList();
                    if (listed.Count == 0 && filter.Length != 0)
                        call.Note("No rig matches '" + filter + "'" + McpGlobalAssetChecks.DidYouMean(rigs.Select(o => o.Name), filter, "."));
                    McpPaging.Page(call, listed, result, "skeletons", rig =>
                    {
                        JObject row = new JObject() { ["name"] = rig.Name, ["bones"] = rig.Skeleton.Bones.Count };
                        if (animations.SkeletonDefs.TryGetValue(rig.Name, out Anim.SkeletonDef def))
                        {
                            row["environment"] = def.IsEnvironment;
                            if (!string.IsNullOrEmpty(def.ReferenceSkeleton) && !Same(def.ReferenceSkeleton, rig.Name)) row["reference_rig"] = def.ReferenceSkeleton;
                        }
                        if (setsUsing.TryGetValue(rig.Name, out int sets)) row["sets_playing_on_it"] = sets;
                        if (setName != null)
                        {
                            if (rig.Authored != 0) row["authored_clips"] = rig.Authored;
                            if (!rig.BigEnough) row["too_few_bones"] = true;
                        }
                        if (modelName != null)
                        {
                            if (!rig.FitsBoneCount) row["too_few_bones"] = true;
                            else if (rig.Fit >= 0) row["fit_m"] = Round(rig.Fit, 3);
                        }
                        return row;
                    }, 100, 5000);
                    return result;
                });
        }

        /// <summary>One rig with its bones. UI thread.</summary>
        private static JObject DescribeRig(McpCall call, Anim animations, string name, string[] words, int limit)
        {
            Skeleton rig = RigSkeleton(animations, name.Trim());
            if (rig == null)
                throw new McpError("There is no rig '" + name + "' in ANIMATION.PAK" + McpGlobalAssetChecks.DidYouMean(animations.Skeletons.Select(o => o.ToString()), name, " (list_skeletons lists them)."));
            JObject result = new JObject() { ["name"] = rig.Name, ["bone_count"] = rig.Bones.Count };
            if (animations.SkeletonDefs.TryGetValue(rig.Name, out Anim.SkeletonDef def))
            {
                result["environment"] = def.IsEnvironment;
                if (!string.IsNullOrEmpty(def.ReferenceSkeleton) && !Same(def.ReferenceSkeleton, rig.Name)) result["reference_rig"] = def.ReferenceSkeleton;
            }
            result["sets_playing_on_it"] = new JArray(animations.Sets.Where(o => Same(o.Skeleton, rig.Name)).Select(o => o.Name).OrderBy(o => o));
            List<int> shown = Enumerable.Range(0, rig.Bones.Count).Where(i => AllWords(words, rig.Bones[i].Name)).ToList();
            McpPaging.Page(call, shown, result, "bones", i =>
            {
                Skeleton.Bone bone = rig.Bones[i];
                Vector3Json(bone.Position, out JArray position);
                System.Numerics.Vector3 euler = InstanceTransform.ToEulerDegrees(bone.Rotation);
                JObject row = new JObject() { ["index"] = i, ["name"] = bone.Name, ["parent"] = bone.ParentIndex };
                if (bone.ParentIndex >= 0 && bone.ParentIndex < rig.Bones.Count) row["parent_name"] = rig.Bones[bone.ParentIndex].Name;
                row["position"] = position;
                row["rotation"] = new JArray(Round(euler.X, 2), Round(euler.Y, 2), Round(euler.Z, 2));
                return row;
            }, 100, 5000);

            //The form bone-name parameters take, from what the open level already writes
            LevelContent content = Singleton.Editor?.CompositeBrowser?.Content;
            if (content?.Level?.Commands != null && content.IsLevelDataLoaded)
            {
                List<string> examples = new List<string>();
                foreach (Composite composite in content.Level.Commands.Entries)
                    foreach (FunctionEntity function in composite.functions)
                    {
                        if (!function.function.IsFunctionType) continue;
                        FunctionType type = function.function.AsFunctionType;
                        string value = type == FunctionType.AnimatedModelAttachmentNode ? McpGlobalAssetChecks.TextOf(function, "bone_name")
                                     : type == FunctionType.CameraPlayAnimation ? McpGlobalAssetChecks.TextOf(function, "bone_to_focus") : null;
                        if (!string.IsNullOrEmpty(value) && !examples.Contains(value)) examples.Add(value);
                        if (examples.Count >= 8) break;
                    }
                if (examples.Count != 0) result["bone_name_values_in_open_level"] = new JArray(examples);
            }
            return result;
        }

        private static void Vector3Json(System.Numerics.Vector3 v, out JArray json) => json = new JArray(Round(v.X, 4), Round(v.Y, 4), Round(v.Z, 4));
        #endregion

        #region Export and import
        private static object ExportAnimations(McpCall call)
        {
            string path = AbsolutePath(call, "path");
            string extension = Path.GetExtension(path).ToLowerInvariant();
            ModelExport.ModelExporter.Format format = ModelExport.ModelExporter.Formats.FirstOrDefault(o => o.Extension == extension && o.Animation);
            if (format == null)
                throw new McpError("'path' has to end .fbx, .glb, .gltf or .dae (the formats that carry animation).");
            string modelName = call.Str("model");
            string mode = (call.Str("mode") ?? "hold").Trim().ToLowerInvariant();
            Anim.RootMotion rootMotion;
            Anim.UntrackedChannels untracked = Anim.UntrackedChannels.RestPose;
            switch (mode)
            {
                case "hold": rootMotion = Anim.RootMotion.Ignore; break;
                case "travel": rootMotion = Anim.RootMotion.Follow; break;
                case "as_stored": rootMotion = Anim.RootMotion.Authored; untracked = Anim.UntrackedChannels.EngineDefaults; break;
                default: throw new McpError("'mode' is hold, travel or as_stored.");
            }
            //The mesh export always fills what a clip leaves untracked with the rest pose, which a re-import would bake in
            if (modelName != null && mode == "as_stored")
                throw new McpError("as_stored exports clips without a mesh: leave out model (or use mode hold or travel to view them on it).");

            //Besides the file: a .gltf keeps its data in a .bin beside it, and a model brings its sidecar and textures
            List<string> alongside = new List<string>();
            string stem = Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path));
            if (extension == ".gltf") alongside.Add(stem + ".bin");
            if (modelName != null)
            {
                alongside.Add(ModelIO.GetSidecarPath(path));
                alongside.Add(stem + " Textures");
            }
            if (!call.Bool("overwrite"))
            {
                List<string> taken = new List<string>() { path }.Concat(alongside).Where(o => File.Exists(o) || Directory.Exists(o)).ToList();
                if (taken.Count != 0)
                    throw new McpError(string.Join(", ", taken) + (taken.Count == 1 ? " already exists" : " already exist") + ". Pass overwrite:true to replace " + (taken.Count == 1 ? "it" : "them") + ", or pick another path.");
            }
            List<string> names = call.StrList("animations");
            string contextName = call.Str("context");
            string rigName = call.Str("rig");
            bool allowLarge = call.Bool("allow_large");

            using (McpEditorTools.Heartbeat(call, "Exporting animations"))
                return McpEditor.UI(() =>
                {
                    Anim animations = RequireAnimations();
                    Anim.AnimationSet set = FindSet(animations, call.Str("set", required: true));
                    List<Anim.ClipReference> clips = new List<Anim.ClipReference>();
                    if (names.Count != 0)
                    {
                        foreach (string name in names)
                        {
                            Anim.ClipReference clip = FindClip(set, name, contextName);
                            if (!clips.Contains(clip)) clips.Add(clip);
                        }
                    }
                    else if (contextName != null)
                        clips.AddRange(FindContext(set, contextName).Clips.Where(o => o != null).OrderBy(o => o.Name ?? "", StringComparer.OrdinalIgnoreCase));
                    else
                        clips.AddRange(set.Contexts.SelectMany(o => o.Clips).Where(o => o != null).OrderBy(o => o.Name ?? "", StringComparer.OrdinalIgnoreCase));

                    List<string> skipped = clips.Where(o => !o.Playable).Select(ClipName).ToList();
                    clips = clips.Where(o => o.Playable).ToList();
                    if (clips.Count == 0)
                        throw new McpError("None of those animations could be read, so there is nothing to export.");

                    Models.CS2 model = null;
                    Level level = null;
                    if (modelName != null)
                    {
                        level = McpEditor.RequireLevel(forEditing: false).Level;
                        model = FindModel(level, modelName);
                    }

                    //A skinned mesh's weights are its own rig's bone numbers, so it only binds properly to that rig
                    Skeleton skeleton, meshRig;
                    List<RigCandidate> candidates = RigsForClips(animations, set, clips);
                    /* Whether every clip can be moved onto a rig: one nothing joins to it plays bone for bone there.
                     * A clip authored on a rig the PAK doesn't hold at all (ANDROID's cutscene shots) has nowhere
                     * better to go than a rig with a bone for every track. */
                    bool Reachable(Skeleton rig) => rig != null && clips.All(o => Same(o.Animation.SkeletonName, rig.Name)
                        || Retargeter.Between(animations, o.Animation.SkeletonName, rig.Name) != null
                        || (RigSkeleton(animations, o.Animation.SkeletonName) == null && rig.Bones.Count > (o.Animation.TrackToBone.Count == 0 ? 0 : o.Animation.TrackToBone.Max())));
                    if (rigName != null)
                    {
                        RigCandidate chosen = candidates.FirstOrDefault(o => Same(o.Name, rigName));
                        skeleton = chosen?.Skeleton ?? RigSkeleton(animations, rigName);
                        if (skeleton == null)
                            throw new McpError("There is no rig '" + rigName + "' in ANIMATION.PAK (list_skeletons with set=" + set.Name + " ranks the ones to use).");
                        if (chosen != null && !chosen.BigEnough)
                            call.Note("'" + skeleton.Name + "' has fewer bones than these animations drive, so some tracks have nowhere to go.");
                        meshRig = MeshRig(animations, model, skeleton, skeleton.Name);
                        if (meshRig != null && !Same(meshRig.Name, skeleton.Name))
                            call.Note(model.Name + " is skinned to '" + meshRig.Name + "', not '" + skeleton.Name + "', so it will not bend where its own joints are; "
                                + (Reachable(meshRig) ? "leave out rig (or pass rig " + meshRig.Name + ") to have the clips retargeted onto its own rig."
                                    : "its own rig can't take these clips either, as nothing in the game's data moves all of them onto it."));
                    }
                    else
                    {
                        if (candidates.Count == 0) throw new McpError("ANIMATION.PAK holds no rigs to export against.");
                        skeleton = candidates[0].Skeleton;
                        /* The rig the clips were authored on is usually a shared reference rig few meshes are skinned to:
                         * ASH's mesh bound to MALE is torn metres apart. The game plays them on the character's own rig,
                         * retargeted, so a mesh's export does too - as long as every clip can be moved onto it. */
                        meshRig = MeshRig(animations, model, skeleton, skeleton.Name, set.Skeleton);
                        bool reachable = Reachable(meshRig);
                        if (meshRig != null && !Same(meshRig.Name, skeleton.Name) && !reachable)
                            call.Note(model.Name + " is skinned to '" + meshRig.Name + "', and nothing in the game's data moves all of these clips onto that rig, so it was bound to '" + skeleton.Name + "' and its limbs follow the wrong bones.");
                        else if (meshRig != null && !Same(meshRig.Name, skeleton.Name))
                        {
                            call.Note(model.Name + " is skinned to '" + meshRig.Name + "', so the clips were "
                                + (candidates[0].Authored == 0 ? "written against that rig bone for bone (none of them name a rig this PAK holds)" : "retargeted onto that rig")
                                + " rather than written against '" + skeleton.Name + "'.");
                            skeleton = meshRig;
                        }
                        else if (candidates[0].Authored == 0)
                            call.Note("None of these animations name a rig this PAK holds, so '" + skeleton.Name + "' was used; pass rig to choose another.");
                    }

                    //Measured per bone per frame: COLLADA spends about 340 bytes formatting each as text, FBX and glTF about 40
                    long estimate = (long)clips.Count * skeleton.Bones.Count * clips.Max(o => o.Animation.FrameCount) * (extension == ".dae" ? 340 : 40);
                    if (estimate >= 250L * 1024 * 1024 && !allowLarge)
                        throw new McpError("Writing " + clips.Count + " animations for a " + skeleton.Bones.Count + " bone rig would produce about " + (estimate / (1024 * 1024))
                            + " MB and take a while. Export fewer (animations or context), use .fbx or .glb rather than .dae, or pass allow_large:true.");

                    string folder = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                    if (model == null)
                        CathodeLibExtensions.ExportAnimations(skeleton, clips, path, rootMotion, untracked);
                    else
                        model.ExportMesh(path, skeleton, clips, rootMotion, level);

                    JObject result = new JObject()
                    {
                        ["path"] = path,
                        ["format"] = format.Description,
                        ["written"] = clips.Count,
                        ["rig"] = skeleton.Name,
                        ["mode"] = mode,
                    };
                    if (model != null) result["model"] = model.Name;
                    List<string> companions = alongside.Where(o => File.Exists(o) || Directory.Exists(o)).ToList();
                    if (companions.Count != 0) result["also_written"] = new JArray(companions);
                    if (skipped.Count != 0) result["skipped_unreadable"] = new JArray(skipped);
                    return result;
                });
        }

        private static AnimationImport.Reading ReadAnimationFile(string file, Skeleton rig, AnimationImport.Options options)
        {
            try { return AnimationImport.Read(file, rig, options); }
            catch (Exception e) { return new AnimationImport.Reading() { Problem = e.Message }; }
        }

        /// <summary>Read one clip of a file the way the retarget choice says: matched by name, converted, or (auto) whichever the file needs.</summary>
        private static AnimationImport.Reading ReadClip(string file, Skeleton rig, AnimationImport.Options options, string retarget)
        {
            options.Retarget = retarget == "always";
            AnimationImport.Reading reading = ReadAnimationFile(file, rig, options);
            if (retarget != "auto" || reading.Retargeted) return reading;
            //A file on another skeleton has no other way in; one sharing only bare joint names (Mixamo without its namespace) is on another skeleton too
            if ((!reading.Ok && reading.Matched == 0 && reading.CanRetarget) || (reading.Ok && reading.ShouldRetarget))
            {
                options.Retarget = true;
                AnimationImport.Reading across = ReadAnimationFile(file, rig, options);
                if (across.Ok || !reading.Ok) return across;
                //Converting failed where matching by name worked: keep that, and say so
                reading.Warnings.Add("Converting it from its own skeleton failed (" + (across.Problem ?? "").Replace("\r\n", " ") + "), so it was matched by name.");
                options.Retarget = false;
            }
            return reading;
        }

        /// <summary>The file's clips an import takes: clip_index as a number, a list of numbers, or 'all'.</summary>
        private static List<int> ClipIndexes(JToken token, int count)
        {
            List<int> indexes = new List<int>();
            if (token == null || token.Type == JTokenType.Null) indexes.Add(0);
            else if (token.Type == JTokenType.String && Same(((string)token).Trim(), "all")) indexes.AddRange(Enumerable.Range(0, count));
            else if (token.Type == JTokenType.Integer) indexes.Add((int)token);
            else if (token.Type == JTokenType.String && int.TryParse(((string)token).Trim(), out int parsed)) indexes.Add(parsed);
            else if (token is JArray list)
                foreach (JToken item in list)
                {
                    if (item.Type != JTokenType.Integer) throw new McpError("'clip_index' lists whole numbers, e.g. [0, 2].");
                    if (!indexes.Contains((int)item)) indexes.Add((int)item);
                }
            else throw new McpError("'clip_index' is a number from 0, a list of them, or 'all'.");
            if (indexes.Count == 0) throw new McpError("'clip_index' lists no clips.");
            foreach (int index in indexes)
                if (index < 0 || index >= count)
                    throw new McpError("The file has " + count + " animation" + (count == 1 ? "" : "s") + " (clip_index 0 to " + (count - 1) + "), so there is no " + index + ".");
            return indexes;
        }

        /// <summary>One clip an import brings in, and what it found.</summary>
        private sealed class ClipPlan
        {
            public int Index;
            public string FileClipName;
            public string Name;
            public string StoredPath;
            public Anim.ClipReference Existing;
            public AnimationImport.Options Options;
            public AnimationImport.Reading Reading;
            public List<string> Problems = new List<string>();
        }

        private static object ImportAnimation(McpCall call)
        {
            string file = AbsolutePath(call, "path");
            if (!File.Exists(file))
                throw new McpError("There is no file at " + file + ".");
            AnimationImport.RootHandling root;
            switch ((call.Str("root") ?? "auto").Trim().ToLowerInvariant())
            {
                case "auto": root = AnimationImport.RootHandling.Auto; break;
                case "engine": root = AnimationImport.RootHandling.LeaveToEngine; break;
                case "authored": root = AnimationImport.RootHandling.KeepAsAuthored; break;
                default: throw new McpError("'root' is auto, engine or authored.");
            }
            string retarget = (call.Str("retarget") ?? "auto").Trim().ToLowerInvariant();
            if (retarget != "auto" && retarget != "always" && retarget != "never")
                throw new McpError("'retarget' is auto, always or never.");
            double rate = call.Num("frame_rate", 0);
            if (rate < 0 || rate > 240)
                throw new McpError("'frame_rate' must be between 1 and 240 (or 0 to take it from the file).");
            int startFrame = call.Int("start_frame", 0);
            int endFrame = call.Has("end_frame") ? call.Int("end_frame", -1) : -1;
            if (startFrame < 0) throw new McpError("'start_frame' counts from 0.");
            if (call.Has("end_frame") && endFrame < startFrame) throw new McpError("'end_frame' must be at least start_frame (" + startFrame + ").");
            bool additive = call.Bool("additive");
            bool dryRun = call.Bool("dry_run");
            bool replace = call.Bool("replace");
            bool preview = call.Bool("preview");
            string contextName = call.Str("context");
            if (contextName != null && contextName.Trim().Length == 0) contextName = null;
            if (preview && !dryRun) throw new McpError("preview goes with dry_run: it draws the clip before it is imported (preview_animation draws one already in ANIMATION.PAK).");

            List<Tuple<string, int>> inFile;
            using (McpEditorTools.Heartbeat(call, "Reading " + Path.GetFileName(file)))
            {
                try { inFile = AnimationImport.ClipsIn(file); }
                catch (Exception e) { throw new McpError("That file couldn't be read: " + e.Message); }
            }
            if (inFile.Count == 0) throw new McpError("There's no animation in " + Path.GetFileName(file) + ".");
            List<int> indexes = ClipIndexes(call.Token("clip_index"), inFile.Count);
            bool several = indexes.Count > 1;
            if (several && (call.Has("name") || call.Has("stored_path")))
                throw new McpError("'name' and 'stored_path' name one clip: leave them out to import several (each takes its own name from the file), or import them one call each.");
            if (several && replace) throw new McpError("replace rebuilds one clip: give one clip_index.");
            if (several && preview) throw new McpError("preview draws one clip: give one clip_index.");

            Anim animations = null;
            Anim.AnimationSet set = null;
            Skeleton rig = null;
            string rigName = null;
            List<ClipPlan> plans = new List<ClipPlan>();
            using (McpEditorTools.Heartbeat(call, "Reading ANIMATION.PAK"))
                McpEditor.UI(() =>
                {
                    animations = RequireAnimations();
                    set = FindSet(animations, call.Str("set", required: true));
                    if (contextName != null) contextName = FindContext(set, contextName).Name;
                    if (contextName != null && contextName.Trim().Length == 0) contextName = null;
                    rigName = (call.Str("rig") ?? AnimationImport.DefaultRigFor(set)).Trim();
                    if (rigName.Length == 0)
                        throw new McpError(set.Name + " has no rig of its own; pass rig (list_skeletons lists them).");
                    rig = RigSkeleton(animations, rigName);
                    if (rig == null || rig.Bones.Count == 0)
                        throw new McpError("There is no rig '" + rigName + "' in ANIMATION.PAK" + McpGlobalAssetChecks.DidYouMean(animations.Skeletons.Select(o => o.ToString()), rigName, " (list_skeletons lists them)."));
                    //Found whatever the case, but written as a case-sensitive hash: the clip has to name the rig exactly as the index does
                    rigName = animations.SkeletonIndex?.GetSkeleton(rigName)?.Name ?? rigName;

                    string stem = AnimationImport.Sanitise(Path.GetFileNameWithoutExtension(file)).ToLowerInvariant();
                    HashSet<string> taken = new HashSet<string>(set.Contexts.SelectMany(o => o.Clips).Select(o => o.Name), StringComparer.OrdinalIgnoreCase);
                    HashSet<string> planned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (int index in indexes)
                    {
                        ClipPlan plan = new ClipPlan() { Index = index, FileClipName = inFile[index].Item1 };
                        if (several)
                        {
                            //Each clip goes by its own name in the file, unless that name says nothing (Mixamo's 'mixamo.com')
                            string name = AnimationImport.ClipNameFrom(plan.FileClipName) ?? stem + "_" + index;
                            string unique = name;
                            for (int n = 2; planned.Contains(unique); n++) unique = name + "_" + n;
                            plan.Name = unique;
                            plan.StoredPath = AnimationImport.PathFor(set, file) + "_" + AnimationImport.Sanitise(unique).ToUpperInvariant();
                        }
                        else
                        {
                            plan.Name = (call.Str("name") ?? stem).Trim();
                            plan.StoredPath = (call.Str("stored_path") ?? AnimationImport.PathFor(set, file)).Trim().Replace('/', '\\');
                        }
                        planned.Add(plan.Name);
                        if (plan.Name.Length == 0) throw new McpError("'name' can't be empty.");
                        if (plan.StoredPath.Length == 0) throw new McpError("'stored_path' can't be empty.");

                        if (replace)
                        {
                            plan.Existing = FindClip(set, plan.Name, contextName);
                            plan.Name = plan.Existing.Name;
                            plan.StoredPath = plan.Existing.Path;
                            if (plan.Existing.Section == null)
                                plan.Problems.Add("'" + plan.Name + "' has no section in ANIMATION.PAK to rebuild.");
                            else if (plan.Existing.Section.Metadata.Count != 1 || plan.Existing.Section.GetAnimations().Count != 1)
                                plan.Problems.Add("'" + plan.Name + "' shares its section with other clips, so it can't be rebuilt in place: import under a new name and point its users at it.");
                            else if (plan.Existing.Section.Metadata[0].Instances.Count > 1)
                                plan.Problems.Add("'" + plan.Name + "' has " + plan.Existing.Section.Metadata[0].Instances.Count + " metadata instance blocks, which its set's clip lines select by number, and a rebuild keeps only the first: import under a new name and point its users at it.");
                            if (call.Has("stored_path")) call.Note("stored_path is ignored with replace: the clip keeps its own (" + plan.StoredPath + ").");
                        }
                        else
                        {
                            //The checks AnimationImport.Add makes, so a dry run can say so too
                            if (taken.Contains(plan.Name))
                                plan.Problems.Add("'" + plan.Name + "' is already the name of an animation in " + set.Name + ": pass another name, or replace:true to rebuild it in place.");
                            if (animations.GetSection(plan.StoredPath, out int _) != null)
                                plan.Problems.Add("Something is already stored at '" + plan.StoredPath + "'; pass another stored_path.");
                        }
                        plan.Options = new AnimationImport.Options()
                        {
                            Rig = rigName,
                            Root = root,
                            FrameRate = (float)rate,
                            Additive = additive,
                            Index = index,
                            StartFrame = startFrame,
                            EndFrame = endFrame,
                            Context = contextName,
                        };
                        plans.Add(plan);
                    }
                });

            foreach (ClipPlan plan in plans)
            {
                using (McpEditorTools.Heartbeat(call, "Reading " + Path.GetFileName(file) + (several ? " clip " + plan.Index : "")))
                    plan.Reading = ReadClip(file, rig, plan.Options, retarget);
                call.ThrowIfCancelled();
            }
            List<ClipPlan> unreadable = plans.Where(o => !o.Reading.Ok).ToList();
            if (!several && unreadable.Count != 0)
                throw new McpError("That can't be imported onto " + rigName + ": " + (unreadable[0].Reading.Problem ?? "nothing was read from the file.").Replace("\r\n", " ")
                    + (retarget == "never" && unreadable[0].Reading.CanRetarget ? " (retarget:'never' was given; the file can be converted with retarget:'auto')." : ""));

            if (!Same(rigName, set.Skeleton) && !string.IsNullOrEmpty(set.Skeleton))
                call.Note("Built against " + rigName + " rather than " + set.Skeleton + ", so the game retargets it onto " + set.Skeleton + " as it plays, as most of " + set.Name + "'s animations do.");

            if (dryRun)
            {
                /* Each clip is built as the import would build it, into nothing - what the import window's preview
                 * does - so an encoder that refuses this PAK's sections (or this clip) says so now, not on import */
                Anim.ClipReference built = null;
                using (McpEditorTools.Heartbeat(call, "Building the clip"))
                    McpEditor.UI(() =>
                    {
                        foreach (ClipPlan plan in plans.Where(o => o.Reading.Ok))
                        {
                            try
                            {
                                Anim.ClipReference clip = AnimationImport.BuildPreview(animations, set, plan.Reading, plan.Name, plan.StoredPath, plan.Options);
                                if (clip?.Section == null) plan.Problems.Add("The clip could not be built.");
                                else if (built == null) built = clip;
                            }
                            catch (Exception e) { plan.Problems.Add("The clip could not be built: " + e.Message); }
                        }
                    });
                JObject report = ImportReport(call, plans, set, rigName, rig, several, inFile, true, replace);
                if (!preview) return report;
                if (built == null) throw new McpError("The clip could not be built, so there is nothing to draw. " + report.ToString(Newtonsoft.Json.Formatting.None));
                return McpEditor.UI(() => ClipSheet(call, animations, set, built, rig, null, null, Anim.RootMotion.Ignore, 1024, report));
            }

            List<string> refused = plans.SelectMany(o => o.Problems).ToList();
            if (refused.Count != 0)
                throw new McpError(string.Join(" ", refused) + " Nothing was imported.");
            List<ClipPlan> ready = plans.Where(o => o.Reading.Ok).ToList();
            if (ready.Count == 0)
                throw new McpError("None of the file's clips could be read: " + string.Join(" ", unreadable.Select(o => "clip " + o.Index + ": " + (o.Reading.Problem ?? "").Replace("\r\n", " "))));
            CloseOrRefuse(call, "ANIMATION.PAK can't be written while it holds it open");
            RefuseIfAnimationPakChanged();

            List<string> failed = new List<string>();
            using (McpEditorTools.Heartbeat(call, "Writing ANIMATION.PAK"))
                McpEditor.UI(() =>
                {
                    foreach (ClipPlan plan in ready)
                    {
                        string problem;
                        bool done = replace
                            ? AnimationImport.Replace(animations, plan.Existing, plan.Reading, plan.Options, out problem)
                            : AnimationImport.Add(animations, set, plan.Reading, plan.Name, plan.StoredPath, plan.Options, out problem);
                        if (!done) { failed.Add("'" + plan.Name + "': " + problem); plan.Problems.Add(problem); continue; }
                        //The pick lists are built once at startup, so they are told about the new clip
                        Singleton.RegisterAnimation(set.Name, plan.Name);
                    }
                    if (failed.Count == ready.Count)
                        throw new McpError((failed.Count == 1 ? "" : "None of the clips went in: ") + string.Join(" ", failed) + " Nothing was written.");
                    Singleton.OnAnimationsModified?.Invoke();
                    WriteAnimationPak(call, animations, string.Join(", ", ready.Where(o => o.Problems.Count == 0).Select(o => "'" + o.Name + "'")) + (replace ? " rebuilt in " : " added to ") + set.Name);
                });

            JObject result = ImportReport(call, plans, set, rigName, rig, several, inFile, false, replace);
            if (failed.Count != 0) result["failed"] = new JArray(failed);
            string names = string.Join(", ", ready.Where(o => o.Problems.Count == 0).Select(o => "'" + o.Name + "'"));
            call.Note("ANIMATION.PAK has been written. " + (replace
                ? names + " now plays the new animation wherever it is named (scripts, trees, blend sets)."
                : "AnimationSet " + set.Name + " with Animation " + names + " now plays " + (contextName == null ? "" : "in the " + contextName + " context ") + "(CMD_PlayAnimation for a character); an open Animation Editor window shows it once reopened."));
            return result;
        }

        /// <summary>A locomotion measurement or blend anchor: how far, fast and which way a clip travels, which the movement system picks and blends on.</summary>
        private static bool IsMotionSetting(string name) =>
            name.StartsWith("spherical_blend", StringComparison.OrdinalIgnoreCase) || name.StartsWith("mirror_spherical_blend", StringComparison.OrdinalIgnoreCase)
            || new[] { "speed", "velocity", "translation", "Rotation", "Incline", "Apex", "MovementDirection" }.Any(o => name.IndexOf(o, StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>What an import read and built, per clip: flat for one clip (as it always was), a list for several.</summary>
        private static JObject ImportReport(McpCall call, List<ClipPlan> plans, Anim.AnimationSet set, string rigName, Skeleton rig, bool several, List<Tuple<string, int>> inFile, bool dryRun, bool replace)
        {
            List<JObject> rows = new List<JObject>();
            foreach (ClipPlan plan in plans)
            {
                AnimationImport.Reading reading = plan.Reading;
                JObject row = new JObject() { ["name"] = plan.Name, ["stored_path"] = plan.StoredPath };
                if (inFile.Count > 1 || plan.Index != 0) row["clip_index"] = plan.Index;
                if (!string.IsNullOrEmpty(plan.FileClipName) && inFile.Count > 1) row["file_clip_name"] = plan.FileClipName;
                if (plan.Options.Context != null) row["context"] = plan.Options.Context;
                if (!reading.Ok)
                {
                    row["problem"] = (reading.Problem ?? "nothing was read from the file").Replace("\r\n", " ");
                    rows.Add(row);
                    continue;
                }
                row["frames"] = reading.Frames;
                if (reading.FileFrames != reading.Frames) row["file_frames"] = reading.FileFrames;
                row["duration"] = Round(reading.Duration, 3);
                row["fps"] = reading.FrameDuration > 0 ? Round(1.0 / reading.FrameDuration, 2) : 0;
                if (reading.FileFrameRate > 0) row["file_fps"] = Round(reading.FileFrameRate, 2);
                row["route"] = reading.Retargeted ? "retargeted" : "by_name";
                if (reading.Retargeted)
                {
                    row["retargeted"] = true;
                    row["mirrored"] = reading.Mirrored;
                    row["bones_driven"] = reading.Matched;
                    row["rig_bones"] = rig.Bones.Count;
                }
                else
                {
                    row["matched_nodes"] = reading.Matched;
                    row["animated_nodes"] = reading.Channels;
                    row["matched_fraction"] = reading.Channels > 0 ? Round((double)reading.Matched / reading.Channels, 2) : 0;
                    if (reading.Matched != 0 && !reading.NamesAuthoritative) row["matched_by_bare_names"] = reading.Matched - reading.MatchedExactly;
                }
                row["scale"] = Round(reading.Scale, 3);
                row["root_moves"] = reading.RootAnimated;
                if (plan.Options.Additive) row["additive"] = true;
                if (reading.Warnings.Count != 0) row["warnings"] = new JArray(reading.Warnings.Select(o => o.Replace("\r\n", " ")));
                if (plan.Existing != null)
                {
                    row["replaces"] = new JObject() { ["frames"] = plan.Existing.Animation?.FrameCount ?? 0, ["duration"] = Round(plan.Existing.Duration, 3), ["markers_kept"] = plan.Existing.Markers.Count(o => o.Time <= reading.Duration + 0.0001f) };
                    //Settings carried over from the old clip: they describe how it moved, not how the new one does
                    List<string> kept = plan.Existing.Section?.Metadata.Count == 1 ? Anim.KeptSettings(plan.Existing.Section) : new List<string>();
                    if (kept.Count != 0)
                    {
                        row["replaces"]["kept_settings"] = new JArray(kept);
                        call.Note("'" + plan.Name + "' keeps the old clip's settings (kept_settings), which describe the old animation" + (kept.Any(IsMotionSetting)
                            ? ": the movement system still chooses and blends it by their speed and travel, so a new animation moving at another speed or turning differently will slide or match badly."
                            : "."));
                    }
                    if (!plan.Existing.Path.StartsWith("ANIMATION\\OPENCAGE\\", StringComparison.OrdinalIgnoreCase))
                        call.Note("'" + plan.Name + "' ships with the game: rebuilding it changes it for every character that plays it, in every level (a vanilla copy is kept for the mod baseline; verifying the game files restores it).");
                }
                if (plan.Problems.Count != 0) row[dryRun ? "would_fail" : "problems"] = new JArray(plan.Problems);
                rows.Add(row);
            }

            if (!several)
            {
                JObject one = new JObject() { ["set"] = set.Name };
                foreach (JProperty property in rows[0].Properties()) one[property.Name] = property.Value;
                one["rig"] = rigName;
                if (dryRun) one["dry_run"] = true;
                else one[replace ? "replaced" : "imported"] = !one.ContainsKey("problems");
                if (inFile.Count > 1)
                {
                    one["file_clips"] = new JArray(inFile.Select((o, i) => new JObject() { ["index"] = i, ["name"] = o.Item1, ["frames"] = o.Item2 }));
                    call.Note("The file holds " + inFile.Count + " animations; clip " + plans[0].Index + " was read. clip_index 'all' imports every one in one write, each under its own name.");
                }
                return one;
            }
            JObject result = new JObject() { ["set"] = set.Name, ["rig"] = rigName, ["clips"] = new JArray(rows) };
            if (dryRun) result["dry_run"] = true;
            else result["imported"] = plans.Count(o => o.Reading.Ok && o.Problems.Count == 0);
            return result;
        }

        private static object RemoveAnimation(McpCall call)
        {
            string setName = call.Str("set", required: true);
            string clipName = call.Str("animation", required: true);
            string contextName = call.Str("context");
            bool dryRun = call.Bool("dry_run");

            List<string> users = new List<string>();
            JObject result = null;
            Anim.ClipReference clip = null;
            Anim animations = null;
            using (McpEditorTools.Heartbeat(call, "Checking who uses it"))
                McpEditor.UI(() =>
                {
                    animations = RequireAnimations();
                    Anim.AnimationSet set = FindSet(animations, setName);
                    clip = FindClip(set, clipName, contextName);
                    if (!(clip.Path ?? "").StartsWith("ANIMATION\\OPENCAGE\\", StringComparison.OrdinalIgnoreCase))
                        throw new McpError("'" + clip.Name + "' ships with the game (stored at " + clip.Path + "), and only clips imported with OpenCAGE (stored under ANIMATION\\OPENCAGE\\) can be removed. "
                            + "To change what it plays, rebuild it with import_animation replace:true.");
                    result = new JObject() { ["set"] = set.Name, ["animation"] = clip.Name, ["context"] = ContextName(clip.Context), ["stored_path"] = clip.Path };

                    //Trees name clips by their name, through the tree set the character uses
                    foreach (TreeHit hit in FindInTrees(animations, clip.Name, "AnimationName", null, 20))
                        users.Add("animation tree " + hit.TreeSet + "\\" + hit.Tree + " node " + hit.Node + " (" + hit.Field + ")");
                    foreach (GlobalAnimClipDB.BlendSet blend in animations.ClipIndex?.BlendSets ?? new List<GlobalAnimClipDB.BlendSet>())
                        if (blend.Clips.Any(o => Same(o.Name, clip.Name)) && BlendUsers(animations, blend).Any(o => Same(o.Database.Character, set.Name)))
                            users.Add("blend set " + blend);
                    Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
                    if (commands != null)
                        foreach (Composite composite in commands.Entries)
                            foreach (FunctionEntity function in composite.functions)
                                if (Same(McpGlobalAssetChecks.TextOf(function, "Animation"), clip.Name) && Same(McpGlobalAssetChecks.TextOf(function, "AnimationSet"), set.Name))
                                    users.Add("entity " + McpScript.EntityName(commands, composite, function) + " (" + McpScript.Id(function.shortGUID) + ") in " + composite.name);
                });
            if (users.Count != 0)
            {
                result["used_by"] = new JArray(users.Take(25));
                throw new McpError("'" + clipName + "' is still named by: " + string.Join("; ", users.Take(10)) + (users.Count > 10 ? " and " + (users.Count - 10) + " more" : "")
                    + ". Point those at another clip first (edit_anim_tree, edit_blend_set, set_parameters); other levels' scripts are not checked.");
            }
            if (dryRun)
            {
                result["dry_run"] = true;
                call.Note("Nothing in the trees, blend sets or the open level names it. Other levels' scripts are not checked: one that still names it plays nothing.");
                return result;
            }

            CloseOrRefuse(call, "ANIMATION.PAK can't be written while it holds it open");
            RefuseIfAnimationPakChanged();
            using (McpEditorTools.Heartbeat(call, "Writing ANIMATION.PAK"))
                McpEditor.UI(() =>
                {
                    string set = clip.Context?.Set?.Name;
                    if (!animations.RemoveClip(clip, out string problem))
                        throw new McpError("'" + clip.Name + "' can't be removed: " + problem);
                    if (set != null && Singleton.AllAnimations.TryGetValue(set, out HashSet<string> names) && !animations.GetSet(set).Contexts.SelectMany(o => o.Clips).Any(o => Same(o.Name, clip.Name)))
                        names.RemoveWhere(o => Same(o, clip.Name));
                    Singleton.OnAnimationsModified?.Invoke();
                    WriteAnimationPak(call, animations, "'" + clip.Name + "' was removed from " + set + "; that is");
                });
            result["removed"] = true;
            return result;
        }

        private static object EditAnimationEvents(McpCall call)
        {
            string setName = call.Str("set", required: true);
            string clipName = call.Str("animation", required: true);
            string contextName = call.Str("context");
            JArray adds = call.Array("add");
            JArray removes = call.Array("remove");
            bool dryRun = call.Bool("dry_run");
            bool allowRetail = call.Bool("allow_retail");
            if (adds.Count + removes.Count == 0) throw new McpError("Give add or remove.");

            //Checked before anything changes
            List<Tuple<float, string, string, string>> adding = new List<Tuple<float, string, string, string>>();
            foreach (JToken token in adds)
            {
                JObject item = token as JObject ?? throw new McpError("Each add entry is an object: {time, property, sound_event?, bone?}.");
                CheckKeys(item, "An add entry", "time", "property", "sound_event", "bone");
                if (item["time"] == null) throw new McpError("Each add entry needs 'time' (seconds).");
                float time = NumberOf(item["time"], "time");
                string property = (ItemStr(item, "property") ?? "").Trim();
                if (property.Length == 0) throw new McpError("Each add entry needs 'property', the marker's name (e.g. footstep_l, or sound).");
                adding.Add(Tuple.Create(time, property, ItemStr(item, "sound_event")?.Trim(), ItemStr(item, "bone")?.Trim()));
            }
            List<Tuple<float?, string>> removing = new List<Tuple<float?, string>>();
            foreach (JToken token in removes)
            {
                JObject item = token as JObject ?? throw new McpError("Each remove entry is an object: {time?, property?}.");
                CheckKeys(item, "A remove entry", "time", "property");
                float? time = item["time"] == null || item["time"].Type == JTokenType.Null ? (float?)null : NumberOf(item["time"], "time");
                string property = ItemStr(item, "property")?.Trim();
                if (time == null && string.IsNullOrEmpty(property)) throw new McpError("A remove entry needs time, property or both.");
                removing.Add(Tuple.Create(time, property));
            }

            JObject result = null;
            Anim animations = null;
            Anim.ClipReference clip = null;
            List<string> changes = new List<string>();
            McpEditor.UI(() =>
            {
                animations = RequireAnimations();
                Anim.AnimationSet set = FindSet(animations, setName);
                clip = FindClip(set, clipName, contextName);
                if (clip.Section == null || clip.Metadata == null)
                    throw new McpError("'" + clip.Name + "' has no metadata in ANIMATION.PAK to hold markers.");
                if (clip.Section.Metadata.Count != 1)
                    throw new McpError("'" + clip.Name + "' shares its section with " + (clip.Section.Metadata.Count - 1) + " other clips; only a clip with a section to itself can be edited here.");
                if (!(clip.Path ?? "").StartsWith("ANIMATION\\OPENCAGE\\", StringComparison.OrdinalIgnoreCase) && !allowRetail)
                    throw new McpError("'" + clip.Name + "' ships with the game: its markers change for every character playing it, in every level. Pass allow_retail:true to edit it anyway.");
                float duration = clip.Duration;
                foreach (Tuple<float, string, string, string> add in adding)
                {
                    if (add.Item1 < 0 || (duration > 0 && add.Item1 > duration + 0.0005f))
                        throw new McpError("A marker at " + add.Item1 + " s is outside the clip, which runs 0 to " + Round(duration, 3) + " s.");
                    if (add.Item3 != null)
                    {
                        string warning = McpGlobalAssetChecks.UnknownValueWarning(EnumStringType.SOUND_EVENT, add.Item3);
                        if (warning != null) call.Note(warning);
                    }
                    changes.Add("add " + add.Item2 + " at " + Round(add.Item1, 3) + " s" + (add.Item3 == null ? "" : " firing " + add.Item3 + (string.IsNullOrEmpty(add.Item4) ? "" : " from " + add.Item4)));
                }
                result = new JObject() { ["set"] = set.Name, ["animation"] = clip.Name, ["duration"] = Round(duration, 3) };
            });

            //Applied to a copy first to count what remove takes, then for real to every copy of the section
            int removed = 0;
            Action<AnimClipDBSec.MetadataSet> edit = metadata =>
            {
                removed = 0;
                AnimClipDBSec.MetadataBlock block = metadata.Instances.Count != 0 ? metadata.Instances[0] : metadata.Common;
                foreach (Tuple<float?, string> remove in removing)
                    foreach (AnimClipDBSec.MetadataBlock each in new[] { metadata.Common }.Concat(metadata.Instances))
                        foreach (AnimClipDBSec.MetadataProperty property in each.Properties.ToList())
                        {
                            if (remove.Item2 != null && !Same(property.Name, remove.Item2)) continue;
                            for (int i = property.Times.Count - 1; i >= 0; i--)
                            {
                                if (remove.Item1 != null && Math.Abs(property.Times[i] - remove.Item1.Value) > 0.001f) continue;
                                property.Times.RemoveAt(i);
                                if (i < property.Events.Count) property.Events.RemoveAt(i);
                                removed++;
                            }
                            if (property.Times.Count == 0) each.Properties.Remove(property);
                            each.HasProperties = each.Properties.Count != 0;
                        }
                //A sound argument this tool made that no marker names any more goes with its marker
                foreach (AnimClipDBSec.MetadataBlock each in new[] { metadata.Common }.Concat(metadata.Instances))
                {
                    HashSet<string> named = new HashSet<string>(each.Properties.SelectMany(p => p.Events).Select(e => e.Name ?? ""));
                    each.Arguments.RemoveAll(o => o.Type == MetadataValueType.AUDIO && (o.Name ?? "").StartsWith("sound_") && !named.Contains(o.Name));
                }
                foreach (Tuple<float, string, string, string> add in adding)
                {
                    AnimClipDBSec.MetadataProperty property = block.Properties.FirstOrDefault(o => Same(o.Name, add.Item2));
                    if (property == null)
                    {
                        property = new AnimClipDBSec.MetadataProperty() { Name = add.Item2 };
                        block.Properties.Add(property);
                        block.HasProperties = true;
                    }
                    int at = property.Times.FindIndex(o => o > add.Item1);
                    if (at < 0) at = property.Times.Count;
                    property.Times.Insert(at, add.Item1);
                    //A sound is an AUDIO argument of the block, which the occurrence names
                    AnimClipDBSec.MetadataEvent fired = new AnimClipDBSec.MetadataEvent() { Type = MetadataValueType.PROPERTY_REFERENCE };
                    if (add.Item3 != null)
                    {
                        string argument = ArgumentName(add);
                        block.Arguments.RemoveAll(o => o.Name == argument);
                        block.Arguments.Add(new AnimClipDBSec.MetadataArgument()
                        {
                            Name = argument,
                            Type = MetadataValueType.AUDIO,
                            Value = AudioValue(add),
                        });
                        fired.Name = argument;
                    }
                    //Occurrences and their events stay paired: once a property has any event, every occurrence has a slot
                    if (add.Item3 != null || property.Events.Count != 0)
                    {
                        while (property.Events.Count < at) property.Events.Add(new AnimClipDBSec.MetadataEvent() { Type = MetadataValueType.PROPERTY_REFERENCE });
                        property.Events.Insert(Math.Min(at, property.Events.Count), fired);
                        while (property.Events.Count < property.Times.Count) property.Events.Add(new AnimClipDBSec.MetadataEvent() { Type = MetadataValueType.PROPERTY_REFERENCE });
                    }
                    property.HasEvents = property.Events.Count != 0;
                }
            };

            if (dryRun)
            {
                McpEditor.UI(() =>
                {
                    AnimClipDBSec.MetadataSet copy = CopyMetadata(clip.Metadata);
                    edit(copy);
                });
                if (removing.Count != 0) changes.Add("remove " + removed + " marker(s)");
                result["changes"] = new JArray(changes);
                result["dry_run"] = true;
                return result;
            }
            CloseOrRefuse(call, "ANIMATION.PAK can't be written while it holds it open");
            RefuseIfAnimationPakChanged();
            McpEditor.UI(() =>
            {
                if (!animations.EditMetadata(clip, edit, out string problem))
                    throw new McpError(problem);
                //Names are stored as hashes: new ones go in the debug string table so they read back
                foreach (Tuple<float, string, string, string> add in adding)
                {
                    animations.AddName(add.Item2, true);
                    if (add.Item3 == null) continue;
                    animations.AddName(ArgumentName(add), true);
                    //The sound is stored as the hash of this whole string: unregistered, it reads back as a number and the marker is lost
                    animations.AddName(AudioValue(add), true);
                }
                Singleton.OnAnimationsModified?.Invoke();
                WriteAnimationPak(call, animations, "The marker change to '" + clip.Name + "' is");
                if (removing.Count != 0) changes.Add("removed " + removed + " marker(s)");
                result["changes"] = new JArray(changes);
                result["markers"] = new JArray(clip.Markers.Select(o => new JObject() { ["time"] = Round(o.Time, 4), ["property"] = o.Property, ["sound_event"] = o.Audio?.Event }));
            });
            result["written"] = true;
            return result;
        }

        /// <summary>An AUDIO argument's value, in the form retail clips carry (Anim.ParseAudioEvent reads it).</summary>
        private static string AudioValue(Tuple<float, string, string, string> add) => "[ArgumentList={},Bone={" + (add.Item4 ?? "") + "},Event={" + add.Item3 + "},Offset={0,0,0},UseArguments={No}]";

        /// <summary>The block argument a sound marker's occurrence names: one per marker name and millisecond.</summary>
        private static string ArgumentName(Tuple<float, string, string, string> add) => "sound_" + AnimationImport.Sanitise(add.Item2).ToLowerInvariant() + "_" + (int)Math.Round(add.Item1 * 1000);

        /// <summary>A copy of a clip's metadata deep enough for edit_animation_events to try its edit on.</summary>
        private static AnimClipDBSec.MetadataSet CopyMetadata(AnimClipDBSec.MetadataSet source)
        {
            AnimClipDBSec.MetadataBlock Block(AnimClipDBSec.MetadataBlock from) => new AnimClipDBSec.MetadataBlock()
            {
                Arguments = new List<AnimClipDBSec.MetadataArgument>(from.Arguments),
                Properties = from.Properties.Select(p => new AnimClipDBSec.MetadataProperty() { Name = p.Name, Times = new List<float>(p.Times), Events = new List<AnimClipDBSec.MetadataEvent>(p.Events), HasEvents = p.HasEvents }).ToList(),
                HasProperties = from.HasProperties,
            };
            AnimClipDBSec.MetadataSet copy = new AnimClipDBSec.MetadataSet() { Common = Block(source.Common) };
            foreach (AnimClipDBSec.MetadataBlock instance in source.Instances) copy.Instances.Add(Block(instance));
            return copy;
        }

        private static object PreviewAnimation(McpCall call)
        {
            string setName = call.Str("set", required: true);
            string clipName = call.Str("animation", required: true);
            string contextName = call.Str("context");
            string rigName = call.Str("rig");
            int count = call.Int("frames", 4);
            if (count < 1 || count > 12) throw new McpError("'frames' is from 1 to 12.");
            JArray times = call.Array("times");
            if (times.Count > 12) throw new McpError("Give at most 12 times.");
            List<double> at = new List<double>();
            foreach (JToken time in times) at.Add(NumberOf(time, "times"));
            List<string> views = call.StrList("views").Select(o => o.Trim().ToLowerInvariant()).ToList();
            string motion = (call.Str("root_motion") ?? "hold").Trim().ToLowerInvariant();
            if (motion != "hold" && motion != "travel") throw new McpError("'root_motion' is hold or travel.");
            int maxWidth = call.Int("max_width", 1024);
            if (maxWidth < 128) throw new McpError("'max_width' must be at least 128.");

            using (McpEditorTools.Heartbeat(call, "Drawing the animation"))
                return McpEditor.UI(() =>
                {
                    Anim animations = RequireAnimations();
                    Anim.AnimationSet set = FindSet(animations, setName);
                    Anim.ClipReference clip = FindClip(set, clipName, contextName);
                    if (!clip.Playable) throw new McpError("'" + clip.Name + "' can't be read out of ANIMATION.PAK, so there is nothing to draw.");
                    Skeleton rig = null;
                    if (rigName != null)
                    {
                        rig = RigSkeleton(animations, rigName);
                        if (rig == null) throw new McpError("There is no rig '" + rigName + "' in ANIMATION.PAK" + McpGlobalAssetChecks.DidYouMean(animations.Skeletons.Select(o => o.ToString()), rigName, " (list_skeletons lists them)."));
                    }
                    foreach (double time in at)
                        if (time < 0 || time > clip.Duration + 0.0005)
                            throw new McpError("'" + clip.Name + "' runs 0 to " + Round(clip.Duration, 3) + " s, so it has no " + time + " s.");
                    JObject report = new JObject() { ["set"] = set.Name, ["animation"] = clip.Name, ["context"] = ContextName(clip.Context), ["duration"] = Round(clip.Duration, 3) };
                    return ClipSheet(call, animations, set, clip, rig, at.Count == 0 ? null : at, views.Count == 0 ? null : views, motion == "travel" ? Anim.RootMotion.Follow : Anim.RootMotion.Ignore, maxWidth, report, count);
                });
        }

        private static int BoneIndex(Skeleton rig, string name) =>
            rig.Bones.FindIndex(o => Same(o.Name, name) || (o.Name ?? "").EndsWith(":" + name, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// A contact sheet of a clip as stick figures (one column per moment, one row per view), with sanity numbers in its caption.
        /// System.Drawing only, so it needs no viewport. UI thread.
        /// </summary>
        private static McpImage ClipSheet(McpCall call, Anim animations, Anim.AnimationSet set, Anim.ClipReference clip, Skeleton rig, List<double> times, List<string> views,
            Anim.RootMotion root, int maxWidth, JObject report, int count = 4)
        {
            HavokPackfile.AnimationClip animation = clip.Animation;
            if (animation == null || animation.FrameCount <= 0) throw new McpError("The clip has no frames to draw.");
            bool character = set.Kind != Anim.AnimationKind.Environment;
            string authored = animation.SkeletonName;
            if (rig == null) rig = RigSkeleton(animations, set.Skeleton) ?? RigSkeleton(animations, authored);
            if (rig == null) throw new McpError("Neither the set's rig nor the one the clip is authored on is in ANIMATION.PAK, so it can't be drawn.");

            //Played on its own rig, retargeted as the game does; a pose that can't be built that way is drawn on the rig it was authored on
            Retargeter retargeter = Same(authored, rig.Name) ? null : Retargeter.Between(animations, authored, rig.Name);
            List<int> frames = new List<int>();
            float step = animation.FrameDuration > 0 ? animation.FrameDuration : 1f / 30f;
            if (times != null) frames.AddRange(times.Select(t => Math.Max(0, Math.Min(animation.FrameCount - 1, (int)Math.Round(t / step)))));
            else for (int i = 0; i < count; i++) frames.Add(count == 1 ? 0 : (int)Math.Round((double)i * (animation.FrameCount - 1) / (count - 1)));

            List<List<System.Numerics.Matrix4x4>> poses = new List<List<System.Numerics.Matrix4x4>>();
            Func<int, List<System.Numerics.Matrix4x4>> sample = f => character ? Anim.SampleModelPose(clip, rig, f, root, retargeter) : Anim.SampleRigPose(clip, rig, f, root, retargeter);
            if (sample(frames[0]) == null && !Same(authored, rig.Name) && RigSkeleton(animations, authored) != null)
            {
                call.Note("It can't be posed on " + rig.Name + " (nothing in the game's data maps " + authored + " onto it), so it is drawn on " + authored + ", the rig it was authored on.");
                rig = RigSkeleton(animations, authored);
                retargeter = null;
            }
            foreach (int frame in frames)
            {
                List<System.Numerics.Matrix4x4> pose = sample(frame);
                if (pose == null) throw new McpError("Frame " + frame + " of '" + clip.Name + "' could not be posed on " + rig.Name + ".");
                poses.Add(pose);
            }
            report["drawn_on"] = rig.Name;
            if (retargeter != null) report["retargeted_from"] = authored;
            report["frames"] = new JArray(frames);
            report["times_s"] = new JArray(frames.Select(f => Round(f * step, 3)));

            //Sanity numbers a picture can't be trusted to show: head height, travel and which way it faces
            List<string> warnings = new List<string>();
            int head = BoneIndex(rig, "HEAD"), hips = BoneIndex(rig, "HIPS");
            int leftFoot = BoneIndex(rig, "LEFTFOOT"), rightFoot = BoneIndex(rig, "RIGHTFOOT"), leftToe = BoneIndex(rig, "LEFTTOEBASE"), rightToe = BoneIndex(rig, "RIGHTTOEBASE");
            if (character && head >= 0 && hips >= 0)
            {
                JArray heights = new JArray();
                for (int i = 0; i < poses.Count; i++)
                {
                    float floor = leftFoot >= 0 && rightFoot >= 0 ? Math.Min(poses[i][leftFoot].Translation.Y, poses[i][rightFoot].Translation.Y) : 0;
                    heights.Add(Round(poses[i][head].Translation.Y - floor, 3));
                    if (poses[i][head].Translation.Y < poses[i][hips].Translation.Y)
                        warnings.Add("At " + Round(frames[i] * step, 2) + " s the head is below the hips: it lies down, or the clip came in upside down.");
                }
                report["head_above_feet_m"] = heights;
                System.Numerics.Vector3 start = poses[0][hips].Translation, end = poses[poses.Count - 1][hips].Translation;
                report["hips_travel_m"] = Round(new System.Numerics.Vector2(end.X - start.X, end.Z - start.Z).Length(), 3);
                if (leftToe >= 0 && rightToe >= 0 && leftFoot >= 0 && rightFoot >= 0)
                {
                    double Yaw(List<System.Numerics.Matrix4x4> pose)
                    {
                        System.Numerics.Vector3 forward = (pose[leftToe].Translation - pose[leftFoot].Translation) + (pose[rightToe].Translation - pose[rightFoot].Translation);
                        return Math.Atan2(forward.X, forward.Z) * 180.0 / Math.PI;
                    }
                    double first = Yaw(poses[0]), last = Yaw(poses[poses.Count - 1]);
                    double turn = ((last - first) % 360 + 540) % 360 - 180;
                    report["facing_yaw_deg"] = new JArray(Round(first, 1), Round(last, 1));
                    report["turns_deg"] = Round(turn, 1);
                    if (Math.Abs(first) > 135)
                        warnings.Add("At the start it faces " + Round(first, 0) + " degrees from +Z (the way a character faces): it may have come in turned round.");
                }
            }
            if (warnings.Count != 0) report["warnings"] = new JArray(warnings);

            if (views == null) views = character ? new List<string>() { "front", "side" } : new List<string>() { "front", "side", "top" };
            foreach (string view in views)
                if (view != "front" && view != "side" && view != "top") throw new McpError("'views' are front, side and top.");
            report["views"] = new JArray(views);

            const int panelW = 220, panelH = 260, headerH = 22, labelW = 56;
            int width = labelW + panelW * poses.Count, height = headerH + panelH * views.Count;
            using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(width, height))
            using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bitmap))
            using (System.Drawing.Font font = new System.Drawing.Font("Segoe UI", 9f))
            using (System.Drawing.Pen left = new System.Drawing.Pen(System.Drawing.Color.FromArgb(40, 90, 200), 2f))
            using (System.Drawing.Pen right = new System.Drawing.Pen(System.Drawing.Color.FromArgb(200, 50, 40), 2f))
            using (System.Drawing.Pen middle = new System.Drawing.Pen(System.Drawing.Color.FromArgb(40, 40, 40), 2f))
            using (System.Drawing.Pen ground = new System.Drawing.Pen(System.Drawing.Color.FromArgb(170, 170, 170), 1f))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.White);
                for (int i = 0; i < poses.Count; i++)
                    g.DrawString(Round(frames[i] * step, 2) + " s (frame " + frames[i] + ")", font, System.Drawing.Brushes.Black, labelW + i * panelW + 6, 4);
                for (int v = 0; v < views.Count; v++)
                {
                    string view = views[v];
                    g.DrawString(view, font, System.Drawing.Brushes.Black, 4, headerH + v * panelH + panelH / 2 - 8);
                    //front: as seen by someone it faces; side: from its right, facing right; top: from above, facing up. Pose space
                    //is Y up, +Z forward, +X the figure's right: from above with forward up, its right is on the right (front mirrors it)
                    Func<System.Numerics.Vector3, System.Drawing.PointF> project = p =>
                        view == "front" ? new System.Drawing.PointF(-p.X, p.Y) : view == "side" ? new System.Drawing.PointF(p.Z, p.Y) : new System.Drawing.PointF(p.X, p.Z);
                    //One scale for the whole row, so movement between moments shows
                    float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                    foreach (List<System.Numerics.Matrix4x4> pose in poses)
                        foreach (System.Numerics.Matrix4x4 bone in pose)
                        {
                            System.Drawing.PointF q = project(bone.Translation);
                            minX = Math.Min(minX, q.X); maxX = Math.Max(maxX, q.X); minY = Math.Min(minY, q.Y); maxY = Math.Max(maxY, q.Y);
                        }
                    float scale = Math.Min((panelW - 24) / Math.Max(0.01f, maxX - minX), (panelH - 24) / Math.Max(0.01f, maxY - minY));
                    for (int i = 0; i < poses.Count; i++)
                    {
                        float originX = labelW + i * panelW + panelW / 2f, originY = headerH + v * panelH + panelH - 12;
                        System.Drawing.PointF ToPanel(System.Numerics.Vector3 p)
                        {
                            System.Drawing.PointF q = project(p);
                            return new System.Drawing.PointF(originX + (q.X - (minX + maxX) / 2f) * scale, originY - (q.Y - minY) * scale);
                        }
                        g.DrawRectangle(ground, labelW + i * panelW, headerH + v * panelH, panelW - 1, panelH - 1);
                        if (view != "top") g.DrawLine(ground, labelW + i * panelW + 4, originY, labelW + (i + 1) * panelW - 4, originY);
                        for (int b = 0; b < rig.Bones.Count && b < poses[i].Count; b++)
                        {
                            //The root is the engine's placement on the floor, and what hangs straight off it (IK targets) would cross the figure
                            int parent = rig.Bones[b].ParentIndex;
                            if (parent <= 0 || parent >= poses[i].Count) continue;
                            string name = rig.Bones[b].Name ?? "";
                            int colon = name.LastIndexOf(':');
                            string bare = colon >= 0 ? name.Substring(colon + 1) : name;
                            System.Drawing.Pen pen = bare.StartsWith("LEFT", StringComparison.OrdinalIgnoreCase) ? left : bare.StartsWith("RIGHT", StringComparison.OrdinalIgnoreCase) ? right : middle;
                            g.DrawLine(pen, ToPanel(poses[i][parent].Translation), ToPanel(poses[i][b].Translation));
                        }
                        if (head >= 0 && head < poses[i].Count)
                        {
                            System.Drawing.PointF h = ToPanel(poses[i][head].Translation);
                            g.FillEllipse(System.Drawing.Brushes.Black, h.X - 4, h.Y - 4, 8, 8);
                        }
                    }
                }

                System.Drawing.Bitmap output = width > maxWidth ? new System.Drawing.Bitmap(bitmap, maxWidth, Math.Max(1, (int)Math.Round(height * (double)maxWidth / width))) : bitmap;
                try
                {
                    using (MemoryStream png = new MemoryStream())
                    {
                        output.Save(png, System.Drawing.Imaging.ImageFormat.Png);
                        report["legend"] = "blue: left limbs, red: right, black: spine and head (dot); one scale per row; side view faces right, top view faces up.";
                        return new McpImage()
                        {
                            Data = png.ToArray(),
                            MimeType = "image/png",
                            Caption = report.ToString(Newtonsoft.Json.Formatting.None),
                        };
                    }
                }
                finally
                {
                    if (!ReferenceEquals(output, bitmap)) output.Dispose();
                }
            }
        }
        #endregion

        #region What characters and props can play
        private static readonly string[] ProfileParameters = { "anim_set", "anim_tree_set", "reference_skeleton", "display_model", "character_class", "attribute_set" };

        /// <summary>
        /// A parameter's effective value on chain[k] (an entity of comps[k]; comps[0] is where the chain starts), as instancing
        /// gives it to this placement - the one rule describe_npc and set_npc use (<see cref="McpCharacterTools.Resolve"/>): a link
        /// out of the parameter feeds it (a composite pin, followed up into the instance placing the composite; a Variable*
        /// function's initial_value, which is how Android_NPC fixes its character_class; or another entity, read as the game
        /// runs), else the outermost alias's value, its own value, or the default. The route is noted.
        /// </summary>
        internal static JToken Effective(Commands commands, List<Composite> comps, List<Entity> chain, int k, string parameter, List<string> route)
        {
            McpCharacterTools.Setting setting = McpCharacterTools.Resolve(commands, comps, chain, k, ShortGuidUtils.Generate(parameter));
            route.AddRange(setting.Route);
            if (setting.Source == "runtime" && setting.Value == null)
                return "(set at run time by " + setting.RuntimeBy + ")";
            return setting.Value;
        }
        private static object GetCharacterAnimationProfile(McpCall call)
        {
            int limit = Limit(call, 20, 200);
            using (McpEditorTools.Heartbeat(call, "Resolving the character"))
                return McpEditor.UI(() =>
                {
                    LevelContent content = McpEditor.RequireLevel(forEditing: false);
                    Commands commands = content.Level.Commands;
                    Composite composite = McpScript.FindComposite(commands, call.Str("composite", required: true));
                    Entity entity = McpScript.FindEntity(commands, composite, call.Str("entity", required: true));

                    //The Characters it stands for: itself, or those inside the archetype it places (instances below it, from it down)
                    List<Tuple<List<Entity>, List<Composite>>> inside = new List<Tuple<List<Entity>, List<Composite>>>();
                    if (entity is FunctionEntity own && own.function.IsFunctionType && own.function.AsFunctionType == FunctionType.Character)
                        inside.Add(Tuple.Create(new List<Entity>(), new List<Composite>()));
                    else if (entity is FunctionEntity instance && !instance.function.IsFunctionType)
                    {
                        Queue<Tuple<List<Entity>, List<Composite>>> pending = new Queue<Tuple<List<Entity>, List<Composite>>>();
                        Composite first = commands.GetComposite(instance.function);
                        if (first != null) pending.Enqueue(Tuple.Create(new List<Entity>(), new List<Composite>() { first }));
                        while (pending.Count != 0 && inside.Count < 8)
                        {
                            Tuple<List<Entity>, List<Composite>> at = pending.Dequeue();
                            Composite here = at.Item2[at.Item2.Count - 1];
                            foreach (FunctionEntity function in here.functions)
                            {
                                if (function.function.IsFunctionType)
                                {
                                    if (function.function.AsFunctionType == FunctionType.Character)
                                        inside.Add(Tuple.Create(at.Item1.Concat(new[] { (Entity)function }).ToList(), at.Item2));
                                    continue;
                                }
                                Composite next = commands.GetComposite(function.function);
                                if (next == null || at.Item2.Contains(next) || at.Item2.Count > 5) continue;
                                pending.Enqueue(Tuple.Create(at.Item1.Concat(new[] { (Entity)function }).ToList(), at.Item2.Concat(new[] { next }).ToList()));
                            }
                        }
                    }
                    if (inside.Count == 0)
                        throw new McpError(McpScript.EntityName(commands, composite, entity) + " is a " + McpScript.TypeName(commands, composite, entity) + " and holds no Character. Give a Character, or an instance of an NPC archetype (find_entities type Character lists them).");

                    //Every placement from the level's root, so instance parameters passed down from above count
                    Composite root = commands.EntryPoints?.FirstOrDefault();
                    McpPlacements walker = new McpPlacements(commands);
                    List<McpPlacements.Placement> placements = new List<McpPlacements.Placement>();
                    int total = root == null ? 0 : walker.PlacementsOf(root, composite, entity, placements, limit, call.Cancel);
                    if (total == 0)
                    {
                        placements.Add(new McpPlacements.Placement() { Composite = composite, Chain = new List<Entity>() { entity } });
                        call.Note(composite.name + " is not placed under the level's root, so only what it sets itself is resolved (values passed in from above are unknown).");
                    }
                    else if (total > placements.Count)
                        call.Note(total + " placements; the first " + placements.Count + " are resolved (raise limit for more).");

                    Dictionary<string, JObject> groups = new Dictionary<string, JObject>();
                    List<JObject> order = new List<JObject>();
                    HashSet<string> animSets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (McpPlacements.Placement placement in placements)
                        foreach (Tuple<List<Entity>, List<Composite>> character in inside)
                        {
                            List<Entity> chain = placement.Chain.Concat(character.Item1).ToList();
                            List<Composite> comps = new List<Composite>() { total == 0 ? composite : root };
                            for (int i = 0; i < chain.Count - 1; i++)
                            {
                                Composite next = McpScript.InstancedComposite(commands, chain[i]);
                                if (next == null) break;
                                comps.Add(next);
                            }
                            if (comps.Count != chain.Count) continue;
                            JObject row = new JObject();
                            Entity target = chain[chain.Count - 1];
                            row["character"] = new JObject() { ["composite"] = comps[comps.Count - 1].name, ["id"] = McpScript.Id(target.shortGUID), ["name"] = McpScript.EntityName(commands, comps[comps.Count - 1], target) };
                            foreach (string parameter in ProfileParameters)
                            {
                                List<string> route = new List<string>();
                                JToken value = Effective(commands, comps, chain, chain.Count - 1, parameter, route);
                                row[parameter] = new JObject() { ["value"] = value ?? JValue.CreateNull(), ["via"] = new JArray(route) };
                                if (parameter == "anim_set" && value != null && value.Type == JTokenType.String) animSets.Add((string)value);
                            }
                            string key = row.ToString(Newtonsoft.Json.Formatting.None);
                            if (!groups.TryGetValue(key, out JObject group))
                            {
                                group = new JObject() { ["placements"] = 0 };
                                if (total != 0) group["example_placement"] = new JArray(placement.Chain.Select(o => McpScript.Id(o.shortGUID)));
                                foreach (JProperty property in row.Properties()) group[property.Name] = property.Value;
                                groups[key] = group;
                                order.Add(group);
                            }
                            group["placements"] = (int)group["placements"] + 1;
                        }

                    JObject result = new JObject()
                    {
                        ["composite"] = composite.name,
                        ["entity"] = McpScript.Brief(commands, composite, entity),
                        ["placements"] = total,
                        ["resolved"] = new JArray(order),
                    };

                    //What each set it ends up with can play
                    Anim animations = Singleton.Animations;
                    if (animations != null && animations.Loaded && animSets.Count != 0)
                    {
                        JObject sets = new JObject();
                        foreach (string name in animSets)
                        {
                            Anim.AnimationSet set = animations.GetSet(name);
                            if (set == null) { sets[name] = new JObject() { ["problem"] = "not an animation set" + McpGlobalAssetChecks.DidYouMean(animations.Sets.Select(o => o.Name), name, ".") }; continue; }
                            JObject info = new JObject()
                            {
                                ["kind"] = IsCharacter(set) ? "character" : "environment",
                                ["rig"] = set.Skeleton,
                                ["rig_in_pak"] = RigSkeleton(animations, set.Skeleton) != null,
                                ["contexts"] = new JArray(set.Contexts.Where(o => o.Clips.Count != 0).Select(o => new JObject() { ["name"] = ContextName(o), ["clips"] = o.Clips.Count })),
                            };
                            List<string> alsoOnRig = McpGlobalAssetChecks.SetsPlayableOn(set.Skeleton).Where(o => !ReferenceEquals(o.Item1, set) && IsCharacter(o.Item1)).Select(o => o.Item1.Name + (o.Item2 ? " (retargeted)" : "")).ToList();
                            if (alsoOnRig.Count != 0) info["other_sets_on_its_rig"] = new JArray(alsoOnRig.Take(20));
                            sets[set.Name] = info;
                        }
                        result["anim_sets"] = sets;
                        call.Note("A character plays clips of the set its CMD_PlayAnimation names in AnimationSet (normally its anim_set); list_animations set=<anim_set> lists them, and a named context's clips play only in that state.");
                    }
                    return result;
                });
        }

        /// <summary>The animation entry a prop composite's EnvironmentModelReference uses, with that entity; null if it has none.</summary>
        private static Tuple<FunctionEntity, EnvironmentAnimations.EnvironmentAnimation> PropEntry(Composite composite)
        {
            foreach (FunctionEntity function in composite.functions)
            {
                if (!function.function.IsFunctionType || function.function.AsFunctionType != FunctionType.EnvironmentModelReference) continue;
                EnvironmentAnimations.EnvironmentAnimation entry = McpGlobalAssetChecks.AnimatedModelOf(function);
                if (entry != null) return Tuple.Create(function, entry);
            }
            return null;
        }

        /// <summary>Each entity of a composite drawing geometry, with the RENDERABLE_INSTANCE resource ids it draws through.</summary>
        private static List<Tuple<FunctionEntity, List<ShortGuid>>> DrawnBy(Composite composite)
        {
            List<Tuple<FunctionEntity, List<ShortGuid>>> drawn = new List<Tuple<FunctionEntity, List<ShortGuid>>>();
            foreach (FunctionEntity function in composite.functions)
            {
                List<ResourceReference> references = (function.GetParameter(ShortGuids.resource)?.content as cResource)?.value ?? function.resources;
                List<ShortGuid> ids = references?.Where(o => o != null && o.resource_type == ResourceType.RENDERABLE_INSTANCE).Select(o => o.resource_id).Distinct().ToList();
                if (ids != null && ids.Count != 0) drawn.Add(Tuple.Create(function, ids));
            }
            return drawn;
        }

        /// <summary>How many of a composite's drawn parts an entry's bones move: the bone each part's resource id is mapped to.</summary>
        public static int PartsMoved(Composite composite, EnvironmentAnimations.EnvironmentAnimation entry, out int parts)
        {
            List<Tuple<FunctionEntity, List<ShortGuid>>> drawn = DrawnBy(composite);
            parts = drawn.Count;
            if (entry?.BoneMappings == null) return 0;
            return drawn.Count(o => o.Item2.Any(id => entry.BoneMappings.Contains(id)));
        }

        private static object DescribeAnimatedProp(McpCall call)
        {
            bool list = call.Bool("list");
            string compositeName = call.Str("composite");
            if (!list && compositeName == null) throw new McpError("Give composite (a prop's composite), or list:true for every animated prop of the open level.");
            if (list && compositeName != null) throw new McpError("Give composite or list, not both.");
            int limit = Limit(call, 50, 500);
            string[] words = Words(call.Str("filter"));

            using (McpEditorTools.Heartbeat(call, "Reading the props"))
                return McpEditor.UI(() =>
                {
                    LevelContent content = McpEditor.RequireLevel(forEditing: false);
                    Commands commands = content.Level.Commands;
                    Anim animations = Singleton.Animations != null && Singleton.Animations.Loaded ? Singleton.Animations : null;
                    Dictionary<ShortGuid, int> instances = new Dictionary<ShortGuid, int>();
                    foreach (Composite composite in commands.Entries)
                        foreach (FunctionEntity function in composite.functions)
                            if (!function.function.IsFunctionType)
                                instances[function.function] = (instances.TryGetValue(function.function, out int n) ? n : 0) + 1;

                    if (list)
                    {
                        List<JObject> rows = new List<JObject>();
                        int matched = 0;
                        foreach (Composite composite in commands.Entries.OrderBy(o => o.name, StringComparer.OrdinalIgnoreCase))
                        {
                            Tuple<FunctionEntity, EnvironmentAnimations.EnvironmentAnimation> prop = PropEntry(composite);
                            if (prop == null || !AllWords(words, composite.name, prop.Item2.SkeletonName)) continue;
                            if (++matched > limit) continue;
                            int moved = PartsMoved(composite, prop.Item2, out int drawn);
                            JObject row = new JObject() { ["composite"] = composite.name, ["entry"] = prop.Item2.ID, ["rig"] = prop.Item2.SkeletonName, ["parts_moved"] = moved, ["parts"] = drawn };
                            if (animations != null)
                                row["sets"] = new JArray(animations.Sets.Where(o => !string.IsNullOrEmpty(prop.Item2.SkeletonName) && Same(o.Skeleton, prop.Item2.SkeletonName)).Select(o => o.Name + " (" + o.ClipCount + " clips)"));
                            row["instances"] = instances.TryGetValue(composite.shortGUID, out int count) ? count : 0;
                            rows.Add(row);
                        }
                        if (matched > limit) call.Note(matched + " animated props; " + limit + " are listed (filter or raise limit).");
                        call.Note("describe_animated_prop with composite gives one in full, with its clips and the wiring to play them.");
                        return new JObject() { ["total"] = matched, ["props"] = new JArray(rows) };
                    }

                    Composite asked = McpScript.FindComposite(commands, compositeName);
                    Composite propComposite = asked;
                    Tuple<FunctionEntity, EnvironmentAnimations.EnvironmentAnimation> found = PropEntry(asked);
                    if (found == null)
                    {
                        //A room or wrapper composite: the props placed in it
                        List<Composite> placed = asked.functions.Where(o => !o.function.IsFunctionType).Select(o => commands.GetComposite(o.function)).Where(o => o != null && PropEntry(o) != null).Distinct().ToList();
                        if (placed.Count == 1) { propComposite = placed[0]; found = PropEntry(propComposite); call.Note(asked.name + " places the animated prop " + propComposite.name + "; that is described."); }
                        else if (placed.Count > 1)
                            throw new McpError(asked.name + " places " + placed.Count + " animated props: " + string.Join(", ", placed.Take(10).Select(o => o.name)) + ". Give one of them as composite.");
                        else
                            throw new McpError(asked.name + " has no EnvironmentModelReference with an animation entry, so nothing in it plays an environment animation. "
                                + "An unrigged prop is animated by keying its ModelReference's position with a CAGEAnimation (animate_parameters, with a link from finished back to start to loop it); "
                                + "a prop skinned in Blender to a retail environment rig comes in animatable with import_model skeleton:<rig>.");
                    }
                    FunctionEntity emr = found.Item1;
                    EnvironmentAnimations.EnvironmentAnimation entry = found.Item2;
                    Skeleton rig = animations == null ? null : RigSkeleton(animations, entry.SkeletonName);

                    JObject result = new JObject()
                    {
                        ["composite"] = propComposite.name,
                        ["environment_model_reference"] = McpScript.Brief(commands, propComposite, emr),
                        ["entry"] = DescribeEntry(entry, commands),
                    };
                    JArray parts = new JArray();
                    foreach (Tuple<FunctionEntity, List<ShortGuid>> part in DrawnBy(propComposite))
                    {
                        int bone = part.Item2.Select(id => entry.BoneMappings?.IndexOf(id) ?? -1).Where(o => o >= 0).DefaultIfEmpty(-1).First();
                        JObject row = new JObject() { ["entity"] = McpScript.EntityName(commands, propComposite, part.Item1), ["id"] = McpScript.Id(part.Item1.shortGUID) };
                        row["bone"] = bone;
                        if (bone >= 0 && rig != null && bone < rig.Bones.Count) row["bone_name"] = rig.Bones[bone].Name;
                        if (bone < 0) row["moves"] = false;
                        parts.Add(row);
                    }
                    result["parts"] = parts;

                    //Who plays it already: a PlayEnvironmentAnimation whose geometry is the prop's own geometry, or an instance of the prop
                    ShortGuid geometry = ShortGuidUtils.Generate("geometry");
                    HashSet<ShortGuid> own = new HashSet<ShortGuid>(propComposite.functions.Select(o => o.shortGUID));
                    List<JObject> players = new List<JObject>();
                    HashSet<string> playedSets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (Composite composite in commands.Entries)
                        foreach (FunctionEntity function in composite.functions)
                        {
                            if (!function.function.IsFunctionType || function.function.AsFunctionType != FunctionType.PlayEnvironmentAnimation) continue;
                            foreach (EntityConnector link in function.childLinks.Where(o => o.thisParamID == geometry))
                            {
                                Entity target = composite.GetEntityByID(link.linkedEntityID);
                                bool hit = (composite == propComposite && own.Contains(link.linkedEntityID))
                                    || (target is FunctionEntity placed && !placed.function.IsFunctionType && placed.function == propComposite.shortGUID);
                                if (!hit) continue;
                                string setName = McpGlobalAssetChecks.TextOf(function, "AnimationSet");
                                if (!string.IsNullOrEmpty(setName)) playedSets.Add(setName);
                                if (players.Count < 20)
                                    players.Add(new JObject()
                                    {
                                        ["composite"] = composite.name,
                                        ["id"] = McpScript.Id(function.shortGUID),
                                        ["name"] = McpScript.EntityName(commands, composite, function),
                                        ["animation_set"] = setName,
                                        ["animation"] = McpGlobalAssetChecks.TextOf(function, "Animation"),
                                        ["geometry"] = McpScript.EntityName(commands, composite, target),
                                    });
                            }
                        }
                    result["played_by"] = new JArray(players);

                    if (animations != null && !string.IsNullOrEmpty(entry.SkeletonName))
                    {
                        result["sets"] = new JArray(animations.Sets.Where(o => Same(o.Skeleton, entry.SkeletonName)).Select(set => new JObject()
                        {
                            ["name"] = set.Name,
                            ["already_played_here"] = playedSets.Contains(set.Name),
                            ["clips"] = new JArray(set.Contexts.SelectMany(o => o.Clips).Where(o => o != null).Take(limit).Select(o => new JObject() { ["name"] = ClipName(o), ["duration"] = Round(o.Duration, 3) })),
                        }));
                        if (rig == null) call.Note("Its rig '" + entry.SkeletonName + "' is not in ANIMATION.PAK.");
                    }
                    result["instances"] = instances.TryGetValue(propComposite.shortGUID, out int placedCount) ? placedCount : 0;
                    result["recipe"] = "Play one of its clips: in the composite that places " + McpScript.CompositeLeaf(propComposite) + ", create_entities a PlayEnvironmentAnimation {AnimationSet: <set>, Animation: <clip>} "
                        + "and add_links {from: <it>, param: 'geometry', to: <the instance>, to_param: 'reference'}; start it with a link to its apply_start (or play_on_reset: true). "
                        + "Inside the prop's own composite, link geometry to " + McpScript.EntityName(commands, propComposite, emr) + " instead.";
                    return result;
                });
        }

        /// <summary>An environment-animation entry as the tools describe it everywhere (set_animated_model too).</summary>
        public static JObject DescribeEntry(EnvironmentAnimations.EnvironmentAnimation entry, Commands commands)
        {
            if (entry == null) return null;
            JObject row = new JObject()
            {
                ["id"] = entry.ID,
                ["rig"] = string.IsNullOrEmpty(entry.SkeletonName) ? null : entry.SkeletonName,
                ["bones"] = entry.BoneMappings?.Count ?? 0,
                ["meshes"] = entry.MeshMappings?.Count ?? 0,
                ["bind_poses"] = entry.InverseBindPoses?.Count ?? 0,
                ["helpers"] = entry.HelperMatrices?.Count ?? 0,
                ["animation_set"] = entry.AnimationSet == 0 ? "(by rig)" : entry.AnimationSet.ToString(),
            };
            if (commands != null)
            {
                //The composites whose EnvironmentModelReference uses it
                List<string> users = commands.Entries.Where(c => c.functions.Any(f => ReferenceEquals(McpGlobalAssetChecks.AnimatedModelOf(f), entry))).Select(c => c.name).ToList();
                row["used_by"] = new JArray(users.Take(10));
                if (users.Count > 10) row["used_by_count"] = users.Count;
            }
            return row;
        }

        private static object CheckAnimations(McpCall call)
        {
            int limit = Limit(call, 100, 2000);
            using (McpEditorTools.Heartbeat(call, "Checking animations"))
                return McpEditor.UI(() =>
                {
                    LevelContent content = McpEditor.RequireLevel(forEditing: false);
                    Commands commands = content.Level.Commands;
                    RequireAnimations();
                    List<Composite> composites = call.Has("composite") ? new List<Composite>() { McpScript.FindComposite(commands, call.Str("composite")) } : commands.Entries;
                    int checkedCount = 0, problemCount = 0;
                    JArray rows = new JArray();
                    foreach (Composite composite in composites)
                        foreach (FunctionEntity function in composite.functions)
                        {
                            if (!function.function.IsFunctionType) continue;
                            FunctionType type = function.function.AsFunctionType;
                            if (type != FunctionType.CMD_PlayAnimation && type != FunctionType.CHR_PlaySecondaryAnimation && type != FunctionType.PlayEnvironmentAnimation && type != FunctionType.Character) continue;
                            checkedCount++;
                            List<string> problems = McpGlobalAssetChecks.AnimationProblems(commands, composite, function);
                            if (problems.Count == 0) continue;
                            problemCount++;
                            if (rows.Count >= limit) continue;
                            rows.Add(new JObject()
                            {
                                ["composite"] = composite.name,
                                ["id"] = McpScript.Id(function.shortGUID),
                                ["name"] = McpScript.EntityName(commands, composite, function),
                                ["type"] = type.ToString(),
                                ["animation_set"] = McpGlobalAssetChecks.TextOf(function, type == FunctionType.Character ? "anim_set" : "AnimationSet"),
                                ["animation"] = type == FunctionType.Character ? null : McpGlobalAssetChecks.TextOf(function, "Animation"),
                                ["problems"] = new JArray(problems),
                            });
                        }
                    if (problemCount > rows.Count) call.Note(problemCount + " entities have problems; " + rows.Count + " are listed (raise limit, or give composite).");
                    return new JObject() { ["checked"] = checkedCount, ["with_problems"] = problemCount, ["problems"] = rows };
                });
        }

        private static object ListCameraClips(McpCall call)
        {
            int limit = Limit(call, 100, 2000);
            string[] words = Words(call.Str("filter"));
            return McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                Commands commands = content.Level.Commands;
                Dictionary<string, JObject> clips = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
                foreach (Composite composite in commands.Entries)
                    foreach (FunctionEntity function in composite.functions)
                    {
                        if (!function.function.IsFunctionType || function.function.AsFunctionType != FunctionType.CameraPlayAnimation) continue;
                        string file = McpGlobalAssetChecks.TextOf(function, "data_file") ?? "";
                        if (!AllWords(words, file)) continue;
                        if (!clips.TryGetValue(file, out JObject row))
                            clips[file] = row = new JObject() { ["data_file"] = file, ["used_by"] = new JArray() };
                        JObject use = new JObject() { ["composite"] = composite.name, ["id"] = McpScript.Id(function.shortGUID), ["name"] = McpScript.EntityName(commands, composite, function) };
                        ParameterData shot = function.GetParameter("shot_number")?.content;
                        if (shot != null) use["shot_number"] = McpValues.ToJson(shot, commands);
                        if (((JArray)row["used_by"]).Count < 10) ((JArray)row["used_by"]).Add(use);
                    }
                List<JObject> listed = clips.Values.OrderBy(o => (string)o["data_file"], StringComparer.OrdinalIgnoreCase).ToList();
                if (listed.Count == 0) call.Note("No CameraPlayAnimation in the open level" + (words.Length == 0 ? "" : " matches") + ". For a camera move of your own, create_camera_animation keys a CameraResource along a path with a CAGEAnimation and wires it to play.");
                JObject result = new JObject() { ["level"] = content.Level.Name };
                McpPaging.Page(call, listed, result, "clips", o => o, 100, 2000);
                return result;
            });
        }
        #endregion

        #region Blend sets
        private sealed class BlendUser
        {
            public AnimClipDB Database;
            public AnimClipDB.Context Context;
            public AnimClipDB.BlendSet Entry;
        }

        private static List<GlobalAnimClipDB.BlendSet> AllBlendSets(Anim animations)
        {
            List<GlobalAnimClipDB.BlendSet> sets = animations.ClipIndex?.BlendSets;
            if (sets == null) throw new McpError("ANIMATION.PAK holds no blend sets (its ANIM_CLIP_DB.BIN could not be read).");
            return sets;
        }

        private static GlobalAnimClipDB.BlendSet FindBlendSet(Anim animations, string name)
        {
            name = (name ?? "").Trim().Replace('/', '\\');
            List<GlobalAnimClipDB.BlendSet> all = AllBlendSets(animations);
            GlobalAnimClipDB.BlendSet hit = all.FirstOrDefault(o => Same(o.ToString(), name));
            if (hit != null) return hit;
            List<GlobalAnimClipDB.BlendSet> named = all.Where(o => Same(o.Name, name)).ToList();
            if (named.Count == 1) return named[0];
            if (named.Count > 1)
                throw new McpError("'" + name + "' is the name of " + named.Count + " blend sets: " + string.Join(", ", named.Select(o => o.ToString())) + ". Give the key.");
            throw new McpError("There is no blend set '" + name + "'" + McpGlobalAssetChecks.DidYouMean(all.Select(o => o.ToString()), name, " (list_blend_sets lists them)."));
        }

        /// <summary>Which characters and contexts can ask for a blend set: the game only reaches one through a character's own clip database.</summary>
        private static List<BlendUser> BlendUsers(Anim animations, GlobalAnimClipDB.BlendSet set)
        {
            string key = set.ToString();
            List<BlendUser> users = new List<BlendUser>();
            foreach (AnimClipDB database in animations.ClipDatabases)
            {
                foreach (AnimClipDB.BlendSet reference in database.BlendSets)
                    if (Same(reference.Filename, key)) users.Add(new BlendUser() { Database = database, Entry = reference });
                foreach (AnimClipDB.Context context in database.Contexts)
                    foreach (AnimClipDB.BlendSet reference in context.BlendSets)
                        if (Same(reference.Filename, key)) users.Add(new BlendUser() { Database = database, Context = context, Entry = reference });
            }
            return users;
        }

        private static string Axes(GlobalAnimClipDB.BlendSet set)
        {
            List<string> axes = new List<string>() { set.BlendPropertyX };
            if (set.Dimensions > 1) axes.Add(set.BlendPropertyY);
            if (set.Dimensions > 2) axes.Add(set.BlendPropertyZ);
            return string.Join(", ", axes.Select(o => string.IsNullOrEmpty(o) ? "(nothing)" : o));
        }

        private static JObject BlendUserJson(BlendUser user)
        {
            JObject row = new JObject() { ["character"] = user.Database.Character };
            if (user.Context != null) row["context"] = user.Context.Name.Length == 0 ? "(default)" : user.Context.Name;
            if (!Same(user.Entry.Name, user.Entry.Filename)) row["known_as"] = user.Entry.Name;
            return row;
        }

        private static object ListBlendSets(McpCall call)
        {
            string filter = (call.Str("filter") ?? "").Trim();
            string name = call.Str("name");
            using (McpEditorTools.Heartbeat(call, "Reading ANIMATION.PAK"))
                return McpEditor.UI(() =>
                {
                    Anim animations = RequireAnimations();
                    if (name != null)
                    {
                        GlobalAnimClipDB.BlendSet set = FindBlendSet(animations, name);
                        JArray clips = new JArray();
                        for (int i = 0; i < set.Clips.Count; i++)
                        {
                            JObject clip = new JObject() { ["index"] = i, ["name"] = set.Clips[i].Name };
                            if (i < set.Durations.Length) clip["duration"] = Round(set.Durations[i], 4);
                            if (i < set.Mirrored.Length && set.Mirrored[i]) clip["mirrored"] = true;
                            clip["used_by_points"] = set.InstanceToClip.Count(o => o == i);
                            clips.Add(clip);
                        }
                        JArray points = new JArray();
                        for (int i = 0; i < set.PlaySpeeds.Length; i++)
                        {
                            JArray position = new JArray();
                            for (int d = 0; d < set.Dimensions; d++)
                            {
                                int at = i * set.Dimensions + d;
                                position.Add(at < set.InstanceProperties.Length ? (JToken)Round(set.InstanceProperties[at], 4) : JValue.CreateNull());
                            }
                            JObject point = new JObject() { ["index"] = i, ["position"] = position };
                            if (i < set.InstanceToClip.Length)
                            {
                                int clip = set.InstanceToClip[i];
                                point["clip"] = clip;
                                point["clip_name"] = clip < set.Clips.Count ? set.Clips[clip].Name : "(missing clip " + clip + ")";
                            }
                            point["speed"] = Round(set.PlaySpeeds[i], 4);
                            points.Add(point);
                        }
                        return new JObject()
                        {
                            ["key"] = set.ToString(),
                            ["name"] = set.Name,
                            ["anim_set"] = set.AnimSet,
                            ["context"] = set.AnimSetContext,
                            ["dimensions"] = set.Dimensions,
                            ["driven_by"] = Axes(set),
                            ["clips"] = clips,
                            ["points"] = points,
                            ["users"] = new JArray(BlendUsers(animations, set).Select(BlendUserJson)),
                            ["note"] = "Clips, which clip each point plays and point speeds can change (edit_blend_set); where the points sit is baked into the game's lookup and is fixed.",
                        };
                    }

                    Dictionary<string, int> userCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (AnimClipDB database in animations.ClipDatabases)
                        foreach (AnimClipDB.BlendSet reference in database.BlendSets.Concat(database.Contexts.SelectMany(o => o.BlendSets)))
                        {
                            userCounts.TryGetValue(reference.Filename ?? "", out int count);
                            userCounts[reference.Filename ?? ""] = count + 1;
                        }
                    string[] words = Words(filter);
                    List<GlobalAnimClipDB.BlendSet> listed = AllBlendSets(animations)
                        .Where(o => filter.Length == 0 || AllWords(words, o.ToString()) || o.Clips.Any(c => AllWords(words, c.Name)))
                        .OrderBy(o => o.ToString(), StringComparer.OrdinalIgnoreCase).ToList();
                    if (filter.Length != 0 && listed.Count == 0)
                        call.Note("Nothing matches '" + filter + "'" + McpGlobalAssetChecks.DidYouMean(AllBlendSets(animations).Select(o => o.ToString()), filter, "."));
                    JObject result = new JObject();
                    McpPaging.Page(call, listed, result, "blend_sets", o =>
                    {
                        userCounts.TryGetValue(o.ToString(), out int users);
                        return new JObject()
                        {
                            ["key"] = o.ToString(),
                            ["name"] = o.Name,
                            ["dimensions"] = o.Dimensions,
                            ["driven_by"] = Axes(o),
                            ["clips"] = o.Clips.Count,
                            ["points"] = o.PlaySpeeds.Length,
                            ["users"] = users,
                        };
                    }, 100);
                    return result;
                });
        }

        private static AnimClipDB FindCharacter(Anim animations, string character)
        {
            character = (character ?? "").Trim();
            AnimClipDB database = animations.ClipDatabases.FirstOrDefault(o => Same(o.Character, character));
            if (database != null) return database;
            throw new McpError("There is no character (animation set) '" + character + "'" + McpGlobalAssetChecks.DidYouMean(animations.ClipDatabases.Select(o => o.Character), character, " (list_animation_sets lists them)."));
        }

        private static AnimClipDB.Context FindCharacterContext(AnimClipDB database, string context)
        {
            string wanted = context.Trim();
            AnimClipDB.Context hit = database.Contexts.FirstOrDefault(o => o.Name.Length != 0 && Same(o.Name, wanted));
            if (hit == null && (wanted.Length == 0 || Same(wanted, "default") || Same(wanted, "(default)")))
                hit = database.Contexts.FirstOrDefault(o => o.Name.Length == 0);
            if (hit == null)
                throw new McpError(database.Character + " has no context '" + context + "'. Its contexts are: " + string.Join(", ", database.Contexts.Select(o => o.Name.Length == 0 ? "(default)" : o.Name)) + ".");
            return hit;
        }

        private static object EditBlendSet(McpCall call)
        {
            string name = call.Str("name", required: true);
            JArray clipEdits = call.Array("clips");
            JArray pointEdits = call.Array("points");
            JArray adds = call.Array("add_users");
            JArray removes = call.Array("remove_users");
            bool dryRun = call.Bool("dry_run");
            if (clipEdits.Count + pointEdits.Count + adds.Count + removes.Count == 0)
                throw new McpError("Give clips, points, add_users or remove_users to change.");
            if (!dryRun)
            {
                CloseOrRefuse(call, "ANIMATION.PAK can't be written while it holds it open");
                RefuseIfAnimationPakChanged();
            }

            using (McpEditorTools.Heartbeat(call, dryRun ? "Checking the blend set" : "Writing ANIMATION.PAK"))
                return McpEditor.UI(() =>
                {
                    Anim animations = RequireAnimations();
                    GlobalAnimClipDB.BlendSet set = FindBlendSet(animations, name);
                    string key = set.ToString();
                    List<BlendUser> users = BlendUsers(animations, set);
                    List<string> changes = new List<string>();
                    List<Action> apply = new List<Action>();

                    //Everything is checked before anything changes, so a bad entry leaves the set as it was
                    foreach (JToken token in clipEdits)
                    {
                        JObject edit = token as JObject ?? throw new McpError("Each clips entry is an object: {index, name?, duration?, mirrored?}.");
                        CheckKeys(edit, "A clips entry", "index", "name", "duration", "mirrored");
                        int i = IndexOf(edit, set.Clips.Count, "clip");
                        string clipName = ItemStr(edit, "name");
                        if (clipName != null)
                        {
                            clipName = clipName.Trim();
                            if (clipName.Length == 0) throw new McpError("A clip's name can't be empty.");
                            //Only a case-sensitive hash is stored, and it has to match the user's own clip: take that clip's exact spelling
                            List<string> spellings = users.Select(u => animations.GetSet(u.Database.Character)).Where(o => o != null)
                                .SelectMany(o => o.Contexts).SelectMany(o => o.Clips).Where(o => o != null)
                                .Select(o => o.Name).Where(o => Same(o, clipName)).Distinct().ToList();
                            string known = spellings.Contains(clipName) ? clipName : spellings.FirstOrDefault();
                            if (known != null) clipName = known;
                            else if (Same(clipName, set.Clips[i].Name)) clipName = set.Clips[i].Name;
                            if (clipName != set.Clips[i].Name)
                            {
                                changes.Add("clip " + i + " name: " + set.Clips[i].Name + " -> " + clipName);
                                if (known == null)
                                    call.Note("None of this blend set's users has an animation called '" + clipName + "' (list_animations lists them); a blend set names clips as its character's set does.");
                                string captured = clipName;
                                GlobalAnimClipDB.BlendClip clip = set.Clips[i];
                                apply.Add(() =>
                                {
                                    //Only the hash is stored: register the name or it reads back as a number
                                    animations.AddName(captured, true);
                                    clip.Name = captured;
                                });
                            }
                        }
                        if (edit["duration"] != null && edit["duration"].Type != JTokenType.Null)
                        {
                            if (i >= set.Durations.Length) throw new McpError("Clip " + i + " has no stored length.");
                            float duration = NumberOf(edit["duration"], "duration");
                            if (duration < 0) throw new McpError("A duration can't be negative.");
                            if (Math.Abs(set.Durations[i] - duration) > 1e-6f)
                            {
                                changes.Add("clip " + i + " duration: " + Round(set.Durations[i], 4) + " -> " + duration);
                                apply.Add(() => set.Durations[i] = duration);
                            }
                        }
                        if (edit["mirrored"] != null && edit["mirrored"].Type != JTokenType.Null)
                        {
                            if (i >= set.Mirrored.Length) throw new McpError("Clip " + i + " has no mirrored flag.");
                            if (edit["mirrored"].Type != JTokenType.Boolean) throw new McpError("'mirrored' is true or false.");
                            bool mirrored = (bool)edit["mirrored"];
                            if (set.Mirrored[i] != mirrored)
                            {
                                changes.Add("clip " + i + " mirrored: " + set.Mirrored[i] + " -> " + mirrored);
                                apply.Add(() => set.Mirrored[i] = mirrored);
                            }
                        }
                    }

                    foreach (JToken token in pointEdits)
                    {
                        JObject edit = token as JObject ?? throw new McpError("Each points entry is an object: {index, clip?, speed?}.");
                        CheckKeys(edit, "A points entry", "index", "clip", "speed");
                        int i = IndexOf(edit, Math.Min(set.PlaySpeeds.Length, set.InstanceToClip.Length), "blend point");
                        JToken clipToken = edit["clip"];
                        if (clipToken != null && clipToken.Type != JTokenType.Null)
                        {
                            int clip;
                            if (clipToken.Type == JTokenType.Integer) clip = (int)clipToken;
                            else if (clipToken.Type != JTokenType.String) throw new McpError("A point's 'clip' is the clip's index or name.");
                            else
                            {
                                string wanted = ((string)clipToken ?? "").Trim();
                                clip = set.Clips.FindIndex(o => Same(o.Name, wanted));
                                if (clip < 0) throw new McpError("The blend set has no clip '" + wanted + "'. Its clips: " + string.Join(", ", set.Clips.Select((o, n) => n + " " + o.Name)) + ".");
                            }
                            if (clip < 0 || clip >= set.Clips.Count || clip > 255)
                                throw new McpError("Clip " + clip + " is out of range: the blend set has " + set.Clips.Count + " clips.");
                            if (set.InstanceToClip[i] != clip)
                            {
                                changes.Add("point " + i + " plays: clip " + set.InstanceToClip[i] + " -> clip " + clip + " (" + set.Clips[clip].Name + ")");
                                byte value = (byte)clip;
                                apply.Add(() => set.InstanceToClip[i] = value);
                            }
                        }
                        if (edit["speed"] != null && edit["speed"].Type != JTokenType.Null)
                        {
                            float speed = NumberOf(edit["speed"], "speed");
                            if (Math.Abs(set.PlaySpeeds[i] - speed) > 1e-6f)
                            {
                                changes.Add("point " + i + " speed: " + Round(set.PlaySpeeds[i], 4) + " -> " + speed);
                                apply.Add(() => set.PlaySpeeds[i] = speed);
                            }
                        }
                    }

                    HashSet<object> adding = new HashSet<object>();
                    foreach (JToken token in adds)
                    {
                        JObject user = token as JObject ?? throw new McpError("Each add_users entry is an object: {character, context?}.");
                        CheckKeys(user, "An add_users entry", "character", "context");
                        AnimClipDB database = FindCharacter(animations, ItemStr(user, "character"));
                        string contextName = ItemStr(user, "context");
                        AnimClipDB.Context context = contextName == null ? null : FindCharacterContext(database, contextName);
                        List<AnimClipDB.BlendSet> into = context == null ? database.BlendSets : context.BlendSets;
                        string who = database.Character + (context == null ? "" : " (" + (context.Name.Length == 0 ? "(default)" : context.Name) + ")");
                        if (into.Any(o => Same(o.Filename, key)) || !adding.Add(into))
                            throw new McpError(who + " already has " + key + ".");
                        changes.Add("give to " + who);
                        apply.Add(() =>
                        {
                            //How a tree node asks for it, and what that resolves to; both live only in the debug string table
                            animations.AddName(set.Name, true);
                            animations.AddName(key, true);
                            into.Add(new AnimClipDB.BlendSet() { Name = set.Name, Filename = key });
                        });
                    }

                    foreach (JToken token in removes)
                    {
                        JObject user = token as JObject ?? throw new McpError("Each remove_users entry is an object: {character, context?}.");
                        CheckKeys(user, "A remove_users entry", "character", "context");
                        AnimClipDB database = FindCharacter(animations, ItemStr(user, "character"));
                        string contextName = ItemStr(user, "context");
                        AnimClipDB.Context context = contextName == null ? null : FindCharacterContext(database, contextName);
                        BlendUser found = users.FirstOrDefault(o => o.Database == database && o.Context == context);
                        string who = database.Character + (context == null ? "" : " (" + (context.Name.Length == 0 ? "(default)" : context.Name) + ")");
                        if (found == null)
                            throw new McpError(who + " doesn't have " + key + " (list_blend_sets with name lists who does).");
                        users.Remove(found);
                        changes.Add("take from " + who);
                        List<AnimClipDB.BlendSet> from = context == null ? database.BlendSets : context.BlendSets;
                        apply.Add(() => from.Remove(found.Entry));
                    }

                    if (changes.Count == 0)
                        throw new McpError("Those values are what " + key + " already holds; nothing to change.");
                    JObject result = new JObject() { ["blend_set"] = key, ["changes"] = new JArray(changes) };
                    if (dryRun)
                    {
                        result["dry_run"] = true;
                        return result;
                    }
                    foreach (Action step in apply) step();
                    WriteAnimationPak(call, animations, "The blend set change is");
                    result["written"] = true;
                    if (Application.OpenForms.OfType<EditBlendSets>().Any())
                        call.Note("The Blend Set Editor is open: it shows the old values until it is reopened, and saving from it also writes any changes it holds.");
                    return result;
                });
        }

        private static int IndexOf(JObject item, int count, string what)
        {
            JToken token = item["index"];
            if (token == null || token.Type != JTokenType.Integer)
                throw new McpError("Each entry needs a whole-number 'index'.");
            int index = (int)token;
            if (index < 0 || index >= count)
                throw new McpError("There is no " + what + " " + index + ": there are " + count + ".");
            return index;
        }

        private static float NumberOf(JToken token, string what)
        {
            if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
                throw new McpError("'" + what + "' must be a number.");
            double value = (double)token;
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new McpError("'" + what + "' must be a finite number.");
            return (float)value;
        }
        #endregion

        #region Animation trees
        private sealed class ByReference : IEqualityComparer<AnimationNode>
        {
            public static readonly ByReference Instance = new ByReference();
            public bool Equals(AnimationNode x, AnimationNode y) => ReferenceEquals(x, y);
            public int GetHashCode(AnimationNode node) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(node);
        }

        private static string TreeSetName(AnimTreeDB database)
        {
            string set = database.Entries.FirstOrDefault()?.Set;
            return string.IsNullOrEmpty(set) ? database.Set ?? "" : set;
        }

        private static AnimTreeDB FindTreeSet(Anim animations, string name)
        {
            name = (name ?? "").Trim();
            AnimTreeDB database = animations.Trees.FirstOrDefault(o => Same(TreeSetName(o), name));
            if (database != null) return database;
            throw new McpError("There is no animation tree set '" + name + "'" + McpGlobalAssetChecks.DidYouMean(animations.Trees.Select(TreeSetName), name, " (list_anim_trees lists them)."));
        }

        private static AnimationTree FindTree(AnimTreeDB database, string name)
        {
            name = (name ?? "").Trim();
            AnimationTree tree = database.Entries.FirstOrDefault(o => o.Name == name) ?? database.Entries.FirstOrDefault(o => Same(o.Name, name));
            if (tree != null) return tree;
            throw new McpError(TreeSetName(database) + " has no tree '" + name + "'" + McpGlobalAssetChecks.DidYouMean(database.Entries.Select(o => o.Name), name, " (list_anim_trees with set lists them)."));
        }

        private static object ListAnimTrees(McpCall call)
        {
            string setName = call.Str("set");
            string[] words = Words(call.Str("filter"));
            using (McpEditorTools.Heartbeat(call, "Reading ANIMATION.PAK"))
                return McpEditor.UI(() =>
                {
                    Anim animations = RequireAnimations();
                    if (setName != null)
                    {
                        AnimTreeDB database = FindTreeSet(animations, setName);
                        List<AnimationTree> trees = database.Entries.Where(o => AllWords(words, o.Name)).OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        JObject inSet = new JObject() { ["set"] = TreeSetName(database) };
                        McpPaging.Page(call, trees, inSet, "trees", o => new JObject() { ["name"] = o.Name, ["nodes"] = o.Nodes.Count }, 200);
                        return inSet;
                    }
                    if (words.Length != 0)
                    {
                        List<JObject> found = new List<JObject>();
                        foreach (AnimTreeDB database in animations.Trees.OrderBy(TreeSetName, StringComparer.OrdinalIgnoreCase))
                            foreach (AnimationTree tree in database.Entries.OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase))
                                if (AllWords(words, tree.Name)) found.Add(new JObject() { ["set"] = TreeSetName(database), ["tree"] = tree.Name, ["nodes"] = tree.Nodes.Count });
                        if (found.Count == 0)
                            call.Note("No tree's name holds '" + string.Join(" ", words) + "'" + McpGlobalAssetChecks.DidYouMean(animations.Trees.SelectMany(o => o.Entries).Select(o => o.Name), string.Join(" ", words), "."));
                        JObject matching = new JObject();
                        McpPaging.Page(call, found, matching, "trees", o => o, 200);
                        return matching;
                    }
                    List<AnimTreeDB> sets = animations.Trees.OrderBy(TreeSetName, StringComparer.OrdinalIgnoreCase).ToList();
                    JObject result = new JObject();
                    McpPaging.Page(call, sets, result, "sets", o => new JObject() { ["set"] = TreeSetName(o), ["trees"] = o.Entries.Count }, 200);
                    return result;
                });
        }

        private static bool IsNodeCollection(Type type)
        {
            if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type)) return false;
            if (type.IsArray) return typeof(AnimationNode).IsAssignableFrom(type.GetElementType());
            foreach (Type contract in type.GetInterfaces())
                if (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return typeof(AnimationNode).IsAssignableFrom(contract.GetGenericArguments()[0]);
            return false;
        }

        private static bool IsObjectList(Type type) => type != typeof(string) && typeof(IList).IsAssignableFrom(type) && !IsNodeCollection(type);

        private static Type ElementType(IList list)
        {
            Type type = list.GetType();
            if (type.IsArray) return type.GetElementType();
            foreach (Type contract in type.GetInterfaces())
                if (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IList<>))
                    return contract.GetGenericArguments()[0];
            return null;
        }

        /// <summary>An enumerated selector matches its states by hashed string rather than by number.</summary>
        private static bool IsHashedStateValue(AnimationNode node, object owner, MemberInfo member)
        {
            Type type = AnimationNodeProxy.TypeOf(member);
            return member.Name == "Value" && !(owner is AnimationNode) && (type == typeof(uint) || type == typeof(int))
                && node != null && node.Type == NodeType.ANIM_Enumerated_Selector;
        }

        private static JToken FieldJson(object value, Type type)
        {
            if (value is AnimationMetadataValue meta)
            {
                PropertyInfo payload = meta.GetType().GetProperty("Value");
                return new JObject()
                {
                    ["ValueType"] = meta.ValueType.ToString(),
                    ["Value"] = payload == null ? JValue.CreateNull() : ValueJson(payload.GetValue(meta, null)),
                    ["RequiresConvert"] = meta.RequiresConvert,
                    ["CanMirror"] = meta.CanMirror,
                    ["CanModulateByPlayspeed"] = meta.CanModulateByPlayspeed,
                };
            }
            if (value == null && type == typeof(string)) return "";
            return ValueJson(value);
        }

        /// <summary>A node's (or a state's) values, the way the node editor's grid reflects them - wiring aside, which is listed as links.</summary>
        private static void WriteFields(JObject into, object owner, object prototype, AnimationNode node)
        {
            foreach (MemberInfo member in AnimationNodeProxy.MembersOf(owner.GetType()))
            {
                Type type = AnimationNodeProxy.TypeOf(member);
                if (owner is AnimationNode && (member.Name == "Type" || member.Name == "Name")) continue;
                if (typeof(AnimationNode).IsAssignableFrom(type) || IsNodeCollection(type)) continue;
                object value = AnimationNodeProxy.Read(owner, member);
                if (IsObjectList(type))
                {
                    IList list = value as IList;
                    if (list == null) { into[member.Name] = JValue.CreateNull(); continue; }
                    IList standard = prototype == null ? null : AnimationNodeProxy.Read(prototype, member) as IList;
                    JArray entries = new JArray();
                    for (int i = 0; i < list.Count; i++)
                    {
                        object item = list[i];
                        if (item == null) continue;
                        object standardItem = standard != null && i < standard.Count && standard[i] != null && standard[i].GetType() == item.GetType()
                            ? standard[i] : AnimationNodeProxy.Prototype(item.GetType());
                        JObject fields = new JObject();
                        WriteFields(fields, item, standardItem, node);
                        //Unused slots stay out: only one that links somewhere or has been changed from its class's start
                        JObject untouched = new JObject();
                        if (standardItem != null) WriteFields(untouched, standardItem, standardItem, node);
                        if (!HasNodeLink(item) && JToken.DeepEquals(fields, untouched)) continue;
                        fields.AddFirst(new JProperty("index", i));
                        entries.Add(fields);
                    }
                    into[member.Name] = entries;
                    continue;
                }
                if (IsHashedStateValue(node, owner, member))
                {
                    uint id = value is int signed ? unchecked((uint)signed) : Convert.ToUInt32(value ?? 0u);
                    into[member.Name] = AnimationHashedString.Format(id);
                    continue;
                }
                into[member.Name] = FieldJson(value, type);
            }
        }

        private static bool HasNodeLink(object item)
        {
            foreach (MemberInfo member in AnimationNodeProxy.MembersOf(item.GetType()))
                if (typeof(AnimationNode).IsAssignableFrom(AnimationNodeProxy.TypeOf(member)) && AnimationNodeProxy.Read(item, member) != null)
                    return true;
            return false;
        }

        private static JObject LinkJson(string field, AnimationNode target, HashSet<string> sharedNames)
        {
            JObject link = new JObject() { ["field"] = field, ["to"] = target.Name };
            if (sharedNames.Contains(target.Name)) link["to_type"] = target.Type.ToString();
            return link;
        }

        private static void CollectLinks(JArray into, object owner, string prefix, HashSet<string> sharedNames)
        {
            foreach (MemberInfo member in AnimationNodeProxy.MembersOf(owner.GetType()))
            {
                Type type = AnimationNodeProxy.TypeOf(member);
                object value = AnimationNodeProxy.Read(owner, member);
                if (typeof(AnimationNode).IsAssignableFrom(type))
                {
                    if (value is AnimationNode target) into.Add(LinkJson(prefix + member.Name, target, sharedNames));
                    continue;
                }
                if (IsNodeCollection(type))
                {
                    List<AnimationNode> nodes = (value as IEnumerable)?.OfType<AnimationNode>().ToList() ?? new List<AnimationNode>();
                    foreach (AnimationNode target in nodes)
                        into.Add(LinkJson(prefix + member.Name, target, sharedNames));
                    continue;
                }
                if (IsObjectList(type) && value is IList list)
                    for (int i = 0; i < list.Count; i++)
                        if (list[i] != null && !list[i].GetType().IsPrimitive && !(list[i] is string))
                            CollectLinks(into, list[i], prefix + member.Name + "[" + i + "].", sharedNames);
            }
        }

        private static HashSet<string> SharedNames(AnimationTree tree)
        {
            return new HashSet<string>(tree.Nodes.GroupBy(o => o.Name).Where(o => o.Select(n => n.Type).Distinct().Count() > 1).Select(o => o.Key));
        }

        /// <summary>
        /// Nodes written by kind (parameters, callbacks, listeners, properties) are always saved; every other node is
        /// saved only as a child of something the tree reaches, so one nothing parents is dropped when the tree is written.
        /// </summary>
        private static List<AnimationNode> UnsavedNodes(AnimationTree tree)
        {
            HashSet<AnimationNode> reached = new HashSet<AnimationNode>(ByReference.Instance);
            Stack<AnimationNode> todo = new Stack<AnimationNode>(tree.Children);
            while (todo.Count != 0)
            {
                AnimationNode node = todo.Pop();
                if (node == null || !reached.Add(node)) continue;
                foreach (AnimationNode child in node.Children) todo.Push(child);
            }
            return tree.Nodes.Where(o => !SavedByKind(o) && !reached.Contains(o)).ToList();
        }

        private static bool SavedByKind(AnimationNode node)
        {
            return node is ParameterNode || node is MetadataListenerNode || node is PropertyListenerNode || node is PropertyNode
                || node.Type == NodeType.ANIM_Callback || node.Type == NodeType.ANIM_AutoFloatParameter;
        }

        private static JObject NodeJson(AnimationNode node, HashSet<string> sharedNames, bool fields)
        {
            JObject row = new JObject() { ["name"] = node.Name, ["type"] = node.Type.ToString() };
            if (fields)
            {
                JObject values = new JObject();
                WriteFields(values, node, AnimationNodeProxy.Prototype(node.GetType()), node);
                row["fields"] = values;
            }
            JArray links = new JArray();
            CollectLinks(links, node, "", sharedNames);
            if (links.Count != 0) row["links"] = links;
            return row;
        }

        private static object GetAnimTree(McpCall call)
        {
            string setName = call.Str("set", required: true);
            string treeName = call.Str("tree", required: true);
            string[] words = Words(call.Str("filter"));
            bool fields = call.Bool("fields", true);
            bool layout = call.Bool("layout");
            using (McpEditorTools.Heartbeat(call, "Reading ANIMATION.PAK"))
                return McpEditor.UI(() =>
                {
                    Anim animations = RequireAnimations();
                    AnimTreeDB database = FindTreeSet(animations, setName);
                    AnimationTree tree = FindTree(database, treeName);
                    HashSet<string> shared = SharedNames(tree);
                    JObject settings = new JObject();
                    WriteFields(settings, tree, AnimationNodeProxy.Prototype(typeof(AnimationTree)), tree);
                    JArray treeLinks = new JArray();
                    CollectLinks(treeLinks, tree, "", shared);
                    List<AnimationNode> nodes = tree.Nodes.Where(o => AllWords(words, o.Name)).ToList();
                    JObject result = new JObject()
                    {
                        ["set"] = TreeSetName(database),
                        ["tree"] = tree.Name,
                        ["settings"] = settings,
                        ["links"] = treeLinks,
                        ["node_count"] = tree.Nodes.Count,
                    };
                    Dictionary<AnimationNode, JObject> drawn = null;
                    if (layout)
                    {
                        drawn = OnTreeCanvas(database, tree, false, (canvas, live) => DrawnJson(canvas));
                        result["layout"] = AnimTreeLayoutManager.Has(database, tree) ? "saved" : "auto";
                        if (drawn.TryGetValue(tree, out JObject top))
                            result["tree_node"] = top;
                    }
                    McpPaging.Page(call, nodes, result, "nodes", o =>
                    {
                        JObject row = NodeJson(o, shared, fields);
                        if (drawn != null && drawn.TryGetValue(o, out JObject where))
                            foreach (JProperty property in where.Properties())
                                row[property.Name] = property.Value.DeepClone();
                        return row;
                    }, 150, 2000);
                    List<AnimationNode> unsaved = UnsavedNodes(tree);
                    if (unsaved.Count != 0)
                    {
                        result["unsaved_nodes"] = new JArray(unsaved.Select(o => o.Name + " (" + o.Type + ")"));
                        call.Note("unsaved_nodes are flow nodes nothing parents, so they are dropped when the tree is written: link them from a flow field (States[i].Node, Child, ...) or @tree's Children.");
                    }
                    if (shared.Count != 0)
                        call.Note("Some names are shared by nodes of different types (" + string.Join(", ", shared.Take(5)) + "): give node_type / to_type for those in edit_anim_tree.");
                    return result;
                });
        }

        private static NodeType ParseNodeType(string text)
        {
            string wanted = (text ?? "").Trim();
            foreach (string name in Enum.GetNames(typeof(NodeType)))
                if (Same(name, wanted) || Same(name, "ANIM_" + wanted))
                    return (NodeType)Enum.Parse(typeof(NodeType), name);
            throw new McpError("'" + text + "' is not a node type. They are: " + string.Join(", ", Enum.GetNames(typeof(NodeType))) + ".");
        }

        private static AnimationNode ResolveNode(AnimationTree tree, string name, string typeText, string what)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new McpError("'" + what + "' is required.");
            name = name.Trim();
            if (name == "@tree") return tree;
            NodeType? type = typeText == null ? (NodeType?)null : ParseNodeType(typeText);
            List<AnimationNode> matches = tree.Nodes.Where(o => o.Name == name && (type == null || o.Type == type)).Distinct(ByReference.Instance).ToList();
            if (matches.Count == 0)
                matches = tree.Nodes.Where(o => Same(o.Name, name) && (type == null || o.Type == type)).Distinct(ByReference.Instance).ToList();
            if (matches.Count == 0)
            {
                if (type == null && Same(name, tree.Name)) return tree;
                throw new McpError("The tree has no node '" + name + "'" + (type == null ? "" : " of type " + type) + McpGlobalAssetChecks.DidYouMean(tree.Nodes.Select(o => o.Name), name, " (get_anim_tree lists them)."));
            }
            if (matches.Select(o => o.Type).Distinct().Count() > 1)
                throw new McpError("Several nodes are called '" + name + "' (" + string.Join(", ", matches.Select(o => o.Type).Distinct()) + "): give " + (what == "node" ? "node_type" : "to_type") + ".");
            return matches[0];
        }

        private sealed class FieldRef
        {
            public object Owner;
            public MemberInfo Member;
            public int? Index;
        }

        /// <summary>Walk a field path such as "States[3].Value" or "Value.CanMirror" down from a node to the member it ends on.</summary>
        private static FieldRef ResolveField(AnimationNode node, string path, bool fillSlots)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new McpError("'field' is required.");
            string[] parts = path.Trim().Split('.');
            object owner = node;
            for (int p = 0; p < parts.Length; p++)
            {
                string part = parts[p].Trim();
                int? index = null;
                int open = part.IndexOf('[');
                if (open >= 0)
                {
                    int close = part.IndexOf(']', open);
                    if (close < 0 || !int.TryParse(part.Substring(open + 1, close - open - 1), out int parsed))
                        throw new McpError("'" + path + "' has a bad index; write it as Name[2].");
                    index = parsed;
                    part = part.Substring(0, open).Trim();
                }
                List<MemberInfo> members = AnimationNodeProxy.MembersOf(owner.GetType()).ToList();
                MemberInfo member = members.FirstOrDefault(o => o.Name == part) ?? members.FirstOrDefault(o => Same(o.Name, part));
                if (member == null)
                    throw new McpError((owner is AnimationNode ? "'" + node.Name + "' (" + node.Type + ")" : "'" + string.Join(".", parts.Take(p)) + "'") + " has no field '" + part + "'. It has: "
                        + string.Join(", ", members.Where(o => o.Name != "Type").Select(o => o.Name)) + ".");
                bool last = p == parts.Length - 1;
                if (index != null)
                {
                    IList list = AnimationNodeProxy.Read(owner, member) as IList;
                    if (list == null) throw new McpError("'" + part + "' is not a list.");
                    if (index < 0 || index >= list.Count)
                        throw new McpError("'" + part + "' has " + list.Count + " slots (0 to " + (list.Count - 1) + "), so there is no [" + index + "].");
                    if (last) return new FieldRef() { Owner = owner, Member = member, Index = index };
                    object item = list[index.Value];
                    if (item == null)
                    {
                        Type element = ElementType(list);
                        if (!fillSlots || element == null || element.IsValueType || typeof(AnimationNode).IsAssignableFrom(element))
                            throw new McpError("'" + part + "[" + index + "]' is empty.");
                        //The state arrays ship with unused slots left null; the node editor fills them the same way
                        item = Activator.CreateInstance(element);
                        list[index.Value] = item;
                    }
                    //A struct comes out as a copy, so a change to one of its fields would never reach the list
                    if (item.GetType().IsValueType)
                        throw new McpError("'" + part + "[" + index + "]' is a single value, so its fields can't be set one at a time.");
                    owner = item;
                    continue;
                }
                if (last) return new FieldRef() { Owner = owner, Member = member };
                object next = AnimationNodeProxy.Read(owner, member);
                if (next == null)
                {
                    if (fillSlots && typeof(AnimationMetadataValue).IsAssignableFrom(AnimationNodeProxy.TypeOf(member)))
                    {
                        next = new FloatMetadataValue();
                        AnimationNodeProxy.Write(owner, member, next);
                    }
                    else
                        throw new McpError("'" + part + "' is empty, so it has no '" + parts[p + 1] + "'.");
                }
                if (next is AnimationNode)
                    throw new McpError("'" + part + "' is a link to another node: edit that node instead.");
                //A struct (a vector, say) comes out as a copy, so a change to one of its fields would never reach the node
                if (next.GetType().IsValueType)
                    throw new McpError("'" + string.Join(".", parts.Take(p + 1).Select(o => o.Trim())) + "' is a single value: set it whole (a vector as [x, y, z]).");
                owner = next;
            }
            throw new McpError("'" + path + "' is not a field path.");
        }

        private static object ParseEnum(JToken token, Type type, string what)
        {
            bool flags = type.IsDefined(typeof(FlagsAttribute), false);
            if (token.Type == JTokenType.Integer) return Enum.ToObject(type, (long)token);
            List<string> names = new List<string>();
            if (token is JArray array)
            {
                if (!flags) throw new McpError("'" + what + "' takes one of: " + string.Join(", ", Enum.GetNames(type)) + ".");
                names.AddRange(array.Select(o => (string)o));
            }
            else
            {
                string text = token.Type == JTokenType.String ? (string)token : token.ToString(Newtonsoft.Json.Formatting.None);
                if (flags) names.AddRange(text.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries));
                else names.Add(text);
            }
            long bits = 0;
            foreach (string raw in names)
            {
                string name = (raw ?? "").Trim();
                string match = Enum.GetNames(type).FirstOrDefault(o => Same(o, name));
                if (match != null) bits |= Convert.ToInt64(Enum.Parse(type, match));
                else if (long.TryParse(name, out long number)) bits |= number;
                else throw new McpError("'" + name + "' is not one of " + what + "'s values: " + string.Join(", ", Enum.GetNames(type)) + ".");
            }
            return Enum.ToObject(type, bits);
        }

        private static object Coerce(JToken token, Type type, string what)
        {
            try
            {
                if (type == typeof(string))
                    return token.Type == JTokenType.Null ? "" : token.Type == JTokenType.String ? (string)token : token.ToString(Newtonsoft.Json.Formatting.None);
                if (token.Type == JTokenType.Null)
                    throw new McpError("'" + what + "' needs a value.");
                if (type == typeof(bool))
                {
                    if (token.Type == JTokenType.Boolean) return (bool)token;
                    if (token.Type == JTokenType.String && bool.TryParse((string)token, out bool parsed)) return parsed;
                    throw new McpError("'" + what + "' is true or false.");
                }
                if (type.IsEnum) return ParseEnum(token, type, what);
                if (type == typeof(System.Numerics.Vector3))
                {
                    if (token is JArray xyz && xyz.Count == 3)
                        return new System.Numerics.Vector3((float)xyz[0], (float)xyz[1], (float)xyz[2]);
                    if (token is JObject named && named["x"] != null && named["y"] != null && named["z"] != null)
                        return new System.Numerics.Vector3((float)named["x"], (float)named["y"], (float)named["z"]);
                    throw new McpError("'" + what + "' is a vector: [x, y, z].");
                }
                if (type == typeof(float) || type == typeof(double))
                {
                    double value = token.Type == JTokenType.String ? double.Parse((string)token, CultureInfo.InvariantCulture) : (double)token;
                    if (double.IsNaN(value) || double.IsInfinity(value)) throw new McpError("'" + what + "' must be a finite number.");
                    return type == typeof(float) ? (object)(float)value : value;
                }
                if (type.IsPrimitive && type != typeof(char))
                {
                    decimal value = token.Type == JTokenType.String ? decimal.Parse((string)token, CultureInfo.InvariantCulture) : (decimal)token;
                    if (value != decimal.Truncate(value)) throw new McpError("'" + what + "' must be a whole number.");
                    return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
                }
            }
            catch (McpError) { throw; }
            catch (OverflowException) { throw new McpError("'" + what + "' is out of range for a " + type.Name + "."); }
            catch (Exception e) { throw new McpError("'" + what + "' can't be " + token.ToString(Newtonsoft.Json.Formatting.None) + " (it is a " + type.Name + "): " + e.Message); }
            throw new McpError("'" + what + "' is a " + type.Name + ", which can't be set here.");
        }

        private static AnimationMetadataValue MetadataFrom(JToken token, AnimationMetadataValue current, string what)
        {
            JObject spec = token as JObject ?? throw new McpError("'" + what + "' is a property value: {ValueType?, Value?, RequiresConvert?, CanMirror?, CanModulateByPlayspeed?}.");
            CheckKeys(spec, "'" + what + "'", "ValueType", "Value", "RequiresConvert", "CanMirror", "CanModulateByPlayspeed");
            MetadataValueType type = spec["ValueType"] != null ? (MetadataValueType)ParseEnum(spec["ValueType"], typeof(MetadataValueType), "ValueType")
                : current?.ValueType ?? MetadataValueType.FLOAT32;
            AnimationMetadataValue fresh = MetadataTypeDescriptor.Create(type);
            fresh.RequiresConvert = spec["RequiresConvert"] != null ? (bool)Coerce(spec["RequiresConvert"], typeof(bool), "RequiresConvert") : current?.RequiresConvert ?? false;
            fresh.CanMirror = spec["CanMirror"] != null ? (bool)Coerce(spec["CanMirror"], typeof(bool), "CanMirror") : current?.CanMirror ?? false;
            fresh.CanModulateByPlayspeed = spec["CanModulateByPlayspeed"] != null ? (bool)Coerce(spec["CanModulateByPlayspeed"], typeof(bool), "CanModulateByPlayspeed") : current?.CanModulateByPlayspeed ?? false;
            PropertyInfo payload = fresh.GetType().GetProperty("Value");
            if (payload != null)
            {
                if (spec["Value"] != null)
                    payload.SetValue(fresh, Coerce(spec["Value"], payload.PropertyType, "Value"), null);
                else if (current != null && current.GetType() == fresh.GetType())
                    payload.SetValue(fresh, payload.GetValue(current, null), null);
            }
            return fresh;
        }

        private static string SetTreeField(Action<string> addName, AnimationNode node, string path, JToken value)
        {
            string trimmed = (path ?? "").Trim();
            if (Same(trimmed, "Name")) throw new McpError("Rename a node with {op:'rename', node, name}.");
            if (Same(trimmed, "Type")) throw new McpError("A node's type can't change: add a node of the new type, relink, and remove this one.");
            if (value == null) throw new McpError("'value' is required.");

            //A property's payload class follows its value type, so changing the type swaps the whole object, keeping its flags
            if (trimmed.EndsWith(".ValueType", StringComparison.OrdinalIgnoreCase))
            {
                FieldRef holder = ResolveField(node, trimmed.Substring(0, trimmed.Length - ".ValueType".Length), true);
                if (holder.Index != null || !typeof(AnimationMetadataValue).IsAssignableFrom(AnimationNodeProxy.TypeOf(holder.Member)))
                    throw new McpError("Only a property value (e.g. a property node's Value) has a ValueType.");
                AnimationMetadataValue current = AnimationNodeProxy.Read(holder.Owner, holder.Member) as AnimationMetadataValue;
                MetadataValueType type = (MetadataValueType)ParseEnum(value, typeof(MetadataValueType), "ValueType");
                if (current != null && current.ValueType == type) return "unchanged";
                AnimationMetadataValue replacement = MetadataTypeDescriptor.Create(type);
                if (current != null)
                {
                    replacement.RequiresConvert = current.RequiresConvert;
                    replacement.CanMirror = current.CanMirror;
                    replacement.CanModulateByPlayspeed = current.CanModulateByPlayspeed;
                }
                AnimationNodeProxy.Write(holder.Owner, holder.Member, replacement);
                return type.ToString();
            }

            FieldRef field = ResolveField(node, trimmed, true);
            if (field.Index != null) throw new McpError("'" + trimmed + "' is a list entry: set one of its fields, e.g. " + trimmed + ".Value.");
            Type fieldType = AnimationNodeProxy.TypeOf(field.Member);
            if (typeof(AnimationNode).IsAssignableFrom(fieldType) || IsNodeCollection(fieldType))
                throw new McpError("'" + trimmed + "' is a link to a node: use {op:'link'} or {op:'unlink'}.");
            if (IsObjectList(fieldType))
                throw new McpError("'" + trimmed + "' is a list: set its entries' fields, e.g. " + trimmed + "[0]." + "<field>.");

            object converted;
            if (typeof(AnimationMetadataValue).IsAssignableFrom(fieldType))
                converted = MetadataFrom(value, AnimationNodeProxy.Read(field.Owner, field.Member) as AnimationMetadataValue, trimmed);
            else if (IsHashedStateValue(node, field.Owner, field.Member))
            {
                uint id;
                if (value.Type == JTokenType.Integer) id = (uint)(long)value;
                else
                {
                    string text = ((string)value ?? "").Trim();
                    if (text.Length != 0 && !uint.TryParse(text, out _)) addName(text);
                    id = AnimationHashedString.Parse(text);
                }
                converted = fieldType == typeof(int) ? (object)unchecked((int)id) : id;
            }
            else
                converted = Coerce(value, fieldType, trimmed);

            //Every name in the PAK is stored as a hash: register new ones so they read back as names
            if (converted is string text2 && text2.Length != 0) addName(text2);
            AnimationNodeProxy.Write(field.Owner, field.Member, converted);
            return converted is AnimationMetadataValue ? "set" : ValueJson(converted).ToString(Newtonsoft.Json.Formatting.None);
        }

        private static bool IsFlowField(FieldRef field)
        {
            string name = field.Member.Name;
            if (field.Owner is AnimationNode)
                return name == "BaseNode" || name == "AdditiveNode" || name == "Child" || name == "LeftStrikeChild" || name == "RightStrikeChild";
            return name == "Node";
        }

        /// <summary>Parenting would loop if the parent is already below the child: the writer recurses through children and would never finish.</summary>
        private static void RefuseLoop(AnimationNode parent, AnimationNode child)
        {
            HashSet<AnimationNode> seen = new HashSet<AnimationNode>(ByReference.Instance);
            Stack<AnimationNode> todo = new Stack<AnimationNode>();
            todo.Push(child);
            while (todo.Count != 0)
            {
                AnimationNode node = todo.Pop();
                if (node == null || !seen.Add(node)) continue;
                if (ReferenceEquals(node, parent))
                    throw new McpError("'" + child.Name + "' is above '" + parent.Name + "' already, so parenting it there would make a loop.");
                foreach (AnimationNode next in node.Children) todo.Push(next);
            }
        }

        private static int IndexByReference(IList list, AnimationNode node)
        {
            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], node)) return i;
            return -1;
        }

        /// <summary>Only some node types can sit in a tree's flow: written there, any other makes the whole tree set unreadable.</summary>
        private static void RefuseNonFlow(AnimationNode target)
        {
            if (!AnimTreeCanvas.IsFlowType(target.Type))
                throw new McpError("'" + target.Name + "' is a " + target.Type + ", which a tree can't hold in its flow: only animations, selectors, parametric, blend, bone mask, IK and weighted nodes can be children.");
        }

        private static void LinkField(AnimationNode node, string path, AnimationNode target)
        {
            if (ReferenceEquals(node, target)) throw new McpError("A node can't link to itself.");
            if (target is AnimationTree) throw new McpError("Nothing links to the tree itself.");
            FieldRef field = ResolveField(node, path, true);
            Type type = AnimationNodeProxy.TypeOf(field.Member);
            if (field.Index != null && IsNodeCollection(type))
                throw new McpError("Link to '" + field.Member.Name + "' without an index; it is a list the node is added to.");
            if (field.Index == null && IsNodeCollection(type))
            {
                IList list = AnimationNodeProxy.Read(field.Owner, field.Member) as IList;
                if (list == null) throw new McpError("'" + path + "' can't be linked.");
                if (IndexByReference(list, target) >= 0) throw new McpError("'" + target.Name + "' is already in " + node.Name + "'s " + field.Member.Name + ".");
                RefuseNonFlow(target);
                RefuseLoop(node, target);
                //A weighted node holds exactly one child; the reader refuses more
                if (node is WeightedNode weighted) { list.Clear(); weighted.Child = target; }
                list.Add(target);
                return;
            }
            if (!typeof(AnimationNode).IsAssignableFrom(type))
                throw new McpError("'" + path + "' is not a link: get_anim_tree lists a node's links, and {op:'set'} changes values.");
            if (!type.IsInstanceOfType(target))
                throw new McpError("'" + path + "' takes a " + type.Name + "; '" + target.Name + "' is a " + target.GetType().Name + " (" + target.Type + ").");
            bool flow = IsFlowField(field);
            if (flow)
            {
                RefuseNonFlow(target);
                RefuseLoop(node, target);
            }
            else if (field.Member.Name == "LeafNode")
                RefuseNonFlow(target);
            AnimationNodeProxy.Write(field.Owner, field.Member, target);
            if (!flow) return;
            //A flow child is written under its parent, which is how the tree keeps it at all
            if (node is WeightedNode) { node.Children.Clear(); node.Children.Add(target); }
            else if (IndexByReference(node.Children, target) < 0) node.Children.Add(target);
        }

        private static void UnlinkField(AnimationNode node, string path, AnimationNode target)
        {
            FieldRef field = ResolveField(node, path, false);
            Type type = AnimationNodeProxy.TypeOf(field.Member);
            if (IsNodeCollection(type))
            {
                IList list = AnimationNodeProxy.Read(field.Owner, field.Member) as IList;
                if (list == null || list.Count == 0) throw new McpError(node.Name + "'s " + field.Member.Name + " is empty.");
                if (field.Index != null) { target = list[field.Index.Value] as AnimationNode; }
                if (target == null) list.Clear();
                else
                {
                    int at = IndexByReference(list, target);
                    if (at < 0) throw new McpError("'" + target.Name + "' is not in " + node.Name + "'s " + field.Member.Name + ".");
                    list.RemoveAt(at);
                }
                if (node is WeightedNode weighted && (target == null || ReferenceEquals(weighted.Child, target))) weighted.Child = null;
                return;
            }
            if (!typeof(AnimationNode).IsAssignableFrom(type))
                throw new McpError("'" + path + "' is not a link: get_anim_tree lists a node's links.");
            AnimationNode current = AnimationNodeProxy.Read(field.Owner, field.Member) as AnimationNode;
            if (current == null) throw new McpError("'" + path + "' links to nothing.");
            if (target != null && !ReferenceEquals(current, target))
                throw new McpError("'" + path + "' links to '" + current.Name + "', not '" + target.Name + "'.");
            AnimationNodeProxy.Write(field.Owner, field.Member, null);
            //As the graph editor unlinks: the child stays under its parent, except a weighted node's, which the reader would relink
            if (node is WeightedNode && field.Member.Name == "Child")
            {
                int at = IndexByReference(node.Children, current);
                if (at >= 0) node.Children.RemoveAt(at);
            }
        }

        private static readonly string[] TreeOpKeys = { "op", "node", "node_type", "field", "value", "to", "to_type", "type", "name", "x", "y" };

        private static string ApplyTreeOp(Action<string> addName, AnimationTree tree, JObject op)
        {
            CheckKeys(op, "An op", TreeOpKeys);
            string kind = (ItemStr(op, "op") ?? "").Trim().ToLowerInvariant();
            switch (kind)
            {
                case "set":
                    {
                        AnimationNode node = ResolveNode(tree, ItemStr(op, "node"), ItemStr(op, "node_type"), "node");
                        string field = ItemStr(op, "field");
                        string now = SetTreeField(addName, node, field, op["value"]);
                        return "set " + Label(node) + "." + field.Trim() + " = " + now;
                    }
                case "rename":
                    {
                        AnimationNode node = ResolveNode(tree, ItemStr(op, "node"), ItemStr(op, "node_type"), "node");
                        if (node is AnimationTree) throw new McpError("The tree itself can't be renamed here.");
                        string name = (ItemStr(op, "name") ?? "").Trim();
                        if (name.Length == 0) throw new McpError("'name' (the new name) is required.");
                        string old = node.Name;
                        try { tree.RenameNode(node, name); }
                        catch (InvalidOperationException e) { throw new McpError(e.Message); }
                        addName(name);
                        return "renamed " + old + " to " + name;
                    }
                case "add_node":
                    {
                        NodeType type = ParseNodeType(ItemStr(op, "type") ?? throw new McpError("'type' is required."));
                        if (type == NodeType.ANIM_Tree_Top_Level) throw new McpError("A tree has only one top level: it is the tree itself.");
                        string name = (ItemStr(op, "name") ?? "").Trim();
                        if (name.Length == 0)
                        {
                            string stem = type.ToString().StartsWith("ANIM_") ? type.ToString().Substring(5) : type.ToString();
                            name = stem;
                            for (int i = 1; tree.TryGetNode(name, out _); i++) name = stem + "_" + i;
                        }
                        else if (tree.TryGetNode(name, out _))
                            throw new McpError("The tree already has a node called '" + name + "'.");
                        //The reader builds an auto float as a plain node; the graph editor's parameter-shaped one would be written twice
                        AnimationNode node = type == NodeType.ANIM_AutoFloatParameter ? new AnimationNode() { Type = type } : AnimationTreeGraph.CreateAnimationNodeInstance(type);
                        node.Name = name;
                        tree.AddNode(node);
                        addName(name);
                        return "added " + name + " (" + type + ")" + (SavedByKind(node) ? "" : " - link it from a flow field or @tree's Children, or it is not saved");
                    }
                case "remove_node":
                    {
                        AnimationNode node = ResolveNode(tree, ItemStr(op, "node"), ItemStr(op, "node_type"), "node");
                        if (node is AnimationTree) throw new McpError("The tree itself can't be removed.");
                        tree.RemoveNode(node);
                        return "removed " + Label(node);
                    }
                case "link":
                    {
                        AnimationNode node = ResolveNode(tree, ItemStr(op, "node"), ItemStr(op, "node_type"), "node");
                        AnimationNode target = ResolveNode(tree, ItemStr(op, "to"), ItemStr(op, "to_type"), "to");
                        string field = ItemStr(op, "field");
                        LinkField(node, field, target);
                        return "linked " + Label(node) + "." + (field ?? "").Trim() + " -> " + target.Name;
                    }
                case "unlink":
                    {
                        AnimationNode node = ResolveNode(tree, ItemStr(op, "node"), ItemStr(op, "node_type"), "node");
                        AnimationNode target = ItemStr(op, "to") == null ? null : ResolveNode(tree, ItemStr(op, "to"), ItemStr(op, "to_type"), "to");
                        string field = ItemStr(op, "field");
                        UnlinkField(node, field, target);
                        return "unlinked " + Label(node) + "." + (field ?? "").Trim() + (target == null ? "" : " from " + target.Name);
                    }
                default:
                    throw new McpError("'op' is one of set, rename, add_node, remove_node, link, unlink.");
            }
        }

        private static string Label(AnimationNode node) => node is AnimationTree ? "@tree" : node.Name;

        private static List<string> ApplyTreeOps(Action<string> addName, AnimationTree tree, JArray ops)
        {
            List<string> done = new List<string>();
            for (int i = 0; i < ops.Count; i++)
            {
                JObject op = ops[i] as JObject ?? throw new McpError("ops[" + i + "] must be an object such as {op:'set', node, field, value}.");
                try { done.Add(ApplyTreeOp(addName, tree, op)); }
                catch (McpError e) { throw new McpError("ops[" + i + "] (" + (ItemStr(op, "op") ?? "?") + "): " + e.Message + " Nothing was changed."); }
            }
            return done;
        }

        /// <summary>A field of a tree node holding a searched value.</summary>
        private sealed class TreeHit
        {
            public string TreeSet, Tree, Node, NodeType, Field, Value;
        }

        /// <summary>
        /// Every node field, across the tree sets, whose text equals <paramref name="value"/> (any case; words: contains them all),
        /// optionally only fields whose path holds <paramref name="field"/>. Walks the fields the node editor shows, list entries included.
        /// </summary>
        private static List<TreeHit> FindInTrees(Anim animations, string value, string field, string setName, int most, bool contains = false)
        {
            List<TreeHit> hits = new List<TreeHit>();
            string[] words = Words(value);
            foreach (AnimTreeDB database in animations.Trees.OrderBy(TreeSetName, StringComparer.OrdinalIgnoreCase))
            {
                if (setName != null && !Same(TreeSetName(database), setName)) continue;
                foreach (AnimationTree tree in database.Entries)
                    foreach (AnimationNode node in new AnimationNode[] { tree }.Concat(tree.Nodes))
                    {
                        CollectHits(node, "", node, (path, text) =>
                        {
                            if (field != null && path.IndexOf(field, StringComparison.OrdinalIgnoreCase) < 0) return;
                            bool match = contains ? AllWords(words, text) : Same(text, value);
                            if (!match || hits.Count >= most) return;
                            hits.Add(new TreeHit() { TreeSet = TreeSetName(database), Tree = tree.Name, Node = Label(node), NodeType = node.Type.ToString(), Field = path, Value = text });
                        }, 0);
                        if (hits.Count >= most) return hits;
                    }
            }
            return hits;
        }

        private static void CollectHits(object owner, string prefix, AnimationNode node, Action<string, string> visit, int depth)
        {
            if (owner == null || depth > 4) return;
            foreach (MemberInfo member in AnimationNodeProxy.MembersOf(owner.GetType()))
            {
                Type type = AnimationNodeProxy.TypeOf(member);
                if (owner is AnimationNode && (member.Name == "Type" || member.Name == "Name")) continue;
                if (typeof(AnimationNode).IsAssignableFrom(type) || IsNodeCollection(type)) continue;
                object value = AnimationNodeProxy.Read(owner, member);
                if (value is string text) { if (text.Length != 0) visit(prefix + member.Name, text); continue; }
                if (IsHashedStateValue(node, owner, member))
                {
                    uint id = value is int signed ? unchecked((uint)signed) : Convert.ToUInt32(value ?? 0u);
                    visit(prefix + member.Name, AnimationHashedString.Format(id));
                    continue;
                }
                if (IsObjectList(type) && value is IList list)
                    for (int i = 0; i < list.Count; i++)
                        if (list[i] != null && !list[i].GetType().IsPrimitive && !(list[i] is string))
                            CollectHits(list[i], prefix + member.Name + "[" + i + "].", node, visit, depth + 1);
            }
        }

        private static object FindInAnimTrees(McpCall call)
        {
            string value = call.Str("value", required: true).Trim();
            if (value.Length == 0) throw new McpError("'value' can't be empty.");
            string field = call.Str("field");
            if (field != null && (Same(field, "any") || field.Trim().Length == 0)) field = null;
            string setName = call.Str("set");
            bool contains = call.Bool("contains");
            int limit = Limit(call, 100, 2000);
            using (McpEditorTools.Heartbeat(call, "Searching the animation trees"))
                return McpEditor.UI(() =>
                {
                    Anim animations = RequireAnimations();
                    if (setName != null) setName = TreeSetName(FindTreeSet(animations, setName));
                    List<TreeHit> hits = FindInTrees(animations, value, field?.Trim(), setName, limit + 1, contains);
                    if (hits.Count > limit) call.Note("More than " + limit + " fields match; the first " + limit + " are listed (give set or field, or raise limit).");
                    if (hits.Count == 0)
                    {
                        List<string> clips = animations.Sets.SelectMany(o => o.Contexts).SelectMany(o => o.Clips).Select(o => o.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        if (clips.Contains(value, StringComparer.OrdinalIgnoreCase))
                            call.Note("'" + value + "' is a clip, but no animation tree plays it: it plays only when a script names it (CMD_PlayAnimation) or a tree leaf is pointed at it (edit_anim_tree).");
                        else
                            call.Note("No tree field " + (contains ? "contains" : "is") + " '" + value + "'" + (contains ? "" : " (contains:true matches part of a value)")
                                + McpGlobalAssetChecks.DidYouMean(clips, value, "."));
                    }
                    return new JObject()
                    {
                        ["value"] = value,
                        ["total"] = Math.Min(hits.Count, limit),
                        ["hits"] = new JArray(hits.Take(limit).Select(o => new JObject() { ["set"] = o.TreeSet, ["tree"] = o.Tree, ["node"] = o.Node, ["node_type"] = o.NodeType, ["field"] = o.Field, ["value"] = o.Value })),
                    };
                });
        }

        /// <summary>
        /// Notes for an AnimationName an edit writes: no set has that clip, or the sets of the open level's characters using this
        /// tree set lack it (a leaf naming a clip its character's set doesn't have plays nothing).
        /// </summary>
        private static void CheckTreeClips(McpCall call, Anim animations, string treeSet, JArray ops)
        {
            List<string> names = new List<string>();
            foreach (JToken token in ops)
            {
                if (!(token is JObject op) || !Same(ItemStr(op, "op"), "set")) continue;
                string field = ItemStr(op, "field") ?? "";
                if (!field.Trim().EndsWith("AnimationName", StringComparison.OrdinalIgnoreCase)) continue;
                string value = ItemStr(op, "value");
                if (!string.IsNullOrWhiteSpace(value)) names.Add(value.Trim());
            }
            if (names.Count == 0) return;

            //The sets the open level's characters on this tree set play from
            HashSet<string> sets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Commands commands = Singleton.Editor?.CompositeBrowser?.Content?.Level?.Commands;
            if (commands != null)
                foreach (Composite composite in commands.Entries)
                    foreach (FunctionEntity function in composite.functions)
                        if (function.function.IsFunctionType && function.function.AsFunctionType == FunctionType.Character && Same(McpGlobalAssetChecks.TextOf(function, "anim_tree_set"), treeSet))
                        {
                            string set = McpGlobalAssetChecks.TextOf(function, "anim_set");
                            if (!string.IsNullOrEmpty(set)) sets.Add(set);
                        }
            List<string> all = animations.Sets.SelectMany(o => o.Contexts).SelectMany(o => o.Clips).Select(o => o.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (string name in names)
            {
                if (!all.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    call.Note("No animation set has a clip called '" + name + "', so that leaf plays nothing" + McpGlobalAssetChecks.DidYouMean(all, name, " (list_animations filter searches them)."));
                    continue;
                }
                List<string> lacking = sets.Where(s => McpGlobalAssetChecks.FindClip(animations.GetSet(s), name) == null).ToList();
                if (lacking.Count != 0)
                    call.Note("'" + name + "' is not in " + string.Join(", ", lacking) + ", the set(s) the open level's characters on the " + treeSet + " tree set play from; for them that leaf plays nothing.");
            }
        }

        private static object EditAnimTree(McpCall call)
        {
            string setName = call.Str("set", required: true);
            string treeName = call.Str("tree", required: true);
            JArray ops = call.Array("ops", required: true);
            bool dryRun = call.Bool("dry_run");
            bool closeGame = call.Bool("close_game");
            if (ops.Count == 0) throw new McpError("'ops' is empty.");
            List<(string name, int x, int y)> placed = PlacedNodes(ops);

            //Every op is tried on a copy of the tree set first: all of them apply to the real one, or none
            List<string> changes = null;
            List<AnimationNode> unsavedAfter = null;
            string treeLabel = null;
            using (McpEditorTools.Heartbeat(call, "Checking the edit"))
                McpEditor.UI(() =>
                {
                    Anim animations = RequireAnimations();
                    AnimTreeDB database = FindTreeSet(animations, setName);
                    AnimationTree tree = FindTree(database, treeName);
                    treeLabel = TreeSetName(database) + "\\" + tree.Name;
                    AnimTreeDB copy;
                    try { copy = new AnimTreeDB(database.ToBytes(), animations.StringsDebug, database.Filepath); }
                    catch (Exception e) { throw new McpError("The tree set could not be copied to check the edit (" + e.Message + ")."); }
                    AnimationTree copyTree = copy.Loaded ? copy.Entries.FirstOrDefault(o => o.Name == tree.Name) : null;
                    if (copyTree == null) throw new McpError("The tree set could not be copied to check the edit.");
                    //Only a node nothing parents is lost in the copy - one added in the tree editor and never linked
                    HashSet<string> copied = new HashSet<string>(copyTree.Nodes.Select(o => o.Type + ":" + o.Name));
                    List<string> unparented = tree.Nodes.Where(o => !copied.Contains(o.Type + ":" + o.Name)).Select(o => o.Name + " (" + o.Type + ")").Distinct().ToList();
                    if (unparented.Count != 0)
                        throw new McpError("This tree holds nodes that nothing parents, so they would not be saved (" + string.Join(", ", unparented.Take(10))
                            + "), probably added in the Animation Tree Editor. Link or delete them there first.");
                    //The copy shares the live debug string table, so new names are registered only when the edit is made for real
                    changes = ApplyTreeOps(name => { }, copyTree, ops);
                    byte[] after;
                    try { after = copy.ToBytes(); }
                    catch (Exception e) { throw new McpError("After those ops the tree could not be written (" + e.Message + "); nothing was changed."); }
                    //Written is not enough: a set that doesn't read back is lost, every tree in it, the next time the animations load
                    AnimTreeDB reread = null;
                    try { reread = new AnimTreeDB(after, animations.StringsDebug, database.Filepath); }
                    catch { }
                    if (reread == null || !reread.Loaded || reread.Entries.Count != copy.Entries.Count)
                        throw new McpError("After those ops the tree set would not read back, so writing it would lose it; nothing was changed.");
                    unsavedAfter = UnsavedNodes(copyTree);
                    CheckTreeClips(call, animations, TreeSetName(database), ops);
                });

            JObject result = new JObject() { ["tree"] = treeLabel, ["changes"] = new JArray(changes) };
            if (unsavedAfter.Count != 0)
            {
                result["unsaved_nodes"] = new JArray(unsavedAfter.Select(o => o.Name + " (" + o.Type + ")"));
                call.Note("unsaved_nodes are flow nodes nothing parents: they are dropped when the tree is written. Link them from a flow field or @tree's Children.");
            }
            if (dryRun)
            {
                result["dry_run"] = true;
                return result;
            }

            if (GameRunning())
            {
                if (!closeGame)
                    throw new McpError(McpErrorCodes.Refused, "Alien: Isolation is running, and ANIMATION.PAK can't be written while it holds it open. Close it, or pass close_game:true to close it as the editor's Save does.");
                CloseGame(call);
            }

            using (McpEditorTools.Heartbeat(call, "Writing ANIMATION.PAK"))
                McpEditor.UI(() =>
                {
                    Anim animations = RequireAnimations();
                    AnimTreeDB database = FindTreeSet(animations, setName);
                    AnimationTree tree = FindTree(database, treeName);
                    //Nothing done to a PAK that can't be written back: one changed on disk since it was loaded would be put back as it was
                    string changedOnDisk = AnimationPakWrite.ChangedOnDisk(animations.PAK.Filepath);
                    if (changedOnDisk != null)
                        throw new McpError(McpErrorCodes.Refused, changedOnDisk + " Nothing was changed.");
                    //A value half-typed into the open editor's inspector goes in first; the layout is taken by node, to follow renames and removals
                    AnimationTreeGraph.FindOpen(tree)?.CommitPendingEdits();
                    Dictionary<AnimationNode, List<AnimTreeLayouts.NodeLayout>> heldNodes = AnimTreeLayoutManager.Hold(database, tree, out TreeLayout held);
                    try { ApplyTreeOps(name => animations.AddName(name, true), tree, ops); }
                    catch (McpError e)
                    {
                        //The copy took every op, so this is the live tree having moved on since: say what state it is in (the layout follows what did apply)
                        FollowTreeEdit(database, tree, held, heldNodes);
                        throw new McpError("The ops checked out on a copy but failed on the tree itself (" + e.Message + "). Earlier ops may be applied in memory; nothing was written. Reopen the tree with get_anim_tree before trying again.");
                    }
                    //The layout follows the tree in memory at once, written or not: the tree's own save writes it with the tree if this write fails
                    FollowTreeEdit(database, tree, held, heldNodes);

                    //As the tree editor saves: the set's database goes back into its PAK entry, then the PAK is written
                    PAK2.File entry = animations.PAK.Entries.FirstOrDefault(o => Same(o.Filename, database.Filepath));
                    if (entry == null)
                        throw new McpError("The tree set's file isn't in ANIMATION.PAK, so the edit is in memory only.");
                    byte[] content = database.ToBytes();
                    if (content == null) throw new McpError("The tree set could not be serialised; the edit is in memory only.");
                    entry.Content = content;
                    //New names only read back as names if the debug string table goes out with them
                    PAK2.File strings = animations.StringsDebug == null ? null : animations.PAK.Entries.FirstOrDefault(o => Same(o.Filename, animations.StringsDebug.Filepath));
                    if (strings != null) strings.Content = animations.StringsDebug.ToBytes();
                    //The set's node layouts go into the PAK with it
                    PlaceAndCommitLayouts(call, database, tree, placed);
                    Modding.ModServices.CaptureBeforeWrite(animations.PAK.Filepath);
                    bool saved;
                    try { saved = animations.PAK.Save(); }
                    finally { AnimationPakWrite.Written(animations); }
                    if (!saved)
                        throw new McpError("ANIMATION.PAK could not be written (is the game running, or the file read-only?). The edit is in memory only: the next edit_anim_tree or ANIMATION.PAK write takes it out.");
                    //This writes the tree set and string table only: a change an earlier failed write left in memory is not in it
                    if (_pendingPak.Count != 0)
                        call.Note("Still only in memory from an earlier failed write (edit_anim_tree doesn't write them): " + string.Join("; ", _pendingPak) + ". The next import_animation, remove_animation, edit_blend_set or edit_animation_events writes them.");
                    Singleton.OnAnimationsModified?.Invoke();
                });
            result["written"] = true;
            return result;
        }

        /* add_node ops that say where the new node is drawn */
        private static List<(string name, int x, int y)> PlacedNodes(JArray ops)
        {
            List<(string, int, int)> placed = new List<(string, int, int)>();
            foreach (JObject op in ops.OfType<JObject>())
            {
                if (!Same(ItemStr(op, "op"), "add_node")) continue;
                bool hasX = op["x"] != null && op["x"].Type != JTokenType.Null, hasY = op["y"] != null && op["y"].Type != JTokenType.Null;
                if (hasX != hasY) throw new McpError("add_node takes x and y together.");
                if (!hasX) continue;
                string name = (ItemStr(op, "name") ?? "").Trim();
                if (name.Length == 0) throw new McpError("add_node with x and y needs its 'name', to know which node they place.");
                int x = (int)Coerce(op["x"], typeof(int), "x"), y = (int)Coerce(op["y"], typeof(int), "y");
                CheckPlace(new System.Drawing.Point(x, y));
                placed.Add((name, x, y));
            }
            return placed;
        }

        /* After edit_anim_tree changed a tree: its stored layout moved onto the nodes' new keys (renamed nodes keep their place,
           removed ones drop out) and the open editor redrawn - as soon as the tree has changed in memory */
        private static void FollowTreeEdit(AnimTreeDB database, AnimationTree tree, TreeLayout held, Dictionary<AnimationNode, List<AnimTreeLayouts.NodeLayout>> heldNodes)
        {
            //A stored layout's change redraws the open editor; a tree laid out automatically has none to tell it, so it is redrawn here
            AnimTreeLayoutManager.Rekey(database, tree, held, heldNodes);
            if (held == null)
                AnimationTreeGraph.FindOpen(tree)?.ReloadFromStore();
        }

        /* Before the tree set is written: new nodes drawn where the ops said, and the set's layouts put into ANIMATION.PAK to go
           out with it (the editor's unsaved layouts of other sets stay for its own save, which writes their trees) */
        private static void PlaceAndCommitLayouts(McpCall call, AnimTreeDB database, AnimationTree tree, List<(string name, int x, int y)> placed)
        {
            if (placed.Count != 0)
            {
                OnTreeCanvas(database, tree, false, (canvas, live) =>
                {
                    foreach ((string name, int x, int y) in placed)
                    {
                        AnimationNode node = tree.Nodes.FirstOrDefault(o => o.Name == name);
                        IReadOnlyList<STNode> copies = canvas.CopiesOf(node);
                        if (copies.Count != 0)
                            copies[0].SetPosition(new System.Drawing.Point(x, y));
                    }
                    StoreCanvas(database, tree, canvas, live);
                    return 0;
                });
            }
            uint set = AnimTreeLayouts.SetHashOf(database, RequireAnimations().StringsDebug);
            if (!AnimTreeLayoutManager.Commit((s, t) => s == set, out string error))
                call.Note("The tree is written, but not its layout (" + error + "); the Animation Tree Editor shows it, and its Save writes it.");
        }

        /* The canvas becomes the tree's stored layout. An off-screen canvas's view means nothing: the view stored before is
           kept (none: the editor fits the tree to its view when it opens it) */
        private static void StoreCanvas(AnimTreeDB database, AnimationTree tree, AnimTreeCanvas canvas, bool live)
        {
            TreeLayout layout = canvas.Capture(withView: live);
            if (!live)
            {
                TreeLayout before = AnimTreeLayoutManager.Get(database, tree);
                if (before != null)
                {
                    layout.CanvasX = before.CanvasX;
                    layout.CanvasY = before.CanvasY;
                    layout.CanvasScale = before.CanvasScale;
                }
            }
            AnimTreeLayoutManager.Put(layout, live ? AnimationTreeGraph.FindOpen(tree) : null);
        }

        /// <summary>
        /// Work on a tree as it is drawn: on the Animation Tree Editor's own canvas when it shows the tree (so the user sees
        /// the change), else on an off-screen one laid out the same way - stored layout, or automatic. UI thread.
        /// </summary>
        private static T OnTreeCanvas<T>(AnimTreeDB database, AnimationTree tree, bool offScreen, Func<AnimTreeCanvas, bool, T> work)
        {
            AnimationTreeGraph open = offScreen ? null : AnimationTreeGraph.FindOpen(tree);
            if (open?.Canvas != null)
                return work(open.Canvas, true);
            using (STNodeEditor editor = new STNodeEditor()
            {
                Size = new System.Drawing.Size(1600, 1000),
                AllowNodeGraphLoops = true,
                AllowSameOwnerConnections = true,
                BackColor = System.Drawing.Color.FromArgb(34, 34, 34),
                Curvature = 0.3F,
                RoundedCornerRadius = 10,
            })
            {
                AnimTreeCanvas canvas = new AnimTreeCanvas(editor, database, tree, RequireAnimations().StringsDebug);
                canvas.Populate(AnimTreeLayoutManager.Get(database, tree));
                return work(canvas, false);
            }
        }

        /* Where each node is drawn: x, y of its first copy, and every copy with the links it draws when it has ghosts */
        private static Dictionary<AnimationNode, JObject> DrawnJson(AnimTreeCanvas canvas)
        {
            Dictionary<AnimationNode, JObject> drawn = new Dictionary<AnimationNode, JObject>(ByReference.Instance);
            List<(STNodeOption output, STNodeOption input)> links = canvas.DrawnLinks();
            foreach (AnimationNode node in new AnimationNode[] { canvas.Tree }.Concat(canvas.Tree.Nodes))
            {
                if (node == null || drawn.ContainsKey(node)) continue;
                IReadOnlyList<STNode> copies = canvas.CopiesOf(node);
                if (copies.Count == 0) continue;
                JObject row = new JObject() { ["x"] = copies[0].Left, ["y"] = copies[0].Top };
                if (copies.Count > 1)
                {
                    JArray each = new JArray();
                    for (int i = 0; i < copies.Count; i++)
                    {
                        JArray draws = new JArray();
                        foreach ((STNodeOption output, STNodeOption input) in links)
                        {
                            if (output.Owner == copies[i])
                                draws.Add(new JObject() { ["pin"] = output.ShortGUID.ToString(), ["to"] = Label(input.Owner.AnimationNode), ["to_pin"] = input.ShortGUID.ToString(), ["to_ghost"] = canvas.CopyIndexOf(input.Owner) });
                            else if (input.Owner == copies[i])
                                draws.Add(new JObject() { ["pin"] = input.ShortGUID.ToString(), ["from"] = Label(output.Owner.AnimationNode), ["from_pin"] = output.ShortGUID.ToString(), ["from_ghost"] = canvas.CopyIndexOf(output.Owner) });
                        }
                        each.Add(new JObject() { ["ghost"] = i, ["x"] = copies[i].Left, ["y"] = copies[i].Top, ["draws"] = draws });
                    }
                    row["copies"] = each;
                }
                drawn[node] = row;
            }
            return drawn;
        }

        private static readonly string[] _treeLayoutActions = { "arrange", "reset", "move", "add_ghost", "move_links", "remove_ghost" };

        private static object EditAnimTreeLayout(McpCall call)
        {
            string setName = call.Str("set", required: true);
            string treeName = call.Str("tree", required: true);
            string action = (call.Str("action", required: true) ?? "").Trim().ToLowerInvariant();
            if (!_treeLayoutActions.Contains(action))
                throw new McpError("'action' is one of " + string.Join(", ", _treeLayoutActions) + ".");
            bool dryRun = call.Bool("dry_run");
            //The layout is written in ANIMATION.PAK, which a running game holds open
            if (!dryRun && call.Bool("close_game") && GameRunning())
                CloseGame(call);

            using (McpEditorTools.Heartbeat(call, "Laying out the tree"))
                return McpEditor.UI(() =>
                {
                    Anim animations = RequireAnimations();
                    AnimTreeDB database = FindTreeSet(animations, setName);
                    AnimationTree tree = FindTree(database, treeName);
                    JObject result = new JObject() { ["tree"] = TreeSetName(database) + "\\" + tree.Name, ["action"] = action };
                    bool hadLayout = AnimTreeLayoutManager.Has(database, tree);

                    if (action == "reset")
                    {
                        if (!hadLayout)
                        {
                            result["unchanged"] = "the tree has no stored layout, so it is laid out automatically already";
                            return result;
                        }
                        if (dryRun)
                        {
                            result["dry_run"] = true;
                            return result;
                        }
                        AnimTreeLayoutManager.Forget(database, tree);
                        WriteTreeLayouts(call, database, tree, result);
                        return result;
                    }

                    object outcome = OnTreeCanvas(database, tree, dryRun, (canvas, live) =>
                    {
                        //Nothing moved on a tree laid out automatically still stores its layout: that is the change
                        string unchanged;
                        try { unchanged = ApplyLayoutAction(call, action, tree, canvas, result); }
                        catch (McpError)
                        {
                            //The editor's canvas is put back as the tree's layout has it, so a refused call leaves nothing half done there
                            if (live)
                                AnimationTreeGraph.FindOpen(tree)?.ReloadFromStore();
                            throw;
                        }
                        if (unchanged != null && hadLayout)
                        {
                            result["unchanged"] = unchanged;
                            return result;
                        }
                        if (live)
                            canvas.Editor.Invalidate();
                        if (dryRun)
                        {
                            result["dry_run"] = true;
                            return result;
                        }
                        StoreCanvas(database, tree, canvas, live);
                        WriteTreeLayouts(call, database, tree, result);
                        return result;
                    });
                    if (dryRun && AnimationTreeGraph.FindOpen(tree) != null)
                        call.Note("dry_run worked on a copy: the Animation Tree Editor still shows the tree as it was.");
                    return outcome;
                });
        }

        /* This tree's layout into ANIMATION.PAK (kept there with the trees) and the PAK written now - unless the game holds it
           open, when it goes out with the PAK's next write. Changes the user has not saved yet, to other trees, wait for their
           own save; so does the layout of a tree with changes not saved yet, as it draws the tree as it is now, not as written. */
        private static void WriteTreeLayouts(McpCall call, AnimTreeDB database, AnimationTree tree, JObject result)
        {
            Anim animations = RequireAnimations();
            string changedOnDisk = AnimationPakWrite.ChangedOnDisk(animations.PAK.Filepath);
            if (changedOnDisk != null)
            {
                result["written"] = false;
                call.Note("The layout is changed in the editor but not written: " + changedOnDisk);
                return;
            }
            PAK2.File entry = animations.PAK.Entries.FirstOrDefault(o => Same(o.Filename, database.Filepath));
            byte[] asWritten = entry?.Content;
            if (asWritten == null || !asWritten.AsSpan().SequenceEqual(database.ToBytes()))
            {
                result["written"] = false;
                call.Note("The tree set has changes not saved yet, so the layout waits to go into ANIMATION.PAK with them (the Animation Tree Editor's Save, or edit_anim_tree): it draws the trees as they are now, not as they are written.");
                return;
            }
            uint set = AnimTreeLayouts.SetHashOf(database, animations.StringsDebug), name = AnimTreeLayouts.TreeHashOf(tree, animations.StringsDebug);
            if (!AnimTreeLayoutManager.Commit((s, t) => s == set && t == name, out string error))
                throw new McpError("The layout is changed in the editor, but could not go into ANIMATION.PAK (" + error + "). The Animation Tree Editor's Save writes it.");
            if (GameRunning())
            {
                result["written"] = false;
                call.Note("Alien: Isolation is running and holds ANIMATION.PAK open, so the layout is kept in memory: it is written with the PAK's next write (the Animation Tree Editor's Save, or any tool writing ANIMATION.PAK). Pass close_game:true to close the game and write it now.");
                return;
            }
            Modding.ModServices.CaptureBeforeWrite(animations.PAK.Filepath);
            bool saved;
            try { saved = animations.PAK.Save(); }
            finally { AnimationPakWrite.Written(animations); }
            if (!saved)
                throw new McpError("The layout is changed in the editor, but ANIMATION.PAK could not be written (is the file read-only?). It goes out with the PAK's next write.");
            if (_pendingPak.Count != 0)
                call.Note("Still only in memory from an earlier failed write (this writes the layout only): " + string.Join("; ", _pendingPak) + ". The next import_animation, remove_animation, edit_blend_set or edit_animation_events writes them.");
            result["written"] = AnimTreeLayoutManager.PakPath;
        }

        /* Does one layout action on the canvas, describing it in result. Returns why nothing changed, or null when something did. */
        private static string ApplyLayoutAction(McpCall call, string action, AnimationTree tree, AnimTreeCanvas canvas, JObject result)
        {
            switch (action)
            {
                case "arrange":
                    {
                        int changed = canvas.ArrangeAll(AnimTreeCanvas.Origin);
                        result["nodes_changed"] = changed;
                        result["ghosts"] = new JArray(new AnimationNode[] { tree }.Concat(tree.Nodes).Distinct(ByReference.Instance)
                            .Where(o => canvas.CopiesOf(o).Count > 1).Select(o => Label(o) + " x" + canvas.CopiesOf(o).Count));
                        if (canvas.Editor.Created)
                            canvas.FitView();
                        return changed == 0 ? "the tree is laid out that way already" : null;
                    }
                case "move":
                    {
                        JArray specs = call.Array("nodes", required: true);
                        if (specs.Count == 0) throw new McpError("'nodes' is empty.");
                        //Every entry checked before any node moves, so a bad one changes nothing
                        List<(AnimationNode node, STNode copy, System.Drawing.Point to)> moves = new List<(AnimationNode, STNode, System.Drawing.Point)>();
                        for (int i = 0; i < specs.Count; i++)
                        {
                            JObject spec = specs[i] as JObject ?? throw new McpError("nodes[" + i + "] must be an object such as {node:'Walk', x:400, y:120}.");
                            try
                            {
                                CheckKeys(spec, "A node", "node", "node_type", "ghost", "x", "y", "dx", "dy");
                                AnimationNode node = ResolveNode(tree, ItemStr(spec, "node"), ItemStr(spec, "node_type"), "node");
                                STNode copy = CopyOf(canvas, node, spec["ghost"] == null ? 0 : (int)Coerce(spec["ghost"], typeof(int), "ghost"));
                                bool absolute = spec["x"] != null || spec["y"] != null, relative = spec["dx"] != null || spec["dy"] != null;
                                if (absolute == relative) throw new McpError("Give x and y, or dx and dy.");
                                System.Drawing.Point to = absolute
                                    ? new System.Drawing.Point((int)Coerce(spec["x"] ?? throw new McpError("x and y go together."), typeof(int), "x"), (int)Coerce(spec["y"] ?? throw new McpError("x and y go together."), typeof(int), "y"))
                                    : new System.Drawing.Point(copy.Left + (spec["dx"] == null ? 0 : (int)Coerce(spec["dx"], typeof(int), "dx")), copy.Top + (spec["dy"] == null ? 0 : (int)Coerce(spec["dy"], typeof(int), "dy")));
                                CheckPlace(to);
                                moves.Add((node, copy, to));
                            }
                            catch (McpError e) { throw new McpError("nodes[" + i + "]: " + e.Message + " Nothing was changed."); }
                        }
                        JArray moved = new JArray();
                        foreach ((AnimationNode node, STNode copy, System.Drawing.Point to) in moves)
                        {
                            if (copy.Location == to) continue;
                            copy.SetPosition(to);
                            moved.Add(Label(node) + (canvas.CopyIndexOf(copy) == 0 ? "" : "#" + canvas.CopyIndexOf(copy)) + " -> " + to.X + "," + to.Y);
                        }
                        result["moved"] = moved;
                        return moved.Count == 0 ? "every node is there already" : null;
                    }
                case "add_ghost":
                case "move_links":
                    {
                        AnimationNode node = ResolveNode(tree, call.Str("node", required: true), call.Str("node_type"), "node");
                        if (node is AnimationTree) throw new McpError("The tree's own node is drawn once.");
                        List<string> others = call.StrList("links") ?? new List<string>();
                        if (action == "move_links" && others.Count == 0) throw new McpError("'links' names the nodes whose links with " + node.Name + " move.");

                        //Every name checked, and every pair shown to share a link, before anything on the canvas changes
                        List<AnimationNode> partners = new List<AnimationNode>();
                        foreach (string otherName in others)
                        {
                            AnimationNode other = ResolveLinked(tree, otherName);
                            bool shares = canvas.DrawnLinks().Any(o => (ReferenceEquals(o.output.Owner.AnimationNode, node) && ReferenceEquals(o.input.Owner.AnimationNode, other))
                                || (ReferenceEquals(o.input.Owner.AnimationNode, node) && ReferenceEquals(o.output.Owner.AnimationNode, other)));
                            if (!shares)
                                throw new McpError(Label(node) + " and " + Label(other) + " share no link. get_anim_tree lists " + node.Name + "'s links.");
                            partners.Add(other);
                        }

                        STNode target;
                        if (action == "add_ghost")
                        {
                            if (call.Has("x") != call.Has("y")) throw new McpError("x and y go together.");
                            if (call.Has("ghost")) throw new McpError("add_ghost makes a new copy: 'ghost' is for move_links and remove_ghost.");
                            if (call.Has("x")) CheckPlace(new System.Drawing.Point(call.Int("x"), call.Int("y")));
                            STNode first = canvas.CopiesOf(node)[0];
                            target = canvas.AddGhost(node, new System.Drawing.Point(first.Left - 100000, first.Top - 100000));
                        }
                        else
                        {
                            if (!call.Has("ghost")) throw new McpError("'ghost' is the copy the links move to (0 = the first).");
                            target = CopyOf(canvas, node, call.Int("ghost"));
                        }

                        JArray movedLinks = new JArray();
                        foreach (AnimationNode other in partners)
                        {
                            int count = 0;
                            foreach ((STNodeOption output, STNodeOption input) in canvas.DrawnLinks())
                            {
                                bool fromNode = ReferenceEquals(output.Owner.AnimationNode, node) && ReferenceEquals(input.Owner.AnimationNode, other);
                                bool toNode = ReferenceEquals(input.Owner.AnimationNode, node) && ReferenceEquals(output.Owner.AnimationNode, other);
                                if (!fromNode && !toNode) continue;
                                STNodeOption own = fromNode ? output : input, far = fromNode ? input : output;
                                if (own.Owner == target) { count++; continue; }
                                if (canvas.MoveLink(own, far, target)) count++;
                            }
                            if (count == 0)
                                throw new McpError(Label(node) + " and " + Label(other) + " share no link. get_anim_tree lists " + node.Name + "'s links.");
                            movedLinks.Add(Label(other) + " (" + count + ")");
                        }

                        if (action == "add_ghost")
                        {
                            if (call.Has("x"))
                                target.SetPosition(new System.Drawing.Point(call.Int("x"), call.Int("y")));
                            else if (partners.Count != 0)
                            {
                                //Beside the first node it draws a link with
                                canvas.PlaceBesideLinks(new List<STNode>() { target });
                            }
                            else
                            {
                                //Drawing nothing yet: just below and right of the first copy, as the editor's Add Ghost Node puts it
                                STNode first = canvas.CopiesOf(node)[0];
                                target.SetPosition(new System.Drawing.Point(first.Left + 40, first.Bottom + 40));
                            }
                            result["ghost"] = canvas.CopyIndexOf(target);
                            result["at"] = new JArray(target.Left, target.Top);
                        }
                        result["links"] = movedLinks;
                        return null;
                    }
                case "remove_ghost":
                    {
                        AnimationNode node = ResolveNode(tree, call.Str("node", required: true), call.Str("node_type"), "node");
                        if (!call.Has("ghost")) throw new McpError("'ghost' is the copy to remove (1 = the first ghost).");
                        STNode copy = CopyOf(canvas, node, call.Int("ghost"));
                        if (!canvas.RemoveCopy(copy))
                            throw new McpError(Label(node) + " is drawn once, so it has no ghost to remove (remove_node in edit_anim_tree deletes the node).");
                        result["removed"] = Label(node) + "#" + call.Int("ghost");
                        result["copies_left"] = canvas.CopiesOf(node).Count;
                        return null;
                    }
            }
            throw new McpError("'action' is one of " + string.Join(", ", _treeLayoutActions) + ".");
        }

        /* Somewhere on the canvas a picture of the tree can still reach */
        private static void CheckPlace(System.Drawing.Point at)
        {
            const int Furthest = 1000000;
            if (Math.Abs((long)at.X) > Furthest || Math.Abs((long)at.Y) > Furthest)
                throw new McpError("x and y must be within " + Furthest + " of 0.");
        }

        /* A node named in 'links': 'name', or 'name:TYPE' when several nodes of different types share the name */
        private static AnimationNode ResolveLinked(AnimationTree tree, string text)
        {
            string name = (text ?? "").Trim(), type = null;
            int colon = name.LastIndexOf(':');
            if (colon > 0)
            {
                string after = name.Substring(colon + 1).Trim();
                if (Enum.GetNames(typeof(NodeType)).Any(o => Same(o, after) || Same(o, "ANIM_" + after)))
                {
                    type = after;
                    name = name.Substring(0, colon).Trim();
                }
            }
            try { return ResolveNode(tree, name, type, "links"); }
            catch (McpError e) when (type == null && e.Message.StartsWith("Several nodes"))
            {
                throw new McpError("Several nodes are called '" + name + "': name the one in 'links' as '" + name + ":TYPE', e.g. '" + name + ":ANIM_Parameter'.");
            }
        }

        private static STNode CopyOf(AnimTreeCanvas canvas, AnimationNode node, int ghost)
        {
            IReadOnlyList<STNode> copies = canvas.CopiesOf(node);
            if (ghost < 0 || ghost >= copies.Count)
                throw new McpError(Label(node) + " is drawn " + (copies.Count == 1 ? "once (ghost 0 only)" : copies.Count + " times (ghost 0 to " + (copies.Count - 1) + ")") + ".");
            return copies[ghost];
        }

        private static object OpenAnimTree(McpCall call)
        {
            string setName = call.Str("set", required: true);
            string treeName = call.Str("tree", required: true);
            return McpEditor.UI(() =>
            {
                Anim animations = RequireAnimations();
                AnimTreeDB database = FindTreeSet(animations, setName);
                AnimationTree tree = FindTree(database, treeName);
                AnimTreeEditor editor = Singleton.Editor?.ShowAnimTreeEditor() ?? throw new McpError("The editor isn't ready.");
                AnimationTreeGraph graph = editor.OpenTree(database, tree) ?? throw new McpError("The Animation Tree Editor could not show " + tree.Name + ".");
                return new JObject()
                {
                    ["opened"] = TreeSetName(database) + "\\" + tree.Name,
                    ["layout"] = AnimTreeLayoutManager.Has(database, tree) ? "saved" : "auto",
                    ["nodes_drawn"] = graph.Canvas?.Editor.Nodes.Count ?? 0,
                };
            });
        }

        private static object CaptureAnimTree(McpCall call)
        {
            string setName = call.Str("set", required: true);
            string treeName = call.Str("tree", required: true);
            int maxWidth = Math.Min(4096, Math.Max(200, call.Int("max_width", 1600)));
            string caption = null;
            byte[] png;
            using (McpEditorTools.Heartbeat(call, "Drawing the tree"))
            {
                png = McpEditor.UI(() =>
                {
                    Anim animations = RequireAnimations();
                    AnimTreeDB database = FindTreeSet(animations, setName);
                    AnimationTree tree = FindTree(database, treeName);
                    AnimationNode around = call.Has("node") ? ResolveNode(tree, call.Str("node"), call.Str("node_type"), "node") : null;
                    //The editor's own canvas only when it has room to draw on; otherwise an off-screen one draws the same layout
                    STNodeEditor shown = AnimationTreeGraph.FindOpen(tree)?.Canvas?.Editor;
                    bool offScreen = shown == null || !shown.Created || shown.Width < 50 || shown.Height < 50;
                    return OnTreeCanvas(database, tree, offScreen, (canvas, live) =>
                    {
                        List<STNode> nodes = canvas.Editor.Nodes.ToArray().Where(o => o != null).ToList();
                        if (nodes.Count == 0)
                            throw new McpError(tree.Name + " has no nodes to picture.");
                        System.Drawing.Rectangle bounds = McpPageTools.Bounds(nodes);
                        if (around != null)
                        {
                            System.Drawing.Rectangle near = McpPageTools.Bounds(canvas.CopiesOf(around));
                            near.Inflate(1400, 900);
                            bounds = near;
                        }
                        bounds.Inflate(60, 60);
                        const int MaxSide = 40000;
                        bool cropped = bounds.Width > MaxSide || bounds.Height > MaxSide;
                        if (cropped)
                            bounds = new System.Drawing.Rectangle(bounds.X, bounds.Y, Math.Min(bounds.Width, MaxSide), Math.Min(bounds.Height, MaxSide));
                        double scale = Math.Min(1.0, Math.Min(maxWidth / (double)bounds.Width, maxWidth * 2.0 / bounds.Height));
                        byte[] data = McpPageTools.Render(canvas.Editor, bounds, scale);
                        caption = TreeSetName(database) + "\\" + tree.Name + " (" + (AnimTreeLayoutManager.Has(database, tree) ? "stored layout" : "laid out automatically") + ")" +
                            (around != null ? ", around " + Label(around) : "") + ": " + nodes.Count + " node(s) drawn, canvas " + bounds.X + "," + bounds.Y + " to " + bounds.Right + "," + bounds.Bottom +
                            " at " + Math.Round(scale * 100) + "%." +
                            (cropped ? " The tree spreads further than " + MaxSide + " units, so only its top-left part is shown: pass 'node' to picture another area." : "");
                        return data;
                    });
                });
            }
            return new McpImage() { Data = png, MimeType = "image/png", Caption = caption };
        }

        #endregion

        #region Behaviour trees
        private static string BehaviourFile
        {
            get
            {
                if (string.IsNullOrEmpty(Singleton.PathToAI)) throw new McpError("The game's folder isn't known.");
                return Path.Combine(Singleton.PathToAI, "DATA", "BINARY_BEHAVIOR", "_DIRECTORY_CONTENTS.BML");
            }
        }

        private static XmlDocument ReadBehaviours(out BML bml)
        {
            string file = BehaviourFile;
            if (!File.Exists(file)) throw new McpError("The behaviour trees file is missing (" + Relative(file) + "); verifying the game files restores it.");
            bml = new BML(file);
            XmlDocument document = bml.Loaded ? bml.Content : null;
            if (document == null) throw new McpError(Relative(file) + " could not be read.");
            return document;
        }

        private static List<XmlElement> BehaviourFiles(XmlDocument document) => document.SelectNodes("//DIR/File").OfType<XmlElement>().ToList();
        private static string BehaviourName(XmlElement file) => Path.GetFileNameWithoutExtension(file.GetAttribute("name"));

        private static XmlElement FindBehaviour(List<XmlElement> files, string name)
        {
            name = (name ?? "").Trim();
            string stem = Path.GetFileNameWithoutExtension(name);
            return files.FirstOrDefault(o => Same(BehaviourName(o), name) || Same(o.GetAttribute("name"), name) || Same(BehaviourName(o), stem));
        }

        private static XmlElement RequireBehaviour(List<XmlElement> files, string name)
        {
            XmlElement file = FindBehaviour(files, name);
            if (file != null) return file;
            throw new McpError("There is no behaviour tree '" + name + "'" + McpGlobalAssetChecks.DidYouMean(files.Select(BehaviourName), name, " (get_behaviour_tree without a name lists them)."));
        }

        private static List<string> References(XmlElement file)
        {
            return file.SelectNodes(".//*[@ReferenceFilename]").OfType<XmlElement>()
                .Select(o => Path.GetFileNameWithoutExtension(o.GetAttribute("ReferenceFilename"))).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string Pretty(IEnumerable<XmlNode> nodes)
        {
            StringBuilder text = new StringBuilder();
            XmlWriterSettings settings = new XmlWriterSettings() { Indent = true, OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment };
            using (XmlWriter writer = XmlWriter.Create(text, settings))
                foreach (XmlNode node in nodes) node.WriteTo(writer);
            return text.ToString();
        }

        private static List<XmlNode> SelectInTree(XmlElement file, string xpath, string what)
        {
            string path = (xpath ?? "").Trim();
            if (path.Length == 0) throw new McpError("'" + what + "' is required.");
            if (path.StartsWith("/"))
                throw new McpError("XPaths are relative to the tree: start with ./ or .// (e.g. .//Node[@Class='LegendPlugin.Nodes.ActionSuccess']), or a child name such as Behavior.");
            XmlNodeList found;
            try { found = file.SelectNodes(path); }
            catch (XPathException e) { throw new McpError("'" + path + "' is not a valid XPath: " + e.Message); }
            List<XmlNode> nodes = found.OfType<XmlNode>().ToList();
            foreach (XmlNode node in nodes)
            {
                //The File element's own attributes are its directory entry (the tree's name), not part of the tree
                if (node is XmlAttribute own && own.OwnerElement == file)
                    throw new McpError("'" + path + "' selects the tree's directory entry, not the tree: select nodes inside it, such as ./Behavior or .//Node.");
                XmlNode step = node is XmlAttribute attribute ? attribute.OwnerElement : node;
                while (step != null && step != file) step = step.ParentNode;
                if (step == null || node == file) throw new McpError("'" + path + "' reaches outside the tree.");
            }
            return nodes;
        }

        /// <summary>Which character classes run each root tree, from the attribute configs (cached until they change).</summary>
        private static Dictionary<string, List<string>> ClassesByTree(out BehaviorTreeDB.Requirements requirements)
        {
            Dictionary<string, List<string>> byTree = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            requirements = null;
            try { requirements = BehaviourTreeLevels.Requirements; }
            catch { return byTree; }
            if (requirements?.ClassTrees == null) return byTree;
            foreach (KeyValuePair<string, string> pair in requirements.ClassTrees)
            {
                if (string.IsNullOrEmpty(pair.Value)) continue;
                if (!byTree.TryGetValue(pair.Value, out List<string> classes)) byTree[pair.Value] = classes = new List<string>();
                classes.Add(pair.Key);
            }
            return byTree;
        }

        private static string ShortClass(string type)
        {
            int dot = (type ?? "").LastIndexOf('.');
            return dot >= 0 ? type.Substring(dot + 1) : type ?? "";
        }

        /// <summary>A tree as indented lines: each Node's class and attributes, each Connector's identifier in brackets.</summary>
        private static void Outline(XmlElement element, int depth, int most, StringBuilder text)
        {
            foreach (XmlElement child in element.ChildNodes.OfType<XmlElement>())
            {
                if (child.Name == "Node")
                {
                    if (depth > most) { text.Append(new string(' ', depth * 2)).AppendLine("..."); continue; }
                    text.Append(new string(' ', depth * 2)).Append(ShortClass(child.GetAttribute("Class")));
                    foreach (XmlAttribute attribute in child.Attributes)
                        if (attribute.Name != "Class") text.Append(' ').Append(attribute.Name).Append('=').Append(attribute.Value);
                    text.AppendLine();
                    Outline(child, depth + 1, most, text);
                }
                else if (child.Name == "Connector")
                {
                    text.Append(new string(' ', depth * 2)).Append('[').Append(child.GetAttribute("Identifier")).AppendLine("]");
                    Outline(child, depth + 1, most, text);
                }
                else if (child.Name == "Comment")
                    text.Append(new string(' ', depth * 2)).Append("// ").AppendLine(child.GetAttribute("Text"));
                else
                    Outline(child, depth, most, text);
            }
        }

        /// <summary>Every node Class across the trees: how often, in how many trees, its attributes with sample values, and its connectors.</summary>
        private sealed class NodeClass
        {
            public int Count;
            public HashSet<string> Trees = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, List<string>> Attributes = new Dictionary<string, List<string>>();
            public HashSet<string> Connectors = new HashSet<string>();
        }

        private static Dictionary<string, NodeClass> Catalogue(List<XmlElement> files)
        {
            Dictionary<string, NodeClass> classes = new Dictionary<string, NodeClass>(StringComparer.Ordinal);
            foreach (XmlElement file in files)
                foreach (XmlElement node in file.SelectNodes(".//Node").OfType<XmlElement>())
                {
                    string type = node.GetAttribute("Class");
                    if (type.Length == 0) continue;
                    if (!classes.TryGetValue(type, out NodeClass entry)) classes[type] = entry = new NodeClass();
                    entry.Count++;
                    entry.Trees.Add(BehaviourName(file));
                    foreach (XmlAttribute attribute in node.Attributes)
                    {
                        if (attribute.Name == "Class") continue;
                        if (!entry.Attributes.TryGetValue(attribute.Name, out List<string> samples)) entry.Attributes[attribute.Name] = samples = new List<string>();
                        if (samples.Count < 4 && !samples.Contains(attribute.Value)) samples.Add(attribute.Value);
                    }
                    foreach (XmlElement connector in node.ChildNodes.OfType<XmlElement>().Where(o => o.Name == "Connector"))
                        entry.Connectors.Add(connector.GetAttribute("Identifier"));
                }
            return classes;
        }

        private static object GetBehaviourTree(McpCall call)
        {
            string name = call.Str("name");
            string className = call.Str("class");
            string filter = (call.Str("filter") ?? "").Trim();
            string xpath = call.Str("xpath");
            bool outline = call.Bool("outline");
            bool catalogue = call.Bool("catalogue");
            int depth = call.Int("depth", 12);
            int maxChars = call.Int("max_chars", 60000);
            if (maxChars < 100) throw new McpError("'max_chars' must be at least 100.");
            int limit = Limit(call, 200);
            if (name != null && className != null) throw new McpError("Give name or class, not both.");
            if (outline && xpath != null) throw new McpError("Give outline or xpath, not both.");
            XmlDocument document = ReadBehaviours(out BML _);
            List<XmlElement> files = BehaviourFiles(document);
            Dictionary<string, List<string>> classesByTree = ClassesByTree(out BehaviorTreeDB.Requirements requirements);

            if (catalogue)
            {
                if (name != null || className != null) throw new McpError("catalogue covers every tree: leave out name and class.");
                string[] words = Words(filter);
                List<KeyValuePair<string, NodeClass>> listed = Catalogue(files).Where(o => AllWords(words, o.Key)).OrderByDescending(o => o.Value.Count).ToList();
                if (listed.Count > limit) call.Note(listed.Count + " node classes; the " + limit + " most used are listed (filter narrows it).");
                return new JObject()
                {
                    ["total"] = listed.Count,
                    ["classes"] = new JArray(listed.Take(limit).Select(o => new JObject()
                    {
                        ["class"] = o.Key,
                        ["uses"] = o.Value.Count,
                        ["trees"] = o.Value.Trees.Count,
                        ["attributes"] = new JObject(o.Value.Attributes.OrderBy(a => a.Key).Select(a => new JProperty(a.Key, new JArray(a.Value)))),
                        ["connectors"] = new JArray(o.Value.Connectors.OrderBy(c => c)),
                    })),
                };
            }

            if (className != null)
            {
                if (requirements?.ClassTrees == null || requirements.ClassTrees.Count == 0)
                    throw new McpError("The character attribute configs could not be read, so which tree a class runs isn't known (get_config_record kind 'attributes').");
                string key = requirements.ClassTrees.Keys.FirstOrDefault(o => Same(o, className.Trim()));
                if (key == null)
                    throw new McpError("No character class is called '" + className + "'" + McpGlobalAssetChecks.DidYouMean(requirements.ClassTrees.Keys, className, " (get_config_record kind 'attributes' lists them)."));
                name = requirements.ClassTrees[key];
                if (string.IsNullOrEmpty(name)) throw new McpError(key + " names no behaviour tree.");
                call.Note(key + " runs the tree " + name + ".");
            }

            if (name == null)
            {
                if (xpath != null || outline) throw new McpError("'xpath' and 'outline' need 'name'.");
                string[] words = Words(filter);
                List<XmlElement> listed = files.Where(o => AllWords(words, BehaviourName(o))).OrderBy(BehaviourName, StringComparer.OrdinalIgnoreCase).ToList();
                if (listed.Count > limit) call.Note(listed.Count + " trees match; " + limit + " are listed.");
                return new JObject()
                {
                    ["file"] = Relative(BehaviourFile),
                    ["total"] = listed.Count,
                    ["trees"] = new JArray(listed.Take(limit).Select(o =>
                    {
                        JObject row = new JObject()
                        {
                            ["name"] = BehaviourName(o),
                            ["nodes"] = o.SelectNodes(".//Node").Count,
                            ["references"] = new JArray(References(o)),
                        };
                        if (classesByTree.TryGetValue(BehaviourName(o), out List<string> classes)) row["used_by_classes"] = new JArray(classes);
                        return row;
                    })),
                };
            }

            XmlElement file = RequireBehaviour(files, name);
            string treeName = BehaviourName(file);
            JObject result = new JObject()
            {
                ["name"] = treeName,
                ["nodes"] = file.SelectNodes(".//Node").Count,
                ["references"] = new JArray(References(file)),
                ["referenced_by"] = new JArray(files.Where(o => o != file && References(o).Any(r => Same(r, treeName))).Select(BehaviourName)),
                ["used_by_classes"] = new JArray(classesByTree.TryGetValue(treeName, out List<string> users) ? users : new List<string>()),
            };
            //A level only loads the root trees its BEHAVIOR_TREE.DB lists (regenerated when it is saved)
            List<string> levelList = McpEditor.UI(() => Singleton.Editor?.CompositeBrowser?.Content?.Level?.BehaviorTreeDB?.Entries?.ToList());
            if (levelList != null)
                result["listed_by_open_level"] = levelList.Contains(treeName, StringComparer.OrdinalIgnoreCase);
            string xml;
            if (outline)
            {
                StringBuilder text = new StringBuilder();
                Outline(file, 0, Math.Max(1, depth), text);
                xml = text.ToString();
                result["outline_chars"] = xml.Length;
                if (xml.Length > maxChars)
                {
                    call.Note("The outline is " + xml.Length + " characters; the first " + maxChars + " are shown. Lower depth, use xpath, or raise max_chars.");
                    xml = xml.Substring(0, maxChars);
                }
                result["outline"] = xml;
                return result;
            }
            if (xpath != null)
            {
                List<XmlNode> nodes = SelectInTree(file, xpath, "xpath");
                result["matches"] = nodes.Count;
                if (nodes.Count > limit) call.Note(nodes.Count + " nodes match; the first " + limit + " are shown.");
                xml = Pretty(nodes.Take(limit));
            }
            else
                xml = Pretty(file.ChildNodes.OfType<XmlNode>());
            result["xml_chars"] = xml.Length;
            if (xml.Length > maxChars)
            {
                call.Note("The XML is " + xml.Length + " characters; the first " + maxChars + " are shown. Use xpath for part of it, or raise max_chars.");
                xml = xml.Substring(0, maxChars);
            }
            result["xml"] = xml;
            return result;
        }

        private static readonly string[] BehaviourOpKeys = { "op", "xpath", "attribute", "value", "xml", "index", "expect" };

        private static string ApplyBehaviourOp(XmlElement file, JObject op)
        {
            CheckKeys(op, "An op", BehaviourOpKeys);
            string kind = (ItemStr(op, "op") ?? "").Trim().ToLowerInvariant();
            string xpath = ItemStr(op, "xpath");
            List<XmlNode> nodes = SelectInTree(file, xpath, "xpath");
            if (op["expect"] != null && op["expect"].Type == JTokenType.Integer && nodes.Count != (int)op["expect"])
                throw new McpError("'" + xpath + "' selects " + nodes.Count + " nodes, not " + (int)op["expect"] + ".");
            if (nodes.Count == 0) throw new McpError("'" + xpath + "' selects nothing.");
            switch (kind)
            {
                case "set_attribute":
                    {
                        string attribute = (ItemStr(op, "attribute") ?? "").Trim();
                        if (attribute.Length == 0) throw new McpError("'attribute' is required.");
                        string value = ItemStr(op, "value") ?? throw new McpError("'value' is required.");
                        foreach (XmlNode node in nodes)
                        {
                            if (!(node is XmlElement element)) throw new McpError("'" + xpath + "' selects something other than elements.");
                            element.SetAttribute(attribute, value);
                        }
                        return "set " + attribute + "=\"" + value + "\" on " + nodes.Count + " node(s)";
                    }
                case "remove_attribute":
                    {
                        string attribute = (ItemStr(op, "attribute") ?? "").Trim();
                        if (attribute.Length == 0) throw new McpError("'attribute' is required.");
                        int removed = 0;
                        foreach (XmlNode node in nodes)
                            if (node is XmlElement element && element.HasAttribute(attribute)) { element.RemoveAttribute(attribute); removed++; }
                        if (removed == 0) throw new McpError("None of those nodes has a '" + attribute + "' attribute.");
                        return "removed " + attribute + " from " + removed + " node(s)";
                    }
                case "insert_xml":
                    {
                        if (nodes.Count != 1 || !(nodes[0] is XmlElement parent))
                            throw new McpError("insert_xml needs an xpath that selects exactly one element to insert into (it selects " + nodes.Count + ").");
                        string xml = ItemStr(op, "xml") ?? throw new McpError("'xml' is required.");
                        XmlDocumentFragment fragment = file.OwnerDocument.CreateDocumentFragment();
                        try { fragment.InnerXml = xml; }
                        catch (XmlException e) { throw new McpError("'xml' is not well formed: " + e.Message); }
                        List<XmlNode> inserted = fragment.ChildNodes.OfType<XmlNode>().Where(o => o is XmlElement).ToList();
                        if (inserted.Count == 0) throw new McpError("'xml' holds no element.");
                        List<XmlElement> children = parent.ChildNodes.OfType<XmlElement>().ToList();
                        int index = op["index"] == null || op["index"].Type == JTokenType.Null ? children.Count : (int)op["index"];
                        if (index < 0 || index > children.Count)
                            throw new McpError("'index' must be from 0 to " + children.Count + " (the parent has " + children.Count + " child elements).");
                        XmlNode before = index < children.Count ? children[index] : null;
                        foreach (XmlNode node in inserted)
                        {
                            if (before == null) parent.AppendChild(node);
                            else parent.InsertBefore(node, before);
                        }
                        return "inserted " + inserted.Count + " element(s) into " + parent.Name + " at " + index;
                    }
                case "remove":
                    {
                        foreach (XmlNode node in nodes)
                        {
                            if (node is XmlAttribute attribute) attribute.OwnerElement.RemoveAttributeNode(attribute);
                            else node.ParentNode?.RemoveChild(node);
                        }
                        return "removed " + nodes.Count + " node(s)";
                    }
                default:
                    throw new McpError("'op' is one of set_attribute, remove_attribute, insert_xml, remove.");
            }
        }

        private static object EditBehaviourTree(McpCall call)
        {
            string name = call.Str("name", required: true).Trim();
            JArray ops = call.Array("ops");
            string xml = call.Str("xml");
            bool create = call.Bool("create");
            bool dryRun = call.Bool("dry_run");
            bool closeGame = call.Bool("close_game");
            if (ops.Count == 0 && xml == null) throw new McpError("Give ops, xml, or both.");
            if (create && xml == null) throw new McpError("create needs xml: the new tree's content.");

            XmlDocument document = ReadBehaviours(out BML bml);
            List<XmlElement> files = BehaviourFiles(document);
            //The vocabulary every tree uses as it stands, to check the edited tree against
            Dictionary<string, NodeClass> known = Catalogue(files);
            XmlElement file;
            if (create)
            {
                if (FindBehaviour(files, name) != null) throw new McpError("There is already a behaviour tree called '" + name + "'.");
                if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z0-9_]+$")) throw new McpError("A new tree's name can only hold letters, digits and underscores.");
                if (name.Length > BehaviorTreeDB.MaxNameLength) throw new McpError("A tree's name can be at most " + BehaviorTreeDB.MaxNameLength + " characters (the game copies it into a fixed-size buffer).");
                //The Behaviour Tree Editor writes each tree to <name>.xml, and Windows opens a device for these names instead
                if (System.Text.RegularExpressions.Regex.IsMatch(name, "^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) throw new McpError(name + " is a name Windows keeps for a device: choose another.");
                XmlElement directory = document.SelectSingleNode("//DIR") as XmlElement ?? throw new McpError("The behaviour trees file has no DIR element.");
                string extension = files.Select(o => Path.GetExtension(o.GetAttribute("name"))).FirstOrDefault(o => !string.IsNullOrEmpty(o)) ?? ".bml";
                file = document.CreateElement("File");
                file.SetAttribute("name", name + extension);
                //In the order the Behaviour Tree Editor compiles them (names compared ignoring case), so its next save doesn't reshuffle the file
                XmlElement after = files.FirstOrDefault(o => string.Compare(o.GetAttribute("name"), file.GetAttribute("name"), StringComparison.OrdinalIgnoreCase) > 0);
                if (after != null && after.ParentNode == directory)
                    directory.InsertBefore(file, after);
                else
                    directory.AppendChild(file);
            }
            else
                file = RequireBehaviour(files, name);

            int before = file.SelectNodes(".//Node").Count;
            List<string> changes = new List<string>();
            if (xml != null)
            {
                XmlDocument parsed = new XmlDocument();
                try { parsed.LoadXml(xml); }
                catch (XmlException e) { throw new McpError("'xml' is not a well formed document: " + e.Message); }
                if (parsed.DocumentElement == null) throw new McpError("'xml' has no root element.");
                XmlNode root = document.ImportNode(parsed.DocumentElement, true);
                while (file.HasChildNodes) file.RemoveChild(file.FirstChild);
                file.AppendChild(root);
                changes.Add(create ? "created the tree" : "replaced the whole tree");
            }
            for (int i = 0; i < ops.Count; i++)
            {
                JObject op = ops[i] as JObject ?? throw new McpError("ops[" + i + "] must be an object.");
                try { changes.Add(ApplyBehaviourOp(file, op)); }
                catch (McpError e) { throw new McpError("ops[" + i + "] (" + (ItemStr(op, "op") ?? "?") + "): " + e.Message + " Nothing was changed."); }
            }
            if (file.ChildNodes.OfType<XmlElement>().Count() != 1)
                throw new McpError("After those changes the tree has " + file.ChildNodes.OfType<XmlElement>().Count() + " root elements instead of one (<Behavior>); nothing was changed.");
            //The game loads a tree from its Behavior's Node: without one it fails to load, and a level listing it would hand
            //every tree listed after it to the wrong characters
            if (file["Behavior"]?["Node"] == null)
                throw new McpError("After those changes the tree has no <Behavior><Node ...> root, which the game needs to load it; nothing was changed.");

            JObject result = new JObject()
            {
                ["name"] = BehaviourName(file),
                ["changes"] = new JArray(changes),
                ["nodes_before"] = create ? 0 : before,
                ["nodes_after"] = file.SelectNodes(".//Node").Count,
            };
            List<string> missing = References(file).Where(r => FindBehaviour(files, r) == null && !Same(r, BehaviourName(file))).ToList();
            if (missing.Count != 0) call.Note("It references trees that don't exist: " + string.Join(", ", missing) + ".");

            //Node classes and attributes no tree used before are most likely misspelt: the game would not know them
            List<string> unknown = new List<string>();
            foreach (XmlElement node in file.SelectNodes(".//Node").OfType<XmlElement>())
            {
                string type = node.GetAttribute("Class");
                if (type.Length == 0) { unknown.Add("a Node with no Class"); continue; }
                if (!known.TryGetValue(type, out NodeClass entry))
                {
                    List<string> near = McpGlobalAssetChecks.Suggest(known.Keys, type, 3);
                    unknown.Add("Class '" + type + "'" + (near.Count == 0 ? "" : " (did you mean " + string.Join(", ", near) + "?)"));
                    continue;
                }
                foreach (XmlAttribute attribute in node.Attributes)
                    if (attribute.Name != "Class" && !entry.Attributes.ContainsKey(attribute.Name))
                        unknown.Add("attribute '" + attribute.Name + "' on " + ShortClass(type) + (entry.Attributes.Count == 0 ? " (it takes no attributes)" : " (it takes " + string.Join(", ", entry.Attributes.Keys.Take(12)) + ")"));
            }
            if (unknown.Count != 0)
            {
                result["unrecognised"] = new JArray(unknown.Distinct().Take(20));
                call.Note("No other tree uses " + (unknown.Count == 1 ? "this" : "these") + " (see unrecognised); check the spelling against get_behaviour_tree catalogue:true.");
            }
            if (dryRun)
            {
                result["dry_run"] = true;
                return result;
            }

            bool editorRunning = ProcessRunning("BehaviourTreeEditor");
            if (GameRunning() || editorRunning)
            {
                if (!closeGame)
                    throw new McpError(McpErrorCodes.Refused, (editorRunning ? "The Behaviour Tree Editor is running (it rewrites this file when it saves)" : "Alien: Isolation is running")
                        + ". Close it, or pass close_game:true to close the game and that editor first.");
                CloseGame(call, new List<string>() { "BehaviourTreeEditor" });
            }
            Modding.ModServices.CaptureBeforeWrite(BehaviourFile);
            bml.Content = document;
            if (!bml.Save())
                throw new McpError(Relative(BehaviourFile) + " could not be written (is it read-only, or held open?). Nothing was changed.");
            result["written"] = true;
            call.Note("Written to " + Relative(BehaviourFile) + ". The Behaviour Tree Editor rebuilds DATA/BEHAVIOR from it the next time it starts.");
            if (create)
                call.Note("A new tree runs once a character class uses it as its Behavior_Tree (set_config_record kind 'attributes'); each level then lists it when the level is saved.");
            McpGameDataTools.NoteBehaviourTreeLevels(call, new[] { "BINARY_BEHAVIOR/_DIRECTORY_CONTENTS.BML" });
            return result;
        }
        #endregion

        #region Sounds
        internal static WwiseSoundLibrary SoundLibrary(McpCall call)
        {
            Task<WwiseSoundLibrary> building = McpEditor.UI(() =>
            {
                if (!SoundPreviewLibrary.IsAvailable) throw new McpError("The game's sound folder could not be found.");
                //Asked on the UI thread: which level is open decides whether its own sound package is part of the index
                return SoundPreviewLibrary.GetAsync();
            });
            try
            {
                using (McpEditorTools.Heartbeat(call, "Indexing the game's soundbanks"))
                    while (!building.Wait(250)) call.ThrowIfCancelled();
            }
            catch (AggregateException e)
            {
                throw new McpError("The game's soundbanks could not be indexed: " + e.GetBaseException().Message);
            }
            return building.Result;
        }

        private static uint ParseSourceId(string text)
        {
            if (!uint.TryParse((text ?? "").Trim(), out uint id)) throw new McpError("'source_id' is a whole number (describe_sound_event lists them).");
            return id;
        }

        private static WwiseSoundVariation PickVariation(McpCall call, WwiseSoundLibrary library, out string eventName)
        {
            eventName = call.Str("event");
            if (eventName != null) eventName = eventName.Trim();
            if (call.Has("source_id"))
            {
                if (call.Has("variation")) throw new McpError("Give variation or source_id, not both.");
                uint id = ParseSourceId(call.Str("source_id"));
                if (eventName != null)
                {
                    WwiseEventResolution resolved = library.Resolve(eventName);
                    WwiseSoundVariation found = resolved.Variations.FirstOrDefault(o => o.SourceId == id);
                    if (found == null) throw new McpError("'" + eventName + "' has no variation with source id " + id + " (describe_sound_event lists them).");
                    return found;
                }
                IList<WwiseMediaLocation> copies = library.AllCopies(id);
                if (copies.Count == 0) throw new McpError("No audio with source id " + id + " ships with the game.");
                return new WwiseSoundVariation() { SourceId = id, Media = copies[0] };
            }
            if (eventName == null) throw new McpError("Give event (and variation), or source_id.");
            WwiseEventResolution resolution = library.Resolve(eventName);
            if (resolution.Variations.Count == 0)
                throw new McpError("'" + eventName + "' plays no audio: " + resolution.Explanation);
            int index = call.Int("variation", 0);
            if (index < 0 || index >= resolution.Variations.Count)
                throw new McpError("'" + eventName + "' has " + resolution.Variations.Count + " variation(s): 0 to " + (resolution.Variations.Count - 1) + ".");
            return resolution.Variations[index];
        }

        internal static string CodecName(ushort format)
        {
            switch (format)
            {
                case WwiseVorbisConverter.FormatVorbis: return "Wwise Vorbis";
                case WwiseAdpcmReader.FormatAdpcm: return "Wwise ADPCM";
                case WwisePcmReader.FormatPcm: return "PCM";
                default: return "0x" + format.ToString("X4");
            }
        }

        private static object ListSoundBanks(McpCall call)
        {
            string filter = (call.Str("filter") ?? "").Trim();
            string bankName = call.Str("bank");

            //Which banks ship, looked up once off the UI thread
            Dictionary<string, string> onDisk = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string directory = SoundPreviewLibrary.SoundDirectory;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                foreach (string bnk in Directory.GetFiles(directory, "*.bnk", SearchOption.AllDirectories))
                    if (!onDisk.ContainsKey(Path.GetFileNameWithoutExtension(bnk))) onDisk[Path.GetFileNameWithoutExtension(bnk)] = Relative(bnk);

            return McpEditor.UI(() =>
            {
                LevelContent content = McpEditor.RequireLevel(forEditing: false);
                SoundBankData banks = content.Level.SoundBankData;
                SoundEventData events = content.Level.SoundEventData;
                if (banks == null || events == null) throw new McpError("The open level has no sound data.");

                Dictionary<uint, SoundBankData.SoundBank> byId = new Dictionary<uint, SoundBankData.SoundBank>();
                foreach (SoundBankData.SoundBank bank in banks.Entries)
                    if (!string.IsNullOrEmpty(bank.Name)) byId[Utilities.SoundHashedString(bank.Name)] = bank;
                Dictionary<string, List<string>> eventsByBank = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                Dictionary<string, SoundBankData.SoundBank> declared = new Dictionary<string, SoundBankData.SoundBank>(StringComparer.OrdinalIgnoreCase);
                foreach (SoundBankData.SoundBank bank in banks.Entries)
                    if (!string.IsNullOrEmpty(bank.Name)) { declared[bank.Name] = bank; eventsByBank[bank.Name] = new List<string>(); }
                foreach (SoundEventData.Soundbank bank in events.Entries)
                {
                    string name = byId.TryGetValue(bank.id, out SoundBankData.SoundBank named) ? named.Name : bank.id.ToString();
                    if (!eventsByBank.TryGetValue(name, out List<string> list)) eventsByBank[name] = list = new List<string>();
                    list.AddRange(bank.events.Select(o => o.name));
                }

                if (bankName != null)
                {
                    string key = eventsByBank.Keys.FirstOrDefault(o => Same(o, bankName.Trim()));
                    if (key == null)
                        throw new McpError("The game's sound data declares no bank '" + bankName + "'" + McpGlobalAssetChecks.DidYouMean(eventsByBank.Keys, bankName, " (list_sound_banks lists them)."));
                    List<string> names = eventsByBank[key].Distinct().OrderBy(o => o, StringComparer.OrdinalIgnoreCase).ToList();
                    JObject one = new JObject() { ["name"] = key };
                    if (declared.TryGetValue(key, out SoundBankData.SoundBank info)) one["localised"] = info.Localised;
                    one["file"] = onDisk.TryGetValue(key, out string file) ? file : null;
                    one["event_count"] = names.Count;
                    McpPaging.Page(call, names, one, "events", o => o, 200, 5000);
                    return one;
                }

                //What loads each bank in the open level: permanently, by a SoundLoadBank, or (unverified) the level's load-zone list
                List<string> permanent = McpSoundTools.PermanentBanks();
                List<McpSoundTools.BankLoader> loaders = McpSoundTools.BankLoaders(content.Level.Commands);
                List<string> zones = McpSoundTools.LoadZoneBanks(content.Level, out bool _);
                string Loads(string bank)
                {
                    if (permanent.Any(p => Same(p, bank))) return "permanent";
                    List<McpSoundTools.BankLoader> by = loaders.Where(l => Same(l.Bank, bank)).ToList();
                    if (by.Any(l => l.OnReset)) return "level start (SoundLoadBank)";
                    if (by.Count != 0) return "when triggered (SoundLoadBank)";
                    return zones.Any(z => Same(z, bank)) ? "load-zone list only" : null;
                }

                string[] words = Words(filter);
                List<string> listed = eventsByBank.Keys.Where(o => AllWords(words, o)).OrderBy(o => o, StringComparer.OrdinalIgnoreCase).ToList();
                if (listed.Count == 0 && filter.Length != 0) call.Note("No bank matches '" + filter + "'" + McpGlobalAssetChecks.DidYouMean(eventsByBank.Keys, filter, "."));
                JObject result = new JObject() { ["level"] = content.Level.Name };
                McpPaging.Page(call, listed, result, "banks", o =>
                {
                    JObject row = new JObject() { ["name"] = o, ["events"] = eventsByBank[o].Distinct().Count() };
                    if (declared.TryGetValue(o, out SoundBankData.SoundBank info) && info.Localised) row["localised"] = true;
                    row["file"] = onDisk.TryGetValue(o, out string file) ? file : null;
                    string loads = Loads(o);
                    if (loads != null) row["loaded_in_open_level"] = loads;
                    return row;
                }, 200, 5000);
                return result;
            });
        }

        private static object DescribeSoundEvent(McpCall call)
        {
            string eventName = call.Str("event", required: true).Trim();
            bool decode = call.Bool("decode");
            int limit = Limit(call, 50, 500);

            JArray banks = null;
            JObject loading = null;
            List<JObject> players = null;
            int playerCount = 0;
            SoundEventData.Soundbank.Event info = null;
            List<string> allEvents = null;
            McpEditor.UI(() =>
            {
                LevelContent content = Singleton.Editor?.CompositeBrowser?.Content;
                if (content?.Level == null || !content.IsLevelDataLoaded) return;
                List<string> declared = SoundEventMetadata.BanksFor(eventName);
                banks = new JArray(declared);
                loading = McpSoundTools.LoadingOf(content.Level.Commands, content.Level, declared);
                players = McpSoundTools.PlayedBy(content.Level.Commands, eventName, limit, out playerCount);
                info = SoundEventMetadata.InfoFor(eventName);
                if (declared.Count == 0) allEvents = SoundEventMetadata.AllEvents();
            });

            WwiseSoundLibrary library = SoundLibrary(call);
            WwiseEventResolution resolution = library.Resolve(eventName);
            JObject result = new JObject()
            {
                ["event"] = info?.name ?? eventName,
                ["outcome"] = resolution.Outcome.ToString(),
                ["plays_audio"] = resolution.HasAudio,
            };
            if (!resolution.HasAudio) result["explanation"] = resolution.Explanation;
            if (resolution.Outcome == WwiseEventOutcome.DialogueEvent) call.Note("list_dialogue_lines lists the lines it can say and their audio.");
            if (resolution.Actions.Count != 0) result["actions"] = new JArray(resolution.Actions);
            if (banks == null)
                call.Note("No level is open, so which banks declare this event and what loads them isn't known; load_level first to see them.");
            else
            {
                result["banks"] = banks;
                result["loading"] = loading;
                if (banks.Count == 0)
                    call.Note("The game's sound data declares no bank holding '" + eventName + "', so it won't play anywhere" + McpGlobalAssetChecks.DidYouMean(allEvents, eventName, " (list_enum_string_values type SOUND_EVENT lists the events)."));
            }

            //How it behaves: audible range, its stop partner and whether it loops (by name and partner: the banks' loop flags aren't read)
            if (info != null)
            {
                if (info.max_attenuation < 1024f) result["audible_within_m"] = Round(info.max_attenuation, 2);
                else result["positional"] = "probably not (1024, the value 2D, stop and setting events carry)";
                if (!string.IsNullOrEmpty(info.metadata?.Trim())) result["metadata"] = info.metadata.Trim();
            }
            string stop = McpSoundTools.StopEventFor(library, eventName);
            if (stop != null) result["stop_event"] = stop;
            bool loopName = eventName.IndexOf("loop", StringComparison.OrdinalIgnoreCase) >= 0;
            result["loops_likely"] = loopName || stop != null;
            if (loopName || stop != null)
                result["loop_hint"] = (loopName ? "its name says loop" : "it has a stop event") + ": a Sound playing it wants stop_event " + (stop ?? "(none found)") + " or a stop link, else it plays until its own end.";
            if (players != null)
            {
                result["played_by_count"] = playerCount;
                if (players.Count != 0) result["played_by"] = new JArray(players);
            }

            result["variation_count"] = resolution.Variations.Count;
            JArray variations = new JArray();
            using (McpEditorTools.Heartbeat(call, decode ? "Decoding the variations" : "Resolving the event"))
                for (int i = 0; i < resolution.Variations.Count && i < limit; i++)
                {
                    call.ThrowIfCancelled();
                    WwiseSoundVariation variation = resolution.Variations[i];
                    JObject row = new JObject() { ["index"] = i, ["source_id"] = variation.SourceId.ToString(), ["sound_id"] = variation.SoundId.ToString() };
                    if (!string.IsNullOrEmpty(variation.Path)) row["container_path"] = variation.Path;
                    if (!string.IsNullOrEmpty(variation.Bank)) row["bank"] = variation.Bank;
                    row["streamed"] = variation.IsStreamed;
                    if (variation.Media != null)
                    {
                        row["file"] = Relative(variation.Media.File);
                        row["bytes"] = variation.Media.Length;
                        if (!string.IsNullOrEmpty(variation.Media.Origin)) row["origin"] = variation.Media.Origin;
                        row["copies"] = library.AllCopies(variation.SourceId).Count;
                        if (decode)
                        {
                            try
                            {
                                byte[] wem = WwiseSoundLibrary.ReadMedia(variation.Media);
                                row["codec"] = CodecName(WemChunks.FormatTag(wem));
                                DecodedAudio audio = WwiseAudioDecoder.Decode(wem);
                                try
                                {
                                    audio.WaitForCompletion();
                                    row["duration"] = Round(audio.Duration.TotalSeconds, 3);
                                    row["sample_rate"] = audio.SampleRate;
                                    row["channels"] = audio.SourceChannels;
                                    if (audio.Truncated) row["truncated_at_s"] = DecodedAudio.MaxPreviewSeconds;
                                    if (audio.Error != null) row["decode_error"] = audio.Error;
                                }
                                finally { audio.Dispose(); }
                            }
                            catch (Exception e) { row["decode_error"] = e.Message; }
                        }
                    }
                    variations.Add(row);
                }
            result["variations"] = variations;
            if (resolution.Variations.Count > limit) call.Note(resolution.Variations.Count + " variations; the first " + limit + " are listed.");
            return result;
        }

        private static object ExportSound(McpCall call)
        {
            string path = AbsolutePath(call, "path");
            if (!Same(Path.GetExtension(path), ".wav")) throw new McpError("'path' has to end .wav.");
            if (File.Exists(path) && !call.Bool("overwrite"))
                throw new McpError(path + " already exists. Pass overwrite:true to replace it, or pick another path.");
            WwiseSoundLibrary library = SoundLibrary(call);
            WwiseSoundVariation variation = PickVariation(call, library, out string eventName);
            if (variation.Media == null) throw new McpError("That variation has no audio of its own.");

            JObject result = new JObject() { ["path"] = path, ["source_id"] = variation.SourceId.ToString() };
            if (eventName != null) result["event"] = eventName;
            using (McpEditorTools.Heartbeat(call, "Decoding the sound"))
            {
                byte[] wem = WwiseSoundLibrary.ReadMedia(variation.Media);
                DecodedAudio audio;
                try { audio = WwiseAudioDecoder.Decode(wem); }
                catch (Exception e) { throw new McpError("That sound can't be decoded: " + e.Message); }
                try
                {
                    byte[] wave = audio.ToWave();
                    string folder = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                    File.WriteAllBytes(path, wave);
                    result["bytes"] = wave.Length;
                    result["duration"] = Round(audio.Duration.TotalSeconds, 3);
                    result["sample_rate"] = audio.SampleRate;
                    result["channels"] = audio.Channels;
                    if (audio.SourceChannels != audio.Channels) call.Note("The original has " + audio.SourceChannels + " channels; the .wav is a stereo downmix.");
                    if (audio.Truncated) call.Note("The sound is longer than " + DecodedAudio.MaxPreviewSeconds + " s, so the .wav holds only the first " + DecodedAudio.MaxPreviewSeconds + " s.");
                    if (audio.Error != null) call.Note("Decoding stopped early (" + audio.Error + "), so the .wav holds only what decoded.");
                }
                finally { audio.Dispose(); }
            }
            return result;
        }

        private static object ReplaceSound(McpCall call)
        {
            string wav = AbsolutePath(call, "wav_path");
            if (!File.Exists(wav)) throw new McpError("There is no file at " + wav + ".");
            double quality = call.Num("quality", 0.6);
            if (quality < 0 || quality > 1) throw new McpError("'quality' is from 0 to 1.");
            SoundImport.Options options = new SoundImport.Options()
            {
                Quality = (float)quality,
                GainDb = call.Num("gain_db", 0),
                NormalizeDb = call.Has("normalize_db") ? call.Num("normalize_db", 0) : (double?)null,
                TrimStart = call.Num("trim_start", 0),
                TrimEnd = call.Num("trim_end", 0),
            };
            if (options.GainDb < -60 || options.GainDb > 40) throw new McpError("'gain_db' is from -60 to 40.");
            if (options.NormalizeDb != null && (options.NormalizeDb > 0 || options.NormalizeDb < -60)) throw new McpError("'normalize_db' is a peak level from -60 to 0 dBFS, e.g. -1.");
            if (options.TrimStart < 0 || options.TrimEnd < 0) throw new McpError("A trim can't be negative.");
            bool dryRun = call.Bool("dry_run");
            if (call.Has("variations") && (call.Has("variation") || call.Has("source_id")))
                throw new McpError("Give variations, or variation / source_id, not both.");

            WwiseSoundLibrary library = SoundLibrary(call);
            string eventName = call.Str("event")?.Trim();
            WwiseEventResolution resolution = eventName == null ? null : library.Resolve(eventName);
            List<WwiseSoundVariation> targets = new List<WwiseSoundVariation>();
            if (call.Has("variations"))
            {
                if (eventName == null) throw new McpError("'variations' needs 'event'.");
                if (resolution.Variations.Count == 0) throw new McpError("'" + eventName + "' plays no audio: " + resolution.Explanation);
                JToken token = call.Token("variations");
                IEnumerable<int> indexes;
                if (token.Type == JTokenType.String && Same(((string)token).Trim(), "all")) indexes = Enumerable.Range(0, resolution.Variations.Count);
                else if (token is JArray list && list.All(o => o.Type == JTokenType.Integer)) indexes = list.Select(o => (int)o);
                else throw new McpError("'variations' is a list of variation indexes, e.g. [0, 2], or 'all'.");
                foreach (int index in indexes.Distinct())
                {
                    if (index < 0 || index >= resolution.Variations.Count)
                        throw new McpError("'" + eventName + "' has " + resolution.Variations.Count + " variation(s): 0 to " + (resolution.Variations.Count - 1) + ".");
                    targets.Add(resolution.Variations[index]);
                }
            }
            else
                targets.Add(PickVariation(call, library, out eventName));
            //The same audio can be two variations' (or be shared): each distinct source is written once
            targets = targets.Where(o => o.Media != null).GroupBy(o => o.SourceId).Select(o => o.First()).ToList();
            if (targets.Count == 0) throw new McpError("That variation has no audio of its own to replace.");

            JArray rows = new JArray();
            List<Tuple<WwiseSoundVariation, SoundImport.Reading>> readings = new List<Tuple<WwiseSoundVariation, SoundImport.Reading>>();
            foreach (WwiseSoundVariation variation in targets)
            {
                //The same audio is often shipped in several banks at once: all of them change, or the old sound comes back wherever another copy loads
                IList<WwiseMediaLocation> copies = library.AllCopies(variation.SourceId);
                SoundImport.Reading reading;
                using (McpEditorTools.Heartbeat(call, "Encoding " + Path.GetFileName(wav)))
                    reading = SoundImport.Read(wav, variation.Media, copies, options);
                if (!reading.Ok)
                    throw new McpError("That audio can't be used" + (targets.Count > 1 ? " for source " + variation.SourceId : "") + ": " + reading.Problem);
                readings.Add(Tuple.Create(variation, reading));
                JObject row = new JObject() { ["source_id"] = variation.SourceId.ToString() };
                int index = resolution == null ? -1 : resolution.Variations.IndexOf(variation);
                if (index >= 0) row["variation"] = index;
                if (!string.IsNullOrEmpty(variation.Path)) row["container_path"] = variation.Path;
                row["codec"] = reading.Codec;
                if (reading.Codec == "Wwise Vorbis") row["quality"] = Round(reading.Quality, 2);
                row["channels"] = reading.Channels;
                row["sample_rate"] = reading.SampleRate;
                row["duration"] = Round(reading.Duration, 3);
                row["original_bytes"] = reading.OriginalBytes;
                row["new_bytes"] = reading.NewBytes;
                row["container"] = reading.Plan?.Kind;
                row["copies"] = new JArray(reading.Copies.Select(o => Relative(o.File)).Distinct(StringComparer.OrdinalIgnoreCase));
                if (reading.Notes.Count != 0) row["notes"] = new JArray(reading.Notes);
                rows.Add(row);
            }

            //One variation keeps the flat result it always had; several are listed
            JObject result = targets.Count == 1 ? (JObject)rows[0].DeepClone() : new JObject() { ["replacing"] = rows };
            if (eventName != null)
            {
                result.AddFirst(new JProperty("event", eventName));
                if (resolution != null)
                {
                    result["variation_count"] = resolution.Variations.Count;
                    List<WwiseSoundVariation> untouched = resolution.Variations.Where(o => !targets.Any(t => t.SourceId == o.SourceId)).ToList();
                    if (untouched.Count != 0 && !call.Has("variation") && !call.Has("variations") && !call.Has("source_id"))
                    {
                        result["other_variations"] = new JArray(untouched.Take(20).Select(o => new JObject() { ["variation"] = resolution.Variations.IndexOf(o), ["container_path"] = o.Path, ["source_id"] = o.SourceId.ToString() }));
                        call.Note("'" + eventName + "' has " + resolution.Variations.Count + " variations and only variation 0 is replaced: the others still play (randomly, or per switch value such as a surface - see other_variations' container_path). variations:'all' replaces every one.");
                    }
                }
            }
            if (dryRun)
            {
                result["dry_run"] = true;
                return result;
            }

            CloseOrRefuse(call, "its sound files can't be written while it holds them open");
            List<string> problems = new List<string>();
            int replaced = 0;
            //Soundbanks written so far: each write lays the bank's audio out again, moving everything after what it replaced
            HashSet<string> rewritten = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Tuple<WwiseSoundVariation, SoundImport.Reading> pair in readings)
            {
                string prefix = readings.Count > 1 ? "source " + pair.Item1.SourceId + ": " : "";
                //Pristine copies of every container this write touches, while they are still pristine
                Modding.ModServices.CaptureBeforeWrite(pair.Item1.Media.File);
                foreach (WwiseMediaLocation copy in pair.Item2.Copies)
                    Modding.ModServices.CaptureBeforeWrite(copy.File);
                //Where this audio is now in a bank an earlier variation was written to (its offset was read before that write)
                string lost = Relocate(pair.Item1, pair.Item2, rewritten);
                if (lost != null)
                {
                    problems.Add(prefix + lost);
                    continue;
                }
                string problem;
                using (McpEditorTools.Heartbeat(call, "Writing the sound files"))
                    if (SoundImport.Apply(pair.Item2, pair.Item1.Media, out problem)) replaced += pair.Item2.Copies.Count;
                    else problems.Add(prefix + (problem ?? "").Replace(Environment.NewLine, " "));
                foreach (WwiseMediaLocation copy in pair.Item2.Copies.Concat(new[] { pair.Item1.Media }))
                    if (copy?.File != null && string.Equals(Path.GetExtension(copy.File), ".bnk", StringComparison.OrdinalIgnoreCase))
                        rewritten.Add(Path.GetFullPath(copy.File));
            }
            //A rewritten bank moves the other audio in it, so the index is rebuilt on the next ask
            SoundPreviewLibrary.Invalidate();
            if (problems.Count != 0)
                throw new McpError("The sound was not fully replaced: " + string.Join(" ", problems));
            result["replaced"] = replaced;
            return result;
        }

        /// <summary>
        /// Point a reading's copies that sit in soundbanks this call has already rewritten at where their audio is now, read
        /// back from the bank: a rewrite lays a bank's audio out again, and a writer working from the old offset would miss it
        /// or overwrite whatever has moved there. Null, or why the audio cannot be found again (then nothing of it is written).
        /// </summary>
        private static string Relocate(WwiseSoundVariation variation, SoundImport.Reading reading, HashSet<string> rewritten)
        {
            if (rewritten.Count == 0) return null;
            foreach (WwiseMediaLocation copy in reading.Copies.Concat(new[] { variation.Media }).Distinct())
            {
                if (copy?.File == null || !rewritten.Contains(Path.GetFullPath(copy.File))) continue;
                WwiseMediaLocation now;
                try
                {
                    if (!WwiseSoundBank.Load(copy.File).EmbeddedMedia.TryGetValue(variation.SourceId, out now)) now = null;
                }
                catch (Exception e)
                {
                    return Path.GetFileName(copy.File) + " could not be read again after an earlier variation was written to it (" + e.Message + "); this one was not written.";
                }
                if (now == null)
                    return "its audio is no longer in " + Path.GetFileName(copy.File) + " after an earlier variation was written to it; this one was not written.";
                copy.Offset = now.Offset;
                copy.Length = now.Length;
            }
            return null;
        }
        #endregion
    }

    /// <summary>
    /// Checks and name lookups the script tools can use on the global data this family's tools browse: whether an
    /// ANIMATION / ANIMATION_SET / SOUND_EVENT value exists, whether an entity's set and clip go together, which sets a
    /// character can play, and near-match suggestions for misspelt names. All read live data. Call on the UI thread.
    /// </summary>
    internal static class McpGlobalAssetChecks
    {
        /// <summary>
        /// A warning when an ANIMATION, ANIMATION_SET or SOUND_EVENT value names nothing the game has, or null
        /// when it is known or the type is none of those. Reads ANIMATION.PAK as it is now (clips imported this
        /// session included) and the open level's sound data.
        /// </summary>
        public static string UnknownValueWarning(EnumStringType type, string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            Anim animations = Singleton.Animations;
            bool live = animations != null && animations.Loaded;
            switch (type)
            {
                case EnumStringType.ANIMATION_SET:
                    if (live ? animations.GetSet(value) != null : (Singleton.AllAnimations.Count == 0 || Singleton.AllAnimations.Keys.Any(o => string.Equals(o, value, StringComparison.OrdinalIgnoreCase)))) return null;
                    return "No animation set is called '" + value + "'" + DidYouMean(live ? animations.Sets.Select(o => o.Name) : Singleton.AllAnimations.Keys, value, " (list_animation_sets lists them).");
                case EnumStringType.ANIMATION:
                    if (live ? animations.Sets.Any(s => s.Contexts.Any(c => c.Clips.Any(x => string.Equals(x.Name, value, StringComparison.OrdinalIgnoreCase))))
                             : (Singleton.AllAnimations.Count == 0 || Singleton.AllAnimations.Values.Any(o => o.Contains(value) || o.Any(n => string.Equals(n, value, StringComparison.OrdinalIgnoreCase))))) return null;
                    return "No animation set has an animation called '" + value + "' (list_animations with filter searches them).";
                case EnumStringType.SOUND_EVENT:
                    if (Singleton.Editor?.CompositeBrowser?.Content?.Level?.SoundEventData == null || SoundEventMetadata.BanksFor(value).Count != 0) return null;
                    return "The game's sound data (the same in every level) declares no sound event '" + value + "', so it won't play" + DidYouMean(SoundEventMetadata.AllEvents(), value, " (list_enum_string_values type SOUND_EVENT lists them).");
                default:
                    return null;
            }
        }

        #region Names
        /// <summary>
        /// The candidates closest to a misspelt or partial name, best first: containing it (or contained in it),
        /// then within a few typos of it or of a part of it ('idel' finds jb_kn_idlebase01, 'ANDRIOD' finds ANDROID).
        /// Case, spaces and punctuation are ignored.
        /// </summary>
        public static List<string> Suggest(IEnumerable<string> candidates, string input, int max = 8)
        {
            string wanted = Normalise(input);
            if (wanted.Length == 0 || candidates == null) return new List<string>();
            int allowed = wanted.Length <= 4 ? 1 : wanted.Length <= 8 ? 2 : 3;
            List<Tuple<string, int, int>> scored = new List<Tuple<string, int, int>>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate) || !seen.Add(candidate)) continue;
                string have = Normalise(candidate);
                if (have.Length == 0) continue;
                int score;
                if (have.Contains(wanted) || (have.Length >= 3 && wanted.Contains(have))) score = 0;
                else
                {
                    int whole = Distance(wanted, have, false);
                    int part = wanted.Length >= 3 ? Distance(wanted, have, true) : int.MaxValue;
                    score = Math.Min(whole, part);
                    if (score > allowed) continue;
                }
                scored.Add(Tuple.Create(candidate, score, Math.Abs(have.Length - wanted.Length)));
            }
            return scored.OrderBy(o => o.Item2).ThenBy(o => o.Item3).ThenBy(o => o.Item1, StringComparer.OrdinalIgnoreCase).Take(max).Select(o => o.Item1).ToList();
        }

        /// <summary>" Did you mean: a, b?" for a failed lookup, or <paramref name="otherwise"/> when nothing is close.</summary>
        public static string DidYouMean(IEnumerable<string> candidates, string input, string otherwise = ".")
        {
            List<string> near = Suggest(candidates, input);
            return near.Count == 0 ? otherwise : ". Did you mean: " + string.Join(", ", near) + "?";
        }

        /* Lowercase letters and digits, every run of anything else one space: the words stay apart, so 'idel' is never
         * found across the join in side_lms */
        private static string Normalise(string text)
        {
            StringBuilder kept = new StringBuilder();
            foreach (char c in text ?? "")
            {
                if (char.IsLetterOrDigit(c)) kept.Append(char.ToLowerInvariant(c));
                else if (kept.Length != 0 && kept[kept.Length - 1] != ' ') kept.Append(' ');
            }
            return kept.ToString().Trim();
        }

        /* Optimal string alignment distance (a swapped pair counts once). With anywhere, against the closest
         * stretch of text rather than all of it (free start and end): how far the pattern is from appearing in it. */
        private static int Distance(string pattern, string text, bool anywhere)
        {
            int m = pattern.Length, n = text.Length;
            int[,] d = new int[m + 1, n + 1];
            for (int i = 0; i <= m; i++) d[i, 0] = i;
            for (int j = 0; j <= n; j++) d[0, j] = anywhere ? 0 : j;
            for (int i = 1; i <= m; i++)
                for (int j = 1; j <= n; j++)
                {
                    int cost = pattern[i - 1] == text[j - 1] ? 0 : 1;
                    int best = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                    if (i > 1 && j > 1 && pattern[i - 1] == text[j - 2] && pattern[i - 2] == text[j - 1])
                        best = Math.Min(best, d[i - 2, j - 2] + 1);
                    d[i, j] = best;
                }
            if (!anywhere) return d[m, n];
            int least = int.MaxValue;
            for (int j = 0; j <= n; j++) least = Math.Min(least, d[m, j]);
            return least;
        }
        #endregion

        #region Animation sets and clips
        /// <summary>An animation set by name (any case), or null.</summary>
        public static Anim.AnimationSet FindSet(string name)
        {
            Anim animations = Singleton.Animations;
            if (animations == null || !animations.Loaded || string.IsNullOrWhiteSpace(name)) return null;
            return animations.GetSet(name.Trim());
        }

        /// <summary>
        /// A clip of a set by the name an ANIMATION parameter gives it (any case; its stored path also works), with the
        /// context holding it. Contexts other than the unnamed one only play while the character is in that state
        /// (WEAPON_HANDGUN, CROUCHED...). Null when the set has no such clip.
        /// </summary>
        public static Anim.ClipReference FindClip(Anim.AnimationSet set, string name)
        {
            if (set == null || string.IsNullOrWhiteSpace(name)) return null;
            name = name.Trim();
            List<Anim.ClipReference> clips = set.Contexts.SelectMany(o => o.Clips).Where(o => o != null).ToList();
            //The unnamed context first: it is the one that always plays
            Anim.ClipReference found = clips.Where(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase)).OrderBy(o => (o.Context?.Name ?? "").Trim().Length == 0 ? 0 : 1).FirstOrDefault()
                ?? clips.FirstOrDefault(o => string.Equals(o.Path, name, StringComparison.OrdinalIgnoreCase));
            if (found != null) return found;
            //A clip whose name the string table doesn't spell reads back as its hash: the game matches by that hash
            uint hash = Utilities.AnimationHashedString(name);
            return clips.FirstOrDefault(o => uint.TryParse(o.Name, out uint id) && id == hash);
        }

        /// <summary>The values a parameter holds as text, when it holds a plain value (not a link), else null.</summary>
        public static string TextOf(Entity entity, string parameter)
        {
            ParameterData content = entity?.GetParameter(parameter)?.content;
            if (content is cString text) return text.value;
            return null;
        }

        /// <summary>
        /// What is wrong with the animation an entity plays, in sentences an assistant can act on, or an empty list:
        /// for CMD_PlayAnimation, CHR_PlaySecondaryAnimation and PlayEnvironmentAnimation, whether AnimationSet names a
        /// set, whether the set is the right kind (a character set for the first two, an environment set for the
        /// third), whether Animation names one of its clips (and in which context), with near names; for a
        /// PlayEnvironmentAnimation whose geometry link reaches an animated prop, whether the set plays on that prop's
        /// rig; for a Character, whether anim_set names a character set. Values fed by a link are not checked.
        /// </summary>
        public static List<string> AnimationProblems(Commands commands, Composite composite, Entity entity)
        {
            List<string> problems = new List<string>();
            if (!(entity is FunctionEntity function) || !function.function.IsFunctionType) return problems;
            FunctionType type = function.function.AsFunctionType;
            Anim animations = Singleton.Animations;
            if (animations == null || !animations.Loaded) return problems;

            if (type == FunctionType.Character)
            {
                string animSet = TextOf(entity, "anim_set");
                if (string.IsNullOrEmpty(animSet)) return problems;
                Anim.AnimationSet set = animations.GetSet(animSet);
                if (set == null) problems.Add("anim_set '" + animSet + "' is not an animation set" + DidYouMean(animations.Sets.Where(o => o.Kind != Anim.AnimationKind.Environment).Select(o => o.Name), animSet, " (list_animation_sets kind character lists them)."));
                else if (set.Kind == Anim.AnimationKind.Environment) problems.Add("anim_set '" + set.Name + "' is an environment (prop) set; a Character needs a character set (list_animation_sets kind character).");
                return problems;
            }
            if (type != FunctionType.CMD_PlayAnimation && type != FunctionType.CHR_PlaySecondaryAnimation && type != FunctionType.PlayEnvironmentAnimation)
                return problems;

            bool environment = type == FunctionType.PlayEnvironmentAnimation;
            string setName = TextOf(entity, "AnimationSet");
            string clipName = TextOf(entity, "Animation");
            if (string.IsNullOrEmpty(setName))
            {
                if (!string.IsNullOrEmpty(clipName)) problems.Add("Animation is '" + clipName + "' but AnimationSet is empty: name the set that holds it.");
                return problems;
            }
            Anim.AnimationSet found = animations.GetSet(setName);
            if (found == null)
            {
                problems.Add("AnimationSet '" + setName + "' is not an animation set" + DidYouMean(animations.Sets.Select(o => o.Name), setName, " (list_animation_sets lists them)."));
                return problems;
            }
            /* The kind is only trusted where it is certain: a set filed with the environment rigs, or one whose rig is a body
             * (hips and a head). Weapon sets (PISTOL, FLAMETHROWER) are neither, and props play them */
            if (environment && found.Kind == Anim.AnimationKind.Character && IsBody(animations, found))
                problems.Add("AnimationSet '" + found.Name + "' is a character's set; PlayEnvironmentAnimation plays a prop's set - describe_animated_prop names the sets a prop can play.");
            else if (!environment && found.Kind == Anim.AnimationKind.Environment)
                problems.Add("AnimationSet '" + found.Name + "' is an environment (prop) set; " + type + " plays a character's set (use PlayEnvironmentAnimation for props).");

            if (!string.IsNullOrEmpty(clipName))
            {
                Anim.ClipReference clip = FindClip(found, clipName);
                if (clip == null)
                {
                    List<string> holders = animations.Sets.Where(s => !ReferenceEquals(s, found) && FindClip(s, clipName) != null).Select(s => s.Name).ToList();
                    problems.Add(found.Name + " has no animation '" + clipName + "', so nothing plays"
                        + (holders.Count != 0 ? " (it is in " + string.Join(", ", holders.Take(6)) + (holders.Count > 6 ? " and " + (holders.Count - 6) + " more" : "") + ")."
                            : DidYouMean(found.Contexts.SelectMany(o => o.Clips).Select(o => o.Name), clipName, " (list_animations set " + found.Name + " lists its clips).")));
                }
                else if ((clip.Context?.Name ?? "").Trim().Length != 0)
                    problems.Add("'" + clip.Name + "' is only in " + found.Name + "'s " + clip.Context.Name + " context, so it plays only while the character is in that state.");
                else if (!clip.Playable)
                    problems.Add("'" + clip.Name + "' is listed in " + found.Name + " but its animation data is missing from ANIMATION.PAK.");
            }

            //A prop's set has to drive the prop's own rig: the rig of the entry its geometry link reaches
            if (environment && composite != null)
            {
                string rig = GeometryRig(commands, composite, entity);
                if (rig != null && !string.IsNullOrEmpty(found.Skeleton) && !string.Equals(rig, found.Skeleton, StringComparison.OrdinalIgnoreCase))
                    problems.Add("Its geometry is animated on the rig '" + rig + "', but " + found.Name + " plays on '" + found.Skeleton + "': pick a set whose rig is " + rig + " (describe_animated_prop lists them).");
            }
            return problems;
        }

        /// <summary>Whether a set's rig is a body - it has hips and a head - rather than a prop or weapon rig.</summary>
        private static bool IsBody(Anim animations, Anim.AnimationSet set)
        {
            Anim.SkeletonAsset asset = animations.GetSkeleton(set.Skeleton);
            List<Skeleton.Bone> bones = asset?.Bones;
            if (bones == null) return false; //not in the PAK (ANDROID's): nothing to tell by, so nothing is claimed
            bool Has(string name) => bones.Any(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase) || (o.Name ?? "").EndsWith(":" + name, StringComparison.OrdinalIgnoreCase));
            return Has("HIPS") && Has("HEAD");
        }

        /// <summary>The rig of the environment-animation entry a PlayEnvironmentAnimation's geometry link reaches, or null.</summary>
        public static string GeometryRig(Commands commands, Composite composite, Entity entity)
        {
            ShortGuid geometry = ShortGuidUtils.Generate("geometry");
            foreach (EntityConnector link in entity.childLinks)
            {
                if (link.thisParamID != geometry) continue;
                Entity target = composite.GetEntityByID(link.linkedEntityID);
                Composite holder = composite;
                if (target is FunctionEntity instance && !instance.function.IsFunctionType)
                {
                    holder = commands.GetComposite(instance.function);
                    target = null;
                }
                if (holder == null) continue;
                foreach (FunctionEntity candidate in holder.functions)
                {
                    if (target != null && candidate != target && !(candidate.function.IsFunctionType && candidate.function.AsFunctionType == FunctionType.EnvironmentModelReference)) continue;
                    EnvironmentAnimations.EnvironmentAnimation entry = AnimatedModelOf(candidate);
                    if (entry != null && !string.IsNullOrEmpty(entry.SkeletonName)) return entry.SkeletonName;
                }
            }
            return null;
        }

        /// <summary>The environment-animation entry an entity's ANIMATED_MODEL resource uses, or null.</summary>
        public static EnvironmentAnimations.EnvironmentAnimation AnimatedModelOf(Entity entity)
        {
            if (!(entity is FunctionEntity function)) return null;
            List<ResourceReference> references = (function.GetParameter(ShortGuids.resource)?.content as cResource)?.value ?? function.resources;
            return references?.FirstOrDefault(o => o != null && o.resource_type == ResourceType.ANIMATED_MODEL && o.AnimatedModel != null)?.AnimatedModel;
        }

        /// <summary>
        /// The animation sets whose clips play on a rig: the rig's own sets and the sets of rigs the game's data
        /// retargets onto it (SKELE\MAPS). Each comes with whether it is retargeted.
        /// </summary>
        public static List<Tuple<Anim.AnimationSet, bool>> SetsPlayableOn(string rig)
        {
            List<Tuple<Anim.AnimationSet, bool>> sets = new List<Tuple<Anim.AnimationSet, bool>>();
            Anim animations = Singleton.Animations;
            if (animations == null || !animations.Loaded || string.IsNullOrEmpty(rig)) return sets;
            Dictionary<string, bool> reach = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (Anim.AnimationSet set in animations.Sets)
            {
                if (string.IsNullOrEmpty(set.Skeleton)) continue;
                if (string.Equals(set.Skeleton, rig, StringComparison.OrdinalIgnoreCase)) { sets.Add(Tuple.Create(set, false)); continue; }
                if (!reach.TryGetValue(set.Skeleton, out bool retargets))
                    reach[set.Skeleton] = retargets = Retargeter.Between(animations, set.Skeleton, rig) != null;
                if (retargets) sets.Add(Tuple.Create(set, true));
            }
            return sets;
        }

        /// <summary>
        /// A note for a CameraPlayAnimation's data_file: these are baked cutscene camera clips that OpenCAGE can't
        /// list or author, so a value no CameraPlayAnimation of the open level already uses is flagged. Null when
        /// the open level plays it already or the value is empty.
        /// </summary>
        public static string CameraClipNote(Commands commands, string dataFile)
        {
            if (string.IsNullOrWhiteSpace(dataFile) || commands == null) return null;
            bool used = commands.Entries.Any(c => c.functions.Any(f => f.function.IsFunctionType && f.function.AsFunctionType == FunctionType.CameraPlayAnimation
                && string.Equals(TextOf(f, "data_file"), dataFile, StringComparison.OrdinalIgnoreCase)));
            if (used) return null;
            return "No CameraPlayAnimation in the open level plays '" + dataFile + "'. Its clips are baked cutscene camera moves the level must ship; OpenCAGE can't list or make them (list_camera_clips lists the ones this level uses). " +
                "For a camera move of your own, key a CameraResource's position with a CAGEAnimation instead: create_camera_animation builds one round a room and wires it to play.";
        }
        #endregion
    }
}
