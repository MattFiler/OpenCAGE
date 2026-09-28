using AlienPAK;
using CATHODE;
using CATHODE.Animations;
using CATHODE.Scripting;
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

namespace OpenCAGE.MCP
{
    /// <summary>
    /// The game's global animation and audio data: ANIMATION.PAK (sets, clips, rigs, blend sets and
    /// animation trees), the AI behaviour trees and the Wwise sounds. None of it belongs to a level, so
    /// reading it needs no level open, and every change is written to the game's files straight away (each
    /// writing tool has a dry_run that says what would change first). None of it is on the undo history.
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
                Description = "The animation sets in the game's ANIMATION.PAK (no level needed): name, character or environment, the rig it plays on, its contexts and clip count. " +
                    "An ANIMATION_SET parameter takes a set's name; list_animations lists its clips. filter matches the set or rig name, or else the sets holding a clip that matches.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Text the set or rig name contains (falls back to the sets holding a matching clip)."),
                    McpSchema.String("kind", "character, environment or all (default).", options: new[] { "character", "environment", "all" }),
                    McpSchema.Integer("limit", "At most this many (default 100).")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListAnimationSets,
            };

            yield return new McpTool()
            {
                Name = "list_animations",
                Title = "List animations",
                Description = "Animation clips in ANIMATION.PAK: the name an ANIMATION parameter takes, its set and context, stored path, authoring rig, frames, length, additive and event count. " +
                    "Give set for one set's clips (context narrows it), or filter to search every set. describe_animation shows one clip in full.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("set", "An animation set's name (list_animation_sets)."),
                    McpSchema.String("context", "Only this context of the set ('default' for the unnamed one). Needs set."),
                    McpSchema.String("filter", "Words the clip's name or stored path must all contain."),
                    McpSchema.String("rig", "Only clips authored on this rig (list_skeletons)."),
                    McpSchema.Integer("limit", "At most this many (default 100, most 1000)."),
                    McpSchema.Integer("offset", "Skip this many matches first (for paging).")),
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
                    "With set, ranks rigs by how many of the set's clips were authored on them (export_animations' default); with model, by fit to that open-level model's skin weights.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Text the rig name contains."),
                    McpSchema.String("set", "Rank rigs for exporting this animation set's clips."),
                    McpSchema.String("model", "Rank rigs by fit to this model of the open level (list_models)."),
                    McpSchema.Integer("limit", "At most this many (default 100).")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListSkeletons,
            };

            yield return new McpTool()
            {
                Name = "export_animations",
                Title = "Export animations",
                Description = "Write clips to an .fbx, .glb, .gltf or .dae file against a rig (default: the one most were authored on). mode hold/travel are for viewing, as_stored for editing " +
                    "and re-importing with import_animation. model adds an open-level mesh bound to the rig (hold or travel only). Unreadable clips are skipped. Writes that file, plus a <name>.bin " +
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
                Description = "Add an animation from an FBX/glTF/DAE file to an animation set in ANIMATION.PAK, converting it from a foreign rig (e.g. the UE mannequin) when no bone names match. " +
                    "Written to the game at once, for every level; not undoable and not removable here. Use dry_run first to see frames, matched bones, warnings and whether it builds.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("path", "Absolute path of the model file holding the animation.", required: true),
                    McpSchema.String("set", "The animation set to add it to (list_animation_sets).", required: true),
                    McpSchema.String("name", "The name the set plays it by, which ANIMATION parameters take (default: the file name)."),
                    McpSchema.String("stored_path", "Where it is stored (default ANIMATION\\OPENCAGE\\<SET>\\<FILE>). Must be unused."),
                    McpSchema.String("rig", "The rig to build it against (default: the rig most of the set's clips use; list_skeletons)."),
                    McpSchema.String("root", "auto (default): keep root motion only if the file moves the root; engine: leave the root to the game; authored: keep it.", options: new[] { "auto", "engine", "authored" }),
                    McpSchema.Number("frame_rate", "Frames per second (default: worked out from the file)."),
                    McpSchema.Boolean("additive", "Layer it over whatever else is playing instead of replacing it."),
                    McpSchema.Integer("clip_index", "Which animation in the file, from 0 (default 0)."),
                    McpSchema.Boolean("dry_run", "Read the file and build the clip, reporting what would be imported, changing nothing.")),
                Run = ImportAnimation,
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
                    McpSchema.Integer("limit", "At most this many (default 100).")),
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
                Description = "Change a blend set's authored half and write ANIMATION.PAK at once (every level; not undoable): a clip's name, length or mirroring, which clip a blend point plays and its speed, " +
                    "and which characters or contexts can use the set. Blend-point positions are baked and cannot change. dry_run reports the changes without writing.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("name", "The blend set's key or unique name.", required: true),
                    McpSchema.Array("clips", "Clip edits: {index, name?, duration?, mirrored?}.", blendClip),
                    McpSchema.Array("points", "Blend point edits: {index, clip? (index or name), speed?}.", blendPoint),
                    McpSchema.Array("add_users", "Give the set to these: {character, context?}.", blendUser),
                    McpSchema.Array("remove_users", "Take the set away from these: {character, context?}.", blendUser),
                    McpSchema.Boolean("dry_run", "Check and report the changes without making them.")),
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
                    McpSchema.Integer("limit", "At most this many (default 200).")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListAnimTrees,
            };

            yield return new McpTool()
            {
                Name = "get_anim_tree",
                Title = "Get animation tree",
                Description = "One animation tree as a node graph: the tree's own settings, then each node's type, fields (with their current values) and links (field -> node), " +
                    "and any flow nodes that nothing parents, which are not saved. The field names and links are what edit_anim_tree takes.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("set", "The tree set (list_anim_trees).", required: true),
                    McpSchema.String("tree", "The tree's name.", required: true),
                    McpSchema.String("filter", "Only nodes whose name contains all these words."),
                    McpSchema.Boolean("fields", "Include each node's field values (default true)."),
                    McpSchema.Integer("limit", "At most this many nodes (default 150)."),
                    McpSchema.Integer("offset", "Skip this many nodes first (for paging).")),
                ReadOnly = true,
                Idempotent = true,
                Run = GetAnimTree,
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
                },
                ["required"] = new JArray("op"),
            };
            yield return new McpTool()
            {
                Name = "edit_anim_tree",
                Title = "Edit animation tree",
                Description = "Edit an animation tree and write ANIMATION.PAK at once (every level; not undoable). ops run in order, all or none: set a field, rename, add_node, remove_node, " +
                    "link or unlink a node field (flow links also parent the node, so it is saved). Refuses while the game runs unless close_game. dry_run checks without writing.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("set", "The tree set.", required: true),
                    McpSchema.String("tree", "The tree's name.", required: true),
                    McpSchema.Array("ops", "e.g. {op:'set',node:'Walk',field:'Looping',value:true}, {op:'link',node:'Sel',field:'States[0].Node',to:'Walk'}.", treeOp, required: true),
                    McpSchema.Boolean("dry_run", "Check the ops against a copy and report, writing nothing."),
                    McpSchema.Boolean("close_game", "Close a running game first, as the editor's own Save does.")),
                Destructive = true,
                Run = EditAnimTree,
            };
            #endregion

            #region Behaviour trees
            yield return new McpTool()
            {
                Name = "get_behaviour_tree",
                Title = "Get behaviour tree",
                Description = "The AI behaviour trees in DATA/BINARY_BEHAVIOR/_DIRECTORY_CONTENTS.BML (what a character's Behavior_Tree attribute names). Without name, lists them; with name, " +
                    "returns its XML (Brainiac Node/Connector elements), the trees it references and those referencing it. xpath returns only the matching nodes.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("name", "A behaviour tree, e.g. NPC_cover."),
                    McpSchema.String("filter", "Without name: text the tree names contain."),
                    McpSchema.String("xpath", "With name: only these nodes, by an XPath relative to the tree (e.g. .//Node[@Class='LegendPlugin.Nodes.ActionSuccess'])."),
                    McpSchema.Integer("max_chars", "Cut the XML at this many characters (default 60000)."),
                    McpSchema.Integer("limit", "At most this many trees or matches (default 200).")),
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
                    "xml replaces the whole tree; create adds a new tree. Refuses while the game or the Behaviour Tree Editor runs unless close_game. dry_run checks without writing.",
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
                Description = "The soundbanks the open level's sound data declares, with how many sound events each holds and whether its .bnk ships. Give bank to list its events. " +
                    "An event plays only while a bank declaring it is loaded. list_enum_string_values (type SOUND_EVENT) lists all events; describe_sound_event shows one.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("filter", "Text the bank name contains."),
                    McpSchema.String("bank", "One bank: list the sound events it declares."),
                    McpSchema.Integer("limit", "At most this many (default 200).")),
                ReadOnly = true,
                Idempotent = true,
                Run = ListSoundBanks,
            };

            yield return new McpTool()
            {
                Name = "describe_sound_event",
                Title = "Describe sound event",
                Description = "What a sound event (a SOUND_EVENT value) plays: the outcome, or why it plays nothing; the open level's banks declaring it; and each variation (random or switch take) " +
                    "with its container path, bank, whether streamed, the file holding it and how many copies ship. decode adds each take's length, rate and channels.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("event", "The sound event's name.", required: true),
                    McpSchema.Boolean("decode", "Decode each variation for its length and format (slower)."),
                    McpSchema.Integer("limit", "At most this many variations (default 50).")),
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
                Description = "Replace one variation's audio with a mono or stereo .wav, encoded to the original's codec, in every bank and package that ships a copy. Writes the game's sound files " +
                    "at once for every level; not undoable (only the mod baseline or verifying the game restores them). Refuses while the game runs. dry_run reports without writing.",
                InputSchema = McpSchema.Object(
                    McpSchema.String("event", "The sound event."),
                    McpSchema.Integer("variation", "The variation's index in describe_sound_event (default 0)."),
                    McpSchema.String("source_id", "The audio's source id, instead of a variation index."),
                    McpSchema.String("wav_path", "Absolute path of the .wav to use.", required: true),
                    McpSchema.Number("quality", "Vorbis quality from 0 to 1 (default 0.6; raised if the game's decoder needs it)."),
                    McpSchema.Boolean("dry_run", "Encode and report what would be written, writing nothing.")),
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

        private static string Relative(string file)
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

        private static bool GameRunning() => ProcessRunning("AI");

        private static void RefuseWhileGameRuns(string why)
        {
            if (GameRunning())
                throw new McpError("Alien: Isolation is running, and " + why + ". Close the game and try again.");
        }

        /// <summary>Write ANIMATION.PAK as the animation browser does, capturing the mod baseline first. UI thread.</summary>
        private static void WriteAnimationPak(Anim animations, string inMemory)
        {
            Modding.ModServices.CaptureBeforeWrite(animations.PAK.Filepath);
            bool saved;
            try { saved = animations.Save(); }
            catch (Exception e) { throw new McpError("ANIMATION.PAK could not be written (" + e.Message + "). " + inMemory + " in memory only, and goes out with the next ANIMATION.PAK save."); }
            if (!saved)
                throw new McpError("ANIMATION.PAK could not be written (is the game running, or the file read-only?). " + inMemory + " in memory only, and goes out with the next ANIMATION.PAK save.");
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
            List<string> near = animations.Sets.Where(o => Holds(o.Name, name)).Select(o => o.Name).OrderBy(o => o.Length).Take(10).ToList();
            throw new McpError("There is no animation set '" + name + "'" + (near.Count == 0 ? " (list_animation_sets lists them)." : ". Did you mean: " + string.Join(", ", near) + "?"));
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
            List<string> near = clips.Where(o => Holds(o.Name, name) || Holds(o.Path, name)).Select(ClipName).Distinct().Take(10).ToList();
            throw new McpError(set.Name + (context == null ? "" : " (" + context + ")") + " has no animation '" + name + "'"
                + (near.Count == 0 ? " (list_animations lists them)." : ". Did you mean: " + string.Join(", ", near) + "?"));
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
            int limit = Limit(call, 100);
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
                    if (filter.Length != 0)
                    {
                        listed = candidates.Where(o => Holds(o.Name, filter) || Holds(o.Skeleton, filter)).ToList();
                        if (listed.Count == 0)
                        {
                            listed = candidates.Where(o => o.Contexts.Any(c => c != null && (Holds(c.Name, filter)
                                || (c.Clips != null && c.Clips.Any(x => x != null && (Holds(x.Name, filter) || Holds(x.Path, filter))))))).ToList();
                            widened = listed.Count != 0;
                        }
                    }
                    if (widened)
                        call.Note("No set or rig is called '" + filter + "', so these are the sets holding an animation that matches it.");
                    if (listed.Count > limit)
                        call.Note(listed.Count + " sets match; " + limit + " are listed (raise limit or narrow filter).");
                    JObject result = new JObject()
                    {
                        ["total"] = listed.Count,
                        ["sets"] = new JArray(listed.Take(limit).Select(SetRow)),
                    };
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
            string[] words = Words(call.Str("filter"));
            int limit = Limit(call, 100);
            int offset = call.Int("offset", 0);
            if (offset < 0) throw new McpError("'offset' can't be negative.");
            if (contextName != null && setName == null)
                throw new McpError("'context' needs 'set'.");

            using (McpEditorTools.Heartbeat(call, "Reading ANIMATION.PAK"))
                return McpEditor.UI(() =>
                {
                    Anim animations = RequireAnimations();
                    IEnumerable<Anim.AnimationContext> contexts;
                    if (setName != null)
                    {
                        Anim.AnimationSet set = FindSet(animations, setName);
                        contexts = contextName == null ? set.Contexts : new List<Anim.AnimationContext>() { FindContext(set, contextName) };
                    }
                    else
                        contexts = animations.Sets.SelectMany(o => o.Contexts);

                    List<Anim.ClipReference> clips = new List<Anim.ClipReference>();
                    foreach (Anim.AnimationContext context in contexts)
                        foreach (Anim.ClipReference clip in context.Clips.Where(o => o != null).OrderBy(o => o.Name ?? "", StringComparer.OrdinalIgnoreCase))
                        {
                            if (clip == null || !AllWords(words, clip.Name, clip.Path)) continue;
                            //The section names the rigs it needs cheaply; only a clip that might be on this one is decoded to be sure
                            if (rig != null)
                            {
                                List<string> needs = clip.Section?.SkeletonDependencies;
                                if (needs != null && needs.Count != 0 && !needs.Any(o => Same(o, rig))) continue;
                                if (!Same(clip.Skeleton, rig)) continue;
                            }
                            clips.Add(clip);
                        }

                    if (clips.Count > offset + limit)
                        call.Note(clips.Count + " animations match; " + limit + " from " + offset + " are listed (use offset for more, or narrow the search).");
                    return new JObject()
                    {
                        ["total"] = clips.Count,
                        ["offset"] = offset,
                        ["animations"] = new JArray(clips.Skip(offset).Take(limit).Select(ClipRow)),
                    };
                });
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

        private static Models.CS2 FindModel(Level level, string name)
        {
            name = (name ?? "").Trim();
            List<Models.CS2> named = level.Models.Entries.Where(o => o != null && (Same(o.Name, name) || Same(ModelLeaf(o.Name), ModelLeaf(name)))).ToList();
            if (named.Count == 0)
                throw new McpError("The open level has no model '" + name + "' (list_models shows them).");
            Models.CS2 exact = named.FirstOrDefault(o => Same(o.Name, name));
            if (exact != null) return exact;
            if (named.Count > 1)
                throw new McpError("'" + name + "' could be: " + string.Join("; ", named.Take(10).Select(o => o.Name)) + ". Give the full name.");
            return named[0];
        }

        private static string ModelLeaf(string name)
        {
            string leaf = (name ?? "").Replace('/', '\\');
            if (leaf.EndsWith(".CS2", StringComparison.OrdinalIgnoreCase)) leaf = leaf.Substring(0, leaf.Length - 4);
            int at = leaf.LastIndexOf('\\');
            return at >= 0 ? leaf.Substring(at + 1) : leaf;
        }

        private static object ListSkeletons(McpCall call)
        {
            string filter = (call.Str("filter") ?? "").Trim();
            string setName = call.Str("set");
            string modelName = call.Str("model");
            int limit = Limit(call, 100);
            if (setName != null && modelName != null)
                throw new McpError("Give set or model, not both.");

            using (McpEditorTools.Heartbeat(call, "Reading ANIMATION.PAK"))
                return McpEditor.UI(() =>
                {
                    Anim animations = RequireAnimations();
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
                            Skeleton skeleton = animations.GetSkeleton(entry)?.Skeleton;
                            if (skeleton == null || !skeleton.Loaded) continue;
                            rigs.Add(new RigCandidate() { Name = entry.Name, Skeleton = skeleton });
                        }
                        if (modelName != null)
                        {
                            //As the model exporter's picker ranks them: big enough first, then scored, then by fit
                            Models.CS2 model = FindModel(McpEditor.RequireLevel(forEditing: false).Level, modelName);
                            int required = Skeleton.RequiredBoneCount(model);
                            foreach (RigCandidate rig in rigs)
                            {
                                rig.FitsBoneCount = rig.Skeleton.Bones.Count >= required;
                                rig.Fit = rig.Skeleton.ScoreFit(model);
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

                    List<RigCandidate> listed = rigs.Where(o => filter.Length == 0 || Holds(o.Name, filter)).ToList();
                    result["total"] = listed.Count;
                    result["skeletons"] = new JArray(listed.Take(limit).Select(rig =>
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
                    }));
                    if (listed.Count > limit)
                        call.Note(listed.Count + " rigs match; " + limit + " are listed.");
                    return result;
                });
        }
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

                    Skeleton skeleton;
                    List<RigCandidate> candidates = RigsForClips(animations, set, clips);
                    if (rigName != null)
                    {
                        RigCandidate chosen = candidates.FirstOrDefault(o => Same(o.Name, rigName));
                        skeleton = chosen?.Skeleton ?? RigSkeleton(animations, rigName);
                        if (skeleton == null)
                            throw new McpError("There is no rig '" + rigName + "' in ANIMATION.PAK (list_skeletons with set=" + set.Name + " ranks the ones to use).");
                        if (chosen != null && !chosen.BigEnough)
                            call.Note("'" + skeleton.Name + "' has fewer bones than these animations drive, so some tracks have nowhere to go.");
                    }
                    else
                    {
                        if (candidates.Count == 0) throw new McpError("ANIMATION.PAK holds no rigs to export against.");
                        skeleton = candidates[0].Skeleton;
                        if (candidates[0].Authored == 0)
                            call.Note("None of these animations name a rig this PAK holds, so '" + skeleton.Name + "' was used; pass rig to choose another.");
                    }

                    //Measured per bone per frame: COLLADA spends about 340 bytes formatting each as text, FBX and glTF about 40
                    long estimate = (long)clips.Count * skeleton.Bones.Count * clips.Max(o => o.Animation.FrameCount) * (extension == ".dae" ? 340 : 40);
                    if (estimate >= 250L * 1024 * 1024 && !allowLarge)
                        throw new McpError("Writing " + clips.Count + " animations for a " + skeleton.Bones.Count + " bone rig would produce about " + (estimate / (1024 * 1024))
                            + " MB and take a while. Export fewer (animations or context), use .fbx or .glb rather than .dae, or pass allow_large:true.");

                    Models.CS2 model = null;
                    Level level = null;
                    if (modelName != null)
                    {
                        level = McpEditor.RequireLevel(forEditing: false).Level;
                        model = FindModel(level, modelName);
                    }

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
            double rate = call.Num("frame_rate", 0);
            if (rate < 0 || rate > 240)
                throw new McpError("'frame_rate' must be between 1 and 240 (or 0 to take it from the file).");
            int index = call.Int("clip_index", 0);
            if (index < 0) throw new McpError("'clip_index' counts from 0.");
            bool additive = call.Bool("additive");
            bool dryRun = call.Bool("dry_run");

            Anim animations = null;
            Anim.AnimationSet set = null;
            Skeleton rig = null;
            string rigName = null, name = null, storedPath = null;
            List<string> clashes = new List<string>();
            using (McpEditorTools.Heartbeat(call, "Reading ANIMATION.PAK"))
                McpEditor.UI(() =>
                {
                    animations = RequireAnimations();
                    set = FindSet(animations, call.Str("set", required: true));
                    rigName = (call.Str("rig") ?? AnimationImport.DefaultRigFor(set)).Trim();
                    if (rigName.Length == 0)
                        throw new McpError(set.Name + " has no rig of its own; pass rig (list_skeletons lists them).");
                    rig = animations.GetSkeleton(rigName)?.Skeleton;
                    if (rig == null || rig.Bones.Count == 0)
                        throw new McpError("There is no rig '" + rigName + "' in ANIMATION.PAK (list_skeletons lists them).");
                    //Found whatever the case, but written as a case-sensitive hash: the clip has to name the rig exactly as the index does
                    rigName = animations.SkeletonIndex?.GetSkeleton(rigName)?.Name ?? rigName;
                    name =(call.Str("name") ?? AnimationImport.Sanitise(Path.GetFileNameWithoutExtension(file)).ToLowerInvariant()).Trim();
                    storedPath = (call.Str("stored_path") ?? AnimationImport.PathFor(set, file)).Trim().Replace('/', '\\');
                    if (name.Length == 0) throw new McpError("'name' can't be empty.");
                    if (storedPath.Length == 0) throw new McpError("'stored_path' can't be empty.");
                    //The checks AnimationImport.Add makes, so a dry run can say so too
                    if (set.Contexts.SelectMany(o => o.Clips).Any(o => Same(o.Name, name)))
                        clashes.Add("'" + name + "' is already the name of an animation in " + set.Name + "; pass another name.");
                    if (animations.GetSection(storedPath, out int _) != null)
                        clashes.Add("Something is already stored at '" + storedPath + "'; pass another stored_path.");
                });

            AnimationImport.Options options = new AnimationImport.Options() { Rig = rigName, Root = root, FrameRate = (float)rate, Additive = additive, Index = index };
            AnimationImport.Reading reading;
            using (McpEditorTools.Heartbeat(call, "Reading " + Path.GetFileName(file)))
            {
                reading = ReadAnimationFile(file, rig, options);
                //A file on another skeleton has no other way in, so it is converted when nothing matched - as the import window decides
                if (!reading.Ok && reading.Matched == 0 && reading.CanRetarget)
                {
                    options.Retarget = true;
                    reading = ReadAnimationFile(file, rig, options);
                }
            }
            if (!reading.Ok)
                throw new McpError("That can't be imported onto " + rigName + ": " + (reading.Problem ?? "nothing was read from the file.").Replace("\r\n", " "));

            JObject result = new JObject()
            {
                ["set"] = set.Name,
                ["name"] = name,
                ["stored_path"] = storedPath,
                ["rig"] = rigName,
                ["frames"] = reading.Frames,
                ["duration"] = Round(reading.Duration, 3),
                ["fps"] = reading.FrameDuration > 0 ? Round(1.0 / reading.FrameDuration, 2) : 0,
            };
            if (reading.FileFrameRate > 0) result["file_fps"] = Round(reading.FileFrameRate, 2);
            if (reading.Retargeted)
            {
                result["retargeted"] = true;
                result["mirrored"] = reading.Mirrored;
                result["bones_driven"] = reading.Matched;
                result["rig_bones"] = rig.Bones.Count;
            }
            else
            {
                result["matched_nodes"] = reading.Matched;
                result["animated_nodes"] = reading.Channels;
            }
            result["scale"] = Round(reading.Scale, 3);
            result["root_moves"] = reading.RootAnimated;
            if (additive) result["additive"] = true;
            if (reading.Warnings.Count != 0) result["warnings"] = new JArray(reading.Warnings.Select(o => o.Replace("\r\n", " ")));
            if (!Same(rigName, set.Skeleton) && !string.IsNullOrEmpty(set.Skeleton))
                call.Note("Built against " + rigName + " rather than " + set.Skeleton + ", so the game retargets it onto " + set.Skeleton + " as it plays, as most of " + set.Name + "'s animations do.");

            if (dryRun)
            {
                /* The clip is built as the import would build it, into nothing - what the import window's preview
                 * does - so an encoder that refuses this PAK's sections (or this clip) says so now, not on import */
                using (McpEditorTools.Heartbeat(call, "Building the clip"))
                    McpEditor.UI(() =>
                    {
                        try
                        {
                            if (AnimationImport.BuildPreview(animations, set, reading, name, storedPath, options)?.Section == null)
                                clashes.Add("The clip could not be built.");
                        }
                        catch (Exception e) { clashes.Add("The clip could not be built: " + e.Message); }
                    });
                result["dry_run"] = true;
                if (clashes.Count != 0) result["would_fail"] = new JArray(clashes);
                return result;
            }
            if (clashes.Count != 0)
                throw new McpError(string.Join(" ", clashes));
            RefuseWhileGameRuns("ANIMATION.PAK can't be written while it holds it open");

            using (McpEditorTools.Heartbeat(call, "Writing ANIMATION.PAK"))
                McpEditor.UI(() =>
                {
                    if (!AnimationImport.Add(animations, set, reading, name, storedPath, options, out string problem))
                        throw new McpError(problem);
                    //The pick lists are built once at startup, so they are told about the new clip
                    Singleton.RegisterAnimation(set.Name, name);
                    Singleton.OnAnimationsModified?.Invoke();
                    WriteAnimationPak(animations, "'" + name + "' was added to " + set.Name);
                });
            result["imported"] = true;
            call.Note("ANIMATION.PAK has been written. ANIMATION parameters on " + set.Name + " characters can now name '" + name + "'; an open Animation Editor window shows it once reopened.");
            return result;
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
            List<string> near = all.Where(o => Holds(o.ToString(), name)).Select(o => o.ToString()).Take(10).ToList();
            throw new McpError("There is no blend set '" + name + "'" + (near.Count == 0 ? " (list_blend_sets lists them)." : ". Did you mean: " + string.Join(", ", near) + "?"));
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
            int limit = Limit(call, 100);
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
                    List<GlobalAnimClipDB.BlendSet> listed = AllBlendSets(animations)
                        .Where(o => filter.Length == 0 || Holds(o.ToString(), filter) || o.Clips.Any(c => Holds(c.Name, filter)))
                        .OrderBy(o => o.ToString(), StringComparer.OrdinalIgnoreCase).ToList();
                    if (listed.Count > limit) call.Note(listed.Count + " blend sets match; " + limit + " are listed.");
                    return new JObject()
                    {
                        ["total"] = listed.Count,
                        ["blend_sets"] = new JArray(listed.Take(limit).Select(o =>
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
                        })),
                    };
                });
        }

        private static AnimClipDB FindCharacter(Anim animations, string character)
        {
            character = (character ?? "").Trim();
            AnimClipDB database = animations.ClipDatabases.FirstOrDefault(o => Same(o.Character, character));
            if (database != null) return database;
            List<string> near = animations.ClipDatabases.Where(o => Holds(o.Character, character)).Select(o => o.Character).Take(10).ToList();
            throw new McpError("There is no character (animation set) '" + character + "'" + (near.Count == 0 ? "." : ". Did you mean: " + string.Join(", ", near) + "?"));
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
                RefuseWhileGameRuns("ANIMATION.PAK can't be written while it holds it open");

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
                    WriteAnimationPak(animations, "The blend set change is");
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
            List<string> near = animations.Trees.Select(TreeSetName).Where(o => Holds(o, name)).Take(10).ToList();
            throw new McpError("There is no animation tree set '" + name + "'" + (near.Count == 0 ? " (list_anim_trees lists them)." : ". Did you mean: " + string.Join(", ", near) + "?"));
        }

        private static AnimationTree FindTree(AnimTreeDB database, string name)
        {
            name = (name ?? "").Trim();
            AnimationTree tree = database.Entries.FirstOrDefault(o => o.Name == name) ?? database.Entries.FirstOrDefault(o => Same(o.Name, name));
            if (tree != null) return tree;
            List<string> near = database.Entries.Where(o => Holds(o.Name, name)).Select(o => o.Name).Take(10).ToList();
            throw new McpError(TreeSetName(database) + " has no tree '" + name + "'" + (near.Count == 0 ? " (list_anim_trees with set lists them)." : ". Did you mean: " + string.Join(", ", near) + "?"));
        }

        private static object ListAnimTrees(McpCall call)
        {
            string setName = call.Str("set");
            string[] words = Words(call.Str("filter"));
            int limit = Limit(call, 200);
            using (McpEditorTools.Heartbeat(call, "Reading ANIMATION.PAK"))
                return McpEditor.UI(() =>
                {
                    Anim animations = RequireAnimations();
                    if (setName != null)
                    {
                        AnimTreeDB database = FindTreeSet(animations, setName);
                        List<AnimationTree> trees = database.Entries.Where(o => AllWords(words, o.Name)).OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase).ToList();
                        if (trees.Count > limit) call.Note(trees.Count + " trees match; " + limit + " are listed.");
                        return new JObject()
                        {
                            ["set"] = TreeSetName(database),
                            ["total"] = trees.Count,
                            ["trees"] = new JArray(trees.Take(limit).Select(o => new JObject() { ["name"] = o.Name, ["nodes"] = o.Nodes.Count })),
                        };
                    }
                    if (words.Length != 0)
                    {
                        List<JObject> found = new List<JObject>();
                        foreach (AnimTreeDB database in animations.Trees.OrderBy(TreeSetName, StringComparer.OrdinalIgnoreCase))
                            foreach (AnimationTree tree in database.Entries.OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase))
                                if (AllWords(words, tree.Name)) found.Add(new JObject() { ["set"] = TreeSetName(database), ["tree"] = tree.Name, ["nodes"] = tree.Nodes.Count });
                        if (found.Count > limit) call.Note(found.Count + " trees match; " + limit + " are listed.");
                        return new JObject() { ["total"] = found.Count, ["trees"] = new JArray(found.Take(limit)) };
                    }
                    List<AnimTreeDB> sets = animations.Trees.OrderBy(TreeSetName, StringComparer.OrdinalIgnoreCase).ToList();
                    return new JObject()
                    {
                        ["total"] = sets.Count,
                        ["sets"] = new JArray(sets.Take(limit).Select(o => new JObject() { ["set"] = TreeSetName(o), ["trees"] = o.Entries.Count })),
                    };
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
            int limit = Limit(call, 150, 2000);
            int offset = call.Int("offset", 0);
            if (offset < 0) throw new McpError("'offset' can't be negative.");
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
                    if (nodes.Count > offset + limit)
                        call.Note(nodes.Count + " nodes match; " + limit + " from " + offset + " are listed (use offset for more, or filter).");
                    JObject result = new JObject()
                    {
                        ["set"] = TreeSetName(database),
                        ["tree"] = tree.Name,
                        ["settings"] = settings,
                        ["links"] = treeLinks,
                        ["node_count"] = tree.Nodes.Count,
                        ["nodes"] = new JArray(nodes.Skip(offset).Take(limit).Select(o => NodeJson(o, shared, fields))),
                    };
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
                List<string> near = tree.Nodes.Where(o => Holds(o.Name, name)).Select(o => o.Name).Distinct().Take(10).ToList();
                throw new McpError("The tree has no node '" + name + "'" + (type == null ? "" : " of type " + type) + (near.Count == 0 ? " (get_anim_tree lists them)." : ". Did you mean: " + string.Join(", ", near) + "?"));
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
            if (flow) RefuseLoop(node, target);
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

        private static readonly string[] TreeOpKeys = { "op", "node", "node_type", "field", "value", "to", "to_type", "type", "name" };

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

        private static object EditAnimTree(McpCall call)
        {
            string setName = call.Str("set", required: true);
            string treeName = call.Str("tree", required: true);
            JArray ops = call.Array("ops", required: true);
            bool dryRun = call.Bool("dry_run");
            bool closeGame = call.Bool("close_game");
            if (ops.Count == 0) throw new McpError("'ops' is empty.");

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
                    try { copy.ToBytes(); }
                    catch (Exception e) { throw new McpError("After those ops the tree could not be written (" + e.Message + "); nothing was changed."); }
                    unsavedAfter = UnsavedNodes(copyTree);
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
                    throw new McpError("Alien: Isolation is running, and ANIMATION.PAK can't be written while it holds it open. Close it, or pass close_game:true to close it as the editor's Save does.");
                EditorUtils.CloseAI();
            }

            using (McpEditorTools.Heartbeat(call, "Writing ANIMATION.PAK"))
                McpEditor.UI(() =>
                {
                    Anim animations = RequireAnimations();
                    AnimTreeDB database = FindTreeSet(animations, setName);
                    AnimationTree tree = FindTree(database, treeName);
                    try { ApplyTreeOps(name => animations.AddName(name, true), tree, ops); }
                    catch (McpError e)
                    {
                        //The copy took every op, so this is the live tree having moved on since: say what state it is in
                        throw new McpError("The ops checked out on a copy but failed on the tree itself (" + e.Message + "). Earlier ops may be applied in memory; nothing was written. Reopen the tree with get_anim_tree before trying again.");
                    }

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
                    Modding.ModServices.CaptureBeforeWrite(animations.PAK.Filepath);
                    if (!animations.PAK.Save())
                        throw new McpError("ANIMATION.PAK could not be written (is the game running, or the file read-only?). The edit is in memory only, and goes out with the next ANIMATION.PAK save.");
                    Singleton.OnAnimationsModified?.Invoke();
                    if (Application.OpenForms.OfType<AnimTreeEditor>().Any())
                        call.Note("The Animation Tree Editor is open: reopen the tree there to see this change (its own Save writes whatever it shows).");
                });
            result["written"] = true;
            return result;
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
            List<string> near = files.Select(BehaviourName).Where(o => Holds(o, name)).Take(10).ToList();
            throw new McpError("There is no behaviour tree '" + name + "'" + (near.Count == 0 ? " (get_behaviour_tree without a name lists them)." : ". Did you mean: " + string.Join(", ", near) + "?"));
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

        private static object GetBehaviourTree(McpCall call)
        {
            string name = call.Str("name");
            string filter = (call.Str("filter") ?? "").Trim();
            string xpath = call.Str("xpath");
            int maxChars = call.Int("max_chars", 60000);
            if (maxChars < 100) throw new McpError("'max_chars' must be at least 100.");
            int limit = Limit(call, 200);
            XmlDocument document = ReadBehaviours(out BML _);
            List<XmlElement> files = BehaviourFiles(document);

            if (name == null)
            {
                if (xpath != null) throw new McpError("'xpath' needs 'name'.");
                List<XmlElement> listed = files.Where(o => filter.Length == 0 || Holds(BehaviourName(o), filter)).OrderBy(BehaviourName, StringComparer.OrdinalIgnoreCase).ToList();
                if (listed.Count > limit) call.Note(listed.Count + " trees match; " + limit + " are listed.");
                return new JObject()
                {
                    ["file"] = Relative(BehaviourFile),
                    ["total"] = listed.Count,
                    ["trees"] = new JArray(listed.Take(limit).Select(o => new JObject()
                    {
                        ["name"] = BehaviourName(o),
                        ["nodes"] = o.SelectNodes(".//Node").Count,
                        ["references"] = new JArray(References(o)),
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
            };
            string xml;
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
            if (dryRun)
            {
                result["dry_run"] = true;
                return result;
            }

            bool editorRunning = ProcessRunning("BehaviourTreeEditor");
            if (GameRunning() || editorRunning)
            {
                if (!closeGame)
                    throw new McpError((editorRunning ? "The Behaviour Tree Editor is running (it rewrites this file when it saves)" : "Alien: Isolation is running")
                        + ". Close it, or pass close_game:true to close the game and that editor first.");
                EditorUtils.CloseAI(new List<string>() { "BehaviourTreeEditor" });
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
        private static WwiseSoundLibrary SoundLibrary(McpCall call)
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

        private static string CodecName(ushort format)
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
            int limit = Limit(call, 200, 5000);

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
                    {
                        List<string> near = eventsByBank.Keys.Where(o => Holds(o, bankName)).Take(10).ToList();
                        throw new McpError("The open level declares no bank '" + bankName + "'" + (near.Count == 0 ? "." : ". Did you mean: " + string.Join(", ", near) + "?"));
                    }
                    List<string> names = eventsByBank[key].Distinct().OrderBy(o => o, StringComparer.OrdinalIgnoreCase).ToList();
                    if (names.Count > limit) call.Note(names.Count + " events; the first " + limit + " are listed.");
                    JObject one = new JObject() { ["name"] = key };
                    if (declared.TryGetValue(key, out SoundBankData.SoundBank info)) one["localised"] = info.Localised;
                    one["file"] = onDisk.TryGetValue(key, out string file) ? file : null;
                    one["event_count"] = names.Count;
                    one["events"] = new JArray(names.Take(limit));
                    return one;
                }

                List<string> listed = eventsByBank.Keys.Where(o => filter.Length == 0 || Holds(o, filter)).OrderBy(o => o, StringComparer.OrdinalIgnoreCase).ToList();
                if (listed.Count > limit) call.Note(listed.Count + " banks match; " + limit + " are listed.");
                return new JObject()
                {
                    ["level"] = content.Level.Name,
                    ["total"] = listed.Count,
                    ["banks"] = new JArray(listed.Take(limit).Select(o =>
                    {
                        JObject row = new JObject() { ["name"] = o, ["events"] = eventsByBank[o].Distinct().Count() };
                        if (declared.TryGetValue(o, out SoundBankData.SoundBank info) && info.Localised) row["localised"] = true;
                        row["file"] = onDisk.TryGetValue(o, out string file) ? file : null;
                        return row;
                    })),
                };
            });
        }

        private static object DescribeSoundEvent(McpCall call)
        {
            string eventName = call.Str("event", required: true).Trim();
            bool decode = call.Bool("decode");
            int limit = Limit(call, 50, 500);

            JArray banks = null;
            string levelName = null;
            McpEditor.UI(() =>
            {
                LevelContent content = Singleton.Editor?.CompositeBrowser?.Content;
                if (content?.Level == null || !content.IsLevelDataLoaded) return;
                levelName = content.Level.Name;
                banks = new JArray(SoundEventMetadata.BanksFor(eventName));
            });

            WwiseSoundLibrary library = SoundLibrary(call);
            WwiseEventResolution resolution = library.Resolve(eventName);
            JObject result = new JObject()
            {
                ["event"] = eventName,
                ["outcome"] = resolution.Outcome.ToString(),
                ["plays_audio"] = resolution.HasAudio,
            };
            if (!resolution.HasAudio) result["explanation"] = resolution.Explanation;
            if (resolution.Actions.Count != 0) result["actions"] = new JArray(resolution.Actions);
            if (banks == null)
                call.Note("No level is open, so which banks declare this event isn't known; load_level first to see them.");
            else
            {
                result["banks"] = banks;
                if (banks.Count == 0)
                    call.Note(levelName + "'s sound data declares no bank holding '" + eventName + "', so it won't play there (list_enum_string_values type SOUND_EVENT lists the events it has).");
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
            bool dryRun = call.Bool("dry_run");
            WwiseSoundLibrary library = SoundLibrary(call);
            WwiseSoundVariation variation = PickVariation(call, library, out string eventName);
            if (variation.Media == null) throw new McpError("That variation has no audio of its own to replace.");

            //The same audio is often shipped in several banks at once: all of them change, or the old sound comes back wherever another copy loads
            IList<WwiseMediaLocation> copies = library.AllCopies(variation.SourceId);
            SoundImport.Reading reading;
            using (McpEditorTools.Heartbeat(call, "Encoding " + Path.GetFileName(wav)))
                reading = SoundImport.Read(wav, variation.Media, copies, new SoundImport.Options() { Quality = (float)quality });
            if (!reading.Ok)
                throw new McpError("That audio can't be used: " + reading.Problem);

            JObject result = new JObject() { ["source_id"] = variation.SourceId.ToString() };
            if (eventName != null) result["event"] = eventName;
            result["codec"] = reading.Codec;
            if (reading.Codec == "Wwise Vorbis") result["quality"] = Round(reading.Quality, 2);
            result["channels"] = reading.Channels;
            result["sample_rate"] = reading.SampleRate;
            result["duration"] = Round(reading.Duration, 3);
            result["original_bytes"] = reading.OriginalBytes;
            result["new_bytes"] = reading.NewBytes;
            result["container"] = reading.Plan?.Kind;
            result["copies"] = new JArray(reading.Copies.Select(o => Relative(o.File)).Distinct(StringComparer.OrdinalIgnoreCase));
            if (reading.Notes.Count != 0) result["notes"] = new JArray(reading.Notes);
            if (dryRun)
            {
                result["dry_run"] = true;
                return result;
            }

            RefuseWhileGameRuns("its sound files can't be written while it holds them open");
            //Pristine copies of every container this write touches, while they are still pristine
            Modding.ModServices.CaptureBeforeWrite(variation.Media.File);
            foreach (WwiseMediaLocation copy in reading.Copies)
                Modding.ModServices.CaptureBeforeWrite(copy.File);
            string problem;
            bool replaced;
            using (McpEditorTools.Heartbeat(call, "Writing the sound files"))
                replaced = SoundImport.Apply(reading, variation.Media, out problem);
            //A rewritten bank moves the other audio in it, so the index is rebuilt on the next ask
            SoundPreviewLibrary.Invalidate();
            if (!replaced)
                throw new McpError("The sound was not fully replaced: " + (problem ?? "").Replace(Environment.NewLine, " "));
            result["replaced"] = reading.Copies.Count;
            return result;
        }
        #endregion
    }

    /// <summary>Checks the script tools can use on the global name lists this file's tools browse.</summary>
    internal static class McpGlobalAssetChecks
    {
        /// <summary>
        /// A warning when an ANIMATION, ANIMATION_SET or SOUND_EVENT value names nothing the game has, or null
        /// when it is known or the type is none of those. Cheap: it reads the name lists built at startup and the
        /// open level's sound data. Call on the UI thread.
        /// </summary>
        public static string UnknownValueWarning(EnumStringType type, string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            switch (type)
            {
                case EnumStringType.ANIMATION_SET:
                    if (Singleton.AllAnimations.Count == 0 || Singleton.AllAnimations.Keys.Any(o => string.Equals(o, value, StringComparison.OrdinalIgnoreCase))) return null;
                    return "No animation set is called '" + value + "' (list_animation_sets lists them).";
                case EnumStringType.ANIMATION:
                    if (Singleton.AllAnimations.Count == 0 || Singleton.AllAnimations.Values.Any(o => o.Contains(value) || o.Any(n => string.Equals(n, value, StringComparison.OrdinalIgnoreCase)))) return null;
                    return "No animation set has an animation called '" + value + "' (list_animations lists them).";
                case EnumStringType.SOUND_EVENT:
                    if (Singleton.Editor?.CompositeBrowser?.Content?.Level?.SoundEventData == null || SoundEventMetadata.BanksFor(value).Count != 0) return null;
                    return "The open level's sound data declares no sound event '" + value + "', so it won't play (list_enum_string_values type SOUND_EVENT lists them).";
                default:
                    return null;
            }
        }
    }
}
